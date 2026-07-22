# TrSetup — Deployment Guide (Build the artifact · Ship it · Run it on a fresh machine)

> **Who this is for.** This guide covers **production deployment**: how a maintainer turns the repo into a downloadable artifact, and how an end user with **no developer tooling at all** gets from that download to a working board. It is the release-side companion to the two existing guides — `docs/TrSetup-BuildAndRun-Guide.md` (how a developer builds and runs from source) and `docs/TrSetup-UsageGuide.md` (how to operate, configure and UAT the app). Where those already cover a step, this guide links rather than repeats.
>
> **Verification status of this document (read before you trust a Windows step).** Every command below was read out of the real scripts and csproj. The **macOS path is proven end to end** on this repo: artifact built, packaged, signature inspected, self-containment counted, board driven live. The **Windows path is NOT proven** — no Windows artifact has ever been produced or launched, on any machine, at any point in this project. Windows sections are marked **⚠ UNTESTED END-TO-END** wherever they appear. They are the correct commands, not a demonstrated outcome.
>
> Tracked as **REQ-FN-036 / BRD-57** (zero-prerequisite distributable), **REQ-FN-037 / BRD-58** (Mac `.dmg`), **REQ-FN-039 / BRD-60** (Windows self-contained artifact), **REQ-FN-038 / BRD-59** (signing — *Blocked*), **REQ-FN-040 / BRD-61** (documented unsigned first-run trust), **REQ-FN-041** (CI pipeline).

## The one rule that shapes everything here

**TrSetup installs .NET. Therefore TrSetup must never require .NET.**

That is the non-circular bootstrap rule (BRD-57). An artifact that needs the .NET Desktop Runtime, the MAUI workload, or the Windows App SDK already present is broken *by definition* — it demands the exact machine state it exists to repair. Every packaging decision below (self-contained publish, bundled Windows App SDK, no trimming, no single-file, an accepted ~150 MB artifact) follows from that one rule, and the build scripts **hard-fail** rather than ship an artifact that violates it.

---

## 1. Artifact matrix

