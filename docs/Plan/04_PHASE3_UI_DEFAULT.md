# Plan 04 — Phase 3: UI as Default Host

## Goal

`WoWStateManagerUI` is the solution's default startup project AND the
host for every test fixture. Tests subscribe to the same protobuf
summary stream the UI renders; the human watching the UI window during
a test run sees exactly what the test asserts on.

This phase is foundational because it changes the test-fixture model
for every subsequent phase. We do it now while the slot count is
manageable.

## Entry pre-requisite

Phase 1 + 2 in flight (UI panels render real data only when those
phases populate it; this phase just establishes the host shape).

## Exit criteria

- [ ] `WoWStateManagerUI` is the `WestworldOfWarcraft.sln` default
      startup project.
- [ ] `WoWStateManagerUI` spawns `WoWStateManager.exe` on launch
      (silent, port 9000 handshake per
      [`Spec/09_UI.md#startup-contract`](../Spec/09_UI.md#startup-contract)).
- [ ] `WoWStateManagerUIFixture` (new) lives at
      `Tests/Tests.Infrastructure/`. Every LiveValidation test class
      inherits from it (directly or transitively).
- [ ] `LiveBotFixture` migrates to inherit
      `WoWStateManagerUIFixture`. All existing LiveValidation tests
      pass without functional change.
- [ ] Test runs render telemetry through the same UI panels the
      operator uses. Optional "hidden-window" mode for headless CI.
- [ ] Failure-event screenshots land in
      `TestResults/LiveLogs/screenshots/<account>/<category>.png`,
      overwritten per run.
- [ ] WPF tests under `Tests/WoWStateManagerUI.Tests` cover the new
      fixture surface.
- [ ] Single-UI / single-connection enforcement — second UI instance
      blocked with a friendly message (per decision Q11).

## Slots

### S3.1 — Solution default startup project

- **Owner:** `monorepo-worker`
- **Status:** open
- **Owned paths:**
  - `WestworldOfWarcraft.sln`
  - `WWoWBot.code-workspace`
- **Goal:** F5 in VS launches the UI. Document in
  [`docs/DEVELOPMENT_GUIDE.md`](../DEVELOPMENT_GUIDE.md).

### S3.2 — UI startup contract

- **Owner:** `monorepo-worker`
- **Status:** open
- **Owned paths:**
  - `UI/WoWStateManagerUI/App.xaml.cs`
  - `UI/WoWStateManagerUI/StateManagerHost.cs` (new)
- **Goal:** UI checks port 9000; if not bound, spawns
  `WoWStateManager.exe` as a child process with redirected stdio.
  Single-instance lock prevents second UI on the same host.

### S3.3 — `IStateManagerClient` extraction

- **Owner:** `monorepo-worker`
- **Status:** open
- **Owned paths:**
  - `UI/WoWStateManagerUI/Clients/StateManagerClient.cs`
  - `UI/WoWStateManagerUI/Clients/IStateManagerClient.cs`
- **Goal:** Pull the protobuf IPC wrapper into an interface that both
  UI MVVM and the test fixture consume.

### S3.4 — `WoWStateManagerUIFixture`

- **Owner:** `monorepo-worker`
- **Status:** open
- **Depends on:** S3.2, S3.3
- **Owned paths:**
  - `Tests/Tests.Infrastructure/WoWStateManagerUIFixture.cs`
  - `Tests/WWoW.Tests.Infrastructure/WoWStateManagerUIFixture.cs`
- **Goal:** Test fixture that:
  1. Launches WoWStateManagerUI in hidden-window mode (configurable
     via `WWOW_TEST_SHOW_UI=1` to debug visually).
  2. Waits for StateManager handshake.
  3. Exposes the same `IStateManagerClient` the UI uses.
  4. Tears down both processes on disposal.

### S3.5 — Migrate `LiveBotFixture`

- **Owner:** `monorepo-worker`
- **Status:** open
- **Depends on:** S3.4
- **Owned paths:**
  - `Tests/BotRunner.Tests/LiveValidation/LiveBotFixture.cs`
- **Goal:** Inherit `WoWStateManagerUIFixture`. Internal API stays
  compatible so existing tests don't recompile.

### S3.6 — Single-connection enforcement

- **Owner:** `monorepo-worker`
- **Status:** open
- **Owned paths:**
  - `Services/WoWStateManager/Listeners/StateManagerSocketListener.cs`
- **Goal:** Only one UI client at a time. Second connection rejected
  with `handshake_mismatch` reason. Tests assert.

### S3.7 — Failure-event screenshot capture

- **Owner:** `monorepo-worker`
- **Status:** open
- **Owned paths:**
  - `Services/ForegroundBotRunner/Diagnostics/FailureScreenshotCapture.cs`
  - `Tests/BotRunner.Tests/LiveValidation/FailureScreenshotTests.cs`
- **Goal:** Hook into the failure-event pipeline (task_timeout,
  physics_parity_break, physics_stuck, bot_crash). On trigger, capture
  the FG window via WindowCapture, write to
  `TestResults/LiveLogs/screenshots/<account>/<category>.png`,
  overwriting any previous capture for that category. Background-only
  bots: no-op. Not extensive, just precise — purpose is agent triage
  of "why didn't that work?" without a human in the loop.

### S3.8 — UI integration test

- **Owner:** `monorepo-test-runner`
- **Status:** open
- **Depends on:** S3.1..S3.7
- **Goal:** Headless test that boots the UI, runs a 30-second snapshot
  of the test realm, asserts panels render non-empty data, then exits
  cleanly.

## Out of scope for Phase 3

- Long-term performance history panel — that needs the metrics
  collection from Phase 5.
- OnDemand activity request UI form — that's Phase 2 (S2.11), gated
  on the launcher landing.
- Multi-operator concurrency — single-connection per decision Q11.
- UI authentication — single-machine, no auth per decision Q9.
