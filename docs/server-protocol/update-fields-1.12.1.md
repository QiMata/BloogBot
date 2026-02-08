# Update Fields — WoW 1.12.1 (Build 5875)

> Auto-generated from `E:\repos\MaNGOS\source\src\game\Objects\UpdateFields_1_12_1.h`

## Overview

Every WoW object has a **descriptor block** — a flat array of 32-bit values at fixed indices. The server sends these fields via `SMSG_UPDATE_OBJECT` packets. Each field is one of:

| Type | Storage | Notes |
|------|---------|-------|
| `INT` | `uint32` | Integer value |
| `FLOAT` | `float` | IEEE 754 float, same 4 bytes |
| `GUID` | 2 × `uint32` | Low word at index N, high word at N+1 |
| `BYTES` | `uint32` | 4 packed bytes (e.g., race/class/gender/powertype) |
| `TWO_SHORT` | `uint32` | 2 packed `uint16` values |

**Visibility flags** control who receives each field:
- `PUBLIC` — sent to all nearby players
- `PRIVATE` — only sent to the owning player
- `OWNER_ONLY` — sent to owner (for pets/items)
- `GROUP_ONLY` — sent to party members
- `DYNAMIC` — visibility varies by context (e.g., health shown to all, but percentage-based for non-owners)
- `SPECIAL_INFO` — sent when inspecting

Each object type inherits from the previous: Object → Item/Unit → Container/Player.

---

## 1. Object Fields (OBJECT_END = 0x6)

Base fields shared by **all** object types.

| Index | Hex | Name | Type | Size | Flags | Description |
|-------|-----|------|------|------|-------|-------------|
| 0 | 0x000 | OBJECT_FIELD_GUID | GUID | 2 | PUBLIC | Object's unique 64-bit GUID |
| 2 | 0x002 | OBJECT_FIELD_TYPE | INT | 1 | PUBLIC | Object type bitmask |
| 3 | 0x003 | OBJECT_FIELD_ENTRY | INT | 1 | PUBLIC | Template entry ID (creature_template.entry, etc.) |
| 4 | 0x004 | OBJECT_FIELD_SCALE_X | FLOAT | 1 | PUBLIC | Visual scale factor (1.0 = normal) |
| 5 | 0x005 | OBJECT_FIELD_PADDING | INT | 1 | NONE | Unused padding |

---

## 2. Item Fields (ITEM_END = 0x30)

Starts at `OBJECT_END` (index 6). Only for Item objects.

| Index | Hex | Name | Type | Size | Flags | Description |
|-------|-----|------|------|------|-------|-------------|
| 6 | 0x006 | ITEM_FIELD_OWNER | GUID | 2 | PUBLIC | Player who owns this item |
| 8 | 0x008 | ITEM_FIELD_CONTAINED | GUID | 2 | PUBLIC | Container (bag) GUID holding this item |
| 10 | 0x00A | ITEM_FIELD_CREATOR | GUID | 2 | PUBLIC | Player who crafted/created this item |
| 12 | 0x00C | ITEM_FIELD_GIFTCREATOR | GUID | 2 | PUBLIC | Player who gifted this item |
| 14 | 0x00E | ITEM_FIELD_STACK_COUNT | INT | 1 | OWNER+UNK2 | Number of items in stack |
| 15 | 0x00F | ITEM_FIELD_DURATION | INT | 1 | OWNER+UNK2 | Remaining duration (ms) |
| 16 | 0x010 | ITEM_FIELD_SPELL_CHARGES | INT | 5 | OWNER+UNK2 | Charges for up to 5 item spells |
| 21 | 0x015 | ITEM_FIELD_FLAGS | INT | 1 | PUBLIC | Item flags (bound, unlocked, etc.) |
| 22 | 0x016 | ITEM_FIELD_ENCHANTMENT | INT | 21 | PUBLIC | 7 enchantment slots × 3 fields (id, duration, charges) |
| 43 | 0x02B | ITEM_FIELD_PROPERTY_SEED | INT | 1 | PUBLIC | Random property seed |
| 44 | 0x02C | ITEM_FIELD_RANDOM_PROPERTIES_ID | INT | 1 | PUBLIC | Random property/suffix ID |
| 45 | 0x02D | ITEM_FIELD_ITEM_TEXT_ID | INT | 1 | OWNER | Text ID for readable items |
| 46 | 0x02E | ITEM_FIELD_DURABILITY | INT | 1 | OWNER+UNK2 | Current durability |
| 47 | 0x02F | ITEM_FIELD_MAXDURABILITY | INT | 1 | OWNER+UNK2 | Maximum durability |

