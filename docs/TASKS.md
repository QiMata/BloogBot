# Master Tasks

## Role
- `docs/TASKS.md` is the master coordination list for every local `TASKS.md`.
- Local files hold project implementation details; this file sets priority, ownership, and execution rules.
- When priorities conflict, this file wins until explicitly updated.

## Master Rules
1. Execute one local `TASKS.md` at a time in queue order; do not jump files mid-task.
2. Keep a handoff pointer (`current file`, `next file`) in this document before switching files.
3. Prefer direct implementation tasks tied to concrete files/symbols, not broad behavior buckets.
4. Keep commands simple and one-line where possible.
5. Never blanket-kill `dotnet`; cleanup must be repo-scoped and evidenced.
6. Every timeout/failure/cancel path must record deterministic teardown evidence.
7. Move completed items to local `TASKS_ARCHIVE.md` in the same session.
8. No blanket repository scans during execution; scan only the active project path and summarize findings in its local `TASKS.md`.
9. Every touched local `TASKS.md` must include `Last delta` and `Next command` in its `Session Handoff` section.
10. Loop-break guard: if two consecutive passes produce no file delta, record blocker + exact next command, then move to the next queued file.
11. Compaction continuity: before session handoff/compaction, update this file's `current file`/`next file` pointers and the active local file's `Session Handoff` so the next agent resumes without rediscovery.
12. Scan-budget guard: for the active queue file, read `TASKS.md` plus only directly referenced implementation files needed to define concrete task IDs; do not pre-load sibling projects.
13. One-file completion gate: do not switch queue files until the active local `TASKS.md` includes concrete IDs, acceptance criteria, and an updated `Session Handoff` (`Last delta`, `Next command`).
14. One-by-one session protocol: each pass must start by reading only `docs/TASKS.md` and the active local `TASKS.md`, produce a concrete file delta for that active file, then advance `current file`/`next file` pointers before any broader scan.
15. Loop-proof handoff rule: each active local `TASKS.md` update must include a one-line pass result in `Session Handoff` (`delta shipped` or `blocked`) so the next session can resume directly from `Next command` without rediscovery.
16. Queue-resume rule: on session restart/compaction, resume from `Current queue file` exactly; do not restart from queue head unless this file explicitly resets the pointer.
17. Autonomous continuation rule: after shipping a delta in the active local file, immediately advance to the next queued file in the same session; do not wait for extra user prompting.
18. Compaction packet rule: before handoff/compaction, write `Pass result`, `Last delta`, and one executable `Next command` in this file and in the active local file.
19. One-step queue discipline: in each pass, expand exactly one queued local `TASKS.md`, then advance `Current queue file`/`Next queue file` before reading any additional queue files.
20. No-rediscovery rule: resume from the previous pass `Next command` first; do not re-scan prior queue files unless the active file reports `blocked`.
21. Exact-resume rule: each new pass must run the prior handoff `Next command` verbatim before issuing any new scan/search command; if it fails, record the failure and replacement command in `Session Handoff`.
22. Documentation-first continuity rule: each pass must leave a breadcrumb pair (`Master tracker` + one executable `Next command`) in both this file and the active local `TASKS.md` so the next session can resume without broad scans.
23. Zero-match test rule: when a filtered `dotnet test` command reports no matching tests, capture `--list-tests` output in the active local `TASKS.md` evidence and keep/add a concrete task ID to close that discovery gap.
24. Anti-loop resume rule: each queue advance must include a concrete `Validation/tests run` result in the active local file and update this file to the next queued `Get-Content` command before any additional scanning.
25. No-rediscovery handoff rule: when a queue file reports `blocked`, advance to the next queue file in the same pass and leave exact `Current queue file`/`Next command` breadcrumbs so the next session never restarts prior files.
26. Queue-tail rule: when `Current queue file` is the final queued item, keep execution on that file until a concrete task ID ships a code/test delta; do not restart from queue head.
27. Compaction-resume guard: after compaction/new session, execute only the active file handoff `Next command` first, then update both handoff blocks before touching any other local `TASKS.md`.
28. Tail-rebase guard: if the queue-tail file records `blocked` once or cannot ship the next required delta, rebase `Current queue file` to the highest-priority unresolved local file and record the reason in `Last delta`.
29. Mandatory next-file step: after shipping a local `TASKS.md` delta, execute that file's `Next command`; if it targets the next queue file, update that next file in the same session before compaction.
30. Queue-rotation guard: when queue-tail work is documentation-complete for the pass, reset `Current queue file` to the earliest unresolved queue item and continue one-by-one from there.

