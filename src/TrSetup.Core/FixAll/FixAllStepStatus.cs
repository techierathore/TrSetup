namespace TrSetup.Core.FixAll;

/// <summary>
/// The outcome of one step inside a fix-all run (REQ-FN-019).
/// </summary>
public enum FixAllStepStatus
{
    /// <summary>The step's fix ran and its re-verify came back green.</summary>
    Fixed = 0,

    /// <summary>The step's fix ran but the re-verify did not come back green.</summary>
    Failed = 1,

    /// <summary>The user declined consent for this step; the whole run halted here.</summary>
    Declined = 2,

    /// <summary>The step's check has no automated fixer; guidance only, the run continues.</summary>
    ManualOnly = 3,

    /// <summary>The step never ran — the run halted earlier, or a dependency of this step failed.</summary>
    Skipped = 4
}
