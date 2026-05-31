---
name: route-pack-generation
description: Generate and validate a static route pack for a long fixed leg, gated by the long-pathing route tests. Use when a frequently-traveled static route should be precomputed and cached.
trigger: route pack, static route, long leg, precomputed route, route gate, LongPathingRouteTests, StaticRoutePackCache, validate a route
---

# Route Pack Generation

## Goal

Produce and validate a static route pack for a long, fixed travel leg so the
PathfindingService can serve it from cache, with the long-pathing route test as
the green gate.

> The pathfinding freeze applies: route packs are **data**; do not add new managed
> repair phases to `Services/PathfindingService/Repository/Navigation.cs`. See
> `docs/physics/README.md`.

## Inputs

- The leg: start/end anchors, race/gender, route policy.
- Key files (verified):
  - Cache/generation: `Services/PathfindingService/RoutePacks/StaticRoutePackCache.cs`.
  - Route manifests: `tools/scripts/routes/*.json` (e.g. `og-zeppelin.json`).
  - Probe: `tools/scripts/probe-routes.ps1`, `Tools/PathPhysicsProbe/`.
  - Gate tests: `Tests/PathfindingService.Tests/LongPathingRouteTests.cs`.
- Spec: `docs/Spec/06_PATHFINDING.md` (§Route-pack manifest — data contract +
  validation guards).
- Area rules: `.github/instructions/tools.instructions.md`.

## Preconditions

- The underlying mesh is correct for the leg (if it clips static geometry, fix the
  mesh first via [[pathfinding-bake-iteration]]).

## Procedure

1. Define the leg in `tools/scripts/routes/<route>.json` (start/end, race/gender,
   policy).
2. Probe it: `tools/scripts/probe-routes.ps1 -Manifest tools/scripts/routes/<route>.json`;
   confirm every segment resolves with no blocking failure.
3. Add a focused route test (or extend `LongPathingRouteTests`) asserting the leg
   avoids known static blockers.
4. Once green, register the pack in `StaticRoutePackCache.cs`.
5. Confirm the route-pack signature matches the baked mmaps + Navigation.dll +
   config (per Spec/06 guards).

## Verification

- `dotnet test Tests/PathfindingService.Tests/PathfindingService.Tests.csproj --filter "FullyQualifiedName~LongPathingRoute"`.
- Probe output shows all segments resolved.

## Outputs

- `tools/scripts/routes/<route>.json` manifest, a route gate test, and a
  `StaticRoutePackCache` entry.

## Failure modes and recovery

- **Caching a route over a broken mesh** — fix the mesh first; a cached bad route
  is worse than none.
- **Signature mismatch** after a mesh/Navigation.dll change invalidates packs —
  regenerate and re-validate.
- **Adding managed repair** to force the gate green — forbidden under the freeze.

## Related skills

- [[pathfinding-bake-iteration]] — fix the mesh the route runs on.
- [[fg-bg-physics-parity]] — movement parity along the leg.
- Reference: `docs/Spec/06_PATHFINDING.md`, `docs/physics/README.md`.
