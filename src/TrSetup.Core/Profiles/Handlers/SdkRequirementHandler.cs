using TrSetup.Core.Catalog.Probing;
using TrSetup.Core.Checks;
using TrSetup.Core.Processes;

namespace TrSetup.Core.Profiles.Handlers;

/// <summary>
/// Presence handler for the <c>sdk</c> requirement type (REQ-FN-021): detects an installed .NET
/// SDK via <c>dotnet --list-sdks</c>. When the requirement declares a <c>version</c> param the
/// probe passes only when a listed SDK version starts with it (e.g. <c>10.0</c>).
/// </summary>
public sealed class SdkRequirementHandler : IProfileRequirementHandler
{
    /// <inheritdoc />
    public string Type => ProfileRequirementTypes.Sdk;

    /// <inheritdoc />
    public Check CreateCheck(ProfileRequirement aRequirement, ProfileCheckContext aContext)
    {
        ArgumentNullException.ThrowIfNull(aRequirement);
        ArgumentNullException.ThrowIfNull(aContext);
        var vVersion = aRequirement.Param("version");
        var vExplain = new CheckExplanation(
            $"The .NET SDK required by this app{(vVersion is null ? string.Empty : $" (version {vVersion})")}.",
            "The app cannot be built or run without a matching .NET SDK installed on this machine.",
            "WORKFLOW §0");
        return new ProfileCheck(
            aRequirement,
            aContext.ProfileName,
            vExplain,
            aToken => DetectAsync(aContext, vVersion, aToken));
    }

    private static async Task<CheckResult> DetectAsync(ProfileCheckContext aContext, string? aVersion, CancellationToken aToken)
    {
        var vRun = await ProcessProbe.RunAsync(
            aContext.ProcessRunner,
            new ProcessRunRequest("dotnet", "--list-sdks", null, TimeSpan.FromSeconds(15)),
            aToken).ConfigureAwait(false);
        if (!vRun.Succeeded || string.IsNullOrWhiteSpace(vRun.StandardOutput))
        {
            return CheckResult.Fail($"No .NET SDK found.\n{vRun.ToEvidenceString()}");
        }

        if (aVersion is null)
        {
            return CheckResult.Pass($"A .NET SDK is installed ($ dotnet --list-sdks).\n{vRun.StandardOutput.Trim()}");
        }

        var vMatches = vRun.StandardOutput
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Any(aLine => aLine.StartsWith(aVersion, StringComparison.OrdinalIgnoreCase));
        return vMatches
            ? CheckResult.Pass($".NET SDK {aVersion}.x present.\n{vRun.StandardOutput.Trim()}")
            : CheckResult.Fail($"No .NET SDK matching {aVersion} found.\n{vRun.StandardOutput.Trim()}");
    }
}
