namespace TrSetup.Core.Catalog.Probing;

/// <summary>
/// Builds the guidance line attached to every failing cross-machine probe (REQ-FN-009):
/// the guidance always NAMES the machine role that owns the fix, because TrSetup never
/// remote-executes — the other machine fixes itself by running TrSetup locally.
/// </summary>
public static class CrossMachineGuidance
{
    /// <summary>
    /// Renders the "go fix it over there" guidance for a failing probe.
    /// </summary>
    /// <param name="aMachineName">The human name of the owning machine (e.g. <c>Windows host</c>, <c>Mac</c>).</param>
    /// <param name="aRoleName">The owning machine role (e.g. <c>Device host</c>).</param>
    /// <returns>One sentence naming the machine and role that own the fix.</returns>
    public static string FixOn(string aMachineName, string aRoleName) =>
        $"TrSetup never remote-executes — run TrSetup on the {aMachineName} ({aRoleName} role) to fix this.";
}
