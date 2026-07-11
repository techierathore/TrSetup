namespace TrSetup.Core.Elevation;

/// <summary>
/// One command that needs elevation (REQ-FN-020). The exact command line is what the user
/// sees in the consent preview — nothing else is ever executed on their behalf.
/// </summary>
/// <param name="FileName">The executable to elevate (e.g. <c>msiexec</c>, <c>apt-get</c>).</param>
/// <param name="Arguments">The argument string, verbatim.</param>
/// <param name="Description">One human sentence describing what the elevated command does.</param>
public sealed record ElevatedCommand(string FileName, string Arguments, string Description)
{
    /// <summary>The exact command line shown to the user and executed — file name plus arguments.</summary>
    public string CommandLine => $"{FileName} {Arguments}".TrimEnd();
}
