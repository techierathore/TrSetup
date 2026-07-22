namespace TrSetup.Core.Catalog.Probing;

/// <summary>
/// A plain HTTP GET reachability probe (REQ-FN-009 / F-BRIDGE). This is the ONLY way checks
/// touch another machine — TrSetup never remote-executes; you run TrSetup on the other
/// machine to fix its items.
/// </summary>
public interface IHttpStatusProbe
{
    /// <summary>
    /// Performs one HTTP GET against the URL, bounded by the 5 s probe timeout, and reports
    /// reachability plus the response instead of throwing on transport failures.
    /// </summary>
    /// <param name="aUrl">The absolute URL to probe (e.g. <c>http://localhost:4723/status</c>).</param>
    /// <param name="aCancellationToken">Cancels the probe.</param>
    /// <returns>The probe outcome — reachable/unreachable plus status code, body and error.</returns>
    Task<HttpProbeResult> GetAsync(string aUrl, CancellationToken aCancellationToken = default);

    /// <summary>
    /// Performs one HTTP GET, optionally accepting a TLS certificate this machine does not trust
    /// (REQ-FN-028).
    /// <para>
    /// <b>Security posture:</b> <paramref name="aAllowUntrustedCertificate"/> is NEVER a default.
    /// It is passed <c>true</c> only for an endpoint URL the user configured themselves AND
    /// explicitly ticked "trust self-signed certificate" for
    /// (<see cref="Settings.TrSetupSettings.TrustedSelfSignedEndpoints"/>). The default
    /// implementation ignores the flag and validates normally, so any probe that does not opt in —
    /// including every existing caller and test double — keeps full validation.
    /// </para>
    /// </summary>
    /// <param name="aUrl">The absolute URL to probe.</param>
    /// <param name="aAllowUntrustedCertificate">Whether to accept an untrusted/self-signed server certificate.</param>
    /// <param name="aCancellationToken">Cancels the probe.</param>
    /// <returns>The probe outcome — reachable/unreachable plus status code, body and error.</returns>
    Task<HttpProbeResult> GetAsync(
        string aUrl,
        bool aAllowUntrustedCertificate,
        CancellationToken aCancellationToken = default)
        => GetAsync(aUrl, aCancellationToken);
}
