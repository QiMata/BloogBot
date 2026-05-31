---
name: gold-standard-export
description: Package a WWoW-proven pattern as a portable, game-agnostic skill in the monorepo-shared registry, with a cross-game smoke test. Use when a repo-local skill is mature enough to reuse in another game repo.
trigger: export a skill, portable skill, cross-game skill, monorepo-shared skill, generalize a skill, gold standard, mmo skill, skill portability
---

# Gold-Standard Skill Export

## Goal

Promote a proven WWoW skill to the monorepo-shared registry so other game repos
(FF XI, WAR, UO, EQ, …) can reuse it — generalized to remove WWoW-specific paths/
symbols, and backed by a cross-game smoke test.

## Inputs

- A repo-local skill that has been **proven** by ≥1 real WWoW invocation.
- Key references:
  - Contract: `docs/Spec/15_SKILLS.md` (§Cross-game replication contract,
    §Game-specific extensions, §bar for cross-game use).
  - Repo-local registry: `.claude/skills/<slug>/SKILL.md`.
  - Monorepo-shared registry: `~/.claude/skills/mmo-*/SKILL.md`.
  - Contract tests: `Tests/BotRunner.Tests/Spec/SkillsContractTests.cs`.
- The SKILL.md structure: frontmatter (`name`, `description`, `trigger`) + the 8
  sections (Goal, Inputs, Preconditions, Procedure, Verification, Outputs, Failure
  modes and recovery, Related skills).

## Preconditions

- The skill works in WWoW (proven) and its `SKILL.md` is contract-conformant.
- You can name the game-agnostic shape vs. the WWoW-specific specifics.

## Procedure

1. Confirm the source skill is proven (a real WWoW invocation closed a task).
2. Copy it to `~/.claude/skills/mmo-<slug>/SKILL.md` and **generalize**: replace
   WWoW-specific absolute paths/symbols with placeholders (e.g.
   `<repo>/tools/MmapGen/config.json`, `<game-largest-capsule>`); keep
   game-specifics in the repo-local `.claude/skills/<game>-<slug>` override.
3. Add a cross-game smoke test in the target repo (FF XI by default) that exercises
   the skill end-to-end and closes ≥1 `docs/TASKS.md` item.
4. Verify against the §bar: works in WWoW, no un-generalized WWoW path/symbol, a
   passing cross-game smoke test exists.

## Verification

- `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --filter "FullyQualifiedName~SkillsContractTests"`
  (the on-disk skill contract).
- The cross-game smoke test exits 0 in the target repo.
- (When the tool lands) the SkillAutoBootstrap self-target canary scores the skill
  ≥0.95 against WWoW.

## Outputs

- `~/.claude/skills/mmo-<slug>/SKILL.md` (generalized) + any
  `.claude/skills/<game>-<slug>` override + a cross-game smoke test.

## Failure modes and recovery

- **Exporting an unproven skill** — only promote after a real WWoW invocation.
- **Leaving WWoW-specific paths** in the shared copy — generalize or it fails the
  portability bar.
- **No cross-game smoke test** — the skill is not "ready for cross-game use".

## Related skills

- All other skills are candidates for export once proven — link the source skill
  here when exporting (e.g. [[botrunner-task-implementation]],
  [[pathfinding-bake-iteration]]).
- Reference: `docs/Spec/15_SKILLS.md`.
