using TrSetup.Core.Catalog.Windows;
using TrSetup.Core.Checks;
using TrSetup.Core.Downloads;
using TrSetup.Core.Fixing;
using Xunit;

namespace TrSetup.Core.Tests;

/// <summary>
/// REQ-FN-015 — Windows auto-fixers: config writes are idempotent managed blocks; installers
/// download the pinned official source (URL surfaced in the preview) and run through the process
/// choke-point; admin steps elevate via a visible UAC child (Start-Process -Verb RunAs) and
/// refuse to run without granted consent (REQ-FN-020); a failed install surfaces its raw output.
/// </summary>
public sealed class WindowsFixerTests : IDisposable
{
    private readonly string objDir;

    /// <summary>Creates a private temp directory for config-write round-trips.</summary>
    public WindowsFixerTests() => objDir = FixerTestSupport.NewTempDir("winfix");

    /// <summary>Deletes the temp directory.</summary>
    public void Dispose()
    {
        if (Directory.Exists(objDir))
        {
            Directory.Delete(objDir, recursive: true);
        }
    }

    /// <summary>
    /// Scenario: the .wslconfig fixer runs twice against the same file.
    /// Expect: the preview names the wsl --shutdown follow-up, and the file holds exactly one
    /// managed block with networkingMode=mirrored (idempotent).
    /// </summary>
    [Fact]
    public async Task WslConfigFixWritesSingleManagedBlockAndPromptsShutdown()
    {
        var vPath = Path.Combine(objDir, ".wslconfig");
        var vRunner = new FakeProcessRunner();
        var vCheck = new WinWslConfigMirroredCheck(vRunner, FixerTestSupport.Fix(vRunner), () => vPath);
        Assert.Contains("wsl --shutdown", vCheck.FixPreview);

        await vCheck.FixAsync!(FixerTestSupport.GrantFor(vCheck), CancellationToken.None);
        await vCheck.FixAsync!(FixerTestSupport.GrantFor(vCheck), CancellationToken.None);

        var vText = File.ReadAllText(vPath);
        Assert.Equal(1, CountOccurrences(vText, ">>> TrSetup managed block: " + WinWslConfigMirroredCheck.BlockId));
        Assert.Contains("networkingMode=mirrored", vText);
    }

    /// <summary>
    /// Scenario: Node is missing, then the fixer downloads the pinned MSI and installs it under
    /// a visible UAC child, then a re-detect finds Node.
    /// Expect: detect Fail → the pinned MSI URL is requested and elevation goes through
    /// Start-Process -Verb RunAs for msiexec → verify Pass.
    /// </summary>
    [Fact]
    public async Task NodeFixDownloadsPinnedMsiElevatesThenGreen()
    {
        var vRunner = new FakeProcessRunner();
        var vDownloader = new FakeInstallerDownloader();
        var vCheck = new WinNodeCheck(vRunner, FixerTestSupport.Fix(vRunner, vDownloader));
        vRunner.Map("Start-Process", 0, "UAC child completed");
        vRunner.Map("node --version", 0, "NODE-MISSING\nNPM-MISSING");
        Assert.Equal(CheckStatus.Fail, (await vCheck.DetectAsync()).Status);
        Assert.Contains(WinNodeCheck.MsiUrl, vCheck.FixPreview);

        var vFix = await vCheck.FixAsync!(FixerTestSupport.GrantFor(vCheck), CancellationToken.None);

        Assert.True(vFix.FixerReportedSuccess);
        Assert.Contains(WinNodeCheck.MsiUrl, vDownloader.RequestedUrls);
        Assert.Contains(vRunner.Invocations, aLine => aLine.Contains("-Verb RunAs") && aLine.Contains("msiexec"));
        vRunner.Reset();
        vRunner.Map("node --version", 0, "NODE=v22.11.0\nNPM=10.9.0");
        Assert.Equal(CheckStatus.Pass, (await vCheck.DetectAsync()).Status);
    }

    /// <summary>
    /// Scenario: the pinned Node MSI download fails.
    /// Expect: the fixer reports failure and the download evidence is surfaced — nothing is
    /// installed on a bad download (REQ-NFR-004).
    /// </summary>
    [Fact]
    public async Task NodeFixFailedDownloadSurfacesEvidence()
    {
        var vRunner = new FakeProcessRunner();
        var vDownloader = new FakeInstallerDownloader(DownloadOutcome.Failed);
        var vCheck = new WinNodeCheck(vRunner, FixerTestSupport.Fix(vRunner, vDownloader));

        var vFix = await vCheck.FixAsync!(FixerTestSupport.GrantFor(vCheck), CancellationToken.None);

        Assert.False(vFix.FixerReportedSuccess);
        Assert.Contains("Failed", vFix.RawOutput);
        Assert.DoesNotContain(vRunner.Invocations, aLine => aLine.Contains("-Verb RunAs"));
    }

