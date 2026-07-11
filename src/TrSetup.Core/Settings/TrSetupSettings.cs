using TrSetup.Core.Checks;

namespace TrSetup.Core.Settings;

/// <summary>
/// The per-machine settings TrSetup persists (REQ-FN-005 / ADR-002): the machine's roles,
/// the selected app profile, and configured endpoints (e.g. the LAN Mac IP). This is the
/// ONLY persisted state — machine state itself is always detected live, never stored.
/// </summary>
public sealed class TrSetupSettings
{
    /// <summary>The roles this machine holds (chosen in the first-run role picker).</summary>
    public MachineRole Roles { get; set; } = MachineRole.None;

    /// <summary>The selected app profile (e.g. <c>AppStudio</c>), or <c>null</c> when none selected.</summary>
    public string? SelectedApp { get; set; }

    /// <summary>
    /// Configured endpoints by name — e.g. <c>MacIp</c> → <c>192.168.1.50</c>. Values are
    /// addresses only; secrets are never stored here (ADR-008).
    /// </summary>
    public Dictionary<string, string> Endpoints { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}
