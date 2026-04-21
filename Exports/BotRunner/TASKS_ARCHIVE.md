# Task Archive

Completed items moved from TASKS.md.

## Archived Snapshot (2026-04-15) - Corpse-run waypoint advancement closeout

- [x] Close the foreground corpse-run runback/reclaim stall.
- Completion notes:
  - `NavigationPath` still corridor-gates standard adaptive-radius advancement, preserving the Orgrimmar corner fix.
  - When probe heuristics are disabled, close waypoint advancement no longer runs the standard shortcut probe veto. This matches the corpse-run policy, which intentionally follows unsmoothed service waypoints and avoids probe pruning.
  - Added deterministic coverage for the probe-disabled close-waypoint case that previously left the foreground ghost pinned near `(237.1,-4749.0,13.0)`.
  - Opt-in foreground corpse-run live validation now restores strict-alive state instead of stalling before reclaim range.
- Validation:
  - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~NavigationPathTests.GetNextWaypoint_ProbeDisabled_AdvancesCloseWaypointEvenWhenShortcutProbeFails|FullyQualifiedName~NavigationPathTests.GetNextWaypoint_DoesNotAdvanceEarly_WhenAdaptiveRadiusShortcutLeavesWalkableCorridor" --logger "console;verbosity=minimal"` -> covered by focused regression bundle, `passed`
  - `$env:WWOW_DATA_DIR='D:\MaNGOS\data'; $env:WWOW_RETRY_FG_CRASH001='1'; dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~DeathCorpseRunTests.Death_ReleaseAndRetrieve_ResurrectsForegroundPlayer" --blame-hang --blame-hang-timeout 5m --logger "console;verbosity=minimal" --results-directory "E:\repos\Westworld of Warcraft\tmp\test-runtime\results-live" --logger "trx;LogFileName=fg_corpse_run_after_corpse_probe_policy.trx"` -> `passed (1/1); alive after 30s, bestDist=34y`

## Archived Snapshot (2026-04-15) - BR-NAV-009 long local-physics horizon hit-wall tolerance

- [x] Avoid rejecting long service route segments solely from short-horizon local physics `hit_wall` when route-layer metrics remain consistent.
- Completion notes:
  - Local segment simulation still rejects proven route-layer mismatches and short blocked legs.
  - Long service legs now tolerate a short-horizon `hit_wall` result only when upward/lateral/absolute route-layer deltas stay below the mismatch thresholds.
  - This keeps wrong-layer Orgrimmar corner repair active while preventing later long route segments from collapsing to no-path.
- Validation:
  - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~NavigationPathTests.GetNextWaypoint_AcceptsLongLocalPhysicsHorizonHit_WhenRouteLayerRemainsConsistent|FullyQualifiedName~NavigationPathTests.GetNextWaypoint_RejectsShortLocalPhysicsHitWall|FullyQualifiedName~NavigationPathTests.GetNextWaypoint_RepairsLocalPhysicsLayerTrap_WhenDownstreamRampWidthProbeIsNoisy" --logger "console;verbosity=minimal"` -> `passed (3/3)`
  - `$env:WWOW_DATA_DIR='D:\MaNGOS\data'; dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~CornerNavigationTests.Navigate_OrgBankToAH_ArrivesWithoutStall" --logger "console;verbosity=minimal" --results-directory "E:\repos\Westworld of Warcraft\tmp\test-runtime\results-live" --logger "trx;LogFileName=corner_navigation_after_long_horizon_hit_relax.trx"` -> `passed (1/1)`

## Archived Snapshot (2026-04-15) - BR-NAV-008 local physics route-layer repair

- [x] `BR-NAV-008` Stop Orgrimmar corner pathing from bouncing between route layers and stale points.
- Completion notes:
  - `PathfindingClient` now exposes `SimulateLocalSegment(...)` for short-horizon route validation with the same local physics stepping used by the background movement stack.
  - `NavigationPath` rejects service route segments that local physics proves climb onto the wrong WMO/terrain layer.
  - When the rejected segment has a viable nearby same-layer detour, `NavigationPath` inserts that detour instead of dropping the route.
  - The detour repair keeps strict checks on the short detour leg and avoids treating the noisy downstream ramp lateral-width probe as authoritative after local physics/support continuity have already proven the stitch-back leg.
  - Live Orgrimmar bank-to-auction-house validation now arrives instead of looping back over the same corner waypoint.
