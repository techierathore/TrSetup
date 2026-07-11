using System.Text;
using TrSetup.Core.Catalog.Probing;
using TrSetup.Core.Elevation;
using TrSetup.Core.Processes;

namespace TrSetup.Core.Fixing;

/// <summary>
/// Small helpers shared by check fixers for turning process runs, sudo handoffs and download
/// evidence into <see cref="FixResult"/> objects. The pipeline still re-verifies afterwards —
/// a fixer's self-reported success is never trusted on its own (REQ-FN-002).
/// </summary>
internal static class FixExecution
{
    /// <summary>
    /// Runs one fix command through the process choke-point (never throwing when the executable
    /// is missing) and wraps its full evidence trail in a <see cref="FixResult"/>.
    /// </summary>
    /// <param name="aProcessRunner">The process choke-point.</param>
    /// <param name="aRequest">The command to run.</param>
    /// <param name="aCancellationToken">Cancels the run.</param>
    /// <returns>A fix result whose raw output is the command's evidence trail.</returns>
    internal static async Task<FixResult> RunAsync(
        IProcessRunner aProcessRunner,
        ProcessRunRequest aRequest,
        CancellationToken aCancellationToken)
    {
        var vRun = await ProcessProbe.RunAsync(aProcessRunner, aRequest, aCancellationToken).ConfigureAwait(false);
        return new FixResult(vRun.Succeeded, vRun.ToEvidenceString());
    }

    /// <summary>
    /// Builds the *nix sudo terminal handoff for a fix that needs root: TrSetup executes
    /// nothing, the raw output is the one line for the user to paste into their own terminal
    /// (REQ-FN-020 / REQ-NFR-002 — no password ever passes through TrSetup).
    /// </summary>
    /// <param name="aCommand">The command that needs root.</param>
    /// <returns>A fix result carrying the paste-into-your-terminal instructions.</returns>
    internal static FixResult SudoHandoff(ElevatedCommand aCommand)
    {
        var vHandoff = ElevationRunner.CreateSudoHandoff(aCommand);
        return new FixResult(false, vHandoff.Instructions);
    }

    /// <summary>
    /// Joins several run evidence blocks into one raw-output string in execution order.
    /// </summary>
    /// <param name="aBlocks">The evidence blocks to join.</param>
    /// <returns>The blocks separated by blank lines.</returns>
    internal static string JoinOutput(params string[] aBlocks)
    {
        var vBuilder = new StringBuilder();
        foreach (var vBlock in aBlocks)
        {
            if (!string.IsNullOrEmpty(vBlock))
            {
                vBuilder.AppendLine(vBlock);
                vBuilder.AppendLine();
            }
        }

        return vBuilder.ToString().TrimEnd();
    }
}
