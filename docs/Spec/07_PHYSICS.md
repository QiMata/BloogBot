# Spec 07 — Physics Parity

## Authority

FG (real `WoW.exe`, PhysX CCT-style binary) is ground truth. BG
(`Exports/Navigation/PhysicsEngine.cpp` + `WoWSharpClient.Movement`) must
match FG within tolerance at every representative checkpoint.

When FG and BG disagree:

- BG is wrong.
- Open a parity round (see [`Plan/09_PARALLEL_BRM_BAKE.md`](../Plan/09_PARALLEL_BRM_BAKE.md)).
- Do not "fix" FG to match BG.

## Tolerances

| Signal | Tolerance |
|---|---|
| Settle Z | ± 0.3 y |
| Walk speed (steady-state) | ± 0.1 y/s |
| Strafe speed | ± 0.1 y/s |
| Jump apex | ± 0.05 y |
| Jump duration | ± 0.05 s |
| Fall time over fixed drop | ± 0.05 s |
| Slope-climb threshold | exact match (no tolerance — binary equivalence required) |
| Step-up height | exact match |

## Physics constants (must match FG)

| Constant | Value |
|---|---|
| Gravity | 19.2911 y/s² |
| Jump initial velocity | 7.9555 y/s |
| Terminal velocity | 60.148 y/s |
| Forward run speed | 7.001 y/s |
| Backward run speed | 4.502 y/s |
| Strafe speed | 6.941 y/s |
| Diagonal (fwd+strafe) | 6.983 y/s (normalized; not √2-scaled) |
| Default capsule height | 2.625 y (Tauren Male) |
| Default capsule radius | 1.0247 y (Tauren Male) |
| `walkableSlopeAngle` (default) | 50° |
| `walkableClimb` (default) | 1.4 y |

Capsule dimensions per race/gender are in
`Exports/GameData.Core/Constants/RaceConstants.cs`.

## Walkability authority

`SceneCache::GetWalkableGroundZ(x, y, max_search_h, walkable_min_normal_z)`
is the authority for "is this surface walkable from this position".
The unwalkable variant `GetGroundZ` may return any solid surface,
including cliff faces.

The architecture rule (established 2026-05-10 round-4 iter-5):

1. **Prime is airborne authority.** `PrimeAirborneTeleportFallIfNeeded`
   sets `MOVEFLAG_FALLINGFAR` and the `INVALID` sentinel for
   `_prevGroundZ` when no walkable support is found below the teleport
   destination.
2. **Physics gates respect Prime.** The `prevGroundUnknown && airborneFlag`
   condition is a legal airborne state; depen-snap-up code paths must
   honor it.
3. **C# clamps respect FALLINGFAR.** The MovementController fallthrough-
   prevention clamp at `MovementController.cs:393` must skip when
   `FALLINGFAR` is set.
4. **Ground probes filter walkability.** `ProcessAirMovement` and
   `PrimeAirborneTeleportFallIfNeeded` both call
   `SceneQuery::GetWalkableGroundZ(..., DEFAULT_WALKABLE_MIN_NORMAL_Z)`,
   not the unfiltered variant.

Any new physics or movement code must adhere to all four. CI tests
under `Tests/Navigation.Physics.Tests/` enforce them.

## Validation harness

Live validation runs the full FG/BG parity loop at named checkpoints.
Each checkpoint produces a JSON report:

```json
{
  "checkpoint": "OG_zeppelin_cliff_fall",
  "FG": { "settleZ": 42.32, "settleXY": [1336.5, -4658.1], "duration_ms": 4250 },
  "BG": { "settleZ": 42.09, "settleXY": [1336.4, -4658.0], "duration_ms": 4310 },
  "dz": 0.23,
  "tolerance": 0.3,
  "status": "PASS",
  "kind": null
}
```

`kind` is the failure category from
[`Spec/12_ERROR_TAXONOMY.md`](12_ERROR_TAXONOMY.md). The validator
artifact-writes a JSON report per run; screenshots are developer aids
and not authority.

### Standard checkpoints

| Checkpoint | Map | Notes |
|---|---|---|
| OG_zeppelin_deck | 1 | Tier-1 walkability gate |
| OG_zeppelin_cliff_fall | 1 | Round-4 iter-5 VICTORY case |
| UC_elevator_top | 0 | Elevator state machine |
| UC_elevator_bottom | 0 | Elevator state machine |
| BRM_blackrock_stairs | 0 | BRM south-face bake gap (parallel) |
| WSG_flagroom_alliance | 489 | BG combat physics |
| WSG_flagroom_horde | 489 | BG combat physics |
| MC_lava_run | 409 | Death-recovery scene |
| Stratholme_undead_entry | 329 | Steep ramp + ground snap |
| Stranglethorn_grom_gol | 0 | Cross-map transport |
| Tirisfal_zeppelin | 0 | Reverse transport |

Add a checkpoint when a parity failure is found in the wild. Never
remove a checkpoint.

## Physics regression suite

`Tests/Navigation.Physics.Tests/` has the deterministic regression suite
(67 tests at last count). Every fix lands a new test before merge. The
suite covers:

- Walkable-checkpoint settle Z (12 points)
- Underground-regression guards (no settle below ADT floor)
- Cliff-fall parity (`OgZeppelinCliffFallParityTests`)
- Slope climb / step-up
- Jump arcs (standing, running, falling)
- Swim entry/exit
- Knockback timing
- Teleport state transitions
- Replan-from-stall validity

## Existing code anchors

| Concept | File |
|---|---|
| C++ physics | `Exports/Navigation/PhysicsEngine.cpp` |
| C++ scene cache | `Exports/Navigation/SceneCache.cpp` |
| C# movement controller | `Exports/WoWSharpClient/Movement/MovementController.cs` |
| FG physics (binary) | `WoW.exe` — see [`docs/physics/`](../physics/) disasm |
| Parity validator | `Tests/Navigation.Physics.Tests/` |
| Physics replay | `Tests/Navigation.Physics.Tests/PhysicsReplayTests.cs` |
| Validation harness | `Tests/BotRunner.Tests/LiveValidation/` |
