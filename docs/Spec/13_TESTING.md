# Spec 13 — Testing

## The contract

**Tests assert through StateManager.** Every behavior test:

1. Configures bots via per-test `*.config.json` referencing the
   `Westworld-Test` realm and named accounts/characters from
   [`Spec/16_REALMS_AND_ACCOUNTS.md`](16_REALMS_AND_ACCOUNTS.md).
2. **Launches `WoWStateManagerUI`** as the test fixture's host process.
   The UI silently starts StateManager and connects (per
   [`Spec/09_UI.md`](09_UI.md)). The fixture obtains the same
   `IStateManagerClient` interface the UI uses and subscribes to the
   same protobuf summary stream. Tests observe live stats exactly the
   way the operator does.
3. Drives state by sending `ObjectiveMessage`s through StateManager or by
   waiting for the mode handler to dispatch them.
4. Asserts on the resulting `WoWActivitySnapshot` polled from
   StateManager.

A test that asserts on internal BotRunner state without going through
the StateManager loop is wrong and must be rewritten.

Screenshots and JSON reports are **developer aids only** — they are not
the pass/fail signal. The assertions are.

## UI as the test host

Per the 2026-05-12 design:

- `WoWStateManagerUI.exe` is the **solution's default startup project**
  and **the test fixture's host process**.
- Tests instantiate `WoWStateManagerUIFixture` (new), which boots the
  UI in a hidden-window mode, lets it spawn StateManager, then exposes
  the `IStateManagerClient` to the test.
- The UI continues running through the test suite. Operators watching
  the UI window during a test run see the same metrics the test
  fixture is asserting against.
- This is also how the UI's connection / panels get exercised on every
  test run — no separate UI integration test required.

`LiveBotFixture` migrates to inherit from `WoWStateManagerUIFixture` so
that all existing LiveValidation tests automatically pick up the
UI-host model.

## Test layers

| Layer | Goal | Server needed? | Speed |
|---|---|---|---|
| Unit | Pure logic, no IPC | No | ms |
| Contract | Proto round-trip, schema | No | ms |
| Component | Service in isolation | No (mocks) | ms–s |
| Integration | Multi-service IPC, single bot | Yes (Docker) | s |
| LiveValidation | Full StateManager + bot + server | Yes | seconds–minutes |
| Load | 50–3000 bot scale | Yes | hours |

## Polling contract

Every live test waits on **predicates with tight timeouts**:

```csharp
await bot.WaitForSnapshotConditionAsync(
    accountName,
    snap => snap.LoadoutStatus == LoadoutStatus.Complete
         && snap.Position.MapId == expectedMap,
    timeout: TimeSpan.FromMinutes(2),
    progressLabel: "automated-loadout-and-travel",
    cancellationToken);
```

Required by every wait:

- A tight `timeout` (not the test-class-level timeout).
- A `progressLabel` for log/screenshot artifact naming.
- A **crash check** that fast-fails on `bot.IsCrashed`.
- A **disconnect check** that fast-fails on
  `snapshot.Lifecycle == Disconnected` unless the test expects it.
- A **final state dump** on failure (screenshot, JSON snapshot, log
  tail) for triage.

No `Thread.Sleep`. No fixed-iteration loops. No "wait N seconds and
check". Always condition.

## Test skip policy

Tests must NEVER skip for "resource not found":

- **Walking to find resources IS the test.** Wider search routes
  validate navigation.
- **Do NOT spawn resources** (no `.gobject add`, no synthetic nodes).
- **Do NOT use `Skip.If` for resource detection failures.** Use
  `Assert.True` / `Assert.Fail`.
- **Acceptable skips:** fixture readiness (bot not connected), known
  client bugs (with crash ID).
- **Not acceptable:** "no pool at Ratchet (respawn timer)", "no nodes
  spawned", "no mob found".

## FG/BG parity tests

Every action with both an FG path and a BG path has a parity test:

```csharp
[Theory]
[InlineData(BotExecutionMode.Foreground)]
[InlineData(BotExecutionMode.Background)]
public async Task SpellCast_HeroicStrike_LandsOnTarget(BotExecutionMode mode)
{
    // shared body asserts same snapshot outcomes for both modes
}
```

Recording-driven physics tests (`Tests/Navigation.Physics.Tests/`) use
FG recordings as authority and assert BG matches within tolerance.

## Shodan rules

Shodan is the production GM liaison + test director. Tests must:

- **Never dispatch `ObjectiveType.*` against the Shodan account.**
- **Never assert on Shodan's snapshot for behavior validation.**
- Use `LiveBotFixture.ResolveBotRunnerActionTargets()` to resolve the
  non-Shodan test bots.
