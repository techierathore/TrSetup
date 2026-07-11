using System.Text;
using TrSetup.Core.Catalog.Probing;
using TrSetup.Core.Checks;
using TrSetup.Core.ConfigWriting;
using TrSetup.Core.Fixing;
using TrSetup.Core.Profiles;
using TrSetup.Core.Settings;

namespace TrSetup.Core.Catalog.Framework;

/// <summary>
/// REQ-FN-024 (BRD-38): the framework "always-on" extra that keeps an app repo's
/// <c>.tfcore/core-config.yaml → runtimeVerification.appium</c> block in sync with the verified
/// heads. It only applies inside an app repo (detect returns <see cref="CheckStatus.NotApplicable"/>
/// when there is no <c>core-config.yaml</c>). The fixer writes the appium block through the
/// idempotent <see cref="ManagedBlockWriter"/> (user config outside the markers preserved
/// byte-for-byte) and then curl-verifies each registered head by HTTP-probing <c>&lt;url&gt;/status</c>.
/// </summary>
public sealed class AppiumConfigBlockCheck : Check
{
    /// <summary>Stable managed-block id written into <c>core-config.yaml</c>.</summary>
    public const string BlockId = "framework.appium-endpoints";

    private const string AndroidUrl = "http://localhost:4723";

    private readonly IHttpStatusProbe objHttpProbe;
    private readonly Func<TrSetupSettings> objSettingsAccessor;
    private readonly CheckFixServices? objFix;
    private readonly Func<string> objConfigPathAccessor;

    /// <summary>
    /// Creates the check.
    /// </summary>
    /// <param name="aHttpProbe">The HTTP GET reachability probe the fixer curl-verifies each head with.</param>
    /// <param name="aSettingsAccessor">Live settings accessor (the configured <c>MacIp</c> endpoint drives the ios/maccatalyst heads).</param>
    /// <param name="aFix">Fixer frameworks; when null the check is detect-only (no Fix button).</param>
    /// <param name="aConfigPathAccessor">Config-path override for tests; defaults to <c>&lt;repoRoot&gt;/.tfcore/core-config.yaml</c>.</param>
    /// <exception cref="ArgumentNullException">Thrown when a required dependency is null.</exception>
    public AppiumConfigBlockCheck(
        IHttpStatusProbe aHttpProbe,
        Func<TrSetupSettings> aSettingsAccessor,
        CheckFixServices? aFix = null,
        Func<string>? aConfigPathAccessor = null)
    {
        objHttpProbe = aHttpProbe ?? throw new ArgumentNullException(nameof(aHttpProbe));
        objSettingsAccessor = aSettingsAccessor ?? throw new ArgumentNullException(nameof(aSettingsAccessor));
        objFix = aFix;
        objConfigPathAccessor = aConfigPathAccessor ?? DefaultConfigPath;
    }

    /// <inheritdoc />
    public override string Id => "framework.appium-config-block";

    /// <inheritdoc />
    public override string Title => "Appium endpoints written to core-config.yaml";

    /// <inheritdoc />
    public override string Category => BoardCategories.FrameworkCore;

    /// <inheritdoc />
    public override MachineRole Roles => MachineRole.AgentHostWsl;

    /// <inheritdoc />
    public override CheckSeverity Severity => CheckSeverity.Recommended;

    /// <inheritdoc />
    public override CheckExplanation Explain => new(
        "The Appium head endpoints (android / ios / maccatalyst) recorded in the app repo's .tfcore/core-config.yaml.",
        "The verifier reads runtimeVerification.appium to drive each MAUI head; a missing or stale block degrades those heads to STATIC-ONLY.",
        "WORKFLOW §0b");

    /// <summary>Whether this check instance was given a fixer.</summary>
    private bool CanFix => objFix is not null;

    /// <inheritdoc />
    public override string? FixPreview => CanFix ? BuildPreview() : null;

    /// <inheritdoc />
    public override CheckFix? FixAsync => CanFix ? FixCoreAsync : null;

    /// <inheritdoc />
    public override Task<CheckResult> DetectAsync(CancellationToken aCancellationToken = default)
    {
        var vPath = objConfigPathAccessor();
        if (!File.Exists(vPath))
        {
            return Task.FromResult(CheckResult.NotApplicable(
                $"No app-repo core-config.yaml at {vPath} — the appium block only applies inside an app repo."));
        }

        var vWriter = objFix?.ConfigWriter ?? new ManagedBlockWriter();
        if (!vWriter.ContainsBlock(vPath, BlockId, CommentSyntax.Hash))
        {
            return Task.FromResult(CheckResult.Fail(
                "core-config.yaml has no TrSetup appium block — offer to write the verified endpoints."));
        }

        return Task.FromResult(BodyMatches(File.ReadAllText(vPath), RenderBody())
            ? CheckResult.Pass("Appium block present and matches the verified endpoints.")
            : CheckResult.Warn("Appium block present but stale (endpoints changed) — offer to rewrite."));
    }

