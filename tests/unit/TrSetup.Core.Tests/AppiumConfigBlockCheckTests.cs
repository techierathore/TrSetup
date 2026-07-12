using TrSetup.Core.Catalog.Framework;
using TrSetup.Core.Catalog.Probing;
using TrSetup.Core.Checks;
using TrSetup.Core.Settings;
using Xunit;

namespace TrSetup.Core.Tests;

/// <summary>
/// REQ-FN-024 — the framework appium-block writer and per-head curl-verify, exercised against a real
/// temp core-config.yaml: detect reports absent/stale/present; the fixer writes the appium block as a
/// single idempotent managed block that matches the WORKFLOW §0b step-4 shape while preserving user
/// config outside the markers; and each registered head is curl-verified via a fake HTTP probe.
/// </summary>
public sealed class AppiumConfigBlockCheckTests : IDisposable
{
    private const string MacIp = "192.168.1.50";

    private readonly string objDir;
    private readonly string objConfigPath;

    /// <summary>Creates a private temp directory holding the app-repo core-config.yaml.</summary>
    public AppiumConfigBlockCheckTests()
    {
        objDir = Path.Combine(Path.GetTempPath(), "trsetup-appium-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(objDir);
        objConfigPath = Path.Combine(objDir, "core-config.yaml");
    }

    /// <summary>Deletes the temp directory.</summary>
    public void Dispose() => Directory.Delete(objDir, recursive: true);

    /// <summary>
    /// Scenario: there is no core-config.yaml at all (not inside an app repo).
    /// Expect: the check is NotApplicable, never a failure.
    /// </summary>
    [Fact]
    public async Task NoConfigFileIsNotApplicable()
    {
        var vCheck = Build(new FakeHttpStatusProbe(), new TrSetupSettings());

        var vResult = await vCheck.DetectAsync(CancellationToken.None);

        Assert.Equal(CheckStatus.NotApplicable, vResult.Status);
    }

    /// <summary>
    /// Scenario: the config exists but has no managed appium block yet.
    /// Expect: detect fails ("offer to write the verified endpoints").
    /// </summary>
    [Fact]
    public async Task MissingBlockDetectsFail()
    {
        File.WriteAllText(objConfigPath, "markdownExploder: true\n# user config\n");
        var vCheck = Build(new FakeHttpStatusProbe(), Settings());

        var vResult = await vCheck.DetectAsync(CancellationToken.None);

        Assert.Equal(CheckStatus.Fail, vResult.Status);
    }

    /// <summary>
    /// Scenario: the fixer runs against a config that already holds user content.
    /// Expect: the written block matches the WORKFLOW §0b step-4 appium shape, the user content
    /// outside the markers survives, and each head is curl-verified via the HTTP probe.
    /// </summary>
    [Fact]
    public async Task FixWritesAppiumBlockAndCurlVerifiesHeads()
    {
        const string vUserTop = "markdownExploder: true\n";
        File.WriteAllText(objConfigPath, vUserTop);
        var vProbe = new FakeHttpStatusProbe();
        vProbe.Responses["http://localhost:4723/status"] = new HttpProbeResult(true, 200, "{}", null);
        var vCheck = Build(vProbe, Settings());

        var vFix = await vCheck.FixAsync!(FixerTestSupport.GrantFor(vCheck), CancellationToken.None);

        var vText = File.ReadAllText(objConfigPath);
        Assert.True(vFix.FixerReportedSuccess);
        Assert.Contains(vUserTop, vText);                                   // user config preserved
        Assert.Contains("runtimeVerification:", vText);
        Assert.Contains("    android:", vText);
        Assert.Contains("      url: http://localhost:4723", vText);
        Assert.Contains("      avd: Pixel_API_34", vText);
        Assert.Contains("    maccatalyst:", vText);
        Assert.Contains($"      url: http://{MacIp}:4723", vText);
        Assert.Contains("reachable (HTTP 200)", vFix.RawOutput);            // android head curl-verified
        Assert.Contains("UNREACHABLE", vFix.RawOutput);                     // ios/maccatalyst heads unmapped
    }

    /// <summary>
    /// Scenario: the fixer runs twice, then detect runs.
    /// Expect: exactly one managed block (idempotent) and detect passes.
    /// </summary>
    [Fact]
    public async Task RerunIsIdempotentThenDetectPasses()
    {
        File.WriteAllText(objConfigPath, "markdownExploder: true\n");
        var vCheck = Build(new FakeHttpStatusProbe(), Settings());

        await vCheck.FixAsync!(FixerTestSupport.GrantFor(vCheck), CancellationToken.None);
        await vCheck.FixAsync!(FixerTestSupport.GrantFor(vCheck), CancellationToken.None);
        var vResult = await vCheck.DetectAsync(CancellationToken.None);

        var vText = File.ReadAllText(objConfigPath);
        Assert.Equal(1, CountOccurrences(vText, ">>> TrSetup managed block: " + AppiumConfigBlockCheck.BlockId));
        Assert.Equal(CheckStatus.Pass, vResult.Status);
    }

    private AppiumConfigBlockCheck Build(IHttpStatusProbe aProbe, TrSetupSettings aSettings)
        => new(aProbe, () => aSettings, FixerTestSupport.Fix(new FakeProcessRunner()), () => objConfigPath);

    private static TrSetupSettings Settings()
    {
        var vSettings = new TrSetupSettings();
        vSettings.Endpoints["MacIp"] = MacIp;
        return vSettings;
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
