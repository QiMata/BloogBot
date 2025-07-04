/**
 * MaNGOS is a full featured server for World of Warcraft, supporting
 * the following clients: 1.12.x, 2.4.3, 3.3.5a, 4.3.4a and 5.4.8
 *
 * Copyright (C) 2005-2025 MaNGOS <https://www.getmangos.eu>
 *
 * This program is free software; you can redistribute it and/or modify
 * it under the terms of the GNU General Public License as published by
 * the Free Software Foundation; either version 2 of the License, or
 * (at your option) any later version.
 *
 * This program is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 * GNU General Public License for more details.
 *
 * You should have received a copy of the GNU General Public License
 * along with this program; if not, write to the Free Software
 * Foundation, Inc., 59 Temple Place, Suite 330, Boston, MA  02111-1307  USA
 *
 * World of Warcraft, and all World of Warcraft or Warcraft art, images,
 * and lore are copyrighted by Blizzard Entertainment, Inc.
 */

#ifndef MANGOS_SHAREDDEFINES_H
#define MANGOS_SHAREDDEFINES_H

#ifndef MANGOS
#define MANGOS
#endif /* MANGOS */

enum Gender
{
    GENDER_MALE = 0,
    GENDER_FEMALE = 1,
    GENDER_NONE = 2
};

#define MAX_GENDER                       3

// Race value is index in ChrRaces.dbc
enum Races
{
    RACE_HUMAN = 1,
    RACE_ORC = 2,
    RACE_DWARF = 3,
    RACE_NIGHTELF = 4,
    RACE_UNDEAD = 5,
    RACE_TAUREN = 6,
    RACE_GNOME = 7,
    RACE_TROLL = 8,
    RACE_GOBLIN = 9,
};

// max+1 for player race
#define MAX_RACES         9

#define RACEMASK_ALL_PLAYABLE \
    ((1<<(RACE_HUMAN-1))    |(1<<(RACE_ORC-1))      |(1<<(RACE_DWARF-1))   | \
     (1<<(RACE_NIGHTELF-1))  |(1<<(RACE_UNDEAD-1))   |(1<<(RACE_TAUREN-1))  | \
     (1<<(RACE_GNOME-1))     |(1<<(RACE_TROLL-1)))

// for most cases batter use ChrRace data for team check as more safe, but when need full mask of team can be use this defines.
#define RACEMASK_ALLIANCE \
    ((1<<(RACE_HUMAN-1))    |(1<<(RACE_DWARF-1))    |(1<<(RACE_NIGHTELF-1))| \
     (1<<(RACE_GNOME-1)))

#define RACEMASK_HORDE \
    ((1<<(RACE_ORC-1))      |(1<<(RACE_UNDEAD-1))   |(1<<(RACE_TAUREN-1))  | \
     (1<<(RACE_TROLL-1)))

// Class value is index in ChrClasses.dbc
enum Classes
{
    CLASS_WARRIOR = 1,
    CLASS_PALADIN = 2,
    CLASS_HUNTER = 3,
    CLASS_ROGUE = 4,
    CLASS_PRIEST = 5,
    // CLASS_DEATH_KNIGHT  = 6,                             // not listed in DBC, will be in 3.0
    CLASS_SHAMAN = 7,
    CLASS_MAGE = 8,
    CLASS_WARLOCK = 9,
    // CLASS_UNK2       = 10,unused
    CLASS_DRUID = 11,
};

// max+1 for player class
#define MAX_CLASSES       12

#define CLASSMASK_ALL_PLAYABLE \
    ((1<<(CLASS_WARRIOR-1))|(1<<(CLASS_PALADIN-1))|(1<<(CLASS_HUNTER-1))| \
     (1<<(CLASS_ROGUE-1))  |(1<<(CLASS_PRIEST-1)) |(1<<(CLASS_SHAMAN-1))| \
     (1<<(CLASS_MAGE-1))   |(1<<(CLASS_WARLOCK-1))|(1<<(CLASS_DRUID-1))   )

#define CLASSMASK_ALL_CREATURES ((1<<(CLASS_WARRIOR-1)) | (1<<(CLASS_PALADIN-1)) | (1<<(CLASS_MAGE-1)) )
#define MAX_CREATURE_CLASS 3

// array index could be used to store class data only Warrior, Paladin and Mage are indexed for creature
//                                                  W  P                 M
static const uint8_t classToIndex[MAX_CLASSES] = { 0, 0, 1, 0, 0, 0, 0, 0, 2, 0, 0, 0 };

#define CLASSMASK_WAND_USERS ((1<<(CLASS_PRIEST-1))|(1<<(CLASS_MAGE-1))|(1<<(CLASS_WARLOCK-1)))

#define PLAYER_MAX_BATTLEGROUND_QUEUES 3

#define HONOR_STANDING_MIN_KILL 15

enum ReputationRank
{
    REP_HATED = 0,
    REP_HOSTILE = 1,
    REP_UNFRIENDLY = 2,
    REP_NEUTRAL = 3,
    REP_FRIENDLY = 4,
    REP_HONORED = 5,
    REP_REVERED = 6,
    REP_EXALTED = 7
};

#define MIN_REPUTATION_RANK (REP_HATED)
#define MAX_REPUTATION_RANK 8

#define MAX_SPILLOVER_FACTIONS 4

enum MoneyConstants
{
    COPPER = 1,
    SILVER = COPPER * 100,
    GOLD = SILVER * 100
};

enum Stats
{
    STAT_STRENGTH = 0,
    STAT_AGILITY = 1,
    STAT_STAMINA = 2,
    STAT_INTELLECT = 3,
    STAT_SPIRIT = 4
};

#define MAX_STATS                        5

/**
 * These are the different possible powers that are available to us, they should
 * be fairly familiar if you've played WoW.
 */
enum Powers
{
    POWER_MANA = 0,         ///< The most common one, mobs usually have this or rage
    POWER_RAGE = 1,         ///< This is what warriors use to cast their spells
    POWER_FOCUS = 2,         ///< Used by hunters after Cataclysm (4.x)
    POWER_ENERGY = 3,         ///< Used by rouges to do their spells
    POWER_HAPPINESS = 4,         ///< Hunter's pet's happiness affect their damage
    MAX_POWERS = 5,
    POWER_ALL = 127,          // default for class? - need check for TBC
    POWER_HEALTH = 0xFFFFFFFE ///< Health, everyone has this (-2 as signed value)
};

#define MAX_POWERS                        5

/**
 * The different spell schools that are available, used in both damage calculation
 * and spell casting to decide what should be affected, the \ref SpellSchools::SPELL_SCHOOL_NORMAL
 * is the armor, others should be self explanatory.
 *
 * Note that these are the values to use for changing ie, the armor via a
 * \ref Modifier, and it is the \ref Modifier::m_miscValue that should be set.
 */
enum SpellSchools
{
    /// Physical, Armor
    SPELL_SCHOOL_NORMAL = 0,
    SPELL_SCHOOL_HOLY = 1,
    SPELL_SCHOOL_FIRE = 2,
    SPELL_SCHOOL_NATURE = 3,
    SPELL_SCHOOL_FROST = 4,
    SPELL_SCHOOL_SHADOW = 5,
    SPELL_SCHOOL_ARCANE = 6
};

#define MAX_SPELL_SCHOOL                  7

/**
 * A bitmask of the available SpellSchools. Used for convenience
 */
enum SpellSchoolMask
{
    /// not exist
    SPELL_SCHOOL_MASK_NONE = 0x00,
    /// PHYSICAL (Armor)
    SPELL_SCHOOL_MASK_NORMAL = (1 << SPELL_SCHOOL_NORMAL),
    SPELL_SCHOOL_MASK_HOLY = (1 << SPELL_SCHOOL_HOLY),
    SPELL_SCHOOL_MASK_FIRE = (1 << SPELL_SCHOOL_FIRE),
    SPELL_SCHOOL_MASK_NATURE = (1 << SPELL_SCHOOL_NATURE),
    SPELL_SCHOOL_MASK_FROST = (1 << SPELL_SCHOOL_FROST),
    SPELL_SCHOOL_MASK_SHADOW = (1 << SPELL_SCHOOL_SHADOW),
    SPELL_SCHOOL_MASK_ARCANE = (1 << SPELL_SCHOOL_ARCANE),

    // unions

    /// 124, not include normal and holy damage
    SPELL_SCHOOL_MASK_SPELL = (SPELL_SCHOOL_MASK_FIRE |
        SPELL_SCHOOL_MASK_NATURE | SPELL_SCHOOL_MASK_FROST |
        SPELL_SCHOOL_MASK_SHADOW | SPELL_SCHOOL_MASK_ARCANE),
    /// 126
    SPELL_SCHOOL_MASK_MAGIC = (SPELL_SCHOOL_MASK_HOLY | SPELL_SCHOOL_MASK_SPELL),

    /// 127
    SPELL_SCHOOL_MASK_ALL = (SPELL_SCHOOL_MASK_NORMAL | SPELL_SCHOOL_MASK_MAGIC)
};

#define SPELL_SCHOOL_MASK_MAGIC                            \
    ( SPELL_SCHOOL_MASK_HOLY | SPELL_SCHOOL_MASK_FIRE | SPELL_SCHOOL_MASK_NATURE |  \
      SPELL_SCHOOL_MASK_FROST | SPELL_SCHOOL_MASK_SHADOW | \
      SPELL_SCHOOL_MASK_ARCANE )

/**
 * Converts a \ref SpellSchools value into a bitmask representation since this is missing
 * in the 1.12 dbc files.
 * @param school The school that should be converted to a bitmask, see \ref SpellSchools
 * @return A bitmask representation of the given \ref SpellSchools
 * \see SpellSchools
 */
inline SpellSchoolMask GetSchoolMask(unsigned int school)
{
    return SpellSchoolMask(1 << school);
}

/**
 * Turns a \ref SpellSchoolMask into a \ref SpellSchools from the first bit
 * that is set in the mask.
 * @param mask the mask you want to get the first school for
 * @return a \ref SpellSchools of the first bit that was set, if none were found
 * \ref SpellSchools::SPELL_SCHOOL_NORMAL is returned
 */
inline SpellSchools GetFirstSchoolInMask(SpellSchoolMask mask)
{
    for (int i = 0; i < MAX_SPELL_SCHOOL; ++i)
        if (mask & (1 << i))
        {
            return SpellSchools(i);
        }

    return SPELL_SCHOOL_NORMAL;
}

enum ItemQualities
{
    ITEM_QUALITY_POOR = 0,                 // GREY
    ITEM_QUALITY_NORMAL = 1,                 // WHITE
    ITEM_QUALITY_UNCOMMON = 2,                 // GREEN
    ITEM_QUALITY_RARE = 3,                 // BLUE
    ITEM_QUALITY_EPIC = 4,                 // PURPLE
    ITEM_QUALITY_LEGENDARY = 5,                 // ORANGE
    ITEM_QUALITY_ARTIFACT = 6                  // LIGHT YELLOW
};

#define MAX_ITEM_QUALITY                 7

const unsigned int ItemQualityColors[MAX_ITEM_QUALITY] =
{
    0xff9d9d9d,        // GREY
    0xffffffff,        // WHITE
    0xff1eff00,        // GREEN
    0xff0070dd,        // BLUE
    0xffa335ee,        // PURPLE
    0xffff8000,        // ORANGE
    0xffe6cc80         // LIGHT YELLOW
};

// ***********************************
// Spell Attributes definitions
// ***********************************

enum SpellAttributes
{
    SPELL_ATTR_UNK0 = 0x00000001,            // 0
    SPELL_ATTR_RANGED = 0x00000002,            // 1 All ranged abilites have this flag
    SPELL_ATTR_ON_NEXT_SWING_1 = 0x00000004,            // 2 on next swing
    SPELL_ATTR_UNK3 = 0x00000008,            // 3 not set in 2.4.2
    SPELL_ATTR_ABILITY = 0x00000010,            // 4 Displays ability instead of spell clientside
    SPELL_ATTR_TRADESPELL = 0x00000020,            // 5 trade spells, will be added by client to a sublist of profession spell
    SPELL_ATTR_PASSIVE = 0x00000040,            // 6 Passive spell
    SPELL_ATTR_HIDDEN_CLIENTSIDE = 0x00000080,            // 7 Spells with this attribute are not visible in spellbook or aura bar TODO: check usage
    SPELL_ATTR_HIDE_IN_COMBAT_LOG = 0x00000100,            // 8 hide created item in tooltip (for effect=24) TODO: implement it
    SPELL_ATTR_TARGET_MAINHAND_ITEM = 0x00000200,            // 9 Client automatically selects item from mainhand slot as a cast target TODO: Implement
    SPELL_ATTR_ON_NEXT_SWING_2 = 0x00000400,            // 10 on next swing 2
    SPELL_ATTR_UNK11 = 0x00000800,            // 11
    SPELL_ATTR_DAYTIME_ONLY = 0x00001000,            // 12 only useable at daytime, not set in 2.4.2
    SPELL_ATTR_NIGHT_ONLY = 0x00002000,            // 13 only useable at night, not set in 2.4.2
    SPELL_ATTR_INDOORS_ONLY = 0x00004000,            // 14 only useable indoors, not set in 2.4.2
    SPELL_ATTR_OUTDOORS_ONLY = 0x00008000,            // 15 Only useable outdoors.
    SPELL_ATTR_NOT_SHAPESHIFT = 0x00010000,            // 16 Not while shapeshifted
    SPELL_ATTR_ONLY_STEALTHED = 0x00020000,            // 17 Must be in stealth
    SPELL_ATTR_DONT_AFFECT_SHEATH_STATE = 0x00040000,            // 18 client won't hide unit weapons in sheath on cast/channel TODO: Implement
    SPELL_ATTR_LEVEL_DAMAGE_CALCULATION = 0x00080000,            // 19 spelldamage depends on caster level
    SPELL_ATTR_STOP_ATTACK_TARGET = 0x00100000,            // 20 Stop attack after use this spell (and not begin attack if use)
    SPELL_ATTR_IMPOSSIBLE_DODGE_PARRY_BLOCK = 0x00200000,            // 21 Can not be dodged/parried/blocked
    SPELL_ATTR_SET_TRACKING_TARGET = 0x00400000,            // 22 SetTrackingTarget
    SPELL_ATTR_CASTABLE_WHILE_DEAD = 0x00800000,            // 23 castable while dead - TODO: Implement
    SPELL_ATTR_CASTABLE_WHILE_MOUNTED = 0x01000000,            // 24 castable while mounted
    SPELL_ATTR_DISABLED_WHILE_ACTIVE = 0x02000000,            // 25 Activate and start cooldown after aura fade or remove summoned creature or go
    SPELL_ATTR_AURA_IS_DEBUFF = 0x04000000,            // 26
    SPELL_ATTR_CASTABLE_WHILE_SITTING = 0x08000000,            // 27 castable while sitting
    SPELL_ATTR_CANT_USED_IN_COMBAT = 0x10000000,            // 28 Can not be used in combat
    SPELL_ATTR_UNAFFECTED_BY_INVULNERABILITY = 0x20000000,            // 29 unaffected by invulnerability (hmm possible not...)
    SPELL_ATTR_HEARTBEAT_RESIST_CHECK = 0x40000000,            // 30 TC 335: random chance the effect will end (subjected to the hearbeat resist)
    SPELL_ATTR_CANT_CANCEL = 0x80000000             // 31 positive aura can't be canceled
};

enum SpellAttributesEx
{
    SPELL_ATTR_EX_UNK0 = 0x00000001,            // 0
    SPELL_ATTR_EX_DRAIN_ALL_POWER = 0x00000002,            // 1 use all power (Only paladin Lay of Hands and Bunyanize)
    SPELL_ATTR_EX_CHANNELED_1 = 0x00000004,            // 2 channeled 1
    SPELL_ATTR_EX_CANT_BE_REDIRECTED = 0x00000008,            // 3
    SPELL_ATTR_EX_UNK4 = 0x00000010,            // 4
    SPELL_ATTR_EX_NOT_BREAK_STEALTH = 0x00000020,            // 5 Not break stealth
    SPELL_ATTR_EX_CHANNELED_2 = 0x00000040,            // 6 channeled 2
    SPELL_ATTR_EX_CANT_BE_REFLECTED = 0x00000080,            // 7
    SPELL_ATTR_EX_NOT_IN_COMBAT_TARGET = 0x00000100,            // 8 Spell req target not to be in combat state
    SPELL_ATTR_EX_FACING_TARGET = 0x00000200,            // 9 TODO: CONFIRM!
    SPELL_ATTR_EX_NO_THREAT = 0x00000400,            // 10 no generates threat on cast 100%
    SPELL_ATTR_EX_UNK11 = 0x00000800,            // 11
    SPELL_ATTR_EX_IS_PICKPOCKET = 0x00001000,            // 12
    SPELL_ATTR_EX_FARSIGHT = 0x00002000,            // 13 related to farsight
    SPELL_ATTR_EX_CHANNEL_TRACK_TARGET = 0x00004000,            // 14
    SPELL_ATTR_EX_DISPEL_AURAS_ON_IMMUNITY = 0x00008000,            // 15 remove auras on immunity
    SPELL_ATTR_EX_UNAFFECTED_BY_SCHOOL_IMMUNE = 0x00010000,            // 16 unaffected by school immunity
    SPELL_ATTR_EX_UNAUTOCASTABLE_BY_CHARMED = 0x00020000,            // 17 TODO: Investigate more: SPELL_ATTR_EX_PLAYER_CANT_CAST_CHARMED, likely related to MC ,for auras SPELL_AURA_TRACK_CREATURES, SPELL_AURA_TRACK_RESOURCES and SPELL_AURA_TRACK_STEALTHED select non-stacking tracking spells
    SPELL_ATTR_EX_UNK18 = 0x00040000,            // 18
    SPELL_ATTR_EX_CANT_TARGET_SELF = 0x00080000,            // 19 spells that exclude the caster
    SPELL_ATTR_EX_REQ_TARGET_COMBO_POINTS = 0x00100000,            // 20 Req combo points on target
    SPELL_ATTR_EX_UNK21 = 0x00200000,            // 21
    SPELL_ATTR_EX_REQ_COMBO_POINTS = 0x00400000,            // 22 Use combo points (in 4.x not required combo point target selected)
    SPELL_ATTR_EX_UNK23 = 0x00800000,            // 23
    SPELL_ATTR_EX_UNK24 = 0x01000000,            // 24 Req fishing pole??
    SPELL_ATTR_EX_UNK25 = 0x02000000,            // 25 not set in 2.4.2
    SPELL_ATTR_EX_UNK26 = 0x04000000,            // 26
    SPELL_ATTR_EX_REFUND_POWER = 0x08000000,            // 27 All these spells refund power on parry or deflect
    SPELL_ATTR_EX_DONT_DISPLAY_IN_AURA_BAR = 0x10000000,            // 28
    SPELL_ATTR_EX_CHANNEL_DISPLAY_SPELL_NAME = 0x20000000,            // 29
    SPELL_ATTR_EX_ENABLE_AT_DODGE = 0x40000000,            // 30 overpower
    SPELL_ATTR_EX_UNK31 = 0x80000000,            // 31
};

