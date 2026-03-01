# WWoWBot.AI Tasks

## Scope
- Local ownership: `WWoWBot.AI/*`.
- Master reference: `docs/TASKS.md` (`MASTER-SUB-041`).
- Master tracker: `MASTER-SUB-041`.
- Goal: restore a compiling AI core, then harden deterministic state/plugin behavior so FG/BG parity work has a stable foundation.

## Execution Rules
1. Execute task IDs in order and keep scans limited to files listed in this document.
2. Keep commands one-line and prefer narrow build/test commands before broader runs.
3. Never blanket-kill `dotnet`; if cleanup is required, use repo-scoped process matching with PID evidence.
4. Do not switch to another local `TASKS.md` until this file has concrete IDs, acceptance criteria, and a complete handoff block.
5. If two consecutive passes produce no delta, log blocker + exact next command, then hand off.
6. Archive completed items to `WWoWBot.AI/TASKS_ARCHIVE.md` in the same session.
7. For AI behavior changes that affect gameplay logic, record FG/BG parity impact and physics-calibration follow-up tasks before marking complete.
8. `Session Handoff` must include `Pass result` (`delta shipped` or `blocked`) and exactly one executable `Next command`.
9. One-by-one loop guard: before any new scan, run the prior `Session Handoff -> Next command` verbatim and record a concrete delta (code, tests, or task docs) in the same pass.
10. After shipping one local delta, set `Session Handoff -> Next command` to the next queue-file read command (or the next task file read when this is queue-tail) and execute it in the same session.

## Environment Checklist
- [x] `dotnet build WWoWBot.AI/BloogBot.AI.csproj --configuration Release --no-restore` now succeeds (`0 warnings, 0 errors`).
- [x] Foundational type files now exist and compile: `WWoWBot.AI/States/BotActivity.cs`, `WWoWBot.AI/StateMachine/Trigger.cs`, `WWoWBot.AI/Annotations/ActivityPluginAttribute.cs`.
- [x] `WWoWBot.AI/Semantic/PluginCatalog.cs` now instantiates plugin objects and uses deterministic ordered discovery.
- [x] `WWoWBot.AI/Semantic/KernelCoordinator.cs` is now namespace-aligned (`namespace BloogBot.AI.Semantic;`).
- [x] Targeted test coverage search returned no matches for `BotActivityStateMachine`, `PluginCatalog`, or `KernelCoordinator` under `Tests/*.cs`.
- [x] `WWoWBot.AI/BloogBot.AI.csproj` now uses `Microsoft.SemanticKernel.Core` `1.72.0` and `Microsoft.Extensions.Logging.Abstractions` `10.0.3`; `NU1904` is cleared.

## Evidence Snapshot (2026-02-25)
- Restore status: `dotnet restore WWoWBot.AI/BloogBot.AI.csproj` succeeded and emitted `NU1904` for `Microsoft.SemanticKernel.Core` `1.54.0`.
- Build status: `dotnet build WWoWBot.AI/BloogBot.AI.csproj --configuration Release --no-restore` failed with `1 warning` and `69 errors`, dominated by missing `BotActivity` and missing `Trigger`.
- Type/file gap: `Test-Path` checks confirmed `BotActivity.cs`, `Trigger.cs`, and `ActivityPluginAttribute.cs` do not exist in expected folders.
- Plugin construction gap: `rg` confirms `KernelPluginFactory.CreateFromObject(t, t.Name)` in `PluginCatalog`, which passes `Type` directly.
- Namespace gap: `rg` over `KernelCoordinator.cs` confirms no namespace declaration.
- Test gap: targeted `rg` under `Tests` found no references to `BotActivityStateMachine`, `PluginCatalog`, or `KernelCoordinator`.

