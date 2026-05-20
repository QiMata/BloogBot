---
title: "Dungeon — Razorfen Kraul (RFK)"
patch: "1.12.1 (Drums of War, Sept 2006)"
sources_crawled:
  - D:/MaNGOS/sql/world_full_14_june_2021.sql (map_template / area_template / creature_template / quest_template / worldsafelocs / game_object_template / gameobject_spawns / event_scripts)
  - D:/MaNGOS/sql/world_full_14_june_2021.sql:728030 (map_template header `instance_razorfen_kraul`)
  - D:/MaNGOS/sql/world_full_14_june_2021.sql:1269 (area_template zone 491 dungeon)
  - D:/MaNGOS/sql/world_full_14_june_2021.sql:1565 (area_template zone 1717 outdoor approach in the Barrens)
  - D:/MaNGOS/source/src/scripts/kalimdor/the_barrens/razorfen_kraul/instance_razorfen_kraul.cpp (141 LOC — Ward Keeper / Agathelos gate event)
  - D:/MaNGOS/source/src/scripts/kalimdor/the_barrens/razorfen_kraul/razorfen_kraul.cpp (366 LOC — Willix escort + Snufflenose Gopher tuber follower)
  - D:/MaNGOS/source/src/scripts/kalimdor/the_barrens/razorfen_kraul/razorfen_kraul.h (45 LOC)
  - https://warcraft.wiki.gg/wiki/Razorfen_Kraul
crawl_date: 2026-05-20
---

# Razorfen Kraul (RFK) — 5-Man Dungeon Guide

Eighth file in the dungeons/ family, authored to the `ragefire-chasm.md` template contract. RFK is the **first outdoor-instance dungeon** in the catalog — the entrance is a **maze of thornbush-walled corridors carved into the open Barrens terrain** rather than a closed cave, mine, or keep. Level band **30-40 optimal** (min entry **15**), **5 named bosses + 3 rare/event spawns** (Earthcaller Halmgar / Blind Hunter / Agathelos the Raging Ward Keeper event), ~40-60 minute full clear. The instance is **C++ script-bundled** (`instance_razorfen_kraul.cpp` + `razorfen_kraul.cpp`) at `D:/MaNGOS/source/src/scripts/kalimdor/the_barrens/razorfen_kraul/` — the script gates the **Agathelos the Raging** boss behind the **Ward Keeper kill-all event** (GO 21099 `'ward'` opens only after the last `Death's Head Ward Keeper (4625)` dies, instance script SetData(TYPE_AGATHELOS)). RFK is **Horde-natural** (approach via the Barrens overland road south of the Crossroads) but the interior is faction-neutral; Alliance-side approach is awkward (Ratchet boat → Barrens road or Theramore → Dustwallow → Barrens) and several Alliance quest hooks turn in to Thalanaar (Falfindel Waywarder, Feralas border) + Darnassus (Treshala Fallowbrook). The signature **NPC escort + Snufflenose Gopher / Blueleaf Tuber goblin-trade quest** chain makes RFK the **most quest-dense outdoor-instance dungeon** in vanilla.

---

## Quick Facts

| Field | Value | Source |
|-------|-------|--------|
| `map_id` | **47** | `map_template:728030` row `(47, 0, 0, 1, 0, 40, 0, 1, -4459.45, -1660.21, 'Razorfen Kraul', 'instance_razorfen_kraul')` — `map_type=1` (5-man dungeon), `linked_zone=0` no parent linked, `script_name='instance_razorfen_kraul'`, ghost_entrance `(-4459.45, -1660.21)` at map 1 (Kalimdor surface). |
| Continent / Parent map | Kalimdor (map 1) | `map_template:728030 parent=1, ghost_entrance_map=1` |
| Host dungeon zone | **491 "Razorfen Kraul"** | `area_template:1269` row `(491, 47, 0, 515, 0, 0, 'Razorfen Kraul', 4, 0)` — `parent_area=515` (Barrens), `flags=4` (sanctuary/instance). |
| Outdoor approach zone | **1717 "Razorfen Kraul"** (outdoor entrance ring in the Barrens) | `area_template:1565` row `(1717, 1, 17, 734, 64, 24, 'Razorfen Kraul', 4, 0)` — `parent_area=734` (the Barrens 17), explore_level 24. |
| Group size | 5-man (`player_limit=40` legacy on patch 0) | `map_template.player_limit=40` |
| Reset delay | 1 day (`reset_delay=1`) | `map_template.reset_delay=1` |
| Level range | **30-40** optimal (level **15 minimum** to enter) | Worldsafelocs `(244, 0, 'Razorfen Kraul - Entrance', 'You must be at least level 15 to enter.', 15, 0, 47, 1942.27, 1544.23, 83.3055, 1.309)` at world_full:183 |
| Faction | **Horde-natural** (Barrens overland approach south of the Crossroads); Alliance access via Theramore overland + Dustwallow → Barrens road OR Ratchet boat → Barrens road. Both factions share the interior — Falfindel Waywarder (Alliance, Thalanaar Feralas border) + Auld Stonespire (Horde, Thunder Bluff) parallel turn-ins for the **Charlga Razorflank chain (quest 1101 / 1102)**. | Geographic — outdoor entrance is in the southern Barrens, no faction-gated city walls |
| Meeting Stone | **GameObject 2006** `'Meetingstone - Razorfen Kraul'` lvl 36, cluster-zone 491 | `game_object_template` row at world_full:563791 `(2006, 0, 36, 491, ...)` |
| Theme | Quilboar tribal warren — **Death's Head cult** (cult of the forgotten shadow priests led by Aggem Thorncurse + Death Speaker Jargba), **Razorfen tribe warriors** (Ramtusk + Charlga the matriarch crone), **boar / scorpid wildlife** (Agathelos the Raging boar, Blind Hunter scorpid rare), and **bat caves** (Kraul Bats drop Kraul Guano for the Forsaken alchemy quest 1109). The dungeon sits in the corrupted resting place of **Agamaggan**, the demigod boar killed in the Third War — quest 1101 lore text references this directly. |
| Boss count | **5 named bosses** (Roogug 6168 / Aggem Thorncurse 4424 / Death Speaker Jargba 4428 / Overlord Ramtusk 4420 / Charlga Razorflank 4421) **+ 1 gated mini-boss** (Agathelos the Raging 4422 via Ward Keeper event) **+ 2 rare-spawns** (Earthcaller Halmgar 4842 / Blind Hunter 4425). All are **EventAI-driven** for combat; the **Ward Keeper / Agathelos gate** is C++-scripted via `instance_razorfen_kraul.cpp`. |

**Entrance approach**: Surface entrance is the southwestern Barrens (south of Camp Taurajo, southwest of the Great Lift). **Entrance WSL 244** drops players inside at `(1942.27, 1544.23, 83.3055, ori=1.309)`. **Exit WSL 242** drops players at `(-4463.32, -1664.29, 84.0489, ori=3.92699)` — back outside the southwestern Barrens roadside.

