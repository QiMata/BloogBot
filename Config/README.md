# Config/

Declarative JSON/YAML consumed at runtime — **data, not code**. Activity
definitions, character/loadout templates, and the foundry persona/storyline
configs live here.

| Path | Contents |
|------|----------|
| `activities/` | Activity definitions (`dungeon.*`, `raid.*`, `quest.*`, `bg.*`, `prof.*`, `boss.*`, `econ.*`, `rep.*`, `event.*`) |
| `foundry/` | persona-runtime, storyline-runtime, storyline-seed |
| `CharacterTemplates/` | character build / loadout templates |
| `schema/activity.schema.json` | JSON Schema every `activities/*.json` must validate against |

Every `activities/*.json` must validate against `schema/activity.schema.json`; add
schema fields before using them, and use the canonical
`Activity → Objective → Task → Action` vocabulary
([docs/Spec/18_TERMINOLOGY.md](../docs/Spec/18_TERMINOLOGY.md)).

- **Agent rules:** [CLAUDE.md](CLAUDE.md) and
  `.github/instructions/config.instructions.md`.
