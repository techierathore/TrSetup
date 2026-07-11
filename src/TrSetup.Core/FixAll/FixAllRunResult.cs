namespace TrSetup.Core.FixAll;

/// <summary>
/// The complete outcome of one fix-all run (REQ-FN-019): every step's result in the
/// dependency-ordered execution order, plus whether (and why) the run halted early.
/// </summary>
/// <param name="Steps">Per-step results in execution order; halted/skipped steps carry a reason and no pipeline result.</param>
/// <param name="Halted">Whether the run stopped before attempting every step (declined consent, or stop-on-failure).</param>
/// <param name="HaltReason">Why the run halted, or <c>null</c> when it ran to the end of the plan.</param>
public sealed record FixAllRunResult(
    IReadOnlyList<FixAllStepResult> Steps,
    bool Halted,
    string? HaltReason)
{
    /// <summary>Whether every step ended green — fixed or manual-only — with nothing failed, declined or skipped.</summary>
    public bool AllGreen =>
        !Halted &&
        Steps.All(aStep => aStep.Status is FixAllStepStatus.Fixed or FixAllStepStatus.ManualOnly);
}
