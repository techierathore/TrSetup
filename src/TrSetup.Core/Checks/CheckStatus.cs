namespace TrSetup.Core.Checks;

/// <summary>
/// The outcome of a detect (or re-verify) run for a single check.
/// </summary>
public enum CheckStatus
{
    /// <summary>The item is present and correctly configured.</summary>
    Pass = 0,

    /// <summary>The item works but something is degraded or advisory (e.g. disk-space floor breached).</summary>
    Warn = 1,

    /// <summary>The item is missing or misconfigured; a fix (auto or manual) is needed.</summary>
    Fail = 2,

    /// <summary>The check is out of scope for this machine's roles / selected app; never rendered as a failure.</summary>
    NotApplicable = 3
}
