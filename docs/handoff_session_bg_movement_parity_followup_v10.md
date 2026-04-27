# Handoff (followup v10): BG bot teleport double-fall — Streams 2E.1 + 2E.2 + Stream 4 (partial) closed; next is Stream 2E.3 (BG FALL_LAND emission gap) and Stream 4 BG cross-map baseline

> **How to use this:** copy/paste this file (top to bottom) into a fresh
> Claude Code session in `e:\repos\Westworld of Warcraft`. Read it in
> full before touching anything. Older context lives in
> [`handoff_session_bg_movement_parity.md`](handoff_session_bg_movement_parity.md)
> and the v1–v9 follow-ups
> ([v1](handoff_session_bg_movement_parity_followup.md),
> [v2](handoff_session_bg_movement_parity_followup_v2.md),
> [v3](handoff_session_bg_movement_parity_followup_v3.md),
> [v4](handoff_session_bg_movement_parity_followup_v4.md),
> [v5](handoff_session_bg_movement_parity_followup_v5.md),
> [v6](handoff_session_bg_movement_parity_followup_v6.md),
> [v7](handoff_session_bg_movement_parity_followup_v7.md),
> [v8](handoff_session_bg_movement_parity_followup_v8.md),
> [v9](handoff_session_bg_movement_parity_followup_v9.md)).

---

## Why this work exists (restated)

A third-party WoW client observing a BG bot after teleport reported the
falling animation playing **twice** — once on local prediction
immediately after teleport, then again when the authoritative position
update arrived showing the bot still mid-air.

**Current parity state (post-v10):**

- Streams 2A → 2D + 3 closed (see v9 handoff for details).
- **Stream 2E.1 closed (this session):** the spurious `CMSG_SET_ACTIVE_MOVER`
  on every BG teleport was traced to
  `RestoreLocalPlayerControlFromHydratedUpdate`'s early-return condition
  gating on `_isInControl && !_isBeingTeleported`. Same-map teleport sets
  `_isBeingTeleported=true` but never loses control, so the predicate fired
  even though WoW.exe never re-affirms active mover on same-map teleport.
  Fix: gate the early return on `_isInControl` alone (commit `3cc3891a`).
  Verified across 4 fresh BG live captures; zero `CMSG_SET_ACTIVE_MOVER`.
- **Stream 2E.2 closed (this session):** captured a 100-yard vertical-drop
  BG live baseline at
  `Tests/WoWSharpClient.Tests/Fixtures/post_teleport_packet_window/background_durotar_high_drop_baseline.json`.
  Even at 10x the standard drop height, BG never emits `MSG_MOVE_FALL_LAND`
  in the 2.5s window. Outbound stream is identical to the 10y baseline:
  `[MSG_MOVE_HEARTBEAT, MSG_MOVE_TELEPORT_ACK]`. This rules out
  "drop too small" — the FALL_LAND emission gap is a real BG physics bug,
  not an artifact of test height. Pinned by parity test
  `BackgroundHighDropBaseline_DoesNotEmitFallLand_PinsBgPhysicsEmissionGap`
  as a CURRENT-BUG oracle (commit `6173456b`).
- **Stream 4 partially closed:** `IsInboundTeleportTrigger` extended in
  both `ForegroundPostTeleportWindowRecorder` and
  `BackgroundPostTeleportWindowRecorder` to also fire on `SMSG_NEW_WORLD`
  and `SMSG_TRANSFER_PENDING` (commit `550a7a21`). FG cross-map baseline
  captured live (Org → Ironforge) and pinned by parity test
  `ForegroundCrossMapBaseline_PinsTransferPendingNewWorldShape` (commit
  `e6da1a89`). **BG cross-map baseline still TODO.**
- **Stream 2E.3 OPEN:** the deeper investigation into *why* live BG never
  emits `MSG_MOVE_FALL_LAND` even on a 100y drop. Hypothesis below.

---

## Done since v9 (this session)

