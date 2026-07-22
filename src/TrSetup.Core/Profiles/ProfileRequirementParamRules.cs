namespace TrSetup.Core.Profiles;

/// <summary>
/// The documented per-type required-parameter contract used during schema validation
/// (REQ-FN-021). A requirement whose <see cref="ProfileRequirement.Params"/> is missing a
/// required key for its type fails load with a clear error — never a silent skip.
/// </summary>
/// <remarks>
/// Presence-style types (Cluster A) list the exact keys their handler reads. The three heavy
/// types (<c>service</c>, <c>runtime-install</c>, <c>disk-space</c>) are owned by Cluster C and
/// list their single required discriminator key (<c>service</c>/<c>runtime</c>/<c>floorGb</c>);
/// their optional keys are defaulted inside the handler.
/// </remarks>
internal static class ProfileRequirementParamRules
{
    private static readonly IReadOnlyDictionary<string, string[]> RequiredKeysByType =
        new Dictionary<string, string[]>(StringComparer.Ordinal)
        {
            [ProfileRequirementTypes.Sdk] = Array.Empty<string>(),              // reads: version (optional)
            [ProfileRequirementTypes.Workload] = new[] { "workload" },          // reads: workload
            [ProfileRequirementTypes.CliTool] = new[] { "command" },            // reads: command, versionArgs?, minVersion?
            [ProfileRequirementTypes.Endpoint] = new[] { "url" },               // reads: url, urlSettingKey?
            [ProfileRequirementTypes.NugetFeed] = new[] { "url" },              // reads: url, patEnvVar?
            [ProfileRequirementTypes.EnvSecret] = new[] { "envVar" },           // reads: envVar (presence-only, ADR-008)
            [ProfileRequirementTypes.AppiumHead] = new[] { "url" },             // reads: url (probes url + /status)
            [ProfileRequirementTypes.Service] = new[] { "service" },            // Cluster C: postgres|ffmpeg; postgres also port?/extension?
            [ProfileRequirementTypes.RuntimeInstall] = new[] { "runtime" },      // Cluster C: e.g. comfyui; releaseTag?
            [ProfileRequirementTypes.DiskSpace] = new[] { "floorGb" }            // Cluster C: integer GB; path?
        };

    /// <summary>
    /// Appends a validation error for every required parameter key absent (or blank) for the
    /// requirement's type.
    /// </summary>
    /// <param name="aType">The requirement type (already validated as known).</param>
    /// <param name="aParams">The requirement's parameter bag.</param>
    /// <param name="aErrors">Sink the caller appends "missing param" messages to.</param>
    /// <param name="aContext">Human context (the requirement id) prefixed to any error.</param>
    public static void Validate(
        string aType,
        IReadOnlyDictionary<string, string> aParams,
        IList<string> aErrors,
        string aContext)
    {
        if (!RequiredKeysByType.TryGetValue(aType, out var vRequired))
        {
            return;
        }

        foreach (var vKey in vRequired)
        {
            if (!aParams.TryGetValue(vKey, out var vValue) || string.IsNullOrWhiteSpace(vValue))
            {
                aErrors.Add($"{aContext}: type '{aType}' requires param '{vKey}'.");
            }
        }
    }
}
