# Navigation Tasks

## Scope
- Project: `Exports/Navigation`
- Owns native pathfinding, collision queries, and physics integration consumed by pathfinding/physics services and tests.
- This file tracks first-party implementation gaps only (exclude third-party vendor TODOs under `Detour/` and `g3dlite/`).
- Master tracker: `MASTER-SUB-007`.

## Execution Rules
1. Work only the top unchecked task unless blocked.
2. Keep scans scoped to `Exports/Navigation` and related direct test projects only.
3. Keep commands simple and one-line.
4. Record `Last delta` and `Next command` in `Session Handoff` every pass.
5. Move completed tasks to `Exports/Navigation/TASKS_ARCHIVE.md` in the same session.
6. Loop-break guard: if two consecutive passes produce no file delta, log blocker + exact next command and move to the next queue file.
7. `Session Handoff` must include `Pass result` (`delta shipped` or `blocked`) and exactly one executable `Next command`.

## Environment Checklist
- [x] Navigation native build succeeds (`Release|x64`) - confirmed 2026-03-12 via MSBuild (VS 2025 Community).
- [x] Pathfinding runtime has access to expected MMAP/VMAP assets when validating corpse-run behavior.
- [x] Native/exported API contracts are synchronized with downstream C#/protobuf consumers.

## Evidence Snapshot (2026-02-28)
- `OverlapCapsule` export implemented - routes to `SceneQuery::OverlapCapsule` via `VMapManager2/StaticMapTree`.
- `backfaceCulling` / `returnPhysMat` in `QueryParams` are marked "Reserved" with explicit behavior docs.
- `PathFinder` machine-specific debug path fixed (batch 3).
- Native build: `& "C:/Program Files/Microsoft Visual Studio/18/Community/MSBuild/Current/Bin/MSBuild.exe" Exports/Navigation/Navigation.vcxproj -p:Configuration=Release -p:Platform=x64 -p:PlatformToolset=v145 -v:minimal` -> 0 errors.
- Physics tests: 76/79 pass (3 pre-existing calibration failures).

## P0 Active Tasks (Ordered)

### NAV-PAR-001 PhysicsEngine parity with original WoW.exe grounded movement
- [x] Session 199: `SceneQuery::EnsureMapLoaded(...)` now upgrades legacy metadata-less `.scene` caches instead of treating them as the steady-state runtime path. It rebuilds the same cached bounds through `SceneCache::Extract(...)`, writes back a v2 cache, and loads the metadata-bearing result, which makes the normal production autoload path return the same frame-16 WMO-group blocker identity (`rootId=1150`, `groupId=3228`, `groupFlags=0x0000AA05`, `selectedMetadataSource=2`) that the fresh-extract proof already showed.
- [x] Session 198: `SceneCache` now preserves per-triangle WMO-group metadata on fresh extracts and through the deterministic `.scene` round-trip path. The packet-backed Undercity frame-16 blocker still selects instance `0x00003B34`, but a fresh bounded extract now proves that selected triangle is `rootId=1150`, `groupId=3228`, `groupFlags=0x0000AA05`, and `selectedMetadataSource=2` after unload/reload. Practical implication: no more MPQ extraction is needed for this blocker; the next runtime fix is getting the normal scene-load path onto the same metadata-bearing cache data.
- [x] Session 197: extended the selected-contact trace with `selectedResolvedModelFlags` and `selectedMetadataSource`, plus a best-effort child doodad match against the parent WMO's default `.doodads` set. The packet-backed Undercity frame-16 blocker still resolves as metadata source `1` (`parent instance`) with `resolvedModelFlags = 0x00000004`, which means post-hoc lookup on the collapsed contact is not enough; the next native fix has to preserve child WMO/M2 metadata earlier in `SceneCache` / `TestTerrainAABB`.
- [x] Session 196: extended the production-DLL grounded-wall trace seam to resolve selected-contact static metadata, which answered the open “more MPQ extraction?” question with binary-backed evidence. The packet-backed Undercity frame-16 blocker still resolves only to parent WMO instance `0x00003B34` with `instance/model flags = 0x00000004` and `rootWmoId = 1150`, while no WMO group match is found for the exact contact triangle. Practical implication: the current `SceneCache` / `TestTerrainAABB` path is preserving geometry but collapsing the deeper child WMO/M2 identity that `0x5FA550` appears to walk, so the next native parity unit is metadata preservation, not more raw triangle extraction.
- [x] Session 192: added a deterministic frame-15 Undercity upper-door contact probe around the production `Navigation.dll`. `QueryTerrainAABBContacts(...)` now exposes the merged `TestTerrainAABB` contact set, and the new tests prove the elevator deck support face is present at the failing frame with a signed downward normal and raw `walkable=0`. They also prove `0x6334A0` only promotes that face on its stateful path, which means the missing runtime piece is the binary selected-contact / `0xC4E544` state path, not a blanket `contact.walkable -> CheckWalkable` replacement.
- [x] Session 191: captured `0x6721B0` / `0x637330` in `docs/physics/0x6721B0_disasm.txt` and aligned `TestTerrainAABB` to emit signed box-relative contact normals instead of upward-flattened ones. The pure `0x6334A0` helper now consumes that signed contact feed, new deterministic orientation tests pin the behavior, and the focused grounded/runtime slices plus both live Durotar parity routes stayed green.
- [x] Session 190: disassembled `0x6334A0` `CheckWalkable`, captured its helper semantics in `docs/physics/0x6334A0_disasm.txt`, and added raw contact triangle/plane data plus a pure `WoWCollision::CheckWalkable(...)` helper with deterministic tests. A direct runtime hookup regressed the live Durotar parity routes and was reverted, so the shipped delta stops at binary evidence, test seams, and deterministic coverage until `TestTerrain` contact orientation / `0x637330` parity is fixed.
- [x] Session 189: top-level `0x633840` branch precedence documented and enforced in `StepV2`. Airborne now wins over swim when both states overlap, matching the binary's `0x633A29` -> `0x633B5E` order.
- [x] Session 188: Disassembled `0x6367B0` and implemented binary-backed retry loop (up to 5 iterations, exit < 1.0f yard). Also documented `0x636100` return codes and `0x636610` merge logic.
- [x] Session 188: Remaining heuristic thresholds audited against binary. `0x636610` uses integer jump-table; our float approximations match.
- [x] Build verified real wall regressions on terrain, WMO, and dynamic-object geometry.
- [x] All 30 `MovementControllerPhysics` + aggregate drift gate + wall replay fixtures green after retry loop implementation.

### NAV-MISS-001 Implement `OverlapCapsule` test export by routing to existing `SceneQuery` implementation
- [x] Done (batch 12). Implemented `OverlapCapsule` export in `PhysicsTestExports.cpp`:
  - gets `VMapManager2` via `VMapFactory::createOrGetVMapManager()`
  - ensures the map is loaded via `SceneQuery::EnsureMapLoaded()`
  - gets `StaticMapTree` via `vmapMgr->GetStaticMapTree(mapId)`
  - calls `SceneQuery::OverlapCapsule(*mapTree, *capsule, hitResults)`
  - copies results to the output buffer up to `maxOverlaps`
- [x] Validation: C++ MSBuild -> 0 errors. Physics tests: 76/79 pass.
- [x] Acceptance: `OverlapCapsule` no longer returns stubbed zero; it routes to real scene-query collision geometry.

### NAV-MISS-002 Resolve explicit query-contract drift in `QueryParams` (`returnPhysMat`, `backfaceCulling`)
- [x] Done (batch 12). Updated `SceneQuery.h`:
  - `backfaceCulling`: now documented as reserved back-face hit filtering
  - `returnPhysMat`: now documented as reserved physical-material retrieval
- [x] Acceptance: no ambiguous TODO/future comments remain; callers see a deterministic contract.

### NAV-MISS-003 Remove machine-specific fallback/debug side effects from `PathFinder`
- [x] Done (batch 3). Replaced hardcoded `C:\Users\Drew\...` path with printf; filter initialization made explicit.
- [x] Acceptance: no machine-specific debug artifact paths remain; filter behavior is explicit and reproducible across environments.

### NAV-MISS-004 Validate corpse runback path use (consume returned path nodes without wall-loop fallback)
- [x] Code-complete. `RetrieveCorpseTask` already consumes the path directly with probe-skip/direct-fallback disabled (`enableProbeHeuristics: false`, `enableDynamicProbeSkipping: false`, `strictPathValidation: true`, `allowDirectFallback: false`). `PathFinder` generates valid Detour paths. No wall-loop fallback exists in this code path.
- [x] Live validation passed (session 188): `DeathCorpseRunTests.Death_ReleaseAndRetrieve_ResurrectsBackgroundPlayer` green with navtrace ownership assertion.

### NAV-FISH-001 Fix Ratchet shoreline terrain sticking / no-LOS approach points
- [ ] Problem: the fishing live test now reaches the correct Ratchet hole from a named teleport, but the bot can still snag on shoreline terrain or end at a cast target with no clean LOS to fishable water before `FishingTask in_cast_range`.
- [ ] Target files: `Exports/Navigation/PhysicsCollideSlide.cpp`, `Exports/Navigation/PhysicsEngine.cpp`, `Exports/Navigation/PathFinder.cpp`, replay/log evidence from `FishingProfessionTests`.
- [x] Progress (2026-03-12 session 72): `PathFinder.cpp` now tries grounded lateral detour candidates before falling back to pure midpoint splitting, and deterministic native coverage now includes a Ratchet dock fishing-approach route (`-957.0,-3755.0,5.0 -> -956.2,-3775.0,0.0`) plus an obstructed direct-segment detour regression. This improves returned route shape, but it does not yet log planned-vs-executed shoreline drift or prove final cast LOS in the live task.
- [ ] Required change:
  1. Reproduce the short Ratchet shoreline approach with planned-vs-executed waypoint evidence from the pathfinding owners.
  2. Fix corridor/collide-slide behavior so the returned short route does not strand the bot on terrain or at a no-LOS endpoint.
  3. Validate against the fishing-hole approach first, then reuse the same diagnostics for other sporadic live pathing failures.
- [ ] Acceptance criteria: the short Ratchet shoreline route consistently reaches a castable, LOS-valid position without terrain sticking or hover/fall artifacts.

### NAV-OBJ-001 Integrate request-scoped dynamic objects into native path validation
- [ ] Problem: `DynamicObjectRegistry` exists and is already used by physics/LOS, but native path generation and path-validation flows do not yet treat caller-supplied live objects as first-class blockers during route shaping.
- [ ] Target files: `Exports/Navigation/Navigation.cpp`, `Exports/Navigation/PathFinder.cpp`, `Exports/Navigation/SceneQuery.cpp`, `Exports/Navigation/DynamicObjectRegistry.*`.
- [ ] Required change:
  1. Accept request-scoped dynamic-object overlays from the service layer.
  2. Use those overlays during segment validation and candidate-route rejection.
  3. Keep the overlay lifecycle deterministic so two bots cannot pollute each other's native obstacle state.
- [ ] Acceptance criteria: native route validation can say "this mmap segment is blocked by live object X" instead of pretending the object is not there.

### NAV-OBJ-002 Add capsule-clearance and support-surface validation for candidate segments
- [ ] Problem: LOS alone is insufficient for walkability. We need to know whether the character capsule can clear the segment and whether the destination/support surface is actually usable.
- [ ] Target files: `Exports/Navigation/SceneQuery.cpp`, `Exports/Navigation/PhysicsEngine.cpp`, `Exports/Navigation/PhysicsCollideSlide.cpp`, native exports as needed.
- [x] Progress (2026-03-12 session 68): added native `ValidateWalkableSegment` in `DllMain.cpp`. It uses `HorizontalSweepAdvance`, support-surface checks, and overlap rejection to classify `Clear`, `BlockedGeometry`, `MissingSupport`, `StepUpTooHigh`, and `StepDownTooFar`. Focused native tests now cover the export directly.
- [x] Progress (2026-03-12 session 69): `SceneQuery.cpp` now exposes capsule-footprint support selection through `GetCapsuleSupportZ(...)`, `ValidateWalkableSegment` uses that probe plus looser overlap tolerance, and short false-negative straight sweeps can fall back to `PhysicsStepV2` so the validator matches real collide-and-slide movement better. The first real Orgrimmar graveyard->center raw-path segment now passes in deterministic native coverage.
- [ ] Required change:
  1. Add reusable segment validation helpers for capsule clearance, support surface, and obstacle squeeze cases.
  2. Use the same walkability thresholds as the physics engine (`STEP_HEIGHT`, slope, step-down limits).
  3. Expose enough native results for the service layer to classify the segment.
