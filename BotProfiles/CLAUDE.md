# BotProfiles - Class/Specialization Combat Profiles

Contains 27 class-specialization behavior profiles plus shared base classes. Each profile defines combat rotation, buff management, rest behavior, and target selection for a specific WoW class and spec.

## Structure

```
BotProfiles/
├── Common/                  # Shared base classes and utilities
├── ProgressionProfiles/     # Leveling progression configs
├── DruidBalance/           ├── HunterBeastMastery/
├── DruidFeral/             ├── HunterMarksmanship/
├── DruidRestoration/       ├── HunterSurvival/
├── MageArcane/             ├── PaladinHoly/
├── MageFire/               ├── PaladinProtection/
├── MageFrost/              ├── PaladinRetribution/
├── PriestDiscipline/       ├── RogueAssassin/
├── PriestHoly/             ├── RogueCombat/
├── PriestShadow/           ├── RogueSubtlety/
├── ShamanElemental/        ├── WarlockAffliction/
├── ShamanEnhancement/      ├── WarlockDemonology/
├── ShamanRestoration/      ├── WarlockDestruction/
├── WarriorArms/            ├── WarriorFury/
└── WarriorProtection/
```

## Pattern

Each profile folder contains classes that implement the combat rotation:
- **Spell priority list** — ordered by effectiveness/situational triggers
- **Mana/resource management** — when to drink, innervate, evocate, etc.
- **Target selection** — which mobs to engage, pull mechanics
- **Rest behavior** — health/mana thresholds for eating/drinking

## Adding a New Profile

1. Copy the closest existing profile folder as a template
2. Implement the class-specific rotation in the main profile class
3. Use `Common/` base classes for shared behavior
4. Test with both ForegroundBotRunner and BackgroundBotRunner
