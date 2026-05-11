# Next-session handoff — FG/BG physics parity (PFS-OVERHAUL-006)

> Working dir: `E:/repos/Westworld of Warcraft/`
> Branch: `main` at HEAD (pushed). Working tree clean except `.env`.

You are continuing PFS-OVERHAUL-006. The OG cliff-fall parity-break has
been LOCALIZED to three C++ snap-up sites. Round-3 prevGroundZ-aware
gates land cleanly (67/67 offline tests pass) but the LIVE validator
still fires the parity break because (a) `PrimeAirborneTeleportFallIfNeeded`
never fires in the bake-validator path (its `MovementFlags == MOVEFLAG_NONE`
precondition is gated out by residual flags), so prevGroundZ is not set
to the below-overhead probe value AND FALLINGFAR is not set, AND (b) a
THIRD snap-up path exists in the PhysicsStepV2 idle branch
(`Exports/Navigation/PhysicsEngine.cpp:5947-5961`) that has its own
GetGroundZ-based "stand on terrain" logic. See
`memory/project_pfs_overhaul_006_round3_session.md` for the full
round-3 diagnosis and the prerequisite for the next cycle.

> 2026-05-10 status (round-3 offline-verified, live still failing): the
> OG `smooth-wp01-cliff-fall-z42` parity break persists. Pre-1c530288:
> BG settled at z=51.62 (cliff fillet). Post-1c530288 + post-round-3:
> BG=53.317. FG=42.291. dz=11.03y. The round-3 PhysicsEngine.cpp +
> PhysicsMovement.cpp gates (prevGroundZ-aware) are CORRECT — verified
> via 2 new deterministic unit tests in
> `Tests/Navigation.Physics.Tests/AirborneOverheadLandingGuardTests.cs`
> + 4 OG parity tests + 14 ServerMovement + ~33 MovementControllerPhysics
> + 16 FrameByFrame = 67 offline tests green.

> 2026-05-11 round-4 iter-2 update: S0 diagnostic shipped (WRN-level
> Prime traces). Live log now PROVES Prime fires for cliff-fall, takes
> the drop<0 (overhead) branch, and the below-probe
> `NativeLocalPhysics.GetWalkableGroundZ(x, y, teleportZ-0.5, 150)`
> returns `belowProbeFound=true belowProbeZ=53.5` — **the SAME overhead
> deck at z=53.5, NOT the ADT below at z=42.29**. The probe is
> "nearest walkable", not "walkable strictly below fromZ". The deck
> 2.3y above the query wins over the ADT 8.9y below. Code at
> MovementController.cs:549 rejects via `belowZ <= teleportZ+0.1f`
> check, falls into no-support-found branch, sets
> `_hasPhysicsGroundContact=false` and LEAVES `_prevGroundZ` at stale
> previous-checkpoint value (~53.9). Round-3 `hasFarPrevGround` gate
> needs `(st.z - prevGroundZ) > STEP_HEIGHT` but actual is
> `51.7 - 53.9 = -2.2` → gate evaluates false → depen runs → snap to
> 53.317. See `memory/project_pfs_overhaul_006_round4_iter2.md`.
>
> Next iteration's fix surface: target the below-probe semantics. Best
> architectural option (Option C in iter-2 memo): don't try to set
> `_prevGroundZ` from C# Prime at all — set it to `INVALID_HEIGHT`
> when an overhead deck is detected, then change the round-3
> `hasFarPrevGround` gate to also fire when `prevGroundZ == INVALID
> && inputAirborneFlag` (FALLINGFAR set). Trust the C# "I'm primed
> for fall" signal even without a known fall reference Z.

## Priority order — DO NOT skip ahead

This applies to WWoW now and to every other game in the monorepo
(FFXI, WAR, UO, EQ, EQ2, PSO, Rag, SWG, D2) when their parity
harnesses come online.

1. **Scene-data flow.** ✅ Verified for WWoW: tile-mode `wwow-scene-data`
   container, 1244 tiles indexed, BG client fetches real triangles
   for tested checkpoints. See
   `memory/project_pfs_overhaul_006_scenedata_eof_diagnosis.md`.

