# Task Archive

Completed items moved from TASKS.md.

## Archived Snapshot (2026-04-03) - RFC coordinator-only prep pattern

- [x] Keep RFC fixture prep-owned and coordinator-owned responsibilities separate all the way through the test lifecycle.
- [x] Ensure RFC coordinator coverage proves the coordinator does not emit setup/prep chat commands.
- Completion notes:
  - `RfcBotFixture` now disables the coordinator during prep, clears stale group state up front, performs revive/level/spell/gear/Orgrimmar staging, and only then re-enables the coordinator.
  - `DungeoneeringCoordinator` now moves from `WaitingForBots` directly into group formation, which keeps the RFC coordinator path out of the old `.learn` / `.character level` prep flow.
  - `CoordinatorStrictCountTests` now pin the contract deterministically: RFC waits for every bot before forming the group, and the coordinator-driven group/teleport flow never emits `.learn`, `.character level`, `.reset`, or `.additem`.

## Archived Snapshot (2026-04-02) - Alterac Valley roster/loadout contract

- [x] Expand `AlteracValleyFixture` from minimum-level queue prep to a level-`60` objective-ready roster contract.
- [x] Encode Horde FG `TESTBOT1` as a High Warlord Tauren Warrior and Alliance FG `AVBOTA1` as a Grand Marshal Paladin, then cover that contract in deterministic fixture configuration tests.
- [x] Add level-`60` class/role-appropriate gear, epic mounts, and baseline elixir staging for all `80` AV participants; non-FG bots should use next-tier-appropriate loadouts.
- Completion notes:
  - `AlteracValleyFixture` already stages the full 80-account AV roster with `TargetLevel=60`, objective-ready loadout prep, batch application of item sets/weapons/elixirs, and mounted first-objective dispatch helpers.
  - `AlteracValleyLoadoutPlan` now acts as the deterministic source of truth for honor-rank loadouts, faction mounts, baseline elixirs, and first-objective assignments for every AV account.
  - `BattlegroundFixtureConfigurationTests` proves the leader contracts and the roster/loadout contract deterministically (`11/11` green in an isolated output), so this work is no longer an active implementation item.

## Archived Snapshot (2026-04-02) - Focused FG fishing packet capture

- [x] Capture a focused FG Ratchet fishing packet reference once the task-owned fishing flow can complete end-to-end.
- Completion notes:
  - The focused live `Fishing_CaptureForegroundPackets_RatchetStagingCast` slice passed and emitted `packets_TESTBOT1.csv`, `transform_TESTBOT1.csv`, and `navtrace_TESTBOT1.json`.
  - The successful FG packet reference included `FishingTask pool_acquired`, `FishingTask in_cast_range_current`, `FishingTask cast_started`, and `FishingTask fishing_loot_success`.
  - This closes the FG reference portion of the fishing parity work; the remaining open work lives in the dual FG/BG parity slice, which is still blocked first on staged Ratchet pool activation/visibility and then on the remaining BG local-pier runtime route when staging succeeds.

## Archived Snapshot (2026-04-02) - Ratchet master-pool activation mapping

- [x] Map Barrens master-pool `2628` activation in-process so live failures can distinguish "local Ratchet children were never selected by the master pool" from "the selected child pool was active but never streamed/approached correctly."
- Completion notes:
  - Added `FishingPoolActivationAnalyzer` plus deterministic tests so the live harness now classifies `NoChildPoolsSpawned`, `MasterPoolSelectedNonLocal`, `LocalPoolSpawnedOnlyOnDirectProbe`, and `LocalPoolSpawnedButInvisible`.
  - April 2 live evidence now covers both local failure modes: some staged refreshes reported a local Ratchet child pool as spawned but still invisible from the dock stages, and other reruns showed the local Ratchet child pools only becoming spawnable on direct child-pool probes after the staged refresh path stayed empty.
  - The remaining open work is no longer "map the master pool." It is stabilizing local staged visibility and then fixing the post-staging `FishingTask` pier search-walk failure.

