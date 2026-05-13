# Activities — Attunements

5 attunement catalog rows. Each gates a raid and is a one-time
character chain.

## Catalog rows

- `attune.mc` — Molten Core attunement (BRD chain).
- `attune.ony-horde` — Onyxia Horde chain (Drakefire Amulet, BRD, UBRS).
- `attune.ony-alliance` — Onyxia Alliance chain (similar).
- `attune.bwl` — BWL attunement (UBRS Vaelan chain).
- `attune.naxx` — Naxxramas attunement (Argent Dawn rep + tribute).

## Task family

| Task | Status |
|---|---|
| `AttunementChainTask` | not-started — orchestrator |
| Quest steps reuse `QuestChainTask` from `quests.md` |
| Item turn-ins reuse `TurnInQuestTask` |

> **Attunement is not a `TaskFamily` head.** Per R16, every catalog
> row's `TaskFamily` must be one of the fixed family-head strings in
> [`Spec/03_BOTRUNNER.md#catalog-of-task-families`](../../Spec/03_BOTRUNNER.md#catalog-of-task-families).
> Attunement chains are realized through three existing families:
> **Questing** (the quest-chain spine — `AcceptQuestTask`,
> `KillObjectiveTask`, `CollectObjectiveTask`, `EscortObjectiveTask`,
> `TurnInQuestTask`, `QuestChainTask`), **Dungeoneering** (the
> UBRS/BRD/Strat completion gates the chain requires), and **Economy**
> (Naxx-only: AH purchase / mat stockpile for Arcane Crystals, Nexus
> Crystals, Righteous Orb). The `AttunementChainTask` documented below
> is a per-chain orchestrator that composes those families; it claims
> `Questing` as its catalog `TaskFamily` for every row (since the
> prerequisite is always a quest chain) with secondary dependencies
> on `Dungeoneering` and (for `attune.naxx`) `Economy`.

## Task specifications

Each task below follows the Phase 0 precision contract: class
declaration, public surface, snapshot contract, BG protocol footprint,
FG memory footprint, test anchor, and catalog `TaskFamily` claim. Per
R19/R25, the public surface bullet records both the current shipped
surface (now: the `BotTask` async shim that forwards `TickAsync` →
`OnTick` → legacy `Update()` body — S1.0 shim landed 2026-05-12) and
the target surface (`TickAsync`/`OnPushedAsync`/`OnPoppedAsync`/
`OnChildFailedAsync` native override, landing under each family slot
S1.4..S1.13). Where no source exists yet, `**Planned anchor:**`
records the file path a Phase 1/Phase 2 worker should create.

### AttunementChainTask

- **Class declaration** — `public sealed class AttunementChainTask : BotTask, IBotTask`
  in namespace `BotRunner.Tasks.Quest`.
  **Planned anchor:** `Exports/BotRunner/Tasks/Quest/AttunementChainTask.cs`
  per slot `SA.1`. Status: `not-started`. No file exists in the tree
  today; `Glob "**/AttunementChainTask*.cs"` returns zero matches and
  `Grep "AttunementChain"` only hits this doc and
  `Tests/BotRunner.Tests/Progression/QuestChainRouterTests.cs` (a
  routing-router test that exercises generic chain dispatch, not
  attunement-specific logic). The closest existing scaffolding is
  `Exports/BotRunner/Progression/QuestChainData.cs`, which already
  declares three attunement-relevant chain rows (`OnyxiaAttunementHorde`,
  `MoltenCoreAttunement`, `BWLAttunement`) but no driver that walks them.
- **Public surface**
  - **Current shipped surface:** none — the file does not exist. The
    representative existing pattern is `Exports/BotRunner/Tasks/*` task
    classes that inherit `BotTask : IBotTask` and rely on the S1.0
    async shim (`TickAsync` → `OnTick` → legacy `Update()` body) plus
    the `PopTask(reason)` helper described in
    [`Spec/03_BOTRUNNER.md#ibottask-interface`](../../Spec/03_BOTRUNNER.md#ibottask-interface).
  - **Target surface (Phase 1, after S1.0):**
    `AttunementChainTask(IBotContext context, string attunementId)`
    where `attunementId` is one of `"attune.mc"`, `"attune.ony-horde"`,
    `"attune.ony-alliance"`, `"attune.bwl"`, `"attune.naxx"`.
    Standard `IBotTask` overrides (`Name`, `Status`, `IsComplete`,
    `IsFailed`, `TickAsync`, `OnPushedAsync`, `OnPoppedAsync`,
    `OnChildFailedAsync`). Loads the per-chain JSON from
    `Bot/attunements/<id>.json` on push; walks the chain step-by-step
    by pushing children: `TravelTask` to each NPC, `AcceptQuestTask`,
    `KillObjectiveTask` / `CollectObjectiveTask` / `EscortObjectiveTask`
    per step, `DungeoneeringTask` for the BRD/UBRS/Strat gates,
    `TurnInQuestTask` at each turn-in NPC. Completion: writes the
    catalog id to `CharacterRosterGoal.CompletedAttunements` (per
    [`Spec/05_PROGRESSION.md`](../../Spec/05_PROGRESSION.md):148) and
    pops with `Complete`.
- **Snapshot contract** — Reads (via `IObjectManager` /
  `BotTaskContext`): `Player.Level`, `Player.Faction`, `Player.MapId`,
  `Player.Position`, `Player.QuestLog` (`QuestsInProgress` /
  `QuestsCompleted`), `Player.Inventory` (transient quest items like
  Core Fragment, Blackhand's Command, Prison Cell Key,
  Blood of the Black Dragon Champion, Drakefire Amulet,
  Arcane/Nexus Crystals, Righteous Orb), `Player.Reputations`
  (Argent Dawn standing for `attune.naxx`), `Player.Spells` /
  `Player.Buffs` (Mark of Drakkisath for `attune.bwl`; Attunement to
  the Core for `attune.mc`), `Player.CopperOnHand` (`attune.naxx`
  60g/30g/0g gate). Writes (effects observable in
  `WoWActivitySnapshot`): no new top-level proto fields — chain
  progress is the existing `currentTaskName = AttunementChainTask`
  plus pushed-child mutations. Completion is observed as
  `CharacterRosterGoal.CompletedAttunements` gaining the catalog id
  (`AccountRoster.CompletedAttunements` per slot `SA.6`). Sub-tasks
  pushed bring the snapshot writes of those tasks (`AcceptQuestTask`
  mutates `QuestLog`, `TurnInQuestTask` mutates rewards selection,
  `DungeoneeringTask` mutates `currentMapId` and `movementData`).
- **BG protocol footprint** — Emits no opcodes itself. Composed
  opcodes from pushed children:
  - **Questing family:** `CMSG_QUESTGIVER_ACCEPT_QUEST = 0x123`,
    `CMSG_QUESTGIVER_COMPLETE_QUEST = 0x123` family,
    `CMSG_QUESTGIVER_CHOOSE_REWARD`, `SMSG_QUESTUPDATE_ADD_KILL`,
    `SMSG_QUESTUPDATE_ADD_ITEM`, `CMSG_QUESTLOG_REMOVE_QUEST` — all
    owned by the children listed in `quests.md`.
  - **Item interaction (Core Fragment, Drakkisath's Brand orb,
    Blackhand's Command right-click):** `CMSG_GAMEOBJ_USE` for world
    objects (Core Fragment, orb); `CMSG_USE_ITEM` for the inventory
    quest-item activation (Blackhand's Command). Both flow through
    `IObjectManager` / `WoWSharpObjectManager`; not emitted by this
    task directly.
  - **Dungeoneering family:** `MSG_RAID_TARGET_UPDATE`,
    `CMSG_SET_SELECTION`, `MSG_MOVE_*` per
    [`dungeons.md`](dungeons.md).
  - **Economy family (`attune.naxx` only):**
    `CMSG_AUCTION_BROWSE_QUERY` / `CMSG_AUCTION_PLACE_BID` for AH
    sourcing of attune mats per [`economy.md`](economy.md).
- **FG memory footprint** — `IObjectManager.Player` (Level, Faction,
  MapId, Position, QuestLog, Inventory, Reputations, Buffs, Money).
  `IObjectManager.NearbyObjects` / `NearbyUnits` to find the Core
  Fragment GameObject (BRD), Drakkisath's Brand orb (UBRS), Scarshield
  Quartermaster (BRS hallway), and quest-giver NPCs (Lothos Riftwaker,
  Warlord Goretooth, Eitrigg, Thrall, Rexxar, Helendis Riverhorn,
  Marshal Windsor, Squire Rowe, Haleh, Archmage Angela Dosantos).
  `IObjectManager.UseGameObject(guid)` for object-click steps
  (`Exports/GameData.Core/Interfaces/IObjectManager.cs`).
  `IObjectManager.UseItem(itemEntry)` for the Blackhand's Command
  inventory-activation step. No direct `LuaCall(...)` — chain steps
  flow through `IObjectManager` for FG/BG parity.
- **Test anchor** — **Planned anchor test:**
  `Tests/BotRunner.Tests/LiveValidation/Attunements/AttunementChainTaskTests.cs::AttunementChain_DispatchesPerChainSpec_CompletionFlagSet`
  (one `[Theory]` row per chain id, asserts
  `AccountRoster.CompletedAttunements` contains the catalog id after
  the chain completes). Filter:
  `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --filter "FullyQualifiedName~AttunementChainTaskTests"`.
  Status: `not-started`. The nearest existing coverage is
  `Tests/BotRunner.Tests/Progression/QuestChainRouterTests.cs` (router
  dispatch, not chain execution) and
  `Tests/RecordedTests.Shared.Tests/Scenarios/WestfallDeadminesAttunementScenario.cs`
  (a recorded-replay scenario unrelated to raid attunements — named
  for the Deadmines key chain, not MC/Ony/BWL/Naxx).
- **Catalog `TaskFamily` claim** — `Questing` (primary, since every
  attunement is gated by a quest chain). Secondary family
  dependencies: `Dungeoneering` (BRD / UBRS / LBRS / Strat
  completion), `Economy` (Naxx mat sourcing only). Per R16 a row may
  claim only one family-head string; the secondary dependencies are
  realized by pushed-child tasks. Cross-referenced rows from
  [`Plan/Activities/00_INDEX.md`](00_INDEX.md): `attune.mc`,
  `attune.ony-horde`, `attune.ony-alliance`, `attune.bwl`,
  `attune.naxx`.

## Per-attunement chain composition

Each chain row binds to a JSON file under `Bot/attunements/<id>.json`
(see existing template at
[`Config/activities/attune.mc.json`](../../../Config/activities/attune.mc.json)).
The chain spec below lists the in-order quest IDs / dungeon
completions / item turn-ins the `AttunementChainTask` walks. Quest IDs
and chain rows are sourced from
[`Exports/BotRunner/Progression/QuestChainData.cs`](../../../Exports/BotRunner/Progression/QuestChainData.cs)
where they exist, and from
[`docs/leveling-guide/attunements/`](../../leveling-guide/attunements/)
guides otherwise. Steps marked `[verify pass 3]` in the leveling guide
remain `**Planned anchor:**` until a future Phase 0 audit nails down
the quest IDs.

### attune.mc — Molten Core (BRD Core Fragment chain)

Source: [`docs/leveling-guide/attunements/molten-core.md`](../../leveling-guide/attunements/molten-core.md)
and `QuestChainData.MoltenCoreAttunement` (single-row chain at
`Exports/BotRunner/Progression/QuestChainData.cs:53-56`).

| # | Step | Family realization | Quest ID / item |
|---|---|---|---|
| 1 | Accept **Attunement to the Core** from **Lothos Riftwaker** (BRM lobby, MapId 0 (Eastern Kingdoms), -7462/-1089/265) at Level ≥ 55 | Questing → `AcceptQuestTask` | Quest **7848** |
| 2 | Enter Blackrock Depths, traverse to MC entrance chamber | Dungeoneering → `DungeoneeringTask` (`dungeon.blackrock-depths` row) | — |
| 3 | Click **Core Fragment** game object near the MC portal | Questing → `CollectObjectiveTask` (GameObject use) | Item: Core Fragment (BoP, transient) |
| 4 | Return to Lothos Riftwaker, turn in **Attunement to the Core** | Questing → `TurnInQuestTask` | Quest **7848** turn-in; reward: Attunement to the Core teleport spell |

Decision Engine reference rules (priority **820**) live in
[`leveling-guide/attunements/molten-core.md#decision-engine-rules`](../../leveling-guide/attunements/molten-core.md).
Existing config: [`Config/activities/attune.mc.json`](../../../Config/activities/attune.mc.json).
The "Drakkisath kill is required for MC" myth is explicitly contradicted
by the guide and **must not** be encoded into the JSON.

### attune.ony-horde — Onyxia Horde (Eitrigg/Rexxar chain)

Source: [`docs/leveling-guide/attunements/onyxia-horde.md`](../../leveling-guide/attunements/onyxia-horde.md)
and `QuestChainData.OnyxiaAttunementHorde` (7-row chain at
`Exports/BotRunner/Progression/QuestChainData.cs:42-51`). Note the
guide narrative documents 14 steps; the chain data captures the 7
load-bearing turn-in steps with verified IDs. Remaining mid-chain
steps are `[verify pass 3]`.

| # | Step | Family realization | Quest ID / item |
|---|---|---|---|
| 1 | **Warlord's Command** from **Warlord Goretooth** (Kargath, Badlands, MapId 0, -7518/-1224/286) — kill LBRS named bosses (Wyrmthalak, Omokk, Voone) | Questing → `AcceptQuestTask` + Dungeoneering → `DungeoneeringTask` (`dungeon.lower-blackrock-spire`) | Quest **4741** |
| 2 | **Eitrigg's Wisdom** — talk to **Eitrigg** (Orgrimmar, MapId 1, 1581/-4420/6) | Questing → `AcceptQuestTask` + `TurnInQuestTask` | Quest **4903** |
| 3 | **For The Horde!** — talk to **Thrall** (Orgrimmar, MapId 1, 1923/-4141/40); kill **Warchief Rend Blackhand** in UBRS | Questing + Dungeoneering → `DungeoneeringTask` (`dungeon.upper-blackrock-spire`) | Quest **4941** |
| 4 | **What the Wind Carries** — Thrall (same coords) | Questing → `AcceptQuestTask` + `TurnInQuestTask` | Quest **4974** |
| 5 | **The Champion of the Horde** — Thrall → find **Rexxar** wandering in Desolace | Questing → `EscortObjectiveTask` variant (Rexxar is a roving NPC; engine scans zone) | Quest **6566** |
| 6 | **Dragonkin Menace** — **Marshal Maxwell** (Burning Steppes, MapId 0, -7534/-1237/286) | Questing → `AcceptQuestTask` (kill objective) | Quest **6602** |
| 7 | **Blood of the Black Dragon Champion** — **Rokaro** (Burning Steppes, MapId 0, -7657/-1233/287); kill **General Drakkisath** in UBRS, retrieve Blood, return → receive **Drakefire Amulet** | Questing + Dungeoneering → `DungeoneeringTask` (`dungeon.upper-blackrock-spire`) | Quest **6568** |

**Bundle optimization:** a single UBRS run completes step 3 (Rend),
step 7 (Drakkisath → Onyxia), and `attune.mc`/`attune.bwl` Drakkisath
items if quests are accepted before zone-in. Encoded in
`Bot/attunements/ony-horde.json` (`SA.ony-horde` slot) as a
`bundleHints` array referencing peer chain ids.

### attune.ony-alliance — Onyxia Alliance (Marshal Windsor chain)

Source: [`docs/leveling-guide/attunements/onyxia-alliance.md`](../../leveling-guide/attunements/onyxia-alliance.md).
11-step chain; not yet in `QuestChainData`. All quest IDs and NPC
coords below are **Planned anchor** entries pending the Phase 0
chain-data audit that adds an `OnyxiaAttunementAlliance` row.

| # | Step | Family realization | Quest ID / item |
|---|---|---|---|
| 1 | **Dragonkin Menace** — **Helendis Riverhorn** (Burning Steppes, Morgan's Vigil) | Questing → `AcceptQuestTask` + kill objective | **Planned anchor** quest id |
| 2 | **The True Masters** (multi-step Stormwind Keep / Lakeshire / Burning Steppes tour) | Questing → `QuestChainTask` sub-chain | **Planned anchor** quest id (multiple) |
| 3 | **Marshal Windsor** — enter BRD, kill **High Interrogator Gerstahn** for **Prison Cell Key**, rescue Windsor | Dungeoneering → `DungeoneeringTask` (`dungeon.blackrock-depths`) | **Planned anchor** quest id |
| 4 | **Abandoned Hope** — return to Marshal Maxwell at Morgan's Vigil | Questing → `TurnInQuestTask` | **Planned anchor** quest id |
| 5 | **A Crumpled Up Note** — BRD trash drop | Questing → `CollectObjectiveTask` | **Planned anchor** item id |
| 6 | **A Shred of Hope** — kill **Golem Lord Argelmach** and **General Angerforge** in BRD | Dungeoneering → `DungeoneeringTask` (`dungeon.blackrock-depths`) | **Planned anchor** quest id |
| 7 | **Jail Break!** — escort Marshal Windsor out of BRD (5-man) | Questing → `EscortObjectiveTask` (slot SQ.2) | **Planned anchor** quest id |
| 8 | **Stormwind Rendezvous** — Marshal Maxwell → **Squire Rowe** (Stormwind gates) | Questing → `AcceptQuestTask` + travel | **Planned anchor** quest id |
| 9 | **The Great Masquerade** — escort Windsor through Stormwind to throne room (Bolvar tanks) | Questing → `EscortObjectiveTask` | **Planned anchor** quest id |
| 10 | **The Dragon's Eye** — Bolvar Fordragon (SW throne) → **Haleh** (Winterspring cave SW of Everlook) | Questing → `AcceptQuestTask` + cross-continent travel | **Planned anchor** quest id |
| 11 | **Drakefire Amulet** — Haleh → kill **General Drakkisath** in UBRS, retrieve **Blood of the Black Dragon Champion**, return → receive **Drakefire Amulet** | Questing + Dungeoneering → `DungeoneeringTask` (`dungeon.upper-blackrock-spire`) | **Planned anchor** quest id |

**Bundle optimization:** identical to `attune.ony-horde` — UBRS
Drakkisath kill completes `attune.ony-alliance` step 11,
`attune.mc` (via Drakkisath's Brand turn-in if MC quest line had used
Drakkisath, though MC actually uses BRD Core Fragment; confusion
clarified in `leveling-guide/attunements/molten-core.md`), and
`attune.bwl` (orb-touch). Encoded in
`Bot/attunements/ony-alliance.json` (`SA.ony-alliance` slot).

### attune.bwl — Blackwing Lair (Blackhand's Command, UBRS orb)

Source: [`docs/leveling-guide/attunements/blackwing-lair.md`](../../leveling-guide/attunements/blackwing-lair.md)
and `QuestChainData.BWLAttunement` (1-row chain at
`Exports/BotRunner/Progression/QuestChainData.cs:58-61`).

| # | Step | Family realization | Quest ID / item |
|---|---|---|---|
| 1 | Find + kill **Scarshield Quartermaster** in BRS upper hallway (outside UBRS instance) | Combat (engaged via push from chain) — `PullStrategyTask` / `PvERotationTask` | — |
| 2 | Loot **Blackhand's Command** item from Quartermaster's corpse | Questing → `CollectObjectiveTask` (loot) | Item: Blackhand's Command |
| 3 | Right-click **Blackhand's Command** in inventory → activates quest | Questing → `AcceptQuestTask` (item-triggered) | Quest **7761** (per `QuestChainData`; quest-giver NPC field "Scarshield Quartermaster" reflects the item's source) |
| 4 | Enter UBRS, clear to **General Drakkisath**, kill him | Dungeoneering → `DungeoneeringTask` (`dungeon.upper-blackrock-spire`) | — |
| 5 | Click **Drakkisath's Brand** orb behind Drakkisath's corpse | Questing → `CollectObjectiveTask` (GameObject use); quest auto-completes on orb touch (no NPC turn-in) | Buff applied: **Mark of Drakkisath** (persistent flag) |

**Bundle optimization:** same UBRS Drakkisath kill that resolves
`attune.bwl` step 4-5 also drops Blood of the Black Dragon Champion
for `attune.ony-*` step 11/7 if the Onyxia chain is at that step.
Encoded in `Bot/attunements/bwl.json` (`SA.bwl` slot).

### attune.naxx — Naxxramas (Argent Dawn rep + Light's Hope tribute)

Source: [`docs/leveling-guide/attunements/naxxramas.md`](../../leveling-guide/attunements/naxxramas.md).
Single-step quest with reputation + economy prerequisites; not in
`QuestChainData` today.

| # | Step | Family realization | Quest ID / item |
|---|---|---|---|
| 1 | Grind Argent Dawn rep to **Honored** (minimum) | Reputation grind via Questing (EPL quest hub) + Dungeoneering (`dungeon.stratholme-undead`, `dungeon.stratholme-live`, `dungeon.scholomance`) | Faction id **529** at standing ≥ Honored (per existing `Config/activities/attune.mc.json:22-23` template style) |
| 2 | Stockpile mats: **5× Arcane Crystal**, **2× Nexus Crystal**, **1× Righteous Orb**, **60g** (Honored tier; less if Revered/Exalted) | Economy → `AuctionHouseBuyTask` (`econ.ah-restock`) and/or Gathering/Crafting families | Items: Arcane Crystal, Nexus Crystal, Righteous Orb |
| 3 | Travel to **Light's Hope Chapel** (EPL, MapId 0, ~80/60), talk to **Archmage Angela Dosantos**, accept **The Dread Citadel - Naxxramas** | Questing → `AcceptQuestTask` | Quest **9122** |
| 4 | Hand over mats + gold; receive Naxxramas attunement | Questing → `TurnInQuestTask` (reward selection: the attune flag is automatic, no choice) | Quest **9122** turn-in |

`ServerCapabilities.Naxx40Implemented` gate (per
[`leveling-guide/attunements/naxxramas.md#vmangos--private-server-notes--critical-caveat`](../../leveling-guide/attunements/naxxramas.md))
is consulted by the parent OnDemand request, not by this task. Encoded
in `Bot/attunements/naxx.json` (`SA.naxx` slot).

## Cross-references to other family files

- **Questing** ([`quests.md`](quests.md)) — `AcceptQuestTask`,
  `KillObjectiveTask`, `CollectObjectiveTask`, `EscortObjectiveTask`,
  `TurnInQuestTask`, `QuestChainTask` (slot `SQ.3`). The
  `AttunementChainTask` is functionally a specialization of
  `QuestChainTask` that knows about cross-instance dungeon gates and
  bundle optimizations; until S1.0 lands the new IBotTask contract,
  the two share a base implementation of "walk a sequence of
  `QuestStep` rows".
- **Dungeoneering** ([`dungeons.md`](dungeons.md)) —
  `DungeoneeringTask`, `PullStrategyTask`, `BossEncounterTask`,
  `LootCorpseTask`. Required for `attune.mc` (BRD),
  `attune.ony-horde` (LBRS + UBRS), `attune.ony-alliance` (BRD +
  UBRS), `attune.bwl` (UBRS), `attune.naxx` (Strat UD + Strat Live +
  Scholo for AD rep).
- **Economy** ([`economy.md`](economy.md)) — `AuctionHouseBuyTask`,
  `AuctionHousePostTask`, `BankWithdrawTask`. Required for
  `attune.naxx` mat stockpile (Arcane/Nexus Crystals, Righteous Orb)
  and the 60g tribute.

## Slots

### SA.1 — `AttunementChainTask`

- **Owner:** `monorepo-worker`
- **Status:** open
- **Owned paths:**
  - `Exports/BotRunner/Tasks/Quest/AttunementChainTask.cs`

### SA.mc — MC attunement

- **Owner:** `monorepo-worker`
- **Status:** open
- **Owned paths:** `Bot/attunements/mc.json`
- **Goal:** Drive a 60 bot through the BRD chain to MC entry.

### SA.ony-horde — Onyxia Horde

- **Owner:** `monorepo-worker`
- **Status:** open
- **Owned paths:** `Bot/attunements/ony-horde.json`

### SA.ony-alliance — Onyxia Alliance

- **Owner:** `monorepo-worker`
- **Status:** open
- **Owned paths:** `Bot/attunements/ony-alliance.json`

### SA.bwl — BWL

- **Owner:** `monorepo-worker`
- **Status:** open
- **Owned paths:** `Bot/attunements/bwl.json`

### SA.naxx — Naxxramas

- **Owner:** `monorepo-worker`
- **Status:** open
- **Owned paths:** `Bot/attunements/naxx.json`

### SA.6 — Account-roster attunement tracking

- **Owner:** `monorepo-worker`
- **Status:** open
- **Depends on:** S4.1
- **Goal:** `AccountRoster.CompletedAttunements` updates per
  attunement. Tests assert legality validator respects this.

## Failure recovery

- **Quest item drop missed** → re-run encounter (e.g. Vael drops
  the BWL attunement item).
- **Class quest gating** (Onyxia chain has class-quest hooks for
  some classes) → handled in per-chain JSON.
