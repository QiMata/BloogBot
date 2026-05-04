# Molten Core Attunement — Attunement to the Core

> **Sources** (crawl date 2026-05-01):
> - https://www.wowhead.com/classic/quest=7848/attunement-to-the-core
> - https://www.warcrafttavern.com/wow-classic/guides/molten-core-attunement/ (referenced via search)
> - https://wowpedia.fandom.com/wiki/Attunement_to_the_Core (referenced via search)
> - https://wowpedia.fandom.com/wiki/Lothos_Riftwaker (referenced via search)
>
> **Pass 2.** Some details (exact BRD path through Shadowforge City, group-stealth tactics) marked `[verify pass 3]`.
>
> **Version note.** All facts here describe live patch **1.12.1 (2006)**. Where Classic 2019 re-release differs, deltas are flagged inline. The 2006 baseline is authoritative.

## What it unlocks

The **Attunement to the Core** spell, granted as the quest reward, gives players a self-cast teleport that delivers them directly to the Molten Core instance entrance from anywhere in the world. **For raid logistics this saves ~10-15 minutes per raid invite** vs walking through BRD ramps + side passages every time.

| MC attunement provides | Why it matters |
|---|---|
| **Self-teleport to MC entrance** | Skip the BRD walk; cuts pre-raid setup |
| **Permanent raid summon target** | Warlocks can summon to MC without first running through BRD |
| **Required for MC raid participation** | An unattuned player MUST walk through BRD to reach the MC portal — possible but slow; engine should require attune for raid characters |

## Important correction — *not* the Drakkisath path

A common misconception is that MC attunement requires killing **General Drakkisath in UBRS**. **This is incorrect.** Drakkisath's death + his head item (**Drakkisath's Brand**) is a step for the **Onyxia attunement chain** (both factions), not for MC.

The MC attunement is purely the **Core Fragment retrieval** path described below. There is no alternative path. The Hand of the Marshal NPC chain (occasionally cited as an alternative) is **also part of the Onyxia attunement chain**, not MC.

## Prerequisites

- **Level 55+** (Lothos Riftwaker requires lvl 55 to give the quest)
- BRD access — either:
  - **Master's Key** (Blackrock Mountain orb access between LBRS / UBRS / MC), OR
  - **Walking through BRM ramps** — slower but doesn't require the key
- A small group recommended (2-3 players minimum) for safety; a single stealthed player or Mage with Invisibility/Frost Nova kiting can make the run solo

## Chain step-by-step

### Phase 1 — Find Lothos Riftwaker

| Step | NPC / Location | Goal | Notes |
|---|---|---|---|
| 1.1 | **Lothos Riftwaker** in **Blackrock Mountain lobby** (the central chamber where the BRD/MC/LBRS/UBRS portals all meet; Lothos stands at the center near the MC portal entrance) | Talk to Lothos | He's an elf NPC, hard to miss in the BRM hub. **Quest giver location near MC portal entry** — same area where players will return after retrieving the fragment. |
| 1.2 | Accept **Attunement to the Core** (quest ID 7848) | Quest acceptance | Requires lvl 55. |

### Phase 2 — Retrieve the Core Fragment from BRD

The Core Fragment is a **clickable object** near the actual MC instance portal **inside Blackrock Depths**. The journey:

