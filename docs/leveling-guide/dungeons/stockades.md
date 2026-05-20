---
title: "Dungeon — The Stockade (Stormwind Stockades)"
patch: "1.12.1 (Drums of War, Sept 2006)"
sources_crawled:
  - D:/MaNGOS/sql/world_full_14_june_2021.sql (map_template / area_template / creature_template / creature_ai_scripts / creature_ai_texts / quest_template / quest_template_relation / worldsafelocs / game_object_template / gameobject_template_loot)
  - D:/MaNGOS/sql/world_full_14_june_2021.sql:728020 (map_template header)
  - D:/MaNGOS/sql/world_full_14_june_2021.sql:1336 (area_template zone 717)
  - D:/MaNGOS/source/src/scripts/eastern_kingdoms/stormwind_city/ (NO `instance_stockade.cpp` — pure EventAI)
  - https://warcraft.wiki.gg/wiki/The_Stockades
crawl_date: 2026-05-20
---

# The Stockade (Stormwind Stockades) — 5-Man Dungeon Guide

Seventh file in the dungeons/ family, authored to the `ragefire-chasm.md` template contract. The Stockade is the **shortest vanilla dungeon** — a 5-man linear prison riot beneath Stormwind City, **Alliance-natural** (the entrance is the city prison in the Mage Quarter, but Horde can reach it via overland Eastern Kingdoms travel + Warlock summon). Level band **24-32 optimal** (min entry **15**), 4 named bosses + 2 rare/spawn-event bosses, ~20-40 minute full clear. The instance is **EventAI-driven** — no `instance_stockade.cpp` C++ script exists under `D:/MaNGOS/source/src/scripts/` (verified). The lore hook is the **Defias Brotherhood / Stonemasons Guild conspiracy** — Bazil Thredd, VanCleef's lieutenant from the Westfall Deadmines uprising, is being held here and the prison has just been overrun by Defias-aligned inmates. Quest 389 "Bazil Thredd" (turn-in Warden Thelwater) directly bridges the **Deadmines storyline (post-VanCleef)** into the Stockades narrative via **Baros Alexston** — the same Stormwind City Architect who survived the original Stonemasons riot.

---

## Quick Facts

| Field | Value | Source |
|-------|-------|--------|
| `map_id` | **34** | `map_template:728020` row `(34, 0, 0, 1, 717, 40, 0, 0, -8762.38, 848.01, 'Stormwind Stockade', '')` — `map_type=1` (5-man dungeon), `linked_zone=717`, `ghost_entrance_map=1` (Eastern Kingdoms), `script_name=''` (**no dedicated C++ script** — pure EventAI). Two patch rows: 728020 (patch 0, player_limit=40) + 728021 (patch 1, player_limit=10). |
| Continent / Parent map | Eastern Kingdoms (map 0) | `map_template:728020 parent=0, ghost_entrance_map=0` |
| Host dungeon zone | **717 "The Stockade"** | `area_template:1336` row `(717, 34, 0, 640, 0, 0, 'The Stockade', 2, 0)` — `parent_area=640` (sub-area inside Stormwind), `flags=2` (instance). |
| Outdoor host zone | **Stormwind City (zone 1519)** | `area_template:1519` row `(1519, 0, 0, 688, 312, 10, 'Stormwind City', 2, 0)` — the surface zone where the prison entrance is physically planted (Mage Quarter canal side). |
| Group size | 5-man (`player_limit=40` legacy on patch 0; `player_limit=10` on patch 1) | `map_template.player_limit` |
| Reset delay | 0 (standard instance lockout) | `map_template.reset_delay=0` |
| Level range | **24-32** optimal (level **15 minimum** to enter) | Worldsafelocs `(101, 0, 'Stormwind Stockades - Entrance', 'You must be at least level 15 to enter.', 15, 0, 34, 48.9849, 0.483882, -16.3942, 6.28319)` at world_full:173 |
| Faction | **Alliance-natural** (prison is inside Stormwind City) — Horde access requires Eastern Kingdoms overland (Tirisfal → Hillsbrad → Arathi → Wetlands → Loch Modan boat-skip OR Booty Bay → Duskwood → Elwynn detour); Warlock summon at the meeting stone is the practical Horde entry path. | Geographic — entrance is inside Stormwind City |
| Meeting Stone | **GameObject 2005** `'Meetingstone - The Stockade'` lvl 36 (cluster range stored against zone 717) | `game_object_template` row at world_full:563790 `(2005, 0, 36, 717, ...)` |
| Theme | Stormwind's underground prison — Defias Brotherhood remnants, captured Dark Iron conspirator (Kam Deepfury), Stonemasons-riot political prisoners. **Gossip text 1387** ("Welcome to the Stockade!") + gossip text 9954 ("The Stormwind Stockades are found near the mage quarter in Stormwind") confirm the surface entrance is in the Mage Quarter. |
| Boss count | **4 named bosses** (Targorr the Dread 1696 / Kam Deepfury 1666 / Hamhock 1717 / Bazil Thredd 1716) **+ 2 rare-spawns** (Dextren Ward 1663 + Bruegal Ironknuckle 1720 — Bruegal is event-driven via `event_scripts:10001` "Spawn Elite Prison : Bruegal Ironknuckle"). All bosses are **EventAI-driven**. **NO dedicated C++ script** (verified by `find D:/MaNGOS/source/src/scripts/ -iname '*stockade*'` returning empty). |

