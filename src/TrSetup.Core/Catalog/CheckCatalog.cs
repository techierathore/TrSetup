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
    /// Per-prerequisite detect budget for the Catalyst build gate (REQ-FN-028): each prerequisite
    /// re-detect is hard-bounded so the whole gate detect (run in parallel) settles well inside the
    /// engine's 5 s row budget even when one prerequisite hangs (e.g. an unauthenticated feed probe).
    /// </summary>
    public static readonly TimeSpan PrerequisiteProbeTimeout = TimeSpan.FromSeconds(3.5);

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
    /// Detects the app's AppRunnerMac prerequisites — all in parallel, each hard-bounded by
    /// <see cref="PrerequisiteProbeTimeout"/> — and returns the ids of those that come back
    /// <see cref="CheckStatus.Fail"/>: the live "still red" set that gates the Catalyst build fixer
    /// (REQ-FN-028). A prerequisite that times out or throws is counted red (the gate never assumes
    /// green) and its id carries a "not confirmed green" suffix so the evidence stays honest.
    /// The returned list is deterministic: prerequisites in catalog order.
    /// </summary>
    /// <param name="aPrerequisites">The gate's prerequisite checks, in catalog order.</param>
    /// <param name="aCancellationToken">Cancels the whole gate detect.</param>
    /// <param name="aPerPrerequisiteTimeout">Per-prerequisite budget override for tests; defaults to <see cref="PrerequisiteProbeTimeout"/>.</param>
    /// <returns>The red (or not-confirmed-green) prerequisite ids in catalog order; empty when all green.</returns>
    internal static async Task<IReadOnlyList<string>> DetectRedIdsAsync(
        IReadOnlyList<Check> aPrerequisites,
        CancellationToken aCancellationToken,
        TimeSpan? aPerPrerequisiteTimeout = null)
    {
        var vTimeout = aPerPrerequisiteTimeout ?? PrerequisiteProbeTimeout;
        var vTasks = aPrerequisites
            .Select(aCheck => DetectPrerequisiteRedIdAsync(aCheck, vTimeout, aCancellationToken))
            .ToList();
        var vRedIds = await Task.WhenAll(vTasks).ConfigureAwait(false);
        return vRedIds.Where(aRedId => aRedId is not null).Select(aRedId => aRedId!).ToList();
    }

    /// <summary>
    /// Detects one prerequisite within the given hard budget and reports its red id — <c>null</c>
    /// when it confirms green (Pass/Warn/NotApplicable), the plain id on a confirmed
    /// <see cref="CheckStatus.Fail"/>, or the id with a "not confirmed green" suffix when the
    /// probe timed out or threw.
    /// </summary>
    /// <param name="aCheck">The prerequisite check to detect.</param>
    /// <param name="aTimeout">The hard per-prerequisite budget.</param>
    /// <param name="aCancellationToken">Cancels the probe.</param>
    /// <returns>The red id to report, or <c>null</c> when the prerequisite is confirmed non-red.</returns>
    private static async Task<string?> DetectPrerequisiteRedIdAsync(
        Check aCheck,
        TimeSpan aTimeout,
        CancellationToken aCancellationToken)
    {
        using var vTimeoutCts = CancellationTokenSource.CreateLinkedTokenSource(aCancellationToken);
        vTimeoutCts.CancelAfter(aTimeout);
        try
        {
            // WaitAsync hard-bounds the detect even when the check ignores its token.
            var vResult = await aCheck.DetectAsync(vTimeoutCts.Token)
                .WaitAsync(vTimeoutCts.Token)
                .ConfigureAwait(false);
            return vResult.Status == CheckStatus.Fail ? aCheck.Id : null;
        }
        catch (OperationCanceledException) when (!aCancellationToken.IsCancellationRequested)
        {
            return $"{aCheck.Id} (not confirmed green: timed out after {aTimeout.TotalSeconds:0.#} s)";
        }
        catch (Exception vEx) when (vEx is not OperationCanceledException)
        {
            return $"{aCheck.Id} (not confirmed green: probe threw {vEx.GetType().Name})";
        }
    }
}
