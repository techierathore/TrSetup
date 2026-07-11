using TrSetup.Core.Catalog.Probing;
using TrSetup.Core.Checks;
using TrSetup.Core.Processes;

namespace TrSetup.Core.Profiles.Handlers;

/// <summary>
/// Presence handler for the <c>workload</c> requirement type (REQ-FN-021): detects an installed
/// .NET workload via <c>dotnet workload list</c>, passing when the output lists the required
/// workload id (param <c>workload</c>, e.g. <c>maui</c> or <c>maui-android</c>).
/// </summary>
public sealed class WorkloadRequirementHandler : IProfileRequirementHandler
{
    /// <inheritdoc />
    public string Type => ProfileRequirementTypes.Workload;

    /// <inheritdoc />
    public Check CreateCheck(ProfileRequirement aRequirement, ProfileCheckContext aContext)
    {
        ArgumentNullException.ThrowIfNull(aRequirement);
        ArgumentNullException.ThrowIfNull(aContext);
        var vWorkload = aRequirement.Param("workload") ?? string.Empty;
        var vExplain = new CheckExplanation(
            $"The .NET workload '{vWorkload}' this app requires.",
            "Workloads ship the SDK packs the app's target platforms build against; the build fails without it.",
            "WORKFLOW §0");
        return new ProfileCheck(
            aRequirement,
            aContext.ProfileName,
            vExplain,
            aToken => DetectAsync(aContext, vWorkload, aToken));
    }

    private static async Task<CheckResult> DetectAsync(ProfileCheckContext aContext, string aWorkload, CancellationToken aToken)
    {
        var vRun = await ProcessProbe.RunAsync(
            aContext.ProcessRunner,
            new ProcessRunRequest("dotnet", "workload list", null, TimeSpan.FromSeconds(30)),
            aToken).ConfigureAwait(false);
        if (!vRun.Succeeded)
        {
            return CheckResult.Fail($"Could not list .NET workloads.\n{vRun.ToEvidenceString()}");
        }

        var vInstalled = vRun.StandardOutput
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Any(aLine => aLine.Split(' ', StringSplitOptions.RemoveEmptyEntries)
                .FirstOrDefault()?.Equals(aWorkload, StringComparison.OrdinalIgnoreCase) == true);
        return vInstalled
            ? CheckResult.Pass($"Workload '{aWorkload}' is installed ($ dotnet workload list).")
            : CheckResult.Fail($"Workload '{aWorkload}' is NOT installed.\n{vRun.StandardOutput.Trim()}");
    }
}
