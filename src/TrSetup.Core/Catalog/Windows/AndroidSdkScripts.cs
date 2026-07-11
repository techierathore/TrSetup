namespace TrSetup.Core.Catalog.Windows;

/// <summary>
/// Shared PowerShell fragments for locating the Android SDK on the Windows device host
/// (standard locations: <c>%ANDROID_HOME%</c> when set, else <c>%LocalAppData%\Android\Sdk</c>).
/// </summary>
internal static class AndroidSdkScripts
{
    /// <summary>
    /// Script prologue that resolves <c>$vSdk</c>, <c>$vSdkManager</c> and <c>$vAvdManager</c>.
    /// </summary>
    internal const string Locator =
        "$vSdk = \"$env:LocalAppData\\Android\\Sdk\"\n" +
        "if ($env:ANDROID_HOME) { $vSdk = $env:ANDROID_HOME }\n" +
        "$vSdkManager = Join-Path $vSdk 'cmdline-tools\\latest\\bin\\sdkmanager.bat'\n" +
        "$vAvdManager = Join-Path $vSdk 'cmdline-tools\\latest\\bin\\avdmanager.bat'\n";

    /// <summary>The API-34 emulator system-image package the reference AVD boots.</summary>
    internal const string Api34ImagePackage = "system-images;android-34;google_apis;x86_64";

    /// <summary>The pinned official Android cmdline-tools (Windows) download URL.</summary>
    internal const string CmdlineToolsUrl =
        "https://dl.google.com/android/repository/commandlinetools-win-11076708_latest.zip";

    /// <summary>
    /// The embedded <c>start-android-verify.ps1</c> helper written to the Windows user profile
    /// by the win.verify-helper fixer — boots the reference AVD headless and starts Appium.
    /// </summary>
    internal const string VerifyHelperScript =
        "# start-android-verify.ps1 — TrSetup managed helper: boot the reference AVD + Appium.\n" +
        "$ErrorActionPreference = 'Stop'\n" +
        "$vSdk = if ($env:ANDROID_HOME) { $env:ANDROID_HOME } else { \"$env:LocalAppData\\Android\\Sdk\" }\n" +
        "$vEmulator = Join-Path $vSdk 'emulator\\emulator.exe'\n" +
        "Start-Process -FilePath $vEmulator -ArgumentList '-avd Pixel_API_34 -no-window -no-snapshot' -WindowStyle Hidden\n" +
        "Start-Process -FilePath 'appium' -ArgumentList '--address 0.0.0.0 --port 4723' -WindowStyle Hidden\n" +
        "Write-Output 'start-android-verify: emulator + appium launching'\n";
}
