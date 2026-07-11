using TrSetup.Core.Catalog;
using TrSetup.Core.Checks;

namespace TrSetup.Core.Profiles;

/// <summary>
/// A board <see cref="Check"/> materialized from a declarative <see cref="ProfileRequirement"/>
/// (REQ-FN-021). A presence-style handler supplies the probe logic as a detect delegate; this
/// class renders it as a Framework-core row scoped to the owning app via <see cref="Apps"/> — so
/// an app's presence rows only surface when that app is the selected one.
/// </summary>
public sealed class ProfileCheck : Check
{
    private readonly Func<CancellationToken, Task<CheckResult>> objDetect;
    private readonly string[] objApps;

    /// <summary>
    /// Creates a profile-backed check.
    /// </summary>
    /// <param name="aRequirement">The requirement whose id/title/roles/severity this row renders.</param>
    /// <param name="aProfileName">The owning profile name — the app this row is scoped to.</param>
    /// <param name="aExplanation">What the item is and why the role needs it.</param>
    /// <param name="aDetect">The probe that reports the item's current state.</param>
    /// <exception cref="ArgumentNullException">Thrown when any argument is null.</exception>
    public ProfileCheck(
        ProfileRequirement aRequirement,
        string aProfileName,
        CheckExplanation aExplanation,
        Func<CancellationToken, Task<CheckResult>> aDetect)
    {
        ArgumentNullException.ThrowIfNull(aRequirement);
        ArgumentNullException.ThrowIfNull(aProfileName);
        Id = aRequirement.Id;
        Title = aRequirement.Title;
        Roles = aRequirement.Roles;
        Severity = aRequirement.Severity;
        Explain = aExplanation ?? throw new ArgumentNullException(nameof(aExplanation));
        objDetect = aDetect ?? throw new ArgumentNullException(nameof(aDetect));
        objApps = new[] { aProfileName };
    }

    /// <inheritdoc />
    public override string Id { get; }

    /// <inheritdoc />
    public override string Title { get; }

    /// <inheritdoc />
    public override string Category => BoardCategories.FrameworkCore;

    /// <inheritdoc />
    public override MachineRole Roles { get; }

    /// <inheritdoc />
    public override CheckSeverity Severity { get; }

    /// <inheritdoc />
    public override CheckExplanation Explain { get; }

    /// <inheritdoc />
    public override IReadOnlyCollection<string> Apps => objApps;

    /// <inheritdoc />
    public override Task<CheckResult> DetectAsync(CancellationToken aCancellationToken = default)
        => objDetect(aCancellationToken);
}
