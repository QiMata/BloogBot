# Recast Physics-Validated Overhaul — Session Handoff (post iter 25)

**Created:** 2026-05-31 ~16:55 EDT (mid-sweep pause for IDE/CLI tool update)
**Last commit on `main`:** `d7c4bba5` `phase(1) iter(25): full map-1 re-bake SUCCESS with auto-derive fix`
**Working dir:** `e:/repos/Westworld of Warcraft`
**Branch:** `main`

---

## TL;DR

Phase 1's last open piece is the D2 sweep + aggregator (iter 26). The
sweep is RUNNING IN BACKGROUND right now. When the resume session
starts, **first action:** check whether the sweep is still running OR has
completed. If completed, jump straight to aggregator + Phase 1 close
measurement.

Phase 1 code is in a known-good state (iter 24 + 25): all 6 Mononen
violations addressed; full map-1 re-bake clean (786 tiles, zero
overflow); bake-fixture pair T3 + T4 PASS 2/2. The Phase 1 EXIT metric
("≥30% Blocked-drop globally vs D4's 13.18% Unrecoverable baseline")
requires the sweep to finish + aggregate to compare.

---

## 1. What's running in background RIGHT NOW

| Process | PID | Purpose | Expected to finish |
|---|---|---|---|
| `powershell.exe` (Start-Process wrapper) | **5252** | Runs `phase0-sweep-map.ps1 -MapId 1 -Samples 5 -NoLoadAdt` | ~18:30-19:00 EDT (~1.5-2 hr from start) |
| `NavMeshPhysicsValidator.exe` (per-tile child) | varies | Sweeps each tile (re-launched per tile by wrapper) | Per-tile, ~5-25s each |

Wrapper PID 5252 stays alive for the duration of the sweep. The validator
child PID changes every tile (the wrapper launches a fresh validator
process per tile and waits for it to exit).

**Sweep output dir:** `E:\repos\Westworld of Warcraft\tmp\iter-overhaul-phase0\sweep-map1\`
- One JSON per completed tile: `tile-<X>-<Y>.json`
- Sweep log: `sweep-map1.log` (in parent dir; timestamped per-tile progress)
- Started ~16:41 EDT; at 16:55 had completed 91/786 tiles with ETA ~98 min.

**Resume check command:**
```powershell
$tiles = Get-ChildItem 'E:\repos\Westworld of Warcraft\tmp\iter-overhaul-phase0\sweep-map1' -Filter 'tile-*.json' | Measure-Object
Get-Process -Id 5252 -ErrorAction SilentlyContinue
"Sweep progress: $($tiles.Count)/786 tiles"
```

If `Get-Process -Id 5252` returns nothing AND tile count = 786 → sweep is COMPLETE, proceed to aggregator.
If `Get-Process -Id 5252` returns a process AND tile count < 786 → sweep is STILL RUNNING. Wait (or set up a poll).
If tile count < 786 AND no wrapper process → sweep CRASHED mid-flight. Investigate.

---

## 2. What to do after the sweep completes

### Step 1: Aggregate

```powershell
& 'E:\repos\Westworld of Warcraft\tools\scripts\phase0-aggregate-sweep.ps1' -MapId 1
```

Writes:
- `E:\repos\Westworld of Warcraft\tmp\iter-overhaul-phase0\sweep-map1-aggregate.json`
- `E:\repos\Westworld of Warcraft\tmp\iter-overhaul-phase0\sweep-map1-summary.md`

### Step 2: Compare to D4 baseline

D4's recorded baseline (preserved at):
- `tmp/iter-overhaul-phase0/sweep-map1-aggregate.preiter21.json`
- `tmp/iter-overhaul-phase0/sweep-map1-summary.preiter21.md`

Key D4 metric (from `docs/Plan/Pathfinding/OVERHAUL_PHASE0_D4_FINDINGS.md` §3):
- Global Unrecoverable (Blocked + UnsafeDrop): **13.18%** (at iter 16, 51.5% coverage)
- D4 §2b notes the FINAL aggregate is likely slightly lower due to no-load-adt bias on second-half tiles.

**Phase 1 exit criterion (proposal §3):** ≥30% RELATIVE drop in Blocked
poly count. So if D4 baseline Blocked = X, iter 26 target is Blocked ≤ 0.7X.

### Step 3: Decision based on result

If Phase 1 EXIT metric is hit (≥30% Blocked-drop globally):
- Iter 26 = Phase 1 close commit; status doc Phase 1 closed
- Iter 27+: full re-bake of remaining 40 maps to `D:\MaNGOS\data` then promote to `D:\wwow-bot\test-data` then promote to `D:\wwow-bot\prod-data` + docker restart
- Run LongPathingTests (T1 should flip FAIL → PASS, the proposal's flagship target)

If Phase 1 EXIT metric is NOT hit:
- Surface to user — there are additional levers (walkableClimb quantization, finer cs, etc.) but the proposal's claim "≥30%" needs reconciliation with reality
- Don't promote anything to test-data or prod-data without user confirmation

---

## 3. Code + data state

### Git HEAD: `d7c4bba5`

Recent commits:
- `d7c4bba5` phase(1) iter(25): full map-1 re-bake SUCCESS with auto-derive fix
- `38e2331a` phase(1) iter(24): re-apply iter-20 + auto-derive ch=cs/2 per-tile
- `1a6df2b1` phase(1) iter(23): audit — vertex overflow root cause identified
- `403e324e` phase(1) iter(22): REVERT iter 20 — Mononen ch=cs/2 broke T3 + 110 tiles
- `b9753758` phase(1) iter(20): backfill commit hash in status doc
- `0743d391` phase(1) iter(20): wire BakeProfile.h cs/ch + remove ch=0.1f override

### Code state (working dir clean post iter 25):

| File | State |
|---|---|
| `tools/MmapGen/include/BakeProfile.h` | iter 24: inline `MakeBakeProfile` + `BakeProfileIsValid` (Mononen rules) |
| `tools/MmapGen/CMakeLists.txt` | iter 24: `MMAPGEN_INC_LOCAL` for include path |
| `tools/MmapGen/contrib/mmap/src/TileWorker.cpp` | iter 24: `#include "BakeProfile.h"`; `from_json` cs/ch from MakeBakeProfile; **buildTile auto-derive ch=cs/2 when per-tile cs override has no ch override**; iter 19's 5 Mononen value updates in `getDefaultConfig` (slope, simplification, region areas, sample dist) |