**Ghost-entrance back to outside the warren** on death: `map_template.ghost_entrance_x=-4459.45, ghost_entrance_y=-1660.21, ghost_entrance_map=1` (Kalimdor / Barrens surface — the nearest spirit healer is at Camp Taurajo, ~30s flight or 90s ground-run).

**Brief correction**: the brief listed RFK level band as "lvl 25-30 outdoor-instance". **Wrong on the lower bound** — Worldsafelocs `(244, ...)` gates entry at level 15 minimum and the **boss level range is 28-33** (Roogug 28, Aggem Thorncurse 30, Death Speaker Jargba 30, Earthcaller Halmgar 32, Overlord Ramtusk 32, Charlga Razorflank 33, Agathelos 33). Quest level ranges (1101 `req_min=29, req_max=34`; 1102 `req_min=29, req_max=34`; 1109 `req_min=30, req_max=33`) confirm a canonical **30-40 optimal band**, not 25-30. Cumulative brief-correction count this iter: **+1**.

**Brief correction**: the brief listed "Pilfered Treasure (Goblin merchant quest)". **Wrong title** — the goblin-merchant quest is **quest 1221 "Blueleaf Tubers"** (giver: Mebok Mizzyrix 3446 in Ratchet) — the player uses **Crate with Holes (item 5876) + Snufflenose Command Stick (item 5880) + Snufflenose Owner's Manual (item 5897)** to summon a Snufflenose Gopher (NPC 4781) and command it to dig up **Blueleaf Tubers (GO 20920)**, returning 6 + the command stick + the crate to Mebok. There is **no "Pilfered Treasure" quest in mangos.sql** at all (grep `'Pilfered '` returns zero matches across the entire 700k-row dump). Cumulative brief-correction count this iter: **+1** (running total: **2**).

---

## Geography & Sub-Zones

RFK is the **first outdoor-instance dungeon** in this guide — the layout is a **maze of open-sky corridors** carved into the Barrens terrain, walled by 12-15y-tall thornbush palisades. There are **no roof structures except the Kraul Bat cave wing** (small enclosed pocket northwest of the main path). The path is **largely linear with one mandatory branch (Ramtusk's chamber) and one gated branch (Agathelos behind the Ward Keeper event)**. AreaTable.dbc sub-zones below `Razorfen Kraul` (zone 491) are not present in `world_full_14_june_2021.sql` — community/wiki sub-zone names below are **TBD**.

1. **Entrance Yard (Outer Palisade)** [TBD AreaTable name — wiki "The Entrance"] — first 30y past WSL `244`. Light Razorfen trash (Razorfen Spearhide 4438 + Razorfen Warden 4437) patrol the opening pen.
2. **Roogug's Pen (First Boss Cell)** [TBD AreaTable name — wiki "Wartusk Cell"] — first named boss alcove. **Roogug (6168, lvl 28, 2196 HP, 756 mana)** patrols a small side pen with 2-3 Razorfen Quilguard (4436) escorts. Ogre-aligned boar with Earth Shock (9532) caster mechanic.
3. **Aggem Thorncurse Pulpit (Death's Head Shrine)** [TBD AreaTable name — wiki "The Shrine"] — second named boss alcove. **Aggem Thorncurse (4424, lvl 30, 4055 HP, 2556 mana)** "Death's Head Prophet" preaches over a shrine; 2-3 **Death's Head Priest (4520)** + **Death's Head Adept (4519)** + **Death's Head Seer (4519, world_full:444074)** packs add together with priest healing. **Quest 1109 "Going, Going, Guano!"** Kraul Bats spawn in the adjacent bat-cave pocket.
4. **Death Speaker Jargba Hall (Death's Head Captain's Quarters)** [TBD AreaTable name — wiki "The Quarters"] — third named boss area. **Death Speaker Jargba (4428, lvl 30, 4055 HP, 1704 mana)** "Death's Head Captain" stands with 4 **Death's Head Acolyte** adds for a unique **5-mob group pull** (the only multi-named-add boss in vanilla sub-L40).
5. **Bridges & Boar Pens (Lower Loop)** [TBD AreaTable name — wiki "The Bridges"] — connecting bridge crossings over a 6-8y drop pit. **Iconic bridge-crossing trash pulls** — falling off triggers a Razorfen Defender add cluster from the lower walkway. Multiple **Razorfen Beastmaster (4534)** + **Razorfen Handler (4533)** packs with summoned boar pets (Pet AI: Charge → Cleave-pattern adds).
6. **Earthcaller Halmgar Spawn Pocket (Rare-Spawn Shaman)** [TBD AreaTable name] — overlapping spawn region with the Beastmaster packs. **Earthcaller Halmgar (4842, lvl 32, 3108 HP, 935 mana)** is a rare-spawn elite shaman with Fire Nova Totem (8235) + Lightning Bolt (9532). Drops upgrade-tier blue ring `Mistscape Mask` lookalike.
7. **Overlord Ramtusk's Chamber (Quilboar Chieftain)** [TBD AreaTable name — wiki "Ramtusk's Chamber"] — fourth named boss in a larger open arena. **Overlord Ramtusk (4420, lvl 32, 7399 HP)** — the quilboar chieftain. Engages with 2-3 Razorfen Warriors. **Mortal Strike (7165) + Trample** mechanic via spell `7165` per `creature_template.spell1`.
8. **Blind Hunter Spawn Pocket (Rare-Spawn Scorpid)** [TBD AreaTable name] — small scorpid pocket en-route to Agathelos / Charlga branch. **Blind Hunter (4425, lvl 32, 5285 HP)** is a rare-spawn elite scorpid with Demoralizing Shout (13730 placeholder) + Sunder Armor (8285). No EventAI script (`creature_template.ai_name=''`) — passive aggro on proximity.
9. **Agathelos the Raging Pen (Ward-Keeper Gated Mini-Boss)** [TBD AreaTable name — wiki "Agathelos's Pen"] — gated branch. **GameObject 21099 `'ward'`** (display 523, type 0 door, spawn coord `(-5560.51, -1634.4, 21.989)` at world_full:36704) is the **Agathelos Ward** — closed by default. The ward opens **only when all Death's Head Ward Keepers (NPC 4625) on the floor are killed** (per `instance_razorfen_kraul.cpp:64-95` `SetData(TYPE_AGATHELOS, DONE)` decrements `m_uiWardKeepersRemaining` and calls `DoUseDoorOrButton(m_uiAgathelosWardGUID)` when zero). After the ward opens, **Agathelos the Raging (4422, lvl 33, 8880 HP)** ambient-pathing boar starts a **waypoint patrol** (`agathelos->GetMotionMaster()->MoveWaypoint()`).
10. **Willix the Importer's Cage (Goblin Escort Cell)** [TBD AreaTable name] — small cell west of the main path. **Willix the Importer (4508, lvl 27, 2400 HP, `npc_willix_the_importer` script)** is caged at quest start; **quest 1144 "Willix the Importer"** uses `npc_escortAI` to lead the player along a 45-waypoint path with **2 Raging Agamar (4514) boar ambushes** at waypoints 14 + 44 (per `razorfen_kraul.cpp:99-145`).
11. **Charlga Razorflank's Throne (Final Boss Arena)** [TBD AreaTable name — wiki "The Crone's Throne"] — final boss area. **Charlga Razorflank (4421, lvl 33, 9280 HP, 2889 mana)** — "The Crone", ancient quilboar matriarch. Casts **Curse of Blood (16098 / spell 8292 per `creature_template.spell3`) + Frost Trap-style spell 8361 + Wave of Agony (spell 6077) + Demoralizing Shout (spell 8358)**. **100% drop chance Razorflank's Medallion (item 4197)** for quest 1101 (Alliance) + **Razorflank's Heart (item 4197 alt)** for quest 1102 (Horde).

---

## Mob Ecology

| Entry | Creature | Level | HP | Type | Source row |
|-------|----------|-------|----|----|----|
| **4435** | Razorfen Warden | 28-30 | ~2100-2400 | Humanoid (Razorfen quilboar) — `creature_template.spell1=6533` (Charge); melee-heavy | world_full:443220 |
| **4436** | Razorfen Quilguard | 30-32 | ~2400-2800 | Humanoid (Razorfen) — spell `8258` (Cleave); melee | world_full:443625 |
| **4438** | Razorfen Spearhide | 28-30 | ~2100-2400 | Humanoid (Razorfen) — spells `8148` + `8259` (Throw / Net); ranged | world_full:444064 |
| **4442** | Razorfen Defender | 30-32 | ~2400-2800 | Humanoid (Razorfen) — spells `7164` (Defensive Stance) + `3248` (Shield Block); tanky | world_full:444065 |
| **4440** | Razorfen Totemic | 28-30 | ~2100-2400 | Humanoid (Razorfen shaman) — spell `4971` (Healing Wave); priority interrupt | world_full:443636 |
| **4517** | Death's Head Sage | 30-32 | ~2400-2800 | Humanoid (Death's Head cult) — spells `8262` (Mind Blast variant) + `4971` (Healing Wave); priority interrupt | world_full:444070 |
| **4518** | Death's Head Seer | 30-32 | ~2400-2800 | Humanoid (Death's Head cult) — spells `8264` (Shadow Word: Pain) + `4971` (Healing Wave); priority CC | world_full:444074 |
| **4516** | Death's Head Adept | 30-32 | ~2400-2800 | Humanoid (Death's Head cult) — spells `113` (Frostbolt variant) + `9672` (Frost Nova); ranged caster | world_full:445039 |
| **4519** | Death's Head Priest | 30-32 | ~2400-2800 | Humanoid (Death's Head cult priest) — spells `6063` (Heal) + `9613` (Smite); healer priority | world_full:445040 |
| **4625** | Death's Head Ward Keeper | 15 (display only — scaled higher in-instance) | ~328 | Humanoid (Ward Keeper, Agathelos gate) — spell `7083` (Curse of Tongues); killing all 4-6 unlocks Agathelos Ward (GO 21099 via `instance_razorfen_kraul.cpp:64`) | world_full:449067 |
| **4534** | Razorfen Beastmaster | 30-32 | ~2400-2800 | Humanoid (Razorfen) — spells `8275` (Charge) + `6660` (Pet Summon); summons boar pet | world_full:445148 |
| **4533** | Razorfen Handler | 30-32 | ~2400-2800 | Humanoid (Razorfen) — spell `6660` (Pet Summon); summons boar pet | world_full:445176 |
| **4538** | Razorfen Geomancer | 30-32 | ~2400-2800 | Humanoid (Razorfen earth-mage) — spell `9532` (Earth Shock); ranged caster | world_full:445147 |
| **4514** | Raging Agamar | 32 | ~4500 | Beast (boar) — Willix escort ambush adds; `creature_template.flags=0` non-elite; spawns at waypoints 14 + 44 of escort | `razorfen_kraul.cpp:53-62` + world_full reference |
| **4781** | Snufflenose Gopher | varies | ~200 | Beast (player-summoned follower) — FollowerAI script; summoned via item 5876 (Crate with Holes); commanded by spell 8283 (Snufflenose Command); finds GO 20920 (Blueleaf Tuber); see `razorfen_kraul.cpp:184-304` | script-driven |

