# Task Archive

Completed items moved from TASKS.md.

## Archived Snapshot (2026-04-03) - `WSM-HOST-001` complete

- [x] Validate `WoWStateManager` as a host-side process against containerized `vmangos-server` dependencies.
- [x] Verify spawned BG workers inherit docker-safe endpoint overrides while `WoWStateManager` remains outside Docker.
- Completion notes:
  - `WoWStateManager` no longer launches or stops `PathfindingService`/`SceneDataService`; it only performs external readiness checks and continues with WoW-client orchestration.
  - Windows compose now runs `pathfinding-service` and `scene-data-service` as separate containers, and `background-bot-runner` is wired to both endpoints.
  - Validation:
    - `dotnet build Services/WoWStateManager/WoWStateManager.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false` -> `succeeded`
    - `docker compose -f .\docker-compose.windows.yml config` -> `succeeded`

## Archived Snapshot (2026-04-03) - RFC coordinator-only lifecycle

- [x] Keep RFC fixture prep outside `DungeoneeringCoordinator`.
- [x] Ensure StateManager only uses the RFC coordinator for coordination after prep is complete.
- Completion notes:
  - `CharacterStateSocketListener` now constructs `DungeoneeringCoordinator` without the old prep-skip toggle.
  - `RfcBotFixture` disables the coordinator during prep and re-enables it only after revive/level/spell/gear/Orgrimmar staging has completed.
  - `DungeoneeringCoordinator` now leaves `WaitingForBots` directly for `FormGroup_Inviting`, which keeps the RFC coordinator path out of the old prep-action flow.

## Archived Snapshot (2026-02-24 19:43:32) - Services/WoWStateManager/TASKS.md

- [x] Ensure coordinator suppression windows never hide test-forwarded actions. Evidence: `INJECTING PENDING ACTION ... (coordinator suppressed 300s)` present for ReleaseCorpse/Goto/RetrieveCorpse in `tmp/deathcorpse_run_current.log`.
