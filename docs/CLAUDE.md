# docs — Specifications & Knowledge Base

Project documentation, specs, plans, and architecture. `docs/SPEC.md` is the
single entry point for autonomous work.

## Map

| Path | Contents |
|------|----------|
| `SPEC.md` | Entry point → Spec contracts, phased Plan, task board |
| `Spec/` | Numbered spec contracts; `18_TERMINOLOGY.md` is the canon for Activity/Objective/Task/Action |
| `Plan/` | Phased roadmap + `Activities/` slots + `Crashes/` |
| `architecture/` | Deep architecture; `aota/` = composition rules |
| `physics/` | Physics + pathfinding; `README.md` indexes the pathfinding-overhaul freeze |
| `server-protocol/` | WoW 1.12.1 protocol reference |
| `TASKS.md` / `ARCHIVE.md` | Active vs completed task history |

## Special rules

- **Read before overwrite.** Never replace a doc without reading it first.
- Update docs when commands/scripts, service behavior, dependencies, schemas, or
  task workflow rules change (`AGENTS.md` §9).
- Keep terminology consistent with `Spec/18_TERMINOLOGY.md`.
- Task-tracker protocol: open items in `TASKS.md`, completed → `ARCHIVE.md`, then
  renumber.

> Path-specific agent rules: `.github/instructions/docs.instructions.md`.