enum SpellAttributesEx2
{
    SPELL_ATTR_EX2_CAN_TARGET_DEAD = 0x00000001,            // 0 can target dead unit or corpse
    SPELL_ATTR_EX2_UNK1 = 0x00000002,            // 1
    SPELL_ATTR_EX2_IGNORE_LOS = 0x00000004,            // 2 ? used for detect can or not spell reflected // do not need LOS (e.g. 18220 since 3.3.3)
    SPELL_ATTR_EX2_UNK3 = 0x00000008,            // 3 auto targeting? (e.g. fishing skill enhancement items since 3.3.3)
    SPELL_ATTR_EX2_DISPLAY_IN_STANCE_BAR = 0x00000010,            // 4
    SPELL_ATTR_EX2_AUTOREPEAT_FLAG = 0x00000020,            // 5
    SPELL_ATTR_EX2_CANT_TARGET_TAPPED = 0x00000040,            // 6 only usable on tabbed by yourself
    SPELL_ATTR_EX2_UNK7 = 0x00000080,            // 7
    SPELL_ATTR_EX2_UNK8 = 0x00000100,            // 8 not set in 2.4.2
    SPELL_ATTR_EX2_UNK9 = 0x00000200,            // 9
    SPELL_ATTR_EX2_UNK10 = 0x00000400,            // 10
    SPELL_ATTR_EX2_HEALTH_FUNNEL = 0x00000800,            // 11
    SPELL_ATTR_EX2_UNK12 = 0x00001000,            // 12
    SPELL_ATTR_EX2_UNK13 = 0x00002000,            // 13
    SPELL_ATTR_EX2_UNK14 = 0x00004000,            // 14
    SPELL_ATTR_EX2_UNK15 = 0x00008000,            // 15 not set in 2.4.2
    SPELL_ATTR_EX2_TAME_BEAST = 0x00010000,            // 16
    SPELL_ATTR_EX2_NOT_RESET_AUTO_ACTIONS = 0x00020000,            // 17 suspend weapon timer instead of resetting it, (?Hunters Shot and Stings only have this flag?)
    SPELL_ATTR_EX2_REQ_DEAD_PET = 0x00040000,            // 18 Only Revive pet - possible req dead pet
    SPELL_ATTR_EX2_NOT_NEED_SHAPESHIFT = 0x00080000,            // 19 does not necessary need shapeshift (pre-3.x not have passive spells with this attribute)
    SPELL_ATTR_EX2_FACING_TARGETS_BACK = 0x00100000,            // 20 TODO: CONFIRM!
    SPELL_ATTR_EX2_DAMAGE_REDUCED_SHIELD = 0x00200000,            // 21 for ice blocks, pala immunity buffs, priest absorb shields, but used also for other spells -> not sure!
    SPELL_ATTR_EX2_UNK22 = 0x00400000,            // 22
    SPELL_ATTR_EX2_IS_ARCANE_CONCENTRATION = 0x00800000,            // 23 Only mage Arcane Concentration have this flag
    SPELL_ATTR_EX2_UNK24 = 0x01000000,            // 24
    SPELL_ATTR_EX2_UNK25 = 0x02000000,            // 25
    SPELL_ATTR_EX2_UNK26 = 0x04000000,            // 26 unaffected by school immunity
    SPELL_ATTR_EX2_UNK27 = 0x08000000,            // 27
    SPELL_ATTR_EX2_UNK28 = 0x10000000,            // 28 no breaks stealth if it fails??
    SPELL_ATTR_EX2_CANT_CRIT = 0x20000000,            // 29 Spell can't crit
    SPELL_ATTR_EX2_TRIGGERED_CAN_TRIGGER_PROC = 0x40000000,            // 30
    SPELL_ATTR_EX2_FOOD_BUFF = 0x80000000,            // 31 Food or Drink Buff (like Well Fed)
};

enum SpellAttributesEx3
{
    SPELL_ATTR_EX3_OUT_OF_COMBAT_ATTACK = 0x00000001,            // 0 Spell landed counts as hostile action against enemy even if it doesn't trigger combat state, propagates PvP flags
    SPELL_ATTR_EX3_UNK1 = 0x00000002,            // 1
    SPELL_ATTR_EX3_UNK2 = 0x00000004,            // 2
    SPELL_ATTR_EX3_BLOCKABLE_SPELL = 0x00000008,            // 3
    SPELL_ATTR_EX3_IGNORE_RESURRECTION_TIMER = 0x00000010,            // 4 Druid Rebirth only this spell have this flag
    SPELL_ATTR_EX3_UNK5 = 0x00000020,            // 5
    SPELL_ATTR_EX3_UNK6 = 0x00000040,            // 6
    SPELL_ATTR_EX3_STACK_FOR_DIFF_CASTERS = 0x00000080,            // 7 create a separate (de)buff stack for each caster
    SPELL_ATTR_EX3_TARGET_ONLY_PLAYER = 0x00000100,            // 8 Can target only player
    SPELL_ATTR_EX3_TRIGGERED_CAN_TRIGGER_SPECIAL = 0x00000200,            // 9 Can only proc auras with SPELL_ATTR_EX3_CAN_PROC_FROM_TRIGGERED_SPECIAL
    SPELL_ATTR_EX3_MAIN_HAND = 0x00000400,            // 10 Main hand weapon required
    SPELL_ATTR_EX3_BATTLEGROUND = 0x00000800,            // 11 Can casted only on battleground
    SPELL_ATTR_EX3_CAST_ON_DEAD = 0x00001000,            // 12 target is a dead player (not every spell has this flag)
    SPELL_ATTR_EX3_DONT_DISPLAY_CHANNEL_BAR = 0x00002000,            // 13
    SPELL_ATTR_EX3_IS_HONORLESS_TARGET = 0x00004000,            // 14 "Honorless Target" only this spells have this flag
    SPELL_ATTR_EX3_RANGED_ATTACK = 0x00008000,            // 15 Auto Shoot, Shoot, Throw,  - this is autoshot flag
    SPELL_ATTR_EX3_CANT_TRIGGER_PROC = 0x00010000,            // 16 confirmed by patchnotes
    SPELL_ATTR_EX3_NO_INITIAL_AGGRO = 0x00020000,            // 17 Causes no aggro if not missed
    SPELL_ATTR_EX3_CANT_MISS = 0x00040000,            // 18 Spell should always hit its target
    SPELL_ATTR_EX3_UNK19 = 0x00080000,            // 19
    SPELL_ATTR_EX3_DEATH_PERSISTENT = 0x00100000,            // 20 Death persistent spells
    SPELL_ATTR_EX3_UNK21 = 0x00200000,            // 21
    SPELL_ATTR_EX3_REQ_WAND = 0x00400000,            // 22 Req wand
    SPELL_ATTR_EX3_UNK23 = 0x00800000,            // 23
    SPELL_ATTR_EX3_REQ_OFFHAND = 0x01000000,            // 24 Req offhand weapon
    SPELL_ATTR_EX3_UNK25 = 0x02000000,            // 25 no cause spell pushback ?
    SPELL_ATTR_EX3_CAN_PROC_FROM_TRIGGERED_SPECIAL = 0x04000000,            // 26 Auras with this attribute can proc off SPELL_ATTR_EX3_TRIGGERED_CAN_TRIGGER_SPECIAL
    SPELL_ATTR_EX3_DRAIN_SOUL = 0x08000000,            // 27
    SPELL_ATTR_EX3_UNK28 = 0x10000000,            // 28 always cast ok ? (requires more research)
    SPELL_ATTR_EX3_NO_DONE_BONUS = 0x20000000,            // 29 Resistances should still affect damage
    SPELL_ATTR_EX3_DONT_DISPLAY_RANGE = 0x40000000,            // 30
    SPELL_ATTR_EX3_UNK31 = 0x80000000             // 31
};

enum SpellAttributesEx4
{
    SPELL_ATTR_EX4_IGNORE_RESISTANCES = 0x00000001,            // 0
    SPELL_ATTR_EX4_PROC_ONLY_ON_CASTER = 0x00000002,            // 1  Only proc on self-cast
    SPELL_ATTR_EX4_UNK2 = 0x00000004,            // 2
    SPELL_ATTR_EX4_UNK3 = 0x00000008,            // 3
    SPELL_ATTR_EX4_UNK4 = 0x00000010,            // 4 This will no longer cause guards to attack on use??
    SPELL_ATTR_EX4_UNK5 = 0x00000020,            // 5
    SPELL_ATTR_EX4_NOT_STEALABLE = 0x00000040,            // 6 although such auras might be dispellable, they can not be stolen
    SPELL_ATTR_EX4_CAN_CAST_WHILE_CASTING = 0x00000080,            // 7 In theory, can use this spell while another is channeled/cast/autocast
    SPELL_ATTR_EX4_STACK_DOT_MODIFIER = 0x00000100,            // 8 no effect on non DoTs?
    SPELL_ATTR_EX4_TRIGGER_ACTIVATE = 0x00000200,            // 9 initially disabled / trigger activate from event (Execute, Riposte, Deep Freeze end other)
    SPELL_ATTR_EX4_SPELL_VS_EXTEND_COST = 0x00000400,            // 10 Rogue Shiv have this flag
    SPELL_ATTR_EX4_UNK11 = 0x00000800,            // 11
    SPELL_ATTR_EX4_UNK12 = 0x00001000,            // 12
    SPELL_ATTR_EX4_UNK13 = 0x00002000,            // 13
    SPELL_ATTR_EX4_DAMAGE_DOESNT_BREAK_AURAS = 0x00004000,            // 14
    SPELL_ATTR_EX4_UNK15 = 0x00008000,            // 15
    SPELL_ATTR_EX4_NOT_USABLE_IN_ARENA = 0x00010000,            // 16 not usable in arena
    SPELL_ATTR_EX4_USABLE_IN_ARENA = 0x00020000,            // 17 usable in arena
    SPELL_ATTR_EX4_UNK18 = 0x00040000,            // 18
    SPELL_ATTR_EX4_UNK19 = 0x00080000,            // 19
    SPELL_ATTR_EX4_NOT_CHECK_SELFCAST_POWER = 0x00100000,            // 20 do not give "more powerful spell" error message
    SPELL_ATTR_EX4_UNK21 = 0x00200000,            // 21
    SPELL_ATTR_EX4_UNK22 = 0x00400000,            // 22
    SPELL_ATTR_EX4_UNK23 = 0x00800000,            // 23
    SPELL_ATTR_EX4_UNK24 = 0x01000000,            // 24
    SPELL_ATTR_EX4_IS_PET_SCALING = 0x02000000,            // 25 pet scaling auras
    SPELL_ATTR_EX4_CAST_ONLY_IN_OUTLAND = 0x04000000,            // 26 Can only be used in Outland.
    SPELL_ATTR_EX4_UNK27 = 0x08000000,            // 27
    SPELL_ATTR_EX4_UNK28 = 0x10000000,            // 28
    SPELL_ATTR_EX4_UNK29 = 0x20000000,            // 29
    SPELL_ATTR_EX4_UNK30 = 0x40000000,            // 30
    SPELL_ATTR_EX4_UNK31 = 0x80000000             // 31
};

enum SheathTypes
{
    SHEATHETYPE_NONE = 0,
    SHEATHETYPE_MAINHAND = 1,
    SHEATHETYPE_OFFHAND = 2,
    SHEATHETYPE_LARGEWEAPONLEFT = 3,
    SHEATHETYPE_LARGEWEAPONRIGHT = 4,
    SHEATHETYPE_HIPWEAPONLEFT = 5,
    SHEATHETYPE_HIPWEAPONRIGHT = 6,
    SHEATHETYPE_SHIELD = 7
};

#define MAX_SHEATHETYPE                  8

enum CharacterSlot
{
    SLOT_HEAD = 0,
    SLOT_NECK = 1,
    SLOT_SHOULDERS = 2,
    SLOT_SHIRT = 3,
    SLOT_CHEST = 4,
    SLOT_WAIST = 5,
    SLOT_LEGS = 6,
    SLOT_FEET = 7,
    SLOT_WRISTS = 8,
    SLOT_HANDS = 9,
    SLOT_FINGER1 = 10,
    SLOT_FINGER2 = 11,
    SLOT_TRINKET1 = 12,
    SLOT_TRINKET2 = 13,
    SLOT_BACK = 14,
    SLOT_MAIN_HAND = 15,
    SLOT_OFF_HAND = 16,
    SLOT_RANGED = 17,
    SLOT_TABARD = 18,
    SLOT_EMPTY = 19
};

enum Language
{
    LANG_UNIVERSAL = 0,
    LANG_ORCISH = 1,
    LANG_DARNASSIAN = 2,
    LANG_TAURAHE = 3,
    LANG_DWARVISH = 6,
    LANG_COMMON = 7,
    LANG_DEMONIC = 8,
    LANG_TITAN = 9,
    LANG_THALASSIAN = 10,
    LANG_DRACONIC = 11,
    LANG_KALIMAG = 12,
    LANG_GNOMISH = 13,
    LANG_TROLL = 14,
    LANG_GUTTERSPEAK = 33,
    LANG_ADDON = 0xFFFFFFFF                        // used by addons, in 2.4.0 not exit, replaced by messagetype?
};

#define LANGUAGES_COUNT   15

// In fact !=0 values is alliance/horde root faction ids
enum Team
{
    TEAM_NONE = 0,                                // used when team value unknown or not set, 0 is also meaning that can be used !team check
    TEAM_BOTH_ALLOWED = 0,                                // used when a check should evaluate true for both teams
    TEAM_INVALID = 1,                                // used to invalidate some team depending checks (means not for both teams)
    HORDE = 67,
    ALLIANCE = 469,
};

enum PvpTeamIndex
{
    TEAM_INDEX_ALLIANCE = 0,
    TEAM_INDEX_HORDE = 1,
    TEAM_INDEX_NEUTRAL = 2,
};

#define PVP_TEAM_COUNT    2

/**
 * This are the different things that a spell can have as it's spell effect, see
 * \ref SpellEntry::Effect for where in the DBC this is stored. Also see \ref HowSpellsWork
 */
