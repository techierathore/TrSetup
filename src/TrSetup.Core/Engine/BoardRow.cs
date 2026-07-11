using TrSetup.Core.Checks;

namespace TrSetup.Core.Engine;

/// <summary>
/// One row of the board model: a check plus its latest detected status and evidence.
/// Out-of-scope rows are <see cref="CheckStatus.NotApplicable"/> and are never failures.
/// </summary>
public sealed class BoardRow
{
    internal BoardRow(Check aCheck, bool aIsInScope, string aOutOfScopeReason)
    {
        Check = aCheck;
        IsInScope = aIsInScope;
        if (!aIsInScope)
        {
            Status = CheckStatus.NotApplicable;
            Evidence = aOutOfScopeReason;
        }
    }

    /// <summary>The check this row renders.</summary>
    public Check Check { get; }

    /// <summary>Whether the check is in scope for the board's roles ∩ selected app.</summary>
    public bool IsInScope { get; }

    /// <summary>
    /// The latest detected status, or <c>null</c> while an in-scope check has not been
    /// detected yet (streaming sweep still running).
    /// </summary>
    public CheckStatus? Status { get; private set; }

    /// <summary>The evidence backing <see cref="Status"/>; empty until first detection.</summary>
    public string Evidence { get; private set; } = string.Empty;

    /// <summary>When the row was last detected (UTC), or <c>null</c> when never detected.</summary>
    public DateTimeOffset? LastDetectedAt { get; private set; }

    /// <summary>
    /// Whether the row renders as a failure. <see cref="CheckStatus.NotApplicable"/> rows
    /// never do — only an in-scope <see cref="CheckStatus.Fail"/> is a failure.
    /// </summary>
    public bool IsFailure => IsInScope && Status == CheckStatus.Fail;

    internal void UpdateResult(CheckResult aResult)
    {
        Status = aResult.Status;
        Evidence = aResult.Evidence;
        LastDetectedAt = DateTimeOffset.UtcNow;
    }
}
