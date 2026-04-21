# Systems.ServiceDefaults Tasks

## Scope
- Directory: `UI/Systems/Systems.ServiceDefaults`
- Project: `UI/Systems/Systems.ServiceDefaults/Systems.ServiceDefaults.csproj`
- Master tracker: `MASTER-SUB-037`
- Primary implementation surface:
  - `UI/Systems/Systems.ServiceDefaults/Extensions.cs`
- Documentation surface:
  - `UI/Systems/Systems.ServiceDefaults/README.md`

## Execution Rules
1. Execute tasks in this file top-down and keep scans limited to files listed in `Scope`.
2. Keep commands simple and one-line.
3. Prioritize deterministic telemetry/resilience behavior for FG/BG parity diagnostics.
4. Do not add broad backlog items; every item must map to a file/symbol contract.
5. Move completed items to `UI/Systems/Systems.ServiceDefaults/TASKS_ARCHIVE.md` in the same session.
6. Every pass must update `Session Handoff` with `Pass result` (`delta shipped` or `blocked`) and one executable `Next command`.
7. Start each pass by running the prior `Session Handoff -> Next command` verbatim before any broader scan/search.
8. After shipping one local delta, set `Session Handoff -> Next command` to the next queue-file read command and execute it in the same session.

## Environment Checklist
- [x] `Extensions.cs` has direct automated coverage in `Tests/Systems.ServiceDefaults.Tests`.
- [x] OpenTelemetry resource fields include configurable service name, bot role, scenario id, and test id.
- [x] `/health` and `/alive` endpoint mapping is configuration-driven outside Development.
- [x] Standard HTTP resilience can be disabled for deterministic integration tests.
- [x] Service discovery allowed-scheme policy is wired through configuration.
- [x] README command/integration guidance is command-first and aligned to current repo paths.

## P0 Active Tasks (Ordered)
None.

Known remaining work in this owner: `0` items.

## Completed P0 Items
- [x] `SSD-MISS-001` Add direct automated coverage for `AddServiceDefaults`, `ConfigureOpenTelemetry`, and `MapDefaultEndpoints`.
- [x] `SSD-MISS-002` Add configuration-driven telemetry resource fields for parity runs.
- [x] `SSD-MISS-003` Make health endpoint exposure policy configuration-driven instead of development-only hardcoding.
- [x] `SSD-MISS-004` Add configurable resilience defaults for deterministic test runs.
- [x] `SSD-MISS-005` Add explicit service discovery scheme policy wiring and documentation.
- [x] `SSD-MISS-006` Simplify and correct README command/integration guidance.

## Simple Command Set
- `dotnet build UI/Systems/Systems.ServiceDefaults/Systems.ServiceDefaults.csproj --configuration Release`
- `dotnet test Tests/Systems.ServiceDefaults.Tests/Systems.ServiceDefaults.Tests.csproj --configuration Release --no-restore --settings Tests/test.runsettings --logger "console;verbosity=minimal"`
- `rg -n "AddServiceDefaults|ConfigureOpenTelemetry|MapDefaultEndpoints|AddStandardResilienceHandler" UI/Systems/Systems.ServiceDefaults/Extensions.cs`

## Session Handoff
- Last updated: 2026-04-15
- Active task: none. `SSD-MISS-001` through `SSD-MISS-006` are complete.
- Last delta: added config-driven telemetry/health/resilience/service-discovery policies, direct extension coverage, and concise README guidance.
- Pass result: `delta shipped`
- Validation/tests run:
  - `dotnet test Tests/Systems.ServiceDefaults.Tests/Systems.ServiceDefaults.Tests.csproj --configuration Release --settings Tests/test.runsettings --logger "console;verbosity=minimal"` -> `passed (8/8)`
  - `dotnet build UI/Systems/Systems.ServiceDefaults/Systems.ServiceDefaults.csproj --configuration Release` -> `succeeded (0 warnings, 0 errors)`
  - Parallel validation caveat: an earlier simultaneous `dotnet build` and `dotnet test` collided on `obj/Release/Systems.ServiceDefaults.dll`; rerunning build alone succeeded.
- Files changed:
  - `UI/Systems/Systems.ServiceDefaults/Extensions.cs`
  - `UI/Systems/Systems.ServiceDefaults/Properties/AssemblyInfo.cs`
  - `UI/Systems/Systems.ServiceDefaults/README.md`
  - `Tests/Systems.ServiceDefaults.Tests/Systems.ServiceDefaults.Tests.csproj`
  - `Tests/Systems.ServiceDefaults.Tests/ServiceDefaultsExtensionsTests.cs`
  - `UI/Systems/Systems.ServiceDefaults/TASKS.md`
  - `UI/Systems/Systems.ServiceDefaults/TASKS_ARCHIVE.md`
- Blockers: none.
- Next command: `Get-Content -Path 'UI/WoWStateManagerUI/TASKS.md' -TotalCount 360`