---

## 3. Container Fields (CONTAINER_END = 0x74)

Starts at `ITEM_END` (index 0x30). Containers inherit all Item fields.

| Index | Hex | Name | Type | Size | Flags | Description |
|-------|-----|------|------|------|-------|-------------|
| 48 | 0x030 | CONTAINER_FIELD_NUM_SLOTS | INT | 1 | PUBLIC | Number of bag slots |
| 49 | 0x031 | CONTAINER_ALIGN_PAD | BYTES | 1 | NONE | Alignment padding |
| 50 | 0x032 | CONTAINER_FIELD_SLOT_1 | GUID | 72 | PUBLIC | 36 slot GUIDs (2 indices each) |

---

## 4. Unit Fields (UNIT_END = 0xBC)

Starts at `OBJECT_END` (index 6). Shared by all Units (NPCs and Players).

| Index | Hex | Name | Type | Size | Flags | Description |
|-------|-----|------|------|------|-------|-------------|
| 6 | 0x006 | UNIT_FIELD_CHARM | GUID | 2 | PUBLIC | GUID of charmed unit |
| 8 | 0x008 | UNIT_FIELD_SUMMON | GUID | 2 | PUBLIC | GUID of summoned unit (pet) |
| 10 | 0x00A | UNIT_FIELD_CHARMEDBY | GUID | 2 | PUBLIC | GUID of charmer |
| 12 | 0x00C | UNIT_FIELD_SUMMONEDBY | GUID | 2 | PUBLIC | GUID of summoner |
| 14 | 0x00E | UNIT_FIELD_CREATEDBY | GUID | 2 | PUBLIC | GUID of creator |
| 16 | 0x010 | UNIT_FIELD_TARGET | GUID | 2 | PUBLIC | Current target GUID |
| 18 | 0x012 | UNIT_FIELD_PERSUADED | GUID | 2 | PUBLIC | Persuaded-by GUID |
| 20 | 0x014 | UNIT_FIELD_CHANNEL_OBJECT | GUID | 2 | PUBLIC | Channeling target GUID |
| 22 | 0x016 | UNIT_FIELD_HEALTH | INT | 1 | DYNAMIC | Current health |
| 23 | 0x017 | UNIT_FIELD_POWER1 | INT | 1 | PUBLIC | Mana |
| 24 | 0x018 | UNIT_FIELD_POWER2 | INT | 1 | PUBLIC | Rage (÷10 for display) |
| 25 | 0x019 | UNIT_FIELD_POWER3 | INT | 1 | PUBLIC | Focus |
| 26 | 0x01A | UNIT_FIELD_POWER4 | INT | 1 | PUBLIC | Energy |
| 27 | 0x01B | UNIT_FIELD_POWER5 | INT | 1 | PUBLIC | Happiness (pet) |
| 28 | 0x01C | UNIT_FIELD_MAXHEALTH | INT | 1 | DYNAMIC | Maximum health |
| 29 | 0x01D | UNIT_FIELD_MAXPOWER1 | INT | 1 | PUBLIC | Max mana |
| 30 | 0x01E | UNIT_FIELD_MAXPOWER2 | INT | 1 | PUBLIC | Max rage |
| 31 | 0x01F | UNIT_FIELD_MAXPOWER3 | INT | 1 | PUBLIC | Max focus |
| 32 | 0x020 | UNIT_FIELD_MAXPOWER4 | INT | 1 | PUBLIC | Max energy |
| 33 | 0x021 | UNIT_FIELD_MAXPOWER5 | INT | 1 | PUBLIC | Max happiness |
| 34 | 0x022 | UNIT_FIELD_LEVEL | INT | 1 | PUBLIC | Unit level |
| 35 | 0x023 | UNIT_FIELD_FACTIONTEMPLATE | INT | 1 | PUBLIC | Faction template ID |
| 36 | 0x024 | UNIT_FIELD_BYTES_0 | BYTES | 1 | PUBLIC | `[race, class, gender, powerType]` |
| 37 | 0x025 | UNIT_VIRTUAL_ITEM_SLOT_DISPLAY | INT | 3 | PUBLIC | Visual weapon display IDs (main/off/ranged) |
| 40 | 0x028 | UNIT_VIRTUAL_ITEM_INFO | BYTES | 6 | PUBLIC | Virtual item info (class, subclass, etc.) |
| 46 | 0x02E | UNIT_FIELD_FLAGS | INT | 1 | PUBLIC | Unit flags (in combat, disarmed, etc.) |
| 47 | 0x02F | UNIT_FIELD_AURA | INT | 48 | PUBLIC | Aura spell IDs (48 aura slots) |
| 95 | 0x05F | UNIT_FIELD_AURAFLAGS | BYTES | 6 | PUBLIC | Aura flags (helpful/harmful/passive per slot) |
| 101 | 0x065 | UNIT_FIELD_AURALEVELS | BYTES | 12 | PUBLIC | Caster level per aura slot |
| 113 | 0x071 | UNIT_FIELD_AURAAPPLICATIONS | BYTES | 12 | PUBLIC | Stack count per aura slot |
| 125 | 0x07D | UNIT_FIELD_AURASTATE | INT | 1 | PUBLIC | Aura state flags (dodge, parry, etc.) |
| 126 | 0x07E | UNIT_FIELD_BASEATTACKTIME | INT | 2 | PUBLIC | Base attack time [main, offhand] (ms) |
| 128 | 0x080 | UNIT_FIELD_RANGEDATTACKTIME | INT | 1 | PRIVATE | Ranged attack time (ms) |
| 129 | 0x081 | UNIT_FIELD_BOUNDINGRADIUS | FLOAT | 1 | PUBLIC | Bounding radius for collision |
| 130 | 0x082 | UNIT_FIELD_COMBATREACH | FLOAT | 1 | PUBLIC | Melee combat reach |
| 131 | 0x083 | UNIT_FIELD_DISPLAYID | INT | 1 | PUBLIC | Current display model ID |
| 132 | 0x084 | UNIT_FIELD_NATIVEDISPLAYID | INT | 1 | PUBLIC | Native/original display model ID |
| 133 | 0x085 | UNIT_FIELD_MOUNTDISPLAYID | INT | 1 | PUBLIC | Mount model ID (0 if not mounted) |
| 134 | 0x086 | UNIT_FIELD_MINDAMAGE | FLOAT | 1 | PRIV+OWNER+SPEC | Minimum melee damage |
| 135 | 0x087 | UNIT_FIELD_MAXDAMAGE | FLOAT | 1 | PRIV+OWNER+SPEC | Maximum melee damage |
| 136 | 0x088 | UNIT_FIELD_MINOFFHANDDAMAGE | FLOAT | 1 | PRIV+OWNER+SPEC | Minimum offhand damage |
| 137 | 0x089 | UNIT_FIELD_MAXOFFHANDDAMAGE | FLOAT | 1 | PRIV+OWNER+SPEC | Maximum offhand damage |
| 138 | 0x08A | UNIT_FIELD_BYTES_1 | BYTES | 1 | PUBLIC | `[standState, petLoyalty, shapeShiftForm, unitFlags_b2]` |
| 139 | 0x08B | UNIT_FIELD_PETNUMBER | INT | 1 | PUBLIC | Pet number |
| 140 | 0x08C | UNIT_FIELD_PET_NAME_TIMESTAMP | INT | 1 | PUBLIC | Pet name timestamp |
| 141 | 0x08D | UNIT_FIELD_PETEXPERIENCE | INT | 1 | OWNER | Pet XP |
| 142 | 0x08E | UNIT_FIELD_PETNEXTLEVELEXP | INT | 1 | OWNER | Pet next level XP |
| 143 | 0x08F | UNIT_DYNAMIC_FLAGS | INT | 1 | DYNAMIC | Dynamic flags (tapped, lootable, tracked, dead) |
| 144 | 0x090 | UNIT_CHANNEL_SPELL | INT | 1 | PUBLIC | Currently channeling spell ID |
| 145 | 0x091 | UNIT_MOD_CAST_SPEED | FLOAT | 1 | PUBLIC | Cast speed modifier (1.0 = normal) |
| 146 | 0x092 | UNIT_CREATED_BY_SPELL | INT | 1 | PUBLIC | Spell ID that created this unit |
| 147 | 0x093 | UNIT_NPC_FLAGS | INT | 1 | PUBLIC | NPC flags (gossip, vendor, trainer, etc.) |
| 148 | 0x094 | UNIT_NPC_EMOTESTATE | INT | 1 | PUBLIC | NPC emote state |
| 149 | 0x095 | UNIT_TRAINING_POINTS | TWO_SHORT | 1 | OWNER | Training points `[spent, total]` |
| 150 | 0x096 | UNIT_FIELD_STAT0 | INT | 1 | PRIV+OWNER | Strength |
| 151 | 0x097 | UNIT_FIELD_STAT1 | INT | 1 | PRIV+OWNER | Agility |
| 152 | 0x098 | UNIT_FIELD_STAT2 | INT | 1 | PRIV+OWNER | Stamina |
| 153 | 0x099 | UNIT_FIELD_STAT3 | INT | 1 | PRIV+OWNER | Intellect |
| 154 | 0x09A | UNIT_FIELD_STAT4 | INT | 1 | PRIV+OWNER | Spirit |
| 155 | 0x09B | UNIT_FIELD_RESISTANCES | INT | 7 | PRIV+OWNER+SPEC | Resistances: `[armor, holy, fire, nature, frost, shadow, arcane]` |
| 162 | 0x0A2 | UNIT_FIELD_BASE_MANA | INT | 1 | PRIV+OWNER | Base mana (before bonuses) |
| 163 | 0x0A3 | UNIT_FIELD_BASE_HEALTH | INT | 1 | PRIV+OWNER | Base health (before bonuses) |
| 164 | 0x0A4 | UNIT_FIELD_BYTES_2 | BYTES | 1 | PUBLIC | `[sheathState, unitBytes2Flags, petFlags, shapeshiftForm]` |
| 165 | 0x0A5 | UNIT_FIELD_ATTACK_POWER | INT | 1 | PRIV+OWNER | Melee attack power |
| 166 | 0x0A6 | UNIT_FIELD_ATTACK_POWER_MODS | TWO_SHORT | 1 | PRIV+OWNER | AP modifier `[positive, negative]` |
| 167 | 0x0A7 | UNIT_FIELD_ATTACK_POWER_MULTIPLIER | FLOAT | 1 | PRIV+OWNER | AP multiplier |
| 168 | 0x0A8 | UNIT_FIELD_RANGED_ATTACK_POWER | INT | 1 | PRIV+OWNER | Ranged attack power |
| 169 | 0x0A9 | UNIT_FIELD_RANGED_ATTACK_POWER_MODS | TWO_SHORT | 1 | PRIV+OWNER | Ranged AP modifier |
| 170 | 0x0AA | UNIT_FIELD_RANGED_ATTACK_POWER_MULTIPLIER | FLOAT | 1 | PRIV+OWNER | Ranged AP multiplier |
| 171 | 0x0AB | UNIT_FIELD_MINRANGEDDAMAGE | FLOAT | 1 | PRIV+OWNER | Minimum ranged damage |
| 172 | 0x0AC | UNIT_FIELD_MAXRANGEDDAMAGE | FLOAT | 1 | PRIV+OWNER | Maximum ranged damage |
| 173 | 0x0AD | UNIT_FIELD_POWER_COST_MODIFIER | INT | 7 | PRIV+OWNER | Power cost mod per school `[phys,holy,fire,nature,frost,shadow,arcane]` |
| 180 | 0x0B4 | UNIT_FIELD_POWER_COST_MULTIPLIER | FLOAT | 7 | PRIV+OWNER | Power cost multiplier per school |
| 187 | 0x0BB | UNIT_FIELD_PADDING | INT | 1 | NONE | Padding |

