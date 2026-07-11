using System.Diagnostics;
using System.Text;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace TrSetup.Core.Processes;

/// <summary>
/// Default <see cref="IProcessRunner"/> built on <see cref="Process"/>: redirects stdout and
/// stderr, streams each line live to the caller's progress sink, and returns the exact
/// command line, full output and exit code as the evidence trail.
/// </summary>
public sealed class ProcessRunner : IProcessRunner
{
    private readonly ILogger<ProcessRunner> objLogger;

    /// <summary>
    /// Creates the runner.
    /// </summary>
    /// <param name="aLogger">Optional logger; a null logger is used when omitted.</param>
    public ProcessRunner(ILogger<ProcessRunner>? aLogger = null)
    {
        objLogger = aLogger ?? NullLogger<ProcessRunner>.Instance;
    }

    /// <inheritdoc />
    public async Task<ProcessRunResult> RunAsync(
        ProcessRunRequest aRequest,
        IProgress<string>? aOutputProgress = null,
        CancellationToken aCancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(aRequest);

        var vCommandLine = $"{aRequest.FileName} {aRequest.Arguments}".TrimEnd();
        objLogger.LogDebug("Running: {CommandLine}", vCommandLine);

        using var vProcess = new Process();
        vProcess.StartInfo = BuildStartInfo(aRequest);

        var vStdOut = new StringBuilder();
        var vStdErr = new StringBuilder();
        vProcess.OutputDataReceived += (_, aArgs) => CaptureLine(aArgs.Data, vStdOut, aOutputProgress);
        vProcess.ErrorDataReceived += (_, aArgs) => CaptureLine(aArgs.Data, vStdErr, aOutputProgress);

        var vStopwatch = Stopwatch.StartNew();
        vProcess.Start();
        vProcess.BeginOutputReadLine();
        vProcess.BeginErrorReadLine();

        var vTimedOut = await WaitForExitAsync(vProcess, aRequest.Timeout, aCancellationToken).ConfigureAwait(false);
        vStopwatch.Stop();

        var vExitCode = vTimedOut ? -1 : vProcess.ExitCode;
        return new ProcessRunResult(
            vCommandLine,
            vExitCode,
            vStdOut.ToString(),
            vStdErr.ToString(),
            vTimedOut,
            vStopwatch.Elapsed);
    }

    private static ProcessStartInfo BuildStartInfo(ProcessRunRequest aRequest)
    {
        return new ProcessStartInfo
        {
            FileName = aRequest.FileName,
            Arguments = aRequest.Arguments,
            WorkingDirectory = aRequest.WorkingDirectory ?? string.Empty,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
    }

    private static void CaptureLine(string? aLine, StringBuilder aTarget, IProgress<string>? aOutputProgress)
    {
        if (aLine is null)
        {
            return;
        }

        aTarget.AppendLine(aLine);
        aOutputProgress?.Report(aLine);
    }

    private async Task<bool> WaitForExitAsync(Process aProcess, TimeSpan? aTimeout, CancellationToken aCancellationToken)
    {
        using var vLinkedCts = CancellationTokenSource.CreateLinkedTokenSource(aCancellationToken);
        if (aTimeout is not null)
        {
            vLinkedCts.CancelAfter(aTimeout.Value);
        }

        try
        {
            await aProcess.WaitForExitAsync(vLinkedCts.Token).ConfigureAwait(false);
            return false;
        }
        catch (OperationCanceledException)
        {
            objLogger.LogWarning("Process did not exit in time; killing process tree.");
            TryKill(aProcess);
            aCancellationToken.ThrowIfCancellationRequested();
            return true;
        }
    }

    private static void TryKill(Process aProcess)
    {
        try
        {
            aProcess.Kill(entireProcessTree: true);
        }
        catch (InvalidOperationException)
        {
            // Process already exited between the timeout and the kill — nothing to do.
        }
    }
}
