namespace TrSetup.Core.Profiles;

/// <summary>
/// Resolves where the app-repo profile override lives (REQ-FN-021 / BRD-34). Mirrors the
/// <see cref="Downloads.TrSetupPaths.RootOverride"/> test-hook style so unit tests can point the
/// repo root at a temp directory without touching the real working tree.
/// </summary>
public static class ProfilePaths
{
    /// <summary>The relative location of the app-repo override within a repo root.</summary>
    public const string OverrideRelativePath = ".tfcore/trsetup-profile.json";

    /// <summary>
    /// Test/override hook: when set, <see cref="RepoRoot"/> returns this path instead of the
    /// current working directory. Set to <c>null</c> to restore the default.
    /// </summary>
    public static string? RepoRootOverride { get; set; }

    /// <summary>
    /// The repo root the app-repo override is resolved against: <see cref="RepoRootOverride"/>
    /// when set, otherwise the process's current working directory.
    /// </summary>
    public static string RepoRoot => RepoRootOverride ?? Directory.GetCurrentDirectory();

    /// <summary>
    /// The absolute path of the app-repo override file (<c>&lt;repoRoot&gt;/.tfcore/trsetup-profile.json</c>).
    /// </summary>
    /// <param name="aRepoRoot">The repo root to resolve against, or <c>null</c> to use <see cref="RepoRoot"/>.</param>
    /// <returns>The absolute override path.</returns>
    public static string OverridePath(string? aRepoRoot = null)
        => Path.Combine(aRepoRoot ?? RepoRoot, ".tfcore", "trsetup-profile.json");

    /// <summary>
    /// Resolves the SOURCE-REPO root to build a named app in (REQ-FN-028 / BRD-42), from the
    /// configured <see cref="Settings.TrSetupSettings.AppRepoPaths"/> entry, and VALIDATES it.
    /// <para>
    /// Deliberately does NOT fall back to <see cref="RepoRoot"/> / the process working directory:
    /// that fallback is the defect this replaces — a published app launched from its own output
    /// folder resolved the "repo" to that folder and emitted a build command for a path with no
    /// sources in it. An unconfigured or invalid path returns <c>null</c> so the caller can refuse
    /// with an honest reason instead of building somewhere wrong.
    /// </para>
    /// </summary>
    /// <param name="aAppName">The app whose repo root is wanted (matches the profile/app name).</param>
    /// <param name="aConfigured">The configured app→repo-path map, or <c>null</c> when none is configured.</param>
    /// <param name="aProblem">On <c>null</c> return, a human-readable reason fit for UI evidence.</param>
    /// <returns>The validated absolute repo root, or <c>null</c> when it is not usable.</returns>
    public static string? ResolveAppRepoRoot(
        string aAppName,
        IReadOnlyDictionary<string, string>? aConfigured,
        out string aProblem)
    {
        if (string.IsNullOrWhiteSpace(aAppName))
        {
            aProblem = "No app selected.";
            return null;
        }

        if (aConfigured is null || !aConfigured.TryGetValue(aAppName, out var vPath) || string.IsNullOrWhiteSpace(vPath))
        {
            aProblem =
                $"No source-repo path configured for '{aAppName}'. Set it in Settings → app repo paths " +
                $"(settings key 'AppRepoPaths': {{ \"{aAppName}\": \"/path/to/{aAppName}\" }}).";
            return null;
        }

        var vFull = Path.GetFullPath(vPath.Trim());
        if (!Directory.Exists(vFull))
        {
            aProblem = $"Configured repo path for '{aAppName}' does not exist: {vFull}.";
            return null;
        }

        if (!LooksLikeRepoRoot(vFull))
        {
            aProblem =
                $"Configured repo path for '{aAppName}' is not a source repo (no .sln/.csproj/.git found): {vFull}.";
            return null;
        }

        aProblem = string.Empty;
        return vFull;
    }

    /// <summary>
    /// Whether a directory looks like a buildable source repo — it holds a solution, a project, or
    /// a git working tree. Guards against pointing the build fixer at an arbitrary folder.
    /// </summary>
    /// <param name="aDirectory">The absolute directory to test.</param>
    /// <returns><c>true</c> when the directory carries a repo/build marker.</returns>
    private static bool LooksLikeRepoRoot(string aDirectory)
        => Directory.Exists(Path.Combine(aDirectory, ".git"))
           || File.Exists(Path.Combine(aDirectory, ".git"))
           || Directory.EnumerateFiles(aDirectory, "*.sln").Any()
           || Directory.EnumerateFiles(aDirectory, "*.slnx").Any()
           || Directory.EnumerateFiles(aDirectory, "*.csproj").Any();
}