Run `git log --oneline -10` for the actual tip. Recent commits on `main`:

1. **`3cc3891a` — `fix(bg-movement-parity): drop spurious CMSG_SET_ACTIVE_MOVER on same-map teleport (Stream 2E.1)`**
   - Single-line fix at
     [`Exports/WoWSharpClient/WoWSharpObjectManager.Network.cs:758`](../Exports/WoWSharpClient/WoWSharpObjectManager.Network.cs):
     dropped `&& !_isBeingTeleported` from the early-return condition in
     `RestoreLocalPlayerControlFromHydratedUpdate`.
   - The existing
     `LocalPlayerUpdate_WithoutPriorAdd_TakesControlAndClearsTransition`
     unit test continues to pass (it sets `_isInControl=false`, which still
     triggers the SET_ACTIVE_MOVER fire).
2. **`2c42019c` — `test(bg-movement-parity): refresh BG baseline + parity test post-Stream-2E.1 (no SET_ACTIVE_MOVER)`**
   - Re-captured the BG live vertical-drop baseline against the
     Stream 2E.1 fix; promoted as the new
     `background_durotar_vertical_drop_baseline.json`.
   - Updated `BackgroundBaseline_ReportsLiveCapturedTeleportPacketSequence`:
     dropped the SET_ACTIVE_MOVER assertions, ADDED
     `Assert.DoesNotContain(... CMSG_SET_ACTIVE_MOVER)` as a regression guard.
3. **`6173456b` — `test(bg-movement-parity): pin BG high-drop FALL_LAND emission gap (Stream 2E.2)`**
   - Captured a 100y vertical-drop BG fixture as
     `background_durotar_high_drop_baseline.json`.
   - Added `BackgroundHighDropBaseline_DoesNotEmitFallLand_PinsBgPhysicsEmissionGap`
     parity test that asserts the gap (`Assert.DoesNotContain(... MSG_MOVE_FALL_LAND)`).
     This is a CURRENT-BUG oracle: when Stream 2E.3 fixes the gap, the test
     will fail and force the author to update the assertion.
4. **`550a7a21` — `feat(bg-diag,fg-diag): trigger post-teleport recorders on SMSG_NEW_WORLD / SMSG_TRANSFER_PENDING (Stream 4)`**
   - Both recorders' `IsInboundTeleportTrigger` now also matches
     `Opcode.SMSG_NEW_WORLD` and `Opcode.SMSG_TRANSFER_PENDING` so cross-map
     teleport scenarios capture properly.
5. **`e6da1a89` — `test(fg-bg-parity): pin FG cross-map (Org -> Ironforge) baseline (Stream 4)`**
   - Captured FG cross-map baseline at
     `foreground_kalimdor_to_ek_cross_map_baseline.json` via the existing
     `Foreground_CrossMapTeleport_CapturesWorldportAckWhenCorpusEnabled` test
     under `WWOW_CAPTURE_POST_TELEPORT_WINDOW=1` + `WWOW_CAPTURE_ACK_CORPUS=1`.
   - Added `ForegroundCrossMapBaseline_PinsTransferPendingNewWorldShape`
     parity test asserting the cross-map handshake shape.

6. **(this commit)** — v10 handoff doc.

Total: 6 parity tests now pass (was 4 in v9). Suite total is 1614 (passed) /
1 (skipped) for `Tests/WoWSharpClient.Tests`.

---

## Stream 2E.3 (PRIMARY OPEN WORK) — Investigate BG FALL_LAND emission gap

### What we know

Live BG never emits `MSG_MOVE_FALL_LAND` in the 2.5s post-teleport window
for either a 10y or 100y vertical drop. WoW.exe (FG) emits it ~1.27s after
the teleport for the 10y case. The synthetic parity test
`Background_AfterTeleportTrigger_OutboundStream_StructurallyMatchesForegroundBaseline`
DOES emit FALL_LAND because it forces a continuous fall via
`NativeLocalPhysics.TestStepOverride`. So the code path that would emit
FALL_LAND exists in `MovementController` and works under controlled inputs;
something in the live runtime path bypasses it.

