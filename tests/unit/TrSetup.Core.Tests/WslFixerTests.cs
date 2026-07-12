using TrSetup.Core.Catalog.Wsl;
using TrSetup.Core.Checks;
using TrSetup.Core.Downloads;
using TrSetup.Core.Fixing;
using Xunit;

namespace TrSetup.Core.Tests;

/// <summary>
/// REQ-FN-014 — WSL auto-fixers: a broken item goes Fix → re-detect green; PATH/file writes are
/// idempotent (managed marker block, single copy on re-run); a failed install surfaces its raw
/// output; sudo steps are terminal handoffs that execute nothing and never touch a password
/// (REQ-FN-020 / REQ-NFR-002); the WSL-side manual rows keep no Fix button.
/// </summary>
[Collection(ManagedRootCollection.Name)]
public sealed class WslFixerTests : IDisposable
{
    private readonly string objHome;
    private readonly string objToolsRootDir;

    /// <summary>Points HOME and the managed tools root at private temp directories.</summary>
    public WslFixerTests()
    {
        objHome = FixerTestSupport.NewTempDir("wslfix-home");
        objToolsRootDir = FixerTestSupport.NewTempDir("wslfix-root");
        TrSetupPaths.RootOverride = objToolsRootDir;
    }

    /// <summary>Restores the managed root and deletes the temp directories.</summary>
    public void Dispose()
    {
        TrSetupPaths.RootOverride = null;
        DeleteQuietly(objHome);
        DeleteQuietly(objToolsRootDir);
    }

    /// <summary>
    /// Scenario: no .NET SDK, then the fixer downloads dotnet-install.sh (pinned URL) and runs
    /// it, then a re-detect finds the SDK.
    /// Expect: detect Fail → fix reports success and requested the pinned URL → verify Pass.
    /// </summary>
    [Fact]
    public async Task DotnetSdkBrokenThenFixThenGreen()
    {
        var vRunner = new FakeProcessRunner();
        var vDownloader = new FakeInstallerDownloader();
        var vProbe = new FakeSystemProbe { HomeDirectory = objHome };
        var vCheck = new WslDotnetSdkCheck(vRunner, vProbe, FixerTestSupport.Fix(vRunner, vDownloader));
        Assert.Equal(CheckStatus.Fail, (await vCheck.DetectAsync()).Status);

        vRunner.Map("dotnet-install.sh", 0, "dotnet-install: Installation finished");
        var vFix = await vCheck.FixAsync!(FixerTestSupport.GrantFor(vCheck), CancellationToken.None);

        Assert.True(vFix.FixerReportedSuccess);
        Assert.Contains(WslDotnetSdkCheck.InstallScriptUrl, vDownloader.RequestedUrls);
        vRunner.Reset();
        vRunner.Map("--list-sdks", 0, "10.0.100 [/home/tester/.dotnet/sdk]");
        Assert.Equal(CheckStatus.Pass, (await vCheck.VerifyAsync()).Status);
    }

    /// <summary>
    /// Scenario: the dotnet-install script runs but exits non-zero (disk full).
    /// Expect: the fixer reports failure and the raw output carries the installer's stderr —
    /// never "assume fixed".
    /// </summary>
    [Fact]
    public async Task DotnetSdkFailedInstallSurfacesRawOutput()
    {
        var vRunner = new FakeProcessRunner();
        var vProbe = new FakeSystemProbe { HomeDirectory = objHome };
        var vCheck = new WslDotnetSdkCheck(vRunner, vProbe, FixerTestSupport.Fix(vRunner));
        vRunner.Map("dotnet-install.sh", 1, string.Empty, "install failed: no space left on device");

        var vFix = await vCheck.FixAsync!(FixerTestSupport.GrantFor(vCheck), CancellationToken.None);

        Assert.False(vFix.FixerReportedSuccess);
        Assert.Contains("no space left on device", vFix.RawOutput);
    }

    /// <summary>
    /// Scenario: the winrun fixer runs twice against the same HOME.
    /// Expect: the script is written and executable, and ~/.bashrc holds exactly one managed
    /// PATH block (idempotent — a re-run never duplicates).
    /// </summary>
    [Fact]
    public async Task WinrunFixWritesExecutableAndIsIdempotent()
    {
        var vProbe = new FakeSystemProbe { HomeDirectory = objHome };
        var vCheck = new WslWinrunBridgeCheck(vProbe, FixerTestSupport.Fix(new FakeProcessRunner()));

        await vCheck.FixAsync!(FixerTestSupport.GrantFor(vCheck), CancellationToken.None);
        await vCheck.FixAsync!(FixerTestSupport.GrantFor(vCheck), CancellationToken.None);

        var vWinrun = Path.Combine(objHome, "bin", "winrun");
        Assert.True(File.Exists(vWinrun));
        if (!OperatingSystem.IsWindows())
        {
            Assert.True(File.GetUnixFileMode(vWinrun).HasFlag(UnixFileMode.UserExecute));
        }

        var vBashrc = File.ReadAllText(Path.Combine(objHome, ".bashrc"));
        Assert.Equal(1, CountOccurrences(vBashrc, ">>> TrSetup managed block: " + WslWinrunBridgeCheck.PathBlockId));
    }

