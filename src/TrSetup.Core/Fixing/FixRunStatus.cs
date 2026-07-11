namespace TrSetup.Core.Fixing;

/// <summary>
/// The outcome of one Detect → Preview → Fix → Re-verify pipeline run for a single check.
/// </summary>
public enum FixRunStatus
{
    /// <summary>The fix ran and the re-verify came back green (<c>Pass</c>).</summary>
    Fixed = 0,

    /// <summary>The fix ran but the re-verify did not come back green — raw output is attached, never "assume fixed".</summary>
    Failed = 1,

    /// <summary>The user declined consent after seeing the preview; nothing was executed.</summary>
    Declined = 2,

    /// <summary>The check has no automated fixer (<c>FixAsync</c> is null); guidance only.</summary>
    ManualOnly = 3
}
