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
