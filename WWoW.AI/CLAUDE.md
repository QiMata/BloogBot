# WWoW.AI — Decision Engine

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

`.\scripts\build.ps1`; unit tests in `Tests/WWoW.AI.Tests`.

> Renamed from `BloogBot.AI` -> `WWoW.AI` on 2026-05-31 (P10 prefix mismatch
> resolved). The legacy product name `BloogBot` is being phased out separately.
