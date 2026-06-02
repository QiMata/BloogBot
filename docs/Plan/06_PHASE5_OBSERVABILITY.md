# Plan 06 â€” Phase 5: Observability + Long-Term Performance History

## Why this is Phase 5 now (not Phase 1)

The 2026-05-12 design refinement moved action/task work to Phase 1.
Observability deferred until after the substrate works, because
metrics without working bots aren't operator-useful.

## Goal

Wire `System.Diagnostics.Metrics` everywhere, ship Prometheus + Grafana
in the Docker stack, normalize logging profiles, populate the UI
Long-Term Performance History panel from
[`Spec/09_UI.md`](../Spec/09_UI.md), and stand up the StateManager
summary APIs the WPF Dashboard consumes.

This phase also lands the **derived counters** at StateManager (per
[`Spec/10_METRICS.md#required-derived-counters-for-the-history-panel`](../Spec/10_METRICS.md#required-derived-counters-for-the-history-panel))
â€” XP/hour by faction Ã— level band, gold deltas, AH activity, travel-leg
duration, stuck incidents â€” so the operator can answer
"how have my bots been doing?" over a 6-hour / 7-day / 90-day window.

## Entry pre-requisite

Phase 1 + Phase 2 + Phase 3 in flight. The bots need to be doing
*something* worth measuring.

## Exit criteria

- [ ] Every service emits the metrics listed in
      [`Spec/10_METRICS.md`](../Spec/10_METRICS.md) Required-Counters /
      -Histograms / -Gauges tables.
- [ ] `/metrics` endpoint exposed by every HTTP-capable service;
      tiny HTTP listener for socket-only services on `<port>+1000`.
- [ ] `docker-compose.vmangos-linux.yml` adds `wwow-prometheus` and
      `wwow-grafana` services with rotated logs.
- [ ] Default `Grafana` dashboard JSON in `docker/grafana/dashboards/`
      renders bot population, activities, OnDemand instances, pathfinding.
- [ ] `LoggingProfile` system implemented in
      `Exports/Telemetry/`; every service resolves a profile at
      startup; `Logging.Profile = "Normal"` is the default.
- [ ] Burst-suppression wrapper exists and is wired into all 6
      container categories from [`Spec/11_LOGGING.md`](../Spec/11_LOGGING.md).
- [ ] StateManager exposes the 8 summary APIs from
      [`Spec/02_STATEMANAGER.md#dashboard-summary-api`](../Spec/02_STATEMANAGER.md#dashboard-summary-api).
- [ ] All connect/disconnect logs normalized (no
      `pathfinding-service` healthcheck warnings).
- [ ] `FailureReasonCatalogTests` green.
- [ ] 50-bot smoke run: no log noise floods the operator console.

## Slots

### S5.1 â€” Telemetry library skeleton

- **Owner:** `monorepo-worker`
- **Status:** open
- **Depends on:** S0.5
- **Owned paths:**
  - `Exports/Telemetry/**`
- **Read-only paths:**
  - `docs/Spec/10_METRICS.md`
  - `docs/Spec/11_LOGGING.md`
  - `docs/Spec/12_ERROR_TAXONOMY.md`
- **Goal:** Land a `MetricsRegistry`, `StructuredLogging`,
  `BurstSuppressor`, `LoggingProfile`. Wire OpenTelemetry exporter
  factory. Add unit tests proving:
  - A counter increments by 1 for each call.
  - A histogram records `Stopwatch.Elapsed` correctly.
  - Burst-suppressor caps a noisy logger to `MaxPerMinute`.
- **Success criteria:**
  - [ ] `dotnet test Tests/Telemetry.Tests` green.

### S5.2 â€” Wire StateManager metrics

- **Owner:** `monorepo-worker`
- **Status:** open
- **Depends on:** S5.1
- **Owned paths:**
  - `Services/WoWStateManager/Metrics/**`
  - `Services/WoWStateManager/Program.cs`
- **Read-only paths:** all of `Services/WoWStateManager/` for instrumentation points
- **Goal:** Emit the 14 StateManager-prefixed metrics from
  [`Spec/10_METRICS.md`](../Spec/10_METRICS.md). `/metrics` returns
  Prometheus format on port `9000`.
- **Success criteria:**
  - [ ] `curl http://localhost:9000/metrics` shows all required names.
  - [ ] Cardinality budget tests pass (label sets per metric â‰¤ budget).

### S5.3 â€” Wire BotRunner metrics

- **Owner:** `monorepo-worker`
- **Status:** open
- **Depends on:** S5.1
- **Owned paths:**
  - `Exports/BotRunner/Metrics/**`
  - `Services/BackgroundBotRunner/Program.cs`
  - `Services/ForegroundBotRunner/Program.cs`
- **Goal:** Emit `wwow.botrunner.*` metrics. Hosted under a per-bot HTTP listener (port `6088 + bot_index`) for BG; FG forwards via IPC for hosting reasons.

### S5.4 â€” Wire PathfindingService + SceneDataService metrics

- **Owner:** `monorepo-worker`
- **Status:** open
- **Depends on:** S5.1
- **Owned paths:**
  - `Services/PathfindingService/Metrics/**`
  - `Services/SceneDataService/Metrics/**`
- **Goal:** Add `/metrics` HTTP listener on port `<basePort>+1000`.

### S5.5 â€” Docker stack: Prometheus + Grafana

- **Owner:** `monorepo-worker`
- **Status:** open
- **Depends on:** S5.2, S5.3, S5.4
- **Owned paths:**
  - `docker-compose.vmangos-linux.yml` (additions only)
  - `docker/prometheus/**`
  - `docker/grafana/**`
- **Goal:** Add `wwow-prometheus` and `wwow-grafana` containers with
  rotated logs and pre-loaded dashboards.
- **Success criteria:**
  - [ ] `docker compose up -d` starts Prometheus + Grafana.
  - [ ] Grafana renders the WWoW Living Server dashboard.

### S5.6 â€” Logging profiles

- **Owner:** `monorepo-worker`
- **Status:** open
- **Depends on:** S5.1
- **Owned paths:**
  - `Exports/Telemetry/LoggingProfile.cs`
  - Service `Program.cs` files (logging hookup only)
- **Goal:** Profile resolution at startup; per-container defaults from
  [`Spec/11_LOGGING.md#per-container-decision`](../Spec/11_LOGGING.md#per-container-decision).
- **Success criteria:**
  - [ ] 50-bot smoke run produces < 100 Warnings total across all
        services in a 60-minute window (operator quiet target).

### S5.7 â€” Burst suppression hookup

- **Owner:** `monorepo-worker`
- **Status:** open
- **Depends on:** S5.1, S5.6
- **Owned paths:** same as S5.6 (logger registration sites)
- **Goal:** Every noisy category from the spec is registered in burst
  suppression. The healthcheck disconnect at PathfindingService logs
  at Debug, not Warning.

### S5.8 â€” StateManager summary APIs

- **Owner:** `monorepo-worker`
- **Status:** open
- **Depends on:** S5.2
- **Owned paths:**
  - `Services/WoWStateManager/Summaries/**`
  - `Services/WoWStateManager/Listeners/StateManagerSocketListener.cs`
  - `Exports/BotCommLayer/communication.proto` (add Summary protos)
- **Spec contracts:** [`Spec/02_STATEMANAGER.md#dashboard-summary-api`](../Spec/02_STATEMANAGER.md#dashboard-summary-api),
  [`Spec/08_PROTOCOLS.md`](../Spec/08_PROTOCOLS.md)
- **Goal:** All 8 summary endpoints reachable from a client via
  protobuf.
- **Success criteria:**
  - [ ] `Tests/WoWStateManagerListenerSummaryTests` round-trips each
        summary call.

### S5.9 â€” Normalize connect/disconnect logging

- **Owner:** `monorepo-worker`
- **Status:** open
- **Depends on:** S5.6, S5.7
- **Owned paths:**
  - `Exports/BotCommLayer/ProtobufSocketServer.cs` (existing logs)
  - `Exports/BotCommLayer/ProtobufAsyncSocketServer.cs`
- **Goal:** Clean EOF after a complete framed request â†’ Debug.
  Truncated mid-frame â†’ Warning. Healthcheck pattern â†’ Debug.

### S5.10 â€” Derived counters at StateManager

- **Owner:** `monorepo-worker`
- **Status:** open
- **Depends on:** S5.2 (snapshot ingest sees deltas)
- **Owned paths:**
  - `Services/WoWStateManager/Metrics/DerivedCounters.cs`
- **Goal:** Populate the bounded-cardinality derived counters listed
  in [`Spec/10_METRICS.md`](../Spec/10_METRICS.md) by diffing
  successive snapshots at the StateManager rather than emitting raw
  per-tick metrics from each bot.

### S5.11 â€” Long-term performance history API

- **Owner:** `monorepo-worker`
- **Status:** open
- **Depends on:** S5.10
- **Owned paths:**
  - `Services/WoWStateManager/Summaries/PerformanceHistoryProvider.cs`
- **Goal:** `GetPerformanceHistory(window)` aggregates derived counters
  for the UI panel. 1-min resolution Ã— 6h, 1-h Ã— 7d, 1-d Ã— 90d.

### S5.12 â€” Phase 5 smoke test

- **Owner:** `monorepo-test-runner`
- **Status:** open
- **Depends on:** S5.1–S5.11
- **Goal:** Run a sustained ~50-bot mix (autonomous + OnDemand) for
  60 minutes:
  - Grafana dashboard renders live data.
  - UI history panel populated.
  - Warnings under threshold.
  - All required counters / histograms / gauges populated.

