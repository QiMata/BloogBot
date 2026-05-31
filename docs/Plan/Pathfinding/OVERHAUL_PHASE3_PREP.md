# Phase 3 Prep — Vmap Extractor Inventory + WMO Fix Scope

> **Iter 7 of the Recast Physics-Validated Overhaul loop.**
> Read-only search to locate the vmap extractor + scope the
> [AzerothCore PR #20822](https://github.com/azerothcore/azerothcore-wotlk/pull/20822)
> + [TrinityCore #23972](https://github.com/TrinityCore/TrinityCore/issues/23972)
> backport. Per kickoff guardrail 1, no production code is touched in Phase 0.

## Major finding: no in-tree vmap extractor

Repo-wide grep for `MOPY` / `MOBN` / `MOBR` / `material_id` / `extractWmo` /
`vmap_extractor` matched **zero** C++ source. The repo consumes
pre-extracted `.vmtile`/`.vmtree` files from `D:/MaNGOS/data/vmaps/`
(produced by an external tool — likely MaNGOS / vmangos / CMaNGOS's
`vmap_extractor` run separately against the WoW client MPQ archives).

What IS in the repo:

| Path | Purpose | LOC |
|---|---|---|
| [`tools/MmapGen/src/game/vmap/`](../../../tools/MmapGen/src/game/vmap/) | **VMap CONSUMER library** — CMaNGOS-derived bake-time API for reading `.vmtile` files. Provides `VMapManager2`, `MapTree`, `ModelInstance`, `TileAssembler`, `WorldModel`. Used by MmapGen to rasterize vmap geometry into Recast. | 5,141 |
| [`Exports/Navigation/WmoDoodadFormat.h`](../../../Exports/Navigation/WmoDoodadFormat.h) | Custom `.doodads` file format used by `ExtractWmoDoodads` for the scene cache. **Auxiliary** doodad-placement data, NOT collision. Read by `SceneCache::Extract` at runtime. | 130 |
| [`Exports/Navigation/SceneQuery.cpp`](../../../Exports/Navigation/SceneQuery.cpp) | Runtime scene cache extraction (lines 1884, 1914 call `SceneCache::Extract`). Operates against already-loaded vmaps via `VMapManager2`. | 1,914+ |

The proposal's Phase 3 step 1 anticipates this:

> "Locate the vmap extractor in this repo. If we're reusing CMaNGOS's
> `vmap_extractor`, fork it into `tools/VmapExtract/` for in-tree control."

We are. Phase 3 must **fork the upstream extractor into the repo**
before any patch work can begin.

## Identifying the upstream extractor

The CMaNGOS lineage of `tools/MmapGen/src/game/vmap/` (TileAssembler.h
header credits "CMaNGOS Project") plus the existing
`tmp/mmapgen-baseline-20260507/src/game/vmap` snapshot strongly suggest
the production extractor is from the **CMaNGOS** (or its parent
mangoszero / vmangos) repository — same family of WoW Vanilla emulators
that produced the consumer library.

**Likely fork target:** `cmangos/mangos-classic` (or equivalent vmangos
branch) `contrib/vmap_extractor/` directory. Upstream URL TBD when
Phase 3 starts (web access not available during loop iters).

## What AzerothCore PR #20822 fixes

Per the proposal §1.1 + §1.2, the patch addresses:

1. **WMO MOPY chunk `material_id = 0xFF`**: triangles flagged as
   collision-only invisible walls. Without the patch, the extractor
   skips these as "not visible therefore not relevant", missing real
   collision.
2. **MOBN/MOBR BSP-only collision**: BSP-tree branches that hold
   collision triangles separate from the MOPY-flagged geometry. Different
   traversal path; can be silently dropped.

The proposal predicts these fixes will surface real collision at the
3 known stall coords:
- (1627.6, -4151.8, 36.9) — iter-1 east-wall (OG warchief building per TrinityCore #23972 list)
- (1608.1, -4382.3, 10.0) — iter-2 OG-interior (sub-floor pocket; iter-2 cull-coord probe found 7-poly Z-stack here)
- (1615.3, -4240.85, ~45) — loop-25 doodad-wall (2 M2 doodads forming a 4.6y vertical wall)

After the patch, Phase 0 probe should show either **(a) no polygon
present at the stall coord** (extractor's recovered collision now
blocks Recast from generating walkable poly) **or (b) polygon present
with Blocked edges** (caught by Phase 4 per-edge sweep).

## Known-bad WMO blacklist

TrinityCore #23972 lists known-broken WMOs in vanilla WoW. Phase 3
maintains a JSON-driven per-WMO patch list in the new
`tools/VmapExtract/wmo_blacklist.json`. Initial entries (per proposal):

- Orgrimmar warchief building (Thrall throne room area)
- Krakenstatue (Stranglethorn Vale)
- Plus the wowdev.wiki list of known WMO geometry issues

## Phase 3 scope estimate

| Work unit | Wall-clock | Iters |
|---|---|---|
| Fork upstream `vmap_extractor` into `tools/VmapExtract/` (CMake build, build glue) | 2-3 hr | 1-2 |
| Apply AzerothCore PR #20822 MOPY material_id=0xFF fix to MOPY chunk parser | 1-2 hr | 1 |
| Apply AzerothCore PR #20822 MOBN/MOBR BSP traversal fix | 1-2 hr | 1 |
| Build `tools/VmapExtract` + verify it produces a `.vmtile` byte-identical to current under no-op (sanity) | 1-2 hr | 1 |
| Add `wmo_blacklist.json` + per-WMO patch system (initial empty seed; populate later) | 0.5-1 hr | 1 |
| Re-extract vmaps for map 1 (Kalimdor) targeting the 3 known stall WMOs | 2-4 hr (background) | 1 |
| Re-bake those tiles + run Phase 0 probe; verify the 3 stalls show Blocked or absent polys | 1-2 hr | 1 |

**Phase 3 total estimate: 8-15 hr wall-clock, 5-8 iters.** Runs after
Phase 2 (vendor upgrade) completes, since the Recast 1.6 rasterization
changes affect what Phase 3's re-extracted vmaps produce.

## Phase 3 acceptance criteria (per proposal §3)

- vmap extractor handles WMO `material_id = 0xFF` triangles correctly.
- vmap extractor handles MOBN/MOBR BSP-only collision.
- Known-bad WMO blacklist applied.
- The 3 historical stall coords show either no polygon present or
  polygon with Blocked edges in the Phase-0 probe.

## What this iter does NOT find

- The actual upstream source URL for the vmap_extractor (web fetch
  not available during loop iters; identification deferred to Phase 3
  start).
- The exact MPQ archive locations on the dev host (assume `D:/MPQ/` or
  similar; verified when Phase 3 starts).
- The byte-format diff between current `.vmtile` files and what the
  AzerothCore-patched extractor would produce (requires running the
  patched extractor; Phase 3 measures this empirically).

## Phase 3 risk flags

| Risk | Likelihood | Mitigation |
|---|---|---|
| Upstream extractor source is GPL-incompatible with our license | Low | CMaNGOS / vmangos / mangos-classic are GPL-2.0; the existing `tools/MmapGen/src/game/vmap/` is already CMaNGOS-derived GPL — Phase 3 fork inherits same license. |
| Re-extracted .vmtile files cause a bake regression on currently-passing tiles | Medium | Bake the smallest affected tile subset first; run bake-fixture pair (guardrail 3) before promoting. The iter-2 cull blast radius lesson applies here too. |
| WMO blacklist seed list is wrong (incorrect WMO IDs) | Medium | Cross-reference against TrinityCore #23972 issue text; initial blacklist is intentionally minimal. |
| AzerothCore PR #20822 logic doesn't apply cleanly to vanilla WoW WMO format (PR was written for 3.3.5a TrinityCore) | Medium | Both 1.12.1 (vanilla) and 3.3.5a WMOs use the same MOPY/MOBN/MOBR chunk format per wowdev.wiki. Spot-check a 1.12.1 WMO header before applying. |

## Sequencing

Per proposal §7: "Phases 1, 2, 3 are mostly independent and can be done
in any order. Phase 4 depends on all three." So Phase 3 can run in
parallel with Phase 1 / Phase 2 once any code-modification work begins.
For this loop's pacing, the prep docs establish all three phase scopes
before any one starts — D4 (go/no-go) then decides which phase opens
first when Phase 0 closes.

## Pre-conditions before starting Phase 3

- ✅ Phase 0 D1 (probe).
- 🔄 Phase 0 D2 (baseline sweep in flight, 38% as of iter 7).
- ✅ Phase 0 D3 (test-failure manifest).
- ❌ Phase 0 D4 (go/no-go findings).

Phase 3 starts only after D4 commits with a `go` recommendation,
sequenced relative to Phase 1+2 per proposal §7.
