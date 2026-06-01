# StorylineManager Tasks

## Scope
- Directory: `UI/StorylineManager`
- Project: `StorylineManager.csproj`
- Focus: local Blazor Server authoring/admin surface for persona storylines, narrative graphs, gameplay arcs, character bindings, memory review, ActivityCatalog lookup, and settings.
- Backend contract: typed REST JSON client only against `Services/PromptHandlingService.Api`; no direct SQLite access.

## Execution Rules
1. Keep WPF as the default operator/test UI; this app is separate authoring/admin tooling.
2. Keep API access loopback/local trusted only.
3. Do not assign live bot activities from this UI in v1.
4. Archive completed items to `UI/StorylineManager/TASKS_ARCHIVE.md` in the same session.
5. Record validation commands and a one-line pass result in Session Handoff.

## Completed Items
- [x] `UI-STORY-MGR-001` Create Blazor Storyline Manager with typed REST client tabs and graph layout editing.

## Open Tasks
- None.

## Simple Command Set
- `dotnet restore UI\StorylineManager\StorylineManager.csproj --verbosity minimal`
- `dotnet build UI\StorylineManager\StorylineManager.csproj --configuration Release --no-restore -v:minimal -m:1`
- `dotnet run --project UI\StorylineManager\StorylineManager.csproj --configuration Debug`

## Session Handoff
- Last updated: 2026-05-30
- Active task: none. `UI-STORY-MGR-001` is complete.
- Last delta: added the Blazor Server authoring app with typed REST client, authoring tabs, memory review actions, ActivityCatalog lookup, and SVG graph layout editing.
- Pass result: `delta shipped`
- Validation/tests run:
  - `dotnet restore UI\StorylineManager\StorylineManager.csproj --verbosity minimal` -> passed
  - `dotnet build UI\StorylineManager\StorylineManager.csproj --configuration Debug --no-restore -v:minimal -m:1` -> passed with NETSDK1206 warning
  - `dotnet build UI\StorylineManager\StorylineManager.csproj --configuration Release --no-restore -v:minimal -m:1` -> passed with NETSDK1206 warning
- Files changed:
  - `UI/StorylineManager/StorylineManager.csproj`
  - `UI/StorylineManager/Program.cs`
  - `UI/StorylineManager/StorylineManagerOptions.cs`
  - `UI/StorylineManager/Services/StorylineApiClient.cs`
  - `UI/StorylineManager/Pages/Index.razor`
  - `UI/StorylineManager/Shared/DraftEditor.razor`
  - `UI/StorylineManager/wwwroot/storylineGraph.js`
  - `UI/StorylineManager/wwwroot/site.css`
- Blockers: none.
- Next command: `dotnet build UI\StorylineManager\StorylineManager.csproj --configuration Release --no-restore -v:minimal -m:1`