## Archived Snapshot (2026-04-02) - Ratchet Fishing Harness

- [x] Keep the Ratchet fishing harness honest: refresh the actual nearby child pools, try the local stage fallbacks in a fixed order, and fail with explicit "no local pool active" evidence before blaming pier/pathfinding.
- Completion notes:
  - `FishingProfessionTests` now refreshes the full local Ratchet child-pool set (`2620/2619/2627/2618/2626/2617/2621`) instead of assuming `.pool update 2628` alone is enough.
  - Both bots now use fixed local stage candidates before dispatching `StartFishing` (`FG: packet-capture -> parity`, `BG: parity -> packet-capture`), and the test refuses to attribute a failure to pathfinding until one of those stages actually surfaces a visible pool.
  - The live failure path now emits the full Barrens master-pool `2628` child-site map (`2607..2627`) directly in the xUnit output so future passes can compare local Ratchet children against the full master footprint without manual DB queries.
  - Latest live evidence is now more precise: some reruns still keep both local stages empty, while other reruns show local Ratchet children as staged-spawned or direct-probe-spawnable without ever becoming visible from the dock stages.

## Archived Snapshot (2026-02-23 09:27:22) - Tests/BotRunner.Tests/TASKS.md

# BotRunner.Tests Tasks

## Purpose
Drive full LiveValidation and BotRunner integration test refactor until FG and BG behaviors are aligned and deterministic.

## Execution Rules
- Work continuously until all tasks in this file are complete.
- Do not request approval; implement tasks continuously.
- Convert every new test finding into a task item immediately.
- Keep setup state-driven: read snapshot first, apply only missing preconditions.
- Use one deterministic setup path per test (no fallback trees).
- Do not use `.gobject add`.
- Do not issue GM chat setup commands while the sender is dead/ghost.
- Life/death/ghost/corpse assertions must come from `ActivitySnapshot` / ObjectManager fields, not SOAP responses.
- Avoid SOAP in normal test flow; use SOAP only for research and hard fallback recovery.

## LiveValidation Audit Queue

### 1. Death and Corpse Retrieval
- [x] Remove multi-branch kill fallback logic.
- [x] Add strict life-state transition checks (alive -> dead/ghost -> alive).
- [x] Emit command-scoped command response evidence for setup commands.
- [x] Restore one deterministic direct kill setup command that actually transitions FG to dead/ghost on this server build.
- [x] Investigate `.die` semantics on current server build (chat `.die` rejected as no-such-command in latest run).
- [x] Keep corpse test behavior phase command-clean: no GM/setup chat commands after kill; only `ReleaseCorpse` and `RetrieveCorpse`.
- [x] Assert and log distinct phases explicitly in snapshots: dead corpse (not ghost), ghost state, reclaim-delay countdown to zero, alive.
- [ ] Validate corpse run remains pathfinding-based (no direct fallback movement) on repeated runs.
- [x] Simplify death-command setup to direct `.kill` -> `.die` fallback without capability-probe command spam.
- [ ] Investigate intermittent corpse-run stall where FG remains ghosted with `moveFlags=0x10000000` and no movement toward corpse.
- [x] Simplify setup flow to snapshot-first strict-alive (fallback teleport only when setup position/map is invalid).
- [x] Capture corpse target from snapshot death transition; for ghost-only transitions use snapshot last-alive corpse fallback (no SOAP dependency in behavior path).
- [x] Restore `mangos.command` to authoritative MaNGOS 1.12 baseline and remove stale fixture/test-injected rows that produce `nonexistent command` reload warnings.
- [x] Apply one-time baseline restore from `D:\MaNGOS\sql\world_full_14_june_2021.sql` command section (4 canonical rows: `wareffortget`, `wareffortset`, and two debug commands), with pre-restore backup and post-restore verification.
- [x] Run LiveValidation with `WWOW_TEST_RESTORE_COMMAND_TABLE=1` to validate baseline restore path and confirm no command-table load warnings in `mangosd.exe` output.
- [ ] Normalize `mangos.command` against expected vanilla 1.12.1 command semantics and remove stale fixture-era help text artifacts (`Enabled by test fixture`).
- [ ] Build and apply a reproducible command-table migration for this repack that restores expected vanilla-era GM command behavior used by tests (`kill/die/select/revive` hierarchy), with source-backed command references and post-reload verification.

