---
name: config-hot-reload-subscriber
description: Add an IConfigSubscriber for a new reloadable config section so a running service applies changes without restart, with ACK/rollback. Use when a config section must hot-reload (the feature-flag / live-tunable path).
trigger: hot reload config, IConfigSubscriber, reloadable config section, feature flag, live config change, ConfigChangedEvent, apply config without restart
---

# Config Hot-Reload Subscriber

## Goal

Make a config section live-reloadable: implement `IConfigSubscriber` for that
scope so when StateManager broadcasts a `ConfigChangedEvent` (file watcher or WPF
edit), the running service validates and applies it, ACKs on success, and the
system rolls back on NACK/timeout. This is the repo's feature-flag / live-tunable
mechanism.

## Inputs

- The config scope name and the runtime state it controls.
- Key references:
  - **Contract (spec):** `docs/Spec/14_CONFIG.md` defines `IConfigSubscriber`
    (`Scope`, `TryApplyAsync(ConfigChangedEvent, ct)` → `ConfigApplyResult`), the
    `ConfigChangedEvent` shape, ACK collection (`HotReloadAckTimeoutMs`, default
    2000ms), and rollback semantics.
  - Config files: `Config/*.json` (e.g. `Config/StateManagerSettings.json`,
    `Config/Pathfinding.json`, `Config/LoggingProfile.json`).
  - Likely registration host: `Services/WoWStateManager/Program.cs` (DI).
- Area rules: `.github/instructions/config.instructions.md`.

> **Status:** As of this branch the `IConfigSubscriber` interface is **spec-only**
> in `docs/Spec/14_CONFIG.md` — no concrete subscriber exists yet. When you
> implement the first one, treat the spec as authoritative and confirm the
> interface location once it lands in `Exports/`/`Services/`.

## Preconditions

- The target config section is documented in `docs/Spec/14_CONFIG.md` as
  hot-reloadable (add it there first if not).
- The owning service builds green.

## Procedure

1. Define/confirm the reloadable scope name and which `Config/*.json` section it
   maps to.
2. Create `<Subsystem>ConfigSubscriber` implementing `IConfigSubscriber`; set
   `Scope` to the scope string.
3. Implement `TryApplyAsync(ConfigChangedEvent evt, ct)`: deserialize the payload,
   **validate**, apply to runtime state, and return
   `ConfigApplyResult(Applied: true)` — or `(Applied: false, FailureReason: …)` on
   a validation error (see [[failure-reason-mapping]]).
4. Register the subscriber in DI:
   `services.AddSingleton<IConfigSubscriber, <Subsystem>ConfigSubscriber>()`.
5. Confirm the broadcast→ACK→(save|rollback) flow per Spec/14: all-ACK emits
   `SaveAck{Applied:true}`; any NACK/timeout rolls back the file and reports the
   failed subscribers.

## Verification

- Build the owning service (e.g.
  `dotnet build Services/WoWStateManager/WoWStateManager.csproj`).
- If `Config/schema/*.schema.json` exists for the section, validate the edited
  JSON against it.
- Edit the config at runtime (or via the WPF config editor) and confirm the change
  applies without restart and an invalid edit rolls back.

## Outputs

- New `<Subsystem>ConfigSubscriber` + DI registration.
- `docs/Spec/14_CONFIG.md` update marking the section reloadable.

## Failure modes and recovery

- **Applying before validating** — a bad payload corrupts live state; validate,
  then apply, then ACK.
- **No rollback on partial failure** — honor the all-or-nothing ACK contract.
- **Blocking past the ACK timeout** — keep `TryApplyAsync` fast; default window is
  2000ms.

## Related skills

- [[failure-reason-mapping]] — the `FailureReason` a NACK reports.
- [[wpf-dashboard-panel]] — the config-editor UI that triggers reloads.
- [[metrics-instrumentation]] / [[logging-noise-reduction]] — common reloadable
  sections.
- Reference: `docs/Spec/14_CONFIG.md`.