- Validation:
  - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~NavigationPathTests.GetNextWaypoint_RepairsLocalPhysicsLayerTrap_WhenDownstreamRampWidthProbeIsNoisy|FullyQualifiedName~NavigationPathTests.GetNextWaypoint_RepairsLocalPhysicsLayerTrap_WithNearbySameLayerDetour|FullyQualifiedName~NavigationPathTests.GetNextWaypoint_SelectsAlternate_WhenLocalPhysicsClimbsOffRouteLayer" --logger "console;verbosity=minimal"` -> `passed (3/3)`
  - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~NavigationPathTests" --logger "console;verbosity=minimal"` -> `passed (77/77)`
  - `$env:WWOW_DATA_DIR='D:\MaNGOS\data'; dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~CornerNavigationTests.Navigate_OrgBankToAH_ArrivesWithoutStall" --logger "console;verbosity=minimal" --results-directory "E:\repos\Westworld of Warcraft\tmp\test-runtime\results-live" --logger "trx;LogFileName=corner_navigation_local_physics_detour_width_relax.trx"` -> `passed (1/1)`

## Archived Snapshot (2026-04-15) - BR-NAV-007 waypoint overshoot closeout

- [x] `BR-NAV-007` Prevent path-following oscillation after the bot crosses an active waypoint.
- Completion notes:
  - `NavigationPath` now retains the path start anchor and checks signed progress along the inbound segment for the active waypoint.
  - If the bot has crossed that waypoint, BotRunner advances the waypoint cursor instead of steering back to the stale point.
  - Advancement is still gated by `ShortcutPreservesWalkableCorridor(...)`; strict mode also requires LOS to the next waypoint.
  - The blocked off-corridor overshoot regression remains pinned.
- Validation:
  - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~NavigationPathTests" --logger "console;verbosity=minimal"` -> `passed (72/72)`

## Archived Snapshot (2026-04-14) - Orgrimmar Corner Navigation Closeout

- [x] Closed the live Orgrimmar bank-to-auction-house route blocker that had been left as deferred issue `D5` in the master tracker.
- Completion notes:
  - `CharacterAction.TravelTo` now upserts a persistent `GoToTask`, so travel uses the same route owner as other long-running movement tasks.
  - `NavigationPath` now gives stuck-driven replans a bounded safer-alternate preference and avoids collapsing overlay-aware service routes with a duplicate local dynamic-object segment rejection.
  - The reproduced live corner route now arrives successfully from the street-level bank approach with the current planner behavior.
- Validation:
  - `dotnet build Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false -nodeReuse:false` -> `succeeded`
  - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~NavigationPathTests.GetNextWaypoint_MovementStuckRecoveryPrefersSaferAlternateWithinTolerance|FullyQualifiedName~NavigationPathTests.GetNextWaypoint_DoesNotLocallyRejectOverlayAwareServiceRouteForDynamicSegmentIntersection|FullyQualifiedName~BotRunnerServiceCombatDispatchTests|FullyQualifiedName~BotRunnerServiceGoToDispatchTests" --logger "console;verbosity=minimal"` -> `passed (12/12)`
  - `powershell -ExecutionPolicy Bypass -File .\run-tests.ps1 -CleanupRepoScopedOnly` -> `No repo-scoped processes to stop.`
  - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~CornerNavigationTests.Navigate_OrgBankToAH_ArrivesWithoutStall" --logger "console;verbosity=minimal" --results-directory "E:\repos\Westworld of Warcraft\tmp\test-runtime\results-live" --logger "trx;LogFileName=orgbank_to_ah_corner_navigation_post_overlay_local_dyn_gate_fix.trx"` -> `passed (1/1)`

## Archived Snapshot (2026-04-13) - BR-FISH-001 Ratchet packet parity closeout

