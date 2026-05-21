# Plan 13 — Phase 9: Catalog completeness

> **Goal.** Close the gap between the 86-row initial catalog and the
> Vision acceptance criterion "every legal player activity has a
> catalog row." Add the missing dungeons, holiday events, escort
> family, social services, mage-portal / warlock-summon services, and
> dungeon-quest sub-Activities so the OnDemand UI can launch any
> activity a human would ever request.
>
> **Entry pre-requisite.** Phase 0 done (catalog scaffold exists).
> Phase 9 can run in parallel with Phases 1–8; the new rows do not
> change existing rows' behavior.

## Exit criteria

- [ ] Every dungeon in the game has a catalog row, including Scarlet Monastery's 4 wings (`dungeon.sm-cathedral`, `-library`, `-armory`, `-graveyard`) and `dungeon.stockades`.
- [ ] Dungeon-quest sub-Activities catalog: one row per `quest_template.Type=81` quest that has a unique pickup → in-dungeon → turn-in flow not already covered by the parent dungeon Activity.
- [ ] Holiday events: rows for Lunar Festival, Hallow's End, Winter Veil, Midsummer Fire Festival, Children's Week, Darkmoon Faire (if server-supported).
- [ ] Social services: `social.mage-port`, `social.warlock-summon`, `social.lfg-cycle`, `social.trade-chat-cycle`, `social.guild-events`, `social.city-ambient`.
- [ ] Escort family promoted to standalone Activity rows for every escort quest tracked in `creature_template.ScriptName LIKE '%Escort%'` or `quest_template.SpecialFlags & ESCORT`.
- [ ] World PvP zone activities: `wpvp.epl-graveyards`, `wpvp.alterac-mountains-skirmish` (if server-supported).
- [ ] `Plan/Activities/00_INDEX.md` lists every new row with slot status.
- [ ] `CatalogMarkdownDriftTests` passes against the expanded catalog (likely ~130–160 rows).

## Slots

### S9.1 — Scarlet Monastery 4-wing rows

- **Owner:** `monorepo-worker`
- **Status:** open
- **Owned paths:**
  - `Services/WoWStateManager/Activities/Catalog/DungeonShard.cs`
  - `Plan/Activities/00_INDEX.md`
  - `Bot/named-locations.json`