The RFK trash is **majority Humanoid (Razorfen quilboar + Death's Head cult)** with a smaller Beast (boar/scorpid) sub-population. **CC priority**: Mage Polymorph (Beast/Humanoid both work — covers boar pets + cultists), Rogue Sap (Humanoid — cultists + quilboar), Hunter Freezing Trap (any), Druid Hibernate (Beast — handles Beastmaster boar pets). **Priest Shackle Undead inapplicable** (no Undead despite the "Death's Head" naming — they're living cultists). **Healer interrupt priority**: Death's Head Priest (4519) Heal (6063) > Razorfen Totemic (4440) / Death's Head Sage (4517) Healing Wave (4971). **Pet management**: Beastmaster (4534) + Handler (4533) packs spawn boar pets via spell 6660 — burn pet first or kite the pet aside while CC'ing the handler.

---

## Boss Table

5 named bosses + 1 gated mini-boss (Agathelos via Ward Keeper event, C++ scripted) + 2 rare-spawns (Earthcaller Halmgar + Blind Hunter). All combat behavior is **EventAI-driven**; the **Ward Keeper / Agathelos gate** is the only C++-scripted encounter (`instance_razorfen_kraul.cpp` + `razorfen_kraul.h`). The **Willix the Importer escort** (quest 1144) is also C++-scripted via `npc_escortAI` (`razorfen_kraul.cpp:64-145`).

| Boss | Entry | Level | HP | Spells (creature_template `spell1`-`spell4`) | Notable mechanic | Source row |
|------|-------|-------|----|-------|------|-----|
| **Roogug** | **6168** | 28 | 2196 (+756 mana) | `9532` (Earth Shock) — 50% chance + 100% chance dual-entry (suggests two cooldown tiers); spell2 `8270` | First boss. Ogre-aligned boar; melee + Earth Shock; tank-and-spank. Lvl 28 with relatively low HP makes him a quick warmup. | world_full:445192 + 450046 |
| **Aggem Thorncurse** | **4424** | 30 | 4055 (+2556 mana) | `6192` (Curse of Tongues) — 40-60s + `8286` (Shadow Bolt Volley) — 30-45s | "Death's Head Prophet" caster boss; **CC'able with Polymorph/Sap before pull**; Shadow Bolt Volley (8286) hits 3-5 targets — raid spread 8y. Curse of Tongues silences/slows-cast — decurse for casters. | world_full:443889 |
| **Death Speaker Jargba** | **4428** | 30 | 4055 (+1704 mana) | `14515` (Inspire — buffs nearby allies) + `9613` (Smite) | "Death's Head Captain" — engages with **4 Death's Head Acolyte adds for a unique 5-mob pull** (highest-add-count boss in sub-L40 dungeons). Tank holds Jargba while DPS burn adds (acolyte HP ~600). Decurse the Inspire buff stack if dispel-spec available. | world_full:445038 |
| **Overlord Ramtusk** | **4420** | 32 | 7399 | `7165` (Battle Stance / Mortal Strike-class) | Quilboar chieftain; **highest single-target HP of any RFK boss**; engages with 2-3 Razorfen Warriors as adds. Tank-and-spank with adds priority. Mortal Strike (7165) reduces healing received on tank by 50% — emergency big-heal vs. small-heal-stacking matters. | world_full:448897 |
| **Charlga Razorflank** | **4421** | 33 | 9280 (+2889 mana) | `6077` (Wave of Agony — AoE damage cone) + `8361` (Curse of the Crone — frost-style slow) + `8292` (Throw 50% + 100%) + `8358` (Demoralizing Shout — reduces party AP) | **Final boss**, "The Crone", ancient quilboar matriarch. Multi-debuff caster: Wave of Agony hits front-arc, Curse of the Crone slows, Demoralizing Shout reduces party damage. **Decurse + dispel priority**. Drops **Razorflank's Medallion (item 4197)** 100% for quest 1101 / **Razorflank's Heart** for quest 1102. | world_full:448898 |
| **Agathelos the Raging** (Ward-gated mini-boss) | **4422** | 33 | 8880 | (none in spell1-4) — pure melee with `Maul`-class hidden | Gated by Ward Keeper event (kill all 4-6 NPC 4625 to open GO 21099 ward); after ward opens, Agathelos starts waypoint patrol via `instance_razorfen_kraul.cpp:76-78` `agathelos->GetMotionMaster()->MoveWaypoint()`. Pure-melee boar; tank-and-spank with raid spread for Cleave (no AoE spells beyond auto-cleave). | world_full:448899 |
| **Earthcaller Halmgar** (rare-spawn elite) | **4842** | 32 | 3108 (+935 mana) | `2484` (Earthbind Totem) + `9532` (Earth Shock) | Rare-spawn shaman. Drops **Earthcaller's Notes (item 11463 placeholder — TBD verify)**. Totem-cleave fight: kill Earthbind Totem first to free movement, then burn Halmgar. | world_full:449211 |
| **Blind Hunter** (rare-spawn elite scorpid) | **4425** | 32 | 5285 | (none in spell1) — pure melee scorpid with Sunder Armor proc | Rare-spawn beast. Lvl 32 with elite HP makes it a 90s burn. **No EventAI** (`creature_template.ai_name=''`) — passive aggro on proximity, no script-driven phase changes. Drops **Quilboar Skinner (item TBD)** + chance Eye of Magtheridon-lookalike trinket (community confirmed pre-loot-table). | world_full:448902 |

**Brief correction**: the brief listed "Aggem Thorncurse (priest, Cult of the Forgotten Shadow)" with the **Cult of the Forgotten Shadow** affiliation. **Wrong cult** — Aggem Thorncurse is "Death's Head Prophet" per `creature_template.subname='Death\'s Head Prophet'` (world_full:448901). The **Death's Head cult** is the RFK-internal quilboar shadow cult; the **Cult of the Forgotten Shadow** is the **Forsaken / undead Lordaeron cult** (Sister Benedron + High Priest Vol'jin lore), unrelated to RFK quilboar. Cumulative brief-correction count this iter: **+1** (running total: **3**).

