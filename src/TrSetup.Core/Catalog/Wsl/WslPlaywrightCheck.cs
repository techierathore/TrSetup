using TrSetup.Core.Catalog.Probing;
using TrSetup.Core.Checks;
using TrSetup.Core.Fixing;
using TrSetup.Core.Processes;

namespace TrSetup.Core.Catalog.Wsl;

/// <summary>
/// F-WSLCHK: "Playwright CLI + headless Chromium" — detects the CLI via
/// <c>npx playwright --version</c> and the downloaded Chromium via the browsers cache
/// directory (honouring <c>PLAYWRIGHT_BROWSERS_PATH</c>).
/// </summary>
public sealed class WslPlaywrightCheck : Check
{
    private const string InstallCliArguments = "-c \"npm i -D playwright && npx playwright install chromium\"";

    private readonly IProcessRunner objProcessRunner;
    private readonly ISystemProbe objSystemProbe;
    private readonly bool objCanFix;

    /// <summary>
    /// Creates the check.
    /// </summary>
    /// <param name="aProcessRunner">The process choke-point the CLI probe runs through.</param>
    /// <param name="aSystemProbe">Local probe used to find the Chromium browsers directory.</param>
    /// <param name="aFix">Fixer frameworks; when null the check is detect-only (no Fix button).</param>
    public WslPlaywrightCheck(IProcessRunner aProcessRunner, ISystemProbe aSystemProbe, CheckFixServices? aFix = null)
    {
        objProcessRunner = aProcessRunner;
        objSystemProbe = aSystemProbe;
        objCanFix = aFix is not null;
    }

    /// <inheritdoc />
    public override string? FixPreview =>
        objCanFix ? "npm i -D playwright && npx playwright install chromium" : null;

    /// <inheritdoc />
    public override CheckFix? FixAsync => objCanFix ? FixCoreAsync : null;

    /// <inheritdoc />
    public override string Id => "wsl.playwright";

    /// <inheritdoc />
    public override string Title => "Playwright CLI + headless Chromium";

    /// <inheritdoc />
    public override string Category => BoardCategories.FrameworkCore;

    /// <inheritdoc />
    public override MachineRole Roles => MachineRole.AgentHostWsl;

    /// <inheritdoc />
    public override CheckSeverity Severity => CheckSeverity.Required;

    /// <inheritdoc />
    public override CheckExplanation Explain => new(
        "The Playwright CLI plus its downloaded headless Chromium browser.",
        "Blazor heads are runtime-verified with headless Playwright from WSL; no CLI or browser means no UI verification.",
        "WORKFLOW §0");

    /// <inheritdoc />
    public override async Task<CheckResult> DetectAsync(CancellationToken aCancellationToken = default)
    {
        var vRun = await ProcessProbe.RunAsync(
            objProcessRunner,
            new ProcessRunRequest("npx", "playwright --version", null, TimeSpan.FromSeconds(30)),
            aCancellationToken).ConfigureAwait(false);
        if (!vRun.Succeeded || string.IsNullOrWhiteSpace(vRun.StandardOutput))
        {
            return CheckResult.Fail($"Playwright CLI not found (npx playwright --version failed).\n{vRun.ToEvidenceString()}");
        }

        var vVersion = vRun.StandardOutput.Trim();
        var vBrowsersDir = objSystemProbe.GetEnvironmentVariable("PLAYWRIGHT_BROWSERS_PATH")
            ?? Path.Combine(objSystemProbe.HomeDirectory, ".cache", "ms-playwright");
        var vChromiumDirs = objSystemProbe.EnumerateDirectories(vBrowsersDir, "chromium*");
        if (vChromiumDirs.Count == 0)
        {
            return CheckResult.Fail(
                $"{vVersion} present but no Chromium browser under {vBrowsersDir} — run: npx playwright install chromium.");
        }

        return CheckResult.Pass(
            $"{vVersion} present; Chromium installed at {vChromiumDirs[0]}.");
    }

    private Task<FixResult> FixCoreAsync(ConsentToken aConsent, CancellationToken aCancellationToken)
        => FixExecution.RunAsync(
            objProcessRunner,
            new ProcessRunRequest("bash", InstallCliArguments, null, TimeSpan.FromMinutes(10)),
            aCancellationToken);
}