enum SpellEffects
{
    SPELL_EFFECT_NONE = 0,
    SPELL_EFFECT_INSTAKILL = 1,
    SPELL_EFFECT_SCHOOL_DAMAGE = 2,
    SPELL_EFFECT_DUMMY = 3,
    SPELL_EFFECT_PORTAL_TELEPORT = 4,
    SPELL_EFFECT_TELEPORT_UNITS = 5,
    SPELL_EFFECT_APPLY_AURA = 6,
    SPELL_EFFECT_ENVIRONMENTAL_DAMAGE = 7,
    SPELL_EFFECT_POWER_DRAIN = 8,
    SPELL_EFFECT_HEALTH_LEECH = 9,
    SPELL_EFFECT_HEAL = 10,
    SPELL_EFFECT_BIND = 11,
    SPELL_EFFECT_PORTAL = 12,
    SPELL_EFFECT_RITUAL_BASE = 13,
    SPELL_EFFECT_RITUAL_SPECIALIZE = 14,
    SPELL_EFFECT_RITUAL_ACTIVATE_PORTAL = 15,
    SPELL_EFFECT_QUEST_COMPLETE = 16,
    SPELL_EFFECT_WEAPON_DAMAGE_NOSCHOOL = 17,
    SPELL_EFFECT_RESURRECT = 18,
    SPELL_EFFECT_ADD_EXTRA_ATTACKS = 19,
    SPELL_EFFECT_DODGE = 20,
    SPELL_EFFECT_EVADE = 21,
    SPELL_EFFECT_PARRY = 22,
    SPELL_EFFECT_BLOCK = 23,
    SPELL_EFFECT_CREATE_ITEM = 24,
    SPELL_EFFECT_WEAPON = 25,
    SPELL_EFFECT_DEFENSE = 26,
    SPELL_EFFECT_PERSISTENT_AREA_AURA = 27,
    SPELL_EFFECT_SUMMON = 28,
    SPELL_EFFECT_LEAP = 29,
    SPELL_EFFECT_ENERGIZE = 30,
    SPELL_EFFECT_WEAPON_PERCENT_DAMAGE = 31,
    SPELL_EFFECT_TRIGGER_MISSILE = 32,
    SPELL_EFFECT_OPEN_LOCK = 33,
    SPELL_EFFECT_SUMMON_CHANGE_ITEM = 34,
    SPELL_EFFECT_APPLY_AREA_AURA_PARTY = 35,
    SPELL_EFFECT_LEARN_SPELL = 36,
    SPELL_EFFECT_SPELL_DEFENSE = 37,
    SPELL_EFFECT_DISPEL = 38,
    SPELL_EFFECT_LANGUAGE = 39,
    SPELL_EFFECT_DUAL_WIELD = 40,
    SPELL_EFFECT_SUMMON_WILD = 41,
    SPELL_EFFECT_SUMMON_GUARDIAN = 42,
    SPELL_EFFECT_TELEPORT_UNITS_FACE_CASTER = 43,
    SPELL_EFFECT_SKILL_STEP = 44,
    SPELL_EFFECT_ADD_HONOR = 45,
    SPELL_EFFECT_SPAWN = 46,
    SPELL_EFFECT_TRADE_SKILL = 47,
    SPELL_EFFECT_STEALTH = 48,
    SPELL_EFFECT_DETECT = 49,
    SPELL_EFFECT_TRANS_DOOR = 50,
    SPELL_EFFECT_FORCE_CRITICAL_HIT = 51,
    SPELL_EFFECT_GUARANTEE_HIT = 52,
    SPELL_EFFECT_ENCHANT_ITEM = 53,
    SPELL_EFFECT_ENCHANT_ITEM_TEMPORARY = 54,
    SPELL_EFFECT_TAMECREATURE = 55,
    SPELL_EFFECT_SUMMON_PET = 56,
    SPELL_EFFECT_LEARN_PET_SPELL = 57,
    SPELL_EFFECT_WEAPON_DAMAGE = 58,
    SPELL_EFFECT_OPEN_LOCK_ITEM = 59,
    SPELL_EFFECT_PROFICIENCY = 60,
    SPELL_EFFECT_SEND_EVENT = 61,
    SPELL_EFFECT_POWER_BURN = 62,
    SPELL_EFFECT_THREAT = 63,
    SPELL_EFFECT_TRIGGER_SPELL = 64,
    SPELL_EFFECT_HEALTH_FUNNEL = 65,
    SPELL_EFFECT_POWER_FUNNEL = 66,
    SPELL_EFFECT_HEAL_MAX_HEALTH = 67,
    SPELL_EFFECT_INTERRUPT_CAST = 68,
    SPELL_EFFECT_DISTRACT = 69,
    SPELL_EFFECT_PULL = 70,
    SPELL_EFFECT_PICKPOCKET = 71,
    SPELL_EFFECT_ADD_FARSIGHT = 72,
    SPELL_EFFECT_SUMMON_POSSESSED = 73,
    SPELL_EFFECT_SUMMON_TOTEM = 74,
    SPELL_EFFECT_HEAL_MECHANICAL = 75,
    SPELL_EFFECT_SUMMON_OBJECT_WILD = 76,
    SPELL_EFFECT_SCRIPT_EFFECT = 77,
    SPELL_EFFECT_ATTACK = 78,
    SPELL_EFFECT_SANCTUARY = 79,
    SPELL_EFFECT_ADD_COMBO_POINTS = 80,
    SPELL_EFFECT_CREATE_HOUSE = 81,
    SPELL_EFFECT_BIND_SIGHT = 82,
    SPELL_EFFECT_DUEL = 83,
    SPELL_EFFECT_STUCK = 84,
    SPELL_EFFECT_SUMMON_PLAYER = 85,
    SPELL_EFFECT_ACTIVATE_OBJECT = 86,
    SPELL_EFFECT_SUMMON_TOTEM_SLOT1 = 87,
    SPELL_EFFECT_SUMMON_TOTEM_SLOT2 = 88,
    SPELL_EFFECT_SUMMON_TOTEM_SLOT3 = 89,
    SPELL_EFFECT_SUMMON_TOTEM_SLOT4 = 90,
    SPELL_EFFECT_THREAT_ALL = 91,
    SPELL_EFFECT_ENCHANT_HELD_ITEM = 92,
    SPELL_EFFECT_SUMMON_PHANTASM = 93,
    SPELL_EFFECT_SELF_RESURRECT = 94,
    SPELL_EFFECT_SKINNING = 95,
    SPELL_EFFECT_CHARGE = 96,
    SPELL_EFFECT_SUMMON_CRITTER = 97,
    SPELL_EFFECT_KNOCK_BACK = 98,
    SPELL_EFFECT_DISENCHANT = 99,
    SPELL_EFFECT_INEBRIATE = 100,
    SPELL_EFFECT_FEED_PET = 101,
    SPELL_EFFECT_DISMISS_PET = 102,
    SPELL_EFFECT_REPUTATION = 103,
    SPELL_EFFECT_SUMMON_OBJECT_SLOT1 = 104,
    SPELL_EFFECT_SUMMON_OBJECT_SLOT2 = 105,
    SPELL_EFFECT_SUMMON_OBJECT_SLOT3 = 106,
    SPELL_EFFECT_SUMMON_OBJECT_SLOT4 = 107,
    SPELL_EFFECT_DISPEL_MECHANIC = 108,
    SPELL_EFFECT_SUMMON_DEAD_PET = 109,
    SPELL_EFFECT_DESTROY_ALL_TOTEMS = 110,
    SPELL_EFFECT_DURABILITY_DAMAGE = 111,
    SPELL_EFFECT_SUMMON_DEMON = 112,
    SPELL_EFFECT_RESURRECT_NEW = 113,
    SPELL_EFFECT_ATTACK_ME = 114,
    SPELL_EFFECT_DURABILITY_DAMAGE_PCT = 115,
    SPELL_EFFECT_SKIN_PLAYER_CORPSE = 116,
    SPELL_EFFECT_SPIRIT_HEAL = 117,
    SPELL_EFFECT_SKILL = 118,
    SPELL_EFFECT_APPLY_AREA_AURA_PET = 119,
    SPELL_EFFECT_TELEPORT_GRAVEYARD = 120,
    SPELL_EFFECT_NORMALIZED_WEAPON_DMG = 121,
    SPELL_EFFECT_122 = 122,
    SPELL_EFFECT_SEND_TAXI = 123,
    SPELL_EFFECT_PLAYER_PULL = 124,
    SPELL_EFFECT_MODIFY_THREAT_PERCENT = 125,
    SPELL_EFFECT_126 = 126,
    SPELL_EFFECT_127 = 127,
    SPELL_EFFECT_128 = 128,
    SPELL_EFFECT_129 = 129,
    TOTAL_SPELL_EFFECTS = 130
};

enum SpellCastResult
{
    SPELL_FAILED_AFFECTING_COMBAT = 0x00,
    SPELL_FAILED_ALREADY_AT_FULL_HEALTH = 0x01,
    SPELL_FAILED_ALREADY_AT_FULL_MANA = 0x02,
    SPELL_FAILED_ALREADY_BEING_TAMED = 0x03,
    SPELL_FAILED_ALREADY_HAVE_CHARM = 0x04,
    SPELL_FAILED_ALREADY_HAVE_SUMMON = 0x05,
    SPELL_FAILED_ALREADY_OPEN = 0x06,
    SPELL_FAILED_MORE_POWERFUL_SPELL_ACTIVE = 0x07,
    // SPELL_FAILED_AUTOTRACK_INTERRUPTED          = 0x08, old commented CAST_FAIL_FAILED = 8,-> 29
    SPELL_FAILED_BAD_IMPLICIT_TARGETS = 0x09,
    SPELL_FAILED_BAD_TARGETS = 0x0A,
    SPELL_FAILED_CANT_BE_CHARMED = 0x0B,
    SPELL_FAILED_CANT_BE_DISENCHANTED = 0x0C,
    SPELL_FAILED_CANT_BE_PROSPECTED = 0x0D,
    SPELL_FAILED_CANT_CAST_ON_TAPPED = 0x0E,
    SPELL_FAILED_CANT_DUEL_WHILE_INVISIBLE = 0x0F,
    SPELL_FAILED_CANT_DUEL_WHILE_STEALTHED = 0x10,
    SPELL_FAILED_CANT_TOO_CLOSE_TO_ENEMY = 0x11,
    SPELL_FAILED_CANT_DO_THAT_YET = 0x12,
    SPELL_FAILED_CASTER_DEAD = 0x13,
    SPELL_FAILED_CHARMED = 0x14,
    SPELL_FAILED_CHEST_IN_USE = 0x15,
    SPELL_FAILED_CONFUSED = 0x16,
    SPELL_FAILED_DONT_REPORT = 0x17,     // [-ZERO] need check
    SPELL_FAILED_EQUIPPED_ITEM = 0x18,
    SPELL_FAILED_EQUIPPED_ITEM_CLASS = 0x19,
    SPELL_FAILED_EQUIPPED_ITEM_CLASS_MAINHAND = 0x1A,
    SPELL_FAILED_EQUIPPED_ITEM_CLASS_OFFHAND = 0x1B,
    SPELL_FAILED_ERROR = 0x1C,
    SPELL_FAILED_FIZZLE = 0x1D,
    SPELL_FAILED_FLEEING = 0x1E,
    SPELL_FAILED_FOOD_LOWLEVEL = 0x1F,
    SPELL_FAILED_HIGHLEVEL = 0x20,
    // SPELL_FAILED_HUNGER_SATIATED                = 0x21,
    SPELL_FAILED_IMMUNE = 0x22,
    SPELL_FAILED_INTERRUPTED = 0x23,
    SPELL_FAILED_INTERRUPTED_COMBAT = 0x24,
    SPELL_FAILED_ITEM_ALREADY_ENCHANTED = 0x25,
    SPELL_FAILED_ITEM_GONE = 0x26,
    SPELL_FAILED_ENCHANT_NOT_EXISTING_ITEM = 0x27,
    SPELL_FAILED_ITEM_NOT_READY = 0x28,
    SPELL_FAILED_LEVEL_REQUIREMENT = 0x29,
    SPELL_FAILED_LINE_OF_SIGHT = 0x2A,
    SPELL_FAILED_LOWLEVEL = 0x2B,
    SPELL_FAILED_SKILL_NOT_HIGH_ENOUGH = 0x2C,
    SPELL_FAILED_MAINHAND_EMPTY = 0x2D,
    SPELL_FAILED_MOVING = 0x2E,
    SPELL_FAILED_NEED_AMMO = 0x2F,
    SPELL_FAILED_NEED_REQUIRES_SOMETHING = 0x30,
    SPELL_FAILED_NEED_EXOTIC_AMMO = 0x31,
    SPELL_FAILED_NOPATH = 0x32,
    SPELL_FAILED_NOT_BEHIND = 0x33,
    SPELL_FAILED_NOT_FISHABLE = 0x34,
    SPELL_FAILED_NOT_HERE = 0x35,
    SPELL_FAILED_NOT_INFRONT = 0x36,
    SPELL_FAILED_NOT_IN_CONTROL = 0x37,
    SPELL_FAILED_NOT_KNOWN = 0x38,
    SPELL_FAILED_NOT_MOUNTED = 0x39,
    SPELL_FAILED_NOT_ON_TAXI = 0x3A,
    SPELL_FAILED_NOT_ON_TRANSPORT = 0x3B,
    SPELL_FAILED_NOT_READY = 0x3C,
    SPELL_FAILED_NOT_SHAPESHIFT = 0x3D,
    SPELL_FAILED_NOT_STANDING = 0x3E,
    SPELL_FAILED_NOT_TRADEABLE = 0x3F,     // rogues trying "enchant" other's weapon with poison
    SPELL_FAILED_NOT_TRADING = 0x40,     // CAST_FAIL_CANT_ENCHANT_TRADE_ITEM
    SPELL_FAILED_NOT_UNSHEATHED = 0x41,     // yellow text
    SPELL_FAILED_NOT_WHILE_GHOST = 0x42,
    SPELL_FAILED_NO_AMMO = 0x43,
    SPELL_FAILED_NO_CHARGES_REMAIN = 0x44,
    SPELL_FAILED_NO_CHAMPION = 0x45,     // CAST_FAIL_NOT_SELECT
    SPELL_FAILED_NO_COMBO_POINTS = 0x46,
    SPELL_FAILED_NO_DUELING = 0x47,
    SPELL_FAILED_NO_ENDURANCE = 0x48,
    SPELL_FAILED_NO_FISH = 0x49,
    SPELL_FAILED_NO_ITEMS_WHILE_SHAPESHIFTED = 0x4A,
    SPELL_FAILED_NO_MOUNTS_ALLOWED = 0x4B,
    SPELL_FAILED_NO_PET = 0x4C,
    SPELL_FAILED_NO_POWER = 0x4D,     // CAST_FAIL_NOT_ENOUGH_MANA
    SPELL_FAILED_NOTHING_TO_DISPEL = 0x4E,
    SPELL_FAILED_NOTHING_TO_STEAL = 0x4F,
    SPELL_FAILED_ONLY_ABOVEWATER = 0x50,     // CAST_FAIL_CANT_USE_WHILE_SWIMMING
    SPELL_FAILED_ONLY_DAYTIME = 0x51,
    SPELL_FAILED_ONLY_INDOORS = 0x52,
    SPELL_FAILED_ONLY_MOUNTED = 0x53,
    SPELL_FAILED_ONLY_NIGHTTIME = 0x54,
    SPELL_FAILED_ONLY_OUTDOORS = 0x55,
    SPELL_FAILED_ONLY_SHAPESHIFT = 0x56,
    SPELL_FAILED_ONLY_STEALTHED = 0x57,
    SPELL_FAILED_ONLY_UNDERWATER = 0x58,     // CAST_FAIL_CAN_ONLY_USE_WHILE_SWIMMING
    SPELL_FAILED_OUT_OF_RANGE = 0x59,
    SPELL_FAILED_PACIFIED = 0x5A,
    SPELL_FAILED_POSSESSED = 0x5B,
    // SPELL_FAILED_REAGENTS                       = 0x5C, [-ZERO] not in 1.12
    SPELL_FAILED_REQUIRES_AREA = 0x5D,     // CAST_FAIL_YOU_NEED_TO_BE_IN_XXX
    SPELL_FAILED_REQUIRES_SPELL_FOCUS = 0x5E,     // CAST_FAIL_REQUIRES_XXX
    SPELL_FAILED_ROOTED = 0x5F,     // CAST_FAIL_UNABLE_TO_MOVE
    SPELL_FAILED_SILENCED = 0x60,
    SPELL_FAILED_SPELL_IN_PROGRESS = 0x61,
    SPELL_FAILED_SPELL_LEARNED = 0x62,
    SPELL_FAILED_SPELL_UNAVAILABLE = 0x63,
    SPELL_FAILED_STUNNED = 0x64,
    SPELL_FAILED_TARGETS_DEAD = 0x65,
    SPELL_FAILED_TARGET_AFFECTING_COMBAT = 0x66,
    SPELL_FAILED_TARGET_AURASTATE = 0x67,     // CAST_FAIL_CANT_DO_THAT_YET_2
    SPELL_FAILED_TARGET_DUELING = 0x68,
    SPELL_FAILED_TARGET_ENEMY = 0x69,
    SPELL_FAILED_TARGET_ENRAGED = 0x6A,     // CAST_FAIL_TARGET_IS_TOO_ENRAGED_TO_CHARM
    SPELL_FAILED_TARGET_FRIENDLY = 0x6B,
    SPELL_FAILED_TARGET_IN_COMBAT = 0x6C,
    SPELL_FAILED_TARGET_IS_PLAYER = 0x6D,
    SPELL_FAILED_TARGET_NOT_DEAD = 0x6E,
    SPELL_FAILED_TARGET_NOT_IN_PARTY = 0x6F,
    SPELL_FAILED_TARGET_NOT_LOOTED = 0x70,     // CAST_FAIL_CREATURE_MUST_BE_LOOTED_FIRST
    SPELL_FAILED_TARGET_NOT_PLAYER = 0x71,
    SPELL_FAILED_TARGET_NO_POCKETS = 0x72,     // CAST_FAIL_NOT_ITEM_TO_STEAL
    SPELL_FAILED_TARGET_NO_WEAPONS = 0x73,
    SPELL_FAILED_TARGET_UNSKINNABLE = 0x74,
    SPELL_FAILED_THIRST_SATIATED = 0x75,
    SPELL_FAILED_TOO_CLOSE = 0x76,
    SPELL_FAILED_TOO_MANY_OF_ITEM = 0x77,
    // SPELL_FAILED_TOTEMS                         = 0x78,  // [-ZERO] not in 1.12
    SPELL_FAILED_TRAINING_POINTS = 0x79,
    SPELL_FAILED_TRY_AGAIN = 0x7A,     // CAST_FAIL_FAILED_ATTEMPT
    SPELL_FAILED_UNIT_NOT_BEHIND = 0x7B,
    SPELL_FAILED_UNIT_NOT_INFRONT = 0x7C,
    SPELL_FAILED_WRONG_PET_FOOD = 0x7D,
    SPELL_FAILED_NOT_WHILE_FATIGUED = 0x7E,
    SPELL_FAILED_TARGET_NOT_IN_INSTANCE = 0x7F,     // CAST_FAIL_TARGET_MUST_BE_IN_THIS_INSTANCE
    SPELL_FAILED_NOT_WHILE_TRADING = 0x80,
    SPELL_FAILED_TARGET_NOT_IN_RAID = 0x81,
    SPELL_FAILED_DISENCHANT_WHILE_LOOTING = 0x82,
    SPELL_FAILED_PROSPECT_WHILE_LOOTING = 0x83,
    //  SPELL_FAILED_PROSPECT_NEED_MORE             = 0x85,
    SPELL_FAILED_TARGET_FREEFORALL = 0x85,
    SPELL_FAILED_NO_EDIBLE_CORPSES = 0x86,
    SPELL_FAILED_ONLY_BATTLEGROUNDS = 0x87,
    SPELL_FAILED_TARGET_NOT_GHOST = 0x88,
    SPELL_FAILED_TOO_MANY_SKILLS = 0x89,     // CAST_FAIL_YOUR_PET_CANT_LEARN_MORE_SKILLS
    SPELL_FAILED_CANT_USE_NEW_ITEM = 0x8A,
    SPELL_FAILED_WRONG_WEATHER = 0x8B,     // CAST_FAIL_CANT_DO_IN_THIS_WEATHER
    SPELL_FAILED_DAMAGE_IMMUNE = 0x8C,     // CAST_FAIL_CANT_DO_IN_IMMUNE
    SPELL_FAILED_PREVENTED_BY_MECHANIC = 0x8D,     // CAST_FAIL_CANT_DO_IN_XXX
    SPELL_FAILED_PLAY_TIME = 0x8E,     // CAST_FAIL_GAME_TIME_OVER
    SPELL_FAILED_REPUTATION = 0x8F,
    SPELL_FAILED_MIN_SKILL = 0x90,
    SPELL_FAILED_UNKNOWN = 0x91,

