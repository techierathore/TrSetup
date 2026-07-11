using TrSetup.Core.Checks;

namespace TrSetup.Core.Fixing;

/// <summary>
/// The full evidence trail of one pipeline run: what happened, the raw fixer output,
/// and the re-verify result that decided the outcome.
/// </summary>
/// <param name="Status">The pipeline outcome for the check.</param>
/// <param name="RawOutput">Raw captured output of the fix run; empty when nothing was executed (declined / manual-only).</param>
/// <param name="VerifyResult">The re-detect result that decided the outcome, or <c>null</c> when no fix was executed.</param>
public sealed record FixRunResult(FixRunStatus Status, string RawOutput, CheckResult? VerifyResult);
