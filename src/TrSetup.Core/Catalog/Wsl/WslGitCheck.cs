using TrSetup.Core.Catalog.Probing;
using TrSetup.Core.Checks;
using TrSetup.Core.Elevation;
using TrSetup.Core.Fixing;
using TrSetup.Core.Processes;

namespace TrSetup.Core.Catalog.Wsl;

/// <summary>
/// F-WSLCHK: "git present (for the owner's manual use)" — detects via <c>git --version</c>.
/// The fixer hands off <c>sudo apt-get install -y git</c> to the user's own terminal
/// (REQ-FN-020 — TrSetup never asks for or stores a sudo password).
/// </summary>
public sealed class WslGitCheck : Check
{
    private readonly IProcessRunner objProcessRunner;
    private readonly bool objCanFix;

    /// <summary>
    /// Creates the check.
    /// </summary>
    /// <param name="aProcessRunner">The process choke-point the detect shells through.</param>
    /// <param name="aFix">Fixer frameworks; when null the check is detect-only (no Fix button).</param>
    public WslGitCheck(IProcessRunner aProcessRunner, CheckFixServices? aFix = null)
    {
        objProcessRunner = aProcessRunner;
        objCanFix = aFix is not null;
    }

    private static ElevatedCommand AptCommand => new("apt-get", "install -y git", "Install git");

    /// <inheritdoc />
    public override string? FixPreview => objCanFix ? $"sudo {AptCommand.CommandLine}" : null;

    /// <inheritdoc />
    public override CheckFix? FixAsync => objCanFix ? FixCoreAsync : null;

    /// <inheritdoc />
    public override string Id => "wsl.git";

    /// <inheritdoc />
    public override string Title => "git present";

    /// <inheritdoc />
    public override string Category => BoardCategories.FrameworkCore;

    /// <inheritdoc />
    public override MachineRole Roles => MachineRole.AgentHostWsl;

    /// <inheritdoc />
    public override CheckSeverity Severity => CheckSeverity.Recommended;

    /// <inheritdoc />
    public override CheckExplanation Explain => new(
        "The git client inside the WSL distro.",
        "The owner commits manually from WSL (agents never run git); without the client no history can be recorded.",
        "WORKFLOW §0");

    /// <inheritdoc />
    public override async Task<CheckResult> DetectAsync(CancellationToken aCancellationToken = default)
    {
        var vRun = await ProcessProbe.RunAsync(
            objProcessRunner,
            new ProcessRunRequest("git", "--version", null, TimeSpan.FromSeconds(10)),
            aCancellationToken).ConfigureAwait(false);
        if (!vRun.Succeeded || string.IsNullOrWhiteSpace(vRun.StandardOutput))
        {
            return CheckResult.Fail($"git not found.\n{vRun.ToEvidenceString()}");
        }

        return CheckResult.Pass($"{vRun.StandardOutput.Trim()} present ($ git --version).");
    }

    private static Task<FixResult> FixCoreAsync(ConsentToken aConsent, CancellationToken aCancellationToken)
        => Task.FromResult(FixExecution.SudoHandoff(AptCommand));
}
