namespace TrSetup.Core.Processes;

/// <summary>
/// The single choke-point for executing external commands (REQ-FN-003). Every detect and
/// fix that shells out goes through here so the exact command line, stdout/stderr and exit
/// code are always captured for the evidence trail, with live incremental output streaming.
/// </summary>
public interface IProcessRunner
{
    /// <summary>
    /// Runs one command asynchronously, capturing all output and optionally streaming each
    /// output line live as it is produced.
    /// </summary>
    /// <param name="aRequest">What to run, where, and with what timeout.</param>
    /// <param name="aOutputProgress">
    /// Optional live sink: called once per output line (stdout and stderr interleaved) as the
    /// process produces it, so long-running fixes stream progress into the UI.
    /// </param>
    /// <param name="aCancellationToken">Cancels the run (the process is killed).</param>
    /// <returns>The full evidence trail of the run.</returns>
    Task<ProcessRunResult> RunAsync(
        ProcessRunRequest aRequest,
        IProgress<string>? aOutputProgress = null,
        CancellationToken aCancellationToken = default);
}