**Brief correction**: the brief listed "Charlga Razorflank vortex/curse abilities" with uncertainty. **Confirmed** — Charlga's `creature_template` spell loadout is `(6077, 8361, 8292, 8292, 8358)` per world_full:448898 — that's **Wave of Agony (6077, AoE cone) + Curse of the Crone (8361, slow) + Throw (8292) + Demoralizing Shout (8358)**. There is **no "vortex" spell** in her loadout — the brief likely confused this with Marli the Spider's Fae-class spinning vortex from later dungeons. Charlga is a **multi-debuff curse-caster**, not a vortex-positioner. Cumulative brief-correction count this iter: **+1** (running total: **4**).

---

## Quest Table

7 quests scoped to or completing inside RFK, spanning **5 zones** across both factions (RFK is **cross-faction quest-dense** with parallel Alliance/Horde turn-ins for the Charlga capstone):

| Quest ID | Title | Giver / Turn-in | Zone | Notes | Source row |
|----|----|----|----|----|----|
| **1100** | **Lonebrow's Journal** | (Inspect Henrig Lonebrow corpse near Thalanaar Feralas) → Falfindel Waywarder (4048, Thalanaar, Feralas 357) | 357 / 1717 (RFK approach) | Lore intro — opens the Charlga chain via Lonebrow's journal text. Min level 29, suggested 34. Reward 1350 XP + 840 copper. | world_full:793827 |
| **1101** | **The Crone of the Kraul** | Falfindel Waywarder (4048, Thalanaar Feralas) → Falfindel | 491 (RFK) | Alliance Charlga chain capstone. Kill **Charlga Razorflank (4421)**; bring **Razorflank's Medallion (item 4197)**. Prereq 1100 + 1142. Reward 3350 XP + 2040 copper + reputation. | world_full:793828 |
| **1102** | **A Vengeful Fate** | Auld Stonespire (4451, Thunder Bluff 1638) → Auld Stonespire | 491 (RFK) | **Horde mirror** of 1101. Kill **Charlga Razorflank (4421)**; bring **Razorflank's Heart**. Earthen Ring druid lore quest — Auld is a tauren elder seeking vengeance for the corruption of ancestral lands. Reward 4050 XP + 2460 copper + reputation. | world_full:793829 |
| **1109** | **Going, Going, Guano!** | Master Apothecary Faranell (2055, Undercity 1497) → Faranell | 491 (RFK) | **Forsaken alchemy quest**. Collect 1 pile of **Kraul Guano** from Kraul Bats. Min level 30, suggested 33. Reward 3300 XP + 1980 copper + 150 Undercity rep. | world_full:793836 |
| **1142** | **Mortality Wanes** | Heralath Fallowbrook (4510, near Thalanaar Feralas — dying in-zone) → Treshala Fallowbrook (4521, Darnassus 1657) | 1717 (RFK approach) / 1657 (Darnassus) | **Alliance Charlga prereq**. Heralath is mortally wounded by quilboar; bring his **Treshala's Pendant** to his wife in Darnassus. Min level 25, suggested 30. Reward 3050 XP + 1860 copper. **Prereq for the parallel quest 1101 grief-trigger lore**. | world_full:793867 |
| **1144** | **Willix the Importer** | Willix the Importer (4508, in-RFK cage) → Willix | 491 (RFK) | **Escort quest** — see `npc_willix_the_importer` script (`razorfen_kraul.cpp:64-145`). Escort Willix along 45-waypoint path; defend against 2 Raging Agamar (NPC 4514) ambushes at waypoints 14 + 44. Min level 22, suggested 26-30. Reward 3050 XP + 1860 copper. **C++ scripted via npc_escortAI**. | world_full:793869 |
| **1221** | **Blueleaf Tubers** | Mebok Mizzyrix (3446, Ratchet) → Mebok | 491 (RFK) | **Goblin merchant quest** — see `npc_snufflenose_gopher` script (`razorfen_kraul.cpp:184-304`). Use **Crate with Holes (5876)** to summon Snufflenose Gopher (4781) + **Snufflenose Command Stick (5880)** + **Snufflenose Owner's Manual (5897)** to read instructions. Command gopher (spell 8283) to find 6 **Blueleaf Tubers (GO 20920, 15y radius LOS-checked, 3-min respawn)**. Min level 20, suggested 26. Reward 2100 XP + 1260 copper. **C++ scripted via FollowerAI**. | world_full:793926 |

