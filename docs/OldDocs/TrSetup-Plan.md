# TrSetup — Environment Doctor & Setup Tool (Plan)

> **Status: PLAN AGREED 2026-07-05** — decisions locked (§10), nothing built yet. Next step: `*day1-greenfield TrSetup` with this plan as the source-doc hint (it bulk-harvests this doc into the BRD + Architecture and produces mockups, coding standards, PROJECT-STATUS, CLAUDE.md and the UsageGuide in one pass; `*author-brd` is only for later per-requirement additions).
>
> Origin: the owner asked (2026-07-05): *"running so many commands on Windows, WSL and Mac is very confusing — write a small GUI tool to check all the necessary integrations and build the bridges itself, one version each for Windows, Mac & WSL; it should also set up the necessary tools for my apps like AppStudio and TrStudio."* Follow-up (same day): add a TUI fallback for WSL; TrSetup must **download and install any missing SDK itself** (Node.js, Android SDK, .NET SDK, Python, ComfyUI, …); and ship **step-by-step build-and-run guides** for getting TrSetup/AppStudio/TrStudio built on the Windows machine and running on the Mac.

---

## 1. The problem

Getting a machine ready for the TechieFlow verification harness (and for building and running the apps themselves) is currently a scattered set of copy-paste commands across **three different environments**:

| Where | What you have to run today | Source |
|---|---|---|
| **WSL** | apt-install 15 headless-Chromium libs; create `~/bin/winrun`; PATH edit; Playwright CLI + browsers; .NET SDK | WORKFLOW §0 |
| **Windows host** | `.wslconfig` mirrored networking + `wsl --shutdown`; Android SDK `sdkmanager`/`avdmanager` (system image + `Pixel_API_34` AVD); Node + `npm install -g appium`; `appium driver install uiautomator2`; `start-android-verify.ps1` helper; MAUI workload | WORKFLOW §0b steps 1–2 |
| **Mac (LAN)** | Xcode; .NET SDK + `dotnet workload install maui`; Node + appium; `xcuitest` + `mac2` drivers; serve `appium --address 0.0.0.0 --port 4723`; stable IP | WORKFLOW §0b step 3 |
| **Per app** | `core-config.yaml → runtimeVerification.appium` registration; `curl …/status` verification; app-specific services (Postgres, ffmpeg, API keys, private NuGet feeds…) | §0b step 4 + each app's BRD |

Every step is manual, order-sensitive, error-prone, easy to forget on a new machine/reinstall, and impossible to *re-verify* quickly ("is my bridge still up?"). A framework user who isn't the owner has no realistic chance of getting this right from prose. And running a portfolio app on the second machine (the Mac) repeats the pain with a different SDK list.

**Goal:** replace all of it with one small app: open it → it shows a red/amber/green board of every required integration for *this machine's role* and *the selected app* → click **Fix** (or **Fix all**) → it downloads/installs/configures whatever is missing itself → re-checks → green.

## 2. Goals & non-goals

**Goals**

1. **Check** every integration the framework and the selected app need on the current machine (detect, not assume).
2. **Fix by installing, not by instructing.** If a required SDK or tool is missing — Node.js, Android SDK, .NET SDK, a MAUI workload, Python, **ComfyUI**, Appium + drivers, Playwright, PostgreSQL+PgVector, ffmpeg — TrSetup **downloads and installs it itself** (owner decision 2026-07-05). "Guided/manual" is reserved for the few things that genuinely can't be automated (Xcode ships only via the App Store; DHCP reservations live in the router).
3. **One codebase, three heads + a TUI**: Windows GUI, macOS GUI, WSL/Linux GUI via browser, and a **terminal TUI fallback** for WSL/headless use (locked decision).
4. **App profiles**: per-app requirement sets (AppStudio, TrStudio, …) as **declarative config, not code** — baked-in defaults with a per-app override in the app repo (`.tfcore/trsetup-profile.json`); **app repo wins** (locked decision).
5. **Cross-machine awareness**: the WSL side can verify it reaches the Windows-host Appium and the LAN Mac (`/status` probes); each machine knows its *role* — including the new **Mac app-runner** role (§3) that prepares the Mac to *run the portfolio apps*, not just host Appium.
6. **Build-and-run guides**: each app ships a human step-by-step `<APP>-BuildAndRun-Guide.md` — build on the Windows machine with Visual Studio, get it running on the Mac — and TrSetup's Mac role automates the Mac-side prerequisites those guides list (§6.5).
7. **Report export**: one Markdown/HTML report of the full board, suitable to paste into a Claude session when something needs human/AI diagnosis.