## P0 Priority Queue (2026-02-25)
1. [ ] Complete direct missing-implementation backlog from code scan (section below).
2. [ ] Keep corpse-run test flow as `.tele name {NAME} Orgrimmar` -> kill -> release -> runback -> reclaim-ready -> resurrect.
3. [ ] Enforce 10-minute max runtime for corpse-run scenarios with repo-scoped process teardown evidence.
4. [ ] Replace broad "per-behavior" backlog items with file-level tasks in local `TASKS.md` files.
5. [ ] Make docs/markdown navigation agent-friendly so Codex and Claude Code can scan structure fast with low context usage.
6. [ ] Track every sub-`TASKS.md` as an explicit master task and execute one file at a time.

## Direct Missing-Implementation Inventory (Code Scan: 2026-02-25)

### Services/ForegroundBotRunner
- [ ] `FG-MISS-001` Implement remaining `NotImplementedException` members in [WoWObject.cs](/E:/repos/Westworld of Warcraft/Services/ForegroundBotRunner/Objects/WoWObject.cs).
- [ ] `FG-MISS-002` Implement remaining `NotImplementedException` members in [WoWUnit.cs](/E:/repos/Westworld of Warcraft/Services/ForegroundBotRunner/Objects/WoWUnit.cs).
- [ ] `FG-MISS-003` Implement remaining `NotImplementedException` members in [WoWPlayer.cs](/E:/repos/Westworld of Warcraft/Services/ForegroundBotRunner/Objects/WoWPlayer.cs).
- [ ] `FG-MISS-004` Add regression checks so FG snapshot/materialization paths never throw on corpse/combat/gathering flows.

### Exports/BotRunner
- [ ] `BR-MISS-001` Implement `ScanForQuestUnitsTask` TODO in [QuestingTask.cs](/E:/repos/Westworld of Warcraft/Exports/BotRunner/Tasks/Questing/QuestingTask.cs:52).

### Exports/WoWSharpClient
- [ ] `WSC-MISS-001` Implement missing `WoWPlayer` fields referenced as "not implemented yet" in [WoWSharpObjectManager.cs](/E:/repos/Westworld of Warcraft/Exports/WoWSharpClient/WoWSharpObjectManager.cs:2000).
- [ ] `WSC-MISS-002` Implement `CMSG_CANCEL_AURA` send path in [WoWUnit.cs](/E:/repos/Westworld of Warcraft/Exports/WoWSharpClient/Models/WoWUnit.cs:270).
- [ ] `WSC-MISS-003` Resolve "Custom navigation strategy not implemented" path in [GossipNetworkClientComponent.cs](/E:/repos/Westworld of Warcraft/Exports/WoWSharpClient/Networking/ClientComponents/GossipNetworkClientComponent.cs:249).

### Exports/Navigation
- [ ] `NAV-MISS-001` Implement TODO stub in [PhysicsTestExports.cpp](/E:/repos/Westworld of Warcraft/Exports/Navigation/PhysicsTestExports.cpp:231).
- [ ] `NAV-MISS-002` Decide and implement `returnPhysMat`/scene-query support in [SceneQuery.h](/E:/repos/Westworld of Warcraft/Exports/Navigation/SceneQuery.h:26).