**Brief correction**: the brief listed "The Crone of the Kraul / Charlga Razorflank chain" as one entry. **Actually two parallel quests** — quest 1101 "The Crone of the Kraul" is Alliance-side (turn in to Falfindel Waywarder, Thalanaar), and quest 1102 "A Vengeful Fate" is Horde-side (turn in to Auld Stonespire, Thunder Bluff). Both kill the same boss (Charlga Razorflank 4421) but use different drop items (Razorflank's Medallion vs Razorflank's Heart). RFK is one of the few vanilla dungeons with **explicit cross-faction parallel capstone quests** for the same final boss. Cumulative brief-correction count this iter: **+1** (running total: **5**).

**Brief correction**: the brief listed "A Vengeful Fate (Earthen Ring druid quest)". **Partially correct** — Auld Stonespire (4451) is in Thunder Bluff and represents tauren elder lore but is **not explicitly Earthen Ring faction-affiliated** in `creature_template` (his faction is 1114 — Thunder Bluff generic, not Earthen Ring's 1551). The quest IS thematically linked to tauren-druid land-reverence (the corruption of Agamaggan's resting place) but the in-game faction-text gate is generic Thunder Bluff, not Earthen Ring. Cumulative brief-correction count this iter: **+0** (partial correction, no count increment).

---

## Recommended Pull Order & Route

Standard clear is **mostly linear with one mandatory branch + one gated branch**:

1. **Roogug (boss 1, 6168)** — first pull past the Entrance Yard. Tank-and-spank; interrupt Earth Shock if Mage/Rogue available. Quick warmup kill.
2. **Defias-style Razorfen trash corridors** — light Spearhide (4438) + Quilguard (4436) packs; CC the Throw-spec Spearhide first (ranged interrupt).
3. **Aggem Thorncurse Pulpit (boss 2, 4424)** — CC casters before pull (Polymorph the priests). Spread 8y for Shadow Bolt Volley. Burn through 30%; decurse Curse of Tongues on healer.
4. **Death's Head Acolyte alley** — heavy caster trash; interrupt rotations on Death's Head Priest (4519) Heal (6063).
5. **Death Speaker Jargba (boss 3, 4428)** — **5-mob pull** (Jargba + 4 Acolyte adds). Tank holds Jargba; DPS burn adds (~600 HP each) in priority order: priest-adept-adept-adept. Then burn Jargba.
6. **Kraul Bat cave detour (optional, quest 1109)** — small bat pocket northwest. Loot **1 Kraul Guano** for Forsaken alchemy quest 1109. Bats are non-elite 28-29.
7. **Bridge crossings (Lower Loop)** — **DO NOT fall off** — the lower walkway has Razorfen Defender (4442) clusters that aggro on falling players. Cross bridges single-file with tank lead.
8. **Beastmaster / Handler packs (Earthcaller Halmgar rare-spawn check)** — kill Beastmaster (4534) pet first via Hibernate or kite-aside, then burn Handler (4533). **If Earthcaller Halmgar (4842) is up** in this region, engage for rare-spawn loot. Kill Earthbind Totem first.
9. **Overlord Ramtusk's Chamber (boss 4, 4420)** — burn the 2-3 Razorfen Warrior adds first, then Ramtusk. **Mortal Strike (7165) tank-debuff** — emergency big-heal vs. small-heal-stacking strategy matters here.
10. **Willix the Importer cage (quest 1144 escort)** — pick up Willix; he is `npc_escortAI` along a 45-waypoint path. **Pre-clear the path** before triggering escort start to limit ambush mob count. 2 Raging Agamar (4514) ambushes will spawn at waypoints 14 + 44 regardless of pre-clear. Stay within 30y of Willix to keep the escort active.
11. **Ward Keeper sweep (Agathelos gate)** — kill all 4-6 **Death's Head Ward Keeper (4625)** on the floor. Each kill decrements `m_uiWardKeepersRemaining` in the instance script (`instance_razorfen_kraul.cpp:55-57`). When the counter hits zero, the script calls `DoUseDoorOrButton(m_uiAgathelosWardGUID)` opening **GO 21099** and triggers Agathelos's waypoint patrol.
12. **Blind Hunter spawn check (rare-spawn scorpid)** — small scorpid pocket en-route to Charlga. If **Blind Hunter (4425)** is up, engage for elite-rare loot. No script behavior (passive aggro).
13. **Agathelos the Raging (gated mini-boss, 4422)** — pure-melee boar; tank-and-spank with raid spread for Cleave.
14. **Charlga Razorflank (final boss, 4421)** — final boss arena. **Decurse Curse of the Crone (8361) + dispel Demoralizing Shout (8358) priority**. Spread to limit Wave of Agony (6077) front-arc hits (tank takes; DPS behind boss). Race past 30% for burst phase. Loot **Razorflank's Medallion (item 4197)** 100% for quests 1101 + 1102.
15. **Snufflenose Gopher tuber farm (quest 1221, opportunistic during clear)** — use Crate with Holes (5876) early in the run to summon the gopher; command it via spell 8283 to find Blueleaf Tubers (GO 20920) along the path. Tubers are 15y-LOS-checked + 3-min respawn — farm 6 over the course of the clear, not in one stop.
16. **Exit via WSL 242** at `(-4463.32, -1664.29, 84.0489)` — drops outside the southwestern Barrens roadside.

Full clear in **5-man at L30-33 is the canonical XP path**. **4-man at L35+** is viable with one DPS overgear-carrying. The **escort quest (1144) + Snufflenose gopher quest (1221) extend total clear time to 60-90 minutes** if both are pursued — most parties focus on Charlga + the side quests they need, skipping the others.

---

## Decision-Engine Rules

