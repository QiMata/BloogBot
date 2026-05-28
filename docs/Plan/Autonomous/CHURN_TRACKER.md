# WWoW Autonomous — Churn Tracker (master backlog)

> **What this is.** The single dependency-ordered backlog an autonomous
> agent (Codex, or a Claude `/loop`) churns to reach
> [`00_DEFINITION_OF_DONE.md`](00_DEFINITION_OF_DONE.md). Every row has
> an **`Accept`** column naming the exact test / predicate / doc that
> flips it to `done`. **No row is `done` without a green `Accept`
> observed *this iteration*** (the verification-trust rule — stale green
> is red).
>
> This layout **layers on** the existing `Plan/` + `Spec/` +
> `leveling-guide/`; it does not replace them. Where a row maps to an
> existing phase slot (`S1.x`, `S2.0`, `Plan/14/S10.x`, `Plan/13/S9.x`)
> it cites it so the detailed "how" is one click away.
>
> **Loop prompt:** [`CODEX_LOOP_PROMPT.md`](CODEX_LOOP_PROMPT.md).
> **Runbook:** [`LIVE_RUNBOOK.md`](LIVE_RUNBOOK.md).
> **Last updated:** 2026-05-28 (layout session, WWoW `main` @ `be6c32af`).

## How the loop picks the next item

1. Read this tracker top-to-bottom each iteration.
2. Pick the **first** row where `Status: todo` AND every ID in `Deps`
   is `Status: done`. (Dependency-first; phase order is the secondary
   sort.) Prefer the **critical path** (bottom of this file) when
   multiple rows are ready.
3. Do the work in a single commit (or bump `Iters-Done` for a
   multi-iter row and leave `todo`).
4. **Run the row's `Accept` gate.** Only if it passes in this run do
   you set `Status: done` + record the `Commit`. If it fails, leave
   `todo`, record what you observed, and either continue the row next
   iter or open a blocker row.
5. Stop conditions: all rows `done`, or a stop condition in the loop
   prompt fires.

## Status legend

- `todo` — not started or in progress (`Iters-Done` tracks partial).
- `done` — `Accept` gate observed green *by a run this-or-a-prior
  iteration*, commit recorded.
- `blocked:human-decision` — waiting on an operator decision that
  changes shared state (e.g. the `Q-D5-1` pathfinding data-dir repoint).
