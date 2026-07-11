using TrSetup.Core.Catalog.Probing;
using TrSetup.Core.Checks;
using TrSetup.Core.ConfigWriting;
using TrSetup.Core.Downloads;
using TrSetup.Core.Fixing;
using TrSetup.Core.Processes;

namespace TrSetup.Core.Catalog.Wsl;

/// <summary>
/// F-WSLCHK: "Node.js present" — detects via <c>node --version</c>. The fixer downloads the
/// pinned official Node LTS tarball into the managed tools root, extracts it there, and adds
/// the <c>bin</c> directory to <c>~/.bashrc</c> via a managed block (REQ-FN-014 — user-local,
/// never collides with a system Node).
/// </summary>
public sealed class WslNodeCheck : Check
{
    /// <summary>The pinned Node.js LTS version the fixer installs.</summary>
    public const string NodeVersion = "v22.11.0";

    /// <summary>The pinned official Node.js LTS Linux tarball URL.</summary>
    public const string TarballUrl =
        "https://nodejs.org/dist/" + NodeVersion + "/node-" + NodeVersion + "-linux-x64.tar.xz";

    /// <summary>The stable managed-block id of the PATH line written into <c>~/.bashrc</c>.</summary>
    public const string PathBlockId = "wsl.node-path";

    private readonly IProcessRunner objProcessRunner;
    private readonly ISystemProbe objSystemProbe;
    private readonly CheckFixServices? objFix;

    /// <summary>
    /// Creates the check.
    /// </summary>
    /// <param name="aProcessRunner">The process choke-point the detect shells through.</param>
    /// <param name="aSystemProbe">Local probe used to locate the user home for the PATH line.</param>
    /// <param name="aFix">Fixer frameworks; when null the check is detect-only (no Fix button).</param>
    public WslNodeCheck(IProcessRunner aProcessRunner, ISystemProbe aSystemProbe, CheckFixServices? aFix = null)
    {
        objProcessRunner = aProcessRunner;
        objSystemProbe = aSystemProbe;
        objFix = aFix;
    }

    private static string NodeDir => Path.Combine(TrSetupPaths.ToolsRoot, "node");

    private string BashrcPath => Path.Combine(objSystemProbe.HomeDirectory, ".bashrc");

    /// <inheritdoc />
    public override string? FixPreview => objFix is null
        ? null
        : InstallerDownloader.BuildFixPreview(new DownloadRequest(TarballUrl, "node", "node-lts.tar.xz")) +
          $"{Environment.NewLine}then extract into {NodeDir} and add {NodeDir}/bin to PATH (managed block '{PathBlockId}')";

    /// <inheritdoc />
    public override CheckFix? FixAsync => objFix is null ? null : FixCoreAsync;

    /// <inheritdoc />
    public override string Id => "wsl.node";

    /// <inheritdoc />
    public override string Title => "Node.js present";

    /// <inheritdoc />
    public override string Category => BoardCategories.FrameworkCore;

    /// <inheritdoc />
    public override MachineRole Roles => MachineRole.AgentHostWsl;

    /// <inheritdoc />
    public override CheckSeverity Severity => CheckSeverity.Required;

    /// <inheritdoc />
    public override CheckExplanation Explain => new(
        "The Node.js runtime inside the WSL distro.",
        "Playwright and the Appium client tooling are npm packages; nothing browser-side runs without Node.",
        "WORKFLOW §0");

    /// <inheritdoc />
    public override async Task<CheckResult> DetectAsync(CancellationToken aCancellationToken = default)
    {
        var vRun = await ProcessProbe.RunAsync(
            objProcessRunner,
            new ProcessRunRequest("node", "--version", null, TimeSpan.FromSeconds(10)),
            aCancellationToken).ConfigureAwait(false);
        if (!vRun.Succeeded || string.IsNullOrWhiteSpace(vRun.StandardOutput))
        {
            return CheckResult.Fail($"Node.js not found.\n{vRun.ToEvidenceString()}");
        }

        return CheckResult.Pass($"Node.js {vRun.StandardOutput.Trim()} present ($ node --version).");
    }

    private async Task<FixResult> FixCoreAsync(ConsentToken aConsent, CancellationToken aCancellationToken)
    {
        var vDownload = await objFix!.Downloader.DownloadAsync(
            new DownloadRequest(TarballUrl, "node", "node-lts.tar.xz"), null, aCancellationToken).ConfigureAwait(false);
        if (!vDownload.Succeeded)
        {
            return new FixResult(false, vDownload.Evidence);
        }

        Directory.CreateDirectory(NodeDir);
        var vExtract = await FixExecution.RunAsync(
            objProcessRunner,
            new ProcessRunRequest("tar", $"-xJf {vDownload.FilePath} -C {NodeDir} --strip-components=1", null, TimeSpan.FromMinutes(5)),
            aCancellationToken).ConfigureAwait(false);
        var vWrite = objFix.ConfigWriter.UpsertBlock(
            BashrcPath, PathBlockId, $"export PATH=\"{NodeDir}/bin:$PATH\"", CommentSyntax.Hash);
        return new FixResult(
            vExtract.FixerReportedSuccess,
            FixExecution.JoinOutput(vDownload.Evidence, vExtract.RawOutput, vWrite.Evidence));
    }
}
