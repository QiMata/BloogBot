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
- [x] `Extensions.cs` is the only implementation file and currently has no direct test coverage.
- [x] No `Tests/*` project currently targets `Systems.ServiceDefaults` extension behavior (`NO_MATCHES_IN_TESTS` from repo scan under `Tests`).
- [x] OpenTelemetry wiring currently adds instrumentation/exporter hooks but no explicit FG/BG role/scenario resource contract (`Extensions.cs:44-82`).
- [x] `MapDefaultEndpoints` maps `/health` and `/alive` only in development (`Extensions.cs:101-109`), with no configuration-driven policy yet.
- [x] Service discovery scheme policy remains commented out (`Extensions.cs:35-38`).

## Evidence Snapshot (2026-02-25)
- `dotnet restore UI/Systems/Systems.ServiceDefaults/Systems.ServiceDefaults.csproj` succeeded.
- `dotnet build UI/Systems/Systems.ServiceDefaults/Systems.ServiceDefaults.csproj --configuration Release` succeeded with `0 Warning(s)` and `0 Error(s)`.
- Core extension seams confirmed in `Extensions.cs`:
- `AddServiceDefaults` entry point (`:18`), `ConfigureOpenTelemetry` (`:44`), `AddStandardResilienceHandler` (`:29`), and `MapDefaultEndpoints` (`:101`).
- Gating/contract gaps confirmed:
- health endpoints restricted by `app.Environment.IsDevelopment()` (`:105`);
- `AllowedSchemes` policy is commented out (`:35-38`).
- Targeted tests scan produced `NO_MATCHES_IN_TESTS` for `Systems.ServiceDefaults|AddServiceDefaults|MapDefaultEndpoints`, confirming no direct consumer test coverage in `Tests/*`.

## P0 Active Tasks (Ordered)
1. [ ] `SSD-MISS-001` Add direct automated coverage for `AddServiceDefaults`, `ConfigureOpenTelemetry`, and `MapDefaultEndpoints`.
- Evidence: no current test project references `Systems.ServiceDefaults` extension methods.
- Files: `UI/Systems/Systems.ServiceDefaults/Extensions.cs`, `Tests/**` (new/updated consuming tests).
- Required breakdown: add tests that validate service registrations, health check tags, and endpoint mapping behavior.
- Validation: `dotnet build UI/Systems/Systems.ServiceDefaults/Systems.ServiceDefaults.csproj --configuration Release`.

2. [ ] `SSD-MISS-002` Add configuration-driven telemetry resource fields for parity runs.
- Evidence: telemetry setup currently adds instrumentation/exporter but does not set explicit FG/BG role/scenario correlation contract.
- Files: `UI/Systems/Systems.ServiceDefaults/Extensions.cs`, `UI/Systems/Systems.ServiceDefaults/README.md`.
- Required breakdown: define standard resource tags (service name, bot role, scenario/test id), wire through configuration, and document required keys.
- Validation: `dotnet build UI/Systems/Systems.ServiceDefaults/Systems.ServiceDefaults.csproj --configuration Release`.

3. [ ] `SSD-MISS-003` Make health endpoint exposure policy configuration-driven instead of development-only hardcoding.
- Evidence: `MapDefaultEndpoints` currently gates `/health` and `/alive` on `app.Environment.IsDevelopment()` only.
- Files: `UI/Systems/Systems.ServiceDefaults/Extensions.cs`, `UI/Systems/Systems.ServiceDefaults/README.md`.
- Required breakdown: add explicit config flag(s) for endpoint exposure policy and document safe defaults.
- Validation: `dotnet build UI/Systems/Systems.ServiceDefaults/Systems.ServiceDefaults.csproj --configuration Release`.

4. [ ] `SSD-MISS-004` Add configurable resilience defaults for deterministic test runs.
- Evidence: `AddStandardResilienceHandler()` is always applied; deterministic integration tests may require tighter/disabled retry behavior.
- Files: `UI/Systems/Systems.ServiceDefaults/Extensions.cs`, `UI/Systems/Systems.ServiceDefaults/README.md`.
- Required breakdown: expose resilience policy knobs via configuration and document a deterministic test profile.
- Validation: `dotnet build UI/Systems/Systems.ServiceDefaults/Systems.ServiceDefaults.csproj --configuration Release`.

5. [ ] `SSD-MISS-005` Add explicit service discovery scheme policy wiring and documentation.
- Evidence: allowed-scheme configuration is commented out and has no enforced contract.
- Files: `UI/Systems/Systems.ServiceDefaults/Extensions.cs`, `UI/Systems/Systems.ServiceDefaults/README.md`.
- Required breakdown: define supported scheme policy (default + override), implement configuration binding, and document behavior.
- Validation: `dotnet build UI/Systems/Systems.ServiceDefaults/Systems.ServiceDefaults.csproj --configuration Release`.

6. [ ] `SSD-MISS-006` Simplify and correct README command/integration guidance.
- Evidence: README is broad and repeats generic guidance; task execution needs concise command-first instructions.
- Files: `UI/Systems/Systems.ServiceDefaults/README.md`, `UI/Systems/Systems.ServiceDefaults/TASKS.md`, `UI/TASKS.md`.
- Required breakdown: keep one build command, one integration snippet, and one diagnostics snippet aligned to current repo paths.
- Validation: `dotnet build UI/Systems/Systems.ServiceDefaults/Systems.ServiceDefaults.csproj --configuration Release`.

## Simple Command Set
- `dotnet build UI/Systems/Systems.ServiceDefaults/Systems.ServiceDefaults.csproj --configuration Release`
- `dotnet build UI/Systems/Systems.AppHost/Systems.AppHost.csproj --configuration Release`
- `rg -n "AddServiceDefaults|ConfigureOpenTelemetry|MapDefaultEndpoints|AddStandardResilienceHandler" UI/Systems/Systems.ServiceDefaults/Extensions.cs`

## Session Handoff
- Last updated: 2026-02-25
- Active task: `SSD-MISS-001` (add direct automated coverage for extension methods).
- Last delta: Added explicit one-by-one continuity rules (`run prior Next command first`, `set next queue-file read command after delta`) so compaction resumes on the next local `TASKS.md`.
- Pass result: `delta shipped`
- Validation/tests run: `dotnet build UI/Systems/Systems.ServiceDefaults/Systems.ServiceDefaults.csproj --configuration Release` -> succeeded (`0 warnings`, `0 errors`).
- Files changed: `UI/Systems/Systems.ServiceDefaults/TASKS.md`.
- Blockers: None.
- Next task: `SSD-MISS-001`.
- Next command: `Get-Content -Path 'UI/WoWStateManagerUI/TASKS.md' -TotalCount 360`.
