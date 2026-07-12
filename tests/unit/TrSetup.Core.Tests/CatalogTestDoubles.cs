using System.Text;
using TrSetup.Core.Catalog.Probing;
using TrSetup.Core.Downloads;
using TrSetup.Core.Processes;

namespace TrSetup.Core.Tests;

/// <summary>
/// Fake <see cref="IInstallerDownloader"/> for fixer tests: returns a canned
/// <see cref="DownloadResult"/> (verified by default) without touching the network, and records
/// every requested URL so a fixer's pinned source can be asserted.
/// </summary>
internal sealed class FakeInstallerDownloader : IInstallerDownloader
{
    private readonly DownloadOutcome objOutcome;

    /// <summary>
    /// Creates the fake.
    /// </summary>
    /// <param name="aOutcome">The outcome to report for every download (defaults to Verified).</param>
    public FakeInstallerDownloader(DownloadOutcome aOutcome = DownloadOutcome.Verified)
    {
        objOutcome = aOutcome;
    }

    /// <summary>Every pinned URL the fixer asked to download, in order.</summary>
    public List<string> RequestedUrls { get; } = new();

    /// <inheritdoc />
    public Task<DownloadResult> DownloadAsync(
        DownloadRequest aRequest,
        IProgress<string>? aProgress = null,
        CancellationToken aCancellationToken = default)
    {
        RequestedUrls.Add(aRequest.Url);
        var vKept = objOutcome is DownloadOutcome.Verified or DownloadOutcome.NoPublishedChecksum;
        var vPath = vKept
            ? Path.Combine(Path.GetTempPath(), aRequest.ToolName, aRequest.FileName)
            : null;
        return Task.FromResult(new DownloadResult(objOutcome, vPath, $"[fake download] {aRequest.Url} → {objOutcome}"));
    }
}

/// <summary>
/// Fake <see cref="IProcessRunner"/> for catalog tests: maps a substring of the command line
/// (including the decoded PowerShell <c>-EncodedCommand</c> script, so Windows-bridge checks
/// can be matched on script content) to a canned result. Unmapped commands read as
/// "command not found" (exit 127).
/// </summary>
internal sealed class FakeProcessRunner : IProcessRunner
{
    private readonly List<(string Key, int ExitCode, string StdOut, string StdErr)> objMappings = new();

    /// <summary>The last request passed to <see cref="RunAsync"/>, for asserting how a check probed.</summary>
    public ProcessRunRequest? LastRequest { get; private set; }

    /// <summary>Every command line (with decoded script) handed to the runner, in order.</summary>
    public List<string> Invocations { get; } = new();

    /// <summary>
    /// Maps any command whose command line (or decoded script) contains the key.
    /// </summary>
    /// <param name="aKey">Substring to match.</param>
    /// <param name="aExitCode">Exit code to report.</param>
    /// <param name="aStandardOutput">Canned stdout.</param>
    /// <param name="aStandardError">Canned stderr.</param>
    public void Map(string aKey, int aExitCode, string aStandardOutput, string aStandardError = "")
        => objMappings.Add((aKey, aExitCode, aStandardOutput, aStandardError));

    /// <summary>Clears all mappings so a check can be re-pointed from "broken" to "installed" between detect passes.</summary>
    public void Reset() => objMappings.Clear();

    /// <inheritdoc />
    public Task<ProcessRunResult> RunAsync(
        ProcessRunRequest aRequest,
        IProgress<string>? aOutputProgress = null,
        CancellationToken aCancellationToken = default)
    {
        LastRequest = aRequest;
        var vCommandLine = $"{aRequest.FileName} {aRequest.Arguments}".TrimEnd();
        var vHaystack = $"{vCommandLine} {DecodeEncodedCommand(aRequest.Arguments)}";
        Invocations.Add(vHaystack);
        foreach (var vMapping in objMappings)
        {
            if (vHaystack.Contains(vMapping.Key, StringComparison.OrdinalIgnoreCase))
            {
                return Task.FromResult(new ProcessRunResult(
                    vCommandLine, vMapping.ExitCode, vMapping.StdOut, vMapping.StdErr, false, TimeSpan.Zero));
            }
        }

        return Task.FromResult(new ProcessRunResult(
            vCommandLine, 127, string.Empty, $"{aRequest.FileName}: command not found", false, TimeSpan.Zero));
    }

