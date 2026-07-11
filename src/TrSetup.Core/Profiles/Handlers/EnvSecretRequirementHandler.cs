using TrSetup.Core.Checks;

namespace TrSetup.Core.Profiles.Handlers;

/// <summary>
/// Presence handler for the <c>env-secret</c> requirement type (REQ-FN-021): detects that a
/// secret's environment variable (param <c>envVar</c>) is set and non-empty. The value is
/// <b>never</b> read, logged, exported, or shown — presence-only (ADR-008); evidence reports only
/// whether the variable is populated.
/// </summary>
public sealed class EnvSecretRequirementHandler : IProfileRequirementHandler
{
    /// <inheritdoc />
    public string Type => ProfileRequirementTypes.EnvSecret;

    /// <inheritdoc />
    public Check CreateCheck(ProfileRequirement aRequirement, ProfileCheckContext aContext)
    {
        ArgumentNullException.ThrowIfNull(aRequirement);
        ArgumentNullException.ThrowIfNull(aContext);
        var vEnvVar = aRequirement.Param("envVar") ?? string.Empty;
        var vExplain = new CheckExplanation(
            $"Whether the secret env var '{vEnvVar}' this app needs is set (presence-only — the value is never read).",
            "The app authenticates with this secret at runtime; a missing secret breaks that integration.",
            "WORKFLOW §0");
        return new ProfileCheck(
            aRequirement,
            aContext.ProfileName,
            vExplain,
            aToken => DetectAsync(aContext, vEnvVar));
    }

    private static Task<CheckResult> DetectAsync(ProfileCheckContext aContext, string aEnvVar)
    {
        var vIsPresent = !string.IsNullOrWhiteSpace(aContext.SystemProbe.GetEnvironmentVariable(aEnvVar));
        var vResult = vIsPresent
            ? CheckResult.Pass($"Secret env var '{aEnvVar}' is set (presence-only; value not read).")
            : CheckResult.Fail($"Secret env var '{aEnvVar}' is not set.");
        return Task.FromResult(vResult);
    }
}
