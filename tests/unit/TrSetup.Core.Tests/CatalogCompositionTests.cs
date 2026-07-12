using TrSetup.Core.Catalog;
using TrSetup.Core.Checks;
using TrSetup.Core.Settings;
using Xunit;

namespace TrSetup.Core.Tests;

/// <summary>
/// REQ-FN-006..009 / REQ-FN-024 — catalog composition: the full built-in list matches the BRD §9
/// F-WSLCHK / F-WINCHK / F-MACCHK tables plus the REQ-FN-024 appium-config-block framework extra
/// (27 rows), ids are unique, every row carries the correct machine-role flags for its table,
/// categories are the board groups in board order, and every row has a populated explanation.
/// </summary>
public sealed class CatalogCompositionTests
{
    private static readonly string[] ExpectedIds =
    {
        "wsl.dotnet-sdk", "wsl.chromium-libs", "wsl.winrun", "wsl.node", "wsl.playwright",
        "wsl.mirrored-networking", "wsl.git",
        "win.wslconfig-mirrored", "win.android-sdk", "win.api34-image", "win.avd-pixel-api34",
        "win.node", "win.appium-uiautomator2", "win.verify-helper", "win.appium-session",
        "win.maui-workload", "win.jdk",
        "mac.xcode-clt", "mac.dotnet-maui", "mac.node", "mac.appium-drivers",
        "mac.appium-launchagent", "mac.stable-ip", "mac.ios-simulator",
        "framework.appium-config-block",
        "wsl.appium-windows", "wsl.appium-mac"
    };

    private static IReadOnlyList<Check> CreateCatalog() =>
        CheckCatalog.CreateAllChecks(
            new FakeProcessRunner(),
            () => new TrSetupSettings(),
            new FakeHttpStatusProbe(),
            new FakeSystemProbe());

    /// <summary>
    /// Scenario: the full catalog is created.
    /// Expect: exactly the 26 BRD §9 rows, in board order, with unique ids.
    /// </summary>
    [Fact]
    public void CatalogMatchesBrdTables()
    {
        var vCatalog = CreateCatalog();

        Assert.Equal(ExpectedIds, vCatalog.Select(aCheck => aCheck.Id).ToArray());
        Assert.Equal(ExpectedIds.Length, vCatalog.Select(aCheck => aCheck.Id).Distinct().Count());
    }

    /// <summary>
    /// Scenario: role flags are inspected per id prefix.
    /// Expect: wsl.* and the framework.* appium-config extra carry AgentHostWsl, win.* rows
    /// DeviceHostWindows, mac.* rows DeviceHostMac — the role-scoping sanity for enumeration.
    /// </summary>
    [Fact]
    public void RowsCarryTheirTableRoleFlags()
    {
        var vCatalog = CreateCatalog();

        foreach (var vCheck in vCatalog)
        {
            var vExpected = vCheck.Id.Split('.')[0] switch
            {
                "wsl" => MachineRole.AgentHostWsl,
                "framework" => MachineRole.AgentHostWsl,
                "win" => MachineRole.DeviceHostWindows,
                _ => MachineRole.DeviceHostMac
            };
            Assert.Equal(vExpected, vCheck.Roles);
        }
    }

    /// <summary>
    /// Scenario: categories of all rows are inspected.
    /// Expect: only the board groups "Framework core" and "Bridges (cross-machine)" occur;
    /// the Bridges group holds exactly the two cross-machine Appium probes; and every
    /// Framework core row precedes every Bridges row (board order).
    /// </summary>
    [Fact]
    public void CategoriesAreBoardGroupsInBoardOrder()
    {
        var vCatalog = CreateCatalog();

        Assert.All(vCatalog, aCheck => Assert.Contains(
            aCheck.Category, new[] { BoardCategories.FrameworkCore, BoardCategories.Bridges }));
        var vBridgeIds = vCatalog
            .Where(aCheck => aCheck.Category == BoardCategories.Bridges)
            .Select(aCheck => aCheck.Id)
            .ToList();
        Assert.Equal(new[] { "wsl.appium-windows", "wsl.appium-mac" }, vBridgeIds);
        var vLastCore = vCatalog.ToList().FindLastIndex(aCheck => aCheck.Category == BoardCategories.FrameworkCore);
        var vFirstBridge = vCatalog.ToList().FindIndex(aCheck => aCheck.Category == BoardCategories.Bridges);
        Assert.True(vLastCore < vFirstBridge);
    }

    /// <summary>
    /// Scenario: every catalog row's explanation is read.
    /// Expect: What and Why are always populated — the detail pane never renders blank.
    /// </summary>
    [Fact]
    public void EveryRowHasPopulatedExplanation()
    {
        var vCatalog = CreateCatalog();

        Assert.All(vCatalog, aCheck =>
        {
            Assert.False(string.IsNullOrWhiteSpace(aCheck.Explain.What));
            Assert.False(string.IsNullOrWhiteSpace(aCheck.Explain.Why));
            Assert.False(string.IsNullOrWhiteSpace(aCheck.Title));
        });
    }

    /// <summary>
    /// Scenario: P2 attaches auto-fixers (REQ-FN-014..016). Exactly the BRD §9 manual rows —
    /// the WSL-side mirrored-networking view and the three cross-machine reachability probes and
    /// the Mac DHCP-reservation stable-IP row — stay manual-only (no Fix button); every other
    /// row now carries both a literal FixPreview and a FixAsync fixer.
    /// </summary>
    [Fact]
    public void ManualRowsStayFixableRowsGetFixers()
    {
        var vCatalog = CreateCatalog();
        var vExpectedManual = new[]
        {
            "wsl.mirrored-networking", "wsl.appium-windows", "wsl.appium-mac", "mac.stable-ip"
        };

        var vManual = vCatalog.Where(aCheck => aCheck.IsManualOnly).Select(aCheck => aCheck.Id).OrderBy(aId => aId);
        Assert.Equal(vExpectedManual.OrderBy(aId => aId), vManual);
        Assert.All(
            vCatalog.Where(aCheck => !aCheck.IsManualOnly),
            aCheck =>
            {
                Assert.NotNull(aCheck.FixAsync);
                Assert.False(string.IsNullOrWhiteSpace(aCheck.FixPreview));
            });
    }
}
