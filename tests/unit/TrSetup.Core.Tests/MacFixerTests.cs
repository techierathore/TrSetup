using TrSetup.Core.Catalog.Mac;
using TrSetup.Core.Checks;
using TrSetup.Core.ConfigWriting;
using TrSetup.Core.Downloads;
using TrSetup.Core.Elevation;
using TrSetup.Core.Fixing;
using TrSetup.Core.Settings;
using Xunit;

namespace TrSetup.Core.Tests;

/// <summary>
/// REQ-FN-016 — Mac auto-fixers: the CLT installs while full Xcode stays manual; SDK/Node/driver
/// installs shell through the process choke-point (Node lands under the managed root with an
/// idempotent PATH block); the LaunchAgent plist is written as a single managed block and loaded
/// so it survives reboots; the DHCP-reservation stable-IP row keeps no Fix button.
/// </summary>
[Collection(ManagedRootCollection.Name)]
public sealed class MacFixerTests : IDisposable
{
    private readonly string objDir;

    /// <summary>Points the managed tools root at a private temp directory.</summary>
    public MacFixerTests()
    {
        objDir = FixerTestSupport.NewTempDir("macfix");
        TrSetupPaths.RootOverride = objDir;
    }

    /// <summary>Restores the managed root and deletes the temp directory.</summary>
    public void Dispose()
    {
        TrSetupPaths.RootOverride = null;
        if (Directory.Exists(objDir))
        {
            Directory.Delete(objDir, recursive: true);
        }
    }

    /// <summary>
    /// Scenario: the Xcode fixer runs.
    /// Expect: it shells <c>xcode-select --install</c> (Command-Line Tools only) and the preview
    /// states full Xcode stays a manual App Store install.
    /// </summary>
    [Fact]
    public async Task XcodeCltFixInstallsCltAndKeepsFullXcodeManual()
    {
        var vRunner = new FakeProcessRunner();
        vRunner.Map("--install", 0, "installing Command Line Tools");
        var vCheck = new MacXcodeCheck(vRunner, FixerTestSupport.Fix(vRunner));
        Assert.Contains("manual", vCheck.FixPreview);

        var vFix = await vCheck.FixAsync!(FixerTestSupport.GrantFor(vCheck), CancellationToken.None);

        Assert.True(vFix.FixerReportedSuccess);
        Assert.Contains(vRunner.Invocations, aLine => aLine.Contains("xcode-select --install"));
    }

    /// <summary>
    /// Scenario: the .NET + MAUI fixer runs.
    /// Expect: it downloads the pinned dotnet-install script, runs it into the user-local dotnet
    /// dir, and installs the maui workload into that SDK.
    /// </summary>
    [Fact]
    public async Task DotnetMauiFixDownloadsSdkThenInstallsWorkload()
    {
        // Inject the shell profile — the fixer now writes a managed PATH block, and without an
        // injected path this test would write into the REAL ~/.zprofile of whoever runs the suite.
        var vProfile = Path.Combine(objDir, ".zprofile");
        var vRunner = new FakeProcessRunner();
        vRunner.Map("dotnet-install.sh", 0, "dotnet installed");
        vRunner.Map("workload install maui", 0, "maui installed");
        var vDownloader = new FakeInstallerDownloader();
        var vCheck = new MacDotnetMauiCheck(vRunner, FixerTestSupport.Fix(vRunner, vDownloader), () => vProfile);

        var vFix = await vCheck.FixAsync!(FixerTestSupport.GrantFor(vCheck), CancellationToken.None);

        Assert.True(vFix.FixerReportedSuccess);
        Assert.Contains(MacDotnetMauiCheck.InstallScriptUrl, vDownloader.RequestedUrls);
        Assert.Contains(vRunner.Invocations, aLine => aLine.Contains("workload install maui"));
    }

