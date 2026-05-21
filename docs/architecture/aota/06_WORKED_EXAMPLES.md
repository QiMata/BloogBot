# 06 — Worked examples: end-to-end A→O→T→A traces

> Prerequisite: [`02_GAME_LOOPS.md`](02_GAME_LOOPS.md),
> [`03_DYNAMIC_COMPOSITION.md`](03_DYNAMIC_COMPOSITION.md).
>
> Four end-to-end examples. Each shows: the Activity catalog row, the
> generated Objective list (with the snapshot/DB inputs that produced
> each), the Task push sequence for one representative Objective, and
> the wire Actions emitted. Read these as the "skeleton" of an
> end-to-end integration test.

---

## Example 1 — `quest.zone.westfall` for a level-12 Alliance Warrior

**Bot snapshot (at Activity assignment):**

```
Race          = Human
Class         = Warrior
Level         = 12
Faction       = Alliance
Position      = (Goldshire, Elwynn, map=0)
QuestsCompleted = { 1, 7, 12, 39, 50, ... }       // 30 Elwynn quests
QuestsInLog   = { }
Reputation[Stormwind] = Friendly (4500 / 6000)
Hearthstone.BoundZoneId = Elwynn (Goldshire Inn)
Inventory     = standard L12 quest greens
Coinage       = 87 silver
```

**Activity row:**

```csharp
new ActivityDefinition {
    Id            = "quest.zone.westfall",
    Family        = ZoneQuesting,
    Location      = "Westfall",
    LevelRange    = new LevelRange(9, 18),
    FactionPolicy = new FactionPolicy(Alliance, AllowCrossFaction: false),
    MinPlayers    = 1, MaxPlayers = 1,
    TaskFamily    = "Questing",
    ExpectedDuration = TimeSpan.FromHours(6),
    ...
}
```

**Composer output (first 24 of ~92 Objectives):**

```
[0]  travel-to-sentinel-hill              Travel    target=(Sentinel Hill, -10520, 1037, 51)
[1]  rebind-hearthstone-sentinel-hill     Interact  npc=InnkeeperHeather
[2]  accept-103-poor-old-blanchy          Interact  npc=FarmerSaldean
[3]  accept-132-peoples-militia           Interact  npc=GryanStoutmantle
[4]  accept-104-westfall-stew             Interact  npc=Salma
[5]  accept-115-coast-isnt-clear          Interact  npc=ProtectorGariel
[6]  collect-feed-of-blanchy-q103         Collect   item=812 count=4 source=GO@Saldean farm
[7]  kill-defias-trapper-q132-o0          Kill      entry=458 count=8 hotspots=[Moonbrook road, Jansen Stead]
[8]  kill-okra-q104-o0                    Collect   item=815 count=8 source=creature@Westfall
[9]  kill-murloc-coastal-q115-o0          Kill      entry=465 count=6 hotspots=[Westfall coast]
[10] travel-to-sentinel-hill              Travel    target=(Sentinel Hill)
[11] turnin-103                           TurnIn    npc=FarmerSaldean
[12] turnin-132                           TurnIn    npc=GryanStoutmantle
[13] turnin-104                           TurnIn    npc=Salma
[14] turnin-115                           TurnIn    npc=ProtectorGariel
[15] accept-135-defias-brotherhood-pt1    Interact  npc=GryanStoutmantle    (chain continuation, just-turned-in 132)
[16] travel-to-defias-messenger-spawn     Travel    target=(Westfall coastal road)
[17] kill-defias-messenger-q135-o0        Kill      entry=470 count=1 hotspots=[coastal-road patrol]
[18] turnin-135                           TurnIn    npc=GryanStoutmantle
[19] accept-138-defias-brotherhood-pt2    Interact  npc=GryanStoutmantle
[20] travel-to-stormwind-mathias-shaw     Travel    target=(SI:7 HQ, Stormwind)   ← cross-zone
[21] turnin-138                           TurnIn    npc=MathiasShaw
[22] accept-142-defias-brotherhood-pt3    Interact  npc=MathiasShaw
[23] travel-to-sentinel-hill              Travel    target=(Sentinel Hill)
…
```

**Snapshot/DB inputs that drove each Objective:**

