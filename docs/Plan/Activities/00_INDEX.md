# Plan/Activities — Catalog Status Board

Every catalog activity has a row in this table plus a slot in the
relevant family file. Slot status fields:

- **spec** — `ActivityDefinition` in `ActivityCatalog.cs`.
- **task-family** — `IBotTask` implementation(s) the activity drives.
- **coordinator** — coordinator that orchestrates the multi-bot
  activity (where applicable).
- **tests** — LiveValidation test(s) asserting the full loop.

Status values: `not-started` | `in-progress` | `done`.

## Starter questing (1-10) — see [`quests.md`](quests.md)

| Id | Activity | Location | Faction | Level | spec | task-family | coordinator | tests |
|---|---|---|---|---|---|---|---|---|
| `quest.starter.elwynn-forest` | Starter questing | Elwynn Forest | Alliance | 1-10 | not-started | partial | not-started | not-started |
| `quest.starter.dun-morogh` | Starter questing | Dun Morogh | Alliance | 1-10 | not-started | partial | not-started | not-started |
| `quest.starter.teldrassil` | Starter questing | Teldrassil | Alliance | 1-10 | not-started | partial | not-started | not-started |
| `quest.starter.durotar` | Starter questing | Durotar | Horde | 1-10 | not-started | partial | not-started | not-started |
| `quest.starter.tirisfal-glades` | Starter questing | Tirisfal Glades | Horde | 1-10 | not-started | partial | not-started | not-started |
| `quest.starter.mulgore` | Starter questing | Mulgore | Horde | 1-10 | not-started | partial | not-started | not-started |

## Zone questing (10-60) — see [`quests.md`](quests.md)

| Id | Activity | Location | Faction | Level |
|---|---|---|---|---|
| `quest.zone.westfall` | Zone questing | Westfall | Alliance | 9-18 |
| `quest.zone.loch-modan` | Zone questing | Loch Modan | Alliance | 10-19 |
| `quest.zone.darkshore` | Zone questing | Darkshore | Alliance | 10-20 |
| `quest.zone.silverpine-forest` | Zone questing | Silverpine Forest | Horde | 10-20 |
| `quest.zone.the-barrens` | Zone questing | The Barrens | Horde | 10-25 |
| `quest.zone.redridge-mountains` | Zone questing | Redridge Mountains | Alliance | 15-25 |
| `quest.zone.ashenvale` | Zone questing | Ashenvale | Either | 18-30 |
| `quest.zone.duskwood` | Zone questing | Duskwood | Alliance | 18-30 |
| `quest.zone.wetlands` | Zone questing | Wetlands | Alliance | 20-30 |
| `quest.zone.hillsbrad-foothills` | Zone questing | Hillsbrad Foothills | Either | 20-30 |
| `quest.zone.stonetalon-mountains` | Zone questing | Stonetalon Mountains | Either | 16-27 |
| `quest.zone.thousand-needles` | Zone questing | Thousand Needles | Either | 25-35 |
| `quest.zone.desolace` | Zone questing | Desolace | Either | 28-38 |
| `quest.zone.arathi-highlands` | Zone questing | Arathi Highlands | Either | 30-40 |
| `quest.zone.stranglethorn-vale` | Zone questing | Stranglethorn Vale | Either | 30-45 |
| `quest.zone.dustwallow-marsh` | Zone questing | Dustwallow Marsh | Either | 35-45 |
| `quest.zone.badlands` | Zone questing | Badlands | Either | 35-45 |
| `quest.zone.tanaris` | Zone questing | Tanaris | Either | 40-50 |
| `quest.zone.feralas` | Zone questing | Feralas | Either | 40-50 |
| `quest.zone.searing-gorge` | Zone questing | Searing Gorge | Either | 43-50 |
| `quest.zone.azshara` | Zone questing | Azshara | Either | 45-55 |
| `quest.zone.the-hinterlands` | Zone questing | The Hinterlands | Either | 30-45 |
| `quest.zone.felwood` | Zone questing | Felwood | Either | 48-55 |
| `quest.zone.ungoro-crater` | Zone questing | Un'Goro Crater | Either | 48-55 |
| `quest.zone.western-plaguelands` | Zone questing | Western Plaguelands | Either | 50-60 |
| `quest.zone.eastern-plaguelands` | Zone questing | Eastern Plaguelands | Either | 53-60 |
| `quest.zone.burning-steppes` | Zone questing | Burning Steppes | Either | 50-58 |
| `quest.zone.winterspring` | Zone questing | Winterspring | Either | 55-60 |
| `quest.zone.silithus` | Zone questing | Silithus | Either | 55-60 |

