# Iter 2 findings — OG↔UC zep is non-pathfinding (stop condition #4)

Session executed iter-2 plan from
[NEXT_SESSION_LONGPATHING_HANDOFF_ITER2.md](NEXT_SESSION_LONGPATHING_HANDOFF_ITER2.md)
in order B → A as recommended.

## Failure B: `OrgrimmarToUndercityZeppelin_BoardsAndDeplanes` — confirmed non-pathfinding

**Stop condition #4 triggered.** Off-mesh additions cannot fix this; surface to user for separate work.

### Evidence (baseline run 2026-05-30T20:08–20:17 UTC; pre-iter2 bake on tile 40,28)

- Bot staged correctly at OG boarding coord (1320.142944, -4653.158691, 53.891945) on map 1.
- `TravelTask` correctly planned the Zeppelin leg:
  `[FG:CHAT] [TRAVEL_LEG] start index=0 type=Zeppelin map=1 end=(2066.9,290.1,97.0)`.
- Bot remained in `TransportPhase.WaitingForArrival` for 5m19s (319s / 420s budget),
  emitting 88 `[TRAVEL_TRANSPORT]` chat traces — ALL with `near=0;transport=0x0`.
- The expected transport (entry **164871**, OG↔UC zep) **never appears** in the bot's
  `nearbyObjects` feed across the entire wait window.
- The bot's `ObjectManager` correctly observes other transports:
  the OG↔Grom'gol zep (entry 175080, same displayId 3031) appears continuously,
  tracked from (1333,-4577,73.9) drifting south to (1355,-4623,71.5) over the wait.
  `TransportObjectIdentity.MatchesTransport` correctly rejects it on entry mismatch.
- After 5m19s `FailIfZeppelinBoardingLost` fires with "The Orgrimmar -> Undercity zeppelin
  was detected at the dock, but the bot missed boarding before the transport left."
  But the trx contains **zero** `[TRAVEL_TRANSPORT_MISSED_BOARDING]` chat emits — the
  predicate likely picked up a stale chat msg from a prior test run that lingered in
  `RecentChatMessages` and wasn't in `diagnosticBaseline`.

### Root cause — vmangos transport schedule phase

`mangos.transports` table shows entry 164871 with `period=360016ms` (6 min).
Container log on startup: `Created transport 164871 on map 0.` — the zep starts its
cycle at the UC side. Whether `period` is per-direction (12-min total round trip) or
total round-trip determines whether a 7-min wait reliably catches an OG dock arrival.
The bot's snapshot/ObjectManager works correctly (proven by 175080 visibility); the
issue is server-side scheduling, not bot code.

### Recommended fixes (not pathfinding; out of scope for iter 2)

1. **Cheapest:** extend `OrgrimmarUndercityZeppelinDockWaitSeconds` from 120 → 540 in
   [Tests/BotRunner.Tests/LiveValidation/LongPathingTests.cs:139](../../../Tests/BotRunner.Tests/LiveValidation/LongPathingTests.cs) —
   total `ZeppelinTransferEvidenceTimeout` becomes 300 + 540 = 840s (14 min), enough
   to cover one full possible cycle regardless of `period` semantics.
2. **Cleanest:** add a `WaitForZeppelinAtDockAsync(164871, timeoutSeconds=480)` helper
   that polls `nearbyObjects` for `entry=164871 && IsTransportAtStop` before dispatching
   `TravelTo`. The test then dispatches knowing the zep is currently at OG.
3. **Confirm hypothesis:** re-run the test 3 times with the existing 7-min window;
   if it sometimes passes and sometimes fails the same way, that's the schedule-phase
   confirmation. If it ALWAYS fails, dig into vmangos's transport spawn code.
4. **Fix the stale chat buffer leak:** the misleading "missed boarding" message is
   itself a test-infrastructure bug — `FailIfZeppelinBoardingLost` should not fire
   from `[TRAVEL_TRANSPORT_MISSED_BOARDING]` chat msgs older than the test's start.

### Why I did not pursue any of the above

Per the handoff:
> Failure B (`OrgrimmarToUndercity`) is confirmed to be a non-pathfinding problem ...
> surface to user with the diagnostic, do NOT add off-mesh entries trying to fix it.

This file is the surface. Iter 2 continued to Option A (CrossroadsToUndercity) per
the handoff's fallback ordering.

## Failure A: `CrossroadsToUndercity_UsesFlightAndZeppelin` — iter 2 off-mesh REGRESSED OG zep fixture, REVERTED

### What iter 2 tried

- Added off-mesh `1 40,28 (1608.100 -4382.300 10.500) (1608.100 -4420.000 11.500) 2.5`
  to [tools/MmapGen/offmesh.txt](../../../tools/MmapGen/offmesh.txt) (OG-interior z=10
  sub-floor pocket bypass — the new stall coord iter-1 surfaced after closing the
  eastern-approach climb).
