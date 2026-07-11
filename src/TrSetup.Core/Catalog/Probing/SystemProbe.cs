namespace TrSetup.Core.Catalog.Probing;

/// <summary>
/// Default <see cref="ISystemProbe"/> over the real filesystem and process environment.
/// All members are exception-safe: probe failures read as "not present", never throw.
/// </summary>
public sealed class SystemProbe : ISystemProbe
{
    /// <inheritdoc />
    public string HomeDirectory =>
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

    /// <inheritdoc />
    public string? GetEnvironmentVariable(string aName) =>
        Environment.GetEnvironmentVariable(aName);

    /// <inheritdoc />
    public bool FileExists(string aPath) => File.Exists(aPath);

    /// <inheritdoc />
    public bool DirectoryExists(string aPath) => Directory.Exists(aPath);

    /// <inheritdoc />
    public bool IsExecutable(string aPath)
    {
        if (!File.Exists(aPath))
        {
            return false;
        }

        if (OperatingSystem.IsWindows())
        {
            return true;
        }

        try
        {
            return File.GetUnixFileMode(aPath).HasFlag(UnixFileMode.UserExecute);
        }
        catch (IOException)
        {
            return false;
        }
    }

    /// <inheritdoc />
    public string? TryReadAllText(string aPath)
    {
        try
        {
            return File.Exists(aPath) ? File.ReadAllText(aPath) : null;
        }
        catch (IOException)
        {
            return null;
        }
        catch (UnauthorizedAccessException)
        {
            return null;
        }
    }

    /// <inheritdoc />
    public IReadOnlyList<string> EnumerateDirectories(string aPath, string aSearchPattern)
    {
        try
        {
            return Directory.Exists(aPath)
                ? Directory.GetDirectories(aPath, aSearchPattern)
                : Array.Empty<string>();
        }
        catch (IOException)
        {
            return Array.Empty<string>();
        }
        catch (UnauthorizedAccessException)
        {
            return Array.Empty<string>();
        }
    }
}
