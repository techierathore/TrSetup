using TrSetup.Core.Checks;

namespace TrSetup.Core.Profiles.Handlers;

/// <summary>
/// Handler for the <c>service</c> requirement type (REQ-FN-026): turns a declarative service
/// requirement into a <see cref="ServiceCheck"/> that branches on the <c>service</c> param.
/// Params: <c>service</c> (required; <c>postgres</c> or <c>ffmpeg</c>), <c>port</c> (optional,
/// postgres, default <c>5432</c>), <c>extension</c> (optional, postgres, default <c>vector</c>).
/// </summary>
public sealed class ServiceRequirementHandler : IProfileRequirementHandler
{
    /// <inheritdoc />
    public string Type => ProfileRequirementTypes.Service;

    /// <inheritdoc />
    public Check CreateCheck(ProfileRequirement aRequirement, ProfileCheckContext aContext)
    {
        ArgumentNullException.ThrowIfNull(aRequirement);
        ArgumentNullException.ThrowIfNull(aContext);
        return new ServiceCheck(aRequirement, aContext.ProfileName, aContext.ProcessRunner, aContext.FixServices);
    }
}