## P0 Active Tasks (Ordered)
1. [x] `AI-PARITY-001` Add explicit AI parity task hooks for FG/BG behavior mirroring.
- Evidence: parity requirement exists at master level but AI-local backlog currently lacks concrete linkage to BotRunner behavior scenarios.
- Files: `WWoWBot.AI/TASKS.md`, `docs/BEHAVIOR_MATRIX.md`, `Tests/BotRunner.Tests/*`.
- Implementation:
  - add three explicit parity cards in this file and `docs/BEHAVIOR_MATRIX.md`:
    - `AI-PARITY-CORPSE-001` corpse-run recovery parity (`.tele name {NAME} Orgrimmar` setup, release/runback/reclaim/rez sequencing),
    - `AI-PARITY-COMBAT-001` combat action selection parity,
    - `AI-PARITY-GATHER-001` gathering/mining behavior parity.
  - for each card, include one simple FG/BG validation command and one required `Navigation.Physics.Tests` follow-up command when movement differs.
  - keep commands one-line and reusable in handoff blocks.
- Acceptance: each AI behavior-impacting change maps to one executable FG/BG scenario check and one physics-calibration follow-up rule when movement differs.

2. [x] `AI-PARITY-CORPSE-001` Define AI-side corpse-run parity gate linked to BotRunner live tests. **PASSED (2026-02-28)** — DeathCorpseRunTests 1/1, 4m 56s.
- Evidence: corpse-run parity remains a P0 cross-project requirement and needs direct AI-side handoff linkage.
- Files: `WWoWBot.AI/TASKS.md`, `docs/BEHAVIOR_MATRIX.md`.
- Validation command: `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore --filter "FullyQualifiedName~DeathCorpseRunTests" --blame-hang --blame-hang-timeout 10m --logger "console;verbosity=minimal"`
- Physics follow-up command: `dotnet test Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --no-restore --settings Tests/Navigation.Physics.Tests/test.runsettings --filter "FullyQualifiedName~MovementControllerPhysicsTests" --logger "console;verbosity=minimal"`
- Acceptance: parity notes capture FG and BG stage ordering (`dead -> ghost -> runback -> reclaim-ready -> resurrected`) and route any movement drift to physics calibration IDs.

3. [x] `AI-PARITY-COMBAT-001` Define AI-side combat parity gate linked to BotRunner live tests. **PASSED (2026-02-28)** — CombatLoopTests 1/1, 6s.
- Evidence: combat behavior parity needs explicit AI task mapping rather than broad per-behavior text.
- Files: `WWoWBot.AI/TASKS.md`, `docs/BEHAVIOR_MATRIX.md`.
- Validation command: `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore --filter "FullyQualifiedName~CombatLoopTests" --blame-hang --blame-hang-timeout 10m --logger "console;verbosity=minimal"`
- Physics follow-up command: `dotnet test Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --no-restore --settings Tests/Navigation.Physics.Tests/test.runsettings --filter "FullyQualifiedName~PhysicsReplayTests|FullyQualifiedName~ErrorPatternDiagnosticTests" --logger "console;verbosity=minimal"`
- Acceptance: parity evidence includes action timing/order and movement positioning equivalence for FG vs BG.

4. [x] `AI-PARITY-GATHER-001` Define AI-side gathering/mining parity gate linked to BotRunner live tests. **PASSED (2026-02-28)** — GatheringProfessionTests 2/2, 4m 20s.
- Evidence: gathering pathing/action parity must be enforced in same cadence as corpse/combat scenarios.
- Files: `WWoWBot.AI/TASKS.md`, `docs/BEHAVIOR_MATRIX.md`.
- Validation command: `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore --filter "FullyQualifiedName~GatheringProfessionTests|FullyQualifiedName~Mining" --blame-hang --blame-hang-timeout 10m --logger "console;verbosity=minimal"`
- Physics follow-up command: `dotnet test Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --no-restore --settings Tests/Navigation.Physics.Tests/test.runsettings --filter "FullyQualifiedName~FrameByFramePhysicsTests" --logger "console;verbosity=minimal"`
- Acceptance: parity evidence includes node approach path, gather cast timing, and post-gather movement equivalence for FG vs BG.

