# Security & Operational Guardrails

> WWoW has no external attack surface — it runs entirely against a **local**
> private server with no cloud services or credentials. The risks here are
> *operational*: destructive process kills, unsafe DB writes, and trust
> boundaries around code injection. This page consolidates the guardrails that
> are otherwise spread across [`../CLAUDE.md`](../CLAUDE.md) and
> [`../AGENTS.md`](../AGENTS.md).

## Local-only trust model

- **No cloud, no cloud credentials.** The game server, SOAP API, and database
  all run locally (see [`local-development.md`](local-development.md)).
- The only credentials anywhere are the **local DB** (`root`/`root` by default,
  overridable via `WWOW_VMANGOS_DB_ROOT_PASSWORD`) and the **local SOAP
  `ADMINISTRATOR`** account used by the integration test director.
- The inner loop (`scripts/bootstrap` → `scripts/build` → `scripts/test-fast`)
  needs none of them.

## Process safety (critical)

Multiple Claude/CI sessions may run concurrently on the same machine. A blanket
process kill destroys other runs and orphans game clients.

**Never run:** `taskkill /F /IM dotnet.exe`, `Stop-Process -Name dotnet`,
`pkill dotnet`, `taskkill /F /IM Game.exe`, or any equivalent.

**Instead:** kill only the specific PID you started, and use the repo-scoped
helpers `run-tests.ps1 -ListRepoScopedProcesses` / `-CleanupRepoScopedOnly`.
Full rule: [`../AGENTS.md`](../AGENTS.md) §6.

## MaNGOS data access — SOAP only

**Never write the MaNGOS MySQL database directly.** All mutable server
operations go through the **SOAP API** at `http://127.0.0.1:7878/`
(`ADMINISTRATOR:PASSWORD`).

- Allowed: **read-only** queries for connectivity checks and non-mutating
  fixtures.
- Sanctioned write helpers: `ExecuteGMCommandAsync(...)` (SOAP) and
  `SendGmChatCommandAsync(...)` (bot chat).
- One documented exception: `EnsureGmCommandsEnabledAsync()` bootstraps GM level
  via MySQL because SOAP itself requires GM access.
- Online-character DB reads are **stale** — see [`troubleshooting.md`](troubleshooting.md).

Full policy: [`../CLAUDE.md`](../CLAUDE.md) (MaNGOS Data Access).

## GM access via Shodan

`Shodan` is a **production GM character** — the liaison that lets human players
request on-demand activities, and the **test director** for setup that needs GM
targeting (`.tele`, `.additem`, `.gobject respawn`). It is governed by a hard
rule:

- Shodan is **never** the subject of a behavior test. The fixture
  (`ResolveBotRunnerActionTargets`, `StageBotRunner*Async`) throws if Shodan is
  staged as an action target.
- Behavior tests dispatch to dedicated test accounts (`TESTBOT1`/`TESTBOT2` and
  category siblings) and assert on those snapshots.

Details: [`../CLAUDE.md`](../CLAUDE.md) (Shodan) and [`testing.md`](testing.md).

## Code-injection trust boundary

Foreground mode injects `Loader.dll` into `WoW.exe` and bootstraps the CLR
in-process (`Exports/Loader`). This is trusted, local-only tooling against your
own client. Foreground work must be **state-gated** and must **not steal focus
or capture the cursor** (a real user may share the desktop). The headless
Background runtime avoids injection entirely.

## See also

- [`../AGENTS.md`](../AGENTS.md) §3 (architecture boundaries), §6 (process safety), §7 (MaNGOS policy)
- [`../CLAUDE.md`](../CLAUDE.md) — Process Safety, MaNGOS Data Access, Shodan, Test Isolation
- [`troubleshooting.md`](troubleshooting.md) — safe cleanup commands
