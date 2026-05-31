---
name: loadout-template-authoring
description: Convert a leveling-guide spec into a CharacterBuildConfig / loadout template (level, spells, skills, gear, reps, quests, talents) that the bot applies via LoadoutTask. Use when defining what a character should know and wear.
trigger: author a loadout, character build config, loadout template, APPLY_LOADOUT, gear and spells template, leveling guide to config, CharacterBuildConfig
---

# Loadout Template Authoring

## Goal

Turn a leveling-guide target (a Fury Warrior at L60 ready for RFC, say) into a
loadout template the bot can apply: target level, spells, skills, equipped/
supplemental items, faction reps, completed quests, and talents — validated
against the MaNGOS data and applied by `LoadoutTask`.

## Inputs

- The character (account/name), target level, and the desired spells/skills/gear/
  reps/quests/talents.
- Key files (verified):
  - **Field reference + GM-command translation + worked example:**
    `docs/Spec/17_LOADOUT.md`.
  - POCO: `Services/WoWStateManager/Settings/CharacterSettings.cs`
    (`LoadoutSpecSettings`).
  - POCO↔proto: `Services/WoWStateManager/Settings/LoadoutSpecConverter.cs`.
  - Source JSON: `Config/characters/<realm>.json` (hot-loaded by StateManager).
  - Executor: `Exports/BotRunner/Tasks/LoadoutTask.cs` (`BuildPlan`); progress on
    `WoWActivitySnapshot.loadout_status` (LOADOUT_IN_PROGRESS → LOADOUT_READY).
  - Test: `Tests/BotRunner.Tests/LoadoutSpecConverterTests.cs`.
- Area rules: `.github/instructions/services.instructions.md`,
  `.github/instructions/config.instructions.md`.

## Preconditions

- Item/spell/skill/faction IDs validated against MaNGOS **read-only** (e.g.
  `item_template`, `spell_template`, `faction_template`) — never mutate the DB.
- The owning service builds green.

## Procedure

1. Edit `Config/characters/<realm>.json`; set the bot's `AccountName` /
   `CharacterName`.
2. Add a `Loadout` block with the Spec/17 fields: `TargetLevel`,
   `SpellIdsToLearn[]`, `Skills[]`, `EquipItems[]`, `SupplementalItemIds[]`,
   `ElixirItemIds[]`, `FactionReps[]`, `CompletedQuestIds[]`, `TalentTemplate`.
3. Validate every ID against the MaNGOS tables (read-only) per the Spec/17
   translation table.
4. StateManager hot-loads the JSON; `LoadoutSpecConverter.ToProto()` maps it for
   the wire; an `APPLY_LOADOUT` objective drives `LoadoutTask.BuildPlan()` in order.
5. Confirm the bot reports `loadout_status` progressing to LOADOUT_READY.

## Verification

- `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --filter "FullyQualifiedName~LoadoutSpecConverter"`.
- `.\scripts\test-fast.ps1`.
- In a LiveValidation test, stage the loadout and assert `loadout_status` reaches
  ready (see [[live-validation-test-authoring]]); GM staging uses `.learn`,
  `.additem`, `.setskill` via the SOAP/bot-chat helpers.

## Outputs

- A `Loadout` block in `Config/characters/<realm>.json`.
- Updated `docs/Spec/17_LOADOUT.md` worked example if a new pattern is introduced.

## Failure modes and recovery

- **Invalid IDs** (wrong client version / typo) → loadout step fails; validate
  against MaNGOS first.
- **Direct DB writes** to apply the loadout — forbidden; use the loadout pipeline /
  SOAP GM commands.
- **Order-dependent steps** (skills before gear that requires them) — follow the
  `BuildPlan` ordering in Spec/17.

## Related skills

- [[mode-handler-implementation]] — `Automated` mode dispatches `APPLY_LOADOUT`.
- [[live-validation-test-authoring]] — stage + assert the loadout.
- [[activity-catalog-bootstrap]] — Activities reference role/loadout templates.
- Reference: `docs/Spec/17_LOADOUT.md`.
