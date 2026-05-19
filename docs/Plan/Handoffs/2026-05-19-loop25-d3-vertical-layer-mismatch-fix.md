# loop-25 D3 — `vertical_layer_mismatch` recovery escalation

> **Handoff source:** PCE implementation loop iter 4 (2026-05-19).
> **Predecessor:** [D2 DIAG memory `project_pfs_loop25_phase_d2_vertical_layer_mismatch`] + this brief (research via `monorepo-explorer` agent).
> **Frozen surfaces (do NOT touch):** `tools/MmapGen/`, `Services/PathfindingService/Repository/Navigation.cs`, `Exports/Navigation/`.

---

## §1 Problem

On `ClimbOrgrimmarZeppelinTowerRampToFrezza` after the D1 guard cleared the original OG zep doodad column, the run times out at `(1249.2,-3902.3,18.3)` on a long alternate route. Evidence: 5 consecutive snapshots in `tmp/test-runtime/screenshots/long-pathing/timeline/ClimbOrgrimmarZeppelinTowerRampToFrezza/03-final-LPATHFG1-20260519T143904Z.json:34-38` all show `reason=vertical_layer_mismatch resolution=waypoint plan=7 smooth=False` with `hitWall=False blocked=1.00 wall=(0,0)` on flat ground (z=16-19y). The recovery `TryReplanFromNearVerticalLayerMismatch` fires repeatedly without converging — there is **no consecutive-fire counter** to escalate.

---

## §2 Producer / consumer sites

