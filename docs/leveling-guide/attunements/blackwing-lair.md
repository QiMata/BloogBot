# Blackwing Lair (BWL) Attunement — Blackhand's Command

> **Sources** (crawl date 2026-05-01):
> - https://www.wowhead.com/classic/guide/blackwing-lair-attunement-blackhands-command-classic-wow
> - https://www.warcrafttavern.com/wow-classic/guides/bwl-attunement/
> - https://wowwiki-archive.fandom.com/wiki/Orb_of_Command (referenced via search)
> - https://project-ascension.fandom.com/wiki/Guide_to_Classic_WoW_Raid_Attunements/Blackwing_Lair (referenced via search)
>
> **Pass 2.** Some details (Scarshield Quartermaster spawn timer, Mark of Drakkisath debuff duration) marked `[verify pass 3]`.
>
> **Version note.** All facts here describe live patch **1.12.1 (2006)**. Where Classic 2019 re-release differs, deltas are flagged inline. The 2006 baseline is authoritative.

## What it unlocks

The **Mark of Drakkisath** flag (granted by touching **Drakkisath's Brand orb** after killing Drakkisath in UBRS) enables a player to use the **Orb of Command** in BRS to teleport directly into **Blackwing Lair**. Without the flag, the Orb of Command is inert.

BWL is the **40-man Tier 2 raid** — Nefarian (Onyxia's brother) and his dragonkin minions; second raid in vanilla progression after MC.

## Three Drakkisath-related items — clarification

A common source of confusion: **three different items** are obtained around Drakkisath in UBRS, each for a different attunement:

| Item | Source | What it's for |
|---|---|---|
| **Blood of the Black Dragon Champion** | Drops from Drakkisath's corpse on kill | **Onyxia attunement** (final step turn-in to Haleh / Rexxar) — see [onyxia-alliance.md](onyxia-alliance.md) / [onyxia-horde.md](onyxia-horde.md) |
| **Drakkisath's Brand** (the orb behind Drakkisath) | Click the orb after Drakkisath dies | **BWL attunement** (this file) — applies Mark of Drakkisath flag |
| **Core Fragment** (NOT a Drakkisath item) | Click the Core Fragment object inside BRD near MC portal — **separate from UBRS entirely** | **MC attunement** — see [molten-core.md](molten-core.md). Drakkisath kill is NOT required for MC. |

**Engine optimization rule**: A single UBRS run with Drakkisath kill **completes BOTH BWL and Onyxia attunes simultaneously** if Blackhand's Command quest is accepted before the run AND Onyxia attune chain is at the Drakkisath step. The MC Core Fragment is a separate trip into BRD.

## Prerequisites

- **Level 60** strongly recommended (UBRS Drakkisath fight)
- **UBRS access** (Seal of Ascension OR 5-player ritual at door — see [seal-of-ascension.md](seal-of-ascension.md))
- 10-player UBRS raid group capable of clearing to Drakkisath
- Quest **Blackhand's Command** picked up before entering UBRS

## Chain step-by-step

### Phase 1 — Pick up Blackhand's Command (the quest)

| Step | NPC / Location | Goal | Notes |
|---|---|---|---|
| 1 | **Scarshield Quartermaster** in the **BRS upper hallway** (between the Blackrock Spire instance portal and the Orb of Command — **outside** the UBRS instance, in the open BRS area) | Kill him + his Scarshield warrior escort | Spawn point is the corridor leading from BRM lobby toward the LBRS/UBRS portals; Quartermaster patrols with 2-3 elite Scarshield warriors. |
| 2 | Loot **Blackhand's Command** from Quartermaster's corpse | Quest item, world drop | Must be looted from Quartermaster — does not drop from other Scarshield mobs |
| 3 | Right-click **Blackhand's Command** in inventory | Activates the quest **Blackhand's Command** | The quest auto-targets the Drakkisath kill objective |

### Phase 2 — Kill Drakkisath in UBRS + touch the orb

| Step | NPC / Location | Goal | Notes |
|---|---|---|---|
| 4 | Enter **UBRS** (via Seal of Ascension OR 5-player ritual) | Standard UBRS clear path | Skip optional bosses; focus on path to Drakkisath at the back of the instance |
| 5 | **Beasts** (Magmadar pack), **Solakar Flamewreath** (5-wave summoning), **Pyroguard Emberseer** (chained-up boss; Cenarion Hold quest), **Goraluk Anvilcrack** (rare elite — drops Sulfuron Hammer recipe), **Warchief Rend Blackhand** (Horde Onyxia attune step), reach **General Drakkisath** chamber | Standard UBRS bosses | Drakkisath is the final boss before the back orb chamber |
| 6 | Kill **General Drakkisath** | Quest progress + drops **Blood of the Black Dragon Champion** for any Onyxia chain players in the group | |
| 7 | Walk past Drakkisath's corpse to the **glowing orb behind him** = **Drakkisath's Brand** | Click the orb | Applies the **Mark of Drakkisath** flag/debuff (visible in player buff list — confirms attune) |
| 8 | Quest **Blackhand's Command** auto-completes when the orb is touched | No NPC turn-in required — the orb interaction is the final step | Mark of Drakkisath persists indefinitely (no decay timer) |

### Phase 3 — Use the Orb of Command for BWL access

After attune, the **Orb of Command** in the corridor where Scarshield Quartermaster was killed becomes usable:

| Step | Action | Result |
|---|---|---|
| 9 | Travel back to **Orb of Command** in BRS hallway (or fly to BRM next time) | Orb glows differently for attuned players |
| 10 | Click the Orb of Command | Teleports directly into **Blackwing Lair**, bypassing further travel |

The Orb of Command works for **40-man raid synchronization** — the entire raid teleports in via individual orb-clicks; Warlock summons + Mage portals can also bring unattuned players to the orb (but those players cannot use the orb without the attune).

## Group requirements

- **Phase 1 Scarshield Quartermaster**: 5-man recommended (Quartermaster + escort are lvl 60-61 elites; 2-3 player group can solo with cooldowns)
- **Phase 2 UBRS Drakkisath**: 10-player raid group (full UBRS clear)
- **Phase 3 Orb of Command + BWL entry**: 40-player raid group

## Total /played time investment

- **Phase 1 Scarshield Quartermaster**: 15-30 minutes (find + kill + loot)
- **Phase 2 UBRS Drakkisath**: ~2-3 hours (typical UBRS clear time)
- **Phase 3 BWL entry travel**: ~5 minutes
- **Total**: ~2.5-3.5 hours /played for a single character; usually completed alongside Onyxia attune in the same UBRS run

## VMaNGOS / private server notes

- **Scarshield Quartermaster** spawn is correctly scripted on VMaNGOS (~30-min respawn; sometimes contested with other guilds farming Blackhand's Command).
- **Drakkisath's Brand orb interaction** works correctly post-Drakkisath-kill.
- **Orb of Command** correctly checks Mark of Drakkisath flag and refuses unattuned players.
- **No race-by-race or class-by-class differences** — single chain applies to all 9 classes / 8 races.

## Decision-Engine Rules

- **id:** `attune.bwl.scarshield-quartermaster` — IF `Level==60 && !QuestsInProgress.Contains(BlackhandsCommand) && !Items.Contains(BlackhandsCommand)` AND in BRS corridor THEN find + kill Scarshield Quartermaster. Priority **750**.
- **id:** `attune.bwl.activate-quest` — IF `Items.Contains(BlackhandsCommand)` THEN right-click to activate quest before UBRS entry. Priority **800** (must accept quest before Drakkisath kill, else quest doesn't auto-complete).
- **id:** `attune.bwl.drakkisath-orb` — IF `QuestsInProgress.Contains(BlackhandsCommand) && Drakkisath.IsDead && PartyAtBackOrbChamber` THEN click Drakkisath's Brand orb. Priority **820** (combat-time, immediately after Drakkisath dies).
- **id:** `attune.bwl.bundle-with-onyxia-and-mc` — IF `QuestsInProgress.Contains(BlackhandsCommand) && QuestsInProgress.Contains(BloodOfBlackDragonChampion)` AND UBRS group ready THEN single Drakkisath kill completes BOTH attunes. Priority **999** (don't waste UBRS lockout). **Critical optimization across attunes.**
- **id:** `attune.bwl.completion-prereq-raid` — IF `RaidScheduledBWL && !Attunements.Contains(BlackwingLair)` THEN raid action suppressed. Priority **999** (BWL Orb of Command is unusable without Mark of Drakkisath).
- **id:** `attune.bwl.requires-ubrs-attune` — IF `!Items.Contains(SealOfAscension) && BWL.AttunePlanned` THEN engine plans Seal of Ascension chain BEFORE BWL attune attempt (UBRS access mandatory for Drakkisath kill). Priority **820**.

## Snapshot Fields Needed

- `Level` (existing)
- `QuestsInProgress` / `QuestsCompleted` (existing) — Blackhand's Command quest ID `[verify pass 3]`
- `Items.Contains(itemId)` — Blackhand's Command (transient quest item)
- `Spells.Contains(MarkOfDrakkisath)` (planned — derived from buff scan; persistent flag after orb touch)
- `Attunements.Contains(BlackwingLair)` (planned — derived from Mark of Drakkisath flag)
- `Drakkisath.IsDead` (planned — derived from boss-encounter state during current UBRS instance)
- `PartyAtBackOrbChamber` (planned — derived from player position relative to Drakkisath chamber)

## Cross-references

- [attunements/seal-of-ascension.md](seal-of-ascension.md) — UBRS attunement (prerequisite); Drakkisath kill is shared with multiple chains
- [attunements/molten-core.md](molten-core.md) — MC attune via Core Fragment (NOT Drakkisath); cleared up the 3-Drakkisath-items confusion
- [attunements/onyxia-alliance.md](onyxia-alliance.md) — Onyxia (Alliance) Drakkisath kill drops Blood of Black Dragon Champion
- [attunements/onyxia-horde.md](onyxia-horde.md) — Onyxia (Horde) Drakkisath kill drops Blood of Black Dragon Champion
- [decision-engine/unlock-graph.md](../decision-engine/unlock-graph.md) — `attune.bwl` node + UBRS prereq + Drakkisath kill shared dependency
- [decision-engine/per-bracket-actions/06-l55-l60.md](../decision-engine/per-bracket-actions/06-l55-l60.md) — BWL attune is a critical-path priority **800** action at lvl 60
- [dungeons/upper-blackrock-spire.md](#) (pass 4) — UBRS dungeon (Drakkisath fight)
- [raids/blackwing-lair.md](#) (pass 5) — BWL raid encounters (after attunement)
