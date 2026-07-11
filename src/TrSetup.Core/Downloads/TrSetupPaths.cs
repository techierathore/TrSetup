namespace TrSetup.Core.Downloads;

/// <summary>
/// The TrSetup-managed filesystem root (REQ-FN-017 / REQ-NFR-004). Everything TrSetup
/// downloads or installs lands under this root so it never collides with system installs —
/// Windows: <c>%LOCALAPPDATA%\TrSetup</c>, Linux/macOS: <c>~/.trsetup</c>.
/// </summary>
public static class TrSetupPaths
{
    /// <summary>
    /// Test/override hook: when set, <see cref="ManagedRoot"/> returns this path instead of
    /// the platform default. Set to <c>null</c> to restore the default.
    /// </summary>
    public static string? RootOverride { get; set; }

    /// <summary>
    /// The TrSetup-managed root directory: <see cref="RootOverride"/> when set, otherwise
    /// <c>%LOCALAPPDATA%\TrSetup</c> on Windows or <c>~/.trsetup</c> on Linux/macOS.
    /// </summary>
    public static string ManagedRoot => RootOverride ?? DefaultManagedRoot();

    /// <summary>
    /// Where downloaded tools and installers are placed: <c>{ManagedRoot}/tools</c>.
    /// Installs under here can never clobber a system-wide install.
    /// </summary>
    public static string ToolsRoot => Path.Combine(ManagedRoot, "tools");

    private static string DefaultManagedRoot()
    {
        if (OperatingSystem.IsWindows())
        {
            var vLocalAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            return Path.Combine(vLocalAppData, "TrSetup");
        }

        var vHome = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        return Path.Combine(vHome, ".trsetup");
    }
}
