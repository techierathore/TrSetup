namespace TrSetup.Core.Settings;

/// <summary>
/// Loads and saves the small local JSON settings file (REQ-FN-005). No database — this file
/// is the only persistence in TrSetup (ADR-002).
/// </summary>
public interface ISettingsStore
{
    /// <summary>The absolute path of the settings file this store reads and writes.</summary>
    string SettingsFilePath { get; }

    /// <summary>
    /// Loads the settings. A missing file is not an error — it reports first-run with
    /// default settings so the role picker is shown.
    /// </summary>
    /// <param name="aCancellationToken">Cancels the read.</param>
    /// <returns>The settings plus the first-run flag.</returns>
    Task<SettingsLoadResult> LoadAsync(CancellationToken aCancellationToken = default);

    /// <summary>
    /// Persists the settings, creating the settings directory when needed.
    /// </summary>
    /// <param name="aSettings">The settings to save.</param>
    /// <param name="aCancellationToken">Cancels the write.</param>
    /// <returns>A task completing when the file is written.</returns>
    Task SaveAsync(TrSetupSettings aSettings, CancellationToken aCancellationToken = default);
}
