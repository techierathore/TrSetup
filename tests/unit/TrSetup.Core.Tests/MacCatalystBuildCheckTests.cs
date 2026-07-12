using TrSetup.Core.Catalog.Mac;
using TrSetup.Core.Checks;
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
    /// Scenario: on the Mac with every prerequisite green, the fixer runs.
    /// Expect: it shells the Catalyst build through the process choke-point and reports success.
    /// </summary>
    [Fact]
    public async Task GreenPrerequisitesFixRunsCatalystBuild()
    {
        var vRunner = new FakeProcessRunner();
        vRunner.Map("net10.0-maccatalyst", 0, "Build succeeded");
        var vCheck = new MacCatalystBuildCheck(
            "AppStudio", vRunner, _ => Task.FromResult(NoReds), FixerTestSupport.Fix(vRunner), aIsMacOs: () => true);

        var vFix = await vCheck.FixAsync!(FixerTestSupport.GrantFor(vCheck), CancellationToken.None);

        Assert.True(vFix.FixerReportedSuccess);
        Assert.Contains(vRunner.Invocations, aLine => aLine.Contains("dotnet build -f net10.0-maccatalyst -c Release"));
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
