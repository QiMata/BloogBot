# Activities — Crafting Professions

Primary crafting: Alchemy, Blacksmithing, Engineering, Enchanting,
Leatherworking, Tailoring. Secondary: Cooking, First Aid.

## Task families

| Task | Status |
|---|---|
| `CraftRecipeTask` | partial — FG (Lua CastSpellByName) works; BG needs `CraftAgent` packet path |
| `MaterialSourcingTask` | not-started — buys/farms missing mats |
| `LearnRecipeTask` | not-started — trainer + recipe-purchase + recipe-drop |
| `TrainerVisitTask` | partial — visit + train action exists |
| `RecipeChooserTask` | not-started — picks the highest-XP recipe affordable from current mats |

## Slots

### SC.1 — BG packet path for craft

- **Owner:** `monorepo-worker`
- **Status:** open (also tracked as S2.12)
- **Owned paths:**
  - `Exports/WoWSharpClient/Agents/CraftAgent.cs`
  - tests
- **Goal:** `CraftRecipeTask` on BG sends the correct
  `CMSG_CRAFT_ITEM`/`CMSG_CAST_SPELL` packet sequence; FG path still
  works via Lua.

### SC.2 — Profession leveling activity

- **Owner:** `monorepo-worker`
- **Status:** open
- **Catalog:** `prof.city-trainer-loop`
- **Goal:** Cycle: train → check mats → source mats (vendor or AH) →
  craft until skill cap → train. Per-profession recipe progression
  table.

### SC.3 — Specialization decisions

- **Owner:** `monorepo-worker`
- **Status:** open
- **Goal:** At Engineering 200, Blacksmithing 200, Leatherworking 225 the
  bot's `CharacterRosterGoal` specifies the spec (Goblin/Gnomish,
  Armorsmith/Weaponsmith, Dragonscale/Elemental/Tribal). Per
  `leveling-guide/sections/04-l30-l40.md`.

### SC.4 — Per-profession LiveValidation

- **Owner:** `monorepo-test-runner`
- **Status:** open
- **Goal:** 8 tests — one per primary + 2 secondary professions.
  Drives a bot from 1 → 75 skill on natural recipes.

## Failure recovery

- **Mats not in inventory** → push `MaterialSourcingTask`.
- **Mats not available on AH** → fall back to gather route or
  trade chat.
- **Recipe not learned** → push `LearnRecipeTask`.

## Task specifications

The four canonical Crafting tasks listed in
`docs/Spec/03_BOTRUNNER.md#catalog-of-task-families`:
`CraftRecipeTask`, `MaterialSourcingTask`, `LearnRecipeTask`,
`TrainerVisitTask`. Per S0.8 each task gets the eight-bullet detail
block: class declaration, current shipped surface, target surface
(per R19 / `Spec/03_BOTRUNNER.md#target-contract-phase-1-target--to-be-implemented-under-slot-s10`),
snapshot reads/writes, BG opcodes, FG calls, test anchor, catalog claim.

### CraftRecipeTask

1. **Class declaration** — **Planned anchor:**
   `Exports/BotRunner/Tasks/Crafting/CraftRecipeTask.cs`, namespace
   `BotRunner.Tasks.Crafting`. Status: `not-started` (no
   `CraftRecipeTask.cs` exists today). The closest shipped surrogate is
   `class BatchCraftTask : BotTask, IBotTask` in namespace
   `BotRunner.Tasks.Crafting`, file
   `Exports/BotRunner/Tasks/Crafting/BatchCraftTask.cs`. The Phase 1
   worker that lands S1.16 (`Plan/02_PHASE1_ACTION_TASK_FOUNDATION.md`)
   should either rename `BatchCraftTask` → `CraftRecipeTask` or wrap it,
   plus add the recipe-resolution surface (skill check, reagent check,
   trade-window open) that `BatchCraftTask` skips today.

