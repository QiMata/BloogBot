---
applyTo: "BotProfiles/**/*.cs"
---

# Class/spec combat profiles (`BotProfiles/*`)

One directory per class/spec (≈27: `WarriorArms`, `MageFrost`, `PriestShadow`, …)
plus `Common/` (shared base) and `ProgressionProfiles/` (leveling configs).

## Profile structure

```
WarriorArms/
├── WarriorArms.cs        # profile class, inherits the shared BotBase
└── Tasks/                # rotation / behavior Tasks
    ├── PvERotationTask.cs
    ├── PvPRotationTask.cs
    ├── BuffTask.cs / RestTask.cs / PullTargetTask.cs
```

## Conventions

- Inherit the shared base in `BotProfiles/Common/`; put rotation logic in `Tasks/`.
- A rotation file is a **Task** (an `IBotTask` orchestrating Actions over many
  ticks with verification), **not** an Action. Keep the
  `Activity → Objective → Task → Action` vocabulary — see
  `docs/Spec/18_TERMINOLOGY.md`.
- Match the surrounding profile's idiom; mirror an existing same-role spec when
  adding a new one.
- Spell/ability data and thresholds belong in the profile, not hardcoded in
  `Exports/BotRunner`.

## Validate with

```powershell
.\scripts\build.ps1
.\scripts\test-fast.ps1
```

## See also

- The **`bot-profile` skill** (use it when creating/modifying a profile).
- `BotProfiles/CLAUDE.md`; symptom map in `AGENTS.md` §10 (wrong rotation/spells).
