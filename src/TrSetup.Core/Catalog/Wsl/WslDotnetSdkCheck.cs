using TrSetup.Core.Catalog.Probing;
using TrSetup.Core.Checks;
using TrSetup.Core.Downloads;
using TrSetup.Core.Fixing;
using TrSetup.Core.Processes;

namespace TrSetup.Core.Catalog.Wsl;

/// <summary>
/// F-WSLCHK: ".NET SDK present (9/10 as configured)" — detects via <c>dotnet --list-sdks</c>,
/// falling back to <c>~/.dotnet/dotnet</c> when <c>dotnet</c> is not on PATH. The fixer runs
/// the official <c>dotnet-install.sh</c> script into the user-local <c>~/.dotnet</c> (no sudo,
/// never collides with a system install — REQ-FN-014 / REQ-NFR-004).
/// </summary>
public sealed class WslDotnetSdkCheck : Check
{
    /// <summary>The pinned official dotnet-install script URL (Microsoft publishes no per-request checksum for it).</summary>
    public const string InstallScriptUrl = "https://dot.net/v1/dotnet-install.sh";

    /// <summary>The channel the fixer installs.</summary>
    public const string Channel = "10.0";

    private readonly IProcessRunner objProcessRunner;
    private readonly ISystemProbe objSystemProbe;
    private readonly CheckFixServices? objFix;

    /// <summary>
    /// Creates the check.
    /// </summary>
    /// <param name="aProcessRunner">The process choke-point the detect shells through.</param>
    /// <param name="aSystemProbe">Local probe used to locate the user-local SDK fallback.</param>
    /// <param name="aFix">Fixer frameworks; when null the check is detect-only (no Fix button).</param>
    public WslDotnetSdkCheck(IProcessRunner aProcessRunner, ISystemProbe aSystemProbe, CheckFixServices? aFix = null)
    {
        objProcessRunner = aProcessRunner;
        objSystemProbe = aSystemProbe;
        objFix = aFix;
    }

    private string InstallDir => Path.Combine(objSystemProbe.HomeDirectory, ".dotnet");

    private string ScriptPath => Path.Combine(TrSetupPaths.ToolsRoot, "dotnet-install", "dotnet-install.sh");

    /// <inheritdoc />
    public override string? FixPreview => objFix is null
        ? null
        : InstallerDownloader.BuildFixPreview(new DownloadRequest(InstallScriptUrl, "dotnet-install", "dotnet-install.sh")) +
          $"{Environment.NewLine}then run  bash {ScriptPath} --channel {Channel} --install-dir {InstallDir}";

    /// <inheritdoc />
    public override CheckFix? FixAsync => objFix is null ? null : FixCoreAsync;

    /// <inheritdoc />
    public override string Id => "wsl.dotnet-sdk";

    /// <inheritdoc />
    public override string Title => ".NET SDK present";

    /// <inheritdoc />
    public override string Category => BoardCategories.FrameworkCore;

    /// <inheritdoc />
    public override MachineRole Roles => MachineRole.AgentHostWsl;

    /// <inheritdoc />
    public override CheckSeverity Severity => CheckSeverity.Required;

    /// <inheritdoc />
    public override CheckExplanation Explain => new(
        "The .NET SDK (9.x/10.x) inside the WSL distro.",
        "Agents build and test the app from WSL; without the SDK no `dotnet build`/`test` works.",
        "WORKFLOW §0");

    /// <inheritdoc />
    public override async Task<CheckResult> DetectAsync(CancellationToken aCancellationToken = default)
    {
        var vRun = await ProcessProbe.RunAsync(
            objProcessRunner,
            new ProcessRunRequest("dotnet", "--list-sdks", null, TimeSpan.FromSeconds(10)),
            aCancellationToken).ConfigureAwait(false);
        if (!vRun.Succeeded)
        {
            var vFallback = Path.Combine(objSystemProbe.HomeDirectory, ".dotnet", "dotnet");
            vRun = await ProcessProbe.RunAsync(
                objProcessRunner,
                new ProcessRunRequest(vFallback, "--list-sdks", null, TimeSpan.FromSeconds(10)),
                aCancellationToken).ConfigureAwait(false);
        }

        return Interpret(vRun);
    }

    private async Task<FixResult> FixCoreAsync(ConsentToken aConsent, CancellationToken aCancellationToken)
    {
        var vDownload = await objFix!.Downloader.DownloadAsync(
            new DownloadRequest(InstallScriptUrl, "dotnet-install", "dotnet-install.sh"),
            null,
            aCancellationToken).ConfigureAwait(false);
        if (!vDownload.Succeeded)
        {
            return new FixResult(false, vDownload.Evidence);
        }

        var vRun = await FixExecution.RunAsync(
            objProcessRunner,
            new ProcessRunRequest(
                "bash",
                $"{vDownload.FilePath} --channel {Channel} --install-dir {InstallDir}",
                null,
                TimeSpan.FromMinutes(10)),
            aCancellationToken).ConfigureAwait(false);
        return new FixResult(vRun.FixerReportedSuccess, FixExecution.JoinOutput(vDownload.Evidence, vRun.RawOutput));
    }

    private static CheckResult Interpret(ProcessRunResult aRun)
    {
        if (!aRun.Succeeded || string.IsNullOrWhiteSpace(aRun.StandardOutput))
        {
            return CheckResult.Fail($".NET SDK not found (dotnet --list-sdks failed).\n{aRun.ToEvidenceString()}");
        }

        var vSdkLines = aRun.StandardOutput
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToList();
        var vModern = vSdkLines
            .Where(aLine => aLine.StartsWith("9.", StringComparison.Ordinal)
                || aLine.StartsWith("10.", StringComparison.Ordinal))
            .ToList();
        if (vModern.Count == 0)
        {
            return CheckResult.Warn(
                $"SDK(s) installed but none is 9.x/10.x: {string.Join("; ", vSdkLines)} ($ {aRun.CommandLine})");
        }

        var vVersion = vModern[^1].Split(' ')[0];
        return CheckResult.Pass($".NET SDK {vVersion} present ($ {aRun.CommandLine} → {vSdkLines.Count} SDK(s)).");
    }
}
