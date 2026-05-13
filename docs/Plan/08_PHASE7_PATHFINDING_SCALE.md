# Plan 08 — Phase 7: Pathfinding/Scene Scale

## Goal

PathfindingService and SceneDataService support the 3000-bot target
without becoming the bottleneck. Route packs warm at startup. Batch
estimates accelerate the scheduler. Sharding behind a thin coordinator
keeps client code single-endpoint.

## Entry pre-requisite

Phase 4 partial (enough automated progression to generate realistic
path-load).

## Exit criteria

- [ ] Route-pack manifest schema implemented; 20 packs warm at
      startup (the catalog hot edges).
- [ ] `PathBatchEstimate` returns ETAs for ≥ 100 paths in < 200 ms.
- [ ] Request de-duplication coalesces identical concurrent requests.
- [ ] Result cache implemented with bounded LRU.
- [ ] Sharding (`hash(accountName) % replicaCount`) works behind a
      thin coordinator process; clients see one endpoint.
- [ ] 500-bot load run: PFS P99 < 2 s.
- [ ] 1000-bot load run: PFS P99 < 3 s.

## Slots

### S5.1 — Route-pack manifest schema

- **Owner:** `monorepo-worker`
- **Status:** open
- **Owned paths:**
  - `Services/PathfindingService/RoutePacks/Manifest/**`
  - `Tests/PathfindingService.Tests/RoutePackManifestTests.cs`

### S5.2 — Pack generation tool

- **Owner:** `monorepo-worker`
- **Status:** open
- **Depends on:** S5.1
- **Owned paths:** `tools/RoutePackGen/**`
- **Goal:** Generate packs from MmapGen output for the catalog hot
  edges (Org flight-master → zeppelin, Crossroads → UC, Stormwind →
  Ironforge tram, etc.). Validate each pack offline.

### S5.3 — Pack load + lookup

- **Owner:** `monorepo-worker`
- **Status:** open
- **Depends on:** S5.1, S5.2
- **Owned paths:** `Services/PathfindingService/RoutePacks/Lookup/**`

### S5.4 — Request de-dup + result cache

- **Owner:** `monorepo-worker`
- **Status:** open
- **Owned paths:** `Services/PathfindingService/Cache/**`

### S5.5 — `PathBatchEstimate`

- **Owner:** `monorepo-worker`
- **Status:** open
- **Depends on:** S5.3, S5.4
- **Owned paths:**
  - `Services/PathfindingService/Batch/PathBatchEstimateHandler.cs`
  - `Exports/BotCommLayer/communication.proto` (additions)

### S5.6 — Coordinator + replica sharding

- **Owner:** `monorepo-worker`
- **Status:** open
- **Depends on:** S5.5
- **Owned paths:**
  - `Services/PathfindingService/Coordinator/**`
- **Goal:** Clients connect to coordinator; coordinator picks replica
  by `hash(account)`. Single-replica deploys remain valid.

### S5.7 — SceneDataService cache + eviction

- **Owner:** `monorepo-worker`
- **Status:** open
- **Owned paths:** `Services/SceneDataService/Cache/**`

### S5.8 — 500-bot load test

- **Owner:** `monorepo-test-runner`
- **Status:** open
- **Depends on:** S5.6, S5.7

### S5.9 — 1000-bot load test

- **Owner:** `monorepo-test-runner`
- **Status:** open
- **Depends on:** S5.8
