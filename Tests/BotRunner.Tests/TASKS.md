# BotRunner.Tests Tasks

## Scope
- Directory: `Tests/BotRunner.Tests`
- Project: `BotRunner.Tests.csproj`
- Master tracker: `docs/TASKS.md`
- Focus: keep BotRunner deterministic tests and live-validation assertions aligned with current FG/BG runtime parity.

## Execution Rules
1. Do not run live validation until the remaining code-only parity work is complete.
2. Prefer compile-only or deterministic test slices when the change only touches live-validation assertions.
3. Keep assertions snapshot-driven; do not reintroduce direct DB validation or FG/BG-specific skip logic for fields that now exist in both models.
4. Use repo-scoped cleanup only; never blanket-kill `dotnet` or `WoW.exe`.
5. Update this file in the same session as any BotRunner test delta.

## Active Priorities
1. Live-validation expectation cleanup
- [x] Remove stale FG coinage stub assumptions from mail/trainer live assertions now that `WoWPlayer.Coinage` is descriptor-backed.
- [ ] Sweep remaining live-validation suites for FG/BG divergence assumptions that are no longer true.
- [ ] Keep moving explicitly BG-only live suites onto BG-only fixtures/settings so behavior regressions are isolated without launching unnecessary FG clients.
- [x] Use dedicated non-overlapping battleground account pools (`AV*`, `WSG*`, `AB*`) and preserve matching existing characters at launch so PvP-rank-bearing battleground characters are reused instead of erased/recreated.
- [ ] Keep the Ratchet fishing slice split into completed FG packet-capture reference work versus the remaining comparison/instrumentation work. Focused FG capture and the focused dual Ratchet path test are green on the current binaries; the remaining open work is authoritative staged local-pool activation/visibility attribution on nondeterministic reruns plus the actual FG/BG packet-sequence comparison.

2. Alterac Valley live-validation expansion
- [x] Reduce `BackgroundBotRunner` per-instance memory / launch pressure enough for AV to bring all `80` accounts in-world; latest AV run settled to `bg=80,off=0` before objective push.
- [x] Get the AV first-objective live slice green; `AV_FullMatch_EnterPrepQueueMountAndReachObjective` now passes with `HordeObjective near=30` and `AllianceObjective near=40`.

3. Final validation prep
- [ ] Keep the final live-validation chunk queued until the remaining parity implementation work is done.
- [ ] Use the final run to collect fresh Orgrimmar transport evidence with the updated FG recorder.

4. Movement/controller parity coverage
Known remaining work in this owner: `0` items.
- [x] Added deterministic coverage for the persistent `BADFACING` retry window that was holding the candidate `3/15` mining route in stationary combat.
- [x] Added targeted BG corpse-run coverage for live waypoint ownership: `DeathCorpseRunTests` now asserts the emitted `navtrace_<account>.json` captured `RetrieveCorpseTask` ownership and a non-null `TraceSnapshot`, with deterministic helper tests covering stable recording-file lookup/cleanup.
- [x] Session 188: `Parity_Durotar_RoadPath_Redirect` proves pause/resume packet ordering. BG `SET_FACING` on mid-route redirects now matches FG. Full live proof bundle green.

## Simple Command Set
1. Build:
- `dotnet build Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false`

2. Deterministic snapshot/protobuf slice:
- `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --filter "FullyQualifiedName~WoWActivitySnapshotMovementTests" --logger "console;verbosity=minimal"`

3. Final live-validation chunk after code-only parity closes:
- `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --filter "FullyQualifiedName~BasicLoopTests|FullyQualifiedName~MovementSpeedTests|FullyQualifiedName~CombatBgTests" -v n --blame-hang --blame-hang-timeout 5m`

