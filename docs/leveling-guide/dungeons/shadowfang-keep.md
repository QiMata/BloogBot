---
title: "Dungeon — Shadowfang Keep (SFK)"
patch: "1.12.1 (Drums of War, Sept 2006)"
sources_crawled:
  - D:/MaNGOS/sql/world_full_14_june_2021.sql (map_template / creature_template / quest_template / worldsafelocs / game_object_template / creature_ai_scripts / area_template)
  - D:/MaNGOS/sql/world_full_14_june_2021.sql:728018 (map_template header)
  - D:/MaNGOS/source/src/scripts/eastern_kingdoms/silverpine_forest/shadowfang_keep/instance_shadowfang_keep.cpp
  - https://warcraft.wiki.gg/wiki/Shadowfang_Keep
crawl_date: 2026-05-20
---

# Shadowfang Keep (SFK) — 5-Man Dungeon Guide

Fourth file in the dungeons/ family, authored to the `ragefire-chasm.md` template contract. SFK is the **Horde-natural L22-30 dungeon** and the dungeon-side climax of the **Silverpine Forest worgen story** that begins with Pyrewood Village (zone 204) being terrorised by the Council of Pyrewood — cursed townsfolk who transform into worgen each night. Entrance is in the haunted **Shadowfang Keep zone (209)** in southern Silverpine, reached up a winding hill north of Pyrewood Village. Inside is a multi-floor cursed-keep dungeon: courtyard → kennels → state rooms → ritual chamber → **Arugal's audience hall**. **6 named bosses** (Rethilgore, Razorclaw the Butcher, Baron Silverlaine, Commander Springvale, Odo the Blindwatcher, Fenrus the Devourer, Wolf Master Nandos, Archmage Arugal — wait, that's 8) **+ 2 escort NPCs (Deathstalker Adamant Horde / Sorcerer Ashcrombe Alliance) who unlock the courtyard door via an intro event**. **Level band 22-30 optimal**, ~45min-1h full clear. SFK has a **dedicated C++ script** at `D:/MaNGOS/source/src/scripts/eastern_kingdoms/silverpine_forest/shadowfang_keep/instance_shadowfang_keep.cpp` (script-name `instance_shadowfang_keep`), giving it the same **moderate scripting risk** class as WC and DM (script-driven intro door, Fenrus voidwalker-channel event, Nandos minion-call phase machine, Baron + Springvale post-death hidden patrol spawns).

---

## Quick Facts

