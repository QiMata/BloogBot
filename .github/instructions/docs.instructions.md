---
applyTo: "docs/**/*.md,**/TASKS.md,**/TASKS_ARCHIVE.md"
---

# Documentation (`docs/*`, task trackers)

## Entry points & canon

- `docs/SPEC.md` is the single entry point for autonomous work (links Spec
  contracts, the phased Plan, per-activity slots, and the task board).
- **Terminology is canon:** `docs/Spec/18_TERMINOLOGY.md` defines
  `Activity → Objective → Task → Action`. Do not introduce "behavior tree" /
  "task family" / "action mapping" synonyms.
- Architecture orientation: `docs/architecture.md` (+ `docs/Spec/01_ARCHITECTURE.md`);
  deep composition rules in `docs/architecture/aota/`.

## Rules

- **Read before overwrite.** Never replace an existing doc without reading it
  first; preserve structure and cross-links.
- **Update docs when** you add/change an executable command or script, service
  behavior or contract flow, a dependency/tool, a protocol/schema, or a task
  workflow rule (`AGENTS.md` §9).
- Treat the actual files in the repo as source of truth when docs and filesystem
  diverge.

## Task trackers (mandatory protocol)

- Open/in-progress items live in `docs/TASKS.md` and local `*/TASKS.md`.
- On completion, move items to the matching `TASKS_ARCHIVE.md` and **renumber**
  remaining items (no gaps). Keep `TASKS.md` lean.
- New work discovered mid-implementation gets a task before moving on.

## Validate with

- No build needed. Verify links resolve and headings/anchors are intact.
  `.\scripts\lint.ps1` is the repo's hard format gate but checks C# whitespace
  only (not markdown), so doc-only edits won't trip it.

## See also

- `docs/CLAUDE.md`, root `AGENTS.md` §8–§9.
