using TrSetup.Core.Checks;
using TrSetup.Core.Fixing;
using TrSetup.Core.Profiles;
using TrSetup.Core.Settings;
using Xunit;

namespace TrSetup.Core.Tests;

/// <summary>
/// REQ-FN-021 — the requirement→check factory: a presence requirement becomes a working
/// <see cref="Check"/> that probes through the process/http/system collaborators; an unregistered
/// type yields a graceful failing placeholder; and profile checks scope to their app via
/// <see cref="Check.AppliesTo"/>.
/// </summary>
public sealed class ProfileCheckFactoryTests
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
    /// Scenario: a cli-tool requirement is turned into a check and detected against a runner that
    /// reports the tool present.
    /// Expect: the check passes with the tool's version in the evidence.
    /// </summary>
    [Fact]
    public async Task CliToolRequirementDetectsPresentTool()
    {
        var vRunner = new FakeProcessRunner();
        vRunner.Map("node --version", 0, "v22.11.0");
        var vCheck = BuildChecks(AppStudioJson, vRunner).Single();

        var vResult = await vCheck.DetectAsync();

        Assert.Equal(CheckStatus.Pass, vResult.Status);
        Assert.Contains("v22.11.0", vResult.Evidence);
    }

    /// <summary>
    /// Scenario: the same cli-tool requirement is detected against a runner where the tool is absent.
    /// Expect: the check fails (command-not-found becomes real detect evidence, not a crash).
    /// </summary>
    [Fact]
    public async Task CliToolRequirementFailsWhenToolMissing()
    {
        var vCheck = BuildChecks(AppStudioJson, new FakeProcessRunner()).Single();

        var vResult = await vCheck.DetectAsync();

        Assert.Equal(CheckStatus.Fail, vResult.Status);
    }

    /// <summary>
    /// Scenario: a factory built against a registry with no handler for a requirement's type builds
    /// that requirement (every known type now has a handler, so the placeholder path is driven with
    /// an empty registry rather than a real-but-unregistered type).
    /// Expect: the factory produces a placeholder check that fails with the "no handler" evidence —
    /// an unhandled type is visible on the board, never a crash.
    /// </summary>
    [Fact]
    public async Task UnknownHandlerTypeYieldsPlaceholder()
    {
        const string vJson = """
        { "name": "AppStudio", "requirements": [
          { "type": "sdk", "id": "appstudio.sdk", "title": "The SDK", "roles": ["AppRunnerMac"] } ] }
        """;
        var vProfile = new BuiltInProfiles().RegisterFromJson(vJson);
        var vRunner = new FakeProcessRunner();
        var vContext = new ProfileCheckContext(
            vProfile.Name,
            vRunner,
            CheckFixServices.CreateDefault(vRunner),
            new FakeHttpStatusProbe(),
            new FakeSystemProbe(),
            () => new TrSetupSettings());
        var vCheck = new ProfileCheckFactory(new ProfileRequirementHandlerRegistry())
            .CreateChecks(vProfile, vContext).Single();

        var vResult = await vCheck.DetectAsync();

        Assert.Equal(CheckStatus.Fail, vResult.Status);
        Assert.Contains("No handler registered for requirement type 'sdk'", vResult.Evidence);
    }

    /// <summary>
    /// Scenario: an AppStudio profile check's scoping is evaluated for several (roles, app) pairs.
    /// Expect: NotApplicable when no app / another app is selected, or when the machine lacks the
    /// requirement's role; in scope only when AppStudio is selected AND the role matches.
    /// </summary>
    [Fact]
    public void ProfileCheckScopesToItsApp()
    {
        var vCheck = BuildChecks(AppStudioJson, new FakeProcessRunner()).Single();

        Assert.False(vCheck.AppliesTo(MachineRole.AppRunnerMac, null));         // no app selected
        Assert.False(vCheck.AppliesTo(MachineRole.AppRunnerMac, "OtherApp"));    // different app
        Assert.False(vCheck.AppliesTo(MachineRole.DeviceHostWindows, "AppStudio")); // role mismatch
        Assert.True(vCheck.AppliesTo(MachineRole.AppRunnerMac, "AppStudio"));    // app + role match
    }

    private static IReadOnlyList<Check> BuildChecks(string aJson, FakeProcessRunner aRunner)
    {
        var vProfile = new BuiltInProfiles().RegisterFromJson(aJson);
        var vContext = new ProfileCheckContext(
            vProfile.Name,
            aRunner,
            CheckFixServices.CreateDefault(aRunner),
            new FakeHttpStatusProbe(),
            new FakeSystemProbe(),
            () => new TrSetupSettings());
        return new ProfileCheckFactory().CreateChecks(vProfile, vContext);
    }
}
