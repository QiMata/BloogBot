# Plan 09 — Phase 8: Living-Server Load

## Goal

Reach the 3,000-bot target with all spec acceptance criteria green.
Staged load runs at 50 / 200 / 500 / 1000 / 3000 bots verify
snapshot latency, pathfinding latency, disconnect rate, activity
success rate, log volume, and CPU/memory budgets.

## Entry pre-requisite

Phase 5 complete.

## Exit criteria

- [ ] 50-bot run: ✅ no Warnings outside expected categories; all
      activity families have at least one completion in a 6-hour
      window.
- [ ] 200-bot run: ✅ activity completion rate > 95%; pathfinding
      P99 < 1 s.
- [ ] 500-bot run: ✅ snapshot P99 < 50 ms; one AV (40v40) fills and
      completes; PFS P99 < 2 s.
- [ ] 1000-bot run: ✅ snapshot P99 < 100 ms; PFS P99 < 3 s;
      disconnect rate < 0.5%/h.
- [ ] 3000-bot run: ✅ snapshot P99 < 500 ms; PFS P99 < 3 s;
      disconnect rate < 0.1%/h; activity completion rate > 95%.
- [ ] Single StateManager handles 3000 bots OR a measured rationale
      exists for partitioning, and the partition is implemented.
- [ ] Living-server smoke checklist (`Spec/00_VISION.md` acceptance
      criteria) all pass.

## Slots

### S6.1 — 50-bot staged run

- **Owner:** `monorepo-test-runner`
- **Status:** open

### S6.2 — 200-bot staged run

- **Owner:** `monorepo-test-runner`
- **Status:** open
- **Depends on:** S6.1

### S6.3 — 500-bot staged run (includes AV fill)

- **Owner:** `monorepo-test-runner`
- **Status:** open
- **Depends on:** S6.2

### S6.4 — 1000-bot staged run

- **Owner:** `monorepo-test-runner`
- **Status:** open
- **Depends on:** S6.3

### S6.5 — 3000-bot staged run

- **Owner:** `monorepo-test-runner`
- **Status:** open
- **Depends on:** S6.4

### S6.6 — StateManager partition decision

- **Owner:** `human` (lead engineer)
- **Status:** open
- **Depends on:** S6.5
- **Goal:** Decide whether to partition based on measured P99 at 3000
  bots. If `< 500 ms`, ship single-process. If `> 500 ms`, ship
  active/passive failover or account-hash partitioning per
  [`Spec/02_STATEMANAGER.md#scale-out-options`](../Spec/02_STATEMANAGER.md#scale-out-options).

### S6.7 — Final acceptance checklist run

- **Owner:** `monorepo-test-runner`
- **Status:** open
- **Depends on:** S6.6
- **Goal:** Run a 12-hour smoke at the final topology, capture metrics
  + Grafana screenshots, verify every line in
  [`Spec/00_VISION.md#acceptance-criteria`](../Spec/00_VISION.md#acceptance-criteria).
