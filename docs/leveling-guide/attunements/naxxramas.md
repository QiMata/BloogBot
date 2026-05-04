# Naxxramas Attunement — The Dread Citadel

> **Sources** (crawl date 2026-05-01):
> - https://www.wowhead.com/classic/news/how-to-get-attuned-to-naxxramas-argent-dawn-reputation-materials-and-gold-cost-378559
> - https://www.wowhead.com/classic/news/how-to-get-attuned-to-wow-classic-naxxramas-argent-dawn-rep-materials-gold-318847
> - https://classicdb.ch/?quest=9122 (referenced via search)
> - https://realsport101.com/article/classic-wow-naxxramas-attunement-how-to-get-naxx-attuned-argent-dawn-rep-gold-righteous-orb-the-dread-citadel
> - https://project-ascension.fandom.com/wiki/Guide_to_Classic_WoW_Raid_Attunements/Naxxramas (referenced via search)
>
> **Pass 2.** Some details (exact AD rep-per-hour grind rates, Eye of Shadow Tailoring recipe) marked `[verify pass 3]`.
>
> **Version note.** All facts here describe live patch **1.12.1 (2006)** plus patch 1.11 (which added Naxxramas, June 2006). Where Classic 2019 re-release differs, deltas are flagged inline. The 2006 baseline is authoritative.

## What it unlocks

The **Naxxramas attunement** flag, granted by completing the quest **"The Dread Citadel - Naxxramas"**, allows entry to the **Naxxramas raid** (40-man, lvl 60, the floating necropolis above Stratholme — the final raid of vanilla 1.12). Naxxramas is the Tier 3 source raid, providing **Dreadnaught/Cryptstalker/Bonescythe/etc.** sets for all 9 classes.

## Important version-correction — Honored, NOT Revered

A common misconception is that Naxx requires **Revered** Argent Dawn rep. **This is incorrect**. The minimum is **Honored**, with higher rep tiers reducing the gold + materials cost:

| AD Rep Tier | Gold cost | Materials | Strategy |
|---|---|---|---|
| **Honored** | **60g** | 5 Arcane Crystals + 2 Nexus Crystals + 1 Righteous Orb | Cheapest rep gate, but costliest in mats/gold |
| **Revered** | **30g** | 2 Arcane Crystals + 1 Nexus Crystal | Mid-tier; recommended trade-off |
| **Exalted** | **0g** | None | Most rep-effort but zero attune cost |

**Engine optimization rule**: For a single character grinding Naxx attune, **Honored is the fastest path-to-attune**. For an account with multiple toons that need Naxx attune, push to **Revered** on a single AD rep grinder (avoids redundant 60g + mat sinks per char). Push to **Exalted** only if AD reputation is otherwise needed (e.g., AD tabard, AD insignia trinket farming).

## Why the attune cost is materials-heavy

Unlike other attunes (Seal of Ascension, MC Core Fragment, Onyxia Marshal Windsor) which are quest-based, **Naxx attune is essentially a paid raid-pass** with a small AD-rep gate. The materials list at Honored:

- **5 Arcane Crystals** — herbed from Truesilver / Mithril nodes (Burning Steppes, Winterspring, Silithus, EPL); ~15-30g per crystal at AH price
- **2 Nexus Crystals** — disenchant of higher-tier epics (typically 5-10g per crystal at AH), or world drop
- **1 Righteous Orb** — drops from Stratholme Live and UD bosses (Postmaster, Ramstein, Baron Rivendare, Magistrate Barthilas) at low %; common AH item ~5-15g

**Total Honored cost approximately**: 60g attune + 75-150g in mats = **~135-210g** total. Engine should plan to budget this in working capital before reaching Naxx attune.

## Prerequisites

- **Level 60**
- **Argent Dawn rep ≥ Honored** (minimum)
- 60g + Arcane Crystals + Nexus Crystals + Righteous Orb in inventory
- Travel to Light's Hope Chapel in Eastern Plaguelands

## Argent Dawn rep grind to Honored

