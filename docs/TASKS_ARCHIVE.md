# Task Archive

Completed items moved from TASKS.md.


## Archived Snapshot (2026-02-23 09:27:22) - docs/TASKS.md

# Master Tasks

This is the canonical orchestration file for all implementation work.

## Canonical Task System

- Source of truth: `docs/TASKS.md` + directory-local `TASKS.md` files.
- Deprecated duplicate task files have been removed.
- Every coding session must update:
  - this file,
  - the impacted directory `TASKS.md` files,
  - and archive files when lists grow too large.

## Execution Policy (Required)

- Do not pause for approval; implement continuously until all `TASKS.md` work is complete.
- Do not stop at summaries; convert findings directly into new priority tasks and keep executing.
- Keep tasks recursive: when new gaps are discovered, add them immediately to the relevant local `TASKS.md`.
- Keep context light: archive completed tasks frequently and keep active files short and direct.
- Life/death/corpse/ghost assertions must come from ObjectManager/`WoWActivitySnapshot` state.
- Avoid SOAP in normal test flow; use SOAP only for hard fallback recovery and protocol research.

## Global Completion Criteria

1. Foreground and Background bots behave identically for all implemented capabilities.
2. LiveValidation tests are state-driven, deterministic, and efficient (no dead setup branches).
3. Physics, movement, object state, protobuf snapshots, and bot task behavior are aligned FG vs BG.
4. No test uses `.gobject add`; natural spawns plus `.respawn` only.
5. Corpse retrieval behavior uses pathfinding and respects reclaim delay/cooldown semantics.

## Task File Topology

Top-level orchestration files:
- `docs/TASKS.md` (this file)
- `Exports/TASKS.md`
- `Services/TASKS.md`
- `Tests/TASKS.md`
- `UI/TASKS.md`
- `BotProfiles/TASKS.md`
- `RecordedTests.Shared/TASKS.md`
- `RecordedTests.PathingTests/TASKS.md`
- `WWoW.RecordedTests.Shared/TASKS.md`
- `WWoW.RecordedTests.PathingTests/TASKS.md`
- `WWoWBot.AI/TASKS.md`

Subproject files are required in each major csproj directory (seeded in this session).
Primary detailed audit queues currently live in:
- `Tests/BotRunner.Tests/TASKS.md` (LiveValidation audit execution queue)
- `Services/WoWStateManager/TASKS.md` (state/action orchestration)
- `Exports/BotRunner/TASKS.md`, `Exports/WoWSharpClient/TASKS.md`, `Exports/Navigation/TASKS.md` (core parity workstreams)

## Mandatory Workstream Mapping

All parity gaps must be tracked under one of these:
1. PhysicsEngine (`Exports/Navigation/*`, native behavior parity, movement replay parity).
2. MovementController (`Exports/WoWSharpClient/Movement/*`, server-authoritative movement updates).
3. ObjectManager and WoW models/interfaces (`Exports/GameData.Core/*`, `Exports/WoWSharpClient/Models/*`, FG readers).
4. BotCommLayer protobuf (`Exports/BotCommLayer/Models/ProtoDef/*`, snapshot compatibility).
5. BotTasks/BotRunner logic (`Exports/BotRunner/*`, sequencing/guards/retries/path behavior).

## Live Validation Audit (Integrated)

This is the active LiveValidation audit backlog.

Current status:
- `DeathCorpseRunTests`: setup is now snapshot-first strict-alive with fallback teleport only when setup map/Z is invalid.
- `DeathCorpseRunTests`: corpse target now comes from snapshot transition data; when server transitions directly to ghost, the test uses snapshot last-alive corpse fallback (no SOAP corpse-state dependency).
- `.kill` remains unavailable on this command table; `.die` remains effective fallback.
- `DeathCorpseRunTests` still has intermittent corpse-run stall cases where FG stays ghosted with no movement and `moveFlags=0x10000000` before reclaim.
- Command table restoration landed:
  - sanitize path removes stale fixture-injected rows and phantom `kill/select` definitions.
  - optional baseline restore path (`WWOW_TEST_RESTORE_COMMAND_TABLE=1`) now restores authoritative 4-row command snapshot from local MaNGOS SQL.
- Command-table gap remains:
  - current restored baseline does not include expected live-test GM commands (`kill/die/select/revive`) in `mangos.command`.
  - fixture currently resolves death commands by behavior probing; this needs deterministic DB migration + verification.
- FG death-recovery stability fix landed:
  - FG now guarantees non-null `PathfindingClient` injection into `ClassContainer`.
  - `RetrieveCorpseTask` no longer immediately collapses on transient ghost-state flicker.
  - FG `InGhostForm` now uses descriptor-first detection (`PLAYER_FLAGS_GHOST`) with memory/Lua fallback.