2. **Public surface — current shipped** (via `BatchCraftTask`)
   - Constructor: `BatchCraftTask(IBotContext context, int spellId, int targetCount)`.
   - Inherits the `BotTask` async shim (`TickAsync` → `OnTick` → legacy `Update()`); body lives in `Update()`. Per-family async refactor in S1.10 Crafting (S1.0/R25, shim-only).
   - Public method: `void OnCastFailed()` — called when
     `SMSG_CAST_FAILED` is received for the active craft spell; bumps
     `_failedCount` and bails after three consecutive failures (out of
     reagents). No wire-up to a `SMSG_CAST_FAILED` observable exists in
     the task today; the comment in `BatchCraftTask.cs:69` documents the
     intent.
   - Internal contract: `CastWaitTicks = 50` (~2.5s post-cast wait),
     completes after `targetCount` casts or three consecutive failures.
   - Helper data: `BotRunner.Combat.CraftingData` (skill IDs 129/185,
     `Recipe` records for First Aid and Cooking with `SpellId`,
     `Materials`, `RequiredSkill/YellowSkill/GreenSkill/GreySkill`,
     `FindBestRecipeForSkillUp`, `MaxCraftCount`). The `CraftRecipeTask`
     constructor must consume a `Recipe` from this table (or the future
     primary-profession equivalent) rather than a raw `spellId`.

3. **Public surface — target (Phase 1, per R19)**
   - `TickAsync(BotTaskContext, CancellationToken)` —
     reagent-presence guard → (FG only) open trade-skill / craft frame
     via `CastSpellByName` of the profession spell → cast recipe
     spell → wait for `SMSG_SPELL_GO` or `SMSG_CAST_FAILED` → repeat
     until target count or reagent exhaustion.
   - `OnPushedAsync` — resolve `Recipe`; if mats missing, push
     `MaterialSourcingTask`; if recipe not yet known, push
     `LearnRecipeTask`; if profession skill cap reached, push
     `TrainerVisitTask` (profession variant).
   - `OnPoppedAsync` — emit
     `craftingStatus.recipeId/craftedCount/failedCount` into the
     activity snapshot; close trade-skill window (FG `CloseCraft()`).
   - `OnChildFailedAsync` — bubble `FailureReason.MissingReagents`
     and `FailureReason.SkillTooLow` to the parent; do not retry
     internally.

4. **Snapshot contract**
   - Reads: `Player.Coinage` (for vendor reagent purchase delta),
     `Player.IsCasting` (cast in flight — gates the next cast),
     `Player.PrimaryProfessionSkill[ProfessionType]` (skill bracket
     selection via `CraftingData.FindBestRecipeForSkillUp`), inventory
     reagent counts (item-id → count map; reads through
     `IObjectManager` on FG, through
     `WoWSharpClient.InventoryManager` on BG).
   - Writes / observable effects: `inventory` deltas (reagents
     consumed, product item added), `currentSpellCast` (during cast),
     `recentChatMessages` (`SMSG_CAST_FAILED` reason strings).
   - **Planned snapshot field:** `craftingStatus { recipeId,
     craftedCount, failedCount, lastFailureReason }` — does not exist
     today; must be added under S1.16 and wired into the proto
     contract (`communication.proto`).