| | **macOS** | **Windows** ⚠ UNTESTED END-TO-END |
|---|---|---|
| Ships as | `TrSetup-<version>-macOS.dmg` — a mountable disk image with the drag-to-Applications layout | `TrSetup-<version>-Windows-<rid>.zip` — a **folder** of files, zipped |
| Contains | `TrSetup.app` + an `/Applications` symlink | `TrSetup.exe` plus every runtime DLL and native asset beside it |
| Head | MAUI Blazor Hybrid, Mac Catalyst (`net10.0-maccatalyst`) | MAUI Blazor Hybrid, unpackaged WinUI 3 (`net10.0-windows10.0.19041.0`, `WindowsPackageType=None`) |
| Architectures | `maccatalyst-arm64` **and** `maccatalyst-x64` — the build lipos them into one **universal** `.app`; the shipped bundle runs natively on Apple Silicon and Intel | `win-x64` (default) and `win-arm64` — **separate zips**, one per RID |
| Bundles the .NET runtime | ✅ **Verified** — 56 `System.*` assemblies in `TrSetup.app/Contents/MonoBundle` | Intended via `SelfContained=true`; asserted at package time by `System.Private.CoreLib.dll` sitting beside the exe. **Never observed on a real publish.** |
| Bundles the UI runtime | n/a (Catalyst uses the system WebKit `WKWebView`) | Intended via `WindowsAppSDKSelfContained=true`; asserted by `Microsoft.WindowsAppRuntime*` + `Microsoft.UI.Xaml.dll` beside the exe |
| Size | **`.app` 156 MB · `.dmg` 49 MB** (measured on this repo, version 1.0) | **Unmeasured.** Expect **~150 MB+** unzipped — self-contained MAUI + WinUI. Size is an accepted trade for the bootstrap rule, not a defect. |
| Signed | **Ad-hoc only** (`Signature=adhoc`, `TeamIdentifier=not set`) — not notarized. See [§5](#os-trust-gates-what-every-user-will-hit). | **Unsigned.** No Authenticode certificate is configured anywhere in the repo. |
| Minimum OS | `LSMinimumSystemVersion` 12.0; csproj `SupportedOSPlatformVersion` 15.0 for maccatalyst | Windows 10 build 17763 (`SupportedOSPlatformVersion` / `TargetPlatformMinVersion` 10.0.17763.0) |

### Why the Windows artifact is a folder, not a single `.exe`

Two properties are deliberately **not** enabled in `src/TrSetup/TrSetup.csproj`, and you should not add them:

- **`PublishSingleFile`** — WinUI 3 / Windows App SDK does not reliably support single-file packing; native assets must sit beside the executable. The honest shape for an unpackaged WinUI app is a folder. If a one-click installer is wanted later, point **Inno Setup** or **WiX** at the published folder — do not chase single-file.
- **`PublishTrimmed`** — trimming is unsupported for MAUI/WinUI and silently breaks XAML and reflection paths. A trimmed build fails at runtime, not at build time, which is the worst possible failure mode for a distributable.

> **The lone `.exe` will not run.** Users must extract and keep the **whole folder**. This is the single most likely support question on Windows.

### What is *not* an artifact

`TrSetup.Web` is a **non-shipping test host** (REQ-FN-034). It is used by the Playwright harness and as the no-build-tools "copy the folder to the Mac" vehicle described in `TrSetup-UsageGuide.md` §3 Option A. **Never publish it as a product download.** Likewise, any lowercase `trsetup` folder under `publish/<rid>/` is a stale artifact of the deleted Spectre CLI head — delete it, do not ship it.

---

## 2. Building each artifact

### 2.1 Which host OS each build requires

| Artifact | Buildable on macOS | Buildable on Windows | Buildable on Linux/WSL |
|---|---|---|---|
| Mac `.dmg` | ✅ **required** | ❌ | ❌ |
| Windows `.zip` | ❌ **impossible — see below** | ✅ **required** | ❌ |

**There is no cross-build path in either direction.** A MAUI head compiles against its OS's native SDKs; Visual Studio's "pair to Mac" is iOS-only and does not produce a Catalyst desktop `.app`.

**Why the Windows artifact cannot be published from a Mac** — this was attempted twice on this repo and failed both times, and the reason is structural, not a missing flag:

1. `TrSetup.csproj` carries a deliberate guard that **replaces** the target-framework list on macOS:
   ```xml
   <TargetFrameworks>net10.0-windows10.0.19041.0</TargetFrameworks>
   <TargetFrameworks Condition="$([MSBuild]::IsOSPlatform('OSX'))">net10.0-maccatalyst</TargetFrameworks>
   ```
   It must replace rather than append — otherwise restore still evaluates the Windows TFM on the Mac and fails with **`NETSDK1100`** (Windows targeting not enabled).
2. Forcing the list back with `-p:TargetFrameworks=…` propagates that as a **global property** into `TrSetupUI` and `TrSetup.Core`, corrupting the project graph and failing with **`NETSDK1005`** (assets file has no target for `net10.0`).

`build/package-windows.ps1` refuses up front for the same reason, with an explicit message rather than a confusing MSBuild error:

```
package-windows.ps1 must run on Windows: the MAUI Windows head needs the Windows App SDK
and XAML toolchain, which are not available on macOS/Linux.
```

**A Windows host is required. There is no workaround.** Use a Windows machine, a Windows VM, or the `windows-latest` CI job ([§2.4](#building-both-artifacts-in-ci-req-fn-041)).

### 2.2 Build the macOS `.dmg` — on a Mac

**Prerequisites on the build Mac** (see `TrSetup-BuildAndRun-Guide.md` §3 Option B for detail): **full Xcode** with the licence accepted — the Command Line Tools alone are not enough — the **.NET 10 SDK**, the **MAUI workload** (`dotnet workload install maui-maccatalyst`), and access to the private **TrBlazeUI** package feed.

```bash
cd <repo-root>
dotnet build src/TrSetup/TrSetup.csproj -f net10.0-maccatalyst -c Release
./build/package-mac.sh
```

`package-mac.sh` takes an optional output directory (default `artifacts/`). It reads `CFBundleShortVersionString` from the built bundle so the filename is traceable to a build, stages the `.app` next to an `/Applications` symlink, and runs `hdiutil create -format UDZO`. Output: `artifacts/TrSetup-<version>-macOS.dmg`.

The script refuses on a non-Darwin host, and refuses if the `.app` is missing — telling you the exact build command to run first.

**Verify self-containment before you publish it** (this is the BRD-57 gate, not a formality):

```bash
APP="src/TrSetup/bin/Release/net10.0-maccatalyst/TrSetup.app"
ls "$APP/Contents/MonoBundle/" | grep -c '^System\.'    # must be ≥10; currently 56
codesign -d -vvv "$APP" 2>&1 | grep -E 'Signature|TeamIdentifier'
du -sh "$APP"
```

Confirmed on this repo: **56** `System.*` assemblies, `Signature=adhoc`, `TeamIdentifier=not set`, bundle **156 MB**, `.dmg` **49 MB**. A packaged `.dmg` has been launched on a machine whose only .NET SDK lived under `~/.dotnet` rather than a system install — which is the practical demonstration that the bundle carries its own runtime.

**Verify the image itself mounts with the right layout:**

```bash
hdiutil attach artifacts/TrSetup-1.0-macOS.dmg      # volume shows TrSetup.app + Applications symlink
hdiutil detach /Volumes/TrSetup
```

### 2.3 Build the Windows zip — on Windows ⚠ UNTESTED END-TO-END

> **Nobody has ever run this successfully.** The csproj properties were verified only by **MSBuild property evaluation on a Mac** (`-p:EnableWindowsTargeting=true`), which proves the graph *resolves* `SelfContained=true`, `WindowsAppSDKSelfContained=true`, `RuntimeIdentifier=win-x64`, `RuntimeIdentifiers=win-x64;win-arm64`, and that `-p:RuntimeIdentifier=win-arm64` correctly overrides the conditional default. It proves **nothing** about whether the publish succeeds, whether the assertion passes, or whether the exe starts. Treat the first real run as an experiment, not a release.

**Prerequisites on the build Windows machine:** .NET 10 SDK, `dotnet workload install maui`, PowerShell, and access to the TrBlazeUI feed.

```powershell
cd <repo-root>
.\build\package-windows.ps1                  # win-x64 (default)
.\build\package-windows.ps1 -Rid win-arm64   # the only other accepted value
```

Parameters are exactly `-Rid` (`win-x64` | `win-arm64`, validated) and `-OutDir` (default `<repo>\artifacts`). There are no other flags.

Internally the script runs one publish — the self-containment switches are **not** repeated on the command line, because they live in the csproj as the single source of truth:

```powershell
dotnet publish <repo>\src\TrSetup\TrSetup.csproj -f net10.0-windows10.0.19041.0 -c Release -r <rid> -o <stage>
```

**Then it hard-asserts self-containment and fails the build if any of the three is missing** — a framework-dependent artifact never gets zipped:

| Asserted file beside `TrSetup.exe` | Proves |
|---|---|
| `System.Private.CoreLib.dll` | the .NET runtime is bundled |
| `Microsoft.WindowsAppRuntime*` | the Windows App SDK is bundled |
| `Microsoft.UI.Xaml.dll` | the WinUI 3 runtime is bundled |

On failure it emits: *"Artifact is NOT self-contained — it would fail on a fresh machine. Check SelfContained / WindowsAppSDKSelfContained in TrSetup.csproj (REQ-FN-039)."*

On success it reads the exe's `ProductVersion` and produces `artifacts\TrSetup-<version>-Windows-<rid>.zip`, printing the size and the SmartScreen warning.

**Verify manually as well** (do not rely on the script alone for a first release):

```powershell
Test-Path .\artifacts\TrSetup-win-x64\System.Private.CoreLib.dll
Get-ChildItem .\artifacts\TrSetup-win-x64 -Filter 'Microsoft.WindowsAppRuntime*'
Test-Path .\artifacts\TrSetup-win-x64\Microsoft.UI.Xaml.dll
```

**The only test that actually closes BRD-57/BRD-60** is the clean-image launch: extract the zip on a Windows machine with **no .NET Desktop Runtime and no Windows App SDK installed**, run `TrSetup.exe`, and confirm the board renders. Until someone does that, REQ-FN-036 stays `PARTIAL`.

### 2.4 Building both artifacts in CI (REQ-FN-041)

`.github/workflows/build-artifacts.yml` builds both without any Apple Developer account, because the Catalyst build uses codesign's ad-hoc identity and therefore needs no certificate and no signing secrets.

| Job | Runner | Does |
|---|---|---|
| `macos` | `macos-15` (pinned, not `-latest` — the image ships the Xcode Catalyst needs) | installs .NET + `maui-maccatalyst`, runs unit tests, builds the `.app`, runs `package-mac.sh`, **asserts ≥10 bundled `System.*` assemblies** |
| `windows` | `windows-latest`, matrix `win-x64` + `win-arm64`, `fail-fast: false` so an arm64 failure cannot hide a good x64 artifact | installs .NET + `maui`, runs unit tests, runs `package-windows.ps1` (which carries its own hard assertion) |
| `release` | `ubuntu-latest`, only on a `v*` tag | downloads both artifacts and publishes a GitHub Release whose body carries the unsigned-install trust steps verbatim |

Triggers: manual `workflow_dispatch`, a `v*` tag, and pull requests touching `src/**`, `build/**`, or the workflow file.

> **⚠ One repository secret is required before the first run: `GH_PACKAGES_PAT`** — a PAT with `read:packages` for the `techierathore` feed. `TrSetupUI` depends on **TrBlazeUI 1.0.7**, which is not on nuget.org, and the default `GITHUB_TOKEN` cannot read another owner's package feed. Without it, restore fails in every job. Set it under **Settings → Secrets and variables → Actions**.
>
> **⚠ This workflow has never executed.** It cannot run until the secret exists and the branch is pushed. Its YAML structure was validated locally; its behaviour was not.

### 2.5 Cutting a release

1. Confirm the version. `ApplicationDisplayVersion` (`1.0`) and `ApplicationVersion` (`1`) live in `src/TrSetup/TrSetup.csproj`; the Mac artifact filename derives from the former via the bundle's `CFBundleShortVersionString`, and the Windows filename from the exe's `ProductVersion`.
2. Tag `vX.Y.Z` and push the tag. The `release` job publishes both artifacts with install instructions in the body.
3. **Do not publish a release whose Windows zip has not been launched on a clean image.** Until that happens, mark the Windows asset as pre-release/experimental in the release notes.

---

## 3. Release and distribution — what "download and run" looks like

A user needs **exactly one file** and **no developer tooling**.

### macOS

1. Download `TrSetup-<version>-macOS.dmg` from the GitHub Release.
2. Double-click it — the volume mounts with the standard layout.
3. Drag **TrSetup** onto the **Applications** shortcut.
4. **Do the one-time trust step in [§5](#os-trust-gates-what-every-user-will-hit) before double-clicking.** Skipping it produces a block dialog that looks like a corrupt download.
5. Open TrSetup from Applications or Launchpad.

Nothing else is installed: no .NET, no Xcode, no MAUI workload, no Homebrew.

### Windows ⚠ UNTESTED END-TO-END

1. Download `TrSetup-<version>-Windows-win-x64.zip` (or `win-arm64` on an ARM device).
2. Right-click the zip → **Properties** → tick **Unblock** → **OK**. *(This clears the Mark-of-the-Web from the archive before extraction and avoids per-file blocks inside it.)*
3. Extract to a stable location — e.g. `C:\Program Files\TrSetup` or `%LOCALAPPDATA%\TrSetup`. **Extract the whole folder; do not pull the exe out on its own.**
4. Run `TrSetup.exe` and clear SmartScreen once — see [§5](#os-trust-gates-what-every-user-will-hit).
5. Optionally pin it or create a Start Menu shortcut. There is no installer and no MSIX — `WindowsPackageType=None` is deliberate (ADR-001), and there is correspondingly **no uninstaller**: deleting the folder is the uninstall.

> There is **no auto-update mechanism**. Upgrading means downloading the new artifact and replacing the app / folder. `~/.trsetup/settings.json` and `%APPDATA%\TrSetup\settings.json` live outside the artifact, so settings survive an upgrade.

---

## 4. First-run configuration

The full reference is `TrSetup-UsageGuide.md` §5. This section covers only what a *deployed* user must decide on day one.

### 4.1 Pick the machine role(s)

On first launch the role picker (`/setup`) appears because no settings file exists yet. Tick only the roles this machine genuinely plays:

| Role | Pick it when this machine… |
|---|---|
| **Agent host (WSL)** | runs the AI agent + verification stack — .NET SDK, Playwright, headless Chromium, the `winrun` bridge |
| **Device host (Windows)** | hosts the Android emulator + Appium (`uiautomator2`) |
| **Device host (Mac)** | serves Appium with the `xcuitest` + `mac2` drivers on the LAN |
| **App runner (Mac)** | builds and runs the portfolio apps — Catalyst builds, SDKs, workloads |
| *"I develop natively"* | is not behind the WSL bridge — this **drops** the mirrored-networking and `winrun` checks (BRD-8) |

Rows belonging to roles you did not pick render `○ N/A`. **That is by design, not an error** — it is the most commonly misread part of the board.

### 4.2 Pick the application profile

**AppStudio**, **TrStudio**, or none (framework-only). The board re-scopes instantly on change — no restart. Switch later via **Settings → Selected application** or the board header's app switcher.

### 4.3 Point this machine at App Manager (multi-machine deployments)

**This is the setting most deployed users will need, and the one that silently blocks them if missed.**

The `appstudio.appmanager-api` check defaults to `https://localhost:5101/`, which is correct **only** when App Manager runs on the same box. On a real two-machine setup — a Mac app-runner plus a Windows device-host running App Manager — the Mac's own `localhost:5101` has nothing on it, so the row can never go green and the Catalyst build fixer stays gated behind a prerequisite the user has no way to satisfy.

**Settings → Endpoints → App Manager API URL.** Enter an absolute `http(s)://` URL (validated in-app), e.g. `https://192.168.1.14:5101/health`. Persisted as `Endpoints.AppManagerUrl` in the settings file; it **replaces** the profile default for this machine only.

- **Point it at a URL that answers 2xx.** App Manager serves `/health`. Its root `/` answers **404**, which reads as *reachable but not healthy* (amber) — enough to not block, but not a green row.
- **Self-signed certificates:** a LAN App Manager typically serves the ASP.NET development certificate (`CN=localhost`, self-signed), which fails validation on **both** issuer and hostname when reached by IP. Turn on **"Trust a self-signed certificate for this endpoint"** — an explicit, per-endpoint, **off-by-default** opt-in. TrSetup then skips certificate validation **for that endpoint alone** and says so in the row's evidence. Every other probe keeps full validation, and a profile's built-in default URL is *always* fully validated — a stale opt-in can never weaken a built-in probe.

A correctly configured row reads, verbatim:

```
Endpoint https://192.168.1.14:5101/health [configured in Settings → Endpoints ['AppManagerUrl'],
self-signed certificate explicitly trusted] answered 200.
```

The evidence always names the URL **and** its provenance, so a `connection refused (localhost:5101)` can no longer be misread as "the service is down" when the real cause is "pointed at the wrong host".

### 4.4 Set `APPMANAGER_API_KEY` — per OS, durably

**TrSetup never stores secrets** (ADR-008). The settings file holds addresses and URLs only. The `appstudio.appmanager-secret` check verifies the environment variable is **present**; it never reads, displays, logs, or exports the value, and the report prints `present (value never shown)`.

The variable must therefore be set **in the environment TrSetup itself is launched from**, and it must survive a reboot. A value exported in a one-off terminal will not be visible to a GUI app launched from the Dock or Start Menu.

**Windows** — set it for the user account, then **restart** the app (and Explorer, or sign out and back in, so the GUI shell inherits it):

```powershell
[Environment]::SetEnvironmentVariable('APPMANAGER_API_KEY', '<your-key>', 'User')
```

Or **System Properties → Advanced → Environment Variables → User variables → New**.

**macOS (GUI launch from Finder/Dock/Launchpad)** — a `.app` launched by `launchd` does **not** inherit your shell profile. Register it with the user's launchd session, then log out and back in:

```bash
launchctl setenv APPMANAGER_API_KEY '<your-key>'
```

`launchctl setenv` does not survive a reboot on its own. For a durable setting, add it to a user LaunchAgent plist under `~/Library/LaunchAgents/` that runs `launchctl setenv` at login, or launch TrSetup from a terminal that has the variable exported.

**macOS / Linux / WSL (terminal launch — the browser board and CLI-driven runs)** — export it in your shell profile:

```bash
echo 'export APPMANAGER_API_KEY="<your-key>"' >> ~/.zshrc   # or ~/.bashrc
```

> **Verify it took effect the same way TrSetup does:** re-run the board and confirm the *AppManager applicationId/API key configured* row goes green. If the terminal shows the variable but the row stays red, the GUI app is not inheriting it — that is the launchd/Explorer inheritance problem above, not a TrSetup bug.

### 4.5 Optionally pre-seed settings instead of using the picker

For a fleet or a headless machine, drop a settings file in place **before first launch** — `TrSetup-UsageGuide.md` §5.3 lists five ready-made samples in `docs/samples/`. Settings live at `%APPDATA%\TrSetup\settings.json` on Windows and `~/.trsetup/settings.json` on macOS/Linux/WSL; the Settings screen always shows the authoritative path for the running machine.

---

## 5. OS trust gates — what every user will hit

> **Neither artifact is code-signed for distribution today.** This was verified, not assumed. **Every** user of **every** current release will hit an OS block on first launch. A release that ships without these instructions in the release body is indistinguishable from a broken download.

### 5.1 Current signing posture (verified)

| | macOS | Windows |
|---|---|---|
| Signing | **Ad-hoc only.** `codesign -d -vvv` on the built bundle reports `Signature=adhoc`, `TeamIdentifier=not set`, `CodeDirectory flags=0x2(adhoc)`. Set by `<CodesignKey>-</CodesignKey>` in the csproj — codesign's ad-hoc identity, which requires no Apple certificate. | **Unsigned.** No Authenticode certificate, `signtool` invocation, or signing property exists anywhere in the csproj, the packaging script, or the CI workflow. |
| Notarized | **No.** Requires a paid Apple Developer account the project does not have. | n/a |
| Gatekeeper verdict | `spctl -a -t exec` → **`rejected`** on the built bundle. Reproduced on a simulated download: setting `com.apple.quarantine` on the `.dmg` as Safari would, mounting it and copying the app out **propagates the flag to the `.app`**, and the app will not open. | SmartScreen shows *"Windows protected your PC"* on an unsigned exe with Mark-of-the-Web. **Predicted from posture, not observed** — no Windows artifact exists to test. |

### 5.2 macOS — the one-time trust step

Install first (open the `.dmg`, drag to Applications), then do **either** of these **once**:

**Option A — right-click, no terminal:**
1. Open **Applications** in Finder.
2. **Right-click** (or Control-click) **TrSetup** → **Open**.
3. Click **Open** in the warning dialog.

**Option B — terminal, one line:**
```bash
xattr -dr com.apple.quarantine /Applications/TrSetup.app
```
*(Verified: applying this to a quarantined copy cleared the flag and the app launched.)*

macOS remembers the choice; afterwards it launches normally from the Dock or Launchpad. **Double-clicking before doing one of these just shows the block** — do the trust step first.

If it still refuses, allow it once via **System Settings → Privacy & Security** (an "Open Anyway" button appears there after a blocked attempt).

### 5.3 Windows — SmartScreen ⚠ UNTESTED END-TO-END

1. Right-click the downloaded **zip** → **Properties** → **Unblock** → **OK**, then extract. *(Doing this on the archive avoids Mark-of-the-Web being stamped on every extracted file.)*
2. Run `TrSetup.exe`. If SmartScreen shows *"Windows protected your PC"*: click **More info** → **Run anyway**.

Two adjacent Windows behaviours worth knowing:

- **"This program might not have installed correctly."** Because the exe is named `TrSetup.exe` (it contains "Setup"), Windows' Program Compatibility Assistant would false-flag it as an unfinished installer. Current builds embed `Platforms\Windows\app.manifest` (a `<compatibility>` block plus `asInvoker <trustInfo>`, wired via a windows-scoped `<ApplicationManifest>`) that exempts the exe from that heuristic. TrSetup is not an installer. If an older build already triggered the dialog once, click **"This program installed correctly"** to clear Windows' cached entry.
- **Antivirus / enterprise policy.** A large unsigned exe that shells out to install SDKs is a plausible heuristic-AV target and may be quarantined outright, or blocked by WDAC/AppLocker in a managed environment. Not observed — flagged as a realistic risk of shipping unsigned.

### 5.4 This is an open production gap — recommendation

**REQ-FN-038 / BRD-59 is `Blocked`, parked by owner decision (2026-07-20): no Apple Developer account for now.** The defect is real and reproduced, not theoretical, and it cannot be closed by an agent — it needs a purchased certificate.

For distribution beyond the owner's own machines, close it in this order:

1. **macOS — Developer ID + notarization + stapling.** Requires an Apple Developer Program membership (~$99/yr). Replace `<CodesignKey>-</CodesignKey>` with the Developer ID Application identity, sign with the hardened runtime, submit the `.dmg` via `notarytool`, then `xattr -dr` disappears from every user's life and §5.2 can be deleted outright.
2. **Windows — Authenticode.** Sign `TrSetup.exe` (and ideally the zip contents) with an OV or, better, an **EV** certificate — EV grants immediate SmartScreen reputation, whereas an OV certificate must accumulate reputation and will still warn for a while. Add a `signtool` step to `package-windows.ps1` after the self-containment assertion and before the zip.
3. Store both certificates as CI secrets and sign in the workflow, so no unsigned artifact can ever reach a Release.

Until then, **the trust steps must appear in the release body**, not only in the docs. They already do — `.github/workflows/build-artifacts.yml`'s `release` job writes them verbatim into the release notes, and both packaging scripts print them at the end of a successful build.

---

## 6. Verifying a successful deployment

Run this on the deployed machine, in order. It takes about two minutes and distinguishes "the window opened" from "the app actually works".

1. **The window opens and shows the board.** On first run you get the role picker (`/setup`) instead — complete it, then you land on the board. A native window titled **TrSetup**.
2. **Rows render, grouped, with real evidence.** Not a blank board. Every row shows an icon **and** a word — `Pass` / `Warn` / `Fail` / `N/A` — plus an evidence line naming the command or path it looked at (`Node.js v22.23.1 present ($ node --version).`). A row with a status but no evidence text is a bug worth reporting.
3. **Profile rows for your selected application are present** — e.g. `appstudio.*` rows when AppStudio is selected. If the board shows framework-core rows only, no application is selected (Settings → Selected application).
4. **Press "Re-check all" and watch statuses stream.** The sweep should complete in well under 30 s.
5. **No row is left `Pending`.** This is the sharpest single signal. Every row must settle to a real verdict; text like *"not yet detected"* or *"never detected"* lingering after a sweep means a detect is hanging, not that the machine is fine. (Historically this was a real defect — an unbounded sequential prerequisite gate — now bounded by `CheckCatalog.PrerequisiteProbeTimeout` at 3.5 s per prerequisite, under the engine's 5 s row budget.)
6. **Click a failing row.** The detail sheet must populate: Explain, Evidence, Fix preview showing the *literal* command, and last run output. Manual-only checks show guidance and **no** Fix button — that is correct behaviour.
7. **Settings shows the real settings-file path** for this machine, and — for a multi-machine deployment — the App Manager row's evidence names the URL you configured **and its provenance**, not `localhost`.
8. **Export report** (`/report`) → produces `.md` + `.html`. **Search both for any secret you configured: zero hits.** `env-secret` rows must read `present (value never shown)`. This is the ADR-008 / REQ-NFR-002 guarantee and is worth checking once per deployment.

**Reference result — this was run live while writing this guide** (macOS Catalyst engine, roles *Device host (Mac)* + *App runner (Mac)* + *native dev*, AppStudio profile; canonical context #3 from `TrSetup-UsageGuide.md` §8):

> 16 rows rendered and **all settled — none Pending**: 11 `Pass`, 1 `Warn`, 4 `Fail`. Passing rows carried live evidence (Xcode 26.6, .NET SDK 10.0.302, Node v22.23.1, Appium 3.5.2 with `xcuitest@11.17.7` + `mac2@4.0.4`, iOS 26.5 simulator runtime). The configured App Manager override went green with `Endpoint https://192.168.1.14:5101/health [configured in Settings → Endpoints ['AppManagerUrl'], self-signed certificate explicitly trusted] answered 200.` `appstudio.appmanager-secret` correctly stayed **red** — *"Secret env var 'APPMANAGER_API_KEY' is not set"* — which is the honest, expected result for a machine where the owner has not exported the key, and is exactly the state §4.4 exists to resolve. Settings rendered the endpoint override, the self-signed opt-in, the profile-details table and the settings-file path with zero horizontal overflow.

A board with red rows is a **successful deployment** — TrSetup's job is to *find* broken prerequisites. Deployment failure looks like: no window, a blank board, or rows stuck Pending.

---

## 7. Troubleshooting

Deployment-time issues only. Runtime/operational issues are in `TrSetup-UsageGuide.md` §7.

| Symptom | Platform | Cause / fix |
|---|---|---|
| App will not open; *"cannot verify the developer"* / *"TrSetup is damaged"* | macOS | Gatekeeper quarantine on an unsigned download — expected, not corruption. Do the [§5.2](#macos-the-one-time-trust-step) trust step. |
| `spctl` says `rejected` | macOS | Correct for the current posture (`Signature=adhoc`, no Team ID, not notarized). Not fixable without a Developer ID certificate — REQ-FN-038. |
| *"Windows protected your PC"* | Windows | SmartScreen on an unsigned exe. **More info** → **Run anyway** ([§5.3](#windows-smartscreen-untested-end-to-end)). |
| Exe won't start; missing DLL, or nothing happens at all | Windows | Either the exe was pulled out of its folder (**ship and keep the whole folder**), or the artifact was not actually self-contained. Check `System.Private.CoreLib.dll`, `Microsoft.WindowsAppRuntime*` and `Microsoft.UI.Xaml.dll` are beside `TrSetup.exe` — that is precisely what `package-windows.ps1` asserts. |
| App demands the .NET Desktop Runtime / Windows App SDK on a fresh machine | Windows | **A BRD-57 bootstrap violation** — the artifact is framework-dependent and must not be shipped. Confirm `SelfContained` and `WindowsAppSDKSelfContained` resolved for the windows target, and that the build ran the packaging script (which would have hard-failed). This is the suspected cause of the 2026-07-20 report that "the exe build failed to install". |
| *"This program might not have installed correctly"* | Windows | Old builds only. Click **"This program installed correctly"** once; current builds embed the PCA-exemption manifest ([§5.3](#windows-smartscreen-untested-end-to-end)). |
| `NETSDK1100` (Windows targeting not enabled) | building on macOS | Expected — the csproj guard exists to prevent this. You cannot build the Windows head on a Mac ([§2.1](#which-host-os-each-build-requires)). |
| `NETSDK1005` (assets file has no target for `net10.0`) | building on macOS | You tried to force `-p:TargetFrameworks=…` past the guard. The global property corrupts the project graph. Use a Windows host. |
| `NETSDK1139` / unknown TFM `net10.0-maccatalyst` | building on Windows | Expected — the Catalyst TFM only activates on macOS. |
| `Could not find a valid Xcode app bundle at '/Library/Developer/CommandLineTools'` | building on macOS | The Mac has only the Command Line Tools; Catalyst needs **full Xcode**. `sudo xcode-select -s /Applications/Xcode.app`, and accept the licence. |
| `package-mac.sh: no .app at …` | building on macOS | Run `dotnet build src/TrSetup/TrSetup.csproj -f net10.0-maccatalyst -c Release` first — the script prints this exact command. |
| Restore fails on TrBlazeUI 1.0.7 (CI or a fresh clone) | any | The package is not on nuget.org. Register the `techierathore` GitHub Packages feed; in CI, set the `GH_PACKAGES_PAT` secret ([§2.4](#building-both-artifacts-in-ci-req-fn-041)). |
| *App Manager API reachable* stays red on a multi-machine setup | any | The profile default `https://localhost:5101/` is wrong for this machine. Set **Settings → Endpoints → App Manager API URL** ([§4.3](#point-this-machine-at-app-manager-multi-machine-deployments)). Read the evidence line — it names the URL it actually probed. |
| That row is amber, not green | any | You pointed it at a URL answering 404 (App Manager's `/` does). Use `/health` or another 2xx URL. |
| That row reports a certificate failure | any | LAN App Manager serving the self-signed ASP.NET dev cert. Turn on **"Trust a self-signed certificate for this endpoint"** — off by default, per-endpoint, and only ever applies to a URL you configured yourself. |
| *AppManager applicationId/API key configured* stays red although the shell shows the variable | any | The GUI app is not inheriting the shell environment. See the launchd / Explorer inheritance notes in [§4.4](#set-appmanager-api-key-per-os-durably). |
| Board rows show `○ N/A` | any | Not an error — only the selected roles' and app's checks run. Widen roles in Settings. |
| A lowercase `trsetup` folder appears under `publish/<rid>/` | any | Stale output of the deleted Spectre CLI head. **Do not run or ship it** — delete it. |

**Where to look next:** the app writes a rolling Serilog file (one per day, ~14 kept) — `%LOCALAPPDATA%\…\com.techierathore.trsetup\Data\logs\trsetup-<date>.log` on Windows, `~/Library/Containers/com.techierathore.trsetup/Data/Library/Application Support/logs/trsetup-<date>.log` on macOS. Open the newest one before filing anything.

---

## 8. Known gaps and what is not yet proven

Stated plainly so nobody mistakes intent for evidence.

### Not proven

1. **⚠ The Windows artifact has never been built.** Not on a developer machine, not in CI, not once. `dotnet publish` for the Windows head has never completed in this project. Everything about the Windows artifact — its existence, its size, whether the self-containment assertion passes, whether the exe starts — is **inference from MSBuild property evaluation performed on a Mac**. REQ-FN-039 is marked `Implemented` at 60% for exactly this reason.
2. **⚠ The Windows artifact has never been launched on a clean image.** That single test is what closes REQ-FN-036 / BRD-57 and REQ-FN-039 / BRD-60. Until it runs, the non-circular bootstrap is **proven on macOS only**.
3. **⚠ The CI workflow has never executed.** It cannot run until `GH_PACKAGES_PAT` exists and the branch is pushed. YAML structure was checked locally; behaviour was not.
4. **`win-arm64` is doubly unproven.** It is declared in `RuntimeIdentifiers` and matrixed in CI, and the conditional-default logic was property-verified — but no arm64 artifact has been produced or run.
5. **Windows SmartScreen behaviour is predicted, not observed** — inferred from the unsigned posture, since there is no artifact to test.
6. **Windows artifact size is unmeasured.** The ~150 MB+ figure is an expectation for a self-contained MAUI/WinUI publish, not a measurement.

### Proven

- **macOS is compliant with BRD-57.** 56 `System.*` assemblies bundled in `Contents/MonoBundle`; a packaged `.dmg` launched on a machine whose only SDK lived under `~/.dotnet`. Bundle 156 MB, `.dmg` 49 MB, universal (arm64 + x86_64).
- **The `.dmg` mounts with a correct drag-install layout** and the app inside is the same build.
- **The Gatekeeper block is real, not theoretical.** Reproduced via a simulated download: quarantine propagated to the `.app` and `spctl` returned `rejected`; the documented `xattr -dr` step cleared it and the app launched.
- **The board, the endpoint override, and the self-signed opt-in work live** — see the reference result in [§6](#verifying-a-successful-deployment).

### Open gaps (accepted or tracked)

| Gap | Status |
|---|---|
| **No code signing on either platform** | REQ-FN-038 `Blocked` — parked by owner (no Apple Developer account). Every user hits a trust prompt. See [§5.4](#this-is-an-open-production-gap-recommendation). |
| **No installer on Windows** | Deliberate (`WindowsPackageType=None`, ADR-001). Users extract a folder; there is no uninstaller. Inno Setup / WiX pointed at the published folder is the upgrade path if wanted. |
| **No auto-update** | Upgrades are manual re-downloads. Settings persist across upgrades. |
| **Large artifacts** | Accepted trade for the non-circular bootstrap. Not fixable by trimming — `PublishTrimmed` is unsupported for MAUI/WinUI and breaks XAML/reflection at runtime. |
| **Private TrBlazeUI feed dependency** | Anyone building from source needs a `read:packages` PAT for the `techierathore` feed. Does not affect users of a prebuilt artifact. |
| **`APPMANAGER_API_KEY` is owner-supplied** | By design (ADR-008) — TrSetup never stores secrets. Cannot be automated away; only documented ([§4.3](#point-this-machine-at-app-manager-multi-machine-deployments) / [§4.4](#set-appmanager-api-key-per-os-durably)). |
| **8 REQ pending external UAT** | Destructive fixers and host-bound boots — `TrSetup-UsageGuide.md` §6 is exactly that list. 0 FAIL. |

---

*Created 2026-07-21 in response to the owner's request to document the process for actual production deployment by users. Grounded in `build/package-mac.sh`, `build/package-windows.ps1`, `src/TrSetup/TrSetup.csproj`, `.github/workflows/build-artifacts.yml`, and the REQ-FN-036/037/038/039/040/041 rows of `docs/TrSetup-Checklist.md`. macOS facts re-confirmed live on this Mac (codesign, spctl, MonoBundle assembly count, artifact sizes); the verification recipe in §6 was executed against the running board. Windows facts are marked untested throughout because no Windows artifact exists.*
