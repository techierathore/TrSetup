using TrSetup.Core.Checks;
using TrSetup.Core.Engine;
using TrSetup.Core.Fixing;
using Xunit;

namespace TrSetup.Core.Tests;

/// <summary>
/// REQ-FN-001 — the <c>Check</c> contract: a fake check drives the full contract
/// (identity, roles, severity, explain, detect, fix preview, fix, verify), and
/// NotApplicable checks never render as failures in the board model.
/// </summary>
public sealed class CheckContractTests
{
    /// <summary>
    /// Scenario: construct a fake check exposing every contract member, then drive
    /// DetectAsync, FixPreview, FixAsync (with a granted consent token) and VerifyAsync.
    /// Expect: all members return the configured values and the calls round-trip.
    /// </summary>
    [Fact]
    public async Task FakeCheckDrivesFullContract()
    {
        var vCheck = new TestDoubles.FakeCheck(
            "test.contract",
            MachineRole.AgentHostWsl | MachineRole.NativeDev,
            aDetect: _ => Task.FromResult(CheckResult.Fail("tool missing")),
            aFix: (aConsent, _) => Task.FromResult(new FixResult(true, $"installed after consent to: {aConsent.PreviewShown}")));

        var vDetect = await vCheck.DetectAsync();
        var vFix = await vCheck.FixAsync!(ConsentToken.Granted(vCheck.FixPreview!), CancellationToken.None);
        var vVerify = await vCheck.VerifyAsync();

        Assert.Equal("test.contract", vCheck.Id);
        Assert.Equal("Fake check test.contract", vCheck.Title);
        Assert.Equal("Test group", vCheck.Category);
        Assert.Equal(MachineRole.AgentHostWsl | MachineRole.NativeDev, vCheck.Roles);
        Assert.Equal(CheckSeverity.Required, vCheck.Severity);
        Assert.Equal("What test.contract is", vCheck.Explain.What);
        Assert.Equal("Why test.contract matters", vCheck.Explain.Why);
        Assert.Equal("https://example.test/docs", vCheck.Explain.DocLink);
        Assert.Equal(CheckStatus.Fail, vDetect.Status);
        Assert.Equal("tool missing", vDetect.Evidence);
        Assert.False(vCheck.IsManualOnly);
        Assert.Contains("test.contract", vCheck.FixPreview);
        Assert.Contains("fake-fix --install test.contract", vFix.RawOutput);
        Assert.Equal(CheckStatus.Fail, vVerify.Status);
    }

    /// <summary>
    /// Scenario: a check whose FixAsync is null.
    /// Expect: IsManualOnly is true and FixPreview is null — the UI shows guidance, not a Fix button.
    /// </summary>
    [Fact]
    public void NullFixAsyncMeansManualOnly()
    {
        var vCheck = new TestDoubles.FakeCheck("test.manual", MachineRole.DeviceHostMac);

        Assert.Null(vCheck.FixAsync);
        Assert.True(vCheck.IsManualOnly);
        Assert.Null(vCheck.FixPreview);
    }

    /// <summary>
    /// Scenario: VerifyAsync is not overridden.
    /// Expect: it re-detects (contract default), incrementing the detect call count.
    /// </summary>
    [Fact]
    public async Task VerifyDefaultsToRedetect()
    {
        var vCheck = new TestDoubles.FakeCheck("test.verify", MachineRole.AgentHostWsl);

        await vCheck.DetectAsync();
        await vCheck.VerifyAsync();

        Assert.Equal(2, vCheck.DetectCallCount);
    }

    /// <summary>
    /// Scenario: a board is built where one check is out of scope (role mismatch) and one
    /// in-scope check detects NotApplicable.
    /// Expect: both rows report NotApplicable and neither is ever a failure.
    /// </summary>
    [Fact]
    public async Task NotApplicableNeverRendersAsFailure()
    {
        var vOutOfScope = new TestDoubles.FakeCheck("test.outofscope", MachineRole.DeviceHostMac);
        var vDetectsNa = new TestDoubles.FakeCheck(
            "test.detectsna",
            MachineRole.AgentHostWsl,
            aDetect: _ => Task.FromResult(CheckResult.NotApplicable("feature not relevant here")));
        var vEngine = new CheckEngine(new[] { vOutOfScope, vDetectsNa });

        var vBoard = vEngine.BuildBoard(MachineRole.AgentHostWsl, null);
        await vEngine.RunDetectSweepAsync(vBoard);

        var vRows = vBoard.Rows.ToList();
        Assert.All(vRows, aRow => Assert.Equal(CheckStatus.NotApplicable, aRow.Status));
        Assert.All(vRows, aRow => Assert.False(aRow.IsFailure));
    }
}
