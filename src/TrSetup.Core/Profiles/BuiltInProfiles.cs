using System.Reflection;

namespace TrSetup.Core.Profiles;

/// <summary>
/// The registry of profiles that ship built into TrSetup (REQ-FN-021 / BRD-34). Built-in
/// profiles are the base the app-repo <c>.tfcore/trsetup-profile.json</c> merges over (app repo
/// wins). Profiles are discovered from embedded JSON resources under
/// <c>src/TrSetup.Core/Profiles/BuiltIn/*.json</c> — each resource is keyed by the <c>name</c>
/// field inside the JSON, so onboarding a new app is a JSON file drop with no tool code.
/// </summary>
/// <remarks>
/// The embedding is configured once in <c>TrSetup.Core.csproj</c>
/// (<c>&lt;EmbeddedResource Include="Profiles\BuiltIn\*.json" /&gt;</c>); any file dropped in that
/// folder is auto-embedded and auto-discovered. Programmatic registration (for tests, or a name
/// not backed by an embedded file) is available via <see cref="Register"/>.
/// </remarks>
public sealed class BuiltInProfiles
{
    private const string EmbeddedResourceMarker = ".Profiles.BuiltIn.";

    private readonly Dictionary<string, TrSetupProfile> objByName = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Creates an empty registry (no auto-discovery). Prefer <see cref="CreateDefault"/> for the
    /// production set discovered from embedded resources.
    /// </summary>
    public BuiltInProfiles()
    {
    }

    /// <summary>The names of every registered built-in profile.</summary>
    public IReadOnlyCollection<string> Names => objByName.Keys.ToList();

    /// <summary>
    /// Builds the production registry by discovering every embedded
    /// <c>Profiles/BuiltIn/*.json</c> resource and keying it by its <c>name</c> field.
    /// </summary>
    /// <returns>The registry populated with every embedded built-in profile.</returns>
    /// <exception cref="ProfileValidationException">Thrown when an embedded profile is malformed.</exception>
    public static BuiltInProfiles CreateDefault()
    {
        var vRegistry = new BuiltInProfiles();
        var vAssembly = typeof(BuiltInProfiles).Assembly;
        foreach (var vResourceName in vAssembly.GetManifestResourceNames())
        {
            if (!vResourceName.Contains(EmbeddedResourceMarker, StringComparison.Ordinal) ||
                !vResourceName.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var vJson = ReadResource(vAssembly, vResourceName);
            var vProfile = ProfileJsonReader.Read(vJson, vResourceName);
            vRegistry.Register(vProfile);
        }

        // Cluster B registers AppStudio + TrStudio here — drop the JSON into
        // src/TrSetup.Core/Profiles/BuiltIn/ (auto-embedded + auto-discovered above),
        // or call vRegistry.Register(profile) explicitly for a non-file source.

        return vRegistry;
    }

    /// <summary>
    /// Registers (or replaces) a built-in profile under its <see cref="TrSetupProfile.Name"/>.
    /// </summary>
    /// <param name="aProfile">The profile to register.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="aProfile"/> is null.</exception>
    public void Register(TrSetupProfile aProfile)
    {
        ArgumentNullException.ThrowIfNull(aProfile);
        objByName[aProfile.Name] = aProfile;
    }

    /// <summary>
    /// Parses a profile JSON document, validates it, and registers it under its name — the
    /// programmatic registration path for a built-in not backed by an embedded file.
    /// </summary>
    /// <param name="aJson">The raw <c>trsetup-profile.json</c> text.</param>
    /// <param name="aSource">Optional human source label used in validation error messages.</param>
    /// <returns>The parsed, registered profile.</returns>
    /// <exception cref="ProfileValidationException">Thrown when the document is malformed or fails validation.</exception>
    public TrSetupProfile RegisterFromJson(string aJson, string? aSource = null)
    {
        var vProfile = ProfileJsonReader.Read(aJson, aSource ?? "in-memory profile");
        Register(vProfile);
        return vProfile;
    }

    /// <summary>
    /// Looks up a built-in profile by name (case-insensitive).
    /// </summary>
    /// <param name="aName">The profile name (e.g. <c>AppStudio</c>).</param>
    /// <returns>The profile, or <c>null</c> when no built-in profile has that name.</returns>
    public TrSetupProfile? Find(string aName)
    {
        if (string.IsNullOrWhiteSpace(aName))
        {
            return null;
        }

        return objByName.TryGetValue(aName, out var vProfile) ? vProfile : null;
    }

    private static string ReadResource(Assembly aAssembly, string aResourceName)
    {
        using var vStream = aAssembly.GetManifestResourceStream(aResourceName)
            ?? throw new InvalidOperationException($"Embedded profile resource '{aResourceName}' could not be opened.");
        using var vReader = new StreamReader(vStream);
        return vReader.ReadToEnd();
    }
}