### Tests/Navigation.Physics.Tests
- [ ] `NPT-MISS-001` Replace placeholder TODO with real physics stepping in [FrameByFramePhysicsTests.cs](/E:/repos/Westworld of Warcraft/Tests/Navigation.Physics.Tests/FrameByFramePhysicsTests.cs:380).
  - Evidence: `SimulatePhysics` currently never calls native physics (`StepPhysicsV2`) and produces synthetic frames only.
  - Files/symbols: [FrameByFramePhysicsTests.cs](/E:/repos/Westworld of Warcraft/Tests/Navigation.Physics.Tests/FrameByFramePhysicsTests.cs:373), [NavigationInterop.cs](/E:/repos/Westworld of Warcraft/Tests/Navigation.Physics.Tests/NavigationInterop.cs:340).
  - Required breakdown: call `StepPhysicsV2` every frame, persist output with each frame snapshot, and feed output position/velocity into next-frame input so tests reflect real engine behavior.
  - Validation: `dotnet test Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --no-restore --settings Tests/Navigation.Physics.Tests/test.runsettings --filter "FullyQualifiedName~FrameByFramePhysicsTests" --logger "console;verbosity=minimal"`.
  - Acceptance: frame simulation loop has no placeholder branch; regressions fail on native physics output deltas.
- [ ] `NPT-MISS-002` Add explicit failing assertions for airborne teleport hover regression in [MovementControllerPhysicsTests.cs](/E:/repos/Westworld of Warcraft/Tests/Navigation.Physics.Tests/MovementControllerPhysicsTests.cs:134) and free-fall progression checks in the same suite.
  - Evidence: teleport recovery test currently verifies "not falling through world" but does not enforce monotonic descent after airborne teleport.
  - Files/symbols: [MovementControllerPhysicsTests.cs](/E:/repos/Westworld of Warcraft/Tests/Navigation.Physics.Tests/MovementControllerPhysicsTests.cs:121), [MovementControllerPhysicsTests.cs](/E:/repos/Westworld of Warcraft/Tests/Navigation.Physics.Tests/MovementControllerPhysicsTests.cs:167).
  - Required breakdown: add assertions for per-frame Z descent/velocity sign across first post-teleport frames, then assert landing window to match FG fall behavior.
  - Validation: `dotnet test Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --no-restore --settings Tests/Navigation.Physics.Tests/test.runsettings --filter "FullyQualifiedName~MovementControllerPhysicsTests" --logger "console;verbosity=minimal"`.
  - Acceptance: BG hover regression fails deterministically; corrected controller path passes with bounded landing frame window.
- [ ] `NPT-MISS-003` Add a frame-by-frame drift regression gate between replay/native outputs and controller outputs in [PhysicsReplayTests.cs](/E:/repos/Westworld of Warcraft/Tests/Navigation.Physics.Tests/PhysicsReplayTests.cs) and [ErrorPatternDiagnosticTests.cs](/E:/repos/Westworld of Warcraft/Tests/Navigation.Physics.Tests/Diagnostics/ErrorPatternDiagnosticTests.cs).
  - Evidence: replay diagnostics are rich but missing a hard gate that blocks controller drift regressions from merging.
  - Files/symbols: [PhysicsReplayTests.cs](/E:/repos/Westworld of Warcraft/Tests/Navigation.Physics.Tests/PhysicsReplayTests.cs:672), [ErrorPatternDiagnosticTests.cs](/E:/repos/Westworld of Warcraft/Tests/Navigation.Physics.Tests/Diagnostics/ErrorPatternDiagnosticTests.cs:94).
  - Required breakdown: define explicit drift thresholds (overall average, steady-state p99, worst clean-frame), assert against them, and emit worst-frame diagnostics on failure.
  - Validation: `dotnet test Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --no-restore --settings Tests/Navigation.Physics.Tests/test.runsettings --filter "FullyQualifiedName~PhysicsReplayTests|FullyQualifiedName~ErrorPatternDiagnosticTests" --logger "console;verbosity=minimal"`.
  - Acceptance: failing threshold immediately blocks regressions with actionable frame IDs and error vectors.

### UI/WoWStateManagerUI
- [ ] `UI-MISS-001` Implement or remove `ConvertBack` throw path in [GreaterThanZeroToBooleanConverter.cs](/E:/repos/Westworld of Warcraft/UI/WoWStateManagerUI/Converters/GreaterThanZeroToBooleanConverter.cs:18).

### RecordedTests.Shared
- [ ] `RTS-MISS-001` Implement S3 storage operations in [S3RecordedTestStorage.cs](/E:/repos/Westworld of Warcraft/RecordedTests.Shared/Storage/S3RecordedTestStorage.cs:49).
- [ ] `RTS-MISS-002` Implement Azure Blob storage operations in [AzureBlobRecordedTestStorage.cs](/E:/repos/Westworld of Warcraft/RecordedTests.Shared/Storage/AzureBlobRecordedTestStorage.cs:47).

