using TrSetup.Core.Checks;
using TrSetup.Core.Downloads;
using TrSetup.Core.Fixing;
using TrSetup.Core.Processes;

namespace TrSetup.Core.Catalog.Mac;

/// <summary>
/// F-MACCHK: ".NET SDK + MAUI workload" — <c>dotnet workload list</c> (its failure also
/// exposes a missing SDK). The fixer installs the SDK into the user-local <c>~/.dotnet</c> via
/// the official <c>dotnet-install.sh</c> (no sudo), then installs the maui workload into it
/// (REQ-FN-016 / REQ-NFR-004).
/// </summary>
public sealed class MacDotnetMauiCheck : MacCheckBase
{
    /// <summary>The pinned official dotnet-install script URL (no per-request checksum published).</summary>
    public const string InstallScriptUrl = "https://dot.net/v1/dotnet-install.sh";

    /// <summary>The channel the fixer installs.</summary>
    public const string Channel = "10.0";

    /// <summary>
    /// Creates the check.
    /// </summary>
    /// <param name="aProcessRunner">The process choke-point the detect shells through.</param>
    /// <param name="aFix">Fixer frameworks; when null the check is detect-only (no Fix button).</param>
    public MacDotnetMauiCheck(IProcessRunner aProcessRunner, CheckFixServices? aFix = null) : base(aProcessRunner, aFix)
    {
    }

    private static string DotnetDir => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".dotnet");

    /// <inheritdoc />
    public override string? FixPreview => CanFix
        ? InstallerDownloader.BuildFixPreview(new DownloadRequest(InstallScriptUrl, "dotnet-install", "dotnet-install.sh")) +
          $"{Environment.NewLine}then run  bash dotnet-install.sh --channel {Channel} --install-dir {DotnetDir}" +
          $"{Environment.NewLine}then  {DotnetDir}/dotnet workload install maui"
        : null;

    /// <inheritdoc />
    public override CheckFix? FixAsync => CanFix ? FixCoreAsync : null;

    /// <inheritdoc />
    public override string Id => "mac.dotnet-maui";

    /// <inheritdoc />
    public override string Title => ".NET SDK + MAUI workload";

    /// <inheritdoc />
    public override CheckSeverity Severity => CheckSeverity.Required;

    /// <inheritdoc />
    public override CheckExplanation Explain => new(
        "The .NET SDK on the Mac with the maui workload installed.",
        "The Mac builds and runs the iOS / Mac Catalyst heads; both need the SDK plus the maui workload.",
        "WORKFLOW §0b");

    /// <inheritdoc />
    public override async Task<CheckResult> DetectAsync(CancellationToken aCancellationToken = default)
    {
        var vRun = await RunMacCommandAsync("dotnet", "workload list", TimeSpan.FromSeconds(60), aCancellationToken)
            .ConfigureAwait(false);
        if (!vRun.Succeeded)
        {
            return CheckResult.Fail($".NET SDK not usable (dotnet workload list failed).\n{vRun.ToEvidenceString()}");
        }

        if (vRun.StandardOutput.Contains("maui", StringComparison.OrdinalIgnoreCase))
        {
            return CheckResult.Pass(".NET SDK present and MAUI workload installed ($ dotnet workload list).");
        }

        return CheckResult.Fail(
            ".NET SDK present but the MAUI workload is not installed ($ dotnet workload list has no 'maui' entry).");
    }

    private async Task<FixResult> FixCoreAsync(ConsentToken aConsent, CancellationToken aCancellationToken)
    {
        var vDownload = await FixServices!.Downloader.DownloadAsync(
            new DownloadRequest(InstallScriptUrl, "dotnet-install", "dotnet-install.sh"),
            null,
            aCancellationToken).ConfigureAwait(false);
        if (!vDownload.Succeeded)
        {
            return new FixResult(false, vDownload.Evidence);
        }

        var vInstall = await RunMacCommandAsync(
            "bash", $"{vDownload.FilePath} --channel {Channel} --install-dir {DotnetDir}",
            TimeSpan.FromMinutes(10), aCancellationToken).ConfigureAwait(false);
        var vWorkload = await RunMacCommandAsync(
            $"{DotnetDir}/dotnet", "workload install maui", TimeSpan.FromMinutes(10), aCancellationToken).ConfigureAwait(false);
        return new FixResult(
            vInstall.Succeeded && vWorkload.Succeeded,
            FixExecution.JoinOutput(vDownload.Evidence, vInstall.ToEvidenceString(), vWorkload.ToEvidenceString()));
    }
}
