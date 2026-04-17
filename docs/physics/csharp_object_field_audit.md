# C# Object Field Audit

## Scope

This is the P2.4.2 audit for the managed object models that matter to packet/state
parity in WoW 1.12.1:

- `WoWObject`
- `WoWGameObject`
- `WoWUnit`
- `WoWPlayer`
- `WoWLocalPlayer`
- `WoWLocalPet`

## Evidence anchors

- `docs/physics/cgobject_layout.md`
  - descriptor storage regions proved from `0x466C70`
  - `CMovement` inner offsets already pinned (`+0x10` position, `+0x1C` facing, `+0x40` flags, `+0x78` fall time, `+0x88..+0x9C` speeds)
- `docs/physics/smsg_update_object_handler.md`
  - create-path vs cached-create mutation order from `0x4651A0`, `0x4660A0`, `0x466350`, `0x466590`, `0x466A20`
- Managed application sites
  - `WoWSharpObjectManager.Objects.cs`
  - `ApplyObjectFieldDiffs`
  - `ApplyGameObjectFieldDiffs`
  - `ApplyUnitFieldDiffs`
  - `ApplyPlayerFieldDiffs`

## `WoWObject` -> `CGObject_C`

| Managed field/group | WoW field source | Notes |
| --- | --- | --- |
| `HighGuid` / `Guid` | `OBJECT_FIELD_GUID` low/high | identity only; parser reads it, but managed object identity should not be mutated after creation |
| `Entry` | `OBJECT_FIELD_ENTRY` | direct mapping |
| `ScaleX` | `OBJECT_FIELD_SCALE_X` | direct mapping |
| `Position` / `Facing` | movement block, or typed descriptor position fields for GO/dynamic/corpse | not base-object descriptors in current evidence; stored on the shared managed base for convenience |
| `LastUpdated` | movement packet client-time / heartbeat time | runtime movement timestamp, not a descriptor field |

Intentional omissions:

- `OBJECT_FIELD_TYPE` is not copied into a separate property because runtime class selection already carries that information.
- Any WoW.exe-internal update-mask or cache/link members are intentionally not modeled. They are container/layout details, not bot-readable game state.

## `WoWGameObject` -> `CGGameObject_C`

| Managed field/group | WoW field source | Notes |
| --- | --- | --- |
| `CreatedBy` | `OBJECT_FIELD_CREATED_BY` low/high | direct mapping |
| `DisplayId` | `GAMEOBJECT_DISPLAYID` | direct mapping |
| `Flags` | `GAMEOBJECT_FLAGS` | direct mapping |
| `Rotation[]` | `GAMEOBJECT_ROTATION .. GAMEOBJECT_STATE-1` | descriptor-backed rotation payload |
| `GoState` | `GAMEOBJECT_STATE` | direct mapping |
| `Position.X/Y/Z` | `GAMEOBJECT_POS_X/Y/Z` or movement block | typed descriptor position exists for game objects |
| `Facing` | `GAMEOBJECT_FACING` or movement block | typed descriptor facing exists for game objects |
| `DynamicFlags` | `GAMEOBJECT_DYN_FLAGS` | direct mapping |
| `FactionTemplate` | `GAMEOBJECT_FACTION` | direct mapping |
| `TypeId` | `GAMEOBJECT_TYPE_ID` | direct mapping |
| `Level` | `GAMEOBJECT_LEVEL` | direct mapping |
| `ArtKit` | `GAMEOBJECT_ARTKIT` | direct mapping |
| `AnimProgress` | `GAMEOBJECT_ANIMPROGRESS` | direct mapping |
| `MovementSpline*` / spline-facing state | `SMSG_MONSTER_MOVE`, spline packets | runtime movement state, not `SMSG_UPDATE_OBJECT` descriptors |
| `Name` | runtime DB/spell/object lookup, not descriptor-backed here | intentional omission from packet model |

Intentional omissions:

- Door/transport/quest-item subtype semantics are still behavior work, not missing descriptor copies.
- WoW.exe graphics-only state beyond the descriptor ranges above is not currently modeled because BG logic does not consume it.

## `WoWUnit` -> `CGUnit_C`