- [ ] Acceptance criteria: the native layer can distinguish "visible" from "walkable with this capsule."

### NAV-OBJ-003 Surface-transition affordance classification
- [ ] Problem: the engine has step/jump/fall substrate, but route planning does not yet tag transitions as step-up, jump-gap, safe-drop, unsafe-drop, or blocked.
- [ ] Target files: `Exports/Navigation/PhysicsEngine.cpp`, `Exports/Navigation/SceneQuery.cpp`, helper exports/tests.
- [ ] Required change:
  1. Reuse existing jump/fall/gap detection to classify candidate transitions.
  2. Emit quantitative metrics for climb height, gap distance, and drop height.
  3. Keep the classification consistent with actual movement execution, not a separate planner-only heuristic.
- [ ] Acceptance criteria: higher layers can ask the native stack what movement affordance a segment requires.

### NAV-OBJ-004 Local detour generation around collidable objects
- [ ] Problem: once a live object blocks an mmap path segment, we need a local workaround instead of failing the whole route immediately.
- [ ] Target files: `Exports/Navigation/PathFinder.cpp`, `Exports/Navigation/Navigation.cpp`, `Exports/Navigation/SceneQuery.cpp`.
- [x] Progress (2026-03-12 session 72): `PathFinder.cpp` now attempts grounded lateral detour candidates around blocked segments before falling back to midpoint refinement. Focused native coverage proves both the Ratchet shoreline fishing approach and a known obstructed direct segment now return multi-point walkable routes instead of trusting the blocked straight segment.
- [ ] Required change:
  1. Generate short detour candidates around dynamic blockers using clearance-aware probes.
  2. Reject detours that only pass LOS but fail capsule/support checks.
  3. Return the best repaired route for service-side smoothing/re-optimization.
- [ ] Acceptance criteria: temporary blockers produce a valid workaround route whenever one exists within a local search envelope.

## Simple Command Set
1. `& "C:/Program Files/Microsoft Visual Studio/18/Community/MSBuild/Current/Bin/MSBuild.exe" Exports/Navigation/Navigation.vcxproj -p:Configuration=Release -p:Platform=x64 -p:PlatformToolset=v145 -v:minimal`
2. `dotnet test Tests/PathfindingService.Tests/PathfindingService.Tests.csproj --configuration Release --no-restore --settings Tests/PathfindingService.Tests/test.runsettings --logger "console;verbosity=minimal"`
3. `dotnet test Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --no-restore --settings Tests/Navigation.Physics.Tests/test.runsettings --logger "console;verbosity=minimal"`
4. `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore --filter "FullyQualifiedName~DeathCorpseRunTests" --blame-hang --blame-hang-timeout 10m --logger "console;verbosity=minimal"`
5. `rg --line-number "TODO|FIXME|NotImplemented|not implemented|stub" Exports/Navigation`

