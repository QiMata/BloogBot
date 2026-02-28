# BotProfiles — Profile Task Map

Quick-reference for which spec has which task files. All 27 specs override the 5 `BotBase` factory methods. Extra tasks (Heal, SummonPet, ConjureItems) are spec-specific additions.

## Factory Methods (BotBase)

| Factory Method | Returns | Required |
|---|---|---|
| `CreateRestTask` | `RestTask` | All specs |
| `CreateMoveToTargetTask` | `PullTargetTask` | All specs |
| `CreateBuffTask` | `BuffTask` | All specs |
| `CreatePvERotationTask` | `PvERotationTask` | All specs |
| `CreatePvPRotationTask` | `PvPRotationTask` | All specs |

## Spec Task Map

Legend: **B**=Buff, **P**=Pull, **PvE**, **PvP**, **R**=Rest, **H**=Heal, **S**=SummonPet, **C**=ConjureItems

| Class | Spec | Directory | Core (B/P/PvE/PvP/R) | Extra Tasks |
|-------|------|-----------|:-----:|-------------|
| Druid | Balance | `DruidBalance` | 5/5 | HealTask |
| Druid | Feral | `DruidFeral` | 5/5 | HealTask |
| Druid | Restoration | `DruidRestoration` | 5/5 | — |
| Hunter | Beast Mastery | `HunterBeastMastery` | 5/5 | HealTask, SummonPetTask, PetManager.cs |
| Hunter | Marksmanship | `HunterMarksmanship` | 5/5 | SummonPetTask |
| Hunter | Survival | `HunterSurvival` | 5/5 | SummonPetTask |
| Mage | Arcane | `MageArcane` | 5/5 | ConjureItemsTask |
| Mage | Fire | `MageFire` | 5/5 | ConjureItemsTask |
| Mage | Frost | `MageFrost` | 5/5 | ConjureItemsTask |
| Paladin | Holy | `PaladinHoly` | 5/5 | — |
| Paladin | Protection | `PaladinProtection` | 5/5 | HealTask |
| Paladin | Retribution | `PaladinRetribution` | 5/5 | HealTask |
| Priest | Discipline | `PriestDiscipline` | 5/5 | — |
| Priest | Holy | `PriestHoly` | 5/5 | — |
| Priest | Shadow | `PriestShadow` | 5/5 | HealTask |
| Rogue | Assassin | `RogueAssassin` | 5/5 | — |
| Rogue | Combat | `RogueCombat` | 5/5 | — |
| Rogue | Subtlety | `RogueSubtlety` | 5/5 | — |
| Shaman | Elemental | `ShamanElemental` | 5/5 | HealTask |
| Shaman | Enhancement | `ShamanEnhancement` | 5/5 | HealTask |
| Shaman | Restoration | `ShamanRestoration` | 5/5 | — |
| Warlock | Affliction | `WarlockAffliction` | 5/5 | SummonPetTask |
| Warlock | Demonology | `WarlockDemonology` | 5/5 | SummonPetTask |
| Warlock | Destruction | `WarlockDestruction` | 5/5 | SummonPetTask |
| Warrior | Arms | `WarriorArms` | 5/5 | — |
| Warrior | Fury | `WarriorFury` | 5/5 | — |
| Warrior | Protection | `WarriorProtection` | 5/5 | — |

## Shared Code

| File | Location | Purpose |
|------|----------|---------|
| `BotBase.cs` | `Common/` | Abstract base with 5 factory methods |
| `WarlockBaseRotationTask.cs` | `Common/` | Shared rotation logic for all warlock specs |

## Extra Task Summary

| Task | Specs |
|------|-------|
| HealTask | DruidBalance, DruidFeral, HunterBeastMastery, PaladinProtection, PaladinRetribution, PriestShadow, ShamanElemental, ShamanEnhancement |
| SummonPetTask | HunterBeastMastery, HunterMarksmanship, HunterSurvival, WarlockAffliction, WarlockDemonology, WarlockDestruction |
| ConjureItemsTask | MageArcane, MageFire, MageFrost |
| PetManager.cs | HunterBeastMastery (root-level, not in Tasks/) |

## File Layout Convention

```
BotProfiles/<SpecName>/
  <SpecName>.cs          # Profile class (extends BotBase)
  Tasks/
    BuffTask.cs
    PullTargetTask.cs
    PvERotationTask.cs
    PvPRotationTask.cs
    RestTask.cs
    [HealTask.cs]        # Optional
    [SummonPetTask.cs]   # Optional (hunters, warlocks)
    [ConjureItemsTask.cs]# Optional (mages)
```

## Parity Notes

- All 27 PvP factories correctly return `PvPRotationTask` (fixed in BP-MISS-001).
- Profile factory wiring is guarded by `BotProfileFactoryBindingsTests` (BP-MISS-002).
- DruidFeral directory renamed from `DruidFeralCombat` (BP-MISS-003).