---

## 5. Player Fields (PLAYER_END = 0x4FC)

Starts at `UNIT_END` (index 0xBC). Total player descriptor size: **1276 fields** (0x4FC).

### 5.1 Basic Info

| Index | Hex | Name | Type | Size | Flags | Description |
|-------|-----|------|------|------|-------|-------------|
| 182 | 0x0B6 | PLAYER_DUEL_ARBITER | GUID | 2 | PUBLIC | Duel arbiter GUID |
| 184 | 0x0B8 | PLAYER_FLAGS | INT | 1 | PUBLIC | Player flags (AFK, DND, GM, ghost, etc.) |
| 185 | 0x0B9 | PLAYER_GUILDID | INT | 1 | PUBLIC | Guild ID |
| 186 | 0x0BA | PLAYER_GUILDRANK | INT | 1 | PUBLIC | Guild rank |
| 187 | 0x0BB | PLAYER_BYTES | BYTES | 1 | PUBLIC | `[skin, face, hairStyle, hairColor]` |
| 188 | 0x0BC | PLAYER_BYTES_2 | BYTES | 1 | PUBLIC | `[facialHair, ?, ?, restState]` |
| 189 | 0x0BD | PLAYER_BYTES_3 | BYTES | 1 | PUBLIC | `[gender, drunkState, ?, pvpRank]` |
| 190 | 0x0BE | PLAYER_DUEL_TEAM | INT | 1 | PUBLIC | Duel team (1 or 2) |
| 191 | 0x0BF | PLAYER_GUILD_TIMESTAMP | INT | 1 | PUBLIC | Guild join timestamp |