## Session Handoff
- Last updated: 2026-03-26 (session 229)
- Active task: `NAV-PAR-001` keep replacing non-binary-backed grounded query/slide heuristics until `CollisionStepWoW` matches the client’s merged-query plus post-`TestTerrain` wall/corner sequence
- Last delta:
  - Session 229 still kept runtime grounded behavior unchanged and pinned the private `0x634DA0` chooser that sits inside the `count == 2` body of `0x634AE0`. `BuildSelectorTriangleEdgeDirection(...)` now mirrors the binary three-edge loop over the selected `0x34` record, including zero-length rejection, the aligned-edge `0x634F40` point-to-line scorer, and the alternate squared plane-offset scorer.
  - New deterministic coverage in `Tests/Navigation.Physics.Tests/WowSelectorTriangleEdgeDirectionTests.cs` now pins mixed fast-path/cross-path scoring, zero-length edge rejection, and the all-zero-length default output through the production DLL.
  - New raw capture now lives in `docs/physics/0x634DA0_disasm.txt`. Practical implication: the remaining alternate-pair gap is no longer any hidden helper inside `0x634AE0`. The next missing runtime piece is the full two-candidate working-vector output and caller-side normalization path in `0x634AE0` / `0x635090`.
  - Session 228 still kept runtime grounded behavior unchanged and pinned the private `0x634FC0` three-plane intersection helper that sits inside the unresolved `count == 2` body of `0x634AE0`. `BuildSelectorPlaneIntersectionPoint(...)` now mirrors the binary solve over the selected plane plus the two candidate planes.
  - New deterministic coverage in `Tests/Navigation.Physics.Tests/WowSelectorPlaneIntersectionPointTests.cs` now pins the exact intersection result for both orthogonal and scaled-coefficient plane triplets through the production DLL.
  - New raw capture now lives in `docs/physics/0x634FC0_disasm.txt`. Practical implication: the remaining alternate-pair gap is no longer another hidden matrix/inversion helper. The next missing runtime piece is the chooser logic in `0x634DA0`.
  - Session 227 still kept runtime grounded behavior unchanged and pinned the private `0x634960` footprint-vs-plane gate that sits inside the unresolved `count == 2` body of `0x634AE0`. `EvaluateSelectorPlaneFootprintMismatch(...)` now mirrors the binary five-point sample ring around `this->position`, including the sample-height constant `0x80C740 = 1.8493989706` and the `1/720` plane-distance epsilon check against the selected plane.
  - New deterministic coverage in `Tests/Navigation.Physics.Tests/WowSelectorPlaneFootprintMismatchTests.cs` now pins the horizontal accept, below-epsilon accept, above-epsilon reject, and vertical-plane reject paths through the production DLL.
  - New raw capture now lives in `docs/physics/0x634960_disasm.txt`. Practical implication: the remaining alternate-pair gap is no longer the front-end gate, the obvious count split, or the footprint plane mismatch check. The next missing runtime piece is the actual line/intersection math inside `0x634FC0` and `0x634DA0`.
  - Session 226 still kept runtime grounded behavior unchanged and pinned the visible `0x6336A0 -> 0x634AE0` front-end on the alternate `0x635090` pair path. `IsSelectorContactWithinAlternateWorkingVectorBand(...)` now mirrors the tiny `0x6336A0` selected-contact `normal.z` gate, and `EvaluateSelectorAlternateWorkingVectorMode(...)` now mirrors the visible `0x634AE0` branch fanout: `count <= 1` or `count > 4` => negate `candidateBuffer[0].normal`, `count == 2` => enter the unresolved two-plane builder body, `count == 3/4` => return the selected `0xC4E534[index].normal`.
  - New deterministic coverage in `Tests/Navigation.Physics.Tests/WowSelectorAlternateWorkingVectorModeTests.cs` now pins that exact slope-band + count-fanout contract through the production DLL.
  - New raw captures now live in `docs/physics/0x6336A0_disasm.txt` and `docs/physics/0x634AE0_disasm.txt`. Practical implication: the remaining alternate-pair gap is no longer the front-end gate or the obvious count split. The next missing runtime piece is the `count == 2` body inside `0x634AE0` plus its private helpers `0x634960`, `0x634FC0`, and `0x634DA0`.
  - Session 225 still kept runtime grounded behavior unchanged and pinned the visible `0x7C5F50` + `0x635450` post-selection transaction. `ComputeVerticalTravelTimeScalar(...)` now mirrors the binary gravity/terminal-velocity time solver, including the `MOVEFLAG_SAFE_FALL` terminal-velocity split, positive-speed clamp, stationary sqrt branch, terminal-velocity fallback, and earlier-positive-root toggle. `EvaluateSelectorPairWindowAdjustment(...)` now mirrors the visible `0x635450` caller contract: call `0x635550`, compute the optional scaled horizontal window, call `0x7C5F50`, zero the move vector when the vertical window is already elapsed, clamp to `windowSpanScalar`, and only scale `move.x/y` when the remaining window is strictly shorter than that scaled horizontal window.
  - New deterministic coverage in `Tests/Navigation.Physics.Tests/WowVerticalTravelTimeTests.cs` and `Tests/Navigation.Physics.Tests/WowSelectorPairWindowAdjustmentTests.cs` now pins those exact binary-backed behaviors through the production DLL.
  - New raw captures now live in `docs/physics/0x635450_disasm.txt` and `docs/physics/0x7C5F50_disasm.txt`. Practical implication: the open selector gap is no longer the visible caller-side post-selection scaler. The next missing runtime piece is exposing the selected index plus paired `0xC4E544[index]` payload on the production grounded path so the real state transaction can be fed into grounded wall resolution instead of rebuilding it from merged contacts.
  - Session 224 still kept runtime grounded behavior unchanged and pinned the visible `0x635550` follow-up gate that `0x635450` calls immediately after `0x6351A0`. `ComputeJumpTimeScalar(...)` now mirrors the binary `0x7C5DA0` helper exactly (`MOVEFLAG_JUMPING` => `verticalSpeed * -1/gravity`, else `0`), and `EvaluateSelectorPairFollowupGate(...)` now mirrors the visible `0x635550` gate: alternate-state short-circuit, reject when `this->+0xA0 >= 0`, early success when the window ends before the jump-time scalar, reject when the window starts after it, and the final strict horizontal-length-squared comparison against `this->+0x84`.
  - New deterministic coverage in `Tests/Navigation.Physics.Tests/WowSelectorPairFollowupGateTests.cs` now pins the jump-time helper plus the full visible `0x635550` branch contract through the production DLL.
  - New raw capture now lives in `docs/physics/0x635550_disasm.txt`. Practical implication: the open selector gap is no longer the visible post-`0x6351A0` follow-up gate. The next missing runtime piece is `0x635450` itself: how it combines the two `0x6351A0` out-state dwords, the `0x635550` result, and the `0x7C5F50` scalar before grounded resolution consumes the selected payload.
  - Session 223 still kept runtime grounded behavior unchanged and pinned the visible `0x6351A0` consumer tail. `EvaluateSelectorAlternateUnitZFallbackGate(...)` now mirrors the binary `0x7C5DA0` / `this+0x84` gate on the alternate unit-Z branch, and `EvaluateSelectorPairConsumer(...)` now mirrors the visible `0x6351A0` return contract: zero-distance early return, `0x632BA0` failure returning `2` with a zeroed move vector, direct-pair return, zero-pair direct success, zero-pair unit-Z success, and alternate-pair fallback.
  - New deterministic coverage in `Tests/Navigation.Physics.Tests/WowSelectorPairConsumerTests.cs` now pins those exact consumer-tail behaviors through the production DLL alongside the existing selector z-match and direction-ranking seams.
  - New raw capture now lives in `docs/physics/0x635734_callsite_disasm.txt`, which closes one important caller-side detail: `0x6351A0` writes two separate out-state dwords, not one.
  - Practical implication: the open selector gap is no longer the visible `0x6351A0` tail. The next missing runtime piece is exposing the real selected index plus paired `0xC4E544[index]` payload to grounded resolution instead of reconstructing a blocker from merged contacts.
  - Session 222 still kept runtime grounded behavior unchanged and pinned the outer `0x63214C` record-buffer loop. `TransformSelectorCandidateRecordBufferToTransportLocal(...)` now mirrors the visible binary gates: no-op when `transportGuidLow | transportGuidHigh == 0`, no-op when `recordCount == 0`, otherwise rewrite every `0x34`-byte record in-place.
  - New deterministic coverage in `Tests/Navigation.Physics.Tests/WowTransportLocalTransformTests.cs` now pins both loop-visible behaviors through the production DLL: zero-guid no-op and nonzero-guid full-buffer rewrite.
  - Practical implication: the remaining `0x631E70` gap is no longer the transport-local loop/gate. The next unresolved piece is the selector transaction that consumes the rewritten buffer.
  - Session 221 still kept runtime grounded behavior unchanged and pinned the exact `0x63214C` record rewrite body on the `0x631E70` path. `TransformSelectorCandidateRecordToTransportLocal(...)` now mirrors the binary `0x34`-byte rewrite: inverse-transform points at `+0x10/+0x1C/+0x28`, rotate the plane normal at `+0x00/+0x04/+0x08`, and recompute `planeD` at `+0x0C`.
  - New deterministic coverage in `Tests/Navigation.Physics.Tests/WowTransportLocalTransformTests.cs` now pins the full record transform through the production DLL instead of only the lower-level point/plane helpers.
  - New raw capture now lives in `docs/physics/0x63214C_disasm.txt`. Practical implication: the remaining `0x631E70` gap is no longer the record contents; it is the surrounding count/guid gate and later selector consumption.
  - Session 220 still kept runtime grounded behavior unchanged and pinned the transport-local point/vector/plane math that sits inside the `0x63214C..0x632270` rewrite loop on the `0x631E70` path. `TransformWorldPointToTransportLocal(...)`, `TransformWorldVectorToTransportLocal(...)`, and `BuildTransportLocalPlane(...)` now mirror the inverse-yaw translation, inverse-yaw vector rotation, and local-plane rebuild used after the binary inverse-frame setup.
  - New deterministic coverage in `Tests/Navigation.Physics.Tests/WowTransportLocalTransformTests.cs` now pins both exact binary-backed behaviors: world point -> transport-local point and world plane -> transport-local plane.
  - New raw captures now live in `docs/physics/0x7BD700_disasm.txt` and `docs/physics/0x7BCC60_disasm.txt`. Practical implication: the remaining `0x631E70` gap is no longer the transform math itself; it is the exact per-contact copy/rewrite loop and later selector consumption.
  - Session 219 still kept runtime grounded behavior unchanged and pinned the swim-side plane flip that sits on the `0x631E70` branch after the `0x30000` query. `NegatePlane(...)` now mirrors the `0x637330` + `0x597AD0` pair: negate the contact normal and negate the plane distance before the rewritten plane is copied back into the contact buffer.
  - New deterministic coverage in `Tests/Navigation.Physics.Tests/WowSwimQueryPlaneFlipTests.cs` now pins both exact binary-backed behaviors: one flip negates the plane, and a second flip returns to the original plane.
  - New raw capture now lives in `docs/physics/0x597AD0_disasm.txt`. Practical implication: the remaining `0x631E70` gap is no longer the swim-side plane rewrite itself; it is the surrounding transport-local contact transform loop and later selector consumption.
  - Session 218 still kept runtime grounded behavior unchanged and pinned the higher-level `0x631E70` cache-miss bounds transaction. `BuildTerrainQueryCacheMissBounds(...)` now mirrors the binary sequence that builds projected query bounds, expands min/max by `0x3E2AAAAB`, and merges that expanded box against cached `0xC4E5A0` through `0x6373B0`.
  - New deterministic coverage in `Tests/Navigation.Physics.Tests/WowTerrainQueryCacheMissBoundsTests.cs` now pins both exact binary-backed behaviors: pure `1/6` expansion on the projected query box and preservation of farther cached bounds after the merge.
  - Practical implication: the remaining `0x631E70` gap is no longer the merged cache-miss bounds handoff. The next unresolved piece is the optional swim-side `0x30000` query / `0x637330` contact-flip work that runs after the main `0x6721B0` query succeeds.
  - Session 217 still kept runtime grounded behavior unchanged and pinned the `0x6372D0` / `0x637300` scalar-offset helpers that sit on the `0x631E70` cache-miss path. `AddScalarToVector3(...)` now mirrors the add-to-all-components helper, and `SubtractScalarFromVector3(...)` mirrors the subtract-from-all-components helper.
  - New deterministic coverage in `Tests/Navigation.Physics.Tests/WowVectorScalarOffsetTests.cs` now pins both exact binary-backed behaviors.
  - New raw captures now live in `docs/physics/0x6372D0_disasm.txt`, `docs/physics/0x637300_disasm.txt`, and `docs/physics/0x61E9C0_disasm.txt`. Practical implication: the remaining `0x631E70` gap is now the higher-level merged transaction around those helpers, not the helpers themselves.
  - Session 216 still kept runtime grounded behavior unchanged and pinned the fixed seed payload that `0x632A30` passes into `0x632280`. `InitializeSelectorTriangleSourceWrapperSeeds(...)` now mirrors the binary `(0,0,-1)` test point, `(0,0,-1)` candidate direction, and initial best ratio `1.0f`.
  - New deterministic coverage in `Tests/Navigation.Physics.Tests/WowSelectorSourceWrapperSeedTests.cs` now pins that exact fixed payload through the production DLL.
  - Practical implication: the remaining `0x632A30` / `0x632280` gap is no longer the fixed wrapper seed state; it is the variable payload around the selected-index seed, `0x631BE0` outputs, and the optional `0x631E70` transaction.
  - Session 215 still kept runtime grounded behavior unchanged and pinned the explicit visible gates on the `0x632A30` wrapper plus the shared `0x6376A0` selector-plane initializer. `InitializeSelectorSupportPlane(...)`, `ClampSelectorReportedBestRatio(...)`, and `FinalizeSelectorTriangleSourceWrapper(...)` now mirror the binary `(0,0,1,0)` init, the `0x80DFEC` zero clamp, and the no-override `0x631E70` early-fail path.
  - New deterministic coverage in `Tests/Navigation.Physics.Tests/WowSelectorSourceWrapperTests.cs` now pins five exact binary-backed behaviors: unit-Z default plane init, clamp at/below `0x80DFEC`, no-override early failure with zeroed scalar, override bypass, and success-path zero clamp.
  - New raw captures now live in `docs/physics/0x632A30_disasm.txt` and `docs/physics/0x6376A0_disasm.txt`. Practical implication: the remaining `0x632A30` / `0x631E70` gap is no longer the wrapper-visible gate behavior; it is the full data transaction feeding `0x632280`.
  - Session 214 still kept runtime grounded behavior unchanged and pinned the exact `0x6373B0` merged-AABB helper from fresh binary evidence. `MergeAabbBounds(...)` now mirrors the componentwise min/max union over two `min/max` boxes, and `CollisionStepWoW` uses it directly for the start/end/half-step merged query volume instead of a local lambda.
  - New deterministic coverage in `Tests/Navigation.Physics.Tests/WowAabbMergeTests.cs` now pins two exact binary-backed behaviors: componentwise min/max union and shared-face preservation.
  - New raw capture now lives in `docs/physics/0x6373B0_disasm.txt`. Practical implication: one more piece of the unresolved `0x631E70` cache-miss transaction is closed, but the actual remaining gap is still `0x637300`, `0x6372D0`, `0x61E9C0`, the optional swim-side query, and the `0x632A30` wrapper that decides when to invoke that path.
  - Session 213 still kept runtime grounded behavior unchanged and pinned the exact projected query AABB that `0x631E70` checks against the cached bounds. `BuildTerrainQueryBounds(...)` now mirrors the binary shape built from the projected feet position before the double `0x637350` gate.
  - New deterministic coverage in `Tests/Navigation.Physics.Tests/WowTerrainQueryBoundsTests.cs` now pins three exact binary-backed behaviors: `XY` min/max from `this+0xB0`, `Z` min at feet height with `Z` max at `feet + this+0xB4`, and the requirement that both corners fit the cached AABB before the cache-hit path can succeed.
  - New raw capture now lives in `docs/physics/0x631E70_disasm.txt`. Practical implication: the remaining `0x631E70` gap is no longer the projected query-box layout; the next gap is the post-cache-miss expand/merge/query transaction and the optional swim-side query/transform work.
  - Session 212 still kept runtime grounded behavior unchanged and pinned the `0x6315F0` terrain-query mask builder as the next pure binary seam inside the unresolved `0x631E70` path. `BuildTerrainQueryMask(...)` now mirrors the exact base-mask split and both augmentation gates before `0x6721B0`.
  - New deterministic coverage in `Tests/Navigation.Physics.Tests/WowTerrainQueryMaskTests.cs` now pins five exact binary-backed behaviors: the `0x5FA550` base-mask split (`0x100111` vs `0x102111`), the strict `this+0x20 > 0x80DFE8` `0x30000` gate, the swim exclusion, and the two-bit `0x8000` tree augment.
  - New raw capture now lives in `docs/physics/0x6315F0_disasm.txt`. Practical implication: the remaining `0x631E70` / `0x632A30` gap is no longer the query-mask math feeding `0x6721B0`; the next gap is the rest of the merged-query builder transaction around that now-pinned mask.
  - Session 211 still kept runtime grounded behavior unchanged and exposed the first explicit cache-hit gate inside the unresolved `0x631E70` path. `IsPointInsideAabbInclusive(...)` now mirrors binary `0x637350` as an inclusive six-float `min/max` bounds test.
  - New deterministic coverage in `Tests/Navigation.Physics.Tests/WowAabbContainmentTests.cs` now pins three exact binary-backed behaviors: inclusive acceptance on the min/max faces, rejection below the min bounds, and rejection above the max bounds.
  - New raw capture now lives in `docs/physics/0x637350_disasm.txt`. Practical implication: the remaining `0x631E70` / `0x632A30` gap is no longer the cached-query AABB reuse gate. The next gap is the larger query-builder / selector-wrapper transaction around that gate.
  - Session 210 still kept runtime grounded behavior unchanged and exposed the next pure post-selector gates on the `0x6351A0` path. `HasSelectorCandidateWithNegativeDiagonalZ(...)` now mirrors binary `0x635410`, and `HasSelectorCandidateWithUnitZ(...)` now mirrors binary `0x6353D0`, both as exact `0x10`-stride scans over the local candidate buffer's `normal.z` field with the binary `0x8026BC` epsilon.
  - New deterministic coverage in `Tests/Navigation.Physics.Tests/WowSelectorCandidateZMatchTests.cs` now pins four exact binary-backed behaviors: the `0x635410` negative-diagonal match, the `0x6353D0` unit-Z match, the binary epsilon window, and the bounded-candidate-count path.
  - New raw captures now live in `docs/physics/0x635410_disasm.txt` and `docs/physics/0x6353D0_disasm.txt`. Practical implication: the remaining selector gap is no longer these tiny post-selector z-match gates, and the old “height-match” label is now closed as incorrect. The next gap is the unresolved `0x632A30` / `0x631E70` setup side of `0x632BA0` plus the broader `0x6351A0` transaction around the selected index and paired payload.
  - Session 209 still kept runtime grounded behavior unchanged and exposed the next pure caller-side selector body. `EvaluateSelectorDirectionRanking(...)` now mirrors the second-half `0x632BA0` five-direction chooser core inside the production DLL, including the 5-direction loop over `supportPlanes[0..4]`, the `0x632F80` quad-record build, the `0x632700` evaluator handoff, the binary `0x80DFEC` overwrite/append/swap window, and the final zero-clamped reported scalar.
  - New deterministic coverage in `Tests/Navigation.Physics.Tests/WowSelectorDirectionRankingTests.cs` now pins five exact binary-backed behaviors: the direction-plane dot-reject path, the builder-reject path, the evaluator-reject path, the append-and-swap near-tie promotion path, and the final `0x80DFEC` zero-clamp gate.
  - `docs/physics/wow_exe_decompilation.md` now records that the production DLL mirrors the second-half `0x632BA0` chooser core rather than only the surrounding helpers. Practical implication: the remaining selector gap is no longer the 5-direction quad-record ranking loop itself; it is the unresolved `0x632A30` / `0x631E70` setup gates plus the downstream `0x6351A0` / `0x635410` selection gate.
  - Session 208 still kept runtime grounded behavior unchanged and exposed the next pure caller-side selector body. `EvaluateSelectorTriangleSourceRanking(...)` now mirrors binary `0x632280` inside the production DLL, including the 4-source loop over `supportPlanes[5..8]`, the `0x632460` translated-triplet clip-plane build, the `0x632700` evaluator handoff, and the binary `0x80DFEC` overwrite/append/swap ranking window on the 5-slot best-candidate buffer.
  - New deterministic coverage in `Tests/Navigation.Physics.Tests/WowSelectorSourceRankingTests.cs` now pins five exact binary-backed behaviors: the source-plane dot-reject path, the builder-reject path, the evaluator-reject path, the overwrite path, and the append-and-swap near-tie path.
  - `docs/physics/wow_exe_decompilation.md` now records that the production DLL mirrors the full `0x632280` caller-side ranking loop instead of only the surrounding builders/evaluator. Practical implication: the remaining selector gap is no longer the 4-source overwrite/append/swap body; it is the 5-direction chooser in `0x632BA0` plus the post-choice `0x6351A0` / `0x635410` gate.
  - Session 207 still kept runtime grounded behavior unchanged and exposed the next pure selector builder. `BuildSelectorCandidateQuadPlaneRecord(...)` now mirrors binary `0x632F80` inside the production DLL, including the 4-selector ring walk, the previous-point flip, and the translated source-plane anchor in slot 4.
  - New deterministic coverage in `Tests/Navigation.Physics.Tests/WowSelectorCandidateQuadPlaneRecordTests.cs` now pins the four oriented side planes, the slot-4 source-plane re-anchor, and the binary early-fail path when one side-plane normal degenerates below `0x8026BC`.
  - New raw capture now lives in `docs/physics/0x632F80_disasm.txt`. Practical implication: the remaining selector gap is no longer the per-record geometry builders; it is the multi-record overwrite/append/swap ranking path in `0x632280` / `0x632BA0` that feeds the now-pinned `0x632700` evaluator.
  - Session 206 still kept runtime grounded behavior unchanged and exposed the next caller-side selector body. `ClipSelectorPointStripAgainstPlanePrefix(...)` now mirrors binary `0x631870`, and `EvaluateSelectorCandidateRecordSet(...)` now mirrors binary `0x632700` inside the production DLL.
  - New deterministic coverage in `Tests/Navigation.Physics.Tests/WowSelectorCandidateRecordSetTests.cs` now pins four exact binary-backed behaviors: the plane-prefix early-fail path, the dot-reject path, the clip-reject path, and the lowest-ratio best-record update path.
  - New raw captures now live in `docs/physics/0x631870_disasm.txt` and `docs/physics/0x632700_disasm.txt`. Practical implication: the remaining selector gap is no longer how one record is filtered, clipped, and validated; it is how `0x632F80` / `0x632280` build and rank the multi-record buffers that feed this now-pinned evaluator.
  - Session 205 still kept runtime grounded behavior unchanged and exposed the next pure caller-side selector helper. `BuildSelectorCandidatePlaneRecord(...)` now mirrors binary `0x632460` inside the production DLL, including the normalized three-point side planes from `0x637480`, the opposite-point flip, and the translated source-plane anchor in slot 3.
  - New deterministic coverage in `Tests/Navigation.Physics.Tests/WowSelectorCandidatePlaneRecordTests.cs` now pins the three oriented side planes, the source-plane re-anchor through the translated first selector point, and the binary early-fail path when one side-plane normal degenerates below `0x8026BC`.
  - New raw captures now live in `docs/physics/0x632460_disasm.txt` and `docs/physics/0x637480_disasm.txt`. Practical implication: the remaining selector gap is no longer the per-record plane geometry; it is the caller-side evaluator/ranking path in `0x632700` / `0x632280` that feeds the now-pinned record into `0x633720` / `0x635090`.
  - Session 204 still kept runtime grounded behavior unchanged and exposed the next pure selector-chain body. `EvaluateSelectorPlaneRatio(...)`, `ClipSelectorPointStripAgainstPlane(...)`, `ClipSelectorPointStripExcludingPlane(...)`, and `ValidateSelectorPointStripCandidate(...)` now mirror the binary `0x6329E0` / `0x632830` / `0x632980` / `0x6318C0` chain inside the production DLL.
  - New deterministic coverage in `Tests/Navigation.Physics.Tests/WowSelectorCandidateValidationTests.cs` now pins four exact binary-backed behaviors: the `0x6329E0` ratio formula, the `0x6318C0` polygon clipper's intersection/source-index output, the `0x632830` first-pass best-ratio update path, and the strict second-pass rejection path after rebuild.
  - New raw captures now live in `docs/physics/0x6329E0_disasm.txt`, `docs/physics/0x632830_disasm.txt`, and `docs/physics/0x6318C0_disasm.txt`. Practical implication: the remaining selector gap is no longer inside the strip validator math itself; it is the caller-side candidate-record producer path feeding this now-pinned helper chain.
  - Session 203 kept runtime behavior unchanged and exposed the next pure binary helper behind the selector-builder chain. `BuildSelectorNeighborhood(...)` now mirrors `0x631BE0`, `BuildWoWSelectorNeighborhood(...)` exports it through the production DLL, and `WowSelectorNeighborhoodTests.cs` now pins the exact 9-point layout plus 32-byte selector table.
  - New binary evidence now lives in `docs/physics/0x631BE0_disasm.txt`. The fresh note in `docs/physics/wow_exe_decompilation.md` tightens the pre-`0x632830` builder path again: `0x631BE0` consumes the now-pinned `0x631440` support strip, emits the exact 9-point neighborhood, and writes the selector bytes used by `0x632460` / `0x632F80`.
  - Practical implication: the remaining native selector gap is no longer the input geometry. The next unit has to explain the candidate-validation and rebuild helpers (`0x6329E0` / `0x632830` / `0x6318C0`) on top of those exact binary inputs.
  - Session 202 kept runtime behavior unchanged and exposed the next pure binary helper behind the selector-builder chain. `BuildSelectorSupportPlanes(...)` now mirrors `0x631440`, `BuildWoWSelectorSupportPlanes(...)` exports it through the production DLL, and `WowSelectorSupportPlaneTests.cs` now pins the exact 9-plane strip.
  - New binary evidence now lives in `docs/physics/0x631440_disasm.txt`. The fresh note in `docs/physics/wow_exe_decompilation.md` tightens the pre-`0x632830` builder path: `0x631440` emits the fixed `±X`, `±Y`, `+Z`, and four diagonal support planes using `0x80DFE4 = 0.8796418905f` and `0x80DFE0 = 0.4756366014f`.
  - Practical implication: the next native parity unit no longer needs to infer the selector support-strip geometry. It can build on the now-pinned binary plane strip while tracing `0x631BE0` / `0x632830`.
  - Session 201 kept runtime behavior unchanged and narrowed the open selector gap one step further. `EvaluateSelectedContactThresholdGate(...)` is now a pure native helper, `EvaluateWoWSelectedContactThresholdGate(...)` exports it through the production DLL, and `UndercityUpperDoorContactTests.cs` now asserts the packet-backed frame-16 merged query contains zero direct-pair candidates under both `0x633760` threshold modes.
  - New binary evidence now lives in `docs/physics/0x632280_disasm.txt`. The fresh note in `docs/physics/wow_exe_decompilation.md` tightens the selector-builder side: `0x632280` initializes a five-slot local candidate buffer, walks four source entries into `0x632460` / `0x632700`, and uses `0x80DFEC` epsilon-ranked overwrite/append/swap logic on the caller-visible best record. The same note now captures the `0x632830` / `0x6329E0` helper shape around that loop.
  - Practical implication: the frame-16 blocker is not “we picked the wrong already-good direct-pair contact.” The raw merged query offers none. The next native parity unit has to trace why the earlier selector-builder path (`0x632280` / `0x632830` / `0x6318C0`) still leads to the WMO wall entry.
  - Session 200 extended the production grounded-wall trace one level deeper into the `0x633760` threshold gate without changing runtime behavior. `GroundedWallResolutionTrace` / `EvaluateGroundedWallSelection(...)` now record the selected contact's threshold point, selected `normal.z`, current/projected `0x6335D0` prism inclusion, and whether that already-selected contact would stay on the direct paired path under either `0x633760` threshold.
  - New binary evidence now lives in `docs/physics/0x6351A0_disasm.txt` and `docs/physics/0x632BA0_disasm.txt`. The fresh note in `docs/physics/wow_exe_decompilation.md` tightens two open constraints: `0x632BA0` builds a five-slot local candidate buffer before `0x632700`, and once the packet-backed frame-16 WMO wall is already selected, the projected `position + requestedMove` point is outside the `0x6335D0` prism so that wall stays on the alternate `0x635090` path under both relaxed and standard thresholds.
  - Practical implication: the immediate blocker moved one step earlier again. The next native parity unit is not a threshold-mode guess inside `0x633760`; it is tracing why `0x632BA0` / `0x632280` select the WMO wall entry instead of the stateful elevator-support candidate present elsewhere in the merged query.
  - Session 199 finished the remaining scene-loader infrastructure blocker. `SceneQuery::EnsureMapLoaded(...)` now detects legacy metadata-less `.scene` files, rebuilds the same cached bounds through `SceneCache::Extract(...)`, writes back a v2 cache, and loads the metadata-bearing result instead of leaving runtime queries on flattened parent-only metadata.
  - `UndercityUpperDoorContactTests.cs` now proves all three relevant states deterministically: a direct manual v1 load still collapses to parent WMO metadata (`src=1`), a fresh extract round-trip resolves WMO group `3228` with `groupFlags = 0x0000AA05`, and the normal `EnsureMapLoaded(...)` path now auto-upgrades the legacy cache and returns that same WMO-group identity (`src=2`).
  - Practical implication: no more MPQ extraction or scene-autoload work is needed for this blocker. The next native parity unit is the binary-selected contact producer chain (`0x633720` / `0x635090` / paired `0xC4E544`) that still chooses and classifies the grounded blocker before `0x6334A0` / `0x636100` run.
  - Session 198 moved the metadata preservation seam from export-time reconstruction into `SceneCache` extraction itself. Fresh extracted scene caches now carry per-triangle metadata in memory, and the deterministic `.scene` round-trip path also preserves it.
  - The packet-backed frame-16 blocker still selects instance `0x00003B34`, but a bounded Undercity re-extract now proves that selected triangle is WMO group `3228` with `groupFlags = 0x0000AA05` under root WMO `1150` after unload/reload.
  - Practical implication: the blocker is not missing geometry and not a doodad-child-only problem. The next runtime fix is to get the normal scene-load path onto metadata-bearing scene caches, then feed that selected WMO-group identity into the `0x633760` threshold/state parity work.
  - Session 197 extended the selected-contact metadata trace one more step with `selectedResolvedModelFlags` and `selectedMetadataSource`. The export now does a best-effort child doodad match against the parent WMO's default `.doodads` set before reporting the final source.
  - The packet-backed frame-16 blocker still resolves as metadata source `1` (`parent instance`) with `resolvedModelFlags = 0x00000004`, so even the current best-effort post-hoc lookup cannot recover deeper child identity from the collapsed contact.
  - Practical implication: the next native implementation pass should stop trying to reconstruct child identity after selection and instead preserve child WMO/M2 metadata earlier in `SceneCache` / `TestTerrainAABB`.
  - Session 196 extended the production-DLL grounded wall trace seam to resolve selected-contact metadata back to the static VMAP instance/model when possible. `PhysicsTestExports.cpp` now reports `selectedInstanceFlags`, `selectedModelFlags`, `selectedRootId`, `selectedGroupId`, and whether an exact WMO group triangle match was found; `NavigationInterop.cs` and `UndercityUpperDoorContactTests.cs` carry the same fields deterministically.
  - The new packet-backed frame-16 regression changes the immediate parity target again. The selected blocker still comes back as instance `0x00003B34`, but the metadata now proves the scene-cache/TestTerrain path only resolves the parent WMO shell: `instance/model flags = 0x00000004`, `rootWmoId = 1150`, `groupId = -1`, `groupMatchFound = 0`.
  - Practical implication: this is not a missing-geometry problem. The current native path is preserving the blocker triangle itself but collapsing the deeper child WMO/M2 identity the binary `0x5FA550` model-property walk appears to use. The next parity unit should preserve child doodad/WMO metadata through `SceneCache` -> `TestTerrainAABB`, then retry the `0x633760` threshold gate.
  - Session 195 moved the grounded wall transaction seam into the production resolver itself instead of leaving selection/branch tracing duplicated in the export layer. `PhysicsEngine.h/.cpp` now expose shared `WoWCollision::ResolveGroundedWallContacts(...)` plus `GroundedWallResolutionTrace`, and the grounded runtime wall lambda calls that helper directly.
  - `PhysicsTestExports.cpp` still exports `EvaluateGroundedWallSelection(...)`, but it now returns the real runtime transaction: state before/after, branch kind, merged/final wall normals, and horizontal-vs-final projected moves from the same helper the live grounded path uses.
  - The widened packet-backed frame-16 tests changed the immediate blocker. The production helper does not pick the stateful elevator support contact the earlier managed reconstruction implied; it selects WMO wall instance `0x00003B34` (`point=(1553.8352, 242.3765, -9.1597)`, `normal≈+X`, `oriented≈-X`) and stays on the horizontal branch before the final zero-move clamp.
  - Practical implication: the next native parity target is deeper in the selected-contact producer chain (`0x633720` / `0x635090` / paired `0xC4E544` payload), not in the post-selection branch helper alone.
  - Session 194 added a production-DLL native trace seam for grounded blocker selection instead of standing up a separate native tester project. `PhysicsTestExports.cpp` now exports `EvaluateGroundedWallSelection(...)`, which mirrors the current blocker-selection path and returns the chosen contact, raw/oriented oppose scores, reorientation bit, and stateful `CheckWalkable` result.
  - `UndercityUpperDoorContactTests.cs` now uses that native trace export for the frame-16 regression instead of rebuilding the selector in C#. This keeps the diagnostic path tied to the real `Navigation.dll` logic while still remaining deterministic and packet-backed.
  - Fresh full-window `0x6351A0` review tightened the selected-contact note in `docs/physics/wow_exe_decompilation.md`: after `0x632BA0` and `0x633720`, the function either returns `0xC4E544[index]` directly, returns a zeroed pair with success, or falls through the `0x7C5DA0` / `0x6353D0` / `0x635090` alternate path.
  - Session 193 carried `groundedWallState` through the native bridge/replay contract so the packet-backed deterministic harness now preserves the same selected-contact state path across grounded frames that the runtime uses.
  - `PhysicsEngine.cpp` now applies `WoWCollision::CheckWalkable(...)` only to the selected primary contact, uses a local `0x635C00`-shaped Z-only correction on the stateful walkable branch, sets the state bit after the non-walkable vertical branch, and reuses that state during later support-surface selection.
  - `docs/physics/wow_exe_decompilation.md` now records the selected-contact container evidence (`0xC4E52C` / `0xC4E534` / `0xC4E544` plus the `0x6351A0 -> 0x632BA0 -> 0x633720 -> 0x635410/0x6353D0` chain), which is why runtime walkability stays constrained to a chosen contact path instead of a merged-query broadcast.
  - `UndercityUpperDoorContactTests.cs` replaced the temporary frame dumps with a deterministic frame-16 regression proving that the merged query contains a statefully walkable horizontal blocker which is non-opposing until its normal is oriented against the current collision position.
  - Important constraint: that current-position normal reorientation is still an inference from the binary-selected contact semantics plus packet-backed frame-16 evidence, not a directly identified helper. Keep it pinned to deterministic replay coverage until the producer chain is traced more fully.
  - Session 192 added a native merged-query recorder to the deterministic test suite instead of relying on one-off temp harnesses. `PhysicsTestExports.cpp` now exports `QueryTerrainAABBContacts(...)`, `NavigationInterop.cs` exposes the matching `TerrainAabbContact` P/Invoke, and `UndercityUpperDoorContactTests.cs` pins the real packet-backed frame-15 merged contact set from `PacketBackedUndercityElevatorUp`.
  - The new frame-15 tests prove the failing Undercity upper-door query already contains the elevator deck support face at deck height with a signed downward normal and raw `walkable=0`.
  - The same tests also prove the pure `0x6334A0` helper only promotes that support face on its stateful path and that many walls in the same merged query would also be promoted if that state were broadcast per-contact. That closes the shortcut hypothesis: do not replace merged-query `contact.walkable` checks with unconditional `CheckWalkable(..., groundedWallFlagBefore=true)`.
  - Practical next step changed: the blocker is now the binary-selected contact / grounded-wall-state path feeding `0x6334A0` (`0xC4E544` producer chain), not the helper body itself.
  - Session 191 closed the `TestTerrain` contact-orientation blocker that stopped the first `0x6334A0` runtime hookup. Fresh binary capture in `docs/physics/0x6721B0_disasm.txt` shows `0x6721B0` copies signed `0x34` contacts directly from the spatial-query buffer and the follow-on helper `0x637330` is a pure three-component negate.
  - `SceneQuery.h/.cpp` now build signed `TestTerrainAABB` contacts through `BuildTerrainAABBContact(...)`: the stored contact normal faces the query box center, `planeDistance` matches that signed normal, and `walkable` now uses signed `normal.z >= cos(50)` instead of `abs(normal.z)`.
  - `PhysicsEngine.cpp` and `PhysicsTestExports.cpp` now feed the pure `0x6334A0` helper from that signed contact normal/plane pair rather than the raw triangle winding. New deterministic coverage in `TerrainAabbContactOrientationTests.cs` locks the floor-below, shelf-above, and wall-facing cases.
  - The signed orientation feed held the focused grounded runtime slice (`MovementControllerPhysics`, `GroundMovement_Position_NotUnderground`) and both live Durotar parity routes, so the next native pass can retry runtime `0x6334A0` usage on top of a parity-safe signed contact feed.
  - Session 190 captured the real `0x6334A0` `CheckWalkable` rule set in `docs/physics/0x6334A0_disasm.txt`. The new binary-backed note documents the positive-slope threshold split (`0.6427876f` vs `0.17364818f`), the negative-normal top-corner touch helper at `0x6333D0` (`1/720`), and the inside-expanded-triangle helper at `0x6335D0` (`1/12`).
  - `SceneQuery.h/.cpp` now preserve raw triangle vertices, raw plane normals, and plane distances on `AABBContact`, so native code can reason about the same contact data the client uses instead of a flattened walkable bit.
  - `PhysicsEngine.h/.cpp` now expose a pure `WoWCollision::CheckWalkable(...)` helper, `PhysicsTestExports.cpp` exports it, and `WowCheckWalkableTests.cs` locks the major binary branches: steep positive slope clears `0x04000000`, shallow positive slope preserves it, steep negative slope consumes it only when the top footprint touches the plane, and the no-touch case stays blocked.
  - A direct runtime hookup of that helper into the current grounded wall resolver regressed both live Durotar parity fixtures immediately. That hookup was reverted before handoff, which leaves the runtime on the previous green baseline and records a new do-not-repeat: do not feed `0x6334A0` from the current `TestTerrainAABB` contact stream until the `TestTerrain` contact-orientation / normal-flip path matches the client.
  - Session 189 closed the first top-level `0x633840` mismatch instead of going deeper into the grounded helper immediately. Fresh disassembly captured in `docs/physics/0x633840_disasm.txt` shows the client checks the airborne helper (`test ah, 0x20`) before the swim helper (`test eax, 0x200000`), with grounded falling through only after both fail.
  - `PhysicsEngine.cpp` now enforces that same precedence in `StepV2`: `useAirbornePath` wins whenever airborne flags are present, even if `MOVEFLAG_SWIMMING` also overlaps on the same frame. Pure swim frames still route through `ProcessSwimMovement`.
  - Added deterministic regression `FrameAheadIntegrationTests.AirborneBranch_TakesPrecedenceOverSwimmingFlag_OnDryGround`, which proves a dry-ground `FALLINGFAR | SWIMMING` frame descends like pure airborne motion and clears `MOVEFLAG_SWIMMING` in the output instead of being misrouted through the swim helper.
  - The rebuilt native DLL held the new precedence test, an existing jump-arc sanity check, the packet-backed swim replay, and the live redirect parity slice. This keeps the new branch-order cleanup isolated and green before the next grounded-helper pass.
  - Session 182 split the grounded `0x636100` helper choice in `PhysicsEngine.cpp`: `resolveWallSlide(...)` now treats the `0x635D80` horizontal correction and the `0x635C00` selected-plane projection as mutually exclusive branches instead of stacking both on sloped selected planes.
  - Session 182 also retargeted `PacketBackedUndercityElevatorUp_ReplayPreservesUpperDoorBlock` to the promoted packet-backed elevator recording’s actual blocked window (`frames 11..19`) so the compact March 25 fixture remains the canonical upper-door regression.
  - Session 182 held the replay-backed terrain/WMO/dynamic wall fixtures, `GroundMovement_Position_NotUnderground`, `MovementControllerPhysics`, and the aggregate replay drift gate on the rebuilt native DLL.
  - Session 180 shipped the missing selected-plane Z correction from the local `0x635C00` helper into `PhysicsEngine.cpp`: grounded wall resolution now carries a radius-clamped vertical correction from the primary blocker contact and uses that corrected support Z for the final `GetGroundZ(...)` query.
  - Session 180 held the replay-backed terrain/WMO/dynamic wall fixtures, `GroundMovement_Position_NotUnderground`, `MovementControllerPhysics`, and the aggregate replay drift gate on the rebuilt native DLL after one transient `LNK1104` retry.
  - Session 179 shipped the smallest cleanly mapped `0x635D80` effect in `PhysicsEngine.cpp`: grounded wall resolution now adds the client’s `0.001f` horizontal pushout after the blocker-plane projection instead of leaving the resolved move exactly on the wall plane.
  - Session 179 held the replay-backed terrain/WMO/dynamic wall fixtures, `GroundMovement_Position_NotUnderground`, `MovementControllerPhysics`, and the aggregate replay drift gate on the rebuilt native DLL after one transient `LNK1104` retry.
  - Session 178 corrected the grounded `0x636610` jump-table mapping in `PhysicsEngine.cpp`: the three-axis case now selects the lone axis from the minority orientation group, while the four-axis case zeroes the merged blocker vector.
  - Session 178 held the replay-backed terrain/WMO/dynamic wall fixtures, `GroundMovement_Position_NotUnderground`, `MovementControllerPhysics`, and the aggregate replay drift gate on the rebuilt native DLL after one transient `LNK1104` retry.
  - Session 177 shipped one more binary-backed `0x636610` merge rule in `PhysicsEngine.cpp`: the grounded three-axis blocker case now zeroes the merged blocker vector instead of selecting the first surviving axis, matching the jump-table behavior seen in the local `WoW.exe` disassembly.
  - Session 177 held the replay-backed terrain/WMO/dynamic wall fixtures, `GroundMovement_Position_NotUnderground`, `MovementControllerPhysics`, and the aggregate replay drift gate on the rebuilt native DLL, so the three-axis zero rule did not reopen the old false-wall or underground regressions.
  - Session 176 removed the grounded `score + 0.1` secondary-axis filter from `buildMergedBlockerNormal(...)`. The best opposing blocker axis still stays primary, but later distinct blocker axes now remain available to the existing `1 / 2 / 3+` merge path instead of being dropped by score threshold.
  - Session 176 also closed the stale wall-fixture blocker for the native parity loop: `PhysicsReplayTests` now has replay-backed terrain (`DurotarWallSlideWindow_ReplayPreservesRecordedDeflection`), WMO (`BlackrockSpireBackpedal_ReplayPreservesWmoContactStalls`), and dynamic-object (`PacketBackedUndercityElevatorUp_ReplayPreservesUpperDoorBlock`) wall regressions.
  - Session 175 attempted the next `0x6367B0` hypothesis by retrying grounded wall resolution once with the already-slid move, but that regressed `MovementControllerPhysicsTests.Forward_LiveSpeedTestRoute_AchievesMinimumSpeed` to `3.26 y/s`; the change was reverted and recorded in `docs/physicsengine-calibration.md` as do-not-repeat.
  - Session 171 removed the remaining custom grounded wall-contact sort, added replay-backed regression `PhysicsReplayTests.DurotarWallSlideWindow_ReplayPreservesRecordedDeflection`, and corrected the local decomp note that `0x637330` is the vec3-negation helper rather than the grounded slide routine.
  - Session 172 corrected `0x6373B0` from “Collide” to the merged-AABB helper and updated grounded `CollisionStepWoW` so the wall query now uses the merged start/full/half-step `TestTerrainAABB` volume instead of accumulating synthetic full-step and half-step `SweepAABB` contacts.
