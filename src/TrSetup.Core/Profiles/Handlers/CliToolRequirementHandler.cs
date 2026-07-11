using TrSetup.Core.Catalog.Probing;
using TrSetup.Core.Checks;
using TrSetup.Core.Processes;

namespace TrSetup.Core.Profiles.Handlers;

/// <summary>
/// Presence handler for the <c>cli-tool</c> requirement type (REQ-FN-021): detects a command-line
/// tool by shelling <c>&lt;command&gt; &lt;versionArgs&gt;</c>. Params: <c>command</c> (required),
/// <c>versionArgs</c> (optional, default <c>--version</c>), <c>minVersion</c> (optional, reported
/// in evidence — no semantic comparison).
/// </summary>
public sealed class CliToolRequirementHandler : IProfileRequirementHandler
{
    /// <inheritdoc />
    public string Type => ProfileRequirementTypes.CliTool;

    /// <inheritdoc />
    public Check CreateCheck(ProfileRequirement aRequirement, ProfileCheckContext aContext)
    {
        ArgumentNullException.ThrowIfNull(aRequirement);
        ArgumentNullException.ThrowIfNull(aContext);
        var vCommand = aRequirement.Param("command") ?? string.Empty;
        var vVersionArgs = aRequirement.Param("versionArgs") ?? "--version";
        var vExplain = new CheckExplanation(
            $"The '{vCommand}' command-line tool this app requires.",
            "The tool is invoked by the app's build or run workflow; absence breaks that step.",
            "WORKFLOW §0");
        return new ProfileCheck(
            aRequirement,
            aContext.ProfileName,
            vExplain,
            aToken => DetectAsync(aContext, vCommand, vVersionArgs, aToken));
    }

    private static async Task<CheckResult> DetectAsync(
        ProfileCheckContext aContext,
        string aCommand,
        string aVersionArgs,
        CancellationToken aToken)
    {
        var vRun = await ProcessProbe.RunAsync(
            aContext.ProcessRunner,
            new ProcessRunRequest(aCommand, aVersionArgs, null, TimeSpan.FromSeconds(15)),
            aToken).ConfigureAwait(false);
        if (!vRun.Succeeded)
        {
            return CheckResult.Fail($"'{aCommand}' not found or failed.\n{vRun.ToEvidenceString()}");
        }

        var vVersionText = vRun.StandardOutput.Trim();
        if (string.IsNullOrWhiteSpace(vVersionText))
        {
            vVersionText = vRun.StandardError.Trim();
        }

        return CheckResult.Pass($"'{aCommand}' present ($ {aCommand} {aVersionArgs}): {vVersionText}");
    }
}