5. **BG protocol footprint**
   - `Opcode.CMSG_CAST_SPELL` — 6-byte vanilla 1.12.1 payload
     `spellId(4) + targetFlags(2)`, sent for the recipe spell itself.
     Encoded by
     `ProfessionsNetworkClientComponent.SendCastSpellAsync`
     (`Exports/WoWSharpClient/Networking/ClientComponents/ProfessionsNetworkClientComponent.cs:481`).
     `CraftItemAsync(recipeId, quantity)`
     (line 275) loops that send with a 500 ms gap. The current
     `BatchCraftTask.Update()` calls `ObjectManager.CastSpell(_spellId)`,
     which on BG ultimately reaches the same opcode via
     `SpellcastingManager` /
     `WoWSharpObjectManager.CastSpell`.
   - `Opcode.SMSG_SPELL_GO` — incoming, signals cast started/finished
     (`ProfessionsNetworkClientComponent.ItemCrafted` observable wires
     here, but `ParseCraftResult` returns zeros today — see
     `ProfessionsNetworkClientComponent.cs:558`).
   - `Opcode.SMSG_CAST_FAILED` — incoming, drives
     `OnCastFailed()` and the reagent-exhaustion bail. Not currently
     subscribed; S1.16 must hook
     `SpellCastingNetworkClientComponent` (or an equivalent
     `CraftAgent` once created) to forward failure reasons.
   - `Opcode.SMSG_ITEM_PUSH_RESULT` — incoming, authoritative signal
     that the crafted item entered inventory; preferred over
     `SMSG_SPELL_GO` per the comment at
     `ProfessionsNetworkClientComponent.cs:562`.
   - **Not needed:** `CMSG_CRAFT_ITEM` does not exist in MaNGOS
     1.12.1; crafting is a normal `CMSG_CAST_SPELL` of the recipe's
     teaching spell, with the trade-skill window auto-opened
     server-side when the profession's "open window" spell is cast.

6. **FG memory footprint**
   - `IObjectManager.CastSpell(uint spellId, int rank = -1, bool castOnSelf = false)`
     — FG implementation routes through Lua via the string overload;
     the int overload at
     `Services/ForegroundBotRunner/Statics/ObjectManager.Combat.cs:166`
     warns "no direct spell-ID cast available; callers should prefer
     string overload". `CraftRecipeTask` on FG must therefore
     translate the recipe row's `SpellId` to a name and use
     `CastSpell(spellName)` (Lua `CastSpellByName`) rather than the
     uint variant. The string call ultimately fires
     `MainThreadLuaCall("CastSpellByName(...)")`.
   - `ICraftFrame.IsOpen / HasMaterialsNeeded(slot) / Craft(slot)`
     — `Services/ForegroundBotRunner/Frames/FgCraftFrame.cs` issues
     Lua `CraftFrame:IsVisible()`, `GetCraftNumReagents`,
     `GetCraftReagentInfo`, and `DoCraft(craftIndex)`. This is the
     `enchanting` / `engineering` CraftFrame path; primary
     trade-skills (`TradeSkillFrame` / `DoTradeSkill`) do not have a
     shipped FG frame yet — flagged as the second FG gap.
   - `IObjectManager.Player.PrimaryProfessionSkill` reads for skill
     bracket selection.
   - No `MainThreadLuaCall("DoTradeSkill(...)")` reference exists in
     `Services/ForegroundBotRunner/` today; that Lua bridge is part
     of the S1.16 surface.

7. **Test anchor** —
   `Tests/BotRunner.Tests/LiveValidation/CraftingProfessionTests.cs::CraftingProfessionTests.FirstAid_LearnAndCraft_ProducesLinenBandage`
   (Shodan-directed First Aid skill stage + reagent stage; BG
   dispatches `ActionType.CastSpell` for `Linen Bandage`, recipe spell
   `3275`, and asserts `Linen Cloth` 1 → `Linen Bandage` 1 within
   8 s). Filter:
   `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --filter "FullyQualifiedName~CraftingProfessionTests.FirstAid_LearnAndCraft_ProducesLinenBandage"`.
   Companion design notes:
   `Tests/BotRunner.Tests/LiveValidation/docs/CraftingProfessionTests.md`.

8. **Catalog `TaskFamily` claim** — `Crafting`. Drives
   `prof.city-trainer-loop` in [`00_INDEX.md`](00_INDEX.md). Also a
   dependency for any consumable-producing row that other families
   schedule (raid flasks for `raid.*`, food/water for `quest.zone.*`
   downtime, repair-bot/jumper-cables produced for `econ.vendor-loop`,
   bandages for `combat.md` recovery branches).

