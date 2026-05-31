# Task Archive

Completed items moved from TASKS.md.

## Archived Snapshot (2026-05-30) - PHS-STORY-MGR-001 Storyline management API and authoring backend

- [x] `PHS-STORY-MGR-001` Added the PromptHandlingService-owned storyline management backend and local REST API host.
- Completed:
  - Added draft/publish domain models, DTOs, validation errors, and management service contracts.
  - Extended SQLite persistence with `StorylineDraft`, `GameplayStoryArc`, `GameplayArcStep`, `CharacterStoryBinding`, and `GraphLayout` tables.
  - Added list/query methods for personas, versions, graphs, nodes, transitions, memory candidates, layouts, gameplay arcs, and character bindings.
  - Added publish validation for required IDs, graph transition endpoints, duplicate gameplay step order, ActivityCatalog ids, and binding runtime updates.
  - Added graph snapshot publishing that upserts current graph/nodes/transitions and deletes removed nodes/transitions for that graph.
  - Added `Services/PromptHandlingService.Api` with localhost-only `/api/storylines/v1` endpoints and read-only ActivityCatalog lookup via the WoWStateManager catalog source.
- Validation:
  - `dotnet restore Services\PromptHandlingService.Api\PromptHandlingService.Api.csproj --verbosity minimal` -> passed.
  - `dotnet build Services\PromptHandlingService.Api\PromptHandlingService.Api.csproj --configuration Debug --no-restore -v:minimal -m:1` -> passed with NETSDK1206 warning.
  - `dotnet build Services\PromptHandlingService.Api\PromptHandlingService.Api.csproj --configuration Release --no-restore -v:minimal -m:1` -> passed with NETSDK1206 warning.
  - `$env:DOTNET_ROLL_FORWARD='Major'; dotnet test Tests\PromptHandlingService.Tests\PromptHandlingService.Tests.csproj --configuration Release --no-restore --filter "FullyQualifiedName~Storyline|FullyQualifiedName~Foundry" --logger "console;verbosity=minimal"` -> passed (36 passed, 3 skipped).
- Next command: `$env:DOTNET_ROLL_FORWARD='Major'; dotnet test Tests\PromptHandlingService.Tests\PromptHandlingService.Tests.csproj --configuration Release --no-restore --filter "FullyQualifiedName~Storyline|FullyQualifiedName~Foundry" --logger "console;verbosity=minimal"`.

## Archived Snapshot (2026-05-24) - PHS-FDRY-DEPLOY-001 Foundry prompt agent and Agent Application

- [x] `PHS-FDRY-DEPLOY-001` Deployed the PromptHandlingService Foundry persona runtime slice to Azure AI Foundry dev.
- Completed:
  - Created model deployment `gpt-5-mini` in `rg-jrhodes-0775/atlsqlsattest-resource` with model version `2025-08-07`, SKU `GlobalStandard`, capacity 50.
  - Created/updated prompt agent `wwow-persona-runtime-dev` version `1`.
  - Created/updated Agent Application `wwow-persona-runtime-dev-app`.
  - Created/updated managed deployment `wwow-persona-runtime-dev-deployment`; state `Running`, provisioning `Succeeded`.
  - Updated service-local Foundry metadata with the active agent version and published application endpoint.
  - Set Foundry Responses calls to minimal reasoning effort so `gpt-5-mini` completes within the 512-token output budget.
- Note: ARM API `2026-01-15-preview` rejected deployment protocol `Agent`; the running app deployment uses protocol `Responses` version `1.0`.
- Validation:
  - Final Azure readbacks -> passed; model `Running`/`Succeeded`, prompt-agent version `active`, Agent Application deployment `Running`/`Succeeded`.
  - Project-scoped `/openai/v1/responses` smoke -> passed; status `completed`.
  - Published application `/applications/wwow-persona-runtime-dev-app/protocols/openai/responses` smoke -> passed; status `completed`.
  - `$env:DOTNET_ROLL_FORWARD='Major'; $env:WWOW_FOUNDRY_LIVE='1'; dotnet test Tests/PromptHandlingService.Tests/PromptHandlingService.Tests.csproj --configuration Release --no-restore --filter "FullyQualifiedName~StorylinePersonaLiveSmokeTests.SeededStorylineRuntimeSmoke_UsesConfiguredFoundryProject|FullyQualifiedName~FoundryPersonaLiveSmokeTests.DirectModelResponseSmoke_UsesConfiguredFoundryProject" --logger "console;verbosity=minimal"` -> passed after temporarily enabling the two statically skipped live tests (2 passed, 0 failed).
  - `$env:DOTNET_ROLL_FORWARD='Major'; dotnet test Tests/PromptHandlingService.Tests/PromptHandlingService.Tests.csproj --configuration Release --no-restore --filter "FullyQualifiedName~Storyline|FullyQualifiedName~Foundry" --logger "console;verbosity=minimal"` -> passed (29 passed, 3 skipped).
