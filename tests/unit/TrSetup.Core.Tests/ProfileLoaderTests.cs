using TrSetup.Core.Checks;
using TrSetup.Core.Profiles;
using Xunit;

namespace TrSetup.Core.Tests;

/// <summary>
/// REQ-FN-021 (BRD-33/34/35) — declarative profile schema validation, built-in + app-repo
/// override merge (app repo wins), and role-tag survival across load and merge.
/// </summary>
public sealed class ProfileLoaderTests
{
    private const string ValidBuiltInJson = """
    {
      "name": "AppStudio",
      "requirements": [
        { "type": "sdk", "id": "shared.sdk", "title": "The .NET SDK",
          "roles": ["DeviceHostWindows"], "severity": "Required",
          "params": { "version": "1" } },
        { "type": "cli-tool", "id": "builtin.node", "title": "Node",
          "roles": ["AppRunnerMac"], "params": { "command": "node" } }
      ]
    }
    """;

    /// <summary>
    /// Scenario: a well-formed profile document is read.
    /// Expect: it loads with its name, requirement count, parsed roles and severity.
    /// </summary>
    [Fact]
    public void ValidProfileLoads()
    {
        var vProfile = ReadProfile(ValidBuiltInJson);

        Assert.Equal("AppStudio", vProfile.Name);
        Assert.Equal(2, vProfile.Requirements.Count);
        var vSdk = vProfile.Requirements[0];
        Assert.Equal(ProfileRequirementTypes.Sdk, vSdk.Type);
        Assert.Equal(MachineRole.DeviceHostWindows, vSdk.Roles);
        Assert.Equal(CheckSeverity.Required, vSdk.Severity);
        Assert.Equal("1", vSdk.Param("version"));
    }

    /// <summary>
    /// Scenario: a document declares an unknown requirement type.
    /// Expect: load fails with a validation error naming the bad type (never a silent skip).
    /// </summary>
    [Fact]
    public void UnknownTypeFailsValidation()
    {
        const string vJson = """
        { "name": "X", "requirements": [
          { "type": "frobnicate", "id": "x.1", "title": "T", "roles": ["AgentHostWsl"] } ] }
        """;

        var vEx = Assert.Throws<ProfileValidationException>(() => ReadProfile(vJson));
        Assert.Contains(vEx.Errors, aError => aError.Contains("frobnicate"));
    }

    /// <summary>
    /// Scenario: a requirement omits a param its type requires (cli-tool without command).
    /// Expect: load fails with a "requires param 'command'" validation error.
    /// </summary>
    [Fact]
    public void MissingRequiredParamFailsValidation()
    {
        const string vJson = """
        { "name": "X", "requirements": [
          { "type": "cli-tool", "id": "x.1", "title": "T", "roles": ["AgentHostWsl"], "params": {} } ] }
        """;

        var vEx = Assert.Throws<ProfileValidationException>(() => ReadProfile(vJson));
        Assert.Contains(vEx.Errors, aError => aError.Contains("command"));
    }

    /// <summary>
    /// Scenario: a requirement lists a role name that is not a known machine role.
    /// Expect: load fails with an "unknown role" validation error.
    /// </summary>
    [Fact]
    public void BadRoleFailsValidation()
    {
        const string vJson = """
        { "name": "X", "requirements": [
          { "type": "sdk", "id": "x.1", "title": "T", "roles": ["NotARealRole"] } ] }
        """;

        var vEx = Assert.Throws<ProfileValidationException>(() => ReadProfile(vJson));
        Assert.Contains(vEx.Errors, aError => aError.Contains("NotARealRole"));
    }

