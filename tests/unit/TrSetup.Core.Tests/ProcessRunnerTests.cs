using TrSetup.Core.Processes;
using Xunit;

namespace TrSetup.Core.Tests;

/// <summary>
/// REQ-FN-003 — the process-runner choke-point: exact command line, stdout/stderr and exit
/// code are captured as the evidence trail, and a long-running command streams its output
/// incrementally through the live progress sink.
/// </summary>
public sealed class ProcessRunnerTests
{
    private static ProcessRunRequest ShellRequest(string aScript, TimeSpan? aTimeout = null)
    {
        return OperatingSystem.IsWindows()
            ? new ProcessRunRequest("cmd.exe", $"/c \"{aScript.Replace(";", "&")}\"", Timeout: aTimeout)
            : new ProcessRunRequest("/bin/bash", $"-c \"{aScript}\"", Timeout: aTimeout);
    }

    /// <summary>
    /// Scenario: run a short shell command printing two stdout lines.
    /// Expect: the result exposes the exact command line, exit code 0, and both lines in
    /// the captured stdout — the full evidence trail.
    /// </summary>
    [Fact]
    public async Task CapturesCommandLineOutputAndExitCode()
    {
        var vRunner = new ProcessRunner();
        var vRequest = ShellRequest("echo alpha; echo beta");

        var vResult = await vRunner.RunAsync(vRequest);

        Assert.Equal($"{vRequest.FileName} {vRequest.Arguments}", vResult.CommandLine);
        Assert.Equal(0, vResult.ExitCode);
        Assert.True(vResult.Succeeded);
        Assert.Contains("alpha", vResult.StandardOutput);
        Assert.Contains("beta", vResult.StandardOutput);
        Assert.Contains(vRequest.FileName, vResult.ToEvidenceString());
    }

    /// <summary>
    /// Scenario: a command writes to stderr and exits non-zero.
    /// Expect: stderr is captured separately, the exit code is preserved, and Succeeded is false.
    /// </summary>
    [Fact]
    public async Task CapturesStderrAndNonZeroExit()
    {
        var vRunner = new ProcessRunner();
        var vScript = OperatingSystem.IsWindows() ? "echo broken 1>&2& exit 3" : "echo broken >&2; exit 3";
        var vRequest = OperatingSystem.IsWindows()
            ? new ProcessRunRequest("cmd.exe", $"/c \"{vScript}\"")
            : new ProcessRunRequest("/bin/bash", $"-c \"{vScript}\"");

        var vResult = await vRunner.RunAsync(vRequest);

        Assert.Equal(3, vResult.ExitCode);
        Assert.False(vResult.Succeeded);
        Assert.Contains("broken", vResult.StandardError);
    }

    /// <summary>
    /// Scenario: a longer-running command prints three lines with pauses between them,
    /// while an IProgress sink collects each line live.
    /// Expect: all three lines arrive through the sink (incremental streaming), spread over
    /// time rather than in one final burst, and also appear in the final captured stdout.
    /// </summary>
    [Fact]
    public async Task StreamsOutputIncrementally()
    {
        var vRunner = new ProcessRunner();
        var vLines = new List<(string Line, DateTimeOffset At)>();
        var vLock = new object();
        var vCollector = new SynchronousProgress(aLine =>
        {
            lock (vLock)
            {
                vLines.Add((aLine, DateTimeOffset.UtcNow));
            }
        });
        var vScript = OperatingSystem.IsWindows()
            ? "echo one& ping -n 2 127.0.0.1 >nul& echo two& ping -n 2 127.0.0.1 >nul& echo three"
            : "echo one; sleep 0.4; echo two; sleep 0.4; echo three";
        var vRequest = OperatingSystem.IsWindows()
            ? new ProcessRunRequest("cmd.exe", $"/c \"{vScript}\"")
            : new ProcessRunRequest("/bin/bash", $"-c \"{vScript}\"");

        var vResult = await vRunner.RunAsync(vRequest, vCollector);

        var vTexts = vLines.Select(aEntry => aEntry.Line).ToList();
        Assert.Contains("one", vTexts);
        Assert.Contains("two", vTexts);
        Assert.Contains("three", vTexts);
        var vSpread = vLines.Max(aEntry => aEntry.At) - vLines.Min(aEntry => aEntry.At);
        Assert.True(vSpread >= TimeSpan.FromMilliseconds(200), $"expected incremental streaming, got spread {vSpread}");
        Assert.Contains("three", vResult.StandardOutput);
    }

    /// <summary>
    /// Scenario: a command that would run far longer than its request timeout.
    /// Expect: the process is killed, TimedOut is true and the exit code is -1.
    /// </summary>
    [Fact]
    public async Task TimeoutKillsTheProcess()
    {
        var vRunner = new ProcessRunner();
        var vRequest = ShellRequest(OperatingSystem.IsWindows() ? "ping -n 30 127.0.0.1 >nul" : "sleep 30",
            aTimeout: TimeSpan.FromMilliseconds(500));

        var vResult = await vRunner.RunAsync(vRequest);

        Assert.True(vResult.TimedOut);
        Assert.Equal(-1, vResult.ExitCode);
        Assert.True(vResult.Duration < TimeSpan.FromSeconds(10));
    }

    /// <summary>
    /// Synchronous IProgress implementation so line timestamps are recorded on the spot
    /// (the built-in Progress&lt;T&gt; posts to a sync context, which can batch callbacks).
    /// </summary>
    private sealed class SynchronousProgress : IProgress<string>
    {
        private readonly Action<string> objHandler;

        public SynchronousProgress(Action<string> aHandler)
        {
            objHandler = aHandler;
        }

        public void Report(string value) => objHandler(value);
    }
}