### 5.2 Quest Log (20 quest slots × 3 fields each = 60 indices)

Each quest slot has: `_1` = Quest ID (GROUP_ONLY), `_2` = State + Counters (PRIVATE, size 2).

| Index | Name | Description |
|-------|------|-------------|
| 192 (0x0C0) | PLAYER_QUEST_LOG_1_1 | Quest 1 ID |
| 193 (0x0C1) | PLAYER_QUEST_LOG_1_2 | Quest 1 state + kill counts (2 fields) |
| ... | ... | ... |
| 249 (0x0F9) | PLAYER_QUEST_LOG_20_1 | Quest 20 ID |
| 250 (0x0FA) | PLAYER_QUEST_LOG_20_2 | Quest 20 state + kill counts (2 fields) |

### 5.3 Visible Items (19 equipment slots × 12 fields each)

Each visible item slot: CREATOR (GUID, 2), display entries (INT, 8), PROPERTIES (TWO_SHORT, 1), PAD (INT, 1) = 12 fields per slot.

Slots: Head(1), Neck(2), Shoulders(3), Body(4), Chest(5), Waist(6), Legs(7), Feet(8), Wrists(9), Hands(10), Finger1(11), Finger2(12), Trinket1(13), Trinket2(14), Back(15), MainHand(16), OffHand(17), Ranged(18), Tabard(19).

