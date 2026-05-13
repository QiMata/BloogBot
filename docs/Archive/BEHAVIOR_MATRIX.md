# Behavior Matrix
## Purpose
- Ordered execution queue for all local TASKS.md files listed in docs/TASKS.md.
- Tracks the first behavior card in each project so parity work can run continuously across agents.
- Status values: Not Started, Researching, Implementing, Verifying, Archived.
## Global Priority Tracks
1. CorpseRunOrgrimmarReleaseRecovery
- Owner set: Tests/BotRunner.Tests, Exports/BotRunner, Services/WoWStateManager, Services/ForegroundBotRunner, Services/BackgroundBotRunner.
- Current status: In Progress.
2. AirTeleportFallRecovery
- Owner set: Exports/Navigation, Exports/WoWSharpClient, Tests/Navigation.Physics.Tests, Services/BackgroundBotRunner.
- Current status: In Progress.
3. CombatAndGatheringParity
- Owner set: Tests/BotRunner.Tests, Exports/BotRunner, BotProfiles, Services/DecisionEngineService.
- Current status: In Progress.
## Ordered Queue
| Order | TASKS.md | Seed Behavior ID | Status | Next Action |
| --- | --- | --- | --- | --- |
| 1 | BotProfiles/TASKS.md | ProfilePullRotationRestParity | Verifying | CombatLoopTests passed with repo-scoped pre/post cleanup evidence; continue with combined corpse/combat/gather cycle and split profile-specific parity gaps. |
| 2 | Exports/TASKS.md | CrossExportCorpseCombatGatheringParity | Researching | Run combined live validation filter and split mismatches into owning export TASKS files. |
| 3 | Exports/BotCommLayer/TASKS.md | SnapshotCorpseMovementContractParity | Researching | Run WoWActivitySnapshotMovementTests; add proto/extension parity tasks for missing fields. |
| 4 | Exports/BotRunner/TASKS.md | CorpseRunOrgrimmarReleaseRecovery | Implementing | Re-run DeathCorpseRunTests with 10m timeout and repo-scoped teardown evidence. |
| 5 | Exports/GameData.Core/TASKS.md | DeathStateAndReclaimModelParity | Researching | Run ReleaseCorpseTaskTests|RetrieveCorpseTaskTests; add interface drift follow-up tasks. |
| 6 | Exports/Loader/TASKS.md | ForegroundAttachStartupDiagnosticsParity | Researching | Build Loader.vcxproj (Release/Win32) and capture startup diagnostics gaps. |
| 7 | Exports/Navigation/TASKS.md | AirTeleportFallRecovery | Implementing | Run physics calibration filters and patch interpolation/vertical-state drift. |
| 8 | Exports/WinImports/TASKS.md | RepoScopedLingeringProcessTeardown | Researching | Run repo-scoped cleanup command and verify PID-scoped termination only. |
| 9 | Exports/WoWSharpClient/TASKS.md | TeleportFlagResetAndFallStateParity | Implementing | Run DeathCorpseRunTests and triage teleport-flag/fall-state mismatches. |
| 10 | RecordedTests.PathingTests/TASKS.md | RecordedPathReplayParityForRunback | Researching | Run pathing test project and create replay parity follow-up tasks for drift. |
| 11 | RecordedTests.Shared/TASKS.md | RecordedSharedFixtureDeterminismParity | Researching | Run RecordedTests.Shared.Tests; confirm deterministic artifact ordering/hashes for FG/BG replay consumers. |
| 12 | Services/TASKS.md | ServiceOrchestrationParityLoop | Researching | Run combined corpse/combat/gather validation and split blockers into owning service TASKS files. |
| 13 | Services/BackgroundBotRunner/TASKS.md | BackgroundRunnerCommandExecutionParity | Implementing | Re-run DeathCorpseRunTests and triage stuck-forward (MOVEFLAG_FORWARD) plus zero-displacement loops. |
| 14 | Services/CppCodeIntelligenceMCP/TASKS.md | CppCodeIntelligenceSymbolParitySupport | Researching | Build CppCodeIntelligenceMCP and log unresolved symbol lookups for movement/physics/corpse tasks. |
| 15 | Services/DecisionEngineService/TASKS.md | DecisionEngineActionSelectionParity | Researching | Run CombatLoopTests|GatheringProfessionTests; compare FG/BG decision trace ordering and cooldown choices. |
| 16 | Services/ForegroundBotRunner/TASKS.md | ForegroundRunnerBaselineDeterminism | Implementing | Re-run DeathCorpseRunTests and capture fresh FG baseline timing/lifecycle evidence for parity diffs. |
| 17 | Services/LoggingMCPServer/TASKS.md | LoggingMcpScenarioCorrelationParity | Researching | Build LoggingMCPServer and define scenario/PID correlation fields for teardown evidence. |
| 18 | Services/PathfindingService/TASKS.md | PathfindingServiceGhostRunbackRouteParity | Implementing | Run PathfindingService tests and add route parity tasks for Orgrimmar ghost runback drift. |
| 19 | Services/PromptHandlingService/TASKS.md | PromptHandlingIntentContractParity | Researching | Run PromptHandlingService tests and create follow-up tasks for intent normalization mismatches. |
| 20 | Services/WoWStateManager/TASKS.md | WoWStateLifecycleSnapshotParity | Implementing | Re-run DeathCorpseRunTests; verify full lifecycle snapshots and reclaim-delay gating for FG/BG. |
| 21 | Tests/TASKS.md | CrossSuiteParityGateExecution | Implementing | Run combined BotRunner live filters with 10m hang timeout and archive repo-scoped teardown evidence. |
| 22 | Tests/BotRunner.Tests/TASKS.md | BotRunnerLiveScenarioParitySuite | Implementing | Run corpse/combat/gathering filters together and split any FG/BG divergence into owner tasks. |
| 23 | Tests/Navigation.Physics.Tests/TASKS.md | NavigationPhysicsCalibrationParitySuite | Implementing | Run Navigation.Physics tests and refine interpolation/fall-state regressions for air teleports. |
| 24 | Tests/PathfindingService.Tests/TASKS.md | PathfindingServiceRouteParitySuite | Researching | Run PathfindingService test suite and capture deterministic route gaps for corpse/combat/gather scenarios. |
| 25 | Tests/PromptHandlingService.Tests/TASKS.md | PromptHandlingServiceContractParitySuite | Researching | Run PromptHandlingService.Tests and collect failing prompt-contract fixtures into owner tasks. |
| 26 | Tests/RecordedTests.PathingTests.Tests/TASKS.md | RecordedPathingReplayParitySuite | Researching | Run Recorded pathing replay tests and file replay drift fixes with recording IDs. |
| 27 | Tests/RecordedTests.Shared.Tests/TASKS.md | RecordedSharedDeterminismSuite | Researching | Run Recorded shared determinism tests and track schema/hash drift findings. |
| 28 | Tests/Tests.Infrastructure/TASKS.md | TestsInfrastructureTeardownGuardParity | Implementing | Run infrastructure suite and enforce repo-scoped timeout/failure cleanup assertions. |
| 29 | Tests/WowSharpClient.NetworkTests/TASKS.md | WoWSharpClientNetworkPacketParitySuite | Researching | Run network tests and compare FG/BG movement/combat packet signatures for parity deltas. |
| 30 | Tests/WoWSharpClient.Tests/TASKS.md | WoWSharpClientMovementDeathStateParitySuite | Researching | Run WoWSharpClient unit tests and triage movement/death-state regressions. |
| 31 | Tests/WoWSimulation/TASKS.md | WoWSimulationScenarioParitySuite | Researching | Run WoWSimulation tests and capture simulated corpse/combat/gathering parity drifts. |
| 32 | Tests/WWoW.RecordedTests.PathingTests.Tests/TASKS.md | WWoWRecordedPathingReplayParitySuite | Researching | Run WWoW recorded pathing tests and add replay drift follow-up tasks by recording ID. |
| 33 | Tests/WWoW.RecordedTests.Shared.Tests/TASKS.md | WWoWRecordedSharedDeterminismSuite | Researching | Run WWoW recorded shared tests and log fixture determinism mismatches. |
| 34 | Tests/WWoW.Tests.Infrastructure/TASKS.md | WWoWTestsInfrastructureTeardownGuard | Researching | Run WWoW infrastructure tests and align cleanup guardrails with main suite rules. |
| 35 | UI/TASKS.md | UiOperationalParityDashboard | Researching | Build WoWStateManager UI and define parity dashboard views for corpse/combat/gathering state diffs. |
| 36 | UI/Systems/Systems.AppHost/TASKS.md | SystemsAppHostParityOrchestration | Researching | Build AppHost and document one-command startup path for parity sessions. |
| 37 | UI/Systems/Systems.ServiceDefaults/TASKS.md | SystemsServiceDefaultsTelemetryParity | Researching | Build ServiceDefaults and add telemetry/correlation tasks for scenario traceability. |
| 38 | UI/WoWStateManagerUI/TASKS.md | WoWStateManagerUiLifecycleParity | Researching | Build WoWStateManagerUI and define lifecycle timeline parity checks for FG/BG states. |
| 39 | WWoW.RecordedTests.PathingTests/TASKS.md | WWoWRecordedPathReplayParity | Researching | Run WWoW pathing replay suite and queue library-level replay parity fixes. |
| 40 | WWoW.RecordedTests.Shared/TASKS.md | WWoWRecordedSharedFixtureParity | Researching | Run WWoW shared determinism tests and file fixture/schema stabilization tasks. |
| 41 | WWoWBot.AI/TASKS.md | AiAbilityAndWorldInteractionParity | Implementing | Execute `AI-PARITY-*` cards in order: corpse-run, combat, then gathering; for any movement drift, immediately run physics calibration follow-up and route deltas to owning TASKS files. |
## AI Parity Cards (MASTER-SUB-041) — ALL PASSED (2026-02-28)
1. `AI-PARITY-CORPSE-001`: **PASSED** (1/1, 4m 56s)
- FG/BG validation: `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore --filter "FullyQualifiedName~DeathCorpseRunTests" --blame-hang --blame-hang-timeout 10m --logger "console;verbosity=minimal"`
- Physics follow-up on movement drift: `dotnet test Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --no-restore --settings Tests/Navigation.Physics.Tests/test.runsettings --filter "FullyQualifiedName~MovementControllerPhysicsTests" --logger "console;verbosity=minimal"`
2. `AI-PARITY-COMBAT-001`: **PASSED** (1/1, 6s)
- FG/BG validation: `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore --filter "FullyQualifiedName~CombatLoopTests" --blame-hang --blame-hang-timeout 10m --logger "console;verbosity=minimal"`
- Physics follow-up on movement drift: `dotnet test Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --no-restore --settings Tests/Navigation.Physics.Tests/test.runsettings --filter "FullyQualifiedName~PhysicsReplayTests|FullyQualifiedName~ErrorPatternDiagnosticTests" --logger "console;verbosity=minimal"`
3. `AI-PARITY-GATHER-001`: **PASSED** (2/2, 4m 20s)
- FG/BG validation: `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore --filter "FullyQualifiedName~GatheringProfessionTests|FullyQualifiedName~Mining" --blame-hang --blame-hang-timeout 10m --logger "console;verbosity=minimal"`
- Physics follow-up on movement drift: `dotnet test Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --no-restore --settings Tests/Navigation.Physics.Tests/test.runsettings --filter "FullyQualifiedName~FrameByFramePhysicsTests" --logger "console;verbosity=minimal"`
## Update Rule
1. When a behavior card is completed, move the related task to local TASKS_ARCHIVE.md and set status to Archived.
2. When verification starts, set status to Verifying and attach command + teardown evidence in that file's Session Handoff.
3. Add new behavior rows immediately when new functionality gaps are discovered.
4. Resume protocol: execute the prior `Session Handoff -> Next command` before any broader scan, then record one concrete delta in the active local `TASKS.md`.

