using TrSetup.Core.Checks;
using TrSetup.Core.Downloads;
using TrSetup.Core.Fixing;
using TrSetup.Core.Processes;

namespace TrSetup.Core.Catalog.Windows;

/// <summary>
/// F-WINCHK: "JDK for Android builds" — detects via <c>JAVA_HOME</c> FIRST, then PATH, and
/// the evidence always says which source was found (Architecture §9: multiple JDKs — Temurin,
/// Android Studio's JBR — can conflict).
/// </summary>
public sealed class WinJdkCheck : WindowsCheckBase
{
    private const string Script =
        "if ($env:JAVA_HOME -and (Test-Path \"$env:JAVA_HOME\\bin\\java.exe\")) {\n" +
        "  Write-Output \"SOURCE=JAVA_HOME ($env:JAVA_HOME)\"\n" +
        "  & \"$env:JAVA_HOME\\bin\\java.exe\" -version 2>&1\n" +
        "  exit 0\n" +
        "}\n" +
        "$vJava = Get-Command java -ErrorAction SilentlyContinue\n" +
        "if ($vJava) {\n" +
        "  Write-Output \"SOURCE=PATH ($($vJava.Source))\"\n" +
        "  java -version 2>&1\n" +
        "  exit 0\n" +
        "}\n" +
        "Write-Output 'JAVA-MISSING'\n" +
        "exit 1\n";

    /// <summary>The pinned Eclipse Temurin 17 (LTS) Windows MSI URL (Adoptium GitHub release asset).</summary>
    public const string MsiUrl =
        "https://github.com/adoptium/temurin17-binaries/releases/download/jdk-17.0.13%2B11/" +
        "OpenJDK17U-jdk_x64_windows_hotspot_17.0.13_11.msi";

    /// <summary>
    /// Creates the check.
    /// </summary>
    /// <param name="aProcessRunner">The process choke-point the detect runs through.</param>
    /// <param name="aFix">Fixer frameworks; when null the check is detect-only (no Fix button).</param>
    public WinJdkCheck(IProcessRunner aProcessRunner, CheckFixServices? aFix = null) : base(aProcessRunner, aFix)
    {
    }

    /// <inheritdoc />
    public override string? FixPreview => CanFix
        ? InstallerDownloader.BuildFixPreview(new DownloadRequest(MsiUrl, "temurin-jdk", "temurin17.msi")) +
          $"{Environment.NewLine}then (UAC) msiexec /i temurin17.msi ADDLOCAL=FeatureMain,FeatureEnvironment,FeatureJavaHome /qn"
        : null;

    /// <inheritdoc />
    public override CheckFix? FixAsync => CanFix ? FixCoreAsync : null;

    /// <inheritdoc />
    public override string Id => "win.jdk";

    /// <inheritdoc />
    public override string Title => "JDK for Android builds";

    /// <inheritdoc />
    public override CheckSeverity Severity => CheckSeverity.Required;

    /// <inheritdoc />
    public override CheckExplanation Explain => new(
        "A JDK the Android toolchain can use, resolved via JAVA_HOME first, then PATH.",
        "Gradle/sdkmanager need Java; with several JDKs installed the one that wins must be known, so the evidence names the source.",
        "Architecture §9");

    /// <inheritdoc />
    public override async Task<CheckResult> DetectAsync(CancellationToken aCancellationToken = default)
    {
        var vRun = await RunWindowsScriptAsync(Script, TimeSpan.FromSeconds(30), aCancellationToken)
            .ConfigureAwait(false);
        if (vRun.StandardOutput.Contains("JAVA-MISSING", StringComparison.Ordinal))
        {
            return CheckResult.Fail(ViaBridge(
                "No JDK found: JAVA_HOME is unset/invalid and no java on PATH."));
        }

        if (!vRun.Succeeded)
        {
            return CheckResult.Fail(ViaBridge($"Could not probe the JDK.\n{vRun.ToEvidenceString()}"));
        }

        var vLines = vRun.StandardOutput
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var vSource = vLines.FirstOrDefault(aLine => aLine.StartsWith("SOURCE=", StringComparison.Ordinal))
            ?? "SOURCE=unknown";
        var vVersion = vLines.FirstOrDefault(aLine => aLine.Contains("version", StringComparison.OrdinalIgnoreCase))
            ?? "version line not captured";
        return CheckResult.Pass(ViaBridge(
            $"JDK found via {vSource["SOURCE=".Length..]}: {vVersion}"));
    }

    private async Task<FixResult> FixCoreAsync(ConsentToken aConsent, CancellationToken aCancellationToken)
    {
        var vDownload = await FixServices!.Downloader.DownloadAsync(
            new DownloadRequest(MsiUrl, "temurin-jdk", "temurin17.msi"), null, aCancellationToken).ConfigureAwait(false);
        if (!vDownload.Succeeded)
        {
            return new FixResult(false, vDownload.Evidence);
        }

        var vCommand = new Elevation.ElevatedCommand(
            "msiexec",
            $"/i \"{vDownload.FilePath}\" ADDLOCAL=FeatureMain,FeatureEnvironment,FeatureJavaHome /qn /norestart",
            "Install Eclipse Temurin 17 JDK");
        var vFix = await RunElevatedFixAsync(vCommand, aConsent, aCancellationToken).ConfigureAwait(false);
        return new FixResult(vFix.FixerReportedSuccess, FixExecution.JoinOutput(vDownload.Evidence, vFix.RawOutput));
    }
}