### WWoW.RecordedTests.Shared
- [ ] `WRTS-MISS-001` Implement S3 storage operations in [S3RecordedTestStorage.cs](/E:/repos/Westworld of Warcraft/WWoW.RecordedTests.Shared/Storage/S3RecordedTestStorage.cs:60).
- [ ] `WRTS-MISS-002` Implement Azure Blob storage operations in [AzureBlobRecordedTestStorage.cs](/E:/repos/Westworld of Warcraft/WWoW.RecordedTests.Shared/Storage/AzureBlobRecordedTestStorage.cs:55).

### Services/CppCodeIntelligenceMCP
- [ ] `CPPMCP-MISS-001` Implement file analysis response in [mcp_server.cpp](/E:/repos/Westworld of Warcraft/Services/CppCodeIntelligenceMCP/src/mcp_server.cpp:94).
- [ ] `CPPMCP-MISS-002` Implement symbol usage analysis in [CppAnalysisService.cs](/E:/repos/Westworld of Warcraft/Services/CppCodeIntelligenceMCP/Services/CppAnalysisService.cs:165).

### Services/PromptHandlingService
- [ ] `PHS-MISS-001` Review/replace `NotImplementedException` conversion guard in [PromptFunctionBase.cs](/E:/repos/Westworld of Warcraft/Services/PromptHandlingService/PromptFunctionBase.cs:47) with explicit supported-path handling.

### Tests/WoWSharpClient.Tests
- [ ] `WSC-TST-001` Resolve TODO placeholders in [SMSG_UPDATE_OBJECT_Tests.cs](/E:/repos/Westworld of Warcraft/Tests/WoWSharpClient.Tests/Handlers/SMSG_UPDATE_OBJECT_Tests.cs:16) and [OpcodeHandler_Tests.cs](/E:/repos/Westworld of Warcraft/Tests/WoWSharpClient.Tests/Handlers/OpcodeHandler_Tests.cs:44) with clear keep/remove decisions.

## Canonical Commands
1. Corpse-run validation:
- `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore --filter "FullyQualifiedName~DeathCorpseRunTests" --blame-hang --blame-hang-timeout 10m --logger "console;verbosity=minimal"`

2. Pathfinding service tests:
- `dotnet test Tests/PathfindingService.Tests/PathfindingService.Tests.csproj --configuration Release --no-restore --logger "console;verbosity=minimal"`

3. Physics calibration:
- `dotnet test Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --no-restore --settings Tests/Navigation.Physics.Tests/test.runsettings --logger "console;verbosity=minimal"`

4. Repo-scoped lingering process cleanup:
- `powershell -ExecutionPolicy Bypass -File .\run-tests.ps1 -CleanupRepoScopedOnly`

## Documentation and Markdown Navigation Tasks
- [ ] `DOCS-NAV-001` Create a concise navigation index for agents (entrypoints, major project docs, common commands, and file map strategy) in `docs/`.
- [ ] `DOCS-NAV-002` Standardize `TASKS.md` structure across projects (scope, ordered priorities, acceptance signals, simple commands, handoff block).
- [ ] `DOCS-NAV-003` Add markdown scanning conventions so long files are section-addressable (stable headers, short sections, explicit file references).
- [x] `DOCS-NAV-004` Add explicit "one sub-`TASKS.md` at a time" execution rule and handoff requirement for context/token discipline.
- [x] `DOCS-NAV-005` Add a lightweight rule for when to summarize instead of scanning whole trees to avoid context blowups.
- [x] `DOCS-NAV-006` Add loop-break and continuity protocol (`Last delta`, `Next command`, blocker handoff) so compact/handoff sessions do not repeat rediscovery.

## Sub-TASKS Execution Queue (One File at a Time)
Status key: `Pending` = needs direct inventory conversion/update, `Synced` = direct/task-level and ready, `Expanded` = direct/task-level plus implementation breakdown/acceptance details.

