# BloogBot.AI Tasks

## Scope
- Local ownership: `BloogBot.AI/*`.
- Test project: `Tests/BloogBot.AI.Tests/BloogBot.AI.Tests.csproj`.
- Master tracker: `MASTER-SUB-041`.
- Goal: keep the AI core compiling and deterministic enough to support FG/BG behavior parity work.

## Execution Rules
1. Keep scans limited to `BloogBot.AI` and `Tests/BloogBot.AI.Tests` while this file is active.
2. Keep commands one-line and prefer narrow build/test commands before broader runs.
3. Never blanket-kill `dotnet`; if cleanup is required, use repo-scoped process matching with PID evidence.
4. Archive completed items to `BloogBot.AI/TASKS_ARCHIVE.md` in the same session.
5. `Session Handoff` must include `Pass result` (`delta shipped` or `blocked`) and exactly one executable `Next command`.

## Environment Checklist
- [x] `dotnet build BloogBot.AI/BloogBot.AI.csproj --configuration Release --no-restore` succeeds.
- [x] Foundational type files compile: `BloogBot.AI/States/BotActivity.cs`, `BloogBot.AI/StateMachine/Trigger.cs`, `BloogBot.AI/Annotations/ActivityPluginAttribute.cs`.
- [x] `BloogBot.AI/Semantic/PluginCatalog.cs` instantiates plugin objects and uses deterministic ordered discovery.
- [x] `BloogBot.AI/Semantic/KernelCoordinator.cs` is namespace-aligned (`namespace BloogBot.AI.Semantic;`).
- [x] `Tests/BloogBot.AI.Tests/BloogBot.AI.Tests.csproj` references the existing `BloogBot.AI/BloogBot.AI.csproj` path.
- [x] `dotnet test Tests/BloogBot.AI.Tests/BloogBot.AI.Tests.csproj --configuration Release --no-restore --logger "console;verbosity=minimal"` passes.

## Current Status (2026-04-15)
- Known remaining owner-local items: `0`.
- The legacy `WWoWBot.AI` path references in this tracker were stale; the actual repo path is `BloogBot.AI`.
- Completed item details are archived in `BloogBot.AI/TASKS_ARCHIVE.md`.

## Completed P0 Items
- [x] `AI-PARITY-001` Add explicit AI parity task hooks for FG/BG behavior mirroring.
- [x] `AI-PARITY-CORPSE-001` Define AI-side corpse-run parity gate linked to BotRunner live tests.
- [x] `AI-PARITY-COMBAT-001` Define AI-side combat parity gate linked to BotRunner live tests.
- [x] `AI-PARITY-GATHER-001` Define AI-side gathering/mining parity gate linked to BotRunner live tests.
- [x] `AI-TST-PATH-001` Fix the AI test project reference from the removed `WWoWBot.AI` path to `BloogBot.AI`.

## Open Tasks
- None.

## Simple Command Set
1. Build AI project: `dotnet build BloogBot.AI/BloogBot.AI.csproj --configuration Release --no-restore`.
2. Run AI tests: `dotnet test Tests/BloogBot.AI.Tests/BloogBot.AI.Tests.csproj --configuration Release --no-restore --logger "console;verbosity=minimal"`.
3. Verify legacy path drift stays gone: `rg -n "WWoWBot.AI" BloogBot.AI Tests/BloogBot.AI.Tests -g "*.csproj" -g "*.md"`.

## Session Handoff
- Last updated: 2026-04-15
- Active task: none. All tracked owner-local items are complete.
- Last delta: fixed `Tests/BloogBot.AI.Tests/BloogBot.AI.Tests.csproj` to reference `..\..\BloogBot.AI\BloogBot.AI.csproj` instead of the removed `..\..\WWoWBot.AI\BloogBot.AI.csproj`.
- Pass result: `delta shipped`
- Validation/tests run:
  - `dotnet restore Tests/BloogBot.AI.Tests/BloogBot.AI.Tests.csproj` -> `restored`
  - `dotnet test Tests/BloogBot.AI.Tests/BloogBot.AI.Tests.csproj --configuration Release --no-restore --logger "console;verbosity=minimal"` -> `passed (121/121)`
  - `powershell -ExecutionPolicy Bypass -File .\run-tests.ps1 -CleanupRepoScopedOnly` -> `No repo-scoped processes to stop.`
- Files changed:
  - `Tests/BloogBot.AI.Tests/BloogBot.AI.Tests.csproj`
  - `BloogBot.AI/TASKS.md`
  - `BloogBot.AI/TASKS_ARCHIVE.md`
  - `docs/TASKS.md`
  - `docs/TASKS_ARCHIVE.md`
- Blockers: none.
- Next command: `rg --files -g TASKS.md | ForEach-Object { rg --with-filename -n "^- \[ \]|\[ \] Problem|Active task:" $_ }`