- `[0]` — `TravelTarget` from the catalog row + bot's current Position.
- `[1]` — `leveling-priority.md` Critical-path Hearth-rebind rule.
- `[2..5]` — `quest_template` filtered by zone, level, race, class; `PrevQuestId = 0`; not in `QuestsCompleted`.
- `[6]` — `quest_template.ReqItemId1` of quest 103 → `gameobject_loot_template` lookup → spawn at Saldean farm.
- `[7]` — `quest_template.ReqCreatureOrGOId1` = creature 458 → `creature` spawn table (>30 spawns).
- `[15]` — `quest_template.PrevQuestId = 132` of quest 135.
- `[20]` — `creature_involvedrelation.id` for quest 138 → SI:7 HQ NPC spawn in Stormwind (map=0, different zone).

**Task push sequence for Objective `[7]` `kill-defias-trapper-q132-o0`:**

```
push KillObjectiveTask(quest=132, creatureEntry=458, requiredCount=8, hotspots=[...])
  Tick 1:
    push GoToTask(hotspot[0] = (-10987, 1442, 38))
      → emits ObjectiveType.TravelTo
      → pushes PathfindingClient.RequestRoute (multi-leg)
      → emits ObjectiveType.StartMovement
      → ... (15-20 ticks of movement)
      → emits ObjectiveType.StopMovement on arrival
  Tick after GoToTask completes:
    push PullStrategyTask(unit=nearest hostile DefiasTrapper)
      → emits ObjectiveType.SetSelection
      → emits MSG_RAID_TARGET_UPDATE (skull marker)
      → emits ObjectiveType.CastSpell("Throw")
      → pop on unit aggression
    push PvERotationTask(WarriorArmsPveRotation)
      → emits ObjectiveType.CastSpell("Mortal Strike")
      → emits ObjectiveType.CastSpell("Rend")
      → emits ObjectiveType.StartMeleeAttack
      → pop on target Health == 0
    push LootCorpseTask(corpseGuid)
      → emits ObjectiveType.Loot (via IObjectManager.LootTargetAsync internally)
      → IObjectManager fires CMSG_LOOT, CMSG_AUTOSTORE_LOOT_ITEM(s), CMSG_LOOT_RELEASE
      → pop
  KillObjectiveTask increments local counter; reads
  snapshot.Player.QuestLogEntries[slotForQ132].QuestCounters[0]
  to verify SMSG_QUESTUPDATE_ADD_KILL was honored
  → loop to next hotspot until counter >= 8
  → on completion: pop (status = Complete)
```

**Resulting wire trace (representative subset):**

```
TravelTo(-10987, 1442, 38)            x1 (Activity request to BotRunner)
StartMovement                          x1
... (15s of movement ticks)
StopMovement                           x1
SetSelection(target=Trapper#1 guid)    x1
SetRaidTarget(unit=trap, marker=Skull) x1
CastSpell("Throw")                     x1
CastSpell("Mortal Strike")             x1 (rotation pick)
StartMeleeAttack                       x1
... (10s of melee)
Loot(corpseGuid)                       x1
... (repeated x8 hotspots)
```

---

## Example 2 — `dungeon.wailing-caverns` for a 5-bot Horde group at level 19

**Group composition (5 bots):**

```
Bot1: Tauren Warrior 19 (tank)        — bound at Crossroads
Bot2: Troll Priest   18 (healer)      — bound at Crossroads
Bot3: Orc Hunter     19 (dps melee/ranged) — bound at Crossroads
Bot4: Orc Warlock    18 (dps ranged)  — bound at Razor Hill
Bot5: Tauren Druid   19 (dps balance) — bound at Thunder Bluff
```

**Activity row:**

```csharp
new ActivityDefinition {
    Id            = "dungeon.wailing-caverns",
    Family        = Dungeon,
    Location      = "Wailing Caverns",
    LevelRange    = new LevelRange(17, 24),
    FactionPolicy = new FactionPolicy(Either, AllowCrossFaction: false),
    MinPlayers    = 5, MaxPlayers = 5,
    RoleTemplate  = new RoleTemplate(Tanks: 1, Healers: 1, Dps: 3),
    EntryRequirements = new EntryRequirements { },     // no key, no attune
    TravelTarget  = new TravelTarget(map: 1, x: -740, y: -2200, z: 17, "WC entrance"),
    ExpectedDuration = TimeSpan.FromMinutes(90),
    TaskFamily    = "Dungeoneering",
    ...
}
```

**Composer output (Objective sequence):**

