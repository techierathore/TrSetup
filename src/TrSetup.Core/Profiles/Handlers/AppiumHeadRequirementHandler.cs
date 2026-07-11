using TrSetup.Core.Checks;

namespace TrSetup.Core.Profiles.Handlers;

/// <summary>
/// Presence handler for the <c>appium-head</c> requirement type (REQ-FN-021): detects an Appium
/// server head is up by probing its <c>/status</c> route (built from param <c>url</c>). A 2xx
/// answer passes; a reachable-but-not-2xx answer warns; no answer fails.
/// </summary>
public sealed class AppiumHeadRequirementHandler : IProfileRequirementHandler
{
    /// <inheritdoc />
    public string Type => ProfileRequirementTypes.AppiumHead;

    /// <inheritdoc />
    public Check CreateCheck(ProfileRequirement aRequirement, ProfileCheckContext aContext)
    {
        ArgumentNullException.ThrowIfNull(aRequirement);
        ArgumentNullException.ThrowIfNull(aContext);
        var vStatusUrl = BuildStatusUrl(aRequirement.Param("url") ?? string.Empty);
        var vExplain = new CheckExplanation(
            $"Whether the Appium head at {vStatusUrl} is up.",
            "The runtime-verification bridge drives the app through this Appium head; if it is down, device verification cannot run.",
            "WORKFLOW §0b");
        return new ProfileCheck(
            aRequirement,
            aContext.ProfileName,
            vExplain,
            aToken => DetectAsync(aContext, vStatusUrl, aToken));
    }

    private static string BuildStatusUrl(string aUrl)
    {
        if (aUrl.EndsWith("/status", StringComparison.OrdinalIgnoreCase))
        {
            return aUrl;
        }

        return aUrl.TrimEnd('/') + "/status";
    }

    private static async Task<CheckResult> DetectAsync(ProfileCheckContext aContext, string aStatusUrl, CancellationToken aToken)
    {
        var vProbe = await aContext.HttpProbe.GetAsync(aStatusUrl, aToken).ConfigureAwait(false);
        if (!vProbe.IsReachable)
        {
            return CheckResult.Fail($"Appium head {aStatusUrl} is unreachable ({vProbe.Error}).");
        }

        return vProbe.IsSuccess
            ? CheckResult.Pass($"Appium head answered {vProbe.StatusCode} at {aStatusUrl}.")
            : CheckResult.Warn($"Appium head {aStatusUrl} answered {vProbe.StatusCode} (reachable but not ready).");
    }
}
