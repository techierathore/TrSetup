using TrSetup.Core.Checks;
using TrSetup.Core.Fixing;
using TrSetup.Core.Processes;

namespace TrSetup.Core.Catalog.Mac;

/// <summary>
/// F-MACCHK: "Xcode / Command-Line Tools" — detects via <c>xcode-select -p</c>. The fixer runs
/// <c>xcode-select --install</c> to install the Command-Line Tools (idempotent — a no-op when
/// already present); full Xcode from the App Store remains the one genuine manual step and
/// never grows an auto-installer (REQ-FN-016).
/// </summary>
public sealed class MacXcodeCheck : MacCheckBase
{
    /// <summary>
    /// Creates the check.
    /// </summary>
    /// <param name="aProcessRunner">The process choke-point the detect shells through.</param>
    /// <param name="aFix">Fixer frameworks; when null the check is detect-only (no Fix button).</param>
    public MacXcodeCheck(IProcessRunner aProcessRunner, CheckFixServices? aFix = null) : base(aProcessRunner, aFix)
    {
    }

    /// <inheritdoc />
    public override string? FixPreview => CanFix
        ? "xcode-select --install (Command-Line Tools only; full Xcode stays a manual App Store install)"
        : null;

    /// <inheritdoc />
    public override CheckFix? FixAsync => CanFix ? FixCoreAsync : null;

    /// <inheritdoc />
    public override string Id => "mac.xcode-clt";

    /// <inheritdoc />
    public override string Title => "Xcode / Command-Line Tools";

    /// <inheritdoc />
    public override CheckSeverity Severity => CheckSeverity.Required;

    /// <inheritdoc />
    public override CheckExplanation Explain => new(
        "The Xcode developer directory (full Xcode or the Command-Line Tools).",
        "Everything Apple-side — simulators, code signing, xcuitest — sits on top of the Xcode toolchain.",
        "WORKFLOW §0b");

    /// <inheritdoc />
    public override async Task<CheckResult> DetectAsync(CancellationToken aCancellationToken = default)
    {
        var vRun = await RunMacCommandAsync("xcode-select", "-p", TimeSpan.FromSeconds(10), aCancellationToken)
            .ConfigureAwait(false);
        if (!vRun.Succeeded || string.IsNullOrWhiteSpace(vRun.StandardOutput))
        {
            return CheckResult.Fail(
                $"No Xcode developer directory (xcode-select -p failed) — run xcode-select --install, or install Xcode from the App Store.\n{vRun.ToEvidenceString()}");
        }

        var vPath = vRun.StandardOutput.Trim();
        var vIsFullXcode = vPath.Contains("Xcode.app", StringComparison.OrdinalIgnoreCase);
        return CheckResult.Pass(
            $"{(vIsFullXcode ? "Full Xcode" : "Command-Line Tools")} active at {vPath} ($ xcode-select -p).");
    }

    private Task<FixResult> FixCoreAsync(ConsentToken aConsent, CancellationToken aCancellationToken)
        => RunMacFixAsync("xcode-select", "--install", TimeSpan.FromMinutes(30), aCancellationToken);
}
