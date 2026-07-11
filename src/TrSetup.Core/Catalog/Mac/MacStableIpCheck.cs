using TrSetup.Core.Checks;
using TrSetup.Core.Processes;
using TrSetup.Core.Settings;

namespace TrSetup.Core.Catalog.Mac;

/// <summary>
/// F-MACCHK: "Stable IP / hostname advertised" — compares the Mac's current LAN address
/// (<c>ipconfig getifaddr en0/en1</c>) with the endpoint the other machines have configured.
/// Manual-only: the durable fix is a DHCP reservation on the router.
/// </summary>
public sealed class MacStableIpCheck : MacCheckBase
{
    private readonly Func<TrSetupSettings> objSettingsAccessor;

    /// <summary>
    /// Creates the check.
    /// </summary>
    /// <param name="aProcessRunner">The process choke-point the detect shells through.</param>
    /// <param name="aSettingsAccessor">Live accessor for current settings (the configured Mac endpoint).</param>
    public MacStableIpCheck(IProcessRunner aProcessRunner, Func<TrSetupSettings> aSettingsAccessor)
        : base(aProcessRunner)
    {
        objSettingsAccessor = aSettingsAccessor;
    }

    /// <inheritdoc />
    public override string Id => "mac.stable-ip";

    /// <inheritdoc />
    public override string Title => "Stable IP / hostname";

    /// <inheritdoc />
    public override CheckSeverity Severity => CheckSeverity.Recommended;

    /// <inheritdoc />
    public override CheckExplanation Explain => new(
        "Whether the Mac's current LAN address still matches the endpoint configured on the other machines.",
        "The WSL agent host probes the Mac by this address; if DHCP hands out a new one, every cross-machine check silently dies.",
        "WORKFLOW §0b");

    /// <inheritdoc />
    public override async Task<CheckResult> DetectAsync(CancellationToken aCancellationToken = default)
    {
        var vSettings = objSettingsAccessor();
        if (!vSettings.Endpoints.TryGetValue("MacIp", out var vConfigured) || string.IsNullOrWhiteSpace(vConfigured))
        {
            return CheckResult.Warn(
                "No MacIp endpoint configured (settings Endpoints['MacIp']) — other machines cannot find this Mac.");
        }

        var vCurrentIp = await GetCurrentIpAsync(aCancellationToken).ConfigureAwait(false);
        if (vCurrentIp is null)
        {
            return CheckResult.Warn(
                $"Could not read the Mac's LAN address (ipconfig getifaddr en0/en1 both failed); configured endpoint is {vConfigured}.");
        }

        if (string.Equals(vCurrentIp, vConfigured.Trim(), StringComparison.OrdinalIgnoreCase))
        {
            return CheckResult.Pass($"Current LAN address {vCurrentIp} matches the configured endpoint.");
        }

        return CheckResult.Fail(
            $"Current LAN address {vCurrentIp} does NOT match the configured endpoint {vConfigured} — " +
            "reserve the Mac's address in the router (DHCP reservation) or update the endpoint on every machine.");
    }

    private async Task<string?> GetCurrentIpAsync(CancellationToken aCancellationToken)
    {
        foreach (var vInterface in new[] { "en0", "en1" })
        {
            var vRun = await RunMacCommandAsync(
                "ipconfig", $"getifaddr {vInterface}", TimeSpan.FromSeconds(10), aCancellationToken)
                .ConfigureAwait(false);
            if (vRun.Succeeded && !string.IsNullOrWhiteSpace(vRun.StandardOutput))
            {
                return vRun.StandardOutput.Trim();
            }
        }

        return null;
    }
}
