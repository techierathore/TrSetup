namespace TrSetup.Core.FixAll;

/// <summary>
/// One live progress update streamed while a fix-all run executes (REQ-FN-019), so the
/// fix-run UI can render per-step status as it happens.
/// </summary>
/// <param name="CheckId">Id of the check the step wraps.</param>
/// <param name="StepNumber">1-based position of the step in the dependency-ordered plan.</param>
/// <param name="TotalSteps">Total number of steps in the plan.</param>
/// <param name="Phase">Whether the step is starting or has completed.</param>
/// <param name="Result">The step's outcome; <c>null</c> while the step is starting.</param>
public sealed record FixAllStepUpdate(
    string CheckId,
    int StepNumber,
    int TotalSteps,
    FixAllStepPhase Phase,
    FixAllStepResult? Result);