### MaterialSourcingTask

1. **Class declaration** — **Planned anchor:**
   `Exports/BotRunner/Tasks/Crafting/MaterialSourcingTask.cs`,
   namespace `BotRunner.Tasks.Crafting`. Status: `not-started`. No
   `MaterialSourcingTask.cs` exists today. The shipped fallbacks live
   in adjacent task families:
   - vendor reagent purchase: `VendorBuyTask` (Economy) at
     `Exports/BotRunner/Tasks/...` (planned in `economy.md`),
   - auction-house mat purchase: `AuctionHouseBuyTask` (Economy),
   - gather route fallback: `GatheringRouteTask` /
     `GatherNodeTask` (`Exports/BotRunner/Tasks/Gathering/`).
   The S1.16 (or a follow-up Crafting slot) worker creates
   `MaterialSourcingTask` as an orchestrator that selects between
   those three sub-tasks per reagent.

2. **Public surface — current shipped** — N/A (task does not exist).
   The reagent-shortfall computation that the task will rely on is
   already in `BotRunner.Combat.CraftingData.MaxCraftCount(Recipe,
   Dictionary<uint,int> inventory)`
   (`Exports/BotRunner/Combat/CraftingData.cs:128`) and the
   needed-vs-have diff is `Recipe.Materials[i].Count -
   inventory[itemId]`.

3. **Public surface — target (Phase 1, per R19)**
   - Constructor: `MaterialSourcingTask(IBotContext context,
     IReadOnlyList<(uint ItemId, int Count)> required,
     SourcingStrategy strategy = SourcingStrategy.Auto)`.
   - `TickAsync` — state machine: `Plan → Vendor → Mail → AH →
     Gather → Done|Failed`. Auto-selects: vendor first if any reagent
     is `vendor_sold` (item template flag), AH next if listings exist
     within budget, gather route last (only for raw mats).
   - `OnPushedAsync` — query `inventory` for current counts; emit
     a planned `materialPlan` snapshot field describing the chosen
     leg.
   - `OnPoppedAsync` — emit `materialOutcome.acquired[itemId]` map
     so the parent (`CraftRecipeTask`) re-checks reagent presence
     before resuming.
   - `OnChildFailedAsync` — escalate to parent with
     `FailureReason.MatsUnavailable` when all three legs exhaust.

4. **Snapshot contract**
   - Reads: `inventory` (reagent counts), `Player.Coinage` (budget
     gate), `Player.MapId/Position` (locality for choosing the
     nearest vendor/AH/gather node).
   - Writes / observable effects (planned): `materialPlan {
     strategy, items[], expectedBudget }`, `materialOutcome {
     acquired, missing, failureReason }`. Today: relies on the
     post-`VendorBuyTask` / `GatherNodeTask` snapshot deltas
     emitted by those tasks (no dedicated crafting snapshot field).

5. **BG protocol footprint** — All opcodes are delegated to the
   pushed Economy / Gathering child:
   - via `VendorBuyTask`: `Opcode.CMSG_LIST_INVENTORY` (vendor open),
     `Opcode.CMSG_BUY_ITEM_IN_SLOT` (purchase). See `economy.md` for
     the canonical block.
   - via `AuctionHouseBuyTask`: `Opcode.CMSG_AUCTION_LIST_ITEMS`,
     `Opcode.CMSG_AUCTION_PLACE_BID`. See `economy.md`.
   - via `GatherNodeTask`: `Opcode.CMSG_GAMEOBJ_USE` (node tap), the
     same opcode `ProfessionsNetworkClientComponent.GatherResourceAsync`
     sends (`ProfessionsNetworkClientComponent.cs:341`).
   - `MaterialSourcingTask` itself emits no opcodes directly.