## Session Handoff
- Last updated: `2026-04-09`
- Pass result: `FG new-account/new-character live flow is stable across repeated runs with state-based realm wizard handling`
- Last delta:
  - Revalidated `ForegroundNewAccountFlowTests.NewAccount_NewCharacter_EntersWorld` after realm wizard no-sweep action hardening; kept fixture focused on one fresh account/character per run.
  - Live test passed repeatedly with repo-local runtime/output roots:
    - `fg_new_account_flow_latest.trx` -> in-world after `129.8s`
    - `fg_new_account_flow_rerun1.trx` -> in-world after `122.5s`
    - `fg_new_account_flow_rerun2.trx` -> in-world after `121.7s`
    - `fg_new_account_flow_no_sweep.trx` -> in-world after `116.9s`
  - Deterministic guard coverage was updated in `FgRealmSelectScreenTests` to assert realm action Lua scripts do not include global frame sweeps.
  - `AlteracValleyFixture` Horde leader account changed from shared `TESTBOT1` to dedicated `AVBOT1`.
  - `ArathiBasinFixture` Horde leader account changed from shared `TESTBOT1` to dedicated `ABBOT1` (Alliance remains `ABBOTA1`).
  - `CoordinatorFixtureBase` launch prep now supports preserving existing characters when any configured race/class/gender match exists; `BattlegroundCoordinatorFixtureBase` enables this policy to avoid unnecessary erase/recreate cycles.
  - Added deterministic coverage for reusable-character matching and battleground account-pool separation.
- Validation:
  - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore --filter "FullyQualifiedName=BotRunner.Tests.LiveValidation.ForegroundNewAccountFlowTests.NewAccount_NewCharacter_EntersWorld" --logger "console;verbosity=minimal" --results-directory E:\repos\Westworld of Warcraft\tmp\test-runtime\results-live --logger "trx;LogFileName=fg_new_account_flow_no_sweep.trx"` -> `passed (1/1)`.
  - `dotnet test Tests/ForegroundBotRunner.Tests/ForegroundBotRunner.Tests.csproj --configuration Release --no-restore --filter "FullyQualifiedName~FgRealmSelectScreenTests|FullyQualifiedName~FgCharacterSelectScreenTests|FullyQualifiedName~ForegroundBotWorkerWorldEntryCinematicTests|FullyQualifiedName~LuaErrorDiagnosticsTests" --logger "console;verbosity=minimal"` -> `passed (21/21)`.
  - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore --filter "FullyQualifiedName~CoordinatorFixtureBaseTests|FullyQualifiedName~BattlegroundFixtureConfigurationTests" --logger "console;verbosity=minimal" --results-directory E:\repos\Westworld of Warcraft\tmp\test-runtime\results` -> `passed (31/31)`.
- Files changed:
  - `Services/ForegroundBotRunner/Frames/FgRealmSelectScreen.cs`
  - `Tests/ForegroundBotRunner.Tests/FgRealmSelectScreenTests.cs`
  - `Tests/BotRunner.Tests/LiveValidation/Battlegrounds/AlteracValleyFixture.cs`
  - `Tests/BotRunner.Tests/LiveValidation/Battlegrounds/ArathiBasinFixture.cs`
  - `Tests/BotRunner.Tests/LiveValidation/CoordinatorFixtureBase.cs`
  - `Tests/BotRunner.Tests/LiveValidation/BattlegroundFixtureConfigurationTests.cs`
  - `Tests/BotRunner.Tests/LiveValidation/CoordinatorFixtureBaseTests.cs`
  - `Tests/BotRunner.Tests/TASKS.md`
  - `docs/TASKS.md`
- Next command:
  - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore --filter "FullyQualifiedName~BotRunner.Tests.LiveValidation.Battlegrounds.BattlegroundEntryTests.AV_FullMatch_EnterPrepQueueMountAndReachObjective" --logger "console;verbosity=normal" --results-directory E:\repos\Westworld of Warcraft\tmp\test-runtime\results-live --logger "trx;LogFileName=av_fg_post_realm_stabilization.trx"`
- Highest-priority unresolved issue in this owner:
  - Ratchet fishing parity remains the highest live-validation gap (staged local-pool activation/visibility attribution and FG/BG packet-sequence comparison).
