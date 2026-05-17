# 07 — Cross-game portability template

> Prerequisite: [`01_LAYERS.md`](01_LAYERS.md), [`03_DYNAMIC_COMPOSITION.md`](03_DYNAMIC_COMPOSITION.md).
>
> The AOTA model is **game-agnostic**. This file is the template for
> porting it to the other game folders in the monorepo. Game-specific
> details change; the four-layer structure and the dynamic-composition
> algorithm do not.

## 1. What's portable, what isn't

### Portable (no per-game change)

- The **four layers** and their semantics (Activity / Objective / Task / Action).
- The **recursive composition rules** (R-A1, R-A2, R-T1..R-T3, R-O1..R-O3, R-AC1..R-AC2, R-W1; see [`01_LAYERS.md`](01_LAYERS.md)).
- The **task-stack execution model** (LIFO, push-child semantics, async lifecycle).
- The **`ComposeObjectives`** algorithm shape (catalog row + snapshot + DB + unlock graph → ordered Objective list).
- The **priority-band scheme** (Survival → Class Identity → Critical Path → Optimal → Background).
- The **separation of OnDemand vs Autonomous** flows and the legality validator's "fixup mode."
- The **test-naming convention** (`Activity × Objective` per test class) and the snapshot-driven assertion rule.

### Game-specific (must be re-authored per game)

- The `ActivityFamily` enum values (WoW has Quest/Dungeon/Raid/BG; FFXI has Mission/CampaignBattle/Mog-house/etc.; D2 has Areas/Bosses/Quests/Cube-Recipes).
- The catalog row fields beyond the common core (`LevelRange`, `Location`) — e.g. WoW's `RoleTemplate` doesn't apply identically to D2.
- The list of `Task` families in `Spec/03_BOTRUNNER.md#catalog-of-task-families`.
- The `ActionType` enum (each game has its own wire surface).
- The DB schema the composer reads (MaNGOS in WoW, LandSandBoat in FFXI, etc.).
- The unlock-graph node namespaces and the priority bands' specifics.

## 2. Per-game porting checklist

For each game directory in the monorepo (FFXI, WAR, UO, EQ, EQ2, PSO,
Ragnarok, SWG, D2, ...), produce a corresponding `docs/architecture/aota/`
tree following this checklist:

```
□ README.md
   - one-paragraph model recap pointing at this doc + the monorepo root CLAUDE.md.
   - per-game inventory: list the catalog families this game uses.

□ 01_LAYERS.md  (lighter than WoW's; just per-game specifics)
   - which ActionType enum values exist in this game's communication.proto
   - which IBotTask families have implementations
   - what the snapshot proto carries for each layer's identity field

□ 02_GAME_LOOPS.md
   - one section per top-level loop in this game
     (e.g. FFXI: missions / quests / merit-grind / crafting / fishing / besieged / chocobo / mog-house / synthesis loops)
     (e.g. D2: act-progression / bossing / cow-runs / Baal-runs / crafting / shopping / cube-recipes / leveling)
     (e.g. WAR: PQ / scenario / open-RvR / questing / influence-grind / crafting)

□ 03_DYNAMIC_COMPOSITION.md
   - the per-game DB tables the composer reads (LandSandBoat schema for FFXI; OpenD2 .d2s/.d2x for D2; etc.)
   - the snapshot fields that gate composition
   - the per-family Compose* sub-algorithms

□ 04_<game-specific-DAG-1>.md   (e.g. 04_QUEST_CHAINS.md for WoW; 04_MISSION_CHAINS.md for FFXI)
□ 05_<game-specific-DAG-2>.md   (e.g. 05_ITEM_REQUIREMENTS.md for WoW; 05_CUBE_RECIPES.md for D2)
□ 06_WORKED_EXAMPLES.md
   - 3-5 representative end-to-end examples
□ 07_PORTABILITY.md (this template; cross-link)
```

## 3. Mapping the model onto each game in the monorepo

