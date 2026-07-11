using Microsoft.Extensions.Logging;
using TrSetup.Core.Catalog.Framework;
using TrSetup.Core.Catalog.Mac;
using TrSetup.Core.Catalog.Probing;
using TrSetup.Core.Catalog.Windows;
using TrSetup.Core.Catalog.Wsl;
using TrSetup.Core.Checks;
using TrSetup.Core.Fixing;
using TrSetup.Core.Processes;
using TrSetup.Core.Profiles;
using TrSetup.Core.Settings;

namespace TrSetup.Core.Catalog;

/// <summary>
/// Assembles the full built-in check catalog — every WSL / Windows / Mac role check and
/// cross-machine probe TrSetup knows about (BRD §9 F-WSLCHK / F-WINCHK / F-MACCHK tables).
/// Every head builds its <see cref="Engine.CheckEngine"/> from this one list so the board is
/// identical everywhere (ADR-005).
/// </summary>
public static class CheckCatalog
{
    /// <summary>
    /// Creates every built-in check in board order (Framework core rows first, then the
    /// cross-machine Bridges probes). The engine scopes them to (machine roles ∩ selected app)
    /// at enumeration time — this list is always the full set.
    /// </summary>
    /// <param name="aProcessRunner">The single process choke-point checks probe through (REQ-FN-003).</param>
    /// <param name="aSettingsAccessor">Live accessor for the current settings (configured endpoints such as the Mac IP).</param>
    /// <param name="aHttpProbe">Optional HTTP probe override for tests; the real 5 s probe is used when omitted.</param>
    /// <param name="aSystemProbe">Optional filesystem/environment probe override for tests; the real one is used when omitted.</param>
    /// <param name="aFixServices">
    /// Optional P2 fixer frameworks (download/config-write/elevation) attached to every fixable
    /// row; the production bundle around <paramref name="aProcessRunner"/> is built when omitted.
    /// </param>
    /// <param name="aProfileLoader">
    /// Optional declarative-profile loader (REQ-FN-021); the default built-in + app-repo override
    /// loader is used when omitted. Used to append the selected app's profile checks.
    /// </param>
    /// <param name="aLogger">Optional logger for profile-append diagnostics; a no-op logger is used when omitted.</param>
    /// <returns>The full built-in catalog plus the selected app's profile checks (when one resolves).</returns>
    public static IReadOnlyList<Check> CreateAllChecks(
        IProcessRunner aProcessRunner,
        Func<TrSetupSettings> aSettingsAccessor,
        IHttpStatusProbe? aHttpProbe = null,
        ISystemProbe? aSystemProbe = null,
        CheckFixServices? aFixServices = null,
        ProfileLoader? aProfileLoader = null,
        ILogger? aLogger = null)
    {
        ArgumentNullException.ThrowIfNull(aProcessRunner);
        ArgumentNullException.ThrowIfNull(aSettingsAccessor);
        var vHttpProbe = aHttpProbe ?? new HttpStatusProbe();
        var vSystemProbe = aSystemProbe ?? new SystemProbe();
        var vFix = aFixServices ?? CheckFixServices.CreateDefault(aProcessRunner);

        var vChecks = new List<Check>
        {
            // Framework core — WSL agent host (REQ-FN-006 detects / REQ-FN-014 fixes, BRD §9 F-WSLCHK).
            new WslDotnetSdkCheck(aProcessRunner, vSystemProbe, vFix),
            new WslChromiumLibsCheck(aProcessRunner, vFix),
            new WslWinrunBridgeCheck(vSystemProbe, vFix),
            new WslNodeCheck(aProcessRunner, vSystemProbe, vFix),
            new WslPlaywrightCheck(aProcessRunner, vSystemProbe, vFix),
            new WslMirroredNetworkingCheck(aProcessRunner),
            new WslGitCheck(aProcessRunner, vFix),

            // Framework core — Windows device host (REQ-FN-007 detects / REQ-FN-015 fixes, BRD §9 F-WINCHK).
            new WinWslConfigMirroredCheck(aProcessRunner, vFix),
            new WinAndroidSdkCheck(aProcessRunner, vFix),
            new WinAndroidApi34ImageCheck(aProcessRunner, vFix),
            new WinAndroidAvdCheck(aProcessRunner, vFix),
            new WinNodeCheck(aProcessRunner, vFix),
            new WinAppiumDriverCheck(aProcessRunner, vFix),
            new WinVerifyHelperCheck(aProcessRunner, vFix),
            new WinAppiumSessionCheck(vHttpProbe, aProcessRunner),
            new WinMauiWorkloadCheck(aProcessRunner, vFix),
            new WinJdkCheck(aProcessRunner, vFix),

            // Framework core — Mac device host (REQ-FN-008 detects / REQ-FN-016 fixes, BRD §9 F-MACCHK).
            new MacXcodeCheck(aProcessRunner, vFix),
            new MacDotnetMauiCheck(aProcessRunner, vFix),
            new MacNodeCheck(aProcessRunner, vFix),
            new MacAppiumDriversCheck(aProcessRunner, vFix),
            new MacAppiumLaunchAgentCheck(aProcessRunner, vHttpProbe, aSettingsAccessor, vFix),
            new MacStableIpCheck(aProcessRunner, aSettingsAccessor),
            new MacIosSimulatorCheck(aProcessRunner, vFix),

            // Framework core — app-repo verification config (REQ-FN-024): writes the appium endpoint
            // block into the app repo's .tfcore/core-config.yaml and curl-verifies each registered head.
            new AppiumConfigBlockCheck(vHttpProbe, aSettingsAccessor, vFix),

            // Bridges (cross-machine) — HTTP probes only, manual guidance (REQ-FN-009, BRD §9 F-BRIDGE).
            new WslWindowsAppiumCheck(vHttpProbe),
            new WslMacAppiumCheck(vHttpProbe, aSettingsAccessor)
        };

        AppendProfileChecks(vChecks, aProcessRunner, aSettingsAccessor, vHttpProbe, vSystemProbe, vFix, aProfileLoader, aLogger);
        return vChecks;
    }

