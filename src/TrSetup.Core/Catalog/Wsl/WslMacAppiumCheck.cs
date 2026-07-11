using TrSetup.Core.Catalog.Probing;
using TrSetup.Core.Checks;
using TrSetup.Core.Settings;

namespace TrSetup.Core.Catalog.Wsl;

/// <summary>
/// F-WSLCHK / F-BRIDGE: "Mac Appium reachable (if app ships iOS/Catalyst)" — plain HTTP GET
/// <c>http://&lt;mac-ip&gt;:4723/status</c> with the endpoint taken live from settings
/// (REQ-FN-009: probe only; failing guidance names the Mac Device host role).
/// </summary>
public sealed class WslMacAppiumCheck : Check
{
    /// <summary>The settings endpoint key holding the LAN Mac address.</summary>
    public const string MacIpEndpointKey = "MacIp";

    private readonly IHttpStatusProbe objHttpProbe;
    private readonly Func<TrSetupSettings> objSettingsAccessor;

    /// <summary>
    /// Creates the check.
    /// </summary>
    /// <param name="aHttpProbe">The HTTP reachability probe.</param>
    /// <param name="aSettingsAccessor">Live accessor for current settings (the configured Mac endpoint).</param>
    public WslMacAppiumCheck(IHttpStatusProbe aHttpProbe, Func<TrSetupSettings> aSettingsAccessor)
    {
        objHttpProbe = aHttpProbe;
        objSettingsAccessor = aSettingsAccessor;
    }

    /// <inheritdoc />
    public override string Id => "wsl.appium-mac";

    /// <inheritdoc />
    public override string Title => "Mac Appium reachable";

    /// <inheritdoc />
    public override string Category => BoardCategories.Bridges;

    /// <inheritdoc />
    public override MachineRole Roles => MachineRole.AgentHostWsl;

    /// <inheritdoc />
    public override CheckSeverity Severity => CheckSeverity.Optional;

    /// <inheritdoc />
    public override CheckExplanation Explain => new(
        "The Appium server on the LAN Mac, reached at the configured Mac endpoint on port 4723.",
        "iOS / Mac Catalyst verification drives simulators over this bridge — needed only when the app ships those heads.",
        "WORKFLOW §0b");

    /// <inheritdoc />
    public override async Task<CheckResult> DetectAsync(CancellationToken aCancellationToken = default)
    {
        var vSettings = objSettingsAccessor();
        if (!vSettings.Endpoints.TryGetValue(MacIpEndpointKey, out var vMacIp) || string.IsNullOrWhiteSpace(vMacIp))
        {
            return CheckResult.NotApplicable(
                $"No Mac endpoint configured (settings Endpoints['{MacIpEndpointKey}']) — required only when the app ships iOS/Mac Catalyst heads.");
        }

        var vUrl = $"http://{vMacIp.Trim()}:4723/status";
        var vProbe = await objHttpProbe.GetAsync(vUrl, aCancellationToken).ConfigureAwait(false);
        if (vProbe.IsSuccess)
        {
            return CheckResult.Pass($"Mac Appium reachable: GET {vUrl} → HTTP {vProbe.StatusCode}. {vProbe.Body}".TrimEnd());
        }

        if (vProbe.IsReachable)
        {
            return CheckResult.Warn(
                $"GET {vUrl} answered HTTP {vProbe.StatusCode} — something is listening on the Mac's 4723 but it does not look like a healthy Appium.");
        }

        return CheckResult.Fail(
            $"Mac Appium unreachable: GET {vUrl} failed ({vProbe.Error}). " +
            CrossMachineGuidance.FixOn("Mac", "Device host"));
    }
}
