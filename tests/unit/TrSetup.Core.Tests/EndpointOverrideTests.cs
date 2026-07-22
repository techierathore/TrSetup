using TrSetup.Core.Catalog.Probing;
using TrSetup.Core.Checks;
using TrSetup.Core.Fixing;
using TrSetup.Core.Profiles;
using TrSetup.Core.Settings;
using Xunit;

namespace TrSetup.Core.Tests;

/// <summary>
/// REQ-FN-028 (BRD-42) — the per-machine App Manager endpoint override. The AppStudio profile
/// declares <c>https://localhost:5101/</c> for a requirement scoped to BOTH
/// <c>DeviceHostWindows</c> and <c>AppRunnerMac</c>; on a real two-machine setup the Mac
/// app-runner has nothing on its own localhost, so the check could never go green. These tests pin
/// the override resolution, the evidence provenance, and the opt-in-only TLS trust.
/// </summary>
public sealed class EndpointOverrideTests
{
    private const string DefaultUrl = "https://localhost:5101/";
    private const string LanUrl = "https://192.168.1.14:5101/";
    private const string SettingKey = "AppManagerUrl";

    private const string AppManagerJson = """
    {
      "name": "AppStudio",
      "requirements": [
        { "type": "endpoint", "id": "appstudio.appmanager-api", "title": "App Manager API reachable",
          "roles": ["DeviceHostWindows", "AppRunnerMac"],
          "params": { "url": "https://localhost:5101/", "urlSettingKey": "AppManagerUrl" } }
      ]
    }
    """;

    /// <summary>
    /// Scenario: no override is configured for the named key.
    /// Expect: the profile's own default URL is used, full TLS validation, and the source names the
    /// settings key so the user can discover the override exists.
    /// </summary>
    [Fact]
    public void ResolveFallsBackToProfileDefaultWhenNoOverrideConfigured()
    {
        var vResolved = EndpointResolver.Resolve(DefaultUrl, SettingKey, new TrSetupSettings());

        Assert.Equal(DefaultUrl, vResolved.Url);
        Assert.False(vResolved.IsOverridden);
        Assert.False(vResolved.AllowSelfSignedCertificate);
        Assert.Contains(SettingKey, vResolved.Source);
    }

    /// <summary>
    /// Scenario: the machine configures the App Manager endpoint at a LAN address.
    /// Expect: the configured URL REPLACES the profile default and is reported as overridden.
    /// </summary>
    [Fact]
    public void ResolveUsesConfiguredOverrideWhenSet()
    {
        var vSettings = new TrSetupSettings { Endpoints = { [SettingKey] = LanUrl } };

        var vResolved = EndpointResolver.Resolve(DefaultUrl, SettingKey, vSettings);

        Assert.Equal(LanUrl, vResolved.Url);
        Assert.True(vResolved.IsOverridden);
        Assert.Contains(SettingKey, vResolved.Source);
    }

    /// <summary>
    /// Scenario: a requirement declares no <c>urlSettingKey</c> at all.
    /// Expect: the URL is fixed by the profile and no settings value can redirect it.
    /// </summary>
    [Fact]
    public void ResolveIgnoresSettingsWhenRequirementDeclaresNoKey()
    {
        var vSettings = new TrSetupSettings { Endpoints = { [SettingKey] = LanUrl } };

        var vResolved = EndpointResolver.Resolve(DefaultUrl, null, vSettings);

        Assert.Equal(DefaultUrl, vResolved.Url);
        Assert.False(vResolved.IsOverridden);
        Assert.False(vResolved.AllowSelfSignedCertificate);
    }

    /// <summary>
    /// Scenario: the user ticked "trust self-signed certificate" for the key AND configured a URL.
    /// Expect: trust is granted — this is the ONLY combination that relaxes validation.
    /// </summary>
    [Fact]
    public void ResolveGrantsTlsTrustOnlyWhenExplicitlyOptedInForAnOverriddenUrl()
    {
        var vOptedIn = new TrSetupSettings
        {
            Endpoints = { [SettingKey] = LanUrl },
            TrustedSelfSignedEndpoints = { SettingKey }
        };

        Assert.True(EndpointResolver.Resolve(DefaultUrl, SettingKey, vOptedIn).AllowSelfSignedCertificate);
    }

    /// <summary>
    /// Scenario: the user ticked trust for the key but configured NO URL, so the built-in profile
    /// default would be probed.
    /// Expect: trust is NOT granted — a profile's own default endpoint is always fully validated,
    /// so a stale opt-in can never silently weaken a built-in probe.
    /// </summary>
    [Fact]
    public void ResolveRefusesTlsTrustForTheProfileDefaultUrl()
    {
        var vSettings = new TrSetupSettings { TrustedSelfSignedEndpoints = { SettingKey } };

        var vResolved = EndpointResolver.Resolve(DefaultUrl, SettingKey, vSettings);

        Assert.Equal(DefaultUrl, vResolved.Url);
        Assert.False(vResolved.AllowSelfSignedCertificate);
    }

    /// <summary>
    /// Scenario: the AppStudio App Manager check detects on a Mac that configured the LAN endpoint,
    /// which answers 200.
    /// Expect: the check PASSES against the overridden URL — the whole point of REQ-FN-028 — and
    /// the localhost default is never probed.
    /// </summary>
    [Fact]
    public async Task AppManagerCheckPassesAgainstTheConfiguredLanEndpoint()
    {
        var vSettings = new TrSetupSettings { Endpoints = { [SettingKey] = LanUrl } };
        var vProbe = new FakeHttpStatusProbe();
        vProbe.Responses[LanUrl] = new HttpProbeResult(true, 200, "ok", null);
        var vCheck = BuildCheck(vProbe, vSettings);

        var vResult = await vCheck.DetectAsync();

        Assert.Equal(CheckStatus.Pass, vResult.Status);
        Assert.Contains(LanUrl, vResult.Evidence);
        Assert.DoesNotContain("localhost", vResult.Evidence);
        Assert.Equal(LanUrl, Assert.Single(vProbe.Requests).Url);
    }