| Starting Index | Name Pattern | Description |
|----------------|-------------|-------------|
| 252 (0x0FC) | PLAYER_VISIBLE_ITEM_1_* | Head slot visible data |
| 264 (0x108) | PLAYER_VISIBLE_ITEM_2_* | Neck slot visible data |
| ... (every +12) | ... | ... |
| 468 (0x1D4) | PLAYER_VISIBLE_ITEM_19_* | Tabard slot visible data |

### 5.4 Inventory Slots

| Index | Hex | Name | Type | Size | Flags | Description |
|-------|-----|------|------|------|-------|-------------|
| 480 | 0x1E0 | PLAYER_FIELD_INV_SLOT_HEAD | GUID | 46 | PRIVATE | 23 inventory slot GUIDs (equipment + bags) |
| 526 | 0x20E | PLAYER_FIELD_PACK_SLOT_1 | GUID | 32 | PRIVATE | 16 backpack slot GUIDs |
| 558 | 0x22E | PLAYER_FIELD_BANK_SLOT_1 | GUID | 48 | PRIVATE | 24 bank slot GUIDs |
| 606 | 0x25E | PLAYER_FIELD_BANKBAG_SLOT_1 | GUID | 12 | PRIVATE | 6 bank bag slot GUIDs |
| 618 | 0x26A | PLAYER_FIELD_VENDORBUYBACK_SLOT_1 | GUID | 24 | PRIVATE | 12 vendor buyback slot GUIDs |
| 642 | 0x282 | PLAYER_FIELD_KEYRING_SLOT_1 | GUID | 64 | PRIVATE | 32 keyring slot GUIDs |

### 5.5 Character Stats & Combat

