# Onyxia Attunement (Alliance) — The Marshal Windsor Chain

> **Sources** (crawl date 2026-05-01):
> - https://www.icy-veins.com/wow-classic/onyxia-alliance-attunement-guide
> - https://www.wowhead.com/classic/guide/onyxia-onyxias-lair-attunement-drakefire-amulet-wow-classic
> - https://www.warcrafttavern.com/wow-classic/guides/onyxias-lair-attunement/
> - https://wowwiki-archive.fandom.com/wiki/Onyxia's_Lair_attunement (referenced via search)
>
> **Pass 2.** Some details (exact NPC coordinates in Stormwind Keep / Lakeshire for The True Masters, Crumpled Up Note drop rate) marked `[verify pass 3]`.
>
> **Version note.** All facts here describe live patch **1.12.1 (2006)**. Where Classic 2019 re-release differs, deltas are flagged inline. The 2006 baseline is authoritative.

## What it unlocks

The **Drakefire Amulet** — required neck-slot key item to enter **Onyxia's Lair** (40-man raid in Dustwallow Marsh). Without it, players cannot zone into Onyxia's Lair. The amulet is BoP and **must be in inventory or equipped** when entering the raid; players don't need to wear it as a stat-piece, but it must be in bags.

The Marshal Windsor chain is **the single longest attunement chain in vanilla 1.12.1** — 11 named quest steps spanning Burning Steppes / Stormwind / BRD / UBRS / Winterspring. It's also one of the most narratively memorable, culminating in the Stormwind throne-room masquerade event where Lady Prestor's Onyxia disguise is revealed.

## Prerequisites

