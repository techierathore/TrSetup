using System.Text.Json;
using TrSetup.Core.Checks;
using TrSetup.Core.Downloads;
using TrSetup.Core.Fixing;
using TrSetup.Core.Processes;

namespace TrSetup.Core.Catalog.Mac;

/// <summary>
/// F-MACCHK: "Appium + xcuitest + mac2 drivers" — <c>appium --version</c> plus
/// <c>appium driver list --installed --json</c>, requiring BOTH Apple drivers AND requiring them to
/// be compatible with the installed server. The fixer installs the pinned Appium globally and
/// converges each driver onto its pinned version (guarded, so a re-run is a no-op) (REQ-FN-016).
/// </summary>
public sealed class MacAppiumDriversCheck : MacCheckBase
{
    // ---------------------------------------------------------------------------------------
    // PINNED VERSIONS (REQ-FN-016). These are pinned DELIBERATELY and must move together.
    //
    // `npm install -g appium` (unpinned) does NOT yield the current Appium on a TrSetup-provisioned
    // Mac. appium@3.x declares engines `^20.19.0 || ^22.12.0 || >=24.0.0`; mac.node used to pin Node
    // v22.11.0, which satisfies NONE of those ranges, so npm silently resolved the newest
    // engine-compatible Appium — 2.0.1 (over two years old). Meanwhile the CURRENT Apple drivers
    // (appium-xcuitest-driver 11.x, appium-mac2-driver 4.x) both declare a peer dependency on
    // appium `^3.0.0-rc.2`. The unpinned fixer therefore produced a permanently mismatched pair: a
    // v2 server that cannot load v4 drivers. Catalyst automation (REQ-FN-030) cannot work in that
    // state, so the check must NOT report Pass for it.
    //
    // Fix: mac.node's pin was raised past the appium@3 engine floor, and the server + both drivers
    // are pinned to a mutually compatible set here. Bump all three together, never one alone.
    // ---------------------------------------------------------------------------------------

    /// <summary>The pinned Appium server version the fixer installs.</summary>
    public const string AppiumVersion = "3.5.2";

    /// <summary>The pinned xcuitest (iOS/tvOS) driver version; peer-compatible with <see cref="AppiumVersion"/>.</summary>
    public const string XcuitestDriverVersion = "11.17.7";

    /// <summary>The pinned mac2 (Mac Catalyst) driver version; peer-compatible with <see cref="AppiumVersion"/>.</summary>
    public const string Mac2DriverVersion = "4.0.4";

    /// <summary>The drivers this check requires, each with the version the fixer converges onto.</summary>
    private static readonly (string Name, string Version)[] RequiredDrivers =
    {
        ("xcuitest", XcuitestDriverVersion),
        ("mac2", Mac2DriverVersion),
    };

    /// <summary>
    /// The single, stable <c>APPIUM_HOME</c> TrSetup owns: <c>{ToolsRoot}/appium</c>.
    ///
    /// REQ-FN-016 defect (same class as the REQ-FN-028 "working dir resolves to process cwd" bug in
    /// <see cref="MacCatalystBuildCheck"/>): with <c>APPIUM_HOME</c> unset, Appium resolves its
    /// extension manifest RELATIVE TO THE PROCESS WORKING DIRECTORY. On this machine that produced
    /// two divergent manifests — <c>&lt;repo&gt;/node_modules/.cache/appium/extensions.yaml</c> when
    /// launched from a directory containing <c>node_modules</c>, and
    /// <c>~/.appium/node_modules/.cache/appium/extensions.yaml</c> otherwise — holding DIFFERENT
    /// driver sets at DIFFERENT versions. The fixer could then install drivers into one manifest
    /// while the detect read another, so a fix that genuinely succeeded still re-verified red (and
    /// the board's answer depended on where the app happened to be launched from). Both the detect
    /// AND the fixer now pin this value, so they always read and write the same manifest.
    /// </summary>
    public static string ManagedAppiumHome => Path.Combine(TrSetupPaths.ToolsRoot, "appium");

