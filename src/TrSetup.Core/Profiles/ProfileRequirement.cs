using TrSetup.Core.Checks;

namespace TrSetup.Core.Profiles;

/// <summary>
/// One declarative requirement instance from a <c>trsetup-profile.json</c> (REQ-FN-021): a
/// generic, role-tagged item of a machine's environment (an SDK, a CLI tool, an endpoint, ...)
/// that the profile loader turns into a board <see cref="Check"/> via a type handler.
/// </summary>
/// <remarks>
/// The <see cref="Id"/> is the override-merge identity (app-repo requirement whose id matches a
/// built-in one REPLACES it) and becomes the resulting check's <see cref="Check.Id"/>. Type-specific
/// fields live in the flexible <see cref="Params"/> bag so new fields never change the model — each
/// type handler documents (and validates) the keys it reads.
/// </remarks>
public sealed class ProfileRequirement
{
    /// <summary>
    /// Creates a requirement instance.
    /// </summary>
    /// <param name="aType">One of the <see cref="ProfileRequirementTypes"/> strings.</param>
    /// <param name="aId">Stable id, unique within the profile (e.g. <c>appstudio.dotnet-sdk</c>).</param>
    /// <param name="aTitle">Short human title rendered on the board row.</param>
    /// <param name="aRoles">The machine roles this requirement applies to (combinable flags).</param>
    /// <param name="aSeverity">How important the requirement is for its roles.</param>
    /// <param name="aParams">Type-specific parameters (url, version, envVar, ...); never null.</param>
    /// <exception cref="ArgumentNullException">Thrown when any reference argument is null.</exception>
    public ProfileRequirement(
        string aType,
        string aId,
        string aTitle,
        MachineRole aRoles,
        CheckSeverity aSeverity,
        IReadOnlyDictionary<string, string> aParams)
    {
        Type = aType ?? throw new ArgumentNullException(nameof(aType));
        Id = aId ?? throw new ArgumentNullException(nameof(aId));
        Title = aTitle ?? throw new ArgumentNullException(nameof(aTitle));
        Roles = aRoles;
        Severity = aSeverity;
        Params = aParams ?? throw new ArgumentNullException(nameof(aParams));
    }

    /// <summary>The requirement type — one of the <see cref="ProfileRequirementTypes"/> strings.</summary>
    public string Type { get; }

    /// <summary>Stable id, unique within the profile; the override-merge identity and resulting check id.</summary>
    public string Id { get; }

    /// <summary>Short human title rendered on the board row.</summary>
    public string Title { get; }

    /// <summary>The machine roles this requirement applies to (combinable flags — BRD-35).</summary>
    public MachineRole Roles { get; }

    /// <summary>How important the requirement is for the roles it applies to.</summary>
    public CheckSeverity Severity { get; }

    /// <summary>
    /// Type-specific parameters read by the requirement's handler (case-insensitive keys):
    /// e.g. <c>version</c>, <c>command</c>, <c>url</c>, <c>envVar</c>, <c>feed</c>, <c>floorGb</c>.
    /// </summary>
    public IReadOnlyDictionary<string, string> Params { get; }

    /// <summary>
    /// Reads a parameter value, returning <c>null</c> when the key is absent or blank.
    /// </summary>
    /// <param name="aKey">The parameter key (case-insensitive).</param>
    /// <returns>The trimmed value, or <c>null</c> when absent/blank.</returns>
    public string? Param(string aKey)
    {
        if (Params.TryGetValue(aKey, out var vValue) && !string.IsNullOrWhiteSpace(vValue))
        {
            return vValue.Trim();
        }

        return null;
    }
}
