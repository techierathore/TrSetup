using TrSetup.Core.Catalog.Probing;
using TrSetup.Core.Checks;
using TrSetup.Core.ConfigWriting;
using TrSetup.Core.Fixing;
using TrSetup.Core.Processes;
using TrSetup.Core.Settings;

namespace TrSetup.Core.Catalog.Mac;

/// <summary>
/// F-MACCHK: "Appium serving on 0.0.0.0:4723 (LaunchAgent, survives reboot)" — HTTP GET of
/// /status on the LAN address (the configured Mac endpoint, so we prove the LAN-visible
/// binding, not just loopback), plus a <c>launchctl list</c> probe for the reboot-surviving
/// LaunchAgent. The fixer writes the LaunchAgent plist inside a managed marker block (so the
/// whole file is TrSetup-owned and re-runs never duplicate) and loads it with
/// <c>launchctl load -w</c> — surviving reboots via <c>RunAtLoad</c>/<c>KeepAlive</c>
/// (REQ-FN-016 / REQ-FN-018).
/// </summary>
public sealed class MacAppiumLaunchAgentCheck : MacCheckBase
{
    /// <summary>The LaunchAgent label / stable managed-block id.</summary>
    public const string AgentLabel = "com.trsetup.appium";

    /// <summary>
    /// The plist body (no XML declaration — a leading marker comment keeps the file well-formed):
    /// runs Appium bound to 0.0.0.0:4723 at load, restarting if it exits.
    /// </summary>
    private const string PlistBody =
        "<!DOCTYPE plist PUBLIC \"-//Apple//DTD PLIST 1.0//EN\" \"http://www.apple.com/DTDs/PropertyList-1.0.dtd\">\n" +
        "<plist version=\"1.0\"><dict>\n" +
        "  <key>Label</key><string>" + AgentLabel + "</string>\n" +
        "  <key>ProgramArguments</key>\n" +
        "  <array><string>/bin/bash</string><string>-lc</string><string>appium --address 0.0.0.0 --port 4723</string></array>\n" +
        "  <key>RunAtLoad</key><true/>\n" +
        "  <key>KeepAlive</key><true/>\n" +
        "</dict></plist>";

    private readonly IHttpStatusProbe objHttpProbe;
    private readonly Func<TrSetupSettings> objSettingsAccessor;
    private readonly CheckFixServices? objFix;
    private readonly Func<string> objPlistPath;

    /// <summary>
    /// Creates the check.
    /// </summary>
    /// <param name="aProcessRunner">The process choke-point the launchctl probe runs through.</param>
    /// <param name="aHttpProbe">The HTTP reachability probe.</param>
    /// <param name="aSettingsAccessor">Live accessor for current settings (the configured Mac endpoint).</param>
    /// <param name="aFix">Fixer frameworks; when null the check is detect-only (no Fix button).</param>
    /// <param name="aPlistPath">Resolver for the LaunchAgent plist path; defaults to <c>~/Library/LaunchAgents/com.trsetup.appium.plist</c>.</param>
    public MacAppiumLaunchAgentCheck(
        IProcessRunner aProcessRunner,
        IHttpStatusProbe aHttpProbe,
        Func<TrSetupSettings> aSettingsAccessor,
        CheckFixServices? aFix = null,
        Func<string>? aPlistPath = null) : base(aProcessRunner, aFix)
    {
        objHttpProbe = aHttpProbe;
        objSettingsAccessor = aSettingsAccessor;
        objFix = aFix;
        objPlistPath = aPlistPath ?? DefaultPlistPath;
    }

    /// <inheritdoc />
    public override string? FixPreview => objFix is null
        ? null
        : $"write LaunchAgent plist {objPlistPath()} (managed block '{AgentLabel}'){Environment.NewLine}" +
          $"then load it: launchctl load -w {objPlistPath()}";

    /// <inheritdoc />
    public override CheckFix? FixAsync => objFix is null ? null : FixCoreAsync;

    private static string DefaultPlistPath() => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        "Library", "LaunchAgents", AgentLabel + ".plist");

    /// <inheritdoc />
    public override string Id => "mac.appium-launchagent";

    /// <inheritdoc />
    public override string Title => "Appium LaunchAgent on 0.0.0.0:4723";

    /// <inheritdoc />
    public override CheckSeverity Severity => CheckSeverity.Required;

    /// <inheritdoc />
    public override CheckExplanation Explain => new(
        "An Appium server bound to 0.0.0.0:4723 via a LaunchAgent so it survives reboots.",
        "The WSL agent host reaches this endpoint over the LAN; a loopback-only or hand-started Appium breaks after every reboot.",
        "WORKFLOW §0b");

    /// <inheritdoc />
    public override async Task<CheckResult> DetectAsync(CancellationToken aCancellationToken = default)
    {
        var vSettings = objSettingsAccessor();
        var vHasEndpoint = vSettings.Endpoints.TryGetValue("MacIp", out var vMacIp)
            && !string.IsNullOrWhiteSpace(vMacIp);
        var vHost = vHasEndpoint ? vMacIp!.Trim() : "localhost";
        var vUrl = $"http://{vHost}:4723/status";

        var vProbe = await objHttpProbe.GetAsync(vUrl, aCancellationToken).ConfigureAwait(false);
        if (!vProbe.IsSuccess)
        {
            var vDetail = vProbe.IsReachable ? $"HTTP {vProbe.StatusCode}" : vProbe.Error;
            return CheckResult.Fail(
                $"Appium not answering on {vUrl} ({vDetail})" +
                (vHasEndpoint ? string.Empty : " — no MacIp endpoint configured, probed loopback only") + ".");
        }

        var vLaunchctl = await RunMacCommandAsync("launchctl", "list", TimeSpan.FromSeconds(10), aCancellationToken)
            .ConfigureAwait(false);
        if (vLaunchctl.Succeeded && vLaunchctl.StandardOutput.Contains("appium", StringComparison.OrdinalIgnoreCase))
        {
            return CheckResult.Pass(
                $"Appium answering on {vUrl} (HTTP {vProbe.StatusCode}) and a LaunchAgent is loaded ($ launchctl list).");
        }

        return CheckResult.Warn(
            $"Appium answering on {vUrl} (HTTP {vProbe.StatusCode}) but no appium LaunchAgent found in launchctl list — it will not survive a reboot.");
    }

    private async Task<FixResult> FixCoreAsync(ConsentToken aConsent, CancellationToken aCancellationToken)
    {
        var vPath = objPlistPath();
        var vWrite = objFix!.ConfigWriter.UpsertBlock(vPath, AgentLabel, PlistBody, CommentSyntax.Xml);
        var vLoad = await RunMacCommandAsync(
            "launchctl", $"load -w {vPath}", TimeSpan.FromSeconds(30), aCancellationToken).ConfigureAwait(false);
        return new FixResult(vLoad.Succeeded, FixExecution.JoinOutput(vWrite.Evidence, vLoad.ToEvidenceString()));
    }
}