    /// <summary>
    /// Scenario: the .NET fixer runs twice with an injected shell-profile path.
    /// Expect: the profile ends up with EXACTLY ONE managed block exporting both DOTNET_ROOT and
    /// PATH. dotnet-install.sh deliberately installs a private SDK on no PATH at all, so without
    /// this block TrSetup could install a .NET the owner can never invoke from their own terminal.
    /// </summary>
    [Fact]
    public async Task DotnetMauiFixWritesManagedPathBlockIdempotently()
    {
        var vProfile = Path.Combine(objDir, ".zprofile");
        var vDotnetDir = Path.Combine(objDir, "dotnet-home");
        MacDotnetMauiCheck.DotnetDirOverride = vDotnetDir;
        try
        {
            var vRunner = new FakeProcessRunner();
            vRunner.Map("dotnet-install.sh", 0, "dotnet installed");
            vRunner.Map("workload install maui", 0, "maui installed");
            var vCheck = new MacDotnetMauiCheck(
                vRunner, FixerTestSupport.Fix(vRunner, new FakeInstallerDownloader()), () => vProfile);

            await vCheck.FixAsync!(FixerTestSupport.GrantFor(vCheck), CancellationToken.None);
            await vCheck.FixAsync!(FixerTestSupport.GrantFor(vCheck), CancellationToken.None);

            var vText = File.ReadAllText(vProfile);
            Assert.Equal(1, CountOccurrences(vText, ">>> TrSetup managed block: " + MacDotnetMauiCheck.PathBlockId));
            Assert.Contains($"export DOTNET_ROOT=\"{vDotnetDir}\"", vText);
            Assert.Contains($"export PATH=\"{vDotnetDir}:$PATH\"", vText);
        }
        finally
        {
            MacDotnetMauiCheck.DotnetDirOverride = null;
        }
    }

    /// <summary>
    /// Scenario: a user already has their own content in ~/.zprofile when the .NET fixer runs.
    /// Expect: everything outside the managed markers survives byte-for-byte — the fixer edits a
    /// real user file and must never clobber it.
    /// </summary>
    [Fact]
    public async Task DotnetMauiFixPreservesExistingShellProfileContent()
    {
        var vProfile = Path.Combine(objDir, ".zprofile");
        const string vUserLine = "export MY_OWN_VAR=\"keep me\"";
        File.WriteAllText(vProfile, vUserLine + Environment.NewLine);
        var vRunner = new FakeProcessRunner();
        vRunner.Map("dotnet-install.sh", 0, "dotnet installed");
        vRunner.Map("workload install maui", 0, "maui installed");
        var vCheck = new MacDotnetMauiCheck(
            vRunner, FixerTestSupport.Fix(vRunner, new FakeInstallerDownloader()), () => vProfile);

        await vCheck.FixAsync!(FixerTestSupport.GrantFor(vCheck), CancellationToken.None);

        var vText = File.ReadAllText(vProfile);
        Assert.Contains(vUserLine, vText);
        Assert.Contains(MacDotnetMauiCheck.PathBlockId, vText);
    }

    /// <summary>
    /// Scenario: the Node fixer runs twice with an injected shell-profile path.
    /// Expect: the pinned tarball URL is requested and the profile holds exactly one managed
    /// node PATH block (idempotent, under the managed root).
    /// </summary>
    [Fact]
    public async Task NodeFixDownloadsPinnedAndWritesManagedPathIdempotently()
    {
        var vProfile = Path.Combine(objDir, ".zprofile");
        var vRunner = new FakeProcessRunner();
        vRunner.Map("tar", 0, "extracted");
        var vDownloader = new FakeInstallerDownloader();
        var vCheck = new MacNodeCheck(vRunner, FixerTestSupport.Fix(vRunner, vDownloader), () => vProfile);

        await vCheck.FixAsync!(FixerTestSupport.GrantFor(vCheck), CancellationToken.None);
        await vCheck.FixAsync!(FixerTestSupport.GrantFor(vCheck), CancellationToken.None);

        Assert.Contains(MacNodeCheck.TarballUrl, vDownloader.RequestedUrls);
        var vText = File.ReadAllText(vProfile);
        Assert.Equal(1, CountOccurrences(vText, ">>> TrSetup managed block: " + MacNodeCheck.PathBlockId));
    }

