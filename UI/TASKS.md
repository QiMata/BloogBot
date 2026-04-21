# UI Tasks

## Scope
- Directory: `UI`
- Master tracker: `MASTER-SUB-035`
- This file is an umbrella router only; implementation details live in child `TASKS.md` files.
- Child task files:
- `UI/Systems/Systems.AppHost/TASKS.md` (`MASTER-SUB-036`)
- `UI/Systems/Systems.ServiceDefaults/TASKS.md` (`MASTER-SUB-037`)
- `UI/WoWStateManagerUI/TASKS.md` (`MASTER-SUB-038`)

## Execution Rules
1. Keep this file as routing-only; do not duplicate child implementation backlog here.
2. Execute child files one-by-one in the order listed under `P0 Active Tasks`.
3. Keep commands simple and one-line.
4. Never blanket-kill `dotnet`; process cleanup must stay repo-scoped and evidenced.
5. Move completed umbrella items to `UI/TASKS_ARCHIVE.md` in the same session.
6. Every pass must update `Session Handoff` with `Pass result` (`delta shipped` or `blocked`) and one executable `Next command`.
7. Start each pass by running the prior `Session Handoff -> Next command` verbatim before any broader scan/search.
8. After shipping one local delta, set `Session Handoff -> Next command` to the next queue-file read command and execute it in the same session.

## Environment Checklist
- [x] `UI/Systems/Systems.AppHost/TASKS.md` is expanded with direct IDs `SAH-MISS-001..006`.
- [x] `UI/Systems/Systems.ServiceDefaults/TASKS.md` is expanded with direct IDs `SSD-MISS-001..006`.
- [x] `UI/WoWStateManagerUI/TASKS.md` is expanded with direct IDs `UI-MISS-001..004`.
- [x] `docs/TASKS.md` maps these child files under `MASTER-SUB-035..038` and currently points queue progression into this UI section.

## Evidence Snapshot (2026-02-25)
- Child queue files are concrete and session-ready:
- `UI/Systems/Systems.AppHost/TASKS.md` defines `SAH-MISS-001..006` and has a `Session Handoff`.
- `UI/Systems/Systems.ServiceDefaults/TASKS.md` defines `SSD-MISS-001..006` and has a `Session Handoff`.
- `UI/WoWStateManagerUI/TASKS.md` defines `UI-MISS-001..004` and has a `Session Handoff`.
- `dotnet build UI/Systems/Systems.AppHost/Systems.AppHost.csproj --configuration Release` succeeded (`0 warnings`, `0 errors`) and confirms `MASTER-SUB-036` baseline compile health.
- `dotnet build UI/Systems/Systems.ServiceDefaults/Systems.ServiceDefaults.csproj --configuration Release` succeeded (`0 warnings`, `0 errors`) and confirms `MASTER-SUB-037` baseline compile health.
- `dotnet build UI/WoWStateManagerUI/WoWStateManagerUI.csproj --configuration Release` succeeded (`0 warnings`, `0 errors`) and confirms `MASTER-SUB-038` baseline compile health.
- Master tracking is aligned:
- `docs/TASKS.md` includes `MASTER-SUB-035..038` entries and queue pointers now target `MASTER-SUB-035` then `MASTER-SUB-036`.

## P0 Active Tasks (Ordered)
1. [x] `UI-UMB-001` Expand and execute `MASTER-SUB-036` (`UI/Systems/Systems.AppHost/TASKS.md`) with concrete IDs.
- Child target: create direct IDs in AppHost local file, then execute top-down.
- Validation command: `dotnet build UI/Systems/Systems.AppHost/Systems.AppHost.csproj --configuration Release`.

2. [x] `UI-UMB-002` Expand and execute `MASTER-SUB-037` (`UI/Systems/Systems.ServiceDefaults/TASKS.md`) with concrete IDs.
- Child target: create direct IDs in ServiceDefaults local file, then execute top-down.
- Validation command: `dotnet build UI/Systems/Systems.ServiceDefaults/Systems.ServiceDefaults.csproj --configuration Release`.

3. [x] `UI-UMB-003` Execute `MASTER-SUB-038` (`UI/WoWStateManagerUI/TASKS.md`) IDs in order: `UI-MISS-001`, then `UI-MISS-002`.
- Child target: remove converter unimplemented path risk and keep UI binding behavior explicit.
- Validation command: `dotnet build UI/WoWStateManagerUI/WoWStateManagerUI.csproj --configuration Release`.

