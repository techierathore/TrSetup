using TrSetup.Core.Catalog.Probing;
using TrSetup.Core.Checks;
using TrSetup.Core.Fixing;
using TrSetup.Core.Processes;
using TrSetup.Core.Profiles;

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
    private readonly Func<string> objWorkingDirectory;
    private readonly Func<string?> objAppBundlePath;

    /// <summary>
    /// Creates the check.
    /// </summary>
    /// <param name="aAppName">The selected app the row is scoped to (drives the title, id and <see cref="Apps"/>).</param>
    /// <param name="aProcessRunner">The process choke-point the build/install shells through.</param>
    /// <param name="aPrerequisiteRedIds">Reports the ids of the app's still-red prerequisites (empty = all green — the gate that enables the fixer).</param>
    /// <param name="aFix">Fixer frameworks; when null the check is detect-only (no Fix button).</param>
    /// <param name="aIsMacOs">macOS detection override for tests; defaults to <see cref="OperatingSystem.IsMacOS"/>.</param>
    /// <param name="aWorkingDirectory">Build working directory override; defaults to <see cref="ProfilePaths.RepoRoot"/>.</param>
    /// <param name="aAppBundlePath">Produced <c>.app</c> path accessor for detect/install; defaults to unknown.</param>
    /// <exception cref="ArgumentNullException">Thrown when a required dependency is null.</exception>
    public MacCatalystBuildCheck(
        string aAppName,
        IProcessRunner aProcessRunner,
        Func<CancellationToken, Task<IReadOnlyList<string>>> aPrerequisiteRedIds,
        CheckFixServices? aFix = null,
        Func<bool>? aIsMacOs = null,
        Func<string>? aWorkingDirectory = null,
        Func<string?>? aAppBundlePath = null)
        : base(aProcessRunner, aFix)
    {
        objAppName = aAppName ?? throw new ArgumentNullException(nameof(aAppName));
        objApps = new[] { aAppName };
        objPrerequisiteRedIds = aPrerequisiteRedIds ?? throw new ArgumentNullException(nameof(aPrerequisiteRedIds));
        objIsMacOs = aIsMacOs ?? OperatingSystem.IsMacOS;
        objWorkingDirectory = aWorkingDirectory ?? (() => ProfilePaths.RepoRoot);
        objAppBundlePath = aAppBundlePath ?? (() => null);
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

        return await BuildAndInstallAsync(aCancellationToken).ConfigureAwait(false);
    }

    private async Task<FixResult> BuildAndInstallAsync(CancellationToken aCancellationToken)
    {
        var vRequest = new ProcessRunRequest(
            "dotnet", "build -f net10.0-maccatalyst -c Release", objWorkingDirectory(), TimeSpan.FromMinutes(30));
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
        var vPath = objAppBundlePath()
            ?? $"{objWorkingDirectory()}/bin/Release/net10.0-maccatalyst/{objAppName}.app";
        return $"cd {objWorkingDirectory()} && {BuildCommand}   →   {vPath} (then: open the produced .app)";
    }
}
