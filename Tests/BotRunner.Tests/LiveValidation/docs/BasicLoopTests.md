# BasicLoopTests

Minimal live-validation health checks that remain after the overhaul deleted setup-only scenarios.

## Bot Execution Mode

**Dual-Bot Conditional** — BG always runs. FG runs when IsFgActionable. FG serves as ground truth for physics/position checks. See [TEST_EXECUTION_MODES.md](TEST_EXECUTION_MODES.md).

## Active Tests

### 1. LoginAndEnterWorld_BothBotsPresent

**Purpose:** Verify the fixture delivered actionable BG state and, when available, actionable FG state.

**Code paths:**
- Test entry: `Tests/BotRunner.Tests/LiveValidation/BasicLoopTests.cs`
- Snapshot refresh: `Tests/BotRunner.Tests/LiveValidation/LiveBotFixture.Snapshots.cs`
- Snapshot production: `Exports/BotRunner/BotRunnerService.cs`
- State transport: `Services/WoWStateManager/StateManagerWorker.cs`

**Assertions:**
- `ScreenState == "InWorld"`
- Character/account names populated
- GUID and position populated
- `LiveBotFixture.IsStrictAlive(...)` returns true

### 2. Physics_PlayerNotFallingThroughWorld

**Purpose:** Keep a single low-level physics regression check in the live suite while heavier movement coverage moves into task-driven tests.

**Code paths:**
- Test entry: `Tests/BotRunner.Tests/LiveValidation/BasicLoopTests.cs`
- Clean-slate setup: `Tests/BotRunner.Tests/LiveValidation/LiveBotFixture.Assertions.cs`
- Stabilization polling: `Tests/BotRunner.Tests/LiveValidation/LiveBotFixture.cs`
- BG movement/physics source: `Services/PathfindingService/`
- FG position source: `Services/ForegroundBotRunner/Statics/ObjectManager.cs`

**Assertions:**
- Final Z remains above `-500`
- Z samples stabilize within 3 seconds
- FG and BG both report stable Z when FG is actionable

## Removed In Overhaul Pass 1

- `Teleport_PlayerMovesToNewPosition`
- `Snapshot_SeesNearbyUnits`
- `Snapshot_SeesNearbyGameObjects`
- `SetLevel_ChangesPlayerLevel`