6. **FG memory footprint** — Same delegation pattern; the
   orchestrator reads `IObjectManager.GetInventoryItemCount(itemId)`
   /equivalent inventory accessor to compute the shortfall, then
   pushes the appropriate child. No direct `LuaCall(...)`
   invocations.

7. **Test anchor** — **Planned anchor:**
   `Tests/BotRunner.Tests/LiveValidation/CraftingProfessionTests.cs::CraftingProfessionTests.FirstAid_AcquireLinenFromVendor_ThenCraft`
   (extension of the shipped `FirstAid_LearnAndCraft_ProducesLinenBandage`
   that strips reagents first and asserts `MaterialSourcingTask`
   pushes `VendorBuyTask` and re-enters `CraftRecipeTask`).
   Status: `not-started`. Filter (once added):
   `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --filter "FullyQualifiedName~CraftingProfessionTests.FirstAid_AcquireLinenFromVendor_ThenCraft"`.

8. **Catalog `TaskFamily` claim** — `Crafting` (orchestrator).
   Drives `prof.city-trainer-loop`. Cross-family dependency: invoked
   by `BatchCraftTask`/`CraftRecipeTask` whenever
   `CraftingData.MaxCraftCount` returns 0 mid-cycle.

### LearnRecipeTask

1. **Class declaration** — **Planned anchor:**
   `Exports/BotRunner/Tasks/Crafting/LearnRecipeTask.cs`, namespace
   `BotRunner.Tasks.Crafting`. Status: `not-started`. No
   `LearnRecipeTask.cs` exists today. The closest shipped surface is
   `TrainerVisitTask` (class-trainer flavor; learns class spells, not
   recipes) and the Lua-buy loop in
   `Services/ForegroundBotRunner/Statics/ObjectManager.Interaction.cs:743-753`
   which buys all `'available'` services from `GetTrainerServiceInfo`
   — that path already covers profession-trainer recipe purchases
   when the player is at a profession trainer, but no task wraps it.

2. **Public surface — current shipped** — N/A (task does not exist).
   Trainer-side recipe purchase is wired through
   `WoWSharpObjectManager.LearnAllAvailableSpellsAsync(ulong
   trainerGuid, CancellationToken)`
   (`Exports/WoWSharpClient/WoWSharpObjectManager.Network.cs:558`),
   which `TrainerVisitTask` calls today and which the profession
   variant of `LearnRecipeTask` should also call (filtered to recipe
   spells the table marks as profession-related).

3. **Public surface — target (Phase 1, per R19)**
   - Constructor: `LearnRecipeTask(IBotContext context, uint
     recipeSpellId, RecipeSource source)` where `RecipeSource ∈ {
     Trainer, Vendor, Drop, Quest }`.
   - `TickAsync` — state machine:
     - `Trainer` → push `TrainerVisitTask` (profession variant) →
       confirm `recipeSpellId` is in `Player.KnownSpells`;
     - `Vendor` → push `VendorBuyTask` for the recipe item, then
       `UseItem(recipeItemId)`;
     - `Drop` → push `GatheringRouteTask` or `KillObjectiveTask` for
       the drop source;
     - `Quest` → push `AcceptQuestTask` / `TurnInQuestTask`.
   - `OnPushedAsync` — short-circuit if `recipeSpellId` is already
     known.
   - `OnPoppedAsync` — emit `learnedRecipes.append(recipeSpellId)`
     in the activity snapshot.
   - `OnChildFailedAsync` — escalate
     `FailureReason.RecipeUnobtainable` to parent (no fallback
     source).

4. **Snapshot contract**
   - Reads: `Player.KnownSpells` (recipe spell ID present?),
     `Player.Position/MapId` (locality for trainer/vendor/drop),
     `Player.Coinage` (recipe purchase cost gate).
   - Writes / observable effects (planned): `knownRecipes` delta;
     `recipeSourcePlan { spellId, source, location }`. Today: no
     dedicated snapshot field — the spell-list growth surfaces
     through standard `Player.KnownSpells` deltas.

