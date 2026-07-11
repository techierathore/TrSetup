# TrSetup — Build & Run Guide (MAUI desktop · Windows exe + Mac Catalyst app)

<!-- AGENT NOTE (never emit as visible text): "Catalyst cannot be built from Windows" is a HOST limitation, not a deliverable cancellation. Never drop, declare impossible, or silently substitute the native Mac Catalyst .app because your machine can't build it — §3 Option B (the owner runs ONE command on the Mac) is the standing path: prepare the source + the exact commands and hand off. The TrSetup.Web osx-arm64 publish (§3 Option A) is a complement offered alongside, never the replacement for the native app. See build-invocation-ladder.md §"What NOT to do". Also: the head project was renamed from its old `.App`-suffixed form to plain TrSetup (`src/TrSetup/TrSetup.csproj`, exe `TrSetup.exe`) on 2026-07-10 under REQ-FN-035 (framework naming standard); this guide's paths reflect the new name. -->

> **2026-07-09 — TrSetup is now a single MAUI desktop app (owner decision).** The former Blazor Server and CLI heads are withdrawn; their code removal is tracked by REQ-FN-034 in `docs/TrSetup-Checklist.md`.

> **Status: BUILT.** The solution exists; the paths and commands below are concrete and verified against it. `TrSetup` is the one shipping head — a MAUI Blazor Hybrid desktop app hosting the `TrSetupUI` RCL over the `TrSetup.Core` engine. The *native* MAUI heads compile against each OS's native SDKs and cannot be cross-published, so the Windows exe is built on Windows (§2) and the native Mac Catalyst `.app` is built on the Mac (§3 Option B). **To simply run TrSetup on a Mac without a Mac build environment**, self-contained-publish the retained `TrSetup.Web` head for `osx-arm64` on Windows and copy the folder over (§3 Option A — the recommended, no-.NET-on-the-Mac path). The sibling guide `AppStudio-BuildAndRun-Guide.md` describes the same shape.

## 1. What TrSetup ships as

| Head | Type | Platforms | Distribution |
|---|---|---|---|
| `TrSetup` | MAUI Blazor Hybrid | Windows (unpackaged exe, `WindowsPackageType=None`) + Mac Catalyst (`net10.0-maccatalyst`) | Windows exe built on Windows (VS or CLI, §2); **Mac `.app` built on the Mac** (one command, §3 — Catalyst cannot be built from Windows) |

Two ways onto a Mac, and the difference matters:

- **The *native* Catalyst `.app` cannot be cross-published.** A MAUI head compiles against Apple's native SDKs, which need **Xcode, only on macOS** (Visual Studio's "pair to Mac" is iOS-only). So the native desktop `.app` must be built *on the Mac* from source (§3 Option B) — you copy the **source**, not a binary.
- **But you do NOT need a Mac (or Xcode, or .NET) to *run* TrSetup on a Mac.** The retained `TrSetup.Web` Blazor head is plain .NET and **self-contained-cross-publishes from Windows for `osx-arm64`**, so you can build the binary on Windows, copy the folder to the Mac, and run the full board in Safari/Chrome — the **copy-to-Mac binaries** path (§3 Option A, recommended). `TrSetup.Web` hosts the *same* `TrSetupUI` RCL over the *same* `TrSetup.Core` engine as the native head, so the Mac sees its own live detect evidence. (`TrSetup.Web` stays a non-shipping head under REQ-FN-034 — it is the test smoke host *and* this copy-to-Mac vehicle, not a second product GUI.)

## 2. Build the Windows exe (Visual Studio)

Same as AppStudio: open `TrSetup.sln` → startup project `TrSetup` → Release, `net10.0-windows10.0.19041.0` → Build. Because `WindowsPackageType=None` (BRD-44, unpackaged), the build produces a plain double-clickable exe — no MSIX, no side-load cert. Distribute the whole self-contained folder:

```
src/TrSetup/bin/Release/net10.0-windows10.0.19041.0/win-x64/TrSetup.exe
```

The `win-x64` RID sub-folder is added automatically by the MAUI Windows build (verified against the real solution — the executable is `TrSetup.exe`). Ship the entire `win-x64\` directory (it carries `TrSetup.exe`, `WebView2Loader.dll` / the WebView2 loader, `RestartAgent.exe`, the RCL/TrBlazeUI DLLs and the app runtime) — the lone `.exe` will not run on its own.

CLI equivalent (this is exactly rung #4 of the build ladder, driven from WSL as `cmd.exe /c "dotnet build src/TrSetup -f net10.0-windows10.0.19041.0 -c Release"`):
```powershell
dotnet build src/TrSetup -f net10.0-windows10.0.19041.0 -c Release
```
On first launch the head opens a single native window titled **TrSetup** (`AutomationId="TrSetupMainPage"`) hosting the Blazor board in a `BlazorWebView` (`AutomationId="TrSetupBlazorWebView"`) — the two stable ids the Appium/WinAppDriver session attaches to.

> **Note — the "might not have installed correctly" popup is suppressed.** Because the exe is named `TrSetup.exe` (contains "Setup"), Windows' Program Compatibility Assistant would otherwise false-flag it as an installer that didn't finish. The build embeds a Win32 manifest (`Platforms/Windows/app.manifest` → `<compatibility>` supportedOS Win7→11 + `asInvoker <trustInfo>`, wired via a windows-scoped `<ApplicationManifest>` in the csproj) that exempts the exe from that heuristic. TrSetup is not an installer. If an older build already showed the dialog once, click **"This program installed correctly"** to clear Windows' cached entry.

## 3. Run on the Mac — two paths

### Option A — copy-to-Mac binaries (no .NET / no Xcode on the Mac) — recommended

The `TrSetup.Web` head is plain .NET, so the "build on Windows → copy → run" flow works and gives you the complete board in Safari/Chrome on the Mac. **Nothing but the copied folder is needed on the Mac.**

**On the Windows machine** (self-contained publish — bundles the .NET runtime; the folder is ~120 MB and must be copied whole):

```bash
dotnet publish src/TrSetup.Web -c Release -r osx-arm64 --self-contained true -o publish/mac/TrSetup.Web
```

(`-r osx-arm64` targets Apple Silicon; use `-r osx-x64` for an Intel Mac. From WSL this is ladder rung #4: `cmd.exe /c "dotnet publish src\TrSetup.Web -c Release -r osx-arm64 --self-contained true -o publish\mac\TrSetup.Web"`.)

Copy `publish/mac/TrSetup.Web/` to the Mac (Finder Cmd+K → `smb://<windows-ip>`, `scp -r`, AirDrop a zip, or USB).

