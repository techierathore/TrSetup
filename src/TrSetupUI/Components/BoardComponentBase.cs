using Microsoft.AspNetCore.Components;
using TrSetupUI.Services;

namespace TrSetupUI.Components;

/// <summary>
/// Base for every board-bound page/component (REQ-UI-001..005): injects the per-circuit
/// <see cref="BoardState"/>, subscribes to its <see cref="BoardState.Changed"/> event to
/// re-render, ensures the board is initialized, and redirects to <c>/setup</c> on first run.
/// </summary>
public abstract class BoardComponentBase : ComponentBase, IDisposable
{
    /// <summary>The per-circuit board/UI state every page binds to.</summary>
    [Inject]
    protected BoardState Board { get; set; } = default!;

    /// <summary>Navigation used for the first-run redirect and deep links.</summary>
    [Inject]
    protected NavigationManager Nav { get; set; } = default!;

    /// <summary>Whether this component is the setup screen (which must not redirect to itself).</summary>
    protected virtual bool IsSetupPage => false;

    /// <inheritdoc />
    protected override async Task OnInitializedAsync()
    {
        Board.Changed += OnBoardChanged;
        if (Board.NeedsSetup && !IsSetupPage)
        {
            Nav.NavigateTo("/setup");
            return;
        }

        await Board.EnsureInitializedAsync();
    }

    /// <summary>Releases the <see cref="BoardState.Changed"/> subscription.</summary>
    public void Dispose()
    {
        Board.Changed -= OnBoardChanged;
        GC.SuppressFinalize(this);
    }

    private void OnBoardChanged() => InvokeAsync(StateHasChanged);
}
