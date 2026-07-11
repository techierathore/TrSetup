using TrSetup.Core.Checks;

namespace TrSetup.Core.Profiles.Handlers;

/// <summary>
/// Handler for the <c>runtime-install</c> requirement type (REQ-FN-025): turns a declarative
/// managed-runtime requirement into a <see cref="RuntimeInstallCheck"/>. Params: <c>runtime</c>
/// (required; e.g. <c>comfyui</c>), <c>releaseTag</c> (optional; pins the GitHub release).
/// </summary>
public sealed class RuntimeInstallRequirementHandler : IProfileRequirementHandler
{
    /// <inheritdoc />
    public string Type => ProfileRequirementTypes.RuntimeInstall;

    /// <inheritdoc />
    public Check CreateCheck(ProfileRequirement aRequirement, ProfileCheckContext aContext)
    {
        ArgumentNullException.ThrowIfNull(aRequirement);
        ArgumentNullException.ThrowIfNull(aContext);
        return new RuntimeInstallCheck(aRequirement, aContext.ProfileName, aContext.ProcessRunner, aContext.FixServices);
    }
}
