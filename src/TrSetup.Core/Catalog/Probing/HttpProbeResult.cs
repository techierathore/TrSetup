namespace TrSetup.Core.Catalog.Probing;

/// <summary>
/// The outcome of one HTTP status probe: whether the endpoint answered at all, and with what.
/// </summary>
/// <param name="IsReachable">Whether an HTTP response (any status code) came back.</param>
/// <param name="StatusCode">The HTTP status code, or <c>null</c> when unreachable.</param>
/// <param name="Body">The response body (truncated for evidence); empty when unreachable.</param>
/// <param name="Error">The transport error (refused, timeout, DNS, ...), or <c>null</c> when reachable.</param>
public sealed record HttpProbeResult(bool IsReachable, int? StatusCode, string Body, string? Error)
{
    /// <summary>Whether the endpoint answered with a success (2xx) status code.</summary>
    public bool IsSuccess => IsReachable && StatusCode is >= 200 and < 300;
}
