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
}
