---
project: TrSetup
stack: .NET 10 / Blazor (TrSetupUI RCL on TrBlazeUI) / MAUI Blazor Hybrid desktop (Windows + Mac Catalyst) / no DB / no RAG
last_updated: 2026-07-11
current_phase: Fix/UAT — Mac-run defects fixed + re-verified; FN-028 working-dir defect, then owner UAT
last_verified_build: PASS
last_verified_date: 2026-07-11
---

# TrSetup — Status

## Where I am
TrSetup ships as **ONE MAUI Blazor Hybrid desktop app**: `src/TrSetup`; `TrSetup.Web` remains the test-only headless smoke host (:5999). **2026-07-11 evening — all three Mac-run defects fixed and re-verified ON the Mac** (fix-issues + executed verify-phase, ledger `docs/.last-verify.json`): the stuck-Pending board streaming defect is gone (UI-001 → **Verified**; every row settles, suite now rejects "Pending"), the Catalyst gate-detect is parallel + budget-bounded (FN-028 verifiable portion re-verified; FixPreview working-dir defect still open), and the documented Catalyst build command restores cleanly on macOS (FN-030 NETSDK1100 cleared → Implemented, `.app` production needs a full-Xcode Mac; FN-031 guide → **Verified**). Remaining work: FN-028's working-dir defect (agent-fixable), then the owner-run UAT rows. Note: `tests/unit` is gitignored, so the historical 126-test suite is WSL-local only — this Mac now carries the 10 new engine/gate tests; owner should copy the WSL suite over.

## Next command to run
```
*fix-issues REQ-FN-028 (FixPreview/Fix working dir resolves to the process cwd — add a configured + validated AppStudio repo path)
```
Then owner-run UAT: walk docs/TrSetup-UsageGuide.md §"UAT plan" (FN-014/015/016/025/026, FN-008 §6.2, FN-028 green-path, FN-030 Windows visual + Catalyst .app on a full-Xcode Mac).

## Open requirements
- [ ] Defect (agent-fixable): FN-028 FixPreview/Fix working dir = process cwd; no configured AppStudio repo path (observed live 2026-07-11)
- [ ] UAT (Implemented, external-host actions — owner runs): FN-014/015/016/025/026 destructive live installs · FN-008 owner walk-through §6.2 · FN-028 Catalyst fixer green-path (needs full Xcode + green prereqs) · FN-030 Windows visual board render + Mac Catalyst `.app` build (needs full Xcode)

Counts: **34 Verified · 9 Implemented (external UAT / defect) · 6 N/A · 0 FAIL · 0 Needs re-verify** (of 49 rows incl. FN-035). 0 Planned.