- **Read-only paths:** `Exports/GameData.Core/Models/Activities/ActivityDefinition.cs`, `docs/Spec/04_ACTIVITIES.md`, MaNGOS `instance_template` rows for mapId=189.
- **Spec contracts:** [`Spec/04_ACTIVITIES.md`](../Spec/04_ACTIVITIES.md), [`Spec/22_WORLD_CYCLES.md §6`](../Spec/22_WORLD_CYCLES.md#6-raid-window-scheduling-autonomous) (lockout policy for non-raid wings is None).
- **Goal:** Add `dungeon.sm-graveyard`, `dungeon.sm-library`, `dungeon.sm-armory`, `dungeon.sm-cathedral` rows. Each wing has a separate map+entry coord and a distinct boss list. Cross-reference [`leveling-guide/dungeons/`](../leveling-guide/dungeons/) for per-wing detail (file pending).
- **Procedure:**
  1. Query MaNGOS via SOAP for SM boss creature_template entries (Houndmaster Loksey, Arcanist Doan, Herod, Scarlet Commander Mograine + High Inquisitor Whitemane).
  2. Resolve the four wing portal coords from `Bot/named-locations.json`; add new entries if missing.
  3. Append four `ActivityDefinition` rows under `DungeonShard.SM_Wings`.
  4. Update `Plan/Activities/00_INDEX.md` with the four new ids.
- **Success criteria:** rows compile; `ActivityCatalogTests` invariants R1/R14/R16/R18 pass on each new row; `OnDemandActivityLauncher` accepts the four new ids in a unit test (LegalityValidator stage returns `Ok`).
- **Failure modes:**
  - Named-location entry missing → catalog test asserts R14 fails; row is `gated`.
  - Boss creature_template missing in target server → row marked `gated` per [`#failure-recovery`](#failure-recovery); does NOT block other slots.
- **ML integration sub-bullet:** none for this slot — rows are hand-authored from the static dungeon layout.

### S9.2 — Stockades row

- **Owner:** `monorepo-worker`
- **Status:** open
- **Owned paths:** as S9.1
- **Read-only paths:** as S9.1, plus the existing 86 catalog rows for prior-art shape.
- **Spec contracts:** as S9.1.
- **Goal:** Add `dungeon.stockades` (Alliance-only at the door but cross-faction-eligible inside; map=34). Range 22-30, MinPlayers=5.
- **Procedure:**
  1. Confirm `instance_template.map=34` exists; capture entry coord `Bot/named-locations.json:stockades-entrance`.
  2. Add row with `FactionPolicy.AllianceFirstHordeFallback` (Spec/04) and `RoleTemplate.StandardDungeon5`.
- **Success criteria:** as S9.1.
- **Failure modes:** as S9.1; additionally, if server caps say `Stockades=false`, row is `gated`.
- **ML integration sub-bullet:** none.

### S9.2 — Stockades row

- **Owner:** `monorepo-worker`
- **Status:** open
- **Owned paths:** as S9.1
- **Goal:** Add `dungeon.stockades` (Alliance-only at the door but cross-faction-eligible inside; map=34). Range 22-30, MinPlayers=5.

### S9.3 — Dungeon-quest sub-Activities

- **Owner:** `monorepo-worker`
- **Status:** open
- **Depends on:** Phase 4 (`IMangosCatalog` exists).
- **Owned paths:**
  - `Services/WoWStateManager/Activities/Catalog/DungeonQuestShard.cs` (new)
  - `Plan/Activities/dungeons.md`
- **Read-only paths:** `Services/WoWStateManager/Catalog/MangosCatalog.cs`, MaNGOS `quest_template` + `quest_relations` rows.
- **Spec contracts:** [`Spec/04_ACTIVITIES.md`](../Spec/04_ACTIVITIES.md), [`architecture/aota/04_QUEST_CHAINS.md`](../architecture/aota/04_QUEST_CHAINS.md).
- **Goal:** For each dungeon, enumerate the in-dungeon quests (`Type=81`) and add a catalog row per quest chain that has a unique pickup+turn-in NPC pair. Examples: "Mission: Possible But Not Probable" (Gnomeregan), "The Iron Marshal" (BRD), "Squires of the Aerie Peak" (LBRS).
- **Procedure:**
  1. SOAP-query MaNGOS for all `quest_template` rows where `Type=81` (instance) AND `MinLevel >= 15`.
  2. Group by `(pickupNPC, turninNPC)` pair; treat each unique pair as a candidate sub-Activity.
  3. Filter: discard rows whose pickup+turn-in are both inside the parent dungeon AND whose objective columns are subset of the parent dungeon's clear sequence.
  4. Emit one row per remaining candidate; cross-reference parent dungeon row via `RelatedActivityIds`.
- **Success criteria:** ≥3 sub-Activity rows added (BRD/Gnomeregan/LBRS minimum); each compiles; `ActivityCatalogTests` invariants pass.
- **Failure modes:** quest_template row absent on the live server → row marked `gated`; pickup or turn-in NPC missing spawn → row marked `gated` with detail `missing_spawn`.
- **ML integration sub-bullet:** Off-line catalog-row auto-suggest tool (slot S9.9 trace pipeline) scores candidate dungeon-quest pairs by how often bots stall on "Type=81 unclassified" during dungeon clears in `tmp/test-runtime/traces/`. High-stall pairs become priority candidates for this slot. Consumes Spec/20 §6.1 trace lines, NOT a runtime advisor.

### S9.4 — Escort-family Activities

- **Owner:** `monorepo-worker`
- **Status:** open
- **Owned paths:**
  - `Services/WoWStateManager/Activities/Catalog/EscortShard.cs` (new)
  - `Plan/Activities/escorts.md` (new)
- **Read-only paths:** MaNGOS `creature_template` (filter on `ScriptName LIKE '%Escort%'`), `quest_template.SpecialFlags`.
- **Spec contracts:** [`Spec/04_ACTIVITIES.md`](../Spec/04_ACTIVITIES.md), [`Spec/19_AOTA_RUNTIME.md §4`](../Spec/19_AOTA_RUNTIME.md#4-objectivetype-enum-closed-set-proto-mirrored) (`ObjectiveType.Escort=8`).
- **Goal:** Promote each escort quest to a standalone Activity row. Escort quests have a different snapshot profile (NPC-death failure mode, no kill counter, fixed duration) that justifies catalog separation. Examples: "Tooga's Quest", "Stinky's Escape", "Cluck!".
- **Procedure:**
  1. Query `creature_template` for escort scripts (`ScriptName LIKE '%Escort%'`) + `quest_template` rows that reference them.
  2. Group by zone; emit one Activity row per escort quest.
  3. Set `TaskFamily="Escort"`, `RoleTemplate.Solo`, `LockoutPolicy.None`, `Duration` from script `WaypointCount * AvgPaceSec`.
  4. Write `Plan/Activities/escorts.md` matching the shape of the other 17 family docs.
- **Success criteria:** ≥10 escort rows added; family doc lints clean against `Plan/Activities/00_INDEX.md` schema.
- **Failure modes:** script entry missing on target server → row marked `gated`; NPC despawn before quest available → no row (event-gated quests deferred to S9.5).
- **ML integration sub-bullet:** none — escort rows are deterministic from DB.

### S9.5 — Holiday event Activities

- **Owner:** `monorepo-worker`
- **Status:** open
- **Depends on:** [`Spec/22_WORLD_CYCLES.md §5`](../Spec/22_WORLD_CYCLES.md#5-game-events-game_event) `IGameEventProvider` lands (open follow-up — no Plan slot yet per Plan/SPEC_FILL_LOOP.md pass-4 note).
- **Owned paths:**
  - `Services/WoWStateManager/Activities/Catalog/HolidayShard.cs` (new)
  - `Plan/Activities/world-events.md`
- **Read-only paths:** MaNGOS `game_event` rows, `Spec/22`.
- **Spec contracts:** [`Spec/04_ACTIVITIES.md`](../Spec/04_ACTIVITIES.md), [`Spec/22_WORLD_CYCLES.md §5`](../Spec/22_WORLD_CYCLES.md#5-game-events-game_event).
- **Goal:** Add one Activity row per `game_event` row that has bot-relevant content:
  - `event.lunar-festival.coin-cycle`
  - `event.hallows-end.candy-bucket-tour`
  - `event.hallows-end.headless-horseman` (gated by server cap)
  - `event.winter-veil.gift-collection`
  - `event.midsummer.bonfire-tour`
  - `event.children-week.orphan-escort` (technically escort-family; cross-referenced from S9.4)
  - `event.darkmoon-faire.tickets-and-vendor` (if server-supported)
  - `event.stv-fishing-extravaganza` (already exists; rename for consistency)
- **Procedure:**
  1. Enumerate `game_event` rows; pair each with the bot-relevant quest chain (e.g. event 7 = Hallow's End → candy-bucket quest chain by zone).
  2. Emit one Activity row per event; set `Family=WorldEvent`, `EligibilityGate` to `GameEventActive(event_id)`.
  3. Cross-reference Escort-family rows (`event.children-week.orphan-escort` references the escort row from S9.4).
- **Success criteria:** ≥6 event rows added; `ActivityCatalogTests` confirms every `Family=WorldEvent` row references a valid `game_event` row (added in S9.8 invariant).
- **Failure modes:** server lacks `game_event` row for an event → row added but auto-gated by the eligibility predicate; OnDemand request returns `RejectionCode.ACTIVITY_TIME_BLOCKED` per [`Spec/23 §3`](../Spec/23_ONDEMAND_API.md#3-response-shape).
- **ML integration sub-bullet:** Off-line tool scores event rows by `outcome.xp_gained + outcome.reward_value` from trace data to prioritize which holiday events the worker emits first.

### S9.6 — Social-service Activities

- **Owner:** `monorepo-worker`
- **Status:** open
- **Depends on:** Phase 11 social-fabric substrate ([`15_PHASE11_SOCIAL_FABRIC.md`](15_PHASE11_SOCIAL_FABRIC.md)).
- **Owned paths:**
  - `Services/WoWStateManager/Activities/Catalog/SocialShard.cs` (new)
  - `Plan/Activities/social.md`
- **Read-only paths:** `Spec/21_SOCIAL_FABRIC.md`, `Spec/24_BEHAVIORAL_VARIATION.md` (for `ChattyLevel`-driven eligibility).
- **Spec contracts:** [`Spec/21_SOCIAL_FABRIC.md §2`](../Spec/21_SOCIAL_FABRIC.md#2-activity-rows-backed-by-this-spec), [`Spec/04_ACTIVITIES.md`](../Spec/04_ACTIVITIES.md).
- **Goal:** Add:
  - `social.mage-port` — a mage bot escorts a human to a major city via Teleport / Portal spells.
  - `social.warlock-summon` — two pool bots help summon a human across the world.
  - `social.lfg-cycle` — a bot posts to LFG channel + accepts dungeon invites.
  - `social.trade-chat-cycle` — a bot rotates AH chatter (Spec/21).
  - `social.guild-events` — bot participates in guild MOTD / chat / bank cycles.
  - `social.city-ambient` — idle-city service traffic (Spec/21 §7).
- **Procedure:**
  1. Emit six rows under `SocialShard.SocialServices`; set `Family=Social`, `RoleTemplate=Solo` for trade/lfg/city-ambient/guild and `RoleTemplate.MagePort` (1 mage) / `RoleTemplate.WarlockSummon` (1 warlock + 1 supporter).
  2. Eligibility predicates per `Spec/24` knobs (e.g. `social.trade-chat-cycle` only eligible when `ChattyLevel != Quiet`).
  3. Write `Plan/Activities/social.md` cross-referencing Plan/15 implementation slots.
- **Success criteria:** all six rows compile; `ActivityCatalogTests` R-Social invariant passes (every `Family=Social` row has `social.*` id prefix, enforced in S9.8).
- **Failure modes:** Plan/15 substrate not landed → rows present but `Family=Social` Activities cannot launch (LegalityValidator returns `MISSING_SUBSTRATE`); does NOT block catalog tests.
- **ML integration sub-bullet:** none for the catalog rows themselves; their *internal* template choice is the Spec/21 §11 advisor.

### S9.7 — World PvP rows (gated)

- **Owner:** `monorepo-worker`
- **Status:** open
- **Owned paths:** as S9.1
- **Read-only paths:** `Services/WoWStateManager/Capabilities/ServerCapabilities.cs`.
- **Spec contracts:** [`Spec/04_ACTIVITIES.md`](../Spec/04_ACTIVITIES.md).
- **Goal:** Add `wpvp.epl-graveyards` (Eastern Plaguelands graveyard tower captures) and `wpvp.silithyst-shard-runs` (Silithus). Gate behind `ServerCapabilities.WorldPvp = true`.
- **Procedure:**
  1. Add `WorldPvp` boolean to `ServerCapabilities` schema if absent.
  2. Emit two rows under `DungeonShard.WorldPvp`; set `Family=WorldEvent` with `EligibilityGate.RequiresServerCapability("WorldPvp")`.
- **Success criteria:** rows present; `LegalityValidator` rejects launches with `RejectionCode.SERVER_DISABLED` when capability is false.
- **Failure modes:** capability flag missing on target server → both rows auto-gated.
- **ML integration sub-bullet:** none — wPvP rows are server-cap-driven.

### S9.8 — Catalog tests update

- **Owner:** `monorepo-test-runner`
- **Status:** open
- **Depends on:** S9.1..S9.7
- **Owned paths:**
  - `Tests/BotRunner.Tests/Activities/CatalogMarkdownDriftTests.cs`
  - `Tests/BotRunner.Tests/Activities/ActivityCatalogTests.cs`
- **Read-only paths:** `Plan/Activities/00_INDEX.md`, all catalog shard files written in S9.1-S9.7.
- **Spec contracts:** [`Spec/04_ACTIVITIES.md`](../Spec/04_ACTIVITIES.md) catalog invariants R1/R14/R16/R18.
- **Goal:** Update the drift test to accept the expanded row count (~130-160). Add two new invariants:
  - R-Social: every `Family=Social` row has a `social.*` id prefix.
  - R-WorldEvent: every `Family=WorldEvent` row references a valid `game_event` row (or is annotated `gated=true` when the server lacks the event).
- **Procedure:**
  1. Loosen the drift-test expected-row range to `[130, 180]`.
  2. Add invariant tests R-Social and R-WorldEvent.
  3. Add a regression: every row in `Plan/Activities/00_INDEX.md` resolves to a real `ActivityDefinition` in the compiled catalog.
- **Success criteria:** `dotnet test --filter Activities` green; no new flakes.
- **Failure modes:** index-markdown / catalog-row drift → R-Index test fails with a diff of missing/extra rows.
- **ML integration sub-bullet:** none.

### S9.9 — Live-validation for one row per new family

- **Owner:** `monorepo-test-runner`
- **Status:** open
- **Depends on:** Phase 2 (OnDemand launcher), S9.1..S9.7.
- **Owned paths:**
  - `Tests/BotRunner.Tests/LiveValidation/Phase9/` (new folder)
- **Read-only paths:** all S9.1-S9.7 catalog shards.
- **Spec contracts:** [`Spec/13_TESTING.md`](../Spec/13_TESTING.md) (LiveValidation conventions), [`Spec/23_ONDEMAND_API.md`](../Spec/23_ONDEMAND_API.md).
- **Goal:** One LiveValidation test per new family:
  - `OnDemand_SmCathedral_Dungeon` — Alliance group, full clear; asserts via `snapshot.ondemand_instances[0].stage` reaches `DONE`.
  - `OnDemand_StvFishingExtravaganza_WorldEvent` — bot wins or places; asserts via `snapshot.active_game_events` contains event_id=26 at launch.
  - `OnDemand_MagePort_SocialService` — mage bot ports a test human; asserts via `snapshot.recent_chat_messages` contains "Teleport: <city>".
  - `OnDemand_HallowsEnd_CandyBucketTour` — bot completes 5 buckets; asserts via inventory-change events for candy items.
  - `Phase9CatalogFill_DynamicProgressive_ExpandedCatalogClosesGoalDistanceTest` — the dynamic-progressive invariant. See §15.
- **Procedure:**
  1. Use the established `LiveBotFixture` and `StageBotRunner*Async` helpers per the Test Isolation Rules; do NOT construct `ObjectiveMessage` directly.
  2. Each test stages with a deterministic personality (Spec/24 §7).
  3. Trace each run to `tmp/test-runtime/traces/<test-name>/` for the off-line tools (S9.3 ML sub-bullet, holiday-prioritization S9.5).
- **Success criteria:** all five tests green on `Westworld-Test`; trace files produced.
- **Failure modes:** any LiveValidation timeout → check parent dungeon / event substrate first; do NOT skip-for-resource-not-found per [`CLAUDE.md`](../../CLAUDE.md#test-skip-policy--critical).
- **ML integration sub-bullet:** Trace data feeds the catalog-row auto-suggest tool (cross-slot with S9.3 + S9.5); also feeds Spec/20 §6 training-data pipeline.

## Slot ownership

All slots `owned-paths` are non-overlapping with Phase 1–8 slots; this
plan can run in parallel without conflict.

## Failure recovery

- **MaNGOS DB row missing** for a referenced quest / creature → mark the
  Activity row as `gated` and skip in `IActivityCatalog`.
- **Catalog test fails on count mismatch** → update the index markdown,
  not the test (the test enforces single-source-of-truth).
- **OnDemand launcher rejects the new id** → check that the `TaskFamily`
  field is in the canonical list (Spec/03 §catalog-of-task-families).

## Dynamic-progressive invariant

Phase 9's catalog expansion MUST preserve the dynamic-progressive
invariant from [`Spec/19 §10`](../Spec/19_AOTA_RUNTIME.md#10-dynamic-progressive-invariant)
across the *expanded* catalog:

1. **Dynamic.** With the catalog expanded by ≥50 rows, the
   `IActivityComposer.Compose(...)` output for two distinct bots
   `(class, level, faction, zone, attunements)` MUST produce different
   Activity sequences when their inputs differ in catalog-relevant
   ways. A small catalog can collapse divergent inputs onto the same
   Activity; the expanded catalog MUST NOT.
2. **Progressive.** Every new row added by S9.1-S9.7 MUST be
   *progressive in expectation* for at least one bot context — i.e.
   exist on at least one trace where its completion produces a
   `roster_distance_delta < 0`. Otherwise the row is decoration not
   gameplay, and the row author should justify it in
   `Plan/Activities/<family>.md`.

Asserted by
`Phase9CatalogFill_DynamicProgressive_ExpandedCatalogClosesGoalDistanceTest`
(slot S9.9 row 5).

## ML integration umbrella

No new runtime advisor; the existing Spec/20 surface is sufficient.
Catalog-fill consumes Spec/20 in two off-line ways:

- **Catalog-row auto-suggest tool** (slots S9.3 + S9.5 ML sub-bullets)
  scans `tmp/test-runtime/traces/<test-name>/*.jsonl` lines of kind
  `task_terminal` with `reason="catalog_invalid"` or
  `reason="task_unrecoverable"` and proposes candidate catalog rows
  whose presence would have rescued the trajectory. Output is a
  ranked candidate list, NOT auto-merged rows.
- **Holiday-event prioritization** (S9.5 ML sub-bullet) reads
  `outcome.xp_gained + outcome.reward_value` from event Activity
  traces to order which event rows the worker emits first.

Both tools are Python-side and run off-line. The C# contract is just
"produce trace lines per Spec/20 §6.1."

## Plan-slot cross-reference

| Slot | Spec contracts |
|---|---|
| S9.1 | [`Spec/04`](../Spec/04_ACTIVITIES.md), [`Spec/22 §6`](../Spec/22_WORLD_CYCLES.md#6-raid-window-scheduling-autonomous) |
| S9.2 | [`Spec/04`](../Spec/04_ACTIVITIES.md) |
| S9.3 | [`Spec/04`](../Spec/04_ACTIVITIES.md), [`aota/04`](../architecture/aota/04_QUEST_CHAINS.md) |
| S9.4 | [`Spec/04`](../Spec/04_ACTIVITIES.md), [`Spec/19 §4`](../Spec/19_AOTA_RUNTIME.md#4-objectivetype-enum-closed-set-proto-mirrored) |
| S9.5 | [`Spec/04`](../Spec/04_ACTIVITIES.md), [`Spec/22 §5`](../Spec/22_WORLD_CYCLES.md#5-game-events-game_event) |
| S9.6 | [`Spec/21 §2`](../Spec/21_SOCIAL_FABRIC.md#2-activity-rows-backed-by-this-spec) |
| S9.7 | [`Spec/04`](../Spec/04_ACTIVITIES.md) |
| S9.8 | [`Spec/04 catalog invariants`](../Spec/04_ACTIVITIES.md) |
| S9.9 | [`Spec/13`](../Spec/13_TESTING.md), [`Spec/23`](../Spec/23_ONDEMAND_API.md) |

## Related specs

- [`Spec/04_ACTIVITIES.md`](../Spec/04_ACTIVITIES.md) — `ActivityDefinition` shape.
- [`Spec/22_WORLD_CYCLES.md`](../Spec/22_WORLD_CYCLES.md) — holiday-event eligibility.
- [`Spec/21_SOCIAL_FABRIC.md`](../Spec/21_SOCIAL_FABRIC.md) — social-service Activities.
- [`architecture/aota/04_QUEST_CHAINS.md`](../architecture/aota/04_QUEST_CHAINS.md) — dungeon-quest sub-Activity composition.
- [`Spec/20_DECISION_ENGINE.md §6`](../Spec/20_DECISION_ENGINE.md#6-training-data-pipeline) — trace pipeline feeding the off-line catalog auto-suggest tool.