    SPELL_CAST_OK = 0xFF      // custom value, don't must be send to client
};

// Spell aura states
enum AuraState
{
    // (C) used in caster aura state     (T) used in target aura state
    AURA_STATE_DEFENSE = 1,            // C   |
    AURA_STATE_HEALTHLESS_20_PERCENT = 2,            // C T |
    AURA_STATE_BERSERKING = 3,            // C   |
    AURA_STATE_FROZEN = 4,            //     | frozen target (but not used for any spells in 1.12.1 at client side)
    AURA_STATE_JUDGEMENT = 5,            // C   |
    // AURA_STATE_UNKNOWN6                   = 6,           //     | not used
    AURA_STATE_HUNTER_PARRY = 7,            // C   |
    AURA_STATE_ROGUE_ATTACK_FROM_STEALTH = 7,            // C   | FIX ME: not implemented yet!
};

// Spell mechanics
enum Mechanics
{
    MECHANIC_NONE = 0,
    MECHANIC_CHARM = 1,
    MECHANIC_DISORIENTED = 2,
    MECHANIC_DISARM = 3,
    MECHANIC_DISTRACT = 4,
    MECHANIC_FEAR = 5,
    MECHANIC_FUMBLE = 6,
    MECHANIC_ROOT = 7,
    MECHANIC_PACIFY = 8,                          // 0 spells use this mechanic
    MECHANIC_SILENCE = 9,
    MECHANIC_SLEEP = 10,
    MECHANIC_SNARE = 11,
    MECHANIC_STUN = 12,
    MECHANIC_FREEZE = 13,
    MECHANIC_KNOCKOUT = 14,
    MECHANIC_BLEED = 15,
    MECHANIC_BANDAGE = 16,
    MECHANIC_POLYMORPH = 17,
    MECHANIC_BANISH = 18,
    MECHANIC_SHIELD = 19,
    MECHANIC_SHACKLE = 20,
    MECHANIC_MOUNT = 21,
    MECHANIC_PERSUADE = 22,                         // 0 spells use this mechanic
    MECHANIC_TURN = 23,
    MECHANIC_HORROR = 24,
    MECHANIC_INVULNERABILITY = 25,
    MECHANIC_INTERRUPT = 26,
    MECHANIC_DAZE = 27,
    MECHANIC_DISCOVERY = 28,
    MECHANIC_IMMUNE_SHIELD = 29,                         // Divine (Blessing) Shield/Protection and Ice Block
    MECHANIC_SAPPED = 30
};

#define FIRST_MECHANIC          1
#define MAX_MECHANIC            31

///Mask defining \ref Mechanics mask which is immune to root and snare
#define IMMUNE_TO_ROOT_AND_SNARE_MASK ( \
                                        (1<<(MECHANIC_ROOT-1))|(1<<(MECHANIC_SNARE-1)))

#define IMMUNE_TO_ROOT_AND_STUN_MASK ( \
                                       (1<<(MECHANIC_ROOT-1))|(1<<(MECHANIC_STUN-1)))

/// Daze and all crowd control spells except polymorph are not removed
#define MECHANIC_NOT_REMOVED_BY_SHAPESHIFT ( \
        (1<<(MECHANIC_CHARM -1))|(1<<(MECHANIC_DISORIENTED-1))|(1<<(MECHANIC_FEAR  -1))| \
        (1<<(MECHANIC_PACIFY-1))|(1<<(MECHANIC_STUN       -1))|(1<<(MECHANIC_FREEZE-1))| \
        (1<<(MECHANIC_BANISH-1))|(1<<(MECHANIC_SHACKLE    -1))|(1<<(MECHANIC_HORROR-1))| \
        (1<<(MECHANIC_TURN  -1))|(1<<(MECHANIC_DAZE       -1))|(1<<(MECHANIC_SAPPED-1)))

/// Different types of \ref Spell s that can be dispelled and what the reason for the dispel is.
/// Also coupled with \ref Aura s as \ref Spell s have \ref Aura s.
enum DispelType
{
    DISPEL_NONE = 0,
    DISPEL_MAGIC = 1,
    DISPEL_CURSE = 2,
    DISPEL_DISEASE = 3,
    DISPEL_POISON = 4,
    DISPEL_STEALTH = 5,
    DISPEL_INVISIBILITY = 6,
    DISPEL_ALL = 7,
    DISPEL_SPE_NPC_ONLY = 8,
    DISPEL_ENRAGE = 9,
    DISPEL_ZG_TICKET = 10
};

#define DISPEL_ALL_MASK ( (1<<DISPEL_MAGIC) | (1<<DISPEL_CURSE) | (1<<DISPEL_DISEASE) | (1<<DISPEL_POISON) )

// To all Immune system,if target has immunes,
// some spell that related to ImmuneToDispel or ImmuneToSchool or ImmuneToDamage type can't cast to it,
// some spell_effects that related to ImmuneToEffect<effect>(only this effect in the spell) can't cast to it,
// some aura(related to Mechanics or ImmuneToState<aura>) can't apply to it.
enum SpellImmunity
{
    IMMUNITY_EFFECT = 0,                     // enum SpellEffects
    IMMUNITY_STATE = 1,                     // enum AuraType
    IMMUNITY_SCHOOL = 2,                     // enum SpellSchoolMask
    IMMUNITY_DAMAGE = 3,                     // enum SpellSchoolMask
    IMMUNITY_DISPEL = 4,                     // enum DispelType
    IMMUNITY_MECHANIC = 5                      // enum Mechanics
};

#define MAX_SPELL_IMMUNITY           6

/**
 * The different types of attacks you can do with
 * weapons
 */
enum WeaponAttackType
{
    ///Main-hand weapon
    BASE_ATTACK = 0,
    ///Off-hand weapon
    OFF_ATTACK = 1,
    ///Ranged weapon, bow/wand etc.
    RANGED_ATTACK = 2
};

#define MAX_ATTACK  3

enum Targets
{
    TARGET_NONE = 0,
    TARGET_SELF = 1,
    TARGET_RANDOM_ENEMY_CHAIN_IN_AREA = 2,                 // only one spell has that, but regardless, it's a target type after all
    TARGET_RANDOM_FRIEND_CHAIN_IN_AREA = 3,
    TARGET_RANDOM_UNIT_CHAIN_IN_AREA = 4,                 // some plague spells that are infectious - maybe targets not-infected friends inrange
    TARGET_PET = 5,
    TARGET_CHAIN_DAMAGE = 6,
    TARGET_AREAEFFECT_INSTANT = 7,                 // targets around provided destination point
    TARGET_AREAEFFECT_CUSTOM = 8,
    TARGET_INNKEEPER_COORDINATES = 9,                 // uses in teleport to innkeeper spells
    TARGET_11 = 11,                // used by spell 4 'Word of Recall Other'
    TARGET_ALL_ENEMY_IN_AREA = 15,
    TARGET_ALL_ENEMY_IN_AREA_INSTANT = 16,
    TARGET_TABLE_X_Y_Z_COORDINATES = 17,                // uses in teleport spells and some other
    TARGET_EFFECT_SELECT = 18,                // highly depends on the spell effect
    TARGET_ALL_PARTY_AROUND_CASTER = 20,
    TARGET_SINGLE_FRIEND = 21,
    TARGET_CASTER_COORDINATES = 22,                // used only in TargetA, target selection dependent from TargetB
    TARGET_GAMEOBJECT = 23,
    TARGET_IN_FRONT_OF_CASTER = 24,
    TARGET_DUELVSPLAYER = 25,
    TARGET_GAMEOBJECT_ITEM = 26,
    TARGET_MASTER = 27,
    TARGET_ALL_ENEMY_IN_AREA_CHANNELED = 28,
    TARGET_29 = 29,
    TARGET_ALL_FRIENDLY_UNITS_AROUND_CASTER = 30,           // select friendly for caster object faction (in different original caster faction) in TargetB used only with TARGET_ALL_AROUND_CASTER and in self casting range in TargetA
    TARGET_ALL_FRIENDLY_UNITS_IN_AREA = 31,
    TARGET_MINION = 32,
    TARGET_ALL_PARTY = 33,
    TARGET_ALL_PARTY_AROUND_CASTER_2 = 34,                // used in Tranquility
    TARGET_SINGLE_PARTY = 35,
    TARGET_ALL_HOSTILE_UNITS_AROUND_CASTER = 36,
    TARGET_AREAEFFECT_PARTY = 37,
    TARGET_SCRIPT = 38,
    TARGET_SELF_FISHING = 39,
    TARGET_FOCUS_OR_SCRIPTED_GAMEOBJECT = 40,
    TARGET_TOTEM_EARTH = 41,
    TARGET_TOTEM_WATER = 42,
    TARGET_TOTEM_AIR = 43,
    TARGET_TOTEM_FIRE = 44,
    TARGET_CHAIN_HEAL = 45,
    TARGET_SCRIPT_COORDINATES = 46,
    TARGET_DYNAMIC_OBJECT_FRONT = 47,
    TARGET_DYNAMIC_OBJECT_BEHIND = 48,
    TARGET_DYNAMIC_OBJECT_LEFT_SIDE = 49,
    TARGET_DYNAMIC_OBJECT_RIGHT_SIDE = 50,
    TARGET_AREAEFFECT_GO_AROUND_SOURCE = 51,
    TARGET_AREAEFFECT_GO_AROUND_DEST = 52,                // gameobject around destination, select by spell_script_target
    TARGET_CURRENT_ENEMY_COORDINATES = 53,                // set unit coordinates as dest, only 16 target B imlemented
    TARGET_LARGE_FRONTAL_CONE = 54,
    TARGET_ALL_RAID_AROUND_CASTER = 56,
    TARGET_SINGLE_FRIEND_2 = 57,
    TARGET_58 = 58,
    TARGET_NARROW_FRONTAL_CONE = 60,
    TARGET_AREAEFFECT_PARTY_AND_CLASS = 61,
    TARGET_DUELVSPLAYER_COORDINATES = 63,
};

/**
 * Tells how a spell that was cast missed or hit, ie it might have been
 * resisted or dodged etc. This enum tells which of those it was. The only
 * one which indicates a hit is SPELL_MISS_NONE
 */
enum SpellMissInfo
{
    SPELL_MISS_NONE = 0, ///< Indicates an actual hit
    SPELL_MISS_MISS = 1,
    SPELL_MISS_RESIST = 2, ///< The spell was resisted
    SPELL_MISS_DODGE = 3,
    SPELL_MISS_PARRY = 4,
    SPELL_MISS_BLOCK = 5,
    SPELL_MISS_EVADE = 6,
    SPELL_MISS_IMMUNE = 7,
    SPELL_MISS_IMMUNE2 = 8,
    SPELL_MISS_DEFLECT = 9,
    SPELL_MISS_ABSORB = 10,
    SPELL_MISS_REFLECT = 11
};

enum SpellHitType
{
    SPELL_HIT_TYPE_UNK1 = 0x00001,
    SPELL_HIT_TYPE_CRIT = 0x00002,
    SPELL_HIT_TYPE_UNK3 = 0x00004,
    SPELL_HIT_TYPE_UNK4 = 0x00008,
    SPELL_HIT_TYPE_UNK5 = 0x00010,
    SPELL_HIT_TYPE_UNK6 = 0x00020
};

/**
 * TODO: Find out where these are used except for Unit::CalculateSpellDamage
 * and dox it properly
 */
enum SpellDmgClass
{
    /// Counted as a spell damage
    SPELL_DAMAGE_CLASS_NONE = 0,
    /// Counted as a spell damage
    SPELL_DAMAGE_CLASS_MAGIC = 1,
    /// Melee damage
    SPELL_DAMAGE_CLASS_MELEE = 2,
    /// Ranged damage
    SPELL_DAMAGE_CLASS_RANGED = 3
};

enum SpellPreventionType
{
    SPELL_PREVENTION_TYPE_NONE = 0,
    SPELL_PREVENTION_TYPE_SILENCE = 1,
    SPELL_PREVENTION_TYPE_PACIFY = 2
};

/// indexes from SpellRange.dbc, listed only special and used in code
enum SpellRangeIndex
{
    /// 0.0
    SPELL_RANGE_IDX_SELF_ONLY = 1,
    /// 5.5 (but dynamic), seems to indicate melee range
    SPELL_RANGE_IDX_COMBAT = 2,
    /// 20 short range
    SPELL_RANGE_IDX_SHORT = 3,
    /// 500000 (anywhere)
    SPELL_RANGE_IDX_ANYWHERE = 13,
};

enum DamageEffectType
{
    /// Used for normal weapon damage (not for class abilities or spells)
    DIRECT_DAMAGE = 0,
    /// spell/class abilities damage
    SPELL_DIRECT_DAMAGE = 1,
    DOT = 2,
    HEAL = 3,
    /// used also in case when damage applied to health but not applied to spell channelInterruptFlags/etc
    NODAMAGE = 4,                            //< used also in case when damage applied to health but not applied to spell channelInterruptFlags/etc
    SELF_DAMAGE_ROGUE_FALL = 5,                            //< used to avoid rogue loosing stealth on falling damage
    SELF_DAMAGE = 6
};

enum GameobjectTypes
{
    GAMEOBJECT_TYPE_DOOR = 0,
    GAMEOBJECT_TYPE_BUTTON = 1,
    GAMEOBJECT_TYPE_QUESTGIVER = 2,
    GAMEOBJECT_TYPE_CHEST = 3,
    GAMEOBJECT_TYPE_BINDER = 4,
    GAMEOBJECT_TYPE_GENERIC = 5,
    GAMEOBJECT_TYPE_TRAP = 6,
    GAMEOBJECT_TYPE_CHAIR = 7,
    GAMEOBJECT_TYPE_SPELL_FOCUS = 8,
    GAMEOBJECT_TYPE_TEXT = 9,
    GAMEOBJECT_TYPE_GOOBER = 10,
    GAMEOBJECT_TYPE_TRANSPORT = 11,
    GAMEOBJECT_TYPE_AREADAMAGE = 12,
    GAMEOBJECT_TYPE_CAMERA = 13,
    GAMEOBJECT_TYPE_MAP_OBJECT = 14,
    GAMEOBJECT_TYPE_MO_TRANSPORT = 15,
    GAMEOBJECT_TYPE_DUEL_ARBITER = 16,
    GAMEOBJECT_TYPE_FISHINGNODE = 17,
    GAMEOBJECT_TYPE_SUMMONING_RITUAL = 18,
    GAMEOBJECT_TYPE_MAILBOX = 19,
    GAMEOBJECT_TYPE_AUCTIONHOUSE = 20,
    GAMEOBJECT_TYPE_GUARDPOST = 21,
    GAMEOBJECT_TYPE_SPELLCASTER = 22,
    GAMEOBJECT_TYPE_MEETINGSTONE = 23,
    GAMEOBJECT_TYPE_FLAGSTAND = 24,
    GAMEOBJECT_TYPE_FISHINGHOLE = 25,
    GAMEOBJECT_TYPE_FLAGDROP = 26,
    GAMEOBJECT_TYPE_MINI_GAME = 27,
    GAMEOBJECT_TYPE_LOTTERY_KIOSK = 28,
    GAMEOBJECT_TYPE_CAPTURE_POINT = 29,
    GAMEOBJECT_TYPE_AURA_GENERATOR = 30,
    GAMEOBJECT_TYPE_DESTRUCTIBLE_BUILDING = 14,    // Not Implemented in Zero
};

#define MAX_GAMEOBJECT_TYPE                  31             // sending to client this or greater value can crash client.

enum GameObjectFlags
{
    GO_FLAG_IN_USE = 0x00000001,                   // disables interaction while animated
    GO_FLAG_LOCKED = 0x00000002,                   // require key, spell, event, etc to be opened. Makes "Locked" appear in tooltip
    GO_FLAG_INTERACT_COND = 0x00000004,                   // can not interact (condition to interact)
    GO_FLAG_TRANSPORT = 0x00000008,                   // any kind of transport? Object can transport (elevator, boat, car)
    GO_FLAG_NO_INTERACT = 0x00000010,                   // players can not interact with this go (often need to remove flag in event)
    GO_FLAG_NODESPAWN = 0x00000020,                   // never despawn, typically for doors, they just change state
    GO_FLAG_TRIGGERED = 0x00000040                    // typically, summoned objects. Triggered by spell or other events
};

enum GameObjectDynamicLowFlags
{
    GO_DYNFLAG_LO_ACTIVATE = 0x01,                 // enables interaction with GO
    GO_DYNFLAG_LO_ANIMATE = 0x02,                 // possibly more distinct animation of GO
    GO_DYNFLAG_LO_NO_INTERACT = 0x04,                 // appears to disable interaction (not fully verified)
    GO_DYNFLAG_LO_SPARKLE = 0x08                  // makes GO sparkle
};

