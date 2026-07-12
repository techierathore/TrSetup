using TrSetup.Core.Catalog;
using TrSetup.Core.Checks;
using TrSetup.Core.Engine;
using TrSetup.Core.Profiles;
using TrSetup.Core.Settings;
using Xunit;

namespace TrSetup.Core.Tests;

/// <summary>
/// REQ-FN-027 — Mac app-runner role aggregation: with roles = <see cref="MachineRole.AppRunnerMac"/>
/// and the AppStudio app selected, the engine enumerates exactly the app's AppRunnerMac-tagged
/// prerequisites (from the resolved profile) plus the culminating Catalyst build fixer — nothing more.
/// </summary>
public sealed class MacAppRunnerAggregationTests : IDisposable
{
    private readonly string objRepoRoot;

    /// <summary>Points the profile repo root at an empty temp dir so no stray override is picked up.</summary>
    public MacAppRunnerAggregationTests()
    {
        objRepoRoot = Path.Combine(Path.GetTempPath(), "trsetup-approot-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(objRepoRoot);
        ProfilePaths.RepoRootOverride = objRepoRoot;
    }

    /// <summary>Restores the profile repo root and deletes the temp dir.</summary>
    public void Dispose()
    {
        ProfilePaths.RepoRootOverride = null;
        Directory.Delete(objRepoRoot, recursive: true);
    }

    /// <summary>
    /// Scenario: an AppRunnerMac machine with AppStudio selected enumerates its board.
    /// Expect: exactly the AppStudio AppRunnerMac prerequisites + <c>appstudio.maccatalyst-build</c>,
    /// and the Catalyst fixer's preview carries the literal build command.
    /// </summary>
    [Fact]
    public void AppRunnerMacBoardEqualsAppPrerequisitesPlusCatalystFixer()
    {
        var vSettings = new TrSetupSettings { Roles = MachineRole.AppRunnerMac, SelectedApp = "AppStudio" };
        var vCatalog = CheckCatalog.CreateAllChecks(
            new FakeProcessRunner(), () => vSettings, new FakeHttpStatusProbe(), new FakeSystemProbe());
        var vEngine = new CheckEngine(vCatalog);

        var vEnumerated = vEngine.EnumerateChecks(MachineRole.AppRunnerMac, "AppStudio");

        var vProfile = new ProfileLoader().Resolve("AppStudio", objRepoRoot)!;
        var vExpected = vProfile.Requirements
            .Where(aReq => (aReq.Roles & MachineRole.AppRunnerMac) != MachineRole.None)
            .Select(aReq => aReq.Id)
            .Append("appstudio.maccatalyst-build")
            .ToHashSet(StringComparer.Ordinal);
        var vActual = vEnumerated.Select(aCheck => aCheck.Id).ToHashSet(StringComparer.Ordinal);
        Assert.Equal(vExpected, vActual);

        var vBuild = vEnumerated.Single(aCheck => aCheck.Id == "appstudio.maccatalyst-build");
        Assert.Contains("dotnet build -f net10.0-maccatalyst -c Release", vBuild.FixPreview);
    }
}
