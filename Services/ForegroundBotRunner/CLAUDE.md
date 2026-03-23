# ForegroundBotRunner — DLL-Injected In-Process Bot

In-process bot that runs inside WoW.exe via DLL injection. Implements `IObjectManager` through direct memory access and Lua calls on the game's main thread.

## Build & Run

Not launched directly — StateManager orchestrates the full pipeline:
1. `CreateProcess(WoW.exe)`
2. `VirtualAllocEx` + `WriteProcessMemory` + `CreateRemoteThread(LoadLibraryW)` → injects `Loader.dll`
3. Loader.dll (C++) bootstraps .NET 8 CLR → loads `ForegroundBotRunner.dll`
4. `Loader.Load()` → spawns STA thread → `Program.StartInjected()` → `ForegroundBotWorker`

## Key Files

| File | Lines | Purpose |
|------|-------|---------|
| `Program.cs` | 133 | Dual-mode entry: Main() (standalone) or StartInjected() (injected) |
| `Loader.cs` | 129 | Native .NET 8 host entry point called by LoadLibraryW |
| `ForegroundBotWorker.cs` | 605 | Main service loop — ObjectManager init, BotRunnerService, anti-AFK |
| `Statics/ObjectManager.cs` | ~2900 | **LARGEST FILE** — IObjectManager impl, object enumeration, Lua dispatch, inventory, screen detection |
| `Statics/WoWEventHandler.cs` | 800 | Event sink for in-game signals |
| `Statics/LoginStateMonitor.cs` | 289 | Diagnostic login state logger |
| `Mem/Memory.cs` | 379 | Core read/write at raw pointers |
| `Mem/Offsets.cs` | 309 | Static memory offset table (1.12.1 addresses) |
| `Mem/Functions.cs` | 263 | Lua call wrappers, LuaState access |
| `Mem/ThreadSynchronizer.cs` | 423 | WndProc hook + WM_USER message queue for main-thread Lua |
| `Mem/Hooks/PacketLogger.cs` | 800+ | Hooks NetClient::Send and NetClient::ProcessMessage for CMSG/SMSG capture |
| `Mem/Hooks/ConnectionStateMachine.cs` | 237 | State machine: DISCONNECTED → AUTHENTICATING → CHARSELECT → IN_WORLD |
| `Mem/Hooks/SignalEventManager.cs` | 229 | Hooks WoW's SignalEvent system for game events |
| `Mem/AntiWarden/WardenDisabler.cs` | 449 | Patches Warden anti-cheat detection |
| `Frames/FgLoginScreen.cs` | 101 | Login with 15s cooldown (prevents ERROR #134) |
| `Frames/FgRealmSelectScreen.cs` | 154 | 3-strategy realm selection rotation |
| `MovementRecorder.cs` | 923 | Movement telemetry for physics replay |

## Directory Structure

| Directory | Purpose |
|-----------|---------|
| `Mem/` | Direct memory manipulation, offsets, Lua wrappers |
| `Mem/Hooks/` | Assembly injection hooks (packets, events, state machine) |
| `Mem/AntiWarden/` | Warden anti-cheat bypass |
| `Objects/` | Memory-mapped WoW object types (WoWUnit, WoWPlayer, LocalPlayer, etc.) |
| `Statics/` | Singleton managers (ObjectManager, WoWEventHandler) |
| `Frames/` | UI automation (login, realm, character select, dialogs) |
| `CombatRotations/` | Class-specific combat rotation profiles |
| `Questing/` | Quest automation (coordinator, log, NPC interaction) |
| `Grouping/` | Party/dungeon management |
| `Logging/` | Named pipe logger back to StateManager |

## Dependencies

- **GameData.Core** — Interfaces (IObjectManager, IWoWUnit, etc.)
- **BotCommLayer** — Protobuf IPC
- **BotRunner** — BotRunnerService behavior trees
- **WoWSharpClient** — WoW protocol types
- **WinImports** — Windows API P/Invoke
- **BotProfiles** — Combat rotation profiles
- **Fasm.NET** — x86 assembly injection for hooks

## Critical Architecture

### ThreadSynchronizer
All Lua calls MUST go through `ThreadSynchronizer.RunOnMainThread()`. Background thread Lua calls silently fail or crash.
- WndProc hook patches GWL_WNDPROC on WoW's window
- Actions queued, processed via WM_USER messages on main thread
- `ManualResetEventSlim` synchronizes caller with completion

### ConnectionStateMachine Lifecycle
```
DISCONNECTED → AUTHENTICATING → CHARSELECT → ENTERING_WORLD → IN_WORLD → TRANSFERRING → IN_WORLD
```
Safety signals: `IsLuaSafe`, `IsObjectManagerValid`, `IsSendingSafe`

### Screen Transition Cooldown
`ObjectManager.IsInScreenTransitionCooldown` — 2s delay after WoWScreenState changes before issuing Lua commands. Prevents ACCESS_VIOLATION crashes during animations.

### PauseNativeCallsDuringWorldEntry
Volatile flag that blocks ThreadSynchronizer WM_USER processing during world entry handshake.

## Key Memory Offsets (1.12.1)

| Offset | Purpose |
|--------|---------|
| `0xB41478` | LoginState string ("login", "connecting", "charselect") |
| `0x0086F694` | ContinentId |
| `0x00B41DA0` | ClientConnection pointer (null when disconnected) |
| `0x9B8/9BC/9C0` | Unit Position X/Y/Z |
| `0x005379A0` | NetClient::Send (hooked by PacketLogger) |
| `0x60BEA0` | Right-click Unit function |
| `0x5F8660` | Right-click GameObject function |
| `0x00468380` | EnumerateVisibleObjects function |

## Vanilla 1.12.1 Specifics

- **EnumerateVisibleObjects callback**: `int callback(int filter, ulong guid)` — filter FIRST, guid SECOND (opposite of TBC/WotLK)
- **CastSpell(int)** is a no-op — only string overload works via Lua `CastSpellByName`
- Low GUIDs (5, 10, etc.) are valid on private servers

## Diagnostic Logs

Written to `<WoW.exe dir>/WWoWLogs/`:
- `crash_trace.log` — ACCESS_VIOLATION diagnostics
- `packet_logger.log` — CMSG/SMSG captures
- `connection_state_machine.log` — State transitions
- `signal_event_manager.log` — Game event hooks

## Warning

`ObjectManager.cs` is ~2900 lines. Always use offset/limit when reading. Refactoring into partial classes is planned (Phase 4.3).