| Index | Hex | Name | Type | Size | Flags | Description |
|-------|-----|------|------|------|-------|-------------|
| 706 | 0x2C2 | PLAYER_FARSIGHT | GUID | 2 | PRIVATE | Far sight target GUID |
| 708 | 0x2C4 | PLAYER_FIELD_COMBO_TARGET | GUID | 2 | PRIVATE | Combo point target GUID |
| 710 | 0x2C6 | PLAYER_XP | INT | 1 | PRIVATE | Current XP |
| 711 | 0x2C7 | PLAYER_NEXT_LEVEL_XP | INT | 1 | PRIVATE | XP needed for next level |
| 712 | 0x2C8 | PLAYER_SKILL_INFO_1_1 | TWO_SHORT | 384 | PRIVATE | 128 skills × 3 fields (id+flag, value+max, bonus+?) |
| 1096 | 0x448 | PLAYER_CHARACTER_POINTS1 | INT | 1 | PRIVATE | Talent points |
| 1097 | 0x449 | PLAYER_CHARACTER_POINTS2 | INT | 1 | PRIVATE | Skill points (professions) |
| 1098 | 0x44A | PLAYER_TRACK_CREATURES | INT | 1 | PRIVATE | Creature tracking mask |
| 1099 | 0x44B | PLAYER_TRACK_RESOURCES | INT | 1 | PRIVATE | Resource tracking mask |
| 1100 | 0x44C | PLAYER_BLOCK_PERCENTAGE | FLOAT | 1 | PRIVATE | Block % |
| 1101 | 0x44D | PLAYER_DODGE_PERCENTAGE | FLOAT | 1 | PRIVATE | Dodge % |
| 1102 | 0x44E | PLAYER_PARRY_PERCENTAGE | FLOAT | 1 | PRIVATE | Parry % |
| 1103 | 0x44F | PLAYER_CRIT_PERCENTAGE | FLOAT | 1 | PRIVATE | Melee crit % |
| 1104 | 0x450 | PLAYER_RANGED_CRIT_PERCENTAGE | FLOAT | 1 | PRIVATE | Ranged crit % |
| 1105 | 0x451 | PLAYER_EXPLORED_ZONES_1 | BYTES | 64 | PRIVATE | 64 explored zone flags (512 zones) |
| 1169 | 0x491 | PLAYER_REST_STATE_EXPERIENCE | INT | 1 | PRIVATE | Rested XP bonus remaining |
| 1170 | 0x492 | PLAYER_FIELD_COINAGE | INT | 1 | PRIVATE | Copper (gold = coinage ÷ 10000) |

### 5.6 Stat Bonuses

| Index | Hex | Name | Type | Size | Flags | Description |
|-------|-----|------|------|------|-------|-------------|
| 1171 | 0x493 | PLAYER_FIELD_POSSTAT0..4 | INT | 5 | PRIVATE | Positive stat bonuses (str/agi/sta/int/spi) |
| 1176 | 0x498 | PLAYER_FIELD_NEGSTAT0..4 | INT | 5 | PRIVATE | Negative stat debuffs |
| 1181 | 0x49D | PLAYER_FIELD_RESISTANCEBUFFMODSPOSITIVE | INT | 7 | PRIVATE | Positive resistance buffs per school |
| 1188 | 0x4A4 | PLAYER_FIELD_RESISTANCEBUFFMODSNEGATIVE | INT | 7 | PRIVATE | Negative resistance debuffs per school |
| 1195 | 0x4AB | PLAYER_FIELD_MOD_DAMAGE_DONE_POS | INT | 7 | PRIVATE | Positive spell damage per school |
| 1202 | 0x4B2 | PLAYER_FIELD_MOD_DAMAGE_DONE_NEG | INT | 7 | PRIVATE | Negative spell damage per school |
| 1209 | 0x4B9 | PLAYER_FIELD_MOD_DAMAGE_DONE_PCT | INT | 7 | PRIVATE | Spell damage % modifier per school |

### 5.7 Miscellaneous

| Index | Hex | Name | Type | Size | Flags | Description |
|-------|-----|------|------|------|-------|-------------|
| 1216 | 0x4C0 | PLAYER_FIELD_BYTES | BYTES | 1 | PRIVATE | Player bytes (action bar toggled states) |
| 1217 | 0x4C1 | PLAYER_AMMO_ID | INT | 1 | PRIVATE | Equipped ammo item ID |
| 1218 | 0x4C2 | PLAYER_SELF_RES_SPELL | INT | 1 | PRIVATE | Self-resurrection spell (soulstone) |
| 1219 | 0x4C3 | PLAYER_FIELD_PVP_MEDALS | INT | 1 | PRIVATE | PvP medals |
| 1220 | 0x4C4 | PLAYER_FIELD_BUYBACK_PRICE_1 | INT | 12 | PRIVATE | Vendor buyback prices (12 slots) |
| 1232 | 0x4D0 | PLAYER_FIELD_BUYBACK_TIMESTAMP_1 | INT | 12 | PRIVATE | Vendor buyback timestamps |