## Simple Command Set
1. Build AI project:
- `dotnet build WWoWBot.AI/BloogBot.AI.csproj --configuration Release --no-restore`
2. Verify missing foundational definitions are resolved:
- `rg -n "BotActivity|Trigger|ActivityPluginAttribute" WWoWBot.AI/States WWoWBot.AI/StateMachine WWoWBot.AI/Annotations`
3. Run AI-focused tests:
- `dotnet test Tests/WWoWBot.AI.Tests/WWoWBot.AI.Tests.csproj --configuration Release --no-restore --logger "console;verbosity=minimal"`
4. Run corpse parity gate:
- `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore --filter "FullyQualifiedName~DeathCorpseRunTests" --blame-hang --blame-hang-timeout 10m --logger "console;verbosity=minimal"`
5. Run combat parity gate:
- `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore --filter "FullyQualifiedName~CombatLoopTests" --blame-hang --blame-hang-timeout 10m --logger "console;verbosity=minimal"`
6. Run gathering parity gate:
- `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore --filter "FullyQualifiedName~GatheringProfessionTests|FullyQualifiedName~Mining" --blame-hang --blame-hang-timeout 10m --logger "console;verbosity=minimal"`
7. Run movement calibration follow-up:
- `dotnet test Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --no-restore --settings Tests/Navigation.Physics.Tests/test.runsettings --logger "console;verbosity=minimal"`

## Session Handoff
- Last updated: 2026-02-28
- Active task: `MASTER-SUB-041` (`WWoWBot.AI/TASKS.md`)
- Last delta: Massive test coverage expansion — 9 new test files, 117 new tests (121 total, was 4).
- Pass result: `delta shipped`
- Validation/tests run:
  - `dotnet test Tests/WWoWBot.AI.Tests/WWoWBot.AI.Tests.csproj --configuration Release` — 121/121 pass (0 fail)
- New test files:
  - `Tests/WWoWBot.AI.Tests/Transitions/ForbiddenTransitionRuleTests.cs` — 20 tests (matching, wildcards, predicates, factory methods)
  - `Tests/WWoWBot.AI.Tests/Transitions/ForbiddenTransitionRegistryTests.cs` — 17 tests (CRUD, default rules, enable/disable, first-match)
  - `Tests/WWoWBot.AI.Tests/Advisory/AdvisoryValidatorTests.cs` — 13 tests (all 5 validation checks, override logging)
  - `Tests/WWoWBot.AI.Tests/Advisory/AdvisoryResolutionTests.cs` — 8 tests (Accepted/Overridden factory, properties)
  - `Tests/WWoWBot.AI.Tests/Advisory/InMemoryAdvisoryOverrideLogTests.cs` — 8 tests (logging, trimming, query by rule)
  - `Tests/WWoWBot.AI.Tests/Observable/StateChangeEventTests.cs` — 21 tests (construction, computed properties, factory methods)
  - `Tests/WWoWBot.AI.Tests/Observable/BotStateObservableTests.cs` — 11 tests (publish, subscribe, dispose, thread safety)
  - `Tests/WWoWBot.AI.Tests/States/MinorStateTests.cs` — 10 tests (construction, equality, validation)
  - `Tests/WWoWBot.AI.Tests/States/BotActivityTests.cs` — 2 tests (enum values, count)
  - `Tests/WWoWBot.AI.Tests/Configuration/DecisionInvocationSettingsTests.cs` — 11 tests (defaults, validation clamping)
- Files changed:
  - 9 new test files (listed above)
  - `WWoWBot.AI/TASKS.md`
- Blockers: AI-PARITY tasks need live server validation
- Next task: `AI-PARITY-001` (`AI-PARITY-CORPSE-001`, then `AI-PARITY-COMBAT-001`, then `AI-PARITY-GATHER-001`).
- Next command: `dotnet test Tests/WWoWBot.AI.Tests/WWoWBot.AI.Tests.csproj --configuration Release --no-restore --logger "console;verbosity=minimal"`