2. **FG/BG physics parity.** ⬅️ **YOU ARE HERE.** BG's
   `Exports/Navigation/PhysicsEngine.cpp` ground-snap and capsule
   sweep must agree with real WoW.exe within
   `WaypointSettleValidator`'s parity tolerance (0.3y per axis).
   Currently 1/12 OG checkpoint breaks parity
   (`smooth-wp01-cliff-fall-z42`: FG=42.29, BG=51.62, dz=9.33y).
   That is the canary — fix it before touching pathfinding.

3. **Pathfinding correctness.** Only after #2 is clean. Frame-by-frame
   waypoint evaluation becomes reliable because both clients agree
   on what each polygon represents.

The full rule with rationale lives in
`memory/feedback_fgbg_physics_parity_priority.md` — read it first.

## What was completed in the prior session (do NOT redo)

| Commit  | Summary |
|---------|---------|
| `af46e835` | Harness drivers wrap teleport in `.gm on`/off; OG fixture promoted to recorder-observed Z values |
| `92920c23` | Bake-validation drivers wrap in `DisableForegroundPacketHooksForCrossMapTransfers` (prevents WoW.exe crash during cross-map teleport) |
| `9187d249` | `docker/linux/vmangos/start-realmd.sh` forces `StrictVersionCheck=0`; universal BG SRP6 auth unblock |
| `fed16c25` | Committed recorded BakeFixture artifacts from 2026-05-10 live runs |
| `e56f3ad7` | Added missing `scenes/` mount to `docker-compose.vmangos-linux.yml`; rebuilt `wwow-scene-data` container so its `ProtobufSocketServer.TryReadExact(allowCleanEndOfStream:true)` silences benign healthcheck disconnects |
| `f844d101` | Codified FG/BG parity priority + initial next-session handoff (mandate, hard rules, run order) |
| `7a79154a` | Localized cliff-fall parity break to `MovementController.cs:466-556` + `SceneCache::GetGroundZ` missing walkable filter. Diagnosis-only commit; updated handoff with concrete fix surfaces. |
| `1c530288` | Shipped round-1 fix. Added `SceneCache::GetWalkableGroundZ` (+ SceneQuery wrapper + C export + P/Invoke + `NativeLocalPhysics.GetWalkableGroundZ`). Switched the two post-teleport probes in MovementController. Fixed the gate to treat "support above teleport Z" as a falling condition (re-probes BELOW for fall reference). 4 new `OgZeppelinCliffFallParityTests` (all green). 7/7 standard physics regression gates pass (including `GroundMovement_Position_NotUnderground` underground guard). |
| (prior session) | Live-verified `1c530288` is PARTIAL. Round-2 attempt to gate `ApplyVerticalDepenetration` on `inputAirborneFlag` had no live effect (BG still 53.32). Reverted. No code shipped. Documentation only — see `memory/project_pfs_overhaul_006_depen_overhead_fix.md`. |
| (round 3, this session) | Localized cliff-fall snap-up to THREE C++ sites: (1) `ApplyVerticalDepenetration` overlap recovery, (2) `ProcessAirMovement` landing detection, (3) `PhysicsStepV2` idle branch. Shipped prevGroundZ-aware gates for (1) and (2), reverted experimental probes in `PhysicsGroundSnap.cpp`. Created `Tests/Navigation.Physics.Tests/AirborneOverheadLandingGuardTests.cs` (2 deterministic tests). All 67 offline tests pass. Live still fails: Prime isn't firing → my gates are no-ops → idle branch (3) re-snaps to deck. See `memory/project_pfs_overhaul_006_round3_session.md`. |

The harness's six acceptance items from the prior mandate are all
green or diagnostically delivered.

## The frontier — your mandate

### Acceptance items

