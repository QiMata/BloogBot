# BuffAndConsumableTests

Merged replacement for `ConsumableUsageTests` and `BuffDismissTests`. The goal is a single documented lifecycle around add-item, use-item, aura observation, and dismiss.

## Bot Execution Mode

**Dual-Bot Conditional** — FG runs first as gold standard, then BG. FG gated on IsFgActionable. See [TEST_EXECUTION_MODES.md](TEST_EXECUTION_MODES.md).

## Active Tests

### 1. UseConsumable_AppliesBuff

**Purpose:** Validate the item-use behavior path with explicit bag and aura metrics.

**Code paths:**
- Test entry: `Tests/BotRunner.Tests/LiveValidation/BuffAndConsumableTests.cs`
- Item add/setup helpers: `Tests/BotRunner.Tests/LiveValidation/LiveBotFixture.BotChat.cs`
- Action forwarding: `Tests/BotRunner.Tests/LiveValidation/LiveBotFixture.cs`
- BotRunner action translation: `Exports/BotRunner/BotRunnerService.cs`
- BG use-item implementation: `Exports/WoWSharpClient/`
- FG use-item implementation: `Services/ForegroundBotRunner/Statics/ObjectManager.cs`

**Assertions:**
- `.additem` succeeds and is reflected in bags
- `ActionType.UseItem` returns `ResponseResult.Success`
- Lion's Strength aura appears
- Elixir bag slot is consumed after use

### 2. DismissBuff_RemovesBuff

**Purpose:** Validate the DismissBuff path after the aura is known to be present.

**Code paths:**
- Test entry: `Tests/BotRunner.Tests/LiveValidation/BuffAndConsumableTests.cs`
- Dismiss dispatch: `Exports/BotRunner/BotRunnerService.cs`
- BG aura/buff tracking gap: `Exports/WoWSharpClient/`
- FG aura cancellation: `Services/ForegroundBotRunner/Statics/ObjectManager.cs`

**Assertions:**
- `ActionType.DismissBuff` returns `ResponseResult.Success`
- FG removes Lion's Strength from snapshot auras
- BG remains explicitly tracked as blocked by `BB-BUFF-001` until `WoWUnit.Buffs` is populated from packets