- Pass result: `delta shipped`
- Validation/tests run:
  - `& "C:/Program Files/Microsoft Visual Studio/18/Community/MSBuild/Current/Bin/MSBuild.exe" Exports/Navigation/Navigation.vcxproj -p:Configuration=Release -p:Platform=x64 -p:PlatformToolset=v145 -p:NodeReuse=false -v:minimal` -> `succeeded`
  - `dotnet build Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false` -> `succeeded`
  - `dotnet test Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~WowSwimQueryPlaneFlipTests|FullyQualifiedName~WowTerrainQueryCacheMissBoundsTests|FullyQualifiedName~WowVectorScalarOffsetTests|FullyQualifiedName~WowTerrainQueryBoundsTests" --logger "console;verbosity=minimal"` -> `passed (9/9)`
  - `& "C:/Program Files/Microsoft Visual Studio/18/Community/MSBuild/Current/Bin/MSBuild.exe" Exports/Navigation/Navigation.vcxproj -p:Configuration=Release -p:Platform=x64 -p:PlatformToolset=v145 -p:NodeReuse=false -v:minimal` -> `succeeded`
  - `dotnet build Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false` -> `succeeded`
  - `dotnet test Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~WowTerrainQueryCacheMissBoundsTests|FullyQualifiedName~WowVectorScalarOffsetTests|FullyQualifiedName~WowTerrainQueryBoundsTests|FullyQualifiedName~WowAabbMergeTests" --logger "console;verbosity=minimal"` -> `passed (9/9)`
  - `& "C:/Program Files/Microsoft Visual Studio/18/Community/MSBuild/Current/Bin/MSBuild.exe" Exports/Navigation/Navigation.vcxproj -p:Configuration=Release -p:Platform=x64 -p:PlatformToolset=v145 -p:NodeReuse=false -v:minimal` -> `succeeded`
  - `dotnet build Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false` -> `succeeded`
  - `dotnet test Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~WowTerrainQueryBoundsTests|FullyQualifiedName~WowTerrainQueryMaskTests|FullyQualifiedName~WowAabbContainmentTests" --logger "console;verbosity=minimal"` -> `passed (11/11)`
  - `dotnet test Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~UndercityUpperDoorContactTests|FullyQualifiedName~WowCheckWalkableTests|FullyQualifiedName~TerrainAabbContactOrientationTests" --logger "console;verbosity=minimal"` -> `passed (16/16)`
  - `& "C:/Program Files/Microsoft Visual Studio/18/Community/MSBuild/Current/Bin/MSBuild.exe" Exports/Navigation/Navigation.vcxproj -p:Configuration=Release -p:Platform=x64 -p:PlatformToolset=v145 -p:NodeReuse=false -v:minimal` -> `succeeded`
  - `dotnet build Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false` -> `succeeded`
  - `dotnet test Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~WowTerrainQueryMaskTests|FullyQualifiedName~WowAabbContainmentTests|FullyQualifiedName~WowSelectorCandidateZMatchTests|FullyQualifiedName~WowSelectorDirectionRankingTests" --logger "console;verbosity=minimal"` -> `passed (17/17)`
  - `dotnet test Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~UndercityUpperDoorContactTests|FullyQualifiedName~WowCheckWalkableTests|FullyQualifiedName~TerrainAabbContactOrientationTests" --logger "console;verbosity=minimal"` -> `passed (16/16)`
  - `& "C:/Program Files/Microsoft Visual Studio/18/Community/MSBuild/Current/Bin/MSBuild.exe" Exports/Navigation/Navigation.vcxproj -p:Configuration=Release -p:Platform=x64 -p:PlatformToolset=v145 -p:NodeReuse=false -v:minimal` -> `succeeded`
  - `dotnet build Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false` -> `succeeded`
  - `dotnet test Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~WowSelectorSupportPlaneTests|FullyQualifiedName~WowSelectorNeighborhoodTests|FullyQualifiedName~WowSelectorCandidateValidationTests|FullyQualifiedName~WowSelectorCandidatePlaneRecordTests|FullyQualifiedName~WowSelectorCandidateRecordSetTests|FullyQualifiedName~WowSelectorCandidateQuadPlaneRecordTests|FullyQualifiedName~WowSelectorSourceRankingTests|FullyQualifiedName~WowSelectorDirectionRankingTests|FullyQualifiedName~WowSelectorCandidateZMatchTests|FullyQualifiedName~WowAabbContainmentTests" --logger "console;verbosity=minimal"` -> `passed (34/34)`
  - `dotnet test Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~UndercityUpperDoorContactTests|FullyQualifiedName~WowCheckWalkableTests|FullyQualifiedName~TerrainAabbContactOrientationTests" --logger "console;verbosity=minimal"` -> `passed (16/16)`
  - `& "C:/Program Files/Microsoft Visual Studio/18/Community/MSBuild/Current/Bin/MSBuild.exe" Exports/Navigation/Navigation.vcxproj -p:Configuration=Release -p:Platform=x64 -p:PlatformToolset=v145 -p:NodeReuse=false -v:minimal` -> `succeeded`
  - `dotnet build Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false` -> `succeeded`
  - `dotnet test Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~WowSelectorSupportPlaneTests|FullyQualifiedName~WowSelectorNeighborhoodTests|FullyQualifiedName~WowSelectorCandidateValidationTests|FullyQualifiedName~WowSelectorCandidatePlaneRecordTests|FullyQualifiedName~WowSelectorCandidateRecordSetTests|FullyQualifiedName~WowSelectorCandidateQuadPlaneRecordTests|FullyQualifiedName~WowSelectorSourceRankingTests|FullyQualifiedName~WowSelectorDirectionRankingTests|FullyQualifiedName~WowSelectorCandidateZMatchTests" --logger "console;verbosity=minimal"` -> `passed (31/31)`
  - `dotnet test Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~UndercityUpperDoorContactTests|FullyQualifiedName~WowCheckWalkableTests|FullyQualifiedName~TerrainAabbContactOrientationTests" --logger "console;verbosity=minimal"` -> `passed (16/16)`
  - `& "C:/Program Files/Microsoft Visual Studio/18/Community/MSBuild/Current/Bin/MSBuild.exe" Exports/Navigation/Navigation.vcxproj -p:Configuration=Release -p:Platform=x64 -p:PlatformToolset=v145 -p:NodeReuse=false -v:minimal` -> `succeeded`
  - `dotnet build Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false` -> `succeeded`
  - `dotnet test Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~WowSelectorSupportPlaneTests|FullyQualifiedName~WowSelectorNeighborhoodTests|FullyQualifiedName~WowSelectorCandidateValidationTests|FullyQualifiedName~WowSelectorCandidatePlaneRecordTests|FullyQualifiedName~WowSelectorCandidateRecordSetTests|FullyQualifiedName~WowSelectorCandidateQuadPlaneRecordTests|FullyQualifiedName~WowSelectorSourceRankingTests|FullyQualifiedName~WowSelectorDirectionRankingTests" --logger "console;verbosity=minimal"` -> `passed (27/27)`
  - `dotnet test Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~UndercityUpperDoorContactTests|FullyQualifiedName~WowCheckWalkableTests|FullyQualifiedName~TerrainAabbContactOrientationTests" --logger "console;verbosity=minimal"` -> `passed (16/16)`
  - `& "C:/Program Files/Microsoft Visual Studio/18/Community/MSBuild/Current/Bin/MSBuild.exe" Exports/Navigation/Navigation.vcxproj -p:Configuration=Release -p:Platform=x64 -p:PlatformToolset=v145 -p:NodeReuse=false -v:minimal` -> `succeeded`
  - `dotnet build Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false` -> `succeeded`
  - `dotnet test Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~WowSelectorSupportPlaneTests|FullyQualifiedName~WowSelectorNeighborhoodTests|FullyQualifiedName~WowSelectorCandidateValidationTests|FullyQualifiedName~WowSelectorCandidatePlaneRecordTests|FullyQualifiedName~WowSelectorCandidateRecordSetTests|FullyQualifiedName~WowSelectorCandidateQuadPlaneRecordTests|FullyQualifiedName~WowSelectorSourceRankingTests" --logger "console;verbosity=minimal"` -> `passed (22/22)`
  - `dotnet test Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~UndercityUpperDoorContactTests|FullyQualifiedName~WowCheckWalkableTests|FullyQualifiedName~TerrainAabbContactOrientationTests" --logger "console;verbosity=minimal"` -> `passed (16/16)`
  - `& "C:/Program Files/Microsoft Visual Studio/18/Community/MSBuild/Current/Bin/MSBuild.exe" Exports/Navigation/Navigation.vcxproj -p:Configuration=Release -p:Platform=x64 -p:PlatformToolset=v145 -p:NodeReuse=false -v:minimal` -> `succeeded`
  - `dotnet build Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false` -> `succeeded`
  - `dotnet test Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~WowSelectorSupportPlaneTests|FullyQualifiedName~WowSelectorNeighborhoodTests|FullyQualifiedName~WowSelectorCandidateValidationTests|FullyQualifiedName~WowSelectorCandidatePlaneRecordTests|FullyQualifiedName~WowSelectorCandidateRecordSetTests|FullyQualifiedName~WowSelectorCandidateQuadPlaneRecordTests" --logger "console;verbosity=minimal"` -> `passed (17/17)`
  - `dotnet test Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~UndercityUpperDoorContactTests|FullyQualifiedName~WowCheckWalkableTests|FullyQualifiedName~TerrainAabbContactOrientationTests" --logger "console;verbosity=minimal"` -> `passed (16/16)`
  - `& "C:/Program Files/Microsoft Visual Studio/18/Community/MSBuild/Current/Bin/MSBuild.exe" Exports/Navigation/Navigation.vcxproj -p:Configuration=Release -p:Platform=x64 -p:PlatformToolset=v145 -p:NodeReuse=false -v:minimal` -> `succeeded`
  - `dotnet build Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false` -> `succeeded`
  - `dotnet test Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~WowSelectorSupportPlaneTests|FullyQualifiedName~WowSelectorNeighborhoodTests|FullyQualifiedName~WowSelectorCandidateValidationTests|FullyQualifiedName~WowSelectorCandidatePlaneRecordTests|FullyQualifiedName~WowSelectorCandidateRecordSetTests" --logger "console;verbosity=minimal"` -> `passed (15/15)`
  - `dotnet test Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~UndercityUpperDoorContactTests|FullyQualifiedName~WowCheckWalkableTests|FullyQualifiedName~TerrainAabbContactOrientationTests" --logger "console;verbosity=minimal"` -> `passed (16/16)`
  - `& "C:/Program Files/Microsoft Visual Studio/18/Community/MSBuild/Current/Bin/MSBuild.exe" Exports/Navigation/Navigation.vcxproj -p:Configuration=Release -p:Platform=x64 -p:PlatformToolset=v145 -p:NodeReuse=false -v:minimal` -> `succeeded`
  - `dotnet build Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false` -> `succeeded`
  - `dotnet test Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~WowSelectorSupportPlaneTests|FullyQualifiedName~WowSelectorNeighborhoodTests|FullyQualifiedName~WowSelectorCandidateValidationTests|FullyQualifiedName~WowSelectorCandidatePlaneRecordTests" --logger "console;verbosity=minimal"` -> `passed (11/11)`
  - `dotnet test Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~UndercityUpperDoorContactTests|FullyQualifiedName~WowCheckWalkableTests|FullyQualifiedName~TerrainAabbContactOrientationTests" --logger "console;verbosity=minimal"` -> `passed (16/16)`
  - `& "C:/Program Files/Microsoft Visual Studio/18/Community/MSBuild/Current/Bin/MSBuild.exe" Exports/Navigation/Navigation.vcxproj -p:Configuration=Release -p:Platform=x64 -p:PlatformToolset=v145 -p:NodeReuse=false -v:minimal` -> `succeeded`
  - `dotnet build Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false` -> `succeeded`
  - `dotnet test Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~UndercityUpperDoorContactTests" --logger "console;verbosity=minimal"` -> `passed (8/8)`
  - `dotnet test Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~WowCheckWalkableTests|FullyQualifiedName~TerrainAabbContactOrientationTests" --logger "console;verbosity=minimal"` -> `passed (7/7)`
  - `& "C:/Program Files/Microsoft Visual Studio/18/Community/MSBuild/Current/Bin/MSBuild.exe" Exports/Navigation/Navigation.vcxproj -p:Configuration=Release -p:Platform=x64 -p:PlatformToolset=v145 -p:NodeReuse=false -v:minimal` -> `succeeded`
  - `dotnet build Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false` -> `succeeded`
  - `dotnet test Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~UndercityUpperDoorContactTests" --logger "console;verbosity=detailed"` -> `passed (5/5)`
  - `& "C:/Program Files/Microsoft Visual Studio/18/Community/MSBuild/Current/Bin/MSBuild.exe" Exports/Navigation/Navigation.vcxproj -p:Configuration=Release -p:Platform=x64 -p:PlatformToolset=v145 -p:NodeReuse=false -v:minimal` -> `succeeded`
  - `dotnet build Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false` -> `succeeded`
  - `& "C:/Program Files/Microsoft Visual Studio/18/Community/MSBuild/Current/Bin/MSBuild.exe" Exports/Navigation/Navigation.vcxproj -p:Configuration=Release -p:Platform=x64 -p:PlatformToolset=v145 -p:NodeReuse=false -v:minimal` -> `succeeded`
  - `dotnet build Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false` -> `succeeded`
  - `dotnet test Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~UndercityUpperDoorContactTests" --logger "console;verbosity=detailed"` -> `passed (6/6)`
  - `dotnet test Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~WowCheckWalkableTests|FullyQualifiedName~TerrainAabbContactOrientationTests|FullyQualifiedName~UndercityUpperDoorContactTests" --logger "console;verbosity=minimal"` -> `passed (13/13)`
  - `dotnet test Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~UndercityUpperDoorContactTests" --logger "console;verbosity=detailed"` -> `passed (5/5)`
  - `& "C:/Program Files/Microsoft Visual Studio/18/Community/MSBuild/Current/Bin/MSBuild.exe" Exports/Navigation/Navigation.vcxproj -p:Configuration=Release -p:Platform=x64 -p:PlatformToolset=v145 -p:NodeReuse=false -v:minimal` -> `succeeded`
  - `dotnet build Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false` -> `succeeded`
  - `dotnet test Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~UndercityUpperDoorContactTests" --logger "console;verbosity=detailed"` -> `passed (4/4)`
  - `dotnet test Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~WowCheckWalkableTests|FullyQualifiedName~TerrainAabbContactOrientationTests" --logger "console;verbosity=minimal"` -> `passed (7/7)`
  - `dotnet test Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~MovementControllerPhysics|FullyQualifiedName~PhysicsReplayTests" --logger "console;verbosity=minimal"` -> `passed (55/56, one skipped MPQ extraction test)`
  - `dotnet test Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~ServerMovementValidationTests.GroundMovement_Position_NotUnderground" --logger "console;verbosity=minimal"` -> `passed (1/1)`
  - `& "C:/Program Files/Microsoft Visual Studio/18/Community/MSBuild/Current/Bin/MSBuild.exe" Exports/Navigation/Navigation.vcxproj -p:Configuration=Release -p:Platform=x64 -p:PlatformToolset=v145 -p:NodeReuse=false -v:minimal` -> `succeeded`
  - `dotnet build Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false` -> `succeeded`
  - `dotnet test Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~UndercityUpperDoorContactTests" --logger "console;verbosity=detailed"` -> `passed (3/3)`
  - `dotnet test Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName=Navigation.Physics.Tests.PhysicsReplayTests.PacketBackedUndercityElevatorUp_ReplayPreservesUpperDoorBlock" --logger "console;verbosity=minimal"` -> `passed (1/1)`
  - `& "C:/Program Files/Microsoft Visual Studio/18/Community/MSBuild/Current/Bin/MSBuild.exe" Exports/Navigation/Navigation.vcxproj -p:Configuration=Release -p:Platform=x64 -p:PlatformToolset=v145 -p:NodeReuse=false -v:minimal` -> `succeeded`
  - `dotnet build Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false` -> `succeeded`
  - `dotnet test Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --no-restore --filter "FullyQualifiedName~UndercityUpperDoorContactTests" --logger "console;verbosity=detailed"` -> `passed (3/3)`
  - `dotnet test Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~TerrainAabbContactOrientationTests|FullyQualifiedName~WowCheckWalkableTests" --logger "console;verbosity=minimal"` -> `passed (7/7)`
  - `dotnet test Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~PhysicsReplayTests.DurotarWallSlideWindow_ReplayPreservesRecordedDeflection|FullyQualifiedName~PhysicsReplayTests.BlackrockSpireBackpedal_ReplayPreservesWmoContactStalls|FullyQualifiedName~PhysicsReplayTests.PacketBackedUndercityElevatorUp_ReplayPreservesUpperDoorBlock|FullyQualifiedName~PhysicsReplayTests.PacketBackedUndercityElevatorUp_ReplayBoardsUndergroundAndExitsUpperDeck" --logger "console;verbosity=minimal"` -> `passed (4/4)`
  - `dotnet test Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~MovementControllerPhysics" --logger "console;verbosity=minimal"` -> `passed (29/29)`
  - `dotnet test Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~ServerMovementValidationTests.GroundMovement_Position_NotUnderground" --logger "console;verbosity=minimal"` -> `passed (1/1)`
  - `dotnet test Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~PhysicsReplayTests.AggregateDriftGate_AllRecordings_CleanFramesWithinThresholds" --logger "console;verbosity=minimal"` -> `passed (1/1)`
  - `& "C:/Program Files/Microsoft Visual Studio/18/Community/MSBuild/Current/Bin/MSBuild.exe" Exports/Navigation/Navigation.vcxproj -p:Configuration=Release -p:Platform=x64 -p:PlatformToolset=v145 -p:NodeReuse=false -v:minimal` -> `succeeded`
  - `dotnet build Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false` -> `succeeded`
  - `dotnet test Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~UndercityUpperDoorContactTests|FullyQualifiedName~WowCheckWalkableTests|FullyQualifiedName~TerrainAabbContactOrientationTests" --logger "console;verbosity=minimal"` -> `passed (9/9)`
  - `& "C:/Program Files/Microsoft Visual Studio/18/Community/MSBuild/Current/Bin/MSBuild.exe" Exports/Navigation/Navigation.vcxproj -p:Configuration=Release -p:Platform=x64 -p:PlatformToolset=v145 -p:NodeReuse=false -v:minimal` -> `succeeded`
  - `dotnet build Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false` -> `succeeded`
  - `dotnet test Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~TerrainAabbContactOrientationTests|FullyQualifiedName~WowCheckWalkableTests|FullyQualifiedName~FrameAheadIntegrationTests.AirborneBranch_TakesPrecedenceOverSwimmingFlag_OnDryGround" --logger "console;verbosity=minimal"` -> `passed (8/8)`
  - `dotnet test Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~TerrainAabbContactOrientationTests|FullyQualifiedName~WowCheckWalkableTests|FullyQualifiedName~FrameAheadIntegrationTests.AirborneBranch_TakesPrecedenceOverSwimmingFlag_OnDryGround|FullyQualifiedName~ServerMovementValidationTests.GroundMovement_Position_NotUnderground|FullyQualifiedName~MovementControllerPhysics" --logger "console;verbosity=minimal"` -> `passed (38/38)`
  - `$env:WWOW_TEST_PRESERVE_EXISTING_PATHFINDING='1'; dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~MovementParityTests.Parity_Durotar_RoadPath_TurnStart" --logger "console;verbosity=minimal"` -> `passed (1/1)`
  - `$env:WWOW_TEST_PRESERVE_EXISTING_PATHFINDING='1'; dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~MovementParityTests.Parity_Durotar_RoadPath_Redirect" --logger "console;verbosity=minimal"` -> `passed (1/1)`
  - `& "C:/Program Files/Microsoft Visual Studio/18/Community/MSBuild/Current/Bin/MSBuild.exe" Exports/Navigation/Navigation.vcxproj -p:Configuration=Release -p:Platform=x64 -p:PlatformToolset=v145 -p:NodeReuse=false -v:minimal` -> `succeeded`
  - `dotnet test Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~WowCheckWalkableTests|FullyQualifiedName~FrameAheadIntegrationTests.AirborneBranch_TakesPrecedenceOverSwimmingFlag_OnDryGround" --logger "console;verbosity=minimal"` -> `passed (5/5)`
  - `$env:WWOW_TEST_PRESERVE_EXISTING_PATHFINDING='1'; dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~MovementParityTests.Parity_Durotar_RoadPath_TurnStart" --logger "console;verbosity=minimal"` -> `passed (1/1)`
  - `$env:WWOW_TEST_PRESERVE_EXISTING_PATHFINDING='1'; dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~MovementParityTests.Parity_Durotar_RoadPath_Redirect" --logger "console;verbosity=minimal"` -> `passed (1/1)`
  - `& "C:/Program Files/Microsoft Visual Studio/18/Community/MSBuild/Current/Bin/MSBuild.exe" Exports/Navigation/Navigation.vcxproj -p:Configuration=Release -p:Platform=x64 -p:PlatformToolset=v145 -p:NodeReuse=false -v:minimal` -> `succeeded`
  - `dotnet build Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false` -> `succeeded`
  - `dotnet test Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~FrameAheadIntegrationTests.AirborneBranch_TakesPrecedenceOverSwimmingFlag_OnDryGround|FullyQualifiedName~FrameAheadIntegrationTests.JumpArc_FlatGround_PeakHeightMatchesPhysics|FullyQualifiedName~PhysicsReplayTests.SwimForward_FrameByFrame_PositionMatchesRecording" --logger "console;verbosity=minimal"` -> `passed (3/3)`
  - `$env:WWOW_TEST_PRESERVE_EXISTING_PATHFINDING='1'; dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~MovementParityTests.Parity_Durotar_RoadPath_Redirect" --logger "console;verbosity=minimal"` -> `passed (1/1)`
  - `& "C:/Program Files/Microsoft Visual Studio/18/Community/MSBuild/Current/Bin/MSBuild.exe" Exports/Navigation/Navigation.vcxproj -p:Configuration=Release -p:Platform=x64 -p:PlatformToolset=v145 -p:NodeReuse=false -v:minimal` -> `succeeded`
  - `dotnet test Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --no-build -p:UseSharedCompilation=false --filter "FullyQualifiedName~PhysicsReplayTests.DurotarWallSlideWindow_ReplayPreservesRecordedDeflection|FullyQualifiedName~PhysicsReplayTests.BlackrockSpireBackpedal_ReplayPreservesWmoContactStalls|FullyQualifiedName~PhysicsReplayTests.PacketBackedUndercityElevatorUp_ReplayPreservesUpperDoorBlock|FullyQualifiedName~PhysicsReplayTests.PacketBackedUndercityElevatorUp_ReplayBoardsUndergroundAndExitsUpperDeck|FullyQualifiedName~FrameByFramePhysicsTests.ValleyOfTrialsSlopeRoute_DoesNotReportFalseWallHits|FullyQualifiedName~ServerMovementValidationTests.GroundMovement_Position_NotUnderground|FullyQualifiedName~MovementControllerPhysics" --logger "console;verbosity=minimal"` -> `35 passed`
  - `dotnet test Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --no-build -p:UseSharedCompilation=false --filter "FullyQualifiedName~PhysicsReplayTests.AggregateDriftGate_AllRecordings_CleanFramesWithinThresholds" --logger "console;verbosity=minimal"` -> `1 passed`
