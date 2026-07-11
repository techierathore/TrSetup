using TrSetup.Core.Checks;
using TrSetup.Core.Fixing;
using TrSetup.Core.Processes;

namespace TrSetup.Core.Catalog.Mac;

/// <summary>
/// F-MACCHK: "Appium + xcuitest + mac2 drivers" — <c>appium --version</c> plus
/// <c>appium driver list --installed</c>, requiring BOTH Apple drivers. The fixer installs
/// Appium globally and adds each missing driver (guarded, so a re-run is a no-op) (REQ-FN-016).
/// </summary>
public sealed class MacAppiumDriversCheck : MacCheckBase
{
    private const string FixArguments =
        "-c \"npm install -g appium && " +
        "{ appium driver list --installed 2>&1 | grep -q xcuitest || appium driver install xcuitest; } && " +
        "{ appium driver list --installed 2>&1 | grep -q mac2 || appium driver install mac2; }\"";

    /// <summary>
    /// Creates the check.
    /// </summary>
    /// <param name="aProcessRunner">The process choke-point the detect shells through.</param>
    /// <param name="aFix">Fixer frameworks; when null the check is detect-only (no Fix button).</param>
    public MacAppiumDriversCheck(IProcessRunner aProcessRunner, CheckFixServices? aFix = null) : base(aProcessRunner, aFix)
    {
    }

    /// <inheritdoc />
    public override string? FixPreview => CanFix
        ? "npm install -g appium && appium driver install xcuitest && appium driver install mac2"
        : null;

    /// <inheritdoc />
    public override CheckFix? FixAsync => CanFix ? FixCoreAsync : null;

    /// <inheritdoc />
    public override string Id => "mac.appium-drivers";

    /// <inheritdoc />
    public override string Title => "Appium + xcuitest + mac2 drivers";

    /// <inheritdoc />
    public override CheckSeverity Severity => CheckSeverity.Required;

    /// <inheritdoc />
    public override CheckExplanation Explain => new(
        "The Appium server with the xcuitest (iOS Simulator) and mac2 (Mac Catalyst) drivers.",
        "iOS heads are driven via xcuitest and Catalyst heads via mac2; verification needs both drivers installed.",
        "WORKFLOW §0b");

    /// <inheritdoc />
    public override async Task<CheckResult> DetectAsync(CancellationToken aCancellationToken = default)
    {
        var vVersionRun = await RunMacCommandAsync("appium", "--version", TimeSpan.FromSeconds(30), aCancellationToken)
            .ConfigureAwait(false);
        if (!vVersionRun.Succeeded)
        {
            return CheckResult.Fail($"Appium not found on the Mac (npm i -g appium).\n{vVersionRun.ToEvidenceString()}");
        }

        var vDriversRun = await RunMacCommandAsync(
            "appium", "driver list --installed", TimeSpan.FromSeconds(30), aCancellationToken).ConfigureAwait(false);
        var vCombined = vDriversRun.StandardOutput + vDriversRun.StandardError;
        var vMissing = new List<string>();
        if (!vCombined.Contains("xcuitest", StringComparison.OrdinalIgnoreCase))
        {
            vMissing.Add("xcuitest");
        }

        if (!vCombined.Contains("mac2", StringComparison.OrdinalIgnoreCase))
        {
            vMissing.Add("mac2");
        }

        if (vMissing.Count > 0)
        {
            return CheckResult.Fail(
                $"Appium {vVersionRun.StandardOutput.Trim()} present but missing driver(s): {string.Join(", ", vMissing)} ($ appium driver list --installed).");
        }

        return CheckResult.Pass(
            $"Appium {vVersionRun.StandardOutput.Trim()} with xcuitest + mac2 drivers installed.");
    }

    private Task<FixResult> FixCoreAsync(ConsentToken aConsent, CancellationToken aCancellationToken)
        => RunMacFixAsync("bash", FixArguments, TimeSpan.FromMinutes(10), aCancellationToken);
}
