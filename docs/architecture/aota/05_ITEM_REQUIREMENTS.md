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

## 9. ML-aided cheapest-source learner

The §4 `ResolveItemSource(...)` resolver is **provenance-correct** —
it walks every DB-declared source kind (QUEST, VENDOR, DROP, CRAFT,
AH) and picks the lowest predicted cost via `sources.minBy(s =>
s.cost)`. But the `s.cost` estimates themselves are **heuristic**:

- `EstimatedDuration(quest, bot)` is a hand-rolled function of
  `quest.LimitTime` + objective-column count + travel guess.
- `EstimatedKillTime(creatureEntry, bot)` is mob `MinHP/MaxHP` /
  class-DPS table.
- `ExpectedKillBudget(chance, twoSigma=true)` is a closed-form
  `1.0 / chance + 2 * sqrt(1.0 / chance)` — correct in expectation
  but mute about variance in actual drop streaks.
- `market.CostToReach(npc, position)` is travel-graph A*.
- `market.GetCheapest(itemId)` AH listing is correct at scrape time
  but stale by minutes when the bot arrives.

Each of these errors compounds. A bot can spend 90 wall-clock minutes
killing low-drop-rate mobs that the heuristic underbid by 3x; an AH
listing the bot raced to may have been bought; a craft chain whose
reagent cost the heuristic underbid because the reagents themselves
have a more efficient source the resolver didn't pick.