```
[0]  group-form                         Social    role-fill via DungeoneeringCoordinator quorum
[1]  travel-to-WC-entrance              Travel    target=(map=1, -740, -2200, 17)   (per-bot)
[2]  enter-instance                     Interact  go=WailingCavernsPortal
[3]  clear-trash-to-evolved-anomaly     Encounter route: instance Bot/dungeons/wailing-caverns.json
[4]  kill-lady-anacondra                Encounter boss_entry=3671  plan=WC_Anacondra
[5]  loot-lady-anacondra                Collect
[6]  clear-trash-to-lord-cobrahn        Encounter
[7]  kill-lord-cobrahn                  Encounter boss_entry=3669
[8]  loot-lord-cobrahn                  Collect
[9]  clear-trash-to-lord-pythas         Encounter
[10] kill-lord-pythas                   Encounter boss_entry=3670
[11] loot-lord-pythas                   Collect
[12] clear-trash-to-skum                Encounter
[13] kill-skum                          Encounter boss_entry=3674
[14] loot-skum                          Collect
[15] clear-trash-to-lord-serpentis      Encounter
[16] kill-lord-serpentis                Encounter boss_entry=3673
[17] loot-lord-serpentis                Collect
[18] (optional) deviate-faerie-dragon-quest-kill (Druid only)  Kill
[19] (optional) dungeon-quest-turnin    TurnIn (handled at zone-out)
[20] mukla-pre-event                    Encounter (Disciple of Naralex)
[21] kill-mutanus-the-devourer          Encounter boss_entry=3654
[22] disciples-of-naralex-event-end     (Wait + listen)
[23] exit-instance                      Travel    target=(WC entrance outside)
```

**Source of the boss list:** `Bot/dungeons/wailing-caverns.json` (Phase 2
deliverable per slot SD.wailing-caverns) — hand-authored from
[`leveling-guide/dungeons/wailing-caverns.md`](../../leveling-guide/dungeons/wailing-caverns.md)
(file pending) and cross-checked against `creature_template.Rank=3`
(elite/boss-rank) spawns in map 43.

**Task push for Objective `[3]` `clear-trash-to-evolved-anomaly`:**

```
push DungeoneeringTask(map=43, leg="entrance-to-anomaly", isLeader=true, waypoints=[...])
  Tick:
    if Aggressors.Any:
      push CreatePvERotationTask(...)
    else if Leader and LineOfSight on nearest hostile:
      push PullStrategyTask(unit)
        SetTarget, MarkSkull, CastSpell("Throw" / pulls a single)
    else:
      TryNavigateToward(nextWaypoint)
        → emits StartMovement / StopMovement
```

**Cross-bot Action flow:**

- Tank's `DungeoneeringTask(isLeader=true)` decides pulls, marks skull.
- All 4 followers' `DungeoneeringTask(isLeader=false)` watches
  `PartyLeader.Position` and follows within 15y; on `Aggressors.Any` they
  push their own `PvERotationTask`.
- Healer's `PvERotationTask` differs: it's a `HolyPriestPveRotation`
  that prioritizes healing the lowest-HP party member.

---

## Example 3 — `prof.mining-route` for a level-25 Dwarf Warrior with Mining 75

**Bot snapshot:**

```
Class     = Warrior
Level     = 25
PrimaryProfessions = [(Mining, 75, max=150), (Blacksmithing, 60, max=150)]
Position  = (Thelsamar, Loch Modan)
Coinage   = 4 gold
```

**Activity row:**

```csharp
new ActivityDefinition {
    Id            = "prof.mining-route",
    Family        = ProfessionGathering,
    Location      = "Mining route",   // resolved at runtime to route file
    LevelRange    = new LevelRange(1, 60),
    FactionPolicy = new FactionPolicy(Either, AllowCrossFaction: false),
    TaskFamily    = "Gathering",
    ExpectedDuration = TimeSpan.FromHours(2),
    ...
}
```

**Route resolution:** Mining 75 means the bot can mine **Tin Veins**
(requires skill 65) and is **5 short of Iron** (requires 100). Composer
reads `Bot/gathering-routes/mining-tin.json` (hand-authored, points at
Loch Modan / Hillsbrad / Stonetalon depending on faction-side).

**Composer output:**

```
[0]  travel-to-route-start            Travel  target=(Loch Modan, -5300, -3900, 350)
[1]  route-loop-tin                   Loop    until skill >= 100 OR no nodes within radius
[2]  travel-to-thelsamar              Travel  target=Thelsamar
[3]  visit-mining-trainer             Interact  npc=Yarr Hammerstone
[4]  train-mining-journeyman-tier     Train   skill=Mining target=150
[5]  travel-to-route-start-iron       Travel  target=(Loch Modan, -5200, -4100, 350)
[6]  route-loop-iron                  Loop    until skill >= 150
[7]  travel-to-thelsamar              Travel
[8]  visit-mining-trainer             Interact
[9]  train-mining-expert-tier         Train   skill=Mining target=225
... (further iterations as bot's skill grows)
```

