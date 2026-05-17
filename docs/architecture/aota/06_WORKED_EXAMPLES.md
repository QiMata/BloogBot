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
      → emits ActionType.TravelTo
      → pushes PathfindingClient.RequestRoute (multi-leg)
      → emits ActionType.StartMovement
      → ... (15-20 ticks of movement)
      → emits ActionType.StopMovement on arrival
  Tick after GoToTask completes:
    push PullStrategyTask(unit=nearest hostile DefiasTrapper)
      → emits ActionType.SetSelection
      → emits MSG_RAID_TARGET_UPDATE (skull marker)
      → emits ActionType.CastSpell("Throw")
      → pop on unit aggression
    push PvERotationTask(WarriorArmsPveRotation)
      → emits ActionType.CastSpell("Mortal Strike")
      → emits ActionType.CastSpell("Rend")
      → emits ActionType.StartMeleeAttack
      → pop on target Health == 0
    push LootCorpseTask(corpseGuid)
      → emits ActionType.Loot (via IObjectManager.LootTargetAsync internally)
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
[3]  accept-bg-invite                 Action    ActionType.AcceptBattlegroundInvite
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
    InteractAndQueue      → InteractWithNpcTask, emit ActionType.JoinBattlegroundQueue
    WaitForInvite         → poll BattlegroundNetworkClientComponent.CurrentState
    AcceptInvite          → emit ActionType.AcceptBattlegroundInvite (CMSG_BATTLEFIELD_PORT)
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