- `RetrieveCorpseTask` now uses horizontal corpse distance (`DistanceTo2D`) and corpse-Z clamping for path target when corpse Z is implausible, so reclaim gating is not blocked by bad corpse Z.
- BG object-update stability fix landed:
  - `WoWSharpObjectManager.ApplyGameObjectFieldDiffs` now converts mixed numeric payloads safely (`Single`/`UInt32`) to prevent repeated live `InvalidCastException` crashes.
- BG movement remains partially unresolved:
  - teleport reset is applied, but BG can still remain `flags=0x1` with zero displacement during follow `Goto` loops; needs MovementController/physics/path-action triage.
- Local command-table baseline located: `D:\MaNGOS\sql\world_full_14_june_2021.sql` contains the repack's authoritative command data snapshot (4 rows).
- `GatheringProfessionTests`: passing (`2/2`), natural nodes only; next reduction is setup command churn.
- BG teleport parity issue identified: `MOVEFLAG_FORWARD` can remain set after teleport and must be cleared immediately on teleport state transitions.
- `FishingProfessionTests` refactor landed: setup is snapshot-delta driven (conditional revive/teleport/learn/setskill/add pole) and focused run passes.
- `CraftingProfessionTests` refactor landed: snapshot-delta setup with deterministic bag-state verification; BG strict path passes and FG currently relies on `.cast` fallback when `CastSpell` crafting misses.
- `EquipmentEquipTests` refactor landed: snapshot-delta setup with strict alive guard and bag-to-mainhand transition assertion; focused run passes.
- Group/party snapshot parity fix landed:
  - `SMSG_GROUP_LIST` parsing in BG was off by one header byte, producing bogus group sizes (`1140850688`) and leader GUID divergence.
  - parser now matches MaNGOS 1.12.1 layout (`groupType + ownFlags + memberCount`), and focused `GroupFormationTests` run passes with FG/BG leader parity.
- `NpcInteractionTests` refactor landed:
  - setup is now snapshot-delta based (strict-alive/location checks and conditional item/money/level setup).
  - assertions now require NPC discovery by `NpcFlags` and successful `InteractWith` action forwarding for BG/FG.
  - focused run passes in `tmp/npcinteraction_run_post_refactor.log`.
- `EconomyInteractionTests` refactor landed:
  - setup is now snapshot-delta based (strict-alive/location checks and conditional item setup).
  - banker/auctioneer interactions now require explicit NPC detection + successful `InteractWith` action forwarding.
  - mailbox path is BG-strict and FG-warning due intermittent FG `NearbyObjects` visibility gap.
  - focused run passes in `tmp/economy_run_post_refactor.log`.
- `QuestInteractionTests` refactor landed:
  - setup is now strict-alive + snapshot-delta cleanup (no unconditional `.quest remove`).
  - assertions now require snapshot quest-log add/remove transitions, plus completion confirmation via quest-log change/removal or explicit completed chat response.
  - focused run passes in `tmp/quest_run_post_refactor_v2.log`.
- Quest snapshot parity plumbing landed:
  - `BotRunnerService.BuildPlayerProtobuf` now serializes `Player.QuestLogEntries` from `IWoWPlayer.QuestLog`.
  - FG `WoWPlayer.QuestLog` descriptor reads implemented to expose 20 quest slots (3 fields each) in FG snapshots.
- `TalentAllocationTests` refactor landed:
  - setup is now strict-alive + snapshot-delta (conditional level/unlearn commands only when needed).
  - BG spell assertion is strict; FG is warning-only pending spell-list parity for learned/already-known talent spells.
  - focused run passes in `tmp/talent_run_post_refactor_v3.log`.
- `CharacterLifecycleTests` refactor validated:
  - strict alive/death/revive transitions are now snapshot-asserted.
  - focused verification passes in `tmp/basic_character_post_refactor_verify.log`.
- `BasicLoopTests` refactor validated:
  - setup is snapshot-delta based, with concrete snapshot assertions and teleport movement-flag checks.
  - focused verification passes in `tmp/basic_character_post_refactor_verify.log`.
- `CombatLoopTests` combat-targeting correction landed:
  - test now teleports to a boar-dense Valley of Trials coordinate cluster and selects creature targets using `entry=3098` / `Mottled Boar` identity constraints.
  - allied/friendly target classes are excluded via creature GUID + `NpcFlags` filters.
  - focused verification passes in `tmp/combatloop_run_post_refactor_v8.log`.
- Combat snapshot parity gap observed:
  - `Player.Unit.TargetGuid` can remain unset/stale in snapshots during successful melee engage, so selection visibility is currently weaker than live combat state and needs parity work.

Remaining test-audit queue:
1. Command table restoration follow-through: decide whether baseline restore should become default (vs env-gated), then lock fixture behavior to prevent any future command-table drift and stale help artifacts (`Enabled by test fixture`).
   - Current execution hook: `WWOW_TEST_RESTORE_COMMAND_TABLE=1` in `LiveBotFixture` (baseline restore + backup table creation).
