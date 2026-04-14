# StateManager Config Schema — Task & Decision Control

## Overview

StateManager configs (`.config.json` files) define which bots to run and **what they're allowed to do**. By restricting tasks and decisions, we can:
- Test individual IBotTask implementations in isolation
- Validate specific decision engine pathways
- Run fully autonomous bots with all features enabled
- Control what coordinators are active (dungeon, battleground, combat)

## Config Location

- **Source:** `Services/WoWStateManager/Settings/Configs/*.config.json`
- **Build output:** `Bot/Debug/net8.0/Settings/Configs/`
- **Active config:** `Settings/StateManagerSettings.json` (what StateManager loads on startup)
- **Override:** `WWOW_SETTINGS_OVERRIDE` env var points to any `.config.json`

## Current Schema (CharacterSettings)

```jsonc
{
  "AccountName": "TESTBOT1",           // Account name (created via SOAP if missing)
  "CharacterClass": "Warrior",         // Warrior|Paladin|Hunter|Rogue|Priest|Shaman|Mage|Warlock|Druid
  "CharacterRace": "Orc",             // Horde: Orc|Undead|Tauren|Troll  Alliance: Human|Dwarf|NightElf|Gnome
  "CharacterGender": "Male",          // Male|Female (affects capsule dimensions)
  "GmLevel": 6,                       // 0=player, 6=full admin
  "ShouldRun": true,                  // false = skip this entry
  "RunnerType": "Background",         // Foreground (DLL inject) | Background (headless)
  "Openness": 1.0,                    // Big Five personality (0.0-1.0)
  "Conscientiousness": 1.0,
  "Extraversion": 1.0,
  "Agreeableness": 1.0,
  "Neuroticism": 1.0,
  "TargetProcessId": null,            // Optional: attach to existing WoW.exe PID
  "CharacterNameAttemptOffset": null,  // Name generation seed offset
  "BehaviorConfig": null,             // BotBehaviorConfig overrides (pull range, rest thresholds, etc.)
  "BuildConfig": null                 // CharacterBuildConfig (spec, talents, gear goals, professions)
}
```

## Proposed Schema Expansion — Task & Decision Control

### New Top-Level Fields

```jsonc
{
  // ... existing fields ...

  // ── Task Restrictions ──────────────────────────────────────────
  // When null/omitted: ALL tasks enabled (fully autonomous).
  // When set: ONLY the listed tasks are allowed. Everything else is blocked.
  "AllowedTasks": null,               // string[] | null — whitelist of IBotTask names
  // Examples:
  //   null                     → fully autonomous (all 57 tasks)
  //   ["GoToTask"]             → can only navigate, nothing else
  //   ["FishingTask","GoToTask","LootCorpseTask"] → fishing loop only

  // ── Decision Restrictions ──────────────────────────────────────
  // When null/omitted: ALL decisions enabled.
  // When set: ONLY listed ActionTypes are dispatchable by DecisionEngine/Coordinator.
  "AllowedActions": null,             // string[] | null — whitelist of ActionType names
  // Examples:
  //   null                     → all 80 action types
  //   ["Goto","Wait"]          → movement-only bot
  //   ["StartMeleeAttack","CastSpell","LootCorpse"] → combat loop only

  // ── Feature Toggles ────────────────────────────────────────────
  "Features": {
    "Combat": true,                   // Enable combat engagement
    "Questing": true,                 // Enable quest accept/complete/turnin
    "Gathering": true,                // Enable herb/ore/fishing gathering
    "Crafting": true,                 // Enable batch crafting
    "Trading": true,                  // Enable trade/AH/mail
    "Grouping": true,                 // Enable party/raid formation
    "Dungeoneering": true,            // Enable dungeon coordinator
    "Battlegrounds": true,            // Enable BG queue/objectives
    "Travel": true,                   // Enable flight paths, hearthstone, mage ports
    "Training": true,                 // Enable spell/skill training at trainers
    "Vendoring": true,                // Enable buy/sell/repair at vendors
    "PetManagement": true,            // Enable pet summoning/feeding (Hunter/Warlock)
    "Looting": true,                  // Enable corpse looting
    "Skinning": true,                 // Enable skinning after loot
    "MountUsage": true,               // Enable mount summoning
    "Rest": true,                     // Enable rest/eat/drink
    "Buffing": true,                  // Enable self-buff application
    "Resurrection": true,             // Enable spirit release/corpse run
    "DecisionEngine": true,           // Enable ML-based decision making
    "ProgressionPlanning": true       // Enable gear/rep/gold goal tracking
  },

  // ── Coordinator Mode ───────────────────────────────────────────
  "CoordinatorMode": null,            // null | "dungeon" | "battleground" | "combat"
  "CoordinatorTarget": null,          // Dungeon/BG name (e.g., "RagefireChasm", "WarsongGulch")

  // ── GM Camera Mode ─────────────────────────────────────────────
  "IsGmCamera": false,                // true = invisible observer, not a playable bot
  "GmCameraOptions": {
    "StartOnGmIsland": true,          // Spawn on GM Island until directed elsewhere
    "Invisible": true,                // .gm visible off
    "Flying": true,                   // .gm fly on
    "GodMode": true                   // .gm god on — immune to damage
  }
}
```

### Feature Toggle → Task Mapping

