namespace TrSetup.Core.Processes;

/// <summary>
/// The complete evidence trail of one executed command — exactly what ran, everything it
/// printed, and how it exited. This is what the board's detail pane shows verbatim.
/// </summary>
/// <param name="CommandLine">The exact command line that was executed (file name + arguments).</param>
/// <param name="ExitCode">The process exit code; <c>-1</c> when the process was killed on timeout.</param>
/// <param name="StandardOutput">Everything the process wrote to stdout.</param>
/// <param name="StandardError">Everything the process wrote to stderr.</param>
/// <param name="TimedOut">Whether the process was killed because it exceeded the request's timeout.</param>
/// <param name="Duration">Wall-clock time the process ran.</param>
public sealed record ProcessRunResult(
    string CommandLine,
    int ExitCode,
    string StandardOutput,
    string StandardError,
    bool TimedOut,
    TimeSpan Duration)
{
    /// <summary>Whether the process exited on its own with exit code 0.</summary>
    public bool Succeeded => !TimedOut && ExitCode == 0;

    /// <summary>
    /// Renders the full evidence trail (command, exit code, stdout, stderr) as one block
    /// suitable for a check's evidence text or a FAILED fix report.
    /// </summary>
    /// <returns>A multi-line human-readable evidence block.</returns>
    public string ToEvidenceString()
    {
        var vBuilder = new System.Text.StringBuilder();
        vBuilder.AppendLine($"$ {CommandLine}");
        vBuilder.AppendLine($"exit code: {ExitCode}{(TimedOut ? " (timed out)" : string.Empty)}");
        if (StandardOutput.Length > 0)
        {
            vBuilder.AppendLine("stdout:").AppendLine(StandardOutput.TrimEnd());
        }

        if (StandardError.Length > 0)
        {
            vBuilder.AppendLine("stderr:").AppendLine(StandardError.TrimEnd());
        }

        return vBuilder.ToString().TrimEnd();
    }
}
