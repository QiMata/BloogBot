```prompt
## Session Handoff — Physics Engine Calibration

### What Was Accomplished Last Session

1. **Fixed π speed multiplier bug** — `MovementController.ApplyPhysicsResult()` used growing `_accumulatedDelta` instead of per-frame `deltaSec` for dead-reckoning, causing ~3.14x movement speed. Fixed in `Exports/WoWSharpClient/Movement/MovementController.cs`.

2. **Fixed CAST_FAILED spam** — Three-pronged fix:
   - `Services/WoWStateManager/Coordination/CombatCoordinator.cs`: `ACTION_COOLDOWN_SEC` 1.5→3.0 (Lightning Bolt = 2.5s cast)
   - `Exports/BotRunner/BotRunnerService.cs`: Added stop movement + face target before cast in `BuildCastSpellSequence`
   - Root causes were: 0x61=SPELL_IN_PROGRESS, 0x23=INTERRUPTED, 0x7C=UNIT_NOT_INFRONT

3. **Fixed collection modified thread-safety** — `Exports/WoWSharpClient/WoWSharpObjectManager.cs`: Added `_objectsLock`, `Objects` property now returns `_objects.ToArray()` snapshot. All mutations wrapped in `lock (_objectsLock)`.

4. **Merged latest `main` into `cpp_physics_system`** — Commit `fe3526e`, pushed to remote. All conflicts resolved favoring our branch.

5. **Physics tests: 58 passed, 1 skipped, 0 failed** — All recordings replay within current tolerances (avg<0.055y, P99<0.25y).

### What to Work On Next

**Primary goal: Achieve P99 position error < 0.0001y (4+ decimal places) between simulated and recorded WoW client frames.**

Read `docs/TASKS.md` for the full prioritized task list. Start with:

#### CRITICAL: Task 2.5a — Fix Physics Engine Returning Unchanged Positions

The background bot's MovementController calls the PathfindingService physics engine every tick, but the C++ engine returns the SAME position as input (`physics=0` count vs `deadReckon=1451`). Dead-reckoning does ALL the actual movement. The engine works perfectly in tests (58/58 pass) but fails in live operation.

**Investigation path:**
1. Add verbose logging to `Services/PathfindingService/PathfindingSocketServer.cs:HandlePhysics()` — log the actual `moveFlags`, `runSpeed`, `deltaTime` received by the native engine, and the output position delta
2. Check if `prevGroundZ = float.NegativeInfinity` (initial value in MovementController) causes the engine to treat the character as "no ground found" and skip movement
3. Verify the proto PhysicsInput field mapping in `PathfindingSocketServer.cs:ToPhysicsInput()` produces a valid `Repository.PhysicsInput` struct
4. Check if `SanitizeOutput()` in `Repository/Physics.cs:146` is zeroing velocities (it checks `MOVEFLAG_MASK_MOVING_OR_TURN`)

**Key files:**
- `Exports/WoWSharpClient/Movement/MovementController.cs` — builds PhysicsInput, calls PathfindingClient
- `Services/PathfindingService/PathfindingSocketServer.cs:190` — `HandlePhysics()` + `ToPhysicsInput()`
- `Services/PathfindingService/Repository/Physics.cs:130` — `StepPhysicsV2()` P/Invoke + `SanitizeOutput()`
- `Exports/Navigation/PhysicsBridge.h` — C++ struct layout (MUST match C# `PhysicsInput` in Repository/Physics.cs)
- `Exports/Navigation/PhysicsEngine.cpp:1432` — `StepV2()` C++ entry point

#### After 2.5a: Task 2.5b — Epsilon Calibration

Key constants affecting precision (see TASKS.md 2.5b for full list):
- `AIR_SWEEP_MARGIN = 0.5f` (PhysicsMovement.cpp:83)
- `LANDING_TOLERANCE = 0.1f` (PhysicsMovement.cpp:84)
- `contactOffset = 0.02f` (PhysicsTolerances.h)
- `STEP_DOWN_HEIGHT = 4.0f` (PhysicsEngine.h:33)

For each: sweep values, replay all recordings, find minimum P99.

#### After calibration target met: Continue with TASKS.md

Make bots operate more intelligently (Phase 4.4) and ensure all actions work on both injected and headless clients (Phase 3 feature parity table in TASKS.md).

### Known Gaps in the Stateless Physics System

See TASKS.md section 2.5c for the full gap analysis table. Key gaps:
1. Ground contact persistence — re-probing every frame causes slope jitter
2. Velocity continuity — no validation between position delta and velocity
3. Dead-reckoning divergence — two movement systems fighting when physics returns unchanged pos
4. Transport frame lag — NearbyGameObjects position may lag 1+ frames

### Test Commands

```bash
# Physics tests (58 pass, ~17 min due to map loading)
dotnet test Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj -v n

# Build specific projects
dotnet build Exports/WoWSharpClient/WoWSharpClient.csproj --no-restore
dotnet build Services/PathfindingService/PathfindingService.csproj --no-restore

# Kill project processes (NEVER kill 'dotnet' blanket!)
powershell -Command "Stop-Process -Name WoWStateManager,BackgroundBotRunner,PathfindingService,WoW -Force -ErrorAction SilentlyContinue"
```

### Current Branch State
- Branch: `cpp_physics_system`
- Latest commit: `fe3526e` (merged main)
- Remote: pushed and up to date
- All builds succeed, all physics tests pass
```
