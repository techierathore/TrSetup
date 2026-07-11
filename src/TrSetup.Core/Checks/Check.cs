namespace TrSetup.Core.Checks;

/// <summary>
/// One row on the setup board: a single verifiable item of the machine's environment
/// (an SDK, a tool, a service, an endpoint, ...). Implementations detect the item's state,
/// explain it, and optionally know how to fix it.
/// </summary>
/// <remarks>
/// This is the REQ-FN-001 contract: <c>Id, Title, Category, Roles, Severity, DetectAsync,
/// Explain, FixAsync?, FixPreview, VerifyAsync</c>. A <c>null</c> <see cref="FixAsync"/> means
/// the item is manual-only (the UI shows "Open guide" instead of a Fix button).
/// </remarks>
public abstract class Check
{
    /// <summary>Stable unique identifier of the check (e.g. <c>wsl.dotnet-sdk</c>); used for deep links and JSON output.</summary>
    public abstract string Id { get; }

    /// <summary>Short human title rendered on the board row.</summary>
    public abstract string Title { get; }

    /// <summary>Board group the row renders under (e.g. "Framework core", "Bridges").</summary>
    public abstract string Category { get; }

    /// <summary>The machine roles this check applies to (flags — a check may span several roles).</summary>
    public abstract MachineRole Roles { get; }

    /// <summary>How important the item is for the roles it applies to.</summary>
    public abstract CheckSeverity Severity { get; }

    /// <summary>What the item is, why it matters, and where the authoritative doc lives.</summary>
    public abstract CheckExplanation Explain { get; }

    /// <summary>
    /// App profiles this check belongs to (e.g. <c>AppStudio</c>). Empty means the check is
    /// framework-level and applies regardless of the selected app.
    /// </summary>
    public virtual IReadOnlyCollection<string> Apps => Array.Empty<string>();

    /// <summary>
    /// The literal commands / download URLs the fix would execute, shown verbatim in the
    /// consent preview. <c>null</c> when the check is manual-only.
    /// </summary>
    public virtual string? FixPreview => null;

    /// <summary>
    /// The automated fix, or <c>null</c> when the item can only be fixed manually
    /// (the UI then shows guidance instead of a Fix button).
    /// </summary>
    public virtual CheckFix? FixAsync => null;

    /// <summary>Whether this check has no automated fixer and only offers guidance.</summary>
    public bool IsManualOnly => FixAsync is null;

    /// <summary>
    /// Probes the machine and reports the item's current state with evidence.
    /// </summary>
    /// <param name="aCancellationToken">Cancels the probe (the engine applies a probe timeout).</param>
    /// <returns>The detected status plus the evidence that produced it.</returns>
    public abstract Task<CheckResult> DetectAsync(CancellationToken aCancellationToken = default);

    /// <summary>
    /// Re-detects after a fix ran. Defaults to <see cref="DetectAsync"/> — a fix is only
    /// considered successful when this comes back <see cref="CheckStatus.Pass"/>.
    /// </summary>
    /// <param name="aCancellationToken">Cancels the probe.</param>
    /// <returns>The re-detected status plus evidence.</returns>
    public virtual Task<CheckResult> VerifyAsync(CancellationToken aCancellationToken = default)
        => DetectAsync(aCancellationToken);

    /// <summary>
    /// Whether this check is in scope for the given machine roles and selected app
    /// (machine roles ∩ check roles, and app match when the check is app-specific).
    /// </summary>
    /// <param name="aMachineRoles">The roles the machine holds.</param>
    /// <param name="aSelectedApp">The currently selected app profile, or <c>null</c> when none.</param>
    /// <returns><c>true</c> when the check should be detected on this machine.</returns>
    public bool AppliesTo(MachineRole aMachineRoles, string? aSelectedApp)
    {
        if ((Roles & aMachineRoles) == MachineRole.None)
        {
            return false;
        }

        if (Apps.Count == 0)
        {
            return true;
        }

        return aSelectedApp is not null && Apps.Contains(aSelectedApp, StringComparer.OrdinalIgnoreCase);
    }
}
