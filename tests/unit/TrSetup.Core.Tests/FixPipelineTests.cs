using TrSetup.Core.Checks;
using TrSetup.Core.Fixing;
using TrSetup.Core.Tests.TestDoubles;
using Xunit;

namespace TrSetup.Core.Tests;

/// <summary>
/// REQ-FN-002 — the Detect → Preview → Fix → Re-verify pipeline: fixes only run after
/// preview+consent, a fix whose verify does not come back green is FAILED with raw output
/// attached (never "assume fixed"), and manual-only checks execute nothing.
/// </summary>
public sealed class FixPipelineTests
{
    /// <summary>
    /// Scenario: a fixer claims success, but VerifyAsync still detects Fail.
    /// Expect: the run yields FixRunStatus.Failed with the fixer's raw output attached and
    /// the failing verify result captured — the pipeline never assumes fixed.
    /// </summary>
    [Fact]
    public async Task FailedVerifyYieldsFailedWithRawOutput()
    {
        var vCheck = new FakeCheck(
            "test.stubborn",
            MachineRole.AgentHostWsl,
            aDetect: _ => Task.FromResult(CheckResult.Fail("still broken: exit code 127")),
            aFix: (_, _) => Task.FromResult(new FixResult(true, "installer said OK\nwrote /opt/tool")));
        var vPipeline = new FixPipeline(new FakeConsentProvider(aGrant: true));

        var vResult = await vPipeline.RunAsync(vCheck);

        Assert.Equal(FixRunStatus.Failed, vResult.Status);
        Assert.Contains("installer said OK", vResult.RawOutput);
        Assert.NotNull(vResult.VerifyResult);
        Assert.Equal(CheckStatus.Fail, vResult.VerifyResult!.Status);
        Assert.Contains("still broken", vResult.VerifyResult.Evidence);
    }

    /// <summary>
    /// Scenario: the fixer runs and the re-verify comes back Pass.
    /// Expect: FixRunStatus.Fixed with the verify evidence attached.
    /// </summary>
    [Fact]
    public async Task GreenVerifyYieldsFixed()
    {
        var vFixed = false;
        var vCheck = new FakeCheck(
            "test.fixable",
            MachineRole.AgentHostWsl,
            aDetect: _ => Task.FromResult(vFixed ? CheckResult.Pass("tool 1.2.3 found") : CheckResult.Fail("missing")),
            aFix: (_, _) =>
            {
                vFixed = true;
                return Task.FromResult(new FixResult(true, "installed tool 1.2.3"));
            });
        var vPipeline = new FixPipeline(new FakeConsentProvider(aGrant: true));

        var vResult = await vPipeline.RunAsync(vCheck);

        Assert.Equal(FixRunStatus.Fixed, vResult.Status);
        Assert.Equal(CheckStatus.Pass, vResult.VerifyResult!.Status);
    }

    /// <summary>
    /// Scenario: the user declines consent after seeing the preview.
    /// Expect: FixRunStatus.Declined, the fixer never executes, and the preview that was
    /// shown is the check's FixPreview.
    /// </summary>
    [Fact]
    public async Task DeclinedConsentExecutesNothing()
    {
        var vFixerRan = false;
        var vCheck = new FakeCheck(
            "test.declined",
            MachineRole.AgentHostWsl,
            aFix: (_, _) =>
            {
                vFixerRan = true;
                return Task.FromResult(new FixResult(true, "should never happen"));
            });
        var vProvider = new FakeConsentProvider(aGrant: false);
        var vPipeline = new FixPipeline(vProvider);

        var vResult = await vPipeline.RunAsync(vCheck);

        Assert.Equal(FixRunStatus.Declined, vResult.Status);
        Assert.False(vFixerRan);
        Assert.Equal(vCheck.FixPreview, vProvider.LastPreviewShown);
        Assert.Empty(vResult.RawOutput);
    }

    /// <summary>
    /// Scenario: the check has no fixer (FixAsync is null).
    /// Expect: FixRunStatus.ManualOnly and consent is never even requested.
    /// </summary>
    [Fact]
    public async Task ManualOnlyCheckSkipsConsentAndFix()
    {
        var vCheck = new FakeCheck("test.manualonly", MachineRole.DeviceHostWindows);
        var vProvider = new FakeConsentProvider(aGrant: true);
        var vPipeline = new FixPipeline(vProvider);

        var vResult = await vPipeline.RunAsync(vCheck);

        Assert.Equal(FixRunStatus.ManualOnly, vResult.Status);
        Assert.Equal(0, vProvider.RequestCount);
    }
}
