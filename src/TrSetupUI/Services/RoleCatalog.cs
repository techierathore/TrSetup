using TrSetup.Core.Checks;

namespace TrSetupUI.Services;

/// <summary>One selectable machine role: flag value plus the display texts the UI renders.</summary>
/// <param name="Role">The <see cref="MachineRole"/> flag this option represents.</param>
/// <param name="Key">Stable kebab-case key used for element ids / test ids.</param>
/// <param name="Title">Short display title (e.g. "Agent host (WSL)").</param>
/// <param name="Description">One-line explanation shown on the role picker card.</param>
/// <param name="Icon">Lucide icon name shown next to the role.</param>
public sealed record RoleOption(MachineRole Role, string Key, string Title, string Description, string Icon);

/// <summary>
/// The fixed role and app-profile vocabularies the UI renders (BRD §5): the four selectable
/// machine roles (native-dev is a variant switch, not a role card) and the app profiles.
/// </summary>
public static class RoleCatalog
{
    /// <summary>Sentinel app-select value meaning "Framework only" (no app profile selected).</summary>
    public const string FrameworkOnlyValue = "framework-only";

    /// <summary>The four selectable machine roles, in picker order.</summary>
    public static IReadOnlyList<RoleOption> Roles { get; } =
    [
        new RoleOption(
            MachineRole.AgentHostWsl,
            "agent-host-wsl",
            "Agent host (WSL)",
            "Runs the AI agent + verification stack — .NET SDK, Playwright, headless Chromium, the winrun bridge.",
            "terminal"),
        new RoleOption(
            MachineRole.DeviceHostWindows,
            "device-host-windows",
            "Device host (Windows)",
            "Hosts the Android emulator + Appium (uiautomator2) so agents can verify apps on a device.",
            "monitor"),
        new RoleOption(
            MachineRole.DeviceHostMac,
            "device-host-mac",
            "Device host (Mac)",
            "Serves Appium with xcuitest + mac2 drivers on the LAN for iOS / Catalyst verification.",
            "laptop"),
        new RoleOption(
            MachineRole.AppRunnerMac,
            "app-runner-mac",
            "App runner (Mac)",
            "Prepares the Mac to build and run the portfolio apps — Catalyst builds, SDK + workloads, app services.",
            "rocket")
    ];

    /// <summary>The selectable app profiles; <see cref="FrameworkOnlyValue"/> maps to no app.</summary>
    public static IReadOnlyList<(string Value, string Label)> Apps { get; } =
    [
        (FrameworkOnlyValue, "Framework only"),
        ("AppStudio", "AppStudio"),
        ("TrStudio", "TrStudio")
    ];

    /// <summary>
    /// Finds the option for a single role flag.
    /// </summary>
    /// <param name="aRole">The role flag to look up.</param>
    /// <returns>The matching option, or <c>null</c> for unknown/variant flags.</returns>
    public static RoleOption? Find(MachineRole aRole)
        => Roles.FirstOrDefault(aOption => aOption.Role == aRole);

    /// <summary>
    /// Expands a combined role flags value into the selected options, in picker order.
    /// </summary>
    /// <param name="aRoles">The combined machine roles.</param>
    /// <returns>The options whose flag is present in <paramref name="aRoles"/>.</returns>
    public static IReadOnlyList<RoleOption> Expand(MachineRole aRoles)
        => Roles.Where(aOption => aRoles.HasFlag(aOption.Role)).ToList();
}