5. **BG protocol footprint** — Delegated to the pushed child task:
   - Trainer recipes: `Opcode.CMSG_GOSSIP_HELLO` + `Opcode.CMSG_TRAINER_LIST`
     + `Opcode.CMSG_TRAINER_BUY_SPELL` via
     `ProfessionsNetworkClientComponent.OpenProfessionTrainerAsync`/`RequestProfessionServicesAsync`/`LearnProfessionSkillAsync`
     (`Exports/WoWSharpClient/Networking/ClientComponents/ProfessionsNetworkClientComponent.cs:143/167/187`)
     and incoming `Opcode.SMSG_TRAINER_LIST` /
     `Opcode.SMSG_TRAINER_BUY_SUCCEEDED` /
     `Opcode.SMSG_TRAINER_BUY_FAILED` parsed by
     `TrainerNetworkClientComponent.ParseTrainerList` /
     `ParseTrainerBuySucceeded` / `ParseTrainerBuyFailed`
     (`TrainerNetworkClientComponent.cs:132/189/197`).
   - Vendor recipes: `Opcode.CMSG_LIST_INVENTORY`,
     `Opcode.CMSG_BUY_ITEM_IN_SLOT`, then `Opcode.CMSG_USE_ITEM` for
     the recipe scroll.
   - Drop / quest recipes: opcodes owned by the pushed
     `KillObjectiveTask` / `AcceptQuestTask` (see `quests.md`).
   - `LearnRecipeTask` itself emits no opcodes directly.

6. **FG memory footprint**
   - `IObjectManager.LearnAllAvailableSpellsAsync(trainerGuid, ct)`
     — the same path `TrainerVisitTask` uses.
   - FG Lua via `Functions.LuaCall("BuyTrainerService(i)")` inside
     `ObjectManager.Interaction.cs:749` already buys every
     `'available'` service; `LearnRecipeTask` (profession trainer
     variant) reuses that loop after filtering for recipe-bearing
     services.
   - `IObjectManager.UseItem(itemId)` — for vendor-recipe scrolls
     (FG uses `Lua` `UseItemByName`).
   - `IObjectManager.Player.KnownSpells` for the post-condition
     check.

7. **Test anchor** — **Planned anchor:**
   `Tests/BotRunner.Tests/LiveValidation/Crafting/LearnRecipeTests.cs::LearnRecipeFromProfessionTrainer_AddsRecipeToKnownSpells`.
   Status: `not-started`. Filter (once added):
   `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --filter "FullyQualifiedName~LearnRecipeTests.LearnRecipeFromProfessionTrainer_AddsRecipeToKnownSpells"`.

8. **Catalog `TaskFamily` claim** — `Crafting`. Drives
   `prof.city-trainer-loop` (recipe acquisition leg). Cross-family
   dependency: `Loadout` (`Spec/03_BOTRUNNER.md#loadout`) for the
   `LearnSpellsTask` flavor that pre-stages profession trainer
   visits.

### TrainerVisitTask

1. **Class declaration** — `class TrainerVisitTask : BotTask,
   IBotTask` in namespace `BotRunner.Tasks`. File:
   `Exports/BotRunner/Tasks/TrainerVisitTask.cs`. Status: `partial`
   — class-trainer flavor exists; the profession-trainer flavor
   (recipe purchasing, profession-skill tier upgrades) reuses the
   same class but with a different NPC filter (see `LearnRecipeTask`
   target above and `ProfessionTrainerScheduler.HordeTrainers` /
   `AllianceTrainers` for the trainer NPC entry table at
   `Exports/BotRunner/Tasks/Crafting/ProfessionTrainerScheduler.cs:26-57`).