## Dungeons — see [`dungeons.md`](dungeons.md)

| Id | Activity | Location | Level | Roles |
|---|---|---|---|---|
| `dungeon.ragefire-chasm` | Dungeon | Ragefire Chasm | 13-18 | 1T 1H 3D |
| `dungeon.wailing-caverns` | Dungeon | Wailing Caverns | 17-24 | 1T 1H 3D |
| `dungeon.deadmines` | Dungeon | Deadmines | 17-26 | 1T 1H 3D |
| `dungeon.shadowfang-keep` | Dungeon | Shadowfang Keep | 22-30 | 1T 1H 3D |
| `dungeon.blackfathom-deeps` | Dungeon | Blackfathom Deeps | 20-30 | 1T 1H 3D |
| `dungeon.razorfen-kraul` | Dungeon | Razorfen Kraul | 24-34 | 1T 1H 3D |
| `dungeon.gnomeregan` | Dungeon | Gnomeregan | 29-38 | 1T 1H 3D |
| `dungeon.razorfen-downs` | Dungeon | Razorfen Downs | 35-45 | 1T 1H 3D |
| `dungeon.uldaman` | Dungeon | Uldaman | 41-51 | 1T 1H 3D |
| `dungeon.zul-farrak` | Dungeon | Zul'Farrak | 44-54 | 1T 1H 3D |
| `dungeon.maraudon` | Dungeon | Maraudon | 46-55 | 1T 1H 3D |
| `dungeon.sunken-temple` | Dungeon | Sunken Temple | 50-56 | 1T 1H 3D |
| `dungeon.blackrock-depths` | Dungeon | Blackrock Depths | 52-60 | 1T 1H 3D |
| `dungeon.lower-blackrock-spire` | Dungeon | Lower Blackrock Spire | 55-60 | 1T 1H 8D |
| `dungeon.upper-blackrock-spire` | Dungeon | Upper Blackrock Spire | 58-60 | 1T 1H 8D |
| `dungeon.dire-maul-east` | Dungeon | Dire Maul East | 55-60 | 1T 1H 3D |
| `dungeon.dire-maul-west` | Dungeon | Dire Maul West | 55-60 | 1T 1H 3D |
| `dungeon.dire-maul-north` | Dungeon | Dire Maul North | 55-60 | 1T 1H 3D |
| `dungeon.scholomance` | Dungeon | Scholomance | 58-60 | 1T 1H 3D |
| `dungeon.stratholme-undead` | Dungeon | Stratholme Undead | 58-60 | 1T 1H 3D |
| `dungeon.stratholme-live` | Dungeon | Stratholme Live | 58-60 | 1T 1H 3D |

## Raids — see [`raids.md`](raids.md)

| Id | Activity | Location | Level | Roles | Attunement |
|---|---|---|---|---|---|
| `raid.zg` | Raid | Zul'Gurub | 60 | 2T 5H 13D (20) | none |
| `raid.aq20` | Raid | Ruins of Ahn'Qiraj | 60 | 2T 5H 13D (20) | none |
| `raid.mc` | Raid | Molten Core | 60 | 5T 8H 27D (40) | yes |
| `raid.onyxia` | Raid | Onyxia's Lair | 60 | 3T 6H 31D (40) | yes (horde/alliance chain) |
| `raid.bwl` | Raid | Blackwing Lair | 60 | 5T 9H 26D (40) | yes |
| `raid.aq40` | Raid | Temple of Ahn'Qiraj | 60 | 5T 9H 26D (40) | scarabs (server-wide) |
| `raid.naxx` | Raid | Naxxramas | 60 | 5T 10H 25D (40) | yes |

