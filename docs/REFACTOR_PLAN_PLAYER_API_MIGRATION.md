# Bot Profile API Migration Plan

## Overview

The bot profiles (in `BotProfiles/`) were written for an older API where player-specific methods like `IsSpellReady`, `CastSpell`, `SetTarget`, `Face`, `MoveToward`, `StopAllMovement`, `GetManaCost`, and `StartRangedAttack` were called directly on `ObjectManager.Player` (the `IWoWLocalPlayer` interface).

However, in the current architecture:
- **These methods exist on `IObjectManager`**, not on `IWoWLocalPlayer`
- `IWoWLocalPlayer` only contains player-specific **properties** (e.g., `CorpsePosition`, `InGhostForm`, `ComboPoints`)

This creates compilation errors in the bot profiles that need to be systematically corrected.

---

## Problem Analysis

### Current Incorrect Pattern (Bot Profiles)

```csharp
// These methods do NOT exist on IWoWLocalPlayer
ObjectManager.Player.IsSpellReady(spellName)
ObjectManager.Player.CastSpell(spellName)
ObjectManager.Player.SetTarget(guid)
ObjectManager.Player.StopAllMovement()
ObjectManager.Player.MoveToward(position)
ObjectManager.Player.Face(position)
ObjectManager.Player.GetManaCost(spellName)
ObjectManager.Player.StartRangedAttack()
ObjectManager.Player.StopCasting()
```

### Correct Pattern (IObjectManager)

```csharp
// These methods exist on IObjectManager
ObjectManager.IsSpellReady(spellName)
ObjectManager.CastSpell(spellName)
ObjectManager.SetTarget(guid)
ObjectManager.StopAllMovement()
ObjectManager.MoveToward(position)
ObjectManager.Face(position)
ObjectManager.GetManaCost(spellName)
ObjectManager.StopCasting()
```

---

## Methods Requiring Migration

### Methods That Should Move from `Player.X()` to `ObjectManager.X()`

| Old Call Pattern | New Call Pattern | Notes |
|------------------|------------------|-------|
| `ObjectManager.Player.IsSpellReady(name)` | `ObjectManager.IsSpellReady(name)` | On IObjectManager |
| `ObjectManager.Player.CastSpell(name, rank, castOnSelf)` | `ObjectManager.CastSpell(name, rank, castOnSelf)` | On IObjectManager |
| `ObjectManager.Player.SetTarget(guid)` | `ObjectManager.SetTarget(guid)` | On IObjectManager |
| `ObjectManager.Player.StopAllMovement()` | `ObjectManager.StopAllMovement()` | On IObjectManager |
| `ObjectManager.Player.MoveToward(pos)` | `ObjectManager.MoveToward(pos)` | On IObjectManager |
| `ObjectManager.Player.Face(pos)` | `ObjectManager.Face(pos)` | On IObjectManager |
| `ObjectManager.Player.GetManaCost(name)` | `ObjectManager.GetManaCost(name)` | On IObjectManager |
| `ObjectManager.Player.StopCasting()` | `ObjectManager.StopCasting()` | On IObjectManager |
| `ObjectManager.Player.StartRangedAttack()` | *Needs implementation* | Not on IObjectManager yet |
| `ObjectManager.Player.DoEmote(emote)` | `ObjectManager.DoEmote(emote)` | On IObjectManager |

### Properties That Stay on `Player`

These should **NOT** be changed - they correctly access player state:

| Property | Location | Notes |
|----------|----------|-------|
| `ObjectManager.Player.IsCasting` | IWoWUnit | Check casting state |
| `ObjectManager.Player.IsChanneling` | IWoWUnit | Check channeling state |
| `ObjectManager.Player.IsInCombat` | IWoWUnit | Combat state |
| `ObjectManager.Player.HealthPercent` | IWoWUnit | Health percentage |
| `ObjectManager.Player.ManaPercent` | IWoWUnit | Mana percentage |
| `ObjectManager.Player.Mana` | IWoWUnit | Current mana |
| `ObjectManager.Player.Position` | IWoWObject | Player position |
| `ObjectManager.Player.HasBuff(name)` | IWoWUnit | Buff check |
| `ObjectManager.Player.HasDebuff(name)` | IWoWUnit | Debuff check |
| `ObjectManager.Player.IsMoving` | IWoWUnit | Movement state |
| `ObjectManager.Player.MovementFlags` | IWoWUnit | Movement flags |
| `ObjectManager.Player.Facing` | IWoWObject | Current facing |
| `ObjectManager.Player.Class` | IWoWPlayer | Player class |
| `ObjectManager.Player.CurrentShapeshiftForm` | IWoWUnit | Druid form |
| `ObjectManager.Player.CorpsePosition` | IWoWLocalPlayer | Corpse location |
| `ObjectManager.Player.InGhostForm` | IWoWLocalPlayer | Ghost state |
| `ObjectManager.Player.ComboPoints` | IWoWLocalPlayer | Rogue combo points |
| `ObjectManager.Player.IsCursed` | IWoWLocalPlayer | Curse check |
| `ObjectManager.Player.IsPoisoned` | IWoWLocalPlayer | Poison check |
| `ObjectManager.Player.IsDiseased` | IWoWLocalPlayer | Disease check |
| `ObjectManager.Player.HasMagicDebuff` | IWoWLocalPlayer | Magic debuff check |
| `ObjectManager.Player.CurrentStance` | IWoWLocalPlayer | Warrior stance |
| `ObjectManager.Player.MainhandIsEnchanted` | IWoWLocalPlayer | Enchant check |
| `ObjectManager.Player.CanRiposte` | IWoWLocalPlayer | Rogue ability |

