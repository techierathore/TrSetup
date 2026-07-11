using TrSetup.Core.Checks;

namespace TrSetup.Core.Engine;

/// <summary>
/// The one observable board model every head renders (REQ-FN-004): the full catalog for a
/// (roles, selected app) scope, grouped by category, with live row updates streamed via
/// <see cref="RowChanged"/> while a detect sweep runs.
/// </summary>
public sealed class CheckBoard
{
    internal CheckBoard(MachineRole aRoles, string? aSelectedApp, IReadOnlyList<BoardGroup> aGroups)
    {
        Roles = aRoles;
        SelectedApp = aSelectedApp;
        Groups = aGroups;
    }

    /// <summary>The machine roles this board was scoped to.</summary>
    public MachineRole Roles { get; }

    /// <summary>The selected app profile this board was scoped to, or <c>null</c> when none.</summary>
    public string? SelectedApp { get; }

    /// <summary>The board groups (categories) in catalog order.</summary>
    public IReadOnlyList<BoardGroup> Groups { get; }

    /// <summary>All rows across all groups, in catalog order.</summary>
    public IEnumerable<BoardRow> Rows => Groups.SelectMany(aGroup => aGroup.Rows);

    /// <summary>Raised whenever a row's status/evidence updates (streaming detect sweeps).</summary>
    public event EventHandler<BoardRowChangedEventArgs>? RowChanged;

    internal void ApplyResult(BoardRow aRow, CheckResult aResult)
    {
        aRow.UpdateResult(aResult);
        RowChanged?.Invoke(this, new BoardRowChangedEventArgs(aRow));
    }
}
