using TrSetup.Core.Checks;

namespace TrSetup.Core.Profiles;

/// <summary>
/// Turns a resolved <see cref="TrSetupProfile"/> into its board <see cref="Check"/> rows
/// (REQ-FN-021), one per requirement, dispatching each to its type handler. A requirement whose
/// type has no registered handler yields a graceful failing placeholder row — an unregistered
/// type is always visible on the board, never a crash.
/// </summary>
public sealed class ProfileCheckFactory
{
    private readonly ProfileRequirementHandlerRegistry objHandlers;

    /// <summary>
    /// Creates the factory.
    /// </summary>
    /// <param name="aHandlers">The handler registry, or <c>null</c> to use <see cref="ProfileRequirementHandlerRegistry.CreateDefault"/>.</param>
    public ProfileCheckFactory(ProfileRequirementHandlerRegistry? aHandlers = null)
    {
        objHandlers = aHandlers ?? ProfileRequirementHandlerRegistry.CreateDefault();
    }

    /// <summary>
    /// Builds the checks for every requirement in the profile.
    /// </summary>
    /// <param name="aProfile">The resolved (merged) profile.</param>
    /// <param name="aContext">The collaborators the handlers build against; its profile name scopes the rows.</param>
    /// <returns>One check per requirement, in profile order.</returns>
    /// <exception cref="ArgumentNullException">Thrown when any argument is null.</exception>
    public IReadOnlyList<Check> CreateChecks(TrSetupProfile aProfile, ProfileCheckContext aContext)
    {
        ArgumentNullException.ThrowIfNull(aProfile);
        ArgumentNullException.ThrowIfNull(aContext);
        var vChecks = new List<Check>(aProfile.Requirements.Count);
        foreach (var vRequirement in aProfile.Requirements)
        {
            var vHandler = objHandlers.Find(vRequirement.Type);
            vChecks.Add(vHandler is null
                ? BuildPlaceholder(vRequirement, aContext.ProfileName)
                : vHandler.CreateCheck(vRequirement, aContext));
        }

        return vChecks;
    }

    private static Check BuildPlaceholder(ProfileRequirement aRequirement, string aProfileName)
    {
        var vEvidence = $"No handler registered for requirement type '{aRequirement.Type}'.";
        var vExplain = new CheckExplanation(
            $"A '{aRequirement.Type}' requirement declared by the profile.",
            "No handler is registered for this requirement type, so it cannot be probed on this build.",
            null);
        return new ProfileCheck(
            aRequirement,
            aProfileName,
            vExplain,
            _ => Task.FromResult(CheckResult.Fail(vEvidence)));
    }
}
