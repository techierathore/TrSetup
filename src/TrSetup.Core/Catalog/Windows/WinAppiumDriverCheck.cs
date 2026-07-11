using TrSetup.Core.Checks;
using TrSetup.Core.Fixing;
using TrSetup.Core.Processes;

namespace TrSetup.Core.Catalog.Windows;

/// <summary>
/// F-WINCHK: "Appium installed + uiautomator2 driver" — <c>appium --version</c> plus
/// <c>appium driver list --installed</c>. The fixer installs Appium globally and adds the
/// uiautomator2 driver (both idempotent — a re-run is a no-op) (REQ-FN-015).
/// </summary>
public sealed class WinAppiumDriverCheck : WindowsCheckBase
{
    private const string Script =
        "$vAppium = Get-Command appium -ErrorAction SilentlyContinue\n" +
        "if (-not $vAppium) { Write-Output 'APPIUM-MISSING'; exit 1 }\n" +
        "Write-Output \"APPIUM=$(appium --version)\"\n" +
        "appium driver list --installed 2>&1\n";

    private const string FixScript =
        "npm install -g appium 2>&1\n" +
        "if (-not (appium driver list --installed 2>&1 | Select-String 'uiautomator2')) { appium driver install uiautomator2 2>&1 }\n" +
        "else { Write-Output 'uiautomator2 already installed' }\n";

    /// <summary>
    /// Creates the check.
    /// </summary>
    /// <param name="aProcessRunner">The process choke-point the detect runs through.</param>
    /// <param name="aFix">Fixer frameworks; when null the check is detect-only (no Fix button).</param>
    public WinAppiumDriverCheck(IProcessRunner aProcessRunner, CheckFixServices? aFix = null) : base(aProcessRunner, aFix)
    {
    }

    /// <inheritdoc />
    public override string? FixPreview => CanFix
        ? "npm install -g appium && appium driver install uiautomator2"
        : null;

    /// <inheritdoc />
    public override CheckFix? FixAsync => CanFix ? FixCoreAsync : null;

    /// <inheritdoc />
    public override string Id => "win.appium-uiautomator2";

    /// <inheritdoc />
    public override string Title => "Appium + uiautomator2 driver";

    /// <inheritdoc />
    public override CheckSeverity Severity => CheckSeverity.Required;

    /// <inheritdoc />
    public override CheckExplanation Explain => new(
        "The Appium server with the uiautomator2 Android driver installed.",
        "The MAUI Android head is driven element-by-element over Appium/uiautomator2 during verification.",
        "WORKFLOW §0b");

    /// <inheritdoc />
    public override async Task<CheckResult> DetectAsync(CancellationToken aCancellationToken = default)
    {
        var vRun = await RunWindowsScriptAsync(Script, TimeSpan.FromSeconds(30), aCancellationToken)
            .ConfigureAwait(false);
        if (vRun.StandardOutput.Contains("APPIUM-MISSING", StringComparison.Ordinal))
        {
            return CheckResult.Fail(ViaBridge("Appium not found on the Windows host (npm i -g appium)."));
        }

        var vCombined = vRun.StandardOutput + vRun.StandardError;
        if (vCombined.Contains("uiautomator2", StringComparison.OrdinalIgnoreCase))
        {
            return CheckResult.Pass(ViaBridge(
                $"Appium present with uiautomator2 driver. {FirstLine(vRun.StandardOutput)}"));
        }

        return CheckResult.Fail(ViaBridge(
            $"Appium present but the uiautomator2 driver is not installed. {FirstLine(vRun.StandardOutput)}"));
    }

    private Task<FixResult> FixCoreAsync(ConsentToken aConsent, CancellationToken aCancellationToken)
        => RunWindowsFixAsync(FixScript, TimeSpan.FromMinutes(10), aCancellationToken);

    private static string FirstLine(string aOutput) =>
        aOutput.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .FirstOrDefault() ?? string.Empty;
}