4. [x] `UI-UMB-004` Keep parent/child status sync between `UI/TASKS.md` and `docs/TASKS.md` after each child-file delta.
- Child target: each completed child pass must update master queue status and handoff pointers.
- Validation command: `rg -n "MASTER-SUB-03[6-8]|Current queue file|Next queue file" docs/TASKS.md`.

## Simple Command Set
- `dotnet build UI/Systems/Systems.AppHost/Systems.AppHost.csproj --configuration Release`
- `dotnet build UI/Systems/Systems.ServiceDefaults/Systems.ServiceDefaults.csproj --configuration Release`
- `dotnet build UI/WoWStateManagerUI/WoWStateManagerUI.csproj --configuration Release`
- `rg -n "MASTER-SUB-03[6-8]|Current queue file|Next queue file" docs/TASKS.md`

## Session Handoff
- Last updated: 2026-02-25
- Active task: none. `UI-UMB-001` through `UI-UMB-004` are complete.
- Last delta: synced UI parent/master status after AppHost, ServiceDefaults, and WPF UI child closeouts.
- Pass result: `delta shipped`
- Validation/tests run:
  - `dotnet build UI/Systems/Systems.AppHost/Systems.AppHost.csproj --configuration Release` -> `succeeded (0 warnings, 0 errors)`
  - `dotnet run --project UI/Systems/Systems.AppHost/Systems.AppHost.csproj --configuration Release --no-build --launch-profile local` -> expected preflight failure listing missing `config`/`data` bind-mount sources in this workspace
  - `dotnet test Tests/Systems.ServiceDefaults.Tests/Systems.ServiceDefaults.Tests.csproj --configuration Release --settings Tests/test.runsettings --logger "console;verbosity=minimal"` -> `passed (8/8)`
  - `dotnet build UI/Systems/Systems.ServiceDefaults/Systems.ServiceDefaults.csproj --configuration Release` -> `succeeded (0 warnings, 0 errors)`
  - `dotnet test Tests/WoWStateManagerUI.Tests/WoWStateManagerUI.Tests.csproj --configuration Release --no-restore --settings Tests/test.runsettings --logger "console;verbosity=minimal"` -> `passed (42/42)`
  - `dotnet build UI/WoWStateManagerUI/WoWStateManagerUI.csproj --configuration Release --no-restore` -> `succeeded (0 warnings, 0 errors)`
  - `rg -n "MASTER-SUB-03[6-8]|Current queue file|Next queue file" docs/TASKS.md` -> matched the current handoff command; the previous master queue fields are no longer present in the current docs structure.
- Files changed:
  - `UI/Systems/Systems.AppHost/Program.cs`
  - `UI/Systems/Systems.AppHost/WowServerConfig.cs`
  - `UI/Systems/Systems.AppHost/appsettings.json`
  - `UI/Systems/Systems.AppHost/Properties/launchSettings.json`
  - `UI/Systems/Systems.AppHost/README.md`
  - `UI/Systems/Systems.AppHost/TASKS.md`
  - `UI/Systems/Systems.AppHost/TASKS_ARCHIVE.md`
  - `UI/Systems/Systems.ServiceDefaults/Extensions.cs`
  - `UI/Systems/Systems.ServiceDefaults/Properties/AssemblyInfo.cs`
  - `UI/Systems/Systems.ServiceDefaults/README.md`
  - `Tests/Systems.ServiceDefaults.Tests/Systems.ServiceDefaults.Tests.csproj`
  - `Tests/Systems.ServiceDefaults.Tests/ServiceDefaultsExtensionsTests.cs`
  - `UI/Systems/Systems.ServiceDefaults/TASKS.md`
  - `UI/Systems/Systems.ServiceDefaults/TASKS_ARCHIVE.md`
  - `Tests/WoWStateManagerUI.Tests/Converters/NullToBoolConverterTests.cs`
  - `Tests/WoWStateManagerUI.Tests/Converters/PathToFilenameConverterTests.cs`
  - `Tests/WoWStateManagerUI.Tests/Converters/ServiceStatusToBrushConverterTests.cs`
  - `UI/WoWStateManagerUI/README.md`
  - `UI/WoWStateManagerUI/TASKS.md`
  - `UI/WoWStateManagerUI/TASKS_ARCHIVE.md`
  - `UI/TASKS.md`
  - `UI/TASKS_ARCHIVE.md`
  - `docs/TASKS.md`
  - `docs/TASKS_ARCHIVE.md`
- Blockers: None.
- Next task: none in this owner.
- Next command: `rg -n "^- \[ \]|Known remaining work|Active task:" --glob TASKS.md`.
