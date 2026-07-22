using TrSetup.Core.Catalog.Probing;
using TrSetup.Core.Checks;
using TrSetup.Core.Fixing;
using TrSetup.Core.Processes;
using TrSetup.Core.Profiles;
using TrSetup.Core.Settings;

namespace TrSetup.Core.Catalog.Mac;

/// <summary>
/// REQ-FN-028 (BRD-42): the culminating Mac app-runner fixer — "Build &amp; install &lt;App&gt; for
/// Mac (Catalyst)". It is scoped to the <see cref="MachineRole.AppRunnerMac"/> role and the selected
/// app. The fixer is <b>disabled while any prerequisite is red</b>: detect reports the still-red
/// prerequisite ids as the failure reason and <see cref="FixAsync"/> refuses to run
/// <c>dotnet build</c> until every prerequisite is green. On non-macOS the row is
/// <see cref="CheckStatus.NotApplicable"/> (a Windows exe can never build a Catalyst app). The live
/// build runs only on the Mac (UAT).
/// </summary>
public sealed class MacCatalystBuildCheck : MacCheckBase
{
    /// <summary>The literal Catalyst build command the fixer runs on the Mac.</summary>
    public const string BuildCommand = "dotnet build -f net10.0-maccatalyst -c Release";

    private readonly string objAppName;
    private readonly string[] objApps;
    private readonly Func<CancellationToken, Task<IReadOnlyList<string>>> objPrerequisiteRedIds;
    private readonly Func<bool> objIsMacOs;
    private readonly Func<RepoRootResolution> objRepoRoot;
    private readonly Func<string?> objAppBundlePath;

    /// <summary>
    /// The outcome of resolving the app's source-repo root: either a validated absolute
    /// <paramref name="Path"/>, or <c>null</c> with a human-readable <paramref name="Problem"/>.
    /// </summary>
    /// <param name="Path">The validated repo root, or <c>null</c> when unusable.</param>
    /// <param name="Problem">Why it is unusable; empty when <paramref name="Path"/> is set.</param>
    private readonly record struct RepoRootResolution(string? Path, string Problem);

    /// <summary>
    /// Creates the check.
    /// </summary>
    /// <param name="aAppName">The selected app the row is scoped to (drives the title, id and <see cref="Apps"/>).</param>
    /// <param name="aProcessRunner">The process choke-point the build/install shells through.</param>
    /// <param name="aPrerequisiteRedIds">Reports the ids of the app's still-red prerequisites (empty = all green — the gate that enables the fixer).</param>
    /// <param name="aFix">Fixer frameworks; when null the check is detect-only (no Fix button).</param>
    /// <param name="aIsMacOs">macOS detection override for tests; defaults to <see cref="OperatingSystem.IsMacOS"/>.</param>
    /// <param name="aWorkingDirectory">
    /// Explicit build working directory override (tests / callers that already know the repo root).
    /// When null the root is resolved and VALIDATED from <paramref name="aSettings"/> — see
    /// <see cref="ProfilePaths.ResolveAppRepoRoot"/>. It deliberately no longer falls back to the
    /// process working directory (REQ-FN-028 defect: a published app resolved its "repo" to its own
    /// output folder).
    /// </param>
    /// <param name="aAppBundlePath">Produced <c>.app</c> path accessor for detect/install; defaults to unknown.</param>
    /// <param name="aSettings">Live settings accessor supplying the configured app→repo-path map.</param>
    /// <exception cref="ArgumentNullException">Thrown when a required dependency is null.</exception>
    public MacCatalystBuildCheck(
        string aAppName,
        IProcessRunner aProcessRunner,
        Func<CancellationToken, Task<IReadOnlyList<string>>> aPrerequisiteRedIds,
        CheckFixServices? aFix = null,
        Func<bool>? aIsMacOs = null,
        Func<string>? aWorkingDirectory = null,
        Func<string?>? aAppBundlePath = null,
        Func<TrSetupSettings>? aSettings = null)
        : base(aProcessRunner, aFix)
    {
        objAppName = aAppName ?? throw new ArgumentNullException(nameof(aAppName));
        objApps = new[] { aAppName };
        objPrerequisiteRedIds = aPrerequisiteRedIds ?? throw new ArgumentNullException(nameof(aPrerequisiteRedIds));
        objIsMacOs = aIsMacOs ?? OperatingSystem.IsMacOS;
        objAppBundlePath = aAppBundlePath ?? (() => null);
        objRepoRoot = aWorkingDirectory is not null
            ? () => new RepoRootResolution(aWorkingDirectory(), string.Empty)
            : () => ResolveConfiguredRoot(aAppName, aSettings);
    }

    /// <summary>
    /// Resolves the app's repo root from live settings, or explains why it cannot.
    /// </summary>
    /// <param name="aAppName">The app whose repo root is wanted.</param>
    /// <param name="aSettings">Live settings accessor, or <c>null</c> when none was supplied.</param>
    /// <returns>The validated root, or <c>null</c> with the reason.</returns>
    private static RepoRootResolution ResolveConfiguredRoot(string aAppName, Func<TrSetupSettings>? aSettings)
    {
        var vConfigured = aSettings?.Invoke().AppRepoPaths;
        var vPath = ProfilePaths.ResolveAppRepoRoot(aAppName, vConfigured, out var vProblem);
        return new RepoRootResolution(vPath, vProblem);
    }

