# Config — Activity & Template Definitions

Declarative JSON consumed at runtime. No code here — edits are data, validated
against a schema.

## Layout

| Path | Contents |
|------|----------|
| `activities/` | Activity definitions: `dungeon.*`, `raid.*`, `quest.*`, `bg.*`, `prof.*`, `boss.*`, `econ.*`, `rep.*`, `event.*` |
| `foundry/` | persona-runtime, storyline-runtime, storyline-seed |
| `CharacterTemplates/` | character build/loadout templates |
| `schema/activity.schema.json` | JSON Schema for activity files |

## Special rules

- Every `activities/*.json` must validate against `schema/activity.schema.json`.
  Add schema fields before using them.
- Naming: `<category>.<name>.json` (match existing prefixes).
- Use canonical `Activity → Objective → Task → Action` vocabulary
  (`docs/Spec/18_TERMINOLOGY.md`); see `docs/architecture/aota/` for how
  Activities compose.

> Path-specific agent rules: `.github/instructions/config.instructions.md`.
