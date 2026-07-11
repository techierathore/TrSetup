using TrSetup.Core.Checks;
using TrSetup.Core.Downloads;
using TrSetup.Core.Fixing;
using TrSetup.Core.Processes;

namespace TrSetup.Core.Catalog.Windows;

/// <summary>
/// F-WINCHK: "Node + npm" — detects both on the Windows side. The fixer downloads the pinned
/// official Node LTS MSI and installs it silently through a visible UAC child (REQ-FN-015 /
/// REQ-FN-020 — the elevation is user-consented and visible, never hidden).
/// </summary>
public sealed class WinNodeCheck : WindowsCheckBase
{
    /// <summary>The pinned Node.js LTS version the fixer installs.</summary>
    public const string NodeVersion = "v22.11.0";

    /// <summary>The pinned official Node.js LTS Windows MSI URL.</summary>
    public const string MsiUrl =
        "https://nodejs.org/dist/" + NodeVersion + "/node-" + NodeVersion + "-x64.msi";

    private const string Script =
        "$vNode = Get-Command node -ErrorAction SilentlyContinue\n" +
        "$vNpm = Get-Command npm -ErrorAction SilentlyContinue\n" +
        "if ($vNode) { Write-Output \"NODE=$(node --version)\" } else { Write-Output 'NODE-MISSING' }\n" +
        "if ($vNpm) { Write-Output \"NPM=$(npm --version)\" } else { Write-Output 'NPM-MISSING' }\n";

    /// <summary>
    /// Creates the check.
    /// </summary>
    /// <param name="aProcessRunner">The process choke-point the detect runs through.</param>
    /// <param name="aFix">Fixer frameworks; when null the check is detect-only (no Fix button).</param>
    public WinNodeCheck(IProcessRunner aProcessRunner, CheckFixServices? aFix = null) : base(aProcessRunner, aFix)
    {
    }

    /// <inheritdoc />
    public override string? FixPreview => CanFix
        ? InstallerDownloader.BuildFixPreview(new DownloadRequest(MsiUrl, "node", "node-lts.msi")) +
          $"{Environment.NewLine}then (UAC) msiexec /i node-lts.msi /qn /norestart"
        : null;

    /// <inheritdoc />
    public override CheckFix? FixAsync => CanFix ? FixCoreAsync : null;

    /// <inheritdoc />
    public override string Id => "win.node";

    /// <inheritdoc />
    public override string Title => "Node.js + npm";

    /// <inheritdoc />
    public override CheckSeverity Severity => CheckSeverity.Required;

    /// <inheritdoc />
    public override CheckExplanation Explain => new(
        "Node.js and npm on the Windows host.",
        "Appium and its drivers are npm packages; the device host cannot install or run them without Node.",
        "WORKFLOW §0b");

    /// <inheritdoc />
    public override async Task<CheckResult> DetectAsync(CancellationToken aCancellationToken = default)
    {
        var vRun = await RunWindowsScriptAsync(Script, TimeSpan.FromSeconds(20), aCancellationToken)
            .ConfigureAwait(false);
        if (!vRun.Succeeded)
        {
            return CheckResult.Fail(ViaBridge($"Could not probe Node/npm.\n{vRun.ToEvidenceString()}"));
        }

        var vOutput = vRun.StandardOutput;
        var vMissing = new List<string>();
        if (vOutput.Contains("NODE-MISSING", StringComparison.Ordinal))
        {
            vMissing.Add("node");
        }

        if (vOutput.Contains("NPM-MISSING", StringComparison.Ordinal))
        {
            vMissing.Add("npm");
        }

        if (vMissing.Count > 0)
        {
            return CheckResult.Fail(ViaBridge($"Missing on the Windows host: {string.Join(", ", vMissing)}."));
        }

        return CheckResult.Pass(ViaBridge($"Node/npm present. {vOutput.Trim().Replace("\n", "; ")}"));
    }

    private async Task<FixResult> FixCoreAsync(ConsentToken aConsent, CancellationToken aCancellationToken)
    {
        var vDownload = await FixServices!.Downloader.DownloadAsync(
            new DownloadRequest(MsiUrl, "node", "node-lts.msi"), null, aCancellationToken).ConfigureAwait(false);
        if (!vDownload.Succeeded)
        {
            return new FixResult(false, vDownload.Evidence);
        }

        var vCommand = new Elevation.ElevatedCommand(
            "msiexec", $"/i \"{vDownload.FilePath}\" /qn /norestart", "Install Node.js LTS");
        var vFix = await RunElevatedFixAsync(vCommand, aConsent, aCancellationToken).ConfigureAwait(false);
        return new FixResult(vFix.FixerReportedSuccess, FixExecution.JoinOutput(vDownload.Evidence, vFix.RawOutput));
    }
}