    /// <summary>
    /// Scenario: the LaunchAgent fixer runs twice with an injected plist path.
    /// Expect: the plist holds exactly one managed block whose body sets RunAtLoad + KeepAlive
    /// (survives reboot), and the fixer loads it with launchctl.
    /// </summary>
    [Fact]
    public async Task AppiumLaunchAgentFixWritesSurvivingPlistIdempotently()
    {
        var vPlist = Path.Combine(objDir, "com.trsetup.appium.plist");
        var vRunner = new FakeProcessRunner();
        vRunner.Map("launchctl", 0, string.Empty);
        var vCheck = new MacAppiumLaunchAgentCheck(
            vRunner, new FakeHttpStatusProbe(), () => new TrSetupSettings(), FixerTestSupport.Fix(vRunner), () => vPlist);

        await vCheck.FixAsync!(FixerTestSupport.GrantFor(vCheck), CancellationToken.None);
        await vCheck.FixAsync!(FixerTestSupport.GrantFor(vCheck), CancellationToken.None);

        var vText = File.ReadAllText(vPlist);
        Assert.Equal(1, CountOccurrences(vText, ">>> TrSetup managed block: " + MacAppiumLaunchAgentCheck.AgentLabel));
        Assert.Contains("RunAtLoad", vText);
        Assert.Contains("KeepAlive", vText);
        Assert.Contains(vRunner.Invocations, aLine => aLine.Contains("launchctl load -w"));
    }

    /// <summary>
    /// REQ-FN-016: the LaunchAgent is the server that ACTUALLY SERVES the drivers, so it must run in
    /// the same environment the Appium fixer installs into — otherwise the board can report Pass for
    /// a driver set the live server never loads. It previously set neither APPIUM_HOME (so the served
    /// Appium resolved its manifest cwd-relative) nor PATH (and `bash -lc` reads ~/.bash_profile, never
    /// the ~/.zprofile block mac.node writes, so the managed Node was invisible to it).
    /// Expect: the plist pins BOTH.
    /// </summary>
    [Fact]
    public async Task AppiumLaunchAgentPlistPinsTheManagedAppiumHomeAndNodePath()
    {
        var vPlist = Path.Combine(objDir, "com.trsetup.appium.plist");
        var vRunner = new FakeProcessRunner();
        vRunner.Map("launchctl", 0, string.Empty);
        var vCheck = new MacAppiumLaunchAgentCheck(
            vRunner, new FakeHttpStatusProbe(), () => new TrSetupSettings(), FixerTestSupport.Fix(vRunner), () => vPlist);

        await vCheck.FixAsync!(FixerTestSupport.GrantFor(vCheck), CancellationToken.None);

        var vText = await File.ReadAllTextAsync(vPlist);
        Assert.Contains("APPIUM_HOME", vText);
        Assert.Contains(MacAppiumDriversCheck.ManagedAppiumHome, vText);
        Assert.Contains(MacNodeCheck.ManagedNodeBinDir, vText);
    }

    /// <summary>
    /// Scenario: the iOS Simulator fixer runs.
    /// Expect: it shells <c>xcodebuild -downloadPlatform iOS</c> through the choke-point.
    /// </summary>
    [Fact]
    public async Task IosSimulatorFixDownloadsRuntime()
    {
        var vRunner = new FakeProcessRunner();
        vRunner.Map("downloadPlatform", 0, "downloaded iOS runtime");
        var vCheck = new MacIosSimulatorCheck(vRunner, FixerTestSupport.Fix(vRunner));

        var vFix = await vCheck.FixAsync!(FixerTestSupport.GrantFor(vCheck), CancellationToken.None);

        Assert.True(vFix.FixerReportedSuccess);
        Assert.Contains(vRunner.Invocations, aLine => aLine.Contains("xcodebuild -downloadPlatform iOS"));
    }

    /// <summary>
    /// Scenario: the stable-IP row, whose durable fix is a router DHCP reservation.
    /// Expect: no automated fixer — it stays manual guidance and never grows a Fix button.
    /// </summary>
    [Fact]
    public void StableIpRowHasNoFixer()
    {
        var vRunner = new FakeProcessRunner();
        var vCheck = new MacStableIpCheck(vRunner, () => new TrSetupSettings());

        Assert.Null(vCheck.FixAsync);
        Assert.True(vCheck.IsManualOnly);
    }

    // ── Post-fix PATH blindness (REQ-FN-016, 2026-07-20) ────────────────────────────────────────
    // A fixer that installs into a TrSetup-managed location must still be DETECTED afterwards. The
    // managed installs are exported only from ~/.zprofile, which a running app never re-reads, so a
    // detect that probes the bare command alone reports RED right after a SUCCESSFUL install and the
    // row looks like "clicking Fix does nothing". These pin the managed-location fallbacks.
    //
    // The bug hid in manual testing because `dotnet TrSetup.Web.dll` is MUXER-hosted and can spawn
    // tools that are absent from PATH; the shipping self-contained MAUI apphost cannot. The fake
    // runner models the apphost: any unmapped command returns 127 "command not found".

