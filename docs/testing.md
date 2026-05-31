# Testing

> Practical guide to running and adding tests. The **rules** for what a test may
> do live in [`Spec/13_TESTING.md`](Spec/13_TESTING.md) and the Test Isolation /
> Test Skip policies in [`../CLAUDE.md`](../CLAUDE.md). This page covers the
> mechanics: framework, commands, layers, and where new tests go.

## Framework

Tests use **xUnit + Moq**. `Tests/Tests.Infrastructure` adds
`Xunit.SkippableFact` for the (narrow) cases where a skip is legitimate. The
SDK is pinned to .NET 8 via [`../global.json`](../global.json).

## Running tests — use the `scripts/` interface

Do not guess `dotnet test` / `run-tests.ps1` invocations; the stable wrappers
are the contract (full reference: [`../scripts/README.md`](../scripts/README.md)).

| Command | Scope |
|---|---|
| `scripts/test-fast` | **Inner loop.** Unit tests only (Layer 3). No server needed. |
| `scripts/test` | Full layered suite (all layers). |
| `scripts/test-integration` | Live integration tests (Layer 4). **Requires the MaNGOS stack up.** |
| `scripts/check` | Pre-PR gate: lint (advisory) → build → fast tests. |

These wrap [`../run-tests.ps1`](../run-tests.ps1), whose layer model is:

- **Layer 1** — native DLL availability checks.
- **Layer 2** — navigation physics + pathfinding tests.
- **Layer 3** — `WoWSharpClient`/`BotRunner` unit tests (fast, no server).
- **Layer 4** — live integration tests (need MaNGOS + SOAP).

A single project can still be run directly when iterating, e.g.:

```powershell
dotnet test Tests/PathfindingService.Tests/PathfindingService.Tests.csproj `
  --configuration Release --no-restore `
  --settings Tests/PathfindingService.Tests/test.runsettings
```

## Test projects

Roughly 15 projects under `Tests/` (one, `Tests.Infrastructure`, is a shared
fixture library, not a suite). By category:

| Category | Projects |
|---|---|
| Unit / logic | `BotRunner.Tests`, `WoWSharpClient.Tests`, `BloogBot.AI.Tests`, `PromptHandlingService.Tests`, `Systems.ServiceDefaults.Tests`, `WoWStateManagerUI.Tests` |
| Native / physics | `Navigation.Physics.Tests`, `PathfindingService.Tests` |
| Protocol / network | `WoWSharpClient.NetworkTests` |
| In-process bot | `ForegroundBotRunner.Tests` |
| Recorded replay | `RecordedTests.Shared.Tests`, `RecordedTests.PathingTests.Tests` |
| Simulation / load | `WoWSimulation`, `LoadTests` (`LoadTestHarness`) |
| Shared fixtures | `Tests.Infrastructure` (`BotServiceFixture`, `MangosServerFixture`, `LiveBotFixture`) |

## LiveValidation (Layer 4)

`Tests/BotRunner.Tests/LiveValidation/` drives **real bot behavior** against a
live MaNGOS server through `WoWStateManager`. It needs the full stack up
(realmd + mangosd + SOAP) and the StateManager running. Entry point:
[`../Tests/BotRunner.Tests/LiveValidation/LiveBotFixture.cs`](../Tests/BotRunner.Tests/LiveValidation/LiveBotFixture.cs).

Two rules from [`../CLAUDE.md`](../CLAUDE.md) bind every new live test:

- **Test isolation** — a test sets up world state, **declares an Activity**, and
  asserts on the bot's *behavior* (snapshot fields, Task-stack progression).
  Do **not** hand-construct an `ObjectiveMessage` and dispatch it in the test
  body — that remote-controls the bot and bypasses DecisionEngine/ActivityResolver.
- **Shodan is director-only** — Shodan is the production GM liaison / test
  director (SOAP setup like `.tele`/`.additem`). It is **never** a test subject;
  dispatch behavior to the dedicated test accounts (`TESTBOT1`, etc.). See
  [`security.md`](security.md).

## Test skip policy

A missing resource (no fishing pool, no node, no mob) is a **real failure** —
a detection/pathfinding/ObjectManager bug — not a reason to skip. Walking to
find resources *is* the test. Acceptable skips are limited to fixture readiness
(bot not connected) and known client bugs. Full policy in [`../CLAUDE.md`](../CLAUDE.md).

## Where to add a test

- Match the source project name: tests for `Foo` go in `Tests/Foo.Tests`.
- A test that needs the live server → `Tests/BotRunner.Tests/LiveValidation/`,
  following the isolation rule above.
- A test that verifies one **Action** shape in isolation belongs in a dedicated
  `Tests/BotRunner.Tests/ActionDispatch/` folder (per the CLAUDE.md convention),
  **not** in LiveValidation.
- IPC/protobuf-contract tests legitimately construct messages directly — those
  live under `Tests/BotRunner.Tests/IPC/` and the protocol test projects.

See also: [`testing/end-to-end-integration-test.md`](testing/end-to-end-integration-test.md)
and [`local-development.md`](local-development.md) for bringing the stack up.