The monorepo today carries these games (per [root CLAUDE.md](../../../../CLAUDE.md#projects)):

### WoW 1.12.1 (`Westworld of Warcraft/`) — the reference

This doc tree.

### Final Fantasy XI (`Final Fantasy XI/FFXIBot/`)

| AOTA Layer | FFXI mapping |
|---|---|
| Activity | Mission (Bastok 1-1 → CoP → Sea/Sky), Quest, Skill-up grind, Crafting session, Fishing session, Besieged campaign, Mog-garden tend, Conquest-tally cycle, Field-of-Valor / Grounds-of-Valor session. |
| Objective | "reach Selbina dock", "defeat orc warlord NPC", "synth 12 stacks of fire crystals", "claim NM `Stray Mary`". |
| Task | `RunToPosTask`, `EngageAndKillTask`, `SynthRecipeTask`, `CallChocoboTask`, `BoardAirshipTask`, `ZoneTask`. |
| Action | FFXI client packets: `InpZoneClientGrid`, `InpAction`, `InpEquipChange`, `InpSynth`, etc. — defined in `FFXIBot/Exports/.../communication.proto`. |
| DB the composer reads | LandSandBoat (DSP fork) — tables `mob_groups`, `mob_spawn_points`, `quests`, `mission`, `item_basic`, `synth_recipes`, `zone_settings`. |
| Unlock graph | `mission.bastok.1-1` → `mission.bastok.1-2` (linear missions); `quest.windurst.zilart-chain.1` → `quest.zilart.2`; advanced jobs (PLD requires Bastok M3-2 + Quest "Lost in Translation"); ranks (Promotion: Centurion → Knight). |

### Warhammer Online (`Warhammer Online - Age of Reckoning/`)

| AOTA Layer | WAR mapping |
|---|---|
| Activity | PQ (Public Quest), Scenario (BG), Open RvR Keep siege, Influence-grind, Quest chain, City-siege, Crafting session, Player-versus-environment Tome unlock. |
| Objective | "complete stage 2 of `Tor Anroc`", "kill 20 Greenskin grunts", "earn 5000 chapter-3 influence", "cap Logrin's Forge keep". |
| Task | `RunToTask`, `EngageTask`, `BattlefieldObjectiveCapTask`, `PqWaveSurviveTask`, `InfluenceTurninTask`. |
| Action | WAR protocol opcodes. |
| DB the composer reads | RoR/Apocalypse private-server SQL tables. |
| Unlock graph | Career rank gates (CR 11 → can join most scenarios); influence tiers (bronze → silver → gold per chapter). |

### Ultima Online (`Ultima Online/UltimaBot/`)

| AOTA Layer | UO mapping |
|---|---|
| Activity | Skill-train grind, Champ-spawn run, Tamed-pet farming, Bod-deed cycle, Lockbox-roll, Provisioner-craft session, Treasure-map dig, Bardic-fight session. |
| Objective | "raise Magery from 80 to 95", "complete the `Despicable Champion` spawn", "tame a mustang and stable", "fill 10 cloth bods at the gypsy camp". |
| Task | `WalkToTask` (no pathfinding navmesh in UO — A* grid), `CastSpellTask`, `SmithSkillTask`, `BodTurninTask`, `PaperdollEquipTask`. |
| Action | UO client packets. |
| DB | ServUO / RunUO SQLite/SQL. |
| Unlock graph | Skill caps (700 total cap; per-skill 100 cap with power scrolls to 120); deed unlocks at Smithing 90/95/100. |

### EverQuest (`EverQuest/EQBot/`)

| AOTA Layer | EQ mapping |
|---|---|
| Activity | Hell-Level grind, Plane-of-Sky attune, Epic-quest chain, Bind-Stone-bind, Faction-grind, Tradeskill loop. |
| Objective | "kill 50 orc pawns in Crushbone", "obtain Glowing Orb of Antiquity", "raise Smithing to 200 (the wall)". |
| Task | `WalkToTask`, `CastSpellTask`, `MeditateTask`, `FactionInteractTask`. |
| Action | EQ client packets (lots of legacy crypto). |
| DB | EQEmu (eqemu_data, content tables). |
| Unlock graph | Faction stairsteps, alignment caps, class-quest chains. |

### EverQuest II (`EverQuest II/EQ2Bot/`)

Same shape; deeper crafting trees and AA-line tradeskills push the
`Profession-Crafting` Activity family further; the `quest_template`-equivalent
is `quest_template_v3` in the EQ2EMu schema with more objective columns.

### Phantasy Star Online (`Phantasy Star Online/PSOBot/`)

| AOTA Layer | PSO mapping |
|---|---|
| Activity | Block-quest run, Episode-mission, Mag-feed, Photon-blast farm, Lobby-meet, Mileage-board grind. |
| Objective | "reach gateway", "defeat Vol Opt phase 2", "fire-photon-blast on Olga Flow phase 2". |
| Task | `WalkToTask`, `AttackTask` (PSO has no rotation depth; just `MeleeChain1`/`2`/`3` and tech spam). |
| Action | PSO protocol packets. |
| Unlock graph | Section ID + difficulty-tier unlocks, Hunter's License, mag-feed timers. |

### Ragnarok Online (`Ragnarok Online/RagBot/`)

| AOTA Layer | Ragnarok mapping |
|---|---|
| Activity | Job-leveling grind, Job-Change quest, MVP-farm, WoE prep, Refine-loop. |
| Objective | "reach Magician class change", "MVP Eddga at 06:00 timer", "+10 a wool scarf". |
| Task | `WalkToTask` (grid pathing), `AttackTask`, `BuyItemTask`, `RefineItemTask`. |
| DB | rAthena / Hercules `mob_db`, `item_db`. |
| Unlock graph | Job tree (Novice → 1st → 2nd → Trans → 3rd). |

### Star Wars Galaxies (`Star Wars Galaxies/SWGBot/`)

Pre-CU SWG has the heaviest profession DAG of any game in the
monorepo; the `Profession-Crafting` Activity family is the dominant
loop. Mission terminals are an Activity in their own right.

### D2 (`D2Bot/`)

| AOTA Layer | D2 mapping |
|---|---|
| Activity | Act-progression, Single-target bossing (Andariel/Duriel/Mephisto/Diablo/Baal), Cow-runs, Pindleskin, Cube-recipes, Shopping cycle, Trade-window cycle. |
| Objective | "reach Tristram waypoint", "kill Baal phase 3", "find 5 perfect topaz". |
| Task | `WalkToTask` (no real pathfinding in D2 — line-of-sight), `AttackTask`, `UseTownPortalTask`, `BeltPotionTask`, `CubeTransmuteTask`. |
| Action | OpenD2 packets. |
| Unlock graph | Difficulty unlocks (Normal → NM → Hell), Akara reset, Anya's reward. |

**D2Bot is the canonical reference for the runtime `IActivity` and
`IObjective` interfaces.** See
[`D2Bot/D2Orchestrator/Orchestration/Activities/IActivity.cs`](../../../../D2Bot/D2Orchestrator/Orchestration/Activities/IActivity.cs)
and `ObjectiveRuntimeContracts.cs`.

## 4. The minimum-viable port

If you only have time to port one thing to a new game, port this:

```
Step 1: Pick ONE Activity family. Author 3-5 catalog rows for it.
Step 2: Implement the ComposeObjectives sub-algorithm for that family
        against the game's DB (read-only).
Step 3: Wire up 5-10 Task implementations (the ones the composed
        Objectives push). Make sure GoToTask works first.
Step 4: Add the snapshot identity fields for Activity/Objective layers
        to the game's `communication.proto`.
Step 5: Write ONE live-validation test that drives an Activity
        end-to-end and asserts the Objective sequence.
```

That gets you a working AOTA stack for one loop in the new game. Then
iterate by family — adding catalog rows, composer cases, Task
implementations, and tests per loop.

## 5. Anti-patterns when porting

| Pattern | Why it's wrong |
|---|---|
| **Hard-coding the Objective list per Activity in C#.** | Defeats the whole "dynamic" claim. The composer reads the DB; the C# is the algorithm, not the data. |
| **Treating Tasks as a flat list (no push-child stack).** | Loses the recursive composition; you'll re-invent it as nested switch-statements. |
| **Adding new `ActionType` values per behavior.** | Wire-cost without payoff. Compose at the Task layer instead. |
| **Skipping the Objective layer entirely (Activity directly pushes Tasks).** | You lose the test-assertion granularity and the failure-isolation surface. |
| **Per-character behavior trees that persist across ticks.** | Hides state from the snapshot. Use the LIFO Task stack; let the snapshot project the top. |
| **Asserting on Task internals in tests.** | Brittle. Assert on `WoWActivitySnapshot` (or game-equivalent) state. |
| **Letting the catalog reach into the unlock graph at compile time.** | The catalog is data; the unlock graph is data; the composer joins them at runtime. Compile-time joins prevent server-config-driven variation. |
| **Per-zone "leveling guide" doc that contradicts the catalog row.** | Leveling guides are *reference data*, not the authoritative source. The catalog wins; a test fails to flag the drift. |

## 6. The shared substrate (monorepo-level)

Some primitives should live in the **shared** monorepo layer rather
than being re-implemented per game:

- **`IBotTask` interface shape** (per game proto-gen since the
  `IObjectManager` signature differs, but the four-method async
  contract is identical).
- **Push-child execution loop** in `IBotRunner` (each game implements
  the loop, but the *contract* is shared).
- **Path-finding service contract** (`PathfindingService` is in
  monorepo root; serves all games via `RequestRoute(map, fromPos, toPos)`).
- **Snapshot identity fields** (`current_activity_id`,
  `current_objective_id`, `current_objective_type`, top-of-stack task
  name; per-game proto but identical roles).
- **Activity legality validator** algorithm (7-step gate is identical;
  the gate inputs differ per game).
- **Priority bands** (per [`leveling-priority.md`](../../leveling-guide/decision-engine/leveling-priority.md);
  the band names and ranges are portable, the specific rule
  assignments are not).

Per [`docs/MULTI_AGENT_ORCHESTRATION.md`](../../../../docs/MULTI_AGENT_ORCHESTRATION.md)
and [`docs/SKILL_DEVELOPMENT_PLAN.md`](../../../../docs/SKILL_DEVELOPMENT_PLAN.md):
when you spot a primitive that is being copy-pasted across two game
folders, that's the signal to lift it into the monorepo-shared layer.

## 7. Reference

- D2Bot canonical `IActivity` / `IObjective`: [`D2Bot/D2Orchestrator/Orchestration/Activities/IActivity.cs`](../../../../D2Bot/D2Orchestrator/Orchestration/Activities/IActivity.cs), [`ObjectiveRuntimeContracts.cs`](../../../../D2Bot/D2Orchestrator/Orchestration/ObjectiveRuntimeContracts.cs), [`Core/IBotTask.cs`](../../../../D2Bot/D2Orchestrator/Core/IBotTask.cs).
- Monorepo cross-cutting docs: [`docs/MONOREPO_OVERVIEW.md`](../../../../docs/MONOREPO_OVERVIEW.md), [`docs/SKILL_DEVELOPMENT_PLAN.md`](../../../../docs/SKILL_DEVELOPMENT_PLAN.md), [`docs/MULTI_AGENT_ORCHESTRATION.md`](../../../../docs/MULTI_AGENT_ORCHESTRATION.md), [`docs/REPO_AGENT_DOCS_PROPAGATION.md`](../../../../docs/REPO_AGENT_DOCS_PROPAGATION.md).
- Per-game docs entry points: each game's own `CLAUDE.md` / `AGENTS.md`.
