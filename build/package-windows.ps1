<#
.SYNOPSIS
    REQ-FN-039 (BRD-60) — publish TrSetup for Windows as a SELF-CONTAINED artifact.

.DESCRIPTION
    Produces a folder + zip that runs on a completely fresh Windows machine with NO .NET Desktop
    Runtime and NO Windows App SDK preinstalled. That is the point: TrSetup installs .NET, so it
    must never itself require .NET (BRD-57, the non-circular bootstrap rule).

    The self-contained switches live in src/TrSetup/TrSetup.csproj, scoped to the windows target
    platform (SelfContained, WindowsAppSDKSelfContained, RuntimeIdentifier(s)), so a plain
    `dotnet publish` already picks them up. They are NOT repeated here — one source of truth.

    WHY A ZIP AND NOT A SINGLE .EXE:
    WinUI 3 / Windows App SDK does not reliably support PublishSingleFile — native assets have to
    sit beside the executable. The honest shape for an unpackaged WinUI app is a folder, shipped
    inside a zip or an installer. If a true one-click installer is wanted later, point Inno Setup
    or WiX at the published folder; do not chase single-file.

    SIGNING: the produced exe is UNSIGNED, so SmartScreen will warn on first run
    (REQ-FN-040 / UsageGuide §1b). Authenticode signing is tracked by REQ-FN-038.

.PARAMETER Rid
    Runtime identifier: win-x64 (default) or win-arm64.

.PARAMETER OutDir
    Output directory. Defaults to <repo>/artifacts.

.EXAMPLE
    .\build\package-windows.ps1
    .\build\package-windows.ps1 -Rid win-arm64
#>
[CmdletBinding()]
param(
    [ValidateSet('win-x64', 'win-arm64')]
    [string]$Rid = 'win-x64',
    [string]$OutDir
)

$ErrorActionPreference = 'Stop'

if (-not $IsWindows) {
    Write-Error "package-windows.ps1 must run on Windows: the MAUI Windows head needs the Windows App SDK and XAML toolchain, which are not available on macOS/Linux."
    exit 1
}

$RepoRoot = Split-Path -Parent $PSScriptRoot
if (-not $OutDir) { $OutDir = Join-Path $RepoRoot 'artifacts' }

$Project = Join-Path $RepoRoot 'src/TrSetup/TrSetup.csproj'
$Tfm     = 'net10.0-windows10.0.19041.0'
$Config  = 'Release'
$Stage   = Join-Path $OutDir "TrSetup-$Rid"

New-Item -ItemType Directory -Force -Path $OutDir | Out-Null
if (Test-Path $Stage) { Remove-Item -Recurse -Force $Stage }

Write-Host "Publishing self-contained ($Rid) ..." -ForegroundColor Cyan
dotnet publish $Project -f $Tfm -c $Config -r $Rid -o $Stage
if ($LASTEXITCODE -ne 0) { Write-Error "dotnet publish failed with exit code $LASTEXITCODE"; exit $LASTEXITCODE }

$Exe = Join-Path $Stage 'TrSetup.exe'
if (-not (Test-Path $Exe)) { Write-Error "Publish completed but TrSetup.exe is missing from $Stage"; exit 1 }

# Self-containment sanity check: the .NET runtime and the Windows App SDK must both be present
# beside the exe. If either is absent the artifact is still framework-dependent and will fail on a
# clean machine — exactly the bug this requirement exists to prevent, so fail loudly here.
$HasRuntime  = Test-Path (Join-Path $Stage 'System.Private.CoreLib.dll')
$HasAppSdk   = @(Get-ChildItem -Path $Stage -Filter 'Microsoft.WindowsAppRuntime*' -ErrorAction SilentlyContinue).Count -gt 0
$HasWinUi    = Test-Path (Join-Path $Stage 'Microsoft.UI.Xaml.dll')

Write-Host ""
Write-Host "Self-containment check:" -ForegroundColor Cyan
Write-Host ("  .NET runtime (System.Private.CoreLib.dll) : {0}" -f $(if ($HasRuntime) { 'present' } else { 'MISSING' }))
Write-Host ("  Windows App SDK (Microsoft.WindowsAppRuntime*): {0}" -f $(if ($HasAppSdk) { 'present' } else { 'MISSING' }))
Write-Host ("  WinUI (Microsoft.UI.Xaml.dll)             : {0}" -f $(if ($HasWinUi) { 'present' } else { 'MISSING' }))

if (-not ($HasRuntime -and $HasAppSdk -and $HasWinUi)) {
    Write-Error "Artifact is NOT self-contained — it would fail on a fresh machine. Check SelfContained / WindowsAppSDKSelfContained in TrSetup.csproj (REQ-FN-039)."
    exit 1
}

$Version = (Get-Item $Exe).VersionInfo.ProductVersion
if (-not $Version) { $Version = '1.0' }
$Zip = Join-Path $OutDir "TrSetup-$Version-Windows-$Rid.zip"
if (Test-Path $Zip) { Remove-Item -Force $Zip }

Write-Host ""
Write-Host "Zipping to $Zip ..." -ForegroundColor Cyan
Compress-Archive -Path (Join-Path $Stage '*') -DestinationPath $Zip

$SizeMb = [math]::Round((Get-Item $Zip).Length / 1MB, 1)
Write-Host ""
Write-Host "Built: $Zip ($SizeMb MB)" -ForegroundColor Green
Write-Host ""
Write-Host "UNSIGNED BUILD (REQ-FN-038 open): SmartScreen will show 'Windows protected your PC'."
Write-Host "Users click 'More info' then 'Run anyway' — see UsageGuide section 1b."