### Working hypothesis

`NativeLocalPhysics.PhysicsStepV2` (the C++ engine) is the binary-parity
authority. After `NotifyTeleportIncoming` resets the controller and the
player position is written to the teleport destination, the very first
`MovementController.Update` call invokes `RunPhysics(deltaSec)` which calls
into the C++ engine.

**The hypothesis:** the C++ engine has a "fall ground search" that, when
asked for physics with `velocity=0` and `MoveFlags=0` at a position with
ground far below, may snap the player straight to the ground (e.g., via a
long downward raycast) instead of returning `FALLINGFAR + slightly-lower-Z`.
If this happens, `output.MovementFlags & FALLINGFAR == 0` and
`ApplyPhysicsResult` clears the flag. `_player.MovementFlags` never sees
`FALLINGFAR` set, so `DetermineOpcode(current=NONE, previous=NONE)` returns
`HEARTBEAT` rather than `FALL_LAND` — no FALL_LAND ever emits.

WoW.exe presumably runs the same C++ engine but feeds it a different first
frame (e.g., it may set `MOVEFLAG_FALLINGFAR` immediately on teleport into
mid-air, before the first physics step), so the engine knows to *continue*
falling rather than snap to ground.

### Concrete next steps

1. **Add diagnostic logging in `MovementController.Update`** at three points:
   - Right after `RunPhysics(deltaSec)` returns: log
     `physicsResult.MovementFlags`, `physicsResult.NewPosZ`,
     `physicsResult.GroundZ`, `_player.Position.Z` for the first 5 frames
     after `Reset()`.
   - Right after `ApplyPhysicsResult`: log
     `_player.MovementFlags` and `_velocity.Z`.
   - Inside the `_needsGroundSnap` block (line 325): log
     `_groundSnapFrames`, `stillFalling`, `_player.MovementFlags` each frame.
   - Re-run the BG vertical-drop capture (Z+10 or Z+100) with logging
     enabled. The diag log at `Bot/Release/net8.0/logs/botrunner_ECONBG1.diag.log`
     captures Serilog Information+ output.
2. **Inspect what `PhysicsStepV2` returns for a freshly-teleported airborne
   player.** If `output.MovementFlags & FALLINGFAR == 0` even when
   `output.NewPosZ < input.Z`, that confirms the C++ engine is auto-snapping.
3. **Check what FG (WoW.exe) does on the first physics tick after teleport.**
   The `Tests/WoWSharpClient.Tests/Fixtures/...vertical_drop_baseline.json`
   only shows packets, not physics state. We may need a separate FG
   instrumentation that records what `MovementFlags` get set on the first
   tick post-teleport. The reference is the disasm at
   [`docs/physics/state_teleport.md`](physics/state_teleport.md) — look for
   what WoW.exe does between the inbound `MSG_MOVE_TELEPORT_ACK` (server
   notification) and the first heartbeat fire.
4. **If the C++ engine is the cause:** options are
   (a) pre-set `_player.MovementFlags = MOVEFLAG_FALLINGFAR` in `Reset()`
       when the teleport drops the player into airborne territory (would
       require a "is ground at teleport Z?" probe in `Reset()`), so the
       first physics tick continues the fall rather than snapping;
   (b) bypass the `_needsGroundSnap` snap behavior when the destination Z
       is far above ground (let normal gravity-based physics run);
   (c) modify `NativeLocalPhysics.PhysicsStepV2` (the C++ engine) to never
       snap from airborne — but this is the binary-parity authority, so
       changing it requires binary-evidence justification.
