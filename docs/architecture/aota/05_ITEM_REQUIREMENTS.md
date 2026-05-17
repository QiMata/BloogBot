# 05 — Item-requirement DAG

> Prerequisite: [`04_QUEST_CHAINS.md`](04_QUEST_CHAINS.md).
>
> Some Activities are gated by **items**, not quests. The Onyxia
> Attunement chain ends with a `RequiredItem` (Drakefire Amulet); UBRS
> entry requires the Seal of Ascension; the MC attunement chain returns
> an `Eternal Quintessence` after Hydraxian rep. This file specifies the
> item-requirement DAG schema and how the DecisionEngine resolves "how
> do I obtain item X".

## 1. Item nodes

A node is **one row of `mangos.item_template`**. Fields the engine
cares about:

| Field | MaNGOS column | Engine usage |
|---|---|---|
| `Id` | `entry` | Stable identifier. |
| `Name` | `name` | UI label. |
| `Class` / `SubClass` | `class`, `subclass` | Distinguish quest items / keys / consumables / gear. `class=12` = quest item; `class=13` = key. |
| `Quality` | `Quality` | 0..5 = poor/common/uncommon/rare/epic/legendary. |
| `Bonding` | `bonding` | 0 = none, 1 = BoP, 2 = BoE, 3 = BoU, 4 = quest. |
| `RequiredLevel` | `RequiredLevel` | Use-level gate. |
| `RequiredSkill` / `RequiredSkillRank` | same | Profession gate (recipes). |
| `RequiredSpell` | `RequiredSpell` | Specialization gate. |
| `RequiredRaces` / `RequiredClasses` | same | Per-class gear (Hunter bow, Paladin libram, etc.). |
| `StartQuest` | `startquest` | Item gives a quest on use (drives `accept-quest` Objective). |
| `Spell{1..5}` + `SpellTrigger{1..5}` | same | Use spells (key turning, consumable). |
| Quest-reward of | (computed from `quest_template.RewItemId*` / `RewChoiceItemId*`) | Reverse lookup: which quest grants this item. |
| Vendor sources | (computed from `npc_vendor`) | Which vendors sell this item (and at what price). |
| Loot sources | (computed from `creature_loot_template`, `gameobject_loot_template`, `reference_loot_template`, `disenchant_loot_template`, `fishing_loot_template`, `mail_loot_template`) | Drop tables for the item. |

## 2. Edges (provenance DAG)

The DAG is *backwards* from the quest DAG: edges point from an item
to its **sources**. Each item can have multiple sources; the engine
picks the cheapest.

```
item.entry
   ▲
   ├── quest reward         (edge label: quest.entry, choice index)
   ├── creature loot drop   (edge label: creature.entry, %chance)
   ├── gameobject loot drop (edge label: go.entry, %chance, respawn)
   ├── vendor purchase      (edge label: npc_vendor.entry, copperPrice, extendedCost)
   ├── crafted              (edge label: spell.entry of craft, materials list)
   ├── AH purchase          (edge label: market data, observed price)
   ├── mail-template        (edge label: mailTemplate.id, the "this comes in the mail" path)
   └── disenchant from item (edge label: source.entry, %chance — enchanters only)
```

## 3. Edge types in detail

### Edge — Quest reward

Detected by joining `quest_template.RewItemId{1..4}` or
`RewChoiceItemId{1..6}` to `item_template.entry`. Drives the
"complete this quest to get this item" Objective sub-tree. The
DecisionEngine prepends the *entire quest chain* leading to the
quest that grants this item (per [`04_QUEST_CHAINS.md`](04_QUEST_CHAINS.md)).

### Edge — Creature loot

Detected by joining `creature_loot_template.item` to `item_template.entry`.
Carries `ChanceOrQuestChance`, `mincountOrRef`, `maxcount`,
`groupid`. The engine reads this for:

- **Expected kill-budget estimation.** For a 1% drop rate, the engine
  budgets ~120 kills with 2-sigma confidence; if a `Collect` Objective
  exceeds this budget, escalate to `task_timeout`.
- **Hotspot resolution.** Joining `creature_loot_template` with
  `creature` (spawn table) gives hotspot coordinates.

### Edge — Gameobject loot

Same shape via `gameobject_loot_template`. Includes herb nodes, mining
nodes, fishing pools, chests, etc.

### Edge — Vendor

Joining `npc_vendor.item` to `item_template.entry`. Carries
`maxcount` (stock), `incrtime` (restock seconds), `ExtendedCost`
(reagent/honor cost in addition to copper). Used for the cheap path
when an item is vendor-purchasable.

### Edge — Crafted

A crafted item appears as a spell whose effect creates the item.
Detected by:

```sql
SELECT spell.entry, spell.EffectItemType_1
FROM   spell_template
WHERE  spell.EffectItemType_1 = :itemId
   OR  spell.EffectItemType_2 = :itemId
   OR  spell.EffectItemType_3 = :itemId;
```

Engine prepends the craft Objective chain (material sourcing + recipe
learning + craft N times). Recipe-not-known triggers a sub-tree to
*acquire the recipe* (vendor, drop, quest reward).

### Edge — AH

There is no MaNGOS table for AH inventory — the engine reads live
auction listings via the BG `WoWSharpClient.AuctionHouseAgent` (or
ForegroundBotRunner Lua scrape) and updates a cached price table in
`Bot/market-data.json`. The cache is rebuilt on every visit to a major
city.

### Edge — Disenchant

Used when the bot is an enchanter and the item is an unwanted
green/blue. Joins `disenchant_loot_template.item` (where item = the
green/blue) to the materials it produces. Drives the
`DisenchantTask`.

### Edge — Mail-template

Some items arrive in the mail after a `quest_template.RewMailTemplateId`
fires. Detected via `quest_template.RewMailTemplateId` and the linked
`mail_loot_template`. The engine emits a `MailRetrieveTask` Objective
some seconds after the quest turn-in.

## 4. Walking the provenance DAG — the resolver

```text
function ResolveItemSource(itemId : int,
                            bot     : Snapshot,
                            db      : IMangosCatalog,
                            market  : MarketDataCache) -> ItemSourcePlan:

    sources = []

    // (a) quest reward — usually the cheapest if eligible
    for qReward in db.QuestRewardsForItem(itemId):
        cost = EstimatedDuration(qReward.quest, bot)
        sources.append(Source(QUEST, qReward.quest, cost))

    // (b) vendor — only if bot has gold and vendor is reachable
    for vEntry in db.VendorsSellingItem(itemId):
        cost = vEntry.copperPrice + market.CostToReach(vEntry.npc, bot.Position)
        if bot.Coinage >= vEntry.copperPrice:
            sources.append(Source(VENDOR, vEntry, cost))

    // (c) creature/gameobject drop — if drop rate is acceptable
    for dropRow in db.LootSourcesForItem(itemId):
        budget = ExpectedKillBudget(dropRow.chance, twoSigma=true)
        cost = budget * EstimatedKillTime(dropRow.creatureEntry, bot)
              + market.CostToReach(dropRow.spawn, bot.Position)
        sources.append(Source(DROP, dropRow, cost))

    // (d) crafted — if bot has the skill
    for craftSpell in db.SpellsCreatingItem(itemId):
        if bot.GetSkill(craftSpell.skillId) >= craftSpell.requiredSkillRank:
            matCost = sum(ResolveItemSource(reagent.itemId, bot, db, market).cost
                          for reagent in craftSpell.reagents)
            sources.append(Source(CRAFT, craftSpell, matCost))

    // (e) AH — if listed and bot has gold
    listing = market.GetCheapest(itemId)
    if listing != null and bot.Coinage >= listing.buyout:
        sources.append(Source(AH, listing, listing.buyout))

    if sources.empty:
        return ItemSourcePlan.NoneAvailable
    return ItemSourcePlan(sources.minBy(s => s.cost))
```

The plan is **injected as a prepended Objective sequence** when an
Activity declares `RequiredItem` and the bot doesn't have it. The
plan's Source kind determines the Task family used:

| Source kind | Inserted Objectives |
|---|---|
| QUEST | Full chain to the quest that rewards the item (per `04_QUEST_CHAINS.md`). |
| VENDOR | `travel-to-vendor` → `vendor-buy(item, count)`. |
| DROP (creature) | `travel-to-hotspot` → `kill-loop(creatureEntry, until item in inventory or budget exhausted)`. |
| DROP (gameobject) | `travel-to-spawn` → `interact-gameobject(go)`. |
| CRAFT | recursive `ResolveItemSource(reagent)` per reagent → `craft-recipe(spell, 1)`. |
| AH | `travel-to-AH` → `buy-auction-listing(itemId, listingId)`. |

## 5. Worked example — Seal of Ascension (UBRS key)

The Seal of Ascension is itemId 22754 in MaNGOS schema (1.12). It is
a **quest reward** at the end of a multi-step chain involving
Drakkisath's Brand, dragonkin scales, and a forge interaction. It is
itself a `RequiredItem` for the UBRS Activity (`dungeon.upper-blackrock-spire`).

The catalog row:

```csharp
new ActivityDefinition {
    Id = "dungeon.upper-blackrock-spire",
    LevelRange = new LevelRange(58, 60),
    EntryRequirements = new EntryRequirements {
        RequiredItems = [22754],          // Seal of Ascension
    },
    ...
}
```

A level-58 Warrior with no Seal in `bot.BagItems` triggers the
provenance walk:

