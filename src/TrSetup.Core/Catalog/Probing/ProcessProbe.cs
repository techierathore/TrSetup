using TrSetup.Core.Processes;

namespace TrSetup.Core.Catalog.Probing;

/// <summary>
/// Exception-safe wrapper over <see cref="IProcessRunner"/> for detect probes: a missing
/// executable (Process.Start throwing) is normal detect evidence — "tool not found" — not a
/// crash, so it is converted into a failed <see cref="ProcessRunResult"/>.
/// </summary>
internal static class ProcessProbe
{
    /// <summary>Exit code reported when the executable itself could not be started.</summary>
    internal const int StartFailedExitCode = 127;

    /// <summary>
    /// Runs the request, converting a failed process start into a non-zero-exit result whose
    /// stderr carries the start error.
    /// </summary>
    /// <param name="aProcessRunner">The runner choke-point.</param>
    /// <param name="aRequest">What to run.</param>
    /// <param name="aCancellationToken">Cancels the run.</param>
    /// <returns>The run result; never throws for a missing executable.</returns>
    internal static async Task<ProcessRunResult> RunAsync(
        IProcessRunner aProcessRunner,
        ProcessRunRequest aRequest,
        CancellationToken aCancellationToken)
    {
        try
        {
            return await aProcessRunner.RunAsync(aRequest, null, aCancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception vEx)
        {
            var vCommandLine = $"{aRequest.FileName} {aRequest.Arguments}".TrimEnd();
            return new ProcessRunResult(
                vCommandLine,
                StartFailedExitCode,
                string.Empty,
                $"could not start '{aRequest.FileName}': {vEx.Message}",
                false,
                TimeSpan.Zero);
        }
    }
}
