using TrSetup.Core.Checks;
using TrSetup.Core.Fixing;
using TrSetup.Core.Profiles;
using TrSetup.Core.Profiles.Handlers;
using TrSetup.Core.Settings;
using Xunit;

namespace TrSetup.Core.Tests;

/// <summary>
/// Cluster C (P3) heavy profile requirement types — REQ-FN-026 (service: postgres+PgVector /
/// ffmpeg install fixers) and REQ-FN-029 (disk-space floor). Also asserts the registry now
/// resolves all three heavy types and that a heavy requirement missing its discriminator param
/// fails load. Every fixer is exercised against a fake process runner — no live install.
/// </summary>
public sealed class ProfileHeavyHandlerTests
{
    /// <summary>
    /// Scenario: a disk-space requirement whose floor exceeds the reported free space.
    /// Expect: a Warn (never a Fail) carrying both the free and the required GB figures.
    /// </summary>
    [Fact]
    public async Task DiskSpaceBreachWarnsWithFigures()
    {
        var vReq = Req("disk-space", CheckSeverity.Recommended, new() { ["floorGb"] = "80", ["path"] = "/data" });
        var vCheck = new DiskSpaceCheck(vReq, "TrStudio", _ => 1L * DiskSpaceCheck.BytesPerGb);

        var vResult = await vCheck.DetectAsync();

        Assert.Equal(CheckStatus.Warn, vResult.Status);
        Assert.Contains("1.0 GB", vResult.Evidence);
        Assert.Contains("80 GB", vResult.Evidence);
        Assert.Equal(CheckSeverity.Recommended, vCheck.Severity);
    }

    /// <summary>
    /// Scenario: a disk-space requirement whose floor is comfortably below the free space, and one
    /// whose drive cannot be read.
    /// Expect: a Pass with the figures; and a Warn (never Fail) when free space is unknown.
    /// </summary>
    [Fact]
    public async Task DiskSpaceAbovefloorPassesAndUnreadableWarns()
    {
        var vReq = Req("disk-space", CheckSeverity.Recommended, new() { ["floorGb"] = "80", ["path"] = "/data" });
        var vPass = await new DiskSpaceCheck(vReq, "TrStudio", _ => 200L * DiskSpaceCheck.BytesPerGb).DetectAsync();
        var vUnknown = await new DiskSpaceCheck(vReq, "TrStudio", _ => null).DetectAsync();

        Assert.Equal(CheckStatus.Pass, vPass.Status);
        Assert.Contains("200.0 GB", vPass.Evidence);
        Assert.NotEqual(CheckStatus.Fail, vUnknown.Status);
        Assert.Equal(CheckStatus.Warn, vUnknown.Status);
    }

    /// <summary>
    /// Scenario: the postgres service detected present with the vector extension, then absent.
    /// Expect: Pass when psql reports the extension row; Fail (tolerated, with evidence) when psql
    /// is not installed.
    /// </summary>
    [Fact]
    public async Task PostgresDetectsPresentAndAbsent()
    {
        var vRunner = new FakeProcessRunner();
        var vCheck = new ServiceCheck(PostgresReq(), "TrStudio", vRunner, FixerTestSupport.Fix(vRunner), () => false);
        vRunner.Map("psql --version", 0, "psql (PostgreSQL) 16.1");
        vRunner.Map("pg_extension WHERE extname = 'vector'", 0, "1");
        Assert.Equal(CheckStatus.Pass, (await vCheck.DetectAsync()).Status);

        vRunner.Reset();
        var vAbsent = await vCheck.DetectAsync();
        Assert.Equal(CheckStatus.Fail, vAbsent.Status);
        Assert.Contains("PostgreSQL not found", vAbsent.Evidence);
    }

    /// <summary>
    /// Scenario: the postgres fix preview is inspected, then run on a macOS host.
    /// Expect: the preview shows the literal winget + brew installs and the idempotent CREATE
    /// EXTENSION; the mac run shells brew and the CREATE EXTENSION through the process runner.
    /// </summary>
    [Fact]
    public async Task PostgresFixPreviewAndMacRun()
    {
        var vRunner = new FakeProcessRunner();
        var vCheck = new ServiceCheck(PostgresReq(), "TrStudio", vRunner, FixerTestSupport.Fix(vRunner), () => false);
        Assert.Contains("winget install --id PostgreSQL.PostgreSQL", vCheck.FixPreview);
        Assert.Contains("brew install postgresql@16", vCheck.FixPreview);
        Assert.Contains("CREATE EXTENSION IF NOT EXISTS vector;", vCheck.FixPreview);
        vRunner.Map("brew install postgresql", 0, "installed");
        vRunner.Map("CREATE EXTENSION", 0, "CREATE EXTENSION");

        var vFix = await vCheck.FixAsync!(FixerTestSupport.GrantFor(vCheck), CancellationToken.None);

        Assert.True(vFix.FixerReportedSuccess);
        Assert.Contains(vRunner.Invocations, aLine => aLine.Contains("brew install postgresql@16"));
        Assert.Contains(vRunner.Invocations, aLine => aLine.Contains("CREATE EXTENSION IF NOT EXISTS vector"));
    }