- **Level 48** to start (Helendis Riverhorn requires it)
- **Level 52** to complete the Great Masquerade Stormwind escort step
- **Level 58+** practical for the final step (UBRS Drakkisath kill)
- BRD access (Master's Key recommended for prison)
- UBRS access (**Seal of Ascension** — see [seal-of-ascension.md](seal-of-ascension.md))
- Group of 5 minimum for BRD escort + UBRS run

## Chain step-by-step

### Phase 1 — Burning Steppes prep (Helendis Riverhorn)

| Step | Quest | NPC / Location | Objective | Notes |
|---|---|---|---|---|
| 1 | **Dragonkin Menace** | **Helendis Riverhorn** (Burning Steppes, southeast — near Morgan's Vigil) | Kill 15 Black Broodlings, 10 Black Dragonspawn, 4 Black Wyrmkin, 1 Black Drake | Lvl 48+ to accept; soloable at 50+ |
| 2 | **The True Masters** (multi-step) | Helendis Riverhorn → various NPCs across Stormwind Keep, Lakeshire, Burning Steppes | Visit a sequence of NPCs to gather intel | Travel-heavy step; 30-60 min /played |

### Phase 2 — Marshal Windsor BRD rescue

| Step | Quest | NPC / Location | Objective | Notes |
|---|---|---|---|---|
| 3 | **Marshal Windsor** | Helendis Riverhorn | Enter BRD, kill **High Interrogator Gerstahn** (drops **Prison Cell Key**), rescue Marshal Windsor from his prison cell | First BRD trip; 5-man recommended |
| 4 | **Abandoned Hope** | Marshal Windsor (in BRD prison) | Return to Marshal Maxwell at Morgan's Vigil (Burning Steppes) | Quick travel back |
| 5 | **A Crumpled Up Note** | Item drop from BRD trash mobs `[verify exact mob pass 3]` | Take note to Marshal Windsor | Drops during BRD run; passively collected |
| 6 | **A Shred of Hope** | Marshal Windsor (BRD) | Kill **Golem Lord Argelmach** AND **General Angerforge** in BRD | Both are BRD bosses, full BRD clear required for Angerforge |
| 7 | **Jail Break!** | Marshal Windsor (BRD) | **Escort Marshal Windsor out of the dungeon** (group required) | **5-man minimum.** Windsor walks slowly; multi-wave fights as he escapes; ~15-20 min escort. **VMaNGOS scripting note**: this escort has historically been buggy on private servers — engine should plan re-attempts. |

### Phase 3 — Stormwind masquerade

| Step | Quest | NPC / Location | Objective | Notes |
|---|---|---|---|---|
| 8 | **Stormwind Rendezvous** | **Marshal Maxwell** (Morgan's Vigil, Burning Steppes) | Speak with **Squire Rowe** near the gates of Stormwind | Travel back to SW |
| 9 | **The Great Masquerade** | **Squire Rowe** (Stormwind, near city gates) | **Escort Marshal Windsor through Stormwind** to the Royal Court; reveal Lady Prestor (Onyxia) in front of King Wrynn; **Bolvar Fordragon** assists in fighting hostile NPCs that spawn | **Lvl 52+ required.** ~5-10 min escort with Bolvar tanking. Multiple waves of hostile NPCs spawn during the throne-room reveal. **Iconic narrative event** — Onyxia escapes after revealing her identity. |

### Phase 4 — Drakkisath kill + Drakefire Amulet

| Step | Quest | NPC / Location | Objective | Notes |
|---|---|---|---|---|
| 10 | **The Dragon's Eye** | **Bolvar Fordragon** (Stormwind throne room, after Masquerade event) | Find **Haleh** in a cave **southwest of Everlook in Winterspring** | Travel to Winterspring; Haleh is a friendly Blue Dragon NPC in disguise |
| 11 | **Drakefire Amulet** | **Haleh** (Winterspring cave) | Kill **General Drakkisath in Upper Blackrock Spire** for **Blood of the Black Dragon Champion** → return to Haleh | **UBRS run with Drakkisath kill** is the final step. Drakkisath kill is **shared** with MC attune (drops Drakkisath's Brand for Lothos) and Onyxia attune (Blood of the Black Dragon Champion). One UBRS run completes both. |
| 12 | (Final hand-in) | Haleh | Receive **Drakefire Amulet** | BoP, neck slot, stats are mediocre — its value is the Onyxia raid access |

## Combined UBRS run optimization

Drakkisath drops **both** items needed simultaneously:

- **Drakkisath's Brand** (or quest item from his corpse) → MC attune via Lothos Riftwaker — see [molten-core.md](molten-core.md)
- **Blood of the Black Dragon Champion** → Onyxia attune via Haleh

**Engine optimization rule**: A single UBRS run with Drakkisath kill **completes both attunes simultaneously**. Engine should ensure both quests are accepted (Attunement to the Core from Lothos + Drakefire Amulet from Haleh) BEFORE the UBRS run, otherwise a second Drakkisath kill is needed (UBRS is a 5-day lockout `[verify pass 3]` — actually UBRS is "no lockout" but ID-shared in some patches; in 1.12.1 it's a normal 5-man dungeon with no lockout, just instance-rest).

Wait — UBRS is actually a **10-man raid in 1.12.1** (was changed from 15-man to 10-man in patch 1.10), with a **5-day lockout**. So a missed Drakkisath turn-in costs 5 real-time days before the next kill.

## Group requirements

- **Phase 2 BRD prison rescue**: 5-player group (full BRD experience)
- **Phase 2 BRD escort (Jail Break!)**: 5-player group (escort + waves)
- **Phase 3 Stormwind escort**: solo viable (Bolvar tanks); engine plans no group
- **Phase 4 UBRS Drakkisath**: 10-player raid group (UBRS is a 10-man instance in 1.12.1)

## Total /played time investment

- **Phase 1 Burning Steppes prep**: ~1-2 hours
- **Phase 2 BRD prison rescue + escort**: 2-3 hours (group BRD + Jail Break escort wave fights)
- **Phase 3 Stormwind masquerade**: 30 minutes
- **Phase 4 UBRS Drakkisath + Winterspring travel**: 2-3 hours (UBRS 10-man + travel)
- **Total**: ~6-10 hours /played, **multi-week** in real time due to coordinating BRD + UBRS groups

## VMaNGOS / private server notes

- **Phase 2 Jail Break! escort** has historically been the most-bugged step on private servers. Marshal Windsor pathing through BRD can fail; engine plans 1-2 re-attempts. `[verify VMaNGOS scripting status pass 3]`
- **Phase 3 Great Masquerade** in Stormwind is **scripted as a multi-wave NPC fight + dialogue event**. The event has narrative timing (multiple talk-pauses) — engine should not interrupt with combat actions during dialogue phases.
- **Bolvar Fordragon** is a powerful NPC tank during the Masquerade; can solo-tank most spawned mobs. Player can DPS / heal / off-tank stragglers.
- **Drakkisath kill** drops items reliably on VMaNGOS.
- **Drakefire Amulet** is correctly required as an inventory check on Onyxia's Lair zone-in.

## Decision-Engine Rules

- **id:** `attune.onyxia-alliance.faction-lock` — IF `Faction==Horde` THEN engine errors out — Alliance chain not applicable.
- **id:** `attune.onyxia-alliance.lvl-48-start` — IF `Faction==Alliance && Level>=48 && !QuestsCompleted.Contains(DragonkinMenace)` AND raid plan includes Onyxia THEN start chain at Helendis Riverhorn. Priority **750** at lvl 48 (early-start advantage; chain takes weeks).
- **id:** `attune.onyxia-alliance.brd-prison` — IF `QuestsInProgress.Contains(MarshalWindsor) && BRDGroupReady` THEN run BRD to rescue Windsor + complete A Shred of Hope (Argelmach + Angerforge kills) + Jail Break! escort. Priority **800**.
- **id:** `attune.onyxia-alliance.bundle-with-drakkisath` — IF `QuestsInProgress.Contains(DrakefireAmulet) && QuestsInProgress.Contains(AttunementToTheCore)` AND UBRS group ready THEN ensure both quests accepted before Drakkisath kill (single kill completes BOTH MC and Onyxia attunes). Priority **999** (don't waste UBRS lockout). **Critical optimization.**
- **id:** `attune.onyxia-alliance.masquerade-solo` — IF `QuestsInProgress.Contains(TheGreatMasquerade)` THEN solo Stormwind escort with Bolvar tanking. Priority **800**. **Don't bring group** — Bolvar handles waves.
- **id:** `attune.onyxia-alliance.amulet-required-for-raid` — IF `RaidScheduledOnyxia && !Items.Contains(DrakefireAmulet)` THEN raid action suppressed. Priority **999** (Onyxia zone-in fails without amulet).

## Snapshot Fields Needed

- `Level`, `Class`, `Faction` (existing)
- `QuestsInProgress` / `QuestsCompleted` (existing) — quest IDs for all 11 chain steps `[verify pass 3]`:
  - Dragonkin Menace, The True Masters (multiple), Marshal Windsor, Abandoned Hope, A Crumpled Up Note, A Shred of Hope, Jail Break!, Stormwind Rendezvous, The Great Masquerade, The Dragon's Eye, Drakefire Amulet
- `Items.Contains(itemId)` — Prison Cell Key (BRD), A Crumpled Up Note (transient), Blood of the Black Dragon Champion (Drakkisath drop), **Drakefire Amulet** (final reward)
- `Attunements.Contains(Onyxia)` (planned — set true when Drakefire Amulet obtained)
- `Attunements.Contains(MoltenCore)` (existing/planned — engine pairs with Onyxia attune for combined UBRS run)
- `BRDGroupReady` / `UBRSGroupReady` (planned — derived from PartyComposition + group state)

## Cross-references

- [attunements/seal-of-ascension.md](seal-of-ascension.md) — UBRS attunement (prerequisite for Drakkisath kill)
- [attunements/molten-core.md](molten-core.md) — MC attunement; **share UBRS Drakkisath kill** for both attunes
- [attunements/onyxia-horde.md](#) (next iteration) — Horde version of the Onyxia chain (different NPCs, similar structure)
- [decision-engine/unlock-graph.md](../decision-engine/unlock-graph.md) — `attune.onyxia` node + UBRS prerequisite
- [decision-engine/per-bracket-actions/06-l55-l60.md](../decision-engine/per-bracket-actions/06-l55-l60.md) — Onyxia attune is a critical-path priority **800** action
- [dungeons/blackrock-depths.md](#) (pass 4) — BRD dungeon (Argelmach, Angerforge, Gerstahn locations)
- [dungeons/upper-blackrock-spire.md](#) (pass 4) — UBRS dungeon (Drakkisath fight)
- [zones/burning-steppes.md](#) (pass 3) — Helendis Riverhorn / Marshal Maxwell hub
- [zones/winterspring.md](#) (pass 3) — Haleh's cave near Everlook
- [raids/onyxias-lair.md](#) (pass 5) — Onyxia raid encounter (after attunement)
