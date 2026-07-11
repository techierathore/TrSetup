namespace TrSetup.Core.Checks;

/// <summary>
/// The roles a machine can play in the three-environment setup (WSL / Windows host / LAN Mac),
/// expressed as combinable flags so one machine may hold several roles at once.
/// </summary>
/// <remarks>
/// <see cref="NativeDev"/> is a variant flag layered on top of a base role ("I develop natively
/// on this machine"), not a standalone role — it widens the applicable check set.
/// </remarks>
[Flags]
public enum MachineRole
{
    /// <summary>No role selected (first-run state before the role picker has been completed).</summary>
    None = 0,

    /// <summary>WSL distro acting as the agent host (Claude Code / OpenCode, Playwright, winrun bridge).</summary>
    AgentHostWsl = 1,

    /// <summary>Windows host acting as the device host (Android SDK, emulator, Appium, MAUI workload).</summary>
    DeviceHostWindows = 2,

    /// <summary>LAN Mac acting as the device host (Xcode, iOS Simulator, Appium xcuitest/mac2).</summary>
    DeviceHostMac = 4,

    /// <summary>LAN Mac acting as the app runner (builds and runs the selected app locally).</summary>
    AppRunnerMac = 8,

    /// <summary>Native-development variant flag: the owner develops natively on this machine.</summary>
    NativeDev = 16
}
