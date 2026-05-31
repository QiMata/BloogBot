# UI Tasks

## Scope
- Directory: `UI`
- Master tracker: `MASTER-SUB-035`
- This file is an umbrella router only; implementation details live in child `TASKS.md` files.
- Child task files:
- `UI/Systems/Systems.AppHost/TASKS.md` (`MASTER-SUB-036`)
- `UI/Systems/Systems.ServiceDefaults/TASKS.md` (`MASTER-SUB-037`)
- `UI/WoWStateManagerUI/TASKS.md` (`MASTER-SUB-038`)
- `UI/StorylineManager/TASKS.md` (`UI-STORY-MGR`)

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
- [x] `UI/StorylineManager/TASKS.md` records the separate Blazor Storyline Manager authoring surface.
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

5. [x] `UI-STORY-MGR-001` Add Blazor Storyline Manager as a separate local authoring/admin UI.
- Child target: create `UI/StorylineManager` with typed REST client tabs for personas, narrative graphs, gameplay arcs, characters, memory review, ActivityCatalog, and settings.
- Validation command: `dotnet build UI\StorylineManager\StorylineManager.csproj --configuration Release --no-restore -v:minimal -m:1`.

## Simple Command Set
- `dotnet build UI/Systems/Systems.AppHost/Systems.AppHost.csproj --configuration Release`
- `dotnet build UI/Systems/Systems.ServiceDefaults/Systems.ServiceDefaults.csproj --configuration Release`
- `dotnet build UI/WoWStateManagerUI/WoWStateManagerUI.csproj --configuration Release`
- `dotnet build UI\StorylineManager\StorylineManager.csproj --configuration Release --no-restore -v:minimal -m:1`
- `rg -n "MASTER-SUB-03[6-8]|Current queue file|Next queue file" docs/TASKS.md`

## Session Handoff
- Last updated: 2026-05-30
- Active task: none. `UI-STORY-MGR-001` is complete.
- Last delta: added `UI/StorylineManager` as a local Blazor Server authoring app that uses a typed REST client only and leaves WPF as the default operator/test host.
- Pass result: `delta shipped`
- Validation/tests run:
  - `dotnet restore UI\StorylineManager\StorylineManager.csproj --verbosity minimal` -> passed
  - `dotnet build UI\StorylineManager\StorylineManager.csproj --configuration Debug --no-restore -v:minimal -m:1` -> passed with NETSDK1206 warning
  - `dotnet build UI\StorylineManager\StorylineManager.csproj --configuration Release --no-restore -v:minimal -m:1` -> passed with NETSDK1206 warning
- Files changed:
  - `UI/StorylineManager/`
  - `UI/TASKS.md`
  - `UI/TASKS_ARCHIVE.md`
  - `docs/TASKS.md`
- Blockers: None.
- Next task: none in this owner.
- Next command: `dotnet build UI\StorylineManager\StorylineManager.csproj --configuration Release --no-restore -v:minimal -m:1`.
