# loop-25 D5 — OG sea-level `no_route` surface at `(1601.2,-4455.3,7.7)`

> **Handoff source:** PCE implementation loop iter 1 (new session, 2026-05-19), live validation of D3+D4 against `ClimbOrgrimmarZeppelinTowerRampToFrezza`.
> **Predecessor:** [D3 fix](2026-05-19-loop25-d3-vertical-layer-mismatch-fix.md). D3 escalation works as designed — bot now clears `(1249.2,-3902.3,18.3)` and travels ~1330y further along the route before stalling at the new surface.
> **Frozen surfaces (do NOT touch):** `tools/MmapGen/`, `Services/PathfindingService/Repository/Navigation.cs`, `Exports/Navigation/`.

---

## §1 Problem

Live test result on commit `92634895` (D4 on top of D3 `7cbfeb09`):

- **Status:** FAIL — `Failed: 1, Passed: 0, Skipped: 0` after 1m 45s wall (well under 15min cap, fail-fast OK).
- **Stall coord:** `(1601.2, -4455.3, 7.7)` on map 1 (Kalimdor, OG city sea-level approach).
- **Target:** `(1331.1, -4649.5, 53.6)` — Frezza zeppelin master atop the OG zep tower (~270y XY remaining).
- **Mode:** `currentSpeed=0 movementFlags=0` for 4+ consecutive 5s polls (20s `SnapshotStallGuard` window fires).
- **PathfindingService verdict:** `no_route` deterministic across **29+ consecutive** `CalculatePath` calls (plan ids 27→29 all `path_unavailable`).
- **D2/D3 surface CLEARED:** the bot got past `(1249.2,-3902.3,18.3)` (D2 stall coord) — D3 escalation worked. New surface is at a *different* tile and a *different* failure class.

### Distinct from D2

| Aspect | D2 surface (closed) | D5 surface (this brief) |
|---|---|---|
| Coord | `(1249.2,-3902.3,18.3)` | `(1601.2,-4455.3,7.7)` |
| Failure class | `vertical_layer_mismatch resolution=waypoint plan=7` (replan loop, advanced WP idx 91→113) | `path_unavailable resolution=no_route plan=27→29` (CalculatePath returns no_route entirely) |
| Bot motion | replanning, advancing waypoints, stalling at z-band mismatch | `currentSpeed=0`, no replans advancing, terminal `no_route` |
| Tile region | `~(40, 29)` (loop-24 trap region) | tile containing `(1601.2,-4455.3,7.7)` — **distinct tile**, needs map-coord-to-tile conversion |

### Trace tail (last 5 poll snapshots)

```
[TRAVEL_WALK_NAV] nav=False reason=path_unavailable resolution=no_route plan=27 idx=0 activeDist=NaN
[TRAVEL_WALK_STALL] count=5 replanned=False anchor==current moved=0.0 plan=27 idx=0
[TRAVEL_WALK_NAV] reason=stalled_near_waypoint resolution=no_route plan=28
[TRAVEL_WALK_NAV] reason=path_unavailable resolution=no_route plan=29
…61 prior samples all `no_route:dNaN->none`
```

---

## §2 Artifacts (absolute paths)

| Artifact | Path |
|---|---|
| Failure screenshot | `tmp/test-runtime/screenshots/long-pathing/Long-travel-stall-before-OG-zeppelin-tower-ramp-climb-stalled-before-reaching-Fr-LPATHFG1-client-28940-win0-20260519_205727.png` |
| Final stall snapshot JSON | `tmp/test-runtime/screenshots/long-pathing/timeline/ClimbOrgrimmarZeppelinTowerRampToFrezza/02-climb-poll-00170-LPATHFG1-20260520T005721Z.json` |
| Console log | `tmp/test-runs/loop25-d-validate/climb-20260520T005401Z.console.log` |
| TRX | `tmp/test-runs/loop25-d-validate/climb-20260520T005401Z.trx` |
| Test summary | `tmp/test-runs/loop25-d-validate/test-summary.json` |
| Timeline (240 wp-* + 17 poll-* files) | `tmp/test-runtime/screenshots/long-pathing/timeline/ClimbOrgrimmarZeppelinTowerRampToFrezza/` |

---

## §3 Hypotheses

**H1 — Phantom poly at start (analogous to loop-21 trap at tile (40,29)):**
The bot is on a polyref at `(1601.2,-4455.3,7.7)` with no real connectivity to the navmesh graph. `CalculatePath` returns `no_route` because there's no path FROM the start poly to anywhere, not because the destination is unreachable.

**H2 — Bake-side disconnect on the containing tile:**
The tile covering `(1601.2,-4455.3,7.7)` has an unbridged gap to the tile containing the target / intermediate route. Off-mesh connections missing. Similar pattern to loop-24 Phase A5 work but on a different tile.

**H3 — OG dock/sea-level vs tower deck disconnect:**
The bot is at z=7.7 (sea level / dock); the route requires going up to z=37+ (city deck) and then z=53 (Frezza). If there's no ramp/teleport poly bridging sea level to deck level at this specific approach, `CalculatePath` finds no route. The D-series predecessor work focused on the *deck-to-tower-top* climb; D5 may be on the **sea-level-to-deck** transition.