**Non-goals**

- **Not** a re-implementation of TrStudioAdmin's *management* surface. Boundary (revised 2026-07-05): TrSetup now **does install host-level runtimes including the isolated Python env and ComfyUI itself** (owner direction); TrStudioAdmin remains the owner of everything *above* the runtime — model downloads into the registry, workflow import/mapping, RunPod endpoints, provider config. Rule of thumb: **TrSetup gets the machine to "the app can start"; the app's own admin takes it from there.**
- **Not** a secrets manager. It checks a key/token is **present and non-empty only** (locked decision: presence-only, no probing of paid APIs); it never stores, displays, or transmits secret values.
- **Not** an installer for the apps' own binaries — the guides + Mac-side build automation cover that; TrSetup prepares the *environment*.
- No auto-elevation tricks: anything needing admin/sudo is run visibly with consent (§7).

## 3. Users & machine roles

Same person today, but four distinct **machine roles** (selected on first run, changeable; a machine can hold several — the Mac is typically *device host + app runner*):

| Role | Machine | What TrSetup checks/fixes there |
|---|---|---|
| **Agent host (WSL)** | WSL distro on the Windows box | §0 bootstrap (apt libs, winrun, PATH), Playwright, .NET SDK, reachability of the other roles, per-app WSL-side needs |
| **Device host (Windows)** | The Windows side of the same box | `.wslconfig` mirrored networking, Android SDK + AVD, Node/npm, Appium + uiautomator2, `start-android-verify.ps1`, MAUI workload, per-app Windows-side needs |
| **Device host (Mac)** | LAN Mac | Xcode CLT presence, .NET SDK + MAUI workload, Node/npm, Appium + xcuitest + mac2, Appium service on `0.0.0.0:4723`, firewall/LAN reachability |
| **App runner (Mac)** | Same LAN Mac | Everything needed to **build and run the portfolio apps locally** per the selected app's profile: .NET SDK + workloads, Xcode, git, private NuGet feed auth, Postgres+PgVector, ffmpeg, Python + ComfyUI, app config keys — then the one-command Mac build of the app itself (§6.5) |

Native macOS / native Windows / native Linux dev (WORKFLOW §16) is the same check catalog with the WSL-bridge items dropped; the role picker covers it ("I develop natively on this machine").

## 4. Architecture — one Blazor UI, three heads, one TUI

Follow the exact pattern the owner's apps already use (shared RCL + multiple heads):

```
TrSetup.sln
├── TrSetupUI/           Razor Class Library — ALL screens, built on TrBlazeUI
├── TrSetup.Core/        check engine, fixers/installers, profiles, process runner (no UI deps)
├── TrSetup.App/         MAUI Blazor Hybrid head → Windows (unpackaged exe) + Mac Catalyst
├── TrSetup.Web/         Blazor Server (Kestrel) head → WSL / native Linux (opens in browser)
└── TrSetup.Cli/         Spectre.Console TUI (`trsetup`) + `trsetup --check --json` for agents/CI
```

