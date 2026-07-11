# TrSetup — Usage Guide (Run it · Configure it · Test it)

> **How to read this guide:** pick the platform you are sitting at — [Windows](#2-run-trsetup-on-windows), [Mac](#3-run-trsetup-on-a-mac), or [WSL](#4-run-the-board-from-wsl-verify-host) — and follow that section top to bottom. Then use [§5 Configure settings](#5-configure-settings--roles-application-endpoints) to point the app at AppStudio / TrStudio, and [§6 UAT test plan](#6-uat-test-plan--step-by-step-per-platform) as your test script. Nothing in a platform section depends on another platform section.
>
> This is the single source for how to run and test TrSetup — the agents (self-smoke, verifier) and the human UAT use the SAME setups and walkthroughs below (`.tfcore/tasks/_smoke-test-policy.md`). Build commands mirror `docs/TrSetup-BuildAndRun-Guide.md`.
>
> **Status (2026-07-10):** 33 REQ Verified · 8 pending external UAT (the [§6 plan](#6-uat-test-plan--step-by-step-per-platform)) · 6 N/A (withdrawn with the CLI head) · 0 FAIL.

## 1. What you are testing

**TrSetup is a local, single-user desktop app** — no login, no accounts, no database. It scans the machine it runs on against a chosen *machine role* (WSL agent host, Windows device host, Mac device host, Mac app runner) and an optional *application profile* (AppStudio or TrStudio), shows every prerequisite as a ✓/⚠/✗ board row, and can **fix** broken items after showing you the exact command and asking consent.

One shipping app: **`TrSetup`** (MAUI Blazor Hybrid — Windows unpackaged exe + Mac Catalyst `.app`). `TrSetup.Web` is a non-shipping test host that also serves as the no-build-tools way to run the board on a Mac ([§3 Option A](#option-a--no-build-tools-on-the-mac-browser-board--recommended-first)).

**The six screens** (all inside the app window, in first-use order):

| Screen | Route | What it does |
|--------|-------|--------------|
| Role picker | `/setup` | First-run: pick machine role(s) + application, "Save & scan" |
| Board | `/` | The dashboard — grouped check rows with ✓/⚠/✗, Re-check all, Fix all, Export |
| Check detail | `/check/{id}` | Click any row: explanation, evidence, fix preview, last output |
| Fix-all run | `/fix-run` | Ordered fix steps, each behind a consent gate |
| Report | `/report` | Export the board as `.md` + `.html` (secrets never printed) |
| Settings | `/settings` | Edit roles, application, endpoints; shows the settings-file path |

---

## 2. Run TrSetup on Windows

### 2.1 Before you run — prerequisites

1. **.NET 10 SDK** installed on Windows (`dotnet --version` in a Windows terminal shows 10.x).
2. **MAUI workload:** `dotnet workload install maui` (one time).
3. The repo at `C:\3AIGenCode\TrSetup` (or your clone path).

### 2.2 Build it (copy-paste as-is)

From a **Windows terminal** (PowerShell or cmd):

```powershell
cd C:\3AIGenCode\TrSetup
dotnet build src/TrSetup -f net10.0-windows10.0.19041.0 -c Release
```

Or from a **WSL terminal**:

```bash
cd /mnt/c/3AIGenCode/TrSetup
cmd.exe /c "dotnet build src\TrSetup -f net10.0-windows10.0.19041.0 -c Release"
```

*(Or Visual Studio: open `TrSetup.sln` → startup project `TrSetup` → Release / `net10.0-windows10.0.19041.0` → Build.)*

### 2.3 Launch it

Either double-click `src\TrSetup\bin\Release\net10.0-windows10.0.19041.0\win-x64\TrSetup.exe` in Explorer, or copy-paste:

From a **Windows terminal**:

```powershell
cd C:\3AIGenCode\TrSetup
start src\TrSetup\bin\Release\net10.0-windows10.0.19041.0\win-x64\TrSetup.exe
```

Or from a **WSL terminal**:

```bash
cd /mnt/c/3AIGenCode/TrSetup
cmd.exe /c "start src\TrSetup\bin\Release\net10.0-windows10.0.19041.0\win-x64\TrSetup.exe"
```

A native window titled **TrSetup** opens showing the board (or the role picker on first run). No installer popup should appear — if an *older* build once showed "This program might not have installed correctly", click **"This program installed correctly"** once to clear Windows' cached entry.

> **Sharing with another Windows machine:** copy the **whole `win-x64\` folder** — the lone exe will not run on its own.

### 2.4 First run

1. The **role picker** auto-shows (no settings yet).
2. Tick **Device host (Windows)** (add others only if this machine really plays them).
3. Answer the "I develop natively" question (normally **off**).
4. Pick the application — **AppStudio** (or TrStudio / none; you can change it later in [§5.2](#52-switch-the-application-appstudio--trstudio--framework-only)).
5. Click **Save & scan this machine** → the board appears, scoped to your role + app. Rows for roles you did *not* pick show `○ N/A` — that is by design, not an error.

---

## 3. Run TrSetup on a Mac

Two independent paths. **If your Mac has no dev tools, use Option A.** If you want the real desktop `.app`, use Option B.

| | Option A — browser board | Option B — native `.app` |
|---|---|---|
| Needs on the Mac | **Nothing** (just the copied folder) | Xcode + .NET 10 SDK + MAUI workload |
| Built where | On **Windows**, copied over | On **the Mac**, from source |
| You get | The full board in Safari at `localhost:5999` | `TrSetup.app` in a native window |

### Option A — no build tools on the Mac (browser board) — recommended first

**Before you start:** a Windows machine with the repo + .NET 10 SDK, and a way to copy ~120 MB to the Mac (SMB share, `scp`, AirDrop zip, or USB).

**Step 1 — on Windows, publish the Mac binary** (copy-paste; if the Mac is Intel, replace `osx-arm64` with `osx-x64`):

From a **Windows terminal**:

```powershell
cd C:\3AIGenCode\TrSetup
dotnet publish src/TrSetup.Web -c Release -r osx-arm64 --self-contained true -o publish/mac/TrSetup.Web
```

Or from a **WSL terminal**:

```bash
cd /mnt/c/3AIGenCode/TrSetup
cmd.exe /c "dotnet publish src\TrSetup.Web -c Release -r osx-arm64 --self-contained true -o publish\mac\TrSetup.Web"
```

**Step 2 — copy the whole `publish/mac/TrSetup.Web/` folder to the Mac** (Finder → Cmd+K → `smb://<windows-ip>`, or `scp -r`, or zip+AirDrop, or USB).

**Step 3 — on the Mac, in Terminal** (copy-paste; the first line assumes you copied the folder to `~/Downloads` — adjust if elsewhere):

```bash
cd ~/Downloads
chmod +x TrSetup.Web/TrSetup.Web                 # copies via SMB/zip strip the execute bit
xattr -dr com.apple.quarantine TrSetup.Web       # clear Gatekeeper quarantine (unsigned build)
./TrSetup.Web/TrSetup.Web                        # starts on http://localhost:5999, opens Safari
```

**Step 4 —** if Safari didn't open automatically, browse to `http://localhost:5999`. If Gatekeeper still blocks the binary, allow it once via **System Settings → Privacy & Security**.

**Step 5 — first run:** complete the role picker — tick **Device host (Mac)** and **App runner (Mac)**, pick **AppStudio**, **Save & scan**. (Or pre-seed the settings file before Step 3: `cp docs/samples/appstudio-mac-runner.json ~/.trsetup/settings.json` — see [§5.3](#53-seed-settings-by-copying-a-sample-file).)

You now have the full board with **live Mac detect evidence** — the `mac.*` rows (Xcode/CLT, .NET+MAUI, Node, Appium xcuitest/mac2, LaunchAgent :4723, stable IP, iOS Simulator) show real Pass/Fail/Warn for *this* Mac.

### Option B — native Catalyst `.app` (build on the Mac)

**Before you start — install on the Mac (one time, manual):**

1. **Xcode** (full app from the App Store; launch once and accept the licence).
2. **.NET 10 SDK** (macOS installer from dotnet.microsoft.com).
3. **MAUI workload:** `dotnet workload install maui`.
4. **git**, and a clone of the TrSetup repo.

**Build and launch** (copy-paste; the first line assumes the repo is cloned at `~/TrSetup` — adjust if elsewhere):

```bash
cd ~/TrSetup
dotnet build src/TrSetup -f net10.0-maccatalyst -c Release
cp -R "src/TrSetup/bin/Release/net10.0-maccatalyst/maccatalyst-arm64/TrSetup.app" /Applications/   # Intel Mac: maccatalyst-x64/
xattr -dr com.apple.quarantine "/Applications/TrSetup.app"
open "/Applications/TrSetup.app"
```

Notes:
- The build is **ad-hoc signed** (no Apple Developer certificate needed). First launch may say "cannot verify the developer" — the `xattr` line above clears it, or allow once via **System Settings → Privacy & Security**.
- This TFM only exists on macOS — trying `net10.0-maccatalyst` on Windows fails with `NETSDK1139` by design.
- First run: same role picker as Option A Step 5.

---

## 4. Run the board from WSL (verify host)

This is the context the automated verifier already covers (all six screens runtime-verified) — you rarely need it manually, but for completeness:

```bash
cd /mnt/c/3AIGenCode/TrSetup
dotnet run --project src/TrSetup.Web        # binds http://localhost:5999
```

Set `TRSETUP_NO_BROWSER=1` to stop the auto-open. Role for this machine: **Agent host (WSL)**; settings file `~/.trsetup/settings.json`.

---

## 5. Configure settings — roles, application, endpoints

TrSetup's entire state is **one small JSON file**:

| OS / head | Settings file |
|-----------|---------------|
| Windows (`TrSetup.exe`) | `%APPDATA%\TrSetup\settings.json` |
| macOS / Linux / WSL | `~/.trsetup/settings.json` |

The **Settings screen shows the exact path on your machine** — that is always the authoritative answer.

### 5.1 In-app, via the Settings screen (the normal way)

1. Launch the app (any platform above).
2. In the sidebar, click **Settings** (the `sliders` icon) → the `/settings` screen opens.
3. Edit any of:
   - **Machine roles** — tick/untick Agent host (WSL) / Device host (Windows) / Device host (Mac) / App runner (Mac), plus the "I develop natively" variant.
   - **Selected application** — AppStudio / TrStudio / none (see [§5.2](#52-switch-the-application-appstudio--trstudio--framework-only)).
   - **Endpoints** — today just the **LAN Mac IP** used by the cross-machine Bridges checks (validated in-app as IP/hostname).
4. Review the read-only **profile details** panel — every requirement row tagged *built-in* vs *app-repo override*.
5. Click **Save** → persists to the JSON file and **re-scopes the board immediately, no restart needed**.

### 5.2 Switch the application (AppStudio / TrStudio / framework-only)

Each application adds its own profile rows to the board; here is how to set up for each:

| To test… | Set SelectedApp to | The board adds | Typical roles |
|----------|--------------------|----------------|---------------|
| **AppStudio** | `AppStudio` | AppStudio SDK/tooling profile rows | WSL agent host, Windows device host, or Mac runner |
| **TrStudio** | `TrStudio` | Postgres+PgVector, ffmpeg, ComfyUI runtime, disk-space floor, provider-key rows | usually one beefy machine with several roles |
| **Framework only** | *(none)* | no app rows — core framework + bridge checks only | any |

Steps: **Settings → Selected application → pick → Save.** The board re-scopes instantly — e.g. switching AppStudio → TrStudio replaces the AppStudio rows with the TrStudio service/ffmpeg/ComfyUI/disk rows. (The board header also offers the app switcher directly.)

### 5.3 Seed settings by copying a sample file

For a fresh or headless machine you can skip the picker entirely — copy a ready-made file **before first launch**.

On **Mac / WSL / Linux** (run from the repo root; swap in the sample you want from the table below):

```bash
cp docs/samples/appstudio-mac-runner.json ~/.trsetup/settings.json
```

On **Windows** (PowerShell):

```powershell
cd C:\3AIGenCode\TrSetup
New-Item -ItemType Directory -Force $env:APPDATA\TrSetup | Out-Null
Copy-Item docs\samples\appstudio-windows-device-host.json $env:APPDATA\TrSetup\settings.json
```

| Sample (in `docs/samples/`) | Roles | App | Use for |
|-----------------------------|-------|-----|---------|
| `framework-only-wsl.json` | `AgentHostWsl` | *(none)* | Core framework checks only |
| `appstudio-wsl-agent.json` | `AgentHostWsl` | AppStudio | The common WSL dev box |
| `appstudio-windows-device-host.json` | `DeviceHostWindows` | AppStudio | Windows box serving Android emulator + Appium |
| `appstudio-mac-runner.json` | `DeviceHostMac, AppRunnerMac` | AppStudio | LAN Mac hosting Appium + building AppStudio (Catalyst) |
| `trstudio-full.json` | all four roles | TrStudio | One machine, every role, the heavy TrStudio profile |

Field reference (full detail in `docs/samples/README.md`):
- **`Roles`** — comma-separated: `AgentHostWsl`, `DeviceHostWindows`, `DeviceHostMac`, `AppRunnerMac`, optional `NativeDev`. Only your roles' checks run; the rest show `○ N/A` (by design).
- **`SelectedApp`** — `"AppStudio"` / `"TrStudio"` / `null`.
- **`Endpoints`** — address-only (today `"MacIp"`). **Secrets are never stored here.**

Everything seeded this way remains editable in-app afterwards ([§5.1](#51-in-app-via-the-settings-screen-the-normal-way)).

---

## 6. UAT test plan — step by step, per platform

Do the sessions in this order. Tick each ✅ as you go; the REQ tags in brackets are what each step proves — when a session passes, mark those REQs `Verified` in `docs/TrSetup-Checklist.md` (Remarks: UAT pass + date).

### 6.1 Windows session (~30 min, at the Windows machine)

1. Build + launch per [§2](#2-run-trsetup-on-windows). ✅ **Board renders in the native TrSetup window, no installer popup.** *[FN-030, BRD-44]*
2. First-run role picker: Device host (Windows) + AppStudio → Save & scan. ✅ **Relaunch shows the board, not the picker** (selections persisted). *[UI-003, FN-005]*
3. On the board: check the grouped rows (Framework core / Bridges / AppStudio profile) each show icon + word + real evidence; press **Re-check all**. ✅ **Sweep completes < 30 s with streaming statuses.** *[UI-001, NFR-001]*
4. Click a failing row → detail sheet: read Explain, expand Last run output, note the Fix preview command. ✅ **All panels populated; manual-only checks show guidance and no Fix button.** *[UI-002]*
5. Deliberately break one **fixable Windows item**, run **Fix** from the board. ✅ **Row goes red → green with re-verify; the fix showed its command and asked consent first.** *[FN-015]*
6. **Fix all** with two broken items: approve the first consent gate, **decline** the second. ✅ **Run halts at the decline, later steps stay pending; re-run approving all succeeds and is idempotent.** *[UI-004, FN-018/019/020]*
7. Switch application to **TrStudio** ([§5.2](#52-switch-the-application-appstudio--trstudio--framework-only)). ✅ **Board re-scopes without reload to the TrStudio rows.** Approve the **ComfyUI** install fixer *[FN-025]* and the **Postgres+PgVector / ffmpeg** fixers *[FN-026]* — rows go green. *(These also count if you run them on the Mac instead.)*
8. **Export report** → Save as `.md` + `.html`; search both files for any secret value you have configured. ✅ **`env-secret` rows say "present (value never shown)"; zero secret values found.** *[UI-005, FN-010, NFR-002]*

### 6.2 Mac session (~30 min, at the Mac)

1. Run the board via **Option A** ([§3](#3-run-trsetup-on-a-mac)) with roles Device host (Mac) + App runner (Mac), AppStudio. ✅ **`mac.*` rows show live Pass/Fail/Warn evidence — Xcode/CLT, .NET+MAUI, Node, Appium, LaunchAgent :4723, stable IP, iOS Simulator — not blanks.** *[FN-008]*
2. Deliberately break one **fixable Mac item**, Fix from the board. ✅ **Red → green with re-verify.** *[FN-016]*
3. Build + launch the native `.app` via **Option B**. ✅ **Native window titled TrSetup shows the board.** *[FN-028 build path]*
4. In the native app, with every Mac app-runner prerequisite green, run the board's **"Build & install AppStudio for Mac (Catalyst)"** fixer. ✅ **It emits AppStudio's `.app`.** *[FN-028]*
5. Confirm the log file exists: `~/Library/Containers/com.techierathore.trsetup/Data/Library/Application Support/logs/trsetup-<date>.log`. ✅ *[NFR-007 on Mac]*
6. *(If not done on Windows step 7)* run the ComfyUI / Postgres+PgVector / ffmpeg fixers here. *[FN-025, FN-026]*

### 6.3 WSL session (~10 min, in the WSL terminal + browser)

1. Break a fixable WSL item — e.g. remove `~/bin/winrun` — launch the board ([§4](#4-run-the-board-from-wsl-verify-host)), Fix it. ✅ **Red → green with re-verify.** *[FN-014]*

> When all three sessions pass, the 8 pending rows (FN-008, FN-014, FN-015, FN-016, FN-025, FN-026, FN-028, FN-030) flip to Verified and the owner sets PROJECT-STATUS to `Released`.

---

## 7. Logs & troubleshooting

The shipping app writes a rolling **Serilog** log (one file per day, ~14 kept). When a detect or fix misbehaves, open the newest `trsetup-<date>.log` **before filing anything**:

| Platform | Log location |
|----------|--------------|
| **Windows** (`TrSetup.exe`) | `%LOCALAPPDATA%\User Name\com.techierathore.trsetup\Data\logs\trsetup-<date>.log` |
| **Mac** (Catalyst `.app`) | `~/Library/Containers/com.techierathore.trsetup/Data/Library/Application Support/logs/trsetup-<date>.log` |
| **`TrSetup.Web`** (browser board / test host) | No file — logs to the console it was launched from |

*(Tip: the Settings screen shows the settings-file path; the log folder sits under the same app-data root.)*

Common issues:

| Symptom | Platform | Fix |
|---------|----------|-----|
| "This program might not have installed correctly" | Windows (old builds only) | Click **"This program installed correctly"** once; current builds embed a manifest that suppresses it |
| "cannot verify the developer" | Mac | `xattr -dr com.apple.quarantine <path>` or allow once in System Settings → Privacy & Security |
| `NETSDK1139` / unknown TFM `net10.0-maccatalyst` | Windows | Expected — the Catalyst head only builds on macOS ([§3 Option B](#option-b--native-catalyst-app-build-on-the-mac)) |
| Port busy / wrong port (browser board) | Mac/WSL | `ASPNETCORE_URLS=http://localhost:5999` before launching; `TRSETUP_NO_BROWSER=1` stops the auto-open |
| A lowercase `trsetup` folder appears under `publish/<rid>/` | any | **Do not run it** — that is the publish output of the deleted Spectre CLI head (removed 2026-07-09, REQ-FN-034; stale copies were purged 2026-07-10). If one reappears it is a stale artifact — delete it. The only supported ways to run TrSetup are §2–§4 |
| Board rows show `○ N/A` | any | Not an error — only your selected roles' + app's checks run; widen the roles in Settings |
| Board cramped below ~390 px width | any | Known minor item — the status table needs horizontal scroll; desktop is the target |

---

## 8. For agents — automated harness & canonical contexts

*(Human testers can stop at §7. This section keeps the agent-facing contract.)*

```bash
dotnet test                # unit tests (Core; the Cli suite was deleted under REQ-FN-034)
npx playwright test        # UI render + visual gates (tests/verify/, incl. settings.spec.ts)
```

The Playwright harness boots `TrSetup.Web` purely as the **test host** for the shared `TrSetupUI` screens (`baseURL http://localhost:5999`; respects `TRSETUP_NO_BROWSER=1`). It is not a product head (REQ-FN-034).

**Canonical machine-role contexts** (there are no logins — these replace test users; agents and UAT use the same four):

| # | Context | Roles | Status | Maps to |
|---|---------|-------|--------|---------|
| 1 | WSL agent-host | Agent host (WSL) | ✅ verified | [§4](#4-run-the-board-from-wsl-verify-host) / [§6.3](#63-wsl-session-10-min-in-the-wsl-terminal--browser) |
| 2 | Windows device-host | Device host (Windows) | ✅ detect verified; fixers = UAT | [§2](#2-run-trsetup-on-windows) / [§6.1](#61-windows-session-30-min-at-the-windows-machine) |
| 3 | Mac device-host + app-runner | Device host (Mac) + App runner (Mac) | ⬜ needs the LAN Mac | [§3](#3-run-trsetup-on-a-mac) / [§6.2](#62-mac-session-30-min-at-the-mac) |
| 4 | Native-dev | any role + "I develop natively" | ⬜ | verifies WSL-bridge checks are dropped (BRD-8) |

Seeding = the settings JSON is the entire "user" state (FN-005), e.g. context #1 + AppStudio: `{ "Roles": "AgentHostWsl", "SelectedApp": "AppStudio" }`.

**Screen-to-REQ coverage map** (what the §6 steps prove, for the verifier):

| Screen / flow | Covers |
|---------------|--------|
| Role picker `/setup` | BRD-6/7/8 · REQ-UI-003, REQ-FN-005 |
| Board `/` | BRD-5/9/10/12/17/33-43 · REQ-UI-001, REQ-FN-004/006/007/021/022/023, REQ-NFR-001 |
| Check detail `/check/{id}` | BRD-11/13 · REQ-UI-002 |
| Fix-all `/fix-run` | BRD-3/18/28/29/30 · REQ-UI-004, REQ-FN-014/018/019/020 |
| Report `/report` | BRD-24/25 · REQ-UI-005, REQ-FN-010, REQ-NFR-002 |
| Settings `/settings` | REQ-UI-006 (Verified 2026-07-09) |
| MAUI head boot Win/Mac | BRD-44 · REQ-FN-028, REQ-FN-030 |

## 9. Known limitations

- **8 REQ pending external UAT** (destructive fixers + host-bound boots) — the [§6 plan](#6-uat-test-plan--step-by-step-per-platform) is exactly that list; 0 FAIL.
- **Heads decommissioned (REQ-FN-034, Verified):** the Spectre CLI head is deleted; `TrSetup.Web` is retained only as the test host / copy-to-Mac vehicle — never document it as a product head. Stale `trsetup` CLI folders (published 2026-07-07 by the since-deleted publish scripts) sat under `publish/{osx-arm64,linux-x64,win-x64}/` until they were deleted on 2026-07-10 — only `TrSetup.Web` remains there.
- **Narrow screens (<390 px):** board status table needs horizontal scroll; desktop is the target.
- Full Xcode install and router DHCP reservation are permanently manual (by design — BRD §12).
- Library gaps: none (TrBlazeUI 0, TechieRag not used).

---
*Restructured 2026-07-10 for human UAT (per-platform, step-by-step) after owner feedback — same facts as the 2026-07-09 revision (REQ-FN-034/035 rescope), reorganized: platform sections §2–§4, settings-per-application §5, per-platform UAT script §6, agent contract moved to §8.*