| Method | Rep gain | /played | Notes |
|---|---|---|---|
| **Stratholme Live + UD dungeon kills** | ~30-50 rep per elite boss | ~6-10 hours to Honored | Bosses drop AD rep until Exalted; trash drops drop off at Honored |
| **Scholomance dungeon kills** | similar to Strat | ~6-10 hours | |
| **EPL quests** | one-time large rep grants per quest completion | ~2-4 hours of quest-pickup | Light's Hope Chapel hub — most quests give 250-500 rep each |
| **Scourgestone turn-ins** | 25 rep per Scourgestone (Argent Dawn Insignia) | Variable | Stones drop from EPL/WPL undead mobs; turn in stacks of 3 |
| **Bone Fragment turn-ins** | 25 rep per stack | Variable | Bone Fragments drop from undead in Plaguelands |

**Engine path**: 1-2 Strat clears + EPL quest hub completion typically gets a fresh-60 to Honored in ~6-12 hours /played. Combine with Light's Hope Chapel quartermaster purchases (AD insignias, tabards) for additional rep utility.

## Chain step-by-step

| Step | NPC / Location | Goal | Notes |
|---|---|---|---|
| 1 | **Archmage Angela Dosantos** at **Light's Hope Chapel** (Eastern Plaguelands, ~80,60 — central neutral hub) | Talk to her with Honored AD rep + materials | Quest **The Dread Citadel - Naxxramas** |
| 2 | Hand over 60g (Honored cost) + 5 Arcane Crystals + 2 Nexus Crystals + 1 Righteous Orb | Pay the attune cost | All consumed; non-refundable |
| 3 | Receive **Naxxramas attunement flag** | Persistent attune | Travel to Stratholme + use the attunement teleport NPC near Light's Hope to enter Naxx (or fly to the floating necropolis above Stratholme) |

**Travel into Naxx**: After attune, Naxxramas is reached via a **dedicated teleport NPC near Light's Hope Chapel** (or a flying-mount-only chamber in patch 1.11 retail; **VMaNGOS handles teleport-only**). Once in, the raid runs 40-man with the four wings (Spider/Plague/Military/Construct) plus Sapphiron + Kel'Thuzad final.

## VMaNGOS / private server notes — critical caveat

**Naxxramas is famously incomplete on most private servers** — the final 1.12.1 raid was added very late in vanilla (patch 1.11, June 2006), and Blizzard's scripting for many encounters wasn't fully captured before the official 1.12.1 server retirement.

**VMaNGOS implementation status (as of recent forks)**:

