namespace TrSetup.Core.Settings;

/// <summary>
/// The endpoint a check actually probes, after the per-machine override in
/// <see cref="TrSetupSettings.Endpoints"/> has been applied to a profile's declared default URL
/// (REQ-FN-028).
/// </summary>
/// <param name="Url">The URL to probe — the override when configured, otherwise the profile default.</param>
/// <param name="IsOverridden">Whether <paramref name="Url"/> came from per-machine settings rather than the profile.</param>
/// <param name="AllowSelfSignedCertificate">
/// Whether the user explicitly opted to accept an untrusted TLS certificate for this endpoint.
/// Only ever <c>true</c> for an overridden URL — see <see cref="TrSetupSettings.TrustedSelfSignedEndpoints"/>.
/// </param>
/// <param name="Source">Human-readable provenance for the evidence line (e.g. "profile default").</param>
public sealed record EndpointResolution(
    string Url,
    bool IsOverridden,
    bool AllowSelfSignedCertificate,
    string Source);

/// <summary>
/// Applies the per-machine endpoint override to a profile's declared endpoint URL (REQ-FN-028 /
/// BRD-42).
/// <para>
/// The problem this solves: <c>appstudio.json</c> declares the App Manager API as
/// <c>https://localhost:5101/</c> while the SAME requirement is scoped to both
/// <c>DeviceHostWindows</c> and <c>AppRunnerMac</c>. On a genuine two-machine setup the Mac
/// app-runner has nothing on its own <c>localhost:5101</c> — the service runs on the Windows
/// device-host — so the check could never go green no matter what the user did. The profile keeps
/// its single-machine default; the machine that needs a different address names it in Settings.
/// </para>
/// </summary>
public static class EndpointResolver
{
    /// <summary>The profile param naming the settings key that may override the requirement's URL.</summary>
    public const string UrlSettingKeyParam = "urlSettingKey";

    /// <summary>
    /// Resolves the URL a check should probe, plus its provenance and TLS posture.
    /// </summary>
    /// <param name="aDefaultUrl">The URL the profile declares (the single-machine default).</param>
    /// <param name="aSettingKey">
    /// The <see cref="TrSetupSettings.Endpoints"/> key that may override it, or <c>null</c> when the
    /// requirement declares no override key (URL is then fixed by the profile).
    /// </param>
    /// <param name="aSettings">Live settings, or <c>null</c> when none are available.</param>
    /// <returns>The effective endpoint to probe; never null.</returns>
    public static EndpointResolution Resolve(string aDefaultUrl, string? aSettingKey, TrSetupSettings? aSettings)
    {
        var vDefault = (aDefaultUrl ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(aSettingKey))
        {
            return new EndpointResolution(vDefault, false, false, "profile default (not overridable)");
        }

        var vKey = aSettingKey.Trim();
        var vHasOverride = aSettings is not null
            && aSettings.Endpoints.TryGetValue(vKey, out var vConfigured)
            && !string.IsNullOrWhiteSpace(vConfigured);

        if (!vHasOverride)
        {
            return new EndpointResolution(
                vDefault,
                false,
                false,
                $"profile default — override it in Settings → Endpoints ['{vKey}']");
        }

        var vUrl = aSettings!.Endpoints[vKey].Trim();

        // Trust is opt-in AND scoped: it only ever applies to a URL the user themselves configured,
        // so a built-in profile default can never be silently probed without validation.
        var vTrust = aSettings.TrustedSelfSignedEndpoints.Contains(vKey);
        var vSource = vTrust
            ? $"configured in Settings → Endpoints ['{vKey}'], self-signed certificate explicitly trusted"
            : $"configured in Settings → Endpoints ['{vKey}']";
        return new EndpointResolution(vUrl, true, vTrust, vSource);
    }
}