- Files changed:
  - `Exports/Navigation/PhysicsEngine.h`
  - `Exports/Navigation/PhysicsEngine.cpp`
  - `Exports/Navigation/PhysicsTestExports.cpp`
  - `Tests/Navigation.Physics.Tests/NavigationInterop.cs`
  - `Tests/Navigation.Physics.Tests/WowSwimQueryPlaneFlipTests.cs`
  - `docs/physics/0x597AD0_disasm.txt`
  - `docs/physics/wow_exe_decompilation.md`
  - `docs/physicsengine-calibration.md`
  - `Exports/Navigation/TASKS.md`
  - `Tests/Navigation.Physics.Tests/TASKS.md`
  - `docs/TASKS.md`
  - `Exports/Navigation/PhysicsEngine.h`
  - `Exports/Navigation/PhysicsEngine.cpp`
  - `Exports/Navigation/PhysicsTestExports.cpp`
  - `Tests/Navigation.Physics.Tests/NavigationInterop.cs`
  - `Tests/Navigation.Physics.Tests/WowTerrainQueryCacheMissBoundsTests.cs`
  - `docs/physics/wow_exe_decompilation.md`
  - `docs/physicsengine-calibration.md`
  - `Exports/Navigation/TASKS.md`
  - `Tests/Navigation.Physics.Tests/TASKS.md`
  - `docs/TASKS.md`
  - `Exports/Navigation/PhysicsEngine.h`
  - `Exports/Navigation/PhysicsEngine.cpp`
  - `Exports/Navigation/PhysicsTestExports.cpp`
  - `Tests/Navigation.Physics.Tests/NavigationInterop.cs`
  - `Tests/Navigation.Physics.Tests/WowTerrainQueryBoundsTests.cs`
  - `docs/physics/0x631E70_disasm.txt`
  - `docs/physics/wow_exe_decompilation.md`
  - `docs/physicsengine-calibration.md`
  - `Exports/Navigation/TASKS.md`
  - `Tests/Navigation.Physics.Tests/TASKS.md`
  - `docs/TASKS.md`
  - `Exports/Navigation/PhysicsEngine.h`
  - `Exports/Navigation/PhysicsEngine.cpp`
  - `Exports/Navigation/PhysicsTestExports.cpp`
  - `Tests/Navigation.Physics.Tests/NavigationInterop.cs`
  - `Tests/Navigation.Physics.Tests/WowTerrainQueryMaskTests.cs`
  - `docs/physics/0x6315F0_disasm.txt`
  - `docs/physics/wow_exe_decompilation.md`
  - `docs/physicsengine-calibration.md`
  - `Exports/Navigation/TASKS.md`
  - `Tests/Navigation.Physics.Tests/TASKS.md`
  - `docs/TASKS.md`
  - `Exports/Navigation/PhysicsEngine.h`
  - `Exports/Navigation/PhysicsEngine.cpp`
  - `Exports/Navigation/PhysicsTestExports.cpp`
  - `Tests/Navigation.Physics.Tests/NavigationInterop.cs`
  - `Tests/Navigation.Physics.Tests/WowSelectorCandidateQuadPlaneRecordTests.cs`
  - `docs/physics/0x632F80_disasm.txt`
  - `docs/physics/wow_exe_decompilation.md`
  - `docs/physicsengine-calibration.md`
  - `Exports/Navigation/TASKS.md`
  - `Tests/Navigation.Physics.Tests/TASKS.md`
  - `docs/TASKS.md`
  - `Exports/Navigation/PhysicsEngine.h`
  - `Exports/Navigation/PhysicsEngine.cpp`
  - `Exports/Navigation/PhysicsTestExports.cpp`
  - `Tests/Navigation.Physics.Tests/NavigationInterop.cs`
  - `Tests/Navigation.Physics.Tests/WowSelectorCandidateRecordSetTests.cs`
  - `docs/physics/0x631870_disasm.txt`
  - `docs/physics/0x632700_disasm.txt`
  - `docs/physics/wow_exe_decompilation.md`
  - `docs/physicsengine-calibration.md`
  - `Exports/Navigation/TASKS.md`
  - `Tests/Navigation.Physics.Tests/TASKS.md`
  - `docs/TASKS.md`
  - `Exports/Navigation/PhysicsEngine.h`
  - `Exports/Navigation/PhysicsEngine.cpp`
  - `Exports/Navigation/PhysicsTestExports.cpp`
  - `Tests/Navigation.Physics.Tests/NavigationInterop.cs`
  - `Tests/Navigation.Physics.Tests/WowSelectorCandidatePlaneRecordTests.cs`
  - `docs/physics/0x632460_disasm.txt`
  - `docs/physics/0x637480_disasm.txt`
  - `docs/physics/wow_exe_decompilation.md`
  - `docs/physicsengine-calibration.md`
  - `Exports/Navigation/TASKS.md`
  - `Tests/Navigation.Physics.Tests/TASKS.md`
  - `docs/TASKS.md`
  - `Exports/Navigation/SceneCache.h`
  - `Exports/Navigation/SceneCache.cpp`
  - `Exports/Navigation/SceneQuery.h`
  - `Exports/Navigation/SceneQuery.cpp`
  - `Exports/Navigation/PhysicsTestExports.cpp`
  - `Tests/Navigation.Physics.Tests/NavigationInterop.cs`
  - `Tests/Navigation.Physics.Tests/UndercityUpperDoorContactTests.cs`
  - `docs/physicsengine-calibration.md`
  - `Exports/Navigation/TASKS.md`
  - `Tests/Navigation.Physics.Tests/TASKS.md`
  - `docs/TASKS.md`
  - `Exports/Navigation/WorldModel.h`
  - `Exports/Navigation/PhysicsTestExports.cpp`
  - `Tests/Navigation.Physics.Tests/NavigationInterop.cs`
  - `Tests/Navigation.Physics.Tests/UndercityUpperDoorContactTests.cs`
  - `docs/physics/wow_exe_decompilation.md`
  - `docs/physicsengine-calibration.md`
  - `Exports/Navigation/TASKS.md`
  - `Tests/Navigation.Physics.Tests/TASKS.md`
  - `docs/TASKS.md`
  - `Exports/Navigation/PhysicsTestExports.cpp`
  - `Tests/Navigation.Physics.Tests/NavigationInterop.cs`
  - `Tests/Navigation.Physics.Tests/UndercityUpperDoorContactTests.cs`
  - `docs/physics/wow_exe_decompilation.md`
  - `docs/physicsengine-calibration.md`
  - `Exports/Navigation/TASKS.md`
  - `Tests/Navigation.Physics.Tests/TASKS.md`
  - `docs/TASKS.md`
  - `Exports/Navigation/PhysicsBridge.h`
  - `Exports/Navigation/PhysicsEngine.h`
  - `Exports/Navigation/PhysicsEngine.cpp`
  - `Services/PathfindingService/Repository/Physics.cs`
  - `Tests/Navigation.Physics.Tests/Helpers/ReplayEngine.cs`
  - `Tests/Navigation.Physics.Tests/NavigationInterop.cs`
  - `Tests/Navigation.Physics.Tests/UndercityUpperDoorContactTests.cs`
  - `docs/physics/wow_exe_decompilation.md`
  - `docs/physicsengine-calibration.md`
  - `Exports/Navigation/TASKS.md`
  - `Tests/Navigation.Physics.Tests/TASKS.md`
  - `docs/TASKS.md`
  - `Exports/Navigation/PhysicsTestExports.cpp`
  - `Tests/Navigation.Physics.Tests/NavigationInterop.cs`
  - `Tests/Navigation.Physics.Tests/UndercityUpperDoorContactTests.cs`
  - `Exports/Navigation/TASKS.md`
  - `Tests/Navigation.Physics.Tests/TASKS.md`
  - `docs/physicsengine-calibration.md`
  - `docs/TASKS.md`
  - `Exports/Navigation/SceneQuery.h`
  - `Exports/Navigation/SceneQuery.cpp`
  - `Exports/Navigation/PhysicsEngine.cpp`
  - `Exports/Navigation/PhysicsTestExports.cpp`
  - `Tests/Navigation.Physics.Tests/NavigationInterop.cs`
  - `Tests/Navigation.Physics.Tests/TerrainAabbContactOrientationTests.cs`
  - `docs/physics/0x6721B0_disasm.txt`
  - `Exports/Navigation/TASKS.md`
  - `Tests/Navigation.Physics.Tests/TASKS.md`
  - `docs/physicsengine-calibration.md`
  - `docs/TASKS.md`
  - `Exports/Navigation/PhysicsEngine.cpp`
  - `Exports/Navigation/PhysicsEngine.h`
  - `Exports/Navigation/PhysicsTestExports.cpp`
  - `Exports/Navigation/SceneQuery.cpp`
  - `Exports/Navigation/SceneQuery.h`
  - `Tests/Navigation.Physics.Tests/NavigationInterop.cs`
  - `Tests/Navigation.Physics.Tests/WowCheckWalkableTests.cs`
  - `docs/physics/0x6334A0_disasm.txt`
  - `Exports/Navigation/TASKS.md`
  - `Tests/Navigation.Physics.Tests/TASKS.md`
  - `docs/physicsengine-calibration.md`
  - `docs/TASKS.md`
  - `Exports/Navigation/PhysicsEngine.cpp`
  - `Tests/Navigation.Physics.Tests/FrameAheadIntegrationTests.cs`
  - `docs/physics/0x633840_disasm.txt`
  - `Exports/Navigation/TASKS.md`
  - `Tests/Navigation.Physics.Tests/TASKS.md`
  - `docs/physicsengine-calibration.md`
  - `docs/TASKS.md`
  - `Exports/Navigation/PhysicsEngine.cpp`
  - `Tests/Navigation.Physics.Tests/PhysicsReplayTests.cs`
  - `Tests/Navigation.Physics.Tests/Helpers/TestConstants.cs`
  - `Exports/Navigation/TASKS.md`
  - `Tests/Navigation.Physics.Tests/TASKS.md`
  - `docs/physicsengine-calibration.md`
  - `docs/TASKS.md`
  - `Exports/Navigation/PhysicsEngine.h`
  - `Exports/Navigation/PhysicsEngine.cpp`
  - `Exports/Navigation/PhysicsTestExports.cpp`
  - `Tests/Navigation.Physics.Tests/NavigationInterop.cs`
  - `Tests/Navigation.Physics.Tests/WowSelectorSourceRankingTests.cs`
  - `docs/physics/wow_exe_decompilation.md`
  - `docs/physicsengine-calibration.md`
  - `Exports/Navigation/TASKS.md`
  - `Tests/Navigation.Physics.Tests/TASKS.md`
  - `docs/TASKS.md`
