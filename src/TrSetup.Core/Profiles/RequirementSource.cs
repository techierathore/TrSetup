namespace TrSetup.Core.Profiles;

/// <summary>
/// The origin of a resolved requirement in the built-in vs app-repo-override merge (REQ-FN-021):
/// surfaced read-only for the Settings profile-details pane (REQ-UI-006) so the UI can show which
/// side won for each row. This is a display concern only — it never changes the merge itself.
/// </summary>
public enum RequirementSource
{
    /// <summary>The requirement came from the built-in profile and was not overridden by the app repo.</summary>
    BuiltIn,

    /// <summary>
    /// The requirement came from the app-repo <c>.tfcore/trsetup-profile.json</c> override — either it
    /// replaced a built-in of the same id, or it is a new id introduced only by the app repo (app repo wins).
    /// </summary>
    Override
}
