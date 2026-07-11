using TrSetup.Core.Catalog.Probing;
using TrSetup.Core.Checks;
using TrSetup.Core.Fixing;
using TrSetup.Core.Processes;

namespace TrSetup.Core.Catalog.Windows;

/// <summary>
/// F-WINCHK: "Appium answers on :4723" — live HTTP GET of the local Appium status endpoint
/// (localhost works both natively and from WSL thanks to mirrored networking). The fixer runs
/// the deployed <c>start-android-verify.ps1</c> helper to boot the emulator + Appium session
/// (REQ-FN-015).
/// </summary>
public sealed class WinAppiumSessionCheck : Check
{
    /// <summary>The probed local Appium status URL.</summary>
    public const string StatusUrl = "http://localhost:4723/status";

    private const string FixScript =
        "$vPath = \"$env:UserProfile\\start-android-verify.ps1\"\n" +
        "if (-not (Test-Path $vPath)) { Write-Output 'HELPER-MISSING — deploy start-android-verify.ps1 first (win.verify-helper)'; exit 1 }\n" +
        "& $vPath 2>&1\n";

    private readonly IHttpStatusProbe objHttpProbe;
    private readonly IProcessRunner? objProcessRunner;

    /// <summary>
    /// Creates the check.
    /// </summary>
    /// <param name="aHttpProbe">The HTTP reachability probe.</param>
    /// <param name="aProcessRunner">The process choke-point the fix runs the helper through; when null the check is detect-only.</param>
    public WinAppiumSessionCheck(IHttpStatusProbe aHttpProbe, IProcessRunner? aProcessRunner = null)
    {
        objHttpProbe = aHttpProbe;
        objProcessRunner = aProcessRunner;
    }

    /// <inheritdoc />
    public override string? FixPreview => objProcessRunner is null
        ? null
        : "run %UserProfile%\\start-android-verify.ps1 (boots the emulator + Appium session)";

    /// <inheritdoc />
    public override CheckFix? FixAsync => objProcessRunner is null ? null : FixCoreAsync;

    /// <inheritdoc />
    public override string Id => "win.appium-session";

    /// <inheritdoc />
    public override string Title => "Appium answers on :4723";

    /// <inheritdoc />
    public override string Category => BoardCategories.FrameworkCore;

    /// <inheritdoc />
    public override MachineRole Roles => MachineRole.DeviceHostWindows;

    /// <inheritdoc />
    public override CheckSeverity Severity => CheckSeverity.Recommended;

    /// <inheritdoc />
    public override CheckExplanation Explain => new(
        "A live Appium server answering GET /status on port 4723 of the Windows host.",
        "Installed-but-not-running Appium still blocks verification; this proves the session endpoint actually answers.",
        "WORKFLOW §0b");

    /// <inheritdoc />
    public override async Task<CheckResult> DetectAsync(CancellationToken aCancellationToken = default)
    {
        var vProbe = await objHttpProbe.GetAsync(StatusUrl, aCancellationToken).ConfigureAwait(false);
        if (vProbe.IsSuccess)
        {
            return CheckResult.Pass($"Appium answering: GET {StatusUrl} → HTTP {vProbe.StatusCode}. {vProbe.Body}".TrimEnd());
        }

        if (vProbe.IsReachable)
        {
            return CheckResult.Warn(
                $"GET {StatusUrl} answered HTTP {vProbe.StatusCode} — port 4723 is bound but not by a healthy Appium.");
        }

        return CheckResult.Fail(
            $"Nothing answering on {StatusUrl} ({vProbe.Error}). Run the start-android-verify.ps1 helper to boot the session.");
    }

    private Task<FixResult> FixCoreAsync(ConsentToken aConsent, CancellationToken aCancellationToken)
        => FixExecution.RunAsync(
            objProcessRunner!,
            WindowsCommandBridge.BuildPowerShell(FixScript, TimeSpan.FromMinutes(3)),
            aCancellationToken);
}
