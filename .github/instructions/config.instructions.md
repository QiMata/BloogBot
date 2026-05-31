---
applyTo: "Config/**/*.json"
---

# Activity & template config (`Config/*`)

Declarative JSON that defines Activities, foundry runtime data, and character
templates consumed at runtime.

| Path | Contents |
|------|----------|
| `Config/activities/` | Activity definitions (`dungeon.*`, `raid.*`, `quest.*`, `bg.*`, `prof.*`, `boss.*`, `econ.*`, `rep.*`, `event.*`) |
| `Config/foundry/` | persona / storyline runtime + seed data |
| `Config/CharacterTemplates/` | character build/loadout templates |
| `Config/schema/activity.schema.json` | JSON Schema for activity files |

## Conventions

- Every `Config/activities/*.json` must validate against
  `Config/schema/activity.schema.json`. Update the schema first if you add a field.
- File naming follows `<category>.<name>.json` — match existing prefixes
  (`dungeon.ragefire-chasm.json`, `raid.zg.json`, `quest.starter.durotar.json`, …).
- Use the canonical `Activity → Objective → Task → Action` vocabulary in field
  names and descriptions (`docs/Spec/18_TERMINOLOGY.md`).

## Validate with

```powershell
.\scripts\build.ps1        # config-consuming projects compile/load
.\scripts\test-fast.ps1
```

Validate a file against the schema with any JSON-Schema tool, e.g.:
`npx ajv-cli validate -s Config/schema/activity.schema.json -d Config/activities/<file>.json`

## Do NOT

- Edit the MaNGOS MySQL database to back a config — server state changes go
  through SOAP only (`AGENTS.md` §7).

## See also

- `Config/CLAUDE.md`, `docs/architecture/aota/` (how Activities compose).