- [x] `BR-FISH-001` Keep Ratchet fishing search-walk failures bounded and attributable:
  - Closed the remaining live fishing blocker without changing native shoreline code again.
  - `FishingTask` search-walk now keeps probe travel targets on the waypoint reference layer and no longer counts nearby wrong-layer positions as arrived.
  - `SpellcastingManager` no longer forces fishing casts through destination payloads; BG now sends the same no-target `CMSG_CAST_SPELL` shape the focused FG packet capture uses.
  - The dual live compare is green on the current binaries: `tmp/test-runtime/results-live/ratchet_fg_bg_packet_sequence_compare_after_fishing_cast_packet_fix.trx`.
- Validation:
  - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~FishingTaskTests" --logger "console;verbosity=minimal"` -> `passed (37/37)`
  - `dotnet test Tests/WoWSharpClient.Tests/WoWSharpClient.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~WoWSharpObjectManagerCombatTests" --logger "console;verbosity=minimal"` -> `passed (5/5)`
  - `powershell -ExecutionPolicy Bypass -File .\run-tests.ps1 -CleanupRepoScopedOnly; $env:WWOW_DATA_DIR='D:\MaNGOS\data'; dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~FishingProfessionTests.Fishing_ComparePacketSequences_BgMatchesFgReference" --logger "console;verbosity=minimal" --results-directory "E:\repos\Westworld of Warcraft\tmp\test-runtime\results-live" --logger "trx;LogFileName=ratchet_fg_bg_packet_sequence_compare_after_fishing_cast_packet_fix.trx"` -> `passed (1/1)`

## Archived Snapshot (2026-04-13) - BR-NAV-005 live closeout

- [x] `BR-NAV-005` Preserve walkable-triangle corridor during bot-side smoothing:
  - Closed the remaining live mining-route blocker without changing path generation again.
  - `SpellcastingManager` now latches confirmed melee auto-attack per target, so once the server confirms a swing (`SMSG_ATTACKSTART` / `ATTACKER_STATE_UPDATE`), repeated combat ticks stop resending `CMSG_ATTACKSWING`.
  - Stop/cancel/rejection paths now clear both pending and confirmed melee state, and deterministic regressions pin the no-resend, retry-after-timeout, and clear-on-stop/cancel/error behavior.
  - The reproduced live BG mining route now passes: `tmp/test-runtime/results-live/mining_bg_gather_route_post_melee_confirm_fix.trx`.
- Validation:
  - `dotnet test Tests/WoWSharpClient.Tests/WoWSharpClient.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~WoWSharpObjectManagerCombatTests|FullyQualifiedName~SpellHandlerTests.HandleAttackStart_LocalPlayerConfirmsPendingAutoAttack|FullyQualifiedName~SpellHandlerTests.HandleAttackStop_LocalPlayerClearsPendingAutoAttack|FullyQualifiedName~SpellHandlerTests.HandleCancelCombat_LocalPlayerClearsTrackedAutoAttackState|FullyQualifiedName~SpellHandlerTests.HandleAttackerStateUpdate_OurSwingConfirmsPendingAutoAttack|FullyQualifiedName~WorldClientAttackErrorTests" --logger "console;verbosity=minimal"` -> `passed (13/13)`
  - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~CombatRotationTaskTests|FullyQualifiedName~GatheringRouteTaskTests|FullyQualifiedName~AtomicBotTaskTests" --logger "console;verbosity=minimal"` -> `passed (99/99)`
  - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore --filter "FullyQualifiedName~GatheringProfessionTests.Mining_BG_GatherCopperVein" --logger "console;verbosity=minimal" --results-directory "E:\repos\Westworld of Warcraft\tmp\test-runtime\results-live" --logger "trx;LogFileName=mining_bg_gather_route_post_melee_confirm_fix.trx"` -> `passed (1/1)`

## Archived Snapshot (2026-04-09) - BR-NAV-004 (Slice 2)

- [x] `BR-NAV-004` slice 2: surfaced route affordance decisions to higher-level BotRunner task logic:
  - Added `NavigationRouteDecision` to `NavigationTraceSnapshot` with explicit plan-level metadata (`HasPath`, support flag, max affordance, estimated cost, alternate compare/select, endpoint retarget).
  - `NavigationPath` now records the selected route decision on each `CalculatePath(...)` call.
  - `GoToTask` now emits per-plan route summaries (`[GOTO_ROUTE]`) to diagnostics and logs.
  - `RetrieveCorpseTask` trace summary now includes the route-decision digest.
  - Updated deterministic tests to assert route-decision fields and trace-summary output.
- Validation:
  - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore --filter "FullyQualifiedName~NavigationPathTests|FullyQualifiedName~GoToTaskFallbackTests|FullyQualifiedName~BotRunnerServiceGoToDispatchTests|FullyQualifiedName~RetrieveCorpseTaskTests.FormatNavigationTraceSummary_IncludesKeyFieldsAndTruncatesPathsAndSamples" --logger "console;verbosity=minimal" --results-directory "E:\repos\Westworld of Warcraft\tmp\test-runtime\results"` -> `passed (69/69)`