**H4 — `IsLongTravelStyleRoute` predicate suppresses replan/recovery:**
Per D3 fix §5 Unknown 2, long-travel routes don't hit `preferSaferAlternateOnReplan` even after escalation. If `path_unavailable` triggers under long-travel guard, no recovery escalation can fire. Distinct from H1-H3 but compounds them.

---

## §4 Proposed next-step probes (DIAG, not IMPL)

Before authoring §5 IMPL, run these diagnostics. **All read-only; no source edits.**

### P1 — Convert stall coord to tile + check bake state

```powershell
# Map coord → tile math (WoW tile convention: tile_x = floor((coord_x + 17066.66) / 533.33))
# (1601.2 + 17066.66) / 533.33 ≈ 35.0 → tile_x ≈ 35
# (-4455.3 + 17066.66) / 533.33 ≈ 23.6 → tile_y ≈ 23 or 24
# Confirm via: ls D:\MaNGOS\data\mmaps\001*.mmtile | grep 23 or 35 — match against actual tile filenames
```

Verify the containing tile exists, is non-zero size, and was built with the loop-23+ MmapGen config.

### P2 — `--dump-poly-stack` at the stall coord

```powershell
dotnet run --project "E:\repos\Westworld of Warcraft\tools\PathPhysicsProbe\PathPhysicsProbe.csproj" -c Release -- `
  --dump-poly-stack `
  --mapId 1 `
  --route 1601.2,-4455.3,7.7 `
  --poly-stack-xy-extent 5 `
  --poly-stack-z-extent 10
