using TrSetup.Core.Checks;
using TrSetup.Core.ConfigWriting;
using TrSetup.Core.Downloads;
using TrSetup.Core.Fixing;
using TrSetup.Core.Processes;

namespace TrSetup.Core.Catalog.Mac;

/// <summary>
/// F-MACCHK: "Node + npm" — detects via <c>node --version</c>. The fixer downloads the pinned
/// official Node LTS tarball into the managed tools root, extracts it there, and adds its
/// <c>bin</c> to <c>~/.zprofile</c> via a managed block (REQ-FN-016 — user-local, never
/// collides with a system Node).
/// </summary>
public sealed class MacNodeCheck : MacCheckBase
{
    /// <summary>
    /// The pinned Node.js LTS version the fixer installs.
    ///
    /// REQ-FN-016: this was v22.11.0, which satisfies NONE of appium@3's engine ranges
    /// (<c>^20.19.0 || ^22.12.0 || >=24.0.0</c>). npm does not fail on that — it silently resolves
    /// the newest engine-compatible Appium instead, which was 2.0.1, while the current Apple drivers
    /// require a v3 server. So the Node pin was the root cause of a permanently mismatched Appium on
    /// every TrSetup-provisioned Mac. Keep this at or above 22.12.0 (see
    /// <see cref="MinimumNodeVersion"/>) whenever <see cref="MacAppiumDriversCheck.AppiumVersion"/>
    /// is a v3 release.
    /// </summary>
    public const string NodeVersion = "v22.23.1";

    /// <summary>
    /// The oldest Node the pinned Appium can run on. Detect FAILS below this rather than passing a
    /// Node that would silently drag <c>npm install -g appium</c> back to an ancient server.
    /// </summary>
    public const string MinimumNodeVersion = "v22.12.0";

    /// <summary>The pinned official Node.js LTS macOS (arm64) tarball URL.</summary>
    public const string TarballUrl =
        "https://nodejs.org/dist/" + NodeVersion + "/node-" + NodeVersion + "-darwin-arm64.tar.gz";

    /// <summary>The stable managed-block id of the PATH line written into <c>~/.zprofile</c>.</summary>
    public const string PathBlockId = "mac.node-path";

    private readonly Func<string> objZprofilePath;

    /// <summary>
    /// Creates the check.
    /// </summary>
    /// <param name="aProcessRunner">The process choke-point the detect shells through.</param>
    /// <param name="aFix">Fixer frameworks; when null the check is detect-only (no Fix button).</param>
    /// <param name="aZprofilePath">Resolver for the shell-profile PATH file; defaults to <c>~/.zprofile</c>.</param>
    public MacNodeCheck(IProcessRunner aProcessRunner, CheckFixServices? aFix = null, Func<string>? aZprofilePath = null)
        : base(aProcessRunner, aFix)
    {
        objZprofilePath = aZprofilePath ?? DefaultZprofilePath;
    }

    private static string NodeDir => Path.Combine(TrSetupPaths.ToolsRoot, "node");

    /// <summary>
    /// The <c>bin</c> directory of the TrSetup-managed Node install (<c>node</c>, <c>npm</c>,
    /// and anything <c>npm install -g</c> puts there). Exposed so sibling checks whose tools are
    /// npm packages — e.g. <see cref="MacAppiumDriversCheck"/> — can resolve them without relying
    /// on the shell PATH, which the managed install only reaches via <c>~/.zprofile</c> in a NEW
    /// login shell.
    /// </summary>
    public static string ManagedNodeBinDir => Path.Combine(NodeDir, "bin");

