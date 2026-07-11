using TrSetup.Core.Catalog.Probing;
using TrSetup.Core.Checks;
using TrSetup.Core.ConfigWriting;
using TrSetup.Core.Fixing;

namespace TrSetup.Core.Catalog.Wsl;

/// <summary>
/// F-WSLCHK: "~/bin/winrun bridge + executable + on PATH" — file probe for the winrun
/// interop script, its execute bit, and whether <c>~/bin</c> is on PATH (falling back to a
/// <c>.bashrc</c> grep for shells not yet reloaded). The fixer writes the script, marks it
/// executable, and adds the PATH line to <c>~/.bashrc</c> inside a managed marker block so
/// re-runs never duplicate and user edits are preserved (REQ-FN-014 / REQ-FN-018).
/// </summary>
public sealed class WslWinrunBridgeCheck : Check
{
    /// <summary>The stable managed-block id of the PATH line written into <c>~/.bashrc</c>.</summary>
    public const string PathBlockId = "wsl.winrun-path";

    /// <summary>The winrun interop script body written to <c>~/bin/winrun</c>.</summary>
    public const string WinrunScript =
        "#!/usr/bin/env bash\n" +
        "# winrun — run a Windows command from WSL over the interop bridge (TrSetup managed).\n" +
        "exec /mnt/c/Windows/System32/WindowsPowerShell/v1.0/powershell.exe -NoProfile -Command \"$@\"\n";

    private readonly ISystemProbe objSystemProbe;
    private readonly CheckFixServices? objFix;

    /// <summary>
    /// Creates the check.
    /// </summary>
    /// <param name="aSystemProbe">Local filesystem/environment probe.</param>
    /// <param name="aFix">Fixer frameworks; when null the check is detect-only (no Fix button).</param>
    public WslWinrunBridgeCheck(ISystemProbe aSystemProbe, CheckFixServices? aFix = null)
    {
        objSystemProbe = aSystemProbe;
        objFix = aFix;
    }

    private string WinrunPath => Path.Combine(objSystemProbe.HomeDirectory, "bin", "winrun");

    private string BashrcPath => Path.Combine(objSystemProbe.HomeDirectory, ".bashrc");

    /// <inheritdoc />
    public override string? FixPreview => objFix is null
        ? null
        : $"write {WinrunPath} (chmod +x){Environment.NewLine}" +
          $"add PATH line to {BashrcPath} (managed block '{PathBlockId}'): export PATH=\"$HOME/bin:$PATH\"";

    /// <inheritdoc />
    public override CheckFix? FixAsync => objFix is null ? null : FixCoreAsync;

    /// <inheritdoc />
    public override string Id => "wsl.winrun";

    /// <inheritdoc />
    public override string Title => "~/bin/winrun bridge on PATH";

    /// <inheritdoc />
    public override string Category => BoardCategories.FrameworkCore;

    /// <inheritdoc />
    public override MachineRole Roles => MachineRole.AgentHostWsl;

    /// <inheritdoc />
    public override CheckSeverity Severity => CheckSeverity.Required;

    /// <inheritdoc />
    public override CheckExplanation Explain => new(
        "The ~/bin/winrun helper script that runs Windows commands from WSL over the interop bridge.",
        "Rung #4 of the build ladder (Windows/MAUI builds from WSL) is driven through winrun; without it agents cannot reach the Windows toolchain.",
        "WORKFLOW §0");

    /// <inheritdoc />
    public override Task<CheckResult> DetectAsync(CancellationToken aCancellationToken = default) =>
        Task.FromResult(Detect());

    private CheckResult Detect()
    {
        var vBinDir = Path.Combine(objSystemProbe.HomeDirectory, "bin");
        var vWinrunPath = Path.Combine(vBinDir, "winrun");
        if (!objSystemProbe.FileExists(vWinrunPath))
        {
            return CheckResult.Fail($"{vWinrunPath} not found — the winrun bridge script is missing.");
        }

        if (!objSystemProbe.IsExecutable(vWinrunPath))
        {
            return CheckResult.Fail($"{vWinrunPath} exists but is not executable (needs chmod +x).");
        }

        var vPath = objSystemProbe.GetEnvironmentVariable("PATH") ?? string.Empty;
        if (vPath.Split(Path.PathSeparator).Contains(vBinDir))
        {
            return CheckResult.Pass($"{vWinrunPath} present, executable, and {vBinDir} is on PATH.");
        }

        var vBashrc = objSystemProbe.TryReadAllText(
            Path.Combine(objSystemProbe.HomeDirectory, ".bashrc")) ?? string.Empty;
        if (vBashrc.Contains("$HOME/bin") || vBashrc.Contains("~/bin") || vBashrc.Contains(vBinDir))
        {
            return CheckResult.Pass(
                $"{vWinrunPath} present and executable; PATH line found in ~/.bashrc (takes effect in new shells).");
        }

        return CheckResult.Warn(
            $"{vWinrunPath} present and executable, but {vBinDir} is not on PATH and no PATH line was found in ~/.bashrc.");
    }

    private Task<FixResult> FixCoreAsync(ConsentToken aConsent, CancellationToken aCancellationToken)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(WinrunPath)!);
        File.WriteAllText(WinrunPath, WinrunScript);
        if (!OperatingSystem.IsWindows())
        {
            File.SetUnixFileMode(
                WinrunPath,
                UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute |
                UnixFileMode.GroupRead | UnixFileMode.GroupExecute |
                UnixFileMode.OtherRead | UnixFileMode.OtherExecute);
        }

        var vWrite = objFix!.ConfigWriter.UpsertBlock(
            BashrcPath, PathBlockId, "export PATH=\"$HOME/bin:$PATH\"", CommentSyntax.Hash);
        var vOutput = FixExecution.JoinOutput(
            $"wrote {WinrunPath} (executable)", vWrite.Evidence, "restart your shell (or: source ~/.bashrc) to pick up PATH");
        return Task.FromResult(new FixResult(true, vOutput));
    }
}
