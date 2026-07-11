namespace TrSetup.Core.Settings;

/// <summary>
/// The result of loading settings: the settings themselves plus whether this is a first
/// run (no settings file existed — drives the first-run role picker).
/// </summary>
/// <param name="Settings">The loaded settings, or defaults when the file was missing/unreadable.</param>
/// <param name="IsFirstRun"><c>true</c> when no settings file existed yet.</param>
public sealed record SettingsLoadResult(TrSetupSettings Settings, bool IsFirstRun);