## Feature Gap Summary (2026-03-27)

### Completed Systems
- Combat rotations (27 specs), movement/pathfinding, fishing, gathering, equipment, death/corpse-run
- BG/FG protocol parity (movement, combat, inventory, spells) — 115 CMSG, 141 SMSG
- Physics engine (WoW.exe exact binary parity), AABB collision, swimming

### Critical Gaps (P8)
- **PARITY-TRADE-001**: 6 trade sequences NullRef on BG — no null guards
- **PARITY-MERCHANT-001**: BG BuybackItem has no packet fallback
- 6 more FG-only sequences need BG packet paths (taxi, trainer, talent, gossip, craft)

### Character Progression Planner (P22 — 33 tasks)
- **Goal-driven behavior**: Each bot has explicit long-term objectives in JSON config — target spec, BiS gear set, reputation standings, rare items, mount, gold target, skill priorities, quest chains
- **ProgressionPlanner**: StateManager evaluates all goals, picks highest-priority activity: survival > training > gear > attunement > rep > mount > gold > profession > default grind
- **Configurable spec**: Spec per bot from config (replaces hardcoded class→spec mapping)
- **BiS gear sets**: Pre-built lists per spec (27 sets), gear gap evaluation drives dungeon farming
- **Pre-built templates**: ~15 archetypes (FuryWarriorPreRaid, HolyPriestMCReady, FrostMageAoEFarmer, etc.)
- **Rare item farming**: Baron Rivendare mount, Ironfoe, Eye of Sulfuras — repeated dungeon runs with instance reset

