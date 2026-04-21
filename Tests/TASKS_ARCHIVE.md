# Task Archive

Completed items moved from TASKS.md.

## Archived Snapshot (2026-04-15) - Tests umbrella active-note refresh

- [x] Refreshed `Tests/TASKS.md` so the latest handoff no longer points at stale `MASTER-SUB-022` fishing/pathfinding work.
- Completed:
  - Umbrella now reports no active owner-local test routing task.
  - PromptHandlingService deterministic suite rerun after DecisionEngine runtime tests were added.
- Validation:
  - `dotnet test Tests/PromptHandlingService.Tests/PromptHandlingService.Tests.csproj --configuration Release --no-restore --settings Tests/test.runsettings --filter "Category!=Integration" --logger "console;verbosity=minimal"` -> `passed (31 passed, 161 skipped, 0 failed, 192 total)`

## Archived Snapshot (2026-02-24 19:43:32) - Tests/TASKS.md

- [x] `DeathCorpseRunTests` setup path switched to `.tele name {NAME} Orgrimmar`.
- [x] `ValleyOfTrials` removed from corpse-run setup flow.

