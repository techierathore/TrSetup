using System.Diagnostics;
using Xunit;
using TrSetup.Core.Catalog;
using TrSetup.Core.Checks;
using TrSetup.Core.Tests.TestDoubles;

namespace TrSetup.Core.Tests.Catalog;

/// <summary>
/// REQ-FN-028 gate-detect budget fix: the Catalyst build gate re-detects its prerequisites in
/// parallel with a hard per-prerequisite bound, counts a timed-out or throwing prerequisite as
/// RED with an honest "not confirmed green" suffix, and reports red ids in catalog order.
/// </summary>
public sealed class CheckCatalogGateDetectTests
{
    private static readonly TimeSpan TestPrereqTimeout = TimeSpan.FromMilliseconds(200);

    /// <summary>
    /// Scenario: one prerequisite hangs (ignores its token, never completes) among fast green
    /// ones. Expected: the red-ids detect returns within ~the per-prerequisite bound (not
    /// unboundedly) and reports the hanging prereq red with the "not confirmed green: timed out"
    /// suffix, never assuming it green.
    /// </summary>
    [Fact]
    public async Task HangingPrerequisiteCountsAsRedWithinBound()
    {
        var vPrereqs = new List<Check>
        {
            new StubCheck("app.fast-green", _ => Task.FromResult(CheckResult.Pass("ok"))),
            StubCheck.Hanging("app.hanging-feed")
        };
        var vStopwatch = Stopwatch.StartNew();

        var vReds = await CheckCatalog.DetectRedIdsAsync(vPrereqs, CancellationToken.None, TestPrereqTimeout);

        vStopwatch.Stop();
        var vRed = Assert.Single(vReds);
        Assert.StartsWith("app.hanging-feed (not confirmed green: timed out after", vRed);
        Assert.True(vStopwatch.Elapsed < TimeSpan.FromSeconds(5),
            $"Gate detect must settle near the {TestPrereqTimeout.TotalMilliseconds} ms bound, took {vStopwatch.Elapsed}.");
    }

    /// <summary>
    /// Scenario: a mix of fast Pass and fast Fail prerequisites, no hangs. Expected: exactly the
    /// Fail ids come back, plain (no suffix), in catalog order.
    /// </summary>
    [Fact]
    public async Task MixedFastPrerequisitesReportExactlyTheFailIds()
    {
        var vPrereqs = new List<Check>
        {
            new StubCheck("app.red-one", _ => Task.FromResult(CheckResult.Fail("missing"))),
            new StubCheck("app.green", _ => Task.FromResult(CheckResult.Pass("ok"))),
            new StubCheck("app.red-two", _ => Task.FromResult(CheckResult.Fail("misconfigured"))),
            new StubCheck("app.warn", _ => Task.FromResult(CheckResult.Warn("degraded")))
        };

        var vReds = await CheckCatalog.DetectRedIdsAsync(vPrereqs, CancellationToken.None, TestPrereqTimeout);

        Assert.Equal(new[] { "app.red-one", "app.red-two" }, vReds);
    }

    /// <summary>
    /// Scenario: a prerequisite whose detect throws. Expected: it counts as red (gate stays
    /// closed) with an honest "not confirmed green: probe threw" suffix instead of crashing the
    /// gate detect or being assumed green.
    /// </summary>
    [Fact]
    public async Task ThrowingPrerequisiteCountsAsRedWithHonestSuffix()
    {
        var vPrereqs = new List<Check>
        {
            new StubCheck("app.throws", _ => Task.FromException<CheckResult>(new InvalidOperationException("boom"))),
            new StubCheck("app.green", _ => Task.FromResult(CheckResult.Pass("ok")))
        };

        var vReds = await CheckCatalog.DetectRedIdsAsync(vPrereqs, CancellationToken.None, TestPrereqTimeout);

        var vRed = Assert.Single(vReds);
        Assert.Equal("app.throws (not confirmed green: probe threw InvalidOperationException)", vRed);
    }

    /// <summary>
    /// Scenario: red ids must be deterministic regardless of probe completion order — a slow-ish
    /// red finishing after a fast red must still be reported in catalog order.
    /// </summary>
    [Fact]
    public async Task RedIdsKeepCatalogOrderRegardlessOfCompletionOrder()
    {
        var vPrereqs = new List<Check>
        {
            new StubCheck("app.slow-red", async aToken =>
            {
                await Task.Delay(50, aToken);
                return CheckResult.Fail("slow but red");
            }),
            new StubCheck("app.fast-red", _ => Task.FromResult(CheckResult.Fail("instantly red")))
        };

        var vReds = await CheckCatalog.DetectRedIdsAsync(vPrereqs, CancellationToken.None, TestPrereqTimeout);

        Assert.Equal(new[] { "app.slow-red", "app.fast-red" }, vReds);
    }

    /// <summary>
    /// Scenario: all prerequisites green and fast. Expected: an empty red set — the gate opens.
    /// </summary>
    [Fact]
    public async Task AllGreenPrerequisitesReportNoReds()
    {
        var vPrereqs = new List<Check>
        {
            new StubCheck("app.green-one", _ => Task.FromResult(CheckResult.Pass("ok"))),
            new StubCheck("app.green-two", _ => Task.FromResult(CheckResult.Pass("ok")))
        };

        var vReds = await CheckCatalog.DetectRedIdsAsync(vPrereqs, CancellationToken.None, TestPrereqTimeout);

        Assert.Empty(vReds);
    }

    /// <summary>
    /// Scenario: the production wiring uses the named constant. Expected: the per-prerequisite
    /// budget stays comfortably inside the engine's 5 s row budget so the gate row itself never
    /// becomes a generic row timeout.
    /// </summary>
    [Fact]
    public void PrerequisiteBudgetStaysInsideEngineRowBudget()
    {
        Assert.True(CheckCatalog.PrerequisiteProbeTimeout < TrSetup.Core.Engine.CheckEngine.DefaultProbeTimeout,
            "The per-prerequisite budget must be smaller than the engine's per-row probe timeout.");
    }
}