enum TextEmotes
{
    TEXTEMOTE_AGREE = 1,
    TEXTEMOTE_AMAZE = 2,
    TEXTEMOTE_ANGRY = 3,
    TEXTEMOTE_APOLOGIZE = 4,
    TEXTEMOTE_APPLAUD = 5,
    TEXTEMOTE_BASHFUL = 6,
    TEXTEMOTE_BECKON = 7,
    TEXTEMOTE_BEG = 8,
    TEXTEMOTE_BITE = 9,
    TEXTEMOTE_BLEED = 10,
    TEXTEMOTE_BLINK = 11,
    TEXTEMOTE_BLUSH = 12,
    TEXTEMOTE_BONK = 13,
    TEXTEMOTE_BORED = 14,
    TEXTEMOTE_BOUNCE = 15,
    TEXTEMOTE_BRB = 16,
    TEXTEMOTE_BOW = 17,
    TEXTEMOTE_BURP = 18,
    TEXTEMOTE_BYE = 19,
    TEXTEMOTE_CACKLE = 20,
    TEXTEMOTE_CHEER = 21,
    TEXTEMOTE_CHICKEN = 22,
    TEXTEMOTE_CHUCKLE = 23,
    TEXTEMOTE_CLAP = 24,
    TEXTEMOTE_CONFUSED = 25,
    TEXTEMOTE_CONGRATULATE = 26,
    TEXTEMOTE_COUGH = 27,
    TEXTEMOTE_COWER = 28,
    TEXTEMOTE_CRACK = 29,
    TEXTEMOTE_CRINGE = 30,
    TEXTEMOTE_CRY = 31,
    TEXTEMOTE_CURIOUS = 32,
    TEXTEMOTE_CURTSEY = 33,
    TEXTEMOTE_DANCE = 34,
    TEXTEMOTE_DRINK = 35,
    TEXTEMOTE_DROOL = 36,
    TEXTEMOTE_EAT = 37,
    TEXTEMOTE_EYE = 38,
    TEXTEMOTE_FART = 39,
    TEXTEMOTE_FIDGET = 40,
    TEXTEMOTE_FLEX = 41,
    TEXTEMOTE_FROWN = 42,
    TEXTEMOTE_GASP = 43,
    TEXTEMOTE_GAZE = 44,
    TEXTEMOTE_GIGGLE = 45,
    TEXTEMOTE_GLARE = 46,
    TEXTEMOTE_GLOAT = 47,
    TEXTEMOTE_GREET = 48,
    TEXTEMOTE_GRIN = 49,
    TEXTEMOTE_GROAN = 50,
    TEXTEMOTE_GROVEL = 51,
    TEXTEMOTE_GUFFAW = 52,
    TEXTEMOTE_HAIL = 53,
    TEXTEMOTE_HAPPY = 54,
    TEXTEMOTE_HELLO = 55,
    TEXTEMOTE_HUG = 56,
    TEXTEMOTE_HUNGRY = 57,
    TEXTEMOTE_KISS = 58,
    TEXTEMOTE_KNEEL = 59,
    TEXTEMOTE_LAUGH = 60,
    TEXTEMOTE_LAYDOWN = 61,
    TEXTEMOTE_MESSAGE = 62,
    TEXTEMOTE_MOAN = 63,
    TEXTEMOTE_MOON = 64,
    TEXTEMOTE_MOURN = 65,
    TEXTEMOTE_NO = 66,
    TEXTEMOTE_NOD = 67,
    TEXTEMOTE_NOSEPICK = 68,
    TEXTEMOTE_PANIC = 69,
    TEXTEMOTE_PEER = 70,
    TEXTEMOTE_PLEAD = 71,
    TEXTEMOTE_POINT = 72,
    TEXTEMOTE_POKE = 73,
    TEXTEMOTE_PRAY = 74,
    TEXTEMOTE_ROAR = 75,
    TEXTEMOTE_ROFL = 76,
    TEXTEMOTE_RUDE = 77,
    TEXTEMOTE_SALUTE = 78,
    TEXTEMOTE_SCRATCH = 79,
    TEXTEMOTE_SEXY = 80,
    TEXTEMOTE_SHAKE = 81,
    TEXTEMOTE_SHOUT = 82,
    TEXTEMOTE_SHRUG = 83,
    TEXTEMOTE_SHY = 84,
    TEXTEMOTE_SIGH = 85,
    TEXTEMOTE_SIT = 86,
    TEXTEMOTE_SLEEP = 87,
    TEXTEMOTE_SNARL = 88,
    TEXTEMOTE_SPIT = 89,
    TEXTEMOTE_STARE = 90,
    TEXTEMOTE_SURPRISED = 91,
    TEXTEMOTE_SURRENDER = 92,
    TEXTEMOTE_TALK = 93,
    TEXTEMOTE_TALKEX = 94,
    TEXTEMOTE_TALKQ = 95,
    TEXTEMOTE_TAP = 96,
    TEXTEMOTE_THANK = 97,
    TEXTEMOTE_THREATEN = 98,
    TEXTEMOTE_TIRED = 99,
    TEXTEMOTE_VICTORY = 100,
    TEXTEMOTE_WAVE = 101,
    TEXTEMOTE_WELCOME = 102,
    TEXTEMOTE_WHINE = 103,
    TEXTEMOTE_WHISTLE = 104,
    TEXTEMOTE_WORK = 105,
    TEXTEMOTE_YAWN = 106,
    TEXTEMOTE_BOGGLE = 107,
    TEXTEMOTE_CALM = 108,
    TEXTEMOTE_COLD = 109,
    TEXTEMOTE_COMFORT = 110,
    TEXTEMOTE_CUDDLE = 111,
    TEXTEMOTE_DUCK = 112,
    TEXTEMOTE_INSULT = 113,
    TEXTEMOTE_INTRODUCE = 114,
    TEXTEMOTE_JK = 115,
    TEXTEMOTE_LICK = 116,
    TEXTEMOTE_LISTEN = 117,
    TEXTEMOTE_LOST = 118,
    TEXTEMOTE_MOCK = 119,
    TEXTEMOTE_PONDER = 120,
    TEXTEMOTE_POUNCE = 121,
    TEXTEMOTE_PRAISE = 122,
    TEXTEMOTE_PURR = 123,
    TEXTEMOTE_PUZZLE = 124,
    TEXTEMOTE_RAISE = 125,
    TEXTEMOTE_READY = 126,
    TEXTEMOTE_SHIMMY = 127,
    TEXTEMOTE_SHIVER = 128,
    TEXTEMOTE_SHOO = 129,
    TEXTEMOTE_SLAP = 130,
    TEXTEMOTE_SMIRK = 131,
    TEXTEMOTE_SNIFF = 132,
    TEXTEMOTE_SNUB = 133,
    TEXTEMOTE_SOOTHE = 134,
    TEXTEMOTE_STINK = 135,
    TEXTEMOTE_TAUNT = 136,
    TEXTEMOTE_TEASE = 137,
    TEXTEMOTE_THIRSTY = 138,
    TEXTEMOTE_VETO = 139,
    TEXTEMOTE_SNICKER = 140,
    TEXTEMOTE_STAND = 141,
    TEXTEMOTE_TICKLE = 142,
    TEXTEMOTE_VIOLIN = 143,
    TEXTEMOTE_SMILE = 163,
    TEXTEMOTE_RASP = 183,
    TEXTEMOTE_PITY = 203,
    TEXTEMOTE_GROWL = 204,
    TEXTEMOTE_BARK = 205,
    TEXTEMOTE_SCARED = 223,
    TEXTEMOTE_FLOP = 224,
    TEXTEMOTE_LOVE = 225,
    TEXTEMOTE_MOO = 226,
    TEXTEMOTE_OPENFIRE = 327,
    TEXTEMOTE_FLIRT = 328,
    TEXTEMOTE_JOKE = 329,
    TEXTEMOTE_COMMEND = 243,
    TEXTEMOTE_WINK = 363,
    TEXTEMOTE_PAT = 364,
    TEXTEMOTE_SERIOUS = 365,
    TEXTEMOTE_MOUNTSPECIAL = 366,
    TEXTEMOTE_GOODLUCK = 367,
    TEXTEMOTE_BLAME = 368,
    TEXTEMOTE_BLANK = 369,
    TEXTEMOTE_BRANDISH = 370,
    TEXTEMOTE_BREATH = 371,
    TEXTEMOTE_DISAGREE = 372,
    TEXTEMOTE_DOUBT = 373,
    TEXTEMOTE_EMBARRASS = 374,
    TEXTEMOTE_ENCOURAGE = 375,
    TEXTEMOTE_ENEMY = 376,
    TEXTEMOTE_EYEBROW = 377,
    TEXTEMOTE_TOAST = 378
};

enum Emote
{
    EMOTE_ONESHOT_NONE = 0,
    EMOTE_ONESHOT_TALK = 1,
    EMOTE_ONESHOT_BOW = 2,
    EMOTE_ONESHOT_WAVE = 3,
    EMOTE_ONESHOT_CHEER = 4,
    EMOTE_ONESHOT_EXCLAMATION = 5,
    EMOTE_ONESHOT_QUESTION = 6,
    EMOTE_ONESHOT_EAT = 7,
    EMOTE_STATE_DANCE = 10,
    EMOTE_ONESHOT_LAUGH = 11,
    EMOTE_STATE_SLEEP = 12,
    EMOTE_STATE_SIT = 13,
    EMOTE_ONESHOT_RUDE = 14,
    EMOTE_ONESHOT_ROAR = 15,
    EMOTE_ONESHOT_KNEEL = 16,
    EMOTE_ONESHOT_KISS = 17,
    EMOTE_ONESHOT_CRY = 18,
    EMOTE_ONESHOT_CHICKEN = 19,
    EMOTE_ONESHOT_BEG = 20,
    EMOTE_ONESHOT_APPLAUD = 21,
    EMOTE_ONESHOT_SHOUT = 22,
    EMOTE_ONESHOT_FLEX = 23,
    EMOTE_ONESHOT_SHY = 24,
    EMOTE_ONESHOT_POINT = 25,
    EMOTE_STATE_STAND = 26,
    EMOTE_STATE_READYUNARMED = 27,
    EMOTE_STATE_WORK_SHEATHED = 28,
    EMOTE_STATE_POINT = 29,
    EMOTE_STATE_NONE = 30,
    EMOTE_ONESHOT_WOUND = 33,
    EMOTE_ONESHOT_WOUNDCRITICAL = 34,
    EMOTE_ONESHOT_ATTACKUNARMED = 35,
    EMOTE_ONESHOT_ATTACK1H = 36,
    EMOTE_ONESHOT_ATTACK2HTIGHT = 37,
    EMOTE_ONESHOT_ATTACK2HLOOSE = 38,
    EMOTE_ONESHOT_PARRYUNARMED = 39,
    EMOTE_ONESHOT_PARRYSHIELD = 43,
    EMOTE_ONESHOT_READYUNARMED = 44,
    EMOTE_ONESHOT_READY1H = 45,
    EMOTE_ONESHOT_READYBOW = 48,
    EMOTE_ONESHOT_SPELLPRECAST = 50,
    EMOTE_ONESHOT_SPELLCAST = 51,
    EMOTE_ONESHOT_BATTLEROAR = 53,
    EMOTE_ONESHOT_SPECIALATTACK1H = 54,
    EMOTE_ONESHOT_KICK = 60,
    EMOTE_ONESHOT_ATTACKTHROWN = 61,
    EMOTE_STATE_STUN = 64,
    EMOTE_STATE_DEAD = 65,
    EMOTE_ONESHOT_SALUTE = 66,
    EMOTE_STATE_KNEEL = 68,
    EMOTE_STATE_USESTANDING = 69,
    EMOTE_ONESHOT_WAVE_NOSHEATHE = 70,
    EMOTE_ONESHOT_CHEER_NOSHEATHE = 71,
    EMOTE_ONESHOT_EAT_NOSHEATHE = 92,
    EMOTE_STATE_STUN_NOSHEATHE = 93,
    EMOTE_ONESHOT_DANCE = 94,
    EMOTE_ONESHOT_SALUTE_NOSHEATH = 113,
    EMOTE_STATE_USESTANDING_NOSHEATHE = 133,
    EMOTE_ONESHOT_LAUGH_NOSHEATHE = 153,
    EMOTE_STATE_WORK = 173,
    EMOTE_STATE_SPELLPRECAST = 193,
    EMOTE_ONESHOT_READYRIFLE = 213,
    EMOTE_STATE_READYRIFLE = 214,
    EMOTE_STATE_WORK_MINING = 233,
    EMOTE_STATE_WORK_CHOPWOOD = 234,
    EMOTE_STATE_APPLAUD = 253,
    EMOTE_ONESHOT_LIFTOFF = 254,
    EMOTE_ONESHOT_YES = 273,
    EMOTE_ONESHOT_NO = 274,
    EMOTE_ONESHOT_TRAIN = 275,
    EMOTE_ONESHOT_LAND = 293,
    EMOTE_STATE_AT_EASE = 313,
    EMOTE_STATE_READY1H = 333,
    EMOTE_STATE_SPELLKNEELSTART = 353,
    EMOTE_STATE_SUBMERGED = 373,
    EMOTE_ONESHOT_SUBMERGE = 374,
    EMOTE_STATE_READY2H = 375,
    EMOTE_STATE_READYBOW = 376,
    EMOTE_ONESHOT_MOUNTSPECIAL = 377,
    EMOTE_STATE_TALK = 378,
    EMOTE_STATE_FISHING = 379,
    EMOTE_ONESHOT_FISHING = 380,
    EMOTE_ONESHOT_LOOT = 381,
    EMOTE_STATE_WHIRLWIND = 382,
    EMOTE_STATE_DROWNED = 383,
    EMOTE_STATE_HOLD_BOW = 384,
    EMOTE_STATE_HOLD_RIFLE = 385,
    EMOTE_STATE_HOLD_THROWN = 386,
    EMOTE_ONESHOT_DROWN = 387,
    EMOTE_ONESHOT_STOMP = 388,
    EMOTE_ONESHOT_ATTACKOFF = 389,
    EMOTE_ONESHOT_ATTACKOFFPIERCE = 390,
    EMOTE_STATE_ROAR = 391,
    EMOTE_STATE_LAUGH = 392,
    EMOTE_ONESHOT_CREATURE_SPECIAL = 393,
    EMOTE_ONESHOT_JUMPLANDRUN = 394,
    EMOTE_ONESHOT_JUMPEND = 395,
    EMOTE_ONESHOT_TALK_NOSHEATHE = 396,
    EMOTE_ONESHOT_POINT_NOSHEATHE = 397,
    EMOTE_STATE_CANNIBALIZE = 398,
    EMOTE_ONESHOT_JUMPSTART = 399,
    EMOTE_STATE_DANCESPECIAL = 400,
    EMOTE_ONESHOT_DANCESPECIAL = 401,
    EMOTE_ONESHOT_CUSTOMSPELL01 = 402,
    EMOTE_ONESHOT_CUSTOMSPELL02 = 403,
    EMOTE_ONESHOT_CUSTOMSPELL03 = 404,
    EMOTE_ONESHOT_CUSTOMSPELL04 = 405,
    EMOTE_ONESHOT_CUSTOMSPELL05 = 406,
    EMOTE_ONESHOT_CUSTOMSPELL06 = 407,
    EMOTE_ONESHOT_CUSTOMSPELL07 = 408,
    EMOTE_ONESHOT_CUSTOMSPELL08 = 409,
    EMOTE_ONESHOT_CUSTOMSPELL09 = 410,
    EMOTE_ONESHOT_CUSTOMSPELL10 = 411,
    EMOTE_STATE_EXCLAIM = 412,
    EMOTE_STATE_SIT_CHAIR_MED = 415,
    EMOTE_STATE_SPELLEFFECT_HOLD = 422,
    EMOTE_STATE_EAT_NO_SHEATHE = 423
};

