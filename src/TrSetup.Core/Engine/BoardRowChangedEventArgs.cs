namespace TrSetup.Core.Engine;

/// <summary>
/// Raised by <see cref="CheckBoard.RowChanged"/> whenever a row's status/evidence updates,
/// so every head (Blazor, TUI) can re-render incrementally while a sweep streams in.
/// </summary>
public sealed class BoardRowChangedEventArgs : EventArgs
{
    internal BoardRowChangedEventArgs(BoardRow aRow)
    {
        Row = aRow;
    }

    /// <summary>The row whose status or evidence just changed.</summary>
    public BoardRow Row { get; }
}
