# Spec 11 — Logging

## Goals

- Default operation is **quiet enough that any Warning is signal.**
- Diagnostics are opt-in by category, account, activity, or duration.
- Docker containers rotate logs without operator intervention.

## Logging profile

Every container/service has a `LoggingProfile` resolved at startup:

```csharp
public sealed record LoggingProfile
{
    public LogLevel DefaultLevel { get; init; }                           // Information
    public IReadOnlyDictionary<string, LogLevel> CategoryOverrides { get; init; }
    public IReadOnlyList<string> DiagnosticCategories { get; init; }      // gated by flag
    public BurstSuppression BurstSuppression { get; init; }
    public IReadOnlyList<string> CorrelationFields { get; init; }
    public string ContainerDriver { get; init; }                          // "local" preferred
    public LogRetention Retention { get; init; }
}

public sealed record BurstSuppression(
    int MaxPerMinute,
    int SuppressedReportInterval,
    IReadOnlyList<string> Categories);
```

Profile presets:

| Profile | Default | Path diag | Snapshot dumps | Connect churn |
|---|---|---|---|---|
| `Quiet` | Warning | off | off | rate-limited |
| `Normal` | Information | off | off | rate-limited |
| `Debug` | Debug | on | sampled | full |
| `Trace` | Trace | on | full | full |

Default profile: `Normal`. Operators switch to `Debug`/`Trace` via UI
or config edit.

## Required structured fields

Every log line emits as structured (JSON) with these fields:

| Field | Type | When |
|---|---|---|
| `timestamp` | ISO-8601 with ms | always |
| `level` | enum | always |
| `service` | string | always |
| `instance` | string | always |
| `category` | string | always (logger name) |
| `correlation_id` | string | per-request scope |
| `account` | string | per-bot scope |
| `character` | string | per-bot scope |
| `activity_id` | string | per-activity scope |
| `activity` | string | per-activity scope |
| `location` | string | per-activity scope |
| `map` | uint32 | per-bot scope (movement/scene) |
| `task` | string | per-task scope |
| `path_request_id` | uint64 | path-related |
| `transport_entry` | uint32 | transport state machine |
| `result` | enum | success/failure/timeout |
| `reason` | string | from error taxonomy on failure |

Implementation uses `Microsoft.Extensions.Logging` scopes; the
`Exports/Telemetry/StructuredLogging.cs` wrapper standardizes scope
creation.

## Burst suppression

Repeated identical log lines (same level + category + message hash) are
suppressed after `MaxPerMinute` occurrences within a 60-second window.
The suppressor emits:

```
[SUPPRESSED] category=path.routing N events suppressed in last 60s
```

once per `SuppressedReportInterval` seconds. The total suppressed count
is also emitted as `wwow.log.suppressed_total{category=...}`.

## Container logging

### Docker driver

Prefer the `local` driver where supported:

```yaml
services:
  wwow-pathfinding:
    logging:
      driver: local
      options:
        max-size: 10m
        max-file: "5"
```

For environments without `local`, fall back to `json-file` with
`max-size` and `max-file` set.

### Per-container decision

| Container | Default | Notes |
|---|---|---|
| `wow-realmd` | Quiet | Suppress auth heartbeat; retain auth failures |
| `wow-mangosd` | Normal | Keep world errors, SOAP failures, DB reconnects; suppress movement/chat spam |
| `wwow-pathfinding` | Normal | Keep startup nav signatures, route-pack warmup, timeouts; gate route dumps |
| `wwow-scene-data` | Normal | Keep cache load + fatal data errors; sample tile queries |
| `background-bot-runner` | Normal | Keep login/task/action failures; suppress snapshot heartbeat |
| `WoWStateManager` | Normal | Keep bot lifecycle, leases, activity, dispatch failures; normalize connect/disconnect churn |
| `wwow-prometheus` | Quiet | Standard Prometheus log policy |
| `wwow-grafana` | Quiet | Standard Grafana log policy |

## Diagnostic toggles

Diagnostics that are **off by default** and require explicit enable:

- **Path diagnostics** — full Detour input/output dumps. Enabled via
  `Logging.EnablePathDiagnostics: true` or per-account scope.
- **Snapshot dumps** — full WoWActivitySnapshot proto dump. Enabled via
  `Logging.EnableSnapshotDumps: true`.
- **Physics replay** — per-frame physics state log. Enabled via
  `Logging.EnablePhysicsReplay: true`.
- **Packet hex dumps** — raw packet hex. Enabled via
  `Logging.EnablePacketHex: true`.

These are also enabled by attaching a `LoggingProfile` scope to a
specific account or activity id:

```csharp
using (logger.BeginScope(new { account = "TESTBOT1" }))
{
    // any code in this scope gets per-account profile applied
}
```

## Noise reduction rules

- **Connect/disconnect churn** is Debug when the disconnect is
  expected (clean EOF, healthcheck, lifecycle). Warning only when the
  reason is unexpected or the rate exceeds threshold.
- **Pathfinding route dumps** disabled by default; sampled on failure.
- **Snapshot dumps** disabled by default; replaced with compact
  structured state summaries.
- **Startup logs** include version, git SHA, config profile, data-root
  signature, exposed endpoints — emitted once.
- **Per-tick snapshots** are Debug (suppressed at Normal).

## Test-side logging

Tests use `Tests.Infrastructure/TestLoggerFactory.cs` to redirect logs
to TRX attachments. Live test artifacts (screenshots, JSON reports)
live under `TestResults/LiveLogs/<test>.log` and are overwritten per
run — they are not tracked in git.

## Correlation ids

- **Bot scope:** `correlation_id` = `<account>:<character>:<sessionId>`.
- **Activity scope:** `correlation_id` = `<activityInstanceId>`.
- **Path scope:** `correlation_id` = `<pathRequestId>`.
- **Request scope:** `correlation_id` = UUID generated per inbound
  request.

Operator triage flow: pull a failing metric → find the
`correlation_id` → grep logs → see the full trace.

## Existing code anchors

| Concept | File |
|---|---|
| Logging wrapper (to be added) | `Exports/Telemetry/StructuredLogging.cs` |
| Burst suppression (to be added) | `Exports/Telemetry/BurstSuppressor.cs` |
| Docker compose | `docker-compose.vmangos-linux.yml` |
| Service host logging | `Services/*/Program.cs` (per service) |
| Test logger factory | `Tests/Tests.Infrastructure/TestLoggerFactory.cs` |