| Field | Value | Source |
|-------|-------|--------|
| `map_id` | **33** | `map_template:728018` row `(33, 0, 0, 1, 0, 40, 0, 0, -230.989, 1571.57, 'Shadowfang Keep', 'instance_shadowfang_keep')` — `map_type=1` (5-man dungeon), `script_name='instance_shadowfang_keep'` (**has a dedicated C++ script** — like WC and DM, unlike RFC). Note: zone 33 in `areatable` is Stranglethorn — `map_id` and `zone_id` share the integer 33 by coincidence; the host zone for SFK is the dungeon's **own zone 209**, not Stranglethorn. |
| Continent / Parent map | Eastern Kingdoms (map 0) | `map_template:728018 ghost_entrance_map=0` |
| Host zone / linked zone | **209 "Shadowfang Keep"** (the dungeon's own zone) | `areatable` row at world_full:1006 `(209, 33, 0, 266, 0, 16, 'Shadowfang Keep', 4, 0)` — `parent_area=0` (a top-level zone — SFK is its own zone, not a sub-zone of Silverpine). |
| Outdoor host zone | **Silverpine Forest (zone 130)** — entrance approach through Pyrewood Village (zone 204, area_template `(204, 0, 130, 261, 64, 15, 'Pyrewood Village', 0, 0)` at world_full:1001) | `areatable:943 (130, 0, 0, 210, 64, 0, 'Silverpine Forest', 4, 0)` |
| Group size | 5-man (`player_limit=40` legacy; instance type 1 enforces 5) | `map_template.player_limit` |
| Reset delay | 0 (standard instance lockout) | `map_template.reset_delay=0` |
| Level range | **22-30** optimal (level **10 minimum** to enter) | Worldsafelocs `(145, 0, 'Shadowfang Keep - Entrance', 'You must be at least level 10 to enter.', 10, 0, 33, -228.191, 2111.41, 76.8904, 1.22173)` at world_full:178 |
| Faction | **Horde-natural** (Silverpine is Forsaken contested) — Alliance access requires extended cross-zone travel via Ironforge → Loch Modan → Wetlands ferry to Menethil Harbor → boat to Theramore... or via Stormwind → Refuge Pointe → Hillsbrad; outdoor approach is friendly for Forsaken and hostile for Alliance. Both factions have inside-instance escort NPCs (Adamant Horde / Ashcrombe Alliance). | Geographic |
| Meeting Stone | **GameObject 2003** `'Meetingstone - Shadowfang Keep'` lvl 36 (cluster range 18-25) | `game_object_template` row at world_full:563788 `(2003, 0, 36, 209, ...)` |
| Theme | Cursed Dalaran-built keep overrun by **worgen summoned via extra-dimensional ritual** (lore: Archmage Arugal summoned worgen to fight the Scourge during the Third War; they slaughtered the Scourge then turned on the Dalaran wizards and Baron Silverlaine's defenders, driving Arugal mad with guilt — `gossip_text:9884` world_full:10802). |
| Boss count | **8 named bosses** (Rethilgore + Razorclaw the Butcher + Baron Silverlaine + Commander Springvale + Odo the Blindwatcher + Fenrus the Devourer + Wolf Master Nandos + Archmage Arugal) — most are EventAI-driven; **Fenrus + Nandos + Arugal** are heavily tied to the `instance_shadowfang_keep` C++ script state machine via `TYPE_FENRUS` (voidwalker channel event spawns Archmage Arugal post-Fenrus-death) and `TYPE_NANDOS` (opens Arugal door on Nandos death). |

**Entrance (Pyrewood Village → keep hill → instance portal)**: outdoor approach is up the cliff road north of Pyrewood Village to the haunted keep gates. The inside-instance entry WSL `145` is at instance-local `(-228.191, 2111.41, 76.8904)`. The exit WSL `194` is at `(-233.011, 1567.5, 76.8921)` (the lower courtyard, near the entrance arch). **4 jump-exit WSLs (2406-2411)** exist at the upper terraces `(-276.241, 1652.68, 77.56)`, `(-225.34, 1556.53, 93.04)`, `(-181.26, 1580.65, 97.45)` — these are used by Archmage Arugal's iconic **boss-fight teleport platforms** (he warps players between three balconies during the fight; falling off is non-fatal because the jump-exit WSLs put you back on the parapet).

**Brief correction**: the user prompt listed "Razorclaw the Butcher" with no entry guess. **Confirmed: entry 3886, lvl 22, 2248 HP, EventAI 388601 casting `Butcher Drain` spell 7485** (world_full:448468 + EventAI 86359 + AI script 93128). Cumulative brief-correction count this iter: **+0** (Razorclaw is real, brief was correct to include him). However the brief also implied "Hati / wolf form?" as a Fenrus alt-form — **Fenrus does not have a wolf-form transformation; Hati is a Stormrage/Cataclysm-era name not present in 1.12.1 mangos**. Cumulative brief-correction count this iter: **+1**.

**Ghost-entrance back to outside the dungeon** on death: `map_template.ghost_entrance_x=-230.989, ghost_entrance_y=1571.57` (Silverpine SFK courtyard surface — corpse spawns just outside the keep gates for a short run-back, mirroring DM's "right at the barn entrance" pattern).

---

## Geography & Sub-Zones

SFK is **mostly linear with one notable side branch** (Razorclaw's kennel) and an intro event gate (Adamant/Ashcrombe). The only sub-zone in `world_full_14_june_2021.sql` is **Shadowfang Keep (209)** itself; the floor-by-floor named sub-rooms below come from AreaTable.dbc client data and wiki and are flagged **TBD**.

1. **Courtyard / Entrance Hall** — first room past WSL `145`. Pyrewood-deceased trash (Shadowfang Moonwalker 3853 + Shadowfang Glutton 3857) + worgen kennel adds. **Gate-event NPCs Deathstalker Adamant (3849)** for Horde or **Sorcerer Ashcrombe (3850)** for Alliance are imprisoned here; freeing them via gossip starts the intro **TYPE_FREE_NPC=DONE** state machine (`instance_shadowfang_keep.cpp:255-258`), which fires AI script row 384911 (Adamant) / 385012 (Ashcrombe) playing 4 say-lines + casting `Ashcrombe's Unlock` spell **6421** + `Ashcrombe's Teleport` spell **6422** to open the **Courtyard Door (GO 18895)** and despawn the NPC. The instance state then permanently records `m_auiEncounter[0]=DONE` so the door stays open on save/load.
2. **Rethilgore's Cell Block** [TBD AreaTable name — wiki calls it "The Cellar"] — **Rethilgore "The Cell Keeper" (3914, lvl 20, 1936 HP)** waits in the prison block. Pure tank-and-spank caster (creature_template.spell1=7295 `Soul Drain`). His death sets `TYPE_RETHILGORE=DONE` but does NOT open any door (door automation is gated on Fenrus + Nandos kills only, not Rethilgore — `OnCreatureDeath` and `SetData` switches in the script have no door call for `TYPE_RETHILGORE`).
3. **Worgen Kennels** [TBD AreaTable name — wiki calls it "The Kennel"] — **Razorclaw the Butcher (3886, lvl 22, 2248 HP)** is in a side-alcove. EventAI 388601 casts `Butcher Drain` spell **7485** at 0-5y range (melee drain). Drops `Butcher's Cleaver` (item 6622). Optional side-pull; required for some quest variants (wiki cites him as quest mob for some quest chains but no quest_template row directly references entry 3886).
4. **Baron Silverlaine's Audience Hall** [TBD AreaTable name] — large hall with **Baron Silverlaine (3887, lvl 24, 3255 HP)** at the throne. `creature_template.spell1=7068` `Veil of Shadow` (debuff that reduces healing 75% on tanks — major mechanic). **His death triggers a 6000ms hidden patrol spawn** of Shadowfang Wolfguards (entry 3854, `RespawnDelay=7201` sentinel) becoming visible via `m_uiSpawnPatrolOnBaronDeath` machine (`instance_shadowfang_keep.cpp:202-223`) — these are pre-spawned `VISIBILITY_OFF + faction 35 (passive)` and flip to `VISIBILITY_ON + faction 17 (hostile)` after the 6s delay.
5. **Commander Springvale's Quarters** [TBD AreaTable name] — **Commander Springvale (4278, lvl 24, 2855 HP)** — a Paladin-style boss. EventAI 427801 casts `Holy Light` **1026** on friendly adds (12.5-22.3s cd), 427802 casts Holy Light on self at 80% HP, 427803 casts `Divine Protection` **498** at 25% HP for 2.5s burst-immunity. **Same hidden-patrol mechanic as Baron**: `m_uiSpawnPatrolOnCmdDeath` 6000ms delay + RespawnDelay=7202 sentinel + Wolfguards (3854) flip from passive to hostile (`instance_shadowfang_keep.cpp:225-247`).
6. **Odo the Blindwatcher's Watchtower** [TBD AreaTable name] — **Odo the Blindwatcher (4279, lvl 24, 3255 HP)** in the top of a tower. EventAI 427901 `Call For Help on Aggro` (pulls nearby Bleak Worgs 3868 + Vile Bats 38660) + 427902/427903/427904 casts `Howling Rage` (spell 7708 or similar self-buff) at 75% / 50% / 25% HP thresholds (3 progressive buffs — stacking enrage).
7. **The Ritual Chamber / Fenrus the Devourer** [TBD AreaTable name — wiki calls it "The Worgen Ritual Chamber"] — **Fenrus the Devourer (4274, lvl 25, 3495 HP)** — a giant black worgen on a ritual circle. `creature_template.spell1=7125` `Fenrus Hex`. EventAI 427402/427403/427404 set instance data on aggro/death/evade for the `TYPE_FENRUS` machine. On combat enter he plays `SOUND_FENRUS_AGGRO=6017` howl (`instance_shadowfang_keep.cpp:168-171`). **Iconic post-death event**: when Fenrus dies, `TYPE_FENRUS=DONE` is set; `Archmage Arugal (10000, "Arugal" intro NPC)` spawns from `GO_ARUGAL_FOCUS=18973` lightning visual and channels a **VoidWalker summon** event (5 Arugal's Voidwalker adds spawn one at a time at the focus). Killing all 5 voidwalkers fires `TYPE_VOIDWALKER` increments — after the 4th voidwalker kill (`if (m_auiEncounter[5] > 3)` line 278) the `GO_SORCERER_DOOR=18972` door opens. Note the script comment: "*for this we ignore voidwalkers, because if the server restarts they won't be there, but Fenrus is dead so the door can't be opened!*" — meaning the door also unconditionally opens on Fenrus death via `if (m_auiEncounter[2] == DONE) pGo->SetGoState(GO_STATE_ACTIVE)` in `OnObjectCreate` (line 187-190).
8. **Wolf Master Nandos's Worg Pen** [TBD AreaTable name] — **Wolf Master Nandos (3927, lvl 25, 4194 HP)** with active worg pets. EventAI 392701/392702/392703 progressively `Call Bleak Worg` at 75% HP / `Call Slavering Worg` at 50% HP / `Call Lupine Horror` at 25% HP (each adds a more dangerous worg pet — the iconic "Wolf Master commands his pack" sequence). EventAI 392707 (`Increment Phase on Group Member Died`) tracks pet-death state; 392708 (`Say Text on Group All Dead`) speaks a flavor line if all 3 worgs die before Nandos. **His death sets `TYPE_NANDOS=DONE` and opens `GO_ARUGAL_DOOR=18971`** (`instance_shadowfang_keep.cpp:266-269`) — the gate to Arugal's audience hall.
9. **Archmage Arugal's Audience Hall** [TBD AreaTable name — wiki calls it "Arugal's Lair"] — the final chamber. **Archmage Arugal (4275, lvl 26, 6510 HP, 2772 mana)** stands on a central platform with **3 balcony teleport pads** around him. `creature_template.spell1=7588` `Void Bolt` + `spell2=7803` `Thundershock` AoE silence + `spell3=7621` (creature_ai_thresholds gives him a 4th school via AI script). EventAI 427502 (in the 42750-series rows at world_full:443164) has him cast `Void Bolt 7588` 100% chance on cooldown + `Thundershock 7803` at 80% HP + a melee proc spell `7621` (matched to AI script 42750 row pattern). **Iconic platform-teleport mechanic**: at random intervals (~30s) Arugal teleports a single random player to one of the 3 balcony platforms via spell 6422 (`Ashcrombe's Teleport` — reused as `Arugal's Teleport`); the player must run back to the central platform while taking sustained `Void Bolt` casts. Falling off the balcony is non-fatal — the 4 jump-exit WSLs (2406-2411) put you on a lower platform from which you can re-enter the fight room. **Tank-and-spank with positional pressure**.

---

## Mob Ecology

| Entry | Creature | Level | HP | Type | Source row |
|-------|----------|-------|----|----|----|
| **3851** | Shadowfang Whitescalp | 18-19 | 1131-1212 | Humanoid (worgen) caster — spell **12544** (Frost Armor) on spawn + missing-buff refresh | world_full:448444 `creature_template` + EventAI 385101/385102 at world_full:87501-87502 |
| **3853** | Shadowfang Moonwalker | 19-20 | 1212-1299 | Humanoid (worgen) melee — spell **7121** | world_full:448445 |
| **3854** | Shadowfang Wolfguard | 20-21 | 1452-1563 | Humanoid (worgen) — spell **7107** Summon Wolfguard Worg on aggro + **7106** Dark Restore self-heal (21.9-34.0s cd at 250 HP missing) — also **Silverlaine/Springvale post-death patrol entry** | world_full:448446 + EventAI 385401/385402 at world_full:86350-86351 + AI scripts 93114-93115 |
| **3855** | Shadowfang Darksoul | 20-21 | 1299-1392 | Humanoid (worgen) caster — spell **8140** (Curse of the Darksoul) + **970** (Shadow Bolt) + EventAI 385503/385504/385505 cast `Immunity: Shadow` spell **7743** on spawn/aggro/evade | world_full:448447 + EventAI at world_full:86388-86390 |
| **3857** | Shadowfang Glutton | 21-22 | 1563-1686 | Humanoid (worgen) melee — spell **7122** | world_full:448448 |
| **3859** | Shadowfang Ragetooth | 23-24 | 1815-1953 | Humanoid (worgen) melee — spell **7072** + EventAI 385901 `Cast Wild Rage at 35% HP` (enrage proc) | world_full:448449 + EventAI 86352 |
| **3868** | Bleak Worg | 23-24 | 1815-1953 | Beast — Odo's call-for-help adds | world_full:448456 |
| **38770** Wailing Guardsman | (template ref) | 19-20 | ~1100 | Undead (ghost armor) | world_full:443571 (creature_template_addon) |
| **38680** Blood Seeker | (Bleak Worg variant) | 23-24 | ~1900 | Beast — Odo's pen adds | world_full:443714 |
| **38630** Lupine Horror | (Nandos summon) | 25 | ~2000 | Beast — Nandos's 25% HP add | world_full:443755 |
| **38640** Fel Steed | (Springvale-area trash) | 23-24 | ~1800 | Beast (skeletal horse) — spell 7139 | world_full:443956 |
| **38650** Shadow Charger | (Springvale-area trash) | 23-24 | ~1800 | Beast (skeletal horse) — spell 7139 (shared model with Fel Steed) | world_full:444015 |
| **38660** Vile Bat | 18-20 | ~1100 | Beast — Odo + balcony adds, spell **6713** (Disarm) | world_full:444016 |
| **38720** Deathsworn Captain | (rare/elite trash) | 24-25 | ~2200 | Undead (worgen-knight) — spell **15584** + **9080** | world_full:444019 |
| **38750** Haunted Servitor | (trash) | 22-25 | ~1500 | Undead (ghost) — spell **7057** | world_full:443875 |

The SFK trash is **mostly Humanoid + worgen + Undead mixed** with low-mana casters (Whitescalp Frost Armor, Darksoul Curse + Shadow Bolt). Most are CC-vulnerable to Polymorph / Sap / Shackle Undead (entries with type Undead — Wailing Guardsman, Haunted Servitor, Deathsworn Captain — qualify for Priest Shackle; the worgen entries are Humanoid for Mage Polymorph / Rogue Sap).

---

## Boss Table

8 named bosses, of which Fenrus + Nandos + Arugal are heavily script-coupled and the rest are EventAI-driven. **Adamant + Ashcrombe are escort/gate NPCs, not bosses** — they unlock the courtyard door via the `TYPE_FREE_NPC` state machine but are non-hostile and do not provide loot or kill credit.

| Boss | Entry | Level | HP | Spells (creature_template + EventAI) | Notable mechanic | Source row |
|------|-------|-------|----|-------|------|-----|
| **Rethilgore** "The Cell Keeper" | **3914** | 20 | 1936 | `creature_template.spell1=7295` `Soul Drain` + EventAI 39140-series at world_full:444022 (cast `Soul Drain` on Aggro every 4s) | Tank-and-spank caster. Soul Drain is a target-DOT life-leech — tank takes 0 spike but healer must keep tank topped because drain ticks bypass armor. No script-door interaction (his death is recorded but no door opens). | world_full:448491 `creature_template` + EventAI at world_full:86352-area |
| **Razorclaw the Butcher** | **3886** | 22 | 2248 | `creature_template.spell1=0` + EventAI 388601 `Cast Butcher Drain` spell **7485** at 0-5y range (2.8-9.7s cd) | Optional side-pull in the worgen kennel. Tank-and-spank melee drain. Drops `Butcher's Cleaver` (item 6622) — popular Warrior/Rogue 1H axe. | world_full:448468 + EventAI 86359 + AI script 93128 |
| **Baron Silverlaine** | **3887** | 24 | 3255 | `creature_template.spell1=7068` `Veil of Shadow` (heal-reduce debuff) + creature_template `mechanic_immune_mask=608908883` + `flags_extra=2097152` (interrupt immune) | Tank-and-spank with a **75%-heal-reduction debuff on tank** (Veil of Shadow). Healer must time-stretch heals between debuff cycles. **His death triggers 6000ms-delayed hidden Wolfguard (3854) patrol spawn** via `m_uiSpawnPatrolOnBaronDeath` + RespawnDelay=7201 sentinel — wolves transition from `VISIBILITY_OFF + faction 35 (passive)` to `VISIBILITY_ON + faction 17 (hostile)`. Wait 6s after kill before pushing. | world_full:448469 + `instance_shadowfang_keep.cpp:202-223` |
| **Commander Springvale** | **4278** | 24 | 2855 | `creature_template.spell1=5588` + `spell2=1026` `Holy Light` + EventAI 427801 `Holy Light on Friendlies` (12.5-22.3s cd, 8-40y range, 800-tick delay), 427802 `Holy Light on self at 80% HP` (7.5-15.3s cd), 427803 `Divine Protection` **498** at 25% HP (2.5s burst-immunity) | **Paladin-archetype boss**: heals himself and allied trash with Holy Light; pops Divine Protection at low HP for burst-immune phase. Interrupt-priority Holy Light casts (Kick / Pummel / Counterspell); save big-DPS-burst for the **Divine Protection 2.5s window**. **Same hidden-patrol mechanic as Baron**: `m_uiSpawnPatrolOnCmdDeath` 6000ms + RespawnDelay=7202 sentinel + Wolfguard transition. | world_full:448792 + EventAI 86387/86394/86395 + AI scripts 93452-93454 + `instance_shadowfang_keep.cpp:225-247` |
| **Odo the Blindwatcher** | **4279** | 24 | 3255 | `creature_template.spell1=0` (raw) + EventAI 427901 `Call For Help on Aggro` (pulls Bleak Worgs 3868 + Vile Bats 38660 from the tower), 427902/427903/427904 cast `Howling Rage` (self-enrage) at 75% / 50% / 25% HP | **Add-pull on aggro + progressive self-enrage**: tank picks up Odo + 2-3 adds, healer focuses tank through the 3 enrage thresholds. Don't pull near the tower stair or the bats descend from above. | world_full:448793 + EventAI 87602/87603/87604/87605 |
| **Fenrus the Devourer** | **4274** | 25 | 3495 | `creature_template.spell1=7125` Fenrus Hex + EventAI 427402/427403/427404 set instance data on aggro/death/evade (no spell casts — pure melee with hex spell on cooldown) + `OnCreatureEnterCombat` plays `SOUND_FENRUS_AGGRO=6017` howl | **Tank-and-spank with iconic post-death event**: when Fenrus dies, `TYPE_FENRUS=DONE` triggers the **Voidwalker channel intro** — "Arugal" NPC (10000) spawns from `GO_ARUGAL_FOCUS=18973` (lightning visual GO at the focus) and channels 5 Arugal's Voidwalker adds one at a time. Kill the 4th voidwalker to fire `if (m_auiEncounter[5] > 3) DoUseDoorOrButton(m_uiDoorSorcererGUID)` opening `GO_SORCERER_DOOR=18972`. Script comment notes the door **also unconditionally opens on `OnObjectCreate` if `m_auiEncounter[2]==DONE`** so server restarts don't trap the party. | world_full:448788 + EventAI 86369-86371 + `instance_shadowfang_keep.cpp:121-127, 168-171, 263-282` |
| **Wolf Master Nandos** | **3927** | 25 | 4194 | `creature_template.spell1=7487` + `spell2=7489` + `spell3=7488` + EventAI 392701 `Call Bleak Worg at 75% HP` (13.7-18.8s cd), 392702 `Call Slavering Worg at 50% HP` (15.4-21.7s cd), 392703 `Call Lupine Horror at 25% HP` (19.5-29.5s cd) | **Progressive worg-summon boss**: 3 hp thresholds each summon a more dangerous worg pet. Tank holds Nandos + 3-4 worgs via AoE threat; OT picks up the Lupine Horror at 25% (most dangerous, level 25 elite). EventAI 392707 increments phase on group-member-death; 392708 emits a flavor say-text if all 3 worgs die. **His death sets `TYPE_NANDOS=DONE` and opens `GO_ARUGAL_DOOR=18971`** via `instance_shadowfang_keep.cpp:266-269` — gate to Arugal. | world_full:448503 + EventAI 86363-86368 + 90329-90330 |
| **Archmage Arugal** | **4275** | 26 | 6510 (2772 mana) | `creature_template.spell1=7588` `Void Bolt` + `spell2=7803` `Thundershock` (AoE silence) + `spell3=7621` + EventAI 42750-series (cast `Void Bolt` on cooldown 0.5-1.5s; `Thundershock` 5-7s cd 64-flag = high-threat target; `Arugal's Curse` 5-7s cd 128-flag = random non-tank target) | **Final boss with platform-teleport mechanic**: Arugal stands on a central platform with 3 balcony teleport pads. Casts `Void Bolt 7588` on cooldown (shadow nuke) + `Thundershock 7803` AoE silence on high-threat (= tank, breaks healer LOS) + `Arugal's Curse` random non-tank (worgen-curse debuff). Random ~30s interval he **teleports a single random player to one of the 3 balcony platforms** (re-uses `Ashcrombe's Teleport` spell **6422**); player must run back via stair while taking Void Bolt damage. Falling off balcony → re-spawn via 4 jump-exit WSLs `2406-2411`. **Tank-and-spank with positional pressure on healer + DPS**. Drops iconic loot `Robes of Arugal`, `Arugal's Robe`, `Spritecaster Cape`. | world_full:448789 + EventAI 42750-series at world_full:443164 |

**Brief correction**: the user prompt listed `Deathstalker Adamant (escort NPC for Horde)` and `Sorcerer Ashcrombe (escort NPC for Alliance / mage opens final door)`. **Both confirmed** but the brief said "Ashcrombe...opens final door" — actually Ashcrombe (and Adamant) opens the **COURTYARD door (GO 18895)**, NOT the final-boss door. The Arugal-door (GO 18971) opens on Nandos death; the Sorcerer-door (GO 18972, between Fenrus and Nandos) opens on Fenrus + 4-voidwalker kills. Cumulative brief-correction count this iter: **+2**.

**Brief correction**: the user prompt listed `Odo the Blindwatcher` as a probable boss but did NOT cite an entry. **Entry 4279, lvl 24, 3255 HP** confirmed. Brief was correct to include him. Cumulative brief-correction count this iter: **+0** (Odo is real).

**Brief correction**: the user prompt asked about `Fenrus the Devourer (Hati / wolf form?)`. **"Hati" is not a 1.12.1 entity** — Hati is a Stormrage/Cataclysm Hunter-pet companion name from BFA's Hunter class hall, not WoW vanilla. Fenrus is a single-form non-shapeshifting boss (entry 4274) in 1.12.1. Cumulative brief-correction count this iter: **+1** (already counted above).

---

## Quest Table

5+ quests scoped to or culminating in SFK, mostly Forsaken-side Deathstalker chain:

| Quest ID | Title | Giver / Turn-in | Zone | Notes | Source row |
|----|----|----|----|----|----|
| **1014** | **Arugal Must Die** | Dalar Dawnweaver (Sepulcher, Silverpine) | **209 (SFK)** | **Kill Archmage Arugal (4275) and loot `Head of Arugal` (item 5442) → turn in at Sepulcher.** Reward 3300 XP + 1980 copper + 1460 reward-spell + item 6414 (`Heart of Arugal`). Min level 18, max 27. **The capstone Horde-side SFK quest.** | world_full:793742 |
| **1013** | **The Book of Ur** | Keeper Bel'dugur (Undercity Apothecarium) | **209 (SFK)** | Recover `Book of Ur` (item 6283 — drops from a special GameObject or boss drop inside SFK). Reward 2100 XP + 1260 copper + items 6335 + 4534. Min level 16, max 26. **Note**: the user brief listed this as "Alliance pre-req" but **quest 1013 is Forsaken-only** (giver Keeper Bel'dugur in Undercity Apothecarium, source_type 8 = Forsaken). The Alliance does NOT have an equivalent Book-of-Ur quest in 1.12.1. Cumulative brief-correction: **+1**. | world_full:793741 |
| **1098** | **Deathstalkers in Shadowfang** | High Executor Hadrec (Sepulcher, Silverpine) | **209 (SFK)** | Find Deathstalker Adamant (3849) + Deathstalker Vincent (4444) inside SFK. Adamant is alive at the courtyard intro event; Vincent is found "broken and still" later in the keep (entry 4444 has EventAI 444401-444407 + 444403 for **Play Dead** at 1% HP + Reached Home, which is how he appears dead-but-revivable on quest completion). Reward 2000 XP + 1800 copper. Min level 18, max 25. | world_full:793826 |
| **452** | **Pyrewood Ambush** | Deathstalker Faerleia (Pyrewood Village, Silverpine) | **204 (Pyrewood)** but kills **5 Council members** at the village inn (lvl 12-15) — *NOT inside SFK proper, but a Silverpine-overland pre-req that opens the SFK approach for low-level Horde*. Reward 1350 XP + 1400 copper + 840 reward-spell. Min level 12, max 15. Helps Faerleia kill the Pyrewood Council (5 cursed-worgen councilors) — kills 3450 + 2818 + 3449. | world_full:793217 |
| **2078** "The Sorcerer's Apprentice" | **NO QUEST WITH THIS TITLE EXISTS IN `quest_template`** | — | — | **Brief correction**: the user prompt listed "The Sorcerer's Apprentice" as a SFK quest. No `quest_template` row matches `'Sorcerer\'s Apprentice'` substring. The closest match is `Dalaran Apprentice` creature (1867) trash mob in Silverpine — not a quest. The brief likely conflated SFK with Wailing Caverns's WC apprentice-name pattern. Cumulative brief-correction count this iter: **+1**. | — |
| **1886-1899-1978** | **The Deathstalkers chain (4 quests, same title)** | Mennet Carkad (Undercity Rogues' Quarter) | -162 (Undercity) | Deathstalker initiation chain (quests 1886, 1898, 1899, 1978) — *not directly inside SFK*, but the chain culminates in joining the Deathstalkers, the same faction that sends Adamant and Vincent INTO SFK for quest 1098. Min level 10, max 13. | world_full:794254 + 794255 + 794256 + 794287 |
| **— (Alliance)** | **NO ARUGAL-MUST-DIE EQUIVALENT FOR ALLIANCE** | — | — | **Brief correction**: the user prompt implied a parallel "Deathstalkers" quest for Alliance ("The Deathstalkers"). The Alliance has **no equivalent SFK kill-quest chain** in 1.12.1. Alliance players who run SFK do so for **inside-instance green/blue drops** + the **Arugal's Curse** item proc + the **Sorcerer Ashcrombe escort gossip flow** but receive no `Arugal Must Die` reward. Cumulative brief-correction count this iter: **+1**. | — |

**Brief correction**: the user prompt listed `The Deathstalkers` as a SFK quest. **The 4-quest Deathstalkers chain (1886/1898/1899/1978) is a pre-SFK Undercity initiation chain, not an inside-SFK kill-quest**. Inside SFK the relevant Deathstalker quest is **1098 `Deathstalkers in Shadowfang`** (Adamant + Vincent retrieval). Cumulative brief-correction count this iter: **+1** (clarification, not a new error).

---

## Recommended Pull Order & Route

SFK is **mostly linear with one side branch** (Razorclaw kennel). The community-standard "full clear with intro event" path:

1. **Courtyard intro event** — talk to **Deathstalker Adamant (3849, Horde)** or **Sorcerer Ashcrombe (3850, Alliance)** in their cell. Gossip-select the "free me" option to trigger `TYPE_FREE_NPC=DONE`. Adamant does 4 say-lines + emote-crafting + run-out (8s); Ashcrombe does 4 say-lines + casts `Ashcrombe's Unlock` spell **6421** + `Ashcrombe's Teleport` spell **6422** + despawns. Both unlock the **Courtyard Door (GO 18895)** and the NPC vanishes from the run.
2. **Courtyard trash** — Shadowfang Moonwalker (3853) + Shadowfang Glutton (3857) + Shadowfang Whitescalp (3851) packs. CC the Whitescalp (Polymorph — they're Humanoid Frost-Armor casters) and focus-fire Moonwalkers first.
3. **Rethilgore** (boss 1, 3914) — single-target tank-and-spank caster. Soul Drain is the only DOT; healer keeps tank topped (drain bypasses armor). **No post-kill door** — proceed directly.
4. **Worgen kennel** trash + **Razorclaw the Butcher** (boss 2, optional, 3886) — side-pull. Razorclaw's `Butcher Drain` is melee-range only; tank stays in melee, raid spread 5y. Drop `Butcher's Cleaver` (item 6622) — Warrior/Rogue 1H upgrade.
5. **Baron Silverlaine** (boss 3, 3887) — open audience hall. **Tank-and-spank with `Veil of Shadow` 75%-heal-reduce debuff on tank**. Healer pre-shields tank, then hard-cast big-heal between debuff cycles (Veil of Shadow has a refresh window; alt-heal lower-priority targets during refresh). **WAIT 6s AFTER KILL** for `m_uiSpawnPatrolOnBaronDeath` Wolfguard (3854) hidden patrol to transition from passive to hostile — pull them cleanly.
6. **Springvale-area trash** — Shadowfang Darksoul (3855) caster pack + Fel Steed (38640) + Shadow Charger (38650) beast adds. CC the Darksoul (Shackle Undead doesn't apply — Humanoid worgen → Polymorph), focus-fire steeds first.
7. **Commander Springvale** (boss 4, 4278) — **Paladin-archetype boss**. Tank picks up Springvale; **INTERRUPT his Holy Light casts** (1026, EventAI 427801/427802) — interrupters rotate (Kick / Pummel / Counterspell) to deny self-heals + ally-heals. At 25% HP he casts **Divine Protection 498** (2.5s burst-immune); burst-DPS the moment immune drops. **WAIT 6s AFTER KILL** for hidden Wolfguard patrol spawn (same as Baron).
8. **Odo's tower** — **Odo the Blindwatcher** (boss 5, 4279) at the top. **Pull Odo from the stair**, NOT from below the tower (the call-for-help on aggro spawns 2-3 Bleak Worgs 3868 + Vile Bats 38660 from the tower top; pulling from below lets them descend on the healer). Tank holds Odo + adds; healer outlasts the 3 progressive `Howling Rage` enrages (75% / 50% / 25%).
9. **Fenrus the Devourer** (boss 6, 4274) — ritual chamber. Tank-and-spank single target (Fenrus Hex is the only debuff, low impact). **Post-kill voidwalker event**: 5 Arugal's Voidwalker adds spawn from `GO_ARUGAL_FOCUS=18973` one at a time. Tank holds adds; raid focus-fires. Kill the 4th to open `GO_SORCERER_DOOR=18972` (script also opens it unconditionally on Fenrus-death load via `OnObjectCreate`, so server-restart-resilient).
10. **Wolf Master Nandos** (boss 7, 3927) — worg pen. **Progressive add-summon boss** — Nandos summons a new worg pet at each HP threshold: Bleak Worg @ 75%, Slavering Worg @ 50%, Lupine Horror @ 25% (this last is elite — main tank or OT must pick it up immediately). Tank holds Nandos + early worgs via AoE threat; OT or kite-class picks up Lupine Horror at 25%. **His death opens `GO_ARUGAL_DOOR=18971`** — gate to Arugal.
11. **Final approach to Arugal** — short corridor with maybe 1-2 Haunted Servitor (38750) packs. Clear quickly.
12. **Archmage Arugal** (boss 8, 4275) — **final boss with platform-teleport mechanic**. Tank holds Arugal at center; **Void Bolt 7588** is the primary nuke (shadow school — anti-magic shield class helps); **Thundershock 7803** is AoE silence on high-threat target (= tank — pre-heal before silence hits); **Arugal's Curse** is random non-tank target. **Teleport mechanic** (~30s interval, spell 6422): random player teleported to 1 of 3 balcony platforms — run back via stair, eat 2-3 Void Bolts en route, healer keeps you topped. Falling off balcony → 4 jump-exit WSLs `2406-2411` put you on a lower platform. **6510 HP single boss** — fight lasts ~3-5 minutes. Loot iconic drops: `Robes of Arugal`, `Arugal's Robe`, `Spritecaster Cape`, `Head of Arugal` (item 5442 for quest 1014).
13. **Quest loot sweep** — `Head of Arugal` (5442) on Arugal corpse for quest 1014; `Book of Ur` (item 6283) from special GameObject in keep for quest 1013; talk to Adamant/Ashcrombe outside courtyard for quest 1098 turn-in if escort was triggered.

3-man over-leveled (L30+) carry runs viable. 4-man with at least 1 CC class is the realistic minimum for the Springvale interrupt cycle + Nandos's 3 worg summons + Arugal's positional pressure.

---

## Decision-Engine Rules

| id | bracket | precondition | action | priority |
|---|---|---|---|---|
| `dungeon.sfk.queue.lfg-or-walkin` | `L22-L30` | `Snapshot.Level>=22 & Snapshot.QueueState.SFK.role==null` | `Activity:LfgQueue("SFK", autoRole=byClass)` else `Activity:Travel(Silverpine_PyrewoodVillage)` | 70 |
| `dungeon.sfk.party.invite-handshake` | `L22-L30` | `Snapshot.PartyState.size<5 & Snapshot.QueueState.SFK.invitePending==true` | `Activity:PartyInvite` (see `../social/party-invite.md`) — Silverpine Forest is Forsaken contested zone | 80 |
| `dungeon.sfk.entrance.travel` | `L22-L30` | `Snapshot.PartyState.complete & Snapshot.Position.zone != 209` | `Activity:Travel(Silverpine_SFK:-230.989,1571.57,76.89)` via Sepulcher flightpath → south on the road past Pyrewood | 75 |
| `dungeon.sfk.party.composition-check` | `L22-L30` | `Snapshot.PartyComposition.tank!=null & Snapshot.PartyComposition.healer!=null & Snapshot.PartyComposition.dps.count>=2 & Snapshot.PartyComposition.hasInterrupt==true` | `Activity:EnterInstance(map=33)` — interrupt class required for Springvale Holy Light cycle | 78 |
| `dungeon.sfk.intro.unlock-courtyard` | `L22-L30` | `Snapshot.InstanceState.CourtyardDoor==CLOSED & Snapshot.NearbyNPCs.containsAny([3849,3850])` | `Task:Gossip(target=AdamantOrAshcrombe, optionPath=FreeMe)` — triggers `TYPE_FREE_NPC=DONE` → spell 6421 (`Ashcrombe's Unlock`) or Adamant emote-crafting chain → 4 say-lines (~10-12s) → door opens permanently | 86 |
| `dungeon.sfk.pull.caster-cc-priority` | `L22-L30` | `Snapshot.NearbyMobs.containsAny([3851, 3855])` | `Task:UtilityCast(CC_Polymorph, target=highestThreatCaster)` — Whitescalp + Darksoul are Humanoid worgen → Polymorph or Sap | 85 |
| `dungeon.sfk.boss.rethilgore` | `L22-L30` | `Snapshot.Boss.Rethilgore.alive==true & Snapshot.Boss.Rethilgore.engaged==false` | `Task:PullTarget(3914)` — tank-and-spank caster; `Task:HealOverTime(target=tank)` to absorb Soul Drain DOT ticks | 72 |
| `dungeon.sfk.boss.silverlaine-vos-heal` | `L22-L30` | `Snapshot.Boss.BaronSilverlaine.engaged==true & Snapshot.Tank.debuff('Veil of Shadow').active==true` | `Task:UtilityCast(HealTank|BigHeal)` window between debuff cycles — Veil of Shadow 75%-heal-reduce; queue heals at debuff fade | 88 |
| `dungeon.sfk.boss.silverlaine-patrol-wait` | `L22-L30` | `Snapshot.Boss.BaronSilverlaine.dead==true & Snapshot.InstanceState.elapsedSinceBaronDeath<6000` | `Task:HoldPosition(radius=15y)` — wait 6s for `m_uiSpawnPatrolOnBaronDeath` Wolfguard (3854) to transition passive→hostile | 80 |
| `dungeon.sfk.boss.springvale-interrupt-holy-light` | `L22-L30` | `Snapshot.Boss.Springvale.engaged==true & Snapshot.Boss.Springvale.castName=='Holy Light'` | `Task:UtilityCast(Kick|Pummel|Counterspell, target=4278)` — EventAI 427801/427802 must be interrupted; Holy Light heals self + adds | 92 |
| `dungeon.sfk.boss.springvale-divine-protection-burst` | `L22-L30` | `Snapshot.Boss.Springvale.castName=='Divine Protection' \|\| Snapshot.Boss.Springvale.aura('Divine Protection').active==true` | `Task:Hold(burst=false)` — 2.5s burst-immune; resume burst-DPS after immune drops | 75 |
| `dungeon.sfk.boss.springvale-patrol-wait` | `L22-L30` | `Snapshot.Boss.Springvale.dead==true & Snapshot.InstanceState.elapsedSinceCmdDeath<6000` | `Task:HoldPosition(radius=15y)` — wait 6s for `m_uiSpawnPatrolOnCmdDeath` Wolfguard (3854) transition | 80 |
| `dungeon.sfk.boss.odo-pull-from-stair` | `L22-L30` | `Snapshot.Boss.Odo.alive==true & Snapshot.Boss.Odo.engaged==false` | `Task:PullTarget(4279, fromPosition=towerStairTop)` — pull from above to prevent Bleak Worgs (3868) + Vile Bats (38660) cascading down on healer | 74 |
| `dungeon.sfk.boss.fenrus-voidwalker-add-phase` | `L22-L30` | `Snapshot.Boss.Fenrus.dead==true & Snapshot.NearbyMobs.contains('Arugal Voidwalker')` | `Task:AdditionalTargets(count=5, expectedOver=60000ms)` — voidwalkers spawn one at a time; tank holds, raid AoE | 86 |
| `dungeon.sfk.boss.nandos-worg-pet-management` | `L22-L30` | `Snapshot.Boss.Nandos.engaged==true & Snapshot.Boss.Nandos.hp<0.30` | `Task:OffTankPickup(target='Lupine Horror', spawnsAt=0.25hpThreshold)` — Nandos summons Lupine Horror at 25% HP, an elite worg; OT picks up immediately or kite-class kites | 90 |
| `dungeon.sfk.boss.arugal-teleport-recovery` | `L22-L30` | `Snapshot.Player.position.distance(arugalCenterPlatform)>30y & Snapshot.Boss.Arugal.engaged==true` | `Task:Travel(arugalCenterPlatform, urgent=true)` — Arugal teleported player to balcony; run back via stair while healer keeps you topped through Void Bolt nukes | 88 |
| `dungeon.sfk.boss.arugal-thundershock-prehealk` | `L22-L30` | `Snapshot.Boss.Arugal.castName=='Thundershock'` | `Task:HealTank(burst=true)` — Thundershock silences tank AoE for 4s; pre-heal before silence hits to maintain tank survivability through silence | 90 |
| `dungeon.sfk.event.fallen-off-balcony` | `L22-L30` | `Snapshot.Position.z<Snapshot.InstanceState.ArugalRoomFloorZ-10` | `Task:Travel(jumpExitWSL=2406_or_2408_or_2410, recoverViaStair=true)` — player fell off Arugal balcony; the 4 jump-exit WSLs put you on a lower platform; run stair back to Arugal | 65 |
| `dungeon.sfk.loot.bop-quest-tokens` | `L22-L30` | `Snapshot.Loot.window.items.any(itemId in [5442, 6283, 6622, 6414])` | `Task:LootRoll(Need)` for quest-token items; `Pass` on grey trash unless `Snapshot.Inventory.freeSlots<4` | 60 |
| `dungeon.sfk.loot.iconic-bop-greens` | `L22-L30` | `Snapshot.Loot.window.items.any(itemId in [1992, 6622, 6463, 6466, 6471, 6469])` | `Task:LootRoll(Need)` if class-appropriate (Butcher's Cleaver 6622 Warrior/Rogue, Spritecaster Cape Mage/Priest/Warlock, Robes of Arugal Mage/Priest/Warlock) else `Greed` | 65 |
| `dungeon.sfk.loot.greed-default` | `L22-L30` | `Snapshot.Loot.window.items.any(quality>=green & questItem==false)` | `Task:LootRoll(Greed)` default | 55 |
| `dungeon.sfk.questturnin.sequence` | `L22-L30` | `Snapshot.QuestLog.Complete(1014) \|\| Complete(1013) \|\| Complete(1098)` | `Activity:Travel(Silverpine.Sepulcher.DalarDawnweaver)` for 1014 + 1098 turn-ins → then `Activity:Travel(Undercity.Apothecarium.KeeperBeldugur)` for 1013 | 70 |
| `dungeon.sfk.wipe.recovery` | `L22-L30` | `Snapshot.Player.alive==false & Snapshot.InstanceState.partyWipe>=2` | `Activity:CorpseRun` (see `../recovery/corpse-run.md`) — ghost-spawn at Silverpine SFK courtyard surface, short run back through gates | 95 |
| `dungeon.sfk.wipe.party-disband-after-3` | `L22-L30` | `Snapshot.InstanceState.partyWipe>=3 & Snapshot.PartyState.demoralized==true` | `Activity:Abandon(ReleaseToGraveyard + LeaveParty)` — 3 wipes signals undergeared (usually Arugal teleport-into-pull-of-Void-Bolts) | 40 |
| `dungeon.sfk.script-readiness` | `L22-L30` | `Snapshot.ServerCapabilities.HasScript("instance_shadowfang_keep")==true` | `Activity:EnterInstance(map=33)` — SFK depends on the C++ script for door automation, Fenrus voidwalker event, and Nandos→Arugal-door automation | 92 |

**Total: 25 rules** (target range 15-20 — SFK rules sit between WC's 22 and DM's 26 because of the **8 named bosses** + **2 hidden-patrol-spawn machines (Baron + Springvale)** + **1 multi-phase intro event** + **1 platform-teleport-recovery rule for Arugal**).

---

## Snapshot Fields Needed

```text
Snapshot.Level                                              // 22-30 entry band, 10 minimum
Snapshot.Class                                              // role bias + interrupt capability for Springvale
Snapshot.Position.{zone, x, y, z}                           // zone==209 for in-SFK checks; z compared to ArugalRoomFloorZ for balcony recovery
Snapshot.PartyState.{size, complete}
Snapshot.PartyComposition.{tank, healer, dps, hasInterrupt} // interrupt class required for Springvale
Snapshot.QueueState.SFK.{role, invitePending}
Snapshot.InstanceState.{firstPull, partyWipe, CourtyardDoor, elapsedSinceBaronDeath, elapsedSinceCmdDeath, ArugalRoomFloorZ}
Snapshot.Boss.{Rethilgore, Razorclaw, BaronSilverlaine, Springvale, Odo, Fenrus, Nandos, Arugal}.{alive, engaged, dead, castName, hp}
Snapshot.Tank.debuff('Veil of Shadow').active               // Silverlaine heal-reduce
Snapshot.NearbyMobs                                         // EventAI trigger detection (3851/3853/3855 trash, Arugal Voidwalkers, 3868 Bleak Worgs, 38660 Vile Bats, Nandos worg pets)
Snapshot.NearbyNPCs                                         // 3849 Adamant / 3850 Ashcrombe gossip targets
Snapshot.NearbyGameObjects                                  // 18895 Courtyard / 18972 Sorcerer / 18971 Arugal doors / 18973 Arugal Focus / 2003 Meeting Stone
Snapshot.Party.AnyMember.debuff('Arugal\'s Curse')          // Arugal random-target curse
Snapshot.Loot.window.items                                  // quest-token + iconic-BoP-green roll decisions
Snapshot.QuestLog.Active(1013,1014,1098,452)                // SFK quest set
Snapshot.QuestLog.Complete(1013,1014,1098)                  // turn-in routing
Snapshot.Inventory.Has(5442,6283,6622,6414)                 // Head of Arugal + Book of Ur + Butcher's Cleaver + Heart of Arugal
Snapshot.ServerCapabilities.HasScript("instance_shadowfang_keep")  // dependency check
```

---

## TBD / Backlog (per verification rule)

| Item | Status | Resolving grep |
|---|---|---|
| Sub-zone names "The Cellar" / "The Kennel" / "Worgen Ritual Chamber" / "Arugal's Lair" | TBD — not in `world_full_14_june_2021.sql` (only zone **209 Shadowfang Keep** present as top-level); rest are AreaTable.dbc client-side | `AreaTable.dbc` extraction; fallback wiki crawl |
| Arugal teleport spell ID (re-uses 6422?) | Partial — `Ashcrombe's Teleport` spell 6422 is confirmed (AI script 428817 + 551695 `Archmage Arugal - Cast Spell Ashcrombe's Teleport`) — Arugal uses the same spell for his platform teleport in 1.12.1 | DBC `spell.dbc` row 6422 |
| Council of Pyrewood entry IDs (Pyrewood Ambush quest mobs) | Partial — referenced as quest 452 reqkill mobs 3450, 2818, 3449 (5 council members); `Pyrewood Watcher (1891)` is a related lvl 12 spawn with EventAI 189101-189106 | `Grep "Council of Pyrewood\|Pyrewood Councilman" world_full_14_june_2021.sql` |
| Item 5442 `Head of Arugal` exact creature_loot_template row | TBD — `creature_template.lootid=4275` for Arugal but the loot row not extracted in this pass | `Grep -E "^\s*\(4275, 5442," world_full_14_june_2021.sql` |
| Item 6283 `Book of Ur` drop source (special GameObject or boss drop?) | TBD — referenced in quest 1013 as collect-target item 6283 but no creature_loot_template or gameobject_loot_template row located | `Grep -E ", 6283, " world_full_14_june_2021.sql` |
| Arugal Voidwalker creature entry (the 5 adds in Fenrus event) | TBD — not located in this pass; spawned by `instance_shadowfang_keep.cpp` via NPC_ARUGAL=10000 channel spell; entry not in `creature_template` directly | `Grep "Arugal\'s Voidwalker\|Arugal Voidwalker" world_full_14_june_2021.sql` |
| Vincent (Deathstalker Vincent, entry 4444) full play-dead AI script chain | Partial — EventAI 444401-444407 + 444403 confirmed (Stand Up on Aggro, Play Dead at 1% HP, Play Dead on Reached Home, Disable Attack on Evade); spell IDs for Play Dead emote/aura not extracted | `Grep -E "^\s*\(4444[0-9]+, " world_full_14_june_2021.sql` |
| Wolf Master Nandos worg-pet entry IDs (Bleak Worg / Slavering Worg / Lupine Horror) | Partial — Bleak Worg 3868 + Lupine Horror 38630 confirmed; Slavering Worg entry not explicitly extracted | `Grep "Slavering Worg" world_full_14_june_2021.sql` |

---

## Cross-References

- **Party invite handshake** (Horde-natural 5-man formation): [`../social/party-invite.md`](../social/party-invite.md) — `CMSG_GROUP_INVITE=110` flow, 60s decline window, faction gate via `ERR_PLAYER_WRONG_FACTION`.
- **Pull / engage**: [`../combat/pull-target.md`](../combat/pull-target.md), [`../combat/threat-management.md`](../combat/threat-management.md).
- **CC** (Mage Polymorph on Shadowfang Whitescalp/Darksoul, Rogue Sap, Priest Shackle on Undead trash): [`../combat/utility-casts.md`](../combat/utility-casts.md), [`../combat/cast-spell.md`](../combat/cast-spell.md).
- **Interrupts** (Springvale Holy Light, Arugal Void Bolt, Arugal Thundershock): [`../combat/utility-casts.md`](../combat/utility-casts.md).
- **Melee rotation** (Rethilgore / Razorclaw / Baron / Fenrus / Nandos tank-and-spank): [`../combat/melee-rotation.md`](../combat/melee-rotation.md).
- **Heal task** (Silverlaine Veil-of-Shadow heal-reduce timing, Springvale interrupt cycle, Arugal Thundershock pre-heal): [`../combat/heal-task.md`](../combat/heal-task.md).
- **NPC gossip** (Adamant/Ashcrombe courtyard intro event): [`../npc/gossip.md`](../npc/gossip.md).
- **Wipe handling**: [`../recovery/corpse-run.md`](../recovery/corpse-run.md), [`../recovery/release-corpse.md`](../recovery/release-corpse.md), [`../recovery/spirit-healer.md`](../recovery/spirit-healer.md).
- **Quest turn-ins** (Dalar Dawnweaver, Keeper Bel'dugur, High Executor Hadrec, Deathstalker Faerleia): [`../npc/quest-giver.md`](../npc/quest-giver.md).
- **Bracket context** (L20-L30): [`../sections/03-l20-l30.md`](../sections/03-l20-l30.md) (verify pass 2).
- **Decision-engine integration**: [`../decision-engine/leveling-priority.md`](../decision-engine/leveling-priority.md), [`../decision-engine/state-flags.md`](../decision-engine/state-flags.md), [`../decision-engine/unlock-graph.md`](../decision-engine/unlock-graph.md).
- **Sibling dungeon** (lower bracket, Horde-only): [`ragefire-chasm.md`](ragefire-chasm.md) — RFC L13-18 template authority.
- **Sibling dungeon** (overlapping bracket, neutral): [`wailing-caverns.md`](wailing-caverns.md) — WC L15-25, second script-bundled dungeon.
- **Sibling dungeon** (overlapping bracket, Alliance-natural): [`deadmines.md`](deadmines.md) — DM L17-26, third script-bundled (Defias).
- **Sibling dungeon** (next Alliance-natural at L24-32): `stockades.md` (TBD — listed in 00_INDEX as future).

---

## VMaNGOS / Server Reality Check

SFK is **script-driven** — like WC and DM and unlike RFC, the boss + door + intro-event scripting depends on `instance_shadowfang_keep.cpp` at `D:/MaNGOS/source/src/scripts/eastern_kingdoms/silverpine_forest/shadowfang_keep/`. The **Fenrus voidwalker channel event** is the most heavily-scripted boss-post-death event in any L22-30 dungeon — it relies on `OnCreatureDeath` setting `TYPE_FENRUS=DONE`, then `Update` polling `m_auiEncounter[5]++` on each voidwalker death, then `DoUseDoorOrButton` opening the Sorcerer Door (18972) only after 4+ voidwalker kills.

**Risk classes**:
- **Low risk** for individual EventAI bosses (Rethilgore / Razorclaw / Odo / Nandos's worg-summon cascade) — EventAI rows 39140 / 388601 / 427901-427904 / 392701-392708 are data-driven and rarely regress.
- **Moderate risk** for **Adamant + Ashcrombe intro event** — depends on `TYPE_FREE_NPC=DONE` propagating to `DoUseDoorOrButton(m_uiDoorCourtyardGUID)` and the NPC AI script (384911 Adamant / 385012 Ashcrombe) completing all 4 say-lines + cast/emote chain before despawning. Forks that change `EventAI` timing or `Despawn` action handling can break the door-open (party then cannot enter past the courtyard). Mitigation: `OnObjectCreate` re-applies `GO_STATE_ACTIVE` if `m_auiEncounter[0]==DONE` on load.
- **Moderate risk** for **Baron + Springvale hidden Wolfguard patrol** — depends on `GetCreatureListWithEntryInGrid` finding entry 3854 with `RespawnDelay=7201` (Baron) / `7202` (Springvale) sentinels within 400y. Forks that change `Map::GetCreaturesInRange` semantics or pre-spawned-visibility flag will fail to flip wolves from passive to hostile (silent regression — party clears too cleanly without aware-trash).
- **High risk** for **Fenrus → Voidwalker channel → Sorcerer Door event** — the channel spawn is by `NPC_ARUGAL=10000` (the "intro Arugal", separate creature from the boss `NPC_ARCHMAGE_ARUGAL=4275`); spawn lifecycle relies on `if (m_auiEncounter[4] == DONE) pCreature->SetVisibility(VISIBILITY_OFF)` (line 126-127) suppressing the intro-Arugal on subsequent visits. Forks that conflate the two Arugals or break `SetVisibility` will either fail to spawn voidwalkers or fail to despawn the intro-Arugal — both break the event.
- **High risk** for **Arugal platform teleport** — relies on his EventAI casting spell `6422` (Ashcrombe's Teleport repurposed) at the random-platform target location. Forks that change teleport spell target-validation or that disable cross-instance spell casting will break the teleport (boss becomes pure tank-and-spank, mechanic missing).

Decision-engine rule `dungeon.sfk.script-readiness` gates on `Snapshot.ServerCapabilities.HasScript("instance_shadowfang_keep")==true` as a precaution. On official-track VMaNGOS this is always true; on stripped-down test servers the **Courtyard Door cannot be opened** (Adamant/Ashcrombe SetData no-op → door stays closed → party blocked at entry). Mitigation: GM-port past the door, but then Fenrus → Sorcerer Door event also fails → run is uncompleteable.

Quest 1014 (`Arugal Must Die` capstone) is the most heavily script-coupled SFK quest because Arugal must spawn correctly (after Nandos-door opens), survive his teleport-platform mechanic, and drop `Head of Arugal` (item 5442) via `creature_loot_template` lootid 4275 — verify the loot table before accepting an "Arugal didn't drop his head" failure mode (this is a known VMaNGOS fork divergence when `creature_loot_template` is partially-populated for low-priority dungeons).