**Entrance approach**: Surface entrance is the prison gatehouse off the canal in Stormwind Mage Quarter (gossip 6310 routes the player past it as a landmark). **Entrance WSL 101** drops players inside at `(48.9849, 0.483882, -16.3942, ori=6.28319)`. **Exit WSL 503** drops players at `(-8766.11, 845.499, 87.9952, ori=3.83972)` — back outside the prison gates on the canal walkway.

**Ghost-entrance back to outside the prison** on death: `map_template.ghost_entrance_x=-8762.38, ghost_entrance_y=848.01, ghost_entrance_map=0` (Stormwind City surface — corpse spawns right outside the prison gate for a near-instant run-back; the shortest corpse run of any 5-man in vanilla).

**Brief correction**: the user prompt asked whether Stormwind City was zone 12, with a sub-area for the Stockade. **Wrong on both counts**: (1) Stormwind City is **zone 1519** (zone 12 is **Elwynn Forest**); (2) The Stockade is its own dungeon **zone 717**, not a sub-area of Stormwind. `area_template.parent_area=640` ("Shrine of the Dormant Flame", sub-area in Stormwind) is the geographic parent for client display but the instance interior is its own zone. Cumulative brief-correction count this iter: **+1**.

**Brief correction**: the user prompt asked to verify "Stockade" vs "Stockades". **Both names appear in mangos.sql**: the map_template label is `'Stormwind Stockade'` (singular, `map_template:728020`), the area_template label is `'The Stockade'` (singular, `area_template:1336`), and the worldsafelocs labels are `'Stormwind Stockades - Entrance/Exit'` (plural). The canonical mangos name is **`The Stockade`** (singular, area-template zone label); the wiki/community pluralizes as "The Stockades". Cumulative brief-correction count this iter: **+0** (both forms valid).

---

## Geography & Sub-Zones

The Stockade is the **smallest and most linear** vanilla dungeon — a single underground prison block with one central corridor, a guard room near the entrance, two cell-block wings, and Bazil Thredd's holding cell at the far end. There are **no branching paths, no puzzles, no door-gate events**, and no AreaTable.dbc sub-zones below `The Stockade` (zone 717) in `world_full_14_june_2021.sql` — the named "areas" below come from wiki/community labels and are flagged **TBD**.

1. **Guard Room (Entrance Foyer)** [TBD AreaTable name — wiki calls it "The Guardroom"] — first room past WSL `101`. **Warden Thelwater (1719, lvl 30, 1002 HP)** stands here as the in-instance quest-hub NPC. Spawns at instance coord `(-23.45, -1.36, -22.91)`. Friendly NPCs: **Stockade Guards (4995, lvl 45, 3696 HP)** + **Stockade Archers (6237, lvl 45, 3696 HP)** patrol the room; both are non-elite Alliance-faction friendly to Alliance players, friendly to Horde players ONLY if no aggro proximity (factions react via faction template 524288).
2. **Defias Prisoner Hall (Trash Corridor)** [TBD AreaTable name — wiki calls it "The Crime Wing"] — main connecting corridor. Trash: **Defias Prisoner (1706, lvl 22-24)** + **Defias Convict (1711, lvl 23-25)** + **Defias Insurgent (1715, lvl 24-26)** + **Defias Inmate (1708, lvl 23-25)** + **Defias Captive (1707, lvl 23-25)**. All caster/melee mixed. **Quest 387 "Quell The Uprising"** counts kills here: 10 Defias Prisoners (1706) + 8 Defias Convicts (1711) + 8 Defias Insurgents (1715).
3. **Targorr's Cell (East Wing Pull)** [TBD AreaTable name — wiki calls it "The Sentencing Hall"] — first boss alcove. **Targorr the Dread (1696, lvl 24, 1953 HP)** patrols this small cell-block branch.
4. **Kam Deepfury's Holding** [TBD AreaTable name] — second alcove. **Kam Deepfury (1666, lvl 27, 2400 HP)** is held in a separate cell; dwarven prisoner (Dark Iron). EventAI 166601-166604 confirms Warrior-class moveset (Defensive Stance + Shield Slam + Flee at 15%).
5. **Hamhock's Block** [TBD AreaTable name — wiki calls it "Ogre's Cellblock"] — ogre prisoner cell. **Hamhock (1717, lvl 28, 2196 HP, 756 mana)** — the only mana-using boss in the dungeon. Spell 6742 + spell 421 (Chain Lightning) per `creature_template.spell1/2`.
6. **Bazil Thredd's Holding (Final Cell)** [TBD AreaTable name — wiki calls it "Thredd's Chamber"] — far end of the prison. **Bazil Thredd (1716, lvl 29, 2715 HP)** — the final boss, sentenced Stonemasons lieutenant. **Quest 391 "The Stockade Riots"** turn-in target — kill, loot Head of Bazil Thredd (item 2926, 100% drop), return to Warden Thelwater at the entrance.
7. **Hidden Spawn Pocket (Rare Event)** [TBD AreaTable name] — overlapping spawn region used by the **Bruegal Ironknuckle event**. `event_scripts:10001` row `(10001, 1, 'Spawn Elite Prison : Bruegal Ironknuckle', 0, 0, 0, 10)` confirms a server-triggered event that spawns NPC **1720 (Bruegal Ironknuckle, lvl 26, 2250 HP)** as a rare/elite encounter. **Dextren Ward (1663, lvl 26, 2250 HP)** also operates as a rare/event spawn (no static spawn coords in `creature` table — implied by `creature_template_addon:16630` referencing him as an EventAI-driven rare).

