namespace TrSetup.Core.FixAll;

/// <summary>
/// Which moment of a step a streamed <see cref="FixAllStepUpdate"/> reports (REQ-FN-019).
/// </summary>
public enum FixAllStepPhase
{
    /// <summary>The step is about to run (consent gate + fix + re-verify follow).</summary>
    Starting = 0,

    /// <summary>The step finished; the update carries its <see cref="FixAllStepResult"/>.</summary>
    Completed = 1
}
