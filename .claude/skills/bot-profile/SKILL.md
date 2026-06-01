---
name: bot-profile
description: Creating or modifying WoW class/spec combat profiles. Use when adding a new class profile, fixing combat rotations, or adjusting bot behavior for a specific specialization.
trigger: new class profile, combat rotation, class spec behavior, fix rotation, buff rest pull mechanics, BotProfiles, add a spec profile
---

# Bot Profile Development

## Goal

Create or modify a class/spec combat profile so the bot fights, buffs, rests, and
pulls correctly for that specialization. A profile composes **Tasks** (rotation,
buff, rest, pull) — not raw Actions.

## Inputs

- The class + spec and its role (healer/tank/dps) and resource model.
- Profile location — all profiles live in `BotProfiles/`:
  ```
  BotProfiles/
  ├── Common/              # Shared base classes — start here
  ├── <ClassName><Spec>/   # e.g., MageFrost/, WarriorArms/
  └── ProgressionProfiles/ # Leveling progression configs
  ```
- Key interfaces:
  - `Exports/GameData.Core/IWoWLocalPlayer.cs` — available player actions.
  - `Exports/GameData.Core/IWoWUnit.cs` — target inspection methods.
  - `Exports/GameData.Core/ISpell.cs` — spell casting interface.
- Area rules: `.github/instructions/bot-profiles.instructions.md`.

## Preconditions

- `WoW.exe` is killed before building (FG injection locks the output DLLs).
- You have picked the closest existing profile as a template (same class/different
  spec, or same role) and reviewed `BotProfiles/Common/` for reusable helpers.

## Procedure

1. **Pick a template**: find the most similar existing profile (same class
   different spec → copy that class's folder; same role → copy a similar role).
2. **Create the folder**: `BotProfiles/<ClassName><Spec>/`.
3. **Implement the profile class** with: combat rotation (spell priority list),
   buff management (self + party), rest behavior (eat/drink thresholds), and pull
   mechanics (ranged/body/pet pull).
4. **Follow the priority-based rotation pattern**:
   1. Emergency actions (health pot, defensive cooldown)
   2. Interrupt / CC if needed
   3. DoT refresh (if applicable)
   4. Cooldowns (if available and appropriate)
   5. Core rotation spells (by priority)
   6. Filler spell
5. **Honor the resource model**: mana (drink at threshold, manage expensive
   spells), rage (build/spend, don't cap), energy (pool for key abilities, manage
   combo points), pet (attack/follow/defensive/aggressive modes).

## Verification

1. Build: `dotnet build WestworldOfWarcraft.sln`.
2. Test with **ForegroundBotRunner** (live WoW client) for visual verification.
3. Test with **BackgroundBotRunner** (headless) for automated regression.
4. Confirm shared utilities in `BotProfiles/Common/` were reused, not duplicated.

## Outputs

- New `BotProfiles/<ClassName><Spec>/` profile + rotation/buff/rest/pull Tasks.
- `docs/TASKS.md` update if task-tracked.

## Failure modes and recovery

- **Wrong spell / rotation** — check the spec's spell priority in
  `BotProfiles/<ClassSpec>/`.
- **Capping a resource** (rage/energy) or oom from mismanaged mana — re-check the
  resource model.
- **Duplicating Common helpers** instead of reusing them.
- **Building with WoW.exe running** → MSB3027 DLL copy lock; kill the specific PID.

## Related skills

- [[botrunner-task-implementation]] — the Task contract a profile's rotation uses.
- [[loadout-template-authoring]] — gear/spells the profile assumes.
- [[debugging]] — when a rotation misbehaves.
