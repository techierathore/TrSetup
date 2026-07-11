> ARCHIVED 2026-07-09 — the CLI agent mode (REQ-FN-032/033) was withdrawn with the CLI head (owner decision: single MAUI desktop app).

# TrSetup — Agent Mode (`--check --json`) & the verify-phase §0 pre-flight gate

> **Status: BUILT — P5.** `trsetup --check --json` is REQ-FN-032 (built, P5); the optional verify-phase §0 pre-flight gate documented in §4 below is REQ-FN-033 (BRD-47, **explicitly optional stretch**). This doc is the stable reference the exit-code / wire-schema contract in `TrSetup.Cli/AgentMode/*` points at.

## 1. What it is

`trsetup --check --json` runs a full detect sweep over the board scoped to this machine's roles + selected app (exactly what the TUI/GUI render), then writes a **machine-readable JSON document to stdout** — nothing else on stdout, no ANSI — and sets a **process exit code that reflects overall status**. It is the headless surface for AI agents and CI: no UI, no interaction.

```bash
trsetup --check --json         # board JSON on stdout; exit code = overall status
```

The sweep is the same `CheckEngine` the GUI/TUI use (ADR-005), so the JSON is never a second implementation — it is the one board, serialized.

## 2. Exit-code contract (stable)

Relied on by CI and by the §4 pre-flight gate. Source of truth: `TrSetup.Cli/AgentMode/TrSetupExitCodes.cs`.

| Code | Meaning | When |
|------|---------|------|
| `0` | **Environment green** | No in-scope **Required**-severity check is failing (warnings and *optional* fails are allowed) |
| `1` | **Required check failed** | At least one in-scope check with `Required` severity is `fail` |
| `2` | **Usage / internal error** | Bad command line, or an internal error before/while producing output |

Note the deliberate design: a `Recommended`/`Optional` failure or any `warn` does **not** flip the exit code — only a **Required** failure gates. This keeps the pre-flight gate about "can the environment actually work", not "is everything perfect".

## 3. Wire schema (version 1)

Stable, versioned. Property names/order are the contract; a breaking change bumps `schemaVersion`. Source of truth: `TrSetup.Cli/AgentMode/BoardReport*.cs`.

```jsonc
{
  "schemaVersion": 1,
  "host": "LAPTOP-IBNQ33KO",          // machine the sweep ran on
  "roles": ["agentHostWsl"],          // camelCase MachineRole flag names in scope
  "selectedApp": "AppStudio",         // or null when framework-only
  "generatedUtc": "2026-07-07T05:39:49Z",
  "firstRun": false,                  // true → no settings file; roles platform-guessed, framework checks only
  "groups": [
    {
      "name": "Framework core",
      "checks": [
        {
          "id": "wsl.dotnet-sdk",     // stable check id (deep-link / gate target)
          "title": ".NET SDK present",
          "status": "pass",           // pass | warn | fail | notApplicable
          "severity": "required",     // required | recommended | optional
          "evidence": ".NET SDK 10.x present…",
          "manualOnly": false         // true → no automated fixer (guidance only)
        }
      ]
    }
  ],
  "summary": { "pass": 20, "warn": 2, "fail": 5, "notApplicable": 0 }
}
```

Parsing tips for a gate:
- **Fast path:** just read the process exit code (§2) — that already encodes "environment green".
- **Rich path:** to name what's red, filter `groups[].checks[]` where `status == "fail" && severity == "required"` and report their `id` + `evidence`.

## 4. verify-phase §0 pre-flight gate (REQ-FN-033 — OPTIONAL stretch)

> **This hook is explicitly optional.** TrSetup ships the surface and a reference gate; wiring it into a given verify pass or CI pipeline is the operator's choice. Nothing in TrSetup *requires* it.

The idea (BRD-47): before a TechieFlow verify pass (or any CI job that assumes a working dev environment), run `trsetup --check --json` as a **§0 environment pre-flight**. If the environment isn't green, **fail fast with the exact red checks** rather than letting a downstream step fail confusingly deep in.

### The reference gate

`scripts/preflight-gate.sh` (bash) and `scripts/preflight-gate.ps1` (PowerShell twin) are the shipped example. Each:

1. Locates the `trsetup` binary — first CLI arg, else `publish/<rid>/trsetup/trsetup`, else `trsetup` on `PATH`.
2. Runs `trsetup --check --json`, capturing stdout + exit code.
3. Prints the `summary` line (pass / warn / fail).
4. Gates on the exit code: **0 → prints "environment green" and exits 0** (the verify/CI job proceeds); **1 → prints every failing Required check `id` + `evidence` and exits 1** (job blocked); **2 → usage/internal error, exits 2**.

```bash
# Example verify-phase §0 usage — block the pass unless the environment is green
scripts/preflight-gate.sh && echo "→ running verify pass" || { echo "→ environment not green; fix the red checks first"; exit 1; }

# Or point it at a specific self-contained publish
scripts/preflight-gate.sh publish/linux-x64/trsetup/trsetup
```

Because the gate is a thin wrapper over the exit-code contract (§2), it works identically against a from-source run (`dotnet run --project src/TrSetup.Cli -- --check --json`) or a self-contained publish (`publish/<rid>/trsetup/trsetup --check --json`) — the same portability the Cli head has (REQ-NFR-006).

### Wiring it into TechieFlow verify-phase §0 (optional)

A verifier that wants the gate adds a §0 step: run `scripts/preflight-gate.sh`; if it exits non-zero, halt the verify pass and surface the printed red checks as the blocker (the operator runs `trsetup` to fix them, then re-runs verify). This keeps the gate **advisory and opt-in** — it never mutates anything, it only reads the board.

---
*Created 2026-07-07 (REQ-FN-032 reference + REQ-FN-033 optional pre-flight gate). Render to HTML via `*generate-html docs/TrSetup-AgentMode.md` on the next docs pass.*