- Baked tile (40,28) → 0012840.mmtile (2,107,876 → 2,108,048 bytes, +172).
  Off-mesh registered: `loadOffMeshConnections:: Found offmesh connection for map 1
  tile [40,28]: (1608.10 -4382.30 10.50) -> (1608.10 -4420.00 11.50) size 2.50`.
- Post-bake `PathPhysicsProbe --detour-resolve --smooth` showed the route shrank from
  25 corners with `idx=22 SteepClimb` to **12 corners** with `idx=10 StepUp Clear` —
  same indirect-value mechanism iter 1 exploited.

### What broke (per handoff stop condition #3)

The mandatory bake-fixture pair regression check FAILED:
- `BrmDungeon_BakeFixtureValidation` — 🟢 PASS 33s
- `OgZeppelin_BakeFixtureValidation` — 🔴 **FAIL 4m21s**, multiple `TELEPORT_FAILED`:
  - `stall-coord-z51.6`, `approach-pos-z51.6`, `smooth-wp00-z51.7`,
    `smooth-wp02-z53.5-LIP`, `smooth-wp03/04/05-z53.5` — all "FG bot never produced
    a settled position after teleport" on OG zep deck (tile 40,29 coords).
  - `boarding-pos-z53.9` — "FG settled XY drifted 11.58y from teleport target
    (tolerance 8y)."
  - `smooth-wp06-z54.4` — settled OK at (1329.10, -4648.60, 54.44).

### Why iter 2 broke OG zep but iter 1 didn't

**Tile adjacency to the OG zep deck (tile 40,29):**
- iter 1's tile (39,28) is **diagonally adjacent** to (40,29) — shares no border.
- iter 2's tile (40,28) **directly borders** (40,29) along the WoW X=1600 line.

The iter-2 bake's cull heuristics (`[POLY-CULL] disabled 302 suspicious mixed-wall
polygon(s)` + `[DT-POLY-CULL] disabled 2279 final suspicious Detour polygon(s)`)
affected the cross-tile portal seam at X=1600, which propagated into the OG zep
deck tile (40,29)'s connectivity. The OG zep deck checkpoints at z=51-53 are
within the propagation blast radius.

### Revert

- `D:\wwow-bot\test-data\mmaps\0012840.mmtile` restored from
  `0012840.mmtile.preiter2-20260530T203427Z.bak` (back to 2,107,876 bytes).
- Promoted to prod-data; `wwow-pathfinding` + `wwow-scene-data` restarted.
- Re-run of bake-fixture pair: **2/2 PASS** in 3m42s combined. Revert confirmed.
- offmesh.txt entry deleted per R18 (no "// removed in iter N" comment).

### What's next for failure A

This was iter 2 of 3 allowed per the handoff's stop condition #2 ("3 consecutive
iterations with no progress"). iter 1 made 230y of progress; iter 2 was a
regression revert. Net: still one viable iteration on the off-mesh budget.

Three options for iter 3:

1. **Different off-mesh placement on tile (40,28) with smaller cull blast.**
   - Smaller `size` radius (1.0 instead of 2.5) — may register without triggering
     the aggressive Detour cull.
   - Endpoint placement farther from the (40,28)↔(40,29) border (X=1600). The
     current endpoint at X=1608.1 is only 8y from the border. Try endpoints
     deeper in tile (40,28) (e.g., X=1620 or X=1640).
   - Different endpoint Z layer. The bot stalls at z=10; the bake's "wall" is in
     the z=10..28 column. An off-mesh whose endpoint is at z=20 (upper-floor
     pocket above the stall coord) instead of z=11.5 (south-of-wall pocket on
     z=10) might trigger different cull paths.

2. **Escalate to Option C (NavigationPath recovery branch).** Iter 1's evidence
   plus iter 2's regression both point at the multi-stall corridor pattern as the
   structural issue. A `BotRunner.NavigationPath` patch that handles wall-collision
   creep with backoff-and-reroute would close ALL multi-stall corridors generally,
   not just this one. Per the handoff, this is the recommended long-term fix.

3. **Accept stop condition.** iter 1 made real progress (230y); iter 2 confirmed
   off-mesh whack-a-mole has diminishing returns near sensitive tile boundaries.
   Surface to the user and stop pathfinding work for this loop; CrossroadsToUndercity
   waits for Option C work.

My recommendation: **Option 2 (escalate to C)** is the highest-leverage move. The
multi-stall corridor pattern is structural; off-mesh whack-a-mole's blast radius
risk grows with each iteration. A NavigationPath recovery branch closes the entire
class of failures in one change.

## See also

- [NEXT_SESSION_LONGPATHING_HANDOFF.md](NEXT_SESSION_LONGPATHING_HANDOFF.md) (iter 1 prompt)
- [NEXT_SESSION_LONGPATHING_HANDOFF_ITER2.md](NEXT_SESSION_LONGPATHING_HANDOFF_ITER2.md) (iter 2 prompt)
