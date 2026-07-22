using TrSetup.Core.Checks;
using TrSetup.Core.Settings;

namespace TrSetup.Core.Profiles.Handlers;

/// <summary>
/// Presence handler for the <c>endpoint</c> requirement type (REQ-FN-021): detects an HTTP
/// endpoint's reachability via a bounded GET. A 2xx answer passes; any other answer warns
/// (reachable but not healthy); no answer fails.
/// <para>
/// Params: <c>url</c> (required — the profile's default endpoint) and <c>urlSettingKey</c>
/// (optional — the <see cref="TrSetupSettings.Endpoints"/> key a machine may override the URL
/// with, REQ-FN-028). The URL is resolved PER DETECT, not once at construction, so saving a new
/// value in Settings is picked up by the very next sweep without a restart.
/// </para>
/// </summary>
public sealed class EndpointRequirementHandler : IProfileRequirementHandler
{
    /// <inheritdoc />
    public string Type => ProfileRequirementTypes.Endpoint;

    /// <inheritdoc />
    public Check CreateCheck(ProfileRequirement aRequirement, ProfileCheckContext aContext)
    {
        ArgumentNullException.ThrowIfNull(aRequirement);
        ArgumentNullException.ThrowIfNull(aContext);
        var vDefaultUrl = aRequirement.Param("url") ?? string.Empty;
        var vSettingKey = aRequirement.Param(EndpointResolver.UrlSettingKeyParam);
        var vExplain = new CheckExplanation(
            vSettingKey is null
                ? $"Reachability of the endpoint {vDefaultUrl} this app depends on."
                : $"Reachability of the endpoint this app depends on — {vDefaultUrl} by default, "
                  + $"overridable per machine in Settings → Endpoints ['{vSettingKey}'] when the service "
                  + "runs on another machine on the LAN.",
            "The app calls this endpoint at runtime; if it is unreachable the dependent workflow fails.",
            "WORKFLOW §0");
        return new ProfileCheck(
            aRequirement,
            aContext.ProfileName,
            vExplain,
            aToken => DetectAsync(aContext, vDefaultUrl, vSettingKey, aToken));
    }

    private static async Task<CheckResult> DetectAsync(
        ProfileCheckContext aContext,
        string aDefaultUrl,
        string? aSettingKey,
        CancellationToken aToken)
    {
        var vEndpoint = EndpointResolver.Resolve(aDefaultUrl, aSettingKey, aContext.SettingsAccessor());
        var vProbe = await aContext.HttpProbe
            .GetAsync(vEndpoint.Url, vEndpoint.AllowSelfSignedCertificate, aToken)
            .ConfigureAwait(false);

        // Always name the endpoint AND where it came from. Without the provenance a
        // "connection refused (localhost:5101)" reads as "the service is down" when the real cause
        // is "this machine is pointed at the wrong host" (REQ-FN-028).
        var vWhere = $"{vEndpoint.Url} [{vEndpoint.Source}]";
        if (!vProbe.IsReachable)
        {
            return CheckResult.Fail($"Endpoint {vWhere} is unreachable ({vProbe.Error}).{TlsHint(vEndpoint, vProbe.Error, aSettingKey)}");
        }

        return vProbe.IsSuccess
            ? CheckResult.Pass($"Endpoint {vWhere} answered {vProbe.StatusCode}.")
            : CheckResult.Warn($"Endpoint {vWhere} answered {vProbe.StatusCode} (reachable but not a success status).");
    }

    /// <summary>
    /// Adds a targeted hint when the transport failure looks like a certificate rejection and the
    /// user has NOT opted into trusting this endpoint's certificate. A dev/self-signed certificate
    /// on a LAN service is otherwise indistinguishable from "the service is broken".
    /// </summary>
    /// <param name="aEndpoint">The resolved endpoint (its trust state decides whether a hint helps).</param>
    /// <param name="aError">The transport error text from the probe.</param>
    /// <param name="aSettingKey">The overriding settings key, or <c>null</c> when not overridable.</param>
    /// <returns>The hint sentence, or an empty string when none applies.</returns>
    private static string TlsHint(EndpointResolution aEndpoint, string? aError, string? aSettingKey)
    {
        if (aEndpoint.AllowSelfSignedCertificate || aSettingKey is null || aError is null)
        {
            return string.Empty;
        }

        var vLooksLikeTls = aError.Contains("certificate", StringComparison.OrdinalIgnoreCase)
            || aError.Contains("SSL", StringComparison.OrdinalIgnoreCase)
            || aError.Contains("secure channel", StringComparison.OrdinalIgnoreCase);

        return vLooksLikeTls
            ? " The server answered but presented a certificate this machine does not trust. If that is a "
              + "development/self-signed certificate on your own LAN, tick \"Trust self-signed certificate\" "
              + $"for '{aSettingKey}' in Settings — TrSetup will not accept it otherwise."
            : string.Empty;
    }
}
