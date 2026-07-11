using System.Text;
using TrSetup.Core.Processes;

namespace TrSetup.Core.Catalog.Probing;

/// <summary>
/// Builds the process requests Windows device-host checks run through. The same request works
/// on native Windows (PowerShell directly) and from WSL (the interop bridge launches
/// <c>powershell.exe</c> on the Windows side): the script travels as a Base64
/// <c>-EncodedCommand</c> so no quoting is lost across either hop.
/// </summary>
public static class WindowsCommandBridge
{
    /// <summary>
    /// Wraps a PowerShell script into a runnable request (non-interactive, no profile).
    /// </summary>
    /// <param name="aScript">The PowerShell script to execute on the Windows side.</param>
    /// <param name="aTimeout">Maximum run time before the process is killed, or <c>null</c> for none.</param>
    /// <returns>The request to hand to the process runner.</returns>
    public static ProcessRunRequest BuildPowerShell(string aScript, TimeSpan? aTimeout = null)
    {
        var vEncoded = Convert.ToBase64String(Encoding.Unicode.GetBytes(aScript));
        return new ProcessRunRequest(
            "powershell.exe",
            $"-NoProfile -NonInteractive -ExecutionPolicy Bypass -EncodedCommand {vEncoded}",
            null,
            aTimeout);
    }

    /// <summary>
    /// Describes how Windows commands are reached from the current platform, for evidence text
    /// (Architecture: detect the platform, never hardcode the hop).
    /// </summary>
    /// <returns><c>native Windows</c> on Windows, otherwise the WSL interop bridge description.</returns>
    public static string Describe() =>
        OperatingSystem.IsWindows() ? "native Windows" : "WSL interop bridge (powershell.exe)";
}
