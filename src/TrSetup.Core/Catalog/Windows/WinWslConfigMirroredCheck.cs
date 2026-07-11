using System.Text.RegularExpressions;
using TrSetup.Core.Checks;
using TrSetup.Core.ConfigWriting;
using TrSetup.Core.Fixing;
using TrSetup.Core.Processes;

namespace TrSetup.Core.Catalog.Windows;

/// <summary>
/// F-WINCHK: ".wslconfig has networkingMode=mirrored" — reads and parses
/// <c>%UserProfile%\.wslconfig</c> on the Windows side. The fixer patches the file with a
/// managed marker block (so user settings outside it are preserved) and tells the user to run
/// <c>wsl --shutdown</c> for it to take effect (REQ-FN-015 / REQ-FN-018).
/// </summary>
public sealed class WinWslConfigMirroredCheck : WindowsCheckBase
{
    /// <summary>The stable managed-block id written into <c>.wslconfig</c>.</summary>
    public const string BlockId = "win.wslconfig-mirrored";

    private const string Script =
        "$vPath = \"$env:UserProfile\\.wslconfig\"\n" +
        "if (Test-Path $vPath) { Write-Output \"FILE=$vPath\"; Get-Content -Raw $vPath } else { Write-Output 'WSLCONFIG-MISSING' }\n";

    private const string BlockBody = "[wsl2]\nnetworkingMode=mirrored";

    private readonly Func<string> objWslConfigPath;

    /// <summary>
    /// Creates the check.
    /// </summary>
    /// <param name="aProcessRunner">The process choke-point the detect runs through.</param>
    /// <param name="aFix">Fixer frameworks; when null the check is detect-only (no Fix button).</param>
    /// <param name="aWslConfigPath">Resolver for the <c>.wslconfig</c> path; defaults to <c>%UserProfile%\.wslconfig</c>.</param>
    public WinWslConfigMirroredCheck(
        IProcessRunner aProcessRunner,
        CheckFixServices? aFix = null,
        Func<string>? aWslConfigPath = null) : base(aProcessRunner, aFix)
    {
        objWslConfigPath = aWslConfigPath ?? DefaultWslConfigPath;
    }

    /// <inheritdoc />
    public override string? FixPreview => CanFix
        ? $"patch {objWslConfigPath()} (managed block '{BlockId}'):{Environment.NewLine}  {BlockBody.Replace("\n", Environment.NewLine + "  ")}" +
          $"{Environment.NewLine}then run: wsl --shutdown"
        : null;

    /// <inheritdoc />
    public override CheckFix? FixAsync => CanFix ? FixCoreAsync : null;

    private static string DefaultWslConfigPath()
        => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".wslconfig");

    /// <inheritdoc />
    public override string Id => "win.wslconfig-mirrored";

    /// <inheritdoc />
    public override string Title => ".wslconfig mirrored networking";

    /// <inheritdoc />
    public override CheckSeverity Severity => CheckSeverity.Required;

    /// <inheritdoc />
    public override CheckExplanation Explain => new(
        "The networkingMode=mirrored setting in %UserProfile%\\.wslconfig.",
        "Mirrored networking is what lets WSL agents reach Windows-host services (Appium, browsers) on localhost.",
        "WORKFLOW §0b");

    /// <inheritdoc />
    public override async Task<CheckResult> DetectAsync(CancellationToken aCancellationToken = default)
    {
        var vRun = await RunWindowsScriptAsync(Script, TimeSpan.FromSeconds(15), aCancellationToken)
            .ConfigureAwait(false);
        if (!vRun.Succeeded)
        {
            return CheckResult.Fail(ViaBridge($"Could not read .wslconfig.\n{vRun.ToEvidenceString()}"));
        }

        if (vRun.StandardOutput.Contains("WSLCONFIG-MISSING", StringComparison.Ordinal))
        {
            return CheckResult.Fail(ViaBridge(
                "%UserProfile%\\.wslconfig not found — mirrored networking is not configured."));
        }

        if (Regex.IsMatch(vRun.StandardOutput, @"networkingMode\s*=\s*mirrored", RegexOptions.IgnoreCase))
        {
            return CheckResult.Pass(ViaBridge(".wslconfig sets networkingMode=mirrored."));
        }

        return CheckResult.Fail(ViaBridge(
            ".wslconfig exists but does not set networkingMode=mirrored."));
    }

    private Task<FixResult> FixCoreAsync(ConsentToken aConsent, CancellationToken aCancellationToken)
    {
        var vWrite = FixServices!.ConfigWriter.UpsertBlock(objWslConfigPath(), BlockId, BlockBody, CommentSyntax.Hash);
        var vOutput = FixExecution.JoinOutput(
            vWrite.Evidence, "run 'wsl --shutdown' for mirrored networking to take effect");
        return Task.FromResult(new FixResult(true, vOutput));
    }
}