| Managed field/group | WoW field source | Notes |
| --- | --- | --- |
| `Charm`, `Summon`, `CharmedBy`, `SummonedBy`, `CreatedBy`, `TargetHighGuid`, `Persuaded`, `ChannelObject` | corresponding `UNIT_FIELD_*` GUID low/high pairs | direct mappings |
| `TargetGuid` | derived from `UNIT_FIELD_TARGET` low/high | convenience mirror of `TargetHighGuid.FullGuid` |
| `Health`, `MaxHealth` | `UNIT_FIELD_HEALTH`, `UNIT_FIELD_MAXHEALTH` | direct mappings |
| `Powers` / `MaxPowers` | `UNIT_FIELD_POWER1..5`, `UNIT_FIELD_MAXPOWER1..5` | mana/rage/focus/energy/happiness only in current model |
| `Level` | `UNIT_FIELD_LEVEL` | direct mapping |
| `FactionTemplate` | `UNIT_FIELD_FACTIONTEMPLATE` | direct mapping |
| `UnitReaction` | derived from `FactionTemplate` vs local player faction | runtime convenience, not a descriptor |
| `Bytes0` | `UNIT_FIELD_BYTES_0` | raw bytes retained |
| `Race`, `Class`, `Gender` on player objects | unpacked from `UNIT_FIELD_BYTES_0` | derived convenience fields |
| `VirtualItemSlotDisplay[]` | `UNIT_VIRTUAL_ITEM_SLOT_DISPLAY .. _02` | direct mapping |
| `VirtualItemInfo[]` | `UNIT_VIRTUAL_ITEM_INFO .. _05` | direct mapping |
| `UnitFlags` | `UNIT_FIELD_FLAGS` | direct mapping |
| `AuraFields[]`, `AuraFlags[]`, `AuraLevels[]`, `AuraApplications[]`, `AuraState` | `UNIT_FIELD_AURA*` ranges | raw aura state retained |
| `Buffs` / `Debuffs` | rebuilt from raw aura arrays after field application | derived lists, not direct descriptor storage |
| `BaseAttackTime`, `OffhandAttackTime`, `OffhandAttackTime1` | `UNIT_FIELD_BASEATTACKTIME`, `UNIT_FIELD_OFFHANDATTACKTIME`, `UNIT_FIELD_RANGEDATTACKTIME` | direct mappings |
| `BoundingRadius`, `CombatReach` | `UNIT_FIELD_BOUNDINGRADIUS`, `UNIT_FIELD_COMBATREACH` | direct mappings |
| `DisplayId`, `NativeDisplayId`, `MountDisplayId` | `UNIT_FIELD_DISPLAYID`, `UNIT_FIELD_NATIVEDISPLAYID`, `UNIT_FIELD_MOUNTDISPLAYID` | direct mappings |
| `MinDamage`, `MaxDamage`, `MinOffhandDamage`, `MaxOffhandDamage` | matching `UNIT_FIELD_*DAMAGE` fields | direct mappings |
| `Bytes1`, `Bytes2` | `UNIT_FIELD_BYTES_1`, `UNIT_FIELD_BYTES_2` | raw bytes retained |
| `PetNumber`, `PetNameTimestamp`, `PetExperience`, `PetNextLevelExperience` | matching `UNIT_FIELD_PET*` fields | direct mappings |
| `DynamicFlags` | `UNIT_DYNAMIC_FLAGS` | direct mapping |
| `ChannelingId`, `SpellcastId`, `IsCasting`, `IsChanneling` | `UNIT_CHANNEL_SPELL`; cast state from packet/runtime state | `SpellcastId` is only partially descriptor-backed today |
| `ModCastSpeed` | `UNIT_MOD_CAST_SPEED` | direct mapping |
| `CreatedBySpell` | `UNIT_CREATED_BY_SPELL` | direct mapping |
| `NpcFlags`, `NpcEmoteState`, `TrainingPoints` | `UNIT_NPC_FLAGS`, `UNIT_NPC_EMOTESTATE`, `UNIT_TRAINING_POINTS` | direct mappings |
| `Strength`, `Agility`, `Stamina`, `Intellect`, `Spirit` | `UNIT_FIELD_STAT0..4` | direct mappings |
| `Resistances[]` | `UNIT_FIELD_RESISTANCES .. _06` | direct mapping |
| `BaseMana`, `BaseHealth` | `UNIT_FIELD_BASE_MANA`, `UNIT_FIELD_BASE_HEALTH` | direct mappings |
| `AttackPower*`, `RangedAttackPower*` | matching `UNIT_FIELD_ATTACK_POWER*` and `UNIT_FIELD_RANGED_ATTACK_POWER*` fields | direct mappings |
| `MinRangedDamage`, `MaxRangedDamage` | `UNIT_FIELD_MINRANGEDDAMAGE`, `UNIT_FIELD_MAXRANGEDDAMAGE` | direct mappings |
| `PowerCostModifiers[]`, `PowerCostMultipliers[]` | matching `UNIT_FIELD_POWER_COST_*` ranges | direct mappings |
| `MovementFlags`, `MovementFlags2`, `FallTime`, `WalkSpeed`, `RunSpeed`, `RunBackSpeed`, `SwimSpeed`, `SwimBackSpeed`, `TurnRate`, `Transport*`, `SwimPitch`, `Jump*`, `SplineElevation` | `CMovement` block fields, not unit descriptors | movement-component state rather than descriptor storage |
| `Extrapolation*` | remote-unit prediction runtime state | derived/runtime-only |
| `Spline*` / facing-target state | `SMSG_MONSTER_MOVE` and spline packets | runtime-only, not `SMSG_UPDATE_OBJECT` descriptor fields |

