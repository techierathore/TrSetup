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
    /// <summary>The pinned Node.js LTS version the fixer installs.</summary>
    public const string NodeVersion = "v22.11.0";

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

    /// <inheritdoc />
    public override async Task<CheckResult> DetectAsync(CancellationToken aCancellationToken = default)
    {
        var vRun = await RunMacCommandAsync("node", "--version", TimeSpan.FromSeconds(10), aCancellationToken)
            .ConfigureAwait(false);
        if (!vRun.Succeeded || string.IsNullOrWhiteSpace(vRun.StandardOutput))
        {
            return CheckResult.Fail($"Node.js not found on the Mac.\n{vRun.ToEvidenceString()}");
        }

        return CheckResult.Pass($"Node.js {vRun.StandardOutput.Trim()} present ($ node --version).");
    }

    private async Task<FixResult> FixCoreAsync(ConsentToken aConsent, CancellationToken aCancellationToken)
    {
        var vDownload = await FixServices!.Downloader.DownloadAsync(
            new DownloadRequest(TarballUrl, "node", "node-lts.tar.gz"), null, aCancellationToken).ConfigureAwait(false);
        if (!vDownload.Succeeded)
        {
            return new FixResult(false, vDownload.Evidence);
        }

        Directory.CreateDirectory(NodeDir);
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
