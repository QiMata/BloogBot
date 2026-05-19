# WoWStateManager - Central Bot Orchestration

Central orchestration service that manages both **Foreground** (DLL-injected into WoW.exe) and **Background** (headless protocol emulation) bots. Listens on **port 9001** (character state IPC) and **port 9000** (state manager API).

## Port assignment

WWoW services live in the **9000-9099 range** (post-2026-05-18 refactor; see commit history). This range gives wide separation from FFXIBot (which uses 5002 + 8088 — pre-refactor WWoW collided with both) and from any other game-bot solution under `E:\repos`. Each repo should pick its own 100-port block; service-specific assignments live in their respective appsettings.json.

| Port | Service | Role |
|---|---|---|
| 9000 | WoWStateManager | StateManagerListener (test-fixture queries, API) |
| 9001 | WoWStateManager | CharacterStateListener (bot snapshot poll) |
| 9002 | PathfindingService | Detour/Recast route service (Docker: wwow-pathfinding) |
| 9003 | SceneDataService | Local collision data (Docker: wwow-scene-data) |
| 9020-9029 | Test fixtures | PathfindingTestFixture, PathfindingValidationFixture |

MaNGOS server-side ports (3724 realm, 7878 SOAP, 8085 world) are unchanged — those are the WoW protocol's canonical ports.

## Key Files

| File | Purpose |
|------|---------|
| `Program.cs` | Service entry point |
| `StateManagerWorker.cs` | Orchestration: bot lifecycle, DLL injection, process monitoring |
| `MangosServerBootstrapper.cs` | Bootstraps connection to MaNGOS game server |
| `MangosServerOptions.cs` | Server connection configuration |
| `Settings/StateManagerSettings.cs` | Service configuration (ports, server connection) |
| `Settings/CharacterSettings.cs` | Per-character config: `BotRunnerType` (Foreground/Background), account, personality |

## Two Bot Modes

**Selection:** `CharacterSettings.RunnerType` (enum: `Foreground = 0`, `Background = 1`)

### Background (Headless)
- Pure C# protocol emulation via WoWSharpClient
- No WoW.exe needed — sends/receives WoW packets directly
- `StateManagerWorker.StartBackgroundBotWorker()` creates WoWClient + BotRunnerService

### Foreground (Injected)
- DLL injection into WoW.exe process
- Direct memory read/write + Lua execution inside game process
- Full launch pipeline in `StateManagerWorker.StartForegroundBotRunner()`:
  1. Sets env vars (`FOREGROUNDBOT_DLL_PATH`, `WWOW_ACCOUNT_NAME`, `WWOW_ACCOUNT_PASSWORD`)
  2. Creates named pipe log server for the injected bot
  3. `CreateProcess(WoW.exe)` (or attach to existing via `TargetProcessId`)
  4. Polls for WoW window (15s timeout)
  5. `VirtualAllocEx` + `WriteProcessMemory` + `CreateRemoteThread(LoadLibraryW)` → injects Loader.dll
  6. Loader bootstraps .NET 8 hostfxr → loads ForegroundBotRunner.dll → calls `Loader.Load()`
  7. ForegroundBotWorker connects back to StateManager on port 9001
  8. Monitoring task: checks process health every 5s, kills orphans after 60s

## Testing

- `BotServiceFixture` (in `Tests/Tests.Infrastructure/`) auto-starts StateManager if not running
- Configure `CharacterSettings` with `RunnerType = Foreground` to test injected path
- Configure `CharacterSettings` with `RunnerType = Background` to test headless path
- Both paths go through StateManager — no manual WoW.exe launching needed

## Config

`appsettings.json`:
- `GameClient:ExecutablePath` → path to WoW.exe (e.g. `D:\World of Warcraft\WoW.exe`)
- `LoaderDllPath` → path to Loader.dll
- `CharacterStateListener:Port` → 9001 (IPC for FG bots)
- `PathfindingService:Port` → 9002 (navigation)
- `StateManagerListener:Port` → 9000 (API)
