using TrSetup.Core.Checks;
using TrSetup.Core.Settings;
using Xunit;

namespace TrSetup.Core.Tests;

/// <summary>
/// REQ-FN-005 — settings persistence: save/reload restores roles, selected app and
/// endpoints; a missing file reports first-run (which drives the role picker); the store
/// path is overridable for tests and the default path is per-OS.
/// </summary>
public sealed class SettingsStoreTests : IDisposable
{
    private readonly string objTempDirectory;

    /// <summary>
    /// Creates an isolated temp directory for each test's settings file.
    /// </summary>
    public SettingsStoreTests()
    {
        objTempDirectory = Path.Combine(Path.GetTempPath(), $"trsetup-tests-{Guid.NewGuid():N}");
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (Directory.Exists(objTempDirectory))
        {
            Directory.Delete(objTempDirectory, recursive: true);
        }
    }

    private string SettingsPath => Path.Combine(objTempDirectory, "settings.json");

    /// <summary>
    /// Scenario: load from a path where no settings file exists.
    /// Expect: IsFirstRun is true with default (role-less) settings — the trigger for the
    /// first-run role picker.
    /// </summary>
    [Fact]
    public async Task MissingFileReportsFirstRun()
    {
        var vStore = new JsonSettingsStore(SettingsPath);

        var vResult = await vStore.LoadAsync();

        Assert.True(vResult.IsFirstRun);
        Assert.Equal(MachineRole.None, vResult.Settings.Roles);
        Assert.Null(vResult.Settings.SelectedApp);
    }

    /// <summary>
    /// Scenario: save roles (WSL + NativeDev), the selected app and a MacIp endpoint, then
    /// reload through a brand-new store instance (a fresh app start).
    /// Expect: every selection is restored and IsFirstRun is false.
    /// </summary>
    [Fact]
    public async Task SaveThenReloadRestoresSelections()
    {
        var vSettings = new TrSetupSettings
        {
            Roles = MachineRole.AgentHostWsl | MachineRole.NativeDev,
            SelectedApp = "AppStudio",
            Endpoints = { ["MacIp"] = "192.168.1.50" }
        };
        await new JsonSettingsStore(SettingsPath).SaveAsync(vSettings);

        var vReloaded = await new JsonSettingsStore(SettingsPath).LoadAsync();

        Assert.False(vReloaded.IsFirstRun);
        Assert.Equal(MachineRole.AgentHostWsl | MachineRole.NativeDev, vReloaded.Settings.Roles);
        Assert.Equal("AppStudio", vReloaded.Settings.SelectedApp);
        Assert.Equal("192.168.1.50", vReloaded.Settings.Endpoints["MacIp"]);
    }

    /// <summary>
    /// Scenario: save into a directory that does not exist yet.
    /// Expect: the store creates the directory and the file lands on disk.
    /// </summary>
    [Fact]
    public async Task SaveCreatesTheSettingsDirectory()
    {
        var vStore = new JsonSettingsStore(SettingsPath);

        await vStore.SaveAsync(new TrSetupSettings { Roles = MachineRole.DeviceHostWindows });

        Assert.True(File.Exists(SettingsPath));
    }

    /// <summary>
    /// Scenario: query the per-OS default settings path.
    /// Expect: it ends in settings.json under a TrSetup-owned folder (%APPDATA%\TrSetup on
    /// Windows, ~/.trsetup elsewhere).
    /// </summary>
    [Fact]
    public void DefaultPathIsPerOs()
    {
        var vPath = JsonSettingsStore.GetDefaultSettingsPath();

        Assert.EndsWith("settings.json", vPath);
        var vExpectedFolder = OperatingSystem.IsWindows() ? "TrSetup" : ".trsetup";
        Assert.Contains(vExpectedFolder, vPath);
    }
}
