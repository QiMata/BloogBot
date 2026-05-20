---
title: "Dungeon — The Deadmines (DM)"
patch: "1.12.1 (Drums of War, Sept 2006)"
sources_crawled:
  - D:/MaNGOS/sql/world_full_14_june_2021.sql (map_template / creature_template / quest_template / worldsafelocs / game_object_template / creature_ai_scripts / area_template)
  - D:/MaNGOS/sql/world_full_14_june_2021.sql:728023-728024 (map_template header)
  - D:/MaNGOS/source/src/scripts/eastern_kingdoms/westfall/deadmines/instance_deadmines.cpp
  - D:/MaNGOS/source/src/scripts/eastern_kingdoms/westfall/deadmines/deadmines.cpp
  - D:/MaNGOS/source/src/scripts/eastern_kingdoms/westfall/deadmines/deadmines.h
  - D:/MaNGOS/source/src/scripts/eastern_kingdoms/westfall/deadmines/boss_mr_smite.cpp
  - https://warcraft.wiki.gg/wiki/Deadmines
crawl_date: 2026-05-20
---

# The Deadmines (DM) — 5-Man Dungeon Guide

Third file in the dungeons/ family, authored to the `ragefire-chasm.md` template contract. DM is the **Alliance counterpart to RFC + WC for first-instance experience** and the dungeon-side climax of the **Defias Brotherhood storyline** spanning Elwynn → Westfall → Redridge → Stormwind. Entrance is in the abandoned mining village of **Moonbrook** in southern Westfall (zone 40), reached through the Defias-occupied surface ruins, with the instance portal hidden inside the barn. Inside is a multi-level mineworks → goblin foundry & smelter → cargo hold → **Ironclad Cove** (zone 1582 — the only DM sub-zone present in `world_full_14_june_2021.sql`) where the Defias ship *The Defiant* hosts the final boss **Edwin VanCleef**. 7 named bosses + 1 rare/optional (Cookie) + 1 rare spawn (Miner Johnson). **Level band 17-26 optimal**, 1h-1h30m full clear. Unlike RFC, DM has a **dedicated C++ script bundle** at `D:/MaNGOS/source/src/scripts/eastern_kingdoms/westfall/deadmines/` (`instance_deadmines.cpp` + `deadmines.cpp` + `deadmines.h` + `boss_mr_smite.cpp`), making scripting risk **moderate** (script-driven door automation, Defias Cannon → Iron Clad Door wall-break event, and Mr. Smite's 3-phase weapon-swap fight can regress on VMaNGOS forks more easily than EventAI rows).

---

## Quick Facts

| Field | Value | Source |
|-------|-------|--------|
| `map_id` | **36** | `map_template:728023` row `(36, 0, 0, 1, 0, 40, 0, 0, -11207.8, 1681.15, 'Deadmines', 'instance_deadmines')` — `map_type=1` (5-man dungeon), `linked_zone=0` (DM uses zone 1581 separately via areatable, not via map_template's linked_zone column), `ghost_entrance_map=0` (Eastern Kingdoms), `script_name='instance_deadmines'` (**has a dedicated C++ script — like WC, unlike RFC**) |
| Continent / Parent map | Eastern Kingdoms (map 0) | `map_template:728023 ghost_entrance_map=0` |
| Host zone / linked zone | **1581 "The Deadmines"** + sub-zone **1582 "Ironclad Cove"** | `areatable.dbc` rows at world_full:1526 `(1581, 36, 0, 695, 0, 0, 'The Deadmines', 2, 0)` + :1527 `(1582, 36, 1581, 696, 0, 0, 'Ironclad Cove', 2, 0)` — Ironclad Cove is parent_area=1581 (child sub-zone of DM proper) |
| Outdoor host zone | Westfall (zone 40) — entrance approach through Moonbrook (zone 20, area_template `(20, 0, 40, 132, 64, 14, 'Moonbrook', 0, 0)` at world_full:849) | Geographic |
| Group size | 5-man (`player_limit=40` legacy; instance type 1 enforces 5) | `map_template.player_limit` |
| Reset delay | 0 (standard instance lockout, no scheduled reset) | `map_template.reset_delay=0` |
| Level range | **17-26** optimal (level **10 minimum** to enter) | Worldsafelocs `(78, 0, 'Deadmines - Entrance', 'You must be at least level 10 to enter.', 10, 0, 36, -14.5732, -385.475, 62.4561, 1.5708)` at world_full:172 |
| Faction | **Alliance** (natural) — entrance is in Westfall, an Alliance contested zone; Horde access requires summon or extended cross-zone travel | Geographic |
| Meeting Stone | **GameObject 2001** `'Meetingstone - The Deadmines'` lvl 36 (cluster range 18-25) | `game_object_template` row at world_full:563786 `(2001, 0, 36, 1581, 0, ...)` |
| Theme | Defias Brotherhood mining stronghold → goblin foundry/smelter → underground harbor with the Defias warship *The Defiant* |
| Boss count | **7 named + 1 optional (Cookie) + 1 rare spawn (Miner Johnson)** — most are EventAI but **Mr. Smite uses a dedicated C++ AI class** (`boss_mr_smiteAI`) with 3-phase equipment-swap mechanic |

**Entrance (Moonbrook ruins → instance portal)**: outdoor approach is in Moonbrook in south-central Westfall (Defias-occupied ghost town). The exit-WSL `(119, 0, 'Deadmines - Exit', '', 0, 0, 0, -11208.7, 1675.9, 24.5733, 4.71239)` at world_full:176 places the surface portal at Eastern Kingdoms coords `(-11208.7, 1675.9, 24.57)`. The inside-instance entry WSL `78` is at instance-local `(-14.5732, -385.475, 62.4561)`. A **back exit** WSL `(121, 0, 'Deadmines - Back Exit', '', 0, 0, 0, -11339.9, 1572.45, 94.3916, 1.5708)` exists at world_full:177 — used after the cannon-fires-the-wall event drops the party out the north side of Ironclad Cove onto the Westfall coast.

**Brief correction**: the user prompt asserted DM may be split into "**map_id = 36 (DM East / Foundry & Smelter side)** OR **48 (DM West / Mast Room)**" with the vanilla server treating it as one or two instances. **Vanilla 1.12.1 has a single Deadmines instance — map 36.** Map 48 is **Blackfathom Deeps** (`map_template:728032 (48, 0, 0, 1, 719, 40, 0, 1, 4249.12, 748.387, 'Blackfathom Deeps', 'instance_blackfathom_deeps')`), not DM West. The Deadmines East/West split is a **Cataclysm-era zone reorganisation** that does not apply to 1.12.1 mangos. **Cumulative brief-correction count this iter: +1.**

**Ghost-entrance back to outside the dungeon** on death: `map_template.ghost_entrance_x=-11207.8, ghost_entrance_y=1681.15` (Westfall Moonbrook surface — corpse spawns just outside the barn entrance for an easy run-back, mirroring WC's "right at the cave mouth" pattern).

---

## Geography & Sub-Zones

DM is a **mostly linear** dungeon with two notable branches (Mr. Smite's Cargo Hold optional pre-Greenskin pull + the Cannon-fire wall-break shortcut to the back exit). The only sub-zone in `world_full_14_june_2021.sql` is **Ironclad Cove (1582)**; the rest of the named sub-zones below come from AreaTable.dbc client data and are flagged **TBD**.

1. **Entrance shaft & Defias Miners area** — first ramp past WSL `78`. Defias Miner (5980) + Defias Strip Miner (4416) packs. Linear corridor.
2. **The Rat Den** [TBD — AreaTable.dbc] — Rockhide Boars + Miner Johnson rare-spawn alcove (entry 3586, lvl 19, 1347 HP, 50% replacement chance of the standard Defias Miner spawn at one specific location per wiki — `creature_ai_scripts` row not present, EventAI flag absent).
3. **Rhahk'Zor's chamber** — open arena where **Rhahk'Zor (644, lvl 19)** waits. Door 13965 "Factory Door" + Door1 GUID handler in `instance_deadmines.cpp:138` opens automatically on his death (`OnCreatureDeath case NPC_RHAHKZOR`). After 30-60s, hidden patrol creatures (entries 634 + 1729 — Defias Overseer + Defias Pirate) become visible via `m_uiSpawnPatrolOnRhahkDeath` timer (`instance_deadmines.cpp:212-230`).
4. **Goblin Foundry** [TBD AreaTable name] — Sneed's Shredder (642) + goblin trash (Goblin Engineer 6220 + Goblin Craftsman 1731 + Goblin Woodcarver 1732). When the Shredder dies, EventAI **64201** fires `Eject Sneed` spell **5141** at `creature death`, spawning **Sneed (643)** out of the wreckage — the iconic boss-phase transition.
5. **Mast Room** [TBD AreaTable name] — open chamber with **Gilnid (1763) "The Smelter"** at the far end. Door 16400 "Door2" handler in `instance_deadmines.cpp:140-141` opens after Sneed kill; door 16399 "Foundry Door" opens after Gilnid kill (`OnCreatureDeath case NPC_GILDNID`).
6. **Cargo Hold / Mr. Smite chamber** [TBD AreaTable name] — open deck area with **Mr. Smite (646, lvl 20, 3872 HP)** patrolling. His script is in `boss_mr_smite.cpp` and has 3 phases:
    - **Phase 1** (100-66% HP): single-hand sword + `SPELL_NIBLE_REFLEXES=6433` aura (parry buff).
    - **Phase 2** (66-33% HP): casts `SPELL_SMITE_STOMP=6432` to stun → walks to `GO_SMITE_CHEST=144111` → equips double `EQUIP_ID_AXE=2183` + casts `SPELL_THRASH=3391` (direct cast, not aura proc per script comment).
    - **Phase 3** (<33% HP): re-Stomp → walks to chest → equips `EQUIP_ID_HAMMER=10756` + casts `SPELL_SMITE_HAMMER=6436` self-buff + `SPELL_SMITE_SLAM=6435` 11s cooldown stun-slam.
    - The transition between phases removes the Nimble Reflexes aura at phase 1→2 and clears `equiping=true` state machine in `boss_mr_smiteAI::UpdateAI`.
7. **Captain Greenskin's chamber** [TBD AreaTable name] — open area transitioning to Ironclad Cove. **Captain Greenskin (647, lvl 20, 2904 HP)** + 2 Defias Pirate adds (657) typically; he casts `5208` (creature_template `spell1`).
8. **Ironclad Cove (zone 1582)** — underground harbor with the Defias warship *The Defiant* moored against a wall. The wall is the **Iron Clad Door (GO 16397)** event:
    - Interact with **Defias Cannon (GO 16398, script `go_defias_cannon`)** to trigger `TYPE_DEFIAS_ENDDOOR=IN_PROGRESS` (`deadmines.cpp:39-51`).
    - Cannon fires → `instance_deadmines.cpp:165-183` SetData → `Iron Clad Door`'s `UseDoorOrButton(0, true)` opens with 3000ms timer.
    - Mr. Smite (if still alive when player triggered cannon) yells `INST_SAY_ALARM1=-1036000` then 15s later `INST_SAY_ALARM2=-1036001` (`instance_deadmines.cpp:281-296`). Defias Pirates (657) on the ship begin running to defensive position `(-99.66, -671.07, 7.42)` via the `Iron Door Step` machine.
9. **The Defiant deck → Edwin VanCleef** — **VanCleef (639, lvl 21, 4168 HP, 'Defias Kingpin')** boards the ship deck after wall break. EventAI 63902-63907 emit yells at aggro / 75% / 50% (with summon allies via spell **5200** "VanCleef's Allies") / 35% / 10% / player kill. Quest 166 (`The Defias Brotherhood` chain final) requires killing him and looting **VanCleef's Head (item 3637)**.
10. **Cookie's Kitchen** [TBD AreaTable name] — optional alcove off Ironclad Cove. **Cookie (645, lvl 20, 2904 HP, 'The Ship's Cook')** — EventAI **64502** casts `Cookie's Cooking` spell **5174** at 50% HP (10-15s cooldown) + **64503** Flee at 15% HP. Drops `Cookie's Stirring Rod` + `Cookie's Tenderizer`.

---

## Mob Ecology

| Entry | Creature | Level | HP | Type | Source row |
|-------|----------|-------|----|----|----|
| **5980** | Defias Miner | 14-15 | ~700 | Humanoid (pickaxe melee) | world_full:443397 `creature_template` `(5980, 'Deadmines - Defias Miner', ..., 14, 15, ...)` |
| **6340** | Defias Overseer | 17-18 | ~900 | Humanoid (whip melee) | world_full:443417 |
| **44160** | Defias Strip Miner | 16-17 | ~850 | Humanoid (pickaxe melee + Mining-Picks debuff) | world_full:443885 |
| **44170** | Defias Taskmaster | 18-19 | ~1000 | Humanoid melee + spell **6660** | world_full:445064 |
| **44180** | Defias Wizard | 18-19 | ~950 | Humanoid caster — `4979` (Frost Nova) + `113` (Frostbolt) + `9053` (Fireball) | world_full:445065 |
| **17290** | Defias Evoker | 17-18 | ~850 | Humanoid caster — spell **11829** (Curse of Weakness) + **4979** (Frost Nova) | world_full:443713 |
| **17320** | Defias Squallshaper | 17-18 | ~850 | Humanoid caster — spell **2138** | world_full:443716 |
| **6220** | Goblin Engineer | 18 | ~900 | Humanoid (bomb-thrower) — spell **3605** (Demolition) + **6660** | world_full:445060 |
| **17310** | Goblin Craftsman | 17-18 | ~900 | Humanoid (mining-equipment maker) — spell **5159** | world_full:443715 |
| **6410** | Goblin Woodcarver | 17-18 | ~900 | Humanoid melee (axe) — spell **5532** (Cleave) | world_full:443418 |
| **657** | Defias Pirate | 18-19 | ~950 | Humanoid melee (cutlass) — Ironclad Cove ship adds | referenced in `instance_deadmines.cpp:282` + creature_template entry |
| **3586** | Miner Johnson (rare) | 19 | 1347 | Humanoid — **rare-spawn replacement** for a standard Defias Miner in The Rat Den area; not in `creature_ai_scripts` (default AI) | world_full:448250 `creature_template` `(3586, 0, 556, ..., 'Miner Johnson', ..., 19, 19, 1347, 1347, ...)` |

The Defias trash mob layout is **mixed melee + caster** — every Wizard / Evoker / Squallshaper pull needs CC or focus-fire-caster-first. Goblin packs are AoE-friendly (low HP) but Goblin Engineers throw Demolition bombs (`3605`) that AoE-pulse-damage the party.

---

## Boss Table

7 named bosses + 1 optional (Cookie) + 1 rare (Miner Johnson). All except **Mr. Smite** are EventAI-driven; Mr. Smite uses a dedicated C++ `boss_mr_smiteAI` class at `boss_mr_smite.cpp`. Boss kill state is tracked indirectly through GUID slots in `instance_deadmines.cpp::OnCreatureCreate` (Rhahk'Zor / Mr. Smite / Gilnid have dedicated GUID slots); door automation (`m_uiDoor1GUID` / `m_uiDoor2GUID` / `m_uiDoor3GUID`) is keyed on `OnCreatureDeath` of the gating boss.

| Boss | Entry | Level | HP | Spells (creature_template + EventAI) | Notable mechanic | Source row |
|------|-------|-------|----|-------|------|-----|
| **Rhahk'Zor** "The Foreman" | **644** | 19 | 4490 | `creature_template.spell1=6304` (`Rhahk'Zor Slam` melee AoE) + EventAI 64401 "Aggro Yell" | Single-target tank-and-spank with a small AoE slam; **his death opens Door1 (GO 13965) and triggers 30-60s later a hidden Defias Overseer (634) + Defias Pirate (1729) patrol becoming visible** (`instance_deadmines.cpp:204-235` `m_uiSpawnPatrolOnRhahkDeath` machine). Patrol's `RespawnDelay=43199` is the sentinel. | world_full:452989+452992 (DB has 2 spawn variants — entry 644 same) + EventAI 64401 at world_full:86503 |
| **Sneed's Shredder** "Lumbermaster" | **642** | 20 | 3872 | `spell1=7399` + `spell2=3603` + `spell3=5141` (Eject Sneed) — EventAI **64201 fires spell 5141 on `creature death`** | **Mech vs pilot two-phase boss**: kill the Shredder to fire spell 5141 which spawns Sneed (643) out of the wreckage. *Do not stop DPS on the Shredder kill* — Sneed appears immediately with full HP. | world_full:445276 `creature_template` + EventAI **64201** at world_full:89350 + AI script row 90996 (`spell=5141, action=4 cast spell`) |
| **Sneed** "Lumbermaster" | **643** | 20 | 2420 | `creature_template.spell1` empty — **no EventAI rows** (default melee AI; `ScriptName=''`) — pure tank-and-spank phase-2 | Tanked melee target; phase-2 of the Shredder fight. Drops `Sneed's Shredder Controls` (mount-style controller item — usable only inside DM). | world_full:445861 |
| **Gilnid** "The Smelter" | **1763** | 20 | 2904 | EventAI **176301** "Random Say OOC" every 120s + AI script row 91669 (broadcast_text 1147/1146) — no combat spell EventAI rows | Tank-and-spank caster — pulled from elevated smelter platform. **His death opens Door3 (GO 16400) AND triggers another 30s hidden patrol** (entries 4417 + 4418 — Defias Companion + Defias Pillager) becoming visible (`instance_deadmines.cpp:237-268` `m_uiSpawnPatrolOnGilnidDeath` machine, sentinel `RespawnDelay=43201`). | world_full:446735 + EventAI 176301 at world_full:86518 |
| **Mr. Smite** "The Ship's First Mate" | **646** | 20 | 3872 | `creature_template.spell1=6432` (Smite Stomp) + `spell2=3417` + `spell3=6264` + `spell4=6435` (Slam) + `ScriptName='boss_mr_smite'` — **dedicated C++ AI** | **3-phase weapon-swap boss**: P1 (>66%) single sword + Nimble Reflexes parry-buff aura 6433; **at 66% HP** casts Stomp 6432 → walks to `GO_SMITE_CHEST=144111` → equips double axe `2183` + casts Thrash 3391 (direct cast, not aura proc); **at 33% HP** repeats stomp → equips Hammer `10756` + casts Smite Hammer 6436 + Slam 6435 (11s cd). **Iron Door Event handler also lives here** (`instance_deadmines.cpp:281` he yells Alarm1+Alarm2 when cannon triggered). | world_full:445864 + `boss_mr_smite.cpp` |
| **Captain Greenskin** | **647** | 20 | 2904 | `creature_template.spell1=5208` + `spell2=5532` (Cleave) — no EventAI rows (default AI) | Tank-and-spank with frontal Cleave 5532 — raid stays behind boss. Drops `Cape of the Brotherhood` (item 5193) and is one of the gates to the cannon-event area. Typically pulled with 2 Defias Pirate (657) adds. | world_full:445865 |
| **Edwin VanCleef** "Defias Kingpin" | **639** | 21 | 4168 | `creature_template.spell1=2029` + EventAI 63902-63907 → yells at aggro/75%/35%/10%/PK + **63904 at 50% HP** casts spell **5200 'Summon VanCleef's Allies'** (3 add-pirates) + EventAI 63907 "Yell on Player Kill" every 5s | **Multi-phase add-spawn boss**: at 50% HP he summons 3 Defias Allies (spell 5200) — the **iconic VanCleef phase**. Tank picks up adds, raid focuses VanCleef. He also emits flavor yells at HP thresholds (75%, 35%, 10%) — pure cosmetic, no mechanic change. Drops his head (item **3637**) for quest 166 (`The Defias Brotherhood` final), plus iconic loot `Cruel Barb` (5191) + `Corsair's Overshirt`. | world_full:445858 + EventAI 63902-63907 at world_full:86026+86558-86562 + AI script row 90991 (`spell=5200, action=4 cast spell`) |
| **Cookie** "The Ship's Cook" (optional) | **645** | 20 | 2904 | `creature_template.spell1=5174` (`Cookie's Cooking` heal/buff) + `spell2=6306` + EventAI **64502** cast 5174 at 50% HP (10-15s cd) + **64503** Flee at 15% HP | **Optional alcove boss** off Ironclad Cove. Drops `Cookie's Stirring Rod` and `Cookie's Tenderizer` — popular Druid healing + Shaman/Warrior 1H mace. Standard tank-and-spank; the **Flee proc at 15%** is the main risk — finish him before the proc or expect him to run to nearby Defias Pirate adds. | world_full:445863 + EventAI 64502+64503 at world_full:86504-86505 + AI script row 91000 |
| **Miner Johnson** (rare spawn) | **3586** | 19 | 1347 | `creature_template.spell1` empty — no EventAI rows (default AI) | **Rare-spawn replacement** for a standard Defias Miner in The Rat Den area (50% chance per wiki — `spawn_data` table not dumped here). Drops `Compact Hammer` (item 1009 — popular Warrior/Paladin 1H mace at this level). Easy tank-and-spank; only special because he's a rare drop trigger. | world_full:448250 |

**Brief correction**: the user prompt asserted bosses with entry guesses "Rhahk'Zor (~644)" + "VanCleef (~639)". Both confirmed. The prompt also listed **Cookie** as "rare/optional" — confirmed optional alcove boss (not rare-spawn — Cookie is always present at his static location; Miner Johnson is the rare-spawn). Brief also omitted **Miner Johnson** entirely. **Cumulative brief-correction count this iter: +2.**

**Brief correction**: the user prompt said boss skill IDs come from `D:/MaNGOS/source/src/scripts/eastern_kingdoms/deadmines/` if a dedicated C++ script exists. **Path was off by one directory** — DM scripts actually live at `D:/MaNGOS/source/src/scripts/eastern_kingdoms/westfall/deadmines/` (Westfall sub-directory, mirroring the geographic zone hierarchy). The `find ... -iname '*deadmines*'` recursive grep returned the correct path. **Cumulative brief-correction count this iter: +3.**

---

## Quest Table

10+ quests scoped to or culminating in DM, including the canonical **Defias Brotherhood chain** (quests 65 → 132 → 135 → 141 → 142 → 155 → 166) that spans Westfall → Redridge → Stormwind before climaxing inside DM:

| Quest ID | Title | Giver / Turn-in | Zone | Notes | Source row |
|----|----|----|----|----|----|
| **155** | **The Defias Brotherhood (escort)** | Gryan Stoutmantle (Sentinel Hill, Westfall) | 40 (Westfall) | **Escort The Defias Traitor** through Moonbrook to the entrance of DM. Reward 1700 XP + 1020 copper. Min level 18. Pre-requisite for quest 166. | world_full:792931 |
| **166** | **The Defias Brotherhood (kill VanCleef)** | Gryan Stoutmantle → Gryan (Sentinel Hill) | **1581 (DM)** | **Kill Edwin VanCleef (639) + loot his head (item 3637) + return.** Reward 2600 XP + 1560 copper + items 6087 (`Stoutmantle's Bind` cape) + 2041 (`Stoutmantle's Buckler`) + 2042. Min level 22. **The capstone DM quest.** | world_full:792942 |
| **167** | **Oh Brother...** | Wilder Thistlenettle (Stormwind, The Canals) | **1581 (DM)** | Recover `Foreman Thistlenettle's Explorers' League Badge` (item 1875) — drops from Foreman Thistlenettle (entry not dumped here — Defias trash drop). Reward 1550 XP + 960 copper. Min level 20. | world_full:792943 |
| **168** | **Collecting Memories** | Wilder Thistlenettle (Stormwind) | **1581 (DM)** | Recover 4 `Miners' Union Cards` (item 1894) — drops from Defias Miner (5980) + Defias Strip Miner (44160). Reward 1350 XP + 840 copper + items 2037 + 2036. Min level 18. | world_full:792944 |
| **214** | **Red Silk Bandanas** | Scout Riell (Sentinel Hill Tower, Westfall) | **1581 (DM)** | Collect 10 `Red Silk Bandanas` (item 915) — drops from VanCleef's elite Defias mobs. Reward 1250 XP + 780 copper + items 2074 + 2089 + 6094. Min level 17. **Repeatable-style** collection quest favored for L17 catchup. | world_full:792991 |
| **373** | **The Unsent Letter** | (Loot from VanCleef's corpse) → Baros Alexston (Stormwind, Cathedral Square) | 1519 (Stormwind) | Item 2874 `The Unsent Letter` drops 100% from VanCleef (639). Turn-in routes to next quest in chain 389. Reward 870 XP + 700 silver. | world_full:793141 |
| **2040** | **Underground Assault** | Shoni the Shilent (Stormwind, Tinkertown) | **1581 (DM)** | Retrieve `Gnoam Sprecklesprocket` (item 7365) — drops from Sneed's Shredder (642) lootid 6420 (item appears in shredder's drop pool). Reward 1550 XP + 960 copper + items 7606 + 7607. Min level 20. **Gnomeregan-attune-adjacent** — sets up later Gnomeregan chain. | world_full:794292 |
| **7938** | **Your Fortune Awaits You...** | Sayge (Darkmoon Faire — Mulgore or Elwynn rotation) | **1581 (DM)** | Sayge fortune-card #24 (item 19424). Travel to DM to find `Mysterious Deadmines Chest` (GO 180024) — visibility gated by `instance_deadmines.cpp:152-163` `OnPlayerEnter` (only visible if `pPlayer->GetQuestStatus(7938) == QUEST_STATUS_COMPLETE`). Faire-event quest type 4. | world_full:795947 |
| **65** | The Defias Brotherhood (1) | Gryan Stoutmantle (Sentinel Hill) | 40 (Westfall) | Chain step 1: Travel to Lakeshire (Redridge) and talk to Wiley. Reward 1350 XP + 840 copper. Min level 18. | world_full:792844 |
| **132** | The Defias Brotherhood (2) | Wiley (Lakeshire Inn, Redridge) | 40 (Westfall) | Chain step 2: Take Wiley's Note (item 1327) to Stoutmantle. Reward 680 XP + 420 copper. | world_full:792909 |
| **135** | The Defias Brotherhood (3) | Gryan Stoutmantle → Mathias Shaw (Stormwind Old Town Barracks) | 1519 (Stormwind) | Chain step 3: Take Wiley's Note to Shaw. Reward 680 XP + 420 copper. | world_full:792912 |
| **141** | The Defias Brotherhood (4) | Mathias Shaw → Stoutmantle | 40 (Westfall) | Chain step 4: Take Shaw's report back to Stoutmantle. Reward 340 XP + 210 copper. | world_full:792917 |
| **142** | The Defias Brotherhood (5) | Stoutmantle → Stoutmantle | 40 (Westfall) | Chain step 5: Track Defias Messenger between Moonbrook / Gold Coast Quarry / Jangolode Mine; kill him and bring his message (item 1381) back. Reward 1350 XP + 840 copper. | world_full:792918 |

**Brief correction**: the user prompt listed **"A Mission of Mercy"** as a DM quest. **No quest title "A Mission of Mercy" exists in `quest_template` linked to zone 1581** (a quest 5862 by that name exists in `quest_template` but it is the Halls of the Dead Hinterlands quest, unrelated to DM). **Cumulative brief-correction count this iter: +4.**

**Brief correction**: the user prompt listed **"Skull of Bone-Cap Walden"** as a DM quest. **No quest_template row matches this title or any "Walden" / "Bone-Cap" substring.** The likely intended candidate is quest 168 `Collecting Memories` (Wilder Thistlenettle — recover Miners' Union Cards from fallen co-workers). **Cumulative brief-correction count this iter: +5.**

---

## Recommended Pull Order & Route

DM is **mostly linear with two branches**. The community-standard "full chain + cannon shortcut" path is:

1. **Entrance shaft & first Defias Miner pack** (5980 + 44160 trash). Single-pull on the ramp; melee Cleave-safe spread.
2. **The Rat Den area** — Defias Miner / Defias Strip Miner packs. **If Miner Johnson (3586) is spawned**, kill him for the `Compact Hammer` (1009).
3. **Defias Wizard / Evoker / Taskmaster mixed packs** — CC the caster (Polymorph / Sap / Shackle Undead doesn't apply — most are Humanoid → Polymorph or Sap) before pull; melee + Taskmaster otherwise focus-fires healer hard.
4. **Rhahk'Zor** (boss 1, 644) — open arena. Single-target tank-and-spank with small AoE slam (6304). **Wait 30-60s after kill** for the hidden Defias Overseer + Defias Pirate patrol to spawn (per `m_uiSpawnPatrolOnRhahkDeath`) — pull patrol cleanly before continuing through Door1.
5. **Goblin Foundry trash** (Goblin Engineer 6220 + Craftsman 17310 + Woodcarver 6410). **Watch for Demolition bomb (spell 3605)** AoE — spread the party 5+ yards.
6. **Sneed's Shredder** (boss 2, 642) — open chamber. **Burn the Shredder; Sneed (643) ejects immediately** via spell 5141 from EventAI 64201 on Shredder death. Do not stop DPS; phase-2 Sneed is a pure tank-and-spank.
7. **Gilnid** (boss 3, 1763) — Mast Room elevated platform. Pure tank-and-spank; pulled from the smelter platform. **Wait 30s after kill** for the second hidden patrol (Defias Companion + Defias Pillager) to spawn before continuing through Door3.
8. **Mr. Smite** (boss 4, 646) — Cargo Hold area. **3-phase weapon-swap fight**:
    - **Phase 1** (100-66% HP): tank-and-spank.
    - **At 66% HP** he Stomps (6432, stun) → walks to chest → equips double axe + Thrash (3391). **DPS pause window of ~5s while he's at chest** — heal up, reposition.
    - **At 33% HP** he Stomps again → walks to chest → equips Hammer + casts Smite Slam (6435, 11s cooldown massive stun + damage). **Healer focus during Slam casts**.
9. **Captain Greenskin** (boss 5, 647) — open area with 2 Defias Pirate (657) adds. Tank holds boss + 1 add via AoE threat; CC/focus-fire the second add; raid stays behind boss to dodge Cleave (5532).
10. **Branch decision — Cookie (optional)**: if running quest 168/214, skip Cookie unless tank wants Cookie's Tenderizer. **Cookie (645)** is in his alcove off Ironclad Cove; tank-and-spank with Flee proc at 15% — finish him quick or interrupt his flee.
11. **Iron Clad Cove cannon event**:
    - Interact with **Defias Cannon (GO 16398)** to trigger `TYPE_DEFIAS_ENDDOOR=IN_PROGRESS`.
    - 3000ms later (`m_uiIronDoorTimer`), **Iron Clad Door (GO 16397) opens** and Mr. Smite (if alive — should be dead by now) yells Alarm1.
    - Defias Pirates (657) on *The Defiant* deck spawn-defend; they run to defensive position `(-99.66, -671.07, 7.42)` via `Iron Door Step` machine.
    - 15s later Mr. Smite (or alarm system) yells Alarm2 — pure flavor.
12. **The Defiant deck → Edwin VanCleef** (boss 6, 639) — pull VanCleef alone first. **At 50% HP he Summons 3 Defias Allies** (spell 5200 via EventAI 63904). Tank picks up 3 adds; raid focuses VanCleef until VanCleef dies. Then mop up adds. Loot VanCleef's head (item 3637) for quest 166.
13. **Quest loot sweep** — `The Unsent Letter` (item 2874) on VanCleef corpse for quest 373; `Red Silk Bandanas` (item 915) from elite trash; `Miners' Union Cards` (item 1894) from miner trash.

3-man over-leveled (L26+) carry runs viable but skip cannon shortcut for safety. 4-man with at least 1 CC class (Mage / Rogue / Priest Polymorph-equivalent) is the realistic minimum for Mr. Smite's 3-phase fight + VanCleef adds-phase.

---

## Decision-Engine Rules

| id | bracket | precondition | action | priority |
|---|---|---|---|---|
| `dungeon.dm.queue.lfg-or-walkin` | `L17-L26` | `Snapshot.Level>=17 & Snapshot.QueueState.DM.role==null` | `Activity:LfgQueue("DM", autoRole=byClass)` else `Activity:Travel(Westfall_Moonbrook)` | 70 |
| `dungeon.dm.party.invite-handshake` | `L17-L26` | `Snapshot.PartyState.size<5 & Snapshot.QueueState.DM.invitePending==true` | `Activity:PartyInvite` (see `../social/party-invite.md`) — faction gate via `ERR_PLAYER_WRONG_FACTION`; Westfall is Alliance contested zone | 80 |
| `dungeon.dm.entrance.travel` | `L17-L26` | `Snapshot.PartyState.complete & Snapshot.Position.zone != 1581` | `Activity:Travel(Westfall_Moonbrook:-11207.8,1681.15,24.6)` via Sentinel Hill flightpath → south to Moonbrook | 75 |
| `dungeon.dm.party.composition-check` | `L17-L26` | `Snapshot.PartyComposition.tank!=null & Snapshot.PartyComposition.healer!=null & Snapshot.PartyComposition.dps.count>=2` | `Activity:EnterInstance(map=36)` | 78 |
| `dungeon.dm.pull.caster-cc-priority` | `L17-L26` | `Snapshot.NearbyMobs.containsAny([44180, 17290, 17320])` | `Task:UtilityCast(CC_PolymorphOrSap, target=highestThreatCaster)` (see `../combat/utility-casts.md`) — Defias Wizard/Evoker/Squallshaper hit hardest at L17-19 if uncontrolled | 85 |
| `dungeon.dm.pull.goblin-bomb-spread` | `L17-L26` | `Snapshot.NearbyMobs.contains(6220) & 6220.castName=='Demolition'` | `Task:Positioning(SpreadFormation, radius=5y)` — Goblin Engineer Demolition (spell 3605) is AoE; spread the party | 78 |
| `dungeon.dm.boss.rhahkzor` | `L17-L26` | `Snapshot.Boss.Rhahkzor.alive==true & Snapshot.Boss.Rhahkzor.engaged==false` | `Task:PullTarget(644)` — tank-and-spank; **emit post-kill wait-30-60s flag** for hidden patrol spawn (instance script `m_uiSpawnPatrolOnRhahkDeath`) | 72 |
| `dungeon.dm.boss.shredder-burn-then-sneed` | `L17-L26` | `Snapshot.Boss.SneedsShredder.alive==true & Snapshot.Boss.SneedsShredder.engaged==false` | `Task:PullTarget(642)` — DPS-burn target; emit `prepare-for-sneed-eject` flag — EventAI 64201 fires spell 5141 on Shredder death spawning Sneed (643) immediately | 75 |
| `dungeon.dm.boss.gilnid-patrol-wait` | `L17-L26` | `Snapshot.Boss.Gilnid.dead==true & Snapshot.InstanceState.elapsedSinceGilnidDeath<30000` | `Task:HoldPosition(radius=15y)` — wait for `m_uiSpawnPatrolOnGilnidDeath` hidden patrol (entries 4417 + 4418) to spawn before pushing through Door3 | 80 |
| `dungeon.dm.boss.smite-phase-pause` | `L17-L26` | `Snapshot.Boss.MrSmite.engaged==true & Snapshot.Boss.MrSmite.castName=='Smite Stomp'` | `Task:HoldPosition + Task:HealUp` — Smite Stomp (6432) is the phase-transition cast; he then walks to chest to swap equipment (5s pause window) | 88 |
| `dungeon.dm.boss.smite-slam-heal` | `L17-L26` | `Snapshot.Boss.MrSmite.castName=='Smite Slam'` | `Task:UtilityCast(HealTank|Bigheal)` — Slam (6435) is the phase-3 11s-cooldown massive stun; tank takes spike damage | 90 |
| `dungeon.dm.boss.greenskin-cleave-safe` | `L17-L26` | `Snapshot.Boss.Greenskin.alive==true & Snapshot.Boss.Greenskin.engaged==false` | `Task:PullTarget(647) + Task:Positioning(BehindBoss)` — raid stays behind boss to dodge Cleave (5532) | 72 |
| `dungeon.dm.boss.cookie-flee-suppress` | `L17-L26` | `Snapshot.Boss.Cookie.hp<0.25 & Snapshot.Boss.Cookie.engaged==true` | `Task:Burst(target=645)` — finish him before EventAI 64503 fires Flee at 15% HP; OR have tank ready to chase | 65 |
| `dungeon.dm.event.cannon-trigger` | `L17-L26` | `Snapshot.Boss.Greenskin.dead==true & Snapshot.NearbyGameObjects.contains(16398) & Snapshot.InstanceState.IronDoor==CLOSED` | `Task:Interact(GameObject=16398 DefiasCannon)` — triggers `TYPE_DEFIAS_ENDDOOR=IN_PROGRESS`; 3000ms later Iron Clad Door (16397) opens | 82 |
| `dungeon.dm.event.cannon-defense` | `L17-L26` | `Snapshot.InstanceState.IronDoor==IN_PROGRESS & Snapshot.NearbyMobs.contains(657)` | `Task:Defend(target=Cannon, radius=20y)` — Defias Pirates (657) charge the cannon position after wall-break; tank picks up | 88 |
| `dungeon.dm.boss.vancleef-adds-phase` | `L17-L26` | `Snapshot.Boss.VanCleef.engaged==true & Snapshot.Boss.VanCleef.hp<0.55 & Snapshot.Boss.VanCleef.hp>0.45` | `Task:AdditionalTargets(prepare_for_summon=5200)` — at 50% HP EventAI 63904 fires Summon VanCleef's Allies (spell 5200, 3 adds); tank picks up adds, raid focuses VanCleef | 92 |
| `dungeon.dm.boss.vancleef-head-loot` | `L17-L26` | `Snapshot.Boss.VanCleef.dead==true & Snapshot.QuestLog.Active(166)` | `Task:LootCorpse + Inventory.RecordItem(3637)` — VanCleef's Head is 100% drop; required for quest 166 turn-in | 90 |
| `dungeon.dm.loot.bop-quest-tokens` | `L17-L26` | `Snapshot.Loot.window.items.any(itemId in [3637, 2874, 1894, 1875, 7365, 915, 19425])` | `Task:LootRoll(Need)` for quest-token items; `Pass` on grey trash unless `Snapshot.Inventory.freeSlots<4` | 60 |
| `dungeon.dm.loot.iconic-bop-greens` | `L17-L26` | `Snapshot.Loot.window.items.any(itemId in [5191, 5193, 5194, 5201, 2169, 1009, 1156, 888])` | `Task:LootRoll(Need)` if class-appropriate (Cruel Barb 5191 Rogue/Warrior, Cape of the Brotherhood 5193 tank/melee, Taskmaster Axe 5194 Warrior 2H, Emberstone Staff 5201 caster, Buzzer Blade 2169 Rogue dagger, Compact Hammer 1009 Paladin/Warrior 1H, Lavishly Jeweled Ring 1156 caster, Naga Battle Gloves 888 plate-tank) else `Greed`; see `../decision-engine/leveling-priority.md` | 65 |
| `dungeon.dm.loot.greed-default` | `L17-L26` | `Snapshot.Loot.window.items.any(quality>=green & questItem==false)` | `Task:LootRoll(Greed)` default; class-spec upgrades trigger `Need` | 55 |
| `dungeon.dm.event.sayge-fortune-chest` | `L17-L26` | `Snapshot.QuestLog.Complete(7938) & Snapshot.NearbyGameObjects.contains(180024)` | `Task:Interact(GameObject=180024 MysteriousDeadminesChest)` — visibility gated by `instance_deadmines.cpp:152-163` (must have quest 7938 status==COMPLETE) — loot Lockbox (item 19425) | 58 |
| `dungeon.dm.questturnin.sequence` | `L17-L26` | `Snapshot.QuestLog.Complete(166) \|\| Complete(168) \|\| Complete(214) \|\| Complete(167)` | `Activity:Travel(Westfall.GryanStoutmantle)` first (166 + 214 turn-ins) → then `Activity:Travel(Stormwind.WilderThistlenettle)` (167 + 168) → then `Activity:Travel(Stormwind.BarosAlexston)` (373 from VanCleef loot) | 70 |
| `dungeon.dm.questturnin.defias-chain` | `L17-L26` | `Snapshot.QuestLog.Complete(166)==true` | `Activity:Travel(Westfall.GryanStoutmantle)` → turn in 166 → loot from VanCleef (item 2874) auto-starts quest 373 → travel Stormwind Cathedral Square Baros Alexston → continue chain | 68 |
| `dungeon.dm.wipe.recovery` | `L17-L26` | `Snapshot.Player.alive==false & Snapshot.InstanceState.partyWipe>=2` | `Activity:CorpseRun` (see `../recovery/corpse-run.md`) — ghost-spawn at Westfall Moonbrook surface `-11207.8,1681.15`, run back through Moonbrook ruins to instance portal | 95 |
| `dungeon.dm.wipe.party-disband-after-3` | `L17-L26` | `Snapshot.InstanceState.partyWipe>=3 & Snapshot.PartyState.demoralized==true` | `Activity:Abandon(ReleaseToGraveyard + LeaveParty)` — 3 wipes in DM signals undergeared or no-CC party (Mr. Smite + VanCleef adds are the usual wipe spots) | 40 |
| `dungeon.dm.script-readiness` | `L17-L26` | `Snapshot.ServerCapabilities.ScriptedInstance.enabled==true & Snapshot.ServerCapabilities.HasScript("instance_deadmines")==true & Snapshot.ServerCapabilities.HasScript("boss_mr_smite")==true` | `Activity:EnterInstance(map=36)` — DM depends on C++ script bundle; if `boss_mr_smite` script missing, Mr. Smite degrades to default melee AI (no phase swaps); if `instance_deadmines` missing, doors do not open on boss kills | 92 |

**Total: 24 rules** (target range 15-20 — DM has more rule slots than RFC like WC did, because of the multi-script Mr. Smite phases + cannon event + Defias-chain quest routing).

---

## Snapshot Fields Needed

```text
Snapshot.Level                                              // 17-26 entry band, 10 minimum
Snapshot.Class                                              // role bias + interrupt + CC capability for Wizard/Evoker
Snapshot.Position.{zone, x, y, z}                           // zone==1581 (or 1582 in Ironclad Cove) for in-DM checks
Snapshot.PartyState.{size, complete}                        // 5-man composition
Snapshot.PartyComposition.{tank, healer, dps}               // role validation
Snapshot.QueueState.DM.{role, invitePending}
Snapshot.InstanceState.{firstPull, partyWipe, IronDoor, elapsedSinceGilnidDeath, elapsedSinceRhahkzorDeath}
Snapshot.Boss.{Rhahkzor, SneedsShredder, Sneed, Gilnid, MrSmite, Greenskin, Cookie, VanCleef}.{alive, engaged, dead, castName, hp}
Snapshot.NearbyMobs                                         // EventAI trigger detection (5980/44160/6220 trash, 657 pirates, 3586 Miner Johnson rare)
Snapshot.NearbyGameObjects                                  // 16397 IronClad / 16398 DefiasCannon / 180024 DMF chest / 13965+16399+16400 doors / 2001 Meeting Stone
Snapshot.Party.AnyMember.debuff(Cleave|Demolition)          // Greenskin / Goblin Engineer trigger
Snapshot.Loot.window.items                                  // quest-token + iconic-BoP-green roll decisions
Snapshot.QuestLog.Active(155,166,167,168,214,373,2040,7938) // DM quest set
Snapshot.QuestLog.Complete(155,166,168,214,7938)            // turn-in routing
Snapshot.Inventory.Has(3637,2874,1894,1875,7365,915,19425)  // VanCleef head + Unsent Letter + Miners' Union Cards + Foreman badge + Gnoam + Red Silk + DMF Lockbox quest tokens
Snapshot.ServerCapabilities.ScriptedInstance.enabled        // script-readiness gate
Snapshot.ServerCapabilities.HasScript("instance_deadmines")
Snapshot.ServerCapabilities.HasScript("boss_mr_smite")      // dependency check for Mr. Smite 3-phase mechanic
```

---

## TBD / Backlog (per verification rule)

| Item | Status | Resolving grep |
|---|---|---|
| Sub-zone names "Goblin Foundry" / "Mast Room" / "Cargo Hold" / "Rat Den" / "Cookie's Kitchen" | TBD — not in `world_full_14_june_2021.sql` (only **Ironclad Cove 1582** present); rest are AreaTable.dbc client-side | `AreaTable.dbc` extraction; fallback wiki crawl |
| Foreman Thistlenettle entry ID (drops item 1875 for quest 167) | TBD — `creature_template` row not located in this pass; likely a unique-spawn Defias variant | `Grep "Foreman Thistlenettle" world_full_14_june_2021.sql` |
| Defias Companion (4417) + Defias Pillager (4418) full creature_template rows | Partial — referenced in `instance_deadmines.cpp:245+254` as Gilnid post-death patrol entries | `Grep -E "^\s*\((4417|4418), [0-9]+, [0-9]+, 0," world_full_14_june_2021.sql` |
| Defias Overseer (634) + Defias Pirate (1729) full rows for Rhahk'Zor post-death patrol | Partial — referenced in `instance_deadmines.cpp:212+221` | `Grep -E "^\s*\((634|1729), [0-9]+, [0-9]+, 0," world_full_14_june_2021.sql` |
| Miner Johnson rare-spawn rate | TBD — `creature_spawn_data.spawn_time_*` not dumped here; wiki cites ~50% replacement chance | `Grep -E "^\s*\(3586," world_full + lookup `creature_spawn_data` |
| Cookie's Tenderizer + Cookie's Stirring Rod exact item IDs | TBD — `creature_template.lootid=6450` for Cookie; item IDs not extracted | `Grep "Cookie's Tenderizer\|Cookie's Stirring Rod" world_full_14_june_2021.sql` |
| VanCleef boss skill 2029 (creature_template.spell1) full identity | TBD — likely a melee swing-style ability; not extracted from `spell_dbc` | `Grep -E "^\s*\(2029," world_full + DBC lookup |
| Sneed's Shredder Controls item ID (rumoured mount-style controller) | TBD — referenced in wiki, not yet located in `item_template` | `Grep "Sneed's Shredder Controls\|Shredder Controls" world_full_14_june_2021.sql` |

---

## Cross-References

- **Party invite handshake** (Alliance 5-man formation): [`../social/party-invite.md`](../social/party-invite.md) — `CMSG_GROUP_INVITE=110` flow, 60s decline window, faction gate via `ERR_PLAYER_WRONG_FACTION`.
- **Pull / engage**: [`../combat/pull-target.md`](../combat/pull-target.md), [`../combat/threat-management.md`](../combat/threat-management.md).
- **CC** (Mage Polymorph / Rogue Sap on Defias Wizard/Evoker/Squallshaper): [`../combat/utility-casts.md`](../combat/utility-casts.md), [`../combat/cast-spell.md`](../combat/cast-spell.md).
- **Interrupts** (Mr. Smite Slam, VanCleef summon, Defias Wizard Fireball): [`../combat/utility-casts.md`](../combat/utility-casts.md).
- **Melee rotation** (Rhahk'Zor / Gilnid / Greenskin tank-and-spank): [`../combat/melee-rotation.md`](../combat/melee-rotation.md).
- **Heal task** (Mr. Smite Slam phase-3 tank-spike triage, VanCleef adds-phase): [`../combat/heal-task.md`](../combat/heal-task.md).
- **GO interact** (Defias Cannon trigger, Mysterious Deadmines Chest loot): [`../npc/gossip.md`](../npc/gossip.md) (or dedicated `gameobject-interact.md` if added pass 2).
- **Wipe handling**: [`../recovery/corpse-run.md`](../recovery/corpse-run.md), [`../recovery/release-corpse.md`](../recovery/release-corpse.md), [`../recovery/spirit-healer.md`](../recovery/spirit-healer.md).
- **Quest turn-ins** (Gryan Stoutmantle, Wilder Thistlenettle, Scout Riell, Baros Alexston, Shoni the Shilent, Sayge): [`../npc/quest-giver.md`](../npc/quest-giver.md).
- **Bracket context** (L10-L20 and L20-L30): [`../sections/02-l10-l20.md`](../sections/02-l10-l20.md), [`../sections/03-l20-l30.md`](../sections/03-l20-l30.md) (verify pass 2).
- **Decision-engine integration**: [`../decision-engine/leveling-priority.md`](../decision-engine/leveling-priority.md), [`../decision-engine/state-flags.md`](../decision-engine/state-flags.md), [`../decision-engine/unlock-graph.md`](../decision-engine/unlock-graph.md).
- **Sibling dungeon** (lower bracket, Horde-only): [`ragefire-chasm.md`](ragefire-chasm.md) — RFC L13-18 template authority.
- **Sibling dungeon** (overlapping bracket, neutral entrance): [`wailing-caverns.md`](wailing-caverns.md) — WC L15-25, second script-bundled dungeon.
- **Sibling dungeon** (next Horde-natural at L22-30): `shadowfang-keep.md` (TBD — listed in 00_INDEX as future).

---

## VMaNGOS / Server Reality Check

DM is **script-driven** — like WC and unlike RFC, the boss + door + cannon-event scripting depends on `instance_deadmines.cpp` + `deadmines.cpp` + `deadmines.h` + `boss_mr_smite.cpp` at `D:/MaNGOS/source/src/scripts/eastern_kingdoms/westfall/deadmines/`. The **Mr. Smite 3-phase weapon-swap fight** is the most heavily-scripted boss in any L17-26 dungeon — it uses a dedicated `boss_mr_smiteAI` class with phase machine + GameObject lookup (`FindNearestGameObject(GO_SMITE_CHEST=144111, 150.0)`) + equipment swap via `SetVirtualItem`.

**Risk classes**:
- **Low risk** for individual EventAI bosses (Rhahk'Zor / Sneed's Shredder / Sneed / Gilnid / Captain Greenskin / Cookie / VanCleef) — EventAI rows 64401 / 64201 / 176301 / 64502 / 64503 / 63902-63907 are data-driven and rarely regress.
- **Moderate risk** for **Mr. Smite** — depends on `boss_mr_smiteAI::PhaseEquipStart` finding the chest GO within 150y, and the `inSpline` state-machine surviving evade/reset edge cases. Forks that refactor `ScriptedAI` base class or `MovePoint` motion-controller can stall the equipment swap.
- **Moderate risk** for the **Cannon → Iron Clad Door event** — depends on `instance_deadmines.cpp::SetData(TYPE_DEFIAS_ENDDOOR, IN_PROGRESS)` propagating to the GO's `UseDoorOrButton(0, true)` call within 3000ms; the Mr. Smite alarm-yell chain assumes Mr. Smite is still alive (or his GUID slot is valid). Forks that change `GetGUID` lifecycle on dead creatures will break the alarm yells (purely cosmetic — does not block progression).
- **Moderate risk** for **hidden patrol spawn machines** on Rhahk'Zor + Gilnid death — depends on `GetCreatureListWithEntryInGrid` finding entries 634/1729 (Rhahk'Zor patrol) and 4417/4418 (Gilnid patrol) with `RespawnDelay=43199/43201` sentinels. If the world DB doesn't include these specific spawn rows, patrols silently fail to appear (party clears too cleanly — minor regression).

Decision-engine rule `dungeon.dm.script-readiness` gates on `Snapshot.ServerCapabilities.HasScript("instance_deadmines")==true && HasScript("boss_mr_smite")==true` as a precaution. On official-track VMaNGOS this is always true; on stripped-down test servers Mr. Smite degrades to default melee AI (no phase swaps, no Slam) and the Defias Cannon GO becomes uninteractable (`TYPE_DEFIAS_ENDDOOR` SetData has no listener) — *Iron Clad Door must then be back-door-cheesed via GM teleport, blocking the canonical VanCleef route*.

Quest 166 (`The Defias Brotherhood` capstone) is the most heavily script-coupled DM quest because VanCleef's 50% HP add-spawn (EventAI 63904 cast spell 5200) and 100%-drop VanCleef's Head (item 3637 via `creature_loot_template` lootid 6390) both need the boss to be killable through his summon-allies phase — verify the EventAI spell-cast permission flag before accepting an "adds don't spawn" failure mode (this is a known VMaNGOS fork divergence when `EventAI.allow_summon_spells` is disabled).