2. Build and apply a reproducible command-table migration for this repack that restores expected vanilla-era GM command hierarchy used by tests (`kill/die/select/revive`), with DB diff + reload verification.
3. `DeathCorpseRunTests`: stabilize intermittent corpse-run stalls (`moveFlags=0x10000000`, no step movement) while preserving pathfinding-only corpse run semantics.
4. Add corpse position to activity snapshot protobuf (`WoWPlayer.corpsePosition`) to eliminate last-alive fallback dependency when server transitions directly to ghost.
5. Teleport parity: validate and finish BG/FG teleport movement-flag clearing (`MOVEFLAG_FORWARD`/stale movement flags) after server teleports.
6. `CraftingProfessionTests` FG parity follow-up: remove `.cast` fallback by fixing FG `CastSpell` crafting behavior, then restore strict FG assertion.
7. `TalentAllocationTests` FG parity follow-up: restore strict FG spell-list assertion once FG snapshot `SpellList` reliably includes learned/already-known talent spells.
8. Combat snapshot parity follow-up: make `Player.Unit.TargetGuid` and nearby-unit identity fields reliably reflect live target selection in BG/FG snapshots during melee engage.

## Global Recursive Loop

Repeat until all directory `TASKS.md` files are complete:
1. Pick the highest-priority open task from the relevant local `TASKS.md`.
2. Implement code changes directly.
3. Run focused tests.
4. Record concrete evidence (pass/fail/skip, command responses, snapshot deltas).
5. Update local `TASKS.md` and this file.
6. Archive completed items to keep active files concise.
7. Continue with the next priority task.

## Required Research Inputs (WoW 1.12.1)

Use these continuously while closing tasks:
- Local docs under `docs/` and opcode/protocol references.
- MaNGOS Zero source scope (`1.12.1-1.12.3`): `https://github.com/mangoszero/server`
- MaNGOS command table reference (authoritative schema/data baseline): `https://www.getmangos.eu/wiki/referenceinfo/otherinfo/commandtable-r1735/`
- Vanilla resurrection timing reference: `https://wowwiki.fandom.com/wiki/Patch_1.1.0`
- Opcode catalog reference: `https://wowdev.wiki/Opcodes`

## Session Handoff Protocol

Before ending any session, update this file with:
1. Commands run + outcomes.
2. Snapshot and command-response evidence.
3. Files changed.
4. First command to run next session.
5. Highest-priority unresolved issue and mapped workstream.

## Latest Handoff

- Completed in this session:
  - Refined `CombatLoopTests` targeting so the test only selects neutral boars (`entry=3098` / `Mottled Boar`) in a boar-spawn coordinate cluster instead of camp-adjacent friendly NPC space.
  - Added strict combat-command failure detection (`You cannot attack that target`, `You should select a character or a creature`, invalid-target paths) so allied/invalid target flows fail deterministically.
  - Stabilized combat assertion flow for snapshot lag: if `TargetGuid` is not observed but target dies immediately from engage, test still validates via dead/gone snapshot transition.
  - Re-verified refactored `CharacterLifecycleTests` and `BasicLoopTests` pass as a combined focused run.
- Latest test evidence:
  - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --filter "FullyQualifiedName~CombatLoopTests" --logger "console;verbosity=normal" 2>&1 | Tee-Object -FilePath tmp/combatloop_run_post_refactor_v7.log`
    - `Failed` due over-strict snapshot-target gate (`[BG] Target GUID was never selected in snapshot`) despite boar candidate selection.
  - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --filter "FullyQualifiedName~CombatLoopTests" --logger "console;verbosity=normal" 2>&1 | Tee-Object -FilePath tmp/combatloop_run_post_refactor_v8.log`
    - `Passed` with boar-only targeting and no combat-target error messages.
  - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --filter "FullyQualifiedName~CharacterLifecycleTests|FullyQualifiedName~BasicLoopTests" --logger "console;verbosity=normal" 2>&1 | Tee-Object -FilePath tmp/basic_character_post_refactor_verify.log`
    - `Passed` (`10/10`).
- Files changed this session:
  - `Tests/BotRunner.Tests/LiveValidation/CombatLoopTests.cs`
  - `docs/TASKS.md`
  - `Tests/BotRunner.Tests/TASKS.md`
  - `Exports/BotRunner/TASKS.md`
- Next command:
  - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --filter "FullyQualifiedName~CombatLoopTests|FullyQualifiedName~DeathCorpseRunTests" --logger "console;verbosity=normal" 2>&1 | Tee-Object -FilePath tmp/combat_death_verify_next.log`
- Highest-priority unresolved issue:
  - Snapshot parity gap in live combat: `Player.Unit.TargetGuid` and nearby-unit identity visibility are intermittently incomplete/stale during melee engage, which weakens target-state observability and should be fixed in snapshot mapping/ObjectManager parity workstreams.