**Task push sequence for Objective `[1]` `route-loop-tin`:**

```
push GatheringRouteTask(nodeEntries={181249,1734 = Tin Vein, Copper Vein},
                        waypoints=[w0..wN])
  every Tick:
    1. scan ObjectManager.GameObjects for entry in nodeEntries within 30y
    2. if hit:
         push GatherNodeTask(nodeGuid)
           push GoToTask(nodeCoord)
           push InteractWithGameObjectTask(nodeGuid)
             → emits Interact action → triggers cast Lua/Spell
           wait for SPELL_GO + LOOT_RELEASE
           push LootCorpseTask(window-loot)
       else:
         push GoToTask(nextWaypoint)
    3. on skill >= 100: pop (status=Complete)
```

**Wire trace:**

```
TravelTo(waypoint[0])
StartMovement → StopMovement   (multiple legs)
SetSelection(tinVeinGuid)
Interact(tinVeinGuid)
... (cast time)
Loot(tinVeinGuid)
[ skill ticks 76, 77, 78 ... ]
```

---

## Example 4 — `bg.warsong-gulch` for a level-19 Druid in the 10-19 bracket

**Bot snapshot:**

```
Class     = Druid
Level     = 19                         (bracket 10-19; "twink" potential)
Position  = (Crossroads, Barrens)
QuestsCompleted = { ... }              (not relevant to BG)
HonorThisWeek = 1240
```

**Activity row:**

```csharp
new ActivityDefinition {
    Id            = "bg.warsong-gulch",
    Family        = Battleground,
    Location      = "Warsong Gulch",
    LevelRange    = new LevelRange(10, 60),
    FactionPolicy = new FactionPolicy(Either, AllowCrossFaction: false),
    MinPlayers    = 10, MaxPlayers = 10,
    RoleTemplate  = new RoleTemplate(Tanks: 0, Healers: 2, Dps: 8),
    TravelTarget  = new TravelTarget(map: 1, x: 1815, y: -4348, z: 4, "Orgrimmar Battlemaster"),
    ExpectedDuration = TimeSpan.FromMinutes(25),
    TaskFamily    = "Bg",
    ...
}
```

**Composer output:**

```
[0]  travel-to-battlemaster           Travel    target=Orgrimmar battlemaster
[1]  queue-for-wsg                    Queue     bgType=WarsongGulch
[2]  wait-for-invite                  Wait      timeout=5min then re-queue
[3]  accept-bg-invite                 Action    ObjectiveType.AcceptBattlegroundInvite
[4]  post-teleport-stabilize          Recovery  wait for valid Position + IsOnNavmesh
[5]  role-assignment                  Decision  ("flag carrier" | "defender" | "midfield")
[6]  flag-loop                        Loop      until 3 caps or BG ends
       sub-objective sequence (per loop iter):
         6a. travel-to-enemy-flagroom   Travel
         6b. interact-enemy-flag        Interact (pickup)
         6c. travel-to-own-flagroom     Travel
         6d. interact-own-flag          Interact (cap)
[7]  exit-bg                          Travel
[8]  collect-honor-rep                Mail/AH if marks of honor are eligible
```

**Task push sequence for Objective `[1]` `queue-for-wsg`:**

```
push BattlegroundQueueTask(bgType=WarsongGulch, expectedMapId=489)
  State machine:
    FindBattlemaster      → scan ObjectManager.Units by Entry+UNIT_NPC_FLAG_BATTLEMASTER
    MoveToBattlemaster    → push GoToTask(npc.Position)
    InteractAndQueue      → InteractWithNpcTask, emit ObjectiveType.JoinBattlegroundQueue
    WaitForInvite         → poll BattlegroundNetworkClientComponent.CurrentState
    AcceptInvite          → emit ObjectiveType.AcceptBattlegroundInvite (CMSG_BATTLEFIELD_PORT)
    WaitForEntry          → poll Player.MapId == 489
    Done                  → pop
```

**Wire trace (queue + entry):**

```
TravelTo(battlemaster.position)
... (movement)
SetSelection(battlemasterGuid)
Interact(battlemasterGuid)
SelectGossipOption(...)                ← opens BG dialog
JoinBattlegroundQueue(bgType=WSG)      ← CMSG_BATTLEMASTER_JOIN
... (wait for SMSG_BATTLEFIELD_STATUS = invited)
AcceptBattlegroundInvite               ← CMSG_BATTLEFIELD_PORT
... (post-teleport stabilize)
```

---

## Cross-example takeaways

