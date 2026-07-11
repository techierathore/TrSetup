namespace TrSetup.Core.Profiles;

/// <summary>
/// A resolved declarative profile (REQ-FN-021): a named set of role-tagged
/// <see cref="ProfileRequirement"/> instances. Produced by the <see cref="ProfileLoader"/> after
/// merging the built-in profile with any app-repo <c>.tfcore/trsetup-profile.json</c> override.
/// </summary>
public sealed class TrSetupProfile
{
    /// <summary>
    /// Creates a profile.
    /// </summary>
    /// <param name="aName">The profile name (matches <see cref="Settings.TrSetupSettings.SelectedApp"/>, e.g. <c>AppStudio</c>).</param>
    /// <param name="aRequirements">The requirement instances the profile declares.</param>
    /// <exception cref="ArgumentNullException">Thrown when any argument is null.</exception>
    public TrSetupProfile(string aName, IReadOnlyList<ProfileRequirement> aRequirements)
    {
        Name = aName ?? throw new ArgumentNullException(nameof(aName));
        Requirements = aRequirements ?? throw new ArgumentNullException(nameof(aRequirements));
    }

    /// <summary>The profile name — the app the requirements scope to (drives <see cref="Checks.Check.Apps"/>).</summary>
    public string Name { get; }

    /// <summary>The role-tagged requirement instances the profile declares.</summary>
    public IReadOnlyList<ProfileRequirement> Requirements { get; }
}
