using TrSetup.Core.Checks;
using TrSetup.Core.ConfigWriting;
using TrSetup.Core.Downloads;
using TrSetup.Core.Fixing;
using TrSetup.Core.Processes;

namespace TrSetup.Core.Catalog.Mac;

/// <summary>
/// F-MACCHK: ".NET SDK + MAUI workload" — <c>dotnet workload list</c> (its failure also
/// exposes a missing SDK). The fixer installs the SDK into the user-local <c>~/.dotnet</c> via
/// the official <c>dotnet-install.sh</c> (no sudo), then installs the maui workload into it
/// (REQ-FN-016 / REQ-NFR-004).
/// </summary>
public sealed class MacDotnetMauiCheck : MacCheckBase
{
    /// <summary>The pinned official dotnet-install script URL (no per-request checksum published).</summary>
    public const string InstallScriptUrl = "https://dot.net/v1/dotnet-install.sh";

    /// <summary>The channel the fixer installs.</summary>
    public const string Channel = "10.0";

    /// <summary>
    /// Creates the check.
    /// </summary>
    /// <param name="aProcessRunner">The process choke-point the detect shells through.</param>
    /// <param name="aFix">Fixer frameworks; when null the check is detect-only (no Fix button).</param>
    /// <param name="aZprofilePath">Resolver for the shell-profile PATH file; defaults to <c>~/.zprofile</c>.</param>
    public MacDotnetMauiCheck(
        IProcessRunner aProcessRunner,
        CheckFixServices? aFix = null,
        Func<string>? aZprofilePath = null)
        : base(aProcessRunner, aFix)
    {
        objZprofilePath = aZprofilePath ?? DefaultZprofilePath;
    }

    private readonly Func<string> objZprofilePath;

    /// <summary>The stable managed-block id of the PATH lines written into <c>~/.zprofile</c>.</summary>
    public const string PathBlockId = "mac.dotnet-path";