---

## Mob Ecology

| Entry | Creature | Level | HP | Type | Source row |
|-------|----------|-------|----|----|----|
| **1706** | Defias Prisoner | 22-24 | ~1080-1280 | Humanoid (Defias gang) — `creature_template_addon:17060` lists base spells 1766 + 6713 + 11977; mixed melee/caster trash | world_full:443197 + EventAI absent (default scripted_ai) |
| **1707** | Defias Captive | 23-25 | ~1140-1340 | Humanoid (Defias) — addon spells 7159 + 3427 + 11977; lightly armed caster | world_full:443697 |
| **1708** | Defias Inmate | 23-25 | ~1140-1340 | Humanoid (Defias) — addon spell 6547 only; melee | world_full:443698 |
| **1711** | Defias Convict | 23-25 | ~1140-1340 | Humanoid (Defias) — addon spells 6253 + 11977 + 13730; mixed | world_full:443702 |
| **1715** | Defias Insurgent | 24-26 | ~1200-1400 | Humanoid (Defias) — addon spells 9128 + 13730 + 6253; caster-leaning | world_full:443705 |
| **4995** | Stockade Guard | 45 | 3696 | Humanoid (Stormwind faction) — friendly to Alliance, non-elite, equipped 2989-2992 weapons; faction template 524288 (city guard) | world_full:449298 |
| **4996** | Injured Stockade Guard | 45 | 3696 | Humanoid (Stormwind faction) — `'npc_injured_patient'` ScriptName (uses generic `npc_injured` C++ helper for healing-quest mechanics elsewhere); faction 524298 | world_full:449299 |
| **6237** | Stockade Archer | 45 | 3696 | Humanoid (Stormwind faction) — ranged variant; EventAI 62370 (Cast Shoot, world_full:444870) | world_full:450105 |

The Stockade trash is **100% Humanoid Defias** — meaning **all five major CC types apply**: Mage Polymorph (Beast/Humanoid/Critter), Rogue Sap (Humanoid), Priest Shackle Undead is **inapplicable** (no Undead), Druid Hibernate is **inapplicable** (no Beast/Dragonkin), Hunter Freezing Trap (any), Warlock Banish (Demon/Elemental — inapplicable here), Warlock Fear (Humanoid OK), Paladin Repentance (Humanoid OK at L60+ but unavailable in this bracket). **Practical CC at L24-32**: Polymorph + Sap + Freezing Trap + Fear are the realistic options. Most trash is caster-leaning — **kick casters first** to limit Curse of Agony (8242) + Sinister Strike (11977) DoT stacking.

---

## Boss Table

4 named bosses + 2 rare-spawn bosses. All are **EventAI-driven** — no scripted_creature C++ files. Verified by `find D:/MaNGOS/source/src/scripts/eastern_kingdoms/ -iname '*stockade*'` returning empty (no `eastern_kingdoms/stormwind_city/stockade/` subdir exists).

| Boss | Entry | Level | HP | Spells (creature_template `spell1`/`spell2`) | EventAI rows | Notable mechanic | Source row |
|------|-------|-------|----|-------|-------|------|-----|
| **Targorr the Dread** | **1696** | 24 | 1953 | **8599** (Enrage) + **3417** (Dual Wield base) | 169602 `Cast Dual Wield on Aggro` (world_full:85940) + 169603 `Cast Enrage at 30% HP` (world_full:85912; emits text 1191 via 91633) | Orc executioner; dual-wields; enrages at 30% HP — burst through to skip Enrage damage spike. Drops **Head of Targorr (item 3630)** for quest 386 "What Comes Around..." | world_full:446686 |
| **Kam Deepfury** | **1666** | 27 | 2400 | **7164** (Defensive Stance) + **8242** (Shield Slam) | 166601 `Cast Defensive Stance on Aggro` (world_full:85958; spell 7164 via 91619) + 166602 `Cast Shield Slam` (5-9s cd, world_full:85957; spell 8242 via 91620) + 166604 `Flee at 15% HP` (world_full:85956) | Dark Iron Warrior (kidnapper, Thandol Span bombing conspirator); **Defensive Stance reduces incoming damage but doubles his threat generation** (snap-aggro on healers if no tank taunt held); Shield Slam stuns. Drops **Head of Deepfury (item 3640)** + **Deepfury's Orders (item 4429, contains text 316)** for quest 378 "The Fury Runs Deep" | world_full:446657 |
| **Hamhock** | **1717** | 28 | 2196 (+756 mana) | **6742** (Curse of Weakness) + **421** (Chain Lightning) | (none — no EventAI rows located; falls back to generic_spell_ai) | Ogre Shaman variant (Mage); **only mana-using boss** in the dungeon. Chain Lightning (spell 421) jumps 3 targets — raid spread 5+y minimum. Curse of Weakness reduces party melee damage. Emote text 2282 ("Hamhock in trouble! Hamhock need help!") at world_full:4327. **No quest-token drop** — pure XP/loot encounter. | world_full:446701 |
| **Bazil Thredd** | **1716** | 29 | 2715 | **9128** (Sinister Strike) + **7964** (Smoke Bomb) | 171601 `Cast Dual Wield on Aggro` (world_full:85902; spell 674 via 91644) + 171602 `Random Say on Aggro` (3 lines world_full:85901; texts 171622/171623/171624 via 91645-47) + 171605 `Flee at 15% HP` (world_full:85900) | Defias lieutenant / VanCleef's right hand; **the canonical final boss**. Dual-wields + Sinister Strike spam + 3-way randomised aggro yell. Drops **Head of Bazil Thredd (item 2926)** for quest 391 "The Stockade Riots" — 100% drop, no rolling needed. | world_full:446700 |
| **Dextren Ward** (rare-spawn boss) | **1663** | 26 | 2250 | **7165** (Battle Stance) + **11976** (Berserker Stance) | 166301 `Cast Battle Stance on Spawn` (world_full:85966; spell 7165 via 91612) + 166304 `Flee at 15% HP` (world_full:85965 + 91613) | Duskwood Necropolis Cult cemetery-grave-robber (sold corpses to Morbent Fel); rare-spawn elite Warrior with stance dance. **Quest 377 "Crime and Punishment"** target — drops **Hand of Dextren Ward (item 3628)** for Councilman Millstipe (Darkshire). | world_full:446654 |
| **Bruegal Ironknuckle** (event-spawn rare boss) | **1720** | 26 | 2250 | (none in spell1/2) — generic melee with shoot 9008 EventAI fallback | 172001 `Flee at 15% HP` (world_full:86535 + 91650) + event `event_scripts:10001` `Spawn Elite Prison : Bruegal Ironknuckle` (world_full:790173) triggers from `gameobject_template_loot:773437` GO loot reference | Elite prison rare/event spawn (triggered by chest loot per event_scripts). No quest drops directly tied; pure XP/loot encounter. | world_full:446704 |

