# ActionDispatch — isolated single-Action tests

This folder is the home for tests that verify **one Action shape in isolation** —
the atomic code primitives at the bottom of the canonical hierarchy
`Activity → Objective → Task → Action`
(see [`docs/Spec/18_TERMINOLOGY.md`](../../../docs/Spec/18_TERMINOLOGY.md)).

It exists because the project's [`CLAUDE.md`](../../../CLAUDE.md) "Test Isolation
Rules" name it as the legal destination for such tests:

> New tests that legitimately need to verify a single Action shape in isolation
> should go in `Tests/BotRunner.Tests/ActionDispatch/` (a new folder created for
> the purpose) — not in `Tests/BotRunner.Tests/LiveValidation/`.

## What belongs here

- Tests that assert the **shape of a single Action / `ObjectiveMessage`** — one
  packet opcode, one memory read/write, one key press — without exercising
  `DecisionEngine`, `ActivityResolver`, or the IBotTask stack.
- The narrow, intentional cases that would otherwise be tempted to construct
  `new ObjectiveMessage { ObjectiveType = ... }` and dispatch it directly.

## What does NOT belong here

- **Behavior tests.** Anything that should drive an **Activity** and assert on the
  bot's resulting behavior (`WoWActivitySnapshot` fields, task-stack progression)
  belongs in `LiveValidation/`, which must *declare an Activity and let the bot
  decide* — never remote-control it via `SendActionAsync(...)`. See the
  "Drive Activities, not Actions" rule in
  [`.github/instructions/tests.instructions.md`](../../../.github/instructions/tests.instructions.md).
- **Shodan dispatches.** Shodan is the production GM Liaison / test director, never
  a test subject. Resolve FG/BG targets via `ResolveBotRunnerActionTargets(...)`.

## References

- [`CLAUDE.md`](../../../CLAUDE.md) — "Test Isolation Rules — CRITICAL"
- [`.github/instructions/tests.instructions.md`](../../../.github/instructions/tests.instructions.md)
- [`docs/Spec/18_TERMINOLOGY.md`](../../../docs/Spec/18_TERMINOLOGY.md) — Activity / Objective / Task / Action canon
- [`docs/Plan/12_PARALLEL_TEST_ISOLATION_REFACTOR.md`](../../../docs/Plan/12_PARALLEL_TEST_ISOLATION_REFACTOR.md) — Phase 12, which rewrites grandfathered Category-A LiveValidation sites