1. **Make `PrimeAirborneTeleportFallIfNeeded` fire after each bake-validator
   teleport.** (PRIMARY BLOCKER — without this, round-3's correct fixes
   stay no-ops in live.)

   Prime's precondition at `MovementController.cs:472-474` requires
   `_player.MovementFlags == MovementFlags.MOVEFLAG_NONE`. In the
   bake-validator path the bot's MovementFlags retain residual intent bits
   (FORWARD etc.) between checkpoints, so Prime returns early. The BG log
   confirms ZERO "Airborne teleport primed falling state" entries across
   the entire run.

   Without Prime:
   - `_prevGroundZ` keeps the previous walkable Z (~53.x from the last
     checkpoint) instead of the below-overhead probe value (42.29).
   - `_player.MovementFlags` has no FALLINGFAR set when the first physics
     frame runs after the cliff-fall teleport.
   - Round-3's `inputAirborneFlag && hasFarPrevGround` gates are no-ops.
   - The bot takes the ground path → idle branch → snap UP to deck.

   Two clean options:

   **Option A (preferred): Detect teleport via position delta in
   MovementController.** The "Position changed outside physics by 19.085
   units" warning at `MovementController.cs:~636` is already a teleport
   signal. Fire Prime here regardless of MovementFlags state. The position
   delta check is more robust than the flag-state precondition.

   **Option B: Force MovementFlags = NONE in the bake-validator harness
   right before each teleport.** Search for the bake-fixture teleport
   routine in `Tests/BotRunner.Tests/LiveValidation/` and clear
   MovementFlags before sending the warp packet.

   Once Prime fires:
   - inputAirborneFlag=true (FALLINGFAR set).
   - `(st.z - input.prevGroundZ) = 51.7 - 42.29 = 9.4y > STEP_HEIGHT (2.028y)`
     → `hasFarPrevGround = true` → `skipVerticalDepen = true` (depen skipped).
   - `(startPos.z - input.prevGroundZ) > STEP_HEIGHT` → `genuinelyAirborne = true`
     → `rejectOverheadLanding = true` (ProcessAirMovement landing snap rejected).
   - Bot falls through to ProcessAirMovement's natural fall path. Lands on
     ADT at 42.29 within ~28 frames (per ComputeFallDisplacement).

2. **Add the prevGroundZ-aware gate to the PhysicsStepV2 idle branch**
   at `Exports/Navigation/PhysicsEngine.cpp:5947-5961`. Belt-and-suspenders
   on top of (1). Same gate shape:
   ```cpp
   const bool idleGenuinelyAirborne =
       (input.prevGroundZ > PhysicsConstants::INVALID_HEIGHT) &&
       inputAirborneFlag &&
       (st.z - input.prevGroundZ) > PhysicsConstants::STEP_HEIGHT;
   const bool idleOverhead = VMAP::IsValidHeight(idleGroundZ) &&
       idleGroundZ > st.z + LANDING_TOLERANCE;
   if (VMAP::IsValidHeight(idleGroundZ) &&
       idleGroundZ >= st.z - PhysicsConstants::STEP_DOWN_HEIGHT &&
       idleGroundZ <= st.z + PhysicsConstants::STEP_HEIGHT &&
       !(idleGenuinelyAirborne && idleOverhead)) {
       st.z = idleGroundZ;
       ...
   }
   ```
   Add a similar gate (using `startPos` if needed) — note the idle branch
   has `st.z` not `startPos`, so use `st.z` directly. Prerequisite: (1)
   must land first or this gate is also a no-op.

3. **Re-run the live OG validator.** Expected on success:
   - `passed=true` (zero `FG_BG_PARITY_BREAK` failures).
   - `smooth-wp01-cliff-fall-z42` BG settles at z≈42.29 within 0.3y of FG.
   - 11/11 walkable checkpoints continue to pass.

   ```powershell
   $env:WWOW_OG_ZEP_BAKE_FIXTURE = '1'
   $env:WWOW_DATA_DIR = 'D:\MaNGOS\data'
   dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj `
     --configuration Release --no-build `
     --filter 'FullyQualifiedName~OgZeppelin_BakeFixtureValidation'
   ```

   **Don't redo**: the round-1 SceneCache walkable filter (commit
   `1c530288`) IS correct and IS load-bearing. Round-3's PhysicsEngine.cpp
   + PhysicsMovement.cpp gates are CORRECT — they just need Prime to fire
   in live. Don't touch `Exports/Navigation/PhysicsGroundSnap.cpp` again
   (the experimental downward-probe approach was reverted; SceneCache may
   not have ADT triangles loaded on first frame post-teleport, making it
   unreliable). The deterministic
   `Tests/Navigation.Physics.Tests/AirborneOverheadLandingGuardTests.cs`
   tests verify the architecture; they MUST continue to pass.