```

Reveal: how many polys at the stall coord? Is the bot on a phantom (`posOverPoly=0`)? Is there a legit ground poly above or below? (Loop-21 trap pattern — coord 2 at the tile-(40,29) trap had 63 phantoms + 1 legit ground poly.)

### P3 — `--enumerate-static-collision` at the stall coord

Reveal: what static collision (WMO + M2 doodad + ADT) is *at* this coord. Sea-level vs deck distinction should be obvious from sourceLabel.

### P4 — `CalculatePath` from a *known-good* OG ground coord to the same target

Pick a confirmed-walkable OG-city coord (e.g. from one of the earlier passing PFS Theory cases like `og_city_*`) and call `CalculatePath` to `(1331.1,-4649.5,53.6)`. If it returns success, the destination IS reachable from valid ground — falsifies H1 in favor of H3 (start coord is the problem, not the destination).

### P5 — Audit the **route LEADING TO** the stall

The bot got to `(1601.2,-4455.3,7.7)` via some prior waypoints. Read the timeline to find which previous waypoint (immediately before the stall) the bot was on. If that waypoint was on the deck (z=37+) and the bot *fell* to z=7.7, the failure isn't a routing bug — it's a **physics-fall-off-deck** bug. Check `02-climb-poll-00169-…` and `00168` JSONs for the prior poll's coord + speed.

---

## §5 IMPL — VERDICT (iter-3 DIAG complete 2026-05-19)

**Hypothesis verdict** after P1-P5 probes:

| H | Status | Evidence |
|---|---|---|
| H1 phantom-only trap | **FALSIFIED** | P2 found legit `posOverPoly=1` poly `0x1000013E0018B` polyIdx 395 surfaceZ=7.838 at stall coord |
| H2 tile bake-gap | **CONFIRMED at MaNGOS bake** | `0012940.mmtile` md5 `cc0d89c4...` in `D:/MaNGOS/data/mmaps/` vs md5 `68b4f4cb...` in `D:/wwow-bot/prod-data/mmaps/` (loop-24 canonical). Same coord → MaNGOS gives 2-corner stub, prod-data gives 1066-corner descent + climb. 8+ neighboring tiles also drifted. |
| H3 sea/deck design issue | FALSIFIED | Test docstring at `Tests/BotRunner.Tests/LiveValidation/LongPathingTests.cs:638-643` explicitly states descent through OG city sea level is the intended route |
| H4 long-travel guard | N/A | `CalculatePath` itself returns `no_route` (probe-verified offline), so the long-travel guard never has a path to suppress |
| Fall-off-deck physics | FALSIFIED | poll-00020 z=36.78 → poll-00060 z=6.85 was the planned 188-WP descent corridor with `movementFlags=1` at every intermediate poll — not a fall |

### Root cause: bake-state drift, NOT a code surface

The live test ran against `dataDir: D:\MaNGOS\data` (per `tmp/test-runs/loop25-d-validate/test-summary.json`), which has an **older bake** than `D:\wwow-bot\prod-data\mmaps\` (loop-24 close-out canonical). The MaNGOS-bake dock poly is a graph island; the prod-data bake has the connectivity. No source change can close this — the fix is an ops promotion.

### Fix — corrected analysis (test config, not bake promotion)

The DIAG worker recommended "promote prod-data → MaNGOS\data", but the `tools/MmapGen/promote-mmaps.ps1` header (lines 18-21) explicitly states **"PFS-OVERHAUL-006: this is the canonical 'release a bake' step. Tests run against test-data/mmaps; once green, promote to prod-data/mmaps; restart Docker; production picks up the new bake. No more mixing with the MaNGOS server's data dir."**

The script promotes `D:\wwow-bot\test-data\mmaps\` → `D:\wwow-bot\prod-data\mmaps\` (NOT into `MaNGOS\data`). The canonical bot bake already lives at `D:\wwow-bot\prod-data\mmaps\` with md5 `68b4f4cb...` (loop-24 close-out canonical).

The actual root cause: `tools/scripts/run-pathfinding-tests.ps1:59` defaults `-DataDir` to `D:\MaNGOS\data`, but per PFS-OVERHAUL-006 the test runner should default to `D:\wwow-bot\prod-data`. The defaults weren't aligned with the overhaul.

**Cleanest fix surfaces (one of)**:

1. **Repoint default** (recommended): change `tools/scripts/run-pathfinding-tests.ps1:59` default from `'D:\MaNGOS\data'` to `'D:\wwow-bot\prod-data'`. Also update line 24's comment + line 5 in `tools/scripts/summarize-pathfinding-reference.ps1`, line 80 of `iterate-pathfinding.ps1`, and line 4 of `export-pathfinding-reference.ps1` for consistency. Then re-run `ClimbOrgrimmarZeppelinTowerRampToFrezza` via `mmo-live-fixtures`.
2. **Env override** (one-off): set `$env:WWOW_DATA_DIR = 'D:\wwow-bot\prod-data'` before invoking `run-pathfinding-tests.ps1`. Quick test of (1).
3. **Sync MaNGOS\data ← prod-data** (NOT recommended per PFS-OVERHAUL-006): would force the server's data dir to match the bot bake. Counter to the overhaul's architecture intent.

### Authorization gate

(1) and (2) are bot-test-config changes scoped to `Westworld of Warcraft/tools/scripts/`. Reversible, no shared-state impact. Safe under "ad-hoc origin/main" WWoW policy (commits straight to main once verified).

### Residual risks after promotion

- **`RefinePathForSteepUphill` skip-long-path cap**: the 1066-corner prod-data path may trip `[NAV-PERF] skip-long-path map=1 pts=1066 > 500` runtime cap. If so, either smooth-decimate the path or raise the cap. Verify by inspecting `RefinePathForSteepUphill` skip behavior post-promotion.
- **Other tiles' drift**: 8+ neighboring tiles also differ. Loop-24 23/0 sweep was probably run against prod-data — re-validate the full PFS sweep against MaNGOS data post-promotion.
- **Runtime polyref snap divergence**: probe iterates polys via `findNearestPoly`; runtime CalculatePath may use a different polyref-resolution path. If runtime snaps to a phantom even on connected bake, even a clean promotion won't close — add runtime-side log of which polyref CalculatePath received for `start` on failing replans.

### Authorization gate

Promotion writes to `D:\MaNGOS\data\` (shared ops state). **Not auto-executed by the impl loop.** Surfaced to user for explicit go-ahead.

### Legacy hypothesis IMPL shapes (kept for record, none required)

| H | Original IMPL shape (NOT NEEDED) |
|---|---|
| H1 | Loop-24 style off-mesh + Navigation.cs off-mesh awareness — N/A (H1 falsified) |
| H2 | New off-mesh entries on containing tile (loop-24 A5 pattern) — N/A (the prod-data bake already has the route; just need to promote) |
| H3 | Off-mesh from sea-to-deck OR TravelTo waypoint precondition — N/A (route is intentional) |
| H4 | Drop `!IsLongTravelStyleRoute()` guard at `NavigationPath.cs:3772` — N/A (no path exists to apply guard to) |
| Fall-off | `MovementController` physics fix — N/A (no fall happened) |

---

## §6 Stop-conditions for this surface

D5 closes when:
1. P1-P5 diagnostics complete + hypothesis confirmed.
2. IMPL lands per the matching §5 row.
3. `ClimbOrgrimmarZeppelinTowerRampToFrezza` re-runs PASS via `mmo-live-fixtures`.
4. Sweep verifies no regression on the 4 loop-24-closed tile-(40,29) tests + the OG zep parity tests.

---

## §7 Notes for the next agent

- The D-series escalation in `NavigationPath.cs` (D3 IMPL) is working as designed. **Do not retry D3 changes.** D5 is a new surface in a new tile with a new failure class.
- `mmo-live-fixtures` skill is the canonical driver for the validation runs.
- The probe CLI's `--findPath` argument **does not exist**; the iter-1 worker's suggested command is wrong. Use `--dump-poly-stack` / `--dump-polyrefs` / `--enumerate-static-collision` (per P2/P3) and read `Program.cs` for the actual argument surface.
- All loop-24 work was on tile `(40, 29)`; D5 is on a *different* tile. Don't apply loop-24's cull list here.
- WWoW commit policy: ad-hoc direct to `origin/main` per user preference — but no IMPL changes have landed for D5 yet, so nothing to commit.
