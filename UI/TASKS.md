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
1. [ ] `UI-UMB-001` Expand and execute `MASTER-SUB-036` (`UI/Systems/Systems.AppHost/TASKS.md`) with concrete IDs.
- Child target: create direct IDs in AppHost local file, then execute top-down.
- Validation command: `dotnet build UI/Systems/Systems.AppHost/Systems.AppHost.csproj --configuration Release`.

2. [ ] `UI-UMB-002` Expand and execute `MASTER-SUB-037` (`UI/Systems/Systems.ServiceDefaults/TASKS.md`) with concrete IDs.
- Child target: create direct IDs in ServiceDefaults local file, then execute top-down.
- Validation command: `dotnet build UI/Systems/Systems.ServiceDefaults/Systems.ServiceDefaults.csproj --configuration Release`.

3. [ ] `UI-UMB-003` Execute `MASTER-SUB-038` (`UI/WoWStateManagerUI/TASKS.md`) IDs in order: `UI-MISS-001`, then `UI-MISS-002`.
- Child target: remove converter unimplemented path risk and keep UI binding behavior explicit.
- Validation command: `dotnet build UI/WoWStateManagerUI/WoWStateManagerUI.csproj --configuration Release`.

4. [ ] `UI-UMB-004` Keep parent/child status sync between `UI/TASKS.md` and `docs/TASKS.md` after each child-file delta.
- Child target: each completed child pass must update master queue status and handoff pointers.
- Validation command: `rg -n "MASTER-SUB-03[6-8]|Current queue file|Next queue file" docs/TASKS.md`.

## Simple Command Set
- `dotnet build UI/Systems/Systems.AppHost/Systems.AppHost.csproj --configuration Release`
- `dotnet build UI/Systems/Systems.ServiceDefaults/Systems.ServiceDefaults.csproj --configuration Release`
- `dotnet build UI/WoWStateManagerUI/WoWStateManagerUI.csproj --configuration Release`
- `rg -n "MASTER-SUB-03[6-8]|Current queue file|Next queue file" docs/TASKS.md`

## Session Handoff
- Last updated: 2026-02-25
- Active task: `UI-UMB-004` (keep parent/child status sync in `UI/TASKS.md` and `docs/TASKS.md`).
- Last delta: Added explicit one-by-one continuity rules (`run prior Next command first`, `set next queue-file read command after delta`) so compaction resumes on the next local `TASKS.md`.
- Pass result: `delta shipped`
- Validation/tests run: `dotnet build UI/Systems/Systems.AppHost/Systems.AppHost.csproj --configuration Release` -> succeeded (`0 warnings`, `0 errors`); `dotnet build UI/Systems/Systems.ServiceDefaults/Systems.ServiceDefaults.csproj --configuration Release` -> succeeded (`0 warnings`, `0 errors`); `dotnet build UI/WoWStateManagerUI/WoWStateManagerUI.csproj --configuration Release` -> succeeded (`0 warnings`, `0 errors`).
- Files changed: `UI/TASKS.md`, `UI/Systems/Systems.AppHost/TASKS.md`, `UI/Systems/Systems.ServiceDefaults/TASKS.md`, `UI/WoWStateManagerUI/TASKS.md`.
- Blockers: None.
- Next task: `UI-UMB-004`.
- Next command: `Get-Content -Path 'UI/Systems/Systems.AppHost/TASKS.md' -TotalCount 360`.