### Bake state on disk:

| Location | Contents | Last touched |
|---|---|---|
| `D:\MaNGOS\data\mmaps\` | **iter-25 bake**: 786 map-1 tiles (676 MB) + other maps (Mar-17 vintage) | 5/31 ~16:32 EDT |
| `D:\MaNGOS\data\mmaps.iter21-failed\` | iter-21 broken bake (675 tiles) — diagnostic only | 5/31 ~13:31 EDT |
| `D:\wwow-bot\test-data\mmaps\` | May-8 production bake (UNTOUCHED) | 5/30 |
| `D:\wwow-bot\prod-data\mmaps\` | May-8 production bake (UNTOUCHED) | 5/30 |

**Important:** the `.preiter21` backup that iter 22 used to roll back from
iter 21's broken bake was renamed in iter 22 back to live `mmaps`. There's
no longer a separate `.preiter21` directory. If iter 25's bake needs
rollback, the source is `D:\MaNGOS\data\mmaps.iter21-failed\` (the
ITER-21 broken bake, but with backups of the May-1 production bake at
per-tile `.pre-*.bak` files inside that dir) OR the test-data /
prod-data dirs which still hold May-8 production.

### Sweep state on disk:

| Location | Contents |
|---|---|
| `tmp/iter-overhaul-phase0/sweep-map1\tile-*.json` | iter-26 sweep IN PROGRESS (was 91/786 at 16:55) |
| `tmp/iter-overhaul-phase0/sweep-map1.preiter21\tile-*.json` | D4-era 409 tile JSONs preserved from iter 21 |
| `tmp/iter-overhaul-phase0/sweep-map1-aggregate.preiter21.json` | D4-era global aggregate (the 13.18% Unrecoverable baseline) |
| `tmp/iter-overhaul-phase0/sweep-map1-summary.preiter21.md` | D4-era summary report |
| `tmp/iter-overhaul-phase0/sweep-map1.log` | iter-26 per-tile progress log |

---

## 4. What was accomplished this session (iters 19 → 25)

**The campaign goal:** Phase 1 SHIPPED — all 41 maps re-baked with Mononen
rules, T1 (CrossroadsToUndercity LongPathingTest) flipped to PASS,
promoted to prod-data + docker restart, integration tests green.

**Progress this session:**

| Iter | Outcome | Commit |
|---|---|---|
| 19 | 5/6 Mononen value updates landed; T3+T4 canary green; per-tile probe blindspot identified | `9140ea44` |
| 20 | 6/6 Mononen wired (including cs=r/2, ch=cs/2 via BakeProfile.h); bake-fixture pair green BUT byte-identical probe again | `0743d391` |
| 21 | Full map-1 re-bake attempt → REGRESSION (T3 vertex overflow + 110 missing tiles) | (no commit) |
| 22 | REVERT iter 20; restored backup; T3+T4 canary green again | `403e324e` |
| 23 | Audit: identified ROOT CAUSE (per-tile cs override + global ch change = mismatched ratio for tile (40,29); also walkableClimb quantization loss) | `1a6df2b1` |
| 24 | Re-applied iter 20 + ADDED auto-derive ch=cs/2 per-tile; T3 + (32,28) + (43,25) single-tile bakes clean; canary 2/2 PASS | `38e2331a` |
| **25** | **Full map-1 re-bake CLEAN: 786 tiles, ZERO overflow, ZERO silent failures, 12.3 min wall; canary 2/2 PASS** | **`d7c4bba5`** |

**Key technical findings banked to memory:**
- [[pfs-overhaul-iter20-phase1-mononen6-csch]] — the iter 20 wire-up
- [[pfs-overhaul-iter21-22-phase1-full-rebake-regression-revert]] — the regression + revert
- [[pfs-overhaul-iter23-audit-vertex-overflow-root-cause]] — the audit findings
- [[pfs-overhaul-iter24-phase1-auto-derive-ch]] — the fix that works

---

## 5. Known footguns banked (from this session's discoveries)

1. **MmapGen `<mapId>` without `--tile X,Y` is INCREMENTAL.**
   `TileWorker::shouldSkipTile` (TileWorker.cpp:10323) skips any tile
   whose existing `.mmtile` has matching MMAP_MAGIC + DT_NAVMESH_VERSION
   + MMAP_VERSION headers. **To force a full re-bake of all tiles for a
   map: DELETE existing `.mmtile` files first, then run MmapGen.** Or add
   a `--force` flag in a future iter.

2. **`bake-all-maps.ps1` reports "OK" even when individual tiles fail.**
   The script counts files on disk and reports OK based on MmapGen's
   exit code. But individual tiles can silently fail (no walkable
   navmesh) OR get terminated by `exit(0)` from another tile's
   overflow. Always cross-check tile count against expected count.

3. **MmapGen calls `exit(0)` on per-tile vertex overflow** (TileWorker.cpp
   :11820). This kills the WHOLE PROCESS, terminating any in-flight worker
   threads. Tiles whose write completed before the exit are kept; tiles
   in progress are lost. In iter 21 this terminated processing of 110
   tiles after tile (40, 29) overflowed.

4. **The poll-loop pattern `while tasklist /FI "PID eq X" /NH | grep -q name`
   is UNRELIABLE on this system.** It falsely reports the process as
   exited even when it's running. **Use `Get-Process` via PowerShell instead:**
   ```bash
   while powershell -NoProfile -Command "exit (Get-Process -Id PID -ErrorAction SilentlyContinue | Measure-Object).Count" 2>/dev/null; [ $? -gt 0 ]; do sleep 60; done
   ```
   The `[ $? -gt 0 ]` invert is critical (PowerShell `exit N` where N=count;
   N>0 means process alive, [ $? -gt 0 ] is true → while continues).

5. **`Start-Process` returns a different PID than the actual MmapGen
   child.** Start-Process launches a wrapper PowerShell which then invokes
   MmapGen.exe as its child. Always grab the wrapper PID from $proc.Id
   AND find the MmapGen.exe PID separately via `Get-CimInstance
   Win32_Process -Filter "Name='MmapGen.exe'"`.

6. **Per-tile config.json overrides + global Mononen rule.** Per-tile
   blocks that override only `cs` (no `ch`) are vulnerable to global ch
   changes. **Iter 24's auto-derive ch=cs/2 logic handles this in
   `buildTile`** — when per-tile JSON contains "cs" but not "ch", auto-set
   `config.ch = config.cs * 0.5f`. Going forward, any per-tile config
   that overrides only one of cs/ch must trust the buildTile auto-derive.

---

## 6. Recovery commands for the resume session

### Check sweep status:
```powershell
Get-Process -Id 5252 -ErrorAction SilentlyContinue
"sweep tiles: $((Get-ChildItem 'E:\repos\Westworld of Warcraft\tmp\iter-overhaul-phase0\sweep-map1' -Filter 'tile-*.json' | Measure-Object).Count)/786"
Get-Content 'E:\repos\Westworld of Warcraft\tmp\iter-overhaul-phase0\sweep-map1.log' -Tail 5
```

### If sweep is done — run aggregator + compare:
```powershell
& 'E:\repos\Westworld of Warcraft\tools\scripts\phase0-aggregate-sweep.ps1' -MapId 1
# Then compare:
$new = Get-Content 'E:\repos\Westworld of Warcraft\tmp\iter-overhaul-phase0\sweep-map1-aggregate.json' -Raw | ConvertFrom-Json
$old = Get-Content 'E:\repos\Westworld of Warcraft\tmp\iter-overhaul-phase0\sweep-map1-aggregate.preiter21.json' -Raw | ConvertFrom-Json
"=== new vs old ==="
"Unrecoverable: new=$($new.unrecoverablePct)% old=$($old.unrecoverablePct)% delta=$([Math]::Round($new.unrecoverablePct - $old.unrecoverablePct, 2))pp"
"Blocked: new=$($new.affordanceCounts.Blocked) old=$($old.affordanceCounts.Blocked) blocked-drop=$([Math]::Round(100.0*($old.affordanceCounts.Blocked - $new.affordanceCounts.Blocked)/$old.affordanceCounts.Blocked, 1))%"
```

(Field names may differ; inspect `sweep-map1-aggregate.preiter21.json` for exact schema.)

### If sweep is still running — re-launch poll:
```bash
# In Bash tool:
while powershell -NoProfile -Command "exit (Get-Process -Id 5252 -ErrorAction SilentlyContinue | Measure-Object).Count" 2>/dev/null; [ $? -gt 0 ]; do sleep 120; done; echo "Sweep wrapper PID 5252 exited at $(date)"
# Set run_in_background=true, timeout_ms=600000
```

### If sweep CRASHED:
- Investigate `sweep-map1.log` for the last successful tile
- Check whether NavMeshPhysicsValidator.exe hit the iter-15 known Mulgore/Thunder Bluff AV
- Restart the sweep — it's idempotent (skips tiles whose JSON already exists)

---

## 7. Iter 26 expected outcomes

**Best case:** Phase 1 EXIT metric hit (≥30% Blocked-drop). Phase 1 closed
in status doc + memory. Iter 27 plans full 41-map re-bake.

**Realistic case:** Some Blocked-drop measurable but <30%. Surface to
user for direction: ship Phase 1 as "5 of 6 + auto-derive + Mononen
defaults" without hitting the proposal's exact target, or chase more
levers (walkableClimb quantization fix, finer cs, vmap extractor Phase 3
work, etc.).

**Worst case:** Sweep shows minimal change OR something regressed. Iter
27 would need to investigate. Note: T3+T4 bake-fixture pair was GREEN
post-iter-25, so any "regression" would be a global-distribution shift,
not a canary breakage.

---

## 8. Open questions banked for future iters

1. **walkableClimb world-unit quantization loss** (iter 23 audit) —
   floor(1.2/0.2562)=4 voxels=1.025y world vs floor(1.2/0.1)=12=1.2y. The
   0.175y loss might filter out walkable area on hilly terrain. Iter 25
   recovered all tiles so this MAY NOT be a practical issue in the bake,
   but worth measuring if the sweep shows degraded walkable affordance
   in mountain regions.

2. **Tile (40, 29) at 10MB is unusually large.** Iter 24's auto-derive
   ch=0.05 for this tile produces a 10MB .mmtile. The previous May-1
   production was 1.86MB. Bake-fixture pair PASSES so runtime accepts
   the larger file, but is the 10MB tile slowing runtime path queries?
   Worth measuring p50/p99 path latency before promotion to prod-data.

3. **The "auto-derive ch=cs/2 per-tile" logic is per-tile json only.**
   Other code paths that set cs but not ch (if any) would still be at
   risk of the old mismatch. Audit: are there any other code paths
   that mutate config.cs after from_json? Currently buildTile is the
   only place I touched.

4. **Phase 2-6 remain.** The proposal's full vision includes Phase 2
   (Recast 1.6 vendor upgrade), Phase 3 (vmap extractor fixes), Phase 4
   (bake-time physics validation pass — the headline work), Phase 5
   (runtime simplification), and Phase 6 (validation coverage expansion).
   This session focused on Phase 1; the user's campaign goal was Phase 1
   SHIPPED.

---

## 9. Files I created/edited this session (delta vs session start)

**Created:**
- `docs/Plan/Pathfinding/OVERHAUL_HANDOFF.md` (THIS file)
- `C:/Users/lrhod/.claude/projects/e--repos/memory/project_pfs_overhaul_iter20_phase1_mononen6_csch.md`
- `C:/Users/lrhod/.claude/projects/e--repos/memory/project_pfs_overhaul_iter21_22_phase1_full_rebake_regression_revert.md`
- `C:/Users/lrhod/.claude/projects/e--repos/memory/project_pfs_overhaul_iter23_audit_vertex_overflow_root_cause.md`
- `C:/Users/lrhod/.claude/projects/e--repos/memory/project_pfs_overhaul_iter24_phase1_auto_derive_ch.md`

**Modified:**
- `tools/MmapGen/include/BakeProfile.h` (iter 24)
- `tools/MmapGen/CMakeLists.txt` (iter 24)
- `tools/MmapGen/contrib/mmap/src/TileWorker.cpp` (iter 24)
- `docs/Plan/Pathfinding/OVERHAUL_LOOP_STATUS.md` (iters 20-25)
- `C:/Users/lrhod/.claude/projects/e--repos/memory/MEMORY.md` (index updates)

---

*End of handoff. Resume session: check sweep status (item §6 step 1), then either aggregate immediately or re-launch poll if still running.*