## Battlegrounds — see [`battlegrounds.md`](battlegrounds.md)

| Id | Activity | Location | Level | Roles |
|---|---|---|---|---|
| `bg.wsg` | Battleground | Warsong Gulch | 10-60 | 10v10 |
| `bg.ab` | Battleground | Arathi Basin | 20-60 | 15v15 |
| `bg.av` | Battleground | Alterac Valley | 51-60 | 40v40 |

## Professions — see [`professions-gathering.md`](professions-gathering.md) / [`professions-crafting.md`](professions-crafting.md)

| Id | Activity | Location | Level |
|---|---|---|---|
| `prof.mining-route` | Profession farming | Mining route | 1-60 |
| `prof.herbalism-route` | Profession farming | Herbalism route | 1-60 |
| `prof.skinning-route` | Profession farming | Skinning route | 1-60 |
| `prof.city-trainer-loop` | Profession leveling | City trainer + recipe loop | 5-60 |

## Economy — see [`economy.md`](economy.md)

| Id | Activity | Location | Level |
|---|---|---|---|
| `econ.ah-restock` | Economy | Auction house restock | 1-60 |
| `econ.vendor-loop` | Economy | Vendor + repair + bank + mail loop | 1-60 |

## Reputations — see [`reputations.md`](reputations.md)

| Id | Activity | Location | Level |
|---|---|---|---|
| `rep.timbermaw-hold` | Reputation grind | Timbermaw Hold | 48-60 |
| `rep.argent-dawn` | Reputation grind | Argent Dawn | 50-60 |
| `rep.cenarion-circle` | Reputation grind | Cenarion Circle | 55-60 |
| `rep.thorium-brotherhood` | Reputation grind | Thorium Brotherhood | 50-60 |
| `rep.zandalar-tribe` | Reputation grind | Zandalar Tribe | 60 |

## Attunements — see [`attunements.md`](attunements.md)

| Id | Activity | Location | Level |
|---|---|---|---|
| `attune.mc` | Attunement | Molten Core attunement | 55-60 |
| `attune.ony-horde` | Attunement | Onyxia Horde chain | 55-60 |
| `attune.ony-alliance` | Attunement | Onyxia Alliance chain | 55-60 |
| `attune.bwl` | Attunement | Blackwing Lair attunement | 58-60 |
| `attune.naxx` | Attunement | Naxxramas attunement | 60 |

## World events — see [`world-events.md`](world-events.md)

| Id | Activity | Location | Level |
|---|---|---|---|
| `event.stv-fishing-extravaganza` | World event | STV Fishing Extravaganza | 30-60 |

## World bosses — see [`world-bosses.md`](world-bosses.md)

| Id | Activity | Location | Level |
|---|---|---|---|
| `boss.azuregos` | World boss | Azuregos (Azshara) | 60 |
| `boss.kazzak` | World boss | Lord Kazzak (Blasted Lands) | 60 |
| `boss.emerald-dragons` | World boss | Emerald Dragons (rotating) | 60 |

## Total: 86 rows

(Previously advertised as 88; the actual compiled catalog ships 86
distinct `ActivityDefinition` literals. See
[`01_CATALOG_ROWS.md`](01_CATALOG_ROWS.md) for the shard breakdown.
`Tests/BotRunner.Tests/Activities/CatalogMarkdownDriftTests.cs`
asserts the id sets here match the catalog.)

## Phase 9 expansion (forward-reference)