### 5.8 Honor / PvP

| Index | Hex | Name | Type | Size | Flags | Description |
|-------|-----|------|------|------|-------|-------------|
| 1244 | 0x4DC | PLAYER_FIELD_SESSION_KILLS | TWO_SHORT | 1 | PRIVATE | `[honorable, dishonorable]` this session |
| 1245 | 0x4DD | PLAYER_FIELD_YESTERDAY_KILLS | TWO_SHORT | 1 | PRIVATE | Kills yesterday |
| 1246 | 0x4DE | PLAYER_FIELD_LAST_WEEK_KILLS | TWO_SHORT | 1 | PRIVATE | Kills last week |
| 1247 | 0x4DF | PLAYER_FIELD_THIS_WEEK_KILLS | TWO_SHORT | 1 | PRIVATE | Kills this week |
| 1248 | 0x4E0 | PLAYER_FIELD_THIS_WEEK_CONTRIBUTION | INT | 1 | PRIVATE | Honor contribution this week |
| 1249 | 0x4E1 | PLAYER_FIELD_LIFETIME_HONORBALE_KILLS | INT | 1 | PRIVATE | Total lifetime honorable kills |
| 1250 | 0x4E2 | PLAYER_FIELD_LIFETIME_DISHONORBALE_KILLS | INT | 1 | PRIVATE | Total lifetime dishonorable kills |
| 1251 | 0x4E3 | PLAYER_FIELD_YESTERDAY_CONTRIBUTION | INT | 1 | PRIVATE | Honor contribution yesterday |
| 1252 | 0x4E4 | PLAYER_FIELD_LAST_WEEK_CONTRIBUTION | INT | 1 | PRIVATE | Honor contribution last week |
| 1253 | 0x4E5 | PLAYER_FIELD_LAST_WEEK_RANK | INT | 1 | PRIVATE | PvP rank last week |
| 1254 | 0x4E6 | PLAYER_FIELD_BYTES2 | BYTES | 1 | PRIVATE | Player bytes 2 |
| 1255 | 0x4E7 | PLAYER_FIELD_WATCHED_FACTION_INDEX | INT | 1 | PRIVATE | Tracked reputation faction index |
| 1256 | 0x4E8 | PLAYER_FIELD_COMBAT_RATING_1 | INT | 20 | PRIVATE | Combat ratings (20 slots) |

**PLAYER_END = 0x4FC (1276 decimal)**

---

## 6. GameObject Fields (GAMEOBJECT_END = 0x1A)

Starts at `OBJECT_END` (index 6).

| Index | Hex | Name | Type | Size | Flags | Description |
|-------|-----|------|------|------|-------|-------------|
| 6 | 0x006 | OBJECT_FIELD_CREATED_BY | GUID | 2 | PUBLIC | Creator GUID |
| 8 | 0x008 | GAMEOBJECT_DISPLAYID | INT | 1 | PUBLIC | Display model ID |
| 9 | 0x009 | GAMEOBJECT_FLAGS | INT | 1 | PUBLIC | GO flags |
| 10 | 0x00A | GAMEOBJECT_ROTATION | FLOAT | 4 | PUBLIC | Rotation quaternion (x,y,z,w) |
| 14 | 0x00E | GAMEOBJECT_STATE | INT | 1 | PUBLIC | State (0=active, 1=closed) |
| 15 | 0x00F | GAMEOBJECT_POS_X | FLOAT | 1 | PUBLIC | Position X |
| 16 | 0x010 | GAMEOBJECT_POS_Y | FLOAT | 1 | PUBLIC | Position Y |
| 17 | 0x011 | GAMEOBJECT_POS_Z | FLOAT | 1 | PUBLIC | Position Z |
| 18 | 0x012 | GAMEOBJECT_FACING | FLOAT | 1 | PUBLIC | Facing/orientation |
| 19 | 0x013 | GAMEOBJECT_DYN_FLAGS | INT | 1 | DYNAMIC | Dynamic flags (activatable, animated) |
| 20 | 0x014 | GAMEOBJECT_FACTION | INT | 1 | PUBLIC | Faction template ID |
| 21 | 0x015 | GAMEOBJECT_TYPE_ID | INT | 1 | PUBLIC | GO type (door, chest, trap, transport, etc.) |
| 22 | 0x016 | GAMEOBJECT_LEVEL | INT | 1 | PUBLIC | Level (for traps, fishing nodes) |
| 23 | 0x017 | GAMEOBJECT_ARTKIT | INT | 1 | PUBLIC | Art kit ID |
| 24 | 0x018 | GAMEOBJECT_ANIMPROGRESS | INT | 1 | DYNAMIC | Animation progress |
| 25 | 0x019 | GAMEOBJECT_PADDING | INT | 1 | NONE | Padding |

