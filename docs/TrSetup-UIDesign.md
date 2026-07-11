# TrSetup — UI Design Spec (Mockups)

> **What this is.** The approved visual design for TrSetup, produced at day-1 (greenfield) before any UI is built. Each screen has a **rendered mockup** (`docs/mockups/{screen}.html`, styled to look like TrBlazeUI) and a **component map** that ties every region to a real **TrBlazeUI control**, so the build (`/trblazeui`) reproduces it 1:1 and the verifier's visual-truth gate (`verify-phase.md §4b`) can diff the live screen against it. This is a HUMAN document → rendered to HTML. The owner APPROVES it (alongside the BRD + Architecture) before build.

## Table of Contents

1. [How to use](#how-to-use)
2. [Design system (TrBlazeUI)](#design-system-trblazeui)
3. [Screens](#screens)
   - [Screen: Board dashboard (`/`)](#screen-board-dashboard)
   - [Screen: Check detail sheet (`/check/{id}`)](#screen-check-detail-sheet-check-id)
   - [Screen: First-run role picker (`/setup`)](#screen-first-run-role-picker-setup)
   - [Screen: Settings / Configuration (`/settings`)](#screen-settings-configuration-settings)
   - [Screen: Fix-all run view (`/fix-run`)](#screen-fix-all-run-view-fix-run)
   - [Screen: Report preview (`/report`)](#screen-report-preview-report)
4. [TUI (Spectre.Console) — not a TrBlazeUI screen](#tui-spectre-console-not-a-trblazeui-screen)

## How to use

- Every screen below links to its rendered mockup in `docs/mockups/`. Open those `.html` files in a browser to see the intended layout.
- The **Component map** is the build contract: `region → TrBlazeUI control`. Only controls that actually exist in the TrBlazeUI library are used (catalog read first: `/mnt/c/3AIGenCode/TrBlazeUI/docs/TrBlazeUI-AI-Reference.md`). No library gaps were found for these screens — `docs/TrSetup-TrBlazeUI-Feedback.md` not needed at design time.
- To change a screen after approval: run `*mockups TrSetup --update` (or `*amend-docs` for a requirement change that adds screens).

## Design system (TrBlazeUI)

- **Source:** TrBlazeUI component library (.NET 10, Tailwind CSS v4, shadcn/ui design; OKLCH token theme). Mockups replicate its design language — spacing, radius (`--radius: 0.625rem`), semantic color tokens, `Inter`/system sans typography — so they are replicable in Blazor 1:1.
- **Layout shell:** `SidebarProvider` → `Sidebar` (collapsible; header logo + app name; nav: Board, Fix all, Report, Setup) + `SidebarInset` (sticky header with `SidebarTrigger`, role + app selectors; scrollable content `p-6`). Same shell on every screen.
- **Theme:** light + dark (`.dark` class); mockups default light with the shadcn neutral palette.
- **Controls inventory used:** `SidebarProvider/Sidebar/SidebarMenu*/SidebarInset`, `Card/CardHeader/CardTitle/CardDescription/CardContent/CardFooter`, `Badge`, `Button` (Default/Outline/Ghost/Destructive/Icon), `Select`, `MultiSelect`, `Checkbox`, `Collapsible`, `Sheet`, `Separator`, `Alert`, `Progress`, `Spinner`, `Skeleton`, `ScrollArea`, `Tooltip`, `Empty`, `Item/ItemGroup`, `Kbd`, `AlertDialog` (consent), `ToastProvider`, `LucideIcon` (status + actions), `Tabs` (report MD/HTML preview).
- **Status vocabulary (used on every screen; icon + text, never color alone — REQ-NFR-003):** ✓ Pass = green `circle-check`, ⚠ Warn = amber `triangle-alert`, ✗ Fail = red `circle-x`, ○ N/A = muted `circle-dashed`, ⟳ Checking = `Spinner`.

## Screens

### Screen: Board dashboard

**Mockup:** [docs/mockups/dashboard.html](./mockups/dashboard.html) · **Role(s):** all · **BRD:** BRD-9, BRD-10, BRD-12, BRD-13 · **REQ:** REQ-UI-001

**Layout (one line):** sidebar shell; sticky header (sidebar trigger · title · roles MultiSelect · app Select · Re-check all · Export); main = one Card per check group, each Card holding status-count badges + Fix all, and one row per check.

**Component map:**

| Region | TrBlazeUI control | Shows / binds | States |
|--------|-------------------|---------------|--------|
| App shell | `SidebarProvider` + `Sidebar` (collapsible) | logo, nav (Board active, Fix all, Report, Setup), version footer | collapsed → icon rail with `Tooltip` |
| Header | `SidebarInset` header + `SidebarTrigger` | page title "Environment board" | — |
| Roles selector | `MultiSelect` | machine roles (multi — e.g. "Device host (Mac) + App runner") | at least one required |
| App selector | `Select` | selected app profile (Framework only / AppStudio / TrStudio / …) | — |
| Global actions | `Button` (Outline) ×2 | "Re-check all" (`refresh-cw`), "Export report" (`file-down`) | Re-check shows `Spinner` while sweeping |
| Check group | `Card` per group (Framework core / Bridges / AppStudio profile / Build & run) | `CardTitle` + count `Badge`s (`5 ✓` secondary, `1 ⚠` warn, `2 ✗` destructive) + "Fix all" `Button` | group with 0 ✗/⚠ shows subdued all-green row |
| Check row | `Item` inside `ItemGroup` | status icon+word (`LucideIcon` + text), check title, one-line evidence (`TypographyMuted`), row actions | Pass rows collapsed to compact line; Fail/Warn rows prominent |
| Row actions | `Button` (Ghost, Small): "Preview", "Fix"; manual-only → "Open guide" (Outline) | wired per check `FixAsync == null` | Fix disabled while a fix is running |
| Build & run group | `Card` with single action `Button` | "Build AppStudio for Mac (Catalyst)" | disabled + `Tooltip` reason until prerequisites green |
| Empty state | `Empty` | "No checks for this role/app selection" | — |
| Toasts | `ToastProvider` | fix results ("MAUI workload installed — re-check green") | — |

**Notes / interactions:** row click opens the Check detail `Sheet` (screen 2); roles/app change re-runs enumeration (no reload); group "Fix all" and global fix actions navigate to `/fix-run` when more than one fix queues. At mobile width the header selectors stack and rows wrap actions below the title.

**Empty / loading / error:** initial sweep renders rows with `Skeleton` lines replaced as detects stream in; probe timeout shows ⚠ with "timed out after 5 s" evidence; enumeration failure shows `Alert` (Danger) with the engine error.

### Screen: Check detail sheet (`/check/{id}`)

**Mockup:** [docs/mockups/check-detail.html](./mockups/check-detail.html) · **Role(s):** all · **BRD:** BRD-11 · **REQ:** REQ-UI-002

**Layout (one line):** board dashboard dimmed beneath a right-side `Sheet` (~480px) with title/status header and four stacked sections: Explain · Detect evidence · Fix preview · Last run output.

**Component map:**

| Region | TrBlazeUI control | Shows / binds | States |
|--------|-------------------|---------------|--------|
| Container | `Sheet` (Side=Right) | check title + status `Badge` in `SheetHeader` | Esc / outside click closes |
| Explain | `SheetDescription` + prose block | what it is, why it's needed, link to WORKFLOW § anchor / guide (`Button` Variant=Link, `external-link` icon) | — |
| Detect evidence | `Card` (muted) | evidence text (e.g. `dotnet --list-sdks` output line) + detected-at timestamp | empty → "not yet detected" |
| Fix preview | code block (`pre` in `ScrollArea`) + copy `Button` (Ghost, `copy` icon) | the literal command(s)/download URLs Fix will run | manual-only → `Alert` (Info): "manual step — see guide" |
| Last run output | `Collapsible` with dark `pre` in `ScrollArea` | exact command line, stdout/stderr, exit code `Badge` | never run → `TypographyMuted` "no runs yet" |
| Footer actions | `SheetFooter`: `Button` "Fix" (Default), "Re-check" (Outline), "Close" (Ghost) | — | Fix hidden for manual-only |

**Notes / interactions:** deep link `/check/{id}` opens the same sheet over the board; copy button toasts "copied".

**Empty / loading / error:** while re-checking, status badge swaps to `Spinner`; a failed fix keeps the sheet open and scrolls to Last run output with exit code highlighted.

### Screen: First-run role picker (`/setup`)

**Mockup:** [docs/mockups/role-picker.html](./mockups/role-picker.html) · **Role(s):** all (first run; reachable later from sidebar "Setup") · **BRD:** BRD-6, BRD-7, BRD-8 · **REQ:** REQ-UI-003

**Layout (one line):** no data yet, so a centered single column (max-w-2xl): welcome header, four selectable role cards (checkbox + icon + one-line description), a native-dev switch, default-app Select, Continue button.

**Component map:**

| Region | TrBlazeUI control | Shows / binds | States |
|--------|-------------------|---------------|--------|
| Page frame | same sidebar shell (nav present, Setup active) | — | first run: sidebar collapsed |
| Welcome | `TypographyH2` + `TypographyMuted` | "Which roles does this machine play?" + explainer | — |
| Role cards | 4× `Card` each wrapping a `Checkbox` + `LucideIcon` + title + one-liner (from BRD §5 table) | Agent host WSL (`terminal`), Device host Windows (`monitor`), Device host Mac (`laptop`), App runner Mac (`rocket`) | selected card gets primary border/ring; multi-select allowed |
| Native-dev variant | `Switch` + `Label` | "I develop natively on this machine (drop WSL-bridge checks)" | — |
| Default app | `Select` | Framework only / AppStudio / TrStudio | — |
| Continue | `Button` (Default, full-width) "Save & scan this machine" | persists settings (REQ-FN-005) → board sweep | disabled until ≥1 role ticked |

**Notes / interactions:** shown automatically when no settings file exists; later visits show current selections pre-ticked with "Save" instead.

**Empty / loading / error:** save failure → `Alert` (Danger) above Continue with the file error.

### Screen: Settings / Configuration (`/settings`)

*Added 2026-07-09.* **Mockup:** none separate — extend [docs/mockups/role-picker.html](./mockups/role-picker.html) patterns · **Role(s):** all (reachable from the board header, beside the role/app selectors) · **BRD:** BRD-56 · **REQ:** REQ-UI-006 (Planned, Phase 6)

**Purpose:** the post-CLI configuration surface. The withdrawn CLI owned endpoint editing (`--mac-ip`, `trsetup config`), the settings-file path display, and profile inspection; the Setup screen (REQ-UI-003) covers roles + app only. This screen puts the full config surface in the MAUI app: roles, selected app, named endpoint values (`TrSetupSettings.Endpoints` — today the LAN Mac IP the Bridges probes target), the settings-file path, and read-only profile details for the selected app.

**Layout (one line):** same sidebar shell; header (title + back-to-board); stacked regions — role cards · app Select · Endpoints inputs · read-only Profile details table · settings-file-path footer line · Save/Cancel.

**Component map:**

| Region | TrBlazeUI control | Shows / binds | States |
|--------|-------------------|---------------|--------|
| Header | `TypographyH2` + `Button` (Ghost, `arrow-left`) "Back to board" | page title "Settings" | — |
| Roles | 4× `Card` each wrapping a `Checkbox` + `LucideIcon` + title + one-liner, plus the native-dev `Switch` — same role-card pattern as `/setup` (REQ-UI-003) | current roles pre-ticked | at least one role required |
| App profile | `Select` | selected app (Framework only / AppStudio / TrStudio) | change refreshes the Profile details region |
| Endpoints | one labelled text `Input` per known endpoint (`Label` + `Input`) — today `MacIp` | `TrSetupSettings.Endpoints` values | address-only validation (IP/hostname); never a secret field (ADR-008 — secrets are presence-only, never entered or shown here) |
| Profile details (read-only) | `Card` + `DataTable`-style table | the selected app's requirement rows (id, type, roles) + a **Source** column: built-in profile vs `.tfcore/trsetup-profile.json` app-repo override (which one wins per REQ-FN-021 merge rules) | no app selected → `Empty` |
| Settings-file path | `TypographyMuted` footer info line (`file-cog` icon) | full path of the settings JSON (parity with the withdrawn `trsetup config`) | — |
| Actions | `Button` (Default) "Save" + `Button` (Outline) "Cancel" | Save persists via `JsonSettingsStore` (REQ-FN-005); Cancel returns to the board unchanged | Save disabled while an endpoint value fails validation |

**Notes / interactions:** no separate HTML mockup — extend `docs/mockups/role-picker.html` patterns; this control map is the build contract for /trblazeui (REQ-UI-006, BRD-56). On save the board re-scopes to the new roles/app/endpoints without reload.

**Acceptance pointers (checklist REQ-UI-006):** settings persist via `JsonSettingsStore` (REQ-FN-005) and survive relaunch; the board re-scopes on save without reload; endpoint inputs validate as addresses and never accept/display secrets (ADR-008); profile-details pane shows built-in vs override source per the REQ-FN-021 merge rules; every interactive control carries a stable `data-testid` (REQ-NFR-005/BRD-52 discipline).

**Empty / loading / error:** save failure → `Alert` (Danger) above the actions with the file error; profile load failure → `Alert` (Danger) in the Profile details region with the loader error.

### Screen: Fix-all run view (`/fix-run`)

**Mockup:** [docs/mockups/fix-run.html](./mockups/fix-run.html) · **Role(s):** all · **BRD:** BRD-29, BRD-30 · **REQ:** REQ-UI-004

**Layout (one line):** header with overall `Progress` + step counter; ordered step list (dependency order) where done steps are compact ✓ rows, the active step is expanded (live output or consent gate), pending steps are muted; footer Stop button.

**Component map:**

| Region | TrBlazeUI control | Shows / binds | States |
|--------|-------------------|---------------|--------|
| Run header | `Card` with `Progress` + `TypographyMuted` "Step 3 of 7 — Appium + uiautomator2" | overall run state | completed → success `Alert` |
| Step list | `ItemGroup`; one `Item` per fix in dependency order | step icon (✓ done / ⟳ active `Spinner` / ○ pending / ✗ failed), title, one-line result | — |
| Consent gate (active step) | inline `AlertDialog`-style `Card` (accent border) | "This step needs elevation" + exact command in `pre` + `Button` "Approve & run" (Default) / "Decline & stop" (Outline) | declined → run halts; later steps stay pending |
| Live output (active step) | dark `pre` in `ScrollArea` (auto-follow) | streaming stdout/stderr | — |
| Failed step | `Collapsible` open with raw output + exit code `Badge` (destructive) | "Continue anyway" / "Stop run" `Button`s | — |
| Footer | `Button` (Destructive Outline) "Stop after current step" | — | — |

**Notes / interactions:** entered from any "Fix all" button with >1 queued fix; single fixes run inline on the board. Order examples surfaced in step subtitles ("runs before: Appium").

**Empty / loading / error:** nothing queued → `Empty` ("Nothing to fix — board is green") with "Back to board" action.

### Screen: Report preview (`/report`)

**Mockup:** [docs/mockups/report.html](./mockups/report.html) · **Role(s):** all · **BRD:** BRD-24, BRD-25 · **REQ:** REQ-UI-005

**Layout (one line):** toolbar (Save .md · Save .html · Copy) above a Card-framed rendered preview of `TrSetup-Report-<host>` — host/roles/app header, per-group status tables, evidence lines.

**Component map:**

| Region | TrBlazeUI control | Shows / binds | States |
|--------|-------------------|---------------|--------|
| Toolbar | `Toolbar` with `ToolbarGroup` + `ToolbarButton`s (`file-down`, `file-code`, `copy`) | Save as MD / Save as HTML / Copy markdown | copy → toast "Report copied" |
| Report header | `Card` header block | host name, date, roles, selected app, overall counts as `Badge`s | — |
| Group sections | per group: `TypographyH3` + `DataTable`-style table | columns: Status (icon+text) · Check · Evidence | — |
| Secret rows | table row + `Badge` (Outline) "presence-only" | e.g. "RunPod:ApiKey — present (value never shown)" | — |
| Footnote | `TypographyMuted` | "Generated by TrSetup — safe to paste into a Claude session (no secret values)." | — |

**Notes / interactions:** preview reflects the board at export time; regenerate via "Re-check all" on the board first.

**Empty / loading / error:** export before first sweep → `Empty` ("Run a check sweep first").

## TUI (Spectre.Console) — not a TrBlazeUI screen

**[WITHDRAWN 2026-07-09 — owner decision: single MAUI desktop app; the CLI/TUI head retires (see checklist REQ-FN-034). Section retained for history.]**

The `trsetup` terminal TUI (REQ-FN-012/013) renders the **same board model** — groups, status glyphs, evidence, `f`/`a`/`r`/`e` keys — via Spectre.Console. It is deliberately **not** mocked in HTML here: it is not built by `/trblazeui` and its visual truth is the terminal. The board grouping, status vocabulary, and action set MUST match the dashboard mockup 1:1 (same engine, thin renderer — Architecture ADR-005). The plan's ASCII sketch in BRD §9 F-BOARD is the TUI's layout reference.