- **Windows & Mac**: the MAUI Blazor Hybrid head — native window; `WindowsPackageType=None` unpackaged on Windows (AppStudio's host model); **Mac Catalyst via MAUI** on macOS (locked decision — one codebase, no separate signed standalone; ad-hoc signing is fine for personal LAN use).
- **WSL/Linux GUI**: the Blazor Server head bound to localhost — run `trsetup gui` in WSL, it starts Kestrel and opens `http://localhost:5999` in the **Windows** browser via mirrored networking (dogfoods the same bridge it checks).
- **TUI fallback** (locked decision): `trsetup` with no args runs the Spectre.Console TUI — the same board (grouped checks, statuses, evidence) rendered in the terminal, arrow-key selection, `f` to fix, `a` fix-all, `r` re-check, `e` export report. Works over SSH and when no browser is reachable. Same `TrSetup.Core` engine — the TUI is a thin renderer, never a second implementation.
- **.NET 10 + TrBlazeUI** (consistent with the portfolio; TrSetup is itself a first-class TechieFlow app — this repo is already scaffolded with the framework, and gets its own BRD/checklist/DevGuide).
- **`TrSetup.Core` is UI-free** so GUI, TUI and `--json` share one engine; `--json` lets the verifier pre-flight the environment itself later (P5).

## 5. The check engine

Every item on the board is a **Check** implementing one contract:

```
Check
├── Id, Title, Category, Roles (which machine roles it applies to)
├── Severity        Required | Recommended | Optional
├── DetectAsync()   → Pass | Warn | Fail | NotApplicable   (+ evidence text)
├── Explain         what it is, why it's needed, doc link (WORKFLOW § anchor)
├── FixAsync()?     null = manual-only; otherwise the scripted install/remediation
├── FixPreview      the literal command(s)/download Fix will run — always shown first
└── VerifyAsync()   re-detect after fix (Fix isn't done until Detect passes)
```

Principles:

- **Detect → Preview → Fix → Re-verify.** A fix that doesn't re-detect green is reported as failed with the raw output attached. Never "assume fixed".
- **Fix means install.** Fixers download official installers/archives (dotnet-install scripts, Node LTS, Android cmdline-tools, ComfyUI release + its own isolated Python, Homebrew/winget where already present) with checksums where published, into TrSetup-managed locations that never collide with system installs (the TrStudio isolated-Python discipline, generalized).
- **Idempotent fixes** (same discipline as `update-framework.sh`'s .gitignore block): running Fix twice is safe; existing user config is respected, never rewritten.
- **Everything is visible**: the exact command line, its stdout/stderr, and exit code live in an expandable detail pane per check. TrSetup is also *teaching* the user what the setup actually is.
- **Elevation is explicit** (§7).
- Checks that depend on another machine (e.g. "Mac Appium reachable") are plain HTTP/ping probes — TrSetup never remote-executes; you run TrSetup *on* the Mac to fix Mac items.

## 6. Check catalog (initial)

### 6.1 Framework core — role: WSL (agent host)

| Check | Detect | Auto-fix |
|---|---|---|
| .NET SDK present (9/10 as configured) | `dotnet --list-sdks` | install via dotnet-install script ✔ |
| Headless-Chromium apt libs (§0 list) | `dpkg -s` each | `sudo apt-get install -y …` ✔ |
| `~/bin/winrun` bridge + executable + on PATH | file + `grep .bashrc` | write file + PATH line ✔ |
| Node.js present | `node --version` | install LTS ✔ |
| Playwright CLI + headless Chromium | `npx playwright --version` / browsers dir | `npm i -D playwright && npx playwright install chromium` ✔ |
| Mirrored networking active (WSL side) | probe a Windows-host port | manual (points at Windows-role fix) |
| Windows-host Appium reachable | `GET http://localhost:4723/status` | manual (run TrSetup on Windows) |
| Mac Appium reachable (if app ships iOS/Catalyst) | `GET http://<mac-ip>:4723/status` | manual (run TrSetup on Mac) |
| `git` present (for the owner's manual use) | `git --version` | `apt-get install git` ✔ |

### 6.2 Device host — role: Windows

| Check | Detect | Auto-fix |
|---|---|---|
| `.wslconfig` has `networkingMode=mirrored` | parse `%UserProfile%\.wslconfig` | append/patch + prompt `wsl --shutdown` ✔ |
| Android SDK + `sdkmanager`/`avdmanager` | probe standard SDK locations | **download cmdline-tools + install SDK** ✔ |
| API-34 system image installed | `sdkmanager --list_installed` | `sdkmanager "system-images;android-34;…"` ✔ |
| `Pixel_API_34` AVD exists | `avdmanager list avd` | `avdmanager create avd …` ✔ |
| Node + npm | `node --version` | install LTS (winget/download) ✔ |
| Appium installed + `uiautomator2` driver | `appium --version`, `appium driver list --installed` | `npm i -g appium && appium driver install uiautomator2` ✔ |
| `start-android-verify.ps1` session helper deployed | file check | write from embedded template ✔ |
| Appium answers on `:4723` (session test) | boot helper → `/status` | run the helper ✔ |
| MAUI workload | `dotnet workload list` | `dotnet workload install maui` ✔ |
| JDK for Android builds | `java -version` / JAVA_HOME | install Temurin ✔ |

### 6.3 Device host — role: Mac

| Check | Detect | Auto-fix |
|---|---|---|
| Xcode / Command-Line Tools | `xcode-select -p` | CLT: `xcode-select --install` ✔; full Xcode: manual (App Store — the one genuine manual step) |
| .NET SDK + MAUI workload | `dotnet workload list` | install SDK + `dotnet workload install maui` ✔ |
| Node + npm | `node --version` | install LTS ✔ |
| Appium + `xcuitest` + `mac2` drivers | `appium driver list --installed` | install ✔ |
| Appium serving on `0.0.0.0:4723` (LaunchAgent, survives reboot) | `GET /status` from LAN address | write LaunchAgent plist + load ✔ |
| Stable IP / hostname advertised | current IP vs configured endpoint | manual guidance (DHCP reservation) |
| iOS Simulator runtime present | `xcrun simctl list` | `xcodebuild -downloadPlatform iOS` ✔ |

### 6.4 Per-app profiles

A profile is a **declarative file** (`trsetup-profile.json`) of requirement *instances* of generic check types: `sdk`, `workload`, `cli-tool`, `service`, `endpoint`, `nuget-feed`, `env-secret`, `disk-space`, `appium-head`, `runtime-install` (e.g. ComfyUI). Built-in profiles ship for the known apps; an app repo may carry `.tfcore/trsetup-profile.json` which **overrides the built-in** (locked decision: both, app repo wins). New app = new profile, no tool code. Each requirement is tagged with the roles it applies to — so one profile drives both "build it on Windows" and "run it on the Mac".

**AppStudio profile** (from `AppStudio-BRD.md` §12):

| Requirement | Check type | Roles | Notes |
|---|---|---|---|
| .NET 10 SDK | `sdk` | Win, Mac-runner | |
| MAUI workload | `workload` | Win, Mac-runner | Windows head unpackaged; Mac Catalyst head builds on the Mac (§6.5) |
| `dotnet` + `git` on PATH | `cli-tool` | Win, Mac-runner | the IDE shells out to both at runtime |
| Xcode | `cli-tool` | Mac-runner | Catalyst build prerequisite |
| techierathore GitHub Packages feed usable | `nuget-feed` | Win, Mac-runner | TrBlazeUI is private; validates NuGet source + PAT (`read:packages`) authenticates |
| App Manager API reachable | `endpoint` | Win, Mac-runner | dev `https://localhost:5101/`, prod `https://api.appmanager.com` |
| AppManager applicationId/API key configured | `env-secret` | Win, Mac-runner | presence-only |

**TrStudio profile** (from `TrStudio-BRD.md` §12; heads as-built are Blazor Server web apps):

| Requirement | Check type | Roles | Notes |
|---|---|---|---|
| .NET 10 SDK (or none if self-contained publish) | `sdk` | Win, Mac-runner | |
| PostgreSQL running + **PgVector extension** | `service` | Win, Mac-runner | app DB + Hangfire queue + RAG bible; auto-install (winget/brew + `CREATE EXTENSION vector`) ✔ |
| `ffmpeg` on PATH | `cli-tool` | Win, Mac-runner | auto-install ✔ |
| Isolated Python env + **ComfyUI** installed | `runtime-install` | Win, Mac-runner | **TrSetup installs it** (owner decision 2026-07-05); TrStudioAdmin manages models/workflows on top |
| Disk space for models (configurable floor) | `disk-space` | Mac-runner | warn-level |
| GitHub PAT for TechieRag NuGet | `nuget-feed` | Win | build-time only |
| `RunPod:ApiKey` / HeyGen key / AppManager key present | `env-secret` | Win, Mac-runner | presence-only (locked) |
| AppManager endpoint reachable | `endpoint` | Win, Mac-runner | |

**Framework profile (always on):** the §6.1–6.3 core for the machine's roles, plus — when run inside an app repo — offering to write the `core-config.yaml → runtimeVerification.appium` block (§0b step 4) from the endpoints it just verified, and `curl`-verifying each registered head.

### 6.5 Build on Windows → run on Mac (the guides + Mac-side automation)

**The honest constraint first:** a Windows `.exe` is a Windows binary — it can never execute on macOS, and Visual Studio on Windows **cannot build a Mac Catalyst app** (Microsoft supports Catalyst builds only *on* a Mac; VS's "pair to Mac" covers iOS only). So "build on Windows, copy to Mac" splits into two real paths:

| App head type | Windows side (Visual Studio) | Mac side |
|---|---|---|
| **MAUI Blazor Hybrid** (AppStudio, TrSetup.App) | Build/publish the **Windows unpackaged exe** — for the Windows machine only | Build the **Mac Catalyst `.app` on the Mac** — one command (`dotnet build -f net10.0-maccatalyst -c Release`), Xcode + MAUI workload present. **TrSetup's Mac app-runner role automates this**: installs the prerequisites, then offers "Build & install <App> for Mac" as a fixer |
| **Blazor Server web** (TrStudio×3, TrSetup.Web) | `dotnet publish -r osx-arm64 --self-contained` **on Windows** → a genuine "copy to the Mac and run" folder (no .NET needed on the Mac) | copy → `chmod +x` → run → open in browser |

Each app ships a step-by-step **`docs/<APP>-BuildAndRun-Guide.md`** (human doc, HTML-rendered like other guides) covering: VS build on Windows (GUI steps + `dotnet` CLI equivalent), the Mac path for its head type, the Mac prerequisites (= the app's TrSetup profile, cross-referenced), and first-run configuration. Written now for **AppStudio** and **TrStudio** (they exist); TrSetup's own guide ships with P4. TrSetup's job is to make the *prerequisites* section of every guide a single "run TrSetup, click Fix all" line.

## 7. Security & permissions stance

- **Consent per elevation.** sudo/admin actions (apt-get, `.wslconfig`, LaunchAgent, service installs) always show the exact command and run only on click; on Windows, UAC-elevated steps run in a visible child process. No stored sudo passwords — WSL fixes needing sudo run via an interactive terminal handoff (the app prints the one command to paste) if the user prefers.
- **Installers from official sources only** (dotnet.microsoft.com scripts, nodejs.org, Google's Android repos, ComfyUI GitHub releases, Homebrew/winget), checksum-verified where the source publishes checksums; download URLs pinned in the profile/engine and visible in FixPreview.
- **Secrets:** presence checks only (locked decision — no probing of paid APIs). Values never logged, never in the exported report.
- **Network:** outbound only to the pinned installer sources + endpoints the user configured; no telemetry.
- **Everything it writes is idempotent and marked** (comment blocks à la the script-managed `.gitignore`), so re-runs never duplicate and user edits are never clobbered.

## 8. UX sketch

Single-window dashboard (TrBlazeUI, same light/dark discipline as the doc shell); the TUI renders the same tree in the terminal:

```
┌─ TrSetup ──────────────────────────── roles: [Mac device host + app runner ▾]  app: [AppStudio ▾] ─┐
│  ● Framework core           5 ✓   1 ⚠   1 ✗          [Fix all]                                     │
│    ✗ Appium mac2 driver missing              [Preview] [Fix]                                       │
│  ● Bridges (cross-machine)   1 ✓   1 ✗                                                             │
│  ● AppStudio profile         4 ✓   2 ✗               [Fix all]                                     │
│    ✗ MAUI workload not installed             [Preview] [Fix]                                       │
│    ✗ GitHub Packages feed: 401                [Open guide]                                         │
│  ● Build & run               [Build AppStudio for Mac (Catalyst)]                                  │
│                                                       [Re-check all]  [Export report]              │
└──────────────────────────────────────────────────────────────────────────────────────────────────┘
```

- Row click → detail pane: Explain (why + WORKFLOW § / guide link), Detect evidence, Fix preview, last run output.
- **Fix all** runs fixes sequentially in dependency order (Node before Appium, SDK before AVD, Postgres before PgVector…), stopping at the first consent/elevation gate.
- TUI: same groups/statuses via Spectre.Console; keys `f` fix, `a` fix-all, `r` re-check, `e` export.
- Export → `TrSetup-Report-<host>.md` (+ HTML via the shared shell), safe to hand to a Claude session.

## 9. Build phases (each independently shippable)

| Phase | Deliverable |
|---|---|
| **P1 — Doctor (read-only)** | `TrSetup.Core` + all Detects for the 4 roles + framework core catalog; Blazor Server head **and the TUI** (both are thin renderers over Core); report export. *No fixes yet — already kills the "is my machine ok?" pain.* |
| **P2 — Fixers/installers** | FixAsync for every ✔ row in §6.1–6.3 incl. SDK downloads, with preview/consent/re-verify; dependency-ordered Fix-all. After P2 proves out, WORKFLOW §0/§0b shrink to "run TrSetup; manual steps below as reference" (locked decision — alongside until then). |
| **P3 — App profiles** | Profile schema + loader (built-in + `.tfcore/trsetup-profile.json` override, app repo wins); AppStudio + TrStudio profiles incl. the `runtime-install` fixers (Postgres+PgVector, ffmpeg, Python+ComfyUI); `core-config.yaml` appium-block writer; "Build <App> for Mac" fixer. |
| **P4 — Native heads** | MAUI Blazor Hybrid head → Windows unpackaged exe + **Mac Catalyst via MAUI** (locked); `TrSetup-BuildAndRun-Guide.md`; distribution scripts. |
| **P5 — CLI/agent mode (stretch)** | `trsetup --check --json` for agent/CI pre-flight; possible verify-phase §0 hook ("environment pre-flight ran green"). |

Development follows TechieFlow end-to-end: this plan → `*day1-greenfield TrSetup` (plan doc as source-doc hint; includes `*mockups`) → review/approve → `*split-brd` (may run inline via day1 §3.5, since this plan is phased) → `*build-phase` — TrSetup is app #11 in the portfolio and dogfoods the framework (this repo is already scaffolded).

## 10. Decisions (locked 2026-07-05)

1. **Name:** **TrSetup**.
2. **Repo:** own repo at `/mnt/c/3AIGenCode/TrSetup`, TechieFlow framework scaffolded in (done 2026-07-05; `git init` is the owner's manual step).
3. **WSL head:** Blazor Server opened in the Windows browser **plus a terminal TUI fallback** (`TrSetup.Cli`, Spectre.Console).
4. **Profiles:** baked-in **and** per-app `.tfcore/trsetup-profile.json` override — **app repo wins**.
5. **Secrets checks:** **presence-only** — never probe paid APIs.
6. **WORKFLOW §0/§0b:** TrSetup sits **alongside** the prose until P2 proves out, then the sections shrink to "run TrSetup" with the manual steps kept as reference.
7. **Mac distribution:** **Mac Catalyst via MAUI** (one codebase); ad-hoc signing is fine for personal LAN use.
8. *(Added same day)* **Auto-install mandate:** any missing required SDK/tool/runtime — Node.js, Android SDK, .NET SDK, Python, **ComfyUI**, etc. — TrSetup downloads and installs itself; the TrStudioAdmin boundary moves up to models/workflows/providers.
9. *(Added same day)* **Build-and-run guides:** step-by-step `<APP>-BuildAndRun-Guide.md` docs for TrSetup, AppStudio and TrStudio covering VS-on-Windows builds and running on the Mac; TrSetup's Mac role installs those guides' prerequisites and automates the Mac-side app builds.

---
*Drafted 2026-07-05 from WORKFLOW §0/§0b/§16, `AppStudio-BRD.md` (BRD-36/55/60, §12), `TrStudio-BRD.md` (§12 constraints, F-ADMIN/F-RUNPOD/F-IDENTITY gating); decisions locked with the owner the same day (§10). Supersedes `TechieFlow/docs/TrDoctor-Plan.md` (removed). Next: `*day1-greenfield TrSetup` with this plan as the source-doc hint.*
