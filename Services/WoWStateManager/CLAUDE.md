# WoWStateManager - Central Bot Orchestration

Central orchestration service that manages both **Foreground** (DLL-injected into WoW.exe) and **Background** (headless protocol emulation) bots. Listens on **port 5002** (character state IPC) and **port 8088** (state manager API).

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
  7. ForegroundBotWorker connects back to StateManager on port 5002
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
- `CharacterStateListener:Port` → 5002 (IPC for FG bots)
- `PathfindingService:Port` → 5001 (navigation)
- `StateManagerListener:Port` → 8088 (API)