```
ResolveItemSource(22754) for bot @ Burning Steppes (level 58)
  - quest reward sources:
      - quest.4736 "Seal of Ascension" (the final turn-in)
        → bot.QuestsCompleted = false
        → upstream quest chain (LBRS + UBRS partial + Vael's room)
        → cost = ~3 hours (estimated)
  - drop sources: none (BoP quest reward)
  - vendor sources: none
  - crafted: none
  - AH: BoP, never listed

  → ItemSourcePlan = QUEST chain (only source)
```

The composer prepends the full Seal of Ascension chain Objectives
**before** the UBRS Objective sequence:

```
ubrs Activity (target):
   [0] accept-4736 "Seal of Ascension" pickup    (depends on completing earlier chain)
   [1] kill-LBRS-drakkisath-related              (chain step)
   [2] interact-vael-room-orb                    (chain step)
   [3] turn-in-4736 → Seal of Ascension in bag   ← inventory predicate satisfied
   [4] travel-to-UBRS-portal                     (original Activity start)
   [5] use-key-on-UBRS-rune                      (uses item 22754)
   [6] enter-UBRS
   ...
```

This is the **recursive composition** of Activity → Objective →
Activity: an Activity's `RequiredItem` invokes a sub-Activity's
worth of Objectives transparently.

## 6. Other major item-requirement chains

| Item | Gates | Source |
|---|---|---|
| **Seal of Ascension** (22754) | UBRS entry | quest chain (LBRS + UBRS partial) |
| **Drakefire Amulet** (13348) | Onyxia's Lair entry (Alliance variant: Marshal Windsor chain; Horde variant: Eitrigg chain) | quest chain (long; ~10 quests) |
| **Hand of Ragnaros** (17204) | (epic 2H mace) | Ragnaros kill loot, MC |
| **Eye of Sulfuras** (17204 variant; recipe 17192) | crafted item gate → BiS Hammer | Ragnaros kill drop + Sulfuron Hammer craft (Blacksmith specialty) |
| **Orb of Deception** (1973) | not gating, but a money chase | random world drop, very rare |
| **Master's Key** (6893) | LBRS, UBRS, MC orb passage | Drakkisath's quest in BRD (`Kharan's Tale` → `What the Wind Carries`) |
| **Shadowforge Key** (11000) | BRD wing access | quest chain in BRD |
| **Crescent Key** (11000-equivalent in Sunken Temple) | ST entry | quest in Swamp of Sorrows |
| **Skeleton Key** (13704) | Scholomance entry | quest in Western Plaguelands + BRD ingredients |
| **Workshop Key** (9240) | Gnomeregan workshop wing | quest in Tinker Town |
| **Onyxia Cloak materials** (Alliance chain) | Onyxia attunement | multi-quest UC/SW chain |
| **Aqual Quintessence / Eternal Quintessence** | MC rune douses | Hydraxian Waterlords rep |
| **Scourgestones** (12840 minor, 12841 invader, 12843 corruptor) | Naxx attunement progression | drops in Plaguelands |
| **Eye of Shadow** (17251) | Naxx attunement final step | rare drop, world bosses + AQ |

Per-chain reference: [`leveling-guide/attunements/`](../../leveling-guide/attunements/).

## 7. Key-as-item subtlety

In 1.12.1 schema, keys carry `class=13` (`ITEM_CLASS_KEY`). The bot
treats keys as **inventory predicates** for areatrigger gates:

```sql
SELECT required_item
FROM   areatrigger_teleport
WHERE  id = :triggerId;
```

If `required_item != 0`, the engine adds an inventory check Objective
to the Travel-Objective that targets the trigger. Missing key →
prepend `ResolveItemSource(required_item)` plan.

## 8. Test surface

For the item-requirement resolver:

1. **`ItemSourceResolverTests.QuestRewardSource_PrefersChainCompletion`** —
   given an item that is a quest reward, the resolver returns a QUEST
   plan with the full chain prepended.
2. **`ItemSourceResolverTests.DropSource_BudgetsKillsByDropRate`** —
   given a 1% drop, the kill budget is ≥100 with the 2-sigma tail
   accounted.
3. **`ItemSourceResolverTests.MultiSource_PicksCheapestBySnapshotCost`** —
   when an item is available via quest AND vendor AND AH, the resolver
   picks the lowest-cost source given the bot's current `Position`
   and `Coinage`.
4. **`ItemSourceResolverTests.BoP_NeverPicksAH`** — soulbound items
   never resolve to the AH source even if listed.

Live-validation:

- `UbrsAttunement_SealOfAscension_FullChain_Tests.cs` — drives a
  level-58 Warrior through the full Seal chain and asserts the Seal
  shows up in bag inventory before the UBRS Activity is allowed to
  start. (Phase 2; today only the legality validator's static check
  fires.)