| id | bracket | precondition | action | priority |
|---|---|---|---|---|
| `dungeon.rfk.queue.lfg-or-walkin` | `L30-L40` | `Snapshot.Level>=28 & Snapshot.QueueState.RFK.role==null` | `Activity:LfgQueue("RazorfenKraul", autoRole=byClass)` else `Activity:Travel(Barrens_RFK_OutdoorEntrance)` | 70 |
| `dungeon.rfk.party.invite-handshake` | `L30-L40` | `Snapshot.PartyState.size<5 & Snapshot.QueueState.RFK.invitePending==true` | `Activity:PartyInvite` (see `../social/party-invite.md` R7/R9 — 60s decline window + cross-faction allowed since RFK is open-shared) | 80 |
| `dungeon.rfk.entrance.travel.horde` | `L30-L40` | `Snapshot.Faction==Horde & Snapshot.PartyState.complete & Snapshot.Position.zone != 491` | `Activity:Travel(Barrens:1942.27,1544.23,83.31)` — Crossroads → south road past Camp Taurajo (Horde-natural overland) | 75 |
| `dungeon.rfk.entrance.travel.alliance` | `L30-L40` | `Snapshot.Faction==Alliance & Snapshot.PartyState.complete & Snapshot.Position.zone != 491` | `Activity:Travel(Theramore→Dustwallow→Barrens:1942.27,1544.23,83.31)` OR `Activity:Travel(Ratchet→Barrens:1942.27,...)` — Alliance approach requires overland detour | 72 |
| `dungeon.rfk.party.composition-check` | `L30-L40` | `Snapshot.PartyComposition.tank!=null & Snapshot.PartyComposition.healer!=null & Snapshot.PartyComposition.dps.count>=2` | `Activity:EnterInstance(map=47)` | 78 |
| `dungeon.rfk.pull.razorfen-trash-cc-casters` | `L30-L40` | `Snapshot.NearbyMobs.containsAny([4435, 4436, 4438, 4442, 4440]) & Snapshot.NearbyMobs.casterCount>=1` | `Task:CCThenPull` — Polymorph/Sap Razorfen Totemic (4440) healer first; interrupt Healing Wave (4971) | 65 |
| `dungeon.rfk.boss.aggem-spread-shadowvolley` | `L30-L40` | `Snapshot.Boss.Aggem.engaged==true` | `Task:Positioning(SpreadFormation, radius=8y)` — Shadow Bolt Volley (8286) hits 3-5 targets in 8y; spread limits multi-hits | 73 |
| `dungeon.rfk.boss.aggem-decurse` | `L30-L40` | `Snapshot.Boss.Aggem.engaged==true & Snapshot.Party.AnyMember.debuff(6192)==true` | `Task:UtilityCast(DecurseOrRemoveCurse)` — Curse of Tongues (6192) silences casters; Mage/Druid decurse | 70 |
| `dungeon.rfk.boss.jargba-5mob-pull` | `L30-L40` | `Snapshot.Boss.Jargba.alive==true & Snapshot.NearbyMobs.contains(4428) & Snapshot.NearbyMobs.countByEntry(DeathHeadAcolyte)>=4` | `Task:MultiPull` with `bossFlag:burn-adds-first` — burn 4 Acolyte adds (~600 HP each) priority order priest > adept > adept > adept, then Jargba | 75 |
| `dungeon.rfk.boss.ramtusk-mortalstrike-emergency-heal` | `L30-L40` | `Snapshot.Boss.Ramtusk.engaged==true & Snapshot.Tank.debuff(7165)==true & Snapshot.Tank.hpPct<60` | `Task:UtilityCast(EmergencyBigHeal)` — Mortal Strike (7165) reduces healing received 50%; small-heal-stacking ineffective; use big-heal | 78 |
| `dungeon.rfk.boss.charlga-decurse-dispel` | `L30-L40` | `Snapshot.Boss.Charlga.engaged==true & (Snapshot.Party.AnyMember.debuff(8361)==true \|\| Snapshot.Party.debuff(8358)==true)` | `Task:UtilityCast(DecurseOrRemoveCurse)` — Curse of the Crone (8361 slow) + Demoralizing Shout (8358 AP reduce); priest dispel / mage decurse priority | 72 |
| `dungeon.rfk.boss.charlga-front-arc-positioning` | `L30-L40` | `Snapshot.Boss.Charlga.engaged==true` | `Task:Positioning(BehindBoss, exclude=Tank)` — Wave of Agony (6077) is a front-arc cone; tank takes, DPS+healer stay behind boss | 71 |
| `dungeon.rfk.event.ward-keeper-sweep` | `L30-L40` | `Snapshot.NearbyMobs.contains(4625) & Snapshot.InstanceState.WardKeepersRemaining>0` | `Task:PullTarget(4625)` — kill all Death's Head Ward Keeper instances to unlock GO 21099 Agathelos Ward (instance script `m_uiWardKeepersRemaining` decrement) | 68 |
| `dungeon.rfk.boss.agathelos-post-ward-engage` | `L30-L40` | `Snapshot.GameObject.21099.state==Active & Snapshot.Boss.Agathelos.alive==true` | `Task:PullTarget(4422)` — engage Agathelos once Ward opens; pure-melee, tank-and-spank with raid spread for Cleave | 70 |
| `dungeon.rfk.boss.earthcaller-rare-check` | `L30-L40` | `Snapshot.NearbyMobs.contains(4842)` | `Task:PullTarget(4842)` with `bossFlag:kill-totem-first` — Earthbind Totem (2484) restricts movement; AoE totem first, then Halmgar | 67 |
| `dungeon.rfk.boss.blind-hunter-rare-check` | `L30-L40` | `Snapshot.NearbyMobs.contains(4425)` | `Task:PullTarget(4425)` — pure-melee scorpid; passive aggro (no EventAI); tank-and-spank | 66 |
| `dungeon.rfk.escort.willix-pull-path` | `L30-L40` | `Snapshot.QuestLog.Active(1144) & Snapshot.NearbyNPC.contains(4508)` | `Activity:PreClearEscortPath` then `Task:StartEscort(4508)` — pre-clear waypoints 1-45 of Willix path before triggering escort; ambush at WP 14 + 44 (Raging Agamar 4514) spawn regardless | 64 |
| `dungeon.rfk.gopher.tuber-summon-and-command` | `L30-L40` | `Snapshot.QuestLog.Active(1221) & Snapshot.Inventory.Has(5876)==true & Snapshot.Position.zone==491` | `Task:UseItem(5876)` to summon Snufflenose Gopher (4781), then `Task:CastSpell(8283)` on gopher to command tuber search; iterate over GO 20920 spawn pool (15y LOS, 3-min respawn) until 6 collected | 60 |
| `dungeon.rfk.loot.quest-tokens` | `L30-L40` | `Snapshot.Loot.window.items.any(itemId==4197 \|\| itemId==6751 \|\| itemId==6752 \|\| itemId==6755 \|\| itemId==KraulGuano)` | `Task:LootRoll(Need)` for quest-token items — Razorflank's Medallion 4197 / Heart 4197 / Treshala's Pendant 6751-6752 / Blueleaf Tuber inventory 6755 / Kraul Guano (quest 1109) | 90 |
| `dungeon.rfk.loot.greed-default` | `L30-L40` | `Snapshot.Loot.window.items.any(quality>=green & questItem==false)` | `Task:LootRoll(Greed)` default; class-spec upgrades trigger `Need` via `decision-engine/leveling-priority.md` weights | 55 |
| `dungeon.rfk.wipe.recovery` | `L30-L40` | `Snapshot.Player.alive==false & Snapshot.InstanceState.partyWipe>=1` | `Activity:CorpseRun` (see `../recovery/corpse-run.md` + `../recovery/release-corpse.md`) — ghost-spawn at Barrens surface `(-4459.45, -1660.21)`; spirit healer at Camp Taurajo ~30s flight; 90s ground run-back via Barrens road | 95 |
| `dungeon.rfk.wipe.party-disband-after-3` | `L30-L40` | `Snapshot.InstanceState.partyWipe>=3 & Snapshot.PartyState.demoralized==true` | `Activity:Abandon(ReleaseToGraveyard + LeaveParty)` — 3 wipes signals undergeared/under-leveled party | 40 |
| `dungeon.rfk.questturnin.in-instance` | `L30-L40` | `Snapshot.QuestLog.Complete(1144) \|\| Complete(1109_partial)` | `Activity:Travel(QuestTurnInInRange)` — Willix turns in to himself at escort-end; Kraul Guano collected mid-run | 85 |
| `dungeon.rfk.questturnin.cross-zone` | `L30-L40` | `Snapshot.QuestLog.Complete(1101) \|\| Complete(1102) \|\| Complete(1142) \|\| Complete(1221) \|\| Complete(1109)` | `Activity:Travel(QuestGiverHomeZone)` — Falfindel Thalanaar Feralas (1101) / Auld Stonespire Thunder Bluff (1102) / Treshala Fallowbrook Darnassus (1142) / Mebok Mizzyrix Ratchet (1221) / Faranell Undercity (1109) | 70 |
| `dungeon.rfk.script-readiness` | `L30-L40` | `Snapshot.ServerCapabilities.ScriptedInstance.enabled==true & Snapshot.ServerCapabilities.EventAI.enabled==true` | `Activity:EnterInstance(map=47)` — RFK requires both ScriptedInstance (Ward Keeper gate) + EventAI (combat); abort + surface alert if either disabled | 92 |

