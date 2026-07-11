using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace TrSetup.Core.Profiles;

/// <summary>
/// Resolves the declarative profile for a selected app (REQ-FN-021 / BRD-33/34/35): it loads the
/// built-in profile of that name, looks for an app-repo override at
/// <c>&lt;repoRoot&gt;/.tfcore/trsetup-profile.json</c>, and merges them so the <b>app repo wins</b> —
/// a repo requirement whose id matches a built-in one REPLACES it, new ids are appended, and
/// un-overridden built-ins are kept in their original order. Role tags ride on each requirement,
/// so a single resolved profile drives build-on-Windows and run-on-Mac alike.
/// </summary>
public sealed class ProfileLoader
{
    private readonly BuiltInProfiles objBuiltIns;
    private readonly ILogger objLogger;

    /// <summary>
    /// Creates the loader.
    /// </summary>
    /// <param name="aBuiltIns">The built-in profile registry, or <c>null</c> to use <see cref="BuiltInProfiles.CreateDefault"/>.</param>
    /// <param name="aLogger">Optional logger; a no-op logger is used when omitted.</param>
    public ProfileLoader(BuiltInProfiles? aBuiltIns = null, ILogger<ProfileLoader>? aLogger = null)
    {
        objBuiltIns = aBuiltIns ?? BuiltInProfiles.CreateDefault();
        objLogger = aLogger ?? NullLogger<ProfileLoader>.Instance;
    }

    /// <summary>
    /// Resolves the merged profile for the named app.
    /// </summary>
    /// <param name="aProfileName">The selected app name (e.g. <c>AppStudio</c>).</param>
    /// <param name="aRepoRoot">Repo root to resolve the app-repo override against, or <c>null</c> for <see cref="ProfilePaths.RepoRoot"/>.</param>
    /// <returns>The merged profile, or <c>null</c> when neither a built-in nor a matching app-repo override exists.</returns>
    /// <exception cref="ProfileValidationException">Thrown when the app-repo override is present but malformed.</exception>
    public TrSetupProfile? Resolve(string aProfileName, string? aRepoRoot = null)
    {
        if (string.IsNullOrWhiteSpace(aProfileName))
        {
            return null;
        }

        var vBuiltIn = objBuiltIns.Find(aProfileName);
        var vOverride = ReadOverride(aProfileName, aRepoRoot);

        if (vBuiltIn is null && vOverride is null)
        {
            return null;
        }

        if (vBuiltIn is null)
        {
            objLogger.LogInformation("Profile '{Profile}' resolved from app-repo override only (no built-in).", aProfileName);
            return vOverride;
        }

        if (vOverride is null)
        {
            return vBuiltIn;
        }

        return Merge(vBuiltIn, vOverride);
    }

    /// <summary>
    /// Resolves the merged profile for the named app <b>with the per-requirement merge source</b>
    /// (REQ-UI-006 Settings profile-details). A read-only companion to <see cref="Resolve"/>: it
    /// applies the exact same merge rules (built-in order preserved, an app-repo requirement of the
    /// same id REPLACES the built-in, new app-repo ids appended) but tags each resulting row as
    /// <see cref="RequirementSource.BuiltIn"/> or <see cref="RequirementSource.Override"/>. It does
    /// not change the merge behaviour <see cref="Resolve"/> produces for the board.
    /// </summary>
    /// <param name="aProfileName">The selected app name (e.g. <c>AppStudio</c>).</param>
    /// <param name="aRepoRoot">Repo root to resolve the app-repo override against, or <c>null</c> for <see cref="ProfilePaths.RepoRoot"/>.</param>
    /// <returns>The resolved, source-tagged requirements in merge order, or <c>null</c> when neither a built-in nor a matching app-repo override exists.</returns>
    /// <exception cref="ProfileValidationException">Thrown when the app-repo override is present but malformed.</exception>
    public IReadOnlyList<ResolvedRequirement>? ResolveWithSources(string aProfileName, string? aRepoRoot = null)
    {
        if (string.IsNullOrWhiteSpace(aProfileName))
        {
            return null;
        }

        var vBuiltIn = objBuiltIns.Find(aProfileName);
        var vOverride = ReadOverride(aProfileName, aRepoRoot);

        if (vBuiltIn is null && vOverride is null)
        {
            return null;
        }

        if (vBuiltIn is null)
        {
            return vOverride!.Requirements
                .Select(aReq => new ResolvedRequirement(aReq, RequirementSource.Override)).ToList();
        }

        if (vOverride is null)
        {
            return vBuiltIn.Requirements
                .Select(aReq => new ResolvedRequirement(aReq, RequirementSource.BuiltIn)).ToList();
        }

        var vOverridesById = vOverride.Requirements.ToDictionary(aReq => aReq.Id, StringComparer.OrdinalIgnoreCase);
        var vResolved = new List<ResolvedRequirement>();

        // Built-in order preserved; an overriding requirement of the same id is tagged Override (app repo wins).
        foreach (var vBuiltInReq in vBuiltIn.Requirements)
        {
            vResolved.Add(vOverridesById.TryGetValue(vBuiltInReq.Id, out var vReplacement)
                ? new ResolvedRequirement(vReplacement, RequirementSource.Override)
                : new ResolvedRequirement(vBuiltInReq, RequirementSource.BuiltIn));
        }

        // New ids introduced only by the app repo are appended in the repo's order.
        var vBuiltInIds = new HashSet<string>(vBuiltIn.Requirements.Select(aReq => aReq.Id), StringComparer.OrdinalIgnoreCase);
        foreach (var vOverrideReq in vOverride.Requirements)
        {
            if (!vBuiltInIds.Contains(vOverrideReq.Id))
            {
                vResolved.Add(new ResolvedRequirement(vOverrideReq, RequirementSource.Override));
            }
        }

        return vResolved;
    }

    private static TrSetupProfile Merge(TrSetupProfile aBuiltIn, TrSetupProfile aOverride)
    {
        var vOverridesById = aOverride.Requirements.ToDictionary(aReq => aReq.Id, StringComparer.OrdinalIgnoreCase);
        var vMerged = new List<ProfileRequirement>();

        // Built-in order preserved; an overriding requirement of the same id REPLACES the built-in (app repo wins).
        foreach (var vBuiltInReq in aBuiltIn.Requirements)
        {
            vMerged.Add(vOverridesById.TryGetValue(vBuiltInReq.Id, out var vReplacement) ? vReplacement : vBuiltInReq);
        }

        // New ids introduced only by the app repo are appended in the repo's order.
        var vBuiltInIds = new HashSet<string>(aBuiltIn.Requirements.Select(aReq => aReq.Id), StringComparer.OrdinalIgnoreCase);
        foreach (var vOverrideReq in aOverride.Requirements)
        {
            if (!vBuiltInIds.Contains(vOverrideReq.Id))
            {
                vMerged.Add(vOverrideReq);
            }
        }

        return new TrSetupProfile(aBuiltIn.Name, vMerged);
    }

    private TrSetupProfile? ReadOverride(string aProfileName, string? aRepoRoot)
    {
        var vPath = ProfilePaths.OverridePath(aRepoRoot);
        if (!File.Exists(vPath))
        {
            return null;
        }

        var vProfile = ProfileJsonReader.Read(File.ReadAllText(vPath), vPath);
        if (!string.Equals(vProfile.Name, aProfileName, StringComparison.OrdinalIgnoreCase))
        {
            objLogger.LogInformation(
                "App-repo override at {Path} is for '{OtherProfile}', not the selected '{Profile}' — ignored.",
                vPath, vProfile.Name, aProfileName);
            return null;
        }

        return vProfile;
    }
}
