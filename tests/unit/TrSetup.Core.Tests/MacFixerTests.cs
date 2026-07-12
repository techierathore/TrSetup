using TrSetup.Core.Catalog.Mac;
using TrSetup.Core.Checks;
using TrSetup.Core.Downloads;
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
        var vRunner = new FakeProcessRunner();
        vRunner.Map("dotnet-install.sh", 0, "dotnet installed");
        vRunner.Map("workload install maui", 0, "maui installed");
        var vDownloader = new FakeInstallerDownloader();
        var vCheck = new MacDotnetMauiCheck(vRunner, FixerTestSupport.Fix(vRunner, vDownloader));

        var vFix = await vCheck.FixAsync!(FixerTestSupport.GrantFor(vCheck), CancellationToken.None);

        Assert.True(vFix.FixerReportedSuccess);
        Assert.Contains(MacDotnetMauiCheck.InstallScriptUrl, vDownloader.RequestedUrls);
        Assert.Contains(vRunner.Invocations, aLine => aLine.Contains("workload install maui"));
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
