using TrSetup.Core.Checks;
using TrSetup.Core.FixAll;
using TrSetup.Core.Fixing;
using TrSetup.Core.Tests.TestDoubles;
using Xunit;

namespace TrSetup.Core.Tests;

/// <summary>
/// REQ-FN-019 — the fix-all dependency-ordered runner: topological execution order on a
/// synthetic graph (Node before Appium, SDK before AVD), declined consent halting the whole
/// run with later steps untouched, continue-vs-stop-on-failure policies, dependent skipping,
/// per-step progress streaming, and cycle rejection.
/// </summary>
public sealed class FixAllRunnerTests
{
    /// <summary>
    /// Scenario: a scrambled plan (appium, avd, node, sdk) where appium depends on node and
    /// avd depends on sdk.
    /// Expect: fixes execute with node before appium and sdk before avd; everything ends Fixed.
    /// </summary>
    [Fact]
    public async Task RunsStepsInDependencyOrder()
    {
        var vFixOrder = new List<string>();
        var vSteps = new List<FixStep>
        {
            new(MakeFixableCheck("win.appium", vFixOrder), new[] { "win.node" }),
            new(MakeFixableCheck("win.avd", vFixOrder), new[] { "win.android-sdk" }),
            new(MakeFixableCheck("win.node", vFixOrder)),
            new(MakeFixableCheck("win.android-sdk", vFixOrder))
        };
        var vRunner = new FixAllRunner(new FixPipeline(new FixAllScriptedConsentProvider()));

        var vResult = await vRunner.RunAsync(vSteps);

        Assert.True(vResult.AllGreen);
        Assert.True(vFixOrder.IndexOf("win.node") < vFixOrder.IndexOf("win.appium"));
        Assert.True(vFixOrder.IndexOf("win.android-sdk") < vFixOrder.IndexOf("win.avd"));
        Assert.All(vResult.Steps, aStep => Assert.Equal(FixAllStepStatus.Fixed, aStep.Status));
    }

    /// <summary>
    /// Scenario: the user declines consent on the middle step of a three-step chain.
    /// Expect: the run halts there — the first step is Fixed, the declined step executed
    /// nothing, and the later step is Skipped with its fixer never invoked (untouched).
    /// </summary>
    [Fact]
    public async Task DeclinedConsentHaltsRunLeavingLaterStepsUntouched()
    {
        var vFixOrder = new List<string>();
        var vSteps = new List<FixStep>
        {
            new(MakeFixableCheck("a.first", vFixOrder)),
            new(MakeFixableCheck("b.declined", vFixOrder), new[] { "a.first" }),
            new(MakeFixableCheck("c.later", vFixOrder), new[] { "b.declined" })
        };
        var vConsent = new FixAllScriptedConsentProvider("b.declined");
        var vRunner = new FixAllRunner(new FixPipeline(vConsent));

        var vResult = await vRunner.RunAsync(vSteps);

        Assert.True(vResult.Halted);
        Assert.Contains("b.declined", vResult.HaltReason);
        Assert.Equal(FixAllStepStatus.Fixed, vResult.Steps[0].Status);
        Assert.Equal(FixAllStepStatus.Declined, vResult.Steps[1].Status);
        Assert.Equal(FixAllStepStatus.Skipped, vResult.Steps[2].Status);
        Assert.Equal(new[] { "a.first" }, vFixOrder);
        Assert.DoesNotContain("c.later", vConsent.RequestedIds);
    }

    /// <summary>
    /// Scenario: the first step's fix never re-verifies green, policy StopOnFailure (default).
    /// Expect: the run halts; the independent second step is Skipped and never executed.
    /// </summary>
    [Fact]
    public async Task StopOnFailureHaltsAtFirstFailedStep()
    {
        var vFixOrder = new List<string>();
        var vSteps = new List<FixStep>
        {
            new(MakeStubbornCheck("a.broken", vFixOrder)),
            new(MakeFixableCheck("b.independent", vFixOrder))
        };
        var vRunner = new FixAllRunner(new FixPipeline(new FixAllScriptedConsentProvider()));

        var vResult = await vRunner.RunAsync(vSteps, FixAllFailurePolicy.StopOnFailure);

        Assert.True(vResult.Halted);
        Assert.Equal(FixAllStepStatus.Failed, vResult.Steps[0].Status);
        Assert.Equal(FixAllStepStatus.Skipped, vResult.Steps[1].Status);
        Assert.Equal(new[] { "a.broken" }, vFixOrder);
    }

