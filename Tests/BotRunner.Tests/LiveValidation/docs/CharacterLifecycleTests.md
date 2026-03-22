# CharacterLifecycleTests

The class now keeps only the inventory-add baseline that other live suites depend on.

## Active Test

### Equipment_AddItemToInventory

**Purpose:** Prove the GM add-item setup path is visible in snapshots for BG and FG.

**Code paths:**
- Test entry: `Tests/BotRunner.Tests/LiveValidation/CharacterLifecycleTests.cs`
- Chat command dispatch/tracing: `Tests/BotRunner.Tests/LiveValidation/LiveBotFixture.BotChat.cs`
- Snapshot polling: `Tests/BotRunner.Tests/LiveValidation/LiveBotFixture.Snapshots.cs`
- Inventory state production: `Exports/BotRunner/BotRunnerService.cs`
- BG inventory updates: `Exports/WoWSharpClient/`
- FG inventory reads: `Services/ForegroundBotRunner/Statics/ObjectManager.cs`

**Assertions:**
- `.additem` dispatch succeeds and is not rejected by the command table
- Linen Cloth appears in bag snapshots
- Pre-existing inventory contamination is cleared before the assertion

## Removed In Overhaul Pass 1

- `Consumable_AddPotionToInventory`
- `Death_KillAndRevive`
- `CharacterCreation_InfoAvailable`
