using TrSetup.Core.Checks;
using TrSetup.Core.Downloads;
using TrSetup.Core.Fixing;
using TrSetup.Core.Processes;

namespace TrSetup.Core.Profiles.Handlers;

/// <summary>
/// REQ-FN-025 — the isolated managed-runtime install check (ComfyUI). Detect looks for the
/// runtime's entrypoint under <see cref="TrSetupPaths.ToolsRoot"/><c>/&lt;runtime&gt;</c>; when
/// absent the fixer downloads the pinned official ComfyUI GitHub release — the Windows portable
/// build, which bundles its <b>own embedded Python</b> so it never collides with a system Python —
/// into the managed location and extracts it there.
/// </summary>
/// <remarks>
/// Boundary (BRD-39): TrSetup installs the runtime only. Models, workflows and providers layered on
/// top of ComfyUI are owned by TrStudioAdmin, not TrSetup — see <see cref="Explain"/>.
/// </remarks>
public sealed class RuntimeInstallCheck : ProfileHeavyCheck
{
    /// <summary>The default pinned ComfyUI release tag installed when the requirement omits <c>releaseTag</c>.</summary>
    public const string DefaultComfyUiTag = "v0.3.60";

    private readonly IProcessRunner objProcessRunner;
    private readonly CheckFixServices objFix;
    private readonly string objRuntime;

    /// <summary>
    /// Creates the check.
    /// </summary>
    /// <param name="aRequirement">The runtime-install requirement (reads <c>runtime</c> and optional <c>releaseTag</c>).</param>
    /// <param name="aProfileName">The owning profile name — the app this row is scoped to.</param>
    /// <param name="aProcessRunner">The process choke-point the extract step shells through.</param>
    /// <param name="aFix">The fixer bundle (installer downloader for the pinned release).</param>
    /// <exception cref="ArgumentNullException">Thrown when a required argument is null.</exception>
    public RuntimeInstallCheck(
        ProfileRequirement aRequirement,
        string aProfileName,
        IProcessRunner aProcessRunner,
        CheckFixServices aFix)
        : base(aRequirement, aProfileName)
    {
        objProcessRunner = aProcessRunner ?? throw new ArgumentNullException(nameof(aProcessRunner));
        objFix = aFix ?? throw new ArgumentNullException(nameof(aFix));
        objRuntime = (aRequirement.Param("runtime") ?? string.Empty).ToLowerInvariant();
    }

    /// <summary>The managed directory the runtime installs into (never a system location).</summary>
    public string RuntimeDir => Path.Combine(TrSetupPaths.ToolsRoot, objRuntime);

    /// <summary>The pinned official download URL for the configured runtime and release tag.</summary>
    public string DownloadUrl =>
        $"https://github.com/comfyanonymous/ComfyUI/releases/download/{ReleaseTag}/ComfyUI_windows_portable_nvidia.7z";

    private string ReleaseTag => Requirement.Param("releaseTag") ?? DefaultComfyUiTag;

    private string EntrypointPath => Path.Combine(RuntimeDir, "main.py");

    private string ArchiveFileName => $"comfyui-{ReleaseTag}.7z";

    private DownloadRequest Download => new(DownloadUrl, objRuntime, ArchiveFileName);

    /// <inheritdoc />
    public override string Category => ProfileBoardCategories.Runtimes;

    /// <inheritdoc />
    public override CheckExplanation Explain => new(
        "The ComfyUI runtime installed in isolation under the TrSetup-managed tools root, with its own embedded Python.",
        "TrSetup installs only the runtime here (BRD-39) — models, workflows and providers on top of it are owned by TrStudioAdmin, not TrSetup.",
        "BRD-39");

    /// <inheritdoc />
    public override string? FixPreview =>
        InstallerDownloader.BuildFixPreview(Download) +
        $"{Environment.NewLine}then extract into {RuntimeDir} (bundled embedded Python — no system-Python collision)";

    /// <inheritdoc />
    public override CheckFix? FixAsync => FixCoreAsync;

    /// <inheritdoc />
    public override Task<CheckResult> DetectAsync(CancellationToken aCancellationToken = default)
    {
        if (File.Exists(EntrypointPath))
        {
            return Task.FromResult(CheckResult.Pass($"'{objRuntime}' runtime present at {RuntimeDir} (entrypoint main.py found)."));
        }

        return Task.FromResult(CheckResult.Fail($"'{objRuntime}' runtime not installed under {RuntimeDir} (no main.py entrypoint)."));
    }

    private async Task<FixResult> FixCoreAsync(ConsentToken aConsent, CancellationToken aCancellationToken)
    {
        var vDownload = await objFix.Downloader.DownloadAsync(Download, null, aCancellationToken).ConfigureAwait(false);
        if (!vDownload.Succeeded)
        {
            return new FixResult(false, vDownload.Evidence);
        }

        Directory.CreateDirectory(RuntimeDir);
        var vExtract = await FixExecution.RunAsync(
            objProcessRunner,
            new ProcessRunRequest("tar", $"-xf \"{vDownload.FilePath}\" -C \"{RuntimeDir}\" --strip-components=1", null, TimeSpan.FromMinutes(10)),
            aCancellationToken).ConfigureAwait(false);
        return new FixResult(vExtract.FixerReportedSuccess, FixExecution.JoinOutput(vDownload.Evidence, vExtract.RawOutput));
    }
}