5. **Verify the fix end-to-end.** After landing the chosen fix, re-run
   `Background_VerticalDropTeleport_CapturesPostTeleportWindow` and confirm
   the BG fixture now contains `MSG_MOVE_FALL_LAND`. Update both
   `BackgroundBaseline_ReportsLiveCapturedTeleportPacketSequence` (drop the
   `BackgroundHighDropBaseline_DoesNotEmitFallLand_PinsBgPhysicsEmissionGap`
   assertion that the gap exists) and the high-drop baseline test to
   require FALL_LAND. Also re-test with a third-party-client observer to
   verify the double-fall animation no longer appears.

---

## Stream 4 — Other teleport scenarios (partial — BG cross-map remains)

**Done:** FG cross-map baseline captured (`foreground_kalimdor_to_ek_cross_map_baseline.json`)
+ parity test (`ForegroundCrossMapBaseline_PinsTransferPendingNewWorldShape`).

**Remaining:**

1. **BG cross-map baseline.** The existing
   `Background_VerticalDropTeleport_CapturesPostTeleportWindow` only does
   same-map teleport. Either (a) extend that test with a cross-map hop
   sub-step before returning to Org, OR (b) add a parallel
   `Background_CrossMapTeleport_CapturesPostTeleportWindow` that mirrors
   the FG one (Org → Ironforge → Org). Capture against
   `WWOW_CAPTURE_BG_POST_TELEPORT_WINDOW=1`, promote the cross-map fixture
   as `background_kalimdor_to_ek_cross_map_baseline.json`, add a
   `BackgroundCrossMapBaseline_*` parity test that pins the BG-today shape.
   **Note:** because the recorder fires on `SMSG_TRANSFER_PENDING` /
   `SMSG_NEW_WORLD` now, the existing test's Org clean-slate teleport
   *would not* trigger a cross-map capture (Org → Org is same-map). Need a
   genuine cross-map hop.

2. **Transport mounting (zeppelin) baselines** (FG + BG). No existing
   live test stages a zeppelin board — would need a new test. Likely uses
   `MSG_MOVE_START_FORWARD` + transport packets, not a teleport opcode,
   so the recorder may not fire. Investigate first whether
   `SMSG_FORCE_MOVE_*` or transport-attach opcodes need to be added to
   `IsInboundTeleportTrigger`.

3. **Knockback baselines** (FG + BG). Trigger via `.knockback` GM command
   on the BG bot. Knockback uses `SMSG_MOVE_KNOCK_BACK` (see
   `WoWSharpObjectManager.TryConsumePendingKnockback` and the
   `MovementController` knockback path at line 209). Likely needs
   `SMSG_MOVE_KNOCK_BACK` added to `IsInboundTeleportTrigger`.

4. **Window duration / WORLDPORT_ACK capture.** The current FG cross-map
   capture truncates at the SMSG_TRANSFER_PENDING handshake; the post-load
   `MSG_MOVE_WORLDPORT_ACK` is outside the 2.5s window because WoW.exe
   pauses packet processing during the map load. Two options:
   (a) Bump `WWOW_POST_TELEPORT_WINDOW_DURATION_MS` env var (if it exists
       — check `ForegroundPostTeleportWindowRecorder.ResolveWindowDurationMs`)
       to 5000ms or higher.
   (b) Add a SECOND recorder (or extend the existing one) that ALSO opens
       a window on outbound `MSG_MOVE_WORLDPORT_ACK` so the post-load
       packet sequence gets its own dedicated capture.

---

## Hard rules (DO NOT VIOLATE)