- Next command: `$env:DOTNET_ROLL_FORWARD='Major'; dotnet test Tests/PromptHandlingService.Tests/PromptHandlingService.Tests.csproj --configuration Release --no-restore --filter "FullyQualifiedName~Storyline|FullyQualifiedName~Foundry" --logger "console;verbosity=minimal"`.

## Archived Snapshot (2026-05-24) - PHS-STORY-001 Foundry storyline graph runtime

- [x] `PHS-STORY-001` Added the persistent storyline graph runtime under `Services/PromptHandlingService`.
- Completed:
  - Added SQLite-backed persona, character state, approved memory, pending memory candidate, narrative graph/node/transition, agent binding, and conversation binding storage under `Storylines/`.
  - Added `IStorylineRepository`, `IStorylineContextResolver`, `IStorylinePersonaRuntime`, and `StorylinePromptInput`.
  - Added deterministic context assembly into `PersonaPromptRequest` and a dialogue-only runtime guard that rejects disallowed Foundry intents with a deterministic fallback.
  - Extended Foundry persona runtime calls with `PersonaPromptRuntimeBinding` for per-persona model/agent/max-token bindings.
  - Added non-secret runtime config and Durotar/Razor Hill seed content.
- Validation:
  - `dotnet build Services/PromptHandlingService/PromptHandlingService.csproj --configuration Release --no-restore` -> passed (0 warnings, 0 errors).
  - `dotnet build Tests/PromptHandlingService.Tests/PromptHandlingService.Tests.csproj --configuration Release --no-restore` -> passed (0 warnings, 0 errors).
  - `$env:DOTNET_ROLL_FORWARD='Major'; dotnet test Tests/PromptHandlingService.Tests/PromptHandlingService.Tests.csproj --configuration Release --no-restore --filter "FullyQualifiedName~Storyline|FullyQualifiedName~Foundry" --logger "console;verbosity=minimal"` -> passed (29 passed, 3 skipped on final run).
- DB/config used:
  - Default DB path: `storyline_runtime.sqlite` under content root.
  - Runtime config: `Config/foundry/storyline-runtime.json`.
  - Seed file: `Config/foundry/storyline-seed.json`.
- Next command: remove the skip on `StorylinePersonaLiveSmokeTests.SeededStorylineRuntimeSmoke_UsesConfiguredFoundryProject`, then run `$env:DOTNET_ROLL_FORWARD='Major'; $env:WWOW_FOUNDRY_LIVE='1'; dotnet test Tests/PromptHandlingService.Tests/PromptHandlingService.Tests.csproj --configuration Release --no-restore --filter "FullyQualifiedName~StorylinePersonaLiveSmokeTests.SeededStorylineRuntimeSmoke_UsesConfiguredFoundryProject" --logger "console;verbosity=minimal"`.

## Archived Snapshot (2026-05-24) - PHS-FDRY-001 Foundry persona runtime adapter

- [x] `PHS-FDRY-001` Added the Foundry persona runtime adapter under `Services/PromptHandlingService`.
- Completed:
  - Added `IFoundryPersonaRuntime`, persona prompt DTOs, deterministic prompt assembly, Foundry Responses SDK wrapper, and an optional prompt-agent version provisioner.
  - Added non-secret Foundry config at `Config/foundry/persona-runtime.json` and service-local `.foundry/agent-metadata.yaml`.
  - Registered the runtime in DI without calling prompt-agent provisioning during startup.
  - Updated specs 20, 21, and 24 to state Foundry is persona/dialogue advisory only.
- Validation:
  - `dotnet restore Services/PromptHandlingService/PromptHandlingService.csproj` -> passed.
  - `dotnet build Services/PromptHandlingService/PromptHandlingService.csproj --configuration Release --no-restore` -> passed (0 warnings, 0 errors on final run).
  - `$env:DOTNET_ROLL_FORWARD='Major'; dotnet test Tests/PromptHandlingService.Tests/PromptHandlingService.Tests.csproj --configuration Release --no-restore --filter "FullyQualifiedName~Foundry" --logger "console;verbosity=minimal"` -> passed (14 passed, 2 skipped).
- Next command: `$env:DOTNET_ROLL_FORWARD='Major'; $env:WWOW_FOUNDRY_LIVE='1'; dotnet test Tests/PromptHandlingService.Tests/PromptHandlingService.Tests.csproj --configuration Release --no-restore --filter "FullyQualifiedName~FoundryPersonaLiveSmokeTests.DirectModelResponseSmoke_UsesConfiguredFoundryProject" --logger "console;verbosity=minimal"`.