### 2. Gathering and Profession Loops
- [x] Keep natural spawn only flow (`.respawn` allowed, no spawn commands).
- [x] Ensure gather success requires actual skill increase.
- [ ] Reduce duplicate setup commands and unnecessary teleport retries.

### 3. Remaining Test-Class Audit
- [x] `FishingProfessionTests.cs` - snapshot-delta setup only; remove unconditional command spam.
- [x] `CraftingProfessionTests.cs` - snapshot-delta setup and targeted teardown only.
- [x] `EquipmentEquipTests.cs` - remove unconditional setup; drive from current equipment/inventory state.
- [ ] `CraftingProfessionTests.cs` FG parity follow-up - remove `.cast` fallback by fixing FG `CastSpell` crafting parity and re-enable strict FG assertion.
- [x] `GroupFormationTests.cs` - start from snapshot group state and verify deterministic cleanup.
- [x] `NpcInteractionTests.cs` - remove redundant setup commands and timing-only assertions.
- [x] `EconomyInteractionTests.cs` - remove redundant setup commands and timing-only assertions.
- [ ] `EconomyInteractionTests.cs` FG mailbox parity follow-up - restore strict FG mailbox assertion once NearbyObjects mailbox visibility is reliable.
- [x] `QuestInteractionTests.cs` - remove redundant setup commands and timing-only assertions.
- [x] `TalentAllocationTests.cs` - remove redundant setup commands and timing-only assertions.
- [ ] `TalentAllocationTests.cs` FG parity follow-up - restore strict FG spell-list assertion once FG snapshot `SpellList` reliably includes learned/already-known talent spells.
- [x] `CharacterLifecycleTests.cs` - tighten death/revive assertions to strict transitions.
- [x] `CombatLoopTests.cs` - verify deterministic setup and minimize redundant movement/setup commands.
- [x] `BasicLoopTests.cs` - replace timing-only checks with concrete snapshot assertions where missing.
- [ ] `CombatLoopTests.cs` snapshot follow-up - close BG/FG target selection visibility gap where `Player.Unit.TargetGuid` can remain unset/stale during successful live boar engages.

## Fixture and Infra Tasks
- [x] Snapshot-driven clean-state setup in fixture (alive/group cleanup).
- [x] Command delta logging (`[CMD-SEND]` + command-scoped `[CMD-RESP]`).
- [ ] Ensure fixture setup avoids unsupported command assumptions across different server command tables.
- [x] Skip unconditional SOAP revive in fixture when snapshot already strict-alive.
- [ ] Audit and fix server-command availability assumptions (SOAP/chat mismatch, HTTP 500 command failures) before death test execution.
- [ ] Keep StateManager action-forward behavior observable enough to diagnose dropped/misordered setup actions quickly.
- [ ] Reduce BG startup noise in FG-focused LiveValidation runs (for example follow-loop `Goto` churn, stuck-forward logs) so death/corpse diagnostics remain signal-heavy.

## Current Evidence
- `DeathCorpseRunTests`: refactor landed and repeated focused runs pass, but intermittent FG ghosted stall remains (`moveFlags=0x10000000`, no corpse-run progress).
- `FishingProfessionTests` refactor validated:
  - `dotnet test ... --filter "FullyQualifiedName~FishingProfessionTests"`
  - pass recorded in `tmp/fishing_run_post_refactor.log`.
