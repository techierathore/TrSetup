using TrSetup.Core.Catalog.Probing;
using TrSetup.Core.Checks;
using TrSetup.Core.Processes;

namespace TrSetup.Core.Catalog.Wsl;

/// <summary>
/// F-WSLCHK: "Mirrored networking active (WSL side)" — detects via
/// <c>wslinfo --networking-mode</c>. Manual-only from this side: the fix (.wslconfig) is owned
/// by the Windows device-host role.
/// </summary>
public sealed class WslMirroredNetworkingCheck : Check
{
    private readonly IProcessRunner objProcessRunner;

    /// <summary>
    /// Creates the check.
    /// </summary>
    /// <param name="aProcessRunner">The process choke-point the detect shells through.</param>
    public WslMirroredNetworkingCheck(IProcessRunner aProcessRunner)
    {
        objProcessRunner = aProcessRunner;
    }

    /// <inheritdoc />
    public override string Id => "wsl.mirrored-networking";

    /// <inheritdoc />
    public override string Title => "Mirrored networking active (WSL side)";

    /// <inheritdoc />
    public override string Category => BoardCategories.FrameworkCore;

    /// <inheritdoc />
    public override MachineRole Roles => MachineRole.AgentHostWsl;

    /// <inheritdoc />
    public override CheckSeverity Severity => CheckSeverity.Required;

    /// <inheritdoc />
    public override CheckExplanation Explain => new(
        "WSL2 mirrored networking mode, which lets WSL reach Windows-host services on localhost.",
        "The WSL→Windows Appium bridge and the `trsetup gui` browser hop both ride on mirrored networking.",
        "WORKFLOW §0b");

    /// <inheritdoc />
    public override async Task<CheckResult> DetectAsync(CancellationToken aCancellationToken = default)
    {
        var vRun = await ProcessProbe.RunAsync(
            objProcessRunner,
            new ProcessRunRequest("wslinfo", "--networking-mode", null, TimeSpan.FromSeconds(10)),
            aCancellationToken).ConfigureAwait(false);
        var vMode = vRun.StandardOutput.Trim();
        if (vRun.Succeeded && vMode.Contains("mirrored", StringComparison.OrdinalIgnoreCase))
        {
            return CheckResult.Pass($"WSL networking mode is '{vMode}' ($ wslinfo --networking-mode).");
        }

        if (vRun.Succeeded && vMode.Length > 0)
        {
            return CheckResult.Fail(
                $"WSL networking mode is '{vMode}', not 'mirrored'. " +
                CrossMachineGuidance.FixOn("Windows host", "Device host") +
                " (its .wslconfig check patches networkingMode=mirrored).");
        }

        return CheckResult.Warn(
            $"Could not determine the WSL networking mode (wslinfo unavailable — WSL may predate mirrored networking).\n{vRun.ToEvidenceString()}");
    }
}
