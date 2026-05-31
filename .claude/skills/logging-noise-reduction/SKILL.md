---
name: logging-noise-reduction
description: Identify noisy log categories and apply level overrides / burst suppression per the logging spec, plus container log rotation. Use when logs are too verbose or a category floods the output.
trigger: reduce log noise, noisy logger, burst suppression, log level override, logging profile, Serilog config, log rotation, quiet logs
---

# Logging Noise Reduction

## Goal

Quiet a noisy log category by adjusting its level and/or applying burst
suppression per the logging spec, and ensure container logs rotate so they don't
fill disk — without losing the signal needed for triage.

## Inputs

- The noisy logger category and the desired level / suppression threshold.
- Key references:
  - **Logging spec:** `docs/Spec/11_LOGGING.md` (profiles Quiet/Normal/Debug/Trace,
    burst-suppression rules, per-container defaults, required structured fields).
  - Service logging config: `Services/<service>/Program.cs` (Serilog host config)
    and `Config/LoggingProfile.json`.
  - Container log driver: `docker-compose*.yml` (logging `max-size` / `max-file`).
- Area rules: `.github/instructions/services.instructions.md` (+
  `.github/instructions/infrastructure.instructions.md` for compose).

> **Note:** Spec/11 references `Exports/Telemetry/{StructuredLogging,BurstSuppressor}.cs`
> which may not exist on this branch yet — apply level overrides via the existing
> Serilog config until the suppression helper lands; treat Spec/11 as authoritative.

## Preconditions

- You have identified the exact category string (e.g.
  `"WoWStateManager.Movement"`) from the actual log output.
- The owning service builds green.

## Procedure

1. Confirm the noisy category from real logs (use Codex to scan large log files
   rather than reading them inline).
2. In the service's logging config / `Config/LoggingProfile.json`, set a category
   override (e.g. `"WoWStateManager.Movement": "Warning"`) per the Spec/11 profile.
3. For bursty categories, add a suppression entry with a `MaxPerMinute` threshold
   and emit a `[SUPPRESSED]` summary metric (`wwow.log.suppressed_total{category}`)
   so suppression is observable (see [[metrics-instrumentation]]).
4. Set container log rotation in compose: `logging.driver: local`, `max-size`,
   `max-file` per the Spec/11 per-container table.
5. Re-run and confirm the signal you need is still present at the chosen level.

## Verification

- Build: `.\scripts\build.ps1`; fast suite: `.\scripts\test-fast.ps1`.
- Run the service and confirm the category volume dropped and important events are
  still logged.
- Confirm container logs rotate at the configured size.

## Outputs

- Updated logging config / `Config/LoggingProfile.json` + (optional) compose log
  rotation.
- `[SUPPRESSED]` metric if burst suppression was added.

## Failure modes and recovery

- **Over-suppressing** hides real errors — never drop Warning/Error for a category
  that can fail meaningfully.
- **No rotation** → disk fills from container logs.
- **Suppressing silently** — always emit the suppression summary so the loss is
  visible.

## Related skills

- [[metrics-instrumentation]] — the suppression-count metric.
- [[config-hot-reload-subscriber]] — make logging levels live-reloadable.
- [[docker-stack-extension]] — set rotation when adding a container.
- Reference: `docs/Spec/11_LOGGING.md`.