enum Anim
{
    ANIM_STAND = 0x0,
    ANIM_DEATH = 0x1,
    ANIM_SPELL = 0x2,
    ANIM_STOP = 0x3,
    ANIM_WALK = 0x4,
    ANIM_RUN = 0x5,
    ANIM_DEAD = 0x6,
    ANIM_RISE = 0x7,
    ANIM_STANDWOUND = 0x8,
    ANIM_COMBATWOUND = 0x9,
    ANIM_COMBATCRITICAL = 0xA,
    ANIM_SHUFFLE_LEFT = 0xB,
    ANIM_SHUFFLE_RIGHT = 0xC,
    ANIM_WALK_BACKWARDS = 0xD,
    ANIM_STUN = 0xE,
    ANIM_HANDS_CLOSED = 0xF,
    ANIM_ATTACKUNARMED = 0x10,
    ANIM_ATTACK1H = 0x11,
    ANIM_ATTACK2HTIGHT = 0x12,
    ANIM_ATTACK2HLOOSE = 0x13,
    ANIM_PARRYUNARMED = 0x14,
    ANIM_PARRY1H = 0x15,
    ANIM_PARRY2HTIGHT = 0x16,
    ANIM_PARRY2HLOOSE = 0x17,
    ANIM_PARRYSHIELD = 0x18,
    ANIM_READYUNARMED = 0x19,
    ANIM_READY1H = 0x1A,
    ANIM_READY2HTIGHT = 0x1B,
    ANIM_READY2HLOOSE = 0x1C,
    ANIM_READYBOW = 0x1D,
    ANIM_DODGE = 0x1E,
    ANIM_SPELLPRECAST = 0x1F,
    ANIM_SPELLCAST = 0x20,
    ANIM_SPELLCASTAREA = 0x21,
    ANIM_NPCWELCOME = 0x22,
    ANIM_NPCGOODBYE = 0x23,
    ANIM_BLOCK = 0x24,
    ANIM_JUMPSTART = 0x25,
    ANIM_JUMP = 0x26,
    ANIM_JUMPEND = 0x27,
    ANIM_FALL = 0x28,
    ANIM_SWIMIDLE = 0x29,
    ANIM_SWIM = 0x2A,
    ANIM_SWIM_LEFT = 0x2B,
    ANIM_SWIM_RIGHT = 0x2C,
    ANIM_SWIM_BACKWARDS = 0x2D,
    ANIM_ATTACKBOW = 0x2E,
    ANIM_FIREBOW = 0x2F,
    ANIM_READYRIFLE = 0x30,
    ANIM_ATTACKRIFLE = 0x31,
    ANIM_LOOT = 0x32,
    ANIM_SPELL_PRECAST_DIRECTED = 0x33,
    ANIM_SPELL_PRECAST_OMNI = 0x34,
    ANIM_SPELL_CAST_DIRECTED = 0x35,
    ANIM_SPELL_CAST_OMNI = 0x36,
    ANIM_SPELL_BATTLEROAR = 0x37,
    ANIM_SPELL_READYABILITY = 0x38,
    ANIM_SPELL_SPECIAL1H = 0x39,
    ANIM_SPELL_SPECIAL2H = 0x3A,
    ANIM_SPELL_SHIELDBASH = 0x3B,
    ANIM_EMOTE_TALK = 0x3C,
    ANIM_EMOTE_EAT = 0x3D,
    ANIM_EMOTE_WORK = 0x3E,
    ANIM_EMOTE_USE_STANDING = 0x3F,
    ANIM_EMOTE_EXCLAMATION = 0x40,
    ANIM_EMOTE_QUESTION = 0x41,
    ANIM_EMOTE_BOW = 0x42,
    ANIM_EMOTE_WAVE = 0x43,
    ANIM_EMOTE_CHEER = 0x44,
    ANIM_EMOTE_DANCE = 0x45,
    ANIM_EMOTE_LAUGH = 0x46,
    ANIM_EMOTE_SLEEP = 0x47,
    ANIM_EMOTE_SIT_GROUND = 0x48,
    ANIM_EMOTE_RUDE = 0x49,
    ANIM_EMOTE_ROAR = 0x4A,
    ANIM_EMOTE_KNEEL = 0x4B,
    ANIM_EMOTE_KISS = 0x4C,
    ANIM_EMOTE_CRY = 0x4D,
    ANIM_EMOTE_CHICKEN = 0x4E,
    ANIM_EMOTE_BEG = 0x4F,
    ANIM_EMOTE_APPLAUD = 0x50,
    ANIM_EMOTE_SHOUT = 0x51,
    ANIM_EMOTE_FLEX = 0x52,
    ANIM_EMOTE_SHY = 0x53,
    ANIM_EMOTE_POINT = 0x54,
    ANIM_ATTACK1HPIERCE = 0x55,
    ANIM_ATTACK2HLOOSEPIERCE = 0x56,
    ANIM_ATTACKOFF = 0x57,
    ANIM_ATTACKOFFPIERCE = 0x58,
    ANIM_SHEATHE = 0x59,
    ANIM_HIPSHEATHE = 0x5A,
    ANIM_MOUNT = 0x5B,
    ANIM_RUN_LEANRIGHT = 0x5C,
    ANIM_RUN_LEANLEFT = 0x5D,
    ANIM_MOUNT_SPECIAL = 0x5E,
    ANIM_KICK = 0x5F,
    ANIM_SITDOWN = 0x60,
    ANIM_SITTING = 0x61,
    ANIM_SITUP = 0x62,
    ANIM_SLEEPDOWN = 0x63,
    ANIM_SLEEPING = 0x64,
    ANIM_SLEEPUP = 0x65,
    ANIM_SITCHAIRLOW = 0x66,
    ANIM_SITCHAIRMEDIUM = 0x67,
    ANIM_SITCHAIRHIGH = 0x68,
    ANIM_LOADBOW = 0x69,
    ANIM_LOADRIFLE = 0x6A,
    ANIM_ATTACKTHROWN = 0x6B,
    ANIM_READYTHROWN = 0x6C,
    ANIM_HOLDBOW = 0x6D,
    ANIM_HOLDRIFLE = 0x6E,
    ANIM_HOLDTHROWN = 0x6F,
    ANIM_LOADTHROWN = 0x70,
    ANIM_EMOTE_SALUTE = 0x71,
    ANIM_KNEELDOWN = 0x72,
    ANIM_KNEELING = 0x73,
    ANIM_KNEELUP = 0x74,
    ANIM_ATTACKUNARMEDOFF = 0x75,
    ANIM_SPECIALUNARMED = 0x76,
    ANIM_STEALTHWALK = 0x77,
    ANIM_STEALTHSTAND = 0x78,
    ANIM_KNOCKDOWN = 0x79,
    ANIM_EATING = 0x7A,
    ANIM_USESTANDINGLOOP = 0x7B,
    ANIM_CHANNELCASTDIRECTED = 0x7C,
    ANIM_CHANNELCASTOMNI = 0x7D,
    ANIM_WHIRLWIND = 0x7E,
    ANIM_BIRTH = 0x7F,
    ANIM_USESTANDINGSTART = 0x80,
    ANIM_USESTANDINGEND = 0x81,
    ANIM_HOWL = 0x82,
    ANIM_DROWN = 0x83,
    ANIM_DROWNED = 0x84,
    ANIM_FISHINGCAST = 0x85,
    ANIM_FISHINGLOOP = 0x86,
    ANIM_FLY = 0x87,
    ANIM_EMOTE_WORK_NO_SHEATHE = 0x88,
    ANIM_EMOTE_STUN_NO_SHEATHE = 0x89,
    ANIM_EMOTE_USE_STANDING_NO_SHEATHE = 0x8A,
    ANIM_SPELL_SLEEP_DOWN = 0x8B,
    ANIM_SPELL_KNEEL_START = 0x8C,
    ANIM_SPELL_KNEEL_LOOP = 0x8D,
    ANIM_SPELL_KNEEL_END = 0x8E,
    ANIM_SPRINT = 0x8F,
    ANIM_IN_FIGHT = 0x90,

    ANIM_GAMEOBJ_SPAWN = 145,
    ANIM_GAMEOBJ_CLOSE = 146,
    ANIM_GAMEOBJ_CLOSED = 147,
    ANIM_GAMEOBJ_OPEN = 148,
    ANIM_GAMEOBJ_OPENED = 149,
    ANIM_GAMEOBJ_DESTROY = 150,
    ANIM_GAMEOBJ_DESTROYED = 151,
    ANIM_GAMEOBJ_REBUILD = 152,
    ANIM_GAMEOBJ_CUSTOM0 = 153,
    ANIM_GAMEOBJ_CUSTOM1 = 154,
    ANIM_GAMEOBJ_CUSTOM2 = 155,
    ANIM_GAMEOBJ_CUSTOM3 = 156,
    ANIM_GAMEOBJ_DESPAWN = 157,
    ANIM_HOLD = 158,
    ANIM_DECAY = 159,
    ANIM_BOWPULL = 160,
    ANIM_BOWRELEASE = 161,
    ANIM_SHIPSTART = 162,
    ANIM_SHIPMOVEING = 163,
    ANIM_SHIPSTOP = 164,
    ANIM_GROUPARROW = 165,
    ANIM_ARROW = 166,
    ANIM_CORPSEARROW = 167,
    ANIM_GUIDEARROW = 168,
    ANIM_SWAY = 169,
    ANIM_DRUIDCATPOUNCE = 170,
    ANIM_DRUIDCATRIP = 171,
    ANIM_DRUIDCATRAKE = 172,
    ANIM_DRUIDCATRAVAGE = 173,
    ANIM_DRUIDCATCLAW = 174,
    ANIM_DRUIDCATCOWER = 175,
    ANIM_DRUIDBEARSWIPE = 176,
    ANIM_DRUIDBEARBITE = 177,
    ANIM_DRUIDBEARMAUL = 178,
    ANIM_DRUIDBEARBASH = 179,
    ANIM_DRAGONTAIL = 180,
    ANIM_DRAGONSTOMP = 181,
    ANIM_DRAGONSPIT = 182,
    ANIM_DRAGONSPITHOVER = 183,
    ANIM_DRAGONSPITFLY = 184,
    ANIM_EMOTEYES = 185,
    ANIM_EMOTENO = 186,
    ANIM_JUMPLANDRUN = 187,
    ANIM_LOOTHOLD = 188,
    ANIM_LOOTUP = 189,
    ANIM_STANDHIGH = 190,
    ANIM_IMPACT = 191,
    ANIM_LIFTOFF = 192,
    ANIM_HOVER = 193,
    ANIM_SUCCUBUSENTICE = 194,
    ANIM_EMOTETRAIN = 195,
    ANIM_EMOTEDEAD = 196,
    ANIM_EMOTEDANCEONCE = 197,
    ANIM_DEFLECT = 198,
    ANIM_EMOTEEATNOSHEATHE = 199,
    ANIM_LAND = 200,
    ANIM_SUBMERGE = 201,
    ANIM_SUBMERGED = 202,
    ANIM_CANNIBALIZE = 203,
    ANIM_ARROWBIRTH = 204,
    ANIM_GROURARROWBIRTH = 205,
    ANIM_CORPSEARROWBIRTH = 206,
    ANIM_GUIDEARROWBIRTH = 207,
    ANIM_EMOTETALKNOSHEATHE = 208,
    ANIM_EMOTEPOINTNOSHEATHE = 209,
    ANIM_EMOTESALUTENOSHEATHE = 210,
    ANIM_EMOTEDANCESPECIAL = 211,
    ANIM_MUTILATE = 212,
    ANIM_CUSTOMSPELL01 = 213,
    ANIM_CUSTOMSPELL02 = 214,
    ANIM_CUSTOMSPELL03 = 215,
    ANIM_CUSTOMSPELL04 = 216,
    ANIM_CUSTOMSPELL05 = 217,
    ANIM_CUSTOMSPELL06 = 218,
    ANIM_CUSTOMSPELL07 = 219,
    ANIM_CUSTOMSPELL08 = 220,
    ANIM_CUSTOMSPELL09 = 221,
    ANIM_CUSTOMSPELL10 = 222,
    ANIM_StealthRun = 223
};

enum LockKeyType
{
    LOCK_KEY_NONE = 0,
    LOCK_KEY_ITEM = 1,
    LOCK_KEY_SKILL = 2
};

enum LockType
{
    LOCKTYPE_PICKLOCK = 1,
    LOCKTYPE_HERBALISM = 2,
    LOCKTYPE_MINING = 3,
    LOCKTYPE_DISARM_TRAP = 4,
    LOCKTYPE_OPEN = 5,
    LOCKTYPE_TREASURE = 6,
    LOCKTYPE_CALCIFIED_ELVEN_GEMS = 7,
    LOCKTYPE_CLOSE = 8,
    LOCKTYPE_ARM_TRAP = 9,
    LOCKTYPE_QUICK_OPEN = 10,
    LOCKTYPE_QUICK_CLOSE = 11,
    LOCKTYPE_OPEN_TINKERING = 12,
    LOCKTYPE_OPEN_KNEELING = 13,
    LOCKTYPE_OPEN_ATTACKING = 14,
    LOCKTYPE_GAHZRIDIAN = 15,
    LOCKTYPE_BLASTING = 16,
    LOCKTYPE_SLOW_OPEN = 17,
    LOCKTYPE_SLOW_CLOSE = 18,
    LOCKTYPE_FISHING = 19
};

enum TrainerType                                            // this is important type for npcs!
{
    TRAINER_TYPE_CLASS = 0,
    TRAINER_TYPE_MOUNTS = 1,                     // on blizz it's 2
    TRAINER_TYPE_TRADESKILLS = 2,
    TRAINER_TYPE_PETS = 3
};

#define MAX_TRAINER_TYPE 4

// CreatureType.dbc
enum CreatureType
{
    CREATURE_TYPE_BEAST = 1,
    CREATURE_TYPE_DRAGONKIN = 2,
    CREATURE_TYPE_DEMON = 3,
    CREATURE_TYPE_ELEMENTAL = 4,
    CREATURE_TYPE_GIANT = 5,
    CREATURE_TYPE_UNDEAD = 6,
    CREATURE_TYPE_HUMANOID = 7,
    CREATURE_TYPE_CRITTER = 8,
    CREATURE_TYPE_MECHANICAL = 9,
    CREATURE_TYPE_NOT_SPECIFIED = 10,
    CREATURE_TYPE_TOTEM = 11,
};

unsigned int const CREATURE_TYPEMASK_HUMANOID_OR_UNDEAD = (1 << (CREATURE_TYPE_HUMANOID - 1)) | (1 << (CREATURE_TYPE_UNDEAD - 1));
unsigned int const CREATURE_TYPEMASK_MECHANICAL_OR_ELEMENTAL = (1 << (CREATURE_TYPE_MECHANICAL - 1)) | (1 << (CREATURE_TYPE_ELEMENTAL - 1));

// CreatureFamily.dbc
enum CreatureFamily
{
    CREATURE_FAMILY_WOLF = 1,
    CREATURE_FAMILY_CAT = 2,
    CREATURE_FAMILY_SPIDER = 3,
    CREATURE_FAMILY_BEAR = 4,
    CREATURE_FAMILY_BOAR = 5,
    CREATURE_FAMILY_CROCOLISK = 6,
    CREATURE_FAMILY_CARRION_BIRD = 7,
    CREATURE_FAMILY_CRAB = 8,
    CREATURE_FAMILY_GORILLA = 9,
    CREATURE_FAMILY_HORSE_CUSTOM = 10,                    // not exist in DBC but used for horse like beasts in DB
    CREATURE_FAMILY_RAPTOR = 11,
    CREATURE_FAMILY_TALLSTRIDER = 12,
    CREATURE_FAMILY_FELHUNTER = 15,
    CREATURE_FAMILY_VOIDWALKER = 16,
    CREATURE_FAMILY_SUCCUBUS = 17,
    CREATURE_FAMILY_DOOMGUARD = 19,
    CREATURE_FAMILY_SCORPID = 20,
    CREATURE_FAMILY_TURTLE = 21,
    CREATURE_FAMILY_IMP = 23,
    CREATURE_FAMILY_BAT = 24,
    CREATURE_FAMILY_HYENA = 25,
    CREATURE_FAMILY_OWL = 26,
    CREATURE_FAMILY_WIND_SERPENT = 27,
    CREATURE_FAMILY_REMOTE_CONTROL = 28,
};

enum CreatureTypeFlags
{
    CREATURE_TYPEFLAGS_TAMEABLE = 0x00000001,       // Tameable by any hunter
    CREATURE_TYPEFLAGS_GHOST_VISIBLE = 0x00000002,       // Creatures which can _also_ be seen when player is a ghost, used in CanInteract function by client, can't be attacked
    CREATURE_TYPEFLAGS_UNK3 = 0x00000004,       // "BOSS" flag for tooltips
    CREATURE_TYPEFLAGS_UNK4 = 0x00000008,
    CREATURE_TYPEFLAGS_UNK5 = 0x00000010,       // controls something in client tooltip related to creature faction
    CREATURE_TYPEFLAGS_UNK6 = 0x00000020,       // may be sound related
    CREATURE_TYPEFLAGS_UNK7 = 0x00000040,       // may be related to attackable / not attackable creatures with spells, used together with lua_IsHelpfulSpell/lua_IsHarmfulSpell
    CREATURE_TYPEFLAGS_UNK8 = 0x00000080,       // has something to do with unit interaction / quest status requests
    CREATURE_TYPEFLAGS_HERBLOOT = 0x00000100,       // Can be looted by herbalist
    CREATURE_TYPEFLAGS_MININGLOOT = 0x00000200,       // Can be looted by miner
    CREATURE_TYPEFLAGS_UNK11 = 0x00000400,       // no idea, but it used by client
    CREATURE_TYPEFLAGS_UNK12 = 0x00000800,       // related to possibility to cast spells while mounted
    CREATURE_TYPEFLAGS_CAN_ASSIST = 0x00001000,       // Can aid any player (and group) in combat. Typically seen for escorting NPC's
    CREATURE_TYPEFLAGS_UNK14 = 0x00002000,       // checked from calls in Lua_PetHasActionBar
    CREATURE_TYPEFLAGS_UNK15 = 0x00004000,       // Lua_UnitGUID, client does guid_low &= 0xFF000000 if this flag is set
    CREATURE_TYPEFLAGS_ENGINEERLOOT = 0x00008000,       // Can be looted by engineer
};

enum CreatureEliteType
{
    CREATURE_ELITE_NORMAL = 0,
    CREATURE_ELITE_ELITE = 1,
    CREATURE_ELITE_RAREELITE = 2,
    CREATURE_ELITE_WORLDBOSS = 3,
    CREATURE_ELITE_RARE = 4,
    CREATURE_UNKNOWN = 5                      // found in 2.2.3 for 2 mobs
};

enum HolidayIds
{
    HOLIDAY_NONE = 0,

    HOLIDAY_FIREWORKS_SPECTACULAR = 62,
    HOLIDAY_FEAST_OF_WINTER_VEIL = 141,
    HOLIDAY_NOBLEGARDEN = 181,
    HOLIDAY_CHILDRENS_WEEK = 201,
    HOLIDAY_CALL_TO_ARMS_AV = 283,
    HOLIDAY_CALL_TO_ARMS_WS = 284,
    HOLIDAY_CALL_TO_ARMS_AB = 285,
    HOLIDAY_FISHING_EXTRAVAGANZA = 301,
    HOLIDAY_HARVEST_FESTIVAL = 321,
    HOLIDAY_HALLOWS_END = 324,
    HOLIDAY_LUNAR_FESTIVAL = 327,
    HOLIDAY_LOVE_IS_IN_THE_AIR = 335,
    HOLIDAY_FIRE_FESTIVAL = 341,
    HOLIDAY_BREWFEST = 372,
    HOLIDAY_DARKMOON_FAIRE_ELWYNN = 374,
    HOLIDAY_DARKMOON_FAIRE_THUNDER = 375,
};

// values based at QuestSort.dbc
enum QuestSort
{
    QUEST_SORT_EPIC = 1,
    QUEST_SORT_WAILING_CAVERNS_OLD = 21,
    QUEST_SORT_SEASONAL = 22,
    QUEST_SORT_UNDERCITY_OLD = 23,
    QUEST_SORT_HERBALISM = 24,
    QUEST_SORT_SCARLET_MONASTERY_OLD = 25,
    QUEST_SORT_ULDAMN_OLD = 41,
    QUEST_SORT_WARLOCK = 61,
    QUEST_SORT_WARRIOR = 81,
    QUEST_SORT_SHAMAN = 82,
    QUEST_SORT_FISHING = 101,
    QUEST_SORT_BLACKSMITHING = 121,
    QUEST_SORT_PALADIN = 141,
    QUEST_SORT_MAGE = 161,
    QUEST_SORT_ROGUE = 162,
    QUEST_SORT_ALCHEMY = 181,
    QUEST_SORT_LEATHERWORKING = 182,
    QUEST_SORT_ENGINEERING = 201,
    QUEST_SORT_TREASURE_MAP = 221,
    QUEST_SORT_SUNKEN_TEMPLE_OLD = 241,
    QUEST_SORT_HUNTER = 261,
    QUEST_SORT_PRIEST = 262,
    QUEST_SORT_DRUID = 263,
    QUEST_SORT_TAILORING = 264,
    QUEST_SORT_SPECIAL = 284,
    QUEST_SORT_COOKING = 304,
    QUEST_SORT_FIRST_AID = 324,
    QUEST_SORT_LEGENDARY = 344,
    QUEST_SORT_DARKMOON_FAIRE = 364,
    QUEST_SORT_AHN_QIRAJ_WAR = 365,
    QUEST_SORT_LUNAR_FESTIVAL = 366,
    QUEST_SORT_REPUTATION = 367,
    QUEST_SORT_INVASION = 368,
    QUEST_SORT_MIDSUMMER = 369,
    QUEST_SORT_BREWFEST = 370
};

