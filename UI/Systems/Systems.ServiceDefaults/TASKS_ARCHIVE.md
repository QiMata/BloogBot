# Task Archive

Completed items moved from TASKS.md.

## Archived Snapshot (2026-04-15) - ServiceDefaults configuration and coverage closeout

- [x] `SSD-MISS-001` Added direct automated coverage for `AddServiceDefaults`, `ConfigureOpenTelemetry`, and `MapDefaultEndpoints`.
- [x] `SSD-MISS-002` Added configuration-driven telemetry resource fields for parity runs.
- [x] `SSD-MISS-003` Made health endpoint exposure policy configuration-driven outside Development.
- [x] `SSD-MISS-004` Added configurable standard HTTP resilience enablement for deterministic test runs.
- [x] `SSD-MISS-005` Added service discovery allowed-scheme policy wiring and documentation.
- [x] `SSD-MISS-006` Replaced broad README guidance with concise current commands and integration snippets.
- Validation:
  - `dotnet test Tests/Systems.ServiceDefaults.Tests/Systems.ServiceDefaults.Tests.csproj --configuration Release --settings Tests/test.runsettings --logger "console;verbosity=minimal"` -> `passed (8/8)`
  - `dotnet build UI/Systems/Systems.ServiceDefaults/Systems.ServiceDefaults.csproj --configuration Release` -> `succeeded (0 warnings, 0 errors)`
  - Parallel validation caveat: an earlier simultaneous `dotnet build` and `dotnet test` collided on `obj/Release/Systems.ServiceDefaults.dll`; rerunning build alone succeeded.
