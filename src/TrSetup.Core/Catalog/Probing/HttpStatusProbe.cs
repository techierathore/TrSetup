namespace TrSetup.Core.Catalog.Probing;

/// <summary>
/// Default <see cref="IHttpStatusProbe"/> over <see cref="HttpClient"/>: HTTP GET only,
/// 5 second timeout (REQ-FN-009), transport failures reported as unreachable results —
/// never exceptions — so a dead endpoint reads as evidence, not a crash.
/// </summary>
public sealed class HttpStatusProbe : IHttpStatusProbe
{
    /// <summary>The fixed cross-machine probe timeout (REQ-FN-009: 5 seconds).</summary>
    public static readonly TimeSpan ProbeTimeout = TimeSpan.FromSeconds(5);

    private const int MaxBodyLength = 300;

    private readonly HttpClient objHttpClient;

    /// <summary>
    /// Creates the probe.
    /// </summary>
    /// <param name="aMessageHandler">
    /// Optional message handler override so unit tests can fake responses; the real handler
    /// is used when omitted.
    /// </param>
    public HttpStatusProbe(HttpMessageHandler? aMessageHandler = null)
    {
        objHttpClient = aMessageHandler is null ? new HttpClient() : new HttpClient(aMessageHandler);
        objHttpClient.Timeout = ProbeTimeout;
    }

    /// <inheritdoc />
    public async Task<HttpProbeResult> GetAsync(string aUrl, CancellationToken aCancellationToken = default)
    {
        try
        {
            using var vResponse = await objHttpClient
                .GetAsync(aUrl, aCancellationToken)
                .ConfigureAwait(false);
            var vBody = await vResponse.Content.ReadAsStringAsync(aCancellationToken).ConfigureAwait(false);
            return new HttpProbeResult(true, (int)vResponse.StatusCode, Truncate(vBody), null);
        }
        catch (OperationCanceledException) when (aCancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception vEx) when (vEx is HttpRequestException or TaskCanceledException or UriFormatException or InvalidOperationException)
        {
            return new HttpProbeResult(false, null, string.Empty, $"{vEx.GetType().Name}: {vEx.Message}");
        }
    }

    private static string Truncate(string aBody) =>
        aBody.Length <= MaxBodyLength ? aBody : aBody[..MaxBodyLength] + "…";
}
