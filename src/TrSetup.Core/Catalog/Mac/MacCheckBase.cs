using TrSetup.Core.Catalog.Probing;
using TrSetup.Core.Checks;
using TrSetup.Core.Fixing;
using TrSetup.Core.Processes;

namespace TrSetup.Core.Catalog.Mac;

/// <summary>
/// Base for Mac device-host checks (F-MACCHK): each detect shells local macOS commands
/// through the process choke-point. These checks only enumerate on machines holding the
/// <see cref="MachineRole.DeviceHostMac"/> role — they never run cross-machine. Fixable
/// subclasses also receive the P2 fixer frameworks (<see cref="CheckFixServices"/>) and run
/// their remediation through the same choke-point (REQ-FN-016).
/// </summary>
public abstract class MacCheckBase : Check
{
    private readonly IProcessRunner objProcessRunner;

    /// <summary>
    /// Creates the check.
    /// </summary>
    /// <param name="aProcessRunner">The process choke-point macOS commands run through.</param>
    /// <param name="aFix">Fixer frameworks; when null the check is detect-only (no Fix button).</param>
    protected MacCheckBase(IProcessRunner aProcessRunner, CheckFixServices? aFix = null)
    {
        objProcessRunner = aProcessRunner;
        FixServices = aFix;
    }

    /// <inheritdoc />
    public override string Category => BoardCategories.FrameworkCore;

    /// <inheritdoc />
    public override MachineRole Roles => MachineRole.DeviceHostMac;

    /// <summary>The fixer frameworks, or <c>null</c> when this check instance is detect-only.</summary>
    protected CheckFixServices? FixServices { get; }

    /// <summary>Whether this check instance was given a fixer.</summary>
    protected bool CanFix => FixServices is not null;

    /// <summary>The process choke-point macOS fix commands run through.</summary>
    protected IProcessRunner ProcessRunner => objProcessRunner;

    /// <summary>
    /// Runs a local macOS command without throwing when the executable is missing.
    /// </summary>
    /// <param name="aFileName">The executable to start.</param>
    /// <param name="aArguments">The argument string.</param>
    /// <param name="aTimeout">Maximum run time before the process is killed.</param>
    /// <param name="aCancellationToken">Cancels the run.</param>
    /// <returns>The run's evidence trail.</returns>
    protected Task<ProcessRunResult> RunMacCommandAsync(
        string aFileName,
        string aArguments,
        TimeSpan aTimeout,
        CancellationToken aCancellationToken) =>
        ProcessProbe.RunAsync(
            objProcessRunner,
            new ProcessRunRequest(aFileName, aArguments, null, aTimeout),
            aCancellationToken);

    /// <summary>
    /// Runs a local macOS fix command and wraps its evidence trail as a <see cref="FixResult"/>.
    /// </summary>
    /// <param name="aFileName">The executable to start.</param>
    /// <param name="aArguments">The argument string.</param>
    /// <param name="aTimeout">Maximum run time before the process is killed.</param>
    /// <param name="aCancellationToken">Cancels the run.</param>
    /// <returns>The fix result carrying the command's raw output.</returns>
    protected async Task<FixResult> RunMacFixAsync(
        string aFileName,
        string aArguments,
        TimeSpan aTimeout,
        CancellationToken aCancellationToken)
    {
        var vRun = await RunMacCommandAsync(aFileName, aArguments, aTimeout, aCancellationToken).ConfigureAwait(false);
        return new FixResult(vRun.Succeeded, vRun.ToEvidenceString());
    }
}