    /// <summary>The <c>appium</c> CLI inside the TrSetup-managed Node install, if one exists.</summary>
    private static string ManagedAppiumBinary => Path.Combine(MacNodeCheck.ManagedNodeBinDir, "appium");

    /// <summary>One installed driver as reported by <c>appium driver list --installed --json</c>.</summary>
    /// <param name="Name">The driver's Appium name (e.g. <c>mac2</c>).</param>
    /// <param name="Version">The installed package version.</param>
    /// <param name="AppiumRange">The server range the driver declares it needs (its <c>appiumVersion</c>).</param>
    private readonly record struct InstalledDriver(string Name, string Version, string AppiumRange);

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
        ? $"APPIUM_HOME={ManagedAppiumHome} && npm install -g appium@{AppiumVersion} && " +
          $"appium driver install xcuitest@{XcuitestDriverVersion} && appium driver install mac2@{Mac2DriverVersion}"
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
        // Bare `appium` first; then the TrSetup-managed Node bin dir, which `npm install -g` targets
        // when Node itself came from mac.node's fixer. Without the fallback a successful fix still
        // re-verified red on a TrSetup-provisioned machine (same defect class as mac.node) — and it
        // stays invisible under the muxer-hosted web head, which can resolve tools the shipping
        // self-contained MAUI app cannot.
        var vAppium = "appium";
        var vVersionRun = await RunAppiumAsync(vAppium, "--version", aCancellationToken).ConfigureAwait(false);
        if (!vVersionRun.Succeeded && File.Exists(ManagedAppiumBinary))
        {
            vAppium = ManagedAppiumBinary;
            vVersionRun = await RunAppiumAsync(vAppium, "--version", aCancellationToken).ConfigureAwait(false);
        }

        if (!vVersionRun.Succeeded)
        {
            // Distinct failure mode #1: no server at all. Kept verbally distinct from the
            // "server present, drivers wrong" cases below, which the board used to misreport as
            // "Appium not found" (REQ-FN-016 defect 4).
            return CheckResult.Fail(
                $"Appium SERVER NOT FOUND on the Mac — neither `appium` on PATH nor {ManagedAppiumBinary}. " +
                $"Run the fixer to install appium@{AppiumVersion}.\n{vVersionRun.ToEvidenceString()}");
        }