The **cheapest-source learner** addresses this without replacing the
§4 algorithm. It hooks the final `sources.minBy(...)` step into the
[`Spec/20 §2 GetObjectiveAdviceAsync`](../../Spec/20_DECISION_ENGINE.md#2-service-surface)
advisor — treating each `Source` candidate as a tied Objective head,
with the advisor scoring them against empirical (snapshot, source,
actual-cost-incurred) tuples from prior traces.

```
ResolveItemSource(itemId, bot, db, market) produces:
  sources = [
    Source(QUEST, quest=4736, estimated_cost=3h),
    Source(DROP,  creature=Dragonkin, estimated_cost=2.5h, drop=0.5%),
    Source(VENDOR, npc=BlackMarketDealer, estimated_cost=200g + 30min travel),
    Source(AH,    listing_id=1837, estimated_cost=450g),
  ]

Learner queries GetObjectiveAdviceAsync with:
  tied_objective_ids = ["acquire-from-quest-4736",
                        "acquire-from-drop-dragonkin",
                        "acquire-from-vendor-bmd",
                        "acquire-from-ah-1837"]

Advisor returns advice based on the bot's observed cost history for
similar (itemId, level_band, faction, current_zone) tuples plus the
gold-axis vs xp-axis dominance in roster_goal_distance[8].

Bot picks up the recommended source's Objective prefix; the chosen
source's Activity-completion outcome line is fed back to off-line
training.
```

### Three maturity phases (mirrors Spec/20 §5)

| Phase | Source-cost estimation | Wire |
|---|---|---|
| 1 — Heuristic | The §4 hand-rolled cost functions; `sources.minBy(s => s.cost)` | `ResolveItemSource` default |
| 2 — Rules + lookup | Per-`(itemId, level_band, faction)` overrides at `Config/decision-engine/item-source-rules.json` (e.g. "Drakefire Amulet @ L60 Alliance always pick quest" — Marshal Windsor chain is faster than the BoP-non-existent alternatives) | Plan/14 slot S10.6 |
| 3 — ONNX | `Services/DecisionEngineService/Models/objective/v1.onnx` extended with item-source features: source kind, source cost estimate, drop-rate variance class, AH market depth | trained on `tmp/test-runtime/traces/Item_*/outcome.jsonl` |

### Phase-3 input features

`ObjectiveContext.tied_objective_ids[]` carries the per-source
candidate ids (format: `acquire-from-<kind>-<entry|id>`).
`ObjectiveContext.tied_objective_costs[]` carries the §4 heuristic
cost estimates so the model knows what to *correct*. Additional
features the off-line trainer injects:

| Feature | Source |
|---|---|
| source_kind one-hot[6] | QUEST / VENDOR / CREATURE_DROP / GO_DROP / CRAFT / AH |
| drop_rate (when DROP) | `dropRow.chance` |
| ah_listing_age_seconds | `now - market.scrapedAt(listing_id)` |
| craft_reagent_recursion_depth | depth of the recursive `ResolveItemSource(reagent)` chain |
| historical_actual_cost_p50 | median actual cost from prior outcomes for `(itemId, source_kind)` |
| historical_actual_cost_p90 | 90th-percentile to capture variance |
| bot_gold_relative_to_target | `bot.Coinage / CharacterRosterGoal.GoldTargetCopper` |

### Fail-soft fallback

`NoAdvice` (timeout / service down / model unavailable / confidence
< 0.5 / recommended source id outside the candidate set) → fall back
to §4 `sources.minBy(s => s.cost)`. The learner can only **reorder**
already-DB-validated sources; it cannot invent a source the §4
provenance walk did not produce. This preserves the "every chosen
source is reachable" guarantee.

### Live-validation guard

Replaying any Item_* trace with the learner forced to `NoAdvice`
MUST still result in the bot eventually acquiring the item AND the
chain's `outcome.roster_distance_delta ≤ 0`. The §4 deterministic
resolver always finds a working path; ML only trims wall-clock.

## 10. Worked example — Drakefire Amulet (Onyxia attunement)

Setup:

- Bot: level-60 Alliance Warrior at Stormwind, no Drakefire Amulet
  in `bot.BagItems`, has `CharacterRosterGoal.Attunements`
  containing `"attune.onyxia"`.
- Available sources (per the §4 walk):
  - QUEST: `quest.4866` "Drakefire Amulet" — turn-in of the
    Marshal Windsor chain (~10-quest chain through Burning Steppes,
    BRD, UBRS), estimated 3-4 hours.
  - DROP: Onyxia herself (chicken-and-egg; she IS the gate the
    amulet opens; eliminated by §4 step (c) circular-dependency
    detection).
  - VENDOR: none (BoP).
  - CRAFT: none.
  - AH: none (BoP).

§4 picks QUEST (only viable source).

### With Phase-2 rules

`Config/decision-engine/item-source-rules.json` declares:
```jsonc
{
  "13348": {
    "level_band_60_alliance": {
      "preferred_source": "quest.4866",
      "rationale": "Marshal Windsor chain is the ONLY non-BoP path; explicit override stops the heuristic from underbidding the chain's travel cost"
    },
    "level_band_60_horde": {
      "preferred_source": "quest.4881",
      "rationale": "Horde Eitrigg chain; do not pick Alliance chain even if cross-faction-eligible"
    }
  }
}
```

Phase-2 advisor returns the same answer as Phase-1 for this case
(QUEST is the only source), but the rule **prevents misconfiguration**
— a future heuristic change that incorrectly identifies a faster
alternative source is caught by the rule.

### With Phase-3 ONNX

The model has trained on 50+ Drakefire-acquisition traces. Two
observations from data:

- Marshal Windsor chain's *measured* wall-clock for the median
  level-60 Alliance Warrior is 4.2h (not the 3-4h heuristic).
- A high-gold bot (`GoldTargetPct` axis already closed) optimizes
  differently from a low-gold bot — the chain rewards 18g80s at
  turn-in, which is meaningful only for low-gold bots.

For this example the QUEST source is still the only option, so the
model returns it. The Phase-3 value shows up in **multi-source
items** like consumables, gear pieces, and reagents — see the next
sub-example.

### Multi-source sub-example: "Greater Healing Potion" (itemId 3928)

Setup:

- Bot: level-50 Holy Priest, needs 20× Greater Healing Potions for
  raid stocking.
- Available sources:
  - QUEST: none.
  - DROP: 1% drop from a handful of mob types in Eastern Plaguelands,
    estimated cost ~6 hours of farming.
  - VENDOR: none (recipe, not item).
  - CRAFT: own Alchemy skill 215 (recipe req 215; bot has it). Reagents
    are 2× Sungrass + 1× Crystal Vial. Sungrass resolves to a 200-level
    herbalism gathering loop in Felwood (estimated 2 hours for 40
    Sungrass + spillover XP). Crystal Vials are vendor 1s each.
  - AH: market lists Greater Healing Potion at 1g50s each (30g total
    for 20); listing scraped 14 minutes ago.

§4 with Phase-1 heuristic picks **CRAFT** because the recursive
Sungrass cost estimate is ~2h and AH costs 30g (which the bot
doesn't have enough of in this example — `Coinage` is 12g, below the
30g threshold; AH is eliminated by §4 step (e)).

### With Phase-3 ONNX on the multi-source item

The model knows from history that:

- Sungrass farming traces for L50 Priests *empirically* take 2.8h ±
  0.4h (vs the 2h heuristic) due to mob aggro on the gathering
  route.
- The Felwood gathering loop incidentally closes
  `roster_goal_distance[ProfessionSkill]` for Herbalism — empirical
  +0.02 delta to that axis per trace.
- A high-XP-axis bot prefers DROP because the EPL mob-grind contributes
  +0.05 to `roster_goal_distance[Level]` per hour — even with the 6h
  cost estimate.

Model returns advice based on the bot's *axis dominance*:

- Bot whose dominant axis is `ProfessionSkill` → CRAFT (Sungrass +
  herbalism XP).
- Bot whose dominant axis is `Level` → DROP (EPL kill XP +
  occasional potion).
- Bot whose dominant axis is `GearTier` and has spare gold → AH (if
  affordable).

The deterministic Phase-1 heuristic always picks CRAFT for this
scenario regardless of axis. The Phase-3 model produces a
**dynamic** answer per-bot, but still bounded by what §4 already
validated as a working source.

## 11. Dynamic-progressive invariant (item-source side)

Per Spec/19 §10 and Spec/05 §Dynamic-progressive invariant, the
cheapest-source learner MUST satisfy:

1. **Dynamic.** Two snapshots that differ in
   `(bot.Coinage, bot.Skills, roster_goal_distance)` MUST sometimes
   produce different `ItemSourcePlan.Source` picks for the same
   `(itemId, snapshot)` query — the §10 Greater-Healing-Potion
   sub-example shows the model picking CRAFT vs DROP vs AH based on
   axis dominance. Identical snapshots produce identical picks.
2. **Progressive.** Every chosen source's Objective prefix produces
   `outcome.roster_distance_delta ≤ 0`. The learner cannot pick a
   source that fails to acquire the item OR that puts the bot
   further from its roster goal (e.g. by spending gold for an AH
   listing when the gold-axis is dominant).

Asserted at §test surface by
`AotaItemRequirements_LearnerPicksDifferentSourcePerAxisDominance`.

## 12. Plan-slot cross-reference

| Slot | Owns | Section here |
|---|---|---|
| [`Plan/03/S2.0`](../../Plan/03_PHASE2_ONDEMAND_ENGINE.md#s20--iactivity--iobjective-runtime-contracts) | `IActivityComposer.Compose(...)` calls `ResolveItemSource` for `RequiredItem` gates | §4 algorithm |
| [`Plan/14/S10.2`](../../Plan/14_PHASE10_DECISION_ENGINE_INTEGRATION.md#s102--objective-composer-tie-breaker) | `ObjectiveTieBreaker.cs` extended to consume source-candidate tied sets | §9 learner |
| [`Plan/14/S10.6`](../../Plan/14_PHASE10_DECISION_ENGINE_INTEGRATION.md#s106--mode-aware-advisor-activation) | `Config/decision-engine/item-source-rules.json` + Mode flip | §9 Phase 2 + Phase 3 |
| [`Plan/14/S10.7`](../../Plan/14_PHASE10_DECISION_ENGINE_INTEGRATION.md#s107--training-trace-plumbing) | Trace pipeline that feeds Phase-3 training | §9 ONNX input |
| **(no slot yet — Plan follow-up)** | `Services/WoWStateManager/Activities/ItemSourceResolver.cs` (the §4 algorithm code; never claimed by a Plan slot) | §4 algorithm |
| **(no slot yet — Plan follow-up)** | `Services/WoWStateManager/Market/MarketDataCache.cs` (AH listing scrape + recency tracking; feeds §4 step (e)) | §4 step (e) |

The two "no slot yet" rows join the Plan-follow-up roster (13th + 14th
orphans: `ItemSourceResolver.cs` + `MarketDataCache.cs`).

## 13. Test surface (learner-side; §4 deterministic resolver still tested by §8)

Contract tests live at
`Tests/BotRunner.Tests/Activities/ItemRequirementsContractTests.cs`.
Assertions go through `snapshot.current_objective_id` (Spec/19 field
34) and `snapshot.advice_log[]` entries with `advisor="objective"`.

- **`ItemSourceLearner_FallSoftOnNoAdvice_PicksHeuristicMin`** — a
  DecisionEngine stub returns `NoAdvice`; the resolver picks the
  Phase-1 `sources.minBy(s => s.cost)` answer; `advice_log` shows
  `used_index = 0xFFFFFFFD` (ServiceDown sentinel).
- **`ItemSourceLearner_CannotInventSourceOutsideProvenanceDag`** —
  a DecisionEngine stub returns `RecommendedObjectiveId=
  "acquire-from-imaginary-vendor"`; the recommendation is discarded
  (not in the §4 walk's candidate set); fall-back applies.
- **`ItemSourceLearner_BoPItemNeverPicksAH`** — for soulbound items,
  the learner cannot return an AH source even under Phase-3
  inference; §4 step (e) eliminated AH from the candidate set
  upstream, so the tied set the advisor sees lacks AH entirely.
- **`AotaItemRequirements_LearnerPicksDifferentSourcePerAxisDominance`** —
  the dynamic-progressive invariant. Two synthetic snapshots
  identical except for `roster_goal_distance` axis dominance
  (ProfessionSkill-dominant vs Level-dominant) produce different
  `ItemSourcePlan.Source.Kind` for the same Greater Healing Potion
  query. Both chosen paths produce
  `outcome.roster_distance_delta ≤ 0`.
