using TrSetup.Core.Catalog.Mac;
using TrSetup.Core.Checks;
using TrSetup.Core.Settings;
using Xunit;

namespace TrSetup.Core.Tests;

/// <summary>
/// REQ-FN-028 — the "Build &amp; install &lt;App&gt; for Mac (Catalyst)" fixer: NotApplicable off macOS;
/// disabled with a reason while any prerequisite is red (detect fails naming the red ids, the fixer
/// refuses); its preview shows the literal <c>dotnet build -f net10.0-maccatalyst -c Release</c>; and
/// once prerequisites are green the fixer shells that build through the process choke-point.
/// </summary>
public sealed class MacCatalystBuildCheckTests
{
    private static readonly IReadOnlyList<string> NoReds = Array.Empty<string>();

    /// <summary>
    /// Scenario: the check runs on a non-macOS machine (WSL / Windows).
    /// Expect: NotApplicable — a Windows exe can never build a Catalyst app.
    /// </summary>
    [Fact]
    public async Task OffMacOsIsNotApplicable()
    {
        var vCheck = new MacCatalystBuildCheck(
            "AppStudio", new FakeProcessRunner(), _ => Task.FromResult(NoReds), aIsMacOs: () => false);

        var vResult = await vCheck.DetectAsync(CancellationToken.None);

        Assert.Equal(CheckStatus.NotApplicable, vResult.Status);
    }

    /// <summary>
    /// Scenario: on the Mac, some prerequisites are still red.
    /// Expect: detect fails naming the red ids and states the fixer stays disabled until green.
    /// </summary>
    [Fact]
    public async Task PrerequisitesRedGatesDetect()
    {
        var vReds = new[] { "appstudio.xcode", "appstudio.dotnet-sdk" };
        var vCheck = new MacCatalystBuildCheck(
            "AppStudio", new FakeProcessRunner(),
            _ => Task.FromResult<IReadOnlyList<string>>(vReds), aIsMacOs: () => true);

        var vResult = await vCheck.DetectAsync(CancellationToken.None);

        Assert.Equal(CheckStatus.Fail, vResult.Status);
        Assert.Contains("appstudio.xcode", vResult.Evidence);
        Assert.Contains("disabled", vResult.Evidence);
    }

    /// <summary>
    /// Scenario: the fixer is invoked while a prerequisite is still red.
    /// Expect: it refuses without running the build (self-reported failure).
    /// </summary>
    [Fact]
    public async Task FixRefusesWhilePrerequisitesRed()
    {
        var vRunner = new FakeProcessRunner();
        var vCheck = new MacCatalystBuildCheck(
            "AppStudio", vRunner,
            _ => Task.FromResult<IReadOnlyList<string>>(new[] { "appstudio.xcode" }),
            FixerTestSupport.Fix(vRunner), aIsMacOs: () => true);

        var vFix = await vCheck.FixAsync!(FixerTestSupport.GrantFor(vCheck), CancellationToken.None);

        Assert.False(vFix.FixerReportedSuccess);
        Assert.Contains("Refused", vFix.RawOutput);
        Assert.DoesNotContain(vRunner.Invocations, aLine => aLine.Contains("net10.0-maccatalyst"));
    }

    /// <summary>
    /// Scenario: inspecting the fixer preview.
    /// Expect: it shows the literal Catalyst build command.
    /// </summary>
    [Fact]
    public void PreviewShowsLiteralBuildCommand()
    {
        var vRunner = new FakeProcessRunner();
        var vCheck = new MacCatalystBuildCheck(
            "AppStudio", vRunner, _ => Task.FromResult(NoReds), FixerTestSupport.Fix(vRunner));

        Assert.Contains("dotnet build -f net10.0-maccatalyst -c Release", vCheck.FixPreview);
    }