    /// <summary>
    /// Scenario: same failing first step, policy ContinueOnFailure, with one step depending
    /// on the failed step and one independent step.
    /// Expect: the run continues — the dependent step is Skipped (dependency failed), the
    /// independent step still runs and is Fixed; the run is not halted.
    /// </summary>
    [Fact]
    public async Task ContinueOnFailureSkipsDependentsAndRunsIndependents()
    {
        var vFixOrder = new List<string>();
        var vSteps = new List<FixStep>
        {
            new(MakeStubbornCheck("a.broken", vFixOrder)),
            new(MakeFixableCheck("b.dependent", vFixOrder), new[] { "a.broken" }),
            new(MakeFixableCheck("c.independent", vFixOrder))
        };
        var vRunner = new FixAllRunner(new FixPipeline(new FixAllScriptedConsentProvider()));

        var vResult = await vRunner.RunAsync(vSteps, FixAllFailurePolicy.ContinueOnFailure);

        Assert.False(vResult.Halted);
        Assert.Equal(FixAllStepStatus.Failed, vResult.Steps[0].Status);
        Assert.Equal(FixAllStepStatus.Skipped, vResult.Steps[1].Status);
        Assert.Contains("a.broken", vResult.Steps[1].Reason);
        Assert.Equal(FixAllStepStatus.Fixed, vResult.Steps[2].Status);
        Assert.DoesNotContain("b.dependent", vFixOrder);
    }

    /// <summary>
    /// Scenario: a two-step plan runs with a progress sink attached.
    /// Expect: each executed step streams Starting then Completed with 1-based numbering,
    /// and the Completed update carries the step result.
    /// </summary>
    [Fact]
    public async Task StreamsPerStepProgress()
    {
        var vFixOrder = new List<string>();
        var vSteps = new List<FixStep>
        {
            new(MakeFixableCheck("a.one", vFixOrder)),
            new(MakeFixableCheck("b.two", vFixOrder), new[] { "a.one" })
        };
        var vUpdates = new List<FixAllStepUpdate>();
        var vRunner = new FixAllRunner(new FixPipeline(new FixAllScriptedConsentProvider()));

        await vRunner.RunAsync(vSteps, FixAllFailurePolicy.StopOnFailure, new SyncProgress(vUpdates));

        Assert.Equal(4, vUpdates.Count);
        Assert.Equal(FixAllStepPhase.Starting, vUpdates[0].Phase);
        Assert.Equal(FixAllStepPhase.Completed, vUpdates[1].Phase);
        Assert.Equal(FixAllStepStatus.Fixed, vUpdates[1].Result!.Status);
        Assert.Equal(1, vUpdates[0].StepNumber);
        Assert.Equal(2, vUpdates[2].StepNumber);
        Assert.Equal(2, vUpdates[3].TotalSteps);
    }

    /// <summary>
    /// Scenario: two steps declare each other as dependencies (a cycle).
    /// Expect: the planner rejects the plan with an InvalidOperationException naming the cycle.
    /// </summary>
    [Fact]
    public async Task CyclicDependenciesAreRejected()
    {
        var vFixOrder = new List<string>();
        var vSteps = new List<FixStep>
        {
            new(MakeFixableCheck("a.chicken", vFixOrder), new[] { "b.egg" }),
            new(MakeFixableCheck("b.egg", vFixOrder), new[] { "a.chicken" })
        };
        var vRunner = new FixAllRunner(new FixPipeline(new FixAllScriptedConsentProvider()));

        var vException = await Assert.ThrowsAsync<InvalidOperationException>(() => vRunner.RunAsync(vSteps));

        Assert.Contains("cycle", vException.Message);
        Assert.Empty(vFixOrder);
    }

    private static FakeCheck MakeFixableCheck(string aId, List<string> aFixOrder)
    {
        var vIsFixed = false;
        return new FakeCheck(
            aId,
            MachineRole.DeviceHostWindows,
            aDetect: _ => Task.FromResult(vIsFixed ? CheckResult.Pass($"{aId} present") : CheckResult.Fail($"{aId} missing")),
            aFix: (_, _) =>
            {
                aFixOrder.Add(aId);
                vIsFixed = true;
                return Task.FromResult(new FixResult(true, $"installed {aId}"));
            });
    }

    private static FakeCheck MakeStubbornCheck(string aId, List<string> aFixOrder)
    {
        return new FakeCheck(
            aId,
            MachineRole.DeviceHostWindows,
            aDetect: _ => Task.FromResult(CheckResult.Fail($"{aId} still broken")),
            aFix: (_, _) =>
            {
                aFixOrder.Add(aId);
                return Task.FromResult(new FixResult(true, $"installer for {aId} claimed success"));
            });
    }

    /// <summary>Synchronous IProgress capture (avoids SynchronizationContext post latency in tests).</summary>
    private sealed class SyncProgress : IProgress<FixAllStepUpdate>
    {
        private readonly List<FixAllStepUpdate> objUpdates;

        public SyncProgress(List<FixAllStepUpdate> aUpdates) => objUpdates = aUpdates;

        public void Report(FixAllStepUpdate aValue) => objUpdates.Add(aValue);
    }
}