    /// <summary>
    /// Scenario: the API-34 system-image fixer runs sdkmanager.
    /// Expect: the sdkmanager package install is shelled through the process choke-point and a
    /// non-zero exit surfaces the raw stderr.
    /// </summary>
    [Fact]
    public async Task Api34ImageFixFailureSurfacesRawOutput()
    {
        var vRunner = new FakeProcessRunner();
        var vCheck = new WinAndroidApi34ImageCheck(vRunner, FixerTestSupport.Fix(vRunner));
        vRunner.Map("system-images;android-34", 1, string.Empty, "Error: could not accept licenses");

        var vFix = await vCheck.FixAsync!(FixerTestSupport.GrantFor(vCheck), CancellationToken.None);

        Assert.False(vFix.FixerReportedSuccess);
        Assert.Contains("could not accept licenses", vFix.RawOutput);
    }

    /// <summary>
    /// Scenario: the cmdline-tools fixer runs.
    /// Expect: it downloads the pinned Google cmdline-tools zip and then shells the unpack +
    /// platform-tools install through the process choke-point.
    /// </summary>
    [Fact]
    public async Task AndroidSdkFixDownloadsPinnedZipThenInstalls()
    {
        var vRunner = new FakeProcessRunner();
        vRunner.Map("platform-tools", 0, "installed platform-tools");
        var vDownloader = new FakeInstallerDownloader();
        var vCheck = new WinAndroidSdkCheck(vRunner, FixerTestSupport.Fix(vRunner, vDownloader));

        var vFix = await vCheck.FixAsync!(FixerTestSupport.GrantFor(vCheck), CancellationToken.None);

        Assert.True(vFix.FixerReportedSuccess);
        Assert.Contains(WinAndroidSdkCheck.CmdlineToolsUrl, vDownloader.RequestedUrls);
        Assert.Contains(vRunner.Invocations, aLine => aLine.Contains("Expand-Archive"));
    }

    /// <summary>
    /// Scenario: the session-helper fixer writes start-android-verify.ps1 from the embedded template.
    /// Expect: the write shells through the choke-point and the emitted script carries the
    /// reference AVD name from the template.
    /// </summary>
    [Fact]
    public async Task VerifyHelperFixWritesTemplateScript()
    {
        var vRunner = new FakeProcessRunner();
        vRunner.Map("Set-Content", 0, "WROTE start-android-verify.ps1");
        var vCheck = new WinVerifyHelperCheck(vRunner, FixerTestSupport.Fix(vRunner));

        var vFix = await vCheck.FixAsync!(FixerTestSupport.GrantFor(vCheck), CancellationToken.None);

        Assert.True(vFix.FixerReportedSuccess);
        Assert.Contains(vRunner.Invocations, aLine => aLine.Contains("Pixel_API_34"));
    }

    /// <summary>
    /// Scenario: the MAUI workload fixer (an admin step) is handed a granted consent token.
    /// Expect: the workload install elevates via a visible UAC child (Start-Process -Verb RunAs).
    /// </summary>
    [Fact]
    public async Task MauiWorkloadFixElevatesThroughUac()
    {
        var vRunner = new FakeProcessRunner();
        vRunner.Map("Start-Process", 0, "UAC child completed");
        var vCheck = new WinMauiWorkloadCheck(vRunner, FixerTestSupport.Fix(vRunner));

        var vFix = await vCheck.FixAsync!(FixerTestSupport.GrantFor(vCheck), CancellationToken.None);

        Assert.True(vFix.FixerReportedSuccess);
        Assert.Contains(vRunner.Invocations, aLine => aLine.Contains("-Verb RunAs") && aLine.Contains("dotnet workload install maui"));
    }

    /// <summary>
    /// Scenario: an admin fixer is handed a DECLINED consent token.
    /// Expect: it throws and nothing is launched — there is no path to elevation without consent
    /// (REQ-NFR-002).
    /// </summary>
    [Fact]
    public async Task ElevatedFixWithoutConsentLaunchesNothing()
    {
        var vRunner = new FakeProcessRunner();
        var vCheck = new WinMauiWorkloadCheck(vRunner, FixerTestSupport.Fix(vRunner));
        var vDeclined = ConsentToken.Declined(vCheck.FixPreview ?? string.Empty);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => vCheck.FixAsync!(vDeclined, CancellationToken.None));

        Assert.Empty(vRunner.Invocations);
    }

    /// <summary>
    /// Scenario: the JDK fixer preview.
    /// Expect: it surfaces the pinned Temurin MSI URL verbatim for consent.
    /// </summary>
    [Fact]
    public void JdkFixPreviewSurfacesPinnedTemurinUrl()
    {
        var vRunner = new FakeProcessRunner();
        var vCheck = new WinJdkCheck(vRunner, FixerTestSupport.Fix(vRunner));

        Assert.Contains(WinJdkCheck.MsiUrl, vCheck.FixPreview);
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
