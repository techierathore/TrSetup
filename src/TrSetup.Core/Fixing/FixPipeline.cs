using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using TrSetup.Core.Checks;

namespace TrSetup.Core.Fixing;

/// <summary>
/// The REQ-FN-002 Detect → Preview → Fix → Re-verify pipeline. Fixes only run after the
/// consent provider has shown the fix preview and the user approved; after the fix,
/// <see cref="Check.VerifyAsync"/> re-detects — a verify that does not come back green
/// yields <see cref="FixRunStatus.Failed"/> with the raw output attached, never "assume fixed".
/// </summary>
public sealed class FixPipeline
{
    private readonly IConsentProvider objConsentProvider;
    private readonly ILogger<FixPipeline> objLogger;

    /// <summary>
    /// Creates the pipeline around the consent gate every fix must pass through.
    /// </summary>
    /// <param name="aConsentProvider">The gate that shows the fix preview and collects the user's decision.</param>
    /// <param name="aLogger">Optional logger; a null logger is used when omitted.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="aConsentProvider"/> is null.</exception>
    public FixPipeline(IConsentProvider aConsentProvider, ILogger<FixPipeline>? aLogger = null)
    {
        objConsentProvider = aConsentProvider ?? throw new ArgumentNullException(nameof(aConsentProvider));
        objLogger = aLogger ?? NullLogger<FixPipeline>.Instance;
    }

    /// <summary>
    /// Runs Preview → Consent → Fix → Re-verify for one check.
    /// </summary>
    /// <param name="aCheck">The check to fix.</param>
    /// <param name="aCancellationToken">Cancels the run.</param>
    /// <returns>
    /// <see cref="FixRunStatus.ManualOnly"/> when the check has no fixer;
    /// <see cref="FixRunStatus.Declined"/> when consent was refused (nothing executed);
    /// <see cref="FixRunStatus.Fixed"/> only when the re-verify came back <see cref="CheckStatus.Pass"/>;
    /// otherwise <see cref="FixRunStatus.Failed"/> with the raw fixer output attached.
    /// </returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="aCheck"/> is null.</exception>
    public async Task<FixRunResult> RunAsync(Check aCheck, CancellationToken aCancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(aCheck);

        var vFixer = aCheck.FixAsync;
        if (vFixer is null)
        {
            objLogger.LogInformation("Check {CheckId} is manual-only; no fix executed.", aCheck.Id);
            return new FixRunResult(FixRunStatus.ManualOnly, string.Empty, null);
        }

        var vConsent = await objConsentProvider.RequestConsentAsync(aCheck, aCancellationToken).ConfigureAwait(false);
        if (!vConsent.IsGranted)
        {
            objLogger.LogInformation("Consent declined for check {CheckId}; nothing executed.", aCheck.Id);
            return new FixRunResult(FixRunStatus.Declined, string.Empty, null);
        }

        var vFixResult = await vFixer(vConsent, aCancellationToken).ConfigureAwait(false);
        var vVerify = await aCheck.VerifyAsync(aCancellationToken).ConfigureAwait(false);

        if (vVerify.Status == CheckStatus.Pass)
        {
            objLogger.LogInformation("Check {CheckId} fixed and re-verified green.", aCheck.Id);
            return new FixRunResult(FixRunStatus.Fixed, vFixResult.RawOutput, vVerify);
        }

        objLogger.LogWarning(
            "Check {CheckId} fix did not re-verify green (status {Status}); reporting FAILED.",
            aCheck.Id,
            vVerify.Status);
        return new FixRunResult(FixRunStatus.Failed, vFixResult.RawOutput, vVerify);
    }
}
