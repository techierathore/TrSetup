using TrSetup.Core.Checks;

namespace TrSetup.Core.Profiles.Handlers;

/// <summary>
/// Handler for the <c>disk-space</c> requirement type (REQ-FN-029): turns a declarative floor
/// requirement into a <see cref="DiskSpaceCheck"/>. Params: <c>floorGb</c> (required integer GB),
/// <c>path</c> (optional; default the TrSetup-managed root drive). The check warns — never fails —
/// on a breach and has no fixer.
/// </summary>
public sealed class DiskSpaceRequirementHandler : IProfileRequirementHandler
{
    /// <inheritdoc />
    public string Type => ProfileRequirementTypes.DiskSpace;

    /// <inheritdoc />
    public Check CreateCheck(ProfileRequirement aRequirement, ProfileCheckContext aContext)
    {
        ArgumentNullException.ThrowIfNull(aRequirement);
        ArgumentNullException.ThrowIfNull(aContext);
        return new DiskSpaceCheck(aRequirement, aContext.ProfileName);
    }
}