## Known blockers
- Catalyst `.app` production needs **full Xcode** on the build Mac (this machine has CLT only — legitimate host prerequisite, tracked by the board's own mac.xcode row; the csproj/guide defect that blocked every Mac is fixed).
- Historical unit-test suite is not in git (`/tests` gitignored) — copy `tests/unit/TrSetup.Core.Tests` from the WSL clone (or un-ignore `/tests`) to reunite the 126 tests with the 10 new ones.
- (Installer per-version SHA-256 pinning remains a deferred follow-up, not a blocker.)

## Verification log
| Date | Phase | Result | Status table |
|------|-------|--------|--------------|
| 2026-07-05 | Day-1 (greenfield) | N/A — docs only, no build | docs/TrSetup-Checklist.md#requirements-status |
| 2026-07-06 | P1-UI + P2-fixers (build-phase) | PASS — build green (rung #4, 0W/0E), 99 tests, 5 UI pages smoked (render+visual) | docs/TrSetup-Checklist.md#requirements-status |
| 2026-07-07 | P3 profiles + app fixers (build-phase) | PASS — build green, 140 tests, profiles E2E render-truth | docs/TrSetup-Checklist.md#requirements-status |
| 2026-07-07 | P4 distribution + MAUI head (build-phase) | PASS — build green, FN-031/NFR-006 verified inline, FN-030 Implemented | docs/TrSetup-Checklist.md#requirements-status |
| 2026-07-07 | P5 stretch — pre-flight gate (build-phase) | PASS — FN-033 verified inline; all REQs built | docs/TrSetup-Checklist.md#requirements-status |
| 2026-07-07 | verify all (flow-master inline, 2 waves) | 36/44 Verified, 0 FAIL; 8 rows documented UAT | docs/TrSetup-Checklist.md#requirements-status |
| 2026-07-07 | Handoff (flow-master) | READY FOR UAT — docs finalized + HTMLs re-rendered | docs/TrSetup-Checklist.md#requirements-status |
| 2026-07-09 | Scope change — MAUI-only (owner decision) | N/A — docs pass only: CLI+Web heads withdrawn, FN-034/NFR-007/UI-006 added | docs/TrSetup-Checklist.md#requirements-status |
| 2026-07-09 | P6 MAUI-only build-phase (flow-master, 3 parallel clusters) | PASS — build green rung #4; 126 Core.Tests; 7/7 UI specs. (Verdicts later found self-attested — see 2026-07-10 audit) | docs/TrSetup-Checklist.md#requirements-status |
| 2026-07-09 | Handoff (flow-master) | READY FOR UAT — UsageGuide finalized; BRD rolled up; HTMLs re-rendered | docs/TrSetup-Checklist.md#requirements-status |
| 2026-07-09 | Mac copy-to-binaries path (flow-master) | BUILT + documented — `TrSetup.Web` self-contained osx-arm64 publish; BuildAndRun §3 Options A/B | docs/TrSetup-Checklist.md#requirements-status |
| 2026-07-10 | Verdict audit + naming standard (framework session) | FN-031/FN-034/NFR-007/UI-006 demoted to `Needs re-verify` (self-attested); REQ-FN-035 added | docs/TrSetup-Checklist.md#requirements-status |
| 2026-07-10 | P6 build-phase FN-035 rename + executed verify-phase (chained inline) | PASS — rename done; build rung #4 0W/0E; exe booted; 126 unit; 7/7 verify specs; ledger written. FN-031/034/NFR-007/UI-006/FN-035 → Verified; UI-001..005 re-confirmed | docs/TrSetup-Checklist.md#requirements-status |
| 2026-07-11 | fix-issues: unstyled Web board (owner screenshots issuessc/, ON the Mac) | PASS — root cause: content root defaulted to launch cwd, so a publish started elsewhere 404'd every static asset (raw HTML; the §3 A′ runbook itself triggered it). Fixed: `ContentRootPath = AppContext.BaseDirectory` (Program.cs, [REQ-FN-034]); publish rebuilt; re-verified styled from repo root + publish/mac (owner's cwd) + `dotnet run` — 4/4 assets 200, 7/7 specs, screenshot inspected. UsageGuide §7 row updated | docs/TrSetup-Checklist.md#requirements-status |
| 2026-07-11 | verify-phase `phase-mac` (verifier, ON the Mac, ledger docs/.last-verify.json) | MIXED — 7/7 UI specs + render/visual gates PASS on the styled :5999 board (mobile-390px caveat not reproduced); FN-008 detects live-correct 7/7; FN-027 re-confirmed. FOUND: FN-030 Catalyst cmd FAIL (NETSDK1100 on macOS), FN-031 + UI-001 → Needs re-verify (guide falsified; stuck-Pending row streaming), FN-028 detect can hang (gate chain) | docs/TrSetup-Checklist.md#requirements-status |
| 2026-07-11 | Mac UAT-blocker fix (flow-master, on the Mac) | PASS — owner's "Web UI completely broken" root-caused: (1) the §3 Option A copy landed as 0-byte RDP placeholders (nothing runnable ever reached the Mac) and (2) the WSL `bin/Debug` output on the copied repo crashes on foreign machines (`UseStaticWebAssets` → DirectoryNotFoundException, Program.cs:25). Fixes: TrBlazeUI 1.0.7 nupkgs reconstructed into `~/LocalNuGet` on the Mac (real DLLs + static assets); Program.cs startup guard added; `publish/mac/TrSetup.Web` (osx-arm64, self-contained) built ON the Mac, booted, Playwright-smoked — styled board, live `mac.*` evidence, 0 errors. FN-008 live-smoked (stays Implemented — owner UAT §6.2 + verifier pending). UsageGuide §3/§7 updated (copy-verify step, Option A′ local publish, 2 new troubleshooting rows) | docs/TrSetup-Checklist.md#requirements-status |
| 2026-07-11 | fix-issues FN-030/UI-001/FN-028 + executed verify-phase re-verify (ON the Mac, ledger docs/.last-verify.json) | PASS — 3 root causes fixed (BoardView parameterless-child re-render skip + engine hard probe timeout; gate detect parallel + 3.5s/prereq bound; csproj TFM guard now replaces the list on OSX). 8/8 specs (suite now rejects "Pending"; new full-settlement test), 10/10 unit, catalyst-build4.log 0×NETSDK1100 (stops only at full-Xcode host state), screenshots inspected. UI-001 + FN-031 → Verified; FN-030 FAIL cleared → Implemented (Xcode UAT); FN-028 re-verified, working-dir defect open | docs/TrSetup-Checklist.md#requirements-status |

## Library feedback summary
- TrBlazeUI: 0 major, 1 minor (TR-001 — `Empty` component reference example vs shipped API) — docs/TrSetup-TrBlazeUI-Feedback.md
- TechieRag: 0 major, 0 minor — not used in this app

## Standards compliance (last check 2026-07-10, verify-phase inline)
- Underscore fields: CLEAN (src/, hand-written source)
- Test method underscores: CLEAN
- Mis-prefixed / snake_case fields & locals: CLEAN (renamed head files keep obj/a/v prefixes)
- Libraries free of Serilog references (NFR-007 boundary): CLEAN
- Build: 0 warnings / 0 errors (rung #4, full solution, 5 projects)

## Deferred / future
- The 8 UAT rows (per-host steps in docs/TrSetup-UsageGuide.md); owner sets `Released` after UAT
- Installer per-version SHA-256 checksums (REQ-FN-017 follow-up); confirm profile env-secret names against each app's canonical config keys when those repos onboard
