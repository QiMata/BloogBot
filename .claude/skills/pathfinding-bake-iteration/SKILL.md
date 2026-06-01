---
name: pathfinding-bake-iteration
description: Iterate tools/MmapGen per-tile bake config to close a navmesh fidelity gap, respecting the pathfinding freeze (mesh fixes in MmapGen, no managed repair). Use when a route clips static geometry or a tile has a hole.
trigger: navmesh gap, mmap bake, MmapGen tile config, mesh fidelity, route clips geometry, regenerate mmaps, pathfinding freeze, bake a tile
---

# Pathfinding Bake Iteration

## Goal

Close a navmesh fidelity gap by tuning the `tools/MmapGen` bake config for the
affected tile(s) and regenerating the mesh — never by adding managed repair logic.

> **FREEZE (2026-05-06).** The pathfinding stack is in an architectural freeze:
> mesh fixes belong in `tools/MmapGen/`; **no new managed repair logic** in
> `Services/PathfindingService/Repository/Navigation.cs` or BotRunner movement
> code. Read `docs/physics/README.md` **before editing anything**.

## Inputs

- The failing leg/tile (map id + tile coords) and the symptom (clip, hole, fall).
- Key references:
  - Entry doc: `docs/physics/README.md`; deep:
    `docs/physics/MMAP_NAVMESH_GENERATION.md`, `docs/physics/MMAP_DATA_FLOW.md`
    (test vs prod data isolation); spec `docs/Spec/06_PATHFINDING.md`.
  - Bake config: `tools/MmapGen/config.json` (per-map/tile params — agent radius,
    erosion, cell size, simplification, anchor-stage manifests),
    `tools/MmapGen/contrib/mmap/src/TileWorker.cpp`, `tools/MmapGen/offmesh.txt`.
  - Build/bake/audit: `tools/MmapGen/build-mmapgen.ps1`,
    `tools/scripts/bake-tile.ps1`, `tools/NavDataAudit/NavDataAudit.csproj`.
- Area rules: `.github/instructions/native.instructions.md` (C++ MmapGen),
  `.github/instructions/tools.instructions.md`.

## Preconditions

- You have read `docs/physics/README.md` and the navmesh-generation doc, and you
  understand the capsule/clearance contract.
- You are working against **test** data, isolated from prod (per MMAP_DATA_FLOW).

## Procedure

1. Reproduce/locate the gap on a failing route or via the physics probe.
2. Tune the tile in `tools/MmapGen/config.json` (erosion / cell size /
   simplification, or add an anchor-stage manifest entry) — or fix
   GO-aware generation in `TileWorker.cpp` if a static object is the cause.
3. Build MmapGen (`tools/MmapGen/build-mmapgen.ps1 -Configuration Release`).
4. Bake the affected tile(s) into the test data dir (`tools/scripts/bake-tile.ps1`).
5. Audit with `NavDataAudit` — confirm the capsule header, GO spawn marks, and the
   stage summary.
6. Run the focused mesh-quality + route gate; iterate until green; only then
   promote the regenerated mmaps to prod.

## Verification

- Focused mesh quality:
  `dotnet test Tests/PathfindingService.Tests/PathfindingService.Tests.csproj --filter "FullyQualifiedName~MmapMeshQuality"`.
- Route gate:
  `dotnet test Tests/PathfindingService.Tests/PathfindingService.Tests.csproj --filter "FullyQualifiedName~LongPathingRoute"`.
- `NavDataAudit` reports a clean tile.

## Outputs

- Updated `tools/MmapGen/config.json` (and/or `TileWorker.cpp`) + regenerated
  mmaps.
- A calibration-doc entry per AGENTS.md §15 (record the tweak + outcome).

## Failure modes and recovery

- **Adding managed repair** (clearance cylinders, detour waypoints, position
  guards) to make a route pass — forbidden under the freeze; fix the mesh.
- **Editing prod data directly** — bake into test data, validate, then promote.
- **Looping tweaks without recording outcomes** — AGENTS.md §15 requires one
  single-scope change per run, logged, with "Do Not Repeat" on regressions.

## Related skills

- [[route-pack-generation]] — validate a long leg after a mesh fix.
- [[fg-bg-physics-parity]] — when the gap is a physics/Z mismatch, not a mesh hole.
- Reference: `docs/physics/README.md`, `docs/Spec/06_PATHFINDING.md`.