    private static string DefaultZprofilePath() => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".zprofile");

    /// <summary>
    /// The shell lines the fixer writes so the managed SDK is usable from the user's own terminal.
    /// <c>dotnet-install.sh</c> installs a private SDK that is on NO PATH by default and documents
    /// that BOTH of these are needed — DOTNET_ROOT alone is not enough to run <c>dotnet</c>, and
    /// PATH alone leaves DOTNET_ROOT unset for tools that read it.
    /// </summary>
    private static string PathBlockBody =>
        $"export DOTNET_ROOT=\"{DotnetDir}\"" + Environment.NewLine +
        $"export PATH=\"{DotnetDir}:$PATH\"";

    /// <summary>
    /// Test/override hook mirroring <see cref="Downloads.TrSetupPaths.RootOverride"/> and
    /// <see cref="Profiles.ProfilePaths.RepoRootOverride"/>: when set, <see cref="DotnetDir"/>
    /// returns this path instead of <c>~/.dotnet</c>, so tests can stage a fake managed SDK without
    /// touching the real user profile. Set to <c>null</c> to restore the default.
    /// </summary>
    internal static string? DotnetDirOverride { get; set; }

    private static string DotnetDir => DotnetDirOverride ?? Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".dotnet");

    /// <inheritdoc />
    public override string? FixPreview => CanFix
        ? InstallerDownloader.BuildFixPreview(new DownloadRequest(InstallScriptUrl, "dotnet-install", "dotnet-install.sh")) +
          $"{Environment.NewLine}then run  bash dotnet-install.sh --channel {Channel} --install-dir {DotnetDir}" +
          $"{Environment.NewLine}then  {DotnetDir}/dotnet workload install maui" +
          $"{Environment.NewLine}then add DOTNET_ROOT + {DotnetDir} to PATH in {objZprofilePath()} (managed block '{PathBlockId}')"
        : null;

    /// <inheritdoc />
    public override CheckFix? FixAsync => CanFix ? FixCoreAsync : null;

    /// <inheritdoc />
    public override string Id => "mac.dotnet-maui";

    /// <inheritdoc />
    public override string Title => ".NET SDK + MAUI workload";

    /// <inheritdoc />
    public override CheckSeverity Severity => CheckSeverity.Required;

    /// <inheritdoc />
    public override CheckExplanation Explain => new(
        "The .NET SDK on the Mac with the maui workload installed.",
        "The Mac builds and runs the iOS / Mac Catalyst heads; both need the SDK plus the maui workload.",
        "WORKFLOW §0b");

    /// <summary>The <c>dotnet</c> muxer inside the managed install this check's fixer produces.</summary>
    private static string ManagedDotnetBinary => Path.Combine(DotnetDir, "dotnet");

    /// <inheritdoc />
    public override async Task<CheckResult> DetectAsync(CancellationToken aCancellationToken = default)
    {
        var vRun = await RunMacCommandAsync("dotnet", "workload list", TimeSpan.FromSeconds(60), aCancellationToken)
            .ConfigureAwait(false);

        // Not resolvable as a bare command — fall back to the managed install this check's own fixer
        // creates (FixCoreAsync installs into DotnetDir and already invokes `{DotnetDir}/dotnet`
        // absolutely). Without this fallback a SUCCESSFUL fix still re-verified red and the row
        // looked like "clicking Fix does nothing" (same defect class as mac.node).
        //
        // Why this was invisible in testing: `dotnet TrSetup.Web.dll` is MUXER-hosted, and a
        // muxer-hosted process can spawn `dotnet` even when it is absent from PATH. The SHIPPING
        // MAUI app is a self-contained apphost with no muxer — there the bare spawn fails with
        // "No such file or directory". Verified both ways on macOS 26.5, 2026-07-20.
        var vUsedManagedFallback = false;
        if (!vRun.Succeeded && File.Exists(ManagedDotnetBinary))
        {
            vRun = await RunMacCommandAsync(
                ManagedDotnetBinary, "workload list", TimeSpan.FromSeconds(60), aCancellationToken).ConfigureAwait(false);
            vUsedManagedFallback = vRun.Succeeded;
        }

        if (!vRun.Succeeded)
        {
            return CheckResult.Fail($".NET SDK not usable (dotnet workload list failed).\n{vRun.ToEvidenceString()}");
        }

        // Resolved via the managed install rather than PATH: say where it lives and that the
        // fixer's '{PathBlockId}' block in ~/.zprofile only reaches a NEW login shell, so `dotnet`
        // will not resolve in an already-open terminal.
        var vWhere = vUsedManagedFallback
            ? $" at {ManagedDotnetBinary} (TrSetup-managed install). Not on this process's PATH — " +
              $"the '{PathBlockId}' block in {objZprofilePath()} applies to new login shells, so open a " +
              "new terminal (or log out and back in) to use `dotnet` directly"
            : string.Empty;

        if (vRun.StandardOutput.Contains("maui", StringComparison.OrdinalIgnoreCase))
        {
            return CheckResult.Pass($".NET SDK present and MAUI workload installed{vWhere} ($ dotnet workload list).");
        }

        return CheckResult.Fail(
            $".NET SDK present{vWhere} but the MAUI workload is not installed ($ dotnet workload list has no 'maui' entry).");
    }

    private async Task<FixResult> FixCoreAsync(ConsentToken aConsent, CancellationToken aCancellationToken)
    {
        var vDownload = await FixServices!.Downloader.DownloadAsync(
            new DownloadRequest(InstallScriptUrl, "dotnet-install", "dotnet-install.sh"),
            null,
            aCancellationToken).ConfigureAwait(false);
        if (!vDownload.Succeeded)
        {
            return new FixResult(false, vDownload.Evidence);
        }

        var vInstall = await RunMacCommandAsync(
            "bash", $"{vDownload.FilePath} --channel {Channel} --install-dir {DotnetDir}",
            TimeSpan.FromMinutes(10), aCancellationToken).ConfigureAwait(false);
        var vWorkload = await RunMacCommandAsync(
            $"{DotnetDir}/dotnet", "workload install maui", TimeSpan.FromMinutes(10), aCancellationToken).ConfigureAwait(false);

        // Put the managed SDK on the USER's PATH, mirroring mac.node's 'mac.node-path' block.
        // dotnet-install.sh deliberately installs a private SDK that is on no PATH at all, so
        // without this TrSetup could install a .NET the owner could never invoke from their own
        // terminal — a tool whose promise is "installs everything" leaving the install unusable.
        // UpsertBlock is idempotent: re-running the fixer replaces the block rather than appending,
        // and everything outside the markers is preserved byte-for-byte.
        var vWrite = FixServices.ConfigWriter.UpsertBlock(
            objZprofilePath(), PathBlockId, PathBlockBody, CommentSyntax.Hash);

        return new FixResult(
            vInstall.Succeeded && vWorkload.Succeeded,
            FixExecution.JoinOutput(
                vDownload.Evidence, vInstall.ToEvidenceString(), vWorkload.ToEvidenceString(), vWrite.Evidence));
    }
}
