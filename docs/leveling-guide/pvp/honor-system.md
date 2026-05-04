# Honor System — 1.12.1 PvP Ranks 1-14

> **Sources** (crawl date 2026-05-01):
> - https://wowpedia.fandom.com/wiki/Honor_system_(Classic) (referenced via search)
> - https://vanilla-wow-archive.fandom.com/wiki/Honor_System (referenced via search)
> - https://wowwiki-archive.fandom.com/wiki/Honor_system_(pre-2.0) (referenced via search)
> - https://www.boosting-ground.com/wow-classic/guides/pvp-guides/wow-classic-honor-system
> - https://www.chaosboost.com/blog/wow-classic-pvp-ranks-basics-of-ranking-system
> - https://splashgame.org/pvp-honor-system/
>
> **Pass 2.** Some details (exact CP-per-rank thresholds, week-1 starter CP budget) marked `[verify pass 3]`.
>
> **Version note.** All facts here describe live patch **1.12.1 (2006)**. The honor system was introduced in **patch 1.4 (May 2005)** and refined through 1.5/1.10/1.12. The Classic 2019 re-release uses essentially the same system. Pre-TBC (2.0.1) the system was discarded and replaced with TBC honor.

## Identity

The 1.12.1 honor system is a **server-wide weekly ranked ladder**. Players earn **Honorable Kills (HKs)** + **Bonus Honor** during the week → calculated into **Contribution Points (CP)** → assigned a **standing rank** at weekly reset (Tuesday). Standing decays by **~20% per week** (player keeps 80% of last week's CP carried forward), so maintaining a high rank requires sustained weekly grinding.

**The single longest grind in vanilla 1.12.1.** Reaching **rank 14 (Grand Marshal / High Warlord)** takes ~12-14 weeks of 60-80 hours/week sustained PvP — the most time-consuming achievement in the game.

## Rank ladder (Alliance / Horde titles)

| Rank | Alliance title | Horde title |
|---|---|---|
| 1 | Private | Scout |
| 2 | Corporal | Grunt |
| 3 | Sergeant | Sergeant |
| 4 | Master Sergeant | Senior Sergeant |
| 5 | Sergeant Major | First Sergeant |
| 6 | Knight | Stone Guard |
| 7 | Knight-Lieutenant | Blood Guard |
| 8 | Knight-Captain | Legionnaire |
| 9 | Knight-Champion | Centurion |
| 10 | Lieutenant Commander | Champion |
| 11 | Commander | Lieutenant General |
| 12 | Marshal | General |
| 13 | Field Marshal | Warlord |
| 14 | **Grand Marshal** | **High Warlord** |

**Naming convention**: Alliance ranks 6+ use Stormwind Royal Guard hierarchy (Knight, Knight-Lieutenant, etc.); Horde uses Orc warband hierarchy (Stone Guard, Blood Guard, Legionnaire, Centurion, etc.).

## Honor mechanics — how CP accumulates

### Honorable Kills (HKs)

A player gets an HK when they participate in killing an enemy player **within 10 levels of their own level**. Kills on lower-level players within range or at a 10-level disadvantage are **dishonorable kills** (DK) — count negatively or not at all.

**CP per HK is variable** and depends on:
- **Enemy rank**: Higher-ranked enemies grant more CP per kill (~2x for rank 10 vs unranked)
- **Damage contribution**: CP is split among all groups/players that damaged the target (so a solo 1v1 kill grants more per-player CP than a 40-man-pile-on)
- **Diminishing returns per target**: Killing the same player repeatedly within a session gives diminishing CP (anti-farm mechanic)

### Bonus Honor (Battlegrounds)

Battlegrounds grant **Bonus Honor** for objectives:
- **Warsong Gulch** (10v10, lvl 10+): Flag captures (~2-3 CP per cap), control pulses
- **Arathi Basin** (15v15, lvl 20+): Flag captures, resource ticks every 30s scaled to controlled bases, end-of-game scoreboard bonus
- **Alterac Valley** (40v40, lvl 51+): Tower captures (~50-100 CP each), Captain kills (~200 CP), boss (Drek'thar/Vanndar Stormpike) win (~1000 CP), graveyard captures, mine captures, Stormpike/Frostwolf rep stacking with rank progression

### Contribution Points (CP) calculation

At weekly reset (Tuesday):
1. Sum of all CP earned this week + 80% of last week's CP carryover = **Adjusted CP**
2. Adjusted CP determines this week's rank standing on the **server-wide ranked list**
3. Top X% of adjusted CP = top X% of ranks (server-relative ranking — your absolute CP doesn't fix your rank; it depends on what other players earn)

### Standing decay rule

**Each week (Tuesday reset)**: Your standing decays by 20%. You keep 80% of last week's CP into the new week. **No grace week** for AFKers. Players who don't grind at all for a week drop ~20% in CP, often costing 1-2 ranks.

**This is why the grind is sustained**: A player who reaches rank 13 in week 8 must continue grinding 60+ hour weeks through week 12-14 to reach rank 14, OR risk slipping back to rank 11-12 from decay alone.

## Time investment per rank

| Rank | Estimated weekly CP needed | Hours/week typical | Weeks from start |
|---|---|---|---|
| 6 (Knight/Stone Guard) | ~8,000 CP | 15-25 hours | Week 2-3 |
| 8 (Knight-Captain/Legionnaire) | ~30,000 CP | 30-40 hours | Week 4-5 |
| 10 (Lieutenant Commander/Champion) | ~80,000 CP | 50-60 hours | Week 6-7 |
| 12 (Marshal/General) | ~150,000 CP | 65-80 hours | Week 8-10 |
| 13 (Field Marshal/Warlord) | ~250,000 CP | 70-80 hours | Week 11-12 |
| **14 (Grand Marshal/High Warlord)** | **~300,000+ CP weekly to top the bracket** | **70-80+ hours** | **Week 13-14** (3-4 weeks of *just* rank 13→14 grind) |

`[verify pass 3 — exact CP thresholds vary by server population and competing players]`

**Realistic gameplay**: Rank 14 in 1.12.1 was reserved for **the few dozen most dedicated players per server** — typical guild push, with friends queuing alongside / setting up friendly kills (a controversial but common practice on retail).

## Rank rewards (gear unlocks)

The PvP rank vendor in **Stormwind / Orgrimmar** sells gear gated by rank. Notable progression:

| Rank | Reward gained | Notes |
|---|---|---|
| 3 (Sergeant) | First major piece available — tabard, minor consumables | |
| 7 (Knight-Lieutenant / Blood Guard) | **First epic gear set** — shoulders, helmets (rare/epic mix) | Major power spike |
| 8 (Knight-Captain / Legionnaire) | More epic gear pieces (chest, gloves) | |
| 10 (Lieutenant Commander / Champion) | **Mount equivalent** (~mount cost discount, special PvP-themed mount) | |
| 11 (Commander / Lieutenant General) | Insignia of the Alliance/Horde (PvP trinket) — Reset on use cleanses many CC | Rank 11+ trinket; meta-defining for Warriors/Rogues |
| 12 (Marshal / General) | More pieces (legs, boots) | |
| 13 (Field Marshal / Warlord) | More pieces; rare gear | |
| **14 (Grand Marshal / High Warlord)** | **Epic weapons** — Grand Marshal's / High Warlord's main-hand or 2H weapons (class-specific); **permanent epic title** that persists across faction transfers | **Terminal goal** |

### Rank 14 weapons (per class)

Each class has a unique Grand Marshal / High Warlord weapon at rank 14 (BoP, requires rank 14 only — never lost on demotion). Examples:

- **Warrior**: Grand Marshal's Battle Hammer (1H mace), Grand Marshal's Sunderer (2H mace), or class-specific 2H sword/axe
- **Hunter**: Grand Marshal's Tome (rifle/bow)
- **Rogue**: Grand Marshal's Stiletto (dagger), Grand Marshal's Sword (1H sword)
- **Priest**: Grand Marshal's Truncheon (1H mace)
- **Mage**: Grand Marshal's Glaive (1H sword) or wand
- **Warlock**: Grand Marshal's Glaive
- **Druid**: Grand Marshal's Stave (staff)
- **Paladin**: Grand Marshal's Hand of Justice (1H sword)
- **Shaman**: High Warlord's Crusher (1H mace) or 2H

These weapons are **competitive with Naxx weapons** for many classes (some are slightly worse than T3-era epics, but available much earlier in raid progression for non-raiders).

## PvP-specific rep grinds (BG-tied)

Each battleground has a paired faction grindable to Exalted:

| BG | Alliance faction | Horde faction | Exalted reward |
|---|---|---|---|
| Warsong Gulch | The Silverwing Sentinels | The Warsong Outriders | Epic shoulders + cloak |
| Arathi Basin | The League of Arathor | The Defilers | **Discount on epic 100% mount** + ring rewards |
| Alterac Valley | Stormpike Guard | Frostwolf Clan | **Ram / Wolf mount** + AV trinkets (Don Julio's Band, etc.) |

**Engine planning**: PvP-flagged characters should grind BG rep concurrent with rank progression. AB Exalted gives a **15% discount on epic mount** (saves ~150g vs Honored). AV Exalted gives the **Ram/Wolf mount** — a unique-color visual.

## World PvP

In 1.12.1, **world PvP has no formal mechanics** — no zone control objectives, no PvP rewards from open-world PvP outside of HK accumulation. Engine should not schedule "world PvP" actions as a primary objective; only as a side-effect of zone questing in contested areas (Hillsbrad, Arathi, EPL/WPL).

**Cross-version delta**: Classic 2019 added various PvP achievement mechanics; in 1.12.1 there are none.

## Strategic considerations

- **Premade groups**: Coordinated 5-man / 10-man / 15-man / 40-man premades dominate uncoordinated PUGs. A premade rank-pushing guild can earn 2-3x the CP/hour of solo-queuing players.
- **Honor farming sessions**: 4-6 hour BG marathons with breaks for queue dodging are standard for serious push.
- **Off-peak grinding**: Server population matters — at off-peak hours queue times are longer but kills against off-peak players grant similar CP.
- **AFK / multiboxing penalties**: 1.12.1 had primitive anti-AFK measures; multiboxing was technically permissible. Classic 2019 has stronger anti-AFK.
- **Rank 14 social cost**: The grind requires near-full-time gaming for 3 months; unsustainable for most players. Most "rank 14" alts in vanilla were guild-supported by rotating teams of friends grinding kills onto a single character.

## VMaNGOS / private server notes

- **Honor system mechanics work correctly** on most VMaNGOS forks.
- **CP-per-kill calculation** is faithful to 1.12.1 retail.
- **Weekly reset (Tuesday)** is configurable per server.
- **AV bonus honor + objective rewards** scripted reliably.
- **Server-population effect**: VMaNGOS servers have lower population than 1.12 retail had, which **shifts the CP curve**: easier to reach rank 14 with less competition (~6-10 weeks instead of 12-14).
- **Rank 14 weapon vendors** are correctly gated by rank flag.

## Decision-Engine Rules

- **id:** `pvp.role-flag` — IF `Account.PvPFlag.IsSet AND character.PvPRole == true` THEN PvP actions get +200 priority modifier across the board.
- **id:** `pvp.bg-queue-on-eligibility` — IF `Level >= 10 AND PvPRole AND BattlegroundsCompleted[WSG] == 0` THEN open WSG queue. Priority **600**.
- **id:** `pvp.bg-queue-ab` — IF `Level >= 20 AND PvPRole AND BattlegroundsCompleted[AB] == 0` THEN open AB queue. Priority **600**.
- **id:** `pvp.bg-queue-av` — IF `Level >= 51 AND PvPRole AND BattlegroundsCompleted[AV] == 0` THEN open AV queue. Priority **600**.
- **id:** `pvp.weekly-rank-target` — IF `Account.PvPRankPushActive AND Player.HonorThisWeek < TargetWeeklyCP(currentRank)` THEN prefer BG queue actions until weekly CP target met. Priority **750**.
- **id:** `pvp.rank-decay-rescue` — IF `PvPRole AND DaysSinceLastBG >= 4 AND HoursIntoCurrentWeek > 96` THEN warn that rank decay is approaching; recommend BG run. Priority **decision-request**.
- **id:** `pvp.av-rep-bundle` — IF `PvPRole AND Reputation[StormpikeGuard or FrostwolfClan] < Exalted` THEN AV queues automatically count toward Exalted rep grind. Priority **600** (bundles with rank push).
- **id:** `pvp.rank-14-grind-suspend` — IF `Account.PvPRankPush AND CurrentRank == 13 AND HoursThisWeek >= 60` THEN suspend non-BG actions. Priority **999** (rank-14 grinds dominate).
- **id:** `pvp.bg-quartermaster-buy` — IF `Reputation[ABFaction] >= Exalted AND PlanningEpicMount AND CopperOnHand >= ABMountCost*0.85` THEN buy AB epic mount via 15% rep discount. Priority **700**.

## Snapshot Fields Needed

- `Level`, `Class`, `Faction` (existing)
- `HonorThisWeek` (existing) — raw HK / contribution points this week
- `HonorRank` (existing) — current rank 0-14
- `HonorRankProgress` (planned — 0-100% within current rank for next-rank eligibility)
- `BattlegroundsCompleted[bgId]` (existing)
- `Reputation[StormpikeGuard | FrostwolfClan | SilverwingSentinels | WarsongOutriders | LeagueOfArathor | Defilers]` (existing under generic Reputation table)
- `Account.PvPFlag.IsSet` (planned config flag)
- `Account.PvPRankPushActive` (planned config flag — engine reads from account-plan)
- `DaysSinceLastBG` (planned — derived telemetry)
- `HoursThisWeek` (planned — derived from /played + last-week telemetry)
- `Player.IsInBG` (existing) — derived from world state

## Cross-references

- [decision-engine/state-flags.md](../decision-engine/state-flags.md) — `HonorThisWeek`, `HonorRank` fields
- [decision-engine/leveling-priority.md](../decision-engine/leveling-priority.md) — PvP role priority modifier
- [pvp/README.md](README.md) — pass 8 BG strategy file outline
- [classes/all-9-classes-summary.md](../classes/all-9-classes-summary.md) — PvP race-pick table per class
- [classes/rogue.md](../classes/rogue.md) — Undead WotF as canonical PvP racial
- [classes/priest.md](../classes/priest.md) — Undead/Troll as Horde PvP-best
- [reputations/](../reputations/) (pass 7) — AV / WSG / AB faction grind details
- [systems/world-buffs.md](../systems/world-buffs.md) — `worldbuff.no-pvp-during-window` rule (avoid losing world buffs to PvP deaths)
