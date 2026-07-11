namespace TrSetup.Core.Profiles;

/// <summary>
/// A resolved <see cref="ProfileRequirement"/> paired with the merge <see cref="RequirementSource"/>
/// that produced it (REQ-UI-006 Settings profile-details). Produced by
/// <see cref="ProfileLoader.ResolveWithSources"/> — a read-only companion to
/// <see cref="ProfileLoader.Resolve"/> that keeps the built-in vs app-repo-override origin per row.
/// </summary>
/// <param name="Requirement">The resolved requirement instance (the app-repo copy when overridden).</param>
/// <param name="Source">Which side of the merge this requirement came from.</param>
public sealed record ResolvedRequirement(ProfileRequirement Requirement, RequirementSource Source);
