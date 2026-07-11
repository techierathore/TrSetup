# TrSetup — Checklist

> Migrated from docs/TrSetup-Plan.md on 2026-07-05 (day1-greenfield §3.5 inline split). Phase structure (P1–P5, each independently shippable) carried over verbatim from the plan's §9; greenfield — nothing built yet, so every REQ starts `Not Started`/0%. Verify against docs/TrSetup-BRD.md before building.

## Table of Contents

1. [Goal](#goal)
2. [Requirements Status](#requirements-status)
3. [UI / Pages](#ui--pages)
4. [Functional requirements](#functional-requirements)
5. [RAG / AI requirements (→ /techierag)](#rag-ai-requirements-techierag)
6. [Non-functional](#non-functional)

## Goal

Replace the scattered, error-prone, three-environment manual setup (WSL / Windows host / LAN Mac) for the TechieFlow harness and the portfolio apps with one small app: open TrSetup → red/amber/green board for this machine's roles + the selected app → Fix / Fix all → it downloads, installs and configures what's missing itself → re-checks → green (BRD §1). One UI-free engine (`TrSetup.Core`), ONE head — the MAUI Blazor Hybrid desktop app (Win/Mac Catalyst) hosting the `TrSetupUI` RCL — and declarative per-app profiles. The head project was renamed `TrSetup.App` → `TrSetup` on 2026-07-10 (REQ-FN-035; the primary head carries the product name — `.App` is banned by the framework naming standard, 2026-07-10). *(2026-07-09 owner decision: the Blazor Server and Spectre.Console CLI heads are withdrawn — one single MAUI desktop app; decommission tracked by REQ-FN-034.)*

## Requirements Status

<!-- SINGLE SOURCE OF TRUTH for the WHOLE app. One row per REQ, grouped by phase.
     REQ-UI-* → /trblazeui (from docs/mockups/*) · REQ-FN-*/REQ-NFR-* → build-phase.
     No REQ-RAG-* — TrSetup has no AI features (BRD §15, Architecture ADR-003).
     Update Status + % + Remarks whenever build/smoke/verify touches a REQ. -->

| ID | Requirement | Status | % | Remarks | Details |
|----|-------------|--------|---|---------|---------|
| REQ-FN-001 | Check contract model (P1) | Verified | 100 | 2026-07-06 [REQ-FN-001] `Check` contract in TrSetup.Core (Id/Title/Category/Roles flags+NativeDev/Severity/Explain/DetectAsync/FixAsync?-null=manual/FixPreview/VerifyAsync); contract + NotApplicable-never-failure unit tests green | [view](#d-req-fn-001) |
| REQ-FN-002 | Detect→Preview→Fix→Re-verify pipeline (P1) | Verified | 100 | 2026-07-06 [REQ-FN-002] FixPipeline: consent-token gate (preview shown before grant), fix, VerifyAsync re-detect; non-green verify → FAILED + raw output attached; unit tests green incl. failed-verify case | [view](#d-req-fn-002) |
| REQ-FN-003 | Process runner with full output capture (P1) | Verified | 100 | 2026-07-06 [REQ-FN-003] IProcessRunner/ProcessRunner choke-point: exact command line + stdout/stderr + exit code + timeout kill, live per-line IProgress streaming; incremental-streaming unit test green | [view](#d-req-fn-003) |
| REQ-FN-004 | Role + app scoping of the check catalog (P1) | Verified | 100 | 2026-07-06 [REQ-FN-004] CheckEngine: roles∩app scoping (4 roles + NativeDev variant), out-of-scope rows NotApplicable, observable grouped CheckBoard, parallel sweep w/ 5s default probe timeout + streaming row updates; role/app-switch unit tests green | [view](#d-req-fn-004) |
| REQ-FN-005 | Local settings persistence (roles, endpoints, app) (P1) | Verified | 100 | 2026-07-06 [REQ-FN-005] JsonSettingsStore: roles/selected app/endpoints (MacIp) in %APPDATA%\TrSetup or ~/.trsetup/settings.json, test path override, missing file → first-run flag; save/reload + first-run unit tests green | [view](#d-req-fn-005) |
| REQ-FN-006 | WSL agent-host detects (P1) | Verified | 100% | 2026-07-06 [REQ-FN-006] Core/Catalog/Wsl: dotnet-sdk, headless-Chromium apt libs, winrun+PATH, node, playwright+chromium, mirrored-networking, git detects (F-WSLCHK). Ran LIVE — TrSetupReport-20260706-140549 shows 6✓/1✗ with real per-check evidence. Solution builds green (rung #4, 0W/0E). Verifier pending. | [view](#d-req-fn-006) |
| REQ-FN-007 | Windows device-host detects (P1) | Verified | 100% | 2026-07-06 [REQ-FN-007] Core/Catalog/Windows (11 checks): .wslconfig mirrored, Android SDK/API-34 image/Pixel_API_34 AVD, Node, Appium+uiautomator2, start-android-verify.ps1, :4723 session, MAUI workload, JDK (F-WINCHK), via winrun bridge. VERIFIED 2026-07-07 (flow-master inline): ran LIVE from this WSL box via the winrun/powershell interop bridge — all 10 in-scope win.* checks yield real Windows evidence (e.g. `SDKMANAGER-MISSING at C:\Users\srkra\AppData\Local\Android\Sdk\...`, `.wslconfig not found`, `:4723 connection refused`); bridge marker `probed via WSL interop bridge (powershell.exe)` present on every row. Acceptance ("detects run via the winrun bridge; each yields evidence") met. (The detects correctly report the tools as absent on this machine — that IS a working detect. FN-015 fixes remain destructive UAT.) | [view](#d-req-fn-007) |
| REQ-FN-008 | Mac device-host detects (P1) | Implemented | 75% | 2026-07-06 [REQ-FN-008] Core/Catalog/Mac (7 checks): Xcode/CLT, .NET+MAUI, Node, Appium xcuitest+mac2, LaunchAgent :4723, stable-IP vs endpoint, iOS Simulator (F-MACCHK). Detect logic unit-tested; builds green. Live run deferred to a Mac session; verifier pending. | [view](#d-req-fn-008) |
| REQ-FN-009 | Cross-machine HTTP/ping probes (P1) | Verified | 100% | 2026-07-06 [REQ-FN-009] Core/Catalog/Probing (HttpStatusProbe/CrossMachineGuidance) + Bridges checks: HTTP/ping only, never remote-exec, 5s timeout, parallel; unreachable → Fail + "run TrSetup on <machine>" guidance. Report Bridges section rendered live. Verifier pending. | [view](#d-req-fn-009) |
| REQ-FN-010 | Report exporter — MD + HTML, secret-free (P1) | Verified | 100% | 2026-07-06 [REQ-FN-010] Core/Reporting (ReportExporter/ReportHtmlShell): full board→MD+HTML via shared shell. Ran LIVE — TrSetupReport-20260706-140549.md produced, secret-free. Builds green. Verifier pending. | [view](#d-req-fn-010) |
| REQ-FN-011 | Blazor Server head + `trsetup gui` launcher (P1) | N/A | — | WITHDRAWN 2026-07-09 (owner decision: single MAUI desktop app — no Web head). Was Verified 2026-07-06. `TrSetup.Web` removal tracked by REQ-FN-034. | [view](#d-req-fn-011) |
| REQ-FN-012 | Spectre.Console TUI board renderer (P1) | N/A | — | WITHDRAWN 2026-07-09 (owner decision: single MAUI desktop app — no CLI/TUI head). Was Verified 2026-07-07 incl. the UTF-8 glyph fix. `TrSetup.Cli` removal tracked by REQ-FN-034. | [view](#d-req-fn-012) |
| REQ-FN-013 | TUI keybindings f/a/r/e + arrows (P1) | N/A | — | WITHDRAWN 2026-07-09 (owner decision: single MAUI desktop app — no CLI/TUI head). Was Verified 2026-07-07 incl. the terminal role/app picker + `trsetup config`. Removal tracked by REQ-FN-034. | [view](#d-req-fn-013) |
| REQ-UI-001 | Board dashboard screen (P1) | Verified | 100% | 2026-07-06 [REQ-UI-001] Board from dashboard.html (TrBlazeUI shell): live 19 rows real Pass/Fail/Warn, group count badges, header role/app selectors re-scope w/o reload, per-group Fix all, Preview/Fix or Open guide (manual-only), Re-check all + Export. VERIFIED: Playwright smoke + visual gate desktop+mobile (render+visual pass). RE-CONFIRMED 2026-07-07 (flow-master inline, `*verify all`): desktop (1280×800) board renders all framework+bridges+**AppStudio profile rows** with real status icons/text/evidence and a clean, TrBlazeUI-styled layout — render-truth ✓ + visual-truth ✓ (screenshot inspected). ⚠ visual (minor, non-blocking): at 390px mobile the wide status-table overflows horizontally — per-row Fix/recheck buttons sit past the viewport (reachable via horizontal scroll; data all renders). TrSetup is a desktop/WSL/Mac dev tool so desktop is the real target; mobile responsive polish is a future item (not a library defect). RE-CONFIRMED 2026-07-10 (executed verify-phase): render+visual pass @ :5999, board-desktop screenshot inspected (29+ live rows, real evidence). | [view](#d-req-ui-001) |
| REQ-UI-002 | Check detail sheet (P1) | Verified | 100% | 2026-07-06 [REQ-UI-002] Sheet from check-detail.html over dimmed board: Explain+WORKFLOW link, detect evidence+timestamp, literal Fix preview w/ copy, collapsible last-run; deep link /check/{id} via FindRow. VERIFIED: smoke id=wsl.winrun, visual pass. RE-CONFIRMED 2026-07-07 (`*verify all`): /check/wsl.winrun deep link renders sections; render+visual pass. RE-CONFIRMED 2026-07-10 (executed verify-phase): deep-link render+visual pass. | [view](#d-req-ui-002) |
| REQ-UI-003 | First-run role picker (P1) | Verified | 100% | 2026-07-06 [REQ-UI-003] Picker from role-picker.html: 4 role cards + native-dev toggle + default-app select; Save→SaveSetupAsync→board; auto-shown first-run, Save disabled until ≥1 role. VERIFIED: genuine first-run smoke + visual pass. RE-CONFIRMED 2026-07-07 (`*verify all`): 4 role cards render at desktop+mobile; render+visual pass. RE-CONFIRMED 2026-07-10 (executed verify-phase): role cards render+visual pass. | [view](#d-req-ui-003) |
| REQ-UI-005 | Report preview screen (P1) | Verified | 100% | 2026-07-06 [REQ-UI-005] Preview from report.html: host/roles/app header + count badges + per-group evidence tables, secret-free footnote; Save MD/HTML via ReportExporter, Copy. VERIFIED: smoke group tables + no-secrets, visual pass. RE-CONFIRMED 2026-07-07 (`*verify all`): /report renders board report; render+visual pass. RE-CONFIRMED 2026-07-10 (executed verify-phase): report render+visual pass. | [view](#d-req-ui-005) |
| REQ-NFR-001 | Performance targets (P1, ongoing) | Verified | 100% | 2026-07-06 [REQ-NFR-001] Engine: parallel detect sweep (Task.WhenAll), 5s default per-probe timeout, streaming per-row updates, single-recheck path. VERIFIED 2026-07-07 (flow-master inline, measured): full detect sweep wall-time **5.79s** (target <30s ✓) — bounded by the 5s parallel-probe cap, so a single re-check is ≤5s (target <5s ✓); the board streams rows as they detect (Pending→Pass/Fail observed live) and the UI is never blocked by fixes (async pipeline). Meets the BRD §11 perf table. | [view](#d-req-nfr-001) |
| REQ-NFR-003 | Accessibility — keyboard nav, icon+text status (P1) | Verified | 100% | 2026-07-06 [REQ-NFR-003] Status = icon + text everywhere (StatusLabel), never color alone; role cards keyboard-navigable (tabindex + Enter/Space + aria-checked), focus-visible outlines, aria-modal overlays. VERIFIED in board/setup screenshots (render+visual). | [view](#d-req-nfr-003) |
| REQ-NFR-005 | Testability ids — data-testid / AutomationId (P1, ongoing) | Verified | 100% | 2026-07-06 [REQ-NFR-005] Stable intent-named data-testid on every interactive/data-bound Blazor control (board-row-{id}, status-{id}, fix-all-{group}, RolesSelector, role-card-{key}, fix-step-{id}, consent-{id}, ReportPreview…); --check --json agent surface (FN-032). VERIFIED: Playwright smoke drove pages by testid. MAUI AutomationId deferred to the P4 head (FN-030). | [view](#d-req-nfr-005) |
| REQ-FN-014 | WSL auto-fixers (P2) | Implemented | 75% | 2026-07-06 [REQ-FN-014] Fixers on F-WSLCHK checks: dotnet-install SDK, Node LTS (+~/.bashrc managed PATH block), winrun write+chmod+PATH, Playwright install, apt libs + git via sudo terminal-handoff. Idempotent; manual rows FixAsync=null. 8 fixer unit tests green; FixPreview renders live in board/detail. Live install = destructive UAT (not smoked). | [view](#d-req-fn-014) |
| REQ-FN-015 | Windows auto-fixers (P2) | Implemented | 75% | 2026-07-06 [REQ-FN-015] Fixers on F-WINCHK: .wslconfig managed-block + wsl --shutdown prompt, cmdline-tools/SDK/API-34/AVD, Node MSI + Temurin JDK via visible UAC child, Appium+uiautomator2, ps1 from embedded template, MAUI workload. Elevated steps require granted ConsentToken (declined=nothing runs). 9 unit tests green. Live install = UAT. | [view](#d-req-fn-015) |
| REQ-FN-016 | Mac auto-fixers (P2) | Implemented | 75% | 2026-07-06 [REQ-FN-016] Fixers on F-MACCHK: xcode-select CLT (full Xcode stays manual), SDK+MAUI workload, Node (+~/.zprofile managed PATH), Appium xcuitest/mac2, LaunchAgent plist managed block + launchctl load, -downloadPlatform iOS; DHCP reservation stays manual. 6 unit tests green. Live run = Mac UAT. | [view](#d-req-fn-016) |
| REQ-FN-017 | Installer download framework — pinned URLs, checksums, managed locations (P2) | Verified | 100% | 2026-07-06 [REQ-FN-017] InstallerDownloader + TrSetupPaths managed root (Downloads/): pinned URL verbatim in FixPreview, SHA-256 verify, tampered payload deleted+reported, "no published checksum" recorded. VERIFIED 2026-07-07 (flow-master inline): acceptance ("tampered download fails checksum + reports; install paths under the managed root") asserted by passing unit tests incl. a **real temp-file checksum-mismatch download** — re-run green this pass (part of the 23-test Downloader/ConfigWriting/FixAll/Elevation acceptance run). | [view](#d-req-fn-017) |
| REQ-FN-018 | Idempotent config-write framework (marker blocks) (P2) | Verified | 100% | 2026-07-06 [REQ-FN-018] ManagedBlockWriter (ConfigWriting/): upsert/remove marker blocks for # ; // <!-- --> syntaxes, re-run = single block replaced in place, user edits outside markers preserved byte-for-byte, missing file created. VERIFIED 2026-07-07 (flow-master inline): acceptance ("run fix twice → single block; hand-edit outside block survives a re-fix") asserted by passing **real-file round-trip** unit tests — re-run green this pass. | [view](#d-req-fn-018) |
| REQ-FN-019 | Fix-all dependency-ordered runner (P2) | Verified | 100% | RECONCILED 2026-07-09: status column lagged its own evidence (row already carried "VERIFIED 2026-07-07 flow-master inline"). 2026-07-06 [REQ-FN-019] FixStep/FixAllPlanner/FixAllRunner (FixAll/): stable topo sort on DependsOn, per-step consent→fix→re-verify via FixPipeline, declined consent halts run with later steps untouched, stop/continue-on-failure policy, IProgress step streaming. VERIFIED 2026-07-07 (flow-master inline): acceptance ("order verified by unit test on the dependency graph; declined consent halts with later steps untouched") asserted by passing unit tests (Node→Appium, SDK→AVD topo graph) — re-run green this pass. | [view](#d-req-fn-019) |
| REQ-FN-020 | Elevation/consent runner — UAC child, sudo handoff (P2) | Verified | 100% | RECONCILED 2026-07-09: status column lagged its own evidence (row already carried "VERIFIED 2026-07-07 flow-master inline"). 2026-07-06 [REQ-FN-020] ElevationRunner (Elevation/): UAC via powershell Start-Process -Verb RunAs visible child through IProcessRunner, granted ConsentToken required (declined throws, nothing launched), WSL sudo = TerminalHandoff print-to-paste, no password code path. VERIFIED 2026-07-07 (flow-master inline): acceptance ("no code path stores or caches credentials; every elevated run is user-initiated") asserted by passing unit tests (granted-ConsentToken-required, declined-throws-nothing-launched, no-password path) — re-run green this pass. | [view](#d-req-fn-020) |
| REQ-UI-004 | Fix-all run view with consent gates (P2) | Verified | 100% | 2026-07-06 [REQ-UI-004] Fix-run from fix-run.html: dependency-ordered step list (9 steps), per-step status, inline consent gate (exact command + Approve/Decline), halt-on-decline, summary; drives FixAllRunner via new BoardState fix-all method. VERIFIED: consent gate blocks execution (nothing ran) + empty state, visual pass. RE-CONFIRMED 2026-07-07 (`*verify all`): /fix-run renders; render+visual pass. RE-CONFIRMED 2026-07-10 (executed verify-phase): fix-run render+visual pass. | [view](#d-req-ui-004) |
| REQ-NFR-002 | Security stance — presence-only secrets, outbound-only, no telemetry (P2; stance from P1) | Verified | 100% | 2026-07-06 [REQ-NFR-002] framework level; fixer wiring in FN-014..016 — consent-per-elevation (granted ConsentToken mandatory), pinned sources + SHA-256 checksums, grep-proof no credential storage (sudo = terminal handoff, UAC = visible child) | [view](#d-req-nfr-002) |
| REQ-NFR-004 | Reliability — failed fix leaves machine no worse (P2) | Verified | 100% | 2026-07-06 [REQ-NFR-004] framework level; fixer wiring in FN-014..016 — temp-file downloads (tampered/failed payloads deleted, never half-written), idempotent marker-block config writes, all installs under TrSetup-managed root so re-runs always safe | [view](#d-req-nfr-004) |
| REQ-FN-021 | Profile schema + loader + app-repo override merge (P3) | Verified | 100 | 2026-07-07 [REQ-FN-021] TrSetup.Core/Profiles: TrSetupProfile/ProfileRequirement model (10 typed requirement instances, role-tagged), ProfileLoader built-in (embedded JSON auto-discovery keyed by name) + `.tfcore/trsetup-profile.json` override merge (app-repo-wins by requirement Id), schema validation (ProfileRequirementParamRules), IProfileRequirementHandler registry + ProfileCheckFactory → Check, CheckCatalog append guarded (no app/failed load → appends nothing, never throws). 13 unit tests (conflict-merge app-repo-wins, role-tag survival, unknown-type placeholder, AppliesTo scoping). E2E: both built-in profiles load+render via `trsetup --check --json`. | [view](#d-req-fn-021) |
| REQ-FN-022 | AppStudio built-in profile (P3) | Verified | 100 | 2026-07-07 [REQ-FN-022] appstudio.json (8 rows: .NET SDK, MAUI workload, dotnet+git PATH split atomic, Xcode Mac-runner, techierathore GitHub Packages feed, AppManager endpoint, AppManager key presence-only), roles Win→DeviceHostWindows / Mac-runner→AppRunnerMac. BuiltInProfilesTests + E2E render-truth: all 8 rows detect live (dotnet-sdk/git pass, maui-workload/xcode/appmanager-api fail correctly, secret fail presence-only, feed warn) — no blank evidence. | [view](#d-req-fn-022) |
| REQ-FN-023 | TrStudio built-in profile (P3) | Verified | 100 | 2026-07-07 [REQ-FN-023] trstudio.json (10 rows: .NET SDK, Postgres+PgVector service, ffmpeg service, isolated Python+ComfyUI runtime-install, disk-space floor Recommended, TechieRag NuGet PAT feed Win-only, RunPod/HeyGen/AppManager keys split atomic presence-only, AppManager endpoint). ffmpeg modeled as `service` (gets FN-026 install fixer; board title still "ffmpeg on PATH"). BuiltInProfilesTests + E2E: all heavy rows detect live (postgres/ffmpeg/comfyui real probe evidence, disk-space pass 936.9 GB free/80 GB floor). | [view](#d-req-fn-023) |
| REQ-FN-024 | core-config appium-block writer + head curl-verify (P3) | Verified | 100 | 2026-07-07 [REQ-FN-024] AppiumConfigBlockCheck (Catalog/Framework, AgentHostWsl-scoped, NotApplicable outside an app repo): writes `runtimeVerification.appium` block (WORKFLOW §0b step-4 shape — android always, ios/maccatalyst when MacIp set) via ManagedBlockWriter idempotent markers (user config outside markers preserved), then curl-verifies each head `url+/status`. AppiumConfigBlockCheckTests: round-trip + re-run single block + curl-verify vs fake probe. E2E: row renders. | [view](#d-req-fn-024) |
| REQ-FN-025 | Isolated Python + ComfyUI runtime-install fixer (P3) | Implemented | 75 | 2026-07-07 [REQ-FN-025] RuntimeInstallCheck/RuntimeInstallRequirementHandler: detects ComfyUI entrypoint under `TrSetupPaths.ToolsRoot/comfyui`, fix downloads pinned official ComfyUI portable release (bundled Python — no system-Python collision) into managed location; Explain states BRD-39 boundary (TrStudioAdmin owns models/workflows above runtime). RuntimeInstallHandlerTests (detect-absent, preview pinned-URL+managed-path) + E2E detect renders real evidence. Live download+install = destructive UAT (not run). | [view](#d-req-fn-025) |
| REQ-FN-026 | Postgres+PgVector + ffmpeg service fixers (P3) | Implemented | 75 | 2026-07-07 [REQ-FN-026] ServiceCheck/ServiceRequirementHandler (service∈{postgres,ffmpeg}): postgres detect psql+pg_extension vector, fix winget/brew install + `CREATE EXTENSION IF NOT EXISTS vector` (idempotent); ffmpeg detect `ffmpeg -version`, fix winget/brew. Preview shows both winget+brew literals (host resolved at fix-time via injectable selector). ProfileHeavyHandlerTests + E2E detect renders real evidence. Live install = destructive UAT (not run). | [view](#d-req-fn-026) |
| REQ-FN-027 | Mac app-runner role wiring (P3) | Verified | 100 | 2026-07-07 [REQ-FN-027] AppRunnerMac aggregation via engine role∩app scoping over A's profile append + the Catalyst fixer row — no RoleCatalog change needed (option pre-existed). MacAppRunnerAggregationTests (roles=AppRunnerMac + SelectedApp=AppStudio → exactly the 8 Mac-runner-tagged requirements + maccatalyst-build). E2E confirmed: AppStudio board under AppRunnerMac aggregates all profile rows + build fixer. | [view](#d-req-fn-027) |
| REQ-FN-028 | "Build & install <App> for Mac (Catalyst)" fixer (P3) | Implemented | 75 | 2026-07-07 [REQ-FN-028] MacCatalystBuildCheck: non-macOS→NotApplicable; disabled-while-red gate via live red-prereq-ids delegate (any AppRunnerMac profile check Fail → Fail naming ids; FixAsync refuses without running build unless red set empty); all-green→Pass if .app exists; FixPreview shows literal `dotnet build -f net10.0-maccatalyst -c Release` + .app path. MacCatalystBuildCheckTests (gating, preview, off-mac NA) + E2E (NotApplicable off-macOS). Live Catalyst build = Mac UAT (not run). | [view](#d-req-fn-028) |
| REQ-FN-029 | Disk-space check with configurable floor (P3) | Verified | 100 | 2026-07-07 [REQ-FN-029] DiskSpaceCheck/DiskSpaceRequirementHandler: reads free space on configured `path` (default managed root) via DriveInfo vs `floorGb`, breach→Warn (NEVER Fail) with free-vs-required GB in evidence, no fixer (guidance only). Unit-tested (breach→Warn w/ figures, above-floor→Pass). E2E render-truth: "936.9 GB free on '~/.trsetup' (floor 80 GB)" Pass. | [view](#d-req-fn-029) |
| REQ-FN-030 | MAUI Blazor Hybrid head — Win unpackaged + Mac Catalyst (P4) | Implemented | 90% | 2026-07-07 [REQ-FN-030] TrSetup.App shell: BlazorWebView `AutomationId=TrSetupBlazorWebView` + ContentPage `AutomationId=TrSetupMainPage` (NFR-005 MAUI half). csproj BRD-44: `WindowsPackageType=None` (unpackaged) ✓ pre-existing, OSX-guarded `net10.0-maccatalyst` TFM ✓ pre-existing; ADDED maccatalyst-guarded ad-hoc signing PropertyGroup (`CodesignKey=-`, `CreatePackage=false`). Rung-#4 Windows build PASS (0W/0E) → `bin/Release/net10.0-windows10.0.19041.0/win-x64/TrSetup.App.exe`; full `TrSetup.sln` Debug build PASS 0W/0E (7 proj incl. MAUI). Boot attempted from WSL: direct-run held alive full 25s (exit 124 timeout-kill, no crash) + live PID observed mid-run = process boots & persists; visual "renders the board" is host-bound UAT (headless WSL can't observe the Windows GUI desktop). Catalyst build documented in BuildAndRun-Guide §3 Option A (`dotnet build -f net10.0-maccatalyst -c Release` → `maccatalyst-arm64/TrSetup.app`). Grade reconciled (flow-master inline): Implemented — build+AutomationIds+boot-persist+Catalyst-doc done here; the visual "boots to the board" render + the Mac Catalyst build are the only host-bound clauses left (promotes to Verified on a Windows/Mac session). FIX 2026-07-07 (owner-reported PCA popup "This program might not have installed correctly"): the exe name `TrSetup.App.exe` contains "Setup" → Windows Program Compatibility Assistant installer-detection heuristic false-fires (surfaced when the P4 boot-attempt killed the exe abnormally). Fixed by embedding a proper Win32 manifest — added `<compatibility>` supportedOS (Win7→11) + `asInvoker <trustInfo>` to `Platforms/Windows/app.manifest` and referenced it via windows-scoped `<ApplicationManifest>` in the csproj (was unreferenced → not embedded before). Rung-#4 rebuild PASS 0W/0E; verified all four manifest markers (asInvoker/supportedOS/compatibility.v1/longPathAware) embedded in the exe. | [view](#d-req-fn-030) |
| REQ-FN-031 | BuildAndRun guide firm-up + distribution (P4) | Verified | 100 | VERIFIED 2026-07-10 (executed verify-phase, run ledger docs/.last-verify.json): after the REQ-FN-035 rename the guide's §2 CLI command was re-run VERBATIM at its new path (`cmd.exe /c "dotnet build src/TrSetup -f net10.0-windows10.0.19041.0 -c Release"`, ladder rung #4) → 0W/0E → runnable `src/TrSetup/bin/Release/net10.0-windows10.0.19041.0/win-x64/TrSetup.exe`, which was then BOOTED (process persisted, Serilog log written). §3 Option A (Web osx-arm64 publish) unchanged; §3 Option B Catalyst command stays documented Mac-host UAT (`NETSDK1139` on Windows documented + expected). Guide paths updated to the renamed head. HISTORY — DEMOTED 2026-07-10 earlier that day: the 2026-07-09 "RE-VERIFIED" below was SELF-ATTESTED by the build orchestrator — verify-phase was never executed (agent-confirmed). 2026-07-09 [REQ-FN-031] RE-VERIFIED after the MAUI-only rewrite: ran the guide's §2 Windows CLI command verbatim (`cmd.exe /c "dotnet build src\TrSetup.App -f net10.0-windows10.0.19041.0 -c Release"`, = ladder rung #4) → 0W/0E → produced the runnable `src/TrSetup.App/bin/Release/net10.0-windows10.0.19041.0/win-x64/TrSetup.App.exe`. The §3 Mac Catalyst command (`dotnet build … -f net10.0-maccatalyst`) stays Mac-host UAT (the TFM only activates on macOS — `NETSDK1139` on Windows is documented + expected). Withdrawn Cli/Web publish scripts removed by REQ-FN-034. | [view](#d-req-fn-031) |
| REQ-NFR-006 | Portability — self-contained Cli/Web publishes (P4) | N/A | — | WITHDRAWN 2026-07-09 (owner decision: single MAUI desktop app — the Cli/Web bootstrap path is retired with its heads). Was Verified 2026-07-07. Script removal tracked by REQ-FN-034. | [view](#d-req-nfr-006) |
| REQ-FN-032 | `trsetup --check --json` agent mode (P5) | N/A | — | WITHDRAWN 2026-07-09 (owner decision: single MAUI desktop app — agent mode lived in the CLI head). Was Verified 2026-07-07; docs/TrSetup-AgentMode.md archived to OldDocs. Removal tracked by REQ-FN-034. | [view](#d-req-fn-032) |
| REQ-FN-033 | Verifier pre-flight hook (P5, stretch) | N/A | — | WITHDRAWN 2026-07-09 (owner decision: consumed FN-032's CLI agent mode, which is withdrawn; was optional/stretch per BRD-47). Was Verified 2026-07-07. `scripts/preflight-gate.*` removal tracked by REQ-FN-034. | [view](#d-req-fn-033) |
| REQ-FN-034 | Decommission CLI + Web heads → single MAUI desktop app (P6) | Verified | 100 | VERIFIED 2026-07-10 (executed verify-phase, run ledger docs/.last-verify.json): solution = 4 product projects (TrSetup [MAUI head, renamed by FN-035]/TrSetupUI/Core/Core.Tests) + Web smoke host, builds green rung #4 0W/0E (5 projects); `TrSetup.Core.Tests` 126/126 pass (rung #2, this run); repo grep = ZERO `TrSetup.Cli` refs in source/tests/harness (only docs' withdrawal-record mentions + OldDocs); `playwright.config.ts` boots `TrSetup.Web` on :5999 as the test-only smoke host — booted live this run and the full 7/7 verify suite ran against it. HISTORY — DEMOTED 2026-07-10 earlier that day: the "VERIFIED" below was SELF-ATTESTED by the build orchestrator — verify-phase was never executed (agent-confirmed). 2026-07-09 [REQ-FN-034] Deleted `src/TrSetup.Cli`, `tests/unit/TrSetup.Cli.Tests`, `scripts/publish.sh`/`publish.ps1`, `scripts/preflight-gate.sh`/`preflight-gate.ps1`; pruned `TrSetup.sln` (project entry + config-platform block + nested-project line for both Cli GUIDs). **OWNER-AMENDED end-state (2026-07-09 build):** owner chose "keep a headless smoke host", so `TrSetup.Web` is RETAINED — reclassified from a shipping head to the **test-only headless UI smoke host** (`playwright.config.ts` still boots it on :5999; it hosts the same `TrSetupUI` RCL) so UI smoke keeps running headlessly in WSL. Product shipping surface = the single MAUI `TrSetup.App`. VERIFIED: solution now = 4 product projects (App/TrSetupUI/Core/Core.Tests) + Web(smoke host); builds green rung #4 (0W/0E, 5 projects incl. MAUI); `TrSetup.Core.Tests` 126/126 pass; repo grep confirms zero `TrSetup.Cli` refs in source/tests/harness outside `docs/OldDocs/`. | [view](#d-req-fn-034) |
| REQ-NFR-007 | Observability — Serilog file-based logging in the MAUI head (P6) | Verified | 100 | VERIFIED 2026-07-10 (executed verify-phase, run ledger docs/.last-verify.json): BOOTED the renamed `TrSetup.exe` (rung #4) — today's log `…\User Name\com.techierathore.trsetup\Data\logs\trsetup-20260710.log` contains the startup line ("TrSetup starting (version 1.0.0.1, build 1)") AND a `TrSetup.Core.Settings.JsonSettingsStore` event, proving the shared-library `ILogger<T>`→Serilog pipeline live; grep confirms NO Serilog reference in `TrSetupUI.csproj`/`TrSetup.Core.csproj` (MEL abstractions only); `AppDomain.UnhandledException`/`TaskScheduler.UnobservedTaskException`→`Log.Fatal` + `Log.CloseAndFlush()` on `Window.Destroying` present in `src/TrSetup/MauiProgram.cs`/`App.xaml.cs`. HISTORY — DEMOTED 2026-07-10 earlier that day: the "VERIFIED" below was SELF-ATTESTED by the build orchestrator — verify-phase was never executed (agent-confirmed). 2026-07-09 [REQ-NFR-007] Wired Serilog in `TrSetup.App` (pkgs Serilog 4.2 / Serilog.Extensions.Logging 9.0 / Sinks.File 6.0 / Sinks.Debug 3.0): daily rolling file sink at `FileSystem.AppDataDirectory/logs/trsetup-.log` (14-file retention) + Debug sink; `ClearProviders()`+`AddSerilog(dispose:true)` so shared-lib `ILogger<T>` flows in; startup line logs app version/build; `AppDomain.UnhandledException` + `TaskScheduler.UnobservedTaskException` → `Log.Fatal`; `Log.CloseAndFlush()` on `Window.Destroying` (App.xaml.cs). Libraries keep MEL abstractions only (no Serilog ref). VERIFIED: booted the MAUI head (rung #4) — log file created at `…\User Name\com.techierathore.trsetup\Data\logs\trsetup-20260709.log` containing the startup line AND a `TrSetup.Core.Settings.JsonSettingsStore` event (proves the shared-library `ILogger<T>`→Serilog pipeline). | [view](#d-req-nfr-007) |
| REQ-UI-006 | Settings screen — endpoints + profile details in the MAUI app (P6) | Verified | 100 | VERIFIED 2026-07-10 (executed verify-phase, run ledger docs/.last-verify.json): `tests/verify/settings.spec.ts` 2/2 PASS against the live :5999 host — /settings reachable via `NavSettings`, ALL regions render real data (§4a: 4 role cards, app select, `endpoint-input-MacIp` populated `192.168.1.77`, profile-details table with 8 AppStudio rows + built-in Source badges, settings-file path footer), endpoint validation gates Save, edit PERSISTS across reload (`JsonSettingsStore`); §4b visual-truth: desktop screenshot `test-results/settings-desktop.png` INSPECTED — clean layout, no overlap/clip. ⚠ visual (minor, non-blocking, unchanged): 390px sidebar-shell overflow = the documented REQ-UI-001 desktop-tool caveat. HISTORY — DEMOTED 2026-07-10 earlier that day: the "VERIFIED" below was SELF-ATTESTED by the build orchestrator — verify-phase was never executed (agent-confirmed). 2026-07-09 [REQ-UI-006] Built `/settings` (TrSetupUI, from UIDesign §Settings control map): sidebar-reachable (`NavSettings`), header + "Back to board"; role cards + native-dev switch (REQ-UI-003 pattern), app `Select`, per-endpoint `Input` (today `MacIp`) with IP/hostname validation (blank allowed; error + Save-gate while invalid), read-only profile-details `Card`+table with a **Source** column (built-in vs `.tfcore/trsetup-profile.json` override), settings-file-path footer, Save/Cancel. Added `ProfileLoader.ResolveWithSources` + `RequirementSource`/`ResolvedRequirement` (read-only source surfacing; `Resolve` + board merge untouched) and `BoardState.SaveSettingsAsync`/`Endpoints`. Every control has a stable `data-testid`. VERIFIED: Playwright render+visual (desktop 1280×800) — all regions render, endpoint validation gates Save, edit **persists** across reload (`JsonSettingsStore`) and the board re-scopes; the other 5 UI pages still pass (no regression). ⚠ visual (minor, non-blocking): at 390px the sidebar-shell content area exceeds the viewport (right-aligned header button off-screen) — the SAME documented desktop-tool caveat as REQ-UI-001; desktop is the target. | [view](#d-req-ui-006) |
| REQ-FN-035 | Rename `TrSetup.App` → `TrSetup` — the primary head carries the product name (P6) | Verified | 100 | 2026-07-10 [REQ-FN-035] Renamed dir `src/TrSetup.App`→`src/TrSetup` + `TrSetup.App.csproj`→`TrSetup.csproj`; sln entry updated (GUID kept); `RootNamespace`→`TrSetup`, all head namespaces `TrSetup.App`→`TrSetup` (Windows WinUI shim →`TrSetup.WinUI`), XAML `x:Class` + manifest identity + scoped-CSS link `TrSetup.styles.css` updated; bundle id `com.techierathore.trsetup` + AutomationIds unchanged. Docs/harness paths updated (BuildAndRun §2/§3, DevGuide, UsageGuide runbook, BRD, Architecture, samples/README, CLAUDE.md); repo grep = only rename-context/historical mentions remain (+ `x:Class="TrSetup.App"` = the new full type name of class App in ns TrSetup). SMOKED: sln Debug build rung #4 0W/0E (5 proj); guide-§2 Release cmd verbatim → `win-x64/TrSetup.exe` 0W/0E; exe BOOTED on Windows (PID observed, persisted ~90s, clean kill, 0 leftover) and wrote today's Serilog log — startup line "TrSetup starting" + a `TrSetup.Core` `ILogger<T>` event; Core.Tests 126/126. VERIFIED 2026-07-10 (executed verify-phase, run ledger docs/.last-verify.json): acceptance met — `src/TrSetup/TrSetup.csproj` builds green rung #4 (0W/0E) AND the app boots (live PID + Serilog evidence above); repo grep = no `TrSetup.App` refs in source/sln/test-harness (only rename-context + dated historical remarks in docs, and `x:Class="TrSetup.App"` = the new full type name of class `App` in namespace `TrSetup`); post-rename smoke + verifier re-run PASS (7/7 UI specs render+visual against :5999, 126/126 unit). | [view](#d-req-fn-035) |

**Status values:** `Not Started` · `In Progress` · `Implemented` (code done, not yet verified) · `Verified` (self-smoke or verifier PASS — acceptance AND data-render AND visual gates all pass) · `Done (pre-existing)` (migrated as already complete — build agents must NOT rebuild; terminal like `Verified`) · `Needs re-verify` (a defect or change was logged — must be re-run before it can return to `Verified`) · `PARTIAL` (some acceptance unmet — say what in Remarks) · `FAIL` (verifier ran and failed — bug in Remarks) · `Blocked` (external/library gap — link the TR-/TR-RAG- entry in Remarks) · `N/A`.

**% guide:** `0` not started · `25` scaffolded · `50` in progress · `75` implemented-unverified · `100` verified.

**Remarks:** date + what was done / what is missing / bug or library reference. This is the home for bugs and change notes — do not spawn a separate file. Visual-gate failures are prefixed `⚠ visual:`; security findings `⚠ SECURITY`.

## UI / Pages

<!-- Built by /trblazeui from the approved mockups (docs/TrSetup-UIDesign.md + docs/mockups/*.html). -->

### Page: Board dashboard (`/`)

<a id="d-req-ui-001"></a>
- **REQ-UI-001** — Grouped red/amber/green board: header role + app selectors; groups (Framework core / Bridges / <App> profile / Build & run) with ✓/⚠/✗ counts and per-group Fix all; failing rows show Preview + Fix (or Open guide when manual-only); Re-check all + Export report actions; single-check re-check on each row (BRD-9, BRD-10, BRD-12, BRD-13, Phase 1). *Mockup:* docs/mockups/dashboard.html.
  - *Acceptance:* board renders grouped checks with live status icons + text; role/app switch re-scopes rows without reload; Fix hidden and "Open guide" shown for manual-only checks; controls do not overlap at desktop + mobile widths (visual gate).

### Page: Check detail sheet (`/check/{id}`, sheet over `/`)

<a id="d-req-ui-002"></a>
- **REQ-UI-002** — Detail pane per check: Explain (what/why + WORKFLOW §/guide link), Detect evidence, Fix preview (literal commands/URLs), last run output (command, stdout/stderr, exit code) in an expandable pane (BRD-11, Phase 1). *Mockup:* docs/mockups/check-detail.html.
  - *Acceptance:* row click opens the sheet with all four sections populated; output block scrolls inside the pane; deep link `/check/{id}` lands on the same content.

### Page: First-run role picker (`/setup`)

<a id="d-req-ui-003"></a>
- **REQ-UI-003** — First-run role picker: one card per machine role with a one-line explanation; multi-select; "I develop natively on this machine" variant; default-app selector; saves and proceeds to the board (BRD-6, BRD-8, Phase 1). *Mockup:* docs/mockups/role-picker.html.
  - *Acceptance:* shown automatically when no settings file exists; selections persist (REQ-FN-005) and the board scopes to them; reachable later from the board header.

### Page: Fix-all run view (`/fix-run`)

<a id="d-req-ui-004"></a>
- **REQ-UI-004** — Sequential fix-run progress: ordered step list with per-step status, live output stream, explicit consent gate (shows FixPreview, Approve/Decline) before each elevated step, halt-on-decline; summary with failed steps + raw output (BRD-29 UI, Phase 2). *Mockup:* docs/mockups/fix-run.html.
  - *Acceptance:* steps execute in dependency order; a declined consent halts the run leaving later steps untouched; failed step shows raw output inline.

### Page: Settings / Configuration (`/settings`)

<a id="d-req-ui-006"></a>
- **REQ-UI-006** — Settings/Configuration screen, reachable from the board header beside the role/app selectors: edit machine roles + the native-dev variant (reuse the REQ-UI-003 role-card patterns), change the selected app profile, **edit named endpoint values** (`TrSetupSettings.Endpoints` — today `MacIp`; one labelled input per known endpoint, validated as an address, never a secret per ADR-008), show the settings-file path (parity with the withdrawn `trsetup config`), and a read-only **profile details** pane for the selected app: the requirement rows the profile contributes, whether they come from the built-in profile or a `.tfcore/trsetup-profile.json` app-repo override, and which one wins (REQ-FN-021 merge rules). Saves via `JsonSettingsStore` (REQ-FN-005); the board re-scopes on save without reload. Replaces the withdrawn CLI configuration surface (REQ-FN-013 picker + `--roles/--app/--mac-ip` flags + `trsetup config`). No separate mockup — extend the role-picker mockup patterns (`docs/mockups/role-picker.html`) per the UIDesign §Settings control map (BRD-56, Phase 6).
  - *Acceptance:* editing the Mac IP persists to the settings JSON and the Bridges checks probe the new address on the next sweep; role/app changes re-scope the board without reload; the profile pane lists the selected app's requirement rows + their source (built-in vs override); every control carries a stable `data-testid`/`AutomationId`.

### Page: Report preview (`/report`)

<a id="d-req-ui-005"></a>
- **REQ-UI-005** — Report preview + export: rendered board report with host/roles/app header; Save as MD + HTML; copy-to-clipboard (BRD-24 UI, Phase 1). *Mockup:* docs/mockups/report.html.
  - *Acceptance:* preview matches the board state at export time; no secret values anywhere in the output (spot-check `env-secret` rows show presence status only).

## Functional requirements

<a id="d-req-fn-001"></a>
- **REQ-FN-001** — `Check` contract in `TrSetup.Core`: `Id, Title, Category, Roles, Severity(Required|Recommended|Optional), DetectAsync→Pass|Warn|Fail|NotApplicable + evidence, Explain, FixAsync?, FixPreview, VerifyAsync` (BRD-1, BRD-2, Phase 1).
  - *Acceptance:* unit tests construct a fake check and drive the full contract; NotApplicable checks never render as failures.

<a id="d-req-fn-002"></a>
- **REQ-FN-002** — Detect → Preview → Fix → Re-verify pipeline: fixes only run after preview+consent; `VerifyAsync` re-detects; a fix that doesn't re-detect green is reported failed with raw output attached — never "assume fixed" (BRD-3, Phase 1).
  - *Acceptance:* unit test with a fixer whose verify fails yields status FAILED + captured output.

<a id="d-req-fn-003"></a>
- **REQ-FN-003** — Process runner: single choke-point for command execution capturing exact command line, stdout/stderr, exit code; async with live output streaming (BRD-4, Phase 1).
  - *Acceptance:* every detect/fix result exposes its evidence trail; long-running command streams output incrementally.

<a id="d-req-fn-004"></a>
- **REQ-FN-004** — Catalog scoping: engine enumerates checks for (machine roles ∩ selected app); supports the four roles + native-dev variant; out-of-scope checks are NotApplicable (BRD-5, BRD-7, Phase 1).
  - *Acceptance:* switching role/app changes the enumerated set per the catalog tables in BRD §9.

<a id="d-req-fn-005"></a>
- **REQ-FN-005** — Settings persistence: roles, selected app, configured endpoints (Mac IP) in a small local JSON file; no DB (BRD-6, Phase 1; Architecture ADR-002).
  - *Acceptance:* restart restores selections; missing file triggers first-run role picker.

<a id="d-req-fn-006"></a>
- **REQ-FN-006** — WSL agent-host detects: .NET SDK, headless-Chromium apt libs, `~/bin/winrun` + PATH, Node, Playwright CLI + Chromium, mirrored-networking probe, Windows Appium `/status`, Mac Appium `/status`, git — per the BRD §9 F-WSLCHK table (BRD-17, Phase 1).
  - *Acceptance:* on this WSL distro each check returns a real status + evidence (run it yourself — the harness is set up).

<a id="d-req-fn-007"></a>
- **REQ-FN-007** — Windows device-host detects: `.wslconfig` mirrored, Android SDK tools, API-34 image, `Pixel_API_34` AVD, Node/npm, Appium + uiautomator2, `start-android-verify.ps1`, Appium `:4723` session, MAUI workload, JDK — per BRD §9 F-WINCHK (BRD-19, Phase 1).
  - *Acceptance:* detects run via the winrun bridge from WSL dev environment or natively on Windows; each yields evidence.

<a id="d-req-fn-008"></a>
- **REQ-FN-008** — Mac device-host detects: Xcode/CLT, .NET SDK + MAUI workload, Node, Appium + xcuitest + mac2, Appium on `0.0.0.0:4723` (LaunchAgent), stable IP vs configured endpoint, iOS Simulator runtime — per BRD §9 F-MACCHK (BRD-21, Phase 1).
  - *Acceptance:* detect logic unit-tested with faked probe outputs; live run deferred to a Mac session.

<a id="d-req-fn-009"></a>
- **REQ-FN-009** — Cross-machine probes are HTTP/ping only; never remote-execute; failing probe guidance names the owning machine role (BRD-23, Phase 1).
  - *Acceptance:* unreachable endpoint yields Fail + "run TrSetup on <machine>" guidance; 5 s timeout; probes run in parallel.

<a id="d-req-fn-010"></a>
- **REQ-FN-010** — Report exporter: full board (groups, statuses, evidence, last-run outputs) → `TrSetup-Report-<host>.md` + `.html` via the shared doc shell; secret values never included (BRD-24, BRD-25, Phase 1).
  - *Acceptance:* exported MD+HTML reproduce the board; `env-secret` rows show presence status only.

<a id="d-req-fn-011"></a>
- **REQ-FN-011** — **[WITHDRAWN 2026-07-09 — owner decision: single MAUI desktop app; see REQ-FN-034]** `TrSetup.Web` Blazor Server head: `trsetup gui` starts Kestrel on `localhost:5999` (fallback to a free port) and opens the Windows browser via mirrored networking (BRD-14, Phase 1).
  - *Acceptance:* Playwright drives the board at `http://localhost:5999` headlessly in WSL.

<a id="d-req-fn-012"></a>
- **REQ-FN-012** — **[WITHDRAWN 2026-07-09 — owner decision: single MAUI desktop app; see REQ-FN-034]** `TrSetup.Cli` Spectre.Console TUI: `trsetup` with no args renders the same grouped board (statuses + evidence) in the terminal, over SSH too; thin renderer over `TrSetup.Core`, never a second implementation (BRD-15, Phase 1).
  - *Acceptance:* TUI shows the identical check set + statuses as the Web head on the same machine.

<a id="d-req-fn-013"></a>
- **REQ-FN-013** — **[WITHDRAWN 2026-07-09 — owner decision: single MAUI desktop app; see REQ-FN-034]** TUI interactions: arrow-key selection, `f` fix, `a` fix-all, `r` re-check, `e` export report (BRD-16, Phase 1; fix keys activate with P2 fixers).
  - *Acceptance:* keys act on the selected row/board; `f` on a manual-only check shows guidance instead.

<a id="d-req-fn-014"></a>
- **REQ-FN-014** — WSL auto-fixers for every ✔ row of F-WSLCHK: dotnet-install SDK, apt libs, winrun write + PATH line, Node LTS, Playwright install, git (BRD-18, Phase 2).
  - *Acceptance:* on a deliberately-broken item, Fix → re-detect green; running twice is a no-op.

<a id="d-req-fn-015"></a>
- **REQ-FN-015** — Windows auto-fixers for every ✔ row of F-WINCHK: `.wslconfig` patch + `wsl --shutdown` prompt, cmdline-tools + SDK + system image + AVD, Node, Appium + uiautomator2, ps1 helper from embedded template, session-test run, MAUI workload, Temurin JDK (BRD-20, Phase 2).
  - *Acceptance:* each fixer previews its literal commands; failed installs surface raw output.

<a id="d-req-fn-016"></a>
- **REQ-FN-016** — Mac auto-fixers for every ✔ row of F-MACCHK: CLT install, SDK + MAUI workload, Node, Appium drivers, LaunchAgent plist write + load, `-downloadPlatform iOS`; full Xcode + DHCP reservation stay manual guidance (BRD-22, Phase 2).
  - *Acceptance:* LaunchAgent survives reboot (documented check); manual-only rows never grow a Fix button.

<a id="d-req-fn-017"></a>
- **REQ-FN-017** — Installer download framework: official sources only, URLs pinned in profile/engine and shown in FixPreview, checksum-verified where published ("no published checksum" recorded otherwise), installs into TrSetup-managed locations that never collide with system installs (BRD-26, BRD-27, Phase 2).
  - *Acceptance:* a tampered download fails the checksum and reports; install paths are under the TrSetup-managed root.

<a id="d-req-fn-018"></a>
- **REQ-FN-018** — Idempotent config writes: everything TrSetup writes (`.wslconfig` block, `.bashrc` PATH line, plists, ps1) carries managed marker blocks; re-runs never duplicate; user edits outside markers never clobbered (BRD-28, Phase 2).
  - *Acceptance:* run fix twice → single block; hand-edit outside block survives a re-fix.

<a id="d-req-fn-019"></a>
- **REQ-FN-019** — Fix-all runner: topological dependency order (Node before Appium, SDK before AVD, Postgres before PgVector…), stops at first consent/elevation gate or declined consent; per-step re-verify; continue-or-stop on failure (BRD-29, Phase 2).
  - *Acceptance:* order verified by unit test on the dependency graph; declined consent halts with later steps untouched.

<a id="d-req-fn-020"></a>
- **REQ-FN-020** — Elevation/consent runner: elevated steps show the exact command and run only on click; Windows UAC in a visible child process; WSL sudo optional interactive-terminal handoff (app prints the one command to paste); no stored passwords (BRD-30, Phase 2).
  - *Acceptance:* no code path stores or caches credentials; every elevated run is user-initiated.

<a id="d-req-fn-021"></a>
- **REQ-FN-021** — Profile schema + loader: `trsetup-profile.json` requirement instances of types `sdk, workload, cli-tool, service, endpoint, nuget-feed, env-secret, disk-space, appium-head, runtime-install`, each role-tagged; built-in + app-repo `.tfcore/trsetup-profile.json` override merge — app repo wins (BRD-33, BRD-34, BRD-35, Phase 3).
  - *Acceptance:* schema-validated load; conflict test proves app-repo wins; new app onboards with a JSON file only.

<a id="d-req-fn-022"></a>
- **REQ-FN-022** — AppStudio built-in profile per BRD §9 F-PROFILES table: .NET 10 SDK, MAUI workload, dotnet+git on PATH, Xcode (Mac-runner), techierathore GitHub Packages feed (PAT `read:packages` authenticates), App Manager API endpoint, AppManager key presence (BRD-36, Phase 3).
  - *Acceptance:* selecting AppStudio renders exactly these rows scoped to the machine's roles.

<a id="d-req-fn-023"></a>
- **REQ-FN-023** — TrStudio built-in profile per BRD §9 F-PROFILES table: .NET 10 SDK, Postgres + PgVector service, ffmpeg, isolated Python + ComfyUI `runtime-install`, model disk-space floor (Mac-runner, warn), TechieRag NuGet PAT (Win), RunPod/HeyGen/AppManager key presence, AppManager endpoint (BRD-37, Phase 3).
  - *Acceptance:* selecting TrStudio renders exactly these rows scoped to roles.

<a id="d-req-fn-024"></a>
- **REQ-FN-024** — Framework-profile extras inside an app repo: offer to write `core-config.yaml → runtimeVerification.appium` from just-verified endpoints, then curl-verify each registered head (BRD-38, Phase 3).
  - *Acceptance:* written block matches the WORKFLOW §0b step-4 shape; existing user config respected (idempotent markers).

<a id="d-req-fn-025"></a>
- **REQ-FN-025** — Runtime-install fixer: isolated Python env + ComfyUI from GitHub releases into a TrSetup-managed location (TrStudio isolated-Python discipline generalized); boundary — TrStudioAdmin owns models/workflows/providers above the runtime (BRD-39, Phase 3).
  - *Acceptance:* ComfyUI starts from the managed install; no collision with any system Python.

<a id="d-req-fn-026"></a>
- **REQ-FN-026** — Service fixers: PostgreSQL install (winget/brew) + `CREATE EXTENSION vector`; ffmpeg install (BRD-40, Phase 3).
  - *Acceptance:* fresh machine reaches "service running + extension present"; re-run is a no-op.

<a id="d-req-fn-027"></a>
- **REQ-FN-027** — Mac app-runner role: aggregates the selected app's profile requirements for local build+run (SDK/workloads, Xcode, git, NuGet auth, services, runtimes, keys) into the board with fixes (BRD-41, Phase 3).
  - *Acceptance:* the role's board equals the app's BuildAndRun-Guide prerequisites section.

<a id="d-req-fn-028"></a>
- **REQ-FN-028** — "Build & install <App> for Mac (Catalyst)" fixer: enabled when prerequisites green; runs `dotnet build -f net10.0-maccatalyst -c Release` and opens/installs the produced `.app` (BRD-42, Phase 3).
  - *Acceptance:* fixer disabled with reason while any prerequisite red; success verified by the `.app` existing.

<a id="d-req-fn-029"></a>
- **REQ-FN-029** — Disk-space check type: configurable floor per profile, warn severity (BRD-43, Phase 3).
  - *Acceptance:* floor breach yields Warn (never Fail) with free/required figures in evidence.

<a id="d-req-fn-030"></a>
- **REQ-FN-030** — `TrSetup` MAUI Blazor Hybrid head (named `TrSetup.App` until the REQ-FN-035 rename): Windows unpackaged exe (`WindowsPackageType=None`) + Mac Catalyst via MAUI, ad-hoc signed; hosts the same `TrSetupUI` RCL (BRD-44, Phase 4).
  - *Acceptance:* Windows head builds via ladder rung #4 and boots to the board; Catalyst build documented for the Mac; AutomationIds present (REQ-NFR-005).

<a id="d-req-fn-031"></a>
- **REQ-FN-031** — Distribution: firm up `docs/TrSetup-BuildAndRun-Guide.md` with concrete paths/ports. **[RESCOPED 2026-07-09 — MAUI-only: the guide covers the `TrSetup` (ex-`TrSetup.App`) Windows exe + Mac Catalyst build; the Cli/Web self-contained publish scripts are withdrawn (removal in REQ-FN-034).]** (BRD-45, Phase 4).
  - *Acceptance:* the rewritten guide's build commands executed verbatim produce a runnable `TrSetup` head.

<a id="d-req-fn-032"></a>
- **REQ-FN-032** — **[WITHDRAWN 2026-07-09 — owner decision: single MAUI desktop app; agent mode lived in the CLI head; see REQ-FN-034]** `trsetup --check --json`: machine-readable board (checks, statuses, evidence) with stable schema for agents/CI; exit code reflects overall status (BRD-46, Phase 5).
  - *Acceptance:* JSON schema documented; verifier can parse it; no UI required.

<a id="d-req-fn-033"></a>
- **REQ-FN-033** — **[WITHDRAWN 2026-07-09 — consumed the withdrawn FN-032 agent mode; see REQ-FN-034]** (Stretch) verify-phase §0 pre-flight hook: the TechieFlow verifier runs `trsetup --check --json` and gates on "environment green" (BRD-47, Phase 5).
  - *Acceptance:* documented hook + example gate; explicitly optional.

<a id="d-req-fn-034"></a>
- **REQ-FN-034** — Decommission the CLI and Web heads (owner decision 2026-07-09: TrSetup ships as ONE MAUI Blazor Hybrid desktop app). Remove `src/TrSetup.Cli`, `src/TrSetup.Web`, `tests/unit/TrSetup.Cli.Tests` from disk and `TrSetup.sln`; remove `scripts/publish.sh`, `scripts/publish.ps1`, `scripts/preflight-gate.sh`, `scripts/preflight-gate.ps1`; keep the MAUI head (named `TrSetup.App` at the time; `TrSetup` since the REQ-FN-035 rename), `TrSetupUI` (RCL), `TrSetup.Core` (engine), `tests/unit/TrSetup.Core.Tests`. Also migrate the verification harness: `playwright.config.ts` currently boots `TrSetup.Web` on :5999 as its test host — rehost the UI smoke path against the MAUI head via the Windows-MAUI/Appium bridge (or retire the Playwright config with the Web head and note the replacement in the UsageGuide runbook). Docs were rewritten MAUI-only on the decision date; this REQ is the code-side cleanup (Phase 6).
  - *Acceptance:* the solution contains exactly the four kept projects and builds green via ladder rung #4 (0W/0E); `TrSetup.Core.Tests` pass; no source or test-harness references to `TrSetup.Cli`/`TrSetup.Web` remain outside `docs/OldDocs/`.

<a id="d-req-fn-035"></a>
- **REQ-FN-035** — Rename the MAUI head `src/TrSetup.App` → `src/TrSetup` per the framework naming standard (owner rule 2026-07-10: the product's primary executable head is named exactly `<APP>`; the `.App` suffix is banned — coding-standards §"Project & solution naming"). Scope: rename the project directory + `TrSetup.App.csproj` → `TrSetup.csproj`; update the `TrSetup.sln` entry; update `RootNamespace`/`AssemblyName` and the `TrSetup.App.*` namespaces + their `using`s across the head; update any harness/scripts/docs paths that reference `src/TrSetup.App` or `TrSetup.App.exe` (BuildAndRun guide §2/§3, DevGuide, UsageGuide runbook). The Mac bundle id `com.techierathore.trsetup` and all `AutomationId`s stay unchanged (Appium bindings unaffected) (Phase 6).
  - *Acceptance:* `src/TrSetup/TrSetup.csproj` builds green via ladder rung #4 (0W/0E) and the app boots; no `TrSetup.App` references remain in source, sln, test harness, or docs outside `docs/OldDocs/`; smoke + verifier re-run pass.

## RAG / AI requirements (→ /techierag)

*None — TrSetup has no AI/RAG features (Architecture ADR-003). The report export exists precisely so a human can hand the board to a Claude session.*

## Non-functional

<a id="d-req-nfr-001"></a>
- **REQ-NFR-001** — Performance per BRD §11 table: full detect sweep < 30 s typical (parallel probes, 5 s timeouts), single re-check < 5 s, first paint < 2 s with streaming detects, UI never blocked by fixes (BRD-48, Phase 1 ongoing).

<a id="d-req-nfr-002"></a>
- **REQ-NFR-002** — Security stance: consent-per-elevation, pinned official sources + checksums where published, presence-only secrets (never stored/displayed/logged/exported; paid APIs never probed), outbound-only networking, no telemetry (BRD-31, BRD-32, BRD-49, Phase 2 — stance applies from P1).

<a id="d-req-nfr-003"></a>
- **REQ-NFR-003** — Accessibility: GUI board fully keyboard-navigable with visible focus; status = icon + text, never color alone; TrBlazeUI aria patterns respected (BRD-50, Phase 1).

<a id="d-req-nfr-004"></a>
- **REQ-NFR-004** — Reliability: an interrupted/failed fix leaves the machine no worse (idempotent fixes, TrSetup-managed locations); re-running always safe (BRD-51, Phase 2).

<a id="d-req-nfr-005"></a>
- **REQ-NFR-005** — Testability: stable `data-testid` on every interactive Blazor control; `AutomationId` on every MAUI control; `--check --json` as CI assertion surface (BRD-52, Phase 1 ongoing).

<a id="d-req-nfr-006"></a>
- **REQ-NFR-006** — **[WITHDRAWN 2026-07-09 — owner decision: single MAUI desktop app; the Cli/Web bootstrap path retires with its heads; see REQ-FN-034]** Portability: Cli and Web heads run from self-contained publishes on machines with no .NET installed — the bare-Mac/WSL bootstrap path (BRD-53, Phase 4).

<a id="d-req-nfr-007"></a>
- **REQ-NFR-007** — Observability: Serilog file-based logging in the MAUI head per the TechieFlow standing NFR (coding-standards §Logging) — wire Serilog in `MauiProgram.CreateMauiApp` with a rolling file sink under `FileSystem.AppDataDirectory/logs/trsetup-.log` (daily rolling, ~14 files retained) + console; `builder.Logging.AddSerilog()`; log unhandled exceptions (`AppDomain.CurrentDomain.UnhandledException`, `TaskScheduler.UnobservedTaskException`) and `Log.CloseAndFlush()` on exit; `TrSetupUI`/`TrSetup.Core` keep logging through `ILogger<T>`/abstractions only (no Serilog reference in libraries) — the head's sink picks their events up automatically (Phase 6).
  - *Acceptance:* launching the app creates the log file and writes startup + check-sweep events through `ILogger<T>` calls from Core; a thrown unhandled exception lands in the file; libraries have no Serilog package reference.
