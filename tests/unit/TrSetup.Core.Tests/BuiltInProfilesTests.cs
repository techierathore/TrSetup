using TrSetup.Core.Checks;
using TrSetup.Core.Profiles;
using Xunit;

namespace TrSetup.Core.Tests;

/// <summary>
/// REQ-FN-022 / REQ-FN-023 — the AppStudio and TrStudio built-in profile JSON files ship embedded
/// under Profiles/BuiltIn and are auto-discovered by <see cref="BuiltInProfiles.CreateDefault"/>.
/// These tests pin the exact requirement id/type/role set of each profile (so the board renders the
/// BRD §9 F-PROFILES rows), prove both profiles load without a schema-validation failure, and prove
/// role tagging filters a Mac-runner-only requirement out of a Windows-only scope.
/// </summary>
public sealed class BuiltInProfilesTests
{
    private const MachineRole Win = MachineRole.DeviceHostWindows;
    private const MachineRole MacRunner = MachineRole.AppRunnerMac;
    private const MachineRole WinAndMac = MachineRole.DeviceHostWindows | MachineRole.AppRunnerMac;

    /// <summary>
    /// Scenario: the production registry is built by auto-discovering embedded built-in JSON.
    /// Expect: it resolves an AppStudio profile whose eight requirements match the BRD rows exactly
    /// (id, type and role tags), with no <see cref="ProfileValidationException"/>.
    /// </summary>
    [Fact]
    public void AppStudioProfileMatchesBrdRows()
    {
        var vProfile = LoadProfile("AppStudio");

        var vExpected = new (string Id, string Type, MachineRole Roles)[]
        {
            ("appstudio.dotnet-sdk", ProfileRequirementTypes.Sdk, WinAndMac),
            ("appstudio.maui-workload", ProfileRequirementTypes.Workload, WinAndMac),
            ("appstudio.dotnet-cli", ProfileRequirementTypes.CliTool, WinAndMac),
            ("appstudio.git-cli", ProfileRequirementTypes.CliTool, WinAndMac),
            ("appstudio.xcode", ProfileRequirementTypes.CliTool, MacRunner),
            ("appstudio.github-packages-feed", ProfileRequirementTypes.NugetFeed, WinAndMac),
            ("appstudio.appmanager-api", ProfileRequirementTypes.Endpoint, WinAndMac),
            ("appstudio.appmanager-secret", ProfileRequirementTypes.EnvSecret, WinAndMac)
        };

        AssertRequirements(vProfile, vExpected);
    }

    /// <summary>
    /// Scenario: the production registry is built by auto-discovering embedded built-in JSON.
    /// Expect: it resolves a TrStudio profile whose nine requirements match the BRD rows exactly
    /// (id, type and role tags, incl. the Win-only feed and the Mac-runner-only disk floor), with no
    /// <see cref="ProfileValidationException"/>.
    /// </summary>
    [Fact]
    public void TrStudioProfileMatchesBrdRows()
    {
        var vProfile = LoadProfile("TrStudio");

        var vExpected = new (string Id, string Type, MachineRole Roles)[]
        {
            ("trstudio.dotnet-sdk", ProfileRequirementTypes.Sdk, WinAndMac),
            ("trstudio.postgres", ProfileRequirementTypes.Service, WinAndMac),
            ("trstudio.ffmpeg", ProfileRequirementTypes.Service, WinAndMac),
            ("trstudio.comfyui", ProfileRequirementTypes.RuntimeInstall, WinAndMac),
            ("trstudio.disk-space", ProfileRequirementTypes.DiskSpace, MacRunner),
            ("trstudio.techierag-feed", ProfileRequirementTypes.NugetFeed, Win),
            ("trstudio.runpod-key", ProfileRequirementTypes.EnvSecret, WinAndMac),
            ("trstudio.heygen-key", ProfileRequirementTypes.EnvSecret, WinAndMac),
            ("trstudio.appmanager-key", ProfileRequirementTypes.EnvSecret, WinAndMac),
            ("trstudio.appmanager-endpoint", ProfileRequirementTypes.Endpoint, WinAndMac)
        };

        AssertRequirements(vProfile, vExpected);
    }

    /// <summary>
    /// Scenario: the TrStudio disk-space requirement is a warn-level item.
    /// Expect: its severity is Recommended (BRD marks the model-storage floor as warn-level).
    /// </summary>
    [Fact]
    public void TrStudioDiskSpaceIsRecommended()
    {
        var vProfile = LoadProfile("TrStudio");
        var vDisk = vProfile.Requirements.Single(aReq => aReq.Id == "trstudio.disk-space");

        Assert.Equal(CheckSeverity.Recommended, vDisk.Severity);
    }

    /// <summary>
    /// Scenario: the AppStudio Xcode requirement is tagged Mac-runner only and a Windows-only scope
    /// is inspected.
    /// Expect: Xcode's role set does not intersect a Windows-only role — so it is not applicable to a
    /// Windows device host, while a Win+Mac row (dotnet) is.
    /// </summary>
    [Fact]
    public void MacOnlyRequirementIsNotApplicableToWindowsScope()
    {
        var vProfile = LoadProfile("AppStudio");
        var vXcode = vProfile.Requirements.Single(aReq => aReq.Id == "appstudio.xcode");
        var vDotnet = vProfile.Requirements.Single(aReq => aReq.Id == "appstudio.dotnet-cli");

        Assert.False(vXcode.Roles.HasFlag(MachineRole.DeviceHostWindows));
        Assert.Equal(MachineRole.None, vXcode.Roles & MachineRole.DeviceHostWindows);
        Assert.True(vDotnet.Roles.HasFlag(MachineRole.DeviceHostWindows));
    }

    private static TrSetupProfile LoadProfile(string aName)
    {
        var vProfile = BuiltInProfiles.CreateDefault().Find(aName);
        Assert.NotNull(vProfile);
        return vProfile!;
    }

    private static void AssertRequirements(
        TrSetupProfile aProfile,
        IReadOnlyList<(string Id, string Type, MachineRole Roles)> aExpected)
    {
        Assert.Equal(aExpected.Count, aProfile.Requirements.Count);
        foreach (var vExpected in aExpected)
        {
            var vRequirement = aProfile.Requirements.Single(aReq => aReq.Id == vExpected.Id);
            Assert.Equal(vExpected.Type, vRequirement.Type);
            Assert.Equal(vExpected.Roles, vRequirement.Roles);
            Assert.False(string.IsNullOrWhiteSpace(vRequirement.Title));
        }
    }
}