2. **Public surface — current shipped**
   - Constructor: `TrainerVisitTask(IBotContext botContext)`.
   - Inherits the `BotTask` async shim (`TickAsync` → `OnTick` → legacy `Update()`); body lives in `Update()`. Per-family async refactor in S1.10 Crafting (S1.0/R25, shim-only).
   - Public static helpers used by tests:
     `static bool IsClassTrainerMatch(string npcName, Class playerClass)`
     (`TrainerVisitTask.cs:107`),
     `static bool IsWrongTrainer(string npcName, Class playerClass)`
     (`TrainerVisitTask.cs:118`).
   - Private state machine: `enum TrainerState { FindTrainer,
     MoveToTrainer, LearnSpells, Done }`; private helpers
     `FindTrainer(player)`, `MoveToTrainer(player)`,
     `LearnSpells()`, `SetState`, `Pop`.
   - Static tables: `ClassKeywords[]` (line 154),
     `ProfessionKeywords[]` (line 167) — the latter is used today
     to *exclude* profession trainers from class-trainer matching;
     the profession variant will invert that filter.
   - Inherited from `BotTask`: `BotTasks`, `ObjectManager`,
     `Logger`, `Config.NpcInteractRange`, `Config.StuckTimeoutMs`,
     `NavigateToward(pos)`, `Wait.For(key, ms, fresh)`.

3. **Public surface — target (Phase 1, per R19)**
   - `TickAsync(BotTaskContext, CancellationToken)` — replaces the
     synchronous `Update()`; same state machine.
   - `OnPushedAsync` — record start position, drain
     `Wait.Remove("trainer_*")`.
   - `OnPoppedAsync` — emit `trainerVisit { trainerGuid,
     learnedCount, errorReason }` snapshot field.
   - `OnChildFailedAsync` — the task pushes no children today; in
     Phase 1 the move-to-trainer leg may push a generic
     `GoToTask` and would surface
     `FailureReason.PathfindingFailed` upward.

4. **Snapshot contract**
   - Reads (via `ObjectManager`):
     `Player.Position`, `(Player as IWoWPlayer).Class`,
     `ObjectManager.Units` (filtered by `NPCFlags.UNIT_NPC_FLAG_TRAINER`),
     `ObjectManager.Aggressors` (combat abort).
   - Writes / observable effects: `movementData` (during the
     move-to-trainer leg via `NavigateToward`),
     `Player.KnownSpells` (post-learn via
     `ObjectManager.RefreshSpells()`, line 217), `recentChatMessages`
     (any trainer-side `SYSTEM_MESSAGE` toasts).
   - **Planned snapshot field:** `trainerVisit { trainerGuid,
     learnedCount, lastError }`.

5. **BG protocol footprint** — Emitted indirectly through
   `ObjectManager.LearnAllAvailableSpellsAsync(trainerGuid, ct)` at
   `TrainerVisitTask.cs:211`. That method (in
   `Exports/WoWSharpClient/WoWSharpObjectManager.Network.cs:558`)
   calls into the agent factory and produces, in order:
   - `Opcode.CMSG_GOSSIP_HELLO` — via
     `GossipAgent.GreetNpcAsync(trainerGuid)`; also encoded by
     `TrainerNetworkClientComponent.OpenTrainerAsync` at
     `TrainerNetworkClientComponent.cs:237` and by
     `ProfessionsNetworkClientComponent.OpenProfessionTrainerAsync`
     at `ProfessionsNetworkClientComponent.cs:143`.
   - `Opcode.CMSG_TRAINER_LIST` — via
     `TrainerNetworkClientComponent.RequestTrainerServicesAsync` at
     `TrainerNetworkClientComponent.cs:303` (8-byte payload,
     trainer GUID).
   - `Opcode.CMSG_TRAINER_BUY_SPELL` — via
     `TrainerNetworkClientComponent.LearnSpellAsync(trainerGuid,
     spellId)` at `TrainerNetworkClientComponent.cs:330/366`
     (12-byte payload: GUID + spellId).
   - Incoming: `Opcode.SMSG_TRAINER_LIST` (parsed at
     `TrainerNetworkClientComponent.cs:132`),
     `Opcode.SMSG_TRAINER_BUY_SUCCEEDED` (parsed at line 189),
     `Opcode.SMSG_TRAINER_BUY_FAILED` (parsed at line 197),
     `Opcode.SMSG_GOSSIP_COMPLETE` (close, line 71).
   - Movement opcodes (`MSG_MOVE_*`) — via
     `NavigateToward(_trainerUnit.Position)` at
     `TrainerVisitTask.cs:191`.
   - `Opcode.CMSG_SET_SELECTION` — via
     `ObjectManager.SetTarget(_trainerGuid)` at
     `TrainerVisitTask.cs:209`.