| Role | File:line |
|---|---|
| Producer (single-fire) | [`Exports/BotRunner/Movement/NavigationPath.cs:1260`](../../Exports/BotRunner/Movement/NavigationPath.cs#L1260) — `TryReplanFromNearVerticalLayerMismatch` → `CalculatePath(..., reason: VerticalLayerMismatch)` |
| Producer (long-travel branch) | [`NavigationPath.cs:1244`](../../Exports/BotRunner/Movement/NavigationPath.cs#L1244) — `TryPromoteLongTravelDestinationProgressWaypoint(..., traceReason: VerticalLayerMismatch)` |
| Sole call site | [`NavigationPath.cs:806`](../../Exports/BotRunner/Movement/NavigationPath.cs#L806) inside `GetNextWaypoint` |
| Resolution tag emit | [`NavigationPath.cs:1019`](../../Exports/BotRunner/Movement/NavigationPath.cs#L1019) — `RecordWaypointResult(..., TRACE_RESOLUTION_WAYPOINT)` |
| Cooldown gate | [`NavigationPath.cs:633`](../../Exports/BotRunner/Movement/NavigationPath.cs#L633) `_lastVerticalLayerReplanTick` + [`NavigationPath.cs:571`](../../Exports/BotRunner/Movement/NavigationPath.cs#L571) `RECALCULATE_COOLDOWN_MS=2000` checked at [`NavigationPath.cs:1253-1257`](../../Exports/BotRunner/Movement/NavigationPath.cs#L1253-L1257) |
| Existing `preferSmoothPath=false` flip | [`NavigationPath.cs:3771-3793`](../../Exports/BotRunner/Movement/NavigationPath.cs#L3771-L3793) — already fires for VLM, so the rebuild is "safer alternate"; but the next tick re-trips `horizontal<=2.5y && upwardDelta>1.25y` predicate at [`NavigationPath.cs:1226-1227`](../../Exports/BotRunner/Movement/NavigationPath.cs#L1226-L1227) |

---

## §3 Existing resolution alternatives

Defined at [`NavigationPath.cs:618-624`](../../Exports/BotRunner/Movement/NavigationPath.cs#L618-L624):

- `TRACE_RESOLUTION_WAYPOINT` — normal advance.
- `TRACE_RESOLUTION_WALL_DEFLECT` — geometric wall-normal deflect ([`:1009`](../../Exports/BotRunner/Movement/NavigationPath.cs#L1009)).
- `TRACE_RESOLUTION_DIRECT_FALLBACK` — straight line when no route ([`:803`](../../Exports/BotRunner/Movement/NavigationPath.cs#L803)/[`:826`](../../Exports/BotRunner/Movement/NavigationPath.cs#L826)/[`:972`](../../Exports/BotRunner/Movement/NavigationPath.cs#L972)).
- `TRACE_RESOLUTION_DIRECT_RECOVERY` — TTL'd direct route after stuck ([`:985`](../../Exports/BotRunner/Movement/NavigationPath.cs#L985), gated by `DIRECT_RECOVERY_TTL_MS=15s`).
- `TRACE_RESOLUTION_NO_ROUTE` — terminal fail.
- `TRACE_RESOLUTION_TRANSPORT_*` — ship/zeppelin special.

No `replan`-tagged resolution exists; the closest analog is `MovementStuckRecovery` reason fed back into `CalculatePath`. Compare existing consecutive-fire tracking at [`NavigationPath.cs:838`](../../Exports/BotRunner/Movement/NavigationPath.cs#L838) `_consecutiveWallHitSamples` + [`NavigationPath.cs:598`](../../Exports/BotRunner/Movement/NavigationPath.cs#L598) `STUCK_RECOVERY_REPEAT_PROMOTION_THRESHOLD=2`.

---

## §4 Proposed fix (~12 LOC + 3 fields + 1 reset hook)

**Target:** [`Exports/BotRunner/Movement/NavigationPath.cs:1252-1267`](../../Exports/BotRunner/Movement/NavigationPath.cs#L1252-L1267) inside `TryReplanFromNearVerticalLayerMismatch`.

New fields near line 633:

```csharp
private int _consecutiveVerticalLayerReplans;
private Position? _lastVerticalLayerReplanPosition;
```

Reset hook in `CalculatePath` (line 3740 block): reset to 0 whenever `reason != VerticalLayerMismatch`. Also reset in the `WaypointsReached` / `IncrementCorridorAdvances` paths so normal long routes that hit one mismatch per few hundred waypoints don't spuriously escalate over a 20+ minute traversal (per Unknown in §5 below).

In `TryReplan...` after the cooldown check:

```csharp
const int VERTICAL_LAYER_ESCALATION_THRESHOLD = 3;
bool sameZBand = _lastVerticalLayerReplanPosition is { } prev
    && MathF.Abs(prev.Z - currentPosition.Z) <= WAYPOINT_VERTICAL_LAYER_DRIFT_TOLERANCE
    && prev.DistanceTo2D(currentPosition) <= STUCK_RECOVERY_PROMOTION_MAX_DISTANCE;
_consecutiveVerticalLayerReplans = sameZBand ? _consecutiveVerticalLayerReplans + 1 : 1;
_lastVerticalLayerReplanPosition = currentPosition;

if (_consecutiveVerticalLayerReplans >= VERTICAL_LAYER_ESCALATION_THRESHOLD)
{
    _consecutiveVerticalLayerReplans = 0;
    _waypoints = [];  // hard reset; bypasses cooldown on fresh-path branch
    CalculatePath(currentPosition, destination, mapId, force: true,
        reason: NavigationTraceReason.MovementStuckRecovery);
    AdvanceReachableWaypoints(currentPosition, mapId, minWaypointDistance);
    waypoint = _currentIndex < _waypoints.Length ? _waypoints[_currentIndex] : null;
    return true;
}
```

Repurposes `MovementStuckRecovery` — reuses the existing safer-alternate logic at line 3771-3793 without introducing a new `TRACE_RESOLUTION_REPLAN` constant.

---

## §5 Constraints / Risks / Unknowns

- **Constraint:** fix must stay in `Exports/BotRunner/Movement/NavigationPath.cs` — `Navigation.cs` 5,600-LOC repair pipeline is frozen.
- **Risk (low):** `MovementStuckRecovery` triggers `preferSaferAlternateOnReplan` ([line 3772](../../Exports/BotRunner/Movement/NavigationPath.cs#L3772)) which flips `preferSmoothPath=false` — same as VLM already sets at 3774. No new behavior surface.
- **Risk (low):** `_waypoints = []` clear is stronger than existing path; `force=true` already bypasses cooldown so the clear is a safety-belt. Could omit if regression risk surfaces.
- **Unknown 1:** Dump evidence is single-bot / single-route. The "5 consecutive snapshots, indices 91→98→103→108→113" cadence has the bot advancing 5-7 waypoints between each VLM fire. The "consecutive" definition MUST reset on clean waypoint advance — otherwise normal long routes that hit one mismatch per few hundred waypoints would spuriously escalate. Counter reset belongs in `WaypointsReached` / `IncrementCorridorAdvances`.
- **Unknown 2:** Whether the `plan=7 smooth=False` state already matches a `MovementStuckRecovery` rebuild firing upstream. If so, escalation to `MovementStuckRecovery` is a no-op. May need to ALSO toggle off the vertical-layer gate temporarily on escalation, otherwise next-tick predicate at line 1226-1227 still triggers.

---

## §6 Test plan

Existing coverage in [`Tests/BotRunner.Tests/Movement/NavigationPathTests.cs`](../../Tests/BotRunner.Tests/Movement/NavigationPathTests.cs):
- Line 2257-2261 (`...LongTravelKeepsSupportedUphillLayerProgression...`) asserts single-fire trace tag.
- Line 2351-2353 (`...LongTravelVerticalMismatchPromotesExistingCorridor...`) asserts single-fire promotion.
- **No test asserts behavior on the 2nd, 3rd, ... consecutive fire.**

D3 new test (sibling Fact near `NavigationPathTests.cs:2310`):
```
GetNextWaypoint_VerticalLayerMismatch_EscalatesToReplanAfterThreeConsecutiveFires
```
Pattern: `DelegatePathfindingClient` returning a 3-waypoint stub that always presents `horizontal<=2.5y && upwardDelta>1.25y` at the head; bump `_tickProvider` by >2000ms between each `GetNextWaypoint` call to clear cooldown; assert `trace.LastReplanReason == MovementStuckRecovery` on the 3rd call and `pathfindingCalls` increments fresh on escalation.

Regression sweep after IMPL (acceptance):
- `NavigationPathTests` — baseline 112/0/7 must hold.
- `PathfindingService.Tests` full sweep — baseline 23/0 must hold.
- `OgZeppelinCliffFallParityTests` — baseline 4/0 must hold.
- `RecordedTests.PathingTests` — baseline 135/0 must hold.
- LiveValidation `ClimbOrgrimmarZeppelinTowerRampToFrezza` — D2 timeout near (1249.2,-3902.3,18.3) clears; bot reaches (1331.1,-4649.5,53.6) without timing out.

---

## §7 Implementation owner

Next CODEX-IMPL slot OR Claude direct (the brief is detailed enough to translate to code without further investigation). Pick based on Codex runtime health at iter 5 dispatch time. Per [[feedback-codex-rescue-result-recovery]], if Codex used, verify via `codex-companion.mjs status` post-dispatch.