        var vServerVersion = LastMeaningfulLine(vVersionRun.StandardOutput);
        return await DetectDriversAsync(vAppium, vServerVersion, aCancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Second half of detect: reads the pinned <c>APPIUM_HOME</c> manifest and judges the drivers
    /// against the server that is actually installed.
    /// </summary>
    /// <param name="aAppium">The resolved appium binary (bare name or managed path).</param>
    /// <param name="aServerVersion">The server version <c>appium --version</c> reported.</param>
    /// <param name="aCancellationToken">Cancels the run.</param>
    /// <returns>The board row's result.</returns>
    private async Task<CheckResult> DetectDriversAsync(
        string aAppium, string aServerVersion, CancellationToken aCancellationToken)
    {
        // `--json` (not the human table) because the old `grep`/Contains guard matched ANY mention
        // of a driver name in stdout+stderr — including Appium's own "Driver "mac2" may be
        // incompatible..." WARNING lines. That made the check report Pass precisely when the driver
        // was unusable. --json writes only the real manifest to stdout; warnings stay on stderr.
        var vDriversRun = await RunAppiumAsync(
            aAppium, "driver list --installed --json 2>/dev/null", aCancellationToken).ConfigureAwait(false);
        if (!TryParseDrivers(vDriversRun.StandardOutput, out var vInstalled))
        {
            return CheckResult.Fail(
                $"Appium {aServerVersion} present, but its driver manifest under APPIUM_HOME={ManagedAppiumHome} " +
                $"could not be read ($ appium driver list --installed --json).\n{vDriversRun.ToEvidenceString()}");
        }

        var vMissing = new List<string>();
        var vIncompatible = new List<string>();
        foreach (var vRequired in RequiredDrivers)
        {
            if (!vInstalled.TryGetValue(vRequired.Name, out var vDriver))
            {
                vMissing.Add(vRequired.Name);
                continue;
            }

            if (!IsServerCompatible(aServerVersion, vDriver.AppiumRange))
            {
                vIncompatible.Add($"{vDriver.Name}@{vDriver.Version} needs Appium {vDriver.AppiumRange}");
            }
        }

        // Distinct failure mode #2: server present, drivers missing.
        if (vMissing.Count > 0)
        {
            return CheckResult.Fail(
                $"Appium {aServerVersion} IS present, but driver(s) MISSING from APPIUM_HOME={ManagedAppiumHome}: " +
                $"{string.Join(", ", vMissing)}. Installed there: {DescribeInstalled(vInstalled)}.");
        }

        // Distinct failure mode #3: server + drivers present but mutually incompatible. Reporting
        // Pass here would be dishonest — Appium refuses to load a driver whose peer range excludes
        // the running server, so Catalyst/iOS automation would not actually work.
        if (vIncompatible.Count > 0)
        {
            return CheckResult.Fail(
                $"Appium {aServerVersion} and its drivers are INCOMPATIBLE in APPIUM_HOME={ManagedAppiumHome}: " +
                $"{string.Join("; ", vIncompatible)}. Run the fixer to converge on the pinned set " +
                $"(appium@{AppiumVersion} + xcuitest@{XcuitestDriverVersion} + mac2@{Mac2DriverVersion}).");
        }

        return CheckResult.Pass(
            $"Appium {aServerVersion} with {DescribeInstalled(vInstalled)} in APPIUM_HOME={ManagedAppiumHome} " +
            "(server and drivers mutually compatible).");
    }

    private async Task<FixResult> FixCoreAsync(ConsentToken aConsent, CancellationToken aCancellationToken)
    {
        Directory.CreateDirectory(ManagedAppiumHome);

        var vInstall = await RunShellAsync(
            $"npm install -g appium@{AppiumVersion}", TimeSpan.FromMinutes(10), aCancellationToken).ConfigureAwait(false);
        if (!vInstall.Succeeded)
        {
            return new FixResult(false, vInstall.ToEvidenceString());
        }

        var vEvidence = new List<string> { vInstall.ToEvidenceString() };
        foreach (var vRequired in RequiredDrivers)
        {
            var vSteps = await EnsureDriverAsync(vRequired.Name, vRequired.Version, aCancellationToken).ConfigureAwait(false);
            vEvidence.AddRange(vSteps);
        }

        // Re-read the manifest and let the ACTUAL end state decide success, rather than trusting the
        // exit code of the last command in a shell `&&` chain.
        var vVerify = await RunAppiumAsync(
            "appium", "driver list --installed --json 2>/dev/null", aCancellationToken).ConfigureAwait(false);
        vEvidence.Add(vVerify.ToEvidenceString());
        var vConverged = TryParseDrivers(vVerify.StandardOutput, out var vFinal) &&
                         RequiredDrivers.All(aRequired =>
                             vFinal.TryGetValue(aRequired.Name, out var vDriver) && vDriver.Version == aRequired.Version);
        return new FixResult(vConverged, FixExecution.JoinOutput(vEvidence.ToArray()));
    }

    /// <summary>
    /// Converges one driver onto its pinned version inside the pinned <c>APPIUM_HOME</c>.
    ///
    /// The old guard was <c>appium driver list --installed | grep -q &lt;name&gt; || appium driver
    /// install &lt;name&gt;</c>. That is fragile twice over: it matches the driver NAME anywhere in
    /// the output (including Appium's incompatibility warnings), and — more importantly — a
    /// name-only match treats an OLD, incompatible driver as "already installed", so the fixer could
    /// never repair a version-mismatched manifest and the row stayed red forever. Guarding on the
    /// exact pinned VERSION instead makes the fixer converge, while still being a true no-op on a
    /// machine that is already correct.
    /// </summary>
    /// <param name="aName">The driver name (e.g. <c>mac2</c>).</param>
    /// <param name="aVersion">The pinned version to land on.</param>
    /// <param name="aCancellationToken">Cancels the run.</param>
    /// <returns>The evidence lines for the steps this driver actually needed.</returns>
    private async Task<IReadOnlyList<string>> EnsureDriverAsync(
        string aName, string aVersion, CancellationToken aCancellationToken)
    {
        var vList = await RunAppiumAsync(
            "appium", "driver list --installed --json 2>/dev/null", aCancellationToken).ConfigureAwait(false);
        var vParsed = TryParseDrivers(vList.StandardOutput, out var vInstalled);
        if (vParsed && vInstalled.TryGetValue(aName, out var vExisting) && vExisting.Version == aVersion)
        {
            return new[] { $"$ appium driver install {aName}@{aVersion} — skipped, already at {aVersion}." };
        }

        var vSteps = new List<string>();
        if (vParsed && vInstalled.ContainsKey(aName))
        {
            // Present but at the wrong version: uninstall first. `appium driver install` refuses to
            // overwrite an existing driver, which is how the wrong version used to get stuck.
            var vUninstall = await RunAppiumAsync(
                "appium", $"driver uninstall {aName}", aCancellationToken).ConfigureAwait(false);
            vSteps.Add(vUninstall.ToEvidenceString());
        }

        var vInstall = await RunAppiumAsync(
            "appium", $"driver install {aName}@{aVersion}", aCancellationToken, TimeSpan.FromMinutes(10))
            .ConfigureAwait(false);
        vSteps.Add(vInstall.ToEvidenceString());
        return vSteps;
    }

    /// <summary>Runs one appium sub-command under the pinned PATH and <c>APPIUM_HOME</c>.</summary>
    /// <param name="aAppium">The appium binary (bare name or absolute managed path).</param>
    /// <param name="aArguments">The appium arguments (may include redirections; this is a shell line).</param>
    /// <param name="aCancellationToken">Cancels the run.</param>
    /// <param name="aTimeout">Optional timeout override; defaults to 60 seconds.</param>
    /// <returns>The run's evidence trail.</returns>
    private Task<ProcessRunResult> RunAppiumAsync(
        string aAppium, string aArguments, CancellationToken aCancellationToken, TimeSpan? aTimeout = null)
        => RunShellAsync($"{aAppium} {aArguments}", aTimeout ?? TimeSpan.FromSeconds(60), aCancellationToken);

    /// <summary>
    /// Runs a shell line with the TrSetup environment applied.
    ///
    /// PATH is PREPENDED with the managed Node bin dir (not replaced) because when Node came from
    /// TrSetup's own mac.node fixer it lives in the managed tools root and is exported only from
    /// <c>~/.zprofile</c>, which a running app never sees — so both the fixer AND the detect could
    /// otherwise fail on a machine TrSetup itself set up. APPIUM_HOME is exported so every Appium
    /// invocation — detect and fix alike — reads and writes the SAME manifest regardless of the
    /// process working directory.
    /// </summary>
    /// <param name="aCommand">The shell line to run.</param>
    /// <param name="aTimeout">Maximum run time before the process is killed.</param>
    /// <param name="aCancellationToken">Cancels the run.</param>
    /// <returns>The run's evidence trail.</returns>
    private Task<ProcessRunResult> RunShellAsync(
        string aCommand, TimeSpan aTimeout, CancellationToken aCancellationToken)
    {
        var vScript =
            $"export PATH=\\\"{MacNodeCheck.ManagedNodeBinDir}:$PATH\\\" && " +
            $"export APPIUM_HOME=\\\"{ManagedAppiumHome}\\\" && {aCommand}";
        return RunMacCommandAsync("bash", $"-c \"{vScript}\"", aTimeout, aCancellationToken);
    }

    /// <summary>
    /// Parses <c>appium driver list --installed --json</c> output into the installed driver set.
    /// </summary>
    /// <param name="aJson">The command's stdout.</param>
    /// <param name="aDrivers">Receives the drivers keyed by name; empty when parsing fails.</param>
    /// <returns><c>true</c> when the output was well-formed JSON (an empty object counts).</returns>
    private static bool TryParseDrivers(string aJson, out Dictionary<string, InstalledDriver> aDrivers)
    {
        aDrivers = new Dictionary<string, InstalledDriver>(StringComparer.OrdinalIgnoreCase);
        var vText = aJson?.Trim();
        if (string.IsNullOrEmpty(vText))
        {
            return false;
        }

        try
        {
            using var vDocument = JsonDocument.Parse(vText);
            if (vDocument.RootElement.ValueKind != JsonValueKind.Object)
            {
                return false;
            }

            foreach (var vEntry in vDocument.RootElement.EnumerateObject())
            {
                aDrivers[vEntry.Name] = new InstalledDriver(
                    vEntry.Name,
                    ReadString(vEntry.Value, "version"),
                    ReadString(vEntry.Value, "appiumVersion"));
            }

            return true;
        }
        catch (JsonException)
        {
            aDrivers.Clear();
            return false;
        }
    }

    private static string ReadString(JsonElement aElement, string aProperty)
        => aElement.ValueKind == JsonValueKind.Object &&
           aElement.TryGetProperty(aProperty, out var vValue) &&
           vValue.ValueKind == JsonValueKind.String
            ? vValue.GetString() ?? string.Empty
            : string.Empty;

    /// <summary>
    /// Whether the installed server satisfies the range a driver declares it needs.
    ///
    /// Appium's extension peer ranges are always caret ranges on a single major
    /// (<c>^2.4.1</c>, <c>^3.0.0-rc.2</c>), and Appium itself refuses to load a driver across a
    /// major boundary — so comparing majors is the honest, dependency-free test. An unparseable
    /// range is treated as compatible rather than failing the row on a parsing opinion.
    /// </summary>
    /// <param name="aServerVersion">The version <c>appium --version</c> reported.</param>
    /// <param name="aRange">The driver's declared <c>appiumVersion</c> range.</param>
    /// <returns><c>true</c> when the majors agree or the range cannot be read.</returns>
    private static bool IsServerCompatible(string aServerVersion, string aRange)
    {
        var vServerMajor = MajorOf(aServerVersion);
        var vRangeMajor = MajorOf(aRange);
        return vServerMajor < 0 || vRangeMajor < 0 || vServerMajor == vRangeMajor;
    }

    /// <summary>Extracts the leading major-version number from a version or caret range.</summary>
    /// <param name="aVersion">A version (<c>3.5.2</c>) or range (<c>^3.0.0-rc.2</c>).</param>
    /// <returns>The major number, or <c>-1</c> when none could be read.</returns>
    private static int MajorOf(string aVersion)
    {
        if (string.IsNullOrWhiteSpace(aVersion))
        {
            return -1;
        }

        var vDigits = aVersion.SkipWhile(aCharacter => !char.IsDigit(aCharacter))
            .TakeWhile(char.IsDigit)
            .ToArray();
        return vDigits.Length > 0 && int.TryParse(new string(vDigits), out var vMajor) ? vMajor : -1;
    }

    /// <summary>Renders the installed drivers for evidence, e.g. "xcuitest@11.17.7, mac2@4.0.4".</summary>
    /// <param name="aDrivers">The parsed driver set.</param>
    /// <returns>A human-readable list, or "no drivers" when empty.</returns>
    private static string DescribeInstalled(Dictionary<string, InstalledDriver> aDrivers)
        => aDrivers.Count == 0
            ? "no drivers"
            : string.Join(", ", aDrivers.Values.Select(aDriver => $"{aDriver.Name}@{aDriver.Version}"));

    /// <summary>
    /// Takes the last non-empty, non-warning line of stdout — <c>appium --version</c> can precede
    /// the number with manifest warnings on some paths.
    /// </summary>
    /// <param name="aOutput">The captured stdout.</param>
    /// <returns>The version line, or an empty string.</returns>
    private static string LastMeaningfulLine(string aOutput)
    {
        var vLines = (aOutput ?? string.Empty)
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(aLine => !aLine.StartsWith("WARN", StringComparison.OrdinalIgnoreCase))
            .ToArray();
        return vLines.Length > 0 ? vLines[^1] : string.Empty;
    }
}
