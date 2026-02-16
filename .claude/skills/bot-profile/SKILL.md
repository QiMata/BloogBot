---
name: bot-profile
description: Creating or modifying WoW class/spec combat profiles. Use when adding a new class profile, fixing combat rotations, or adjusting bot behavior for a specific specialization.
---

# Bot Profile Development

## Profile Location

All profiles live in `BotProfiles/`. Each class-spec combo has its own folder:
```
BotProfiles/
├── Common/              # Shared base classes — start here
├── <ClassName><Spec>/   # e.g., MageFrost/, WarriorArms/
└── ProgressionProfiles/ # Leveling progression configs
```

## Creating a New Profile

1. **Pick a template**: Find the most similar existing profile
   - Same class, different spec? Copy that class's folder
   - Same role (healer/tank/dps)? Copy a similar role profile
2. **Create the folder**: `BotProfiles/<ClassName><Spec>/`
3. **Implement the profile class** with these components:
   - Combat rotation (spell priority list)
   - Buff management (self-buffs, party buffs)
   - Rest behavior (eat/drink thresholds)
   - Pull mechanics (ranged pull, body pull, pet pull)

## Combat Rotation Pattern

Rotations follow a priority-based pattern:
```
1. Emergency actions (health pot, defensive cooldown)
2. Interrupt/CC if needed
3. DoT refresh (if applicable)
4. Cooldowns (if available and appropriate)
5. Core rotation spells (by priority)
6. Filler spell
```

## Key Interfaces

- Check `Exports/GameData.Core/IWoWLocalPlayer.cs` for available player actions
- Check `Exports/GameData.Core/IWoWUnit.cs` for target inspection methods
- Check `Exports/GameData.Core/ISpell.cs` for spell casting interface

## Resource Management

Each class has different resource patterns:
- **Mana classes**: Track mana %, drink at threshold, manage expensive spells
- **Rage classes**: Build/spend rotation, don't cap rage
- **Energy classes**: Pool energy for key abilities, manage combo points
- **Pet classes**: Pet attack, follow, defensive/aggressive modes

## Testing a Profile

1. Build: `dotnet build WestworldOfWarcraft.sln`
2. Test with **ForegroundBotRunner** (live WoW client) for visual verification
3. Test with **BackgroundBotRunner** (headless) for automated regression
4. Check `BotProfiles/Common/` for any shared utility methods you should reuse
