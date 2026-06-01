# Execution Plan (DEFERRED — pathfinding freeze): Split Navigation.cs

> **STATUS: NOT YET ACTIONABLE.** `Services/PathfindingService/Repository/Navigation.cs`
> is inside the active pathfinding freeze (`docs/physics/README.md`, since
> 2026-05-06). **Do not execute this plan** until the freeze lifts and route
> authority has finished moving to `tools/MmapGen/`. This file is the ready-to-go
> plan for that day.

## Goal
Split the ~6,756-line `Navigation.cs` into cohesive `partial class` files by
responsibility, mirroring the safe partial-split pattern already proven on
`SqliteStorylineRepository` (see `.agent/plans/service-file-partial-splits.md`).
Readability only — zero behavior change.

## Current behavior
One ~6,756-line `Navigation` class mixing: record/enum type defs; budget/affordance
constants; native structs + P/Invoke declarations; and large method groups for
path validation, affordance repair, local-physics reachability, static repair
routing, ground-Z refinement, and dynamic-overlay handling.

## Proposed behavior (post-freeze)
`Navigation` becomes `partial`, split into files such as:
- `Navigation.Interop.cs` — native structs + P/Invoke declarations + enums.
- `Navigation.Query.cs` — path query / waypoint generation entry points.
- `Navigation.RoutePackCache.cs` — static route-pack cache.
- `Navigation.Repair.cs` — affordance/segment repair + local-physics reachability.
- `Navigation.Overlay.cs` — dynamic-object overlay handling.
Members move **verbatim**; no signature/body changes.

## Files likely to change
`Services/PathfindingService/Repository/Navigation.cs` (-> `partial`) + the new
`Navigation.*.cs` partials. **FROZEN AREA** — also implies no managed-repair logic
changes; mesh fixes belong in `tools/MmapGen/`.

## Tests to add/update
None added. The full `Tests/PathfindingService.Tests/` suite (route, overlay,
repair, cache, long-pathing) is the gate — runs only where the native
`Navigation.dll` toolchain exists.

## Compatibility concerns
None at behavior level (internal partial split). Must not perturb the wire
contract or the FG/BG movement parity the freeze protects.

## Migration concerns
None (no persisted state).

## Validation commands
```bash
pwsh scripts/check-project-layering.ps1
dotnet build WestworldOfWarcraft.sln
dotnet test  Tests/PathfindingService.Tests/PathfindingService.Tests.csproj --configuration Release
```

## Rollback plan
Isolated commit -> `git revert`. Partial splits revert trivially.

## Open questions
- Final boundary lines depend on the post-overhaul shape of `Navigation.cs`;
  re-map before splitting (the line ranges above are pre-overhaul).
- Confirm the freeze is formally lifted in `docs/physics/README.md` first.