| Feature | Tasks Enabled | ActionTypes Enabled |
|---------|--------------|---------------------|
| `Combat` | StartAttackTask, PullTargetTask, PvERotationTask, PvPRotationTask, PvPEngagementTask, StopAttackTask | StartMeleeAttack, StartRangedAttack, StartWandAttack, StopAttack, CastSpell |
| `Questing` | QuestingTask, AcceptQuestTask, CompleteQuestTask, EscortQuestTask, SelectGossipTask | AcceptQuest, DeclineQuest, SelectReward, CompleteQuest, SelectGossip |
| `Gathering` | GatherNodeTask, FishingTask | GatherNode, StartFishing, StartGatheringRoute |
| `Crafting` | BatchCraftTask | Craft |
| `Trading` | MailTransferTask | OfferTrade, OfferGold, OfferItem, AcceptTrade, DeclineTrade, CheckMail |
| `Grouping` | (party management) | SendGroupInvite, AcceptGroupInvite, LeaveGroup, DisbandGroup, ConvertToRaid, ChangeRaidSubgroup, PromoteLeader, PromoteAssistant |
| `Dungeoneering` | DungeoneeringTask, EncounterMechanicsTask, FarmBossTask, MasterLootDistributionTask | StartDungeoneering |
| `Battlegrounds` | BattlegroundQueueTask, WsgObjectiveTask, AbObjectiveTask, AvObjectiveTask, BgRewardCollectionTask | JoinBattleground, AcceptBattleground, LeaveBattleground |
| `Travel` | TravelTask, TakeFlightPathTask, UseHearthstoneTask, MageTeleportTask, WarlockSummonTask, MeetingStoneSummonTask, GoToTask | Goto, SelectTaxiNode, VisitFlightMaster, TravelTo |
| `Training` | TrainSpellTask, TrainerVisitTask, LevelUpTrainerTask | TrainSkill, TrainTalent, VisitTrainer |
| `Vendoring` | VendorVisitTask | BuyItem, BuybackItem, SellItem, RepairItem, RepairAllItems, VisitVendor |
| `PetManagement` | PetManagementTask, PetFeedingTask, SummonPetTask | (via CastSpell) |
| `Looting` | LootCorpseTask | LootCorpse |
| `Skinning` | SkinCorpseTask | SkinCorpse |
| `MountUsage` | MountAcquisitionTask | (via CastSpell) |
| `Rest` | RestTask | Wait |
| `Buffing` | BuffTask, ConjureItemsTask | CastSpell |
| `Resurrection` | ReleaseCorpseTask, RetrieveCorpseTask | ReleaseCorpse, RetrieveCorpse, Resurrect |

### Priority/Precedence

When both `AllowedTasks` and `Features` are set:
- `AllowedTasks` is the **hard whitelist** — nothing outside this list runs
- `Features` is the **soft toggle** — provides a human-readable way to enable/disable groups
- If `AllowedTasks` is null, `Features` drives task selection
- If `AllowedTasks` is set, `Features` is ignored (explicit overrides implicit)

## GM Camera Character

### Concept

A permanent Foreground character that:
- Lives on GM Island (`mapId=1, x=16222, y=16265, z=12`) until directed elsewhere
- Is always invisible (`.gm visible off`), flying (`.gm fly on`), god mode (`.gm god on`)
- Executes GM commands on behalf of StateManager (SOAP alternative for online-character ops)
- Serves as a camera for future spectator/streaming features
- Is **never** a test target — excluded from coordinators and test fixtures
- Will later process user requests (e.g., "show me what RFCBOT3 is doing")

### Config Entry

```json
{
  "AccountName": "CAMBOT",
  "CharacterClass": "Warrior",
  "CharacterRace": "Orc",
  "CharacterGender": "Male",
  "GmLevel": 6,
  "ShouldRun": true,
  "RunnerType": "Foreground",
  "IsGmCamera": true,
  "GmCameraOptions": {
    "StartOnGmIsland": true,
    "Invisible": true,
    "Flying": true,
    "GodMode": true
  },
  "AllowedTasks": [],
  "AllowedActions": [],
  "Features": {
    "Combat": false,
    "Questing": false,
    "Gathering": false,
    "Crafting": false,
    "Trading": false,
    "Grouping": false,
    "Dungeoneering": false,
    "Battlegrounds": false,
    "Travel": false,
    "Training": false,
    "Vendoring": false,
    "PetManagement": false,
    "Looting": false,
    "Skinning": false,
    "MountUsage": false,
    "Rest": false,
    "Buffing": false,
    "Resurrection": false,
    "DecisionEngine": false,
    "ProgressionPlanning": false
  }
}
```

## Implementation Plan

### Phase 1: Schema (Config files only — no code changes)
- [x] Define `AllowedTasks`, `AllowedActions`, `Features` schema
- [x] Create per-task test configs
- [x] Create GM Camera config
- [x] Document feature toggle → task mapping

### Phase 2: CharacterSettings Code (requires code changes)
- [ ] Add `AllowedTasks`, `AllowedActions`, `Features`, `IsGmCamera`, `GmCameraOptions` to `CharacterSettings.cs`
- [ ] Add `CoordinatorMode`, `CoordinatorTarget` to `CharacterSettings.cs`
- [ ] Ensure JSON serialization backward-compatible (null = all enabled)

### Phase 3: StateManagerWorker Enforcement
- [ ] Filter `AllowedTasks` in BotRunnerService task stack push
- [ ] Filter `AllowedActions` in CharacterStateSocketListener action dispatch
- [ ] Apply `Features` toggles as macro filters over task/action sets
- [ ] Skip coordinator enrollment for `IsGmCamera` characters
- [ ] GM Camera startup sequence: login → `.gm on` → `.gm visible off` → `.gm fly on` → `.gm god on` → teleport to GM Island

### Phase 4: Config Editor UI
- [ ] Feature toggle checkboxes in Config Editor detail panel
- [ ] AllowedTasks multi-select list
- [ ] AllowedActions multi-select list
- [ ] GM Camera toggle with options sub-panel
- [ ] Coordinator mode dropdown
