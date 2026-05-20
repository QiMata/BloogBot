---
title: "Dungeon — Ragefire Chasm (RFC)"
patch: "1.12.1 (Drums of War, Sept 2006)"
sources_crawled:
  - D:/MaNGOS/sql/world_full_14_june_2021.sql (map_template / creature_template / quest_template / game_event / worldsafelocs / areatrigger / creature_ai_scripts)
  - D:/MaNGOS/sql/world_full_14_june_2021.sql:728011 (map_template header)
  - https://warcraft.wiki.gg/wiki/Ragefire_Chasm
crawl_date: 2026-05-20
---

# Ragefire Chasm (RFC) — 5-Man Dungeon Guide

**First file in the dungeons/ family established as the template** (per iter-28 Activity-catalog inventory). RFC is the **lowest-level instance in the game** — Horde-only entrance from the **Cleft of Shadow** in central Orgrimmar, leading to a small volcanic cavern under the city occupied by **Searing Blade** cultists, **Troggs**, and **Ragefire troll-shaman**. 4 bosses, ~30-60 minute full clear, level band **13-18 optimal**. Acts as a Horde counterpart to Alliance Deadmines for first-instance experience and a key venue for the iconic **Hidden Enemies** chain (Searing Blade infiltration, Neeru Fireblade questline). **All boss AI is EventAI-driven** — no dedicated `instance_ragefire_chasm.cpp` script exists under `D:/MaNGOS/source/src/scripts/kalimdor/` (unlike Maraudon / DM / Strat). Scripting risk is therefore **low** (EventAI is data-driven via `creature_ai_scripts` table).

---

## Quick Facts

| Field | Value | Source |
|-------|-------|--------|
| `map_id` | **389** | `map_template:728012+` row `(389, 0, 0, 1, 2437, 40, 0, 1, 1816.76, -4423.37, 'Ragefire Chasm', '')` — `map_type=1` (5-man dungeon), `linked_zone=2437`, `ghost_entrance_map=1` (Kalimdor), `script_name=''` (no dedicated C++ script) |
| Continent / Parent map | Kalimdor (map 1) | `map_template:728013` |
| Linked zone | **2437** "Ragefire Chasm" | `areatable.dbc` row `(2437, 389, 0, 927, 0, 0, 'Ragefire Chasm', 4, 0)` at world_full:1758 |
| Group size | 5-man (`player_limit=40` in template is the legacy cap; instance type 1 enforces 5) | `map_template.player_limit` |
| Reset delay | 0 (no scheduled reset; standard instance lockout via instance_reset) | `map_template.reset_delay=0` |
| Level range | **13-18** optimal (level **8 minimum** to enter) | Worldsafelocs `(2230, 0, 'Ragefire Chasm - Entrance', 'You must be at least level 8 to enter.', 8, 0, 389, ...)` at world_full:226 |
| Faction | **Horde-only** entrance (Orgrimmar interior); Alliance summon only via Warlock + Meeting Stone | Geographic — entrance is inside Orgrimmar city |
| Meeting Stone | **GameObject 2000** `'Meetingstone - Ragefire Chasm'` lvl 36 (cluster range) | `game_object_template` row at world_full:563785 `(2000, 0, 36, 2437, 0, ...)` |
| Theme | Volcanic cavern under Orgrimmar — lava, Searing Blade demonology, Trogg uprising |
| Boss count | **4** named bosses (all EventAI scripted) |

**Entrance (Cleft of Shadow → instance portal)**: gateway in central Orgrimmar; worldsafelocs `2230` (`x=0.797643, y=-8.23429, z=-15.5288, o=4.71239`) is the **inside-instance** ghost-entrance graveyard target.

**Ghost-entrance back to outside Orgrimmar** on death: `map_template.ghost_entrance_x=1816.76, ghost_entrance_y=-4423.37` (Durotar Valley of Spirits ridge — corpse spawns outside city walls, requiring run-back).

**Exit**: worldsafelocs `2226` `(x=1814.99, y=-4419.23, z=-18.8151, o=1.91986)` — drops outside the Orgrimmar east entrance, near Razor Hill flightpath corridor.

---

## Geography & Sub-Zones

