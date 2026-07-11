using TrSetup.Core.Checks;
using TrSetup.Core.Fixing;
using TrSetup.Core.Processes;

namespace TrSetup.Core.Catalog.Windows;

/// <summary>
/// F-WINCHK: "Pixel_API_34 AVD exists" — fast-paths the AVD directory, then falls back to
/// <c>avdmanager list avd</c>. The fixer creates the AVD from the API-34 image only when it is
/// absent — an existing AVD directory short-circuits, so re-runs are a no-op (REQ-FN-015).
/// </summary>
public sealed class WinAndroidAvdCheck : WindowsCheckBase
{
    /// <summary>The reference AVD name the verify harness boots.</summary>
    public const string AvdName = "Pixel_API_34";

    private const string Script =
        "if (Test-Path \"$env:UserProfile\\.android\\avd\\Pixel_API_34.avd\") { Write-Output \"AVD-FOUND $env:UserProfile\\.android\\avd\\Pixel_API_34.avd\"; exit 0 }\n" +
        AndroidSdkScripts.Locator +
        "if (-not (Test-Path $vAvdManager)) { Write-Output 'AVDMANAGER-MISSING'; exit 1 }\n" +
        "& $vAvdManager list avd 2>&1\n";

    private const string FixScript =
        "if (Test-Path \"$env:UserProfile\\.android\\avd\\Pixel_API_34.avd\") { Write-Output 'AVD-ALREADY-EXISTS'; exit 0 }\n" +
        AndroidSdkScripts.Locator +
        "if (-not (Test-Path $vAvdManager)) { Write-Output 'AVDMANAGER-MISSING'; exit 1 }\n" +
        "echo no | & $vAvdManager create avd -n Pixel_API_34 -k \"" + AndroidSdkScripts.Api34ImagePackage + "\" -d pixel 2>&1\n";

    /// <summary>
    /// Creates the check.
    /// </summary>
    /// <param name="aProcessRunner">The process choke-point the detect runs through.</param>
    /// <param name="aFix">Fixer frameworks; when null the check is detect-only (no Fix button).</param>
    public WinAndroidAvdCheck(IProcessRunner aProcessRunner, CheckFixServices? aFix = null) : base(aProcessRunner, aFix)
    {
    }

    /// <inheritdoc />
    public override string? FixPreview => CanFix
        ? $"avdmanager create avd -n {AvdName} -k \"{AndroidSdkScripts.Api34ImagePackage}\" -d pixel (skipped if it already exists)"
        : null;

    /// <inheritdoc />
    public override CheckFix? FixAsync => CanFix ? FixCoreAsync : null;

    /// <inheritdoc />
    public override string Id => "win.avd-pixel-api34";

    /// <inheritdoc />
    public override string Title => "Pixel_API_34 AVD";

    /// <inheritdoc />
    public override CheckSeverity Severity => CheckSeverity.Required;

    /// <inheritdoc />
    public override CheckExplanation Explain => new(
        $"The reference Android Virtual Device '{AvdName}'.",
        "The Appium session helper boots exactly this AVD; verification scripts address it by name.",
        "WORKFLOW §0b");

    /// <inheritdoc />
    public override async Task<CheckResult> DetectAsync(CancellationToken aCancellationToken = default)
    {
        var vRun = await RunWindowsScriptAsync(Script, TimeSpan.FromSeconds(60), aCancellationToken)
            .ConfigureAwait(false);
        if (vRun.StandardOutput.Contains("AVD-FOUND", StringComparison.Ordinal)
            || vRun.StandardOutput.Contains(AvdName, StringComparison.OrdinalIgnoreCase))
        {
            return CheckResult.Pass(ViaBridge($"AVD '{AvdName}' exists. {FirstLine(vRun.StandardOutput)}"));
        }

        if (vRun.StandardOutput.Contains("AVDMANAGER-MISSING", StringComparison.Ordinal))
        {
            return CheckResult.Fail(ViaBridge(
                "avdmanager not found — install the Android SDK cmdline-tools first (win.android-sdk)."));
        }

        return CheckResult.Fail(ViaBridge(
            $"AVD '{AvdName}' not found (no .android\\avd entry and avdmanager list avd does not mention it)."));
    }

    private Task<FixResult> FixCoreAsync(ConsentToken aConsent, CancellationToken aCancellationToken)
        => RunWindowsFixAsync(FixScript, TimeSpan.FromMinutes(3), aCancellationToken);

    private static string FirstLine(string aOutput) =>
        aOutput.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .FirstOrDefault() ?? string.Empty;
}
