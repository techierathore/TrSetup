using TrSetup.Core.Checks;
using TrSetup.Core.Fixing;

namespace TrSetup.Core.Tests;

/// <summary>
/// Consent gate scripted per check id for fix-all tests: grants every request except the
/// check ids it was told to decline, and records the order consent was requested in.
/// </summary>
public sealed class FixAllScriptedConsentProvider : IConsentProvider
{
    private readonly HashSet<string> objDeclinedIds;

    /// <summary>
    /// Creates the provider.
    /// </summary>
    /// <param name="aDeclinedIds">Check ids whose consent requests are declined; all others are granted.</param>
    public FixAllScriptedConsentProvider(params string[] aDeclinedIds)
    {
        objDeclinedIds = new HashSet<string>(aDeclinedIds, StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>The check ids consent was requested for, in request order.</summary>
    public List<string> RequestedIds { get; } = new();

    /// <inheritdoc />
    public Task<ConsentToken> RequestConsentAsync(Check aCheck, CancellationToken aCancellationToken = default)
    {
        RequestedIds.Add(aCheck.Id);
        var vPreview = aCheck.FixPreview ?? string.Empty;
        return Task.FromResult(objDeclinedIds.Contains(aCheck.Id)
            ? ConsentToken.Declined(vPreview)
            : ConsentToken.Granted(vPreview));
    }
}
