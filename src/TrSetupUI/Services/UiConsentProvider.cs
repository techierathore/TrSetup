using TrSetup.Core.Checks;
using TrSetup.Core.Fixing;

namespace TrSetupUI.Services;

/// <summary>
/// The GUI consent gate (REQ-FN-002 / IConsentProvider): surfaces the check whose fix wants
/// to run so the shell can show its <see cref="Check.FixPreview"/> in a modal dialog, then
/// completes with the user's explicit Approve / Decline decision. One request is pending at
/// a time — fixes queue sequentially through the pipeline.
/// </summary>
public sealed class UiConsentProvider : IConsentProvider
{
    private readonly object objGate = new();
    private TaskCompletionSource<bool>? objPendingDecision;

    /// <summary>The check currently awaiting consent, or <c>null</c> when no dialog is needed.</summary>
    public Check? PendingCheck { get; private set; }

    /// <summary>Raised when a consent request opens or closes (the shell re-renders the dialog).</summary>
    public event Action? Changed;

    /// <inheritdoc />
    public async Task<ConsentToken> RequestConsentAsync(Check aCheck, CancellationToken aCancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(aCheck);
        TaskCompletionSource<bool> vDecision;
        lock (objGate)
        {
            if (objPendingDecision is not null)
            {
                throw new InvalidOperationException("A consent request is already pending.");
            }

            vDecision = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            objPendingDecision = vDecision;
            PendingCheck = aCheck;
        }

        Changed?.Invoke();
        using var vRegistration = aCancellationToken.Register(() => vDecision.TrySetResult(false));
        try
        {
            var vIsGranted = await vDecision.Task.ConfigureAwait(false);
            var vPreview = aCheck.FixPreview ?? string.Empty;
            return vIsGranted ? ConsentToken.Granted(vPreview) : ConsentToken.Declined(vPreview);
        }
        finally
        {
            lock (objGate)
            {
                objPendingDecision = null;
                PendingCheck = null;
            }

            Changed?.Invoke();
        }
    }

    /// <summary>Records the user's approval of the pending fix preview.</summary>
    public void Approve() => objPendingDecision?.TrySetResult(true);

    /// <summary>Records the user's refusal of the pending fix preview (nothing executes).</summary>
    public void Decline() => objPendingDecision?.TrySetResult(false);
}