    /// <inheritdoc />
    public override string Id => objAppName.ToLowerInvariant() + ".maccatalyst-build";

    /// <inheritdoc />
    public override string Title => $"Build & install {objAppName} for Mac (Catalyst)";

    /// <inheritdoc />
    public override MachineRole Roles => MachineRole.AppRunnerMac;

    /// <inheritdoc />
    public override IReadOnlyCollection<string> Apps => objApps;

    /// <inheritdoc />
    public override CheckSeverity Severity => CheckSeverity.Recommended;

    /// <inheritdoc />
    public override CheckExplanation Explain => new(
        $"A one-click Catalyst build of {objAppName} on the Mac ({BuildCommand}), then installs/opens the produced .app.",
        "Automates the Mac path of the app's BuildAndRun guide — but only once every build prerequisite is green.",
        "WORKFLOW §0b");

    /// <inheritdoc />
    public override string? FixPreview => CanFix ? BuildPreview() : null;

    /// <inheritdoc />
    public override CheckFix? FixAsync => CanFix ? FixCoreAsync : null;

    /// <inheritdoc />
    public override async Task<CheckResult> DetectAsync(CancellationToken aCancellationToken = default)
    {
        if (!objIsMacOs())
        {
            return CheckResult.NotApplicable("Catalyst builds run only on macOS (Mac app-runner role).");
        }

        var vReds = await objPrerequisiteRedIds(aCancellationToken).ConfigureAwait(false);
        if (vReds.Count > 0)
        {
            return CheckResult.Fail(
                $"Prerequisites still red — fix them first: {string.Join(", ", vReds)}. " +
                "The Catalyst build fixer stays disabled until every prerequisite is green.");
        }

        return DetectBuilt();
    }

    private CheckResult DetectBuilt()
    {
        var vPath = objAppBundlePath();
        if (vPath is not null && Directory.Exists(vPath))
        {
            return CheckResult.Pass($"Catalyst .app built at {vPath}.");
        }

        return CheckResult.Fail(
            "Prerequisites green — ready to build. No Catalyst .app yet; run the fixer to build & install it.");
    }

    private async Task<FixResult> FixCoreAsync(ConsentToken aConsent, CancellationToken aCancellationToken)
    {
        var vReds = await objPrerequisiteRedIds(aCancellationToken).ConfigureAwait(false);
        if (vReds.Count > 0)
        {
            return new FixResult(false, $"Refused: prerequisites still red: {string.Join(", ", vReds)}.");
        }

        // REQ-FN-028: refuse rather than build in whatever directory the process happens to be in.
        var vRoot = objRepoRoot();
        if (vRoot.Path is null)
        {
            return new FixResult(false, $"Refused: {vRoot.Problem} Nothing was built.");
        }

        return await BuildAndInstallAsync(vRoot.Path, aCancellationToken).ConfigureAwait(false);
    }

    private async Task<FixResult> BuildAndInstallAsync(string aRepoRoot, CancellationToken aCancellationToken)
    {
        var vRequest = new ProcessRunRequest(
            "dotnet", "build -f net10.0-maccatalyst -c Release", aRepoRoot, TimeSpan.FromMinutes(30));
        var vRun = await ProcessProbe.RunAsync(ProcessRunner, vRequest, aCancellationToken).ConfigureAwait(false);
        if (!vRun.Succeeded)
        {
            return new FixResult(false, vRun.ToEvidenceString());
        }

        return await InstallAsync(vRun.ToEvidenceString(), aCancellationToken).ConfigureAwait(false);
    }

    private async Task<FixResult> InstallAsync(string aBuildEvidence, CancellationToken aCancellationToken)
    {
        var vPath = objAppBundlePath();
        if (vPath is null)
        {
            return new FixResult(true, aBuildEvidence);
        }

        var vOpen = await RunMacFixAsync("open", vPath, TimeSpan.FromMinutes(1), aCancellationToken).ConfigureAwait(false);
        return new FixResult(vOpen.FixerReportedSuccess, aBuildEvidence + Environment.NewLine + vOpen.RawOutput);
    }

    private string BuildPreview()
    {
        var vRoot = objRepoRoot();

        // The literal command is always shown (it is the documented contract of the preview), but an
        // unresolved repo root is stated plainly instead of being papered over with the process cwd.
        if (vRoot.Path is null)
        {
            return $"{BuildCommand}   —   BLOCKED: {vRoot.Problem} " +
                   "The fixer will refuse until a valid repo path is configured.";
        }

        var vPath = objAppBundlePath()
            ?? Path.Combine(vRoot.Path, "bin", "Release", "net10.0-maccatalyst", $"{objAppName}.app");
        return $"cd {vRoot.Path} && {BuildCommand}   →   {vPath} (then: open the produced .app)";
    }
}