[`Plan/13_PHASE9_CATALOG_FILL.md`](../13_PHASE9_CATALOG_FILL.md) ships
the catalog from 86 → ~150 rows across slots S9.1-S9.7. The
`CatalogMarkdownDriftTests` row-count assertion was loosened (Plan/13
S9.8) to accept the `[130, 180]` range during the expansion. Forward
references the test surface targets:

| Plan/13 slot | New rows (sketch) | New family file (if needed) |
|---|---|---|
| S9.1 | `dungeon.sm-graveyard`, `dungeon.sm-library`, `dungeon.sm-armory`, `dungeon.sm-cathedral` | extend [`dungeons.md`](dungeons.md) |
| S9.2 | `dungeon.stockades` | extend [`dungeons.md`](dungeons.md) |
| S9.3 | dungeon-quest sub-Activities (≥3 to start: BRD/Gnomeregan/LBRS) | extend [`dungeons.md`](dungeons.md) |
| S9.4 | ≥10 escort-family rows (Tooga, Stinky, Cluck, etc.) | new [`escorts.md`](escorts.md) |
| S9.5 | event.* rows for Lunar Festival / Hallow's End / Winter Veil / Midsummer / Children's Week / Darkmoon Faire | extend [`world-events.md`](world-events.md) |
| S9.6 | `social.mage-port`, `social.warlock-summon`, `social.lfg-cycle`, `social.trade-chat-cycle`, `social.guild-events`, `social.city-ambient` | new [`social.md`](social.md) |
| S9.7 | `wpvp.epl-graveyards`, `wpvp.silithyst-shard-runs` (gated) | extend [`dungeons.md`](dungeons.md) wpvp section |

Each new row inherits the same training-trace contract below.

## Training-trace contract (per row)

