# Observability

> How to see what the bots and services are doing. The detailed contracts are
> [`Spec/10_METRICS.md`](Spec/10_METRICS.md) (metrics) and
> [`Spec/11_LOGGING.md`](Spec/11_LOGGING.md) (logging); this page is the
> practical pointer.

## Logging

Services log via **Serilog**. Local run output lands under `logs/`, which is
also where physics/replay **calibration handoffs** are recorded — required
reading before iterative tuning (see the Calibration Anti-Loop rule in
[`../AGENTS.md`](../AGENTS.md) §15). The logging contract (levels, structure,
what to log) is [`Spec/11_LOGGING.md`](Spec/11_LOGGING.md).

> When scanning large log files, prefer a summarizing pass over reading them
> whole into context — see the token-efficient tooling guidance in
> [`../CLAUDE.md`](../CLAUDE.md).

## Metrics & live state

- **`ActivitySnapshot`** is the primary live-state signal — bots publish major
  state deltas to StateManager, which the WPF UI (`UI/WoWStateManagerUI`) and
  live tests poll. Contract: [`Spec/10_METRICS.md`](Spec/10_METRICS.md), wire
  shape: [`api-contracts.md`](api-contracts.md).
- Live tests poll StateManager APIs, fail fast on disconnect/crash, and capture
  latest screenshots/state dumps — see [`testing.md`](testing.md).

## Errors

The error taxonomy (categories, how failures are classified and surfaced) is
[`Spec/12_ERROR_TAXONOMY.md`](Spec/12_ERROR_TAXONOMY.md). For "which file owns a
given failure" see [`troubleshooting.md`](troubleshooting.md).
