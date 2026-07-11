namespace TrSetup.Core.Catalog.Probing;

/// <summary>
/// Read-only local-machine probes (files, directories, environment variables) used by detect
/// logic, abstracted behind an interface so unit tests can fake filesystem and environment
/// state without touching the real machine.
/// </summary>
public interface ISystemProbe
{
    /// <summary>The current user's home directory (e.g. <c>/home/user</c>).</summary>
    string HomeDirectory { get; }

    /// <summary>
    /// Reads an environment variable of the current process.
    /// </summary>
    /// <param name="aName">The variable name (e.g. <c>PATH</c>).</param>
    /// <returns>The value, or <c>null</c> when the variable is not set.</returns>
    string? GetEnvironmentVariable(string aName);

    /// <summary>
    /// Whether a file exists at the given path.
    /// </summary>
    /// <param name="aPath">Absolute path to test.</param>
    /// <returns><c>true</c> when the file exists.</returns>
    bool FileExists(string aPath);

    /// <summary>
    /// Whether a directory exists at the given path.
    /// </summary>
    /// <param name="aPath">Absolute path to test.</param>
    /// <returns><c>true</c> when the directory exists.</returns>
    bool DirectoryExists(string aPath);

    /// <summary>
    /// Whether the file at the given path carries an execute permission for the current user
    /// (always <c>true</c> for existing files on Windows, which has no execute bit).
    /// </summary>
    /// <param name="aPath">Absolute path to test.</param>
    /// <returns><c>true</c> when the file exists and is executable.</returns>
    bool IsExecutable(string aPath);

    /// <summary>
    /// Reads a text file, returning <c>null</c> instead of throwing when the file is missing
    /// or unreadable.
    /// </summary>
    /// <param name="aPath">Absolute path to read.</param>
    /// <returns>The file contents, or <c>null</c> when it could not be read.</returns>
    string? TryReadAllText(string aPath);

    /// <summary>
    /// Enumerates the immediate sub-directories of a directory matching a pattern; returns an
    /// empty list when the directory does not exist.
    /// </summary>
    /// <param name="aPath">The directory to enumerate.</param>
    /// <param name="aSearchPattern">Wildcard pattern (e.g. <c>chromium*</c>).</param>
    /// <returns>Full paths of the matching sub-directories.</returns>
    IReadOnlyList<string> EnumerateDirectories(string aPath, string aSearchPattern);
}
