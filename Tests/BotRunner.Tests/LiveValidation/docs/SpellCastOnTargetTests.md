# SpellCastOnTargetTests

## Bot Execution Mode

**Shodan BG-action** - `Economy.config.json` launches `ECONBG1` as the
Background Orc Warrior action target, `ECONFG1` idle for topology parity, and
SHODAN as the Background Gnome Mage director.

## Test Methods

### CastSpell_BattleShout_AuraApplied

Validates the BG `ActionType.CastSpell` pipeline with Battle Shout (`6673`), an
instant warrior self-buff that appears in `Player.Unit.Auras`.

## Shodan Staging

The test body no longer issues setup GM commands. Staging is fixture-owned:

| Stage | Helper | Notes |
|-------|--------|-------|
| Settings | `EnsureSettingsAsync(Economy.config.json)` | Launches FG + BG + SHODAN topology. |
| Character guard | `AssertConfiguredCharactersMatchAsync(...)` | Ensures the action target is an Orc Warrior. |
| Loadout | `StageBotRunnerLoadoutAsync(...)` | Teaches explicit spell id `6673`; no catch-all `.learn` commands. |
| Rage | `StageBotRunnerRageAsync(...)` | Sets 200 internal rage units, enough for the 10-rage cast. |
| Aura cleanup | `StageBotRunnerAurasAbsentAsync(...)` | Removes Battle Shout and stale strength/stance auras before the action. |

## Action Flow

1. Resolve action targets with `ResolveBotRunnerActionTargets(includeForegroundIfActionable: false)`.
2. Stage `ECONBG1`; SHODAN never resolves as an action target.
3. Dispatch correlated `ActionType.CastSpell` with `IntParam = 6673`.
4. Fail early if the matching `CommandAckEvent` reports `Failed` or `TimedOut`.
5. Assert aura `6673` appears in the BG snapshot.
6. Remove the Battle Shout aura through fixture cleanup.

## Current Status

Migrated on 2026-04-25.

- `spell_cast_on_target_shodan.trx` -> `passed (1/1)`.
- Deterministic safety bundle -> `passed (33/33)`.
- Action/dispatch readiness bundle -> `passed (60/60)`.

FG remains idle in this migrated slice because prior Shodan spell-id migrations
documented the foreground `ActionType.CastSpell` by-id gap separately. This file
now validates the BG spell-id cast path while preserving the shared FG/BG/SHODAN
launch topology.
