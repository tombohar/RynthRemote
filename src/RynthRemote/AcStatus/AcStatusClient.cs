using System.Net.Http.Headers;
using System.Text.Json;

namespace RynthRemote.AcStatus;

/// Result of one status fetch: either a parsed payload (Ok) or a human-readable
/// reason it failed (so the UI can show "can't reach the agent" without throwing).
public sealed record AcStatusResult(bool Ok, AcStatusPayload? Payload, string? Error)
{
    public static AcStatusResult Success(AcStatusPayload p) => new(true, p, null);
    public static AcStatusResult Fail(string error) => new(false, null, error);
}

/// Result of a control command POST.
public sealed record AcCommandResult(bool Ok, string? Error)
{
    public static AcCommandResult Success() => new(true, null);
    public static AcCommandResult Fail(string error) => new(false, error);
}

public interface IAcStatusClient
{
    /// Fetches the StatusAgent feed from the configured URL. Never throws for
    /// network/parse problems — returns a Fail result with a message instead.
    /// Only an honoured cancellation (page disposed) propagates.
    Task<AcStatusResult> GetStatusAsync(string? baseUrl, string? token, CancellationToken ct = default);

    /// POSTs a control command (toggle/profile/utility) to the agent for one client.
    /// Never throws for network problems — returns a Fail result instead.
    Task<AcCommandResult> PostCommandAsync(string? baseUrl, string? token, int pid, string action, string value, CancellationToken ct = default);

    /// Fetches one /frame to learn its byte size (for the data-rate estimate). -1 on failure.
    Task<int> MeasureFrameBytesAsync(string? baseUrl, string? token, int pid, int quality, int width, CancellationToken ct = default);

    /// HD video WebRTC signalling, done NATIVELY (not from WebView JS — iOS WKWebView blocks an insecure
    /// http fetch from web content as active mixed content). POSTs the browser's offer SDP, returns the
    /// agent's answer SDP (null on failure). The JS only creates the offer + applies the answer.
    Task<string?> PostWebRtcOfferAsync(string? baseUrl, string? token, int pid, string offerSdp, CancellationToken ct = default);

    /// Tells the agent to tear down a pid's HD session. Best-effort.
    Task PostWebRtcStopAsync(string? baseUrl, string? token, int pid, CancellationToken ct = default);

    /// Diagnostic: forward a WKWebView WebRTC log line to the agent log (so it's readable PC-side).
    Task PostWebRtcClientLogAsync(string? baseUrl, string? token, int pid, string msg, CancellationToken ct = default);
}

