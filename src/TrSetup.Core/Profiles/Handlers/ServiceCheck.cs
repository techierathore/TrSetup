using TrSetup.Core.Catalog.Probing;
using TrSetup.Core.Checks;
using TrSetup.Core.Elevation;
using TrSetup.Core.Fixing;
using TrSetup.Core.Processes;

namespace TrSetup.Core.Profiles.Handlers;

/// <summary>
/// REQ-FN-026 — the local-service check. Branches on the requirement's <c>service</c> param:
/// <list type="bullet">
/// <item><c>postgres</c>: detects PostgreSQL presence AND the <c>vector</c> (PgVector) extension;
/// the fixer installs Postgres (winget on Windows / brew on macOS) then runs an idempotent
/// <c>CREATE EXTENSION IF NOT EXISTS vector;</c>.</item>
/// <item><c>ffmpeg</c>: detects <c>ffmpeg -version</c>; the fixer installs ffmpeg (winget / brew).</item>
/// </list>
/// The install elevates through a visible UAC child on Windows (REQ-FN-020); brew on macOS runs
/// un-elevated (Homebrew refuses root). Both the install and the SQL are idempotent, so a re-run is
/// a no-op, and a failed install surfaces its raw output verbatim.
/// </summary>
public sealed class ServiceCheck : ProfileHeavyCheck
{
    /// <summary>The <c>service</c> param value for the PostgreSQL + PgVector service.</summary>
    public const string Postgres = "postgres";

    /// <summary>The <c>service</c> param value for the ffmpeg media toolchain.</summary>
    public const string Ffmpeg = "ffmpeg";

    private const string PostgresWingetId = "PostgreSQL.PostgreSQL";
    private const string PostgresBrewFormula = "postgresql@16";
    private const string FfmpegWingetId = "Gyan.FFmpeg";
    private const string FfmpegBrewFormula = "ffmpeg";

    private readonly IProcessRunner objProcessRunner;
    private readonly CheckFixServices objFix;
    private readonly Func<bool> objIsWindowsHost;
    private readonly string objService;

    /// <summary>
    /// Creates the check.
    /// </summary>
    /// <param name="aRequirement">The service requirement (reads <c>service</c>, <c>port</c>, <c>extension</c>).</param>
    /// <param name="aProfileName">The owning profile name — the app this row is scoped to.</param>
    /// <param name="aProcessRunner">The process choke-point detect/fix shells through.</param>
    /// <param name="aFix">The fixer bundle (elevation runner for the install, SQL run through the runner).</param>
    /// <param name="aIsWindowsHost">Host-is-Windows resolver (defaults to <see cref="OperatingSystem.IsWindows"/>); injectable for tests.</param>
    /// <exception cref="ArgumentNullException">Thrown when a required argument is null.</exception>
    public ServiceCheck(
        ProfileRequirement aRequirement,
        string aProfileName,
        IProcessRunner aProcessRunner,
        CheckFixServices aFix,
        Func<bool>? aIsWindowsHost = null)
        : base(aRequirement, aProfileName)
    {
        objProcessRunner = aProcessRunner ?? throw new ArgumentNullException(nameof(aProcessRunner));
        objFix = aFix ?? throw new ArgumentNullException(nameof(aFix));
        objIsWindowsHost = aIsWindowsHost ?? OperatingSystem.IsWindows;
        objService = (aRequirement.Param("service") ?? string.Empty).ToLowerInvariant();
    }

    private bool IsPostgres => string.Equals(objService, Postgres, StringComparison.Ordinal);

    private bool IsFfmpeg => string.Equals(objService, Ffmpeg, StringComparison.Ordinal);

    private string Port => Requirement.Param("port") ?? "5432";

    private string Extension => Requirement.Param("extension") ?? "vector";

    /// <inheritdoc />
    public override string Category => ProfileBoardCategories.Services;

    /// <inheritdoc />
    public override CheckExplanation Explain => IsPostgres
        ? new CheckExplanation(
            $"A running PostgreSQL server (port {Port}) with the '{Extension}' extension installed.",
            "The app persists embeddings in Postgres via PgVector; without the server or the extension those writes fail.",
            "WORKFLOW §0")
        : new CheckExplanation(
            "The ffmpeg media toolchain on PATH.",
            "The app shells out to ffmpeg for media encode/transcode; absence breaks that step.",
            "WORKFLOW §0");

    /// <inheritdoc />
    public override string? FixPreview => IsPostgres ? PostgresPreview() : IsFfmpeg ? FfmpegPreview() : null;

    /// <inheritdoc />
    public override CheckFix? FixAsync => IsPostgres ? FixPostgresAsync : IsFfmpeg ? FixFfmpegAsync : null;

