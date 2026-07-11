using TrSetup.Core.Checks;
using TrSetup.Core.Fixing;
using TrSetup.Core.Processes;

namespace TrSetup.Core.Catalog.Windows;

/// <summary>
/// F-WINCHK: "API-34 system image installed" — runs <c>sdkmanager --list_installed</c> and
/// looks for a <c>system-images;android-34</c> entry. The fixer runs
/// <c>sdkmanager "system-images;android-34;google_apis;x86_64"</c> (idempotent — a re-run is a
/// no-op for an already-installed package) (REQ-FN-015).
/// </summary>
public sealed class WinAndroidApi34ImageCheck : WindowsCheckBase
{
    private const string Script = AndroidSdkScripts.Locator +
        "if (-not (Test-Path $vSdkManager)) { Write-Output 'SDKMANAGER-MISSING'; exit 1 }\n" +
        "& $vSdkManager --list_installed 2>&1\n";

    private const string FixScript = AndroidSdkScripts.Locator +
        "if (-not (Test-Path $vSdkManager)) { Write-Output 'SDKMANAGER-MISSING'; exit 1 }\n" +
        "echo y | & $vSdkManager \"" + AndroidSdkScripts.Api34ImagePackage + "\" --sdk_root=$vSdk 2>&1\n";

    /// <summary>
    /// Creates the check.
    /// </summary>
    /// <param name="aProcessRunner">The process choke-point the detect runs through.</param>
    /// <param name="aFix">Fixer frameworks; when null the check is detect-only (no Fix button).</param>
    public WinAndroidApi34ImageCheck(IProcessRunner aProcessRunner, CheckFixServices? aFix = null)
        : base(aProcessRunner, aFix)
    {
    }

    /// <inheritdoc />
    public override string? FixPreview => CanFix
        ? $"sdkmanager \"{AndroidSdkScripts.Api34ImagePackage}\""
        : null;

    /// <inheritdoc />
    public override CheckFix? FixAsync => CanFix ? FixCoreAsync : null;

    /// <inheritdoc />
    public override string Id => "win.api34-image";

    /// <inheritdoc />
    public override string Title => "Android API-34 system image";

    /// <inheritdoc />
    public override CheckSeverity Severity => CheckSeverity.Required;

    /// <inheritdoc />
    public override CheckExplanation Explain => new(
        "The Android 14 (API-34) emulator system image.",
        "The reference AVD (Pixel_API_34) boots this image; MAUI Android verification has nothing to run on without it.",
        "WORKFLOW §0b");

    /// <inheritdoc />
    public override async Task<CheckResult> DetectAsync(CancellationToken aCancellationToken = default)
    {
        var vRun = await RunWindowsScriptAsync(Script, TimeSpan.FromSeconds(60), aCancellationToken)
            .ConfigureAwait(false);
        if (vRun.StandardOutput.Contains("SDKMANAGER-MISSING", StringComparison.Ordinal))
        {
            return CheckResult.Fail(ViaBridge(
                "sdkmanager not found — install the Android SDK cmdline-tools first (win.android-sdk)."));
        }

        if (!vRun.Succeeded)
        {
            return CheckResult.Fail(ViaBridge($"sdkmanager --list_installed failed.\n{vRun.ToEvidenceString()}"));
        }

        if (vRun.StandardOutput.Contains("system-images;android-34", StringComparison.OrdinalIgnoreCase))
        {
            return CheckResult.Pass(ViaBridge(
                "API-34 system image installed (sdkmanager --list_installed contains system-images;android-34)."));
        }

        return CheckResult.Fail(ViaBridge(
            "No system-images;android-34 entry in sdkmanager --list_installed — the API-34 image is not installed."));
    }

    private Task<FixResult> FixCoreAsync(ConsentToken aConsent, CancellationToken aCancellationToken)
        => RunWindowsFixAsync(FixScript, TimeSpan.FromMinutes(15), aCancellationToken);
}
