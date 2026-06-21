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
}

public sealed class AcStatusClient : IAcStatusClient
{
    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNameCaseInsensitive = true };
    // Bounds the WHOLE request incl. the body read. HttpClient.Timeout with
    // ResponseHeadersRead only covers up to the headers, so a stalled/trickling
    // body would otherwise hang the poll until the page is disposed.
    private static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(8);
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

    /// Same normalisation as BuildStatusUri but targets the /command route.
    public static Uri BuildCommandUri(string baseUrl)
    {
        var status = BuildStatusUri(baseUrl);
        var ub = new UriBuilder(status) { Path = status.AbsolutePath.Replace("/status.json", "/command", StringComparison.OrdinalIgnoreCase).Replace("/status", "/command", StringComparison.OrdinalIgnoreCase), Query = "" };
        return ub.Uri;
    }

    /// MJPEG live-view URL for one client: ".../stream?pid=N[&token=...]". Fed straight into an
    /// &lt;img&gt; src — the token rides as a query param since img tags can't set headers.
    public static string BuildStreamUrl(string baseUrl, int pid, string? token)
    {
        var status = BuildStatusUri(baseUrl);
        var ub = new UriBuilder(status)
        {
            Path = status.AbsolutePath
                .Replace("/status.json", "/stream", StringComparison.OrdinalIgnoreCase)
                .Replace("/status", "/stream", StringComparison.OrdinalIgnoreCase),
        };
        string q = "pid=" + pid;
        if (!string.IsNullOrWhiteSpace(token)) q += "&token=" + Uri.EscapeDataString(token.Trim());
        ub.Query = q;
        return ub.Uri.ToString();
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
