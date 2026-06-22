using System.Net.Http;
using System.Net.WebSockets;
using AVFoundation;
using CoreAnimation;
using CoreFoundation;
using CoreGraphics;
using CoreMedia;
using Foundation;
using UIKit;

namespace RynthRemote.Platforms.iOS;

/// <summary>
/// Native iOS HD video: opens ws://&lt;agent&gt;/video?pid=N (works remotely over Tailscale, no WebRTC/ICE),
/// parses the raw H.264 Annex-B byte stream into access units, and feeds them to an
/// AVSampleBufferDisplayLayer via VideoToolbox (no profile/level cap → true 60fps). Decode errors are
/// POSTed to the agent's /webrtc/clientlog so they're readable PC-side (no Xcode needed).
/// </summary>
public sealed class H264StreamView : UIView
{
    private readonly AVSampleBufferDisplayLayer _layer = new();
    private ClientWebSocket? _ws;
    private CancellationTokenSource? _cts;
    private CMVideoFormatDescription? _format;
    private byte[]? _sps, _pps;
    private string _baseUrl = "", _token = "";
    private int _pid;
    private bool _loggedFirstFrame;
    private bool _awaitingKeyframe;   // after a drop-to-live flush, skip frames until the next IDR (clean restart)

    public H264StreamView()
    {
        BackgroundColor = UIColor.Black;
        _layer.VideoGravity = "AVLayerVideoGravityResizeAspect";   // NSString constant; the property is a string
        Layer.AddSublayer(_layer);
    }

    public override void LayoutSubviews()
    {
        base.LayoutSubviews();
        _layer.Frame = Bounds;
    }

    public void Start(string baseUrl, int pid, string token)
    {
        _baseUrl = baseUrl ?? ""; _pid = pid; _token = token ?? "";
        _cts = new CancellationTokenSource();
        _ = RunAsync(_cts.Token);
    }

    public void Stop()
    {
        try { _cts?.Cancel(); } catch { }
        try { _ws?.Abort(); } catch { }
        _ws = null;
    }

    private async Task RunAsync(CancellationToken ct)
    {
        try
        {
            string scheme = _baseUrl.StartsWith("https", StringComparison.OrdinalIgnoreCase) ? "wss" : "ws";
            // Normalise "host:8740" / "http://host:8740/status" -> "ws://host:8740/video?pid=N[&token=]".
            var host = _baseUrl.Replace("https://", "").Replace("http://", "").TrimEnd('/');
            int slash = host.IndexOf('/');
            if (slash >= 0) host = host.Substring(0, slash);
            string url = $"{scheme}://{host}/video?pid={_pid}";
            if (!string.IsNullOrWhiteSpace(_token)) url += "&token=" + Uri.EscapeDataString(_token);

            _ws = new ClientWebSocket();
            await _ws.ConnectAsync(new Uri(url), ct).ConfigureAwait(false);
            await PostLog("ws connected").ConfigureAwait(false);

            var stream = new List<byte>(1 << 18);
            var buf = new byte[65536];
            while (!ct.IsCancellationRequested && _ws.State == WebSocketState.Open)
            {
                var seg = new ArraySegment<byte>(buf);
                WebSocketReceiveResult r;
                try { r = await _ws.ReceiveAsync(seg, ct).ConfigureAwait(false); }
                catch { break; }
                if (r.MessageType == WebSocketMessageType.Close) break;
                for (int i = 0; i < r.Count; i++) stream.Add(buf[i]);
                ParseAndDecode(stream);
            }
        }
        catch (OperationCanceledException) { }
        catch (Exception ex) { await PostLog("ws error: " + ex.Message).ConfigureAwait(false); }
    }

    // Pull complete NALs out of the Annex-B byte buffer (keep the trailing incomplete one), grouping into
    // access units by the AUD (type 9) delimiter; decode each AU.
    private readonly List<byte[]> _auNals = new();
    private void ParseAndDecode(List<byte> stream)
    {
        var starts = FindStartCodes(stream);
        if (starts.Count < 2) return;
        int last = starts.Count - 1;
        for (int k = 0; k < last; k++)
        {
            int from = starts[k], to = starts[k + 1];
            int nalStart = SkipStartCode(stream, from, to);
            int len = to - nalStart;
            while (len > 0 && stream[nalStart + len - 1] == 0) len--;   // strip next start code's leading zeros
            if (len <= 0) continue;
            int type = stream[nalStart] & 0x1F;
            if (type == 9) { FlushAu(); continue; }                      // AUD ends the previous AU
            var nal = new byte[len];
            for (int i = 0; i < len; i++) nal[i] = stream[nalStart + i];
            if (type == 7) _sps = nal;
            else if (type == 8) _pps = nal;
            else _auNals.Add(nal);                                       // VCL / SEI etc.
        }
        stream.RemoveRange(0, starts[last]);
    }

