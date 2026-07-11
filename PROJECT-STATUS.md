---
project: TrSetup
stack: .NET 10 / Blazor (TrSetupUI RCL on TrBlazeUI) / MAUI Blazor Hybrid desktop (Windows + Mac Catalyst) / no DB / no RAG
last_updated: 2026-07-10
current_phase: UAT — all agent-verifiable REQs Verified; 8 owner-run host-bound rows remain
last_verified_build: PASS
last_verified_date: 2026-07-10
---

# TrSetup — Status

## Where I am
TrSetup ships as **ONE MAUI Blazor Hybrid desktop app**: `src/TrSetup` (**renamed from `TrSetup.App` on 2026-07-10, REQ-FN-035** — dir, csproj, sln, namespaces, docs/harness paths updated; bundle id + AutomationIds unchanged). `TrSetup.Web` remains the test-only headless smoke host (:5999). The four 2026-07-10-demoted rows (FN-031/FN-034/NFR-007/UI-006) plus FN-035 are now **Verified by an executed verify-phase run** (run ledger `docs/.last-verify.json`): 7/7 Playwright specs (render + visual gates, screenshots inspected), 126/126 Core unit tests, and the renamed `TrSetup.exe` built via the guide's verbatim command and booted live (Serilog log written).

## Next command to run
```
(owner-run) Walk docs/TrSetup-UsageGuide.md §"UAT plan — the 8 pending rows" (FN-008/014/015/016/025/026/028/030), then set current_phase: Released.
```

## Open requirements
- [ ] UAT (Implemented, external-host actions — owner runs): FN-014/015/016/025/026 destructive live installs · FN-008 Mac detects · FN-028 Catalyst build (Mac) · FN-030 Windows visual board render

Counts: **34 Verified · 8 Implemented (external UAT) · 6 N/A** (of 48). 0 FAIL · 0 Needs re-verify · 0 Planned.

## Known blockers
- None. (Installer per-version SHA-256 pinning remains a deferred follow-up, not a blocker.)

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