## Archived Snapshot (2026-04-09) - BR-NAV-004 (Slice 1)

- [x] `BR-NAV-004` slice 1: movement/path consumer route affordance gating in `NavigationPath`:
  - Reject unsupported route candidates (`CliffCount > 0`).
  - Compare alternate path variant and prefer cheaper supported valid route when available.
  - Deterministic coverage added: `NavigationPathTests.GetNextWaypoint_PrefersCheaperSupportedAlternateWhenPrimaryHasCliffSegment`.
- Validation:
  - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore --filter "FullyQualifiedName~NavigationPathTests" --logger "console;verbosity=minimal" --results-directory "E:\repos\Westworld of Warcraft\tmp\test-runtime\results"` -> `passed (61/61)`

## Archived Snapshot (2026-04-09) - BR-NAV-003

- [x] `BR-NAV-003` dynamic-blocker replanning:
  - Added explicit trace reason `dynamic_blocker_observed` in `NavigationTraceReason`.
  - `NavigationPath` now carries dynamic-blocker evidence from segment validation and triggers forced planned replans on a bounded cooldown when no route is available (instead of waiting for stall-loop escalation).
  - Added deterministic coverage in `NavigationPathTests.GetNextWaypoint_TraceRecordsDynamicBlockerDrivenReplanReason`.
- Validation:
  - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore --filter "FullyQualifiedName~NavigationPathTests|FullyQualifiedName~NavigationPathFactoryTests|FullyQualifiedName~PathfindingOverlayBuilderTests|FullyQualifiedName~GoToTaskFallbackTests|FullyQualifiedName~BotRunnerServiceGoToDispatchTests" --logger "console;verbosity=minimal" --results-directory "E:\repos\Westworld of Warcraft\tmp\test-runtime\results"` -> `passed (73/73)`

## Archived Snapshot (2026-04-09) - BR-NAV-001 and BR-NAV-002

- [x] `BR-NAV-001` conservative object overlay filter:
  - Nearby overlay now includes only collision/gameplay-relevant object types (`Door`, `Transport`, `MapObject*`, `DestructibleBuilding`, `TrapDoor`, `MeetingStone`, `FlagStand`, `FlagDrop`, `CapturePoint`).
  - Deterministic allowlist coverage added in `PathfindingOverlayBuilderTests`.
- [x] `BR-NAV-002` movement capability + route policy threading:
  - Added `NavigationPathFactory` profiles (`Standard`, `CorpseRun`) so BotRunner call sites consistently thread `race/gender/capsule` and policy flags into `NavigationPath`.
  - Updated BotRunner navigation call sites to consume shared factory/profile path.
  - Added deterministic coverage in `NavigationPathFactoryTests` for standard smoothing, corpse-run unsmoothed routing, and null-player default capabilities.
- Validation:
  - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore --filter "FullyQualifiedName~PathfindingOverlayBuilderTests|FullyQualifiedName~NavigationPathFactoryTests|FullyQualifiedName~PathfindingClientRequestTests" --logger "console;verbosity=minimal" --results-directory "E:\repos\Westworld of Warcraft\tmp\test-runtime\results"` -> `passed (8/8)`
  - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore --filter "FullyQualifiedName~NavigationPathTests|FullyQualifiedName~GoToTaskFallbackTests|FullyQualifiedName~BotRunnerServiceGoToDispatchTests" --logger "console;verbosity=minimal" --results-directory "E:\repos\Westworld of Warcraft\tmp\test-runtime\results"` -> `passed (66/66)`