    private void FlushAu()
    {
        if (_auNals.Count == 0) return;
        var nals = _auNals.ToArray();
        _auNals.Clear();
        try { Decode(nals); }
        catch (Exception ex) { _ = PostLog("decode err: " + ex.Message); }
    }

    private void Decode(byte[][] nals)
    {
        if (_format == null)
        {
            if (_sps == null || _pps == null) return;                    // wait for parameter sets
            _format = CMVideoFormatDescription.FromH264ParameterSets(new List<byte[]> { _sps, _pps }, 4, out var fe);
            if (fe != CMFormatDescriptionError.None || _format == null) { _ = PostLog("format err: " + fe); return; }
        }

        // AVCC: concat [4-byte big-endian NAL length][NAL] for each VCL NAL in the AU.
        int total = 0;
        foreach (var n in nals) total += 4 + n.Length;
        var avcc = new byte[total];
        int p = 0;
        foreach (var n in nals)
        {
            int l = n.Length;
            avcc[p] = (byte)(l >> 24); avcc[p + 1] = (byte)(l >> 16); avcc[p + 2] = (byte)(l >> 8); avcc[p + 3] = (byte)l;
            Buffer.BlockCopy(n, 0, avcc, p + 4, l);
            p += 4 + l;
        }

        using var block = CMBlockBuffer.FromMemoryBlock(avcc, 0, CMBlockBufferFlags.AssureMemoryNow, out var be);
        if (be != CMBlockBufferError.None || block == null) { _ = PostLog("block err: " + be); return; }
        var sample = CMSampleBuffer.CreateReady(block, _format, 1, null, new nuint[] { (nuint)avcc.Length }, out var se);
        if (se != CMSampleBufferError.None || sample == null) { _ = PostLog("sample err: " + se); return; }

        // Display immediately (no presentation timestamps — lowest latency).
        var att = sample.GetSampleAttachments(true);
        if (att.Length > 0) att[0].DisplayImmediately = true;

        bool hasIdr = false;                                  // does this access unit carry a keyframe?
        foreach (var n in nals) if ((n[0] & 0x1F) == 5) { hasIdr = true; break; }

        DispatchQueue.MainQueue.DispatchAsync(() =>
        {
            // Drop-to-live: never let the display layer accumulate a backlog. AVSampleBufferDisplayLayer
            // plays its queue in order, so a network burst (TCP/Tailscale delivering several frames at once)
            // piles up and the lag never recovers — control drifts to >1s after a few minutes. When the layer
            // reports it's backed up (ReadyForMoreMediaData == false) or the decode failed, flush the stale
            // frames and resync at the next keyframe so we always present the freshest image. The encoder's
            // short GOP keeps the post-flush gap tiny. (Can't just drop P-frames — they'd corrupt until the
            // next IDR — so we wait for one.)
            if (_layer.Status == AVQueuedSampleBufferRenderingStatus.Failed || !_layer.ReadyForMoreMediaData)
            {
                _layer.Flush();
                _awaitingKeyframe = true;
            }
            if (_awaitingKeyframe && !hasIdr) { sample.Dispose(); return; }   // skip until a clean restart point
            _awaitingKeyframe = false;
            _layer.Enqueue(sample);
            if (!_loggedFirstFrame) { _loggedFirstFrame = true; _ = PostLog("first frame enqueued"); }
            sample.Dispose();
        });
    }

    private async Task PostLog(string msg)
    {
        try
        {
            var host = _baseUrl.Replace("https://", "").Replace("http://", "").TrimEnd('/');
            int slash = host.IndexOf('/'); if (slash >= 0) host = host.Substring(0, slash);
            string url = $"http://{host}/webrtc/clientlog?pid={_pid}";
            if (!string.IsNullOrWhiteSpace(_token)) url += "&token=" + Uri.EscapeDataString(_token);
            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
            await http.PostAsync(url, new StringContent("[ios] " + msg)).ConfigureAwait(false);
        }
        catch { }
    }

    private static int SkipStartCode(List<byte> a, int from, int to)
    {
        int q = from;
        while (q + 2 < to && !(a[q] == 0 && a[q + 1] == 0 && a[q + 2] == 1)) q++;
        return Math.Min(q + 3, to);
    }

    private static List<int> FindStartCodes(List<byte> a)
    {
        var idx = new List<int>();
        for (int i = 0; i + 2 < a.Count; i++)
            if (a[i] == 0 && a[i + 1] == 0 && a[i + 2] == 1) { idx.Add(i > 0 && a[i - 1] == 0 ? i - 1 : i); i += 2; }
        return idx;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) Stop();
        base.Dispose(disposing);
    }
}
