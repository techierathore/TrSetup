using TrSetup.Core.Checks;

namespace TrSetup.Core.Profiles.Handlers;

/// <summary>
/// Presence handler for the <c>nuget-feed</c> requirement type (REQ-FN-021): detects a NuGet feed
/// is reachable via a bounded GET (param <c>url</c>) and, when the feed needs a PAT (param
/// <c>patEnvVar</c>), that the token env var is present and non-empty. The PAT is handled
/// <b>presence-only</b> (ADR-008) — its value is never read, logged, or shown.
/// </summary>
public sealed class NugetFeedRequirementHandler : IProfileRequirementHandler
{
    /// <inheritdoc />
    public string Type => ProfileRequirementTypes.NugetFeed;

    /// <inheritdoc />
    public Check CreateCheck(ProfileRequirement aRequirement, ProfileCheckContext aContext)
    {
        ArgumentNullException.ThrowIfNull(aRequirement);
        ArgumentNullException.ThrowIfNull(aContext);
        var vUrl = aRequirement.Param("url") ?? string.Empty;
        var vPatEnvVar = aRequirement.Param("patEnvVar");
        var vExplain = new CheckExplanation(
            $"Reachability of the NuGet feed {vUrl}{(vPatEnvVar is null ? string.Empty : " (auth token required)")}.",
            "Restore pulls this app's packages from the feed; an unreachable feed or missing auth token breaks restore.",
            "WORKFLOW §0");
        return new ProfileCheck(
            aRequirement,
            aContext.ProfileName,
            vExplain,
            aToken => DetectAsync(aContext, vUrl, vPatEnvVar, aToken));
    }

    private static async Task<CheckResult> DetectAsync(
        ProfileCheckContext aContext,
        string aUrl,
        string? aPatEnvVar,
        CancellationToken aToken)
    {
        var vProbe = await aContext.HttpProbe.GetAsync(aUrl, aToken).ConfigureAwait(false);
        if (!vProbe.IsReachable)
        {
            return CheckResult.Fail($"NuGet feed {aUrl} is unreachable ({vProbe.Error}).");
        }

        if (aPatEnvVar is not null)
        {
            var vHasToken = !string.IsNullOrWhiteSpace(aContext.SystemProbe.GetEnvironmentVariable(aPatEnvVar));
            if (!vHasToken)
            {
                return CheckResult.Warn(
                    $"NuGet feed {aUrl} is reachable (answered {vProbe.StatusCode}) but the PAT env var '{aPatEnvVar}' is not set.");
            }

            return CheckResult.Pass(
                $"NuGet feed {aUrl} reachable (answered {vProbe.StatusCode}); PAT env var '{aPatEnvVar}' is present.");
        }

        return CheckResult.Pass($"NuGet feed {aUrl} reachable (answered {vProbe.StatusCode}).");
    }
}
