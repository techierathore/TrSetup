using TrSetup.Core.Catalog.Probing;
using TrSetup.Core.Checks;
using TrSetup.Core.Elevation;
using TrSetup.Core.Fixing;
using TrSetup.Core.Processes;

namespace TrSetup.Core.Catalog.Wsl;

/// <summary>
/// F-WSLCHK: "Headless-Chromium apt libs (§0 list)" — <c>dpkg -s</c> probes each of the
/// 15 shared libraries headless Chromium needs, accepting the Ubuntu 24.04 <c>t64</c>
/// package renames as equivalent.
/// </summary>
public sealed class WslChromiumLibsCheck : Check
{
    /// <summary>The WORKFLOW §0 apt library list headless Chromium links against.</summary>
    public static readonly IReadOnlyList<string> RequiredPackages = new[]
    {
        "libnss3", "libnspr4", "libatk1.0-0", "libatk-bridge2.0-0", "libcups2",
        "libdrm2", "libxkbcommon0", "libxcomposite1", "libxdamage1", "libxfixes3",
        "libxrandr2", "libgbm1", "libpango-1.0-0", "libcairo2", "libasound2"
    };

    private readonly IProcessRunner objProcessRunner;
    private readonly bool objCanFix;

    /// <summary>
    /// Creates the check.
    /// </summary>
    /// <param name="aProcessRunner">The process choke-point the dpkg probe runs through.</param>
    /// <param name="aFix">Fixer frameworks; when null the check is detect-only (no Fix button).</param>
    public WslChromiumLibsCheck(IProcessRunner aProcessRunner, CheckFixServices? aFix = null)
    {
        objProcessRunner = aProcessRunner;
        objCanFix = aFix is not null;
    }

    /// <summary>The exact apt command the fix hands off to the user's terminal.</summary>
    private static ElevatedCommand AptCommand =>
        new("apt-get", $"install -y {string.Join(' ', RequiredPackages)}", "Install the headless-Chromium apt libraries");

    /// <inheritdoc />
    public override string? FixPreview => objCanFix ? $"sudo {AptCommand.CommandLine}" : null;

    /// <inheritdoc />
    public override CheckFix? FixAsync => objCanFix ? FixCoreAsync : null;

    /// <inheritdoc />
    public override string Id => "wsl.chromium-libs";

    /// <inheritdoc />
    public override string Title => "Headless-Chromium apt libs";

    /// <inheritdoc />
    public override string Category => BoardCategories.FrameworkCore;

    /// <inheritdoc />
    public override MachineRole Roles => MachineRole.AgentHostWsl;

    /// <inheritdoc />
    public override CheckSeverity Severity => CheckSeverity.Required;

    /// <inheritdoc />
    public override CheckExplanation Explain => new(
        "The apt shared libraries headless Chromium needs (the WORKFLOW §0 list of 15).",
        "Playwright's Chromium cannot launch headless in WSL without them — runtime verification dies at startup.",
        "WORKFLOW §0");

    /// <inheritdoc />
    public override async Task<CheckResult> DetectAsync(CancellationToken aCancellationToken = default)
    {
        var vScript =
            $"for p in {string.Join(' ', RequiredPackages)}; " +
            "do dpkg -s $p >/dev/null 2>&1 || dpkg -s ${p}t64 >/dev/null 2>&1 || echo missing: $p; done";
        var vRun = await ProcessProbe.RunAsync(
            objProcessRunner,
            new ProcessRunRequest("bash", $"-c \"{vScript}\"", null, TimeSpan.FromSeconds(10)),
            aCancellationToken).ConfigureAwait(false);
        if (vRun.ExitCode != 0)
        {
            return CheckResult.Fail($"Could not query dpkg for the Chromium libraries.\n{vRun.ToEvidenceString()}");
        }

        var vMissing = vRun.StandardOutput
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(aLine => aLine.StartsWith("missing:", StringComparison.Ordinal))
            .Select(aLine => aLine["missing:".Length..].Trim())
            .ToList();
        if (vMissing.Count > 0)
        {
            return CheckResult.Fail(
                $"{vMissing.Count} of {RequiredPackages.Count} headless-Chromium apt libraries missing: " +
                $"{string.Join(", ", vMissing)} (dpkg -s each, t64 renames accepted).");
        }

        return CheckResult.Pass(
            $"All {RequiredPackages.Count} headless-Chromium apt libraries installed (dpkg -s each, t64 renames accepted).");
    }

    private static Task<FixResult> FixCoreAsync(ConsentToken aConsent, CancellationToken aCancellationToken)
        => Task.FromResult(FixExecution.SudoHandoff(AptCommand));
}