- All `StageBotRunner*Async` helpers throw if Shodan is the target.
- Every Shodan-shaped test logs:
  `[ACTION-PLAN] SHODAN <category>: director only, no <feature> dispatch.`

## Live test fixture contract

`Tests/BotRunner.Tests/LiveValidation/LiveBotFixture.cs` is the
fixture used by LiveValidation tests:

- Owns lifecycle (bot launch, server check, cleanup).
- StateManager owns coordination (mode dispatch, snapshot ingest).
- BotRunner owns execution (behavior trees, IObjectManager calls).
- Object managers expose game state.

These boundaries are R6 in the monorepo CLAUDE.md and they are enforced
in fixture code via assertions.

## GM command policy

Tests use SOAP (port 7878) for GM commands. **Never edit MaNGOS MySQL
directly** for character/server mutation.

- `LiveBotFixture.ExecuteGMCommandAsync()` — SOAP.
- `LiveBotFixture.SendGmChatCommandAsync()` — via bot chat (Shodan).
- Read-only MySQL queries (e.g. `playercreateinfo_item`) are
  acceptable.
- **No `.gm on` in tests.** Account-level GM access only.
- `.reset` subcommands strip test state between runs:
  `.reset honor|level|spells|stats|talents|items|all`.

## Test categories (existing)

| Category | Project | Coverage |
|---|---|---|
| Basic Loop | BotRunner.Tests | Login, snapshot, teleport, level, units |
| Character Lifecycle | BotRunner.Tests | Create, items, death/revive |
| Combat | BotRunner.Tests | Melee/ranged/stop/distance |
| Consumables | BotRunner.Tests | Use item + buff check |
| Crafting | BotRunner.Tests | Learn + craft via packet path |
| Death/Corpse | BotRunner.Tests | Release/retrieve, ghost run |
| Economy | BotRunner.Tests | Bank, AH, mail, vendor buy/sell |
| Equipment | BotRunner.Tests | Equip/unequip |
| Fishing | BotRunner.Tests | Full fishing loop |
| Gathering | BotRunner.Tests | Mining/herbalism |
| Group | BotRunner.Tests | Invite + accept + cleanup |
| NPC Interaction | BotRunner.Tests | Vendor, trainer, flight master |
| Navigation | BotRunner.Tests | Short + city + cross-map |
| Quest | BotRunner.Tests | Add, complete, remove |
| Talent | BotRunner.Tests | Learn via GM, spell in snapshot |
| Loot | BotRunner.Tests | Kill → loot → verify inventory |
| Spell Cast | BotRunner.Tests | Heroic Strike on mob, verify HP delta |
| Buff Dismiss | BotRunner.Tests | Apply + dismiss |
| Movement parity | WoWSharpClient.Tests | 30+ recorded sessions |
| Physics | Navigation.Physics.Tests | Walkable, climb, jump, fall, swim |
| Packet handlers | WoWSharpClient.Tests | 937+ opcode tests |
| Pathfinding | PathfindingService.Tests | Path + route-pack |
| Recorded path replay | RecordedTests.PathingTests.Tests | Deterministic replay |
| Mock server | WoWSimulation.Tests | Mock MaNGOS |

## Required new test families (Phase 2+)

For every catalog activity row, there must be at least one
LiveValidation test that:

1. Configures a roster sufficient to fill the role template.
2. Submits the activity request via StateManager.
3. Polls until lifecycle reaches `Completed` or `Failed`.
4. Asserts:
   - Bots travelled to `TravelTarget`.
   - Group formed with the right composition.
   - Activity-specific success condition (e.g. dungeon final boss
     killed, BG match completed, fishing pool exhausted).
5. Asserts post-completion state (lease released, bots returned to
   progression).

These are added by the activity-family slots in
[`Plan/Activities/`](../Plan/Activities/).

## Existing code anchors

| Concept | File |
|---|---|
| Live fixture | `Tests/BotRunner.Tests/LiveValidation/LiveBotFixture.cs` |
| Test director helpers | `Tests/BotRunner.Tests/LiveValidation/LiveBotFixture.TestDirector.cs` |
| Live logs | `TestResults/LiveLogs/<test>.log` (overwritten per run, not git-tracked) |
| Latest results | `TestResults/latest/` |
| MaNGOS server fixture | `Tests/Tests.Infrastructure/MangosServerFixture.cs` |
| Shodan policy | `Tests/BotRunner.Tests/LiveValidation/docs/SHODAN_MIGRATION_INVENTORY.md` |
| Test patterns (monorepo) | [`../../docs/TEST_PATTERNS.md`](../../docs/TEST_PATTERNS.md) |
| Screenshots contract | [`../../docs/TEST_SCREENSHOTS.md`](../../docs/TEST_SCREENSHOTS.md) |