## Archived Snapshot (2026-02-23 09:27:22) - Exports/BotRunner/TASKS.md

# BotRunner Tasks

## Scope
Shared bot action sequencing, behavior trees, task semantics, and snapshot mapping.

## Rules
- Execute without approval prompts.
- Work continuously until all tasks in this file are complete.
- Keep task behavior deterministic and snapshot-verifiable.

## Active Priorities
1. Death/corpse behavior
- [x] Keep `RetrieveCorpseTask` strictly pathfinding-driven (no direct fallback).
- [x] Ensure reclaim-delay waiting/retry behavior matches live server timing.
- [x] Align release/retrieve transitions with strict life-state checks.
- [x] Add explicit task-stack diagnostics for push/pop reason codes (`ReleaseCorpseTask`, `RetrieveCorpseTask`) to speed future parity triage.
- [x] Use descriptor-backed ghost/dead state (player flags + stand state) for death-recovery decisions so task scheduling is not blocked by `InGhostForm` drift.
- [x] Make corpse-retrieval movement/reclaim gating horizontal-distance based (`DistanceTo2D`) and clamp corpse navigation Z when corpse Z is implausible vs ghost Z.
- [ ] Add explicit `RetrieveCorpseTask` diagnostics in FG-friendly output (not only Serilog) so reclaim/send cadence is visible in passing test logs.

2. Action sequencing and guards
- [x] Keep dead-state send-chat guards consistent with test setup requirements.
- [x] Simplify death-command sequencing to a deterministic `.kill` -> `.die` fallback path without capability-probe spam.
- [ ] Ensure movement/action tasks do not overwrite long-running gather/corpse actions.
- [x] Move `Goto` behavior to pathfinding-driven waypoint movement (no direct steering by default), with explicit no-route stop/wait handling to avoid stuck-forward loops.
- [ ] Tune `Goto` no-route retry/log behavior (`[GOTO] No route ...`) so BG follow loops remain observable without log spam.

3. Snapshot field completeness
- [x] Removed Lua ghost-bit OR fallback in `BuildPlayerProtobuf` so life-state assertions come from descriptor-backed snapshot fields.
- [x] Serialize quest-log snapshot data (`WoWPlayer.QuestLogEntries`) from `IWoWPlayer.QuestLog` for live quest-state assertions.
- [x] Serialize nearby-unit identity fields (`GameObject.Entry`, `GameObject.Name`) in `BuildUnitProtobuf` for deterministic live target classification.
- [ ] Keep all parity-critical fields mapped and serialized consistently for FG and BG.
- [ ] Fix live combat target-visibility parity: `Player.Unit.TargetGuid` can remain unset/stale during successful `StartMeleeAttack` engage (BG/FG), causing snapshot-observability drift from real in-game target state.
- [ ] Audit FG nearby-unit name completeness (`<unknown>` names in live scans) and either fix source mapping or ensure fallback identity fields are always populated for target classification.
- [ ] Add targeted diagnostics when ghost corpse-run movement stalls (e.g., movement flags stuck at non-moving values like `0x10000000`) to distinguish pathfinding no-route vs controller/root-state issues.
- [ ] Add corpse-position snapshot serialization (`WoWPlayer.corpsePosition`) so death tests/tasks do not rely on last-alive fallback when server transitions directly to ghost.

## Session Handoff
- Last bug/task closed:
  - `RetrieveCorpseTask` now uses horizontal corpse distance for run/reclaim gating and clamps corpse-nav Z when corpse Z delta is extreme.
  - Fixed FG corpse-retrieval churn by hardening `RetrieveCorpseTask` against transient ghost-state flicker and ensuring FG always provides a non-null `PathfindingClient` to `ClassContainer`.
  - Simplified death-command dispatch in live fixture to avoid dead-state chat spam and removed capability-probe dead code.
  - Snapshot mapping now relies on descriptor `PlayerFlags` only (removed local Lua ghost-bit injection path).
  - Death-recovery scheduling now uses descriptor-backed ghost/corpse detection (`PLAYER_FLAGS_GHOST`, stand-state dead) instead of relying solely on `InGhostForm`.
  - Added explicit task push/pop diagnostics (`[TASK-PUSH]`, `[TASK-POP]`) for release/retrieve/teleport task transitions.
  - `SendChat` dead/ghost guard and resurrect sequence checks now use descriptor-backed death-state helper.
  - Added quest-log serialization in `BuildPlayerProtobuf` (`QuestLogEntries`) so LiveValidation can assert quest add/remove transitions from snapshots.
  - Added nearby-unit `Entry`/`Name` snapshot serialization so combat tests can force boar-only targeting using snapshot state.
