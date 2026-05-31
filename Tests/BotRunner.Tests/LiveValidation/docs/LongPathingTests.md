# LongPathingTests

Focused staged long-pathing validation for routes that require more than one
travel mode.

## Bot Execution Mode

**Shodan-directed FG action** - `LongPathing.config.json` launches `LPATHFG1`
as a Tauren Male foreground Warrior action target, `LPATHBG1` idle for
topology parity, and SHODAN as director. The test body dispatches only
`ObjectiveType.TravelTo` to the Tauren foreground target after fixture-owned
Crossroads staging and taxi readiness.

The test scopes `Injection__DisablePacketHooks=true` and
`WWOW_DISABLE_PACKET_HOOKS=1` for the full run because the Orgrimmar ->
Undercity zeppelin is a foreground cross-map world transfer, matching the
packet-hook crash guard used by dungeon and battleground transfer fixtures.

## Test Methods

- `CrossroadsToUndercity_UsesFlightAndZeppelin`: stages the Horde target at
  Crossroads, grants taxi access through the existing fixture helper, dispatches
  `TravelTo` to the Undercity target, and requires evidence for the staged
  route: Crossroads taxi `25 -> 23`, Orgrimmar taxi arrival, walking to the
  Orgrimmar zeppelin tower, actual zeppelin transport state or map transfer,
  Eastern Kingdoms arrival, and final proximity to the Undercity destination.

## Fast-Fail Blockers

The Orgrimmar flight-master -> zeppelin walk now fails quickly when the live
route enters known bad object/terrain states instead of waiting for the broad
walk timeout:

- Bonfire/object choke after taxi landing near `(1673,-4334,53)`.
- Palm-tree descent collision near `(1605,-4425,10)`.
- Steep-incline route selection, including `[TRAVEL_WALK_NAV]` diagnostics
  with `afford=SteepClimb` for the zeppelin walk target.
- Tower support/flagpole object collision near `(1371,-4439,31)`.
- Tower base/deck mismatch near `(1343,-4641,25)`, including diagnostics with
  `nav=False`, `resolution=no_route`, and `active=none`.

The tower approach success check also requires deck-ish Z before the test moves
on to the zeppelin leg; ground-level proximity to `(1341.0,-4638.6)` is no
longer enough.

## Offline Route Gate

Live Crossroads -> Undercity validation is gated by
`PathfindingService.Tests.LongPathingRouteTests.OrgrimmarFlightMasterToZeppelinRoute_AvoidsKnownStaticObjectBlockers`.
That test calculates the Orgrimmar flight-master -> zeppelin walking route
with the Tauren Male capsule and fails before launching WoW if the generated
path clips:

- Lower flight-master bonfire:
  `guid=10975 entry=177026 display=4572 pos=(1665.50,-4360.83,26.66)`.
- Bank-front palm/static model snag near `(1605.00,-4425.20,10.20)`.
- Bank-front bonfire:
  `guid=10090 entry=177019 display=4572 pos=(1592.37,-4427.32,8.05)`.
- Z-hallway early-cut north and south corners near
  `(1513.20,-4415.90,20.00)` and `(1415.30,-4372.90,25.30)`.
- Exterior steep incline near `(1383.00,-4385.00,28.00)`.
- Exterior rope-line support near `(1371.10,-4439.40,30.90)`.

As of May 1, 2026, the offline gate is red against `D:\MaNGOS\data` after
removing an invalid PathfindingService static-clearance workaround. Live
Crossroads -> Undercity validation remains paused until regenerated GO-aware
mmaps make normal pathfinding avoid these blockers.

Do not make this gate pass by adding route-specific production code: no
hardcoded Orgrimmar clearance cylinders, no waypoint exceptions, and no
live-position guard as a substitute for static collision in the generated
navmesh. The live blocker guard is diagnostic only; the fix belongs in
gameobject export, mmap generation, and regenerated map data.

## Runtime Linkage

- `TravelTo` now queues `TravelTask` for cross-map destinations.
- `CrossMapRouter` must plan the route as staged objectives instead of a
  single direct path or a Ratchet/Booty Bay neutral shortcut.
- Walk legs must be resolved through PathfindingService with the configured
  Tauren Male capsule. `[TRAVEL_WALK_NAV]` diagnostics include the resolved
  agent race/gender and capsule so live failures can be tied to the exact
  request metadata.
- Flight-path activation uses the existing object-manager taxi packet path.
- Zeppelin handling uses `TransportWaitingLogic` and snapshot evidence for the
  Orgrimmar/Undercity route. Nearby zeppelin objects are diagnostic only; the
  assertion requires `TransportGuid` / `ONTRANSPORT` or map transfer evidence,
  and the transfer-evidence wait is long enough to cover the scheduled
  transport wait budget. Transport diagnostics include the expected
  gameobject entry/display/name and nearest objects formatted with GUIDs, with
  static (`0xF120`) and moving (`0x1FC0`) transport GUIDs decoded back to
  route entries before matching.
- Failure screenshots are captured from the managed WoW.exe PID for the target
  account; desktop or unrelated game-window fallbacks are intentionally not
  accepted as evidence.
