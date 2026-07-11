namespace TrSetup.Core.Catalog;

/// <summary>
/// The board group names checks render under (BRD §9 F-BOARD grouping). Kept as constants so
/// every check in a group spells the category identically and the board groups stay stable.
/// </summary>
public static class BoardCategories
{
    /// <summary>Machine-local framework items: SDKs, tools, bridges installed on this box.</summary>
    public const string FrameworkCore = "Framework core";

    /// <summary>Cross-machine reachability probes (HTTP/ping only — never remote-execute).</summary>
    public const string Bridges = "Bridges (cross-machine)";
}
