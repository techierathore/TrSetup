# TrSetup settings — samples & how to configure

TrSetup persists one small JSON file (roles + selected app + endpoints — REQ-FN-005). The app reads it on every run; there is **no database**. You do **not** have to hand-edit it — the in-app **Settings screen** does it for you — but the file and these samples exist so you can also configure a headless / fresh machine by copying a file.

## Where the settings file lives

| OS | Path |
|----|------|
| Windows | `%APPDATA%\TrSetup\settings.json` |
| macOS / Linux / WSL | `~/.trsetup/settings.json` |

The **Settings screen** (`/settings`, the `sliders` item in the sidebar) shows the exact path on the current machine, plus the current roles/app/endpoints.

## Two ways to set roles + app + endpoints

1. **In-app Settings screen (easiest):** launch `TrSetup`, open **Settings** from the sidebar (or complete the first-run role picker). Edit roles, the native-dev variant, the selected app, and endpoint values (the LAN Mac IP) with in-app validation; **Save** persists to the file above and re-scopes the board without a reload. *(The old `trsetup` CLI and its `--roles/--app/--mac-ip` flags were removed in REQ-FN-034 — the Settings screen replaces them.)*
2. **Copy a sample file:** copy one of the files in this folder to the settings path above and edit it.

## Sample files in this folder

| File | Roles | App | Use for |
|------|-------|-----|---------|
| `framework-only-wsl.json` | `AgentHostWsl` | *(none)* | A WSL agent-host box checking only the core framework tools (no app profile rows). |
| `appstudio-wsl-agent.json` | `AgentHostWsl` | AppStudio | The common dev box: WSL agent host building/verifying **AppStudio**. |
| `appstudio-windows-device-host.json` | `DeviceHostWindows` | AppStudio | A Windows host serving the Android emulator + Appium for **AppStudio** device verification. |
| `appstudio-mac-runner.json` | `DeviceHostMac, AppRunnerMac` | AppStudio | A LAN Mac that both hosts Appium and builds/runs **AppStudio** (Catalyst). |
| `trstudio-full.json` | all four roles | TrStudio | A single machine wearing every role, verifying the heavy **TrStudio** profile (Postgres+PgVector, ffmpeg, ComfyUI runtime, disk-space floor, provider keys). |
| `trsetup-settings.sample.json` | `DeviceHostMac, AppRunnerMac` | AppStudio | The original generic sample (same shape as `appstudio-mac-runner.json`). |

Copy example (Mac app-runner):

```bash
cp docs/samples/appstudio-mac-runner.json ~/.trsetup/settings.json
```

## Field reference

- **`Roles`** — a comma-separated list of the roles THIS machine plays. Valid values:
  `AgentHostWsl`, `DeviceHostWindows`, `DeviceHostMac`, `AppRunnerMac`, and the optional `NativeDev` variant.
  A machine can hold several (e.g. a Mac that both hosts Appium and runs the apps: `"DeviceHostMac, AppRunnerMac"`).
  Only the checks for your roles run; everything else shows as `○ N/A` (that is by design — it is not an error).
- **`SelectedApp`** — `"AppStudio"`, `"TrStudio"`, or `null` for framework-only. Selecting an app adds that app's profile rows (SDKs, services, runtimes, keys) to the board.
- **`Endpoints`** — addresses/URLs for cross-machine probes. `"MacIp"` (the LAN Mac's address) and `"AppManagerUrl"` (REQ-FN-028 — this machine's App Manager URL, e.g. `"https://192.168.1.14:5101/health"`; blank/absent uses the AppStudio profile's `https://localhost:5101/` default). A profile `endpoint` requirement opts into being overridable by declaring a `urlSettingKey` param naming one of these keys. Secrets are **never** stored here (ADR-008).
- **`TrustedSelfSignedEndpoints`** — endpoint keys whose untrusted/self-signed TLS certificate you explicitly accept, e.g. `["AppManagerUrl"]`. Opt-in only, empty by default, and honoured **only** for a URL you configured yourself — a profile's built-in default URL is always fully validated.
