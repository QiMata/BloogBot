# 04 — Quest-chain DAG schema

> Prerequisite: [`03_DYNAMIC_COMPOSITION.md`](03_DYNAMIC_COMPOSITION.md).
>
> Quests are the longest-tail input to the dynamic Objective composer.
> This file specifies the DAG schema, the MaNGOS columns that produce
> each edge, and the gates that filter the DAG per bot.

## 1. Nodes

A node in the quest DAG is **one row of `mangos.quest_template`**.
Every node carries:

| Field | MaNGOS column | Semantic role in the DAG |
|---|---|---|
| `Id` | `entry` | Stable identifier. |
| `Title` | `Title` | UI label, log line. |
| `MinLevel` / `QuestLevel` | `MinLevel`, `QuestLevel` | Level gate. |
| `Type` | `Type` | 0 = normal, 1 = group, 41 = PvP, 81 = Dungeon, 82 = Raid, 83 = Heroic (1.12 ignores), 21 = Class. |
| `RequiredRaces` | `RequiredRaces` | Bitmask. |
| `RequiredClasses` | `RequiredClasses` | Bitmask. |
| `RequiredSkill` / `RequiredSkillValue` | same names | Profession gate. |
| `RequiredCondition` (server-script) | `RequiredCondition` | Custom server gate. |
| `LimitTime` | `LimitTime` | Time-limit (seconds). |
| `SpecialFlags` | `SpecialFlags` | Bit 0 = repeatable, bit 1 = explore-completable, bit 2 = autoaccept, bit 3 = escort. |
| Objective columns (4) | `ReqCreatureOrGOId1..4`, `ReqCreatureOrGOCount1..4` | Kill or interact objectives. |
| Item-collect columns (6) | `ReqItemId1..6`, `ReqItemCount1..6` | Collect objectives. |
| Spell-cast columns (4) | `ReqSpellCast1..4` | Cast-on-target objectives. |
| Source item | `SrcItemId` | Provided item the quest gives the player on accept (drives `SrcSpell`, used for class quests like Mage's Conjure Refreshment scrolls). |
| Reward columns | `RewItemId1..4`, `RewItemCount1..4`, `RewChoiceItemId1..6`, `RewChoiceItemCount1..6`, `RewMoneyMaxLevel`, `RewSpellCast`, `RewHonorableKills`, `RewMailTemplateId`, `RewMailDelaySecs` | Reward selection inputs. |
| Chain link | `PrevQuestId`, `NextQuestId`, `NextQuestInChain` | Edges (see §2). |
| Exclusivity | `ExclusiveGroup` | One-of-N node grouping (see §3). |

## 2. Edges

Three edge kinds in the DAG; all are derived directly from
`quest_template` columns:

### Edge type A — `PrevQuest` (hard prerequisite)

```
quest.entry   <─ PrevQuestId ─   quest.NextEntry
```

Semantic: "the bot cannot have `quest.NextEntry` offered until it has
**turned in** `quest.PrevQuestId`." Multiple successors per
predecessor are allowed (branching). One predecessor per successor
column (`PrevQuestId` is a single uint32).

Detection in DB: `quest_template.PrevQuestId != 0`.

### Edge type B — `NextQuestInChain` (chain continuation)

```
quest.NextQuestInChain ─►   quest.NextEntry
```

Semantic: "after completing `quest.entry`, the *intended* next quest in
the chain is `quest.NextQuestInChain`." Used by the UI's auto-accept
hint and, for the DecisionEngine, used as a **priority boost** for the
next quest (chain continuation is preferred over starting a parallel
chain).

Detection in DB: `quest_template.NextQuestInChain != 0`.

Note: A chain edge implies a `PrevQuest` edge in the reverse direction.
The DB carries both for indexing.

### Edge type C — `ExclusiveGroup` (one-of-N)

```
quest.entry  ─┐
              ├─── ExclusiveGroup = -G  (negative = one-of)
quest.entry' ─┤
              ├─── ExclusiveGroup = -G
quest.entry'' ─┘
```

Semantic: "pick exactly one of these quests; completing one **fails**
the others." Classic example: faction-side reward choices ("Side with
the Alliance" vs "Side with the Horde" in Hillsbrad).

Positive `ExclusiveGroup` = "all in this group must be completed";
negative `ExclusiveGroup` = "pick one." The DecisionEngine respects
this by emitting a `OneOf{...}` virtual Objective that resolves to
the engine's choice.

Detection in DB: `quest_template.ExclusiveGroup != 0`.

## 3. Quest pickup / turn-in NPCs (the spatial edge)

Quest nodes have no built-in coordinate; the spawn coordinates come
from joining with `quest_relations`:

```sql
-- pickup NPCs
SELECT cr.id AS npcEntry, c.position_x, c.position_y, c.position_z, c.map
FROM   creature_questrelation cr
JOIN   creature c ON c.id = cr.id
WHERE  cr.quest = :questId;

-- turn-in NPCs
SELECT cir.id AS npcEntry, c.position_x, c.position_y, c.position_z, c.map
FROM   creature_involvedrelation cir
JOIN   creature c ON c.id = cir.id
WHERE  cir.quest = :questId;
```

Same shape for `gameobject_questrelation` / `gameobject_involvedrelation`
when the pickup or turn-in is a gameobject (rare but exists — e.g.
"read the inscription" quests pulled from a stone tablet GO).

## 4. The per-bot DAG filter

Given the unfiltered `quest_template` DAG, the engine filters per bot
in this order (first failure drops the node):

```text
function FilterQuestNode(q : Quest, bot : Snapshot) -> bool:
    if bot.Level < q.MinLevel: return false
    if bot.Level + 5 < q.QuestLevel: return false             // too low; gray-XP gate
    if bot.Level - 5 > q.QuestLevel and q.Type != Repeatable:
        return false                                          // skip green-XP (optional bracket-level rule)
    if (q.RequiredRaces & (1 << bot.Race)) == 0: return false
    if (q.RequiredClasses & (1 << bot.Class)) == 0: return false
    if q.RequiredSkill != 0 and bot.GetSkill(q.RequiredSkill) < q.RequiredSkillValue:
        return false
    if q.entry in bot.QuestsCompleted and (q.SpecialFlags & REPEATABLE) == 0:
        return false
    if q.entry in bot.QuestsAbandonedRecently:                // cooldown so the engine doesn't whiplash
        return false
    if q.LimitTime > 0 and ExpectedDuration(q, bot) > q.LimitTime:
        return false                                          // we won't finish in time
    if q.Type == Group and bot.PartyComposition.PlayerCount < q.MinPartySize:
        return false                                          // group quest, no party

    // prerequisite edges
    if q.PrevQuestId != 0 and q.PrevQuestId not in bot.QuestsCompleted:
        return false

    // exclusive-group: another in the group is already completed
    if q.ExclusiveGroup < 0:
        groupQuests = db.QuestsInExclusiveGroup(q.ExclusiveGroup)
        if any(g in bot.QuestsCompleted for g in groupQuests):
            return false

    return true
```

## 5. Objective synthesis per quest

Given a quest passes the filter, the composer (per
[`03_DYNAMIC_COMPOSITION.md#4-worked-composequestingobjectives`](03_DYNAMIC_COMPOSITION.md))
synthesizes Objectives per quest column. The mapping:

| `quest_template` column | Synthesized Objective | Task family |
|---|---|---|
| `pickup-NPC coord` (from quest_relations) | `travel-to-{q}-pickup` (Travel) | Travel |
| `pickup-NPC` | `accept-{q}` (Interact) | Questing — `AcceptQuestTask(q)` |
| `ReqCreatureOrGOId{1..4} > 0` (creature) | `kill-{entry}-q{q}-o{i}` (Kill) | Questing — `KillObjectiveTask` |
| `ReqCreatureOrGOId{1..4} < 0` (gameobject) | `use-{entry}-q{q}-o{i}` (Interact-GO) | Questing — `UseGameObjectTask` |
| `ReqItemId{1..6} > 0` | `collect-{itemId}-q{q}-i{i}` (Collect) | Questing — `CollectObjectiveTask` |
| `ReqSpellCast{1..4}` | `cast-{spellId}-q{q}-s{i}` (CastSpell) | Questing — embedded today |
| `SpecialFlags & ESCORT` (server-tagged via `creature_template.ScriptName`) | `escort-{entry}-q{q}` | Questing — `EscortObjectiveTask` |
| `turnin-NPC coord` | `travel-to-{q}-turnin` (Travel) | Travel |
| `turnin-NPC` | `turnin-{q}` (TurnIn) | Questing — `TurnInQuestTask` |

The Objective sequence per quest is therefore typically:

```
travel-to-pickup ─► accept ─► [kill | collect | use | cast | escort]+ ─► travel-to-turnin ─► turnin
```

Some quests skip travel-to-pickup (auto-accepted on entry to a
specific zone via `SpecialFlags & AUTO_ACCEPT`), and some skip
travel-to-turnin (turn-in is at the same NPC who gave the quest —
identical pickup/turn-in NPC).

## 6. Reward resolution at turn-in

`TurnInQuestTask` calls `IRewardSelector.SelectQuestReward(choice, ctx)`.
Per [`Spec/03_BOTRUNNER.md#reward-selection`](../../Spec/03_BOTRUNNER.md#reward-selection):
the invariant is "always picks a reward." The selector reads:

- `RewItemId{1..4}` — guaranteed rewards (auto-granted, no choice).
- `RewChoiceItemId{1..6}` — choice list; the bot picks **exactly one**.

The selection algorithm (initial Phase 2 — trivial) picks index 0.
Phase 4 upgrades to the `ProgressionPlanner` selector that reads
`CharacterBuildConfig.TargetGearSet` and picks the choice that best
advances the BiS plan. Future: ML-augmented selection.

## 7. Cross-zone chains

Some quest chains span zones (Westfall → Stormwind → Stranglethorn,
"The Defias Brotherhood" → "Defias Tower" → ...). These appear in the
DAG as ordinary `PrevQuestId` / `NextQuestInChain` edges; the
composer prepends a **cross-zone Travel Objective** when the next
quest's pickup NPC is not in the current Activity's `Location` zone.

The Activity itself does **not** change: a single `quest.zone.westfall`
Activity drives the bot to follow a chain that ends in Stormwind for
turn-in. When the chain terminates in a *different* main-zone
(Westfall → Duskwood transition), the composer emits the
`transition-to-next-zone` Objective and the ProgressionPlanner
issues a new `quest.zone.duskwood` Activity assignment.

## 8. Class quests

Class quests carry `Type = 21` and `RequiredClasses` bitmask single-bit.
They unlock baseline class kit (Hunter pet, Warlock summons, Druid forms,
Shaman totems, Paladin Tome of Divinity, Priest race-spell).

The DAG is identical in shape, but the priority band lifts to **Class
Identity (800–999)** per
[`leveling-priority.md`](../../leveling-guide/decision-engine/leveling-priority.md).
Class quests preempt zone-quest Objectives within the same bracket.

Per-class chains: [`leveling-guide/classes/*.md`](../../leveling-guide/classes/).

## 9. Worked example — "The Defias Brotherhood" chain (Westfall)

| Step | Quest entry | Title | Pickup zone | Turn-in zone | Notes |
|---|---|---|---|---|---|
| 1 | 132 | The People's Militia | Westfall (Sentinel Hill) | Westfall | Kill 8 Defias Trapper. |
| 2 | 135 | The Defias Brotherhood (Part 1) | Westfall | Westfall | Kill Defias Messenger. Triggers Part 2. |
| 3 | 138 | The Defias Brotherhood (Part 2) | Westfall | Stormwind (Mathias Shaw) | Travel to SW to turn in. |
| 4 | 142 | The Defias Brotherhood (Part 3) | Stormwind | Westfall (Sentinel Hill) | Travel back. Triggers Deadmines pre-quest. |
| 5 | 155 | The Defias Brotherhood (Part 4) | Westfall (Sentinel Hill) | Deadmines (inside) | **Group quest.** Activity flips to `dungeon.deadmines`. |
| 6 | 168 | Halls of the Dead | Deadmines | Westfall | Loot from Edwin VanCleef. |

DAG edges (subset):

```
132 ──PrevQuest──► 135
135 ──PrevQuest──► 138
138 ──PrevQuest──► 142
142 ──PrevQuest──► 155
155 ──PrevQuest──► 168
135 ──ChainHint──► 138
138 ──ChainHint──► 142
…
```

Composer behavior for a level-15 Alliance Warrior at Sentinel Hill
with none of these completed:

1. Filter pass: all 6 nodes survive (level fits, race/class fits).
2. Topological order: `132 → 135 → 138 → 142 → 155 → 168`.
3. Synthesize Objectives:
   ```
   travel-to-132-pickup (Travel, Sentinel Hill)
   accept-132          (Interact, Captain Danuvin)
   kill-defias-trapper-q132-o0 (Kill, count=8, hotspots=Moonbrook road)
   travel-to-132-turnin (Travel, Sentinel Hill) [same NPC, collapsed]
   turnin-132          (TurnIn)
   accept-135          (Interact, Marshal Gryan Stoutmantle)
   kill-defias-messenger-q135-o0 (Kill, count=1, hotspots=Westfall coastal road)
   turnin-135          (TurnIn)
   accept-138          (Interact, same NPC)
   travel-to-138-turnin (Travel, Stormwind — Mathias Shaw at SI:7 HQ)
     [composer emits cross-zone travel; bot now passes from Westfall
      into Elwynn into Stormwind]
   turnin-138          (TurnIn)
   …
   ```
4. At step 155 the quest's `Type=Group` (`MinPartySize=5`); the
   ProgressionPlanner flips the Activity to `dungeon.deadmines` and
   the chain pauses until the bot quorums into a 5-person party
   (DungeoneeringCoordinator).

This sequence is **not stored anywhere**; it is reconstituted on
every `ComposeObjectives` call. As the bot progresses, its
`QuestsCompleted` snapshot field grows and the composer
short-circuits the completed nodes.

## 10. Per-zone quest catalogs (slot SQ.4)

To accelerate the composer (and to allow hand-tuning of preferred
chain orderings), `Bot/quests/zone-<name>.json` is authored per
zone with the primary chain quest IDs in turn-order plus optional
side quests. The composer prefers the authored order when present
but **falls back to DB-driven topological sort** when the JSON is
missing or out of date. Per
[`Plan/Activities/quests.md#sq4`](../../Plan/Activities/quests.md).

The JSON shape:

```json
{
  "zone": "Westfall",
  "primaryChains": [
    ["defias.132", "defias.135", "defias.138", "defias.142", "defias.155", "defias.168"]
  ],
  "sideQuests": ["coyote.cull", "redridge.expedition.start"],
  "skipChains": []
}
```

`skipChains` allows a hand-curated "skip this dead-end chain" hint
(e.g. green-XP chains that exist in DB but rarely worth the time).

## 11. ML-aided quest-chain ordering (the optimizer)

The §4 filter + §3 cross-reference (DAG topological sort) is **chain-
correct** but not **chain-optimal**. Two independent observations
explain why:

- **Inter-chain interleaving.** When a bot in Westfall has the
  Defias chain (§9) AND the side quests `coyote.cull` and
  `westfall.gnoll-cleanup` all eligible, the topological sort gives
  *one* valid linear order — but multiple are valid (the side quests
  have no `PrevQuest` edges into the Defias chain, so they can
  interleave anywhere). Different interleavings produce different
  travel distances, different mob-density overlap, and therefore
  different wall-clock-per-XP rates.
- **Cross-zone hub timing.** Step 3 of §9 (Defias Part 2) takes the
  bot to Stormwind. If the bot ALSO has `redridge.expedition.start`
  (a side quest that begins in Stormwind), interleaving the
  Stormwind side quest at that moment is "free" in travel cost. The
  composer doesn't know this without empirical data — the per-zone
  catalog (§10) hand-codes some of it; ML can learn more.

The **quest-chain ordering optimizer** is the ML-aided counterpart
to §4 + §9. It reuses the
[`Spec/20 §2 GetObjectiveAdviceAsync`](../../Spec/20_DECISION_ENGINE.md#2-service-surface)
advisor (no new RPC) but with `tied_objective_ids` populated from
**concurrently-eligible chain heads** rather than just composer ties.

```
ComposeQuestingObjectives produces:
  primary_chain  = [accept-132, ..., turnin-168]      # Defias §9
  side_quest_q47 = [accept-47,  ..., turnin-47]       # coyote.cull
  side_quest_q83 = [accept-83,  ..., turnin-83]       # gnoll-cleanup

Topological sort produces multiple valid linearizations:
  Order A: 132 → 47 → 135 → 83 → 138 → 142 → 155 → 168
  Order B: 132 → 47 → 83 → 135 → 138 → 142 → 155 → 168
  Order C: 132 → 135 → 47 → 138 → 142 → 83 → 155 → 168

Optimizer queries GetObjectiveAdviceAsync with:
  tied_objective_ids = ["accept-132", "accept-47", "accept-83"]
  (the three chains' heads that are all currently eligible)

Advisor returns the recommended FIRST step. Bot picks it up; on
turnin, the optimizer queries again with the new tied set. The
process iterates as the snapshot mutates.
```

### Three maturity phases (mirrors Spec/20 §5)

| Phase | Source | Per-quest scoring used |
|---|---|---|
| 1 — Heuristic | Lowest current-zone travel distance + highest XP-per-step (XP from `quest_template.RewMoneyMaxLevel` + the bot's level XP curve) | `Services/WoWStateManager/Progression/QuestChainHeuristic.cs` |
| 2 — Rules + lookup | `Bot/quests/zone-<name>.json` (§10) authoritative when present; per-bot-class overrides via `Config/decision-engine/quest-chain-rules.json` | per-zone JSON wins over DB-driven sort |
| 3 — ONNX | `Services/DecisionEngineService/Models/objective/v1.onnx` extended with quest-chain features: in-zone-distance, XP-per-hour history, drop-rate stats, party-availability lookahead | trained on `tmp/test-runtime/traces/Quest_*/outcome.jsonl` |

### Fail-soft fallback

`NoAdvice` (timeout / service down / model unavailable / confidence
< 0.5 / recommended id outside concurrently-eligible set) → fall
back to the §3 deterministic topological sort + the §10 per-zone
hand-authored order when present. The optimizer **cannot** invent
quest IDs outside the per-bot-filtered DAG; it can only reorder
already-valid options.

### Live-validation guard

Replaying any Quest_* trace with the optimizer forced to `NoAdvice`
MUST still produce a completing quest chain whose
`outcome.roster_distance_delta ≤ 0` (Spec/05 §RosterPlanner.Distance
metric). The deterministic stack always closes the chain; the
optimizer just trims wall-clock.

## 12. Worked example — "Defias chain + 2 side quests, ML-aided"

Setup (extends §9):

- Bot: level-15 Alliance Warrior at Sentinel Hill, none of the §9
  Defias chain completed.
- Side quest A: `coyote.cull` (eligible, pickup at Sentinel Hill,
  hotspot is the Westfall coyote pack 200 yd south).
- Side quest B: `westfall.gnoll-cleanup` (eligible, pickup at
  Sentinel Hill, hotspot is the gnoll camp 400 yd east).
- Side quest C: `redridge.expedition.start` (eligible, pickup at
  Lakeshire — but the §9 chain takes the bot through Stormwind on
  step 3, so Lakeshire is "free travel" THEN).

Composer §3 produces the topological order from §9 step 2 plus the
side quests interleavable anywhere. Three concurrently-eligible
chain heads at t=0: `accept-132` (Defias chain), `accept-coyote`
(side A), `accept-gnoll` (side B). `accept-redridge` is also
eligible but blocked by *spatial* gate (bot is in Westfall, Lakeshire
is in Redridge — composer holds it for the cross-zone-hub trip).

### With Phase-1 heuristic

`QuestChainHeuristic.Score(chain_head, bot.Position)`:
- `accept-132`: pickup at Sentinel Hill (0 yd); 8 mobs to kill;
  estimated 12 minutes. Score by lowest travel: top.
- `accept-coyote`: pickup at Sentinel Hill (0 yd) but hotspot 200
  yd south; estimated 5 minutes.
- `accept-gnoll`: pickup at Sentinel Hill (0 yd) but hotspot 400
  yd east; estimated 8 minutes.

Heuristic picks `accept-coyote` (lowest hotspot distance + shortest
duration). After turnin, the optimizer queries again with
`["accept-132", "accept-gnoll"]` and picks one based on the same
heuristic.

### With Phase-2 rules

`Bot/quests/zone-westfall.json` declares:
```jsonc
{
  "primaryChains": [["defias.132", "defias.135", "defias.138", ...]],
  "sideQuests": ["coyote.cull", "westfall.gnoll-cleanup"],
  "preferredInterleave": [
    {"after": "defias.132", "insert": ["coyote.cull"]},
    {"after": "defias.135", "insert": ["westfall.gnoll-cleanup"]}
  ]
}
```

Phase-2 advisor returns `RecommendedObjectiveId="accept-132"` first
(the primary-chain head wins under hand-authored ordering).
`accept-coyote` is inserted after `turnin-132` per `preferredInterleave`.

### With Phase-3 ONNX

The model has trained on 500+ Westfall L15-Alliance-Warrior traces.
It learns:
- `accept-132` first → bot stays at Sentinel Hill for kill-trapper +
  turnin → only THEN does the coyote/gnoll fan out make sense
  because the bot is mid-zone.
- BUT if the bot has `CharacterRosterGoal.GoldTargetCopper` close to
  goal (gold axis dominant in `RosterPlanner.Distance`), the model
  picks `accept-coyote` first because coyote pelts vendor for 32c
  each → faster gold-axis closure.

Model returns advice that depends on `roster_goal_distance[8]`
(Spec/20 §2.1) — the bot's current axis-distribution determines the
order. **Dynamic-progressive invariant holds**: a different bot with
gold-axis dominance gets a different recommendation; both bots'
chains complete and close `RosterPlanner.Distance`.

### Cross-zone optimization preserved

Whichever phase is active, step 3 of §9 (Defias Part 2 turnin at
Stormwind) is the trigger to evaluate `accept-redridge`. The
optimizer queries with `tied_objective_ids = ["travel-to-138-turnin",
"travel-to-redridge-expedition-pickup"]` — and the right answer is
to do them together. Cross-zone-hub interleaving is the strongest
empirical win the optimizer brings over the deterministic sort.

## 13. Dynamic-progressive invariant (quest-chain-ordering side)

Per the loop's invariant (Spec/19 §10, Spec/05 §Dynamic-progressive
invariant), the quest-chain optimizer MUST satisfy:

1. **Dynamic.** Two snapshots that differ in
   `(Race, Class, Level, QuestsCompleted, Reputation,
   roster_goal_distance)` MUST sometimes produce different
   chain-head pick orders — the §12 example shows gold-axis
   dominance flipping the Phase-3 pick from `accept-132` to
   `accept-coyote`. Identical snapshots produce identical orders
   (the optimizer is deterministic given fixed `Mode` and feature
   inputs).
2. **Progressive.** Every chain-head selection MUST belong to the
   §4 filtered DAG and produce non-positive
   `outcome.roster_distance_delta` on turnin. The optimizer cannot
   pick a quest that gates progression backward — only one that
   closes distance, possibly more efficiently than the
   deterministic sort.

Asserted at the §test surface by
`AotaQuestChains_OptimizerProducesDifferentOrderPerRosterAxisDominance`
in the test file below.

## 14. Plan-slot cross-reference

| Slot | Owns | Section here |
|---|---|---|
| [`Plan/03/S2.0`](../../Plan/03_PHASE2_ONDEMAND_ENGINE.md#s20--iactivity--iobjective-runtime-contracts) | `IActivityComposer.Compose(...)` (calls `ComposeQuestingObjectives`) | §3-§9 |
| [`Plan/14/S10.2`](../../Plan/14_PHASE10_DECISION_ENGINE_INTEGRATION.md#s102--objective-composer-tie-breaker) | `ObjectiveTieBreaker.cs` (extended to consume chain-head tied sets) | §11 optimizer |
| [`Plan/14/S10.6`](../../Plan/14_PHASE10_DECISION_ENGINE_INTEGRATION.md#s106--mode-aware-advisor-activation) | `Config/decision-engine/quest-chain-rules.json` + Mode flip | §11 Phase 2 + Phase 3 |
| [`Plan/14/S10.7`](../../Plan/14_PHASE10_DECISION_ENGINE_INTEGRATION.md#s107--training-trace-plumbing) | Trace pipeline that feeds Phase-3 training | §11 ONNX input |
| [`Plan/13`](../../Plan/13_PHASE9_CATALOG_FILL.md) | Per-zone quest catalogs `Bot/quests/zone-<name>.json` | §10 + §11 Phase 2 |
| **(no slot yet — Plan follow-up)** | `Services/WoWStateManager/Progression/QuestChainHeuristic.cs` | §11 Phase 1 |

The "no slot yet" entry joins the Plan-follow-up roster (12th
orphan: `QuestChainHeuristic.cs`).

## 15. Test surface (optimizer-side; deterministic §3 still tested by aota/03)

Contract tests live at
`Tests/BotRunner.Tests/Activities/QuestChainOrderingContractTests.cs`.
Assertions go through `snapshot.current_objective_id` (Spec/19 field
34) and `snapshot.advice_log[]` entries with `advisor="objective"`.

- **`QuestChain_TopologicallyValidOrderingsAreAllAccepted`** — three
  hand-built valid linearizations of the §9 Defias chain + side
  quests all pass the topological-sort validation; the heuristic
  picks one based on travel cost.
- **`QuestChain_OptimizerCannotInventQuestOutsideFilteredDag`** — a
  DecisionEngine stub returning `RecommendedObjectiveId="accept-9999"`
  (not in the per-bot-filtered DAG) is discarded; the deterministic
  fallback applies.
- **`QuestChain_PreferredInterleaveJsonOverridesTopologicalOrder`** —
  with `Bot/quests/zone-westfall.json` declaring `preferredInterleave:
  [{after: "defias.132", insert: ["coyote.cull"]}]`, the composer
  emits `accept-coyote` after `turnin-132` regardless of pure
  topological sort.
- **`AotaQuestChains_OptimizerProducesDifferentOrderPerRosterAxisDominance`** —
  the dynamic-progressive invariant. For two synthetic snapshots
  identical except for `roster_goal_distance[GoldTargetPct]` (one
  near-goal, one far-from-goal), Phase-3 ONNX produces different
  chain-head picks; both completions close `RosterPlanner.Distance`.
