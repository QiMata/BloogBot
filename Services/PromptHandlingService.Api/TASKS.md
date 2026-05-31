# PromptHandlingService.Api Tasks

## Scope
- Directory: `Services/PromptHandlingService.Api`
- Project: `PromptHandlingService.Api.csproj`
- Focus: localhost-only REST JSON management API for PromptHandlingService storyline authoring.
- Public prefix: `/api/storylines/v1`.

## Execution Rules
1. Bind to loopback/local trusted access only.
2. Keep PromptHandlingService authoritative for SQLite writes and publish validation.
3. Reuse ActivityCatalog read-only for gameplay arc validation and lookup.
4. Do not replace WPF/StateManager protobuf TCP operator control flow.
5. Archive completed items to `Services/PromptHandlingService.Api/TASKS_ARCHIVE.md` in the same session.

## Completed Items
- [x] `PHS-API-001` Add localhost storyline management API host.

## Open Tasks
- None.

## Simple Command Set
- `dotnet restore Services\PromptHandlingService.Api\PromptHandlingService.Api.csproj --verbosity minimal`
- `dotnet build Services\PromptHandlingService.Api\PromptHandlingService.Api.csproj --configuration Release --no-restore -v:minimal -m:1`
- `dotnet run --project Services\PromptHandlingService.Api\PromptHandlingService.Api.csproj --configuration Debug`

## Session Handoff
- Last updated: 2026-05-30
- Active task: none. `PHS-API-001` is complete.
- Last delta: added localhost-only API host exposing `/api/storylines/v1` endpoints for health, drafts, publish, personas, graphs, gameplay arcs, character bindings, memory review, ActivityCatalog lookup, and graph layout.
- Pass result: `delta shipped`
- Validation/tests run:
  - `dotnet restore Services\PromptHandlingService.Api\PromptHandlingService.Api.csproj --verbosity minimal` -> passed
  - `dotnet build Services\PromptHandlingService.Api\PromptHandlingService.Api.csproj --configuration Debug --no-restore -v:minimal -m:1` -> passed with NETSDK1206 warning
  - `dotnet build Services\PromptHandlingService.Api\PromptHandlingService.Api.csproj --configuration Release --no-restore -v:minimal -m:1` -> passed with NETSDK1206 warning
- Files changed:
  - `Services/PromptHandlingService.Api/PromptHandlingService.Api.csproj`
  - `Services/PromptHandlingService.Api/Program.cs`
  - `Services/PromptHandlingService.Api/ActivityCatalogStorylineAdapter.cs`
  - `Services/PromptHandlingService.Api/appsettings.json`
  - `Services/PromptHandlingService.Api/Properties/launchSettings.json`
- Blockers: none.
- Next command: `dotnet build Services\PromptHandlingService.Api\PromptHandlingService.Api.csproj --configuration Release --no-restore -v:minimal -m:1`
