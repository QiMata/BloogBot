# Next-session handoff — FG/BG physics parity (PFS-OVERHAUL-006)

> Working dir: `E:/repos/Westworld of Warcraft/`
> Branch: `main` at `e56f3ad7` (pushed). Working tree clean except `.env`.

You are continuing PFS-OVERHAUL-006. The validation harness is fully
operational live; SceneDataService is verified working (BG fetches
real ADT triangles for the OG zep tile). The FOUNDATIONAL frontier
is now **FG/BG physics parity**, NOT pathfinding.

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
| `e56f3ad7` | **THIS SESSION**: added missing `scenes/` mount to `docker-compose.vmangos-linux.yml`; rebuilt `wwow-scene-data` container so its `ProtobufSocketServer.TryReadExact(allowCleanEndOfStream:true)` silences benign healthcheck disconnects |

The harness's six acceptance items from the prior mandate are all
green or diagnostically delivered.

## The frontier — your mandate

### Acceptance items

1. **Diagnose the OG `smooth-wp01-cliff-fall-z42` BG/FG parity break.**
   - FG settles at `(1337.35, -4645.09, 42.29)` — falls 9y to real ADT
     ground.
   - BG settles at `(1337.30, -4645.10, 51.62)` — stays on the deck
     triangle the navmesh contains.
   - Both clients see the same triangles (SceneData is delivering).
   - Diverge in how they interpret deck-edge collision.
   - Canonical fix surface: `Exports/Navigation/PhysicsGroundSnap.cpp`
     (line ~285 area is where capsule-vs-edge logic lives) and
     `Exports/Navigation/PhysicsEngine.cpp` ground-snap pass.
   - Reference report:
     `tmp/test-runtime/screenshots/long-pathing/bake-validation/og-zeppelin/bake-validation-ClimbOrgrimmarTowerToFrezza-20260510T191634Z.json`.
   - Frame-replay tooling: `PhysicsReplayTrace` / packet-trace recorder
     (see `Services/BackgroundBotRunner/Diagnostics/BackgroundPacketTraceRecorder.cs`).
   - Multi-angle screenshots already captured (4 yaws per checkpoint)
     for visual inspection: `screenshots/smooth-wp01-cliff-fall-z42-LPATHFG1-yaw*-*.png`.

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
  the OG fixture, OR
- You've localized the divergence to a specific code site
  (PhysicsGroundSnap line + reason: capsule-vs-edge / step-up / slope
  threshold / depenetration order) AND documented the next-cycle fix
  surface in memory.

At stopping time, write a session-summary memory entry, commit + push,
and surface any remaining blocker concisely. Then update this
`NEXT_SESSION_HANDOFF.md` for the session after.
