namespace TrSetup.Core.FixAll;

/// <summary>
/// What a fix-all run does when a step's fix fails its re-verify (REQ-FN-019).
/// Declined consent always halts the whole run regardless of this policy.
/// </summary>
public enum FixAllFailurePolicy
{
    /// <summary>Stop the run at the first failed step; later steps are left untouched.</summary>
    StopOnFailure = 0,

    /// <summary>Keep going after a failed step; steps depending on the failed one are skipped.</summary>
    ContinueOnFailure = 1
}