- [ ] `MASTER-SUB-001` `BotProfiles/TASKS.md` (`Expanded`) - execute `BP-MISS-001`, then `BP-MISS-002`, then `BP-MISS-003`, then `BP-MISS-004`.
- [ ] `MASTER-SUB-002` `Exports/TASKS.md` (`Expanded`) - umbrella routing-only model in place; execute `EXP-UMB-001`, then `EXP-UMB-002`, then `EXP-UMB-003`, then `EXP-UMB-004`.
- [ ] `MASTER-SUB-003` `Exports/BotCommLayer/TASKS.md` (`Expanded`) - execute `BCL-MISS-001`, then `BCL-MISS-002`, then `BCL-MISS-003`, then `BCL-MISS-004`.
- [ ] `MASTER-SUB-004` `Exports/BotRunner/TASKS.md` (`Expanded`) - execute `BR-MISS-001`, then `BR-MISS-002`, then `BR-MISS-003`.
- [ ] `MASTER-SUB-005` `Exports/GameData.Core/TASKS.md` (`Expanded`) - execute `GDC-MISS-001`, then `GDC-MISS-002`, then `GDC-MISS-003`.
- [ ] `MASTER-SUB-006` `Exports/Loader/TASKS.md` (`Expanded`) - execute `LDR-MISS-001`, then `LDR-MISS-002`, then `LDR-MISS-003`.
- [ ] `MASTER-SUB-007` `Exports/Navigation/TASKS.md` (`Expanded`) - execute `NAV-MISS-001`, then `NAV-MISS-002`, then `NAV-MISS-003`, then `NAV-MISS-004`.
- [ ] `MASTER-SUB-008` `Exports/WinImports/TASKS.md` (`Expanded`) - execute `WINIMP-MISS-001`, then `WINIMP-MISS-002`, then `WINIMP-MISS-003`, then `WINIMP-MISS-004`, then `WINIMP-MISS-005`.
- [ ] `MASTER-SUB-009` `Exports/WoWSharpClient/TASKS.md` (`Expanded`) - execute `WSC-MISS-001`, then `WSC-MISS-002`, then `WSC-MISS-003`, then `WSC-MISS-004`.
- [ ] `MASTER-SUB-010` `RecordedTests.PathingTests/TASKS.md` (`Expanded`) - execute `RPT-MISS-001`, then `RPT-MISS-002`, then `RPT-MISS-003`, then `RPT-MISS-004`, then `RPT-MISS-005`.
- [ ] `MASTER-SUB-011` `RecordedTests.Shared/TASKS.md` (`Expanded`) - execute `RTS-MISS-001`, then `RTS-MISS-002`, then `RTS-MISS-003`, then `RTS-MISS-004`.
- [ ] `MASTER-SUB-012` `Services/TASKS.md` (`Expanded`) - execute `SRV-UMB-001`, then `SRV-UMB-002`, then `SRV-UMB-003`, then `SRV-UMB-004`.
- [ ] `MASTER-SUB-013` `Services/BackgroundBotRunner/TASKS.md` (`Expanded`) - execute `BBR-MISS-001`, then `BBR-MISS-002`, then `BBR-MISS-003`, then `BBR-MISS-004`, then `BBR-MISS-005`.
- [ ] `MASTER-SUB-014` `Services/CppCodeIntelligenceMCP/TASKS.md` (`Expanded`) - execute `CPPMCP-BLD-001`, then `CPPMCP-MISS-001`, then `CPPMCP-MISS-002`, then `CPPMCP-ARCH-001`, then `CPPMCP-ARCH-002`, then `CPPMCP-TST-001`, then `CPPMCP-DOC-001`.
- [ ] `MASTER-SUB-015` `Services/DecisionEngineService/TASKS.md` (`Expanded`) - execute `DES-MISS-001`, then `DES-MISS-002`, then `DES-MISS-003`, then `DES-MISS-004`, then `DES-MISS-005`.
- [ ] `MASTER-SUB-016` `Services/ForegroundBotRunner/TASKS.md` (`Expanded`) - execute `FG-MISS-001`, then `FG-MISS-002`, then `FG-MISS-003`, then `FG-MISS-004`, then `FG-MISS-005`.
- [ ] `MASTER-SUB-017` `Services/LoggingMCPServer/TASKS.md` (`Expanded`) - execute `LMCP-MISS-001`, then `LMCP-MISS-002`, then `LMCP-MISS-003`, then `LMCP-MISS-004`, then `LMCP-MISS-005`, then `LMCP-MISS-006`.
- [ ] `MASTER-SUB-018` `Services/PathfindingService/TASKS.md` (`Expanded`) - execute `PFS-MISS-001`, then `PFS-MISS-002`, then `PFS-MISS-003`, then `PFS-MISS-004`, then `PFS-MISS-005`, then `PFS-MISS-006`, then `PFS-MISS-007`.
- [ ] `MASTER-SUB-019` `Services/PromptHandlingService/TASKS.md` (`Expanded`) - execute `PHS-MISS-001`, then `PHS-MISS-002`, then `PHS-MISS-003`.
- [ ] `MASTER-SUB-020` `Services/WoWStateManager/TASKS.md` (`Expanded`) - execute `WSM-MISS-001`, then `WSM-MISS-002`, then `WSM-MISS-003`, then `WSM-MISS-004`, then `WSM-MISS-005`.
- [ ] `MASTER-SUB-021` `Tests/TASKS.md` (`Expanded`) - execute `TST-UMB-001`, then `TST-UMB-002`, then `TST-UMB-003`, then `TST-UMB-004`, then `TST-UMB-005`.
- [ ] `MASTER-SUB-022` `Tests/BotRunner.Tests/TASKS.md` (`Expanded`) - execute `BRT-CR-001`, then `BRT-CR-002`, then `BRT-RT-001`, then `BRT-RT-002`, then `BRT-PAR-001`, then `BRT-PAR-002`.
- [ ] `MASTER-SUB-023` `Tests/Navigation.Physics.Tests/TASKS.md` (`Expanded`) - execute `NPT-MISS-001`, then `NPT-MISS-002`, then `NPT-MISS-003`.
- [ ] `MASTER-SUB-024` `Tests/PathfindingService.Tests/TASKS.md` (`Expanded`) - execute `PFS-TST-001`, then `PFS-TST-002`, then `PFS-TST-003`, then `PFS-TST-004`, then `PFS-TST-005`, then `PFS-TST-006`.
- [ ] `MASTER-SUB-025` `Tests/PromptHandlingService.Tests/TASKS.md` (`Expanded`) - execute `PHS-TST-001`, then `PHS-TST-002`, then `PHS-TST-003`, then `PHS-TST-004`, then `PHS-TST-005`.
- [ ] `MASTER-SUB-026` `Tests/RecordedTests.PathingTests.Tests/TASKS.md` (`Expanded`) - execute `RPTT-TST-001`, then `RPTT-TST-002`, then `RPTT-TST-003`, then `RPTT-TST-004`, then `RPTT-TST-005`, then `RPTT-TST-006`.
- [ ] `MASTER-SUB-027` `Tests/RecordedTests.Shared.Tests/TASKS.md` (`Expanded`) - execute `RTS-TST-001`, then `RTS-TST-002`, then `RTS-TST-003`, then `RTS-TST-004`, then `RTS-TST-005`, then `RTS-TST-006`.
- [ ] `MASTER-SUB-028` `Tests/Tests.Infrastructure/TASKS.md` (`Expanded`) - execute `TINF-MISS-001`, then `TINF-MISS-002`, then `TINF-MISS-003`, then `TINF-MISS-004`, then `TINF-MISS-005`, then `TINF-MISS-006`.
- [ ] `MASTER-SUB-029` `Tests/WowSharpClient.NetworkTests/TASKS.md` (`Expanded`) - execute `WSCN-TST-001`, then `WSCN-TST-002`, then `WSCN-TST-003`, then `WSCN-TST-004`, then `WSCN-TST-005`, then `WSCN-TST-006`.
- [ ] `MASTER-SUB-030` `Tests/WoWSharpClient.Tests/TASKS.md` (`Expanded`) - execute `WSC-TST-001`, then `WSC-TST-002`, then `WSC-TST-003`, then `WSC-TST-004`.
- [ ] `MASTER-SUB-031` `Tests/WoWSimulation/TASKS.md` (`Expanded`) - execute `WSIM-TST-001`, then `WSIM-TST-002`, then `WSIM-TST-003`, then `WSIM-TST-004`, then `WSIM-TST-005`, then `WSIM-TST-006`.
- [ ] `MASTER-SUB-032` `Tests/WWoW.RecordedTests.PathingTests.Tests/TASKS.md` (`Expanded`) - execute `RPTT-TST-001`, then `RPTT-TST-002`, then `RPTT-TST-003`, then `RPTT-TST-004`, then `RPTT-TST-005`, then `RPTT-TST-006`.
- [ ] `MASTER-SUB-033` `Tests/WWoW.RecordedTests.Shared.Tests/TASKS.md` (`Expanded`) - execute `WRTS-TST-001`, then `WRTS-TST-002`, then `WRTS-TST-003`, then `WRTS-TST-004`, then `WRTS-TST-005`, then `WRTS-TST-006`.
- [ ] `MASTER-SUB-034` `Tests/WWoW.Tests.Infrastructure/TASKS.md` (`Expanded`) - execute `WWINF-TST-001`, then `WWINF-TST-002`, then `WWINF-TST-003`, then `WWINF-TST-004`, then `WWINF-TST-005`, then `WWINF-TST-006`.
- [ ] `MASTER-SUB-035` `UI/TASKS.md` (`Expanded`) - execute `UI-UMB-001`, then `UI-UMB-002`, then `UI-UMB-003`, then `UI-UMB-004`.
- [ ] `MASTER-SUB-036` `UI/Systems/Systems.AppHost/TASKS.md` (`Expanded`) - execute `SAH-MISS-001`, then `SAH-MISS-002`, then `SAH-MISS-003`, then `SAH-MISS-004`, then `SAH-MISS-005`, then `SAH-MISS-006`.
- [ ] `MASTER-SUB-037` `UI/Systems/Systems.ServiceDefaults/TASKS.md` (`Expanded`) - execute `SSD-MISS-001`, then `SSD-MISS-002`, then `SSD-MISS-003`, then `SSD-MISS-004`, then `SSD-MISS-005`, then `SSD-MISS-006`.
- [ ] `MASTER-SUB-038` `UI/WoWStateManagerUI/TASKS.md` (`Expanded`) - execute `UI-MISS-001`, then `UI-MISS-002`, then `UI-MISS-003`, then `UI-MISS-004`.
- [ ] `MASTER-SUB-039` `WWoW.RecordedTests.PathingTests/TASKS.md` (`Expanded`) - execute `WWRPT-RUN-001`, then `WWRPT-CFG-001`, then `WWRPT-PATH-001`, then `WWRPT-DOC-001`.
- [ ] `MASTER-SUB-040` `WWoW.RecordedTests.Shared/TASKS.md` (`Expanded`) - execute `WRTS-CONTRACT-001`, then `WRTS-CONTRACT-002`, then `WRTS-MISS-001`, then `WRTS-MISS-002`, then `WRTS-PARITY-001`, then `WRTS-DOC-001`.
- [ ] `MASTER-SUB-041` `WWoWBot.AI/TASKS.md` (`Expanded`) - completed: `AI-CORE-001`, `AI-CORE-002`, `AI-CORE-003`, `AI-SEM-001`, `AI-SEM-002`, `AI-TST-001`, `AI-SEC-001`; execute open parity IDs: `AI-PARITY-001`, then `AI-PARITY-CORPSE-001`, then `AI-PARITY-COMBAT-001`, then `AI-PARITY-GATHER-001`.

## Session Handoff
- Last updated: 2026-02-25
- Sub-`TASKS.md` coverage check: `41/41` local sub-task files are explicitly tracked in this master file.
- Current top priority: run the corpse/pathing documentation tranche one file at a time with no broad scans.
- Current queue file: `MASTER-SUB-001` -> `BotProfiles/TASKS.md`.
- Next queue file: `MASTER-SUB-002` -> `Exports/TASKS.md`.
- Last delta: applied queue-rotation guard for documentation continuity and rebased from queue-tail (`MASTER-SUB-041`) to the earliest unresolved file (`MASTER-SUB-001`) so one-by-one expansion can continue without rediscovery loops.
- Pass result: `delta shipped`
- Next command: `Get-Content -Path 'BotProfiles/TASKS.md' -TotalCount 360`
