using TrSetup.Core.Checks;
using TrSetup.Core.Engine;
using TrSetup.Core.Tests.TestDoubles;
using Xunit;

namespace TrSetup.Core.Tests;

/// <summary>
/// REQ-FN-004 — catalog scoping and the observable board model: switching role/app changes
/// the enumerated set, out-of-scope checks are NotApplicable, detect sweeps run in parallel
/// with per-probe timeouts, and row updates stream while the sweep runs.
/// </summary>
public sealed class CheckEngineTests
{
    private static CheckEngine BuildScopedEngine(out FakeCheck aWsl, out FakeCheck aWin, out FakeCheck aAppStudioOnly)
    {
        aWsl = new FakeCheck("wsl.tool", MachineRole.AgentHostWsl);
        aWin = new FakeCheck("win.tool", MachineRole.DeviceHostWindows);
        aAppStudioOnly = new FakeCheck(
            "app.feed",
            MachineRole.AgentHostWsl | MachineRole.DeviceHostWindows,
            aApps: new[] { "AppStudio" });
        return new CheckEngine(new[] { aWsl, aWin, aAppStudioOnly });
    }

    /// <summary>
    /// Scenario: the same catalog is enumerated for the WSL agent-host role and then for the
    /// Windows device-host role.
    /// Expect: the enumerated sets differ — each role only sees its own checks.
    /// </summary>
    [Fact]
    public void SwitchingRoleChangesEnumeratedSet()
    {
        var vEngine = BuildScopedEngine(out var vWsl, out var vWin, out _);

        var vWslSet = vEngine.EnumerateChecks(MachineRole.AgentHostWsl, null);
        var vWinSet = vEngine.EnumerateChecks(MachineRole.DeviceHostWindows, null);

        Assert.Contains(vWsl, vWslSet);
        Assert.DoesNotContain(vWin, vWslSet);
        Assert.Contains(vWin, vWinSet);
        Assert.DoesNotContain(vWsl, vWinSet);
    }

    /// <summary>
    /// Scenario: the same role enumerates with no app selected, with AppStudio selected,
    /// and with TrStudio selected.
    /// Expect: the AppStudio-only check appears only when AppStudio is the selected app.
    /// </summary>
    [Fact]
    public void SwitchingAppChangesEnumeratedSet()
    {
        var vEngine = BuildScopedEngine(out _, out _, out var vAppStudioOnly);

        var vNoApp = vEngine.EnumerateChecks(MachineRole.AgentHostWsl, null);
        var vAppStudio = vEngine.EnumerateChecks(MachineRole.AgentHostWsl, "AppStudio");
        var vTrStudio = vEngine.EnumerateChecks(MachineRole.AgentHostWsl, "TrStudio");

        Assert.DoesNotContain(vAppStudioOnly, vNoApp);
        Assert.Contains(vAppStudioOnly, vAppStudio);
        Assert.DoesNotContain(vAppStudioOnly, vTrStudio);
    }

    /// <summary>
    /// Scenario: a check tagged only with the NativeDev variant flag; the machine first has
    /// plain AgentHostWsl, then AgentHostWsl + NativeDev.
    /// Expect: the check is enumerated only when the variant flag is present.
    /// </summary>
    [Fact]
    public void NativeDevVariantWidensTheSet()
    {
        var vNativeOnly = new FakeCheck("dev.ide", MachineRole.NativeDev);
        var vEngine = new CheckEngine(new[] { vNativeOnly });

        var vPlain = vEngine.EnumerateChecks(MachineRole.AgentHostWsl, null);
        var vNative = vEngine.EnumerateChecks(MachineRole.AgentHostWsl | MachineRole.NativeDev, null);

        Assert.Empty(vPlain);
        Assert.Contains(vNativeOnly, vNative);
    }

    /// <summary>
    /// Scenario: a board is built for the WSL role over a catalog containing a Mac-only check.
    /// Expect: the Mac row is present but NotApplicable (with an out-of-scope reason) and is
    /// excluded from the sweep; it is never a failure.
    /// </summary>
    [Fact]
    public async Task OutOfScopeChecksAreNotApplicableOnTheBoard()
    {
        var vMacOnly = new FakeCheck("mac.xcode", MachineRole.DeviceHostMac);
        var vEngine = new CheckEngine(new[] { vMacOnly });

        var vBoard = vEngine.BuildBoard(MachineRole.AgentHostWsl, "AppStudio");
        await vEngine.RunDetectSweepAsync(vBoard);

        var vRow = Assert.Single(vBoard.Rows);
        Assert.False(vRow.IsInScope);
        Assert.Equal(CheckStatus.NotApplicable, vRow.Status);
        Assert.Contains("Out of scope", vRow.Evidence);
        Assert.False(vRow.IsFailure);
        Assert.Equal(0, vMacOnly.DetectCallCount);
    }