The cavern is a single connected volcanic basin with three loosely-named pockets in client AreaTable lore (sub-zones are **not** in `world_full_14_june_2021.sql`'s `area_template` — they live in `AreaTable.dbc`; flagged TBD below). Layout from instance entrance:

1. **Entrance ramp & antechamber** (Trogg ambush zone — Ragefire Trogg, lvl 13-15 melee).
2. **Lavafingers Passage** [TBD — sub-zone name from AreaTable.dbc, not in mangos.sql; functionally the lava-tongue corridor leading to Taragaman + Oggleflint area].
3. **The Maw of Shadow** [TBD — sub-zone name from AreaTable.dbc; functionally the Searing Blade ritual chamber housing Jergosh + Bazzalan].
4. Open central lava lake with **Oggleflint** (Ragefire Chieftain) on a raised stone platform.

**Brief correction**: the user prompt asserted sub-zones `Lavafingers Passage` and `The Maw of Shadow` exist in mangos.sql — they do NOT (no matches for either string in `world_full_14_june_2021.sql`). These names are AreaTable.dbc client-data sub-zones (or wiki-folk names); the SQL only knows the parent `Ragefire Chasm` zone 2437. **Cumulative brief-correction count this iter: +1.**

---

## Mob Ecology

| Entry | Creature | Level | HP | Type | Source row |
|-------|----------|-------|----|----|----|
| **11318** | Ragefire Trogg | 13-15 | 819-984 | Humanoid (Trogg) | world_full:452086 `creature_template` |
| **11319** | Ragefire Shaman | 13-15 | 759-903 | Humanoid (Trogg caster) — casts spell **9532** (Lightning Bolt) | world_full:452087 + EventAI 1131901 at world_full:86259 ("Flee at 15% HP") |
| 11320 | Earthborer | ~14 | — | Beast (lava-worm) | creature_template_addon at world_full:443393 entry `113200`, base spell **18070** |
| 11321 | Searing Blade Cultist | ~15 | — | Humanoid caster — Curse spell **8242** (Curse of Agony) | world_full:444485 `113220` |
| 11322 | Searing Blade Enforcer | ~15 | — | Humanoid melee | world_full:444486 `113230` |
| 11323 | Searing Blade Warlock | ~15 | — | Humanoid caster — spell **20791** | world_full:445029 `113240` |

`EventAI` flag in `creature_template.ScriptName` column: `'EventAI'` set for Ragefire Shaman (11319) + Bazzalan (11519). Other RFC mobs use default AI fed from `creature_ai_scripts` rows in the 1131*01 / 1151*01 namespace (`1131901` = Ragefire Shaman flee; `1151901` + `1151902` = Bazzalan Sinister Strike + Poison).

---

## Boss Table

All four bosses are **EventAI-driven**. No `instance_ragefire_chasm` script exists at `D:/MaNGOS/source/src/scripts/kalimdor/` (verified by `find ... -iname '*ragefire*'` returning empty). Scripting reliability is therefore high — data-driven EventAI does not regress with C++ refactors.

| Boss | Entry | Level | HP | Spells (creature_template `spell1`/`spell2`) | Notable mechanic | Source row |
|------|-------|-------|----|-------|------|-----|
| **Oggleflint** (Ragefire Chieftain) | **11517** | 16 | 1424 | **5532** (Cleave) | Tank-and-spank with frontal Cleave; spawns first | world_full:452185 + creature_template_addon `115170` at world_full:444509 |
| **Jergosh the Invoker** | **11518** | 16 | 1381 (+1712 mana) | **20800** + **18267** | Caster — Immolate + caster-AI; interrupt or LoS | world_full:452186 + addon `115180` at world_full:444510 (with column 14=`20800`, column 21=`18267`) |
| **Bazzalan** (Imp tied to Burning Blade cult) | **11519** | 16 | 1513 | **2007** (Sinister Strike base) + EventAI extras **14873** (Sinister Strike) + **744** (Poison) | Rogue-class melee; uses Poison DoT — Cure Disease/Poison if available | world_full:452187 + EventAI rows `1151901`/`1151902` at world_full:88881 + 96549-50 |
| **Taragaman the Hungerer** (Felguard) | **11520** | 16 | 1869 | **7970** + **18072** + **11970** | Highest-HP RFC boss; quest target for **Slaying the Beast (5761)** — drops **Item 14540 "Taragaman the Hungerer's Heart"** at 100% rate (quest token) | world_full:452188 + addon `115200` at world_full:443230 + item_template `14540` at world_full:635891 |

**Brief correction**: the user prompt asked for boss skill IDs cited via `Spell.dbc` or `scripted_creature/AI files at D:/MaNGOS/source/src/scripts/eastern_kingdoms/ or azeroth/`. RFC is on **Kalimdor**, not Eastern Kingdoms — and crucially **has no scripted_creature .cpp at all**. Skill IDs come from `creature_template.spell1/2` columns + `creature_ai_scripts` table rows. **Cumulative brief-correction count this iter: +2.**

---

## Quest Table

5 quests scoped to or completing inside RFC, plus the 5-step **Hidden Enemies** Thrall chain (steps 5726-5730) which originates in Orgrimmar but routes through RFC at step 5728:

| Quest ID | Title | Giver / Turn-in | Zone | Notes | Source row |
|----|----|----|----|----|----|
| **5761** | **Slaying the Beast** | Neeru Fireblade (Cleft of Shadow, Orgrimmar) | 2437 (RFC) | Kill Taragaman the Hungerer (11520); turn in his heart (item 14540). Reward 1150 XP + 800 copper. Min level 9. | world_full:795355 |
| **5722** | **Searching for the Lost Satchel** | Rahauro (Mulgore/Thunder Bluff) | 2437 (RFC) | "Search Ragefire Chasm for Maur Grimtotem's lost satchel" — clickable gameobject inside RFC. | world_full:795344 |
| **5725** | **The Power to Destroy...** | Varimathras (Undercity Royal Quarter) | 2437 (RFC) | Bring the books *Spells of Shadow* and *Incantations from the Nether* (drop from Searing Blade Cultists/Warlocks) to Varimathras in Undercity. | world_full:795347 |
| **5726** | Hidden Enemies (1) | Thrall (Grommash Hold, Orgrimmar) | 1637 (Orgrimmar) | Bring a Lieutenant's insignia (Burning Blade) — leads to RFC chain. | world_full:795348 |
| **5727** | Hidden Enemies (2) | Thrall | 1637 (Orgrimmar) | "Take the Lieutenant's insignia to Neeru Fireblade." Sets up Searing Blade investigation. | world_full:795349 |
| **5728** | Hidden Enemies (3) | Neeru Fireblade | **2437 (RFC)** | "Gauge Neeru Fireblade's loyalty by entering RFC and observing Searing Blade activities." | world_full:795350 |
| **5729** | Hidden Enemies (4) | Neeru Fireblade | 1637 (Orgrimmar) | "Speak to Neeru Fireblade in Orgrimmar." Returns from RFC. | world_full:795351 |
| **5730** | Hidden Enemies (5) | Neeru Fireblade → Thrall | 1637 (Orgrimmar) | "Speak to Thrall in Orgrimmar and tell him what you've found." Wraps the chain. | world_full:795352 |

**Brief correction**: the user prompt listed "Testing the Vessel" as an RFC quest. **This is wrong** — quest 3123 "Testing the Vessel" is the **Witch Doctor Uzer'i Feralas → Hinterlands Wildkin muisek-capture chain**, zone 357 (Feralas), unrelated to RFC. **Cumulative brief-correction count this iter: +3.** RFC's canonical 5-quest set is **Slaying the Beast + Searching for the Lost Satchel + The Power to Destroy... + Hidden Enemies steps 5728** (plus the surrounding chain steps that route through RFC).

---

## Recommended Pull Order & Route

Standard clear is **linear** — RFC has no branching wings (unlike DM / Strat). The path threads through trash to each boss in turn:

1. **Entrance ramp** → first Trogg pack (2-3x Ragefire Trogg 11318). Tank single-pulls or LOS-pulls around the entrance bend; melee Cleave-safe spread.
2. **First Shaman pack** (1x Ragefire Shaman 11319 + 2x Trogg). **Interrupt the Shaman's Lightning Bolt (9532) on cooldown**; finish him before the 15%-HP Flee proc (EventAI 1131901) sends him running to add more packs.
3. **Searing Blade vestibule** — 2-3x Searing Blade Cultists (11321, Curse of Agony 8242) + Enforcers (11322 melee). CC Cultist if available (Polymorph, Sap); tank Enforcer; range-kill caster first to limit DoT stacking.
4. **Oggleflint** (boss 1, 11517) — tank pulls onto raised stone platform; raid behind to avoid Cleave (5532).
5. **Lava-tongue corridor** — Earthborer (11320) lava-worm packs; AoE-friendly. Mind the lava trim — pulling into it costs HP per tick.
6. **Taragaman the Hungerer** (boss 2, 11520, the highest-HP at 1869) — open chamber; **quest target for 5761 Slaying the Beast**; 100% drop of item 14540 (heart). Tank-and-spank.
7. **Searing Blade ritual chamber** — Warlocks (11323, spell 20791) + final Cultist pack. CC + focus-fire casters.
8. **Jergosh the Invoker** (boss 3, 11518) — caster boss; interrupt Immolate (18267) on cooldown; tank holds, ranged DPS.
9. **Bazzalan** (boss 4, 11519) — Rogue-class melee + Poison (744) DoT. Cure Poison/Abolish Poison if available; otherwise tank through (Poison is mild at lvl 16).

Full clear in 4-man (no DPS surplus) is viable for groups with at least one Mage or Warlock for CC. 3-man is possible for over-leveled (L20+) carry runs but defeats the XP curve.

---

## Decision-Engine Rules

| id | bracket | precondition | action | priority |
|---|---|---|---|---|
| `dungeon.rfc.queue.lfg-or-walkin` | `L13-L18` | `Snapshot.Faction==Horde & Snapshot.Level>=13 & Snapshot.QueueState.RFC.role==null` | `Activity:LfgQueue("RFC", autoRole=byClass)` else `Activity:Travel(Orgrimmar_CleftOfShadow)` | 70 |
| `dungeon.rfc.party.invite-handshake` | `L13-L18` | `Snapshot.PartyState.size<5 & Snapshot.QueueState.RFC.invitePending==true` | `Activity:PartyInvite` (see `../social/party-invite.md` R7/R9 — 60s decline window + faction gate via `ERR_PLAYER_WRONG_FACTION`) | 80 |
| `dungeon.rfc.entrance.travel` | `L13-L18` | `Snapshot.PartyState.complete & Snapshot.Position.zone != 2437` | `Activity:Travel(Orgrimmar:0.79,-8.23,-15.52)` via Cleft of Shadow portal | 75 |
| `dungeon.rfc.party.composition-check` | `L13-L18` | `Snapshot.PartyComposition.tank!=null & Snapshot.PartyComposition.healer!=null & Snapshot.PartyComposition.dps.count>=2` | `Activity:EnterInstance(map=389)` | 78 |
| `dungeon.rfc.pull.trogg-ambush` | `L13-L18` | `Snapshot.InstanceState.firstPull==true & Snapshot.NearbyMobs.contains(11318)` | `Task:PullTarget` (see `../combat/pull-target.md`) — single-target tank pull; melee Cleave-safe spread | 65 |
| `dungeon.rfc.pull.shaman-priority` | `L13-L18` | `Snapshot.NearbyMobs.contains(11319) & 11319.castInterruptible==true` | `Task:CastSpell(KickOrEquivalent, target=11319)` — Ragefire Shaman casts Lightning Bolt (spell 9532); also has Flee@15% (EventAI 1131901) → finish him before flee proc | 85 |
| `dungeon.rfc.boss.oggleflint` | `L13-L18` | `Snapshot.Boss.Oggleflint.alive==true & Snapshot.Boss.Oggleflint.engaged==false` | `Task:PullTarget(11517)` — tank-and-spank; raid stays behind boss to dodge Cleave (5532) | 72 |
| `dungeon.rfc.boss.taragaman-quest-loot` | `L13-L18` | `Snapshot.Boss.Taragaman.dead==true & Snapshot.QuestLog.Active(5761)` | `Task:LootCorpse + Inventory.RecordItem(14540)` — heart is 100% drop, no rolling needed | 90 |
| `dungeon.rfc.boss.jergosh-interrupt` | `L13-L18` | `Snapshot.Boss.Jergosh.castName in ("Immolate","Curse")` | `Task:UtilityCast(Interrupt, target=11518)` (see `../combat/utility-casts.md`) | 82 |
| `dungeon.rfc.boss.bazzalan-poison-cure` | `L13-L18` | `Snapshot.Boss.Bazzalan.engaged==true & Snapshot.Party.AnyMember.debuff(744)==true` | `Task:UtilityCast(CurePoison|AbolishPoison)` — Bazzalan EventAI 1151902 applies Poison spell 744 | 78 |
| `dungeon.rfc.loot.bop-quest-tokens` | `L13-L18` | `Snapshot.Loot.window.items.any(itemId==14540 \|\| itemId==15457)` | `Task:LootRoll(Need)` for quest-token items; `Pass` on grey vendor trash unless `Snapshot.Inventory.freeSlots<4` | 60 |
| `dungeon.rfc.loot.greed-default` | `L13-L18` | `Snapshot.Loot.window.items.any(quality>=green & questItem==false)` | `Task:LootRoll(Greed)` default; class-spec upgrades trigger `Need` via `decision-engine/leveling-priority.md` weights | 55 |
| `dungeon.rfc.wipe.recovery` | `L13-L18` | `Snapshot.Player.alive==false & Snapshot.InstanceState.partyWipe>=2` | `Activity:CorpseRun` (see `../recovery/corpse-run.md` + `../recovery/release-corpse.md`) — ghost-spawn at `1816.76,-4423.37` Kalimdor surface, run back through Cleft of Shadow | 95 |
| `dungeon.rfc.wipe.party-disband-after-3` | `L13-L18` | `Snapshot.InstanceState.partyWipe>=3 & Snapshot.PartyState.demoralized==true` | `Activity:Abandon(ReleaseToGraveyard + LeaveParty)` — RFC is not a grind-through; 3 wipes suggests undergeared/under-leveled party | 40 |
| `dungeon.rfc.questturnin.sequence` | `L13-L18` | `Snapshot.QuestLog.Complete(5761) \|\| Complete(5722) \|\| Complete(5728)` | `Activity:Travel(Orgrimmar.NeeruFireblade)` first (5761+5728 turn-ins) → then `Activity:Travel(ThunderBluff.Rahauro)` (5722) → then Undercity Royal Quarter (5725) via Zeppelin (see `../travel/zeppelin.md` if exists, else `../npc/quest-giver.md`) | 70 |
| `dungeon.rfc.questturnin.hidden-enemies-chain` | `L13-L18` | `Snapshot.QuestLog.Complete(5728)==true` | `Activity:Travel(Orgrimmar.NeeruFireblade)` → turn in 5728 → accept 5729 → turn in 5729 → accept 5730 → travel to Grommash Hold → turn in 5730 to Thrall (chain anchor) | 68 |
| `dungeon.rfc.alliance.summon-only` | `L13-L18` | `Snapshot.Faction==Alliance & Snapshot.QueueState.RFC.summonOffered==true` | `Activity:AcceptWarlockSummon` (see `../travel/warlock-summon.md`) — Alliance cannot enter Orgrimmar safely; summon directly to meeting stone (GO 2000) | 50 |
| `dungeon.rfc.script-readiness` | `L13-L18` | `Snapshot.ServerCapabilities.EventAI.enabled==true` | `Activity:EnterInstance(map=389)` — RFC is 100% EventAI; if EventAI disabled (modded server), abort and surface alert | 92 |

**Total: 18 rules** (target range 15-20).

---

## Snapshot Fields Needed

```text
Snapshot.Level                                    // 13-18 entry band, 8 minimum
Snapshot.Faction                                  // Horde walk-in vs Alliance summon
Snapshot.Class                                    // role bias + interrupt capability for Jergosh
Snapshot.Position.{zone, x, y, z}                 // zone==2437 for in-RFC checks
Snapshot.PartyState.{size, complete}              // 5-man composition
Snapshot.PartyComposition.{tank, healer, dps}     // role validation
Snapshot.QueueState.RFC.{role, invitePending, summonOffered}
Snapshot.InstanceState.{firstPull, partyWipe}     // wipe-counter + opener detection
Snapshot.Boss.{Oggleflint, Jergosh, Bazzalan, Taragaman}.{alive, engaged, castName, dead}
Snapshot.NearbyMobs                               // EventAI trigger detection (11319 Shaman caster, 11318 Trogg)
Snapshot.Party.AnyMember.debuff(744)              // Bazzalan Poison cure trigger
Snapshot.Loot.window.items                        // Need/Greed/Pass decisions
Snapshot.QuestLog.Active(5761,5722,5725,5728)     // RFC quest set
Snapshot.QuestLog.Complete(5728,5761)             // turn-in routing
Snapshot.Inventory.Has(14540)                     // Taragaman heart quest token
Snapshot.ServerCapabilities.EventAI.enabled       // script-readiness gate
```

---

## TBD / Backlog (per verification rule)

| Item | Status | Resolving grep |
|---|---|---|
| Sub-zone names "Lavafingers Passage" / "The Maw of Shadow" | TBD — not in `world_full_14_june_2021.sql` | `AreaTable.dbc` extraction (mangos uses DBC for sub-zones); fallback wiki crawl |
| Exact RFC trash entry IDs (Earthborer 11320, etc.) | Partial — creature_template_addon rows confirm `113180/113190/113200/113220/113230/113240` exist; full creature_template rows for 11320-11323 not all dumped here | `Grep -E "^\s*\(1132[0-3],"` on world_full_14_june_2021.sql |
| Item 15457 (referenced in loot rule) | TBD — likely Searing Blade book drop for quest 5725 | `Grep "Spells of Shadow\|Incantations from the Nether"` on world_full_14_june_2021.sql |
| Travel doc `../travel/zeppelin.md` | TBD — referenced but may not exist yet | `ls "E:/repos/Westworld of Warcraft/docs/leveling-guide/travel/"` |

---

## Cross-References

- **Party invite handshake** (Horde 5-man formation): [`../social/party-invite.md`](../social/party-invite.md) — `CMSG_GROUP_INVITE=110` flow, 60s decline window, faction gate via `ERR_PLAYER_WRONG_FACTION`.
- **Pull / engage**: [`../combat/pull-target.md`](../combat/pull-target.md), [`../combat/threat-management.md`](../combat/threat-management.md).
- **Interrupts / utility casts** (Jergosh Immolate, Ragefire Shaman Lightning Bolt): [`../combat/utility-casts.md`](../combat/utility-casts.md), [`../combat/cast-spell.md`](../combat/cast-spell.md).
- **Melee rotation** (Oggleflint / Bazzalan tank-and-spank): [`../combat/melee-rotation.md`](../combat/melee-rotation.md).
- **Heal task** (5-man healer triage): [`../combat/heal-task.md`](../combat/heal-task.md).
- **Wipe handling**: [`../recovery/corpse-run.md`](../recovery/corpse-run.md), [`../recovery/release-corpse.md`](../recovery/release-corpse.md), [`../recovery/spirit-healer.md`](../recovery/spirit-healer.md).
- **Quest turn-ins** (Neeru Fireblade, Thrall, Rahauro, Varimathras): [`../npc/quest-giver.md`](../npc/quest-giver.md), [`../npc/gossip.md`](../npc/gossip.md).
- **Alliance summon path**: [`../travel/warlock-summon.md`](../travel/warlock-summon.md).
- **Bracket context** (L10-L20): [`../sections/02-l10-l20.md`](../sections/02-l10-l20.md) (or nearest equivalent — verify pass 2).
- **Decision-engine integration**: [`../decision-engine/leveling-priority.md`](../decision-engine/leveling-priority.md), [`../decision-engine/state-flags.md`](../decision-engine/state-flags.md), [`../decision-engine/unlock-graph.md`](../decision-engine/unlock-graph.md).
- **Sibling dungeon** (next Horde-natural at L15-25): [`wailing-caverns.md`](wailing-caverns.md) (TBD — listed in 00_INDEX as future).

---

## VMaNGOS / Server Reality Check

RFC is **fully EventAI-driven** (verified: no `instance_ragefire_chasm.cpp` under `D:/MaNGOS/source/src/scripts/kalimdor/`). EventAI data rows confirmed for Ragefire Shaman flee (1131901) and Bazzalan Sinister Strike + Poison (1151901 + 1151902). Risk of script-break across modern VMaNGOS forks is **low** — EventAI is data-driven and rarely regresses. Decision-engine rule `dungeon.rfc.script-readiness` gates on `Snapshot.ServerCapabilities.EventAI.enabled==true` as a precaution; on official-track VMaNGOS this is always true.

No known boss-mechanic divergences from retail 1.12.1. The Hidden Enemies chain (5726-5730) has been stable since 14-June-2021 world dump; quest IDs verified inline above.
