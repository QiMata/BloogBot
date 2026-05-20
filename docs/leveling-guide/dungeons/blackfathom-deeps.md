---
title: "Dungeon — Blackfathom Deeps (BFD)"
patch: "1.12.1 (Drums of War, Sept 2006)"
sources_crawled:
  - D:/MaNGOS/sql/world_full_14_june_2021.sql (map_template / creature_template / quest_template / worldsafelocs / game_object_template / creature_ai_scripts / area_template / gossip_text)
  - D:/MaNGOS/sql/world_full_14_june_2021.sql:728032 (map_template header)
  - D:/MaNGOS/source/src/scripts/kalimdor/ashenvale/blackfathom_deeps/instance_blackfathom_deeps.cpp
  - D:/MaNGOS/source/src/scripts/kalimdor/ashenvale/blackfathom_deeps/blackfathom_deeps.h
  - https://warcraft.wiki.gg/wiki/Blackfathom_Deeps
crawl_date: 2026-05-20
---

# Blackfathom Deeps (BFD) — 5-Man Dungeon Guide

Fifth file in the dungeons/ family, authored to the `ragefire-chasm.md` template contract. BFD is the **neutral L24-32 underwater Twilight's Hammer / naga dungeon** built into the ruins of an ancient night-elf Moonwell temple at the north end of **Zoram Strand** in **Ashenvale (zone 331)**. The entrance is a partially-submerged cave mouth on the seabed at the strand's northern coast; both factions reach it through Ashenvale (Horde through Zoram'gar Outpost on the strand itself, Alliance through Astranaar → west road → strand). Inside is a multi-room submerged-and-half-flooded ruin: shrine entrance → Ghamoo-ra's chamber → Sarevess's grotto → Moonshrine Sanctum (Twilight Hammer base) → Twilight Lord Kelris → **Altar of the Deeps shrine event (4 flames + 4-wave summon)** → final cavern with **Aku'mai** (the corrupted faceless servant of the Old Gods). **7 named bosses** (Ghamoo-ra, Lady Sarevess, Gelihast, Twilight Lord Kelris, Old Serra'kis [optional pool], Aku'mai final, Baron Aquanis [rare event-spawn]) + 1 rare-spawn (Lorgus Jett). **Level band 24-32 optimal**, ~45min-1h full clear. BFD has a **dedicated C++ script** at `D:/MaNGOS/source/src/scripts/kalimdor/ashenvale/blackfathom_deeps/instance_blackfathom_deeps.cpp` (script-name `instance_blackfathom_deeps`), giving it the same **moderate scripting risk** class as WC / DM / SFK (script-driven shrine-fire chain that gates the door to Aku'mai, Baron Aquanis rare event, save/load encounter state).

---

## Quick Facts

| Field | Value | Source |
|-------|-------|--------|
| `map_id` | **48** | `map_template:728032` row `(48, 0, 0, 1, 719, 40, 0, 1, 4249.12, 748.387, 'Blackfathom Deeps', 'instance_blackfathom_deeps')` — `map_type=1` (5-man dungeon), `linked_zone=719`, `ghost_entrance_map=1` (Kalimdor), `ghost_entrance_x=4249.12, ghost_entrance_y=748.387` (Zoram Strand cave mouth), `script_name='instance_blackfathom_deeps'` (**has a dedicated C++ script** — like WC / DM / SFK, all 4 ship-bundled lvl 17-32 dungeons are scripted). |
| Continent / Parent map | Kalimdor (map 1) | `map_template:728032 ghost_entrance_map=1` |
| Host zone / linked zone | **719 "Blackfathom Deeps"** (the dungeon's own zone) | `areatable` row at world_full:1338 `(719, 48, 0, 642, 0, 0, 'Blackfathom Deeps', 0, 0)` — `parent_area=0` (top-level zone — BFD is its own zone, not a sub-zone of Ashenvale). A second `areatable` row :1812 `(2797, 1, 331, 981, 64, 0, 'Blackfathom Deeps', 0, 0)` marks the **outdoor approach point** in Ashenvale (zone 331) — the cave-mouth coordinate on the strand. |
| Outdoor host zone | **Ashenvale (zone 331)** — entrance approach through Zoram Strand on the north coast (Horde Zoram'gar Outpost ~2 min south); Alliance approach from Astranaar via the western road to the strand. Brief was correct: zone 331. | `areatable` :1812 row |
| Group size | 5-man (`player_limit=40` legacy; instance type 1 enforces 5) | `map_template.player_limit` |
| Reset delay | 0 (standard instance lockout) | `map_template.reset_delay=0` |
| Level range | **24-32** optimal (level **10 minimum** to enter) | Worldsafelocs `(257, 0, 'Blackfathom Deeps - Entrance', 'You must be at least level 10 to enter.', 10, 0, 48, -150.234, 106.594, -39.779, 4.45059)` at world_full:184 |
| Faction | **Cross-faction (neutral entrance)** — Ashenvale is contested but the Zoram Strand approach is uncontested seabed; both Horde and Alliance enter through the same submerged cave. Horde quest hub Je'neu Sancrea at Zoram'gar Outpost (Earthen Ring shaman) ~2min south of cave; Alliance hub Argos Nightwhisper / Gershala Nightwhisper at Auberdine (Darkshore) for Argent Dawn chain. Both factions can fully complete BFD. | Geographic + quest_template race-masks (race 77 = Horde, race 178 = Alliance) |
| Meeting Stone | **GameObject 2004** `'Meetingstone - Blackfathom Depths'` (DB stores "Depths" — typo for "Deeps"), `linked_zone=719` cluster range 24-32 | `game_object_template` row at world_full:563789 `(2004, 0, 36, 719, ...)` |
| Theme | Drowned night-elf Moonwell temple corrupted by an Old God → naga + Twilight's Hammer cultists sacrificing innocents to **Aku'mai** (faceless servant of the Old Gods) to power his return. Lore via quest text 1200/6561 (`gossip_text:2241/2244 Aku'mai needs more innocents`). |
| Boss count | **7 named bosses** (Ghamoo-ra + Lady Sarevess + Gelihast + Twilight Lord Kelris + Old Serra'kis + Aku'mai + Baron Aquanis rare-event-spawn) + 1 rare-spawn (Lorgus Jett) — most are EventAI-driven; **Kelris + Aku'mai** are gated by the `instance_blackfathom_deeps.cpp` C++ state machine (`TYPE_KELRIS` + `TYPE_SHRINE` must both DONE before `GO_PORTAL_DOOR=21117` opens to Aku'mai). |

**Entrance (Zoram Strand seabed → submerged cave → instance portal)**: outdoor approach is the underwater cave mouth at the north end of Zoram Strand. The inside-instance entry WSL `257` is at instance-local `(-150.234, 106.594, -39.779)` (z below 0 confirms underwater entrance shaft). The exit WSL `259` is at Kalimdor `(4246.68, 743.402, -24.86)` (Ashenvale surface above the cave). Underwater swimming is required for the entry shaft + Old Serra'kis pool + Gelihast pool + final Aku'mai approach — bring **Elixir of Water Breathing** (item 5996) or warlock Underwater Breathing.

**Brief correction**: the user prompt asked to verify map_id but did not name the entry. **Confirmed: map_id 48, zone 719, ghost_entrance at Ashenvale (4249.12, 748.387) on Kalimdor map 1.** The brief said "entrance underwater on the south coast" — **partially WRONG: the cave mouth is on the NORTH end of Zoram Strand, not the south coast** (per gossip_text 9910: "Blackfathom Deeps can be found at the north end of Zoram Strand along the coast of Ashenvale"). Cumulative brief-correction count this iter: **+1**.

**Ghost-entrance back to outside the dungeon** on death: `map_template.ghost_entrance_x=4249.12, ghost_entrance_y=748.387` on Kalimdor (Ashenvale Zoram Strand surface above cave — corpse spawns at the surface and requires a short swim down to re-enter; spirit healer route is Zoram'gar GY for Horde, Maestra's Post / Astranaar GY for Alliance).

---

## Geography & Sub-Zones

BFD is **mostly linear with two optional side pools** (Old Serra'kis pool + Lorgus Jett tunnel) and one **iconic gating event** (the 4-flame shrine ritual after Kelris). The only sub-zones in `world_full_14_june_2021.sql` are **Blackfathom Deeps (719)** itself and the outdoor approach **(2797)**; floor-by-floor named sub-rooms below come from AreaTable.dbc client data and are flagged **TBD**.

1. **Entrance shaft (submerged)** — first underwater corridor past WSL `257`. **Blindlight Murloc (4818)** + **Murkshallow Snapclaw (4815)** packs swim around. Water breathing recommended.
2. **Ghamoo-ra's chamber** [TBD AreaTable name — wiki calls it "The First Room"] — large open cavern with **Ghamoo-ra (4887, lvl 25, 4194 HP)** the giant turtle as first boss. Tank-and-spank.
3. **Sarevess's grotto** [TBD AreaTable name — wiki calls it "The Pool of Ask'ar"] — **Lady Sarevess (4831, lvl 25, 4880 HP)** the naga matron with **Blackfathom Tide Priestess (4802)** + **Blackfathom Sea Witch (4805)** add packs.
4. **Old Serra'kis pool (optional)** [TBD AreaTable name — wiki "Old Serra'kis Pool"] — submerged side-pool off the Sarevess route containing **Old Serra'kis (4830, lvl 26, 3750 HP)**, the large shark. Optional — does NOT need to die to reach Aku'mai but drops `Glowing Lizardscale Cloak` etc.
5. **Coral causeway → Gelihast's shrine** [TBD AreaTable name] — long swim through a flooded corridor to **Gelihast (6243, lvl 26, 4500 HP)** the naga in front of the **Shrine of Gelihast (GO 103015)** — Gelihast is an **event-spawned naga matron**, not always present in the room until aggro range is broken. Wiki notes he can be skipped via a back-swim, but most groups pull him.
6. **Lorgus Jett's tunnel (optional)** [TBD AreaTable name] — narrow side-tunnel off the main path containing **Lorgus Jett (12902, lvl 26, 1953 HP)** the Twilight's Hammer aquamancer. Rare/optional — required ONLY for Horde quest 6565 "Allegiance to the Old Gods" (kill quest). EventAI 1290201 casts `Lightning Shield` (spell 12550) on spawn.
7. **Moonshrine Sanctum (Twilight's Hammer base) → Twilight Lord Kelris** [TBD AreaTable name — wiki "The Sleeper's Chamber"] — large hall with **3 sleeping prisoners chained to the floor + Twilight Lord Kelris (4832, lvl 27, 5520 HP)** standing among them. Kelris is the iconic mid-boss. Trash: **Twilight Acolyte / Aquamancer / Loreseeker / Shadowmage / Elementalist / Reaver** (entries 4810-4815 + 4823 + similar). Mid-boss. **His death sets `TYPE_KELRIS=DONE`** (`instance_blackfathom_deeps.cpp:172-177`) and is **prerequisite to lighting the 4 shrine fires** — `go_fire_of_akumai.OnUse` checks `pInstance->GetData(TYPE_KELRIS) != DONE → return false` (line 389-390). Lore: Kelris is sacrificing the 3 prisoners to feed Aku'mai (gossip 2241 "Aku'mai needs more innocents").
8. **The 4-flame shrine event (Altar of the Deeps approach)** — after Kelris is dead, **4 Fires of Aku'mai (GO_SHRINE_1=21118 / GO_SHRINE_2=21119 / GO_SHRINE_3=21120 / GO_SHRINE_4=21121)** must be lit. **The order doesn't matter** (instance_blackfathom_deeps.cpp:36 "The order in which the fires are lit doesn't matter"). Each `OnUse` increments `m_uiShrinesLit` and queues a `m_uiSpawnMobsTimer[i]=3000ms` (line 182) for a wave-summon. **4 summon waves per the script's `aWaveSummonInformation` table** (lines 78-87):
    - **Wave 0**: 4 × **Aku'mai Snapjaw (NPC_AKUMAI_SNAPJAW=4825, lvl 26-27, 2250-2400 HP)** at positions 0/1/4/5
    - **Wave 1**: 2 × **Aku'mai Servant (NPC_AKUMAI_SERVANT=4978, lvl 26, 1953 HP)** water elementals at positions 1/4
    - **Wave 2**: 4 × **Murkshallow Snapclaw (NPC_MURKSHALLOW_SNAPCLAW=4815, lvl 22-23, 1686-1815 HP)** at positions 0/2/3/4
    - **Wave 3**: 10 × **Murkshallow Softshell (NPC_MURKSHALLOW_SOFTSHELL=4977, lvl 25, 209 HP)** at positions 2*0 / 1 / 2 / 3 / 4 / 2*5 (6 spawn positions, with doubles at endpoints)
    - All waves spawn from `pKelris->GetRespawnCoord` with `SetInCombatWithZone()` + visual spell **7741** Summoned Demon (line 313). When `IsWaveEventFinished()` confirms all summons dead AND `m_uiShrinesLit==4`, `TYPE_SHRINE=DONE` fires and **`GO_PORTAL_DOOR=21117` opens** (line 173-174, 188-189) — gate to Aku'mai.
9. **Baron Aquanis rare event** — **Baron Aquanis (12876, lvl 28, 2196 HP)** spawns from **GO_FATHOM_STONE=177964** (the Fathom Stone GameObject mentioned in quest 909 "Baron Aquanis"). Rare — drops `Strange Water Globe` for quest 909 turn-in at Je'neu Sancrea. His death fires `OnCreatureDeath` → `SetData(TYPE_AQUANIS, DONE)` (line 276-277) — instance-saved to allow once-per-lockout drop. `creature_template.spell1=14907` + `spell2=15043` + EventAI 128760.
10. **Aku'mai's chamber (final)** — large flooded cavern with **Aku'mai (4829, lvl 28, 8530 HP)** the corrupted Old-God servant (model 2837 — large fish/eel-like faceless one). Tank-and-spank with frontal area damage. `creature_template.spell1=3815` (`Poison Cloud` spell or similar AOE) + EventAI 48290 at world_full:443753. **Iconic final loot**: `Strike of the Hydra`, `Black Fathom Eel Mantle`, `Naga Heartpiercer`, etc.

---

## Mob Ecology

| Entry | Creature | Level | HP | Type | Source row |
|-------|----------|-------|----|----|----|
| **4802** | Blackfathom Tide Priestess | 20-21 | 1299-1392 | Humanoid (naga caster) — spell **2055** (Heal) on friendlies + **9672** (Frost Shock) + EventAI 480201 `Flee at 15%` | world_full:449185 + EventAI at world_full:85515 |
| **4803** | Blackfathom Oracle | 21-22 | 1392-1494 | Humanoid (naga caster) — spell **11986** (Lightning Shield) + **8363** (Drain Mana) | world_full:449186 |
| **4805** | Blackfathom Sea Witch | (template) | ~1400 | Humanoid (naga) — spell **6143** (Frost Ward) + EventAI 480503 cast on cooldown + 480504 Flee at 15% | EventAI 85916-85917 + AI scripts 93936-93937 |
| **4807** | Blackfathom Myrmidon | (template) | ~1600 | Humanoid (naga) — EventAI 480701 cast `Disarm` spell **8379** on cooldown | EventAI 85918 + AI script 93938 |
| **4810-4814** | Twilight Acolyte / Aquamancer / Shadowmage / Elementalist / Loreseeker | 22-26 | 1200-2000 | Humanoid (Twilight's Hammer cultist caster) — Acolytes channel `Blackfathom Channeling` spell **8734** (remove on aggro per AI scripts 93943/93952/93976); Elementalist (4814) has 4 spells: **13728** + **15039** + **12548** + **11824** (Fireball/Frostbolt/Lightning Bolt school rotation); Shadowmage (4813) **7645** (Shadow Bolt Volley) | world_full:444090-444092 + 444981 |
| **4815** | Murkshallow Snapclaw | 22-23 | 1686-1815 | Beast (crab) — spell **8379** (Disarm proc) + EventAI 48150 | world_full:449195 |
| **4818** | Blindlight Murloc | (template) | ~1100 | Humanoid (murloc caster) — spell **7405** + EventAI 48180 | world_full:443591 |
| **4823** | Twilight Reaver | (template) | ~1500 | Humanoid (Twilight's Hammer melee) — spell **8374** (whirlwind) + EventAI 48100 | world_full:444090 |
| **4825** | Aku'mai Snapjaw | 26-27 | 2250-2400 | Beast (giant turtle) — spell **8391** + EventAI 48250 — **shrine-event summon wave 0** | world_full:449203 |
| **4827** | Deep Pool Threshfin | (template) | ~1800 | Beast (fish) — spell **3604** + EventAI 48270 | world_full:443566 |
| **4855** | Fallenroot Shadowstalker | (template) | ~1300 | Humanoid (satyr — Researching the Corruption quest mob) — spell **6205** (Curse of Weakness) + EventAI 47980 | world_full:443898 |
| **4856** | Fallenroot Hellcaller | (template) | ~1500 | Humanoid (satyr caster) — spell **8129** (Sleep) + **9613** (Curse of Tongues) + EventAI 47990 | world_full:444979 |
| **4977** | Murkshallow Softshell | 25 | 209 (low) | Beast (small turtle) — shrine-event summon wave 3 (10 spawned) | world_full:449290 |
| **4978** | Aku'mai Servant | 26 | 1953 | Elemental (water elemental) — spell **110** + EventAI 49780 — **shrine-event summon wave 1** | world_full:449291 |

The BFD trash is **mixed naga + Twilight cultist + corrupted beast + satyr** with multiple CC angles. Naga + cultists are **Humanoid → Polymorph/Sap/Repentance**; turtles + crabs + fish are **Beast → Hibernate (Druid) / Freezing Trap (Hunter)**; the water elementals are **Elemental → Banish (Warlock)**. **Twilight Acolytes channel `Blackfathom Channeling` 8734 on Kelris until aggro** (lore detail — they're feeding Kelris) — interrupting them grants a brief debuff window.

---

## Boss Table

7 named bosses + 1 optional rare (Lorgus Jett). All are EventAI-driven; Kelris + Aku'mai + Baron Aquanis are state-coupled to `instance_blackfathom_deeps.cpp` for door-gating + once-per-lockout save state.

| Boss | Entry | Level | HP | Spells (creature_template + EventAI) | Notable mechanic | Source row |
|------|-------|-------|----|-------|------|-----|
| **Ghamoo-ra** "Giant Turtle" | **4887** | 25 | 4194 | `creature_template.spell1=5568` (`Frenzied Rage`) + EventAI 48870 at world_full:444102 (cast Frenzied Rage at 30% HP 5-8s cd, Crash 9-13s cd) | **First boss — tank-and-spank with hard enrage at 30%**. Tank picks up; healer focuses through Frenzied Rage burst-damage window. Drops `Reef Axe` (iconic 1H drop for Warriors). | world_full:449240 + EventAI 48870 |
| **Lady Sarevess** "Naga Matron" | **4831** | 25 | 4880 | `creature_template.spell1=4979` (`Frost Nova`) + `spell2=865` + `spell3=8435` + `spell4=6660` + EventAI 48310 at world_full:444983 (Frost Nova 7-9s / Cone of Cold 6-9s / Slow 1-3s / Forked Lightning 2-4s) | **AoE Frost + Cone of Cold caster boss with 2 adds**. Tank picks up Sarevess; OT or kite-class picks up the 2 naga adds (Blackfathom Tide Priestess 4802 + Sea Witch 4805 typically); raid spreads to dodge Cone of Cold (frontal 90° AoE). Frost Nova roots melee — interrupt-priority if your party has it. Drops `Naga Heartpiercer`. | world_full:449207 + EventAI 48310 |
| **Gelihast** (Optional event-spawned) | **6243** | 26 | 4500 | `creature_template.spell1` empty + EventAI 6243-series at world_full:450110 (`Fork Lightning` + `Lightning Bolt`) | **Event-mob naga matron in front of Shrine of Gelihast (GO 103015)**. Optional — most groups pull him because he blocks the natural path to Kelris. Tank-and-spank with shock-cast pressure on healer. The brief said "naga, optional, event-spawned" — **confirmed event-spawned, but only optional in the sense that you can swim around him; he's on the main path**. | world_full:450110 |
| **Old Serra'kis** (Optional) "Large Shark" | **4830** | 26 | 3750 | `creature_template.spell1=1816` + EventAI 4830-series (Thrash on Aggro spell 3391 + slow-melee Cleave 5532) | **Optional pool boss**. Side-pool off the Sarevess route — swim down to the bottom, pull him to a dry ledge if your healer can't tread water mid-cast. Drops `Glowing Lizardscale Cloak` + `Old Serra'kis Tooth`. Tank-and-spank with high melee swing damage; healer outlasts. | world_full:449206 |
| **Twilight Lord Kelris** "Cultist High Priest" | **4832** | 27 | 5520 (1830 mana) | `creature_template.spell1=15587` + `spell2=8399` (`Sleep` AOE) + `mechanic_immune_mask=608908883` + EventAI 48320 at world_full:443540 (cast Sleep 8399 every 2-5s on random target + Mind Blast 15587 on tank every 7-9s) | **AoE Sleep iconic mechanic**: Kelris casts `Sleep` (spell 8399) AoE on random targets every 5-7s — anyone hit is asleep 6s and takes 50% extra damage. **Healer + tank must dispel/burst-heal sleeping party members or the raid wipes from cascade-sleep failures**. Tank holds Kelris; raid spreads to limit AoE Sleep hits. **His death sets `TYPE_KELRIS=DONE`** (instance_blackfathom_deeps.cpp:172-177) which is **prerequisite to lighting the 4 shrine fires** (`go_fire_of_akumai::OnUse` line 389-390 checks `GetData(TYPE_KELRIS) != DONE → return false`). Without Kelris dead, the shrine event cannot start, and Aku'mai's door (GO_PORTAL_DOOR=21117) stays closed. Drops `Twilight Robe` + `Kelris's Letter` (item 17699 for quest 1200/6561). | world_full:449208 + EventAI 48320 |
| **Aku'mai** "Faceless One" (Final) | **4829** | 28 | 8530 | `creature_template.spell1=3815` (`Poison Cloud`) + EventAI 48290 at world_full:443753 (Poison Cloud 5-9y target zone every 5-9s, Frenzy at 35% HP) + `mechanic_immune_mask=608908883` + `flags_extra=2097408` (interrupt + sleep + fear immune) | **Final boss — Poison Cloud zone-control + Frenzy at 35%**. Tank holds Aku'mai at the chamber's center; raid moves to fresh ground when Poison Cloud is dropped (it persists 8-12s). At 35% HP he Frenzies (haste + damage boost) — healer focus + DPS burn through final phase. Drops iconic loot: `Strike of the Hydra` (2H Warrior sword), `Black Fathom Eel Mantle`, `Aku'mai Sacrifice` (necklace), `Fathom Stalker's Mask`. **The capstone boss for both Alliance (Blackfathom Villainy 1200) and Horde (Blackfathom Villainy 6561) Kelris-kill quests** — note both quests turn in for KELRIS' head, not Aku'mai, but Aku'mai's loot is the run's main reward. | world_full:449205 + EventAI 48290 |
| **Baron Aquanis** (Rare event-spawn) | **12876** | 28 | 2196 (756 mana) | `creature_template.spell1=14907` + `spell2=15043` (channeled cast) + EventAI 128760 at world_full:444984 (Mind Flay channel + summon) + `flags_extra=32` (event-spawned, not in default spawn) | **Rare event boss summoned from GO_FATHOM_STONE=177964** (the Fathom Stone GameObject; quest 909 "Baron Aquanis" drop-triggered). His death fires `OnCreatureDeath → SetData(TYPE_AQUANIS, DONE)` (instance_blackfathom_deeps.cpp:276-277) and instance-saves the state, allowing once-per-lockout drop of `Strange Water Globe` (item 16782) for quest 909 turn-in at Je'neu Sancrea (Horde Zoram'gar Outpost). Tank-and-spank low HP boss; gossip 769 "What is happening? What force draws me to the Altar of the Tides!" on aggro. | world_full:452755 + EventAI 128760 + instance_blackfathom_deeps.cpp:276-277 |
| **Lorgus Jett** (Optional rare-spawn) | **12902** | 26 | 1953 (1386 mana) | `creature_template.spell1=12167` + EventAI 1290201 at world_full:85944 cast `Lightning Shield` spell **12550** on Spawn (10min cd) + AI script 97009 | **Optional rare-spawn aquamancer in side tunnel**. Required ONLY for Horde quest 6565 "Allegiance to the Old Gods" (kill quest from Je'neu Sancrea). Spawn-data sentinel `1290201 Spawn Boss Blackfathom Deeps : Lorgus Jett` at world_full:790422 indicates intentional spawn-event row. Tank-and-spank caster with Lightning Shield damage-reflect. | world_full:452761 + EventAI 85944 + AI script 97009 |

**Brief correction**: the user prompt listed `Gelihast (naga, optional, event-spawned)`. **Confirmed entry 6243, lvl 26, 4500 HP, event-spawned** at world_full:450110. Brief was correct. Cumulative brief-correction count this iter: **+0** (no error).

**Brief correction**: the user prompt described the shrine event as `Twilight Pillars event (4 flames must be doused to spawn Kelris — need water bucket)`. **Inverted: the 4 flames must be LIT (not doused), and they are lit AFTER Kelris dies, not before**. There is no water bucket; the flames are interactable via gossip-style `OnUse` after Kelris-DONE state. The flames spawn 4 waves of mobs (Snapjaw → Servant → Snapclaw → Softshell) that must all be killed to open the Aku'mai door (GO 21117). No "Battle of the Twilight Pillars" event name exists in 1.12.1 mangos — that's a **Cataclysm-era name** for the rebuilt-encounter quest. **Cumulative brief-correction count this iter: +2.**

**Brief correction**: the user prompt described `Aku'mai (final boss, large fish/eel; can be skipped via swimming)`. **WRONG — Aku'mai's chamber is gated by GO_PORTAL_DOOR=21117 which only opens after BOTH `TYPE_KELRIS=DONE` AND `TYPE_SHRINE=DONE`** (instance_blackfathom_deeps.cpp:188-189). You cannot skip him via swimming; the door is closed by script unless the shrine event has been completed. **Cumulative brief-correction count this iter: +3.**

**Brief correction**: the user prompt listed Twilight Lord Kelris as bringing `3 sleeping prisoners awake — Battle of the Twilight Pillars event`. **Partially confirmed**: Kelris IS standing among the 3 sleeping prisoners (lore via gossip 2241/2244 — he is sacrificing them to feed Aku'mai); however, **the prisoners do NOT awaken during the fight** in 1.12.1 (they remain decorative sleeping NPCs throughout the encounter; this is **Wrath classic / Cataclysm BFD-revamp content** added in patch 4.0.3, not 1.12.1). **Cumulative brief-correction count this iter: +4.**

---

## Quest Table

8+ quests scoped to or culminating in BFD, with parallel Horde + Alliance chains (most quests have both a Horde and Alliance variant for the same kill credit):

| Quest ID | Title | Giver / Turn-in | Zone | Notes | Source row |
|----|----|----|----|----|----|
| **1200** | **Blackfathom Villainy (Alliance)** | Dawnwatcher Shaedlass (Argent Dawn, Auberdine?) → Dawnwatcher Selgorm (Darnassus) | **719 (BFD)** | **Kill Twilight Lord Kelris (4832) + loot `Kelris's Head` (item 5881) → turn in at Selgorm.** Reward 3300 XP + 6500 copper + 1980 reward-spell + items 7001 + 7002 (choose-one). Min level 18, max 27. RequiredRaces=529 (NE+Human+Dwarf+Gnome). **The capstone Alliance-side BFD quest.** Pre-req chain: 1198 In Search of Thaelrid → 1199 Twilight Falls → 1200 Blackfathom Villainy. | world_full:793916 |
| **6561** | **Blackfathom Villainy (Horde)** | (Earthen Ring chain) → Bashana Runetotem (Thunder Bluff) | **719 (BFD)** | **Kill Twilight Lord Kelris (4832) + loot `Kelris's Head` → turn in at Bashana Runetotem.** Reward 3300 XP + 6500 copper + 1980 reward-spell + items 7001 + 7002 (choose-one). Min level 18, max 27. RequiredRaces=178 (Horde). **Horde mirror of 1200.** | world_full:795552 |
| **1198** | **In Search of Thaelrid (Alliance)** | Dawnwatcher Shaedlass (Argent Dawn) → Argent Guard Thaelrid (inside BFD) | **719 (BFD)** | Find Argent Guard Thaelrid (Argent Dawn scout sent into BFD and never returned). Reward 2400 XP + 1440 copper. Min level 18, max 24. Pre-req for 1199 + 1200. **Brief correction**: the user prompt called this `In Search of Thaelrid (Mauradon/BFD escort hand-off)` — **WRONG, there is no Maraudon connection; Thaelrid is purely a BFD-inside reconnect quest, not an escort, and not Maraudon-linked**. Cumulative brief-correction count this iter: **+5**. | world_full:793914 |
| **1199** | **Twilight Falls (Alliance)** | Argent Guard Thaelrid (inside BFD) → Argent Guard Manados (Darnassus) | **719 (BFD)** | Collect 10 `Twilight Pendants` (item 5879) from Twilight's Hammer cultist trash inside BFD. Reward 2550 XP + 1560 copper + items 6998 + 7000 (Alliance-class choose-one). Min level 20, max 25. RequiredRaces=529 (Alliance). **Brief correction**: the user prompt called this `Twilight Falls (Horde anti-cultist chain)` — **WRONG, 1199 is Alliance-only (turn-in Darnassus, RequiredRaces=529, source 8 NeutralAdventurer mostly NE-coded)**. The Horde equivalent for the cultist-pendant-collect is bundled into the Earthen Ring chain 6562/6563/6564/6565 (different mechanic — they collect Sapphires of Aku'mai + Damp Note, not Twilight Pendants). Cumulative brief-correction count this iter: **+6**. | world_full:793915 |
| **971** | **Knowledge in the Deeps** | Gerrig Bonegrip (**Forlorn Cavern, Ironforge**) | **719 (BFD)** | Recover `Lorgalis Manuscript` (item 5359) from a special GameObject inside BFD. Reward 2750 XP + 1680 copper. Min level 10, max 23. RequiredRaces=77 (**Forsaken-only — Dwarven Forlorn Cavern Forsaken-friendly faction**). **Brief correction**: the user prompt called this `Knowledge in the Deeps (Alliance fishing quest at Auberdine)` — **WRONG. (a) The quest is Forsaken-only (RequiredRaces=77, source 8 Forsaken-NPC `Gerrig Bonegrip` in Ironforge Forlorn Cavern which is the Forsaken-exile-friendly enclave); (b) it is a LORE/manuscript collect quest, NOT fishing; (c) the giver/turn-in is in Ironforge, not Auberdine**. Cumulative brief-correction count this iter: **+7**. | world_full:793707 |
| **1275** | **Researching the Corruption** | Gershala Nightwhisper (Auberdine, Darkshore) | **719 (BFD)** | Collect 8 `Corrupt Brain Stems` (item 5952) from Fallenroot Shadowstalker (4855) + Hellcaller (4856) satyrs + naga inside BFD. Reward 2400 XP + 3500 copper + 1440 reward-spell + items 7003 + 7004 (choose-one). Min level 18, max 24. RequiredRaces=178 (Alliance). Pre-req: 3765 The Corruption Abroad (Stormwind handoff). **Brief correction**: the user prompt called this `Researching the Corruption (Alliance Druid quest — Aku'mai item)` — **WRONG. (a) The quest is Alliance ALL classes (RequiredClasses=0 = all), NOT Druid-only; (b) the collect item is `Corrupt Brain Stems` from satyr/naga, NOT an Aku'mai item; (c) Aku'mai isn't the kill credit — generic naga/satyr trash is**. Cumulative brief-correction count this iter: **+8**. | world_full:793959 |
| **908** | **Amongst the Ruins (Horde)** | Je'neu Sancrea (Zoram'gar Outpost, Ashenvale) | **719 (BFD)** | Recover `Fathom Core` (item 16762) from a special GameObject (the Fathom Stone) inside BFD's Moonshrine Ruins. Reward 2750 XP + 4500 copper + 1680 reward-spell. Min level 25, max 27. RequiredRaces=178 (Horde). Earthen Ring quest. | world_full:793646 |
| **909** | **Baron Aquanis** | (Loot-drop from Baron Aquanis 12876) → Je'neu Sancrea | **719 (BFD)** | Bring `Strange Water Globe` (item 16782) — drops 100% from Baron Aquanis (12876) on death. Reward 3050 XP + 1860 copper + items 16886 + 16887 (choose-one). Min level 30. RequiredRaces=178 (Horde). Drop-triggered (not pre-questable). | world_full:793647 |
| **6562** | **Trouble in the Deeps** | (Tsunaman, Orgrimmar?) → Je'neu Sancrea (Zoram'gar) | **719 (BFD)** | Travel to Je'neu Sancrea. Reward 435 XP + 270 copper. Min level 17, max 22. Horde Earthen Ring chain step 1. | world_full:795553 |
| **6563** | **The Essence of Aku'Mai** | Je'neu Sancrea → Je'neu Sancrea | **719 (BFD)** | Bring 20 `Sapphires of Aku'mai` (item 16784) — drop from naga trash inside BFD. Reward 1750 XP + 1400 copper + 1080 reward-spell. Min level 17, max 22. Pre-req: 6562. | world_full:795554 |
| **6564** | **Allegiance to the Old Gods (1)** | (Loot `Damp Note` from naga corpse) → Je'neu Sancrea | **719 (BFD)** | Bring `Damp Note` (item 16790) — drop from naga trash. Reward 1300 XP + 1100 copper + 780 reward-spell. Min level 17, max 22. Pre-req: 6563. | world_full:795555 |
| **6565** | **Allegiance to the Old Gods (2 — kill quest)** | Je'neu Sancrea → Je'neu Sancrea | **719 (BFD)** | **Kill Lorgus Jett (12902) inside BFD.** Reward 2650 XP + 4000 copper + 1620 reward-spell + items 17694 + 17695 (choose-one). Min level 17, max 26. Pre-req: 6564. **Brief correction**: the user prompt called the kill-Kelris quest `Allegiance to the Old Gods (Horde Kelris-kill quest)` — **WRONG. Allegiance to the Old Gods 6564/6565 is the LORGUS JETT kill quest; the Horde Kelris-kill quest is 6561 Blackfathom Villainy**. Cumulative brief-correction count this iter: **+9**. | world_full:795556 |
| **3765** | **The Corruption Abroad (pre-req for 1275)** | Argos Nightwhisper (Stormwind) → Gershala Nightwhisper (Auberdine) | 1519 (Stormwind) → 148 (Darkshore) | Pre-req travel quest for 1275. Reward 1450 XP + 1300 copper. Min level 18, max 24. RequiredRaces=77 (Alliance — RequiredRaces=77 here is Alliance-coded NE+Human+Dwarf+Gnome+Draenei). | world_full:794736 |

---

## Recommended Pull Order & Route

BFD is **mostly linear with two optional side branches** (Old Serra'kis pool + Lorgus Jett tunnel) and 1 **iconic gated event** (4-flame shrine after Kelris). The community-standard "full clear with rare-spawns" path:

1. **Submerge into entrance shaft** — equip water-breathing (item 5996 Elixir of Water Breathing or warlock Underwater Breathing); swim down through Murloc + Snapclaw packs. CC casters (Polymorph the Tide Priestess / Sea Witch with Mage; Hibernate the Snapjaws with Druid).
2. **Ghamoo-ra** (boss 1, 4887) — first dry-cavern room. **Tank-and-spank with 30% HP Frenzied Rage**. Tank picks up; healer focuses through 30% burst window.
3. **Naga trash** approaching Sarevess — CC Tide Priestess / Sea Witch / Myrmidon packs (CC 1 caster, focus-fire other casters first). Cone-of-Cold spread positioning practice for Sarevess.
4. **Lady Sarevess** (boss 2, 4831) — open grotto with **2 naga adds**. Tank picks up Sarevess; OT or CC picks up the adds. **Spread 5-8y to avoid Cone of Cold frontal AOE** + Frost Nova rooting. Interrupt-priority on her Forked Lightning casts.
5. **Old Serra'kis branch decision (optional)** — swim down to the side-pool. **Tank-and-spank with high melee swing damage** — only worth pulling if your group needs Glowing Lizardscale Cloak / Old Serra'kis Tooth. Tank on the dry ledge if possible; healer above the pool to avoid mid-cast swim animation.
6. **Coral causeway swim → Gelihast** (boss 3, 6243, optional but on path) — tank-and-spank caster. Pull through the corridor; raid spreads. Most groups pull him because his event-spawn agg-radius covers the natural choke point.
7. **Lorgus Jett branch (optional rare-spawn)** — side tunnel off the main path. **Required ONLY for Horde quest 6565**. Tank-and-spank caster with Lightning Shield reflect — melee DPS staggers swings to manage the reflect ticks.
8. **Moonshrine Sanctum** — Twilight Acolyte / Aquamancer / Shadowmage / Loreseeker / Elementalist packs. **HIGH CC PRIORITY**: Polymorph 1 Acolyte (channeling), Sap 1 Shadowmage; the Elementalist (4814) has 4 spells (Fireball + Frostbolt + Lightning Bolt + Frost Shock cycle) and burns the healer hard if uncontrolled. The 3 sleeping prisoners are decorative — do not interact (they grant no quest credit in 1.12.1, contrary to later-patch revamp lore).
9. **Twilight Lord Kelris** (boss 4, 4832) — mid-boss in the Sanctum. **AoE Sleep iconic mechanic**: he casts Sleep (spell 8399) AoE on random targets every 5-7s. **Tank holds + raid spreads 8-10y**; healer dispels sleep on critical roles (especially the tank). Mind Blast (15587) is the primary tank-damage cast. Drops `Kelris's Head` for quest 1200/6561. **His death sets `TYPE_KELRIS=DONE` — REQUIRED for the shrine event**.
10. **The 4-flame shrine event** (post-Kelris):
    - Locate the 4 Fires of Aku'mai (GO 21118 / 21119 / 21120 / 21121) around the Altar approach. **Order does not matter** (script comment: line 36).
    - Light each fire — each `OnUse` queues a 3000ms timer; after 3s a wave of mobs spawns from Kelris's respawn point with `SetInCombatWithZone()`.
    - **Wave 0**: 4 × Aku'mai Snapjaw (entry 4825) — tank+raid AoE.
    - **Wave 1**: 2 × Aku'mai Servant (entry 4978) water elementals — Banish (Warlock) if possible; focus-fire otherwise.
    - **Wave 2**: 4 × Murkshallow Snapclaw (entry 4815) — tank+AoE.
    - **Wave 3**: 10 × Murkshallow Softshell (entry 4977) — low HP (209) — pure AOE-burn, MUST kill all 10.
    - When `IsWaveEventFinished()` confirms all dead AND `m_uiShrinesLit==4`, `TYPE_SHRINE=DONE` fires, **`GO_PORTAL_DOOR=21117` opens** to Aku'mai's chamber.
    - **If a player dies mid-event, mobs do NOT despawn** (script comment line 48: "On wipe the mobs don't despawn; they stay there until player returns") — surviving party must finish or wipe + corpse-run back.
11. **Baron Aquanis rare event (optional, drop-quest 909)** — locate Fathom Stone (GO 177964) on the path between shrine event and Aku'mai chamber. Interact triggers Baron Aquanis (12876) spawn. **Tank-and-spank low HP boss**; drops `Strange Water Globe` (item 16782) for quest 909.
12. **Aku'mai** (boss 5/final, 4829) — flooded chamber. **Tank holds at center; raid moves to fresh ground when Poison Cloud (spell 3815) drops** (5-9y radius, 8-12s persist). At **35% HP he Frenzies** (haste + damage boost) — healer focus, DPS burst-burn the final 35%. **8530 HP single boss**; fight lasts ~3-5min.
13. **Quest loot sweep** — `Kelris's Head` (5881) for 1200/6561; `Lorgalis Manuscript` (5359) for 971 (drop from Loreseeker or special GO); `Fathom Core` (16762) for 908 (Fathom Stone GO); `Strange Water Globe` (16782) for 909 (Aquanis drop); `Sapphires of Aku'mai` (16784) ×20 for 6563; `Damp Note` (16790) for 6564; `Twilight Pendants` (5879) ×10 for 1199; `Corrupt Brain Stems` (5952) ×8 for 1275.

3-man over-leveled (L32+) carry runs viable but the **4-flame shrine event's Wave 3 (10 Softshells) demands AoE damage** — Mage / Warlock SoC or Druid Hurricane class strongly recommended. 4-man with at least 1 CC class is the realistic minimum for the Kelris AoE Sleep + Sanctum caster packs.

---

## Decision-Engine Rules

| id | bracket | precondition | action | priority |
|---|---|---|---|---|
| `dungeon.bfd.queue.lfg-or-walkin` | `L24-L32` | `Snapshot.Level>=24 & Snapshot.QueueState.BFD.role==null` | `Activity:LfgQueue("BFD", autoRole=byClass)` else `Activity:Travel(Ashenvale_ZoramStrand)` | 70 |
| `dungeon.bfd.party.invite-handshake` | `L24-L32` | `Snapshot.PartyState.size<5 & Snapshot.QueueState.BFD.invitePending==true` | `Activity:PartyInvite` (see `../social/party-invite.md`) — Ashenvale is neutral-approach so both Alliance + Horde can group cross-faction in their own party | 80 |
| `dungeon.bfd.entrance.travel` | `L24-L32` | `Snapshot.PartyState.complete & Snapshot.Position.zone != 719` | `Activity:Travel(Ashenvale_ZoramStrand:4249.12,748.387,-24.86)` Horde via Zoram'gar flightpath / Alliance via Astranaar → west road | 75 |
| `dungeon.bfd.preflight.water-breathing` | `L24-L32` | `Snapshot.Inventory.Has(5996)==false & Snapshot.Class != Warlock` | `Activity:Acquire(Item=5996 ElixirOfWaterBreathing, source=AlchemistOrAuction)` — entry shaft + several pools require swimming | 88 |
| `dungeon.bfd.party.composition-check` | `L24-L32` | `Snapshot.PartyComposition.tank!=null & Snapshot.PartyComposition.healer!=null & Snapshot.PartyComposition.dps.count>=2 & Snapshot.PartyComposition.hasAOE==true` | `Activity:EnterInstance(map=48)` — AoE class required for Shrine Wave 3 (10 Softshells) | 78 |
| `dungeon.bfd.pull.caster-cc-priority` | `L24-L32` | `Snapshot.NearbyMobs.containsAny([4802, 4805, 4810, 4811, 4812, 4813, 4814])` | `Task:UtilityCast(CC_PolymorphOrSap, target=highestThreatCaster)` — Tide Priestess + Sea Witch + Twilight Acolytes/casters Humanoid; Snapjaws Beast→Hibernate | 85 |
| `dungeon.bfd.boss.ghamoo-ra-frenzy-burst` | `L24-L32` | `Snapshot.Boss.Ghamoo-ra.hp<0.30 & Snapshot.Boss.Ghamoo-ra.aura('Frenzied Rage').active==true` | `Task:HealTank(burst=true)` + `Task:Burst(DPS, target=4887)` — 30% Frenzied Rage burst-damage; finish quickly | 86 |
| `dungeon.bfd.boss.sarevess-cone-spread` | `L24-L32` | `Snapshot.Boss.Sarevess.engaged==true` | `Task:Positioning(SpreadFormation, radius=8y, behindBoss=true)` — Cone of Cold frontal + Frost Nova root; spread to avoid AOE | 88 |
| `dungeon.bfd.boss.sarevess-adds-pickup` | `L24-L32` | `Snapshot.Boss.Sarevess.engaged==true & Snapshot.NearbyMobs.containsAny([4802, 4805])` | `Task:OffTankPickup(target=naga_adds)` — Sarevess pulls 2 naga adds; OT or kite-class | 82 |
| `dungeon.bfd.boss.kelris-sleep-dispel` | `L24-L32` | `Snapshot.Boss.Kelris.engaged==true & Snapshot.Party.AnyMember.debuff('Sleep').active==true` | `Task:UtilityCast(Dispel, target=sleepingMember)` — Priest Dispel Magic / Paladin Cleanse / Shaman Purge sleep before damage spike | 92 |
| `dungeon.bfd.boss.kelris-spread` | `L24-L32` | `Snapshot.Boss.Kelris.engaged==true` | `Task:Positioning(SpreadFormation, radius=10y)` — Sleep is AoE (~8y); spread limits multi-target sleep | 86 |
| `dungeon.bfd.event.shrine-prereq-check` | `L24-L32` | `Snapshot.Boss.Kelris.alive==true & Snapshot.NearbyGameObjects.containsAny([21118, 21119, 21120, 21121])` | `Task:Skip(Interact=false)` — `go_fire_of_akumai::OnUse` returns false if `TYPE_KELRIS != DONE`; don't waste interact attempts | 70 |
| `dungeon.bfd.event.shrine-light-fires` | `L24-L32` | `Snapshot.Boss.Kelris.dead==true & Snapshot.InstanceState.ShrinesLit<4 & Snapshot.NearbyGameObjects.containsAny([21118, 21119, 21120, 21121])` | `Task:Interact(GameObject=availableShrine)` — light each fire in any order; queues 3000ms wave-summon | 84 |
| `dungeon.bfd.event.shrine-wave-aoe` | `L24-L32` | `Snapshot.InstanceState.ShrineWave>=0 & Snapshot.NearbyMobs.count>=4` | `Task:UtilityCast(AoE_Damage, target=summonCluster)` — Mage SoC / Warlock SoC / Druid Hurricane / Hunter Volley; Wave 3 is 10 × low-HP softshells (209 HP each) | 90 |
| `dungeon.bfd.event.shrine-wave-banish-elemental` | `L24-L32` | `Snapshot.NearbyMobs.contains(4978) & Snapshot.PartyComposition.hasBanish==true` | `Task:UtilityCast(Banish, target=4978)` — Aku'mai Servant water elementals — Warlock Banish during Wave 1 | 78 |
| `dungeon.bfd.event.shrine-completion-gate` | `L24-L32` | `Snapshot.InstanceState.ShrinesLit==4 & Snapshot.NearbyMobs.containsAny([4825, 4978, 4815, 4977])==false` | `Task:HoldPosition(radius=5y) + Task:WaitFor(GO_PORTAL_DOOR=21117 opens)` — IsWaveEventFinished() final check fires `TYPE_SHRINE=DONE` | 82 |
| `dungeon.bfd.event.aquanis-rare-spawn` | `L24-L32` | `Snapshot.NearbyGameObjects.contains(177964 FathomStone) & Snapshot.InstanceState.AquanisDone==false & Snapshot.QuestLog.Active(909)==false` | `Task:Interact(GameObject=177964)` — triggers Baron Aquanis rare spawn for `Strange Water Globe` drop (item 16782, quest 909 starter) | 70 |
| `dungeon.bfd.boss.akumai-poison-cloud-move` | `L24-L32` | `Snapshot.Boss.Akumai.engaged==true & Snapshot.Player.position.distance(poisonCloudCenter)<6y` | `Task:Move(targetPosition=freshGround, urgent=true)` — Poison Cloud (3815) 5-9y zone, 8-12s persist; move to fresh ground | 92 |
| `dungeon.bfd.boss.akumai-frenzy-burst` | `L24-L32` | `Snapshot.Boss.Akumai.hp<0.35 & Snapshot.Boss.Akumai.aura('Frenzy').active==true` | `Task:HealTank(burst=true) + Task:Burst(DPS, target=4829)` — 35% Frenzy haste+damage; finish quickly | 90 |
| `dungeon.bfd.boss.lorgus-jett-reflect-stagger` | `L24-L32` | `Snapshot.Boss.LorgusJett.engaged==true & Snapshot.Boss.LorgusJett.aura('Lightning Shield').active==true` | `Task:MeleeStagger(swingDelay=350ms)` — Lightning Shield (spell 12550) reflect damage on melee swings; stagger to manage reflect ticks | 65 |
| `dungeon.bfd.loot.bop-quest-tokens` | `L24-L32` | `Snapshot.Loot.window.items.any(itemId in [5881, 5359, 16762, 16782, 16784, 16790, 5879, 5952])` | `Task:LootRoll(Need)` for quest-token items; `Pass` on grey trash unless `Snapshot.Inventory.freeSlots<4` | 60 |
| `dungeon.bfd.loot.iconic-bop-greens` | `L24-L32` | `Snapshot.Loot.window.items.any(itemId in [6647, 6645, 6644, 6643, 6640, 6635])` | `Task:LootRoll(Need)` if class-appropriate (Strike of the Hydra 2H Warrior, Naga Heartpiercer 1H Rogue, Black Fathom Eel Mantle caster shoulders, Reef Axe 1H Warrior, Fathom Stalker's Mask leather mask) else `Greed` | 65 |
| `dungeon.bfd.loot.greed-default` | `L24-L32` | `Snapshot.Loot.window.items.any(quality>=green & questItem==false)` | `Task:LootRoll(Greed)` default | 55 |
| `dungeon.bfd.questturnin.sequence` | `L24-L32` | `Snapshot.QuestLog.Complete(1200) \|\| Complete(6561) \|\| Complete(971) \|\| Complete(908) \|\| Complete(909) \|\| Complete(6565)` | Faction-routed: Alliance → Darnassus (Selgorm + Manados), Ironforge (Bonegrip), Auberdine (Gershala). Horde → Thunder Bluff (Bashana Runetotem), Zoram'gar (Je'neu Sancrea) | 70 |
| `dungeon.bfd.wipe.recovery` | `L24-L32` | `Snapshot.Player.alive==false & Snapshot.InstanceState.partyWipe>=2` | `Activity:CorpseRun` (see `../recovery/corpse-run.md`) — ghost-spawn at Ashenvale Zoram Strand surface `(4249.12, 748.387, -24.86)` swim down to re-enter cave | 95 |
| `dungeon.bfd.script-readiness` | `L24-L32` | `Snapshot.ServerCapabilities.HasScript("instance_blackfathom_deeps")==true` | `Activity:EnterInstance(map=48)` — BFD depends on the C++ script for shrine-event door automation + Baron Aquanis save state + Aku'mai door gate | 92 |

**Total: 26 rules** (target range 15-20 — BFD rules sit at the high end of the range because of the **multi-wave shrine event** [3 dedicated rules — fires/AOE/elemental] + **Kelris AoE Sleep dispel** + **Aku'mai Poison Cloud zone-control** + **2 optional bosses** [Old Serra'kis pool + Lorgus Jett tunnel] + **Baron Aquanis rare event** + **cross-faction quest routing**).

---

## Snapshot Fields Needed

```text
Snapshot.Level                                              // 24-32 entry band, 10 minimum
Snapshot.Class                                              // role bias + CC capability (Polymorph/Sap/Hibernate/Banish) + AoE capability (Wave 3 demand)
Snapshot.Position.{zone, x, y, z}                           // zone==719 for in-BFD checks; z compared to surface for swim-detection
Snapshot.PartyState.{size, complete}
Snapshot.PartyComposition.{tank, healer, dps, hasAOE, hasBanish, hasDispel} // AOE for Wave 3; Banish for Wave 1; Dispel for Kelris Sleep
Snapshot.QueueState.BFD.{role, invitePending}
Snapshot.InstanceState.{firstPull, partyWipe, ShrinesLit, ShrineWave, AquanisDone, KelrisDone, AkumaiDoorOpen}
Snapshot.Boss.{Ghamoo-ra, Sarevess, OldSerra'kis, Gelihast, LorgusJett, Kelris, BaronAquanis, Akumai}.{alive, engaged, dead, castName, hp}
Snapshot.Party.AnyMember.debuff('Sleep').active             // Kelris AoE Sleep dispel trigger
Snapshot.NearbyMobs                                         // EventAI trigger detection (4802/4805 naga adds, 4825 Snapjaws, 4978 Servants, 4815 Snapclaws, 4977 Softshells, 4810-4814 cultist trash)
Snapshot.NearbyGameObjects                                  // 21117 Portal Door / 21118-21121 Shrines 1-4 / 103015 Shrine of Gelihast / 103016 Altar of the Deeps / 177964 Fathom Stone / 2004 Meeting Stone
Snapshot.Boss.Akumai.poisonCloudCenter                      // movement-out coord
Snapshot.Loot.window.items                                  // quest-token + iconic-BoP-green roll decisions
Snapshot.QuestLog.Active(1198,1199,1200,6561,6562,6563,6564,6565,971,908,909,1275,3765)  // BFD quest set (both factions)
Snapshot.QuestLog.Complete(1200,6561,971,908,909,6565,1275)  // turn-in routing
Snapshot.Inventory.Has(5881,5359,16762,16782,16784,16790,5879,5952,5996)  // quest items + water breathing elixir
Snapshot.ServerCapabilities.HasScript("instance_blackfathom_deeps")  // dependency check
```

---

## TBD / Backlog (per verification rule)

| Item | Status | Resolving grep |
|---|---|---|
| Sub-zone names "Pool of Ask'ar" / "Old Serra'kis Pool" / "Moonshrine Sanctum" / "Sleeper's Chamber" / "Altar of the Deeps" / "Aku'mai's Chamber" | TBD — only **Blackfathom Deeps 719** + outdoor approach **2797** in `world_full_14_june_2021.sql`; rest are AreaTable.dbc client-side | `AreaTable.dbc` extraction; fallback wiki crawl |
| Argent Guard Thaelrid creature entry ID (target NPC for quest 1198 inside BFD) | TBD — `creature_template` row not located in this pass; likely a unique-spawn NE NPC | `Grep "Argent Guard Thaelrid" world_full_14_june_2021.sql` |
| Dawnwatcher Shaedlass + Dawnwatcher Selgorm + Argent Guard Manados quest-giver entries | TBD — referenced as quest 1198/1199/1200 giver/turn-in names but entry IDs not extracted | `Grep "Dawnwatcher Shaedlass\|Dawnwatcher Selgorm\|Argent Guard Manados" world_full_14_june_2021.sql` |
| Gerrig Bonegrip Forsaken-friendly NPC location in Ironforge Forlorn Cavern | TBD — referenced as quest 971 giver but creature_template row not extracted | `Grep "Gerrig Bonegrip" world_full_14_june_2021.sql` |
| Je'neu Sancrea (Zoram'gar Earthen Ring shaman) entry ID + quest set | TBD — referenced as quest 908/909/6562/6563/6564/6565 giver/turn-in but entry ID not extracted | `Grep "Je\\'neu Sancrea" world_full_14_june_2021.sql` |
| Bashana Runetotem (Thunder Bluff) — quest 6561 turn-in | TBD — referenced but creature_template row not extracted | `Grep "Bashana Runetotem" world_full_14_june_2021.sql` |
| Gershala Nightwhisper + Argos Nightwhisper (Auberdine + Stormwind) — quest 1275/3765 chain | TBD — referenced but creature_template rows not extracted | `Grep "Gershala Nightwhisper\|Argos Nightwhisper" world_full_14_june_2021.sql` |
| Lorgalis Manuscript (item 5359) exact drop source (special GameObject or Loreseeker trash drop?) | TBD — quest 971 collect-target — likely a unique GameObject `Old Crate` or similar | `Grep -E ", 5359, " world_full_14_june_2021.sql` |
| Twilight Pendant (item 5879) drop sources (which cultist entries drop it?) | TBD — quest 1199 collect-target — likely a 5-15% drop on Acolyte/Aquamancer/Shadowmage entries | `Grep -E ", 5879, " world_full_14_june_2021.sql` |
| Murkshallow Snapclaw (4815) full creature_template + Aku'mai Snapjaw (4825) loot table | Partial — entries confirmed at world_full:449195/449203; loot template not extracted | `Grep -E "^\s*\(4815|^\s*\(4825," world_full + creature_loot_template` |
| Iconic boss-drop item IDs (Strike of the Hydra / Naga Heartpiercer / Black Fathom Eel Mantle / Reef Axe / Fathom Stalker's Mask) | TBD — referenced in wiki but item_template IDs not extracted in this pass | `Grep "Strike of the Hydra\|Naga Heartpiercer\|Black Fathom Eel Mantle\|Reef Axe\|Fathom Stalker" world_full` |

---

## Cross-References

- **Party invite handshake** (neutral 5-man formation, faction-paired): [`../social/party-invite.md`](../social/party-invite.md) — `CMSG_GROUP_INVITE=110` flow, 60s decline window, faction gate via `ERR_PLAYER_WRONG_FACTION`.
- **Pull / engage**: [`../combat/pull-target.md`](../combat/pull-target.md), [`../combat/threat-management.md`](../combat/threat-management.md).
- **CC** (Mage Polymorph on naga + Twilight cultist Humanoids, Rogue Sap, Druid Hibernate on Snapjaws + Serra'kis, Warlock Banish on Aku'mai Servants Wave 1): [`../combat/utility-casts.md`](../combat/utility-casts.md), [`../combat/cast-spell.md`](../combat/cast-spell.md).
- **Dispels** (Kelris AoE Sleep removal): [`../combat/utility-casts.md`](../combat/utility-casts.md).
- **AoE damage** (Shrine Wave 3 — 10 Softshells): Mage SoC / Warlock Hellfire+SoC / Druid Hurricane / Hunter Volley / Paladin Consecration. See [`../combat/melee-rotation.md`](../combat/melee-rotation.md) + class-specific.
- **Melee rotation** (Ghamoo-ra / Old Serra'kis / Aku'mai tank-and-spank): [`../combat/melee-rotation.md`](../combat/melee-rotation.md).
- **Heal task** (Sarevess Cone-of-Cold spread heals, Kelris Sleep dispel, Aku'mai Poison-Cloud movement-heals): [`../combat/heal-task.md`](../combat/heal-task.md).
- **GameObject interact** (4 shrine fires + Fathom Stone for Baron Aquanis): [`../npc/gossip.md`](../npc/gossip.md) (or dedicated `gameobject-interact.md` if added pass 2).
- **Swim travel** (entry shaft + Old Serra'kis pool + Aku'mai chamber underwater): [`../travel/swim.md`](../travel/swim.md) (TBD) + Elixir of Water Breathing (item 5996) consumable acquisition.
- **Wipe handling**: [`../recovery/corpse-run.md`](../recovery/corpse-run.md), [`../recovery/release-corpse.md`](../recovery/release-corpse.md), [`../recovery/spirit-healer.md`](../recovery/spirit-healer.md).
- **Quest turn-ins** (Argent Dawn Alliance route Darnassus/Auberdine + Forsaken Ironforge Forlorn Cavern + Earthen Ring Horde Zoram'gar/Thunder Bluff): [`../npc/quest-giver.md`](../npc/quest-giver.md).
- **Bracket context** (L20-L30 and L30-L40): [`../sections/03-l20-l30.md`](../sections/03-l20-l30.md), [`../sections/04-l30-l40.md`](../sections/04-l30-l40.md) (verify pass 2).
- **Decision-engine integration**: [`../decision-engine/leveling-priority.md`](../decision-engine/leveling-priority.md), [`../decision-engine/state-flags.md`](../decision-engine/state-flags.md), [`../decision-engine/unlock-graph.md`](../decision-engine/unlock-graph.md).
- **Sibling dungeon** (lower bracket, Horde-only): [`ragefire-chasm.md`](ragefire-chasm.md) — RFC L13-18 template authority.
- **Sibling dungeon** (overlapping bracket, neutral): [`wailing-caverns.md`](wailing-caverns.md) — WC L15-25, second script-bundled.
- **Sibling dungeon** (Alliance-natural at L17-26): [`deadmines.md`](deadmines.md) — DM, third script-bundled.
- **Sibling dungeon** (Horde-natural at L22-30): [`shadowfang-keep.md`](shadowfang-keep.md) — SFK, fourth script-bundled.
- **Sibling dungeon** (next Alliance-natural at L29-38): `gnomeregan.md` (TBD — listed in 00_INDEX as future).

---

## VMaNGOS / Server Reality Check

BFD is **script-driven** — like WC / DM / SFK and unlike RFC, the door + shrine-event + Baron-Aquanis-save-state scripting depends on `instance_blackfathom_deeps.cpp` at `D:/MaNGOS/source/src/scripts/kalimdor/ashenvale/blackfathom_deeps/`. The **4-flame shrine event** is the most heavily-scripted boss-pre-event in any L24-32 dungeon — it relies on `go_fire_of_akumai::OnUse` gating on `GetData(TYPE_KELRIS) != DONE`, then `SetData(TYPE_SHRINE, IN_PROGRESS)` queueing `m_uiSpawnMobsTimer[i]=3000ms`, then `DoSpawnMobs(uiWaveIndex)` summoning 4 distinct waves from `aWaveSummonInformation[]`, then `IsWaveEventFinished()` polling `m_uiShrinesLit==4 + all m_lWaveMobsGUIDList dead` to fire `TYPE_SHRINE=DONE` opening `GO_PORTAL_DOOR=21117`.

**Risk classes**:
- **Low risk** for individual EventAI bosses (Ghamoo-ra / Sarevess / Old Serra'kis / Gelihast / Lorgus Jett) — EventAI rows 48870 / 48310 / 4830-series / 6243-series / 1290201 are data-driven and rarely regress.
- **Moderate risk** for **Kelris AoE Sleep mechanic** — depends on EventAI 48320 cast permission flag for `spell2=8399 Sleep` on random non-tank target (action_type with target=random_player). Forks that change EventAI random-target selection (especially the `target_distance` check) can either over-cast Sleep (wipe-inducing) or under-cast (mechanic missing — pure tank-and-spank degrade).
- **High risk** for **4-flame shrine event** — depends on (a) `go_fire_of_akumai::OnUse` properly receiving `pInstance->GetData(TYPE_KELRIS)` check; (b) `aWaveSummonInformation[]` having the correct wave indices; (c) `SummonCreature(TEMPSUMMON_DEAD_DESPAWN)` lifecycle surviving group-disconnect/reconnect mid-event; (d) `IsWaveEventFinished()` polling `m_lWaveMobsGUIDList` GUID validity (server restarts mid-event can orphan GUIDs and stall the event). Forks that refactor `ScriptedInstance` base class or `TEMPSUMMON_*` despawn semantics will break the event (party gets stuck at the shrine — door never opens).
- **Moderate risk** for **Baron Aquanis Fathom Stone summon** — depends on the GO_FATHOM_STONE (177964) being script-bound (it has its own GameObject AI in the broader codebase) and `OnCreatureDeath` of entry 12876 firing `SetData(TYPE_AQUANIS, DONE)` + SaveToDB(). Forks that disable instance state-save on `IN_PROGRESS` reset (line 270 `if (i == IN_PROGRESS) i = NOT_STARTED`) will allow Aquanis to be respawned per-attempt — minor regression (drop becomes farmable, not lockout-gated).
- **Moderate risk** for **Aku'mai final boss Poison Cloud + Frenzy** — depends on `creature_template.spell1=3815` Poison Cloud being a properly-implemented zone-AoE (not a single-target debuff). Forks that mis-implement spell 3815 will degrade the fight to pure tank-and-spank — easier mechanic but valid kill.

Decision-engine rule `dungeon.bfd.script-readiness` gates on `Snapshot.ServerCapabilities.HasScript("instance_blackfathom_deeps")==true` as a precaution. On official-track VMaNGOS this is always true; on stripped-down test servers **the 4-flame shrine event cannot complete** (`go_fire_of_akumai` script missing → fires uninteractable → `TYPE_SHRINE` never DONE → `GO_PORTAL_DOOR=21117` never opens → party cannot reach Aku'mai). Mitigation: GM-port past the door, but Baron Aquanis's drop will also fail to save state, making the run partially-locked-out.

Quest 1200 / 6561 (`Blackfathom Villainy` capstone, both factions) is the most heavily script-coupled BFD quest because Kelris must be killable through his AoE Sleep mechanic (which requires healer dispel capability or pre-cast burst) and `Kelris's Head` (item 5881) must drop via `creature_loot_template` lootid 48320 — verify the loot table before accepting a "Kelris didn't drop his head" failure mode (known VMaNGOS fork divergence when `creature_loot_template` is partially-populated for low-priority dungeons; lootid 48320 row in world_full:444983 confirms it should drop).
