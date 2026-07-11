namespace TrSetup.Core.Profiles;

/// <summary>
/// The ten generic requirement-instance type strings a declarative <c>trsetup-profile.json</c>
/// may declare (REQ-FN-021 / BRD-33). Kept as constants in one place so handler registrations,
/// validation, and profile JSON all reference the same symbolic strings — never a raw literal.
/// </summary>
public static class ProfileRequirementTypes
{
    /// <summary>A .NET SDK requirement (detected via <c>dotnet --list-sdks</c>).</summary>
    public const string Sdk = "sdk";

    /// <summary>A .NET workload requirement (detected via <c>dotnet workload list</c>).</summary>
    public const string Workload = "workload";

    /// <summary>A command-line tool requirement (detected via <c>&lt;command&gt; --version</c>).</summary>
    public const string CliTool = "cli-tool";

    /// <summary>A long-running local service requirement (heavy type — Cluster C).</summary>
    public const string Service = "service";

    /// <summary>An HTTP endpoint reachability requirement (detected via an HTTP GET probe).</summary>
    public const string Endpoint = "endpoint";

    /// <summary>A NuGet feed reachability requirement (feed reachable + optional PAT presence).</summary>
    public const string NugetFeed = "nuget-feed";

    /// <summary>An environment secret presence requirement (presence-only, ADR-008 — value never read).</summary>
    public const string EnvSecret = "env-secret";

    /// <summary>A free-disk-space floor requirement (heavy type — Cluster C).</summary>
    public const string DiskSpace = "disk-space";

    /// <summary>An Appium head reachability requirement (detected via an HTTP <c>/status</c> probe).</summary>
    public const string AppiumHead = "appium-head";

    /// <summary>A managed runtime install requirement (heavy type — Cluster C).</summary>
    public const string RuntimeInstall = "runtime-install";

    /// <summary>The complete set of the ten known requirement type strings.</summary>
    public static IReadOnlyCollection<string> All { get; } = new[]
    {
        Sdk, Workload, CliTool, Service, Endpoint, NugetFeed, EnvSecret, DiskSpace, AppiumHead, RuntimeInstall
    };

    /// <summary>
    /// Whether the given string is one of the ten known requirement types.
    /// </summary>
    /// <param name="aType">The type string to test (case-sensitive — the JSON uses the exact lower-case forms).</param>
    /// <returns><c>true</c> when <paramref name="aType"/> is a known requirement type.</returns>
    public static bool IsKnown(string aType) => All.Contains(aType);
}