Every catalog row, when run through LiveValidation per
[`Spec/13 §Training-trace capture`](../../Spec/13_TESTING.md#training-trace-capture),
produces a JSONL trace at
`tmp/test-runtime/traces/<TestClass.TestMethod>/<timestamp>.jsonl`.
This section pins **which Spec/20 advisors each family triggers** so
the off-line trainer knows what features to extract. Trace lines
follow [`Spec/20 §6.1`](../../Spec/20_DECISION_ENGINE.md#61-trace-line-schema).

| Family | Spec/20 advisors triggered per Activity run | Expected outcome-line count per Activity | Per-Activity training value |
|---|---|---|---|
| Starter questing | `objective` (compose questing chain), `reward` (turn-in choices), `personality_cluster` (one-shot at bot creation) | 1 per Activity completion | medium — small chains, fast iteration, good for warm-start data on the L1-10 brackets |
| Zone questing | `objective` × many (chain ordering, sub-Objective tie-breaks), `reward` × ~25 turn-ins, `chat_template` (LFG queue posts), `activity_request` (whisper-driven cross-zone hand-offs, rare) | 1-3 per Activity (Activity may chain into the next zone) | **high** — bulk of the leveling-loop training corpus; Phase-3 Objective model trains primarily on these |
| Dungeons | `objective` (boss-pull tie-breaks), `threat` × many (tank target swaps), `rotation` × very many (combat ticks), `reward` (boss-loot rolls), `activity_request` (OnDemand `!run rfc` etc.) | 1 per full clear | **high** — combat-rotation training corpus origin; the rotation advisor's labeled data is mostly dungeon runs |
| Raids | `objective` (encounter order), `threat` × very many, `rotation` × very many, `reward` × ~7-15 boss loot, `chat_template` (raid-recruit posts), `activity_request` (`!raid mc`) | 1 per boss kill or 1 per attempt | **highest** — per-encounter sub-traces; raid traces are gold for tank-swap and rotation training |
| Battlegrounds | `objective` (cap/hold/escort sub-Objectives), `threat` (PvP target prioritization — different model from PvE!), `rotation` (PvP rotation diverges from PvE), `chat_template` (BG comms minimal) | 1 per match | medium — limited Phase-3 value for PvE models; high value for PvP-specific models |
| Profession farming | `objective` (route ordering — interleave with side quests), `cheapest-source-learner` (when farming gathered mats has alternatives), `chat_template` (`wts` posts for surplus) | 1 per route completion | medium — economy training corpus |
| Profession crafting | `objective` (recipe-priority ordering), `cheapest-source-learner` (reagent sourcing, recursive), `reward` (recipe choice when learning multiple) | 1 per craft cycle | medium |
| Economy | `objective` (AH cycle vs vendor cycle), `chat_template` (`wts`/`wtb`/lfg posts), `personality_cluster` (chattiness + AhPostingUnderscutPercent) | 1 per cycle | **high for economy + chat ML** |
| Reputations | `objective` (turn-in chain ordering — long-tail; e.g. Argent Dawn bone-fragments), `cheapest-source-learner` (rep-token sourcing) | 1 per Activity (often ranges into the next Activity if rep target is mid-tier) | high — these chains produce the longest contiguous Objective sequences, valuable for sequence-learning |
| Attunements | `objective` (chain ordering across zones), `cheapest-source-learner` (gated item gates per [`aota/05`](../../architecture/aota/05_ITEM_REQUIREMENTS.md)) | 1 per full chain | **highest** — multi-Activity recursive chains, strong test of the Objective advisor's planning depth |
| World events | `objective` (event sub-Objective ordering), `activity_request` (`!fish ratchet` etc.) | 1 per event Activity completion (event-time-windowed) | low — limited training-data volume |
| World bosses | `objective` (timing decisions — pre-buff vs immediate), `threat`, `rotation`, `chat_template` (recruit posts) | 1 per attempt | low (volume); high per-trace value when they fire |
| Social (Plan/13 S9.6) | `chat_template` × very many (every emitted post), `personality_cluster`, `activity_request` (whisper-driven mage-port / warlock-summon) | varied (mage-port is 1; trade-chat-cycle is N posts per cycle) | **highest for chat_template + activity_request ML** |
| Escorts (Plan/13 S9.4) | `objective` (escort routing tie-breaks), `threat` (defending the escort target) | 1 per escort attempt | medium |
| Holiday events (Plan/13 S9.5) | `objective`, `activity_request`, `personality_cluster` | varied | low–medium |

The Phase-3 ONNX trainer per advisor picks its corpus from the family
columns above:

- **`objective` model**: trains primarily on Zone questing + Dungeons
  + Raids + Reputations + Attunements traces (highest-volume Objective
  diversity).
- **`rotation` model**: trains primarily on Dungeons + Raids combat
  ticks; PvP rotation is a separate model trained on Battlegrounds.
- **`threat` model**: tank-only; trains primarily on Dungeons + Raids
  tank traces; PvP threat is a separate model.
- **`reward` model**: trains on every turn-in + boss kill across all
  families.
- **`chat_template` model**: trains on Social + Economy chat-emission
  traces.
- **`activity_request` model**: trains on the Shodan whisper handler's
  operator-confirmation traces (every `!run/!raid/!bg/!port/!summon`
  whisper that gets accepted contributes a label).
- **`personality_cluster` model**: trains on per-bot
  account-creation snapshots aggregated across all families.

## Dynamic-progressive invariant (per-row trace assertion)

Per [`Spec/13 §Dynamic-progressive invariant`](../../Spec/13_TESTING.md#dynamic-progressive-invariant),
**every** catalog row's LiveValidation trace MUST produce an `outcome`
line with `roster_distance_delta ≤ 0` when `completion="complete"`.
This is enforced both:

1. **At fixture teardown** (Spec/13 §Correctness contract — failing
   trace = failing test).
2. **Across the per-row test suite** by
   `Testing_DynamicProgressive_LiveValidationProducesNonPositiveRosterDeltaTest`
   (Spec/13 §Test surface).

If a catalog row's trace consistently produces
`roster_distance_delta = 0` (cosmetic-only completions) AND no
trace shows strictly-negative delta over a representative sample,
the row is **decoration**, not gameplay, and the row author must
either:

- Justify the decoration in the matching family file
  (e.g. `quests.md`, `social.md`) with an explicit `Rationale` block,
  OR
- Adjust the Activity's Objective sequence so the completion closes
  at least one axis of `RosterPlanner.Distance` (e.g. `social.city-ambient`
  alone closes nothing; pairing it with mailbox + AH posts closes
  the `GoldTargetPct` axis indirectly).

## Plan-slot cross-reference

| Slot | Owns | Section here |
|---|---|---|
| [`Plan/03/S2.0`](../03_PHASE2_ONDEMAND_ENGINE.md#s20--iactivity--iobjective-runtime-contracts) | `IActivityComposer.Compose(...)` (the runtime that walks every row in this index) | All rows |
| [`Plan/13`](../13_PHASE9_CATALOG_FILL.md) | The 86 → ~150 catalog expansion | §Phase 9 expansion |
| [`Plan/13/S9.8`](../13_PHASE9_CATALOG_FILL.md) | `CatalogMarkdownDriftTests` row-count + invariant tests | §Total: 86 rows |
| [`Plan/14/S10.7`](../14_PHASE10_DECISION_ENGINE_INTEGRATION.md#s107--training-trace-plumbing) | `TraceWriter.cs` (writes the JSONL traces this section consumes) | §Training-trace contract |
| [`Plan/14/S10.8`](../14_PHASE10_DECISION_ENGINE_INTEGRATION.md#s108--livevalidation-for-advisor-wire) | The dynamic-progressive guard suite | §Dynamic-progressive invariant |
| Per-family Plan docs | `quests.md`, `dungeons.md`, `raids.md`, `bgs.md`, `professions.md`, `economy.md`, `reputations.md`, `attunements.md`, `world-events.md`, `world-bosses.md`, plus Plan/13 new `escorts.md` + `social.md` | per-row implementation slot tracking |

## Test surface

Contract tests for the index file itself live at
`Tests/BotRunner.Tests/Activities/IndexTrainingTracePlanContractTests.cs`
(complements the existing `CatalogMarkdownDriftTests.cs` from Plan/13
S9.8). Tests assert against on-disk markdown structure and against
trace JSONL files; never against composer internal state.

- **`IndexTrainingTracePlan_EveryFamilyAppearsInTraceContractTable`** —
  every `^## ` family heading in this file appears in the
  §Training-trace contract table's left column. New families added
  to the catalog without a corresponding trace-contract row fail
  this test.
- **`IndexTrainingTracePlan_AdvisorsListedAreSubsetOfSpec20Surface`** —
  for every family, the listed advisors are a subset of
  `{rotation, threat, reward, objective, chat_template,
   activity_request, personality_cluster, cheapest-source-learner}`
  (where `cheapest-source-learner` is a §aota/05-defined alias for
  `objective` over source candidates). No advisor name typos.
- **`IndexTrainingTracePlan_EveryRowProducesOutcomeLineAcrossSample`** —
  scan the union of `tmp/test-runtime/traces/*/` from a
  representative-suite run; assert that for EVERY catalog row id (as
  enumerated by `IActivityCatalog`), at least one trace file contains
  a `kind="outcome"` line referencing that row's `activity_id`.
  Rows that never produce an outcome line in the representative
  suite indicate a LiveValidation coverage gap.
- **`Activities00Index_DynamicProgressive_NonPositiveRosterDeltaPerRowTest`** —
  for every catalog row id, the union of outcome lines referencing
  that row across `tmp/test-runtime/traces/*/` MUST contain at
  least one entry with `roster_distance_delta < 0` (strictly
  negative — proof that the row closes goal distance at least
  sometimes). Rows whose every outcome has `delta = 0` are
  flagged as **decoration** and must be justified in their family
  file per §Dynamic-progressive invariant.