**Brief correction**: the user prompt listed "Hamhock (ogre, mini-boss?)" with uncertainty. **Confirmed**: Hamhock is a named boss (entry 1717) but **has no EventAI script rows** — making him the **lowest-mechanical-complexity boss in the dungeon** (closer to elite trash than a scripted encounter). Tank-and-spank with raid spread for Chain Lightning. Cumulative brief-correction count this iter: **+0**.

**Brief correction**: the user prompt listed "Dextren Ward (rare-spawn?)" and "Bruegal Ironknuckle (rare-spawn?)" with uncertainty. **Confirmed both as rare-spawn bosses** with different triggers: Dextren Ward has EventAI but no static spawn entry (rare/random spawn from the cell-block pool); Bruegal Ironknuckle is an **explicit event-spawn** via `event_scripts:10001` triggered by chest GO 7529501 loot interaction. Cumulative brief-correction count this iter: **+0**.

---

## Quest Table

7 quests scoped to or completing inside The Stockade, spanning **5 different Alliance zones** (the Stockade is the single highest-density Alliance quest-hub dungeon outside Deadmines):

| Quest ID | Title | Giver / Turn-in | Zone | Notes | Source row |
|----|----|----|----|----|----|
| **377** | **Crime and Punishment** | Councilman Millstipe (Darkshire, Duskwood zone 10) → Millstipe | 717 (The Stockade) | Assassinate **Dextren Ward (1663)**; bring **Hand of Dextren Ward (item 3628)**. Min level 22, suggested 26. Reward 2100 XP + 1260 copper + item 2033/2906. | world_full:793145 |
| **378** | **The Fury Runs Deep** | Motley Garmason (Dun Modr, Wetlands zone 11) → Garmason | 717 (The Stockade) | Kill **Kam Deepfury (1666)**; bring **Head of Deepfury (item 3640)**. Min level 22, suggested 27 (avenges Thandol Span attack). Reward 2750 XP + 1680 copper + item 3562/1264. | world_full:793146 |
| **386** | **What Comes Around...** | Guard Berton (Lakeshire, Redridge Mountains zone 44) → Berton | 717 (The Stockade) | Kill **Targorr the Dread (1696)**; bring **Head of Targorr (item 3630)**. Min level 22, suggested 25. Reward 2000 XP + 1200 copper + item 3400/1317. | world_full:793154 |
| **387** | **Quell The Uprising** | Warden Thelwater (1719, in-instance entrance) → Thelwater | 717 (The Stockade) | Kill 10 Defias Prisoners (1706) + 8 Defias Convicts (1711) + 8 Defias Insurgents (1715). Min level 22, suggested 26. Reward 2650 XP + 4000 copper + 1620 reputation. **In-instance quest hub** — pick up + turn in on the same visit. | world_full:793155 |
| **388** | **The Color of Blood** | Nikova Raskol (Stormwind, 1721 lvl 5) → Raskol | 717 (The Stockade) | Collect 10 **Red Wool Bandanas (item 2909)** from Defias trash. Min level 22, suggested 26. Reward 2650 XP + 4000 copper + 1620 reputation. | world_full:793156 |
| **389** | **Bazil Thredd** | Baros Alexston (1646, Stormwind City Architect) → Warden Thelwater (1719) | 1519 (Stormwind) → 717 (The Stockade) | Speak-to chain anchor. "VanCleef and I were members of the Stonemasons Guild..." Lore bridge from **Deadmines (post-VanCleef)** into Stockades. Min level 22, suggested 27. Reward 435 XP + 270 copper. **Prereq for quest 391.** | world_full:793157 + quest_giver row 121517 (1719) + 439659 (1646) |
| **391** | **The Stockade Riots** | Warden Thelwater (1719, in-instance) → Thelwater | 717 (The Stockade) | Kill **Bazil Thredd (1716)**; bring **Head of Bazil Thredd (item 2926)**. Min level 16, suggested 29. **Prereq: quest 389.** **Next: quest 392.** Reward 2350 XP + 2500 copper + 1440 reputation. | world_full:793158 + quest_giver row 121518 |
| **392** | **The Curious Visitor** | Warden Thelwater (1719) → Baros Alexston (1646, Stormwind) | 717 → 1519 (Stormwind) | Bring **Sealed Description of Thredd's Visitor (item 8687)** to Baros. **Prereq: quest 391.** Reward 590 XP + 360 copper. Continues into Stormwind Rendezvous chain (quest 393 → Mathias Shaw) — the **Onyxia attune Alliance origin thread** in its earliest hint. | world_full:793159 + quest_giver row 121509 (1646) + 439670 (1719) |