    /// <summary>
    /// Scenario: the Node fixer runs twice — downloading the pinned LTS tarball and adding the
    /// managed PATH block.
    /// Expect: the pinned URL is requested and ~/.bashrc holds exactly one node PATH block.
    /// </summary>
    [Fact]
    public async Task NodeFixDownloadsPinnedAndWritesManagedPathIdempotently()
    {
        var vRunner = new FakeProcessRunner();
        vRunner.Map("tar", 0, "extracted");
        var vDownloader = new FakeInstallerDownloader();
        var vProbe = new FakeSystemProbe { HomeDirectory = objHome };
        var vCheck = new WslNodeCheck(vRunner, vProbe, FixerTestSupport.Fix(vRunner, vDownloader));

        await vCheck.FixAsync!(FixerTestSupport.GrantFor(vCheck), CancellationToken.None);
        await vCheck.FixAsync!(FixerTestSupport.GrantFor(vCheck), CancellationToken.None);

        Assert.Contains(WslNodeCheck.TarballUrl, vDownloader.RequestedUrls);
        var vBashrc = File.ReadAllText(Path.Combine(objHome, ".bashrc"));
        Assert.Equal(1, CountOccurrences(vBashrc, ">>> TrSetup managed block: " + WslNodeCheck.PathBlockId));
    }

    /// <summary>
    /// Scenario: the headless-Chromium apt libraries are fixed — an apt step that needs root.
    /// Expect: no process is executed; the fix returns the one sudo line to paste, and the
    /// instructions state TrSetup never asks for or stores the password (REQ-NFR-002).
    /// </summary>
    [Fact]
    public async Task ChromiumLibsFixIsSudoHandoffThatExecutesNothing()
    {
        var vRunner = new FakeProcessRunner();
        var vCheck = new WslChromiumLibsCheck(vRunner, FixerTestSupport.Fix(vRunner));

        Assert.StartsWith("sudo apt-get install -y ", vCheck.FixPreview);
        var vFix = await vCheck.FixAsync!(FixerTestSupport.GrantFor(vCheck), CancellationToken.None);

        Assert.False(vFix.FixerReportedSuccess);
        Assert.Contains("never asks for or stores", vFix.RawOutput);
        Assert.Empty(vRunner.Invocations);
    }

    /// <summary>
    /// Scenario: the Playwright fixer runs.
    /// Expect: it shells the npm install + browser install through the process choke-point.
    /// </summary>
    [Fact]
    public async Task PlaywrightFixRunsInstallThroughProcessRunner()
    {
        var vRunner = new FakeProcessRunner();
        vRunner.Map("playwright install chromium", 0, "downloaded chromium");
        var vProbe = new FakeSystemProbe { HomeDirectory = objHome };
        var vCheck = new WslPlaywrightCheck(vRunner, vProbe, FixerTestSupport.Fix(vRunner));

        var vFix = await vCheck.FixAsync!(FixerTestSupport.GrantFor(vCheck), CancellationToken.None);

        Assert.True(vFix.FixerReportedSuccess);
        Assert.Contains(vRunner.Invocations, aLine => aLine.Contains("playwright install chromium"));
    }

    /// <summary>
    /// Scenario: the git fixer previews and runs.
    /// Expect: a sudo terminal handoff for <c>apt-get install -y git</c> that executes nothing.
    /// </summary>
    [Fact]
    public async Task GitFixIsSudoHandoff()
    {
        var vRunner = new FakeProcessRunner();
        var vCheck = new WslGitCheck(vRunner, FixerTestSupport.Fix(vRunner));

        Assert.Equal("sudo apt-get install -y git", vCheck.FixPreview);
        var vFix = await vCheck.FixAsync!(FixerTestSupport.GrantFor(vCheck), CancellationToken.None);

        Assert.Contains("sudo apt-get install -y git", vFix.RawOutput);
        Assert.Empty(vRunner.Invocations);
    }

    /// <summary>
    /// Scenario: the WSL-side rows the BRD marks manual (mirrored networking view + the two
    /// cross-machine reachability probes).
    /// Expect: no automated fixer — the board shows "Open guide", not a Fix button.
    /// </summary>
    [Fact]
    public void ManualWslRowsHaveNoFixer()
    {
        var vRunner = new FakeProcessRunner();
        Assert.Null(new WslMirroredNetworkingCheck(vRunner).FixAsync);
        Assert.Null(new WslWindowsAppiumCheck(new FakeHttpStatusProbe()).FixAsync);
        Assert.Null(new WslMacAppiumCheck(new FakeHttpStatusProbe(), () => new TrSetup.Core.Settings.TrSetupSettings()).FixAsync);
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

    private static void DeleteQuietly(string aDir)
    {
        try
        {
            if (Directory.Exists(aDir))
            {
                Directory.Delete(aDir, recursive: true);
            }
        }
        catch (IOException)
        {
            // Best-effort temp cleanup.
        }
    }
}
