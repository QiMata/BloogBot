---
title: "Dungeon — Wailing Caverns (WC)"
patch: "1.12.1 (Drums of War, Sept 2006)"
sources_crawled:
  - D:/MaNGOS/sql/world_full_14_june_2021.sql (map_template / creature_template / quest_template / worldsafelocs / game_object_template / creature_ai_scripts)
  - D:/MaNGOS/sql/world_full_14_june_2021.sql:728027-728028 (map_template header)
  - D:/MaNGOS/source/src/scripts/kalimdor/the_barrens/wailing_caverns/instance_wailing_caverns.cpp
  - D:/MaNGOS/source/src/scripts/kalimdor/the_barrens/wailing_caverns/wailing_caverns.cpp
  - D:/MaNGOS/source/src/scripts/kalimdor/the_barrens/wailing_caverns/def_wailing_caverns.h
  - https://warcraft.wiki.gg/wiki/Wailing_Caverns
crawl_date: 2026-05-20
---

# Wailing Caverns (WC) — 5-Man Dungeon Guide

Second file in the dungeons/ family, authored to the `ragefire-chasm.md` template contract. WC is the **second-lowest instance in the game** and the first dungeon both factions naturally queue for. The entrance is at **Lushwater Oasis** in the **southern Barrens** (zone 17, not Northern Barrens — vanilla 1.12.1 predates Cataclysm's Northern/Southern split). The lair is a labyrinthine cavern system corrupted by **Naralex**'s shattered Emerald Dream descent: peaceful druids twisted into the **Druids of the Fang** (Cobrahn/Pythas/Anacondra/Serpentis), gigantic **deviate** wildlife (ravagers/vipers/shamblers/dreadfangs/venomwings), a 4-arm **murloc** quest-end boss (Mutanus the Devourer), and an Awakening-event culmination at Naralex's chamber. Unlike RFC, **WC has a dedicated C++ script bundle** at `D:/MaNGOS/source/src/scripts/kalimdor/the_barrens/wailing_caverns/` (`instance_wailing_caverns.cpp` + `wailing_caverns.cpp` + `def_wailing_caverns.h`), making scripting risk **moderate** (script-driven escort events can regress on VMaNGOS forks more easily than EventAI rows). 6 named bosses + 1 optional rare (Verdan the Everliving) + 1 escort culmination (Naralex via Disciple of Naralex). **Level band 15-25 optimal**.

---

## Quick Facts

| Field | Value | Source |
|-------|-------|--------|
| `map_id` | **43** | `map_template:728027` row `(43, 0, 0, 1, 718, 40, 0, 1, -751.131, -2209.24, 'Wailing Caverns', 'instance_wailing_caverns')` — `map_type=1` (5-man dungeon), `linked_zone=718`, `ghost_entrance_map=1` (Kalimdor), `script_name='instance_wailing_caverns'` (**has a dedicated C++ script — unlike RFC**) |
| Continent / Parent map | Kalimdor (map 1) | `map_template:728027 ghost_entrance_map=1` |
| Linked zone | **718** "Wailing Caverns" | `areatable.dbc` row `(718, 43, 0, 641, 0, 0, 'Wailing Caverns', 4, 0)` at world_full:1337 |
| Group size | 5-man (`player_limit=40` legacy; instance type 1 enforces 5) | `map_template.player_limit` |
| Reset delay | 0 (standard instance lockout, no scheduled reset) | `map_template.reset_delay=0` |
| Level range | **15-25** optimal (level **10 minimum** to enter) | Worldsafelocs `(228, 0, 'Wailing Caverns - Entrance', 'You must be at least level 10 to enter.', 10, 0, 43, -158.441, 131.601, -74.2552, 5.84685)` at world_full:181 |
| Faction | **Both** (neutral outdoor entrance at Lushwater Oasis, southern Barrens) | Geographic — entrance is in a contested zone (zone 17, Barrens) outside any city |
| Meeting Stone | **GameObject 2002** `'Meetingstone - Wailing Caverns'` (cluster range 36 ≈ L18-25) | `game_object_template` row at world_full:563787 `(2002, 0, 36, 718, 0, ...)` |
| Theme | Subterranean Naralex Emerald-Dream nightmare — Druids of the Fang serpent-corruption + deviate beast aberrations + murloc invader |
| Boss count | **6 + 1 optional rare + 1 escort culmination** (Cobrahn, Pythas, Anacondra, Serpentis, Skum, Mutanus + Verdan optional rare + Naralex Awakening) — most are EventAI but the **Disciple of Naralex escort + Mutanus spawn** are script-driven via `wailing_caverns.cpp` |

**Entrance (Lushwater Oasis → instance portal)**: outdoor entrance is in the Lushwater Oasis SW of the Crossroads in the (vanilla-unsplit) Barrens. The exit-WSL `(226, 0, 'Wailing Caverns - Exit', '', 0, 0, 1, -738.462, -2217.8, 16.919)` at world_full:180 places the surface portal at Kalimdor coords `(-738.462, -2217.8, 16.919)`. The inside-instance entry WSL `228` is at instance-local `(-158.441, 131.601, -74.2552)`.

**Brief correction**: the user prompt asserted the entrance was in "Northern Barrens at approximately (-741, -2218)". The `-741, -2218` figure is roughly correct (`-738.462, -2217.8` exit-WSL), but **"Northern Barrens" is a Cataclysm zone split** — in 1.12.1 the parent zone is simply **The Barrens (17)**. **Cumulative brief-correction count this iter: +1.**

**Ghost-entrance back to outside the dungeon** on death: `map_template.ghost_entrance_x=-751.131, ghost_entrance_y=-2209.24` (Barrens Lushwater Oasis surface — corpse spawns just outside the cave mouth for an easy run-back).

---

## Geography & Sub-Zones

WC is a **labyrinth**, not a linear corridor — three main loops crossing at central chambers. The 4 Fanglord bosses are scattered (deliberately) so groups can choose pull order, but `Leaders of the Fang` (quest 914) requires **all four** for completion. Sub-zone names below are flagged **TBD** because `AreaTable.dbc` sub-zones do not appear in `world_full_14_june_2021.sql` — only the parent zone 718 is in SQL.

1. **Cavern entrance & winding upper passages** — first corridor immediately past the WSL `228` instance-entry; Deviate Ravagers (3636) ambush packs.
2. **Pool of Tears / Crescent Pool** [TBD — sub-zone names from AreaTable.dbc, not in mangos.sql; functionally the wet-floor pools with **Lord Cobrahn** (3669) in his druid-form alcove].
3. **Lord Pythas chamber** [TBD AreaTable name] — raised stone platform with Naralex-marked ritual circle.
4. **Lady Anacondra chamber** [TBD] — typically reached via a separate vine-corridor branch.
5. **Skum's lair** (Deviate Crocolisk hatchling 3674) — partly-submerged grotto; optional but on-route to Serpentis.
6. **Lord Serpentis chamber** [TBD] — final Fanglord; the kill turns in `Leaders of the Fang` plus enables Disciple gossip option 202 (`GOSSIP_DISCIPLE_SPECIAL` in `def_wailing_caverns.h`).
7. **Verdan the Everliving alcove** (entry **5775**, optional rare — does not spawn 100%) — corner-zone treant; drops `Tail Spike` 6448 (BoP green dagger, lvl 22).
8. **Naralex's Chamber** (Disciple of Naralex escort culminates here; **Mutanus the Devourer 3654 spawns during Awakening event** at position `(142.7, 254.0, -102.2)` per `wailing_caverns.cpp:77 + 503`).

---

## Mob Ecology

| Entry | Creature | Level | HP | Type | Source row |
|-------|----------|-------|----|----|----|
| **3636** | Deviate Ravager | 14-15 | ~700 | Beast (raptor mutate) | `creature_template` (entry referenced in quest 1487 `Deviate Eradication` objective) + `creature_template_addon` row at world_full:443994 `(36360, ...)` |
| **5755** | Deviate Viper | 14-15 | ~650 | Beast (snake mutate); base spell 7947 + Mind-Numbing Poison via creature_template_addon | world_full:444178 `(57550, 'Wailing Caverns - Deviate Viper', 7947, ...)` |
| **5761** | Deviate Shambler | 15-16 | ~750 | Beast (toad mutate) | `creature_template` (entry referenced in quest 1487 objective) |
| **5056** | Deviate Dreadfang | 15-16 | ~750 | Beast (spider mutate) | `creature_template` (entry referenced in quest 1487 objective) |
| **5762** | Deviate Moccasin | 14-15 | ~600 | Beast (small snake — Awakening-event summon, see `wailing_caverns.cpp:65`) | summon-only via Disciple escort event positions 7-10 |
| **5763** | Nightmare Ectoplasm | 16-18 | ~900 | Elemental (Emerald-Dream nightmare entity) — drops `Wailing Essence` 6464 for quest 1491 Smart Drinks | `wailing_caverns.cpp:66`; quest objective in 1491 |
| **3680** | Serpentbloom Snake | 1 | 8 | Beast (decorative — guards Serpentbloom herb 13891) | world_full:448329 `(3680, ..., 'Serpentbloom Snake', ..., 8, 8, ...)` |
| **3840** | Druid of the Fang (trash) | 19-20 | 1212-1299 | Humanoid (caster — spell 9532 Lightning Bolt + 8041 Serpent Form + 5187 Healing Touch + 8040) | world_full:448433 + EventAI 384001 at world_full:93102 (Flee proc) |

The Druids of the Fang trash mob (3840) is **shape-shifter EventAI** — they cast Serpent Form (8041) below 50% HP (mirroring Cobrahn/Pythas Fanglord behaviour) and Flee proc at ~15% HP per `creature_ai_scripts` row 384001 (`Druid of the Fang - Flee`). Treat them like mini-Cobrahns.

---

## Boss Table

All Fanglord bosses + Skum + Verdan are **EventAI-driven** (`creature_template.ScriptName='EventAI'` for entries 3669, 3670, 3671, 3673, 3674, 5775, 3654). The **Disciple of Naralex (3678)** uses C++ script `npc_disciple_of_naralex` (escort AI). The **Mutanus the Devourer spawn** is fired by the Disciple escort phase 6 (`wailing_caverns.cpp:503`). Boss kill state is tracked via `def_wailing_caverns.h::TYPE_*` slots into `instance_wailing_caverns.cpp` so the Awakening-event spawn order respects boss completion (Disciple gossip option 202 unlocks only after all 4 Fanglords are dead — see `wailing_caverns.cpp:606`).

| Boss | Entry | Level | HP | Spells (creature_template + EventAI) | Notable mechanic | Source row |
|------|-------|-------|----|-------|------|-----|
| **Lord Cobrahn** | **3669** | 20 | 2165 | 8040 (Druid Form melee) + EventAI 366901-366908 → Serpent Form transform at 30% HP, second phase melee via spell template swap | Two-phase transformation: Druid Form casts spell pool, then **Serpent Form at 30% HP** (auras shift to melee + venom DoT). Kill **before phase swap** if possible (DPS race window ~5s). | world_full:448321 + EventAI 366901-366908 at world_full:86571+ |
| **Lord Pythas** | **3670** | 21 | 2320 | EventAI 367001-367004: yell on aggro, Healing Touch (5187) 15%-HP trigger + spell pool 700/8147/9532 | Caster Fanglord — Healing Touch at 15% HP can fully refresh him. **Interrupt or stun the heal cast**, otherwise tank cycles through 2-3 extra rotations. | world_full:448322 + EventAI 367001-367004 at world_full:86576+ + AI scripts at 86892-86894 |
| **Lady Anacondra** | **3671** | 20 | 2165 | EventAI 367101-367105: Aggro yell + Nature Channeling (Sleep-like CC) + spell pool 8148/5187/700/9532 — sleep aura applied **every 2s** OOC | Caster Fanglord — **periodically AoE-sleeps party** (Nature Channeling spell). Tank must hold aggro through sleep breaks; healer triages. Cleanse if possible. | world_full:448323 + EventAI 367101-367105 at world_full:86889+ + script row 87159 |
| **Lord Serpentis** | **3673** | 21 | 2784 | EventAI 367301-367304: aggro yell (2102 from broadcast_text), spell pool 6778/700/9532 (poison + caster nukes) | Caster Fanglord — pure poison/shadow damage. Hardest of the 4 (highest HP). His kill enables **Disciple gossip option `GOSSIP_DISCIPLE_SPECIAL=202`** to start the Awakening event. | world_full:448325 + EventAI 367301-367304 at world_full:86886+ |
| **Skum** | **3674** | 21 | 3907 | EventAI 367401: Chained Bolt (lightning chain) every 4-7s, melee 85-110 damage | Deviate Crocolisk hatchling — pure tank-and-spank with chain-lightning damage on multiple targets. **Spread the party** to limit Chained Bolt jumps. | world_full:448326 + EventAI 367401 at world_full:87472 |
| **Mutanus the Devourer** | **3654** | 22 | 4496 | EventAI; melee 117-151 damage, 4-arm murloc grapple | **Spawns during Naralex Awakening event** at Disciple phase 6 (`wailing_caverns.cpp:503`). Final boss. Must be killed for `TYPE_MUTANUS=DONE` (escort phase 8 gate). Drops `Stinging Viper` 6472 BoP main-hand 1H sword. | world_full:448308 |
| **Verdan the Everliving** (optional rare) | **5775** | 21 | 4168 | EventAI 577501: Grasping Vines (root) every 13-16s; melee 201-230 damage | **Optional rare spawn — does NOT spawn 100%**. Treant-form boss with root mechanic; usually skipped unless on-route. Drops `Tail Spike` 6448 (BoP green dagger). | world_full:449793 + EventAI 577501 at world_full:88034 + AI script 94644 |
| **Boahn** (rare Druid of the Fang variant) | **3672** | 20 | 1299 | EventAI 367201-367202: Flee at 15% HP + Serpent Form at 50% HP | Rare elite version of `Druid of the Fang` (3840). Drops `Boahn's Fang` 5423 (BoP MH dagger, lvl 17 req — popular Rogue twink piece). Not always present. | world_full:448324 + EventAI 367201-367202 at world_full:86577+ |

**Brief correction**: the user prompt listed "Lord Cobrahn, Lord Pythas, Lady Anacondra, Lord Serpentis, Skum, Mutanus the Devourer, Verdan the Everliving (some are optional)" as the boss roster. **Confirmed** with one expansion: Verdan AND Boahn are the two non-guaranteed spawns (Boahn was omitted from the brief). The 6 guaranteed bosses are Cobrahn/Pythas/Anacondra/Serpentis/Skum/Mutanus; Verdan + Boahn are optional rares. **Cumulative brief-correction count this iter: +2.**

---

## Quest Table

7 quests scoped to or completing inside WC (zone 718), plus 1 Druid-class quest **Body and Heart** (quest 6002 Horde / 6001 Alliance) which uses a **Moonkin Stone outside WC at the Lushwater Oasis area** — this is the quest the user prompt referenced obliquely as "Druid of the Fang" / "Lupine Mane (item 6766)", but **neither name is in mangos.sql**. Body and Heart uses Cenarion Lunardust + faces Lunaclaw (not a WC mob); it is a Druid prerequisite for Bear Form, completed in The Barrens but **not inside** WC.

| Quest ID | Title | Giver / Turn-in | Zone | Notes | Source row |
|----|----|----|----|----|----|
| **914** | **Leaders of the Fang** | Nara Wildmane (Thunder Bluff, Elder Rise) | 718 (WC) | Bring 4 dream gems (items 6504+6505+9738+9739+9740+9741 chain — quest_objectives encode all 4 boss kills). Reward 2200 XP + 1320 copper + item 1000. Min level 10. **The capstone WC quest.** | world_full:793651 |
| **1489** | Hamuul Runetotem | Nara Wildmane → Hamuul (Thunder Bluff Elder Rise) | 718 (WC) | Speak-to chain step. Routes player from oases-investigation chain into WC Leaders chain. | world_full:794077 |
| **1490** | Nara Wildmane | Hamuul → Nara Wildmane (TB) | 718 (WC) | Returns chain to Nara, unlocks quest 914. | world_full:794078 |
| **1486** | **Deviate Hides** | Nalpak (Disciple of Naralex 5767, inside WC) | 718 (WC) | Collect 20 Deviate Hides (item 6443) from deviate mobs. Reward `item 6480` (Slick Deviate Leggings) + `item 918`. Min level 13. **In-instance NPC turn-in.** | world_full:794074 |
| **1487** | **Deviate Eradication** | Ebru (Disciple of Naralex 5768, inside WC) | 718 (WC) | Kill 7 each of: Deviate Ravager (3636), Deviate Viper (5755), Deviate Shambler (5761), Deviate Dreadfang (5056). Reward items 6476/8071/6481. Min level 15. **In-instance NPC turn-in.** | world_full:794075 |
| **962** | **Serpentbloom** | Apothecary Zamah (Thunder Bluff Apothecarium) | 718 (WC) | Collect 10 Serpentbloom (item 5339). Item is gathered from clickable plant 13891/19535 inside WC. Reward item 10919. Min level 14. | world_full:793698 |
| **959** | **Trouble at the Docks** | Crane Operator Bigglefuzz (Ratchet) | 718 (WC) | Recover the 99-Year-Old Port (item 5334) from Mad Magglish inside WC. Min level 14. | world_full:793695 |
| **1491** | **Smart Drinks** | Mebok Mizzyrix (Ratchet) | 718 (WC) | Collect 6 Wailing Essence (item 6464) from Nightmare Ectoplasm (5763). Min level 13. | world_full:794079 |
| **3366** | **The Glowing Shard** | (Shard pickup ground-loot) → Ratchet inquiry | 718 (WC) | Phase 1 of Nightmare Shard druid chain (continues via quest 6981). Item 10441 drops from bosses. Min level 15. | world_full:794623 |
| **7944** | Sayge's Fortune #25 | Darkmoon Faire (Sayge) | 718 (WC) | Darkmoon-event quest; objective is to visit WC. Quest type 4 (Faire). | NPC text at world_full:8891 + creature spawn 44661 (npc 16096 inside WC at `1340.38, -4638.02`) |

**Brief correction**: the user prompt listed "Disciple of Naralex / Leaders of the Fang / Deviate Hides / Deviate Eradication / Druid of the Fang" as a pre-quest chain. **Two errors**: (1) "Disciple of Naralex" is the **NPC name (3678)**, not a quest title — no quest by that name exists in `quest_template`; (2) "Druid of the Fang" as a quest title is also **not in `quest_template`** — it is the NPC subtitle for the trash mobs (3840) and rare Boahn (3672). The likely intended chain is **Hamuul Runetotem (1489) → Nara Wildmane (1490) → Leaders of the Fang (914)** for the Horde routing, paired with the **in-instance Disciple quests Deviate Hides (1486) + Deviate Eradication (1487)**. **Cumulative brief-correction count this iter: +3.**

**Brief correction**: the user prompt cited item 6766 as "Lupine Mane" for the Druid pre-quest. **Item 6766 in mangos.sql is `npc_text` (Brill/Forsaken hunter gossip about Rand Rhobart), NOT an item_template row, and no item named "Lupine Mane" exists in `item_template`**. The Druid Bear Form prerequisite is `Body and Heart` (quest 6002 Horde / 6001 Alliance) which uses `Cenarion Lunardust` and faces Lunaclaw at a Moonkin Stone — **outdoor in The Barrens, not inside WC**. **Cumulative brief-correction count this iter: +4.**

---

## Recommended Pull Order & Route

WC is a **labyrinth** — multiple valid routes. The community-standard "speed-clear with full chain" path is:

1. **Cavern entrance ramp** → first Deviate Ravager (3636) and Deviate Viper (5755) packs. Single-pull on the ramp; raid spread for Viper Mind-Numbing Poison.
2. **Nalpak / Ebru intake** — pick up `Deviate Hides` (1486) from Nalpak (NPC 5767) and `Deviate Eradication` (1487) from Ebru (NPC 5768) at the in-instance Disciple of Naralex camp.
3. **First Druid of the Fang trash pack** (3840) — CC the caster (Polymorph / Sap / Repentance) before pulling — Serpent-Form transform at 50% HP doubles incoming melee.
4. **Lord Cobrahn** (boss 1, 3669) — engage at full HP. **Race to kill before 30% HP** to skip Serpent-Form phase; otherwise DPS rotation continues with shape-shifted melee.
5. **Lord Pythas** (boss 2, 3670) — caster Fanglord. **Interrupt Healing Touch (5187)** at 15% HP — otherwise add 2-3 rotations.
6. **Skum** (boss 3, 3674) — Crocolisk hatchling. **Spread the party** to break Chained Bolt chain-jumps.
7. **Lady Anacondra** (boss 4, 3671) — caster Fanglord. **AoE sleep** every 2s OOC — tank holds through sleep breaks; cleanse if Druid/Priest/Paladin in party.
8. **Lord Serpentis** (boss 5, 3673) — caster Fanglord. **Highest HP at 2784**. Kill enables `GOSSIP_DISCIPLE_SPECIAL=202` Awakening-event gossip on Disciple of Naralex (3678).
9. **Verdan the Everliving** (optional, 5775) — rare spawn in corner alcove. Skip if not present at the alcove. If engaged, **break Grasping Vines roots** on the melee or kite melee out of range.
10. **Trash to Naralex's Chamber** — fight through Druid of the Fang trash (3840) and Deviate Moccasin (5762) ambushes.
11. **Awakening Event (Disciple of Naralex escort, 3678)**:
    - Trigger gossip option 202 on Disciple (`wailing_caverns.cpp:606`). Disciple casts Mark (spell 5232) on the inviting player, applies Awakening channel (spell 6271).
    - Disciple walks waypoint chain (10 positions in `wailing_caverns.cpp:72-84`).
    - **Phases 0-5**: Disciple speaks/escorts through corridor; party defends against Deviate Ravager + Viper + Moccasin + Nightmare Ectoplasm summon waves at `Position[3-9]`.
    - **Phase 6**: **Mutanus the Devourer (3654) spawns** at `Position[3] = (142.7, 254.0, -102.2)`. Disciple yells `SAY_MUTANUS_SPAWNED=1276` ("This $n is a minion from Naralex's nightmare no doubt!").
    - **Mutanus kill** (phase 8 gate `TYPE_MUTANUS=DONE`) triggers Naralex awakening yell `SAY_NARALEX_AWAKEN=1271`, `TYPE_DISCIPLE=DONE` is set, and Naralex shape-shifts (Cast `SPELL_SHAPESHIFT=8153`) into bird form and flies out (phases 11-12: both Disciple and Naralex `SetFly(true)` and MovePoint to `(101.0, 239.2, -91.2)` at speed 35).
12. **Loot drops** — `Stinging Viper` 6472 from Mutanus, `Robe of the Moccasin` 6465, `Snakeskin Bag` 6446 (8-slot bag — extremely valuable at this level), `Tail Spike` 6448 if Verdan engaged.

3-man over-leveled (L25+) carry runs are viable but skip Mutanus loot rolls. 4-man with a CC class (Mage Polymorph / Rogue Sap / Priest Shackle Undead doesn't apply here — most mobs are Humanoid or Beast) is the realistic minimum for the Awakening event.

---

## Decision-Engine Rules

| id | bracket | precondition | action | priority |
|---|---|---|---|---|
| `dungeon.wc.queue.lfg-or-walkin` | `L15-L25` | `Snapshot.Level>=15 & Snapshot.QueueState.WC.role==null` | `Activity:LfgQueue("WC", autoRole=byClass)` else `Activity:Travel(Barrens_LushwaterOasis)` | 70 |
| `dungeon.wc.party.invite-handshake` | `L15-L25` | `Snapshot.PartyState.size<5 & Snapshot.QueueState.WC.invitePending==true` | `Activity:PartyInvite` (see `../social/party-invite.md` — neutral-zone meeting so no faction gate; cross-faction parties cannot form, individual faction checked) | 80 |
| `dungeon.wc.entrance.travel` | `L15-L25` | `Snapshot.PartyState.complete & Snapshot.Position.zone != 718` | `Activity:Travel(Barrens:-738.46,-2217.80,16.92)` via Crossroads → south to Lushwater Oasis | 75 |
| `dungeon.wc.party.composition-check` | `L15-L25` | `Snapshot.PartyComposition.tank!=null & Snapshot.PartyComposition.healer!=null & Snapshot.PartyComposition.dps.count>=2` | `Activity:EnterInstance(map=43)` | 78 |
| `dungeon.wc.quest.intake-in-instance` | `L15-L25` | `Snapshot.Position.zone==718 & Snapshot.QuestLog.Active(1486)==false & Snapshot.Position.near(Nalpak_5767)` | `Activity:AcceptQuest(1486, 1487)` from Nalpak (5767) + Ebru (5768) at the Disciple camp | 72 |
| `dungeon.wc.pull.druid-of-fang` | `L15-L25` | `Snapshot.NearbyMobs.contains(3840) & 3840.castInterruptible==true` | `Task:CastSpell(KickOrEquivalent, target=3840)` — Druid of the Fang transforms to Serpent Form at 50% HP via EventAI 384001; **CC before pull** (Polymorph/Sap/Hibernate) | 85 |
| `dungeon.wc.boss.cobrahn-race-kill` | `L15-L25` | `Snapshot.Boss.Cobrahn.alive==true & Snapshot.Boss.Cobrahn.engaged==false` | `Task:PullTarget(3669)` — DPS-race target; **emit `pre-30%-HP-kill` priority** to skip Serpent Form phase swap (EventAI 366903 trigger at 30% HP) | 72 |
| `dungeon.wc.boss.pythas-interrupt-heal` | `L15-L25` | `Snapshot.Boss.Pythas.castName == "Healing Touch" \|\| Snapshot.Boss.Pythas.hp<0.20` | `Task:UtilityCast(Interrupt, target=3670)` (see `../combat/utility-casts.md`) — Healing Touch (5187) at 15% HP refresh window | 88 |
| `dungeon.wc.boss.anacondra-sleep-cleanse` | `L15-L25` | `Snapshot.Boss.Anacondra.engaged==true & Snapshot.Party.AnyMember.debuff(Sleep)==true` | `Task:UtilityCast(CleanseOrAbolish)` — Lady Anacondra periodically AoE-sleeps party (Nature Channeling) | 80 |
| `dungeon.wc.boss.skum-spread` | `L15-L25` | `Snapshot.Boss.Skum.engaged==true` | `Task:Positioning(SpreadFormation, radius=10y)` — Skum casts Chained Bolt (jumps to nearby targets); spread to break the chain | 70 |
| `dungeon.wc.boss.serpentis-poison-cleanse` | `L15-L25` | `Snapshot.Boss.Serpentis.engaged==true & Snapshot.Party.AnyMember.debuff(Poison)==true` | `Task:UtilityCast(CurePoison\|AbolishPoison)` — Lord Serpentis spell pool 6778 + 700 includes poison DoT | 78 |
| `dungeon.wc.event.naralex-awakening-prep` | `L15-L25` | `Snapshot.Boss.Serpentis.dead==true & Snapshot.Boss.Cobrahn.dead==true & Snapshot.Boss.Pythas.dead==true & Snapshot.Boss.Anacondra.dead==true` | `Activity:TalkTo(NPC=3678, gossipOption=202)` (see `../npc/gossip.md`) — triggers Awakening event (`GOSSIP_DISCIPLE_SPECIAL=202`); requires ALL 4 Fanglords dead | 78 |
| `dungeon.wc.event.naralex-defend` | `L15-L25` | `Snapshot.InstanceState.NaralexAwakening==IN_PROGRESS & Snapshot.NearbyMobs.containsAny([3636, 5755, 5762, 5763])` | `Task:Defend(target=Disciple_3678, radius=30y)` — escort defends Disciple at Naralex chamber against summon waves | 90 |
| `dungeon.wc.event.mutanus-engage` | `L15-L25` | `Snapshot.InstanceState.NaralexAwakening.phase==6 & Snapshot.NearbyMobs.contains(3654)` | `Task:PullTarget(3654)` — Mutanus the Devourer spawned by Disciple phase 6; kill is the `TYPE_MUTANUS=DONE` gate | 92 |
| `dungeon.wc.loot.snakeskin-bag` | `L15-L25` | `Snapshot.Loot.window.items.any(itemId==6446)` | `Task:LootRoll(Need)` — Snakeskin Bag is 8-slot, BiS at this level for inventory expansion | 65 |
| `dungeon.wc.loot.boss-bop-green` | `L15-L25` | `Snapshot.Loot.window.items.any(itemId in [6472, 6465, 6448, 5423])` | `Task:LootRoll(Need)` if class-appropriate else `Greed`; see `../decision-engine/leveling-priority.md` weights | 60 |
| `dungeon.wc.loot.greed-default` | `L15-L25` | `Snapshot.Loot.window.items.any(quality>=green & questItem==false)` | `Task:LootRoll(Greed)` default; class-spec upgrades trigger `Need` | 55 |
| `dungeon.wc.quest.serpentbloom-gather` | `L15-L25` | `Snapshot.QuestLog.Active(962) & Snapshot.NearbyGameObjects.contains(13891)` | `Task:Interact(GameObject=13891 Serpentbloom)` — clickable plant inside WC; collects item 5339 toward quest 962 | 58 |
| `dungeon.wc.questturnin.sequence` | `L15-L25` | `Snapshot.QuestLog.Complete(914) \|\| Complete(1486) \|\| Complete(1487)` | `Activity:Travel(ThunderBluff.NaraWildmane_or_Hamuul)` for 914 + 1490 turn-ins; in-instance Nalpak/Ebru for 1486/1487; Ratchet Mebok/Bigglefuzz for 1491/959 | 70 |
| `dungeon.wc.wipe.recovery` | `L15-L25` | `Snapshot.Player.alive==false & Snapshot.InstanceState.partyWipe>=2` | `Activity:CorpseRun` (see `../recovery/corpse-run.md`) — ghost-spawn at Barrens surface `-751.13,-2209.24`, walk back into cave mouth | 95 |
| `dungeon.wc.wipe.party-disband-after-3` | `L15-L25` | `Snapshot.InstanceState.partyWipe>=3 & Snapshot.PartyState.demoralized==true` | `Activity:Abandon(ReleaseToGraveyard + LeaveParty)` — 3 wipes in WC signals undergeared or no-CC party | 40 |
| `dungeon.wc.script-readiness` | `L15-L25` | `Snapshot.ServerCapabilities.ScriptedInstance.enabled==true & Snapshot.ServerCapabilities.HasScript("instance_wailing_caverns")==true` | `Activity:EnterInstance(map=43)` — WC depends on C++ script bundle (`instance_wailing_caverns.cpp` + `wailing_caverns.cpp`); if script missing, Awakening event will not fire and `Leaders of the Fang` chain stalls | 92 |

**Total: 22 rules** (target range 15-20 — WC has more rule slots than RFC because of the script-driven Awakening event and the larger boss count).

---

## Snapshot Fields Needed

```text
Snapshot.Level                                              // 15-25 entry band, 10 minimum
Snapshot.Class                                              // role bias + interrupt capability for Pythas heal-interrupt
Snapshot.Position.{zone, x, y, z}                           // zone==718 for in-WC checks
Snapshot.PartyState.{size, complete}                        // 5-man composition
Snapshot.PartyComposition.{tank, healer, dps}               // role validation
Snapshot.QueueState.WC.{role, invitePending}
Snapshot.InstanceState.{firstPull, partyWipe, NaralexAwakening.{phase, status}}
Snapshot.Boss.{Cobrahn, Pythas, Anacondra, Serpentis, Skum, Mutanus, Verdan}.{alive, engaged, dead, castName, hp}
Snapshot.NearbyMobs                                         // EventAI trigger detection (3840 trash, 3636/5755/5761/5056 deviates, 3654 Mutanus spawn)
Snapshot.NearbyGameObjects                                  // Serpentbloom plant 13891 + Meeting Stone 2002
Snapshot.Party.AnyMember.debuff(Sleep|Poison)               // Anacondra sleep + Serpentis poison cleanse triggers
Snapshot.Loot.window.items                                  // Snakeskin Bag 6446 + BoP-green roll decisions
Snapshot.QuestLog.Active(914,1486,1487,962,959,1491,3366)   // WC quest set
Snapshot.QuestLog.Complete(914,1486,1487)                   // turn-in routing
Snapshot.Inventory.Has(6443,5339,5334,6464)                 // Deviate Hides + Serpentbloom + Port + Wailing Essence quest tokens
Snapshot.ServerCapabilities.ScriptedInstance.enabled        // script-readiness gate
Snapshot.ServerCapabilities.HasScript("instance_wailing_caverns")  // dependency check
```

---

## TBD / Backlog (per verification rule)

| Item | Status | Resolving grep |
|---|---|---|
| Sub-zone names "Pool of Tears" / "Crescent Pool" / Fanglord chamber labels | TBD — not in `world_full_14_june_2021.sql` | `AreaTable.dbc` extraction (mangos uses DBC for sub-zones); fallback wiki crawl |
| Outdoor Druid hill (`The Wailer's Hilltop`?) + entrance approach NPCs | TBD — clarify which surface NPC gives druid pre-quest if any (Disciples are inside the instance, not outside) | `Grep "Wailing Caverns" + "Hamuul\|Loganaar\|Mathrengyl"` on world_full_14_june_2021.sql |
| Druid class quest "Body and Heart" full chain (6001/6002 Lunardust) | Partial — quest_template rows confirmed at world_full:795411-795412 | Cross-link with `npc/quest-giver.md` and `../zones/barrens.md` Moonkin Stone gameobject lookup |
| Verdan + Boahn rare-spawn rates | TBD — `creature_template.spawnTimeSecsMin/Max` not dumped here | `Grep -E "^\s*\(5775,\|^\s*\(3672," world_full + lookup `creature_spawn_data.spawn_time_*` |
| Awakening event "summon wave" exact mob entries per phase | Partial — `wailing_caverns.cpp:65-67` confirms 3636/5755/5762/5763 but per-phase wave counts at positions 7-10 need full read | Read `wailing_caverns.cpp:480-505` for full phase-3/4 summon loops |
| Cross-faction Alliance routing into WC (no Alliance hub near Lushwater Oasis) | TBD — Alliance access typically via Ratchet flightpath then walk; verify FP id | `Grep "Ratchet" + "taxi"` on world_full_14_june_2021.sql |

---

## Cross-References

- **Party invite handshake** (cross-faction-impossible 5-man at neutral zone): [`../social/party-invite.md`](../social/party-invite.md) — `CMSG_GROUP_INVITE=110` flow.
- **Pull / engage**: [`../combat/pull-target.md`](../combat/pull-target.md), [`../combat/threat-management.md`](../combat/threat-management.md).
- **Interrupts / utility casts** (Pythas Healing Touch, Druid of the Fang Serpent Form transform): [`../combat/utility-casts.md`](../combat/utility-casts.md), [`../combat/cast-spell.md`](../combat/cast-spell.md).
- **Melee rotation** (Cobrahn DPS-race tank-and-spank): [`../combat/melee-rotation.md`](../combat/melee-rotation.md).
- **Heal task** (Anacondra sleep-cleanse triage, Serpentis poison-cleanse): [`../combat/heal-task.md`](../combat/heal-task.md).
- **NPC gossip** (Disciple of Naralex Awakening-event trigger via gossip option 202): [`../npc/gossip.md`](../npc/gossip.md), [`../npc/quest-giver.md`](../npc/quest-giver.md).
- **Wipe handling**: [`../recovery/corpse-run.md`](../recovery/corpse-run.md), [`../recovery/release-corpse.md`](../recovery/release-corpse.md), [`../recovery/spirit-healer.md`](../recovery/spirit-healer.md).
- **Quest turn-ins** (Nara Wildmane / Hamuul Runetotem / Nalpak / Ebru / Apothecary Zamah / Mebok Mizzyrix / Crane Operator Bigglefuzz): [`../npc/quest-giver.md`](../npc/quest-giver.md).
- **Bracket context** (L10-L20 and L20-L30): [`../sections/02-l10-l20.md`](../sections/02-l10-l20.md), [`../sections/03-l20-l30.md`](../sections/03-l20-l30.md) (verify pass 2).
- **Decision-engine integration**: [`../decision-engine/leveling-priority.md`](../decision-engine/leveling-priority.md), [`../decision-engine/state-flags.md`](../decision-engine/state-flags.md), [`../decision-engine/unlock-graph.md`](../decision-engine/unlock-graph.md).
- **Sibling dungeon** (lower bracket, Horde-only): [`ragefire-chasm.md`](ragefire-chasm.md) — RFC L13-18 template authority.
- **Sibling dungeon** (next Alliance-natural at L17-26): [`deadmines.md`](deadmines.md) (TBD — listed in 00_INDEX as future).

---

## VMaNGOS / Server Reality Check

WC is **script-driven** — unlike RFC, the boss + escort scripting depends on `instance_wailing_caverns.cpp` + `wailing_caverns.cpp` + `def_wailing_caverns.h` at `D:/MaNGOS/source/src/scripts/kalimdor/the_barrens/wailing_caverns/`. The Disciple of Naralex escort, Mutanus the Devourer spawn-during-Awakening, and Naralex-shape-shift fly-out are **C++ event hooks**, NOT EventAI rows.

**Risk classes**:
- **Low risk** for individual Fanglord + Skum + Verdan boss fights (EventAI rows 366*/367*/367[1-4]*/3674*/577501 are data-driven and rarely regress).
- **Moderate risk** for the **Naralex Awakening event** (script-driven, depends on `ScriptedInstance::SetData(TYPE_*, DONE)` transitions in `instance_wailing_caverns.cpp` matching the `npc_disciple_of_naralex::UpdateAI` phase machine in `wailing_caverns.cpp`). Forks that refactor `npc_escortAI` or `ScriptedInstance` base classes can stall the event.
- **Moderate risk** for Mutanus spawn — depends on `Naralex->FindNearestCreature(MOB_MUTANUS_DEVOURER, 100.0f)` at `wailing_caverns.cpp:510` finding the freshly-summoned creature within 100y of Naralex's position.

Decision-engine rule `dungeon.wc.script-readiness` gates on `Snapshot.ServerCapabilities.HasScript("instance_wailing_caverns")==true` as a precaution. On official-track VMaNGOS this is always true; on stripped-down test servers it may be false and `Leaders of the Fang` (914) cannot be turned in because the Awakening event stalls.

Quest 914 (Leaders of the Fang) is the most heavily script-coupled WC quest because `GOSSIP_DISCIPLE_SPECIAL=202` only appears if all 4 Fanglords are flagged DONE in the instance data — verify the instance-data persistence path before accepting a "Fanglord kill state lost on server restart" failure mode (this is a known VMaNGOS fork divergence).