    private async Task<FixResult> FixCoreAsync(ConsentToken aConsent, CancellationToken aCancellationToken)
    {
        var vWrite = objFix!.ConfigWriter.UpsertBlock(
            objConfigPathAccessor(), BlockId, RenderBody(), CommentSyntax.Hash);
        var vVerify = await CurlVerifyHeadsAsync(aCancellationToken).ConfigureAwait(false);
        return new FixResult(true, vWrite.Evidence + Environment.NewLine + vVerify);
    }

    private async Task<string> CurlVerifyHeadsAsync(CancellationToken aCancellationToken)
    {
        var vLines = new List<string>();
        foreach (var vHead in Heads())
        {
            var vStatusUrl = vHead.Url.TrimEnd('/') + "/status";
            var vResult = await objHttpProbe.GetAsync(vStatusUrl, aCancellationToken).ConfigureAwait(false);
            vLines.Add($"{vHead.Name}: {DescribeProbe(vStatusUrl, vResult)}");
        }

        return string.Join(Environment.NewLine, vLines);
    }

    private static string DescribeProbe(string aUrl, HttpProbeResult aResult)
        => aResult.IsReachable
            ? $"{aUrl} reachable (HTTP {aResult.StatusCode})"
            : $"{aUrl} UNREACHABLE ({aResult.Error})";

    private string BuildPreview()
    {
        var vHeads = string.Join(", ", Heads().Select(aHead => $"{aHead.Name}→{aHead.Url}/status"));
        return $"Write managed block '{BlockId}' into {objConfigPathAccessor()} (runtimeVerification.appium), " +
               $"then curl-verify each head: {vHeads}";
    }

    private string RenderBody()
    {
        var vBuilder = new StringBuilder();
        vBuilder.AppendLine("runtimeVerification:");
        vBuilder.AppendLine("  appium:");
        AppendAndroid(vBuilder);
        AppendMacHeads(vBuilder);
        return vBuilder.ToString().TrimEnd();
    }

    private static void AppendAndroid(StringBuilder aBuilder)
    {
        aBuilder.AppendLine("    android:");
        aBuilder.AppendLine($"      url: {AndroidUrl}");
        aBuilder.AppendLine("      avd: Pixel_API_34");
        aBuilder.AppendLine("      launch: winrun \"powershell -File start-android-verify.ps1\"");
    }

    private void AppendMacHeads(StringBuilder aBuilder)
    {
        var vMacIp = MacIp();
        if (vMacIp is null)
        {
            return;
        }

        aBuilder.AppendLine("    ios:");
        aBuilder.AppendLine($"      url: http://{vMacIp}:4723");
        aBuilder.AppendLine("      simulator: \"iPhone 15\"");
        aBuilder.AppendLine("    maccatalyst:");
        aBuilder.AppendLine($"      url: http://{vMacIp}:4723");
    }

    private IReadOnlyList<(string Name, string Url)> Heads()
    {
        var vHeads = new List<(string Name, string Url)> { ("android", AndroidUrl) };
        var vMacIp = MacIp();
        if (vMacIp is not null)
        {
            vHeads.Add(("ios", $"http://{vMacIp}:4723"));
            vHeads.Add(("maccatalyst", $"http://{vMacIp}:4723"));
        }

        return vHeads;
    }

    private string? MacIp()
    {
        var vSettings = objSettingsAccessor();
        return vSettings.Endpoints.TryGetValue("MacIp", out var vIp) && !string.IsNullOrWhiteSpace(vIp)
            ? vIp.Trim()
            : null;
    }

    private static bool BodyMatches(string aFileText, string aBody)
    {
        foreach (var vLine in aBody.Split('\n'))
        {
            var vTrimmed = vLine.TrimEnd('\r');
            if (vTrimmed.Length > 0 && !aFileText.Contains(vTrimmed, StringComparison.Ordinal))
            {
                return false;
            }
        }

        return true;
    }

    private static string DefaultConfigPath()
        => Path.Combine(ProfilePaths.RepoRoot, ".tfcore", "core-config.yaml");
}