    /// <summary>
    /// Appends the selected app's declarative-profile checks after the framework rows (REQ-FN-021).
    /// The engine scopes them by <see cref="Check.AppliesTo"/>, so they only render when their app
    /// is selected. Guarded end-to-end: no app selected, no profile resolved, or a profile load
    /// failure appends nothing and never throws out of catalog assembly.
    /// </summary>
    private static void AppendProfileChecks(
        List<Check> aChecks,
        IProcessRunner aProcessRunner,
        Func<TrSetupSettings> aSettingsAccessor,
        IHttpStatusProbe aHttpProbe,
        ISystemProbe aSystemProbe,
        CheckFixServices aFix,
        ProfileLoader? aProfileLoader,
        ILogger? aLogger)
    {
        var vSelectedApp = aSettingsAccessor().SelectedApp;
        if (string.IsNullOrWhiteSpace(vSelectedApp))
        {
            return;
        }

        try
        {
            var vLoader = aProfileLoader ?? new ProfileLoader();
            var vProfile = vLoader.Resolve(vSelectedApp);
            if (vProfile is null)
            {
                aLogger?.LogInformation("No declarative profile resolved for selected app '{App}'.", vSelectedApp);
                return;
            }

            var vContext = new ProfileCheckContext(vProfile.Name, aProcessRunner, aFix, aHttpProbe, aSystemProbe, aSettingsAccessor);
            var vProfileChecks = new ProfileCheckFactory().CreateChecks(vProfile, vContext);
            aChecks.AddRange(vProfileChecks);
            AppendMacCatalystBuildCheck(aChecks, vProfile.Name, vProfileChecks, aProcessRunner, aFix);
        }
        catch (Exception vEx)
        {
            // Never let profile assembly break the framework board (Hard rule: catalog must not throw).
            aLogger?.LogWarning(vEx, "Failed to append profile checks for '{App}'; framework rows kept.", vSelectedApp);
        }
    }

    /// <summary>
    /// Appends the Mac app-runner "Build &amp; install &lt;App&gt; for Mac (Catalyst)" fixer (REQ-FN-028)
    /// as the culminating <see cref="MachineRole.AppRunnerMac"/> row — so the role's board equals the
    /// app's build-and-run prerequisites plus this one-click fixer (REQ-FN-027). The fixer's
    /// enablement gate is driven live by the app's AppRunnerMac-tagged profile checks: while any of
    /// them detect red, the fixer refuses.
    /// </summary>
    private static void AppendMacCatalystBuildCheck(
        List<Check> aChecks,
        string aAppName,
        IReadOnlyList<Check> aProfileChecks,
        IProcessRunner aProcessRunner,
        CheckFixServices aFix)
    {
        var vPrerequisites = aProfileChecks
            .Where(aCheck => (aCheck.Roles & MachineRole.AppRunnerMac) != MachineRole.None)
            .ToList();
        aChecks.Add(new MacCatalystBuildCheck(
            aAppName, aProcessRunner, aToken => DetectRedIdsAsync(vPrerequisites, aToken), aFix));
    }

    /// <summary>
    /// Detects the app's AppRunnerMac prerequisites and returns the ids of those that come back
    /// <see cref="CheckStatus.Fail"/> — the live "still red" set that gates the Catalyst build fixer.
    /// </summary>
    private static async Task<IReadOnlyList<string>> DetectRedIdsAsync(
        IReadOnlyList<Check> aPrerequisites,
        CancellationToken aCancellationToken)
    {
        var vReds = new List<string>();
        foreach (var vCheck in aPrerequisites)
        {
            var vResult = await vCheck.DetectAsync(aCancellationToken).ConfigureAwait(false);
            if (vResult.Status == CheckStatus.Fail)
            {
                vReds.Add(vCheck.Id);
            }
        }

        return vReds;
    }
}