- **R1 — No blind sequences/counters/timing hacks.** Gate on snapshot signal.
- **R2 — Poll snapshots, don't sleep.** Use `WaitForSnapshotConditionAsync(...)`.
- **R3 — Fail fast.** Per-milestone tight timeouts; `onClientCrashCheck` everywhere.
- **R4 — No silent exception swallowing.** Log warnings with context.
- **R5 — Fixture owns lifecycle, StateManager owns coordination, BotRunner owns execution.**
- **R6 — GM/admin commands must be asserted.** Use `AssertGMCommandSucceededAsync` etc.
- **R7 — Cross-test state must be reset.** `EnsureCleanSlateAsync` at test start.
- **R8 — x86 vs x64.** ForegroundBotRunner=x86. BG+StateManager+most tests=x64.
- **No background agents.** `run_in_background: true` is forbidden.
- **Single session only.** Auto-compaction handles context.
- **Commit and push frequently.** Every logical unit of work.
- **Shodan is GM-liaison + setup-only.** Never dispatch behavior actions to Shodan. Don't weaken `ResolveBotRunnerActionTargets`.
- **No `.gobject add`, no synthetic node spawns.** Test against natural world state.
- **Live MaNGOS via Docker is always running** — `docker ps` to confirm.
- **No MySQL mutations.** SOAP / bot chat for all game-state changes.
- **No `.learn all_myclass` / `.learn all_myspells`.** Always teach by explicit numeric spell ID.
- **Don't run `dotnet test --filter "Category!=RequiresInfrastructure"`** as a "broad unit suite". Use positive filters or per-project.
- **When you locally flip a test for verification, REVERT IT before committing** unless the flip is itself the commit.
- **Read `snapshot.RecentChatMessages` BEFORE diagnosing.** Server-side errors flow back through the snapshot.
- **Binary parity is THE rule for movement/physics.** When the managed code is more conservative or more aggressive than WoW.exe, that's a parity bug — fix the managed side, then update the test that pinned the divergence.
- **NEVER add `_needsGroundSnap` (or any physics-tick state) to the teleport ACK readiness gates.** Per [`docs/physics/state_teleport.md`](physics/state_teleport.md) the binary gates only on `0x468570`. Re-introducing the gate will resurrect the third-party-client double-fall regression that `eda32b09` fixed.
- **NEVER re-introduce the `!_needsGroundSnap &&` guard at MovementController.cs:379.** The FG fixture proves WoW.exe broadcasts during the snap window; suppressing those packets was the original Stream 2B regression.
- **NEVER reorder `DetermineOpcode` to put the MOVE_STOP rule before the FALL_LAND rules.** The FG fixture proves landing wins over stop for `FALLINGFAR/JUMPING → grounded` transitions.
- **NEVER remove or weaken the `NotifyExternalPacketSent` cadence-suppression call from `TryFlushPendingTeleportAck`.** Removing it resurrects the +1 HB Stream 2C regression. The FG fixture proves WoW.exe leaves the 500ms post-ACK window silent.
- **NEVER drop the `BackgroundPostTeleportWindowRecorder` Start() call from `BackgroundBotWorker`** when `WWOW_CAPTURE_BG_POST_TELEPORT_WINDOW=1`. The BG-side capture infrastructure depends on it.
- **NEVER re-introduce the `&& !_isBeingTeleported` predicate in `RestoreLocalPlayerControlFromHydratedUpdate`'s early-return condition** at `WoWSharpObjectManager.Network.cs:758`. The current early-return condition is `if (_isInControl) return;` — adding `&& !_isBeingTeleported` resurrects the spurious `CMSG_SET_ACTIVE_MOVER`-on-every-teleport regression that Stream 2E.1 closed. Pinned by `BackgroundBaseline_ReportsLiveCapturedTeleportPacketSequence` and `BackgroundHighDropBaseline_DoesNotEmitFallLand_PinsBgPhysicsEmissionGap` (both have `Assert.DoesNotContain(... CMSG_SET_ACTIVE_MOVER)`).
- **Kill WoW.exe before building.** FG injects DLLs into WoW.exe from the build output; a running WoW.exe locks them and causes MSB3027. `tasklist //FI "IMAGENAME eq WoW.exe" //FO LIST` then `taskkill //F //PID <pid>` — kill only YOUR PIDs (per the process safety rules; never blanket-kill).

### Bash CWD note

`bash` calls in this harness do **not** persist `cd`. Use absolute
paths or chain: `cd "e:/repos/Westworld of Warcraft" && git ...`.

### Docker sub-shell note