    /// <summary>
    /// Scenario: nothing is configured, so the localhost default is probed and refused.
    /// Expect: the failure evidence names the URL AND its provenance, so "connection refused" can
    /// not be mistaken for "the service is down" when the real cause is a wrong host.
    /// </summary>
    [Fact]
    public async Task UnreachableDefaultEvidenceNamesTheEndpointAndItsProvenance()
    {
        var vCheck = BuildCheck(new FakeHttpStatusProbe(), new TrSetupSettings());

        var vResult = await vCheck.DetectAsync();

        Assert.Equal(CheckStatus.Fail, vResult.Status);
        Assert.Contains(DefaultUrl, vResult.Evidence);
        Assert.Contains("profile default", vResult.Evidence);
        Assert.Contains(SettingKey, vResult.Evidence);
    }

    /// <summary>
    /// Scenario: the overridden LAN endpoint rejects on TLS (an ASP.NET dev certificate) and the
    /// user has not opted into trusting it.
    /// Expect: the evidence adds the targeted "trust self-signed certificate" hint rather than
    /// leaving a certificate rejection looking like an outage.
    /// </summary>
    [Fact]
    public async Task CertificateRejectionEvidenceOffersTheExplicitTrustOptIn()
    {
        var vSettings = new TrSetupSettings { Endpoints = { [SettingKey] = LanUrl } };
        var vProbe = new FakeHttpStatusProbe();
        vProbe.Responses[LanUrl] = new HttpProbeResult(
            false, null, string.Empty, "HttpRequestException: The SSL connection could not be established (certificate)");
        var vCheck = BuildCheck(vProbe, vSettings);

        var vResult = await vCheck.DetectAsync();

        Assert.Equal(CheckStatus.Fail, vResult.Status);
        Assert.Contains("Trust self-signed certificate", vResult.Evidence);
        Assert.Contains(SettingKey, vResult.Evidence);
    }

    /// <summary>
    /// Scenario: detect runs with and without the per-endpoint trust opt-in.
    /// Expect: the relaxed-TLS flag reaches the probe ONLY when the user opted in — the default
    /// probe path never asks for reduced certificate validation.
    /// </summary>
    [Fact]
    public async Task TrustOptInIsTheOnlyThingThatRelaxesTlsAtTheProbe()
    {
        var vPlain = new FakeHttpStatusProbe();
        await BuildCheck(vPlain, new TrSetupSettings { Endpoints = { [SettingKey] = LanUrl } }).DetectAsync();
        Assert.False(Assert.Single(vPlain.Requests).AllowUntrustedCertificate);

        var vTrusting = new FakeHttpStatusProbe();
        await BuildCheck(
            vTrusting,
            new TrSetupSettings
            {
                Endpoints = { [SettingKey] = LanUrl },
                TrustedSelfSignedEndpoints = { SettingKey }
            }).DetectAsync();
        Assert.True(Assert.Single(vTrusting.Requests).AllowUntrustedCertificate);
    }

    /// <summary>
    /// Scenario: the settings override changes AFTER the check was constructed (the user saves a new
    /// value and the board re-scopes without a restart).
    /// Expect: the next detect probes the NEW URL — resolution happens per detect, not once.
    /// </summary>
    [Fact]
    public async Task OverrideIsResolvedPerDetectSoASaveTakesEffectWithoutRestart()
    {
        var vSettings = new TrSetupSettings();
        var vProbe = new FakeHttpStatusProbe();
        vProbe.Responses[LanUrl] = new HttpProbeResult(true, 200, "ok", null);
        var vCheck = BuildCheck(vProbe, vSettings);

        var vBefore = await vCheck.DetectAsync();
        vSettings.Endpoints[SettingKey] = LanUrl;
        var vAfter = await vCheck.DetectAsync();

        Assert.Equal(CheckStatus.Fail, vBefore.Status);
        Assert.Equal(CheckStatus.Pass, vAfter.Status);
    }

    /// <summary>
    /// Scenario: the shipped AppStudio built-in profile is loaded.
    /// Expect: the App Manager endpoint still defaults to localhost (single-machine case unchanged)
    /// AND declares the override key, so a multi-machine setup is configurable at all.
    /// </summary>
    [Fact]
    public void BuiltInAppStudioProfileDeclaresTheAppManagerOverrideKey()
    {
        var vRequirement = BuiltInProfiles.CreateDefault()
            .Find("AppStudio")!
            .Requirements
            .Single(aReq => aReq.Id == "appstudio.appmanager-api");

        Assert.Equal(DefaultUrl, vRequirement.Param("url"));
        Assert.Equal(SettingKey, vRequirement.Param(EndpointResolver.UrlSettingKeyParam));
    }

    private static Check BuildCheck(IHttpStatusProbe aProbe, TrSetupSettings aSettings)
    {
        var vProfile = new BuiltInProfiles().RegisterFromJson(AppManagerJson);
        var vRunner = new FakeProcessRunner();
        var vContext = new ProfileCheckContext(
            vProfile.Name,
            vRunner,
            CheckFixServices.CreateDefault(vRunner),
            aProbe,
            new FakeSystemProbe(),
            () => aSettings);
        return new ProfileCheckFactory().CreateChecks(vProfile, vContext).Single();
    }
}