    private static string DefaultZprofilePath() => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".zprofile");

    /// <inheritdoc />
    public override string? FixPreview => CanFix
        ? InstallerDownloader.BuildFixPreview(new DownloadRequest(TarballUrl, "node", "node-lts.tar.gz")) +
          $"{Environment.NewLine}then extract into {NodeDir} and add {NodeDir}/bin to PATH (managed block '{PathBlockId}')"
        : null;

    /// <inheritdoc />
    public override CheckFix? FixAsync => CanFix ? FixCoreAsync : null;

    /// <inheritdoc />
    public override string Id => "mac.node";

    /// <inheritdoc />
    public override string Title => "Node.js + npm";

    /// <inheritdoc />
    public override CheckSeverity Severity => CheckSeverity.Required;

    /// <inheritdoc />
    public override CheckExplanation Explain => new(
        "The Node.js runtime (and npm) on the Mac.",
        "Appium and its xcuitest/mac2 drivers are npm packages; nothing installs without Node.",
        "WORKFLOW §0b");

    /// <summary>The <c>node</c> binary inside the managed install the fixer produces.</summary>
    private static string ManagedNodeBinary => Path.Combine(NodeDir, "bin", "node");

    /// <inheritdoc />
    public override async Task<CheckResult> DetectAsync(CancellationToken aCancellationToken = default)
    {
        var vRun = await RunMacCommandAsync("node", "--version", TimeSpan.FromSeconds(10), aCancellationToken)
            .ConfigureAwait(false);
        if (vRun.Succeeded && !string.IsNullOrWhiteSpace(vRun.StandardOutput))
        {
            var vFound = vRun.StandardOutput.Trim();
            return IsAtLeastMinimum(vFound)
                ? CheckResult.Pass($"Node.js {vFound} present ($ node --version).")
                : TooOld(vFound);
        }

        // Not on this process's PATH — but the fixer installs user-locally into the managed tools
        // root and exports it from ~/.zprofile, which only a NEW LOGIN SHELL ever sources. Without
        // this second probe a just-succeeded fix still re-verified red, so a 200 MB+ install looked
        // to the user like "clicking Fix does nothing". Probe the managed binary directly.
        if (File.Exists(ManagedNodeBinary))
        {
            var vManaged = await RunMacCommandAsync(
                ManagedNodeBinary, "--version", TimeSpan.FromSeconds(10), aCancellationToken).ConfigureAwait(false);
            if (vManaged.Succeeded && !string.IsNullOrWhiteSpace(vManaged.StandardOutput) &&
                !IsAtLeastMinimum(vManaged.StandardOutput.Trim()))
            {
                return TooOld(vManaged.StandardOutput.Trim());
            }

            if (vManaged.Succeeded && !string.IsNullOrWhiteSpace(vManaged.StandardOutput))
            {
                return CheckResult.Pass(
                    $"Node.js {vManaged.StandardOutput.Trim()} present at {ManagedNodeBinary} (TrSetup-managed install). " +
                    $"Not yet on this process's PATH — the '{PathBlockId}' block in {objZprofilePath()} " +
                    "applies to new login shells, so open a new terminal (or log out and back in) to use `node` directly.");
            }
        }

        return CheckResult.Fail($"Node.js not found on the Mac.\n{vRun.ToEvidenceString()}");
    }

    /// <summary>
    /// The "present but too old" result. Stated as its own failure so the board says WHY, rather
    /// than the misleading "Node.js not found" (REQ-FN-016).
    /// </summary>
    /// <param name="aFound">The Node version that was actually found.</param>
    /// <returns>The failing result.</returns>
    private static CheckResult TooOld(string aFound) => CheckResult.Fail(
        $"Node.js {aFound} is present but TOO OLD — appium@{MacAppiumDriversCheck.AppiumVersion} requires at least " +
        $"Node {MinimumNodeVersion}. On an older Node, `npm install -g appium` silently resolves an ancient " +
        $"server that cannot load the current xcuitest/mac2 drivers. Run the fixer to install {NodeVersion}.");

    /// <summary>
    /// Whether a reported <c>node --version</c> string is at least <see cref="MinimumNodeVersion"/>.
    /// </summary>
    /// <param name="aVersion">The version string, e.g. <c>v22.23.1</c>.</param>
    /// <returns><c>true</c> when new enough, or when the string could not be parsed (never fail a row on a parsing opinion).</returns>
    private static bool IsAtLeastMinimum(string aVersion)
    {
        var vFound = ParseVersion(aVersion);
        var vMinimum = ParseVersion(MinimumNodeVersion);
        return vFound is null || vMinimum is null || vFound >= vMinimum;
    }

    /// <summary>Parses a leading <c>vMAJOR.MINOR.PATCH</c> into a comparable <see cref="Version"/>.</summary>
    /// <param name="aVersion">The raw version string.</param>
    /// <returns>The parsed version, or <c>null</c> when unreadable.</returns>
    private static Version? ParseVersion(string aVersion)
        => Version.TryParse(aVersion.Trim().TrimStart('v', 'V').Split('-')[0], out var vParsed) ? vParsed : null;

    private async Task<FixResult> FixCoreAsync(ConsentToken aConsent, CancellationToken aCancellationToken)
    {
        // REQ-FN-016: an UPGRADE must REPLACE the managed install, not overlay it. `tar -xzf` only
        // adds/overwrites entries — it never deletes ones the new tarball lacks — so extracting
        // v22.23.1 on top of v22.11.0 left 9 stale directories inside npm's own node_modules. The
        // resulting hybrid npm died on every command with "Class extends value undefined is not a
        // constructor or null", i.e. the Node fixer bricked npm for the Appium fixer that depends on
        // it. Observed live while bumping the pin.
        //
        // This MUST happen BEFORE the download: the downloader stages the archive INSIDE this very
        // directory ({ToolsRoot}/node/node-lts.tar.gz), so clearing afterwards deletes the tarball
        // that is about to be extracted (also observed live — tar then failed with "No such file").
        if (Directory.Exists(NodeDir))
        {
            Directory.Delete(NodeDir, recursive: true);
        }

        Directory.CreateDirectory(NodeDir);

        var vDownload = await FixServices!.Downloader.DownloadAsync(
            new DownloadRequest(TarballUrl, "node", "node-lts.tar.gz"), null, aCancellationToken).ConfigureAwait(false);
        if (!vDownload.Succeeded)
        {
            return new FixResult(false, vDownload.Evidence);
        }

        var vExtract = await RunMacCommandAsync(
            "tar", $"-xzf {vDownload.FilePath} -C {NodeDir} --strip-components=1",
            TimeSpan.FromMinutes(5), aCancellationToken).ConfigureAwait(false);
        var vWrite = FixServices.ConfigWriter.UpsertBlock(
            objZprofilePath(), PathBlockId, $"export PATH=\"{NodeDir}/bin:$PATH\"", CommentSyntax.Hash);
        return new FixResult(
            vExtract.Succeeded,
            FixExecution.JoinOutput(vDownload.Evidence, vExtract.ToEvidenceString(), vWrite.Evidence));
    }
}
