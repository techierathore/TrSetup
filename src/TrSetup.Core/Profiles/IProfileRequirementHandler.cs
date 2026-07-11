using TrSetup.Core.Checks;

namespace TrSetup.Core.Profiles;

/// <summary>
/// Turns a declarative <see cref="ProfileRequirement"/> of one <see cref="Type"/> into a concrete
/// board <see cref="Check"/> (REQ-FN-021). One handler per requirement type; registered in the
/// <see cref="ProfileRequirementHandlerRegistry"/>. This is the extension seam Cluster C plugs the
/// heavy types (<c>service</c>, <c>runtime-install</c>, <c>disk-space</c>) into.
/// </summary>
public interface IProfileRequirementHandler
{
    /// <summary>The requirement type this handler builds (a <see cref="ProfileRequirementTypes"/> string).</summary>
    string Type { get; }

    /// <summary>
    /// Builds the board check for the given requirement.
    /// </summary>
    /// <param name="aRequirement">The requirement instance (already schema-validated for this type).</param>
    /// <param name="aContext">The collaborators (process runner, probes, fixers, settings) to build against.</param>
    /// <returns>The concrete check rendered as a board row.</returns>
    Check CreateCheck(ProfileRequirement aRequirement, ProfileCheckContext aContext);
}
