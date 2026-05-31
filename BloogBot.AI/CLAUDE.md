# BloogBot.AI — Decision Engine

LLM-assisted decision/activity engine. Semantic Kernel for invocation, a
Stateless state machine for activity flow, Postgres for character memory.

## Key areas

| Folder | Purpose |
|--------|---------|
| `Semantic/` | `KernelCoordinator`, `PluginCatalog` — Semantic Kernel wiring |
| `StateMachine/` | `BotActivityStateMachine` (Stateless), `Trigger` |
| `States/` | `BotActivity`, `MinorState` definitions |
| `Transitions/` | Forbidden-transition registry + rules |
| `Invocation/` | `DecisionInvoker` + invocation events |
| `Memory/` | `CharacterMemoryService`, `PostgresCharacterMemoryRepository` (Npgsql) |
| `Advisory/` | LLM advisory validation + override log |
| `Summary/` | summary/distillation pipeline |

## Dependencies

- Semantic Kernel, Stateless, Npgsql (Postgres).
- Depends on `GameData.Core` interfaces only — do not add upward deps.

## Build / test

`.\scripts\build.ps1`; unit tests in `Tests/BloogBot.AI.Tests`.

> Note: legacy prefix mismatch (`BloogBot.AI` vs `WWoW.AI.Tests`) is tracked in
> P10 — do not rename without coordinating.
