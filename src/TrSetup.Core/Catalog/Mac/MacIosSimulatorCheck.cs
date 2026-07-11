using TrSetup.Core.Checks;
using TrSetup.Core.Fixing;
using TrSetup.Core.Processes;

namespace TrSetup.Core.Catalog.Mac;

/// <summary>
/// F-MACCHK: "iOS Simulator runtime present" — <c>xcrun simctl list runtimes</c>, looking
/// for an installed iOS runtime. The fixer runs <c>xcodebuild -downloadPlatform iOS</c>
/// (idempotent — a no-op when the runtime is already present) (REQ-FN-016).
/// </summary>
public sealed class MacIosSimulatorCheck : MacCheckBase
{
    /// <summary>
    /// Creates the check.
    /// </summary>
    /// <param name="aProcessRunner">The process choke-point the detect shells through.</param>
    /// <param name="aFix">Fixer frameworks; when null the check is detect-only (no Fix button).</param>
    public MacIosSimulatorCheck(IProcessRunner aProcessRunner, CheckFixServices? aFix = null) : base(aProcessRunner, aFix)
    {
    }

    /// <inheritdoc />
    public override string? FixPreview => CanFix ? "xcodebuild -downloadPlatform iOS" : null;

    /// <inheritdoc />
    public override CheckFix? FixAsync => CanFix ? FixCoreAsync : null;

    /// <inheritdoc />
    public override string Id => "mac.ios-simulator";

    /// <inheritdoc />
    public override string Title => "iOS Simulator runtime";

    /// <inheritdoc />
    public override CheckSeverity Severity => CheckSeverity.Required;

    /// <inheritdoc />
    public override CheckExplanation Explain => new(
        "An installed iOS Simulator runtime (xcodebuild -downloadPlatform iOS).",
        "The iOS head is verified on the Simulator; without a runtime there is no device to boot.",
        "WORKFLOW §0b");

    /// <inheritdoc />
    public override async Task<CheckResult> DetectAsync(CancellationToken aCancellationToken = default)
    {
        var vRun = await RunMacCommandAsync(
            "xcrun", "simctl list runtimes", TimeSpan.FromSeconds(30), aCancellationToken).ConfigureAwait(false);
        if (!vRun.Succeeded)
        {
            return CheckResult.Fail($"Could not list Simulator runtimes (is Xcode installed?).\n{vRun.ToEvidenceString()}");
        }

        var vIosLine = vRun.StandardOutput
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .FirstOrDefault(aLine => aLine.StartsWith("iOS ", StringComparison.OrdinalIgnoreCase));
        if (vIosLine is null)
        {
            return CheckResult.Fail(
                "No iOS Simulator runtime installed ($ xcrun simctl list runtimes) — run xcodebuild -downloadPlatform iOS.");
        }

        return CheckResult.Pass($"iOS Simulator runtime present: {vIosLine} ($ xcrun simctl list runtimes).");
    }

    private Task<FixResult> FixCoreAsync(ConsentToken aConsent, CancellationToken aCancellationToken)
        => RunMacFixAsync("xcodebuild", "-downloadPlatform iOS", TimeSpan.FromMinutes(30), aCancellationToken);
}
