---
applyTo: "BloogBot.AI/**/*.cs"
---

# AI decision & memory layer (`BloogBot.AI/*`)

The activity state machine, LLM advisory, and character-memory layer that sits
above the runtime.

| Path | Role |
|------|------|
| `StateMachine/` | `BotActivityStateMachine`, activity-history, triggers |
| `States/` | canonical `BotActivity` / `MinorState` enums |
| `Advisory/` | LLM advisory results + `IAdvisoryValidator`, override log |
| `Transitions/` | forbidden-transition registry + rules |
| `Semantic/` | Semantic Kernel coordinator + plugin catalog |
| `Memory/` | `PostgresCharacterMemoryRepository`, character memory service |
| `Summary/`, `Invocation/`, `Observable/`, `Configuration/` | summarization, decision invocation, state observation |

## Conventions

- Use the canonical `Activity → Objective → Task → Action` vocabulary
  (`docs/Spec/18_TERMINOLOGY.md`) and the existing `BotActivity` / `MinorState`
  enums — do not invent parallel state names.
- Route LLM/advisory output through `IAdvisoryValidator` and the
  forbidden-transition registry; never apply raw model output to bot state.
- Character memory persists to **Postgres** — this is distinct from MaNGOS game
  state and from the protobuf snapshot stream.

## Validate with

```powershell
.\scripts\build.ps1
.\scripts\test-fast.ps1                      # includes WWoWBot.AI.Tests
```

## Do NOT

- Mutate MaNGOS game state from here — server state goes through SOAP only
  (`AGENTS.md` §7).
- Bypass advisory validation / forbidden-transition checks when consuming LLM
  results.

## See also

- `BloogBot.AI/CLAUDE.md`, `Services/DecisionEngineService`
  (`services.instructions.md`), `docs/architecture/aota/`.
- The legacy `BloogBot.AI` / `WWoWBot.AI.Tests` prefix mismatch is tracked in
  `CLAUDE.md` (P10) — do not rename as part of unrelated work.