Intentional omissions:

- Threat table, spell queue state, and any WoW.exe-only pointers/lists are not modeled because no stable 1.12.1 field offsets are documented yet.
- `SpellcastId` is still not sourced from a proven descriptor field in this pass; current combat logic relies on packet-driven updates.

## `WoWPlayer` -> `CGPlayer_C`

| Managed field/group | WoW field source | Notes |
| --- | --- | --- |
| `PlayerFlags` | `PLAYER_FLAGS` | direct mapping |
| `GuildId`, `GuildRank`, `GuildTimestamp` | `PLAYER_GUILDID`, `PLAYER_GUILDRANK`, `PLAYER_GUILD_TIMESTAMP` | direct mappings |
| `PlayerBytes`, `PlayerBytes2`, `PlayerBytes3`, `FieldBytes`, `FieldBytes2`, `Bytes`, `Bytes3` | matching `PLAYER_*BYTES*` fields | raw byte storage retained |
| `DuelArbiter`, `ComboTarget` | matching GUID low/high pairs | direct mappings |
| `QuestLog[]` | `PLAYER_QUEST_LOG_1_1 .. PLAYER_QUEST_LOG_LAST_3` | quest id / counters / state triplets |
| `VisibleItems[]` | `PLAYER_VISIBLE_ITEM_1_CREATOR .. PLAYER_VISIBLE_ITEM_19_PAD` | creator/owner/contained/gift/itemId/stack/durability/property-seed payloads |
| `Inventory[]` | `PLAYER_FIELD_INV_SLOT_HEAD .. before PACK_SLOT_1` | raw packed GUID words |
| `PackSlots[]`, `BankSlots[]`, `BankBagSlots[]`, `VendorBuybackSlots[]`, `KeyringSlots[]` | matching `PLAYER_FIELD_*_SLOT_*` ranges | raw packed GUID words / slot payloads |
| `Farsight` | `PLAYER_FARSIGHT` | direct mapping |
| `XP`, `NextLevelXP` | `PLAYER_XP`, `PLAYER_NEXT_LEVEL_XP` | direct mappings |
| `SkillInfo[]` | `PLAYER_SKILL_INFO_1_1 .. +383` | three uint32s per skill slot |
| `CharacterPoints1`, `CharacterPoints2` | matching `PLAYER_CHARACTER_POINTS*` fields | direct mappings |
| `TrackCreatures`, `TrackResources` | matching `PLAYER_TRACK_*` fields | direct mappings |
| `BlockPercentage`, `DodgePercentage`, `ParryPercentage`, `CritPercentage`, `RangedCritPercentage` | matching `PLAYER_*PERCENTAGE` fields | direct mappings |
| `ExploredZones[]` | `PLAYER_EXPLORED_ZONES_1 .. before REST_STATE_EXPERIENCE` | direct mapping |
| `RestStateExperience` | `PLAYER_REST_STATE_EXPERIENCE` | direct mapping |
| `Coinage` | `PLAYER_FIELD_COINAGE` | direct mapping |
| `StatBonusesPos[]`, `StatBonusesNeg[]` | `PLAYER_FIELD_POSSTAT*`, `PLAYER_FIELD_NEGSTAT*` | direct mappings |
| `ResistBonusesPos[]`, `ResistBonusesNeg[]` | positive/negative resistance bonus ranges | direct mappings |
| `ModDamageDonePos[]`, `ModDamageDoneNeg[]`, `ModDamageDonePct[]` | corresponding `PLAYER_FIELD_MOD_DAMAGE_DONE_*` ranges | direct mappings |
| `AmmoId`, `SelfResSpell`, `PvpMedals` | matching player fields | direct mappings |
| `BuybackPrices[]`, `BuybackTimestamps[]` | matching `PLAYER_FIELD_BUYBACK_*` ranges | direct mappings |
| `SessionKills`, `YesterdayKills`, `LastWeekKills`, `ThisWeekContribution`, `LastWeekContribution`, `LifetimeHonorableKills`, `LifetimeDishonorableKills` | matching honor/stat fields where still present in 1.12.1 model | `LifetimeDishonorableKills` remains a known enum/version collision area |
| `WatchedFactionIndex` | `PLAYER_FIELD_WATCHED_FACTION_INDEX` | direct mapping |
| `CombatRating[]` | `PLAYER_FIELD_COMBAT_RATING_1 .. +19` | kept for enum compatibility even though TBC-era meaning bleeds in |
| `ChosenTitle`, `KnownTitles` | matching title fields | direct mapping |
| `ModHealingDonePos`, `ModTargetResistance` | matching player modifier fields | direct mappings |
| `OffhandCritPercentage`, `SpellCritPercentage[]` | matching crit percentage fields | direct mappings |
| `ModManaRegen`, `ModManaRegenInterrupt`, `MaxLevel` | matching player regen/max-level fields | direct mappings |
| `DailyQuests[]` | `PLAYER_FIELD_DAILY_QUESTS_1 .. +9` | kept for enum compatibility |
| `MapId` | world-entry / transfer packets, not player descriptors | runtime world-session state |
| `Race`, `Class`, `Gender` | unpacked from `UNIT_FIELD_BYTES_0` in the inherited unit range | derived convenience fields |

