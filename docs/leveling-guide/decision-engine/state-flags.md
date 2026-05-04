# Snapshot State Flags

> **Pass 1 skeleton.** Inventory of `WoWActivitySnapshot` fields the leveling-guide rules read. Some fields already exist in the WWoW codebase; some will need to be added as later passes introduce rules that depend on them. Each field is annotated with **status: existing / planned**.

## Conventions

- **Type column** uses C# notation since `WoWActivitySnapshot` is a C# / proto-generated type.
- **Status:**
  - `existing` — already on the snapshot (or trivially derivable from existing fields)
  - `planned` — a later pass needs this; track in the alignment backlog
- All planned fields are surfaced in [unlock-graph.md](unlock-graph.md) as well so the engine team has a single ratchet to add them.

## Identity

| Field | Type | Status | Notes |
|---|---|---|---|
| `CharacterName` | `string` | existing | |
| `Account` | `string` | existing | groups characters under a single end-state goal |
| `Faction` | `Faction { Alliance, Horde }` | existing | drives faction-locked decisions (Pal/Sha, BG turn-ins, city access) |
| `Race` | `Race` enum (1.12 races: Human/Dwarf/NightElf/Gnome/Orc/Tauren/Troll/Undead) | existing | racials + zone start |
| `Class` | `Class` enum (1.12 classes: 9, no DK/Monk/DH) | existing | |
| `Level` | `int` (1-60) | existing | bracket selector |
| `XPInLevel` / `XPToNextLevel` | `int` / `int` | existing | rest-XP heuristics |

## Talent points

| Field | Type | Status | Notes |
|---|---|---|---|
| `TalentPointsUnspent` | `int` | existing | |
| `TalentTreePoints` | `Dictionary<TreeId, int>` | planned | per-tree spend so engine can detect respec needs |
| `ActiveSpec` | `enum { Leveling, PvE, PvP, Hybrid }` | planned | derived from `TalentTreePoints`; informs gear / consumable picks |

## Quest log

| Field | Type | Status | Notes |
|---|---|---|---|
| `QuestsInLog` | `IReadOnlyList<int>` (questIds) | existing | |
| `QuestsCompleted` | `IReadOnlySet<int>` | existing | |
| `QuestStatus[questId]` | `enum { NotPicked, InProgress, Completable, Turned }` | existing | |
| `LastZoneId` / `CurrentZoneId` | `int` | existing | |
| `Hearthstone.BoundZoneId` | `int` | planned | needed for hearth-vs-fly decisions |

## Reputation

| Field | Type | Status | Notes |
|---|---|---|---|
| `Reputation[factionId]` | `RepStanding { Hostile..Exalted }` + raw value | existing | drives turn-in and gear unlock rules |
| `ReputationParagonable[factionId]` | `bool` | n/a (TBC concept) | not in 1.12.1 — Exalted is terminal |

## Profession / skill

| Field | Type | Status | Notes |
|---|---|---|---|
| `PrimaryProfessions` | `(SkillId, int rank, int max)[]` | existing | max two |
| `SecondaryProfessions` | `(SkillId, int rank, int max)[]` | existing | Cooking / First Aid / Fishing |
| `WeaponSkill[weaponType]` | `int` (0..300) | existing | gates weapon-skill quest eligibility |
| `RidingSkill` | `int` (0/75/150) | existing | mount tier |

## Gear

| Field | Type | Status | Notes |
|---|---|---|---|
| `EquippedItems[slot]` | `ItemRef` | existing | |
| `BagItems` | `ItemRef[]` | existing | |
| `BankItems` | `ItemRef[]` | planned | bank scan only when at a city |
| `EffectiveItemLevel` | `int` | planned | aggregate of equipped slots; used as a "gear gate" proxy |
| `ResistancePool` | `Dictionary<School, int>` | planned | drives raid-readiness checks (Fire res for MC, Frost for AQ40 P, Nature for Huhuran, Shadow for Loatheb) |

## Currency

| Field | Type | Status | Notes |
|---|---|---|---|
| `CopperOnHand` | `long` | existing | |
| `MountTier` | `enum { None, Apprentice40, Journeyman60, Epic60Class }` | planned | derived from `RidingSkill` + spellbook scan; class-epic detection (Charger/Dreadsteed) |

## Attunements / keys

| Field | Type | Status | Notes |
|---|---|---|---|
| `KeysInBags` | `IReadOnlySet<int>` (itemIds) | existing | Shadowforge Key, Crescent Key, Master's Key, Skeleton Key, Workshop Key, Seal of Ascension |
| `Attunements` | `IReadOnlySet<AttunementId>` | planned | enum: `MoltenCore`, `Onyxia`, `BlackwingLair`, `Naxxramas` |
| `OnyxiaQuestProgress` | `enum` | planned | per-step progress through Marshal Windsor / Eitrigg chain |

## PvP

| Field | Type | Status | Notes |
|---|---|---|---|
| `HonorThisWeek` | `int` (raw HKs / contribution points) | existing | |
| `HonorRank` | `int` (0-14) | existing | rank 14 = Grand Marshal / High Warlord (terminal) |
| `BattlegroundsCompleted[bgId]` | `int` count | existing | |
| `PvPMarks` | `Dictionary<bgId, int>` | n/a | marks-of-honor are TBC; in 1.12.1 BG rep is direct |

## Buffs / world buffs

| Field | Type | Status | Notes |
|---|---|---|---|
| `ActiveBuffs` | `BuffRef[]` (max 16 in 1.12) | existing | engine should detect Onyxia / Nef / Rend / Songflower / DM tribute buffs before raid-window actions |
| `WorldBuffWindowOpen` | `bool` | planned | derived: ≥1 raid-relevant world buff active AND raid scheduled within decay window |

## Telemetry / housekeeping

| Field | Type | Status | Notes |
|---|---|---|---|
| `PlayedTime` | `TimeSpan` | existing | sanity-check progression rate |
| `LastDeathLocation` | `Vector3?` | existing | corpse-run logic |
| `IsInCombat` | `bool` | existing | suspends most non-combat actions |
| `IsInDungeonInstance` | `bool` | existing | suppresses outdoor questing rules |
| `PartyComposition` | `(Class, Level)[]` | existing | dungeon-suitability gate |

## Decision-Engine Rules

This file does not contain rules — it is the **vocabulary** other rule files import. Every `## Decision-Engine Rules` section in the rest of the guide must reference fields **only by the names above**. New rules that need a field not listed here MUST add it to this table with status `planned` in the same PR.

## Snapshot Fields Needed

(Self-referential — this *is* the field inventory.)