Calling `docker exec mangosd /bin/sh -c "..."` from this harness's
bash mangles `/...` paths. Either prefix `MSYS_NO_PATHCONV=1` or
invoke the binary directly:
`docker exec mangosd tail -50 /opt/vmangos/storage/logs/Server.log`.

### Live BG capture env vars

For `Background_VerticalDropTeleport_CapturesPostTeleportWindow`:
- `WWOW_ENABLE_RECORDING_ARTIFACTS=1` (master switch)
- `WWOW_CAPTURE_BG_POST_TELEPORT_WINDOW=1` (BG-specific)
- `WWOW_REPO_ROOT="e:/repos/Westworld of Warcraft"` (output dir resolution)
  — without this, the test fails at `Assert.NotNull(windowDir)`.

For `Foreground_CrossMapTeleport_CapturesWorldportAckWhenCorpusEnabled`:
- `WWOW_ENABLE_RECORDING_ARTIFACTS=1`
- `WWOW_CAPTURE_POST_TELEPORT_WINDOW=1` (FG-specific)
- `WWOW_CAPTURE_ACK_CORPUS=1` (required by the test for ACK fixture path)
- `WWOW_REPO_ROOT="e:/repos/Westworld of Warcraft"`

---

## Working agreements with the user (carry over)

- The user prefers concise responses. State decisions and changes; don't narrate deliberation.
- The user expects you to keep iterating without asking permission for each phase. Only ask if you genuinely need a decision they haven't already given.
- For risky actions (force push, deleting branches, mass file deletion, schema migrations), always confirm. Code edits and small commits are local/reversible — proceed.
- Integration tests run against the always-live Docker MaNGOS stack. Confirm `docker ps` once at session start; don't keep re-checking.

---

## Starter checklist

1. `git status && git fetch origin && git log --oneline -10` — confirm
   you're on `main`, working tree clean. Tip should be the v10 doc commit
   landing on top of `e6da1a89 test(fg-bg-parity): pin FG cross-map (Org
   -> Ironforge) baseline (Stream 4)`.
2. `docker ps` — confirm `mangosd`, `realmd`, `pathfinding-service`,
   `maria-db` healthy.