- `CraftingProfessionTests` refactor validated:
  - setup is now snapshot-delta driven (alive guard, conditional teleport/learn/additem, no unconditional unlearn/relearn path).
  - BG strict assertion passes; FG uses `.cast` fallback and currently logs warning path for parity gap.
  - pass recorded in `tmp/crafting_run_post_refactor.log`.
- `EquipmentEquipTests` refactor validated:
  - setup is now snapshot-delta driven with strict alive guard and conditional proficiency/item setup.
  - assertion now requires bag-to-mainhand transition (`Worn Mace` count in bags decreases and mainhand GUID becomes non-zero).
  - pass recorded in `tmp/equipment_run_post_refactor.log`.
- `GroupFormationTests` refactor + BG group parser fix validated:
  - `SMSG_GROUP_LIST` parser corrected to MaNGOS 1.12.1 header layout, removing bogus BG group-size values (`1140850688` -> `1`).
  - focused run now passes with consistent leader GUID parity and deterministic cleanup.
  - pass recorded in `tmp/groupformation_run_post_parser_fix.log`.
- `NpcInteractionTests` refactor validated:
  - setup now uses snapshot deltas (strict-alive/location checks, conditional additem/money/level setup only when missing).
  - interactions now assert NPC discovery by `NpcFlags` and `InteractWith` dispatch success for BG/FG.
  - focused run passes and captures reduced command churn in `tmp/npcinteraction_run_post_refactor.log`.
- `EconomyInteractionTests` refactor validated:
  - setup now uses snapshot deltas (strict-alive/location checks, conditional item setup).
  - assertions now require banker/auctioneer interaction success for BG/FG.
  - mailbox path now validates BG strictly and logs FG warning when mailbox-like nearby objects are absent in FG snapshots.
  - focused run passes in `tmp/economy_run_post_refactor.log`.
- `QuestInteractionTests` refactor validated:
  - setup now uses strict-alive snapshot guard and conditional cleanup (`.quest remove` only when needed).
  - assertions now require snapshot quest-log transitions for add/remove and completion confirmation via quest-log change/removal or explicit completed chat response.
  - focused run passes in `tmp/quest_run_post_refactor_v2.log`.
- Quest snapshot plumbing validated:
  - `BotRunnerService.BuildPlayerProtobuf` now serializes `Player.QuestLogEntries` from `IWoWPlayer.QuestLog`.
  - FG `WoWPlayer.QuestLog` descriptor reads now implemented (20 slots x 3 fields).
- `TalentAllocationTests` refactor validated:
  - setup now uses strict-alive snapshot guard + conditional level/unlearn setup (no unconditional `.character level`).
  - BG path is strict (`SpellList` must include spell after `.learn`); FG path is warning-only pending spell-list parity.
  - focused run passes in `tmp/talent_run_post_refactor_v3.log`.
- `CombatLoopTests` targeting correction validated:
  - test now teleports to boar-dense coordinates and only selects creature candidates with boar identity (`entry=3098` / `Mottled Boar`), excluding allied/friendly NPC classes.
  - invalid target errors are now treated as hard failures (`You cannot attack that target`, `You should select a character or a creature`).
  - focused run passes in `tmp/combatloop_run_post_refactor_v8.log`.
  - parity gap remains: snapshot `TargetGuid` can be absent even when combat engage/kill succeeds (tracked task above).
- `CharacterLifecycleTests` + `BasicLoopTests` verification:
  - focused combined run passes (`10/10`) in `tmp/basic_character_post_refactor_verify.log`.