public sealed class AcStatusClient : IAcStatusClient
{
    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNameCaseInsensitive = true };
    // Bounds the WHOLE request incl. the body read. HttpClient.Timeout with
    // ResponseHeadersRead only covers up to the headers, so a stalled/trickling
    // body would otherwise hang the poll until the page is disposed. Kept tight
    // (a healthy status fetch is well under 1s) so a jittery Tailscale poll fails
    // fast and the loop retries soon, instead of one stall compounding into a
    // multi-second gap on the dashboard.
    private static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(5);
    private readonly HttpClient _http;

    public AcStatusClient(HttpClient http) => _http = http;

    public async Task<AcStatusResult> GetStatusAsync(string? baseUrl, string? token, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(baseUrl))
            return AcStatusResult.Fail("No status URL configured.");

        Uri uri;
        try { uri = BuildStatusUri(baseUrl); }
        catch { return AcStatusResult.Fail("Status URL isn't a valid address."); }

        // Time-box the whole exchange (connect → headers → body → parse), not just headers.
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(ct);
        linked.CancelAfter(RequestTimeout);
        var lct = linked.Token;
        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Get, uri);
            if (!string.IsNullOrWhiteSpace(token))
                req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token.Trim());

            using var res = await _http.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, lct)
                                       .ConfigureAwait(false);
            if (!res.IsSuccessStatusCode)
                return AcStatusResult.Fail($"Agent returned {(int)res.StatusCode} {res.ReasonPhrase}.");

            await using var stream = await res.Content.ReadAsStreamAsync(lct).ConfigureAwait(false);
            var payload = await JsonSerializer.DeserializeAsync<AcStatusPayload>(stream, JsonOpts, lct)
                                              .ConfigureAwait(false);
            return payload is null
                ? AcStatusResult.Fail("Agent returned an empty response.")
                : AcStatusResult.Success(payload);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;   // caller cancelled (page disposed) — let it bubble
        }
        catch (OperationCanceledException)
        {
            return AcStatusResult.Fail("Timed out reaching the agent.");   // our RequestTimeout or HttpClient.Timeout
        }
        catch (HttpRequestException ex)
        {
            return AcStatusResult.Fail("Can't reach the agent — " + ex.Message);
        }
        catch (JsonException)
        {
            return AcStatusResult.Fail("The agent sent data this app couldn't read.");
        }
    }

    public async Task<AcCommandResult> PostCommandAsync(string? baseUrl, string? token, int pid, string action, string value, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(baseUrl))
            return AcCommandResult.Fail("No status URL configured.");

        Uri uri;
        try { uri = BuildCommandUri(baseUrl); }
        catch { return AcCommandResult.Fail("Status URL isn't a valid address."); }

        using var linked = CancellationTokenSource.CreateLinkedTokenSource(ct);
        linked.CancelAfter(RequestTimeout);
        var lct = linked.Token;
        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Post, uri);
            if (!string.IsNullOrWhiteSpace(token))
                req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token.Trim());
            string body = JsonSerializer.Serialize(new { pid, action, value });
            req.Content = new StringContent(body, System.Text.Encoding.UTF8, "application/json");

            using var res = await _http.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, lct)
                                       .ConfigureAwait(false);
            return res.IsSuccessStatusCode
                ? AcCommandResult.Success()
                : AcCommandResult.Fail($"Agent returned {(int)res.StatusCode} {res.ReasonPhrase}.");
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
        catch (OperationCanceledException) { return AcCommandResult.Fail("Timed out reaching the agent."); }
        catch (HttpRequestException ex) { return AcCommandResult.Fail("Can't reach the agent — " + ex.Message); }
    }

    public async Task<string?> PostWebRtcOfferAsync(string? baseUrl, string? token, int pid, string offerSdp, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(baseUrl)) return null;
        Uri uri;
        try { uri = BuildWebRtcUri(baseUrl, "/webrtc/offer", pid, token); }
        catch { return null; }

        using var linked = CancellationTokenSource.CreateLinkedTokenSource(ct);
        linked.CancelAfter(RequestTimeout);
        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Post, uri);
            if (!string.IsNullOrWhiteSpace(token))
                req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token.Trim());
            req.Content = new StringContent(JsonSerializer.Serialize(new { sdp = offerSdp }), System.Text.Encoding.UTF8, "application/json");
            using var res = await _http.SendAsync(req, HttpCompletionOption.ResponseContentRead, linked.Token).ConfigureAwait(false);
            if (!res.IsSuccessStatusCode) return null;
            string json = await res.Content.ReadAsStringAsync(linked.Token).ConfigureAwait(false);
            using var doc = JsonDocument.Parse(json);
            return doc.RootElement.TryGetProperty("sdp", out var s) ? s.GetString() : null;
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
        catch { return null; }
    }

    public async Task PostWebRtcStopAsync(string? baseUrl, string? token, int pid, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(baseUrl)) return;
        Uri uri;
        try { uri = BuildWebRtcUri(baseUrl, "/webrtc/stop", pid, token); }
        catch { return; }
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(ct);
        linked.CancelAfter(RequestTimeout);
        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Post, uri);
            if (!string.IsNullOrWhiteSpace(token))
                req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token.Trim());
            using var res = await _http.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, linked.Token).ConfigureAwait(false);
        }
        catch { /* best-effort */ }
    }

    public async Task PostWebRtcClientLogAsync(string? baseUrl, string? token, int pid, string msg, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(baseUrl)) return;
        Uri uri;
        try { uri = BuildWebRtcUri(baseUrl, "/webrtc/clientlog", pid, token); }
        catch { return; }
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(ct);
        linked.CancelAfter(RequestTimeout);
        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Post, uri);
            if (!string.IsNullOrWhiteSpace(token))
                req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token.Trim());
            req.Content = new StringContent(msg, System.Text.Encoding.UTF8, "text/plain");
            using var res = await _http.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, linked.Token).ConfigureAwait(false);
        }
        catch { /* best-effort */ }
    }

    private static Uri BuildWebRtcUri(string baseUrl, string route, int pid, string? token)
    {
        var status = BuildStatusUri(baseUrl);
        var ub = new UriBuilder(status)
        {
            Path = status.AbsolutePath
                .Replace("/status.json", route, StringComparison.OrdinalIgnoreCase)
                .Replace("/status", route, StringComparison.OrdinalIgnoreCase),
            Query = "pid=" + pid + (string.IsNullOrWhiteSpace(token) ? "" : "&token=" + Uri.EscapeDataString(token.Trim())),
        };
        return ub.Uri;
    }

    /// Same normalisation as BuildStatusUri but targets the /command route.
    public static Uri BuildCommandUri(string baseUrl)
    {
        var status = BuildStatusUri(baseUrl);
        var ub = new UriBuilder(status) { Path = status.AbsolutePath.Replace("/status.json", "/command", StringComparison.OrdinalIgnoreCase).Replace("/status", "/command", StringComparison.OrdinalIgnoreCase), Query = "" };
        return ub.Uri;
    }

    /// MJPEG live-view URL for one client: ".../stream?pid=N&fps=&q=&w=[&e=][&token=]". Fed straight
    /// into an &lt;img&gt; src — token rides as a query param since img tags can't set headers. `epoch`
    /// changes only on app-resume, forcing the &lt;img&gt; to reconnect a stream iOS dropped in the background.
    public static string BuildStreamUrl(string baseUrl, int pid, string? token, int fps, int quality, int width, int epoch)
        => BuildMediaUrl(baseUrl, "/stream", pid, token, fps, quality, width, epoch);

    /// Single-frame URL (for the data-rate probe).
    public static string BuildFrameUrl(string baseUrl, int pid, string? token, int quality, int width)
        => BuildMediaUrl(baseUrl, "/frame", pid, token, 0, quality, width, 0);

    private static string BuildMediaUrl(string baseUrl, string route, int pid, string? token, int fps, int quality, int width, int epoch)
    {
        var status = BuildStatusUri(baseUrl);
        var ub = new UriBuilder(status)
        {
            Path = status.AbsolutePath
                .Replace("/status.json", route, StringComparison.OrdinalIgnoreCase)
                .Replace("/status", route, StringComparison.OrdinalIgnoreCase),
        };
        var q = new System.Text.StringBuilder($"pid={pid}&q={quality}&w={width}");
        if (fps > 0) q.Append("&fps=").Append(fps);
        if (epoch > 0) q.Append("&e=").Append(epoch);
        if (!string.IsNullOrWhiteSpace(token)) q.Append("&token=").Append(Uri.EscapeDataString(token.Trim()));
        ub.Query = q.ToString();
        return ub.Uri.ToString();
    }

    public async Task<int> MeasureFrameBytesAsync(string? baseUrl, string? token, int pid, int quality, int width, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(baseUrl)) return -1;
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(ct);
        linked.CancelAfter(RequestTimeout);
        try
        {
            var uri = new Uri(BuildFrameUrl(baseUrl, pid, token, quality, width));
            using var req = new HttpRequestMessage(HttpMethod.Get, uri);
            using var res = await _http.SendAsync(req, HttpCompletionOption.ResponseContentRead, linked.Token).ConfigureAwait(false);
            if (!res.IsSuccessStatusCode) return -1;
            byte[] bytes = await res.Content.ReadAsByteArrayAsync(linked.Token).ConfigureAwait(false);
            return bytes.Length;
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
        catch { return -1; }
    }

    /// Accepts "host:8740", "http://host:8740", or a full ".../status[.json]" URL and
    /// normalises to the agent's /status route, preserving any query string (e.g. ?token=).
    public static Uri BuildStatusUri(string baseUrl)
    {
        string s = baseUrl.Trim();
        if (!s.Contains("://", StringComparison.Ordinal))
            s = "http://" + s;   // tolerate a bare host:port

        var b = new Uri(s, UriKind.Absolute);
        string path = b.AbsolutePath.TrimEnd('/');
        bool hasStatus = path.EndsWith("/status", StringComparison.OrdinalIgnoreCase)
                      || path.EndsWith("/status.json", StringComparison.OrdinalIgnoreCase);

        // Rebuild from the trimmed path either way, so "/status/" normalises to "/status"
        // (returning b verbatim would keep the trailing slash and could 404 the agent).
        var ub = new UriBuilder(b) { Path = hasStatus ? path : (path.Length == 0 ? "" : path) + "/status" };
        return ub.Uri;
    }
}
