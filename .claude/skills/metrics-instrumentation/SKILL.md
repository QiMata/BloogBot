---
name: metrics-instrumentation
description: Add new meters / counters / histograms with spec-compliant names and bounded label cardinality, exported via OpenTelemetry. Use when a service needs new observability metrics.
trigger: add a metric, new meter, counter histogram, OpenTelemetry, instrument a service, metric naming, cardinality budget, observability
---

# Metrics Instrumentation

## Goal

Add observability metrics to a service with names and labels that obey the metric
naming spec and stay within the cardinality budget, exported through the shared
OpenTelemetry pipeline.

## Inputs

- What to measure (count / duration / value), the meter name, and the labels
  (kept low-cardinality).
- Key references:
  - **Naming + cardinality spec:** `docs/Spec/10_METRICS.md` (the authoritative
    naming rules and budgets — e.g. high-cardinality labels like `account` only on
    bounded metrics).
  - OTel pipeline: `UI/Systems/Systems.ServiceDefaults/Extensions.cs`
    (`ConfigureOpenTelemetry` / `.WithMetrics()`).
  - Per-service registration: `Services/<service>/Program.cs` using
    `System.Diagnostics.Metrics` (`new Meter(...)`, `CreateCounter`/
    `CreateHistogram`).
- Area rules: `.github/instructions/services.instructions.md`.

> **Note:** A centralized `Exports/Telemetry/MetricsRegistry.cs` is described in
> the spec but may not exist on this branch yet — define meters in the owning
> service per Spec/10 until the registry lands.

## Preconditions

- A meter name following the `wwow.<service>.<domain>.<name>` convention from
  Spec/10.
- The service builds green and uses ServiceDefaults for telemetry.

## Procedure

1. In the service's `Program.cs` (or a dedicated metrics class), create the meter:
   `var meter = new Meter("wwow.<service>.<domain>", "1.0");`.
2. Create the instrument: `meter.CreateCounter<long>("wwow.<service>.<domain>.<name>")`
   (or `CreateHistogram`), naming it per Spec/10.
3. Emit at the observation point with **bounded** labels:
   `counter.Add(1, new KeyValuePair<string, object?>("<key>", "<value>"))`.
4. Verify the meter is included in the OTel `.WithMetrics()` registration in
   `Systems.ServiceDefaults/Extensions.cs`.
5. Check the label set against the Spec/10 cardinality budget.

## Verification

- Build: `.\scripts\build.ps1`; fast suite: `.\scripts\test-fast.ps1`.
- Run the service and confirm the metric appears on its telemetry endpoint with
  the expected name and labels.
- Confirm no unbounded label (e.g. raw GUIDs) was added.

## Outputs

- New meter/instrument in the service + OTel export wiring.
- Doc note in `docs/Spec/10_METRICS.md` if a new metric family is introduced.

## Failure modes and recovery

- **Unbounded cardinality** (per-entity labels) blows up the metrics store —
  follow the Spec/10 budget.
- **Off-spec name** breaks dashboards/aggregation — use `wwow.<service>.<domain>.<name>`.
- **Meter not registered** in OTel → metric silently absent.

## Related skills

- [[logging-noise-reduction]] — the sibling observability concern.
- [[docker-stack-extension]] — new services should ship metrics from day one.
- [[wpf-dashboard-panel]] — surface metrics in the operator console.
- Reference: `docs/Spec/10_METRICS.md`.