---

## 7. DynamicObject Fields (DYNAMICOBJECT_END = 0x10)

| Index | Hex | Name | Type | Size | Flags | Description |
|-------|-----|------|------|------|-------|-------------|
| 6 | 0x006 | DYNAMICOBJECT_CASTER | GUID | 2 | PUBLIC | Caster GUID |
| 8 | 0x008 | DYNAMICOBJECT_BYTES | BYTES | 1 | PUBLIC | Type + effect bytes |
| 9 | 0x009 | DYNAMICOBJECT_SPELLID | INT | 1 | PUBLIC | Spell ID |
| 10 | 0x00A | DYNAMICOBJECT_RADIUS | FLOAT | 1 | PUBLIC | Effect radius |
| 11 | 0x00B | DYNAMICOBJECT_POS_X | FLOAT | 1 | PUBLIC | Position X |
| 12 | 0x00C | DYNAMICOBJECT_POS_Y | FLOAT | 1 | PUBLIC | Position Y |
| 13 | 0x00D | DYNAMICOBJECT_POS_Z | FLOAT | 1 | PUBLIC | Position Z |
| 14 | 0x00E | DYNAMICOBJECT_FACING | FLOAT | 1 | PUBLIC | Facing |
| 15 | 0x00F | DYNAMICOBJECT_PAD | BYTES | 1 | PUBLIC | Padding |

---

## 8. Corpse Fields (CORPSE_END = 0x26)

| Index | Hex | Name | Type | Size | Flags | Description |
|-------|-----|------|------|------|-------|-------------|
| 6 | 0x006 | CORPSE_FIELD_OWNER | GUID | 2 | PUBLIC | Owning player GUID |
| 8 | 0x008 | CORPSE_FIELD_FACING | FLOAT | 1 | PUBLIC | Corpse facing |
| 9 | 0x009 | CORPSE_FIELD_POS_X | FLOAT | 1 | PUBLIC | Position X |
| 10 | 0x00A | CORPSE_FIELD_POS_Y | FLOAT | 1 | PUBLIC | Position Y |
| 11 | 0x00B | CORPSE_FIELD_POS_Z | FLOAT | 1 | PUBLIC | Position Z |
| 12 | 0x00C | CORPSE_FIELD_DISPLAY_ID | INT | 1 | PUBLIC | Display model |
| 13 | 0x00D | CORPSE_FIELD_ITEM | INT | 19 | PUBLIC | Equipment display IDs (19 slots) |
| 32 | 0x020 | CORPSE_FIELD_BYTES_1 | BYTES | 1 | PUBLIC | `[race, gender, skin, face]` |
| 33 | 0x021 | CORPSE_FIELD_BYTES_2 | BYTES | 1 | PUBLIC | `[hairStyle, hairColor, facialHair, ?]` |
| 34 | 0x022 | CORPSE_FIELD_GUILD | INT | 1 | PUBLIC | Guild ID |
| 35 | 0x023 | CORPSE_FIELD_FLAGS | INT | 1 | PUBLIC | Corpse flags |
| 36 | 0x024 | CORPSE_FIELD_DYNAMIC_FLAGS | INT | 1 | DYNAMIC | Dynamic flags |
| 37 | 0x025 | CORPSE_FIELD_PAD | INT | 1 | NONE | Padding |

---

## Summary: Field Counts

| Object Type | Start Index | End Index | Field Count |
|-------------|------------|-----------|-------------|
| Object | 0x000 | 0x006 | 6 |
| Item | 0x006 | 0x030 | 42 |
| Container | 0x030 | 0x074 | 68 |
| Unit | 0x006 | 0x0BC | 182 |
| Player | 0x0BC | 0x4FC | 1094 |
| GameObject | 0x006 | 0x01A | 20 |
| DynamicObject | 0x006 | 0x010 | 10 |
| Corpse | 0x006 | 0x026 | 32 |

**Note:** Unit+Player total = 6 (Object) + 182 (Unit) + 1094 (Player) = **1276 fields** per player.
