namespace TrSetup.Core.Profiles;

/// <summary>
/// Board group names owned by the heavy profile requirement types (Cluster C, REQ-FN-025/026/029).
/// Kept separate from <see cref="Catalog.BoardCategories"/> so the heavy rows group under their own
/// headings without editing the framework-core catalog's category constants.
/// </summary>
public static class ProfileBoardCategories
{
    /// <summary>Long-running local services a profile requires (Postgres+PgVector, ffmpeg) — REQ-FN-026.</summary>
    public const string Services = "Services";

    /// <summary>Managed, isolated runtimes a profile installs (ComfyUI) — REQ-FN-025.</summary>
    public const string Runtimes = "Runtimes";

    /// <summary>Machine capacity floors (free disk space) — REQ-FN-029.</summary>
    public const string Capacity = "Capacity";
}
