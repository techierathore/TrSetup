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
}
