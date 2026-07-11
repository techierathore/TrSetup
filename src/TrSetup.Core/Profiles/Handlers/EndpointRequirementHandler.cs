using TrSetup.Core.Checks;

namespace TrSetup.Core.Profiles.Handlers;

/// <summary>
/// Presence handler for the <c>endpoint</c> requirement type (REQ-FN-021): detects an HTTP
/// endpoint's reachability via a bounded GET (param <c>url</c>). A 2xx answer passes; any other
/// answer warns (reachable but not healthy); no answer fails.
/// </summary>
public sealed class EndpointRequirementHandler : IProfileRequirementHandler
{
    /// <inheritdoc />
    public string Type => ProfileRequirementTypes.Endpoint;

    /// <inheritdoc />
    public Check CreateCheck(ProfileRequirement aRequirement, ProfileCheckContext aContext)
    {
        ArgumentNullException.ThrowIfNull(aRequirement);
        ArgumentNullException.ThrowIfNull(aContext);
        var vUrl = aRequirement.Param("url") ?? string.Empty;
        var vExplain = new CheckExplanation(
            $"Reachability of the endpoint {vUrl} this app depends on.",
            "The app calls this endpoint at runtime; if it is unreachable the dependent workflow fails.",
            "WORKFLOW §0");
        return new ProfileCheck(
            aRequirement,
            aContext.ProfileName,
            vExplain,
            aToken => DetectAsync(aContext, vUrl, aToken));
    }

    private static async Task<CheckResult> DetectAsync(ProfileCheckContext aContext, string aUrl, CancellationToken aToken)
    {
        var vProbe = await aContext.HttpProbe.GetAsync(aUrl, aToken).ConfigureAwait(false);
        if (!vProbe.IsReachable)
        {
            return CheckResult.Fail($"Endpoint {aUrl} is unreachable ({vProbe.Error}).");
        }

        return vProbe.IsSuccess
            ? CheckResult.Pass($"Endpoint {aUrl} answered {vProbe.StatusCode}.")
            : CheckResult.Warn($"Endpoint {aUrl} answered {vProbe.StatusCode} (reachable but not a success status).");
    }
}
