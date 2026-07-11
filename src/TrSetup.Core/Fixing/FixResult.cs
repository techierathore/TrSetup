namespace TrSetup.Core.Fixing;

/// <summary>
/// What a fixer reports about its own run. The pipeline never trusts this alone —
/// only a green <c>VerifyAsync</c> re-detect marks a check fixed.
/// </summary>
/// <param name="FixerReportedSuccess">Whether the fixer itself believed it succeeded (e.g. installer exit code 0).</param>
/// <param name="RawOutput">The raw captured output of the fix run (command lines, stdout/stderr, exit codes).</param>
public sealed record FixResult(bool FixerReportedSuccess, string RawOutput);