1. **`GoToTask` appears in every example.** Travel-to-Position is the
   universal substrate. Improvements to the Travel family compound
   across every Activity family — see
   [`Plan/Activities/travel.md`](../../Plan/Activities/travel.md).

2. **The Objective layer is where tests assert.** Every Objective
   maps to a snapshot predicate (`QuestLog[slot].Counter == 8`,
   `nearbyUnits[bossGuid].health == 0`, `bagContents[itemId] >= 1`,
   `Player.MapId == 489`). Live-validation tests poll those
   predicates, not the Task stack.

3. **The Activity catalog is sparse; the Objective list is dense.**
   86 rows authored × ~20-100 Objectives composed at runtime = the
   full behavior surface, none of it hand-typed beyond the
   per-encounter JSON files.

4. **Recursive composition matters.** The UBRS example in
   [`05_ITEM_REQUIREMENTS.md`](05_ITEM_REQUIREMENTS.md) shows an
   Activity invoking another Activity's worth of Objectives through
   `RequiredItem` → quest chain. Travel decomposes into multi-leg
   travel. Crafting decomposes into reagent sourcing which may
   decompose into gathering routes. The leaf primitives stay small;
   the composition does the heavy lifting.

5. **ML accelerates, never reverses.** Example 5 below threads six
   advisor consultations through a single Activity: Activity
   selection ([aota/03 §9](03_DYNAMIC_COMPOSITION.md#9-ml-aided-composition-the-composer-learning-loop)),
   quest-chain ordering ([aota/04 §11](04_QUEST_CHAINS.md#11-ml-aided-quest-chain-ordering-the-optimizer)),
   cheapest-source learner ([aota/05 §9](05_ITEM_REQUIREMENTS.md#9-ml-aided-cheapest-source-learner)),
   sub-Objective tie-break, reward selector ([Spec/03 §Reward selection](../../Spec/03_BOTRUNNER.md#reward-selection)),
   and the one-shot personality cluster ([Spec/24 §11](../../Spec/24_BEHAVIORAL_VARIATION.md#11-ml-integration--personality-clustering)).
   Each advisor has a fall-soft path; the worst-case "every advisor
   NoAdvice" run still completes the chain and still closes roster
   distance — just ~25-30% slower. The deterministic stack is the
   floor; ML is the ceiling, not a wall.

## Example 5 — ML-aided composition for a level-40 Mage zone transition

Setup:

- Bot: Gnome Frost Mage, level 40, Alliance, currently in **Eastern
  Plaguelands** (Light's Hope Chapel). Quests completed: most of the
  Plaguelands starter quests (level 35-42 bracket exhausted),
  including the Argent Dawn intro turn-in. Reputation: Argent Dawn
  *Friendly* (3000/6000 toward Honored).
- `CharacterRosterGoal.TargetLevel = 60`,
  `Reputations = [{factionId=529 ArgentDawn, MinStanding=Revered}]`,
  `Attunements = ["attune.naxx"]` (Argent Dawn Revered is a Naxx
  prerequisite).
- Snapshot's 8-axis `roster_goal_distance` (Spec/05):
  `Level=0.33, GearTier=0.42, AttunementStep=0.62, ReputationTier=0.18,
   GoldTargetPct=0.10, MountTier=0.50, PvPRank=0.00,
   ProfessionSkill=0.40`. Total scalar ≈ 0.31; **AttunementStep is
  dominant**.

The composer just completed `quest.zone.eastern-plaguelands` and is
picking the next Activity. Three Activities pass §3 algorithm
filtering for this bot:

| Candidate Activity | Family | Heuristic priority |
|---|---|---|
| `quest.zone.western-plaguelands` | ZoneQuesting | 500 (default) |
| `reputation.argent-dawn` | Reputation | 500 (default) |
| `dungeon.stratholme-undead` (UD wing) | Dungeon | 500 (default) |

All three tie on band, tie on roster-priority, tie on level fit. The
deterministic §3 step 5 sort would lex-sort by Activity Id and pick
`dungeon.stratholme-undead`. The ML-aided composer (Plan/14 S10.6 ML
mode active) does six advisor consultations during the ensuing
Activity. Each consultation is shown below with the **Phase-1
fallback**, the **Phase-3 ML pick**, and the **resulting trace
line**.

### Consultation 1 — Activity selection (composer learning loop)

Surface: `GetObjectiveAdviceAsync` ([aota/03 §9](03_DYNAMIC_COMPOSITION.md#9-ml-aided-composition-the-composer-learning-loop)).

```
ObjectiveContext sent:
  tied_objective_ids = [
    "activity-quest-zone-western-plaguelands",
    "activity-reputation-argent-dawn",
    "activity-dungeon-stratholme-undead"
  ]
  tied_objective_costs = [estimated 4h, estimated 3h, estimated 1.5h]
  tied_unlock_fanout = [12, 28, 7]  # AD-rep unlocks the most
  roster_goal_distance = [0.33, 0.42, 0.62, 0.18, 0.10, 0.50, 0.00, 0.40]

Phase-1 fallback:
  Lex sort → "activity-dungeon-stratholme-undead" wins. Bot starts
  Strat-UD pug attempt.

Phase-3 ML pick (model trained on 200+ similar L40-attune-grind traces):
  RecommendedObjectiveId = "activity-reputation-argent-dawn"
  Confidence = 0.78
  Rationale = "AttunementStep axis (0.62) dominates roster distance.
               Argent Dawn rep grind closes 0.16 of that axis per hour
               (turn-in chains in WPL accept Scourgestone +
               Bone-fragment caches, which incidentally drop from WPL
               mobs the bot will fight regardless). Stratholme-UD
               would close the same axis only on group quorum, which
               is unreliable solo."

Trace line produced:
  {"ts":..., "kind":"advice_response", "request_id": 9001,
   "advisor": "objective", "mode_used": "Ml", "model_version": "v1.0.3",
   "advice": {"recommended_objective_id": "activity-reputation-argent-dawn",
              "confidence": 0.78,
              "rationale": "AttunementStep axis dominates..."},
   "used_by_caller": true}
```

Bot picks **`reputation.argent-dawn`**. The composer §3 algorithm
then synthesizes its Objective sequence per
[03_DYNAMIC_COMPOSITION.md §4](03_DYNAMIC_COMPOSITION.md#4-worked-composequestingobjectives).

### Consultation 2 — Quest-chain ordering optimizer

Surface: `GetObjectiveAdviceAsync` ([aota/04 §11](04_QUEST_CHAINS.md#11-ml-aided-quest-chain-ordering-the-optimizer)).

The composer's per-bot DAG filter (aota/04 §4) returns three
concurrently-eligible chain heads in WPL:

- `accept-5505` "Glyphic Letter" (Argent Dawn turn-in chain head;
  rewards 250 rep).
- `accept-5901` "The Crimson Courier" (side quest, rewards 25 rep +
  4g80s).
- `accept-5805` "Scourge Bones" (collect Bone Fragments; rewards 75
  rep per turn-in, repeatable until Revered).

```
ObjectiveContext:
  tied_objective_ids = ["accept-5505", "accept-5901", "accept-5805"]
  tied_objective_costs = [35min, 22min, 18min]    # heuristic
  tied_unlock_fanout = [3, 0, 0]                  # only 5505 chains

Phase-1 fallback (lex sort): "accept-5505" first.

Phase-3 ML pick: "accept-5805" first.
  Rationale = "Repeatable bone-fragment turn-in stacks empirically;
               bot's bag has 4 Scourge Bones from EPL transit
               already. Turn-in NOW yields 300 rep with zero
               additional kill time. 5505 chain head is best after
               5805 immediate turn-in."
```

The composer pushes `accept-5805` first. After turnin, the optimizer
re-queries — now with the bot in WPL central plot — and picks
`accept-5505` (the chain) next, since the bone-fragment opportunistic
gain is realized.

### Consultation 3 — Cheapest-source learner (item gate)

Surface: `GetObjectiveAdviceAsync` ([aota/05 §9](05_ITEM_REQUIREMENTS.md#9-ml-aided-cheapest-source-learner)).

`accept-5505` "Glyphic Letter" is itself a precondition-gated quest:
the bot must already have a **Scourgestone** in inventory
(itemId=12840). §4 walks the provenance DAG:

```
ResolveItemSource(12840, bot, db, market):
  sources = [
    Source(DROP, creature="Wandering Skeleton", chance=0.04,
           est_cost=15min, hotspot=WPL Felstone Field),
    Source(DROP, creature="Plaguebat", chance=0.02,
           est_cost=28min, hotspot=WPL Northridge Lumber Camp),
    Source(AH, listing={item:12840, count:1, buyout:1g50s},
           bot.Coinage=8g50s ≥ 1g50s, est_cost=10min travel-to-Booty-Bay-AH),
  ]
```

```
Phase-1 fallback: sources.minBy(s => s.cost) → AH (10min).

Phase-3 ML pick: DROP (Wandering Skeleton).
  Rationale = "AH listing 4 minutes old; market scrape stale rate
               historically ~30% for L40-band 1g listings — bot
               races to AH, listing gone, takes 20+min real time.
               Wandering Skeleton historical actual_cost_p50 for
               L40 Mage = 12min (heuristic 15min overestimate).
               DROP also yields ~80 XP per kill on the way, closing
               Level axis (0.33 → 0.31)."
```

Bot heads to Felstone Field and farms Wandering Skeletons. Within
14 wall-clock minutes the Scourgestone drops. Bot now has the
prerequisite item.

### Consultation 4 — Composer tie-break on next sub-Objective

Surface: same `GetObjectiveAdviceAsync` ([aota/03 §9](03_DYNAMIC_COMPOSITION.md#9-ml-aided-composition-the-composer-learning-loop)) but at a sub-Objective scope.

After the Scourgestone drops, the composer must decide between two
tied Objectives in the bot's queue:

- `travel-to-5505-pickup` (Glyphic Letter pickup at Light's Hope).
- `turnin-5805` (Scourge Bones repeatable turn-in at Chillwind Camp).

Both are valid; both close `AttunementStep` and `ReputationTier`.

```
Phase-1 fallback (lex sort): "travel-to-5505-pickup" first.

Phase-3 ML pick: "turnin-5805" first.
  Rationale = "Light's Hope and Chillwind are both 4-5min flight;
               Chillwind has the AH and mailbox the bot needs to
               liquidate the 6 Bone-Fragment-side-drop items
               (Sharp Claw, etc., ~3g vendor each). Travel to
               Chillwind first amortizes the city-services side
               quests; bot's GoldTargetPct (0.10) also closes."
```

### Consultation 5 — Reward selector

Surface: `GetRewardAdviceAsync` ([Spec/03 §Reward selection](../../Spec/03_BOTRUNNER.md#reward-selection)).

`turnin-5505` "Glyphic Letter" offers 3 reward choices: a head item,
a chest item, an off-hand frill. Bot is a Mage; the head and chest
both match the spec's `TargetGearSet`.

```
RewardContext:
  reward_item_ids = [18746, 18747, 18748]   # synthetic ids
  reward_item_quality = [3, 3, 2]            # both head + chest are blue
  reward_item_slot = [HEAD, CHEST, BACK]
  reward_item_sell_price = [400c, 350c, 80c]
  currently_equipped_in_slot = [18001, 18002, 18003]   # bot's current

Phase-1 fallback (first-valid by index): index 0 (HEAD).

Phase-2 rules (BiS table at Config/decision-engine/reward-rules.json):
  FrostMage @ L40 reward-rules.json declares head=BiS-priority,
  chest=second. Picks index 0 (HEAD).

Phase-3 ML pick: index 1 (CHEST).
  Rationale = "Bot's current head (18001) is already mid-tier blue;
               chest slot (18002) is the lower-quality green that
               the Mage spec's BiS rule book flags as 'upgrade soon'.
               Picking CHEST closes more GearTier-axis distance.
               PersonalityProfile.RewardPriority=Bis honors this."
```

### Consultation 6 — Personality jitter applied on every Tick

Surface: `PersonalityProfile.Jitter("ReactionTimeJitterMs")` ([Spec/24 §6](../../Spec/24_BEHAVIORAL_VARIATION.md#6-variance-application-via-task-base-class)).

Every Task pop / push adds 50-250 ms of reaction-time jitter from
the bot's `PersonalityProfile` (deterministic per accountName). This
is the *only* per-tick advisor consultation — actually no advisor
call; the personality is a one-shot at profile generation
([Spec/24 §11](../../Spec/24_BEHAVIORAL_VARIATION.md#11-ml-integration--personality-clustering)).

For this bot ("GNOMEFROST07" hash → cluster `talkative-altoholic`),
`ReactionTimeJitterMs = 187` and `IntraRotationJitterMs = 64`. Two
other bots running the same Activity at the same time emit
discernibly different `dispatchedAtMs` cadence in their traces.

### Outcome and the dynamic-progressive invariant

After 2h 15min (vs 3h heuristic estimate; the ML path saved 45min),
the bot reaches AD *Honored* (4500/6000 → 6000/6000 Honored, 750/8400
into Revered). Outcome trace line:

```json
{"ts":..., "kind":"outcome",
 "activity_id":"reputation.argent-dawn",
 "completion":"complete",
 "wall_clock_ms": 8100000,
 "xp_gained": 18750,
 "gear_slots_filled": 1,       // chest swap from consult 5
 "gold_delta_copper": +24500,  // bone-fragment side-drops sold
 "roster_distance_delta": -0.087}
```

`roster_distance_delta = -0.087` — the Activity strictly closed
distance. Replaying the same scenario with **all six advisors forced
to `NoAdvice`** (Mode=Trivial) would have produced
`roster_distance_delta ≈ -0.061` over 3h (the Phase-1 fallback path
is slower but still progressive). The ML path saved 45 minutes AND
closed +0.026 additional distance — but **the deterministic floor is
preserved**: ML accelerates closure; it cannot reverse it.

This is the dynamic-progressive invariant in concrete form:

- **Dynamic.** A bot with `Level`-axis dominance instead of
  `AttunementStep`-axis dominance (e.g. a different roster goal)
  would have gotten Consultation 1's advice toward `quest.zone.western-plaguelands`
  instead of `reputation.argent-dawn` — same composer, different
  pick, different trace.
- **Progressive.** Every advisor's pick had a fall-soft path. The
  worst-case ML outage (every advisor returns `NoAdvice`) still
  completes the chain and still closes roster distance — just
  ~25-30% slower.

## Plan-slot cross-reference (ML-aided example)

The six advisor consultations in Example 5 each pin to a Plan/14
slot:

| Consultation | Surface | Plan/14 slot |
|---|---|---|
| 1 Activity selection | `GetObjectiveAdviceAsync` (composer-level) | [S10.2](../../Plan/14_PHASE10_DECISION_ENGINE_INTEGRATION.md#s102--objective-composer-tie-breaker) |
| 2 Quest-chain ordering | `GetObjectiveAdviceAsync` (chain-head tied set) | [S10.2](../../Plan/14_PHASE10_DECISION_ENGINE_INTEGRATION.md#s102--objective-composer-tie-breaker) |
| 3 Cheapest-source learner | `GetObjectiveAdviceAsync` (source-candidate tied set) | [S10.2](../../Plan/14_PHASE10_DECISION_ENGINE_INTEGRATION.md#s102--objective-composer-tie-breaker) |
| 4 Sub-Objective tie-break | `GetObjectiveAdviceAsync` (composer-level again) | [S10.2](../../Plan/14_PHASE10_DECISION_ENGINE_INTEGRATION.md#s102--objective-composer-tie-breaker) |
| 5 Reward selector | `GetRewardAdviceAsync` | [S10.1](../../Plan/14_PHASE10_DECISION_ENGINE_INTEGRATION.md#s101--reward-selector-advisor-wire) |
| 6 Personality cluster | `GetPersonalityClusterAdviceAsync` (one-shot at profile gen) | [S10.11](../../Plan/14_PHASE10_DECISION_ENGINE_INTEGRATION.md#s1011--personalitycluster-advisor-wire) |

Trace capture for the example is owned by [`Plan/14 S10.7`](../../Plan/14_PHASE10_DECISION_ENGINE_INTEGRATION.md#s107--training-trace-plumbing);
the live-validation guard is [`Plan/14 S10.8`](../../Plan/14_PHASE10_DECISION_ENGINE_INTEGRATION.md#s108--livevalidation-for-advisor-wire).

## Test surface (Example 5 specifically)

Contract tests live at
`Tests/BotRunner.Tests/Activities/MlAidedWorkedExampleContractTests.cs`.
Assertions go through trace JSONL files emitted to
`tmp/test-runtime/traces/MlAidedWorkedExample_*/` plus
`snapshot.advice_log[]` (Spec/19 field 36) entries.

- **`MlAidedWorkedExample_Consultation1_FallsBackOnNoAdvice`** —
  replaying the Example 5 scenario with the Activity-selection
  advisor pinned to `NoAdvice` produces the Phase-1 lex-fallback pick
  (`dungeon.stratholme-undead`), and the Activity still completes
  with `outcome.roster_distance_delta ≤ 0`.
- **`MlAidedWorkedExample_AllSixConsultationsLogged`** — a real Ml-
  mode run produces ≥ 6 `advice_log` entries across the Activity,
  one per Consultation in §Example 5, with `advisor` values covering
  `{objective, reward, personality_cluster}`.
- **`MlAidedWorkedExample_DynamicProgressive_OutcomeDeltaIsNonPositiveTest`** —
  the dynamic-progressive guard. Two synthetic snapshots identical
  except for axis dominance (`AttunementStep` vs `Level`) produce
  different Consultation 1 picks (`reputation.argent-dawn` vs
  `quest.zone.western-plaguelands`); both completions trace
  `outcome.roster_distance_delta ≤ 0`. The all-advisors-NoAdvice
  replay also completes with non-positive delta — the deterministic
  floor.
