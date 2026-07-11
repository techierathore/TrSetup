using TrSetup.Core.Checks;

namespace TrSetup.Core.Profiles.Handlers;

/// <summary>
/// Base for the heavy profile-requirement checks (Cluster C: <c>service</c>, <c>runtime-install</c>,
/// <c>disk-space</c>). Unlike a presence-style <see cref="ProfileCheck"/>, a heavy check subclasses
/// <see cref="Check"/> directly so it can expose a real Fix button (<see cref="Check.FixAsync"/>).
/// This base carries the requirement-derived identity (<see cref="Id"/>, <see cref="Title"/>,
/// <see cref="Roles"/>, <see cref="Severity"/>) and scopes the row to the owning app via
/// <see cref="Apps"/> — the concrete subclass supplies category, explanation, detect and fix.
/// </summary>
public abstract class ProfileHeavyCheck : Check
{
    private readonly string[] objApps;

    /// <summary>
    /// Creates the heavy check.
    /// </summary>
    /// <param name="aRequirement">The requirement whose id/title/roles/severity this row renders.</param>
    /// <param name="aProfileName">The owning profile name — the app this row is scoped to.</param>
    /// <exception cref="ArgumentNullException">Thrown when any argument is null.</exception>
    protected ProfileHeavyCheck(ProfileRequirement aRequirement, string aProfileName)
    {
        Requirement = aRequirement ?? throw new ArgumentNullException(nameof(aRequirement));
        ArgumentNullException.ThrowIfNull(aProfileName);
        Id = aRequirement.Id;
        Title = aRequirement.Title;
        Roles = aRequirement.Roles;
        Severity = aRequirement.Severity;
        objApps = new[] { aProfileName };
    }

    /// <summary>The declarative requirement backing this row (its params drive detect/fix).</summary>
    protected ProfileRequirement Requirement { get; }

    /// <inheritdoc />
    public override string Id { get; }

    /// <inheritdoc />
    public override string Title { get; }

    /// <inheritdoc />
    public override MachineRole Roles { get; }

    /// <inheritdoc />
    public override CheckSeverity Severity { get; }

    /// <inheritdoc />
    public override IReadOnlyCollection<string> Apps => objApps;
}