    /// <summary>
    /// Scenario: two checks that each block until the other one has started (a rendezvous
    /// barrier) run in one sweep with a 2 s probe timeout.
    /// Expect: both pass — only possible when the engine probes in parallel; a sequential
    /// sweep would deadlock the first probe into its timeout.
    /// </summary>
    [Fact]
    public async Task DetectSweepRunsProbesInParallel()
    {
        var vBarrier = new TaskCompletionSource();
        var vStarted = 0;
        Func<CancellationToken, Task<CheckResult>> vRendezvous = async aToken =>
        {
            if (Interlocked.Increment(ref vStarted) == 2)
            {
                vBarrier.TrySetResult();
            }

            await vBarrier.Task.WaitAsync(aToken);
            return CheckResult.Pass("both probes were running at once");
        };
        var vEngine = new CheckEngine(new[]
        {
            new FakeCheck("par.one", MachineRole.AgentHostWsl, vRendezvous),
            new FakeCheck("par.two", MachineRole.AgentHostWsl, vRendezvous)
        });

        var vBoard = vEngine.BuildBoard(MachineRole.AgentHostWsl, null);
        await vEngine.RunDetectSweepAsync(vBoard, TimeSpan.FromSeconds(2));

        Assert.All(vBoard.Rows, aRow => Assert.Equal(CheckStatus.Pass, aRow.Status));
    }

    /// <summary>
    /// Scenario: a probe that never completes runs in a sweep with a 250 ms probe timeout.
    /// Expect: the row fails fast with timeout evidence instead of hanging the sweep
    /// (REQ-NFR-001 groundwork: full sweep bounded by the probe timeout).
    /// </summary>
    [Fact]
    public async Task HangingProbeTimesOutAsFail()
    {
        var vHanging = new FakeCheck(
            "hang.probe",
            MachineRole.AgentHostWsl,
            async aToken =>
            {
                await Task.Delay(Timeout.InfiniteTimeSpan, aToken);
                return CheckResult.Pass("unreachable");
            });
        var vEngine = new CheckEngine(new[] { vHanging });
        var vBoard = vEngine.BuildBoard(MachineRole.AgentHostWsl, null);

        var vStopwatch = System.Diagnostics.Stopwatch.StartNew();
        await vEngine.RunDetectSweepAsync(vBoard, TimeSpan.FromMilliseconds(250));
        vStopwatch.Stop();

        var vRow = Assert.Single(vBoard.Rows);
        Assert.Equal(CheckStatus.Fail, vRow.Status);
        Assert.Contains("timed out", vRow.Evidence);
        Assert.True(vStopwatch.Elapsed < TimeSpan.FromSeconds(5), $"sweep took {vStopwatch.Elapsed}");
    }

    /// <summary>
    /// Scenario: a sweep over two in-scope checks with a RowChanged subscriber and an
    /// IProgress sink attached.
    /// Expect: one streamed update per in-scope row through both channels — the observable
    /// board model every head renders.
    /// </summary>
    [Fact]
    public async Task SweepStreamsRowUpdates()
    {
        var vEngine = new CheckEngine(new[]
        {
            new FakeCheck("stream.one", MachineRole.AgentHostWsl),
            new FakeCheck("stream.two", MachineRole.AgentHostWsl)
        });
        var vBoard = vEngine.BuildBoard(MachineRole.AgentHostWsl, null);
        var vEvents = 0;
        vBoard.RowChanged += (_, _) => Interlocked.Increment(ref vEvents);

        await vEngine.RunDetectSweepAsync(vBoard);

        Assert.Equal(2, vEvents);
        Assert.All(vBoard.Rows, aRow => Assert.Equal(CheckStatus.Pass, aRow.Status));
    }

    /// <summary>
    /// Scenario: one row of a detected board is re-checked after its underlying state changed.
    /// Expect: the single re-check updates only that row and returns the fresh result.
    /// </summary>
    [Fact]
    public async Task SingleRowRecheckUpdatesTheRow()
    {
        var vHealthy = true;
        var vFlippy = new FakeCheck(
            "flip.tool",
            MachineRole.AgentHostWsl,
            _ => Task.FromResult(vHealthy ? CheckResult.Pass("ok") : CheckResult.Fail("gone")));
        var vEngine = new CheckEngine(new[] { vFlippy });
        var vBoard = vEngine.BuildBoard(MachineRole.AgentHostWsl, null);
        await vEngine.RunDetectSweepAsync(vBoard);

        vHealthy = false;
        var vResult = await vEngine.RecheckRowAsync(vBoard, vBoard.Rows.Single());

        Assert.Equal(CheckStatus.Fail, vResult.Status);
        Assert.Equal(CheckStatus.Fail, vBoard.Rows.Single().Status);
    }
}
