# Troubleshooting

> Common failures and their fixes, by symptom. For code-level "which file owns
> this behavior" navigation see [`../AGENTS.md`](../AGENTS.md) §10; for constants,
> env paths, and known issues see [`TECHNICAL_NOTES.md`](TECHNICAL_NOTES.md).

## Build & environment

| Symptom | Cause | Fix |
|---|---|---|
| `MSB3027` / `MSB3021` — cannot copy a native DLL (`FastCall.dll`, `Loader.dll`, `Navigation.dll`) | A running `WoW.exe` has the injected DLL locked | Find the PID and kill **only that PID** (`tasklist /FI "IMAGENAME eq WoW.exe"`, then `taskkill /F /PID <pid>`). Never blanket-kill — see [`security.md`](security.md). |
| Managed build fails referencing missing `FastCall.dll`/`Loader.dll`/`Navigation.dll` | The native C++ outputs were never produced; a `dotnet`-only build is incomplete | Run `scripts/build -Native` (needs Visual Studio + the `v145` toolset). See [`local-development.md`](local-development.md). |
| `scripts/bootstrap` stops with "compatible SDK not found" | Only a newer SDK (9.x/10.x) is installed | Install the **.NET 8 SDK** — pinned by [`../global.json`](../global.json) to match CI. |
| `scripts/build -Native` reports MSBuild/toolset not found | C++ workload or `v145` toolset missing | Install VS *Desktop development with C++*; the script prints what to install. Raw MSBuild commands: [`../AGENTS.md`](../AGENTS.md) §5, [`BUILD.md`](BUILD.md). |
| `scripts/lint` fails | Formatting drift (`dotnet format --verify-no-changes`) | Run `scripts/format` to apply. Lint is advisory in `scripts/check` (no `.editorconfig` baseline yet). |

## Tests

| Symptom | Cause | Fix |
|---|---|---|
| Layer 4 / integration tests fail to connect | MaNGOS stack not running | Bring it up first (see [`local-development.md`](local-development.md), [`DOCKER_STACK.md`](DOCKER_STACK.md)); use `scripts/test-integration` only with the stack up. |
| A live test "skips" for a missing pool/node/mob | **This is a real failure**, not an acceptable skip | Treat as a detection/pathfinding/ObjectManager bug. See the Test Skip Policy in [`../CLAUDE.md`](../CLAUDE.md). |
| A live test seems to bypass DecisionEngine | Test hand-builds `ObjectiveMessage` (remote-control anti-pattern) | Rewrite to declare an Activity and assert on the snapshot — [`testing.md`](testing.md). |
| Fixture targets resolve to Shodan and throw | Shodan is director-only, never a subject | Dispatch to dedicated test accounts via `ResolveBotRunnerActionTargets()` — [`security.md`](security.md). |

## Runtime / live behavior

| Symptom | Where to look |
|---|---|
| Bot not moving / pathing failure | `Services/PathfindingService` → `Exports/Navigation/PathFinder.cpp` |
| Physics glitch (falling, clipping, sliding) | `Exports/Navigation/PhysicsEngine.cpp`, `PhysicsCollideSlide.cpp` — but **read [`physics/README.md`](physics/README.md) first** (frozen stack) |
| Wrong spell / combat rotation | `BotProfiles/<ClassSpec>/` |
| Login / connection failure | `Exports/WoWSharpClient/Client`, `Exports/WoWSharpClient/Networking` |
| State machine stuck | `Services/WoWStateManager/StateManagerWorker.cs` |
| IPC / service comms issue | `Exports/BotCommLayer/*` — see [`IPC_COMMUNICATION.md`](IPC_COMMUNICATION.md) |
| DLL injection failure | `Exports/Loader/dllmain.cpp`, `Exports/Loader/simple_loader.cpp` |
| Decision engine wrong choice | `Services/DecisionEngineService/`, [`Spec/20_DECISION_ENGINE.md`](Spec/20_DECISION_ENGINE.md) |

## Stale data when a character is online

A MySQL read for an **online** character returns stale data — the server holds
authoritative state in memory and only persists on save/logout. Never assert
live state from a direct DB read; use StateManager snapshots instead. Details:
[`../CLAUDE.md`](../CLAUDE.md) (MaNGOS Data Access).

## Process cleanup (safe)

Never run blanket kills (`taskkill /F /IM dotnet.exe`, `Stop-Process -Name dotnet`,
`taskkill /F /IM Game.exe`). Other Claude/CI sessions may be running. Use the
repo-scoped helpers:

```powershell
.\run-tests.ps1 -ListRepoScopedProcesses
.\run-tests.ps1 -CleanupRepoScopedOnly
```

See [`security.md`](security.md) and [`../AGENTS.md`](../AGENTS.md) §6.
