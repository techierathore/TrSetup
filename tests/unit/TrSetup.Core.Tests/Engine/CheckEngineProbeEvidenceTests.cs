using System.Diagnostics;
using Xunit;
using TrSetup.Core.Catalog.Probing;
using TrSetup.Core.Checks;
using TrSetup.Core.Engine;
using TrSetup.Core.Tests.TestDoubles;

namespace TrSetup.Core.Tests.Engine;

/// <summary>
/// REQ-FN-016 nested-timeout race (2026-07-21): the engine's per-check budget must be
/// STRICTLY GREATER than any inner probe timeout, so a check that fails its own probe
/// always wins the race and gets to report ITS OWN evidence.
/// </summary>
/// <remarks>
/// The defect: <see cref="CheckEngine.DefaultProbeTimeout"/> and
/// <see cref="HttpStatusProbe.ProbeTimeout"/> were both a hard-coded 5 s, so both fired at
/// the same instant and the engine's generic "Probe timed out after 5 s." replaced the
/// check's own diagnosis — discarding the only text naming the address that was probed.
/// These tests pin BOTH halves of the contract: evidence survives when the check itself
/// fails, and the budget still hard-bounds a genuinely stuck check (REQ-UI-001).
/// </remarks>
public sealed class CheckEngineProbeEvidenceTests
{
    /// <summary>The engine's generic message, which must NOT appear when a check reports its own.</summary>
    private const string GenericTimeoutText = "Probe timed out";

    /// <summary>
    /// Scenario: the shipped defaults. Expected: the engine budget strictly exceeds the inner
    /// HTTP probe timeout — the drift guard that keeps the two from silently re-equalising.
    /// </summary>
    [Fact]
    public void DefaultBudgetStrictlyExceedsInnerHttpProbeTimeout()
    {
        Assert.True(
            CheckEngine.DefaultProbeTimeout > HttpStatusProbe.ProbeTimeout,
            $"Engine budget {CheckEngine.DefaultProbeTimeout} must exceed the inner HTTP probe " +
            $"timeout {HttpStatusProbe.ProbeTimeout}, else a check's own evidence is discarded.");
    }

    /// <summary>
    /// Scenario: a check whose inner probe times out and returns its own diagnosis JUST UNDER the
    /// engine budget — the exact live shape of <c>mac.appium-launchagent</c>. Expected: the row
    /// carries the check's own address-bearing evidence, not the engine's generic timeout string.
    /// </summary>
    [Fact]
    public async Task SweepKeepsCheckOwnEvidenceWhenInnerProbeFailsJustUnderBudget()
    {
        // Same ratio as production (inner probe finishes one headroom before the budget),
        // scaled down so the test stays fast.
        var vInnerProbe = TimeSpan.FromMilliseconds(150);
        var vEngineBudget = TimeSpan.FromMilliseconds(900);
        const string vOwnEvidence = "Appium not answering on http://192.168.1.77:4723/status (timeout).";

        var vCheck = new StubCheck("test.inner-timeout", async aToken =>
        {
            // The check's inner probe gives up on its own budget and reports what it learned.
            await Task.Delay(vInnerProbe, aToken).ConfigureAwait(false);
            return CheckResult.Fail(vOwnEvidence);
        });
        var vEngine = new CheckEngine(new Check[] { vCheck });
        var vBoard = vEngine.BuildBoard(MachineRole.AgentHostWsl, null);

        await vEngine.RunDetectSweepAsync(vBoard, vEngineBudget);

        var vRow = Assert.Single(vBoard.Rows);
        Assert.Equal(CheckStatus.Fail, vRow.Status);
        Assert.Equal(vOwnEvidence, vRow.Evidence);
        Assert.DoesNotContain(GenericTimeoutText, vRow.Evidence, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Scenario: the same check re-checked as a single row. Expected: RecheckRowAsync also returns
    /// the check's own evidence — the board detail sheet must not show the generic string either.
    /// </summary>
    [Fact]
    public async Task RecheckKeepsCheckOwnEvidenceWhenInnerProbeFailsJustUnderBudget()
    {
        const string vOwnEvidence = "Endpoint unreachable at http://10.0.0.4:4723/status.";
        var vCheck = new StubCheck("test.inner-timeout", async aToken =>
        {
            await Task.Delay(TimeSpan.FromMilliseconds(150), aToken).ConfigureAwait(false);
            return CheckResult.Fail(vOwnEvidence);
        });
        var vEngine = new CheckEngine(new Check[] { vCheck });
        var vBoard = vEngine.BuildBoard(MachineRole.AgentHostWsl, null);
        var vRow = Assert.Single(vBoard.Rows);

        var vResult = await vEngine.RecheckRowAsync(vBoard, vRow, TimeSpan.FromMilliseconds(900));

        Assert.Equal(CheckStatus.Fail, vResult.Status);
        Assert.Equal(vOwnEvidence, vResult.Evidence);
    }

    /// <summary>
    /// Scenario: a check that hangs forever and ignores its token, run under the SAME budget as the
    /// evidence tests above. Expected: it still settles as an engine timeout well inside the budget
    /// — widening the budget must not regress the REQ-UI-001 hard bound.
    /// </summary>
    [Fact]
    public async Task SweepStillHardBoundsIndefinitelyHangingCheck()
    {
        var vEngineBudget = TimeSpan.FromMilliseconds(900);
        var vEngine = new CheckEngine(new[] { (Check)StubCheck.Hanging("test.hanging") });
        var vBoard = vEngine.BuildBoard(MachineRole.AgentHostWsl, null);
        var vStopwatch = Stopwatch.StartNew();

        await vEngine.RunDetectSweepAsync(vBoard, vEngineBudget);

        vStopwatch.Stop();
        var vRow = Assert.Single(vBoard.Rows);
        Assert.Equal(CheckStatus.Fail, vRow.Status);
        Assert.Contains(GenericTimeoutText, vRow.Evidence, StringComparison.OrdinalIgnoreCase);
        Assert.True(
            vStopwatch.Elapsed < vEngineBudget * 5,
            $"Hanging check must settle near the {vEngineBudget.TotalMilliseconds} ms budget, took {vStopwatch.Elapsed}.");
    }
}