Intentional omissions:

- TBC-only arena-team / currency fields are explicitly ignored in `ApplyPlayerFieldDiffs` because they are outside Vanilla 1.12.1 bot logic.
- Inventory item-object hydration from slot GUIDs is best-effort and still called out as incomplete in the parity plan / `MEMORY.md`.

## `WoWLocalPlayer`

`WoWLocalPlayer` adds local-only convenience/runtime state on top of the `WoWPlayer`
descriptor map:

| Managed field/group | Source | Notes |
| --- | --- | --- |
| `CorpsePosition` | `SMSG_CORPSE_QUERY` | runtime packet state, not a descriptor |
| `ComboPoints` | combo-point packets / local state | runtime convenience |
| `IsAutoAttacking` | attack-start/stop packet flow | runtime combat state |
| `CanRiposte` | spell-state tracking | runtime-only |
| `MainhandIsEnchanted` | item/enchant runtime lookup | runtime-only |
| `TastyCorpsesNearby` | local object query | runtime-only |
| `CorpseRecoveryDelaySeconds` | death/ghost flow timing | runtime-only |
| `Copper` | derived from `Coinage` | computed property |
| `InGhostForm`, `CanResurrect`, `InBattleground`, `HasQuestTargets`, stance/debuff helpers | derived from descriptor fields plus runtime state | computed properties, not extra storage |

## `WoWLocalPet`

`WoWLocalPet` does not currently introduce new descriptor-backed state beyond
`WoWUnit`. It is a runtime wrapper that exposes local pet commands after the bot
promotes a `WoWUnit` whose `SummonedBy` matches the local player.

Intentional omission:

- The promotion decision itself is runtime policy. P2.4 still needs binary evidence for
  whether WoW.exe has separate add/update promotion points for `CGPet_C`.

## Current gaps that remain real

- `CGPet_C`-specific layout is still not proven in `cgobject_layout.md`.
- The exact WoW.exe fields behind threat-table and spell-cast queue state remain unresolved.
- Local-player inventory hydration is still incomplete because the slot GUID snapshot is not
  yet sourced with full WoW.exe parity.
