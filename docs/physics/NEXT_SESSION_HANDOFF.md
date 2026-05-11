# Next-session handoff — FG/BG physics parity (PFS-OVERHAUL-006)

> Working dir: `E:/repos/Westworld of Warcraft/`
> Branch: `main` at HEAD (pushed). Working tree clean except `.env`.

You are continuing PFS-OVERHAUL-006. The OG cliff-fall parity-break
fix from commit `1c530288` (round 1) was **live-verified as PARTIAL**.
A round-2 attempt to gate `ApplyVerticalDepenetration` on
`inputAirborneFlag` had ZERO live effect and was reverted. Round 3
must localize the actual snap-up site somewhere in `PhysicsStepV2`.

> 2026-05-10 status (post-live-verify): the OG `smooth-wp01-cliff-fall-z42`
> parity break has CHANGED but persists. Pre-1c530288: BG settled at
> z=51.62 (the cliff fillet). Post-1c530288 (round 1): BG settles at
> **z=53.32** (the deck above teleport). FG=42.29. dz=11.00y. The
> round-1 fix is necessary (cleanly rejects the cliff fillet) but
> insufficient (some other PhysicsStepV2 mechanism still snaps UP to
> the overhead deck). See
> `memory/project_pfs_overhaul_006_depen_overhead_fix.md` for the
> round-2-attempt-and-revert details + the recommended round-3 plan.

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
| (this session) | Live-verified `1c530288` is PARTIAL. Round-2 attempt to gate `ApplyVerticalDepenetration` on `inputAirborneFlag` had no live effect (BG still 53.32). Reverted. No code shipped. Documentation only — see `memory/project_pfs_overhaul_006_depen_overhead_fix.md`. |

The harness's six acceptance items from the prior mandate are all
green or diagnostically delivered.

## The frontier — your mandate

### Acceptance items

1. **Localize the snap-up site in PhysicsStepV2 (round 3 of cliff-fall fix).**

   Live-verify of commit `1c530288` (round 1) shows BG settle moved
   51.62→**53.32** instead of falling to 42.29. The round-1 fix
   correctly rejects the cliff fillet at z=51.62, but the bot now
   snaps UP to the deck at z=53.5 via a SECOND mechanism inside
   `Exports/Navigation/PhysicsEngine.cpp::PhysicsStepV2`.

   Latest validator JSON (canonical source of truth):
   `tmp/test-runtime/screenshots/long-pathing/bake-validation/og-zeppelin/bake-validation-ClimbOrgrimmarTowerToFrezza-20260511T002911Z.json`
   - 11/11 walkable checkpoints PASS (no regression).
   - `smooth-wp01-cliff-fall-z42`: FG=42.29, BG=53.32, dz=11.03y, FAILED.

   BG live trace (Bot/Release/net8.0/WWoWLogs/bg_LPATHBG120260510.log):
   - 20:29:04: teleport to (1337.3,-4645.1,51.7) (drop from boarding-pos).
   - 20:29:04.529: "Post-teleport ground snap complete: pos=(1337.3,-4645.1,**53.3**)
     groundZ=53.317 moveFlags=0x0 envFlags=0x0 indoors=false **frames=1**".
   - Bot moved UP 1.617y in ONE physics frame. No
     "Nearby teleport support probe corrected" log line, so
     `TrySnapToNearbyTeleportSupport` did NOT fire.

   **Round-2 attempt failed**: gating `ApplyVerticalDepenetration`
   on `inputAirborneFlag` (PhysicsEngine.cpp:5604) had ZERO live
   effect — BG still settled at 53.32. The depen loop is NOT the
   snap mechanism. Reverted. See
   `memory/project_pfs_overhaul_006_depen_overhead_fix.md` for full
   details.

   **Recommended round-3 diagnostic**:
   1. Add temporary per-Z-mutation logging at every `st.z = ...`
      site in `Exports/Navigation/PhysicsEngine.cpp::PhysicsStepV2`
      (search for `st.z =` between lines ~5400 and ~6100).
   2. Trigger the cliff-fall via the live validator:
      ```powershell
      $env:WWOW_OG_ZEP_BAKE_FIXTURE = '1'
      $env:WWOW_DATA_DIR = 'D:\MaNGOS\data'
      dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj `
        --configuration Release --no-build `
        --filter 'FullyQualifiedName~OgZeppelin_BakeFixtureValidation'
      ```
   3. Read `Bot/Release/net8.0/WWoWLogs/bg_LPATHBG{date}.log`
      around the cliff-fall timestamp. The first log line where
      st.z jumps from 51.7 → ~53.3 IS the snap-up site.
   4. Likely candidates per memory entry:
      - PhysicsStepV2 `hasPrevGround` branch at ~line 5454.
      - Deferred depen vector at line 5635-5667 (Side region only,
        but worth verifying).
      - PhysicsThreePass collision sweep (initial overlap settle).
   5. Once localized, the fix shape is: when FALLINGFAR is set AND
      the candidate ground surface is ABOVE the bot's feet by more
      than `walkableClimb`, suppress the snap. Real WoW falls past
      overhead WMO/M2 floors; BG must too.

   **Don't redo**: the round-1 SceneCache walkable filter (commit
   `1c530288`) IS correct and IS load-bearing. Its unit tests prove
   the post-teleport probes correctly prime FALLINGFAR with
   `_prevGroundZ=42.29`. The downstream snap-up is a separate bug.

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