    /// <summary>
    /// Scenario: the postgres fix runs on a Windows host.
    /// Expect: the winget install elevates through a visible UAC child (Start-Process -Verb RunAs).
    /// </summary>
    [Fact]
    public async Task PostgresFixWindowsElevatesWinget()
    {
        var vRunner = new FakeProcessRunner();
        var vCheck = new ServiceCheck(PostgresReq(), "TrStudio", vRunner, FixerTestSupport.Fix(vRunner), () => true);
        vRunner.Map("Start-Process", 0, "UAC child completed");
        vRunner.Map("CREATE EXTENSION", 0, "CREATE EXTENSION");

        var vFix = await vCheck.FixAsync!(FixerTestSupport.GrantFor(vCheck), CancellationToken.None);

        Assert.True(vFix.FixerReportedSuccess);
        Assert.Contains(vRunner.Invocations, aLine => aLine.Contains("-Verb RunAs") && aLine.Contains("winget"));
    }

    /// <summary>
    /// Scenario: the ffmpeg service is detected present, then its fix preview inspected.
    /// Expect: Pass on <c>ffmpeg -version</c>; the preview shows the literal winget + brew installs
    /// and no CREATE EXTENSION (ffmpeg has no extension step).
    /// </summary>
    [Fact]
    public async Task FfmpegDetectsAndPreviews()
    {
        var vRunner = new FakeProcessRunner();
        var vReq = Req("service", CheckSeverity.Required, new() { ["service"] = "ffmpeg" });
        var vCheck = new ServiceCheck(vReq, "TrStudio", vRunner, FixerTestSupport.Fix(vRunner), () => false);
        vRunner.Map("ffmpeg -version", 0, "ffmpeg version 6.0");
        Assert.Equal(CheckStatus.Pass, (await vCheck.DetectAsync()).Status);
        Assert.Contains("winget install --id Gyan.FFmpeg", vCheck.FixPreview);
        Assert.Contains("brew install ffmpeg", vCheck.FixPreview);
        Assert.DoesNotContain("CREATE EXTENSION", vCheck.FixPreview);
    }

    /// <summary>
    /// Scenario: the default registry is asked for the three heavy types, and a profile of each is
    /// turned into checks by the factory.
    /// Expect: each type resolves its Cluster C handler and yields the concrete heavy check subclass
    /// (never the failing placeholder).
    /// </summary>
    [Fact]
    public void RegistryResolvesHeavyTypesToConcreteChecks()
    {
        var vRegistry = ProfileRequirementHandlerRegistry.CreateDefault();
        Assert.IsType<ServiceRequirementHandler>(vRegistry.Find(ProfileRequirementTypes.Service));
        Assert.IsType<RuntimeInstallRequirementHandler>(vRegistry.Find(ProfileRequirementTypes.RuntimeInstall));
        Assert.IsType<DiskSpaceRequirementHandler>(vRegistry.Find(ProfileRequirementTypes.DiskSpace));

        const string vJson = """
        { "name": "TrStudio", "requirements": [
          { "type": "service", "id": "s", "title": "Svc", "roles": ["AppRunnerMac"], "params": { "service": "ffmpeg" } },
          { "type": "runtime-install", "id": "r", "title": "Rt", "roles": ["AppRunnerMac"], "params": { "runtime": "comfyui" } },
          { "type": "disk-space", "id": "d", "title": "Disk", "roles": ["AppRunnerMac"], "params": { "floorGb": "80" } } ] }
        """;
        var vChecks = BuildChecks(vJson);

        Assert.Contains(vChecks, aCheck => aCheck is ServiceCheck);
        Assert.Contains(vChecks, aCheck => aCheck is RuntimeInstallCheck);
        Assert.Contains(vChecks, aCheck => aCheck is DiskSpaceCheck);
    }

    /// <summary>
    /// Scenario: heavy requirements each omit their required discriminator param.
    /// Expect: load fails with a validation error naming the missing key (never a silent skip).
    /// </summary>
    [Theory]
    [InlineData("service", "service")]
    [InlineData("runtime-install", "runtime")]
    [InlineData("disk-space", "floorGb")]
    public void HeavyTypeMissingRequiredParamFailsLoad(string aType, string aKey)
    {
        var vJson = $$"""
        { "name": "X", "requirements": [
          { "type": "{{aType}}", "id": "x.1", "title": "T", "roles": ["AppRunnerMac"], "params": {} } ] }
        """;

        var vEx = Assert.Throws<ProfileValidationException>(() => new BuiltInProfiles().RegisterFromJson(vJson));
        Assert.Contains(vEx.Errors, aError => aError.Contains(aKey));
    }

    private static ProfileRequirement PostgresReq()
        => Req("service", CheckSeverity.Required, new() { ["service"] = "postgres", ["port"] = "5432", ["extension"] = "vector" });

    private static ProfileRequirement Req(string aType, CheckSeverity aSeverity, Dictionary<string, string> aParams)
    {
        var vParams = new Dictionary<string, string>(aParams, StringComparer.OrdinalIgnoreCase);
        return new ProfileRequirement(aType, aType + ".id", "Title", MachineRole.AppRunnerMac, aSeverity, vParams);
    }

    private static IReadOnlyList<Check> BuildChecks(string aJson)
    {
        var vProfile = new BuiltInProfiles().RegisterFromJson(aJson);
        var vRunner = new FakeProcessRunner();
        var vContext = new ProfileCheckContext(
            vProfile.Name,
            vRunner,
            CheckFixServices.CreateDefault(vRunner),
            new FakeHttpStatusProbe(),
            new FakeSystemProbe(),
            () => new TrSetupSettings());
        return new ProfileCheckFactory().CreateChecks(vProfile, vContext);
    }
}
