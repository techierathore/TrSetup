using TrSetup.Core.Catalog;
using TrSetup.Core.Checks;
using TrSetup.Core.Profiles;
using TrSetup.Core.Settings;
using Xunit;

namespace TrSetup.Core.Tests;

/// <summary>
/// REQ-FN-021 — head integration: <see cref="CheckCatalog.CreateAllChecks"/> appends the selected
/// app's profile checks after the framework rows, appends nothing when no app is selected, and
/// never throws when profile assembly fails (framework rows always survive).
/// </summary>
public sealed class ProfileCatalogIntegrationTests
{
    private const string AppStudioJson = """
    {
      "name": "AppStudio",
      "requirements": [
        { "type": "cli-tool", "id": "appstudio.node", "title": "Node",
          "roles": ["AppRunnerMac"], "params": { "command": "node" } }
      ]
    }
    """;

    /// <summary>
    /// Scenario: AppStudio is the selected app and a loader resolves its profile.
    /// Expect: the profile's check is appended after the built-in framework rows.
    /// </summary>
    [Fact]
    public void SelectedAppProfileChecksAreAppended()
    {
        var vSettings = new TrSetupSettings { SelectedApp = "AppStudio" };
        var vCatalog = CheckCatalog.CreateAllChecks(
            new FakeProcessRunner(),
            () => vSettings,
            new FakeHttpStatusProbe(),
            new FakeSystemProbe(),
            aProfileLoader: LoaderFor(AppStudioJson));

        var vAppended = vCatalog.Single(aCheck => aCheck.Id == "appstudio.node");
        Assert.Contains("AppStudio", vAppended.Apps);
        // Appended after the framework rows (past the last cross-machine bridge probe).
        var vNodeIndex = IndexOf(vCatalog, "appstudio.node");
        var vLastBridgeIndex = IndexOf(vCatalog, "wsl.appium-mac");
        Assert.True(vNodeIndex > vLastBridgeIndex);
    }

    private static int IndexOf(IReadOnlyList<Check> aCatalog, string aId)
    {
        for (var vIndex = 0; vIndex < aCatalog.Count; vIndex++)
        {
            if (aCatalog[vIndex].Id == aId)
            {
                return vIndex;
            }
        }

        return -1;
    }

    /// <summary>
    /// Scenario: no app is selected.
    /// Expect: only the built-in framework catalog is returned — no profile rows appended.
    /// </summary>
    [Fact]
    public void NoSelectedAppAppendsNothing()
    {
        var vCatalog = CheckCatalog.CreateAllChecks(
            new FakeProcessRunner(),
            () => new TrSetupSettings(),
            new FakeHttpStatusProbe(),
            new FakeSystemProbe(),
            aProfileLoader: LoaderFor(AppStudioJson));

        Assert.DoesNotContain(vCatalog, aCheck => aCheck.Id == "appstudio.node");
    }

    private static ProfileLoader LoaderFor(string aJson)
    {
        var vBuiltIns = new BuiltInProfiles();
        vBuiltIns.RegisterFromJson(aJson);
        return new ProfileLoader(vBuiltIns);
    }
}