inline uint8_t ClassByQuestSort(int QuestSort)
{
    switch (QuestSort)
    {
    case QUEST_SORT_WARLOCK:        return CLASS_WARLOCK;
    case QUEST_SORT_WARRIOR:        return CLASS_WARRIOR;
    case QUEST_SORT_SHAMAN:         return CLASS_SHAMAN;
    case QUEST_SORT_PALADIN:        return CLASS_PALADIN;
    case QUEST_SORT_MAGE:           return CLASS_MAGE;
    case QUEST_SORT_ROGUE:          return CLASS_ROGUE;
    case QUEST_SORT_HUNTER:         return CLASS_HUNTER;
    case QUEST_SORT_PRIEST:         return CLASS_PRIEST;
    case QUEST_SORT_DRUID:          return CLASS_DRUID;
    }
    return 0;
}

// Data from SpellLine.dbc (1.12.1 checked)
enum SkillType
{
    SKILL_NONE = 0,

    SKILL_FROST = 6,
    SKILL_FIRE = 8,
    SKILL_ARMS = 26,
    SKILL_COMBAT = 38,
    SKILL_SUBTLETY = 39,
    SKILL_POISONS = 40,
    SKILL_SWORDS = 43,
    SKILL_AXES = 44,
    SKILL_BOWS = 45,
    SKILL_GUNS = 46,
    SKILL_BEAST_MASTERY = 50,
    SKILL_SURVIVAL = 51,
    SKILL_MACES = 54,
    SKILL_2H_SWORDS = 55,
    SKILL_HOLY = 56,
    SKILL_SHADOW = 78,
    SKILL_DEFENSE = 95,
    SKILL_LANG_COMMON = 98,
    SKILL_RACIAL_DWARVEN = 101,
    SKILL_LANG_ORCISH = 109,
    SKILL_LANG_DWARVEN = 111,
    SKILL_LANG_DARNASSIAN = 113,
    SKILL_LANG_TAURAHE = 115,
    SKILL_DUAL_WIELD = 118,
    SKILL_RACIAL_TAUREN = 124,
    SKILL_ORC_RACIAL = 125,
    SKILL_RACIAL_NIGHT_ELF = 126,
    SKILL_FIRST_AID = 129,
    SKILL_FERAL_COMBAT = 134,
    SKILL_STAVES = 136,
    SKILL_LANG_THALASSIAN = 137,
    SKILL_LANG_DRACONIC = 138,
    SKILL_LANG_DEMON_TONGUE = 139,
    SKILL_LANG_TITAN = 140,
    SKILL_LANG_OLD_TONGUE = 141,
    SKILL_SURVIVAL2 = 142,
    SKILL_RIDING_HORSE = 148,
    SKILL_RIDING_WOLF = 149,
    SKILL_RIDING_TIGER = 150,
    SKILL_RIDING_RAM = 152,
    SKILL_SWIMING = 155,
    SKILL_2H_MACES = 160,
    SKILL_UNARMED = 162,
    SKILL_MARKSMANSHIP = 163,
    SKILL_BLACKSMITHING = 164,
    SKILL_LEATHERWORKING = 165,
    SKILL_ALCHEMY = 171,
    SKILL_2H_AXES = 172,
    SKILL_DAGGERS = 173,
    SKILL_THROWN = 176,
    SKILL_HERBALISM = 182,
    SKILL_GENERIC_DND = 183,
    SKILL_RETRIBUTION = 184,
    SKILL_COOKING = 185,
    SKILL_MINING = 186,
    SKILL_PET_IMP = 188,
    SKILL_PET_FELHUNTER = 189,
    SKILL_TAILORING = 197,
    SKILL_ENGINEERING = 202,
    SKILL_PET_SPIDER = 203,
    SKILL_PET_VOIDWALKER = 204,
    SKILL_PET_SUCCUBUS = 205,
    SKILL_PET_INFERNAL = 206,
    SKILL_PET_DOOMGUARD = 207,
    SKILL_PET_WOLF = 208,
    SKILL_PET_CAT = 209,
    SKILL_PET_BEAR = 210,
    SKILL_PET_BOAR = 211,
    SKILL_PET_CROCILISK = 212,
    SKILL_PET_CARRION_BIRD = 213,
    SKILL_PET_CRAB = 214,
    SKILL_PET_GORILLA = 215,
    SKILL_PET_RAPTOR = 217,
    SKILL_PET_TALLSTRIDER = 218,
    SKILL_RACIAL_UNDED = 220,
    SKILL_CROSSBOWS = 226,
    SKILL_WANDS = 228,
    SKILL_POLEARMS = 229,
    SKILL_PET_SCORPID = 236,
    SKILL_ARCANE = 237,
    SKILL_PET_TURTLE = 251,
    SKILL_ASSASSINATION = 253,
    SKILL_FURY = 256,
    SKILL_PROTECTION = 257,
    SKILL_BEAST_TRAINING = 261,
    SKILL_PROTECTION2 = 267,
    SKILL_PET_TALENTS = 270,
    SKILL_PLATE_MAIL = 293,
    SKILL_LANG_GNOMISH = 313,
    SKILL_LANG_TROLL = 315,
    SKILL_ENCHANTING = 333,
    SKILL_DEMONOLOGY = 354,
    SKILL_AFFLICTION = 355,
    SKILL_FISHING = 356,
    SKILL_ENHANCEMENT = 373,
    SKILL_RESTORATION = 374,
    SKILL_ELEMENTAL_COMBAT = 375,
    SKILL_SKINNING = 393,
    SKILL_MAIL = 413,
    SKILL_LEATHER = 414,
    SKILL_CLOTH = 415,
    SKILL_SHIELD = 433,
    SKILL_FIST_WEAPONS = 473,
    SKILL_RIDING_RAPTOR = 533,
    SKILL_RIDING_MECHANOSTRIDER = 553,
    SKILL_RIDING_UNDEAD_HORSE = 554,
    SKILL_RESTORATION2 = 573,
    SKILL_BALANCE = 574,
    SKILL_DESTRUCTION = 593,
    SKILL_HOLY2 = 594,
    SKILL_DISCIPLINE = 613,
    SKILL_LOCKPICKING = 633,
    SKILL_PET_BAT = 653,
    SKILL_PET_HYENA = 654,
    SKILL_PET_OWL = 655,
    SKILL_PET_WIND_SERPENT = 656,
    SKILL_LANG_GUTTERSPEAK = 673,
    SKILL_RIDING_KODO = 713,
    SKILL_RACIAL_TROLL = 733,
    SKILL_RACIAL_GNOME = 753,
    SKILL_RACIAL_HUMAN = 754,
    SKILL_PET_EVENT_RC = 758,
    SKILL_RIDING = 762,
};

#define MAX_SKILL_TYPE               763

inline SkillType SkillByLockType(LockType locktype)
{
    switch (locktype)
    {
    case LOCKTYPE_PICKLOCK:    return SKILL_LOCKPICKING;
    case LOCKTYPE_HERBALISM:   return SKILL_HERBALISM;
    case LOCKTYPE_MINING:      return SKILL_MINING;
    case LOCKTYPE_FISHING:     return SKILL_FISHING;
    default: break;
    }
    return SKILL_NONE;
}

inline unsigned int SkillByQuestSort(int QuestSort)
{
    switch (QuestSort)
    {
    case QUEST_SORT_HERBALISM:      return SKILL_HERBALISM;
    case QUEST_SORT_FISHING:        return SKILL_FISHING;
    case QUEST_SORT_BLACKSMITHING:  return SKILL_BLACKSMITHING;
    case QUEST_SORT_ALCHEMY:        return SKILL_ALCHEMY;
    case QUEST_SORT_LEATHERWORKING: return SKILL_LEATHERWORKING;
    case QUEST_SORT_ENGINEERING:    return SKILL_ENGINEERING;
    case QUEST_SORT_TAILORING:      return SKILL_TAILORING;
    case QUEST_SORT_COOKING:        return SKILL_COOKING;
    case QUEST_SORT_FIRST_AID:      return SKILL_FIRST_AID;
    }
    return 0;
}

enum SkillCategory
{
    SKILL_CATEGORY_ATTRIBUTES = 5,
    SKILL_CATEGORY_WEAPON = 6,
    SKILL_CATEGORY_CLASS = 7,
    SKILL_CATEGORY_ARMOR = 8,
    SKILL_CATEGORY_SECONDARY = 9,                       // secondary professions
    SKILL_CATEGORY_LANGUAGES = 10,
    SKILL_CATEGORY_PROFESSION = 11,                      // primary professions
    SKILL_CATEGORY_GENERIC = 12
};

enum UnitDynFlags
{
    UNIT_DYNFLAG_NONE = 0x0000,
    UNIT_DYNFLAG_LOOTABLE = 0x0001,
    UNIT_DYNFLAG_TRACK_UNIT = 0x0002,
    UNIT_DYNFLAG_TAPPED = 0x0004,       // Lua_UnitIsTapped - Indicates the target as grey for the client.
    UNIT_DYNFLAG_ROOTED = 0x0008,
    UNIT_DYNFLAG_SPECIALINFO = 0x0010,
    UNIT_DYNFLAG_DEAD = 0x0020,
};

enum CorpseDynFlags
{
    CORPSE_DYNFLAG_LOOTABLE = 0x0001
};

// Passive Spell codes explicit used in code
#define SPELL_ID_PASSIVE_BATTLE_STANCE          2457
#define SPELL_ID_PASSIVE_RESURRECTION_SICKNESS  15007
#define SPELL_ID_WEAPON_SWITCH_COOLDOWN_1_5s    6119
#define SPELL_ID_WEAPON_SWITCH_COOLDOWN_1_0s    6123

enum WeatherType
{
    WEATHER_TYPE_FINE = 0,
    WEATHER_TYPE_RAIN = 1,
    WEATHER_TYPE_SNOW = 2,
    WEATHER_TYPE_STORM = 3
};

#define MAX_WEATHER_TYPE 4

enum ChatMsg
{
    CHAT_MSG_ADDON = 0xFFFFFFFF,
    CHAT_MSG_SAY = 0x00,
    CHAT_MSG_PARTY = 0x01,
    CHAT_MSG_RAID = 0x02,
    CHAT_MSG_GUILD = 0x03,
    CHAT_MSG_OFFICER = 0x04,
    CHAT_MSG_YELL = 0x05,
    CHAT_MSG_WHISPER = 0x06,
    CHAT_MSG_WHISPER_INFORM = 0x07,
    CHAT_MSG_EMOTE = 0x08,
    CHAT_MSG_TEXT_EMOTE = 0x09,
    CHAT_MSG_SYSTEM = 0x0A,
    CHAT_MSG_MONSTER_SAY = 0x0B,
    CHAT_MSG_MONSTER_YELL = 0x0C,
    CHAT_MSG_MONSTER_EMOTE = 0x0D,
    CHAT_MSG_CHANNEL = 0x0E,
    CHAT_MSG_CHANNEL_JOIN = 0x0F,
    CHAT_MSG_CHANNEL_LEAVE = 0x10,
    CHAT_MSG_CHANNEL_LIST = 0x11,
    CHAT_MSG_CHANNEL_NOTICE = 0x12,
    CHAT_MSG_CHANNEL_NOTICE_USER = 0x13,
    CHAT_MSG_AFK = 0x14,
    CHAT_MSG_DND = 0x15,
    CHAT_MSG_IGNORED = 0x16,
    CHAT_MSG_SKILL = 0x17,
    CHAT_MSG_LOOT = 0x18,
    CHAT_MSG_MONSTER_WHISPER = 0x1A,
    CHAT_MSG_BG_SYSTEM_NEUTRAL = 0x52,
    CHAT_MSG_BG_SYSTEM_ALLIANCE = 0x53,
    CHAT_MSG_BG_SYSTEM_HORDE = 0x54,
    CHAT_MSG_RAID_LEADER = 0x57,
    CHAT_MSG_RAID_WARNING = 0x58,
    CHAT_MSG_RAID_BOSS_WHISPER = 0x59,
    CHAT_MSG_RAID_BOSS_EMOTE = 0x5A,
    CHAT_MSG_BATTLEGROUND = 0x5C,
    CHAT_MSG_BATTLEGROUND_LEADER = 0x5D,

    // [-ZERO] Need find correct values
    // CHAT_MSG_REPLY                  = 0x09,
    CHAT_MSG_MONSTER_PARTY = 0x30, // 0x0D, just selected some free random value for avoid duplicates with really existed values
    // CHAT_MSG_MONEY                  = 0x1C,
    // CHAT_MSG_OPENING                = 0x1D,
    // CHAT_MSG_TRADESKILLS            = 0x1E,
    // CHAT_MSG_PET_INFO               = 0x1F,
    // CHAT_MSG_COMBAT_MISC_INFO       = 0x20,
    // CHAT_MSG_COMBAT_XP_GAIN         = 0x21,
    // CHAT_MSG_COMBAT_HONOR_GAIN      = 0x22,
    // CHAT_MSG_COMBAT_FACTION_CHANGE  = 0x23,
    // CHAT_MSG_FILTERED               = 0x2B,
    // CHAT_MSG_RESTRICTED             = 0x2E,
};

#define MAX_CHAT_MSG_TYPE 0x5E

enum ChatLinkColors
{
    CHAT_LINK_COLOR_TALENT = 0xff4e96f7,   // blue
    CHAT_LINK_COLOR_SPELL = 0xff71d5ff,   // bright blue
    CHAT_LINK_COLOR_ENCHANT = 0xffffd000,   // orange
};

// Values from ItemPetFood (power of (value-1) used for compare with CreatureFamilyEntry.petDietMask
enum PetDiet
{
    PET_DIET_MEAT = 1,
    PET_DIET_FISH = 2,
    PET_DIET_CHEESE = 3,
    PET_DIET_BREAD = 4,
    PET_DIET_FUNGAS = 5,
    PET_DIET_FRUIT = 6,
    PET_DIET_RAW_MEAT = 7,
    PET_DIET_RAW_FISH = 8
};

#define MAX_PET_DIET 9

#define CHAIN_SPELL_JUMP_RADIUS 10

// Max values for Guild
#define GUILD_EVENTLOG_MAX_RECORDS  100
#define GUILD_RANKS_MIN_COUNT       5
#define GUILD_RANKS_MAX_COUNT       10

enum AiReaction
{
    AI_REACTION_ALERT = 0,                               // pre-aggro (used in client packet handler)
    AI_REACTION_FRIENDLY = 1,                               // (NOT used in client packet handler)
    AI_REACTION_HOSTILE = 2,                               // sent on every attack, triggers aggro sound (used in client packet handler)
    AI_REACTION_AFRAID = 3,                               // seen for polymorph (when AI not in control of self?) (NOT used in client packet handler)
    AI_REACTION_DESTROY = 4,                               // used on object destroy (NOT used in client packet handler)
};

// Diminishing Returns Types
enum DiminishingReturnsType
{
    DRTYPE_NONE = 0,                                // this spell is not diminished, but may have limited it's duration to 10s
    DRTYPE_PLAYER = 1,                                // this spell is diminished only when applied on players
    DRTYPE_ALL = 2                                 // this spell is diminished in every case
};

// Diminishing Return Groups
enum DiminishingGroup
{
    // Common Groups
    DIMINISHING_NONE,
    DIMINISHING_CONTROL_STUN,                               // Player Controlled stuns
    DIMINISHING_TRIGGER_STUN,                               // By aura proced stuns, usualy chance on hit talents
    DIMINISHING_SLEEP,
    DIMINISHING_CONTROL_ROOT,                               // Immobilizing effects from casted spells
    DIMINISHING_TRIGGER_ROOT,                               // Immobilizing effects from triggered spells like Frostbite
    DIMINISHING_FEAR,                                       // Non-warlock fears
    DIMINISHING_CHARM,
    // Mage Specific
    DIMINISHING_POLYMORPH,
    // Rogue Specific
    DIMINISHING_KIDNEYSHOT,                                 // Kidney Shot is not diminished with Cheap Shot
    DIMINISHING_BLIND,
    // Warlock Specific
    DIMINISHING_DEATHCOIL,                                  // Death Coil Diminish only with another Death Coil
    DIMINISHING_WARLOCK_FEAR,                               // Also with Sedduction
    // Shared Class Specific
    DIMINISHING_DISARM,                                     // From 2.3.0
    DIMINISHING_SILENCE,                                    // From 2.3.0
    DIMINISHING_FREEZE,                                     // Hunter's Freezing Trap
    DIMINISHING_KNOCKOUT,                                   // Also with Sap, all Knockout mechanics are here
    DIMINISHING_BANISH,
    // Other
    // Don't Diminish, but limit duration to 10s
    DIMINISHING_LIMITONLY
};

enum SummonType
{
    SUMMON_TYPE_CRITTER = 41,
    SUMMON_TYPE_GUARDIAN = 61,
    SUMMON_TYPE_TOTEM_SLOT1 = 63,
    SUMMON_TYPE_WILD = 64,
    SUMMON_TYPE_POSESSED = 65,
    SUMMON_TYPE_DEMON = 66,
    SUMMON_TYPE_SUMMON = 67,
    SUMMON_TYPE_TOTEM_SLOT2 = 81,
    SUMMON_TYPE_TOTEM_SLOT3 = 82,
    SUMMON_TYPE_TOTEM_SLOT4 = 83,
    SUMMON_TYPE_TOTEM = 121,
    SUMMON_TYPE_UNKNOWN3 = 181,
    SUMMON_TYPE_UNKNOWN4 = 187,
    SUMMON_TYPE_UNKNOWN1 = 247,
    SUMMON_TYPE_CRITTER2 = 407,
    SUMMON_TYPE_CRITTER3 = 307,
    SUMMON_TYPE_UNKNOWN5 = 409,
    SUMMON_TYPE_UNKNOWN2 = 427,
    SUMMON_TYPE_POSESSED2 = 428
};