2. **Add 2-3 more deck-edge / cliff-edge parity checkpoints** to the
   OG fixture so parity breaks have a wider surface, not just one
   case. Recorder workflow:
   ```powershell
   $env:WWOW_OG_ZEP_RECORD_FIXTURE = '1'
   dotnet test Tests/BotRunner.Tests --filter 'FullyQualifiedName~OgZeppelin_RecordFixture'
   ```
   Manually review the recorded JSON + screenshots, refine, then
   promote into the canonical `tools/MmapGen/test-fixtures/og-zeppelin.json`.

3. **Unblock BRM checkpoints.** All 4 BRD/LBRS/WMO-trap/Slice-F
   checkpoints currently fire `TELEPORT_FAILED` because WoW.exe
   crashes during the cross-map teleport. Without FG settle, BG
   parity can't be evaluated. Options:
   - WER local dumps + WinDbg to root-cause the BRD/LBRS crash
     (see `memory/project_pfs_overhaul_006_brm_iteration_session_summary.md`).
   - Use BG-only validation for BRM checkpoints — modify validator
     to accept BG-settled-Z as the reference when FG is unavailable.
     (Tradeoff: weakens parity-canary; but unblocks bake diagnosis.)
   - Approach BRM coords from an in-map starting position (e.g., FG
     teleport to BRM safe-zone first, then short hops to checkpoints)
     to avoid the cross-map crash.

4. **Generalize the harness to a per-game `IBakeValidationHost`
   adapter pattern** as a Skill so FFXI/WAR/UO/etc. can re-use the
   same validator + fixture format. The current `LiveBakeValidationHost`
   is WWoW-specific (binds to `LiveBotFixture`). Each game ports:
   - Its own settle-detection logic (snapshot polling).
   - Its own GM teleport command.
   - Its own multi-angle camera invocation (`.go xyzo` in WoW,
     `@goto` + `/lookat` in FFXI, equivalent in WAR).

### Hard rules — DO NOT violate

These come forward from `memory/MEMORY.md` and the root `CLAUDE.md`:

1. **`feedback_pathfinding_freeze.md`**: no extending `Navigation.cs`
   repair phases, route-packs, per-spot tests, or BotRunner
   boarding-position configs. Fix at the mesh OR the physics engine,
   never in managed repair.
2. **`feedback_pathfinding_anti_patterns.md`**: don't lower
   `walkableSlopeAngle`/`walkableClimb` from harvested client values;
   don't add bot-side jump-up for regular pathing. Bake-fidelity
   fixes go in `tools/MmapGen/`, physics fixes in
   `Exports/Navigation/`, but NOT in BotRunner runtime workarounds.
3. **`feedback_pathfinding_docker_reload.md`**: after every
   `tools/MmapGen` tile regen, `docker restart wwow-pathfinding`.
4. **`project_pathfinding_tile_coords.md`**: `MmapGen.exe --tile X,Y`
   is OPPOSITE-order from `<map>_<Y>_<X>.mmtile`. Trust the source.
5. **`project_pfs_overhaul_006_polyref_polyIdx_decoding.md`**:
   polyref ≠ polyIdx. Decode via `polyIdx = polyref & 0xFFFFF`.
6. **Universal changes, not edge-case bandaids.** Per-tile bake
   patches, per-spot fixture overrides, route-pack hand-tuning —
   all anti-pattern. The user's standing instruction.
7. **No silent swallowing** (`CLAUDE.md` R5): `catch {}` is banned in
   launch, injection, memory, packet, fixture, and protobuf paths.
