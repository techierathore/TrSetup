using TrSetup.Core.Catalog.Probing;
using TrSetup.Core.Checks;

namespace TrSetup.Core.Catalog.Wsl;

/// <summary>
/// F-WSLCHK / F-BRIDGE: "Windows-host Appium reachable" — plain HTTP GET
/// <c>http://localhost:4723/status</c> over mirrored networking (REQ-FN-009: probe only,
/// never remote-execute; failing guidance names the Windows Device host role).
/// </summary>
public sealed class WslWindowsAppiumCheck : Check
{
    /// <summary>The probed Windows-host Appium status URL (localhost via mirrored networking).</summary>
    public const string StatusUrl = "http://localhost:4723/status";

    private readonly IHttpStatusProbe objHttpProbe;

    /// <summary>
    /// Creates the check.
    /// </summary>
    /// <param name="aHttpProbe">The HTTP reachability probe.</param>
    public WslWindowsAppiumCheck(IHttpStatusProbe aHttpProbe)
    {
        objHttpProbe = aHttpProbe;
    }

    /// <inheritdoc />
    public override string Id => "wsl.appium-windows";

    /// <inheritdoc />
    public override string Title => "Windows-host Appium reachable";

    /// <inheritdoc />
    public override string Category => BoardCategories.Bridges;

    /// <inheritdoc />
    public override MachineRole Roles => MachineRole.AgentHostWsl;

    /// <inheritdoc />
    public override CheckSeverity Severity => CheckSeverity.Recommended;

    /// <inheritdoc />
    public override CheckExplanation Explain => new(
        "The Appium server on the Windows host, reached from WSL as localhost:4723 via mirrored networking.",
        "MAUI Android verification drives the emulator over this bridge; unreachable means no Android runtime gate.",
        "WORKFLOW §0b");

    /// <inheritdoc />
    public override async Task<CheckResult> DetectAsync(CancellationToken aCancellationToken = default)
    {
        var vProbe = await objHttpProbe.GetAsync(StatusUrl, aCancellationToken).ConfigureAwait(false);
        if (vProbe.IsSuccess)
        {
            return CheckResult.Pass($"Windows Appium reachable: GET {StatusUrl} → HTTP {vProbe.StatusCode}. {vProbe.Body}".TrimEnd());
        }

        if (vProbe.IsReachable)
        {
            return CheckResult.Warn(
                $"GET {StatusUrl} answered HTTP {vProbe.StatusCode} — something is listening on 4723 but it does not look like a healthy Appium.");
        }

        return CheckResult.Fail(
            $"Windows Appium unreachable: GET {StatusUrl} failed ({vProbe.Error}). " +
            CrossMachineGuidance.FixOn("Windows host", "Device host"));
    }
}
