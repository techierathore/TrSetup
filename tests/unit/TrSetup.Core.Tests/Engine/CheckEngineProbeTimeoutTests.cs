using System.Diagnostics;
using Xunit;
using TrSetup.Core.Checks;
using TrSetup.Core.Engine;
using TrSetup.Core.Tests.TestDoubles;

namespace TrSetup.Core.Tests.Engine;

/// <summary>
/// REQ-UI-001 engine hang fix: the per-probe timeout must be a HARD bound. A check whose
/// <c>DetectAsync</c> ignores its <see cref="CancellationToken"/> (network probe, stuck
/// subprocess) must still settle its row as a timeout Fail instead of leaving it
/// "Pending" forever.
/// </summary>
public sealed class CheckEngineProbeTimeoutTests
{
    private static readonly TimeSpan TestProbeTimeout = TimeSpan.FromMilliseconds(200);

    /// <summary>
    /// Scenario: a detect sweep over a check that ignores its cancellation token and never
    /// completes. Expected: the sweep itself completes within ~the probe timeout and the row is
    /// settled as Fail with "timed out" evidence — never left un-detected.
    /// </summary>
    [Fact]
    public async Task SweepSettlesTokenIgnoringProbeAsTimeoutFail()
    {
        var vEngine = new CheckEngine(new[] { (Check)StubCheck.Hanging("test.hanging") });
        var vBoard = vEngine.BuildBoard(MachineRole.AgentHostWsl, null);
        var vStopwatch = Stopwatch.StartNew();

        await vEngine.RunDetectSweepAsync(vBoard, TestProbeTimeout);

        vStopwatch.Stop();
        var vRow = Assert.Single(vBoard.Rows);
        Assert.Equal(CheckStatus.Fail, vRow.Status);
        Assert.Contains("timed out", vRow.Evidence, StringComparison.OrdinalIgnoreCase);
        Assert.True(vStopwatch.Elapsed < TimeSpan.FromSeconds(5),
            $"Sweep must settle near the {TestProbeTimeout.TotalMilliseconds} ms budget, took {vStopwatch.Elapsed}.");
    }

    /// <summary>
    /// Scenario: a single-row re-check of a check that ignores its cancellation token and never
    /// completes. Expected: RecheckRowAsync returns a Fail result with "timed out" evidence
    /// within ~the probe timeout.
    /// </summary>
    [Fact]
    public async Task RecheckSettlesTokenIgnoringProbeAsTimeoutFail()
    {
        var vEngine = new CheckEngine(new[] { (Check)StubCheck.Hanging("test.hanging") });
        var vBoard = vEngine.BuildBoard(MachineRole.AgentHostWsl, null);
        var vRow = Assert.Single(vBoard.Rows);

        var vResult = await vEngine.RecheckRowAsync(vBoard, vRow, TestProbeTimeout);

        Assert.Equal(CheckStatus.Fail, vResult.Status);
        Assert.Contains("timed out", vResult.Evidence, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Scenario: a hanging probe next to a fast passing probe in the same sweep. Expected: the
    /// fast row passes untouched while the hanging row settles as the timeout Fail — one bad
    /// probe never poisons the rest of the board.
    /// </summary>
    [Fact]
    public async Task SweepKeepsFastRowsWhileTimingOutHangingRow()
    {
        var vChecks = new Check[]
        {
            StubCheck.Hanging("test.hanging"),
            new StubCheck("test.fast", _ => Task.FromResult(CheckResult.Pass("ok")))
        };
        var vEngine = new CheckEngine(vChecks);
        var vBoard = vEngine.BuildBoard(MachineRole.AgentHostWsl, null);

        await vEngine.RunDetectSweepAsync(vBoard, TestProbeTimeout);

        var vRows = vBoard.Rows.ToList();
        Assert.Equal(CheckStatus.Fail, vRows.Single(aRow => aRow.Check.Id == "test.hanging").Status);
        Assert.Equal(CheckStatus.Pass, vRows.Single(aRow => aRow.Check.Id == "test.fast").Status);
    }

    /// <summary>
    /// Scenario: a well-behaved probe that completes quickly. Expected: the hard bound does not
    /// alter the happy path — the row settles with the probe's own result and evidence.
    /// </summary>
    [Fact]
    public async Task SweepKeepsWellBehavedProbeResultIntact()
    {
        var vEngine = new CheckEngine(
            new Check[] { new StubCheck("test.pass", _ => Task.FromResult(CheckResult.Pass("all good"))) });
        var vBoard = vEngine.BuildBoard(MachineRole.AgentHostWsl, null);

        await vEngine.RunDetectSweepAsync(vBoard, TestProbeTimeout);

        var vRow = Assert.Single(vBoard.Rows);
        Assert.Equal(CheckStatus.Pass, vRow.Status);
        Assert.Equal("all good", vRow.Evidence);
    }
}