enum InstanceResetMethod
{
    INSTANCE_RESET_ALL,
    INSTANCE_RESET_GLOBAL,
    INSTANCE_RESET_GROUP_DISBAND,
    INSTANCE_RESET_GROUP_JOIN,
    INSTANCE_RESET_RESPAWN_DELAY                            // called from reset scheduler for request reset at map unload when map loaded at reset attempt for normal dungeon
};

// byte flags  value (UNIT_FIELD_BYTES_1,2) (SpellShapeshiftForm.dbc, checked for 1.12.1)
enum ShapeshiftForm
{
    FORM_NONE = 0x00,
    FORM_CAT = 0x01,
    FORM_TREE = 0x02,
    FORM_TRAVEL = 0x03,
    FORM_AQUA = 0x04,
    FORM_BEAR = 0x05,
    FORM_AMBIENT = 0x06,
    FORM_GHOUL = 0x07,
    FORM_DIREBEAR = 0x08,
    FORM_CREATUREBEAR = 0x0E,
    FORM_CREATURECAT = 0x0F,
    FORM_GHOSTWOLF = 0x10,
    FORM_BATTLESTANCE = 0x11,
    FORM_DEFENSIVESTANCE = 0x12,
    FORM_BERSERKERSTANCE = 0x13,
    FORM_SHADOW = 0x1C,
    FORM_STEALTH = 0x1E,
    FORM_MOONKIN = 0x1F,
    FORM_SPIRITOFREDEMPTION = 0x20
};

enum ResponseCodes
{
    RESPONSE_SUCCESS = 0x00,
    RESPONSE_FAILURE = 0x01,
    RESPONSE_CANCELLED = 0x02,
    RESPONSE_DISCONNECTED = 0x03,
    RESPONSE_FAILED_TO_CONNECT = 0x04,
    RESPONSE_CONNECTED = 0x05,
    RESPONSE_VERSION_MISMATCH = 0x06,

    CSTATUS_CONNECTING = 0x07,
    CSTATUS_NEGOTIATING_SECURITY = 0x08,
    CSTATUS_NEGOTIATION_COMPLETE = 0x09,
    CSTATUS_NEGOTIATION_FAILED = 0x0A,
    CSTATUS_AUTHENTICATING = 0x0B,

    AUTH_OK = 0x0C,
    AUTH_FAILED = 0x0D,
    AUTH_REJECT = 0x0E,
    AUTH_BAD_SERVER_PROOF = 0x0F,
    AUTH_UNAVAILABLE = 0x10,
    AUTH_SYSTEM_ERROR = 0x11,
    AUTH_BILLING_ERROR = 0x12,
    AUTH_BILLING_EXPIRED = 0x13,
    AUTH_VERSION_MISMATCH = 0x14,
    AUTH_UNKNOWN_ACCOUNT = 0x15,
    AUTH_INCORRECT_PASSWORD = 0x16,
    AUTH_SESSION_EXPIRED = 0x17,
    AUTH_SERVER_SHUTTING_DOWN = 0x18,
    AUTH_ALREADY_LOGGING_IN = 0x19,
    AUTH_LOGIN_SERVER_NOT_FOUND = 0x1A,
    AUTH_WAIT_QUEUE = 0x1B,
    AUTH_BANNED = 0x1C,
    AUTH_ALREADY_ONLINE = 0x1D,
    AUTH_NO_TIME = 0x1E,
    AUTH_DB_BUSY = 0x1F,
    AUTH_SUSPENDED = 0x20,
    AUTH_PARENTAL_CONTROL = 0x21,

    REALM_LIST_IN_PROGRESS = 0x22,
    REALM_LIST_SUCCESS = 0x23,
    REALM_LIST_FAILED = 0x24,
    REALM_LIST_INVALID = 0x25,
    REALM_LIST_REALM_NOT_FOUND = 0x26,

    ACCOUNT_CREATE_IN_PROGRESS = 0x27,
    ACCOUNT_CREATE_SUCCESS = 0x28,
    ACCOUNT_CREATE_FAILED = 0x29,

    CHAR_LIST_RETRIEVING = 0x2A,
    CHAR_LIST_RETRIEVED = 0x2B,
    CHAR_LIST_FAILED = 0x2C,

    CHAR_CREATE_IN_PROGRESS = 0x2D,
    CHAR_CREATE_SUCCESS = 0x2E,
    CHAR_CREATE_ERROR = 0x2F,
    CHAR_CREATE_FAILED = 0x30,
    CHAR_CREATE_NAME_IN_USE = 0x31,
    CHAR_CREATE_DISABLED = 0x32,
    CHAR_CREATE_PVP_TEAMS_VIOLATION = 0x33,
    CHAR_CREATE_SERVER_LIMIT = 0x34,
    CHAR_CREATE_ACCOUNT_LIMIT = 0x35,
    CHAR_CREATE_SERVER_QUEUE = 0x36,
    CHAR_CREATE_ONLY_EXISTING = 0x37,

    CHAR_DELETE_IN_PROGRESS = 0x38,
    CHAR_DELETE_SUCCESS = 0x39,
    CHAR_DELETE_FAILED = 0x3A,
    CHAR_DELETE_FAILED_LOCKED_FOR_TRANSFER = 0x3B,

    CHAR_LOGIN_IN_PROGRESS = 0x3C,
    CHAR_LOGIN_SUCCESS = 0x3D,
    CHAR_LOGIN_NO_WORLD = 0x3E,
    CHAR_LOGIN_DUPLICATE_CHARACTER = 0x3F,
    CHAR_LOGIN_NO_INSTANCES = 0x40,
    CHAR_LOGIN_FAILED = 0x41,
    CHAR_LOGIN_DISABLED = 0x42,
    CHAR_LOGIN_NO_CHARACTER = 0x43,
    CHAR_LOGIN_LOCKED_FOR_TRANSFER = 0x44,

    CHAR_NAME_NO_NAME = 0x45,
    CHAR_NAME_TOO_SHORT = 0x46,
    CHAR_NAME_TOO_LONG = 0x47,
    CHAR_NAME_INVALID_CHARACTER = 0x48,
    CHAR_NAME_MIXED_LANGUAGES = 0x49,
    CHAR_NAME_PROFANE = 0x4A,
    CHAR_NAME_RESERVED = 0x4B,
    CHAR_NAME_INVALID_APOSTROPHE = 0x4C,
    CHAR_NAME_MULTIPLE_APOSTROPHES = 0x4D,
    CHAR_NAME_THREE_CONSECUTIVE = 0x4E,
    CHAR_NAME_INVALID_SPACE = 0x4F,
    CHAR_NAME_CONSECUTIVE_SPACES = 0x50,
    CHAR_NAME_FAILURE = 0x51,
    CHAR_NAME_SUCCESS = 0x52,
};

/// Ban function modes
enum BanMode
{
    BAN_ACCOUNT,
    BAN_CHARACTER,
    BAN_IP
};

/// Ban function return codes
enum BanReturn
{
    BAN_SUCCESS,
    BAN_SYNTAX_ERROR,
    BAN_NOTFOUND
};

// indexes of BattlemasterList.dbc
enum BattleGroundTypeId
{
    BATTLEGROUND_TYPE_NONE = 0,
    BATTLEGROUND_AV = 1,
    BATTLEGROUND_WS = 2,
    BATTLEGROUND_AB = 3,
};
#define MAX_BATTLEGROUND_TYPE_ID 4

inline BattleGroundTypeId GetBattleGroundTypeIdByMapId(unsigned int mapId)
{
    switch (mapId)
    {
    case 30:    return BATTLEGROUND_AV;
    case 489:   return BATTLEGROUND_WS;
    case 529:   return BATTLEGROUND_AB;
    default:    return BATTLEGROUND_TYPE_NONE;
    }
}

inline unsigned int GetBattleGrounMapIdByTypeId(BattleGroundTypeId bgTypeId)
{
    switch (bgTypeId)
    {
    case BATTLEGROUND_AV:   return 30;
    case BATTLEGROUND_WS:   return 489;
    case BATTLEGROUND_AB:   return 529;
    default:                return 0;   // none
    }

    // impossible, just make compiler happy
    return 0;
}

enum MailResponseType
{
    MAIL_SEND = 0,
    MAIL_MONEY_TAKEN = 1,
    MAIL_ITEM_TAKEN = 2,
    MAIL_RETURNED_TO_SENDER = 3,
    MAIL_DELETED = 4,
    MAIL_MADE_PERMANENT = 5
};

enum MailResponseResult
{
    MAIL_OK = 0,
    MAIL_ERR_EQUIP_ERROR = 1,
    MAIL_ERR_CANNOT_SEND_TO_SELF = 2,
    MAIL_ERR_NOT_ENOUGH_MONEY = 3,
    MAIL_ERR_RECIPIENT_NOT_FOUND = 4,
    MAIL_ERR_NOT_YOUR_TEAM = 5,
    MAIL_ERR_INTERNAL_ERROR = 6,
    MAIL_ERR_DISABLED_FOR_TRIAL_ACC = 14,
    MAIL_ERR_RECIPIENT_CAP_REACHED = 15,
    //in SMSG_SEND_MAIL_RESULT, 7-13 and 16+: "Mail database error"
    MAIL_ERR_CANT_SEND_WRAPPED_COD = 16,
    MAIL_ERR_MAIL_AND_CHAT_SUSPENDED = 17,
    MAIL_ERR_TOO_MANY_ATTACHMENTS = 18,
    MAIL_ERR_MAIL_ATTACHMENT_INVALID = 19,
};

// reasons for why pet tame may fail
// in fact, these are also used elsewhere
enum PetTameFailureReason
{
    PETTAME_INVALIDCREATURE = 1,
    PETTAME_TOOMANY = 2,
    PETTAME_CREATUREALREADYOWNED = 3,
    PETTAME_NOTTAMEABLE = 4,
    PETTAME_ANOTHERSUMMONACTIVE = 5,
    PETTAME_UNITSCANTTAME = 6,
    PETTAME_NOPETAVAILABLE = 7,                    // not used in taming
    PETTAME_INTERNALERROR = 8,
    PETTAME_TOOHIGHLEVEL = 9,
    PETTAME_DEAD = 10,                   // not used in taming
    PETTAME_NOTDEAD = 11,                   // not used in taming
    PETTAME_UNKNOWNERROR = 12
};

/**
 * These are the different totem types that are available.
 * Stored in SummonProperties.dbc with slot+1 values
 * \see Totem
 * \see Unit::GetTotemGuid
 */
enum TotemSlot
{
    TOTEM_SLOT_FIRE = 0,
    TOTEM_SLOT_EARTH = 1,
    TOTEM_SLOT_WATER = 2,
    TOTEM_SLOT_AIR = 3,
};

#define TOTEM_SLOT_NONE 255                                 // custom value for no slot case

#define MAX_TOTEM_SLOT  4

enum TradeStatus
{
    TRADE_STATUS_BUSY = 0,
    TRADE_STATUS_BEGIN_TRADE = 1,
    TRADE_STATUS_OPEN_WINDOW = 2,
    TRADE_STATUS_TRADE_CANCELED = 3,
    TRADE_STATUS_TRADE_ACCEPT = 4,
    TRADE_STATUS_BUSY_2 = 5,
    TRADE_STATUS_NO_TARGET = 6,
    TRADE_STATUS_BACK_TO_TRADE = 7,
    TRADE_STATUS_TRADE_COMPLETE = 8,
    TRADE_STATUS_TRADE_REJECTED = 9,
    TRADE_STATUS_TARGET_TO_FAR = 10,
    TRADE_STATUS_WRONG_FACTION = 11,
    TRADE_STATUS_CLOSE_WINDOW = 12,
    TRADE_STATUS_UNKNOWN_13 = 13,                       // handled with TRADE_STATUS_TRADE_CANCELED
    TRADE_STATUS_IGNORE_YOU = 14,
    TRADE_STATUS_YOU_STUNNED = 15,
    TRADE_STATUS_TARGET_STUNNED = 16,
    TRADE_STATUS_YOU_DEAD = 17,
    TRADE_STATUS_TARGET_DEAD = 18,
    TRADE_STATUS_YOU_LOGOUT = 19,
    TRADE_STATUS_TARGET_LOGOUT = 20,
    TRADE_STATUS_TRIAL_ACCOUNT = 21,                       // Trial accounts can not perform that action
    TRADE_STATUS_WRONG_REALM = 22                        // You can only trade conjured items... (cross realm BG related).
};

enum WorldStateType
{
    WORLD_STATE_REMOVE = 0,
    WORLD_STATE_ADD = 1
};

enum ActivateTaxiReply
{
    ERR_TAXIOK = 0,
    ERR_TAXIUNSPECIFIEDSERVERERROR = 1,
    ERR_TAXINOSUCHPATH = 2,
    ERR_TAXINOTENOUGHMONEY = 3,
    ERR_TAXITOOFARAWAY = 4,
    ERR_TAXINOVENDORNEARBY = 5,
    ERR_TAXINOTVISITED = 6,
    ERR_TAXIPLAYERBUSY = 7,
    ERR_TAXIPLAYERALREADYMOUNTED = 8,
    ERR_TAXIPLAYERSHAPESHIFTED = 9,
    ERR_TAXIPLAYERMOVING = 10,
    ERR_TAXISAMENODE = 11,
    ERR_TAXINOTSTANDING = 12
};

enum AreaLockStatus
{
    AREA_LOCKSTATUS_OK = 0,
    AREA_LOCKSTATUS_UNKNOWN_ERROR = 1,
    AREA_LOCKSTATUS_LEVEL_NOT_EQUAL = 2,
    AREA_LOCKSTATUS_LEVEL_TOO_LOW = 3,
    AREA_LOCKSTATUS_LEVEL_TOO_HIGH = 4,
    AREA_LOCKSTATUS_RAID_LOCKED = 5,
    AREA_LOCKSTATUS_QUEST_NOT_COMPLETED = 6,
    AREA_LOCKSTATUS_MISSING_ITEM = 7,
    AREA_LOCKSTATUS_ZONE_IN_COMBAT = 8,
    AREA_LOCKSTATUS_INSTANCE_IS_FULL = 9,
    AREA_LOCKSTATUS_NOT_ALLOWED = 10,
    AREA_LOCKSTATUS_HAS_BIND = 11,
    AREA_LOCKSTATUS_WRONG_TEAM = 12,
    AREA_LOCKSTATUS_PVP_RANK = 100
};

enum TrackedAuraType
{
    TRACK_AURA_TYPE_NOT_TRACKED = 0,        // relation - caster : target is n:m (usual case)
    TRACK_AURA_TYPE_SINGLE_TARGET = 1,        // relation - caster : target is 1:1. Might get stolen
    MAX_TRACKED_AURA_TYPES
};

// we need to stick to 1 version or half of the stuff will work for someone
// others will not and opposite
// will only support 1.12.1 client (build 5875), 1.12.2 client (build 6005) and 1.12.3 client (build 6141)..

#define EXPECTED_MANGOSD_CLIENT_BUILD        {5875, 6005, 6141, 0}
#define EXPECTED_MANGOSD_CLIENT_VERSION      "1.12.x"

// Max creature level (included some bosses and elite)
#define DEFAULT_MAX_CREATURE_LEVEL 65

enum TeleportLocation
{
    TELEPORT_LOCATION_HOMEBIND = 0,
    TELEPORT_LOCATION_BG_ENTRY_POINT = 1,
};

/**
 * Some statuses that can be sent with the \ref OpcodesList::SMSG_GM_TICKET_STATUS_UPDATE opcode
 * to change what the client is currently showing about your open ticket.
 * \see WorldSession::SendGMTicketStatusUpdate
 */
enum GMTicketStatus
{
    /**
     * This code is used when the client closed the ticket itself and we shouldn't send an update
     * message to it */
    GM_TICKET_STATUS_DO_NOTHING = -1,
    /** On this client responds by CMSG_GMTICKET_GETTICKET, updating the local ticket copy
    */
    GM_TICKET_STATUS_ASK_UPDATE = 1,
    /** Should close the window in the top right corner telling you that you have a
     * ticket open */
    GM_TICKET_STATUS_CLOSE = 2,
    /** Should close the window telling you you have an open ticket and query you for
     * answers on a survey, how good did the GM perform?
     * \see GMTicket::SaveSurveyData
     */
    GM_TICKET_STATUS_SURVEY = 3
};

/**
 * This denotes the different levels of whisper logging that can be active via configuration, the
 * string for this in the config file is LogWhispers, the config enum is
 * \ref eConfigUInt32Values::CONFIG_UINT32_LOG_WHISPERS and the default value is 1, ie: we only
 * log whispers related to tickets.
 *
 * The database table that everything is logged to is character.character_whispers
 * \see Player::LogWhisper
 */
enum WhisperLoggingLevels
{
    /**
     * When this is the level used no logging of whispers at all is done
     */
    WHISPER_LOGGING_NONE = 0,
    /**
     * When this level is used we log everything related to GM-tickets, ie: when a GM first whispers
     * the holder of a ticket until that ticket is closed
     */
    WHISPER_LOGGING_TICKETS = 1,
    /**
     * This will log all whispers made between players, GM-tickets included
     */
    WHISPER_LOGGING_EVERYTHING = 2
};

/*
    Creature entries for more readable code
*/
enum CreatureEntriesConsts
{
    CREATURE_TAINTED_OOZE = 7092,
    CREATURE_CURSED_OOZE = 7086,
    CREATURE_MUCULENT_OOZE = 6556,
    CREATURE_PRIMAL_OOZE = 6557,
    CREATURE_GLUTINOUS_OOZE = 6559,
};

enum SpellEntriesConsts
{
    SPELL_FILLING_EMPTY_JAR__CURSED_OOZE = 15698,
    SPELL_FILLING_EMPTY_JAR__TAINTED_OOZE = 15699,
    SPELL_FILLING_EMPTY_JAR__PURE_OOZE = 15702, // (Works on  Primal, Muculent and Glutonous Ooze)
    SPELL_GM_FREEZE = 9454,
};

#endif