using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace TrSetup.Core.Settings;

/// <summary>
/// Default <see cref="ISettingsStore"/>: one small JSON file in a per-OS location —
/// <c>%APPDATA%\TrSetup\settings.json</c> on Windows, <c>~/.trsetup/settings.json</c>
/// elsewhere — with a path override for tests.
/// </summary>
public sealed class JsonSettingsStore : ISettingsStore
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly ILogger<JsonSettingsStore> objLogger;

    /// <summary>
    /// Creates the store.
    /// </summary>
    /// <param name="aSettingsFilePath">
    /// Override for the settings file path (used by tests); <c>null</c> uses the per-OS default
    /// from <see cref="GetDefaultSettingsPath"/>.
    /// </param>
    /// <param name="aLogger">Optional logger; a null logger is used when omitted.</param>
    public JsonSettingsStore(string? aSettingsFilePath = null, ILogger<JsonSettingsStore>? aLogger = null)
    {
        SettingsFilePath = aSettingsFilePath ?? GetDefaultSettingsPath();
        objLogger = aLogger ?? NullLogger<JsonSettingsStore>.Instance;
    }

    /// <inheritdoc />
    public string SettingsFilePath { get; }

    /// <summary>
    /// Computes the per-OS default settings path: <c>%APPDATA%\TrSetup\settings.json</c> on
    /// Windows, <c>~/.trsetup/settings.json</c> on Linux/macOS.
    /// </summary>
    /// <returns>The absolute default settings file path for this OS.</returns>
    public static string GetDefaultSettingsPath()
    {
        if (OperatingSystem.IsWindows())
        {
            var vAppData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            return Path.Combine(vAppData, "TrSetup", "settings.json");
        }

        var vHome = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        return Path.Combine(vHome, ".trsetup", "settings.json");
    }

    /// <inheritdoc />
    public async Task<SettingsLoadResult> LoadAsync(CancellationToken aCancellationToken = default)
    {
        if (!File.Exists(SettingsFilePath))
        {
            objLogger.LogInformation("No settings file at {Path} — first run.", SettingsFilePath);
            return new SettingsLoadResult(new TrSetupSettings(), IsFirstRun: true);
        }

        await using var vStream = File.OpenRead(SettingsFilePath);
        var vSettings = await JsonSerializer
            .DeserializeAsync<TrSetupSettings>(vStream, SerializerOptions, aCancellationToken)
            .ConfigureAwait(false);
        return new SettingsLoadResult(Normalize(vSettings ?? new TrSetupSettings()), IsFirstRun: false);
    }

    /// <summary>
    /// Restores the case-insensitive comparers the settings model declares. The serializer
    /// constructs fresh collections for the settable properties, which come back with the DEFAULT
    /// ordinal comparer — so a reloaded <c>Endpoints["appmanagerurl"]</c> or a trust opt-in whose
    /// casing differs from the profile's key would silently miss after a restart, while working
    /// perfectly in the session that saved it.
    /// </summary>
    /// <param name="aSettings">The freshly deserialized settings.</param>
    /// <returns>The same instance with case-insensitive lookup collections.</returns>
    private static TrSetupSettings Normalize(TrSetupSettings aSettings)
    {
        aSettings.Endpoints = new Dictionary<string, string>(aSettings.Endpoints, StringComparer.OrdinalIgnoreCase);
        aSettings.AppRepoPaths = new Dictionary<string, string>(aSettings.AppRepoPaths, StringComparer.OrdinalIgnoreCase);
        aSettings.TrustedSelfSignedEndpoints =
            new HashSet<string>(aSettings.TrustedSelfSignedEndpoints, StringComparer.OrdinalIgnoreCase);
        return aSettings;
    }

    /// <inheritdoc />
    public async Task SaveAsync(TrSetupSettings aSettings, CancellationToken aCancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(aSettings);
        var vDirectory = Path.GetDirectoryName(SettingsFilePath);
        if (!string.IsNullOrEmpty(vDirectory))
        {
            Directory.CreateDirectory(vDirectory);
        }

        await using var vStream = File.Create(SettingsFilePath);
        await JsonSerializer
            .SerializeAsync(vStream, aSettings, SerializerOptions, aCancellationToken)
            .ConfigureAwait(false);
        objLogger.LogInformation("Settings saved to {Path}.", SettingsFilePath);
    }
}
