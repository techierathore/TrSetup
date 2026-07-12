using TrSetup.Core.Processes;

namespace TrSetup.Core.Tests;

/// <summary>
/// Fake process choke-point for elevation tests: never launches anything, records every
/// request it receives, and returns a scripted successful result.
/// </summary>
public sealed class ElevationFakeProcessRunner : IProcessRunner
{
    /// <summary>Every request handed to the runner, in order.</summary>
    public List<ProcessRunRequest> Requests { get; } = new();

    /// <inheritdoc />
    public Task<ProcessRunResult> RunAsync(
        ProcessRunRequest aRequest,
        IProgress<string>? aOutputProgress = null,
        CancellationToken aCancellationToken = default)
    {
        Requests.Add(aRequest);
        var vCommandLine = $"{aRequest.FileName} {aRequest.Arguments}".TrimEnd();
        return Task.FromResult(new ProcessRunResult(vCommandLine, 0, string.Empty, string.Empty, false, TimeSpan.Zero));
    }
}
