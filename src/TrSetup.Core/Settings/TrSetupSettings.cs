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
    /// Configured endpoints by name — e.g. <c>MacIp</c> → <c>192.168.1.50</c>, or
    /// <c>AppManagerUrl</c> → <c>https://192.168.1.14:5101/</c>. Values are addresses/URLs only;
    /// secrets are never stored here (ADR-008).
    /// <para>
    /// A profile <c>endpoint</c> requirement may name one of these keys via its
    /// <c>urlSettingKey</c> param (REQ-FN-028): when the key holds a value it REPLACES the
    /// profile's hardcoded default URL for this machine. That is what lets a Mac app-runner point
    /// the App Manager check at the Windows device-host on the LAN while the single-machine
    /// default stays <c>https://localhost:5101/</c>.
    /// </para>
    /// </summary>
    public Dictionary<string, string> Endpoints { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Endpoint keys (from <see cref="Endpoints"/>) whose TLS certificate the user has EXPLICITLY
    /// opted to accept without validation — an opt-in trust affordance, never a default.
    /// <para>
    /// Rationale (REQ-FN-028): a LAN App Manager typically serves the ASP.NET development
    /// certificate (<c>CN=localhost</c>, self-signed), so probing it by IP fails validation on both
    /// issuer AND hostname. Rather than disable certificate validation globally — a security
    /// regression that would silently weaken EVERY probe — trust is granted per endpoint key, by
    /// the user, in Settings, and applies ONLY to a URL the user themselves overrode. A profile's
    /// built-in default URL is always probed with full validation.
    /// </para>
    /// </summary>
    public HashSet<string> TrustedSelfSignedEndpoints { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Configured source-repo roots by app name — e.g. <c>AppStudio</c> →
    /// <c>/Users/me/MyCode/AppStudio</c> (REQ-FN-028 / BRD-42). The Catalyst build fixer builds in
    /// this directory. When an app has no valid entry the fixer REFUSES rather than falling back to
    /// the process working directory, which previously resolved to wherever the app happened to be
    /// launched from (the publish folder) and produced a build in the wrong place.
    /// </summary>
    public Dictionary<string, string> AppRepoPaths { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}