- Validation tests run:
  - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --filter "FullyQualifiedName~DeathCorpseRunTests" --logger "console;verbosity=normal"` (latest: `Passed`, ~2m15s)
  - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --filter "FullyQualifiedName~DeathCorpseRunTests" --logger "console;verbosity=normal"`
  - Result set in this session:
    - `Passed` (~2m10s) after descriptor/death-recovery updates.
    - one rerun `Skipped` due live fixture precondition state.
    - one rerun `Failed` (intermittent FG corpse-run stall persisted: `dist=127.8`, `step=0.0`, `moveFlags=0x10000000`).
    - subsequent rerun `Passed` (~2m10s).
- Files changed:
  - `Exports/BotRunner/Tasks/RetrieveCorpseTask.cs`
  - `Exports/BotRunner/Tasks/BotTask.cs`
  - `Exports/BotRunner/Tasks/ReleaseCorpseTask.cs`
  - `Exports/BotRunner/Tasks/RetrieveCorpseTask.cs`
  - `Exports/BotRunner/Tasks/TeleportTask.cs`
  - `Exports/BotRunner/BotRunnerService.cs`
  - `Exports/BotRunner/BotRunnerService.ActionDispatch.cs`
  - `Exports/BotRunner/BotRunnerService.Sequences.Combat.cs`
  - `Exports/BotRunner/BotRunnerService.Sequences.Movement.cs`
- Next task:
  - Continue isolating intermittent corpse-run stalls with new task-stack diagnostics and movement-flag context (`moveFlags`, reclaim delay, path/no-path) across repeated live runs.
  - Correlate FG `RetrieveCorpseTask` route availability with the pathfinding service when stall signature appears (`dist=127.8`, no movement ticks).
  - Close combat snapshot parity gap where live targeting succeeds but `Player.Unit.TargetGuid`/unit identity can be stale in snapshots during melee engage.

## Archive
Move completed items to `Exports/BotRunner/TASKS_ARCHIVE.md`.




## Archived Snapshot (2026-02-24 19:43:32) - Exports/BotRunner/TASKS.md

- [x] `RetrieveCorpseTask`: route/probe resolution runs before stall recovery.
- [x] `RetrieveCorpseTask`: stall detection uses horizontal movement intent.
- [x] `RetrieveCorpseTask`: nested recovery is suppressed during unstick maneuvers.
- [x] `RetrieveCorpseTask`: no-path fallback drives toward corpse before timeout abort.

## Archived Snapshot (2026-04-15) - BG gather server retry parity

- [x] `GatheringRouteTask` stop/use/cast sequence now matches the live BG server trigger path.
  - `ForceStopImmediate()` is followed by node facing/use before the delayed gather cast.
  - Retryable `SPELL_FAILED_TRY_AGAIN` errors schedule a bounded same-node retry instead of abandoning the visible node.
  - No-loot after a still-visible active node also retries within the bounded gather attempt budget.
- Validation:
  - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~GatheringRouteTaskTests" --logger "console;verbosity=minimal"` -> `passed (12/12)`
  - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~GatheringRouteTaskTests|FullyQualifiedName~GatheringRouteSelectionTests" --logger "console;verbosity=minimal"` -> `passed (22/22)`
  - `$env:WWOW_DATA_DIR='D:\MaNGOS\data'; $env:WWOW_TEST_PRESERVE_EXISTING_PATHFINDING='1'; $env:WWOW_ENABLE_RECORDING_ARTIFACTS='1'; dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~GatheringProfessionTests.Herbalism_BG_GatherHerb" --logger "console;verbosity=minimal" --results-directory "E:\repos\Westworld of Warcraft\tmp\test-runtime\results-live" --logger "trx;LogFileName=herbalism_bg_retry_try_again.trx"` -> `passed (1/1)`