### Cross-World Travel Planner (P21 — 30 tasks)
- **TravelObjective system**: StateManager sets destination → BotRunner decomposes into multi-leg route → executes sequentially
- **Existing foundation**: CrossMapRouter (multi-leg planner), MapTransitionGraph (13 transitions), TransportData (11 transports), FlightPathData (48 nodes), TransportWaitingLogic (boarding state machine)
- **New tasks**: TravelTask, TakeFlightPathTask, UseHearthstoneTask, SetBindPointTask, MageTeleportTask, WarlockSummonTask
- **Missing data**: Deeprun Tram, ~25 dungeon/raid portals, mage spell IDs, innkeeper locations, graveyard positions
- **Route optimization**: Hearthstone strategy, class teleport integration, re-planning on failure

### Missing Gameplay Systems
- **P10 Battlegrounds**: 27 opcodes defined, ZERO handlers — no BG queue/join/objectives/honor
- **P11 40-Man Raids**: Basic party works, missing ready check, encounter mechanics, master loot
- **P12 World PvP**: No hostile player detection or PvP engagement
- **P13 Questing**: Accept/complete works, kill/item objective tracking not parsed
- **P14 Pets**: Call/dismiss works, no feeding/training/stance/ability management
- **P15 Channels**: No ChannelNetworkClientComponent — can't join/leave channels
- **P16 Crafting**: Framework exists, no batch crafting or profession automation
- **P17 Progression**: No talent auto-allocation, trainer scheduling, zone routing
- **P18 Economy**: AH/bank/mail infrastructure exists, no posting strategy or automation
- **P19 Travel**: No hearthstone auto-use, spirit healer navigation, mount usage

### Scalability (P9) — Architecture Redesign Required
- Current: 1 process per bot, max ~50 bots
- Target: 3000 concurrent bots
- Blockers: 3 static singletons, thread-per-connection sockets, blocking IPC, single PathfindingService
- Plan: 28 tasks across singleton removal, async I/O, sharding, load testing

## Session Handoff
- Last updated: 2026-03-27
- Active queue items: P8 (FG/BG parity), P9 (scalability), P10-P20 (gameplay systems)
- Last delta: Full codebase analysis → 130+ new tasks written with detailed specs for agent parallelization
- Next command: Start with P8.1 (trade sequence null guards) — smallest blast radius, immediate BG stability fix