    /// <summary>Creates an executable-looking stub file, making parent directories as needed.</summary>
    /// <param name="aPath">Absolute path of the stub to create.</param>
    private static void StageBinary(string aPath)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(aPath)!);
        File.WriteAllText(aPath, "#!/bin/sh\n");
    }

    /// <summary>
    /// Scenario: Node is NOT on the process PATH, but mac.node's fixer already installed it under
    /// the managed tools root.
    /// Expect: detect passes via the managed binary and the evidence names it plus the new-login-shell
    /// caveat — never a bare Fail that hides a completed install.
    /// </summary>
    [Fact]
    public async Task NodeDetectFallsBackToManagedInstallWhenNotOnPath()
    {
        var vManagedNode = Path.Combine(MacNodeCheck.ManagedNodeBinDir, "node");
        StageBinary(vManagedNode);
        var vRunner = new FakeProcessRunner();
        vRunner.Map(vManagedNode, 0, MacNodeCheck.NodeVersion);   // bare `node` stays unmapped => 127
        var vCheck = new MacNodeCheck(vRunner);

        var vResult = await vCheck.DetectAsync(CancellationToken.None);

        Assert.Equal(CheckStatus.Pass, vResult.Status);
        Assert.Contains(MacNodeCheck.NodeVersion, vResult.Evidence);
        Assert.Contains(vManagedNode, vResult.Evidence);
        Assert.Contains("PATH", vResult.Evidence);
    }

    /// <summary>
    /// Scenario: neither the bare command nor a managed install exists.
    /// Expect: an honest Fail — the fallback must not invent a pass.
    /// </summary>
    [Fact]
    public async Task NodeDetectStillFailsWhenNeitherPathNorManagedInstallExists()
    {
        var vCheck = new MacNodeCheck(new FakeProcessRunner());

        var vResult = await vCheck.DetectAsync(CancellationToken.None);

        Assert.Equal(CheckStatus.Fail, vResult.Status);
        Assert.Contains("not found", vResult.Evidence, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Scenario: `dotnet` is not on the process PATH, but this check's own fixer already installed
    /// the SDK (with the maui workload) into its managed directory.
    /// Expect: detect passes via that managed muxer and says where it lives — the defect proved on
    /// macOS 26.5 was a successful install re-verifying red.
    /// </summary>
    [Fact]
    public async Task DotnetMauiDetectFallsBackToManagedSdkWhenNotOnPath()
    {
        var vDotnetDir = Path.Combine(objDir, "dotnet-home");
        var vManagedDotnet = Path.Combine(vDotnetDir, "dotnet");
        StageBinary(vManagedDotnet);
        MacDotnetMauiCheck.DotnetDirOverride = vDotnetDir;
        try
        {
            var vRunner = new FakeProcessRunner();
            vRunner.Map(vManagedDotnet, 0, "maui-maccatalyst 10.0.20");  // bare `dotnet` => 127
            var vCheck = new MacDotnetMauiCheck(vRunner);

            var vResult = await vCheck.DetectAsync(CancellationToken.None);

            Assert.Equal(CheckStatus.Pass, vResult.Status);
            Assert.Contains(vManagedDotnet, vResult.Evidence);
            Assert.Contains("PATH", vResult.Evidence);
        }
        finally
        {
            MacDotnetMauiCheck.DotnetDirOverride = null;
        }
    }

    /// <summary>
    /// Scenario: appium is not on the process PATH, but it was npm-installed into the TrSetup-managed
    /// Node install (the machine TrSetup itself provisioned).
    /// Expect: detect resolves it there and reports the drivers, rather than "Appium not found".
    /// </summary>
    [Fact]
    public async Task AppiumDetectFallsBackToManagedNodeBinWhenNotOnPath()
    {
        var vManagedAppium = Path.Combine(MacNodeCheck.ManagedNodeBinDir, "appium");
        StageBinary(vManagedAppium);
        var vRunner = new FakeProcessRunner();
        vRunner.Map($"{vManagedAppium} driver list", 0, PinnedDriverJson);
        vRunner.Map($"{vManagedAppium} --version", 0, MacAppiumDriversCheck.AppiumVersion);
        var vCheck = new MacAppiumDriversCheck(vRunner);

        var vResult = await vCheck.DetectAsync(CancellationToken.None);

        Assert.Equal(CheckStatus.Pass, vResult.Status);
    }

    /// <summary>
    /// Scenario: the appium fixer runs on a machine whose Node came from mac.node's fixer, so npm is
    /// only in the managed bin dir.
    /// Expect: the fix PREPENDS that dir to PATH before shelling npm — otherwise TrSetup installs
    /// Node and then cannot use it. Prepend (not replace) so a system Node still resolves normally.
    /// </summary>
    [Fact]
    public async Task AppiumFixPutsManagedNodeBinOnPathBeforeShellingNpm()
    {
        var vRunner = new FakeProcessRunner();
        vRunner.Map("npm install -g appium", 0, "added 1 package");
        var vCheck = new MacAppiumDriversCheck(vRunner, FixerTestSupport.Fix(vRunner));

        await vCheck.FixAsync!(FixerTestSupport.GrantFor(vCheck), CancellationToken.None);

        var vInvocation = Assert.Single(vRunner.Invocations, aLine => aLine.Contains("npm install -g appium"));
        Assert.Contains(MacNodeCheck.ManagedNodeBinDir, vInvocation);
        Assert.Contains("$PATH", vInvocation);
    }

    /// <summary>
    /// REQ-FN-016 defect 1 — the important one. With <c>APPIUM_HOME</c> unset, Appium resolves its
    /// extension manifest from the PROCESS WORKING DIRECTORY, so the fixer could install drivers into
    /// one manifest while the detect read another (observed live: a repo-local manifest holding
    /// mac2@4.0.4 and a <c>~/.appium</c> manifest holding mac2@2.2.2).
    /// Expect: EVERY appium invocation — detect and fix alike — exports the one managed APPIUM_HOME.
    /// </summary>
    [Fact]
    public async Task AppiumDetectAndFixPinTheSameManagedAppiumHome()
    {
        var vRunner = new FakeProcessRunner();
        vRunner.Map("driver list", 0, PinnedDriverJson);
        vRunner.Map("appium --version", 0, MacAppiumDriversCheck.AppiumVersion);
        vRunner.Map("npm install -g appium", 0, "added 1 package");
        var vCheck = new MacAppiumDriversCheck(vRunner, FixerTestSupport.Fix(vRunner));

        await vCheck.DetectAsync(CancellationToken.None);
        await vCheck.FixAsync!(FixerTestSupport.GrantFor(vCheck), CancellationToken.None);

        Assert.NotEmpty(vRunner.Invocations);
        Assert.All(vRunner.Invocations, aLine =>
            Assert.Contains($"APPIUM_HOME=\\\"{MacAppiumDriversCheck.ManagedAppiumHome}\\\"", aLine));
    }

    /// <summary>
    /// REQ-FN-016 defect 4: the board reported "Appium not found" when the truth was "Appium is
    /// there, its drivers are not".
    /// Expect: a missing BINARY and missing DRIVERS produce plainly different evidence.
    /// </summary>
    [Fact]
    public async Task AppiumDetectDistinguishesMissingBinaryFromMissingDrivers()
    {
        var vNoBinary = new FakeProcessRunner();
        var vBinaryResult = await new MacAppiumDriversCheck(vNoBinary).DetectAsync(CancellationToken.None);

        var vNoDrivers = new FakeProcessRunner();
        vNoDrivers.Map("driver list", 0, "{}");
        vNoDrivers.Map("appium --version", 0, MacAppiumDriversCheck.AppiumVersion);
        var vDriverResult = await new MacAppiumDriversCheck(vNoDrivers).DetectAsync(CancellationToken.None);

        Assert.Equal(CheckStatus.Fail, vBinaryResult.Status);
        Assert.Contains("SERVER NOT FOUND", vBinaryResult.Evidence);

        Assert.Equal(CheckStatus.Fail, vDriverResult.Status);
        Assert.DoesNotContain("SERVER NOT FOUND", vDriverResult.Evidence);
        Assert.Contains("MISSING", vDriverResult.Evidence);
        Assert.Contains("xcuitest", vDriverResult.Evidence);
        Assert.Contains("mac2", vDriverResult.Evidence);
    }

    /// <summary>
    /// The live defect on the UAT Mac: appium 2.0.1 with mac2@4.0.4, whose peer range is
    /// <c>^3.0.0-rc.2</c>. Appium will not load a driver across a major boundary, so Catalyst
    /// automation cannot work.
    /// Expect: Fail (never Pass) and evidence that says INCOMPATIBLE, not "missing".
    /// </summary>
    [Fact]
    public async Task AppiumDetectFailsWhenDriversAreIncompatibleWithTheInstalledServer()
    {
        var vRunner = new FakeProcessRunner();
        vRunner.Map("driver list", 0, DriverJson(("xcuitest", "9.10.5", "^2.0.0"), ("mac2", "4.0.4", "^3.0.0-rc.2")));
        vRunner.Map("appium --version", 0, "2.0.1");
        var vCheck = new MacAppiumDriversCheck(vRunner);

        var vResult = await vCheck.DetectAsync(CancellationToken.None);

        Assert.Equal(CheckStatus.Fail, vResult.Status);
        Assert.Contains("INCOMPATIBLE", vResult.Evidence);
        Assert.Contains("mac2@4.0.4", vResult.Evidence);
    }

    /// <summary>
    /// The old detect matched driver names anywhere in stdout+stderr, so Appium's own
    /// "Driver "xcuitest" may be incompatible..." WARNING made the row report Pass for a driver that
    /// was not installed at all.
    /// Expect: only the <c>--json</c> manifest on stdout counts; a name in a warning proves nothing.
    /// </summary>
    [Fact]
    public async Task AppiumDetectIgnoresDriverNamesThatAppearOnlyInWarnings()
    {
        var vRunner = new FakeProcessRunner();
        vRunner.Map(
            "driver list",
            0,
            DriverJson(("mac2", MacAppiumDriversCheck.Mac2DriverVersion, "^3.0.0-rc.2")),
            "WARN Appium Driver \"xcuitest\" has 1 potential problem");
        vRunner.Map("appium --version", 0, MacAppiumDriversCheck.AppiumVersion);
        var vCheck = new MacAppiumDriversCheck(vRunner);

        var vResult = await vCheck.DetectAsync(CancellationToken.None);

        Assert.Equal(CheckStatus.Fail, vResult.Status);
        Assert.Contains("MISSING", vResult.Evidence);
        Assert.Contains("xcuitest", vResult.Evidence);
    }

    /// <summary>
    /// REQ-FN-016 defect 2: unpinned <c>npm install -g appium</c> resolved 2.0.1 on the pinned Node,
    /// which cannot load the current drivers.
    /// Expect: the fixer installs the pinned server AND both pinned, peer-compatible drivers.
    /// </summary>
    [Fact]
    public async Task AppiumFixInstallsThePinnedMutuallyCompatibleSet()
    {
        var vRunner = new FakeProcessRunner();
        vRunner.Map("driver list", 0, "{}");
        vRunner.Map("npm install -g appium", 0, "added 1 package");
        vRunner.Map("driver install", 0, "successfully installed");
        var vCheck = new MacAppiumDriversCheck(vRunner, FixerTestSupport.Fix(vRunner));

        await vCheck.FixAsync!(FixerTestSupport.GrantFor(vCheck), CancellationToken.None);

        Assert.Contains(vRunner.Invocations, aLine =>
            aLine.Contains($"npm install -g appium@{MacAppiumDriversCheck.AppiumVersion}"));
        Assert.Contains(vRunner.Invocations, aLine =>
            aLine.Contains($"driver install xcuitest@{MacAppiumDriversCheck.XcuitestDriverVersion}"));
        Assert.Contains(vRunner.Invocations, aLine =>
            aLine.Contains($"driver install mac2@{MacAppiumDriversCheck.Mac2DriverVersion}"));
    }

    /// <summary>
    /// The old guard was <c>... | grep -q mac2 || appium driver install mac2</c>: a NAME match, so a
    /// driver stuck at an old incompatible version counted as "already installed" and the fixer could
    /// never repair it — the row stayed red forever.
    /// Expect: a wrong-version driver is uninstalled and reinstalled at the pinned version.
    /// </summary>
    [Fact]
    public async Task AppiumFixReplacesADriverStuckAtTheWrongVersion()
    {
        var vRunner = new FakeProcessRunner();
        vRunner.Map("driver list", 0, DriverJson(
            ("xcuitest", MacAppiumDriversCheck.XcuitestDriverVersion, "^3.0.0-rc.2"),
            ("mac2", "2.2.2", "^2.4.1")));
        vRunner.Map("npm install -g appium", 0, "added 1 package");
        vRunner.Map("driver uninstall", 0, "uninstalled");
        vRunner.Map("driver install", 0, "successfully installed");
        var vCheck = new MacAppiumDriversCheck(vRunner, FixerTestSupport.Fix(vRunner));

        await vCheck.FixAsync!(FixerTestSupport.GrantFor(vCheck), CancellationToken.None);

        Assert.Contains(vRunner.Invocations, aLine => aLine.Contains("driver uninstall mac2"));
        Assert.Contains(vRunner.Invocations, aLine =>
            aLine.Contains($"driver install mac2@{MacAppiumDriversCheck.Mac2DriverVersion}"));

        // xcuitest was already at the pinned version — it must NOT be churned.
        Assert.DoesNotContain(vRunner.Invocations, aLine => aLine.Contains("driver uninstall xcuitest"));
        Assert.DoesNotContain(vRunner.Invocations, aLine => aLine.Contains("driver install xcuitest"));
    }

    /// <summary>
    /// Expect: on an already-correct machine the fixer installs nothing and still reports success,
    /// judged from a re-read of the manifest rather than a shell chain's exit code.
    /// </summary>
    [Fact]
    public async Task AppiumFixIsANoOpWhenAlreadyAtThePinnedVersions()
    {
        var vRunner = new FakeProcessRunner();
        vRunner.Map("driver list", 0, PinnedDriverJson);
        vRunner.Map("npm install -g appium", 0, "changed 0 packages");
        var vCheck = new MacAppiumDriversCheck(vRunner, FixerTestSupport.Fix(vRunner));

        var vFix = await vCheck.FixAsync!(FixerTestSupport.GrantFor(vCheck), CancellationToken.None);

        Assert.True(vFix.FixerReportedSuccess);
        Assert.DoesNotContain(vRunner.Invocations, aLine => aLine.Contains("driver install"));
        Assert.DoesNotContain(vRunner.Invocations, aLine => aLine.Contains("driver uninstall"));
    }

    /// <summary>
    /// REQ-FN-016 defect 2, root cause: Node v22.11.0 satisfies none of appium@3's engine ranges, so
    /// <c>npm install -g appium</c> silently resolved a v2 server. A Node that old must not read as
    /// green, or the Appium row is doomed before it runs.
    /// Expect: Fail that names the reason, not the misleading "Node.js not found".
    /// </summary>
    [Fact]
    public async Task NodeDetectFailsWhenTooOldForThePinnedAppium()
    {
        var vRunner = new FakeProcessRunner();
        vRunner.Map("node --version", 0, "v22.11.0");
        var vCheck = new MacNodeCheck(vRunner);

        var vResult = await vCheck.DetectAsync(CancellationToken.None);

        Assert.Equal(CheckStatus.Fail, vResult.Status);
        Assert.Contains("TOO OLD", vResult.Evidence);
        Assert.Contains(MacNodeCheck.MinimumNodeVersion, vResult.Evidence);
        Assert.DoesNotContain("not found", vResult.Evidence);
    }

    /// <summary>
    /// REQ-FN-016: `tar -xzf` only adds/overwrites — it never deletes entries the new tarball lacks.
    /// Extracting the bumped Node pin ON TOP of an older managed install therefore left stale files
    /// inside npm's own node_modules, and the hybrid npm died on every command with
    /// "Class extends value undefined is not a constructor or null" — bricking the Appium fixer that
    /// depends on it (observed live).
    /// Expect: the fixer CLEARS the managed Node dir before extracting, so no stale file survives.
    /// </summary>
    [Fact]
    public async Task NodeFixReplacesRatherThanOverlaysAnExistingManagedInstall()
    {
        var vStale = Path.Combine(MacNodeCheck.ManagedNodeBinDir, "..", "lib", "node_modules", "npm", "stale-from-old-version");
        Directory.CreateDirectory(Path.GetDirectoryName(vStale)!);
        await File.WriteAllTextAsync(vStale, "left behind by the previous Node");
        Assert.True(File.Exists(vStale));

        var vRunner = new FakeProcessRunner();
        vRunner.Map("tar", 0, "extracted");
        var vCheck = new MacNodeCheck(
            vRunner,
            FixerTestSupport.Fix(vRunner),
            () => Path.Combine(objDir, ".zprofile"));

        await vCheck.FixAsync!(FixerTestSupport.GrantFor(vCheck), CancellationToken.None);

        Assert.False(File.Exists(vStale));
    }

    /// <summary>
    /// REQ-FN-016 regression: the real downloader stages the archive INSIDE the managed Node dir
    /// ({ToolsRoot}/node/node-lts.tar.gz). Clearing that dir AFTER the download therefore deletes the
    /// very tarball about to be extracted — tar then failed with "No such file or directory" and the
    /// machine was left with NO Node at all (observed live).
    /// Expect: the clear happens BEFORE the download, so the staged archive survives to the extract.
    /// </summary>
    [Fact]
    public async Task NodeFixClearsTheManagedDirBeforeStagingTheTarballNotAfter()
    {
        var vNodeDir = Path.Combine(MacNodeCheck.ManagedNodeBinDir, "..");
        var vStaged = Path.GetFullPath(Path.Combine(vNodeDir, "node-lts.tar.gz"));
        var vRunner = new FakeProcessRunner();
        vRunner.Map("tar", 0, "extracted");
        var vCheck = new MacNodeCheck(
            vRunner,
            new CheckFixServices(
                new StagingInsideNodeDirDownloader(vStaged),
                new ManagedBlockWriter(),
                new ElevationRunner(vRunner)),
            () => Path.Combine(objDir, ".zprofile"));

        await vCheck.FixAsync!(FixerTestSupport.GrantFor(vCheck), CancellationToken.None);

        Assert.True(File.Exists(vStaged), "the staged tarball was deleted before it could be extracted");
        Assert.Contains(vRunner.Invocations, aLine => aLine.Contains("tar") && aLine.Contains("node-lts.tar.gz"));
    }

    /// <summary>
    /// A downloader that behaves like the real one: it stages the archive INSIDE the managed Node
    /// directory, which is exactly what makes the clear-then-download ordering load-bearing.
    /// </summary>
    private sealed class StagingInsideNodeDirDownloader : IInstallerDownloader
    {
        private readonly string objTargetPath;

        /// <summary>Creates the fake.</summary>
        /// <param name="aTargetPath">Where the archive is staged (inside the managed Node dir).</param>
        public StagingInsideNodeDirDownloader(string aTargetPath) => objTargetPath = aTargetPath;

        /// <inheritdoc />
        public async Task<DownloadResult> DownloadAsync(
            DownloadRequest aRequest,
            IProgress<string>? aProgress = null,
            CancellationToken aCancellationToken = default)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(objTargetPath)!);
            await File.WriteAllTextAsync(objTargetPath, "pinned node tarball", aCancellationToken);
            return new DownloadResult(DownloadOutcome.NoPublishedChecksum, objTargetPath, "staged");
        }
    }

    /// <summary>The manifest JSON for a correctly-converged machine (server + both pinned drivers).</summary>
    private static string PinnedDriverJson => DriverJson(
        ("xcuitest", MacAppiumDriversCheck.XcuitestDriverVersion, "^3.0.0-rc.2"),
        ("mac2", MacAppiumDriversCheck.Mac2DriverVersion, "^3.0.0-rc.2"));

    /// <summary>
    /// Builds the shape <c>appium driver list --installed --json</c> emits on stdout.
    /// </summary>
    /// <param name="aDrivers">Each driver's name, installed version and declared server range.</param>
    /// <returns>The JSON document.</returns>
    private static string DriverJson(params (string Name, string Version, string AppiumRange)[] aDrivers)
        => "{" + string.Join(
            ",",
            aDrivers.Select(aDriver =>
                $"\"{aDriver.Name}\":{{\"pkgName\":\"appium-{aDriver.Name}-driver\"," +
                $"\"version\":\"{aDriver.Version}\",\"appiumVersion\":\"{aDriver.AppiumRange}\",\"installed\":true}}")) + "}";

    private static int CountOccurrences(string aText, string aNeedle)
    {
        var vCount = 0;
        var vIndex = 0;
        while ((vIndex = aText.IndexOf(aNeedle, vIndex, StringComparison.Ordinal)) >= 0)
        {
            vCount++;
            vIndex += aNeedle.Length;
        }

        return vCount;
    }
}