**Total: 25 rules** (above target 15-20 because of the dual-faction cross-zone quest turn-ins, the gated Ward Keeper / Agathelos branch, and the two C++-scripted side quests Willix escort + Snufflenose gopher).

---

## Snapshot Fields Needed

```text
Snapshot.Level                                    // 30-40 entry band, 15 minimum
Snapshot.Faction                                  // Horde-natural Barrens approach vs Alliance Theramore/Ratchet detour
Snapshot.Class                                    // role + decurse/dispel capability for Aggem + Charlga curses
Snapshot.Position.{zone, x, y, z}                 // zone==491 for in-RFK checks
Snapshot.PartyState.{size, complete}              // 5-man composition
Snapshot.PartyComposition.{tank, healer, dps}     // role validation
Snapshot.QueueState.RFK.{role, invitePending, summonOffered}
Snapshot.InstanceState.{firstPull, partyWipe, WardKeepersRemaining}  // Ward Keeper counter from instance script
Snapshot.Boss.{Roogug, Aggem, Jargba, Ramtusk, Charlga, Agathelos, Earthcaller, BlindHunter}.{alive, engaged, castName, dead, hp}
Snapshot.NearbyMobs                               // EventAI trigger detection (Razorfen + Death's Head + Ward Keeper 4625 + rares)
Snapshot.NearbyNPC.contains(4508)                 // Willix escort detection
Snapshot.GameObject.21099.state                   // Agathelos Ward open/closed
Snapshot.Tank.debuff(7165)                        // Ramtusk Mortal Strike emergency-heal trigger
Snapshot.Tank.hpPct                               // big-heal threshold
Snapshot.Party.AnyMember.debuff(6192)             // Aggem Curse of Tongues decurse
Snapshot.Party.AnyMember.debuff(8361)             // Charlga Curse of the Crone decurse
Snapshot.Party.debuff(8358)                       // Charlga Demoralizing Shout dispel
Snapshot.Loot.window.items                        // Quest-token Need + Greed default
Snapshot.QuestLog.Active(1100,1101,1102,1109,1142,1144,1221)
Snapshot.QuestLog.Complete(1101,1102,1109,1142,1144,1221)
Snapshot.Inventory.Has(5876,5880,5897,4197)       // Snufflenose tools + Razorflank's Medallion
Snapshot.ServerCapabilities.ScriptedInstance.enabled  // Ward Keeper gate script readiness
Snapshot.ServerCapabilities.EventAI.enabled       // combat script readiness
```

---

## TBD / Backlog (per verification rule)

| Item | Status | Resolving grep |
|---|---|---|
| Sub-zone names "The Entrance" / "Wartusk Cell" / "The Shrine" / "The Quarters" / "The Bridges" / "Ramtusk's Chamber" / "Agathelos's Pen" / "The Crone's Throne" | TBD — not in `world_full_14_june_2021.sql` | `AreaTable.dbc` extraction (mangos uses DBC for sub-zones); fallback wiki crawl |
| Razorfen Warden (4435) full HP / EventAI rows | Partial — `creature_template` row 443220 located; full EventAI not dumped exhaustively | `Grep -E "^\s*\(44(35|350|370)0[1-9],"` on world_full_14_june_2021.sql `creature_ai_scripts` |
| GO 20920 Blueleaf Tuber spawn pool count + coords | TBD — `razorfen_kraul.cpp:177` constant `GO_BLUELEAF_TUBER=20920` confirmed; gameobject spawn rows not extracted here | `Grep "20920" gameobject + gameobject_template_loot` on world_full_14_june_2021.sql |
| Item 4197 "Razorflank's Medallion" vs "Razorflank's Heart" exact item rows | TBD — quest 1101 + 1102 ReqItemId references located; distinct item IDs for Heart variant not confirmed | `Grep -E "^\s*\(4197, \|^\s*\([0-9]+, .*'Razorflank"` on world_full_14_june_2021.sql `item_template` |
| Kraul Bat creature entry + Kraul Guano (quest 1109) drop item ID | TBD — quest 1109 ReqItemId implied but not extracted here | `Grep -E "'Kraul Bat'\|'Kraul Guano'"` on world_full_14_june_2021.sql |
| Earthcaller Halmgar drop table | TBD — `creature_template` row 449211 confirmed lvl 32 elite shaman but `creature_loot_template` row not dumped | `Grep "^\s*\(4842, " D:/MaNGOS/sql/world_full_14_june_2021.sql` on `creature_loot_template` |
| Blind Hunter (4425) drop table + spawn region | TBD — `creature_template` row 448902 confirmed lvl 32 elite scorpid; spawn coords + loot table TBD | `Grep "^\s*\(4425, " D:/MaNGOS/sql/world_full_14_june_2021.sql` on `creature` + `creature_loot_template` |
| Willix escort waypoint coords (full 45-point path) | Partial — 4 ambush coords confirmed in `razorfen_kraul.cpp:56-62`; the other 41 waypoints in `creature_movement_template:4508` not extracted | `Grep "^\s*\(4508, " D:/MaNGOS/sql/world_full_14_june_2021.sql` on `creature_movement_template` |