---

## Files Requiring Changes

Based on code search, these bot profile files need updating:

### High Priority (Confirmed compilation errors)

1. `BotProfiles/MageFrost/Tasks/ConjureItemsTask.cs`
   - `ObjectManager.Player.IsSpellReady()` ? `ObjectManager.IsSpellReady()`
   - `ObjectManager.Player.CastSpell()` ? `ObjectManager.CastSpell()`

2. `BotProfiles/DruidBalance/Tasks/HealTask.cs`
   - `ObjectManager.Player.IsSpellReady()` ? `ObjectManager.IsSpellReady()`
   - `ObjectManager.Player.CastSpell()` ? `ObjectManager.CastSpell()`
   - `ObjectManager.Player.GetManaCost()` ? `ObjectManager.GetManaCost()`

3. `BotProfiles/PriestShadow/Tasks/PullTargetTask.cs`
   - `ObjectManager.Player.SetTarget()` ? `ObjectManager.SetTarget()`
   - `ObjectManager.Player.IsSpellReady()` ? `ObjectManager.IsSpellReady()`
   - `ObjectManager.Player.CastSpell()` ? `ObjectManager.CastSpell()`
   - `ObjectManager.Player.StopAllMovement()` ? `ObjectManager.StopAllMovement()`
   - `ObjectManager.Player.MoveToward()` ? `ObjectManager.MoveToward()`

4. `BotProfiles/HunterBeastMastery/Tasks/PullTargetTask.cs`
   - `ObjectManager.Player.SetTarget()` ? `ObjectManager.SetTarget()`
   - `ObjectManager.Player.StopAllMovement()` ? `ObjectManager.StopAllMovement()`
   - `ObjectManager.Player.StartRangedAttack()` ? Needs implementation
   - `ObjectManager.Player.MoveToward()` ? `ObjectManager.MoveToward()`

### Likely Affected (Need file search to confirm)

All task files in `BotProfiles/*/Tasks/` directories:
- `RestTask.cs` files
- `PvERotationTask.cs` files
- `BuffTask.cs` files
- `HealTask.cs` files
- `PullTargetTask.cs` files

---

## Implementation Steps

### Phase 1: Verify IObjectManager Interface

Ensure `IObjectManager` has all required methods (it does, based on analysis):
- ? `IsSpellReady(string spellName)`
- ? `CastSpell(string spellName, int rank = -1, bool castOnSelf = false)`
- ? `SetTarget(ulong guid)`
- ? `StopAllMovement()` (default interface implementation)
- ? `MoveToward(Position pos)` (default interface implementation)
- ? `Face(Position pos)` (default interface implementation)
- ? `GetManaCost(string spellName)`
- ? `StopCasting()`
- ? `DoEmote(Emote emote)`
- ? `StartRangedAttack()` - **MISSING - needs to be added**

### Phase 2: Add Missing Methods to IObjectManager

Add `StartRangedAttack()` to `IObjectManager` interface:

```csharp
// In Exports/GameData.Core/Interfaces/IObjectManager.cs
void StartRangedAttack();
```

Implement in both:
- `ForegroundBotRunner/Statics/ObjectManager.cs`
- `WoWSharpClient/WoWSharpObjectManager.cs`

### Phase 3: Update Bot Profiles

For each bot profile task file:

1. **Find all instances of** `ObjectManager.Player.{MethodName}` where `{MethodName}` is one of the methods that should be on `IObjectManager`

2. **Replace with** `ObjectManager.{MethodName}`

3. **Keep unchanged** any property accesses like:
   - `ObjectManager.Player.IsCasting`
   - `ObjectManager.Player.HealthPercent`
   - `ObjectManager.Player.HasBuff()`
   - etc.

### Phase 4: Build and Test

1. Run `dotnet build` on the solution
2. Fix any remaining compilation errors
3. Test bot profiles in-game

---

## Search Patterns for Refactoring

Use these regex patterns to find instances needing changes:

```regex
# Methods to migrate (action methods on wrong interface)
ObjectManager\.Player\.(IsSpellReady|CastSpell|SetTarget|StopAllMovement|MoveToward|Face|GetManaCost|StopCasting|StartRangedAttack|DoEmote|StartMovement|StopMovement|SetFacing|Turn180)\s*\(
```