- **Spider Wing** (Anub'Rekhan, Faerlina, Maexxna): largely complete and playable
- **Plague Wing** (Noth, Heigan the Unclean, Loatheb): mostly complete; Heigan's dance is challenging on some forks
- **Military Wing** (Razuvious, Gothik, Four Horsemen): complete on most forks; Razuvious requires Mind Control mechanics
- **Construct Wing** (Patchwerk, Grobbulus, Gluth, Thaddius): complete
- **Sapphiron**: variable across forks; ice block + life drain mechanics sometimes incomplete
- **Kel'Thuzad**: variable; full encounter scripting is the most-incomplete on private servers

**Engine ServerCapabilities flag**: The DecisionEngine should consult `ServerCapabilities.Naxx40Implemented` config flag before scheduling Naxx raid actions. On servers where Naxx is not fully implemented, raid plans should suspend Naxx-specific objectives. See [sections/00-questions-and-answers.md Q2](../sections/00-questions-and-answers.md#q2--how-does-the-engine-handle-vmangos-vs-retail-1121-divergence-on-raid-content).

## Eye of Shadow connection

The **Eye of Shadow** item (drops from Lord Kazzak in Blasted Lands and shares its name with the Onyxia attune item) is **also a Tailoring/Enchanting reagent** for some Naxx-related crafted items but **is NOT directly part of the Naxx attune**. Engine should not confuse the two items.

## Group requirements

- **Phase 1 AD rep grind**: 5-man Stratholme/Scholomance runs
- **Phase 2 attune turn-in at Light's Hope**: solo (just hand in mats + gold)

## Total /played time investment

- **AD rep grind to Honored**: 6-12 hours /played (concurrent with farming pre-raid BiS gear and lvl-60 dungeons)
- **Material gathering**: variable (5-10 hours if buying from AH; 15-30 hours self-farming)
- **Attune turn-in**: 5 minutes
- **Total to first Naxx entry**: typically 2-4 weeks real-time after dinging 60, since AD rep grind overlaps with general dungeon progression

## Decision-Engine Rules

- **id:** `attune.naxx.lvl-gate` — IF `Level<60` THEN suspend Naxx attune planning.
- **id:** `attune.naxx.ad-rep-grind` — IF `Level==60 && Reputation[ArgentDawn]<Honored && Account.RaiderPlanIncludesNaxx` THEN start AD rep grind via EPL quests + Strat/Scholo dungeon runs. Priority **400** (background; bundled with general dungeon progression).
- **id:** `attune.naxx.material-stockpile` — IF `Reputation[ArgentDawn]>=Honored && (Items.ArcaneCrystalCount<5 OR Items.NexusCrystalCount<2 OR !Items.Contains(RighteousOrb))` THEN gather materials (AH purchase OR farm Strat for Righteous Orbs OR herbalism for Arcane Crystals). Priority **500**.
- **id:** `attune.naxx.dread-citadel-quest` — IF `Reputation[ArgentDawn]>=Honored && Items.HasAllNaxxMats && CopperOnHand>=NaxxAttuneCost` AND in EPL THEN visit Archmage Angela Dosantos at Light's Hope Chapel. Priority **820**.
- **id:** `attune.naxx.rep-tier-trade-off` — IF `Reputation[ArgentDawn]>=Honored && Reputation[ArgentDawn]<Revered && CopperOnHand<NaxxAttuneCostHonored` THEN engine flags decision request: continue rep grind to Revered for cheaper attune, OR farm gold for Honored cost. Priority **decision-request** (account-level planner).
- **id:** `attune.naxx.completion-prereq-raid` — IF `RaidScheduledNaxx && !Attunements.Contains(Naxxramas)` THEN raid action suppressed. Priority **999**.
- **id:** `attune.naxx.server-capabilities-check` — IF `RaidScheduledNaxx && ServerCapabilities.Naxx40Implemented==false` THEN suspend Naxx raid action. Priority **999** (never schedule a raid the server cannot complete).

## Snapshot Fields Needed

- `Level`, `Class` (existing)
- `Reputation[ArgentDawn]` (existing) — `Hostile..Exalted` standing
- `CopperOnHand` (existing) — gates 60g/30g/0g attune cost based on rep tier
- `Items.ArcaneCrystalCount` (planned — bag scan; helper)
- `Items.NexusCrystalCount` (planned — bag scan)
- `Items.Contains(RighteousOrb)` (planned)
- `Items.HasAllNaxxMats` (planned helper — derived from above)
- `QuestsInProgress` / `QuestsCompleted` (existing) — TheDreadCitadelNaxxramas quest ID `9122` per Wowhead
- `Attunements.Contains(Naxxramas)` (planned — set true when quest completed)
- `Account.RaiderPlanIncludesNaxx` (planned — config flag)
- `ServerCapabilities.Naxx40Implemented` (planned — server config; engine reads on startup)

## Cross-references

- [attunements/seal-of-ascension.md](seal-of-ascension.md) — UBRS attunement (different chain, but shares many of the same lvl-60 prep activities)
- [attunements/molten-core.md](molten-core.md) — MC attune
- [attunements/onyxia-alliance.md](onyxia-alliance.md), [attunements/onyxia-horde.md](onyxia-horde.md) — Onyxia chains
- [attunements/blackwing-lair.md](blackwing-lair.md) — BWL chain
- [decision-engine/unlock-graph.md](../decision-engine/unlock-graph.md) — `attune.naxx` node + AD-rep prerequisite + ServerCapabilities check
- [decision-engine/per-bracket-actions/06-l55-l60.md](../decision-engine/per-bracket-actions/06-l55-l60.md) — Naxx attune is priority **720** at lvl 60
- [reputations/argent-dawn.md](#) (pass 7) — Argent Dawn rep grind methods + tabard + insignia trinket
- [zones/eastern-plaguelands.md](#) (pass 3) — Light's Hope Chapel hub
- [raids/naxxramas.md](#) (pass 5) — Naxxramas raid encounters (after attunement) + VMaNGOS implementation per-encounter
- [sections/00-questions-and-answers.md Q2](../sections/00-questions-and-answers.md) — open question about VMaNGOS / Naxx implementation completeness