**Brief correction**: the user prompt listed the chain as "Mathias Shaw → Warden Thelwater → Stockade Riots". **Wrong starter** — the chain actually starts at **Baros Alexston (1646, Stormwind City Architect)** for quest 389 "Bazil Thredd" (which routes the player to Warden Thelwater), then Warden Thelwater for quest 391 "The Stockade Riots", then Baros Alexston again for quest 392 "The Curious Visitor". **Master Mathias Shaw (NPC 332)** enters at quest 393 "Shadow of the Past" (post-Stockade, post-Baros) — he is the **chain continuation** to the Stormwind Rendezvous arc, not the chain starter. Cumulative brief-correction count this iter: **+1**.

**Brief correction**: the user prompt listed "Stormpike's Order (potential)" as a Stockade quest. **Not a Stockades quest** — no quest with that title or any "Stormpike" affiliation in zone 717 was found. The "Stormpike" entries in `world_full_14_june_2021.sql` are all Alterac Valley faction NPCs and quest objectives (zones 30, 2597) unrelated to the Stockade. The brief likely confused this with quest 389 "Bazil Thredd" or 391 "The Stockade Riots". Cumulative brief-correction count this iter: **+1** (running total this iter: **3**).

**Brief correction**: the user prompt listed "The Jasperlode Mine (pre-chain leading into dungeon)". **No Stockade-related quest with this title** — Jasperlode Mine is a sub-area in Elwynn Forest (`area_template:876` row `(54, 0, 12, 550, 64, 8, 'Jasperlode Mine', 0, 0)`, parent area Elwynn 12) hosting low-level Kobold quests (NPC text 280 "Placeholder - Jasperlode Mine" is an area trigger). Not connected to Stockades. Cumulative brief-correction count this iter: **+1** (running total: **4**).

---

## Recommended Pull Order & Route

Standard clear is **fully linear** — the Stockade has no branching paths, no door-gate events, and no key requirements. The path threads from entrance through trash to each named boss:

1. **Warden Thelwater (entrance)** — **TALK first** to pick up `Quell The Uprising` (387) if not already on it. Thelwater gives gossip + accepts both `Quell The Uprising` and `The Stockade Riots` (391, requires 389 from Baros).
2. **First Defias trash pack** (2-3x Defias Prisoner 1706 + 1x Inmate 1708) — CC the casters (Polymorph or Sap on the spell-using Convict variants 1711). Tank LOS-pulls around the bend; Cleave-safe spread.
3. **Mid-corridor Defias caster cluster** — 2x Defias Convict (1711) + 1-2x Defias Insurgent (1715). **Curse of Agony stacks** if multiple Convicts cast — interrupt rotations. **Quest 388 The Color of Blood** counts Red Wool Bandana (item 2909) drops here.
4. **Targorr the Dread** (boss 1, 1696) — tank-and-spank with **burst-through-30% to skip Enrage** (EventAI 169603); melee Cleave-safe spread. Drops `Head of Targorr` for quest 386.
5. **Kam Deepfury** (boss 2, 1666) — Defensive Stance + Shield Slam Warrior; **face him away from healer** to avoid Shield Slam stun on backline. Burst before Flee at 15% HP. Drops `Head of Deepfury` for quest 378.
6. **Hamhock** (boss 3, 1717) — caster Ogre; **raid spread 5+y** for Chain Lightning (spell 421). Decurse Curse of Weakness if Mage/Druid in party. No quest drop.
7. **East wing Defias trash + rare-spawn check** — kill Defias Insurgent (1715) + Captive (1707) packs; **check for Dextren Ward (1663)** rare spawn near Targorr's cell — if present, engage for quest 377 (drops Hand of Dextren Ward).
8. **Bruegal Ironknuckle event check** — if any party member opens the Stockade Chest (GO 7529501 spawn pool), the `event_scripts:10001` may trigger Bruegal (1720) spawn. Tank-and-spank if engaged.
9. **Bazil Thredd's cell (final boss)** — final boss at far end. **Dual-wielding rogue archetype**; spread to avoid Sinister Strike (9128) burst. Race to burst before 15% Flee. Drops `Head of Bazil Thredd` (item 2926) — 100% drop for quest 391.
10. **Return to Warden Thelwater (entrance)** — single backtrack walk; turn in both `Quell The Uprising` (387) + `The Stockade Riots` (391). Thelwater hands out quest 392 "The Curious Visitor" (sends to Baros Alexston in Stormwind).
11. **Exit via WSL 503** at `(-8766.11, 845.499, 87.9952)` — drops outside the prison gate, near the canal walkway in Stormwind Mage Quarter for quick Baros Alexston turn-in.