3. Read in order:
   - This handoff (top to bottom).
   - [`Tests/WoWSharpClient.Tests/Fixtures/post_teleport_packet_window/foreground_durotar_vertical_drop_baseline.json`](../Tests/WoWSharpClient.Tests/Fixtures/post_teleport_packet_window/foreground_durotar_vertical_drop_baseline.json)
   - [`Tests/WoWSharpClient.Tests/Fixtures/post_teleport_packet_window/background_durotar_vertical_drop_baseline.json`](../Tests/WoWSharpClient.Tests/Fixtures/post_teleport_packet_window/background_durotar_vertical_drop_baseline.json)
     (post-Stream-2E.1 shape — no SET_ACTIVE_MOVER)
   - [`Tests/WoWSharpClient.Tests/Fixtures/post_teleport_packet_window/background_durotar_high_drop_baseline.json`](../Tests/WoWSharpClient.Tests/Fixtures/post_teleport_packet_window/background_durotar_high_drop_baseline.json)
     (Stream 2E.2 — proves no FALL_LAND even at 100y)
   - [`Tests/WoWSharpClient.Tests/Fixtures/post_teleport_packet_window/foreground_kalimdor_to_ek_cross_map_baseline.json`](../Tests/WoWSharpClient.Tests/Fixtures/post_teleport_packet_window/foreground_kalimdor_to_ek_cross_map_baseline.json)
     (Stream 4 — FG cross-map oracle)
   - [`Tests/WoWSharpClient.Tests/Parity/PostTeleportPacketWindowParityTests.cs`](../Tests/WoWSharpClient.Tests/Parity/PostTeleportPacketWindowParityTests.cs)
     — 6 parity tests (was 4 in v9).
   - [`Exports/WoWSharpClient/WoWSharpObjectManager.Network.cs`](../Exports/WoWSharpClient/WoWSharpObjectManager.Network.cs)
     — `RestoreLocalPlayerControlFromHydratedUpdate` (line 747); the
     Stream 2E.1 fix is at line 758.
   - [`Exports/WoWSharpClient/Movement/MovementController.cs`](../Exports/WoWSharpClient/Movement/MovementController.cs)
     — `Update()` (line 184), `_needsGroundSnap` block (line 325),
     `ApplyPhysicsResult` (line 803), `DetermineOpcode` (line 1231),
     `Reset` (line 1388). Stream 2E.3 investigation lives here.
   - [`Exports/WoWSharpClient/Movement/NativeLocalPhysics.cs`](../Exports/WoWSharpClient/Movement/NativeLocalPhysics.cs) +
     [`Exports/WoWSharpClient/Movement/NativePhysicsInterop.cs`](../Exports/WoWSharpClient/Movement/NativePhysicsInterop.cs)
     — the C++ engine call boundary; `PhysicsStepV2` is the binary-parity
     authority.
   - [`Services/BackgroundBotRunner/Diagnostics/BackgroundPostTeleportWindowRecorder.cs`](../Services/BackgroundBotRunner/Diagnostics/BackgroundPostTeleportWindowRecorder.cs)
     and [`Services/ForegroundBotRunner/Diagnostics/ForegroundPostTeleportWindowRecorder.cs`](../Services/ForegroundBotRunner/Diagnostics/ForegroundPostTeleportWindowRecorder.cs)
     (Stream 4 trigger extension at line 242 and 241 respectively).
   - [`docs/physics/state_teleport.md`](physics/state_teleport.md),
     [`docs/physics/bg_movement_parity_audit.md`](physics/bg_movement_parity_audit.md)
     (currently mentions 2B + 2C — needs an update for 2D + 2E).
   - [`memory/movement_physics.md`](C:/Users/lrhod/.claude/projects/e--repos-Westworld-of-Warcraft/memory/movement_physics.md).
   - [`CLAUDE.md`](../CLAUDE.md) and per-repo `CLAUDE.md` for repo-wide rules.
   - [`memory/MEMORY.md`](C:/Users/lrhod/.claude/projects/e--repos-Westworld-of-Warcraft/memory/MEMORY.md).
4. **Open work, in priority order:**
   - **Stream 2E.3** — investigate why live BG never emits
     `MSG_MOVE_FALL_LAND`. Concrete first steps in the Stream 2E.3 section
     above. Add diagnostic logging in `MovementController.Update`,
     re-run live BG capture, inspect what `PhysicsStepV2` returns for a
     freshly-teleported airborne player.
   - **Stream 4 — BG cross-map baseline.** Add a BG cross-map capture
     test, promote the fixture as
     `background_kalimdor_to_ek_cross_map_baseline.json`, add a
     `BackgroundCrossMapBaseline_*` parity test.
   - **Stream 4 — transport / knockback baselines.** Lower priority;
     extend recorder triggers as needed (likely
     `SMSG_MOVE_KNOCK_BACK` for knockback, transport-attach opcodes for
     mounting).
   - **Window duration / WORLDPORT_ACK capture extension.** Decide
     between bumping window duration vs adding a second recorder hook.
5. Commit and push after each unit. Don't accumulate.

---

## When you near context exhaustion

Write the next followup handoff at
`docs/handoff_session_bg_movement_parity_followup_v11.md`,
self-contained as this one is. Include:

1. Concise restatement of the goal.
2. Current commit hash and a short summary of what's been done since
   this prompt was written.
3. The remaining tasks, with completed work moved to a "Done" appendix
   or referenced via prior handoff.
4. Any new blockers, surprises, or design pivots.
5. Repeat all the **Hard rules** above.
6. Repeat **this** "When you near context exhaustion" instruction so
   the next agent does the same.

The user copies/pastes the latest handoff into a fresh session.
**Make it self-contained** — assume the next agent has zero memory.
