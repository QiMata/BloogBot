Continue work in: e:\repos\Westworld of Warcraft

Date context:
- Today is 2026-02-23.
- Continue from current workspace state; do not reset/revert unrelated dirty files.

Primary blocker to fix now:
- Live corpse run test still fails because BG runback stalls with stale forward movement flag and zero displacement.

Non-negotiable rules:
1. Use ActivitySnapshot/ObjectManager state as source of truth for alive/dead/ghost/corpse/reclaim timing.
2. SOAP is fallback only (recovery/research), not normal test behavior.
3. During corpse behavior phase (after death), do not use GM chat commands.
4. No teleport-to-corpse shortcuts; corpse retrieval must be pathfinding-driven.
5. Keep TASKS files continuously updated (commands, outcomes, evidence paths, files changed, next command).

Current verified evidence:
- tmp/deathcorpse_run_current.log:
  - Fail: BotRunner.Tests.LiveValidation.DeathCorpseRunTests.Death_ReleaseAndRetrieve_ResurrectsPlayer
  - Failure signature: "[BG] scenario failed: stale MOVEFLAG_FORWARD persisted while no displacement during runback"
  - Many run ticks like:
    - dist2D=90.2, step=0.0, reclaimDelay=0s, moveFlags=0x1
    - dist2D=334.7, step=0.0, reclaimDelay=0s, moveFlags=0x1
  - Coordinator suppression is active and visible:
    - "INJECTING PENDING ACTION ... (coordinator suppressed 300s)"
  - Release/Retrieve dispatches succeed, but movement doesn’t progress.
- tmp/combatloop_current.log:
  - Pass: Combat_TargetAndKillMob_MobDies

Already in code (do not regress):
- Live fixture sets:
  - WWOW_DISABLE_AUTORELEASE_CORPSE_TASK=1
  - WWOW_DISABLE_AUTORETRIEVE_CORPSE_TASK=1
  - WWOW_TEST_COORD_SUPPRESS_SECONDS=300
- BotRunnerService supports env toggles for auto release/retrieve behavior.
- CharacterStateSocketListener supports configurable coordinator suppression seconds.
- DeathCorpseRunTests already:
  - Enforces client ReleaseCorpse/RetrieveCorpse flow
  - Tracks no post-death GM command delta
  - Asserts movement progression / stale-forward failures
  - Uses setup variant retries and seeding attempts

Start by reading only these files first:
- docs/TASKS.md
- Tests/BotRunner.Tests/TASKS.md
- Exports/BotRunner/TASKS.md
- Tests/BotRunner.Tests/LiveValidation/DeathCorpseRunTests.cs
- Tests/BotRunner.Tests/LiveValidation/LiveBotFixture.cs
- Exports/BotRunner/Tasks/RetrieveCorpseTask.cs
- Exports/WoWSharpClient/Movement/MovementController.cs
- Services/WoWStateManager/TASKS.md

Immediate implementation goals:
1) Fix corpse runback displacement reliability:
- In RetrieveCorpseTask, add explicit stale-forward/no-displacement recovery:
  - detect repeated no movement while in ghost runback
  - force stop movement, clear/rebuild waypoint/probe path, then re-drive pathfinding
  - keep pathfinding-driven behavior only
- In MovementController, harden stale MOVEFLAG_FORWARD cleanup after state/teleport transitions:
  - if forward flag persists with repeated no actual displacement, issue stop/reset packet sequence and recover.
  - avoid false positives for one-time teleport/graveyard jumps only.

2) Keep test flow state-driven and strict:
- Preserve alive -> setup -> kill -> release action -> ghost -> runback -> reclaim-ready -> retrieve action -> alive.
- Keep no-GM-post-death assertions and movement-over-time assertions.

3) BG/FG parity:
- Preserve/add parity assertions for corpse phases:
  - dead corpse, ghost, moving to corpse, reclaim-ready, alive.
- Ensure BG mirrors FG decisions from snapshot state.

4) Reduce GM setup dependency across LiveValidation:
- Remove unnecessary GM setup spam where client actions + snapshot deltas can do it.

Required commands (log to tmp/ each iteration):
- dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --filter "FullyQualifiedName~DeathCorpseRunTests" --logger "console;verbosity=normal" 2>&1 | Tee-Object -FilePath tmp/deathcorpse_run_current.log
- dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --filter "FullyQualifiedName~CombatLoopTests" --logger "console;verbosity=normal" 2>&1 | Tee-Object -FilePath tmp/combatloop_current.log

At end of each iteration:
- Update:
  - docs/TASKS.md
  - Tests/BotRunner.Tests/TASKS.md
  - Exports/BotRunner/TASKS.md
  - (and Services/WoWStateManager/TASKS.md if forwarding/coordinator behavior changed)
- Include exact commands run, outcomes, evidence log paths, files changed, and next command.
- Continue immediately to next highest-priority blocker.