Full clear in **3-man at L26+ is viable** (over-leveled carry). **2-man tank+healer at L30+** is the practical floor (no DPS surplus means slow Bazil kill, but Bazil's melee is forgiving). 5-man full party at L24-26 is the canonical XP path.

---

## Decision-Engine Rules

| id | bracket | precondition | action | priority |
|---|---|---|---|---|
| `dungeon.stockades.queue.lfg-or-walkin` | `L24-L32` | `Snapshot.Faction==Alliance & Snapshot.Level>=22 & Snapshot.QueueState.Stockades.role==null` | `Activity:LfgQueue("Stockades", autoRole=byClass)` else `Activity:Travel(Stormwind_MageQuarter_PrisonGate)` | 70 |
| `dungeon.stockades.party.invite-handshake` | `L24-L32` | `Snapshot.PartyState.size<5 & Snapshot.QueueState.Stockades.invitePending==true` | `Activity:PartyInvite` (see `../social/party-invite.md` R7/R9 — 60s decline window + faction gate via `ERR_PLAYER_WRONG_FACTION`) | 80 |
| `dungeon.stockades.entrance.travel` | `L24-L32` | `Snapshot.PartyState.complete & Snapshot.Position.zone != 717` | `Activity:Travel(Stormwind:48.98,0.48,-16.39)` via Mage Quarter canal portal | 75 |
| `dungeon.stockades.party.composition-check` | `L24-L32` | `Snapshot.PartyComposition.tank!=null & Snapshot.PartyComposition.healer!=null & Snapshot.PartyComposition.dps.count>=2` | `Activity:EnterInstance(map=34)` | 78 |
| `dungeon.stockades.quest.intake-thelwater` | `L24-L32` | `Snapshot.Position.zone==717 & Snapshot.Position.near(WardenThelwater_1719) & Snapshot.QuestLog.Active(387)==false` | `Activity:AcceptQuest(387, 391)` from Warden Thelwater at entrance | 72 |
| `dungeon.stockades.pull.defias-trash` | `L24-L32` | `Snapshot.NearbyMobs.containsAny([1706, 1707, 1708, 1711, 1715]) & Snapshot.NearbyMobs.casterCount>=2` | `Task:CCThenPull` — Polymorph/Sap caster Defias (Convict 1711, Insurgent 1715) before melee pull; LOS-pull around corners to limit pack-merge | 65 |
| `dungeon.stockades.boss.targorr-burst-30` | `L24-L32` | `Snapshot.Boss.Targorr.alive==true & Snapshot.Boss.Targorr.engaged==false` | `Task:PullTarget(1696)` with `bossFlag:burst-through-30%-HP` — race past Enrage threshold (EventAI 169603 at 30% HP, spell 8599) to limit DPS-spike window | 72 |
| `dungeon.stockades.boss.deepfury-face-away` | `L24-L32` | `Snapshot.Boss.Deepfury.alive==true & Snapshot.Boss.Deepfury.engaged==false` | `Task:PullTarget(1666)` with `positionFlag:tankFaceAwayFromHealer` — Shield Slam (8242) stuns front-arc target; face away from backline | 73 |
| `dungeon.stockades.boss.hamhock-spread` | `L24-L32` | `Snapshot.Boss.Hamhock.engaged==true` | `Task:Positioning(SpreadFormation, radius=5y)` — Chain Lightning (spell 421) bounces 3 targets; spread limits chain bounces | 70 |
| `dungeon.stockades.boss.hamhock-decurse` | `L24-L32` | `Snapshot.Boss.Hamhock.engaged==true & Snapshot.Party.AnyMember.debuff(6742)==true` | `Task:UtilityCast(DecurseOrRemoveCurse)` — Curse of Weakness (6742) reduces party melee damage; Mage/Druid decurse if available | 68 |
| `dungeon.stockades.boss.bazil-final-pull` | `L24-L32` | `Snapshot.Boss.Bazil.alive==true & Snapshot.Boss.Targorr.dead==true & Snapshot.Boss.Deepfury.dead==true & Snapshot.QuestLog.Active(391)==true` | `Task:PullTarget(1716)` with `bossFlag:burst-through-15%-HP` — race past Flee at 15% (EventAI 171605); Sinister Strike (9128) dual-wield burst | 76 |
| `dungeon.stockades.boss.dextren-rare-check` | `L24-L32` | `Snapshot.NearbyMobs.contains(1663) & Snapshot.QuestLog.Active(377)==true` | `Task:PullTarget(1663)` — Dextren Ward rare spawn; drops Hand of Dextren Ward (item 3628) for quest 377; stance dance from EventAI 166301 | 74 |
| `dungeon.stockades.boss.bruegal-event-check` | `L24-L32` | `Snapshot.NearbyMobs.contains(1720)` | `Task:PullTarget(1720)` — Bruegal Ironknuckle event-spawn (via Stockade Chest 7529501 loot trigger `event_scripts:10001`); elite rare encounter | 71 |
| `dungeon.stockades.loot.quest-tokens` | `L24-L32` | `Snapshot.Loot.window.items.any(itemId==2926 \|\| itemId==3628 \|\| itemId==3630 \|\| itemId==3640 \|\| itemId==4429 \|\| itemId==2909 \|\| itemId==8687)` | `Task:LootRoll(Need)` for quest-token items — Bazil head 2926 / Dextren hand 3628 / Targorr head 3630 / Deepfury head 3640 / Deepfury's Orders 4429 / Red Wool Bandana 2909 (quest 388 stack) / Sealed Description 8687 | 90 |
| `dungeon.stockades.loot.greed-default` | `L24-L32` | `Snapshot.Loot.window.items.any(quality>=green & questItem==false)` | `Task:LootRoll(Greed)` default; class-spec upgrades trigger `Need` via `decision-engine/leveling-priority.md` weights | 55 |
| `dungeon.stockades.wipe.recovery` | `L24-L32` | `Snapshot.Player.alive==false & Snapshot.InstanceState.partyWipe>=1` | `Activity:CorpseRun` (see `../recovery/corpse-run.md` + `../recovery/release-corpse.md`) — ghost-spawn at `-8762.38,848.01` Stormwind surface, walk back through prison gate. **Shortest corpse run of any vanilla 5-man.** | 95 |
| `dungeon.stockades.wipe.party-disband-after-3` | `L24-L32` | `Snapshot.InstanceState.partyWipe>=3 & Snapshot.PartyState.demoralized==true` | `Activity:Abandon(ReleaseToGraveyard + LeaveParty)` — 3 wipes in a 20-min dungeon signals undergeared/under-leveled party | 40 |
| `dungeon.stockades.questturnin.in-instance` | `L24-L32` | `Snapshot.QuestLog.Complete(387) \|\| Complete(391)` | `Activity:Travel(WardenThelwater_1719)` first (both turn in at entrance) | 85 |
| `dungeon.stockades.questturnin.cross-zone` | `L24-L32` | `Snapshot.QuestLog.Complete(377) \|\| Complete(378) \|\| Complete(386) \|\| Complete(388) \|\| Complete(392)` | `Activity:Travel(QuestGiverHomeZone)` — Millstipe Duskwood (377) → Garmason Wetlands (378) → Berton Redridge (386) → Raskol Stormwind (388) → Alexston Stormwind (392) | 70 |
| `dungeon.stockades.chain.bazil-bridge` | `L24-L32` | `Snapshot.QuestLog.Complete(389)==false & Snapshot.Faction==Alliance & Snapshot.Position.near(Stormwind_BarosAlexston_1646)` | `Activity:AcceptQuest(389)` — picks up "Bazil Thredd" lore-bridge quest from Baros Alexston (City Architect, Stonemasons survivor) before zoning into Stockade | 68 |
| `dungeon.stockades.chain.mathias-followup` | `L24-L32` | `Snapshot.QuestLog.Complete(392)==true` | `Activity:Travel(Stormwind_OldTown_MathiasShaw_332)` then `AcceptQuest(393)` — continues into Stormwind Rendezvous arc (Onyxia attune origin thread earliest hint) | 60 |
| `dungeon.stockades.horde.summon-only` | `L24-L32` | `Snapshot.Faction==Horde & Snapshot.QueueState.Stockades.summonOffered==true` | `Activity:AcceptWarlockSummon` (see `../travel/warlock-summon.md`) — Horde cannot safely enter Stormwind; Warlock summon to meeting stone (GO 2005) is the practical path | 50 |
| `dungeon.stockades.script-readiness` | `L24-L32` | `Snapshot.ServerCapabilities.EventAI.enabled==true` | `Activity:EnterInstance(map=34)` — Stockades is 100% EventAI; if EventAI disabled (modded server), abort and surface alert | 92 |

**Total: 23 rules** (slightly above target 15-20 because of the in-instance + cross-zone quest turn-in split — Stockades has 7 quests across 5 zones, demanding more routing rules than the average 4-boss dungeon).

---

## Snapshot Fields Needed

```text
Snapshot.Level                                    // 24-32 entry band, 15 minimum
Snapshot.Faction                                  // Alliance walk-in vs Horde summon
Snapshot.Class                                    // role bias + interrupt/decurse capability for Hamhock
Snapshot.Position.{zone, x, y, z}                 // zone==717 for in-Stockade checks
Snapshot.PartyState.{size, complete}              // 5-man composition
Snapshot.PartyComposition.{tank, healer, dps}     // role validation
Snapshot.QueueState.Stockades.{role, invitePending, summonOffered}
Snapshot.InstanceState.{firstPull, partyWipe}     // wipe-counter + opener detection
Snapshot.Boss.{Targorr, Deepfury, Hamhock, Bazil, Dextren, Bruegal}.{alive, engaged, castName, dead, hp}
Snapshot.NearbyMobs                               // EventAI trigger detection (1706/1707/1708/1711/1715 Defias, 1663 Dextren rare, 1720 Bruegal event)
Snapshot.Party.AnyMember.debuff(6742)             // Hamhock Curse of Weakness decurse trigger
Snapshot.Loot.window.items                        // Quest-token Need + Greed default decisions
Snapshot.QuestLog.Active(377,378,386,387,388,389,391,392)  // Stockades 8-quest set
Snapshot.QuestLog.Complete(389,391,392)           // turn-in routing
Snapshot.Inventory.Has(2926,3628,3630,3640,4429,2909,8687)  // quest tokens
Snapshot.ServerCapabilities.EventAI.enabled       // script-readiness gate
```

---

## TBD / Backlog (per verification rule)

| Item | Status | Resolving grep |
|---|---|---|
| Sub-zone names "The Guardroom" / "The Crime Wing" / "Sentencing Hall" / "Ogre's Cellblock" / "Thredd's Chamber" | TBD — not in `world_full_14_june_2021.sql` | `AreaTable.dbc` extraction (mangos uses DBC for sub-zones); fallback wiki crawl |
| Defias Captive (1707) + Inmate (1708) full HP rows | Partial — `creature_template_addon` rows 17070/17080 located; full creature_template entries not dumped exhaustively | `Grep -E "^\s*\(170[78], 0, "` on world_full_14_june_2021.sql |
| GO 7529501 "The Stockade Chest" exact spawn coords | TBD — `gameobject_loot_template:7529501` lists items 31963/32162/26178/26179/31964 but spawn position not extracted | `Grep "7529501" gameobject_loot_template + gameobject spawns table` |
| Item 2909 "Red Wool Bandana" drop rate | TBD — referenced as quest 388 objective (10× collection) but drop-source-table rows not extracted here | `Grep -E "ReqItemId.*2909\|2909 .*Bandana"` on world_full_14_june_2021.sql |
| Mathias Shaw vanilla entry (NPC 332 is lvl 62 modern SI:7) | Possible regression — only one Mathias Shaw row exists in mangos.sql (entry 332 lvl 62) which post-dates the L29 Stockade chain; questgiver_relation for 393 needs verification | `Grep -E "121509.*332\|^\s*\(332, 393"` on world_full_14_june_2021.sql |

---

## Cross-References

- **Party invite handshake** (Alliance 5-man formation): [`../social/party-invite.md`](../social/party-invite.md) — `CMSG_GROUP_INVITE=110` flow, 60s decline window, faction gate via `ERR_PLAYER_WRONG_FACTION`.
- **Pull / engage**: [`../combat/pull-target.md`](../combat/pull-target.md), [`../combat/threat-management.md`](../combat/threat-management.md).
- **CC before pull** (Polymorph/Sap on Defias casters): [`../combat/cc-pull.md`](../combat/cc-pull.md), [`../combat/cast-spell.md`](../combat/cast-spell.md).
- **Interrupts / decurse** (Hamhock Curse of Weakness): [`../combat/utility-casts.md`](../combat/utility-casts.md).
- **Melee rotation** (Targorr / Deepfury / Bazil tank-and-spank): [`../combat/melee-rotation.md`](../combat/melee-rotation.md).
- **Heal task** (5-man healer triage): [`../combat/heal-task.md`](../combat/heal-task.md).
- **Wipe handling**: [`../recovery/corpse-run.md`](../recovery/corpse-run.md), [`../recovery/release-corpse.md`](../recovery/release-corpse.md), [`../recovery/spirit-healer.md`](../recovery/spirit-healer.md).
- **Quest turn-ins** (cross-zone routing Duskwood/Wetlands/Redridge/Stormwind): [`../npc/quest-giver.md`](../npc/quest-giver.md), [`../npc/gossip.md`](../npc/gossip.md).
- **Horde summon path**: [`../travel/warlock-summon.md`](../travel/warlock-summon.md).
- **Bracket context** (L20-L30): [`../sections/03-l20-l30.md`](../sections/03-l20-l30.md) (or nearest equivalent — verify pass 2).
- **Decision-engine integration**: [`../decision-engine/leveling-priority.md`](../decision-engine/leveling-priority.md), [`../decision-engine/state-flags.md`](../decision-engine/state-flags.md), [`../decision-engine/unlock-graph.md`](../decision-engine/unlock-graph.md).
- **Lore-bridge dungeon** (Bazil Thredd → Stonemasons → VanCleef arc): [`deadmines.md`](deadmines.md) — Stockades' chain quest 389 explicitly references Deadmines events (the Stonemasons riot, VanCleef-Bazil partnership).
- **Sibling dungeon** (next Alliance-natural at L22-30): [`shadowfang-keep.md`](shadowfang-keep.md) is Horde-natural but cross-faction at L22-30; otherwise [`deadmines.md`](deadmines.md) at L17-26 leads in.

---

## VMaNGOS / Server Reality Check

The Stockade is **fully EventAI-driven** (verified: no `instance_stockade.cpp` or `stockade.cpp` under `D:/MaNGOS/source/src/scripts/eastern_kingdoms/stormwind_city/` — only `quest_stormwind_rendezvous.cpp` + `stormwind_city.cpp` exist, both for the surrounding city, not the dungeon). EventAI data rows confirmed for all named bosses (Targorr 169602/169603, Deepfury 166601/166602/166604, Bazil 171601/171602/171605) + rare-spawns (Dextren 166301/166304, Bruegal 172001 + `event_scripts:10001`). Risk of script-break across modern VMaNGOS forks is **very low** — EventAI is data-driven and the Stockade's mechanics (stance-switch, flee, chain-lightning, enrage, smoke bomb) are all generic spell rows mapped to existing client `Spell.dbc` IDs.

No known boss-mechanic divergences from retail 1.12.1. The Bazil Thredd → Baros Alexston → Mathias Shaw chain (quests 389 → 391 → 392 → 393) has been stable since the 14-June-2021 world dump; the chain's Onyxia-attune downstream hint (Lord Gregor Lescovar + Marzon assassin) traces back to this Stockade encounter, making it the **earliest possible Alliance Onyxia-attune lore anchor** in the leveling guide.

The `npc_injured_patient` ScriptName on the Injured Stockade Guard (4996) refers to a **generic** C++ helper class shared across vanilla (used by various injured-NPC fixture quests elsewhere) — it does NOT make the Stockade itself script-bundled; verified by absence of a dedicated `instance_stockade` map_template entry.