## Session Handoff
- Last audited class: `CombatLoopTests.cs`
- Commands run:
  - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --filter "FullyQualifiedName~CombatLoopTests" --logger "console;verbosity=normal" 2>&1 | Tee-Object -FilePath tmp/combatloop_run_post_refactor_v7.log`
  - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --filter "FullyQualifiedName~CombatLoopTests" --logger "console;verbosity=normal" 2>&1 | Tee-Object -FilePath tmp/combatloop_run_post_refactor_v8.log`
  - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --filter "FullyQualifiedName~CharacterLifecycleTests|FullyQualifiedName~BasicLoopTests" --logger "console;verbosity=normal" 2>&1 | Tee-Object -FilePath tmp/basic_character_post_refactor_verify.log`
- Snapshot/command evidence:
  - `CombatLoopTests` v7 fail evidence captured boar candidates (`entry=3098`) while snapshot `TargetGuid` remained unset for BG in some engage windows.
  - `CombatLoopTests` v8 pass confirms deterministic boar-only setup and successful kill flow without allied/invalid-target combat errors.
  - `CharacterLifecycleTests` + `BasicLoopTests` combined verification passes (`10/10`) after refactors.
- Files changed this session:
  - `Tests/BotRunner.Tests/LiveValidation/CombatLoopTests.cs`
  - `Tests/BotRunner.Tests/TASKS.md`
- Next class/task:
  - `CombatLoopTests` snapshot parity follow-up: make target-selection visibility (`Player.Unit.TargetGuid`) deterministic in activity snapshots during melee engage for BG/FG.

## Archive
Move completed items to `Tests/BotRunner.Tests/TASKS_ARCHIVE.md`.




## Archived Snapshot (2026-02-24 19:43:32) - Tests/BotRunner.Tests/TASKS.md

- [x] Use `.tele name {NAME} Orgrimmar` before kill in `DeathCorpseRunTests`.
- [x] Remove `ValleyOfTrials` setup dependency from corpse-run test flow.
- [x] Removed reseed/variant retry death-loop path from runback setup.
- [x] Preserved strict corpse lifecycle ordering in test assertions.
- [x] Added timeout/runsettings plumbing baseline for test sessions.
- [x] Switched corpse setup teleport from `ValleyOfTrials` to Orgrimmar named teleport command path.


## Archived Snapshot (2026-03-11) - Tests/BotRunner.Tests/TASKS.md

- [x] `BRT-OVR-003` Unblock the last BG live failure in `FishingProfessionTests`.
- Evidence:
  - BG now handles `SMSG_SUPERCEDED_SPELL` and `SMSG_REMOVED_SPELL` in the spell-state path.
  - `FishingData.ResolveCastableFishingSpellId(...)` prefers the highest currently-known fishing rank and falls back to skill-derived rank only when the known-spell list is missing.
  - `FishingProfessionTests` now passes with catch detection based on bag delta or skill-up.
  - Broad `LiveValidation` rerun finished `33 passed, 0 failed, 2 skipped`.

- [x] `BRT-OVR-005` Isolate the FG herbalism crash/group-formation fallout and prove no active gameobject spawn path remains.
- Evidence:
  - Repo scan + DB verification confirmed the Mangos Silverleaf error referenced the natural world row `gameobject.guid=1641` / `id=1618`, not a test-spawned node.
  - `GatheringProfessionTests` now keeps BG as the hard assertion path while FG mining/herbalism is best-effort reference coverage with safe-zone cleanup in `finally`.
  - `GroupFormationTests` now starts from `EnsureCleanSlateAsync()` and a live `CheckFgActionableAsync()` probe so post-crash FG restarts skip instead of timing out.
  - Validation:
    - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore --filter "FullyQualifiedName~GatheringProfessionTests|FullyQualifiedName~GroupFormationTests" --blame-hang --blame-hang-timeout 10m --logger "console;verbosity=minimal"` -> `2 passed, 1 skipped`
    - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore --filter "FullyQualifiedName~LiveValidation" --blame-hang --blame-hang-timeout 10m --logger "console;verbosity=minimal"` -> `33 passed, 0 failed, 2 skipped`

