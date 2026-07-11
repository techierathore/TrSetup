using TrSetup.Core.Checks;
using TrSetup.Core.Fixing;
using TrSetup.Core.Processes;

namespace TrSetup.Core.Catalog.Windows;

/// <summary>
/// F-WINCHK: "start-android-verify.ps1 session helper deployed" — file check in the standard
/// helper locations under the Windows user profile. The fixer writes the helper from an
/// embedded template (overwrites with identical bytes on a re-run — idempotent) (REQ-FN-015).
/// </summary>
public sealed class WinVerifyHelperCheck : WindowsCheckBase
{
    private const string Script =
        "$vPaths = @(\"$env:UserProfile\\start-android-verify.ps1\", \"$env:UserProfile\\bin\\start-android-verify.ps1\")\n" +
        "foreach ($vPath in $vPaths) { if (Test-Path $vPath) { Write-Output \"FOUND=$vPath\"; exit 0 } }\n" +
        "Write-Output 'MISSING'\n";

    private const string FixScript =
        "$vContent = @'\n" + AndroidSdkScripts.VerifyHelperScript + "'@\n" +
        "Set-Content -Path \"$env:UserProfile\\start-android-verify.ps1\" -Value $vContent -Encoding utf8\n" +
        "Write-Output \"WROTE $env:UserProfile\\start-android-verify.ps1\"\n";

    /// <summary>
    /// Creates the check.
    /// </summary>
    /// <param name="aProcessRunner">The process choke-point the detect runs through.</param>
    /// <param name="aFix">Fixer frameworks; when null the check is detect-only (no Fix button).</param>
    public WinVerifyHelperCheck(IProcessRunner aProcessRunner, CheckFixServices? aFix = null) : base(aProcessRunner, aFix)
    {
    }

    /// <inheritdoc />
    public override string? FixPreview => CanFix
        ? "write %UserProfile%\\start-android-verify.ps1 from the embedded template"
        : null;

    /// <inheritdoc />
    public override CheckFix? FixAsync => CanFix ? FixCoreAsync : null;

    /// <inheritdoc />
    public override string Id => "win.verify-helper";

    /// <inheritdoc />
    public override string Title => "start-android-verify.ps1 helper";

    /// <inheritdoc />
    public override CheckSeverity Severity => CheckSeverity.Recommended;

    /// <inheritdoc />
    public override CheckExplanation Explain => new(
        "The start-android-verify.ps1 helper that boots the AVD and Appium for a verify session.",
        "One script gives agents a reproducible way to bring the Android verify stack up; without it every session is hand-rolled.",
        "WORKFLOW §0b");

    /// <inheritdoc />
    public override async Task<CheckResult> DetectAsync(CancellationToken aCancellationToken = default)
    {
        var vRun = await RunWindowsScriptAsync(Script, TimeSpan.FromSeconds(15), aCancellationToken)
            .ConfigureAwait(false);
        if (vRun.StandardOutput.Contains("FOUND=", StringComparison.Ordinal))
        {
            return CheckResult.Pass(ViaBridge($"Helper deployed: {vRun.StandardOutput.Trim()}"));
        }

        if (vRun.StandardOutput.Contains("MISSING", StringComparison.Ordinal))
        {
            return CheckResult.Fail(ViaBridge(
                "start-android-verify.ps1 not found in %UserProfile% or %UserProfile%\\bin."));
        }

        return CheckResult.Fail(ViaBridge($"Could not probe for the helper.\n{vRun.ToEvidenceString()}"));
    }

    private Task<FixResult> FixCoreAsync(ConsentToken aConsent, CancellationToken aCancellationToken)
        => RunWindowsFixAsync(FixScript, TimeSpan.FromSeconds(30), aCancellationToken);
}