**On the Mac:**

```bash
chmod +x TrSetup.Web/TrSetup.Web                    # SMB/zip copies strip the execute bit
xattr -dr com.apple.quarantine TrSetup.Web          # clear Gatekeeper quarantine (unsigned build)
./TrSetup.Web/TrSetup.Web                            # binds http://localhost:5999 and opens Safari
```

The head binds `localhost:5999` and best-effort opens the Mac browser (`open`). Set `ASPNETCORE_URLS=http://localhost:5999` before launching to force the port; set `TRSETUP_NO_BROWSER=1` to stop it auto-opening. If Gatekeeper still complains, allow once via **System Settings → Privacy & Security**. The board runs the same checks/fixes as the native head; only the auto-**fixers** that install Mac tooling still need §4's runtime prerequisites present.

### Option B — native Catalyst `.app` (built on the Mac)

Prerequisites on the Mac (install manually — since the CLI bootstrapper head is withdrawn, TrSetup no longer installs its own build toolchain): Xcode (full, licence accepted), .NET 10 SDK, the MAUI workload (`dotnet workload install maui`), and git.

```bash
git clone <TrSetup repo> && cd TrSetup
dotnet build src/TrSetup -f net10.0-maccatalyst -c Release
open "src/TrSetup/bin/Release/net10.0-maccatalyst/maccatalyst-arm64/TrSetup.app"
```

- The `net10.0-maccatalyst` TFM is appended to `TrSetup.csproj` **only on macOS** (an `IsOSPlatform('OSX')` guard on `<TargetFrameworks>`), so this command is a no-op / unknown-TFM on Windows — Catalyst genuinely can only be built on the Mac (the §1 toolchain rule).
- Output bundle: **`src/TrSetup/bin/Release/net10.0-maccatalyst/maccatalyst-arm64/TrSetup.app`** on Apple Silicon (an Intel Mac emits `maccatalyst-x64/` — the RID sub-folder follows the build host's architecture).
- **Ad-hoc signed, no Apple certificate needed.** The csproj sets `<CodesignKey>-</CodesignKey>` + `<CreatePackage>false</CreatePackage>` under a maccatalyst-guarded `PropertyGroup`, so the build produces a runnable `.app` (not a `.pkg`) signed with codesign's ad-hoc identity — a personal, unnotarised build. First launch may hit Gatekeeper ("cannot verify the developer"); clear it once with `xattr -dr com.apple.quarantine "…/TrSetup.app"` or allow it via System Settings → Privacy & Security.
- The window/board chrome carries the same AutomationIds as the Windows head (`TrSetupMainPage` page, `TrSetupBlazorWebView` web view) so the Appium `mac2` driver attaches by bundle id `com.techierathore.trsetup`.

Once running, TrSetup does what it was built for: the board checks the machine, and every fix shows the exact command before running it and re-verifies after — Xcode CLT, SDKs, Appium, Postgres + PgVector, ffmpeg, isolated Python + ComfyUI, … per the selected machine role.

## 4. Troubleshooting (observed / expected)

- **Windows "This program might not have installed correctly" popup** — suppressed by the embedded PCA-exemption manifest; see the note in §2. Only older builds (before the manifest) can show it; clear the cached entry with **"This program installed correctly"**.
- **macOS "cannot verify the developer"** — the Catalyst `.app` is an ad-hoc-signed personal build under Gatekeeper quarantine. Fix: `xattr -dr com.apple.quarantine "…/TrSetup.app"`, or allow once via System Settings → Privacy & Security (see §3).
- **`NETSDK1139` / unknown TFM `net10.0-maccatalyst` when building on Windows** — expected, not a bug: the Catalyst TFM only activates on macOS (the csproj guard in §3). Build the Catalyst head on the Mac.

---
*Created 2026-07-05 alongside the plan (now `docs/OldDocs/TrSetup-Plan.md` §6.5); expanded 2026-07-06 with the Option A/B framing; firmed up 2026-07-07 at P4 (REQ-FN-031/REQ-NFR-006). Rewritten 2026-07-09 as MAUI-desktop-only after the owner decision to withdraw the Blazor Server and CLI heads (code removal tracked by REQ-FN-034): the Option A/B framing (§1b), the copy-to-Mac cross-publish path (old Option B), the WSL run section, and the `scripts/publish.*` / portability section (§5) were removed with the heads they described. 2026-07-10: head project renamed to `src/TrSetup` / `TrSetup.exe` (REQ-FN-035) — all paths updated. Last updated: 2026-07-10.*
