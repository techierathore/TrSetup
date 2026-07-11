using TrSetup.Core.Checks;
using TrSetup.Core.Downloads;
using TrSetup.Core.Fixing;
using TrSetup.Core.Processes;

namespace TrSetup.Core.Catalog.Windows;

/// <summary>
/// F-WINCHK: "Android SDK + sdkmanager/avdmanager" — probes the standard SDK locations
/// (%ANDROID_HOME%, %LocalAppData%\Android\Sdk) for the cmdline-tools. The fixer downloads the
/// pinned official cmdline-tools zip, unpacks it into <c>cmdline-tools\latest</c> and installs
/// platform-tools (REQ-FN-015).
/// </summary>
public sealed class WinAndroidSdkCheck : WindowsCheckBase
{
    private const string Script = AndroidSdkScripts.Locator +
        "if (Test-Path $vSdkManager) { Write-Output \"SDKMANAGER=$vSdkManager\" } else { Write-Output \"SDKMANAGER-MISSING at $vSdkManager\" }\n" +
        "if (Test-Path $vAvdManager) { Write-Output \"AVDMANAGER=$vAvdManager\" } else { Write-Output \"AVDMANAGER-MISSING at $vAvdManager\" }\n";

    /// <summary>The pinned official Android cmdline-tools (Windows) download URL the fixer fetches.</summary>
    public const string CmdlineToolsUrl = AndroidSdkScripts.CmdlineToolsUrl;

    /// <summary>
    /// Creates the check.
    /// </summary>
    /// <param name="aProcessRunner">The process choke-point the detect runs through.</param>
    /// <param name="aFix">Fixer frameworks; when null the check is detect-only (no Fix button).</param>
    public WinAndroidSdkCheck(IProcessRunner aProcessRunner, CheckFixServices? aFix = null) : base(aProcessRunner, aFix)
    {
    }

    /// <inheritdoc />
    public override string? FixPreview => CanFix
        ? InstallerDownloader.BuildFixPreview(
              new DownloadRequest(AndroidSdkScripts.CmdlineToolsUrl, "android-cmdline-tools", "cmdline-tools.zip")) +
          $"{Environment.NewLine}then unpack into %LocalAppData%\\Android\\Sdk\\cmdline-tools\\latest and install platform-tools"
        : null;

    /// <inheritdoc />
    public override CheckFix? FixAsync => CanFix ? FixCoreAsync : null;

    /// <inheritdoc />
    public override string Id => "win.android-sdk";

    /// <inheritdoc />
    public override string Title => "Android SDK cmdline-tools";

    /// <inheritdoc />
    public override CheckSeverity Severity => CheckSeverity.Required;

    /// <inheritdoc />
    public override CheckExplanation Explain => new(
        "The Android SDK with sdkmanager/avdmanager cmdline-tools in a standard location.",
        "Everything Android — system images, AVDs, the emulator — is installed and managed through these tools.",
        "WORKFLOW §0b");

    /// <inheritdoc />
    public override async Task<CheckResult> DetectAsync(CancellationToken aCancellationToken = default)
    {
        var vRun = await RunWindowsScriptAsync(Script, TimeSpan.FromSeconds(15), aCancellationToken)
            .ConfigureAwait(false);
        if (!vRun.Succeeded)
        {
            return CheckResult.Fail(ViaBridge($"Could not probe the Android SDK locations.\n{vRun.ToEvidenceString()}"));
        }

        var vOutput = vRun.StandardOutput;
        var vHasSdkManager = vOutput.Contains("SDKMANAGER=", StringComparison.Ordinal);
        var vHasAvdManager = vOutput.Contains("AVDMANAGER=", StringComparison.Ordinal);
        if (vHasSdkManager && vHasAvdManager)
        {
            return CheckResult.Pass(ViaBridge($"Android SDK cmdline-tools found. {vOutput.Trim()}"));
        }

        return CheckResult.Fail(ViaBridge(
            $"Android SDK cmdline-tools incomplete or missing. {vOutput.Trim()}"));
    }

    private async Task<FixResult> FixCoreAsync(ConsentToken aConsent, CancellationToken aCancellationToken)
    {
        var vDownload = await FixServices!.Downloader.DownloadAsync(
            new DownloadRequest(AndroidSdkScripts.CmdlineToolsUrl, "android-cmdline-tools", "cmdline-tools.zip"),
            null,
            aCancellationToken).ConfigureAwait(false);
        if (!vDownload.Succeeded)
        {
            return new FixResult(false, vDownload.Evidence);
        }

        var vScript = AndroidSdkScripts.Locator +
            "New-Item -ItemType Directory -Force -Path (Join-Path $vSdk 'cmdline-tools') | Out-Null\n" +
            $"Expand-Archive -Path '{vDownload.FilePath}' -DestinationPath (Join-Path $vSdk 'cmdline-tools') -Force\n" +
            "$vExtracted = Join-Path $vSdk 'cmdline-tools\\cmdline-tools'\n" +
            "$vLatest = Join-Path $vSdk 'cmdline-tools\\latest'\n" +
            "if (Test-Path $vExtracted) { if (Test-Path $vLatest) { Remove-Item -Recurse -Force $vLatest }; Move-Item $vExtracted $vLatest }\n" +
            "$vSdkManager = Join-Path $vLatest 'bin\\sdkmanager.bat'\n" +
            "echo y | & $vSdkManager \"platform-tools\" --sdk_root=$vSdk 2>&1\n";
        var vFix = await RunWindowsFixAsync(vScript, TimeSpan.FromMinutes(10), aCancellationToken).ConfigureAwait(false);
        return new FixResult(vFix.FixerReportedSuccess, FixExecution.JoinOutput(vDownload.Evidence, vFix.RawOutput));
    }
}