    private static string DecodeEncodedCommand(string aArguments)
    {
        const string EncodedMarker = "-EncodedCommand ";
        var vIndex = aArguments.IndexOf(EncodedMarker, StringComparison.OrdinalIgnoreCase);
        if (vIndex < 0)
        {
            return string.Empty;
        }

        var vBase64 = aArguments[(vIndex + EncodedMarker.Length)..].Trim();
        return Encoding.Unicode.GetString(Convert.FromBase64String(vBase64));
    }
}

/// <summary>
/// Fake <see cref="ISystemProbe"/> for catalog tests: fully in-memory filesystem and
/// environment state.
/// </summary>
internal sealed class FakeSystemProbe : ISystemProbe
{
    /// <summary>Files that exist.</summary>
    public HashSet<string> Files { get; } = new(StringComparer.Ordinal);

    /// <summary>Files that carry the execute bit.</summary>
    public HashSet<string> ExecutableFiles { get; } = new(StringComparer.Ordinal);

    /// <summary>Readable file contents by path.</summary>
    public Dictionary<string, string> FileContents { get; } = new(StringComparer.Ordinal);

    /// <summary>Existing directories mapped to their immediate sub-directory names.</summary>
    public Dictionary<string, List<string>> Directories { get; } = new(StringComparer.Ordinal);

    /// <summary>Environment variables by name.</summary>
    public Dictionary<string, string> EnvironmentVariables { get; } = new(StringComparer.Ordinal);

    /// <inheritdoc />
    public string HomeDirectory { get; set; } = "/home/tester";

    /// <inheritdoc />
    public string? GetEnvironmentVariable(string aName)
        => EnvironmentVariables.TryGetValue(aName, out var vValue) ? vValue : null;

    /// <inheritdoc />
    public bool FileExists(string aPath) => Files.Contains(aPath) || FileContents.ContainsKey(aPath);

    /// <inheritdoc />
    public bool DirectoryExists(string aPath) => Directories.ContainsKey(aPath);

    /// <inheritdoc />
    public bool IsExecutable(string aPath) => ExecutableFiles.Contains(aPath);

    /// <inheritdoc />
    public string? TryReadAllText(string aPath)
        => FileContents.TryGetValue(aPath, out var vContent) ? vContent : null;

    /// <inheritdoc />
    public IReadOnlyList<string> EnumerateDirectories(string aPath, string aSearchPattern)
    {
        if (!Directories.TryGetValue(aPath, out var vChildren))
        {
            return Array.Empty<string>();
        }

        var vPrefix = aSearchPattern.TrimEnd('*');
        return vChildren
            .Where(aName => aName.StartsWith(vPrefix, StringComparison.OrdinalIgnoreCase))
            .Select(aName => Path.Combine(aPath, aName))
            .ToList();
    }
}

/// <summary>
/// Fake <see cref="IHttpStatusProbe"/> for catalog tests: URL → canned probe result;
/// unmapped URLs read as connection-refused.
/// </summary>
internal sealed class FakeHttpStatusProbe : IHttpStatusProbe
{
    /// <summary>Canned responses by exact URL.</summary>
    public Dictionary<string, HttpProbeResult> Responses { get; } = new(StringComparer.OrdinalIgnoreCase);

    /// <inheritdoc />
    public Task<HttpProbeResult> GetAsync(string aUrl, CancellationToken aCancellationToken = default)
    {
        if (Responses.TryGetValue(aUrl, out var vResult))
        {
            return Task.FromResult(vResult);
        }

        return Task.FromResult(new HttpProbeResult(
            false, null, string.Empty, "HttpRequestException: Connection refused"));
    }
}

/// <summary>
/// Fake <see cref="HttpMessageHandler"/> so <see cref="HttpStatusProbe"/> itself can be
/// driven with canned HTTP responses or transport exceptions.
/// </summary>
internal sealed class FakeHttpMessageHandler : HttpMessageHandler
{
    private readonly Func<HttpRequestMessage, HttpResponseMessage> objResponder;

    /// <summary>
    /// Creates the handler.
    /// </summary>
    /// <param name="aResponder">Builds (or throws for) the response per request.</param>
    public FakeHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> aResponder)
    {
        objResponder = aResponder;
    }

    /// <inheritdoc />
    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage aRequest,
        CancellationToken aCancellationToken)
        => Task.FromResult(objResponder(aRequest));
}
