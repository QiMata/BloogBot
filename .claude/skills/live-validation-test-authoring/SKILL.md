---
name: live-validation-test-authoring
description: Author a LiveValidation integration test that exercises the full StateManager → BotRunner loop against the live MaNGOS stack. Use when adding an end-to-end behavior test that drives an Activity and asserts on bot snapshots.
trigger: write a live test, LiveValidation, integration test, StateManager loop, bot behavior test, snapshot assertion, EnsureCleanSlateAsync, StageBotRunner, end-to-end bot test
---

# LiveValidation Test Authoring

## Goal

Add one integration test under `Tests/BotRunner.Tests/LiveValidation/` that sets up
world state, **declares an Activity**, lets `DecisionEngine`/`ActivityResolver` pick
the bot's behavior, and asserts on the bot's resulting `WoWActivitySnapshot`. The
test must verify *behavior*, not remote-control the bot — and must respect the
test-isolation and Shodan rules so it can run in parallel without contamination.

## Inputs

- The Activity to exercise and the observable outcome (snapshot field(s),
  task-stack progression, or captured `ObjectiveMessage` stream).
- The roster: dedicated test accounts (FG/BG, e.g. TESTBOT1/TESTBOT2 or
  category-specific siblings like GATHFG1/BG1) — **never** Shodan.
- Key files:
  - Fixture: `Tests/BotRunner.Tests/LiveValidation/LiveBotFixture.cs` and its
    partials — `.TestDirector.cs` (staging/GM setup), `.Snapshots.cs`
    (`GetSnapshotAsync`, `RefreshSnapshotsAsync`), `.Assertions.cs`,
    `.BotChat.cs`, `.GmCommands.cs`, `.ServerManagement.cs`, `.ShodanLoadout.cs`.
  - Multi-bot base: `Tests/BotRunner.Tests/LiveValidation/CoordinatorFixtureBase.cs`.
  - Collections: `LiveValidationCollection` (+ specialized `CombatArenaCollection`,
    `SingleBotValidationCollection`, `BgOnlyValidationCollection`).
  - The MANDATORY method pattern + key rules in `Tests/CLAUDE.md`.
- Legal entry points: `ResolveBotRunnerActionTargets(...)`,
  `StageBotRunner*Async(...)`, `EnsureCleanSlateAsync(...)`,
  `SendGmChatCommandAsync(...)` / `ExecuteGMCommandAsync(...)` (SOAP),
  `BotTeleportAsync(...)`, `WaitForNearbyUnitAsync(...)`.
- Area rules: `.github/instructions/tests.instructions.md`.

## Preconditions

- The MaNGOS stack is up (auth/world/MySQL + SOAP on 7878) and StateManager is
  reachable; otherwise the test must **skip on fixture-not-ready**, not on missing
  resources.
- You have read CLAUDE.md "Test Isolation Rules" and "Shodan: Production GM
  Liaison, Test Director". New tests declare an Activity and assert on snapshots;
  they do **not** construct `new ObjectiveMessage{...}` in the test body.
- `BotRunner.Tests` runs x86 (FG injection compat) → an x86 `Navigation.dll` is on
  the output path (`Bot/Release/net8.0/x86/Navigation.dll`).

## Procedure

1. **Create** `Tests/BotRunner.Tests/LiveValidation/<Feature>Tests.cs`,
   `[Collection(LiveValidationCollection.Name)]`, inject `LiveBotFixture` +
   `ITestOutputHelper`, call `_bot.SetOutput(output)` and guard with
   `global::Tests.Infrastructure.Skip.IfNot(_bot.IsReady, _bot.FailureReason ...)`.
2. **Method**: `[SkippableFact]` + `[Trait("Category","RequiresInfrastructure")]`,
   named `Feature_Scenario_ExpectedBehavior`.
3. **Resolve targets**: `var (fg, bg) = ...ResolveBotRunnerActionTargets(...)` —
   this throws if Shodan resolves as a subject (by design).
4. **Clean slate (MANDATORY)**: `await _bot.EnsureCleanSlateAsync(bg, "BG")`
   (revive → leave group → teleport to safe zone → `.gm off`). Repeat for FG if
   used.
5. **Stage world state**: spells/items/skills/level via
   `StageBotRunnerLoadoutAsync(...)`; GM setup via `SendGmChatCommandAsync` /
   `ExecuteGMCommandAsync`; position via `BotTeleportAsync`. Do GM-mode steps
   before clean-slate returns (it ends with `.gm off`).
6. **Declare the Activity** (today via the `AssignedActivity` string +
   `StageBotRunner*Async`; via `IActivity.Start(...)` once Phase-2 slot S2.0
   lands) and let the bot decide — do not dispatch raw Objectives.
7. **Assert on behavior**: `await _bot.RefreshSnapshotsAsync();
   var snap = await _bot.GetSnapshotAsync(bg);` then assert on snapshot fields
   (position is `snap.Player?.Unit?.GameObject?.Base?.Position`), task-stack
   progression, or the captured `ObjectiveMessage` stream. Use `Assert.*` /
   `Assert.Fail` — never `Skip.If` for a missing resource.
8. **No state leakage**: undo side effects (guild `.guild delete`, group
   `DISBAND_GROUP`, trade `DECLINE_TRADE`).

## Verification

- Integration run: `.\scripts\test-integration.ps1` (Layer 4) or
  `.\run-tests.ps1 -Layer 4`.
- Targeted:
  `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --filter "FullyQualifiedName~<Feature>Tests"`.
- Confirm the test **fails loudly** (not skips) when the bot can't find a world
  resource that the DB says exists — that is a real detection/pathing bug.
- Confirm it **skips** cleanly when the fixture isn't ready.

## Outputs

- New `Tests/BotRunner.Tests/LiveValidation/<Feature>Tests.cs`.
- Captured screenshots/state dumps on failure (fixture emits latest artifacts).
- `docs/TASKS.md` update if task-tracked.

## Failure modes and recovery

- **Remote-controlling the bot.** Constructing `new ObjectiveMessage{...}` and
  calling `SendActionAsync` in the test body bypasses DecisionEngine/
  ActivityResolver — it cannot catch regressions. Declare an Activity instead.
  (Single-Action shape checks belong in `Tests/BotRunner.Tests/ActionDispatch/`.)
- **Targeting Shodan.** Shodan is the GM Liaison / test director only; the fixture
  throws if you stage it as a subject. Use `ResolveBotRunnerActionTargets`.
- **Skipping for "resource not found".** Forbidden — that is a real failure. Walk
  to find resources; do not `.gobject add` synthetic ones.
- **Leaving `.gm on`.** Corrupts VMaNGOS UnitReaction bits (mobs won't aggro).
  Ensure `.gm off` (clean slate handles it).
- **Trusting DB reads for online characters.** They are stale — assert via
  StateManager snapshots, never direct MySQL.
- **Cross-test contamination.** Forgetting `EnsureCleanSlateAsync` breaks parallel
  isolation.

## Related skills

- [[botrunner-task-implementation]] — the Task whose behavior you are validating.
- [[coordinator-implementation]] — multi-bot Activities (use `CoordinatorFixtureBase`).
- [[loadout-template-authoring]] — stage gear/spells for the test.
- [[failure-reason-mapping]] — assert the expected failure classification.
- Reference: `Tests/CLAUDE.md`, CLAUDE.md "Test Isolation Rules".