    /// <inheritdoc />
    public override Task<CheckResult> DetectAsync(CancellationToken aCancellationToken = default)
    {
        if (IsPostgres)
        {
            return DetectPostgresAsync(aCancellationToken);
        }

        if (IsFfmpeg)
        {
            return DetectFfmpegAsync(aCancellationToken);
        }

        return Task.FromResult(CheckResult.Fail($"Unsupported service '{objService}' (expected '{Postgres}' or '{Ffmpeg}')."));
    }

    private async Task<CheckResult> DetectPostgresAsync(CancellationToken aCancellationToken)
    {
        var vPresence = await ProcessProbe.RunAsync(
            objProcessRunner,
            new ProcessRunRequest("psql", "--version", null, TimeSpan.FromSeconds(15)),
            aCancellationToken).ConfigureAwait(false);
        if (!vPresence.Succeeded)
        {
            return CheckResult.Fail($"PostgreSQL not found (psql --version failed).\n{vPresence.ToEvidenceString()}");
        }

        var vExtension = await ProcessProbe.RunAsync(
            objProcessRunner,
            new ProcessRunRequest("psql", ExtensionQueryArgs(), null, TimeSpan.FromSeconds(15)),
            aCancellationToken).ConfigureAwait(false);
        if (vExtension.Succeeded && vExtension.StandardOutput.Trim() == "1")
        {
            return CheckResult.Pass($"PostgreSQL present with '{Extension}' extension on port {Port}.\n{vPresence.StandardOutput.Trim()}");
        }

        return CheckResult.Fail($"PostgreSQL present but '{Extension}' extension is missing on port {Port}.\n{vExtension.ToEvidenceString()}");
    }

    private async Task<CheckResult> DetectFfmpegAsync(CancellationToken aCancellationToken)
    {
        var vRun = await ProcessProbe.RunAsync(
            objProcessRunner,
            new ProcessRunRequest("ffmpeg", "-version", null, TimeSpan.FromSeconds(15)),
            aCancellationToken).ConfigureAwait(false);
        if (!vRun.Succeeded)
        {
            return CheckResult.Fail($"ffmpeg not found (ffmpeg -version failed).\n{vRun.ToEvidenceString()}");
        }

        return CheckResult.Pass($"ffmpeg present ($ ffmpeg -version): {vRun.StandardOutput.Trim().Split('\n').FirstOrDefault()}");
    }

    private async Task<FixResult> FixPostgresAsync(ConsentToken aConsent, CancellationToken aCancellationToken)
    {
        var vInstall = await InstallAsync(PostgresWingetId, PostgresBrewFormula, "Install PostgreSQL server", aConsent, aCancellationToken)
            .ConfigureAwait(false);
        var vSql = await FixExecution.RunAsync(
            objProcessRunner,
            new ProcessRunRequest("psql", CreateExtensionArgs(), null, TimeSpan.FromSeconds(30)),
            aCancellationToken).ConfigureAwait(false);
        return new FixResult(vInstall.FixerReportedSuccess && vSql.FixerReportedSuccess, FixExecution.JoinOutput(vInstall.RawOutput, vSql.RawOutput));
    }

    private Task<FixResult> FixFfmpegAsync(ConsentToken aConsent, CancellationToken aCancellationToken)
        => InstallAsync(FfmpegWingetId, FfmpegBrewFormula, "Install ffmpeg", aConsent, aCancellationToken);

    private async Task<FixResult> InstallAsync(
        string aWingetId,
        string aBrewFormula,
        string aDescription,
        ConsentToken aConsent,
        CancellationToken aCancellationToken)
    {
        if (objIsWindowsHost())
        {
            var vCommand = new ElevatedCommand("winget", WingetArgs(aWingetId), aDescription);
            var vRun = await objFix.ElevationRunner
                .RunWindowsElevatedAsync(vCommand, aConsent, null, aCancellationToken).ConfigureAwait(false);
            return new FixResult(vRun.Succeeded, vRun.ToEvidenceString());
        }

        return await FixExecution.RunAsync(
            objProcessRunner,
            new ProcessRunRequest("brew", $"install {aBrewFormula}", null, TimeSpan.FromMinutes(10)),
            aCancellationToken).ConfigureAwait(false);
    }

    private static string WingetArgs(string aPackageId)
        => $"install --id {aPackageId} --silent --accept-package-agreements --accept-source-agreements";

    private string ExtensionQueryArgs()
        => $"-p {Port} -tAc \"SELECT 1 FROM pg_extension WHERE extname = '{Extension}'\"";

    private string CreateExtensionArgs()
        => $"-p {Port} -c \"CREATE EXTENSION IF NOT EXISTS {Extension};\"";

    private string PostgresPreview()
        => $"On Windows: winget {WingetArgs(PostgresWingetId)}{Environment.NewLine}" +
           $"On macOS:   brew install {PostgresBrewFormula}{Environment.NewLine}" +
           $"then:       psql {CreateExtensionArgs()}";

    private string FfmpegPreview()
        => $"On Windows: winget {WingetArgs(FfmpegWingetId)}{Environment.NewLine}" +
           $"On macOS:   brew install {FfmpegBrewFormula}";
}
