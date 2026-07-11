using TrSetup.Core.Checks;

namespace TrSetup.Core.Engine;

/// <summary>
/// One group on the board (a check category such as "Framework core" or "Bridges")
/// with its rows and live status counts.
/// </summary>
public sealed class BoardGroup
{
    internal BoardGroup(string aName, IReadOnlyList<BoardRow> aRows)
    {
        Name = aName;
        Rows = aRows;
    }

    /// <summary>The group name (the checks' <see cref="Check.Category"/>).</summary>
    public string Name { get; }

    /// <summary>The rows in this group, in catalog order.</summary>
    public IReadOnlyList<BoardRow> Rows { get; }

    /// <summary>Number of rows currently passing.</summary>
    public int PassCount => Rows.Count(aRow => aRow.Status == CheckStatus.Pass);

    /// <summary>Number of rows currently warning.</summary>
    public int WarnCount => Rows.Count(aRow => aRow.Status == CheckStatus.Warn);

    /// <summary>Number of rows currently failing (in-scope fails only).</summary>
    public int FailCount => Rows.Count(aRow => aRow.IsFailure);

    /// <summary>Number of rows out of scope (<see cref="CheckStatus.NotApplicable"/>).</summary>
    public int NotApplicableCount => Rows.Count(aRow => aRow.Status == CheckStatus.NotApplicable);
}