6. **FG memory footprint**
   - `IObjectManager.Player`, `IObjectManager.Units`,
     `IObjectManager.Aggressors`.
   - `IObjectManager.StopAllMovement()`.
   - `IObjectManager.SetTarget(ulong guid)`.
   - `IObjectManager.LearnAllAvailableSpellsAsync(ulong, CancellationToken)`
     — the FG implementation drives gossip + trainer Lua. The shipped
     Lua is the buy-all-available loop in
     `Services/ForegroundBotRunner/Statics/ObjectManager.Interaction.cs:743-753`:
     `local n = GetNumTrainerServices(); for i = 1,n do local _,_,avail
     = GetTrainerServiceInfo(i); if avail == 'available' then
     BuyTrainerService(i); ... end; end`, then
     `CloseTrainer()`
     (`ObjectManager.Interaction.cs:764`).
   - `IObjectManager.RefreshSpells()` (line 217 of
     `TrainerVisitTask.cs`).
   - No direct `LuaCall(...)` in `TrainerVisitTask.cs`; all Lua
     side effects flow through `IObjectManager` so the FG and BG
     call shapes match. The `FgTrainerFrame.LearnSpellByIndex(...)`
     Lua at `Services/ForegroundBotRunner/Frames/FgTrainerFrame.cs:43`
     (`BuyTrainerService(trainerIndex)`) is the per-spell granular
     variant that the profession flavor of `LearnRecipeTask` would
     use to buy a specific recipe rather than the full sweep.

7. **Test anchor** —
   - Unit:
     `Tests/BotRunner.Tests/Combat/TrainerVisitTaskTests.cs::TrainerVisitTaskIsClassTrainerMatchTests.*`
     (and `*WrongTrainerTests.*`) covers the NPC-name matcher.
     Filter:
     `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --filter "FullyQualifiedName~TrainerVisitTaskIsClassTrainerMatchTests"`.
   - Agent:
     `Tests/WoWSharpClient.Tests/Agent/TrainerNetworkAgentTests.cs`
     and `ProfessionsNetworkAgentTests.cs` cover the underlying
     BG packet path.
     Filter:
     `dotnet test Tests/WoWSharpClient.Tests/WoWSharpClient.Tests.csproj --filter "FullyQualifiedName~TrainerNetworkAgentTests"`.
   - LiveValidation: **Planned anchor:**
     `Tests/BotRunner.Tests/LiveValidation/Crafting/TrainerVisitTests.cs::TrainerVisit_ClassTrainer_LearnsAvailableSpells`.
     Status: `not-started`.

8. **Catalog `TaskFamily` claim** — `Crafting` for the
   profession-trainer flavor (drives `prof.city-trainer-loop`).
   `Equipment` / `Loadout` for the class-trainer flavor today
   (consumed by `LoadoutTask.LearnAllAvailableSpellsAsync` per
   `Spec/03_BOTRUNNER.md#loadout`). Implicit dependency for every
   row whose `LevelRange` crosses a profession-skill tier boundary
   (75 / 150 / 225 / 300 per
   `ProfessionTrainerScheduler.Tiers` at
   `Exports/BotRunner/Tasks/Crafting/ProfessionTrainerScheduler.cs:16-21`).