```regex
# Properties that should STAY on Player (do NOT change these)
ObjectManager\.Player\.(IsCasting|IsChanneling|IsInCombat|Health|HealthPercent|Mana|ManaPercent|Position|HasBuff|HasDebuff|IsMoving|MovementFlags|Facing|Class|CurrentShapeshiftForm|CorpsePosition|InGhostForm|ComboPoints|IsCursed|IsPoisoned|IsDiseased|HasMagicDebuff|CurrentStance|MainhandIsEnchanted|CanRiposte|Rage|RagePercent|Energy|EnergyPercent|Level|Guid|Name|TargetGuid|Buffs|Debuffs|Race|IsAutoAttacking|IsStunned|IsConfused|IsFleeing|IsSwimming)
```

---

## Example Refactored Code

### Before (Incorrect)

```csharp
public void Update()
{
    if (ObjectManager.Player.IsCasting) return;  // ? Correct - property access
    
    if (ObjectManager.Player.IsSpellReady(Frostbolt))  // ? Wrong
        ObjectManager.Player.CastSpell(Frostbolt);     // ? Wrong
    
    ObjectManager.Player.SetTarget(target.Guid);       // ? Wrong
    ObjectManager.Player.StopAllMovement();            // ? Wrong
}
```

### After (Correct)

```csharp
public void Update()
{
    if (ObjectManager.Player.IsCasting) return;  // ? Correct - property access
    
    if (ObjectManager.IsSpellReady(Frostbolt))   // ? Correct - method on ObjectManager
        ObjectManager.CastSpell(Frostbolt);      // ? Correct - method on ObjectManager
    
    ObjectManager.SetTarget(target.Guid);        // ? Correct - method on ObjectManager
    ObjectManager.StopAllMovement();             // ? Correct - method on ObjectManager
}
```

---

## Prompt for Next Agent Session

Copy the following prompt to continue this refactoring task in a new session:

---

I need to continue the bot profile API migration refactoring. The plan is documented in `docs/REFACTOR_PLAN_PLAYER_API_MIGRATION.md`.

The core issue is that bot profiles in `BotProfiles/` call methods like `IsSpellReady`, `CastSpell`, `SetTarget`, `StopAllMovement`, `MoveToward`, `Face`, `GetManaCost`, and `StopCasting` on `ObjectManager.Player` (IWoWLocalPlayer), but these methods actually exist on `IObjectManager`.

Tasks to complete:

1. First, add the missing `StartRangedAttack()` method to `IObjectManager` interface in `Exports/GameData.Core/Interfaces/IObjectManager.cs`

2. Implement `StartRangedAttack()` in both ObjectManager implementations:
   - `Services/ForegroundBotRunner/Statics/ObjectManager.cs`
   - `Exports/WoWSharpClient/WoWSharpObjectManager.cs`

3. Search all files in `BotProfiles/` directory for the incorrect call patterns and fix them:
   - Change `ObjectManager.Player.IsSpellReady(x)` to `ObjectManager.IsSpellReady(x)`
   - Change `ObjectManager.Player.CastSpell(x)` to `ObjectManager.CastSpell(x)`
   - Change `ObjectManager.Player.SetTarget(x)` to `ObjectManager.SetTarget(x)`
   - Change `ObjectManager.Player.StopAllMovement()` to `ObjectManager.StopAllMovement()`
   - Change `ObjectManager.Player.MoveToward(x)` to `ObjectManager.MoveToward(x)`
   - Change `ObjectManager.Player.Face(x)` to `ObjectManager.Face(x)`
   - Change `ObjectManager.Player.GetManaCost(x)` to `ObjectManager.GetManaCost(x)`
   - Change `ObjectManager.Player.StopCasting()` to `ObjectManager.StopCasting()`
   - Change `ObjectManager.Player.StartRangedAttack()` to `ObjectManager.StartRangedAttack()`
   - Change `ObjectManager.Player.DoEmote(x)` to `ObjectManager.DoEmote(x)`

4. DO NOT change property accesses that should remain on Player, such as:
   - `ObjectManager.Player.IsCasting` (stays as-is)
   - `ObjectManager.Player.HasBuff(x)` (stays as-is)
   - `ObjectManager.Player.HealthPercent` (stays as-is)
   - `ObjectManager.Player.Position` (stays as-is)

5. Run `dotnet build` to verify all compilation errors are fixed.

---

## Verification Checklist

After completing the refactoring:

- [ ] `IObjectManager` has `StartRangedAttack()` method
- [ ] Both `ObjectManager` implementations implement `StartRangedAttack()`
- [ ] No bot profile files call methods on `ObjectManager.Player` that should be on `ObjectManager`
- [ ] Solution builds without errors
- [ ] Property accesses on `ObjectManager.Player` are preserved (not incorrectly changed)

---

## Architecture Note

This API split follows the principle that:
- **`IWoWLocalPlayer`** represents the **state** of the local player character (properties)
- **`IObjectManager`** provides **actions** that interact with the game world (methods)

This separation allows the same interfaces to work across different implementations:
- `ForegroundBotRunner` (process injection)
- `WoWSharpClient` (pure network client)
