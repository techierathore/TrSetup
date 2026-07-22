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
    private readonly HttpMessageHandler? objMessageHandler;
    private readonly Lock objTrustingLock = new();
    private HttpClient? objTrustingClient;

    /// <summary>
    /// Creates the probe.
    /// </summary>
    /// <param name="aMessageHandler">
    /// Optional message handler override so unit tests can fake responses; the real handler
    /// is used when omitted.
    /// </param>
    public HttpStatusProbe(HttpMessageHandler? aMessageHandler = null)
    {
        objMessageHandler = aMessageHandler;
        objHttpClient = aMessageHandler is null ? new HttpClient() : new HttpClient(aMessageHandler);
        objHttpClient.Timeout = ProbeTimeout;
    }

    /// <inheritdoc />
    public Task<HttpProbeResult> GetAsync(string aUrl, CancellationToken aCancellationToken = default)
        => GetAsync(aUrl, objHttpClient, aCancellationToken);

    /// <inheritdoc />
    public Task<HttpProbeResult> GetAsync(
        string aUrl,
        bool aAllowUntrustedCertificate,
        CancellationToken aCancellationToken = default)
        => GetAsync(
            aUrl,
            aAllowUntrustedCertificate ? TrustingClient() : objHttpClient,
            aCancellationToken);

    /// <summary>
    /// The lazily built client that accepts ANY server certificate. Deliberately separate from the
    /// default client so relaxed TLS can never leak into an ordinary probe: a caller has to ask for
    /// it explicitly, and only an endpoint the user configured and opted into ever does
    /// (REQ-FN-028, <see cref="Settings.TrSetupSettings.TrustedSelfSignedEndpoints"/>).
    /// </summary>
    /// <returns>The certificate-trusting client for this probe instance.</returns>
    private HttpClient TrustingClient()
    {
        lock (objTrustingLock)
        {
            if (objTrustingClient is not null)
            {
                return objTrustingClient;
            }

            // A faked handler already bypasses the transport, so honour it rather than building a
            // real socket handler underneath a unit test.
            HttpMessageHandler vHandler = objMessageHandler ?? new HttpClientHandler
            {
                ServerCertificateCustomValidationCallback =
                    HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
            };

            objTrustingClient = new HttpClient(vHandler, disposeHandler: objMessageHandler is null)
            {
                Timeout = ProbeTimeout
            };
            return objTrustingClient;
        }
    }

    private static async Task<HttpProbeResult> GetAsync(
        string aUrl,
        HttpClient aClient,
        CancellationToken aCancellationToken)
    {
        try
        {
            using var vResponse = await aClient
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
