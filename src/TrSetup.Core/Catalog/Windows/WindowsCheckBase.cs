using TrSetup.Core.Catalog.Probing;
using TrSetup.Core.Checks;
using TrSetup.Core.Elevation;
using TrSetup.Core.Fixing;
using TrSetup.Core.Processes;

namespace TrSetup.Core.Catalog.Windows;

/// <summary>
/// Base for Windows device-host checks (F-WINCHK): every detect runs a PowerShell script
/// through the process choke-point — natively on Windows, over the WSL interop bridge from
/// WSL — with the hop named in the evidence (Architecture: detect the platform, never
/// hardcode which side we are on). Fixable subclasses also receive the P2 fixer frameworks
/// (<see cref="CheckFixServices"/>) and run their remediation through the same choke-point,
/// elevating via a visible UAC child where admin is required (REQ-FN-015 / REQ-FN-020).
/// </summary>
public abstract class WindowsCheckBase : Check
{
    private readonly IProcessRunner objProcessRunner;

    /// <summary>
    /// Creates the check.
    /// </summary>
    /// <param name="aProcessRunner">The process choke-point Windows scripts run through.</param>
    /// <param name="aFix">Fixer frameworks; when null the check is detect-only (no Fix button).</param>
    protected WindowsCheckBase(IProcessRunner aProcessRunner, CheckFixServices? aFix = null)
    {
        objProcessRunner = aProcessRunner;
        FixServices = aFix;
    }

    /// <inheritdoc />
    public override string Category => BoardCategories.FrameworkCore;

    /// <inheritdoc />
    public override MachineRole Roles => MachineRole.DeviceHostWindows;

    /// <summary>The fixer frameworks, or <c>null</c> when this check instance is detect-only.</summary>
    protected CheckFixServices? FixServices { get; }

    /// <summary>Whether this check instance was given a fixer.</summary>
    protected bool CanFix => FixServices is not null;

    /// <summary>
    /// Runs a PowerShell script on the Windows side (directly on Windows; via the interop
    /// bridge from WSL) without throwing when PowerShell itself cannot start.
    /// </summary>
    /// <param name="aScript">The PowerShell script to run.</param>
    /// <param name="aTimeout">Maximum run time before the process is killed.</param>
    /// <param name="aCancellationToken">Cancels the run.</param>
    /// <returns>The run's evidence trail.</returns>
    protected Task<ProcessRunResult> RunWindowsScriptAsync(
        string aScript,
        TimeSpan aTimeout,
        CancellationToken aCancellationToken) =>
        ProcessProbe.RunAsync(
            objProcessRunner,
            WindowsCommandBridge.BuildPowerShell(aScript, aTimeout),
            aCancellationToken);

    /// <summary>
    /// Runs a PowerShell fix script and wraps its evidence trail as a <see cref="FixResult"/>.
    /// </summary>
    /// <param name="aScript">The PowerShell fix script to run.</param>
    /// <param name="aTimeout">Maximum run time before the process is killed.</param>
    /// <param name="aCancellationToken">Cancels the run.</param>
    /// <returns>The fix result carrying the script's raw output.</returns>
    protected async Task<FixResult> RunWindowsFixAsync(
        string aScript,
        TimeSpan aTimeout,
        CancellationToken aCancellationToken)
    {
        var vRun = await RunWindowsScriptAsync(aScript, aTimeout, aCancellationToken).ConfigureAwait(false);
        return new FixResult(vRun.Succeeded, vRun.ToEvidenceString());
    }

    /// <summary>
    /// Runs a PowerShell fix script elevated through a visible UAC child (REQ-FN-020) and wraps
    /// the launcher's evidence trail as a <see cref="FixResult"/>. Requires a granted consent token.
    /// </summary>
    /// <param name="aScript">The PowerShell fix script to run elevated.</param>
    /// <param name="aDescription">One sentence describing what the elevated command does.</param>
    /// <param name="aConsent">The granted consent token issued after the command was previewed.</param>
    /// <param name="aCancellationToken">Cancels waiting for the elevated child.</param>
    /// <returns>The fix result carrying the launcher's raw output.</returns>
    protected async Task<FixResult> RunWindowsElevatedFixAsync(
        string aScript,
        string aDescription,
        ConsentToken aConsent,
        CancellationToken aCancellationToken)
    {
        var vCommand = new ElevatedCommand(
            "powershell.exe", $"-NoProfile -NonInteractive -Command \"{aScript}\"", aDescription);
        var vRun = await FixServices!.ElevationRunner
            .RunWindowsElevatedAsync(vCommand, aConsent, null, aCancellationToken).ConfigureAwait(false);
        return new FixResult(vRun.Succeeded, vRun.ToEvidenceString());
    }

    /// <summary>
    /// Runs an arbitrary elevated command through a visible UAC child (REQ-FN-020) and wraps the
    /// launcher's evidence trail as a <see cref="FixResult"/>. Requires a granted consent token.
    /// </summary>
    /// <param name="aCommand">The command to elevate (e.g. <c>msiexec /i ... /qn</c>).</param>
    /// <param name="aConsent">The granted consent token issued after the command was previewed.</param>
    /// <param name="aCancellationToken">Cancels waiting for the elevated child.</param>
    /// <returns>The fix result carrying the launcher's raw output.</returns>
    protected async Task<FixResult> RunElevatedFixAsync(
        ElevatedCommand aCommand,
        ConsentToken aConsent,
        CancellationToken aCancellationToken)
    {
        var vRun = await FixServices!.ElevationRunner
            .RunWindowsElevatedAsync(aCommand, aConsent, null, aCancellationToken).ConfigureAwait(false);
        return new FixResult(vRun.Succeeded, vRun.ToEvidenceString());
    }

    /// <summary>
    /// Appends the probed-via hop (native Windows vs WSL interop bridge) to an evidence line.
    /// </summary>
    /// <param name="aEvidence">The evidence text to annotate.</param>
    /// <returns>The evidence with the hop named.</returns>
    protected static string ViaBridge(string aEvidence) =>
        $"{aEvidence} [probed via {WindowsCommandBridge.Describe()}]";
}
