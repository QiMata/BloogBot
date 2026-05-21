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
- The `ObjectiveType` enum (each game has its own wire surface).
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
   - which ObjectiveType enum values exist in this game's communication.proto
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
| **Adding new `ObjectiveType` values per behavior.** | Wire-cost without payoff. Compose at the Task layer instead. |
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

## 7. Cross-game ML reuse

The §6 "shared substrate" lists the deterministic primitives that
travel between games unchanged. The **ML surface** (Spec/20 §2's
seven advisor RPCs plus the off-line learners in
[`Spec/04 §ML`](../../Spec/04_ACTIVITIES.md#ml-integration--selection-weight-learning),
[`Spec/10 §Anomaly detection`](../../Spec/10_METRICS.md#anomaly-detection),
[`Spec/12 §Failure-cause clustering`](../../Spec/12_ERROR_TAXONOMY.md#failure-cause-clustering--ml-integration),
[`Spec/15 §Skill applicability scoring`](../../Spec/15_SKILLS.md#skill-applicability-scoring-cross-game-auto-bootstrap),
[`Spec/16 §Personality clustering`](../../Spec/24_BEHAVIORAL_VARIATION.md#11-ml-integration--personality-clustering))
follows a more nuanced portability rule: **interface shape ports;
trained models do not.**

### Per-advisor portability matrix

| Advisor | Interface shape | Context message shape | Trained model file | Per-game retraining cost |
|---|---|---|---|---|
| `GetRotationAdviceAsync` | portable (one method, `RotationContext` → `RotationAdvice`) | mostly portable (`known_spell_ids`, `active_aura_ids`, `gcd_remaining_ms` are universal MMO concepts; ability id semantics differ per game) | **NOT portable** — WoW Frost Mage rotation has zero overlap with FFXI BLM | re-train per (game, class/job) — ~200 traces per spec for Phase 3 |
| `GetThreatAdviceAsync` | portable | mostly portable (`candidate_guids`, `candidate_threats`, `candidate_is_caster` are universal) | partially portable — threat mechanics are similar in WoW/EQ2/SWG-PvE; very different in FFXI (enmity is composite) | per-game retrain; FFXI requires a different feature set |
| `GetRewardAdviceAsync` | portable | mostly portable (`reward_item_ids`, `reward_item_slot`, `reward_item_quality` translate) | mostly NOT portable — item-id space is per-game; slot semantics overlap | per-game retrain; can warm-start from WoW model for slot-preference axis |
| `GetObjectiveAdviceAsync` | **portable** | **portable** — `tied_objective_ids`, `roster_goal_distance[8]`, `bot_position`, `current_zone_id`/`current_map_id` are universal | **mostly portable** — the 8-axis `roster_goal_distance` vector has the same shape across MMOs (Level/Gear/Attunement/Rep/Gold/Mount/PvP/Profession all exist) | warm-start strong; per-game tuning ~50 traces vs 200 |
| `GetChatTemplateAdviceAsync` | portable | template-id space is per-game (Bot/chat-templates/) | NOT portable — chat conventions differ by game community | per-game retrain |
| `GetActivityRequestAdviceAsync` | portable | per-game catalog id space | NOT portable — whisper shorthand differs per game (`!run rfc` vs `!party Saber`) | per-game retrain |
| `GetPersonalityClusterAdviceAsync` | **portable** | **highly portable** — the 8-axis `roster_goal_distance` + `chattiness` + `target_level` are universal MMO bot dimensions | **partially portable** — chattiness distribution travels well; reward-priority distribution is class-system-dependent | warm-start very strong; per-game tuning is just centroid-position adjustments |

**The key invariant:** the seven `I*AdviceAsync` *method shapes* are
portable as a verbatim copy-paste of the interface declaration. The
proto messages are portable as a copy with renamed game-specific
fields. The trained `.onnx` files are not — but the *training
pipeline* (Spec/20 §6 trace consumer + Python trainer) IS portable
end-to-end, because the trace JSONL schema is also portable.

### What's portable from the ML pipeline

- **`tmp/test-runtime/traces/<test-name>/<timestamp>.jsonl` schema**
  per [`Spec/20 §6.1`](../../Spec/20_DECISION_ENGINE.md#61-trace-line-schema) — `kind`, `advisor`, `context` shape, `outcome.roster_distance_delta` field. **Game-agnostic.** Every game's trace writer emits the same JSONL.
- **Trace correctness contract** per [`Spec/20 §6.2`](../../Spec/20_DECISION_ENGINE.md#62-trace-correctness-contract). **Game-agnostic.**
- **`RosterPlanner.Distance` 8-axis vector** per [`Spec/05 §RosterPlanner.Distance`](../../Spec/05_PROGRESSION.md#rosterplannerdistance--the-canonical-progression-metric). **Game-agnostic** in shape; each game maps its own progression to the 8 axes. (E.g. FFXI's `Attunement` axis becomes "key items + ROTZ/COP missions"; SWG's `Mount` axis becomes "vehicle/landspeeder tier".)
- **Off-line trainer (Python)** — same toolchain (skl2onnx, sentence-transformers, gradient-boosted regressors) — per-game retrain.
- **ONNX runtime loader (`ModelDescriptor`, `Mode` enum)** per [`Spec/20 §4`](../../Spec/20_DECISION_ENGINE.md#4-model-lifecycle). **Game-agnostic** — just point at a different `Models/<advisor>/v<n>.onnx`.

### What's NOT portable

- **Trained `.onnx` files.** Each game has its own corpus; cross-game model transfer is academic curiosity, not engineering practice.
- **Per-game `Config/decision-engine/*-rules.json` lookup tables**
  (Phase 2). The rules are authored against the game's specific catalog ids, item ids, faction ids.
- **Game-specific feature embeddings.** `ItemSourceContext.craft_reagent_recursion_depth` makes sense in WoW where Alchemy reagents recurse 1-2 levels; FFXI's HQ-tier crafting recursion is 3-5 levels and the model needs to relearn the curve.

### Skill auto-bootstrap is the meta-ML

[`Spec/15 §Skill applicability scoring`](../../Spec/15_SKILLS.md#skill-applicability-scoring-cross-game-auto-bootstrap)
is the one ML system that is **explicitly cross-game** by design.
Its job is to rank which WWoW-proven skills apply to a new target
game repo. The model is trained on a corpus that spans games (the
input feature includes a `file_inventory_hash_distance` cross-game
similarity feature; the labeled outcome is "did this skill close a
TASKS.md item in the target repo").

This is the load-bearing meta-loop: when WWoW reaches end-state and
the autonomous loop migrates to FFXI / WAR / UO / EQ / EQ2 / PSO /
Ragnarok / SWG, the skill-applicability scorer is what tells the lead
agent which skills to dispatch first. It is **the only ML model in
the monorepo that is genuinely portable across games as a trained
artifact.**

## 8. Worked example — porting WWoW's Objective advisor to FFXI

Setup: WWoW has shipped Plan/14 Phase 10 end-to-end. All seven
advisors have Phase 3 ONNX models trained on WWoW traces. The
autonomous loop is migrating to FFXI (`Final Fantasy XI/FFXIBot/`).
Goal: light up `GetObjectiveAdviceAsync` for FFXI bots.

### Step 1 — port the interface and proto

Copy the `IDecisionEngineClient` interface declaration from
`Exports/BotRunner/Clients/DecisionEngineClient.cs` (WWoW) to
`Final Fantasy XI/FFXIBot/Exports/BotRunner/Clients/DecisionEngineClient.cs`.
The seven method signatures are verbatim portable. Copy
`Exports/BotCommLayer/Models/ProtoDef/decision-engine.proto` and
adjust:

- `import "communication.proto";` → `import "ffxi-communication.proto";`
- `repeated string tied_objective_ids = 8;` → unchanged (string)
- `ObjectiveType current_objective_type = ...` → enum values are
  per-game (`Travel`, `Interact`, `Engage`, `Skillchain`, `Magic Burst`
  for FFXI vs `Kill`, `Collect`, `CastSpell` for WWoW). Adjust the
  enum body; field number unchanged.

The seven `oneof body` tags 1-8 (request) and 1-8 (response) stay
the same; the wire is now FFXI-compatible.

### Step 2 — port `RosterPlanner.Distance` axes

Copy `Services/WoWStateManager/Progression/RosterPlanner.cs` from WWoW
to FFXI. The `DistanceAxis` enum's 8 values map:

| WWoW axis | FFXI axis |
|---|---|
| Level | "level" (1-75 cap in vanilla FFXI) |
| GearTier | "gear-relic-tier" |
| AttunementStep | "key-items-and-missions" (ROTZ + COP + AU) |
| ReputationTier | "fame-tiers" (per-city Fame system) |
| GoldTargetPct | "gil-target-pct" |
| MountTier | "chocobo-license + raptor" |
| PvPRank | "ballista-rank" |
| ProfessionSkill | "crafting-skill-max" (multiple per character) |

The 8 axes survive verbatim. `DefaultWeights` likely needs FFXI tuning
(Attunement axis is heavier in FFXI than WWoW), but the *shape* is
identical.

### Step 3 — copy the trace JSONL schema

Per Spec/20 §6.1, the JSONL schema is game-agnostic. Copy
`Services/DecisionEngineService/Tracing/TraceWriter.cs` from WWoW;
the only adjustment is the `outcome.roster_distance_delta` source —
the Python aggregator reads it the same way.

### Step 4 — bootstrap with WWoW's `Models/objective/v1.onnx`

This is the surprising step: **the WWoW Objective advisor's ONNX
model is partially usable as a warm-start for FFXI**. Why:

- The model's primary input feature is `roster_goal_distance[8]` —
  identical 8-axis vector in both games.
- The model's secondary feature is `tied_objective_costs[8]` — costs
  are normalized to seconds, also game-agnostic.
- The model's output is `(tied_index, confidence)` — index-based, no
  game-specific encoding.

The warm-start approach: deploy `Models/objective/v1.onnx` from WWoW
into FFXI's `Services/DecisionEngineService/Models/objective/v1.onnx`,
flag it `confidence_threshold = 0.7` (higher than WWoW's 0.5 since
the model is out-of-domain), and let FFXI's first 50 LiveValidation
traces feed the Python re-trainer. After ~50 traces, swap in
`Models/objective/v2.onnx` (FFXI-tuned) with confidence_threshold
back to 0.5.

This is **dramatically faster** than training from scratch (which
would require 200+ traces before Phase-3 confidence is meaningful).

### Step 5 — confirm dynamic-progressive invariant in the new game

Before flipping FFXI's `advisors.objective.mode` from `Trivial` to
`Ml`, the live-validation guard must fire: replaying a representative
FFXI trace with the WWoW model loaded MUST still produce
`outcome.roster_distance_delta ≤ 0`. If the WWoW model's out-of-
domain inference makes anti-progressive picks, the model is rolled
back and FFXI ships with `Mode=Trivial` until enough FFXI-native
traces accumulate.

This is the same invariant from
[Example 5 in `06_WORKED_EXAMPLES.md`](06_WORKED_EXAMPLES.md#example-5--ml-aided-composition-for-a-level-40-mage-zone-transition) —
the deterministic stack is the floor, ML is the ceiling, the
all-NoAdvice replay closes goal distance regardless. **The invariant
ports verbatim across games.**

## 9. Dynamic-progressive invariant (cross-game)

The dynamic-progressive invariant is the only **non-negotiable**
property that survives a game port. Per
[`Spec/19 §10`](../../Spec/19_AOTA_RUNTIME.md#10-dynamic-progressive-invariant)
and [`Spec/20 §10`](../../Spec/20_DECISION_ENGINE.md#10-dynamic-progressive-invariant),
every game's runtime MUST satisfy:

1. **Dynamic.** Different bot contexts produce different Activity /
   Objective / Reward / source choices. Both WoW Frost Mages and
   FFXI Black Mages must show *different* `current_objective_id`
   trajectories per snapshot input. Identical inputs → identical
   choices.
2. **Progressive.** Every Activity completion's
   `outcome.roster_distance_delta` is `≤ 0`. The deterministic stack
   alone produces non-positive deltas; ML can only accelerate
   closure, never reverse it.

The invariant is universal because it is **defined on the trace
surface**, which is itself game-agnostic. A new game's
LiveValidation suite that produces traces satisfying §6.2's
correctness contract automatically inherits the invariant assertion.

## 10. Plan-slot cross-reference

| Slot | Owns | Section here |
|---|---|---|
| [`Plan/14/S10.0`](../../Plan/14_PHASE10_DECISION_ENGINE_INTEGRATION.md#s100--idecisionengineclient-shim--transport) | `IDecisionEngineClient` interface — verbatim portable | §7 portability matrix row 1 |
| [`Plan/14/S10.6`](../../Plan/14_PHASE10_DECISION_ENGINE_INTEGRATION.md#s106--mode-aware-advisor-activation) | Per-game `Config/decision-engine.json` + `Models/<advisor>/v<n>.onnx` | §7 §What's NOT portable |
| [`Plan/14/S10.7`](../../Plan/14_PHASE10_DECISION_ENGINE_INTEGRATION.md#s107--training-trace-plumbing) | Trace JSONL schema (universal) + Python trainer toolchain (per-game retrain) | §7 §What's portable from the ML pipeline |
| [`Plan/15`](../../Plan/15_PHASE11_SOCIAL_FABRIC.md) | Per-game `Bot/chat-templates/` library | §7 portability matrix row 5 |
| [`Plan/16`](../../Plan/16_PHASE12_BEHAVIORAL_VARIATION.md) | Per-game `Config/personalities.json` mix | §7 portability matrix row 7 |
| Spec/15 [§Skill applicability scoring](../../Spec/15_SKILLS.md#skill-applicability-scoring-cross-game-auto-bootstrap) | **Cross-game-portable ML model** (the only one) | §7 §Skill auto-bootstrap is the meta-ML |
| **(no slot yet — Plan follow-up)** | `Tools/CrossGameModelWarmStart/` (CLI for §8 step 4 "deploy WWoW model into FFXI as warm-start, swap to v2 after 50 traces") | §8 step 4 |

The "no slot yet" row joins the Plan-follow-up roster (15th
orphan: `Tools/CrossGameModelWarmStart/`).

## 11. Test surface

Contract tests live at
`Tests/BotRunner.Tests/Activities/CrossGameMlReuseContractTests.cs`.
The tests assert against the **portable** properties of the ML
surface; per-game training is out of scope (tested by each game's own
test suite).

- **`CrossGameMl_ClientInterfaceShapeIsLiteralCopy`** — comparing the
  WWoW `IDecisionEngineClient` interface with the FFXI port: same 7
  method names, same parameter types modulo per-game proto namespaces.
  Asserted via reflection across the two assemblies.
- **`CrossGameMl_TraceJsonlSchemaIsGameAgnostic`** — a WWoW
  `outcome.jsonl` line and an FFXI `outcome.jsonl` line both validate
  against the same Spec/20 §6.1 schema; field set is identical.
- **`CrossGameMl_RosterDistanceEightAxisSurvivesPort`** — calling
  the WWoW `RosterPlanner.Distance` against a synthetic FFXI snapshot
  (with the §8 step 2 axis mapping) returns a structurally-valid
  `RosterPlannerDistance` with 8 PerAxis entries summing to
  TotalScalar within 1e-5.
- **`CrossGameMl_WarmStartGuard_OutOfDomainModelDoesNotMakeAntiProgressivePicks`** —
  loading WWoW's `Models/objective/v1.onnx` into an FFXI test fixture
  with `confidence_threshold=0.7` and replaying a synthetic FFXI
  trace produces `outcome.roster_distance_delta ≤ 0` for every
  completion. If the model's out-of-domain inference would have made
  anti-progressive picks, they are filtered by the confidence floor.
- **`AotaPortability_DynamicProgressive_InvariantSurvivesCrossGamePortTest`** —
  the dynamic-progressive invariant from §9. For ≥2 synthetic
  snapshots that differ in `roster_goal_distance` axis dominance
  applied to each of WWoW and FFXI, the resulting first-Objective
  picks differ across snapshots AND each Activity completion's
  `roster_distance_delta` is ≤ 0. Asserted across BOTH games in the
  same test (cross-assembly comparison).

## 12. Reference

- D2Bot canonical `IActivity` / `IObjective`: [`D2Bot/D2Orchestrator/Orchestration/Activities/IActivity.cs`](../../../../D2Bot/D2Orchestrator/Orchestration/Activities/IActivity.cs), [`ObjectiveRuntimeContracts.cs`](../../../../D2Bot/D2Orchestrator/Orchestration/ObjectiveRuntimeContracts.cs), [`Core/IBotTask.cs`](../../../../D2Bot/D2Orchestrator/Core/IBotTask.cs).
- Monorepo cross-cutting docs: [`docs/MONOREPO_OVERVIEW.md`](../../../../docs/MONOREPO_OVERVIEW.md), [`docs/SKILL_DEVELOPMENT_PLAN.md`](../../../../docs/SKILL_DEVELOPMENT_PLAN.md), [`docs/MULTI_AGENT_ORCHESTRATION.md`](../../../../docs/MULTI_AGENT_ORCHESTRATION.md), [`docs/REPO_AGENT_DOCS_PROPAGATION.md`](../../../../docs/REPO_AGENT_DOCS_PROPAGATION.md).
- Per-game docs entry points: each game's own `CLAUDE.md` / `AGENTS.md`.
