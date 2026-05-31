---
applyTo: "Tests/**/*.cs"
---

# Tests (`Tests/*`)

xUnit + Moq across ~15 projects. Project naming: `{ProjectName}.Tests` matching
the source project exactly.

## Layers & how to run

| Layer | What | Command |
|-------|------|---------|
| 3 | Unit (no server) | `.\scripts\test-fast.ps1` |
| 4 | Live integration (needs MaNGOS stack) | `.\scripts\test-integration.ps1` |
| all | Full layered suite | `.\scripts\test.ps1` (wraps `.\run-tests.ps1`) |

Target one project:
`dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore --settings Tests/BotRunner.Tests/test.runsettings`

## LiveValidation rules (`Tests/BotRunner.Tests/LiveValidation/`) — CRITICAL

- **Drive Activities, not Actions.** A new test sets up world state, declares an
  Activity, and asserts on the bot's behavior (`WoWActivitySnapshot`, task-stack
  progression). Do **not** `new ObjectiveMessage{...}` + `SendActionAsync(...)`
  in a new test body — that remote-controls the bot and bypasses
  DecisionEngine/ActivityResolver. (Existing Category-A sites are grandfathered
  until Phase 12 — do not mass-refactor them.)
- New isolated single-Action checks go in `Tests/BotRunner.Tests/ActionDispatch/`,
  not LiveValidation.
- **Shodan is the GM director only** — never a test subject. Dispatch to resolved
  FG/BG accounts via `ResolveBotRunnerActionTargets(...)`.
- **Never skip for "resource not found."** Missing pool/node/mob the bot can't
  find is a real failure (`Assert.Fail`, not `Skip.If`). Acceptable skips:
  fixture-not-ready, known client bugs (e.g. CRASH-001).
- GM setup uses **SOAP only** (`ExecuteGMCommandAsync` / `SendGmChatCommandAsync`).

## Conventions

- Live tests poll StateManager APIs, fail fast on disconnect/crash, capture
  latest screenshots/state dumps.
- Use traits for filtering (`Category=Unit|Integration|RequiresInfrastructure`).
- **Format gate:** test `*.cs` are subject to the same hard format gate as the
  rest of the tree — `.\scripts\lint.ps1` (`dotnet format whitespace
  --verify-no-changes` against the root `.editorconfig`), enforced by
  `.\scripts\check.ps1`. Run `.\scripts\format.ps1` to fix.

## See also

- `Tests/CLAUDE.md`, `docs/testing.md`, `AGENTS.md` §5 (test commands).
- `Tests/BotRunner.Tests/LiveValidation/docs/` (Shodan + execution-mode docs).