- `blocked:human-RE` — waiting on a human-only live capture (memory
  offset / packet id for un-RE'd content).

## Accept-gate vocabulary

- `LIVE:<TestClass>[.<Method>]` — a `Category=Integration` test under
  `Tests/BotRunner.Tests/LiveValidation/` (or `Tests/WoWSharpClient.Tests`)
  that must pass in a clean headless run via
  `pwsh tools/run-live.ps1 -Filter <X>` (`A.2`), **and** whose JSONL
  trace satisfies the dynamic-progressive invariant
  (`roster_distance_delta ≤ 0` on completion — see
  [`../Activities/00_INDEX.md`](../Activities/00_INDEX.md) §Dynamic-progressive).
- `UNIT:<TestClass>` — a unit slice (`Category!=Integration`) that must pass.
- `PRED:<expr>` — a `CharacterProgression` predicate observed true on a
  live bot snapshot at the end of the Activity.
- `DOC:<file>` — a markdown deliverable exists and is internally
  consistent (for spec/enumeration rows).
- `SOAK:<duration>` — the capability holds unattended for the duration.

---

## Progress snapshot

| Metric | Value |
|---|---|
| Tracker opened | 2026-05-28 |
| Phases | A (verify) · B (activity runtime + families) · C (progression) · D (roster) · E (endgame/stretch) |
| Rows total | 36 (A 10 · B 14 · C 6 · D 4 · E 3 [stretch]; families ship incrementally) |
| Rows done | 0 / 36 — fresh layout; nothing verified by this session (layout-only). |
| Docker stack | `wow-mangosd`, `wow-realmd`, `maria-db`, `wwow-pathfinding` (9002), `wwow-scene-data` (9003) — all `Up (healthy)` 2026-05-28. The loop does NOT manage docker. |
| Phase status (existing Plan) | Phase 0 **done** (2026-05-12). Phase 1 substrate **partial**: `S1.0` (IBotTask contract) ✅, `S1.2` (MovementController audit 33/33) ✅, `S1.3` (PathfindingService 23/0) ✅, `S1.15-S1.19` (BG `Network*Frame` paths) coded/parity-pending; `S1.1` physics-family checkpoints **open** → `A.8`; `S1.4-S1.14` task families **no dry-run** → Phase B; `S1.20` 1h shakeout **open** → `A.10`. Phases 2-12 **not-started**. |
| Live baseline | **None re-verified this session (layout-only).** Slot baselines: `NavigationPathTests` 113/0/7, `OgZeppelin` 4/0, `RecordedTests.Pathing` 135/0, `PathfindingService` 143/14(3 pre-existing)/13. The OG-Zeppelin tower-climb live test (`A.7`) is RED; `Q-D5-1` data-dir drift (`A.6`) blocks pathfinding validation pending an operator decision. |
| Critical path | **B.composer** (`S2.0` IActivity/IObjective + `IActivityComposer`) → **B.combat** → **B.travel** → **B.questing** → **B.solo-xp** → **C.statemodel** → **C.progression** → **C.unlock-runtime** → **C.goalplanner** → **D.roster** → **D.progression-loop**. `B.composer` and `C.goalplanner` are the two load-bearing unblockers: the composer turns a catalog row into a live Objective graph (today nothing does), and the goalplanner replaces the hardcoded `ProgressionPlanner` stub with a `decision-engine/`-driven chooser. |

---

## Phase A — Verification foundation (make the harness trustworthy)

> Nothing downstream can be trusted until the live suite runs reliably
> headless and the existing `done`/`partial` claims in
> [`../Activities/00_INDEX.md`](../Activities/00_INDEX.md) are
> re-validated. This phase is the prerequisite for treating any
> `Accept: LIVE:` gate as meaningful. Harness facts +
> commands: [`LIVE_RUNBOOK.md`](LIVE_RUNBOOK.md).

| ID | Title | Deps | Iters | Iters-Done | Accept | Status | Commit |
|---|---|---|---|---|---|---|---|
| A.1 | **Headless live-run env gates.** Establish a `WWOW_SKIP_SERVER_RESTART=1` gate so the live suite runs against the already-up VMaNGOS stack without a full StateManager/MaNGOS restart on each settings change (today `BotServiceFixture.EnsureSettingsAsync` does a lazy reload + may restart on settings mismatch — `Services/WoWStateManager/CLAUDE.md`, `Tests/Tests.Infrastructure/BotServiceFixture.cs`), and a `WWOW_DISABLE_UI=1` gate so `UI/WoWStateManagerUI` is not spawned during a "headless" run (Phase 3 makes UI the default host — the gate keeps the loop headless). Confirm which already exists; add only the missing one (R18 — no parallel old/new). | — | 1 | 0 | `LIVE:` a single FG smoke test (`A.3`) runs **past setup** against the up stack with both gates set, no UI window, no full server restart | todo | |
| A.2 | **`tools/run-live.ps1` wrapper + `LIVE_RUNBOOK.md`.** A wrapper that: (1) docker-preflights the 5 containers (`wow-mangosd`/`wow-realmd`/`maria-db`/`wwow-pathfinding`/`wwow-scene-data` all `Up`); (2) sets the `A.1` gates + points `TEMP`/`DOTNET_CLI_HOME`/`VSTEST_RESULTS_DIRECTORY` at repo-local `tmp/` (mirror `run-tests.ps1` lines 23-46); (3) builds once; (4) runs a single `--filter "FullyQualifiedName~<X>"` against `Tests/BotRunner.Tests` with `--blame-hang`; (5) retries up to `-MaxAttempts` (default 3) on **setup-timeout only** (never a real behavioral fail); (6) prints artifact paths. The runbook documents it. | A.1 | 1 | 0 | `DOC:LIVE_RUNBOOK.md` consistent with the script + `pwsh tools/run-live.ps1 -Filter <A.3 test>` exits 0 | todo | |
| A.3 | **Canonical single-bot live smoke anchor.** Identify (or write) the "login → in-world → writes latest artifacts" test that is the harness anchor — the WWoW equivalent of a boot-to-in-world check. Should boot a FG bot via StateManager (`StartForegroundBotRunner` → inject `Loader.dll` → connect on 9001), reach in-world, and write a snapshot + screenshot to `tmp/test-runtime/screenshots/<test>/` (R16). Prefer reusing an existing minimal `LiveValidation` login/connect test if one passes; otherwise add `ForegroundLoginSmokeTests`. | A.2 | 2 | 0 | `LIVE:` the anchor test passes **3/3** clean runs via `run-live.ps1`; artifacts written | todo | |
| A.4 | **Trust-gap audit.** Re-run **every** Activity marked `done` or `partial` in [`../Activities/00_INDEX.md`](../Activities/00_INDEX.md) (the gathering family `done`; the dungeon task-family `done`/coordinator `partial`; economy `partial`) via `run-live.ps1`. For each: confirm green (record run date in `00_INDEX.md`) OR downgrade the status + open a fix row here. Honesty over optimism — the gathering/dungeon `done` claims predate a reliable headless harness. | A.3 | 3 | 0 | each audited `00_INDEX.md` row's status reflects a run executed this audit (green-with-date or downgraded+fix-row) | todo | |
| A.5 | **Telemetry/observability spine for the loop.** A normalized failure-reason surface (extend the Phase-0 `FailureReason` enum) + a per-run JSON ledger the loop parses to decide done/blocked/flaky. Mine `Spec/10-13` + [`../06_PHASE5_OBSERVABILITY.md`](../06_PHASE5_OBSERVABILITY.md). Keep it minimal — the goal is a parseable run verdict, not full Grafana (that's `Plan/05`). | A.3 | 2 | 0 | `UNIT:` failure-enum mapping tests + a live run emits a parseable ledger the loop can read | todo | |
| A.6 | **`Q-D5-1` pathfinding data-dir decision (operator gate).** The pathfinding test runner `tools/scripts/run-pathfinding-tests.ps1:59` defaults `DataDir` to `D:\MaNGOS\data` while loop-24 canonical bakes live at `D:\wwow-bot\prod-data\mmaps\` (md5 drift; the D5 OG-sea-level `no_route` was **test config drift, not code** — [`../Handoffs/2026-05-19-loop25-d5-og-sea-level-no-route-surface.md`](../Handoffs/2026-05-19-loop25-d5-og-sea-level-no-route-surface.md), [`QUESTIONS.md` Q-D5-1](../QUESTIONS.md)). **Recommended Option A:** repoint line 59 + 3 sibling scripts to `prod-data` (~4 LOC). Fallback: `$env:WWOW_DATA_DIR` override. Do NOT sync `MaNGOS\data ← prod-data`. The pathfinding overhaul freeze means mesh fixes go in `tools/MmapGen/`, not managed repair (`CLAUDE.md`). **This is an operator-decision gate** — propose the diff, then `blocked:human-decision` until approved. | A.2 | 1 | 0 | operator approves the repoint **and** the pathfinding validation suite (incl. the OG sea-level route) runs green against `prod-data` | blocked:human-decision | |
| A.7 | **OG Zeppelin tower-climb live test (the open nav thread).** `DeckLipClimbFromGruntToLiteralFrezza` / `ClimbOrgrimmarZeppelinTowerRampToFrezza` is the subject of a ~40-commit caller-side route-consumption loop (HEAD `be6c32af` and back): each fix lands a green unit test, then the live rerun stalls at the *next* micro wall-climb gate (latest `(1352.1,-4526.7,35.2)`). It is **unit-green, live-red**. This is a Codex INVESTIGATION/fix row (R16: read the captured PNG under `tmp/test-runtime/screenshots/`, not just the log). Mesh fixes → `tools/MmapGen/` only (freeze); caller-side route consumption → `Exports/BotRunner` `NavigationPath.cs`. Depends on `A.6` (D5 proved the data-dir matters). | A.6 | 4 | 0 | `LIVE:` the OG-Zeppelin tower-climb test reaches Frezza in a clean run | todo | |
| A.8 | **`S1.1` physics-family checkpoints (Phase 1 substrate gate, R13).** Close the open `S1.1`: representative FG↔BG physics-parity checkpoints **per task family** (validate in order scene-data → FG/BG physics parity → pathfinding; the `FG_BG_PARITY_BREAK` canary is the signal — root `CLAUDE.md` R13). Today only OG guard checkpoints (12/12) exist. | A.4 | 3 | 0 | `UNIT:`/`LIVE:` FG↔BG physics parity within tolerance on ≥1 representative checkpoint per task family (travel/combat/recovery at minimum) | todo | |
| A.9 | **`S1.15-S1.19` BG `Network*Frame` parity tests.** The 5 BG packet paths (Trade/Craft/Vendor/Taxi/Trainer `Network*Frame` adapters) are coded but parity tests are pending (TASKS.md). Add the FG↔BG parity tests that prove the BG path matches recorded FG behavior (root `CLAUDE.md`: BG validated against FG recordings). | A.4 | 2 | 0 | `UNIT:` BG↔FG parity tests green for all 5 `Network*Frame` paths | todo | |
| A.10 | **`S1.20` one-hour shake-out (Phase 1 acceptance gate).** A single bot runs ≥1h unattended (grind/quest in a zone) with no unrecovered failure and no crash — the Phase-1 exit gate. Depends on the core primitives existing. | A.8, A.9, B.combat, B.recovery | 2 | 0 | `SOAK:1h` one bot, no human input, no unrecovered failure, no `WoW.exe` crash | todo | |

---

## Phase B — Activity runtime + task families (build the `<TBD>`/`partial` Activities)

> WWoW Activities are **data rows** (`ActivityDefinition` in
> `Services/WoWStateManager/Activities/ActivityCatalog.cs` + the 5
> `ActivityCatalogRows.Shard*.cs`; record type at
> `Exports/GameData.Core/Models/Activities/ActivityDefinition.cs`) — most
> of the 86 catalog rows already exist. The gap is the **runtime**:
> (a) the `IActivityComposer` that walks a row into an Objective graph
> (`B.composer` / `S2.0`); (b) the per-family `IBotTask` impls
> (`Plan/01` `S1.4-S1.14`); (c) the `LiveValidation` test driving the
> Activity (via `IActivity.Start` once `S2.0` lands; via the
> `AssignedActivity` string + `StageBotRunner*Async` helpers until then)
> and asserting the snapshot + the dynamic-progressive invariant.
>
> **Per-Activity build contract (every B row inherits — do not repeat
> per row):**
> 1. **Catalog row** exists in `ActivityCatalogRows.Shard*.cs` + the row
>    in [`../Activities/00_INDEX.md`](../Activities/00_INDEX.md); if a new
>    row is needed, add it in shard order and keep `CatalogMarkdownDriftTests`
>    green (it asserts markdown↔code id parity).
> 2. **`IBotTask` impls** live in `Exports/BotRunner/` task families (R17 —
>    BotRunner, never StateManager). The four-layer model is
>    Activity→Objective→Task→Action (`Spec/18_TERMINOLOGY.md`); Actions
>    are atomic + local, Objectives cross the wire.
> 3. **Live test** under `Tests/BotRunner.Tests/LiveValidation/` named
>    `<Family><Scenario>Tests`, run via `run-live.ps1 -Filter`. **Drive
>    the Activity, assert the snapshot — never construct a raw
>    `ObjectiveMessage` in the test body** (`CLAUDE.md` test-isolation
>    rule). FG + BG where the family supports both.
> 4. **Consume, don't hardcode:** zones/NPCs/items resolve by name via
>    the MaNGOS DB (SOAP/read-only) / `IGameDatabase`; the *journey*
>    (which Activity when) is the GoalPlanner's job (Phase C), NOT baked
>    into the Activity.
> 5. **Copy-templates** the docs name: gathering (`prof.mining-route` —
>    `GatheringRouteTask`, the most-complete family), the dungeon shape
>    (`dungeon.deadmines`), the economy cycle (`econ.vendor-loop`).
>    Per-family detail: the matching `../Activities/<family>.md` doc.

### B-core — the load-bearing primitives + the runtime that drives them

| ID | Title | Deps | Iters | Iters-Done | Accept | Status | Commit |
|---|---|---|---|---|---|---|---|
| B.composer | **`IActivity`/`IObjective` runtime + `IActivityComposer` (`Plan/03 S2.0` — THE load-bearing unblocker).** Build the runtime that walks an `ActivityDefinition` catalog row into an Objective graph and decomposes Objectives into Tasks via the behavior tree + MaNGOS-DB lookups (`Spec/19_AOTA_RUNTIME.md`, `docs/architecture/aota/03_DYNAMIC_COMPOSITION.md`). Today **nothing composes a row into a live Objective graph** — tests drive Activities via an `AssignedActivity` string. This unblocks every Phase B Activity reaching R-Live through the real decision path, and is what `C.goalplanner` dispatches into. **Full plan + file inventory: [`S20_COMPOSER_PLAN.md`](S20_COMPOSER_PLAN.md).** | A.4 | 5 | 0 | `UNIT:` composer tests (a catalog row → expected Objective sequence; recursive prereq composition per `aota/03`) **AND** `LIVE:` one simple Activity driven via `IActivity.Start(...)` (not the `AssignedActivity` string) reaches completion | todo | |
| B.combat | **Base `combat` capability** — engage / rotation / threat / loot / claim for the leveling grind. Wraps the per-class/spec rotations in `BotProfiles/` (18+ profiles already exist). Single-shot per tick, cooldowns read from snapshot; the `combat` row in `00_INDEX` is the grind primitive every leveling + dungeon + BG Activity builds on. Pair: [`../Activities/combat.md`](../Activities/combat.md). | B.composer | 4 | 0 | `LIVE:` a bot engages + kills a mob + loots + gains XP (FG + BG) | todo | |
| B.travel | **`travel` family** — run / mount / taxi / zone-transition to a named location on the navmesh. Pair: [`../Activities/travel.md`](../Activities/travel.md). Mesh issues → `tools/MmapGen/` (freeze); `GoToTask` is the universal child Task (`Spec/18`). | B.composer | 3 | 0 | `LIVE:` bot travels FG + BG to a named location (multi-leg, incl. a taxi hop) | todo | |
| B.recovery | **`recovery` family** — death → spirit-healer / corpse-run, stuck-recovery, disconnect/relog. `StuckRecoveryTask` is `partial` (needs `IsOnNavmesh` gating per `Plan/09`); `SpiritHealerTask` is `not-started`. Required for ANY unattended run. Pair: [`../Activities/recovery.md`](../Activities/recovery.md). | B.composer, B.travel | 3 | 0 | `LIVE:` bot recovers from a forced death (corpse-run + resurrect) + resumes its prior Activity | todo | |
| B.questing | **Zone-questing task family** — accept / track objective / turn-in / chain ordering (the bulk leveling corpus). `AcceptQuestTask` is `partial` (FG-only, no BG path, no quest-id parameter — survey). Pair: [`../Activities/quests.md`](../Activities/quests.md). Copy-template the `quest.starter.*` shard rows. | B.composer, B.combat, B.travel | 4 | 0 | `LIVE:` bot completes a starter-zone quest chain (e.g. `quest.starter.elwynn-forest` or `quest.zone.westfall`) FG + BG, gaining XP/levels | todo | |
| B.solo-xp | **Solo grind/quest leveling loop** so leveling does NOT require a group (decouples "gain levels" from the flaky multi-bot path). Composes `combat` + `questing` + `recovery` against the optimal-grind-zone selection. | B.combat, B.questing, B.recovery | 3 | 0 | `LIVE:` bot gains ≥1 level unattended over a fixed window | todo | |
| B.group | **Organic group formation + `IActivityCoordinator` (`Plan/03 S2.9` — the multi-bot unblocker).** Level/role-compatible bots converge on the same Objective and form a group with NO scheduler / NO leases (`Spec/05_PROGRESSION.md`). `CoordinatorFixtureBase` exists for tests but has hardcoded retry limits (survey); the production coordinator is the gap. This unblocks dungeons/raids/BGs. | B.composer, B.travel | 4 | 0 | `LIVE:` 5 compatible bots form a group + accept a shared Activity objective without a scheduler (no bot left ungrouped) | todo | |

### B-supporting — professions / economy / dungeons / BG / progression Activities

| ID | Title | Deps | Iters | Iters-Done | Accept | Status | Commit |
|---|---|---|---|---|---|---|---|
| B.professions-gather | **Gathering family** (mining / herbalism / skinning / fishing routes). Marked `done` in `00_INDEX` — **re-verify under `A.4`** (the `done` claim predates a reliable harness). Copy-template `GatheringRouteTask`. Pair: [`../Activities/professions-gathering.md`](../Activities/professions-gathering.md). | B.composer, B.travel | 2 | 0 | `LIVE:` bot completes a gather route + raises the skill (FG + BG) | todo | |
| B.professions-craft | **Crafting family** — `prof.city-trainer-loop` (train recipes at tier boundaries 75/150/225) + drive one craft to skill cap. `partial` today. Pair: [`../Activities/professions-crafting.md`](../Activities/professions-crafting.md). | B.professions-gather, B.economy | 3 | 0 | `LIVE:` bot trains at a city trainer + raises a craft skill over a session | todo | |
| B.economy | **`economy` family** — `econ.vendor-loop` (vendor → repair → bank → mail) + `econ.ah-restock`. Underlying `VendorSellTask`/`RepairAllTask`/`BankDepositTask` exist (`partial` coordination). Pair: [`../Activities/economy.md`](../Activities/economy.md). | B.composer | 3 | 0 | `LIVE:` bot completes a vendor-loop cycle + an AH-restock cycle | todo | |
| B.dungeoneering | **Dungeon family + coordinator** — group → travel → clear. `DungeoneeringTask` is `done`; the coordinator is `partial` (needs the `B.group` `IActivityCoordinator`). Copy-template `dungeon.deadmines`. Pair: [`../Activities/dungeons.md`](../Activities/dungeons.md). | B.group, B.combat | 4 | 0 | `LIVE:` a 5-bot group enters + clears a low dungeon (`dungeon.ragefire-chasm` or `dungeon.deadmines`) | todo | |
| B.equipment | **`equipment` family** — auto-equip upgrades + per-spec gear-tier progression (consumes `C.itemset`). Decides + equips the best available item per slot. Pair: gear sections of the class guides under `leveling-guide/classes/`. | B.composer, C.itemset | 3 | 0 | `LIVE:` bot equips an upgrade picked by the gear logic (no empty slots after a dungeon clear) | todo | |
| B.reputations | **Reputation-grind orchestrator** — dispatches into questing / combat / gathering subpaths to move a rep one standing tier. `not-started`. Pair: [`../Activities/reputations.md`](../Activities/reputations.md). NB: faction-ids `609`/`59`/`270` carry `⚠ Unverified` flags (`QUESTIONS.md` Q-S0.9.5) — verify against `mangos.faction_template` (read-only) before use. | B.questing, B.combat | 3 | 0 | `LIVE:` bot raises a target reputation one standing tier; `PRED:` rep standing increased on snapshot | todo | |
| B.attunements | **Attunement-chain orchestrator** — reuses questing + dungeon steps to complete the MC attunement (the §2.2 floor). `not-started`; `attune.mc` is the target. Pair: [`../Activities/attunements.md`](../Activities/attunements.md). | B.questing, B.dungeoneering | 3 | 0 | `LIVE:` bot completes the Molten Core attunement chain; `PRED:attunements.mc == true` | todo | |

---

## Phase C — The progression layer (the consumer of `decision-engine/`)

> The [`decision-engine/`](../../leveling-guide/decision-engine/)
> markdown is complete (`README.md` `PickNextAction` pseudocode +
> `state-flags.md` + `unlock-graph.md` + `leveling-priority.md` 5 bands +
> `per-bracket-actions/` 6 files) but has **no runtime consumer**. This
> phase builds it. This is the heart of "autonomous" — without it the
> Activities exist but nothing chooses which to run.
>
> **Full plan + the "what already exists" map:
> [`PROGRESSION_LAYER_PLAN.md`](PROGRESSION_LAYER_PLAN.md).** Key facts:
> `ProgressionPlanner` (`Services/WoWStateManager/Progression/ProgressionPlanner.cs`)
> exists as a **hardcoded 8-priority stub that returns `null` ~95% of the
> time** (every resolver is a `TODO`); the dynamic `CharacterProgression`
> model does **not** exist (only static `CharacterBuildConfig`); the
> `DecisionEngineService` (port 5004, ML.NET) exists but is **orphaned**
> (no client shim; the 7-advisor RPC surface is spec-only — `Spec/20`).
> Phase C is *build the runtime consumer + the state model + replace the
> stub*, NOT build-the-markdown.

| ID | Title | Deps | Iters | Iters-Done | Accept | Status | Commit |
|---|---|---|---|---|---|---|---|
| C.statemodel | **`CharacterProgression` runtime model.** Create `Exports/GameData.Core/Progression/`; add the model + the fields `state-flags.md` enumerates (level / talents-spent / per-slot gear tier / professions / attunements / reputations / mounts / gold), populated from `WoWActivitySnapshot` (additive `[ProtoMember]` only — R10; FG mirror + BG handlers). Ids resolve by name via `IGameDatabase` / read-only MaNGOS. See plan §2. | A.4 | 3 | 0 | `UNIT:CharacterProgressionTests` — fields round-trip protobuf + read by predicate helpers; present on a live single-bot snapshot | todo | |
| C.progression | **`CharacterCompletionPredicate.cs`** (DoD §3 shape) — `IsCharacterComplete` + per-tier sub-predicates (`IsLevel60`/`IsSpecComplete`/`IsPreRaidGeared`/`IsProfessionMaxed`/`IsAttuned`/`HasRiding`/`NeededReputationsAtGate`). **Compose** existing snapshot reads — do not duplicate. Separate `IsCharacterFullyMaxed()` holds the §5 stretch tiers. | C.statemodel | 2 | 0 | `UNIT:CharacterCompletionPredicateTests` — false on §2.1 initial, true on §2.2 terminal, + one per-tier fixture flipping a single missing dimension | todo | |
| C.itemset | **`docs/Plan/Autonomous/ITEM_TARGETS.md`** — per class/spec pre-raid gear set (dungeon-blue / pre-raid-BiS) + dungeon/quest accessories (DoD §2.2). Source: `leveling-guide/classes/` gear sections + dungeon/raid loot. Item names must be id-resolvable against `mangos.item_template`. Raid tier sets flagged stretch. | C.statemodel | 2 | 0 | `DOC:ITEM_TARGETS.md` covers all class/spec combos with id-resolvable item names | todo | |
| C.unlock-runtime | **`UnlockGraph.cs` + a markdown-table loader** for `decision-engine/unlock-graph.md`. Expose `IsAcyclic()` + `ArePrerequisitesMet(node, CharacterProgression)` (GATE = hard prereq, SOFT = advisory). Node predicates call the `C.progression` sub-predicates. Make the markdown available at runtime (embedded resource recommended — the guide is the only authoritative rule source, per `decision-engine/README.md`). | C.statemodel | 3 | 0 | `UNIT:UnlockGraphTests` — acyclic + gate eval correct per fixture (L1 fresh: leveling tier open / endgame closed) | todo | |
| C.goalplanner | **REPLACE the hardcoded `ProgressionPlanner` stub** (`Services/WoWStateManager/Progression/ProgressionPlanner.cs`) — R18: delete the 8 hardcoded priority branches + the stubbed `ResolveGearSource`/`ResolveRepSource`/`GetProfessionLevel` resolvers; keep the SM-side `ObjectiveMessage`-returning shape (planner stays SM-side per R17 — it selects the Objective, BotRunner decomposes it). New logic implements `decision-engine/README.md` `PickNextAction`: P0 interrupts (survival) first, then the highest-priority eligible action among prerequisite-met bracket actions per `leveling-priority.md` (the 5 bands) + the current bracket's `per-bracket-actions/NN-*.md`, terminating on `IsCharacterComplete`. Dispatches into `B.composer`. See plan §6. | C.progression, C.unlock-runtime, B.composer, B.solo-xp | 5 | 0 | `UNIT:ProgressionPlannerTests` (correct next-action per fixture: L1→leveling action, L40-no-mount→mount path, L60-gear-gap→dungeon) + `LIVE:` one bot advances ≥1 unlock-graph tier unattended | todo | |
| C.advisor-wire | **`DecisionEngineClient` shim (`Plan/14 S10.0`) + `NoAdvice` default.** Wire the 7-advisor RPC surface (rotation/threat/reward/objective/chat_template/activity_request/personality_cluster — `Spec/20`) over the port-5004 protobuf transport with a `NoAdvice` stub default so the planner makes correct deterministic choices and "advisors off" still completes within budget (`Plan/14` correctness invariant). `RewardSelector` (impl absent — only `RewardSelectorContractTests` exists) consults the reward advisor when confidence ≥ 0.5 (`Plan/14 S10.1`). This is the in-scope bridge to the ML stretch (§5). | C.goalplanner | 3 | 0 | `UNIT:` advisor-wire tests + a replay with `advisors=NoAdvice` produces `roster_distance_delta ≤ 0` (no correctness regression) | todo | |

---

## Phase D — Autonomous roster loop (Phase 6 foundation)

> This is the [`../07_PHASE6_AUTOPROGRESSION.md`](../07_PHASE6_AUTOPROGRESSION.md)
> + [`../../Spec/05_PROGRESSION.md`](../../Spec/05_PROGRESSION.md) vision,
> finally buildable once C is green. `RosterPlanner` does **not exist**
> yet (a known orphan — root WWoW `CLAUDE.md` flags it). Do NOT start
> before C — a roster planner on a half-built progression layer is an
> undebuggable pile.

| ID | Title | Deps | Iters | Iters-Done | Accept | Status | Commit |
|---|---|---|---|---|---|---|---|
| D.roster | **`RosterPlanner`** — N bots, each assigned a long-term goal; faction/class/spec/profession coverage rules (`Plan/07` exit criteria); idle is a bug. Population-shape config + `AccountRoster` persistence. | C.goalplanner | 4 | 0 | `UNIT:RosterPlannerTests` (coverage rules + goal assignment) + no bot idle in a live multi-bot run | todo | |
| D.progression-loop | **Per-bot progression pursuit** — each bot's `ProgressionPlanner` picks + runs Activities continuously; bots converge into groups organically (`B.group`). | D.roster, B.group | 4 | 0 | `LIVE:` 3+ bots each advance their goal over a window; ≥1 organic group forms | todo | |
| D.economy-seed | **Steady-state economy seeding** (AH posting / vendor traffic / mail / trade-chat) at a calibrated rate so the economy looks alive (`Spec/00_VISION.md §3`). | D.roster, B.economy | 2 | 0 | `LIVE:` economy steady-state holds over a window (AH listings + mail flow observed) | todo | |
| D.soak | **Multi-hour unattended roster soak** — the population-level generalization of `Plan/07`'s 24h-20-bot test, scoped to a churnable duration. No human input, no unrecovered failure, zero idle-bot warnings. | D.progression-loop, D.economy-seed, B.recovery | 2 | 0 | `SOAK:` (≥6h, extend toward 24h) no human input, no unrecovered failure, no idle-bot warning | todo | |

---

## Phase E — Endgame flips (stretch-adjacent: raids / world bosses / catalog fill)

> These broaden coverage past the core done-predicate (§5 stretch). The
> raid family is explicitly deferred in the activity docs (needs
> GM-applied gear + attunement fixtures). The loop reaches these only
> after the critical path + core Phase B are green.

| ID | Title | Deps | Iters | Iters-Done | Accept | Status | Commit |
|---|---|---|---|---|---|---|---|
| E.raids | **Raid family** — `RaidPositioningTask` / `RaidEncounterTask` / `MasterLootTask` / `ReadyCheckTask` (all `not-started`, deferred pending GM gear + attunement fixtures — survey). Start with a 20-man (`raid.zg` / `raid.aq20`, no attunement). Pair: [`../Activities/raids.md`](../Activities/raids.md). | B.group, B.combat, B.attunements | 4 | 0 | `LIVE:` a 20-man group clears the first boss of `raid.zg` or `raid.aq20` | todo | |
| E.world-bosses | **World-boss engagement + spawn detection** (`WorldBossEngagementTask` + per-boss `SWB.*` specs — `not-started`). Pair: [`../Activities/world-bosses.md`](../Activities/world-bosses.md). | B.group, B.combat | 3 | 0 | `LIVE:` a raid engages a staged world boss (`boss.azuregos`/`boss.kazzak`) | todo | |
| E.catalog-fill | **`Plan/13` catalog expansion (86 → ~150)** — SM 4-wing rows + Stockades + dungeon-quest sub-Activities + escorts + holiday events + social + wPvP (`Plan/13 S9.1-S9.7`). Each new row inherits the training-trace + dynamic-progressive contract. The `CatalogMarkdownDriftTests` row-count assertion is already loosened to `[130,180]` (`S9.8`). | B.composer | 6 | 0 | `DOC:`+`UNIT:` catalog grows with drift tests green; ≥1 new-family Activity reaches R-Live | todo | |

---

## Notes for the human reviewer

- **Two load-bearing unblockers, not one.** Unlike the sibling FFXI
  loop (one unblocker: BG dispatch), WWoW has two: **`B.composer`**
  (`S2.0` — nothing turns a catalog row into a live Objective graph) and
  **`C.goalplanner`** (replace the hardcoded `ProgressionPlanner` stub
  with a `decision-engine/`-driven chooser). The composer unblocks
  every Activity reaching R-Live; the goalplanner makes the bot *choose*.
- **The critical path to "characters autonomously level"** is explicit:
  `B.composer → B.combat → B.travel → B.questing → B.solo-xp →
  C.statemodel → C.progression → C.unlock-runtime → C.goalplanner →
  D.roster → D.progression-loop`. An agent that does only this path
  produces a bot that autonomously levels (even if not every gear tier /
  profession / endgame raid is covered). Everything else broadens
  coverage.
- **Trust nothing green you didn't run.** `00_INDEX.md` carries `done`
  claims (gathering, dungeon task-family) that predate a reliable
  headless harness. `A.4` re-validates them; a row stays `done` here only
  if its `Accept` passed this iteration.
- **The two human-gated items are isolated + flagged.** `A.6` is an
  operator *decision* (the `Q-D5-1` pathfinding data-dir repoint —
  changes shared bake state, so it needs a go-ahead). Any future
  live-RE capture for un-RE'd content is `blocked:human-RE`. The loop
  scaffolds and routes around both.
- **Honor the pathfinding overhaul freeze.** Mesh fixes go in
  `tools/MmapGen/`, not the managed repair pipeline (`CLAUDE.md`,
  `docs/physics/PATHFINDING_OVERHAUL.md`). `A.7`/`B.travel` touch
  caller-side route consumption only.
- **Stretch is out of the core loop on purpose** (§5 of the DoD):
  3,000-bot scale-load, full ML-advisor maturity, raid tier sets, all
  professions, behavioral-variation polish. The loop reaches "everything
  works" when the §2.2 character predicate is R-Auto green + a small
  roster soaks (`D.soak`).