    /// <summary>
    /// Creates a throwaway directory that passes repo-root validation (carries a .csproj marker).
    /// </summary>
    /// <returns>The absolute path of the created directory.</returns>
    private static string CreateFakeRepo()
    {
        var vRoot = Path.Combine(Path.GetTempPath(), "trsetup-repo-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(vRoot);
        File.WriteAllText(Path.Combine(vRoot, "Fake.csproj"), "<Project />");
        return vRoot;
    }

    /// <summary>
    /// Scenario: on the Mac with every prerequisite green AND a configured, valid repo path.
    /// Expect: it shells the Catalyst build through the process choke-point, in that repo root.
    /// </summary>
    [Fact]
    public async Task GreenPrerequisitesFixRunsCatalystBuild()
    {
        var vRepo = CreateFakeRepo();
        try
        {
            var vRunner = new FakeProcessRunner();
            vRunner.Map("net10.0-maccatalyst", 0, "Build succeeded");
            var vSettings = new TrSetupSettings { AppRepoPaths = { ["AppStudio"] = vRepo } };
            var vCheck = new MacCatalystBuildCheck(
                "AppStudio", vRunner, _ => Task.FromResult(NoReds), FixerTestSupport.Fix(vRunner),
                aIsMacOs: () => true, aSettings: () => vSettings);

            var vFix = await vCheck.FixAsync!(FixerTestSupport.GrantFor(vCheck), CancellationToken.None);

            Assert.True(vFix.FixerReportedSuccess);
            Assert.Contains(vRunner.Invocations, aLine => aLine.Contains("dotnet build -f net10.0-maccatalyst -c Release"));
        }
        finally
        {
            Directory.Delete(vRepo, recursive: true);
        }
    }

    /// <summary>
    /// Scenario (REQ-FN-028 defect): prerequisites green, but NO repo path is configured for the app.
    /// Expect: the fixer REFUSES and runs nothing — it must never fall back to the process working
    /// directory, which previously resolved to the publish folder and built in the wrong place.
    /// </summary>
    [Fact]
    public async Task UnconfiguredRepoPathRefusesWithoutBuilding()
    {
        var vRunner = new FakeProcessRunner();
        var vCheck = new MacCatalystBuildCheck(
            "AppStudio", vRunner, _ => Task.FromResult(NoReds), FixerTestSupport.Fix(vRunner),
            aIsMacOs: () => true, aSettings: () => new TrSetupSettings());

        var vFix = await vCheck.FixAsync!(FixerTestSupport.GrantFor(vCheck), CancellationToken.None);

        Assert.False(vFix.FixerReportedSuccess);
        Assert.Contains("Refused", vFix.RawOutput);
        Assert.Contains("No source-repo path configured", vFix.RawOutput);
        Assert.DoesNotContain(vRunner.Invocations, aLine => aLine.Contains("dotnet build"));
    }

    /// <summary>
    /// Scenario: a repo path IS configured but points at a directory that does not exist.
    /// Expect: the fixer refuses naming the bad path, and runs nothing.
    /// </summary>
    [Fact]
    public async Task MissingRepoPathRefusesWithoutBuilding()
    {
        var vRunner = new FakeProcessRunner();
        var vMissing = Path.Combine(Path.GetTempPath(), "trsetup-absent-" + Guid.NewGuid().ToString("N"));
        var vSettings = new TrSetupSettings { AppRepoPaths = { ["AppStudio"] = vMissing } };
        var vCheck = new MacCatalystBuildCheck(
            "AppStudio", vRunner, _ => Task.FromResult(NoReds), FixerTestSupport.Fix(vRunner),
            aIsMacOs: () => true, aSettings: () => vSettings);

        var vFix = await vCheck.FixAsync!(FixerTestSupport.GrantFor(vCheck), CancellationToken.None);

        Assert.False(vFix.FixerReportedSuccess);
        Assert.Contains("does not exist", vFix.RawOutput);
        Assert.DoesNotContain(vRunner.Invocations, aLine => aLine.Contains("dotnet build"));
    }

    /// <summary>
    /// Scenario: a configured path exists but is an arbitrary folder with no repo/build marker.
    /// Expect: refused — the fixer must not run a build in a directory that is not a source repo.
    /// </summary>
    [Fact]
    public async Task NonRepoDirectoryRefusesWithoutBuilding()
    {
        var vPlain = Path.Combine(Path.GetTempPath(), "trsetup-plain-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(vPlain);
        try
        {
            var vRunner = new FakeProcessRunner();
            var vSettings = new TrSetupSettings { AppRepoPaths = { ["AppStudio"] = vPlain } };
            var vCheck = new MacCatalystBuildCheck(
                "AppStudio", vRunner, _ => Task.FromResult(NoReds), FixerTestSupport.Fix(vRunner),
                aIsMacOs: () => true, aSettings: () => vSettings);

            var vFix = await vCheck.FixAsync!(FixerTestSupport.GrantFor(vCheck), CancellationToken.None);

            Assert.False(vFix.FixerReportedSuccess);
            Assert.Contains("not a source repo", vFix.RawOutput);
            Assert.DoesNotContain(vRunner.Invocations, aLine => aLine.Contains("dotnet build"));
        }
        finally
        {
            Directory.Delete(vPlain, recursive: true);
        }
    }

    /// <summary>
    /// Scenario: inspecting the preview with no repo path configured.
    /// Expect: it still shows the literal build command, but states plainly that it is blocked —
    /// never a command line built from the process working directory.
    /// </summary>
    [Fact]
    public void PreviewStatesBlockedWhenRepoPathUnconfigured()
    {
        var vRunner = new FakeProcessRunner();
        var vCheck = new MacCatalystBuildCheck(
            "AppStudio", vRunner, _ => Task.FromResult(NoReds), FixerTestSupport.Fix(vRunner),
            aSettings: () => new TrSetupSettings());

        Assert.Contains(MacCatalystBuildCheck.BuildCommand, vCheck.FixPreview);
        Assert.Contains("BLOCKED", vCheck.FixPreview);
        Assert.DoesNotContain(Directory.GetCurrentDirectory(), vCheck.FixPreview);
    }

    /// <summary>
    /// Scenario: inspecting the preview with a configured, valid repo path.
    /// Expect: the preview targets THAT directory, not the process working directory.
    /// </summary>
    [Fact]
    public void PreviewTargetsConfiguredRepoPath()
    {
        var vRepo = CreateFakeRepo();
        try
        {
            var vSettings = new TrSetupSettings { AppRepoPaths = { ["AppStudio"] = vRepo } };
            var vCheck = new MacCatalystBuildCheck(
                "AppStudio", new FakeProcessRunner(), _ => Task.FromResult(NoReds),
                FixerTestSupport.Fix(new FakeProcessRunner()), aSettings: () => vSettings);

            Assert.Contains(vRepo, vCheck.FixPreview);
            Assert.Contains(MacCatalystBuildCheck.BuildCommand, vCheck.FixPreview);
        }
        finally
        {
            Directory.Delete(vRepo, recursive: true);
        }
    }

    /// <summary>
    /// Scenario: on the Mac, prerequisites green and the produced .app already exists.
    /// Expect: detect passes, evidencing the built .app path.
    /// </summary>
    [Fact]
    public async Task GreenPrerequisitesWithBuiltAppPasses()
    {
        var vAppDir = Path.Combine(Path.GetTempPath(), "trsetup-catalyst-" + Guid.NewGuid().ToString("N") + ".app");
        Directory.CreateDirectory(vAppDir);
        try
        {
            var vCheck = new MacCatalystBuildCheck(
                "AppStudio", new FakeProcessRunner(), _ => Task.FromResult(NoReds),
                aIsMacOs: () => true, aAppBundlePath: () => vAppDir);

            var vResult = await vCheck.DetectAsync(CancellationToken.None);

            Assert.Equal(CheckStatus.Pass, vResult.Status);
            Assert.Contains(vAppDir, vResult.Evidence);
        }
        finally
        {
            Directory.Delete(vAppDir, recursive: true);
        }
    }
}
