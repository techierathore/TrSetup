namespace TrSetup.Core.Processes;

/// <summary>
/// Describes one command to execute through the process runner choke-point.
/// </summary>
/// <param name="FileName">The executable to start (e.g. <c>dotnet</c>, <c>bash</c>, <c>cmd.exe</c>).</param>
/// <param name="Arguments">The argument string passed verbatim to the executable.</param>
/// <param name="WorkingDirectory">Working directory for the process, or <c>null</c> for the current directory.</param>
/// <param name="Timeout">Maximum run time before the process is killed, or <c>null</c> for no timeout.</param>
public sealed record ProcessRunRequest(
    string FileName,
    string Arguments,
    string? WorkingDirectory = null,
    TimeSpan? Timeout = null);
