using TrSetup.Core.Checks;
using TrSetup.Core.Downloads;
using TrSetup.Core.Profiles;
using TrSetup.Core.Profiles.Handlers;
using Xunit;

namespace TrSetup.Core.Tests;

/// <summary>
/// REQ-FN-025 — the isolated ComfyUI runtime-install check. Detect finds the entrypoint under the
/// managed tools root; the fixer downloads the pinned official GitHub release into the managed
/// location (its own embedded Python — no system-Python collision) and extracts it there. The real
/// download is UAT; here detect, preview and the fix orchestration run against fakes and a temp
/// managed root that is reset in a finally.
/// </summary>
[Collection(ManagedRootCollection.Name)]
public sealed class RuntimeInstallHandlerTests : IDisposable
{
    private readonly string objRoot;

    /// <summary>Points the managed root at a private temp dir so no real install is touched.</summary>
    public RuntimeInstallHandlerTests()
    {
        objRoot = FixerTestSupport.NewTempDir("runtime");
        TrSetupPaths.RootOverride = objRoot;
    }

    /// <summary>Restores the managed root and deletes the temp dir.</summary>
    public void Dispose()
    {
        TrSetupPaths.RootOverride = null;
        if (Directory.Exists(objRoot))
        {
            Directory.Delete(objRoot, recursive: true);
        }
    }

    /// <summary>
    /// Scenario: the ComfyUI runtime is absent from the managed root; the fix preview is inspected.
    /// Expect: detect Fails with evidence naming the managed dir; the preview shows the pinned
    /// GitHub release URL and the managed target path.
    /// </summary>
    [Fact]
    public async Task RuntimeDetectAbsentAndPreviewShowsPinnedUrlAndManagedPath()
    {
        var vCheck = new RuntimeInstallCheck(ComfyReq(), "TrStudio", new FakeProcessRunner(), FixerTestSupport.Fix(new FakeProcessRunner()));

        var vResult = await vCheck.DetectAsync();

        Assert.Equal(CheckStatus.Fail, vResult.Status);
        Assert.Contains(vCheck.RuntimeDir, vResult.Evidence);
        Assert.Contains(vCheck.DownloadUrl, vCheck.FixPreview);
        Assert.Contains(vCheck.RuntimeDir, vCheck.FixPreview);
        Assert.Contains(RuntimeInstallCheck.DefaultComfyUiTag, vCheck.DownloadUrl);
    }

    /// <summary>
    /// Scenario: the runtime entrypoint (main.py) exists under the managed dir.
    /// Expect: detect Passes, reporting the managed install.
    /// </summary>
    [Fact]
    public async Task RuntimeDetectPresentWhenEntrypointExists()
    {
        var vCheck = new RuntimeInstallCheck(ComfyReq(), "TrStudio", new FakeProcessRunner(), FixerTestSupport.Fix(new FakeProcessRunner()));
        Directory.CreateDirectory(vCheck.RuntimeDir);
        File.WriteAllText(Path.Combine(vCheck.RuntimeDir, "main.py"), "# comfyui");

        var vResult = await vCheck.DetectAsync();

        Assert.Equal(CheckStatus.Pass, vResult.Status);
    }

    /// <summary>
    /// Scenario: the fix downloads the pinned release then extracts it into the managed dir.
    /// Expect: the pinned URL is the one requested and the extraction runs through the process
    /// runner into the managed location (no system path touched).
    /// </summary>
    [Fact]
    public async Task RuntimeFixDownloadsPinnedReleaseAndExtractsIntoManagedDir()
    {
        var vRunner = new FakeProcessRunner();
        var vDownloader = new FakeInstallerDownloader();
        var vCheck = new RuntimeInstallCheck(ComfyReq(), "TrStudio", vRunner, FixerTestSupport.Fix(vRunner, vDownloader));
        vRunner.Map("tar -xf", 0, "extracted");

        var vFix = await vCheck.FixAsync!(FixerTestSupport.GrantFor(vCheck), CancellationToken.None);

        Assert.True(vFix.FixerReportedSuccess);
        Assert.Contains(vCheck.DownloadUrl, vDownloader.RequestedUrls);
        Assert.Contains(vRunner.Invocations, aLine => aLine.Contains("tar -xf") && aLine.Contains(vCheck.RuntimeDir));
    }

    private static ProfileRequirement ComfyReq()
    {
        var vParams = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { ["runtime"] = "comfyui" };
        return new ProfileRequirement("runtime-install", "trstudio.comfyui", "ComfyUI", MachineRole.AppRunnerMac, CheckSeverity.Required, vParams);
    }
}