| Step | Path | Notes |
|---|---|---|
| 2.1 | Enter **Blackrock Depths** via the orb in BRM (requires Master's Key) OR walk through the BRD entrance | Master's Key path is fastest |
| 2.2 | Traverse BRD: **Detention Block → Halls of Crafting → Lyceum → Shadowforge City → Imperial Hall area** `[verify exact path pass 3]` | Standard BRD path; majority of trash is avoidable with sneak / Invisibility / Mage Frost Nova kiting |
| 2.3 | Past the Imperial Hall, take a side passage that leads to the **molten lava chamber containing the MC entrance portal** | The MC portal is at the **far back of BRD** in a chamber separate from Emperor Dagran Thaurissan's throne |
| 2.4 | Near the MC entrance portal, find and click the **Core Fragment** object | Quest item appears in inventory; **single-use, BoP** |
| 2.5 | Return to Lothos Riftwaker via Hearthstone or BRM exit + outdoor walk | Path back is shorter via Hearthstone if bound to BRM-area inn |

**Group tactics**:
- **Stealth class** (Rogue or Druid Cat Form) can solo the run by avoiding all combat — stealth past trash, click fragment, hearth out.
- **Mage** can Invisibility-pot their way through trash; Frost Nova + Blink + Ice Block as panic buttons.
- **2-3 player group** runs through, killing only patrols that block the path; full BRD clear is unnecessary.
- **Full 5-man** if also doing other BRD objectives (Shadowforge Key chain, Ironfoe drop run, etc.).

### Phase 3 — Turn in to Lothos Riftwaker

| Step | NPC / Location | Reward | Notes |
|---|---|---|---|
| 3.1 | Return to **Lothos Riftwaker** in BRM lobby | Hand in Core Fragment | |
| 3.2 | Receive **Attunement to the Core** spell | Self-teleport to MC entrance, no cooldown other than spell-cast time, **persistent raid attune flag** | |

## Group requirements

- **Solo viable** for stealth classes or Mages with consumable supply (Invisibility Potions, Free Action Potions)
- **2-3 player** recommended for non-stealth classes
- **No bosses required** — the Core Fragment is a clickable object, not a drop. Engine plans this as a **quick run** (~30-60 minutes /played).

## Total /played time investment

- **Best case (stealth class)**: 20-30 minutes for a single careful BRD run
- **Average (group of 2-3)**: 40-60 minutes
- **Bundling with other BRD objectives** (Shadowforge Key, Ironfoe / Hand of Justice chest farms): can be done concurrently if a 5-man group is running BRD anyway

## VMaNGOS / private server notes

- **Lothos Riftwaker** is fully scripted on VMaNGOS at the BRM lobby.
- **Core Fragment object** is correctly placed near MC entrance portal inside BRD; click interaction works.
- **Attunement to the Core** teleport spell delivers to MC entrance correctly.
- **No race-by-race or class-by-class differences** — single quest path applies to all 9 classes / 8 races.

## Decision-Engine Rules

- **id:** `attune.mc.lvl-gate` — IF `Level<55` THEN suspend MC attune planning. Engine waits until ding 55.
- **id:** `attune.mc.start-quest` — IF `Level>=55 && !QuestsCompleted.Contains(AttunementToTheCore)` AND in BRM lobby THEN talk to Lothos Riftwaker. Priority **820** (raid-prep critical-path).
- **id:** `attune.mc.run-brd` — IF `QuestsInProgress.Contains(AttunementToTheCore)` THEN plan BRD run for Core Fragment. Priority **820**. **Bundle with Master's Key acquisition** (if not already obtained) and other BRD farm targets (Hand of Justice trinket, Shadowforge Key for Pal/Warlock chains).
- **id:** `attune.mc.solo-stealth` — IF `Class IN {Rogue, Druid (Feral)}` AND `Level>=58` THEN solo the run via stealth. Priority **800** (cost-efficient single-player completion).
- **id:** `attune.mc.solo-mage` — IF `Class==Mage && Level>=60 && Items.Contains(InvisibilityPotion)` THEN solo via Invisibility kiting. Priority **800**.
- **id:** `attune.mc.bundle-with-master-key` — IF `!KeysInBags.Contains(MastersKey) && !Attunements.Contains(MoltenCore)` THEN engine plans Master's Key chain (BRD lower) BEFORE attempting MC attune (saves a BRM-walk vs orb-port). Priority **700** (logistics optimization).
- **id:** `attune.mc.completion-prereq-raid` — IF `RaidScheduledMC && !Attunements.Contains(MoltenCore)` THEN MC attune is **mandatory blocker**; raid action suppressed. Priority **999** (do not enter MC unattuned — wastes raid logistics).

## Snapshot Fields Needed

- `Level`, `Class` (existing)
- `QuestsInProgress` / `QuestsCompleted` (existing) — **AttunementToTheCore** quest ID `7848`
- `Items.Contains(itemId)` — Core Fragment (transient quest item; in-bag during chain)
- `Spells.Contains(AttunementToTheCore)` (planned — derived from quest completion or spellbook scan)
- `Attunements.Contains(MoltenCore)` (planned — set true on quest completion; stays persistent)
- `KeysInBags.Contains(MastersKey)` (existing)
- `Items.Contains(InvisibilityPotion)` (planned — for Mage solo run rule)

## Cross-references

- [attunements/seal-of-ascension.md](seal-of-ascension.md) — UBRS attunement (Drakkisath kill) is the **Onyxia attune step**, NOT MC; common confusion clarified
- [attunements/onyxia-alliance.md](#) (next iteration) — Onyxia attune via Marshal Windsor + Drakkisath head
- [attunements/onyxia-horde.md](#) (later iteration) — Onyxia attune via Eitrigg/Rexxar + Drakkisath head
- [attunements/blackwing-lair.md](#) (later iteration) — BWL Vaelastrasz orb (UBRS prereq, not MC)
- [decision-engine/unlock-graph.md](../decision-engine/unlock-graph.md) — `attune.molten-core` and `quest.attunement-to-the-core` graph nodes
- [decision-engine/per-bracket-actions/06-l55-l60.md](../decision-engine/per-bracket-actions/06-l55-l60.md) — MC attune is a critical-path priority **820** action at lvl 60
- [dungeons/blackrock-depths.md](#) (pass 4) — BRD dungeon details + Master's Key chain
- [raids/molten-core.md](#) (pass 5) — MC raid encounters (after attunement)
- [reputations/hydraxian-waterlords.md](#) (pass 7) — Hydraxian Waterlords rep for Aqual/Eternal Quintessence (boss douses inside MC; orthogonal to attunement but commonly bundled)