    /// <summary>
    /// Scenario: the app-repo override redefines a built-in id with a new param and adds a new id.
    /// Expect: the overriding requirement REPLACES the built-in one (app repo wins) and the new id
    /// is appended — proving BRD-34's conflict rule.
    /// </summary>
    [Fact]
    public void AppRepoOverrideWinsOnConflict()
    {
        const string vOverrideJson = """
        {
          "name": "AppStudio",
          "requirements": [
            { "type": "sdk", "id": "shared.sdk", "title": "The .NET SDK (repo)",
              "roles": ["AppRunnerMac", "DeviceHostWindows"], "params": { "version": "2" } },
            { "type": "cli-tool", "id": "repo.git", "title": "Git",
              "roles": ["AgentHostWsl"], "params": { "command": "git" } }
          ]
        }
        """;

        var vRepoRoot = WithOverrideFile(vOverrideJson);
        try
        {
            var vLoader = new ProfileLoader(BuiltInsWith(ValidBuiltInJson));
            var vMerged = vLoader.Resolve("AppStudio", vRepoRoot);

            Assert.NotNull(vMerged);
            var vShared = vMerged!.Requirements.Single(aReq => aReq.Id == "shared.sdk");
            Assert.Equal("2", vShared.Param("version"));                        // app repo won
            Assert.Contains(vMerged.Requirements, aReq => aReq.Id == "repo.git"); // new id appended
            Assert.Contains(vMerged.Requirements, aReq => aReq.Id == "builtin.node"); // un-overridden built-in kept
        }
        finally
        {
            ResetOverride(vRepoRoot);
        }
    }

    /// <summary>
    /// Scenario: roles are inspected on a merged requirement.
    /// Expect: the multi-role tag from the overriding requirement survives the load+merge intact.
    /// </summary>
    [Fact]
    public void RoleTaggingSurvivesLoadAndMerge()
    {
        const string vOverrideJson = """
        {
          "name": "AppStudio",
          "requirements": [
            { "type": "sdk", "id": "shared.sdk", "title": "SDK",
              "roles": ["AppRunnerMac", "DeviceHostWindows"], "params": { "version": "2" } }
          ]
        }
        """;

        var vRepoRoot = WithOverrideFile(vOverrideJson);
        try
        {
            var vMerged = new ProfileLoader(BuiltInsWith(ValidBuiltInJson)).Resolve("AppStudio", vRepoRoot);
            var vShared = vMerged!.Requirements.Single(aReq => aReq.Id == "shared.sdk");

            Assert.Equal(MachineRole.AppRunnerMac | MachineRole.DeviceHostWindows, vShared.Roles);
        }
        finally
        {
            ResetOverride(vRepoRoot);
        }
    }

    /// <summary>
    /// Scenario: an app has no built-in profile, only an app-repo file.
    /// Expect: it resolves from the repo file alone — a new app onboards with a JSON file only.
    /// </summary>
    [Fact]
    public void NewAppOnboardsFromRepoFileOnly()
    {
        var vRepoRoot = WithOverrideFile(ValidBuiltInJson);
        try
        {
            var vProfile = new ProfileLoader(new BuiltInProfiles()).Resolve("AppStudio", vRepoRoot);

            Assert.NotNull(vProfile);
            Assert.Equal("AppStudio", vProfile!.Name);
            Assert.Equal(2, vProfile.Requirements.Count);
        }
        finally
        {
            ResetOverride(vRepoRoot);
        }
    }

    private static TrSetupProfile ReadProfile(string aJson)
        => new ProfileLoader(BuiltInsWith(aJson)).Resolve("AppStudio", NonExistentRepoRoot())!;

    private static BuiltInProfiles BuiltInsWith(string aJson)
    {
        var vRegistry = new BuiltInProfiles();
        vRegistry.RegisterFromJson(aJson);
        return vRegistry;
    }

    private static string NonExistentRepoRoot()
        => Path.Combine(Path.GetTempPath(), "trsetup-no-repo-" + Guid.NewGuid().ToString("N"));

    private static string WithOverrideFile(string aJson)
    {
        var vRepoRoot = Path.Combine(Path.GetTempPath(), "trsetup-profile-test-" + Guid.NewGuid().ToString("N"));
        var vTfcore = Path.Combine(vRepoRoot, ".tfcore");
        Directory.CreateDirectory(vTfcore);
        File.WriteAllText(Path.Combine(vTfcore, "trsetup-profile.json"), aJson);
        return vRepoRoot;
    }

    private static void ResetOverride(string aRepoRoot)
    {
        if (Directory.Exists(aRepoRoot))
        {
            Directory.Delete(aRepoRoot, recursive: true);
        }
    }
}