- Next command: `py -c "from capstone import *; import pathlib; code=pathlib.Path(r'D:/World of Warcraft/WoW.exe').read_bytes(); md=Cs(CS_ARCH_X86, CS_MODE_32); start=0x635090; data=code[start-0x400000:start-0x400000+704]; [print(f'0x{i.address:08X}: {i.mnemonic:8s} {i.op_str}') for i in md.disasm(data, start)]"`
- Blockers:
  - The production-DLL deterministic harness now exposes grounded blocker selection directly, and the visible `0x6351A0` tail is closed. The next missing visibility is the paired `0xC4E544` payload, the selected index that chose it, and how `0x635450` consumes the two out-state dwords after `0x6351A0`.
  - The next missing visibility is still inside the selected-contact producer chain, not in a separate native test project. The higher-leverage step is a transaction/export seam around the production DLL so deterministic tests can capture the chosen index plus paired `0xC4E544` payload directly.
  - The helper body is no longer the immediate blocker. The new frame-15 contact probe proves the missing runtime piece is the binary-selected contact / grounded-wall-state path feeding `0x6334A0`; without that state path, a blanket stateful helper call would also promote many wall contacts in the same merged query.
  - The exact grounded post-`TestTerrain` wall/corner resolution helper is still unresolved in the binary; the current stateless path now uses merged blocker-axis resolution on top of the correct merged query volume, but it still lacks the real `0x6334A0` walkability logic and the remaining `0x636100` return-code / movement-fraction bookkeeping around `0x635C00` / `0x635D80`.
  - Do not route the new `0x6334A0` helper into live grounded resolution again until `TestTerrainAABB` contact orientation and the post-query `0x637330` normal-flip path are parity-safe; the first direct hookup already regressed both live Durotar routes and was reverted.
  - Do not replace merged-query `contact.walkable` with unconditional `CheckWalkable(..., groundedWallFlagBefore=true)` or any equivalent per-contact broadcast; the frame-15 contact dump proves that would bless unrelated walls.
  - Do not reintroduce the reverted two-pass remaining-move reprojection loop without new binary evidence; it is now a documented regression.
  - `0x6373B0` is closed as the merged-AABB helper; do not spend more time treating it as the missing collision/slide routine.
  - Verified replay-backed wall fixtures now exist, so the next native pass should use those fixtures instead of the stale Stormwind / RFC / Un'Goro coordinate probes.
  - Managed `MovementController` cadence and ownership parity still need a focused audit once the native grounded wall path is reduced to the client’s real post-query sequence.
