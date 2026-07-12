using TrSetup.Core.Checks;
using TrSetup.Core.Fixing;

namespace TrSetup.Core.Tests.TestDoubles;

/// <summary>
/// Scripted consent gate: grants or declines every request and records the fix preview
/// it was asked to display, so tests can assert the preview+consent flow.
/// </summary>
public sealed class FakeConsentProvider : IConsentProvider
{
    private readonly bool objGrant;

    /// <summary>
    /// Creates the provider.
    /// </summary>
    /// <param name="aGrant">Whether every consent request is granted.</param>
    public FakeConsentProvider(bool aGrant)
    {
        objGrant = aGrant;
    }

    /// <summary>The fix preview of the last check consent was requested for, or null when never asked.</summary>
    public string? LastPreviewShown { get; private set; }

    /// <summary>How many times consent was requested.</summary>
    public int RequestCount { get; private set; }

    /// <inheritdoc />
    public Task<ConsentToken> RequestConsentAsync(Check aCheck, CancellationToken aCancellationToken = default)
    {
        RequestCount++;
        LastPreviewShown = aCheck.FixPreview;
        var vPreview = aCheck.FixPreview ?? string.Empty;
        return Task.FromResult(objGrant ? ConsentToken.Granted(vPreview) : ConsentToken.Declined(vPreview));
    }
}
