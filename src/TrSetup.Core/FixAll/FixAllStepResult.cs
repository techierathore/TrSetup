using TrSetup.Core.Fixing;

namespace TrSetup.Core.FixAll;

/// <summary>
/// The recorded outcome of one step in a fix-all run (REQ-FN-019).
/// </summary>
/// <param name="CheckId">Id of the check the step wrapped.</param>
/// <param name="Status">How the step ended.</param>
/// <param name="PipelineResult">The full pipeline evidence for the step, or <c>null</c> when the step never ran (skipped).</param>
/// <param name="Reason">Human-readable reason — why the step was skipped, or a short outcome note.</param>
public sealed record FixAllStepResult(
    string CheckId,
    FixAllStepStatus Status,
    FixRunResult? PipelineResult,
    string Reason);