8. **Process safety** (`Westworld of Warcraft/CLAUDE.md`): never
   blanket-kill `dotnet.exe` or `WoW.exe`. Kill only PIDs your
   session launched.
9. **SOAP not MySQL** for any character/server mutation. Only
   `EnsureGmCommandsEnabledAsync` may write MySQL.
10. **`.env` is local-only — never stage or commit it.** Working tree
    will show `M .env` indefinitely; ignore on every `git status`.
11. **Single session, auto-compact.** Do NOT start a new session.
    Commit + push frequently.
12. **JSON validator reports are authoritative** over prior memos.
    Cross-check before trusting historical narratives. See
    `memory/reference_high_velocity_commands.md`.

## Run order

```powershell
# 1. Verify clean baseline
cd 'E:\repos\Westworld of Warcraft'
git status --short                            # only .env modified
git log --oneline origin/main..HEAD           # empty
docker ps | Select-String "wwow-scene-data"   # tile mode, healthy

# 2. Confirm SceneData is still silent
docker logs --tail 20 wwow-scene-data         # only startup lines, no warnings

# 3. Read the prior parity reports (don't re-run yet)
Get-Content tmp/test-runtime/screenshots/long-pathing/bake-validation/og-zeppelin/bake-validation-*.json | ConvertFrom-Json
Get-Content tmp/test-runtime/screenshots/long-pathing/bake-validation/flamecrest-to-brm/bake-validation-*.json | ConvertFrom-Json

# 4. Inspect physics ground-snap path
# Open Exports/Navigation/PhysicsGroundSnap.cpp around line 285
# Open Exports/Navigation/PhysicsEngine.cpp ExecuteDownPass / GetGroundZ

# 5. If you need fresh OG parity data after a physics fix:
$env:WWOW_OG_ZEP_BAKE_FIXTURE = '1'
$env:WWOW_DATA_DIR = 'D:\MaNGOS\data'
dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build `
  --filter 'FullyQualifiedName~OgZeppelin_BakeFixtureValidation'
```

## High-velocity commands (read first)

`memory/reference_high_velocity_commands.md` codifies the patterns that
produced fast iteration this session:
- Validator JSON reports as truth source #1.
- `docker logs -t` for cadence detection.
- `docker inspect <c> --format '{{ json .Mounts }}'` for actual deployed
  state.
- Container rebuild + recreate cycle.
- Comparing deployed vs current source via `git show <commit>:<file>`.

## Begin by reading (in this order)

1. `E:\repos\CLAUDE.md` — root operating contract.
2. `E:\repos\Westworld of Warcraft\CLAUDE.md` — repo-specific rules.
3. `C:\Users\lrhod\.claude\projects\e--repos\memory\MEMORY.md` and key entries:
   - `feedback_fgbg_physics_parity_priority.md` ← THE priority rule
   - `reference_high_velocity_commands.md` ← workflow accelerators
   - `project_pfs_overhaul_006_scenedata_eof_diagnosis.md` ← why SceneData is done
   - `project_pfs_overhaul_006_validation_harness_session.md` ← harness architecture
4. The two BakeFixture JSON reports under
   `tmp/test-runtime/screenshots/long-pathing/bake-validation/`.
5. `Exports/Navigation/PhysicsGroundSnap.cpp` and
   `Exports/Navigation/PhysicsEngine.cpp` ground-snap pass.
6. `docs/physics/10_PARITY_TEST_HARNESS.md` for the PhysX CCT checklist.

## Stopping conditions

Stop iterating when EITHER:
- The OG `smooth-wp01-cliff-fall-z42` parity break is resolved AND a
  fresh validator run shows zero `FG_BG_PARITY_BREAK` failures across
  the OG fixture (BG settles at z≈42.29 within 0.3y of FG), OR
- You've localized the snap-up site to a specific PhysicsStepV2 code
  line (with reason: hasPrevGround branch / collision sweep settle /
  deferred depen / etc.) AND documented the next-cycle fix surface in
  memory.

At stopping time, write a session-summary memory entry, commit + push,
and surface any remaining blocker concisely. Then update this
`NEXT_SESSION_HANDOFF.md` for the session after.
