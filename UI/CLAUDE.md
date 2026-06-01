# UI — Desktop, Web & Orchestration

User-facing surfaces and .NET Aspire orchestration. Depends on the `Services/*`
layer; never the reverse.

## Projects

| Path | Stack | Purpose |
|------|-------|---------|
| `WoWStateManagerUI/` | WPF (.NET 8) | Desktop UI: state monitoring, activity tracking. `Converters/` holds binding converters. |
| `StorylineManager/` | ASP.NET | Quest/storyline authoring + progression UI. |
| `Systems/Systems.AppHost/` | .NET Aspire | Service orchestration host (wires services + ports). |
| `Systems/Systems.ServiceDefaults/` | Aspire | Shared service defaults (telemetry, health, resilience). |

Legacy (do not extend): `StateManagerUI/`, `WWoW.Systems/`, `Bot/`.

## Special rules

- WPF: keep the UI thread free; marshal long/IO work back via the dispatcher.
- FG-triggering UI must be **state-gated** and must never steal focus or capture
  the cursor.
- Endpoints/ports are wired in `Systems.AppHost`; don't hardcode them downstream.

## Build / test

`.\scripts\build.ps1`; `Tests/Systems.ServiceDefaults.Tests` covers ServiceDefaults.

> Path-specific agent rules: `.github/instructions/ui.instructions.md`.