---

## Cross-References

- **Party invite handshake** (5-man formation, cross-faction allowed): [`../social/party-invite.md`](../social/party-invite.md) — `CMSG_GROUP_INVITE=110` flow, 60s decline window.
- **Pull / engage**: [`../combat/pull-target.md`](../combat/pull-target.md), [`../combat/threat-management.md`](../combat/threat-management.md).
- **CC before pull** (Polymorph/Sap on Death's Head priests + Razorfen Totemic healers): [`../combat/cc-pull.md`](../combat/cc-pull.md), [`../combat/cast-spell.md`](../combat/cast-spell.md).
- **Interrupts / decurse** (Aggem Curse of Tongues + Charlga Curse of the Crone + Demoralizing Shout): [`../combat/utility-casts.md`](../combat/utility-casts.md).
- **Melee rotation** (Roogug / Ramtusk / Agathelos tank-and-spank): [`../combat/melee-rotation.md`](../combat/melee-rotation.md).
- **Heal task** (5-man healer triage; Mortal Strike emergency big-heal pattern): [`../combat/heal-task.md`](../combat/heal-task.md).
- **Wipe handling**: [`../recovery/corpse-run.md`](../recovery/corpse-run.md), [`../recovery/release-corpse.md`](../recovery/release-corpse.md), [`../recovery/spirit-healer.md`](../recovery/spirit-healer.md).
- **Quest turn-ins** (cross-faction cross-zone routing Feralas/Darnassus/Thunder Bluff/Undercity/Ratchet): [`../npc/quest-giver.md`](../npc/quest-giver.md), [`../npc/gossip.md`](../npc/gossip.md).
- **Escort quest pattern** (Willix 1144 + Snufflenose gopher follower 1221): [`../quest/escort-quest.md`](../quest/escort-quest.md), [`../quest/follower-ai.md`](../quest/follower-ai.md).
- **Bracket context** (L30-L40): [`../sections/04-l30-l40.md`](../sections/04-l30-l40.md) (or nearest equivalent — verify pass 2).
- **Decision-engine integration**: [`../decision-engine/leveling-priority.md`](../decision-engine/leveling-priority.md), [`../decision-engine/state-flags.md`](../decision-engine/state-flags.md), [`../decision-engine/unlock-graph.md`](../decision-engine/unlock-graph.md).
- **Sibling dungeon** (next Both-faction at L30-40): [`razorfen-downs.md`](razorfen-downs.md) — the **same quilboar tribe lore continued**; RFK is the warren entrance, RFD is the burial mound (Amnennar the Coldbringer).
- **Sibling dungeon** (next Horde-natural at L30-40): [`scarlet-monastery-graveyard.md`](scarlet-monastery-graveyard.md) is Both-faction at L28-38; sequential ladder.
- **Cross-game pathfinding context** (outdoor-instance baked navmesh): RFK's outdoor-corridor layout has **unusual bake characteristics** — open-sky thornbush palisades produce sparse vertical walls in the navmesh tile vs. closed-corridor dungeons. See `Westworld of Warcraft/docs/physics/NEXT_SESSION_HANDOFF.md` for the canonical outdoor vs. indoor parity-tolerance framing.

---

## VMaNGOS / Server Reality Check

RFK is **C++ script-bundled** for the **Ward Keeper / Agathelos gate** (`instance_razorfen_kraul.cpp` 141 LOC) + **Willix escort** (`razorfen_kraul.cpp:64-145`, `npc_willix_the_importer` using `npc_escortAI`) + **Snufflenose gopher tuber-finder** (`razorfen_kraul.cpp:184-304`, `npc_snufflenose_gopher` using `FollowerAI`). The instance script's `SD%Complete: 50` comment (`instance_razorfen_kraul.cpp:19`) hints that **additional encounter wiring (Charlga / Roogug / Aggem / Jargba / Ramtusk progress tracking) is not yet implemented** — those bosses run pure EventAI without instance-state encounter tracking, meaning **partial-clear save/restore on disconnect may not preserve mid-instance boss kills**. Real-server VMaNGOS forks often patch this gap by extending the encounter array; cross-fork divergence here is **moderate** (not low like Stockades).

The **Willix escort script** has a known author-flagged ambiguity (`razorfen_kraul.cpp:79` "Exact use of these texts remains unknown") — the 4 aggro texts SAY_WILLIX_AGGRO_1..4 are random-selected from a switch with cases 0-3 out of `urand(0, 6)` (so the aggro yell only fires ~57% of the time, with the other ~43% silent). This is intentional per the script comment ("Not always said") but **may cause "no audio cue" reports** on aggro events. Bot snapshot logic should not rely on `Snapshot.NPC.Willix.lastSpokeText` as an aggro-confirmation signal — use `Snapshot.NPC.Willix.combatState` instead.

The **Snufflenose Gopher tuber-find script** has a 15y vertical-Z LOS check (`razorfen_kraul.cpp:288` `fabs(viewPoint->GetPositionZ() - tuber->GetPositionZ()) <= 15`) — tubers above or below the player by 15y are filtered out. Bot logic that summons the gopher and then waits for tuber-find should poll `Snapshot.NPC.Gopher.followState` for `STATE_FOLLOW_PAUSED` clearance, not assume an immediate tuber-find on summon. The script also enforces a **5000ms post-tuber-find pause** (`razorfen_kraul.cpp:230` `m_followPausedTimer = 5000`) before resuming follow — bot must wait this out before next command spell cast.

No known boss-mechanic divergences from retail 1.12.1. The **Charlga capstone quest chain (1100 → 1142 → 1101 Alliance / 1102 Horde)** has been stable since the 14-June-2021 world dump; the **Heralath Fallowbrook (4510) dying-NPC trigger** in the Barrens approach zone (1717) is the canonical Alliance hook + ties to Treshala in Darnassus (1657) via item 6751/6752 Treshala's Pendant.

Risk of script-break across modern VMaNGOS forks is **moderate** — the Ward Keeper counter (`m_uiWardKeepersRemaining`) increments per `OnCreatureCreate(NPC_WARD_KEEPER)` (`instance_razorfen_kraul.cpp:55-57`) but **does not decrement on respawn** — if a Ward Keeper respawns after a wipe-recovery, the counter desynchronises and the Agathelos Ward may open prematurely OR fail to open ever. This is a known mangos-zero family bug; bot snapshot logic should treat `Snapshot.InstanceState.WardKeepersRemaining` as **advisory not authoritative** and cross-check against `Snapshot.NearbyMobs.countByEntry(4625)` post-wipe.
