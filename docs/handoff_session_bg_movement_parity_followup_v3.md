# Handoff (followup v3): BG bot teleport double-fall + movement parity audit

> **How to use this:** copy/paste this file (top to bottom) into a fresh
> Claude Code session in `e:\repos\Westworld of Warcraft`. Read it in
> full before touching anything. Older context lives in
> [`handoff_session_bg_movement_parity.md`](handoff_session_bg_movement_parity.md)
> (the original), [`handoff_session_bg_movement_parity_followup.md`](handoff_session_bg_movement_parity_followup.md) (v1),
> and [`handoff_session_bg_movement_parity_followup_v2.md`](handoff_session_bg_movement_parity_followup_v2.md) (v2).
> Read those if anything below is unclear.

---

## Why this work exists (restated)

A third-party WoW client observing a BG bot after teleport sees the
falling animation play **twice** — once on local prediction immediately
after the teleport, then a second time when the authoritative position
update arrives showing the bot still in mid-air. Diagnosis (frozen at
v2): two contributing pieces caused a silent broadcast window
post-teleport while the BG bot ground-snapped:

1. `TryFlushPendingTeleportAck` held the outbound `MSG_MOVE_TELEPORT_ACK`
   while `_needsGroundSnap` was true (binary-parity violation per
   `docs/physics/state_teleport.md`).
2. `MovementController:379` suppressed all outbound heartbeats during
   the same ground-snap window (added in commit `49915f62` to keep
   transient `FALLINGFAR` flags out of recorded traces).

Track 1 — fix #1, the binary-parity violation — was the v3 deliverable.

---

## Done since v2

Run `git log --oneline -8` for the actual tip. The two new commits on
top of `46536694` are:

1. **`eda32b09` — `fix(bg-movement): drop _needsGroundSnap gate from MSG_MOVE_TELEPORT_ACK flush`**
   - [`Exports/WoWSharpClient/WoWSharpObjectManager.Movement.cs:734-748`](../Exports/WoWSharpClient/WoWSharpObjectManager.Movement.cs):
     removes `|| _movementController.NeedsGroundSnap` from
     `TryFlushPendingTeleportAck` per
     `docs/physics/state_teleport.md`.
   - [`Tests/WoWSharpClient.Tests/ObjectManagerWorldSessionTests.cs`](../Tests/WoWSharpClient.Tests/ObjectManagerWorldSessionTests.cs):
     renames the test that pinned the divergence to
     `TryFlushPendingTeleportAck_WaitsForUpdates_ButNotGroundSnapNorSceneData`,
     flips it to assert the ACK fires while `_needsGroundSnap == true`
     once readiness gates pass. Adds companion test
     `TryFlushPendingTeleportAck_FiresWhenGroundSnapCleared`.
   - [`Tests/WoWSharpClient.Tests/Parity/StateMachineParityTests.cs:50-89`](../Tests/WoWSharpClient.Tests/Parity/StateMachineParityTests.cs):
     renames `MoveTeleport_AckWaitsForGroundSnap_ButNotSceneData` →
     `MoveTeleport_AckFiresAfterControlGrant_RegardlessOfGroundSnapOrSceneData`;
     drops the `MarkTeleportGroundSnapResolved()` prerequisite.
   - [`Tests/WoWSharpClient.Tests/Parity/PacketFlowParityTests.cs:175-211`](../Tests/WoWSharpClient.Tests/Parity/PacketFlowParityTests.cs):
     drops the redundant `MarkTeleportGroundSnapResolved()` call from
     the round-trip test.
   - 204/204 unit tests in the WoWSharpClient parity suite pass; the
     live regression test
     `BgPostTeleportStabilizationTests.BgBot_TeleportAboveGround_FallingFlagsClearAndPositionStabilizesWithinBound`
     also passes (1m 37s).

2. **`ebff10a4` — `test(bg-movement): pin queue-first deferred-ACK contract for all 5 remaining speed-change variants`**
   - [`Tests/WoWSharpClient.Tests/Parity/PacketFlowParityTests.cs`](../Tests/WoWSharpClient.Tests/Parity/PacketFlowParityTests.cs):
     adds `[Theory] ForceSpeedChangeFamily_QueuesDeferredAck_ThenFlushesWithUpdatedState`
     covering RUN_BACK, SWIM, SWIM_BACK, WALK, TURN_RATE — the 5 speed
     variants the original `ForceRunSpeedChange_*` test didn't cover.
   - [`Tests/WoWSharpClient.Tests/Parity/PacketFlowTraceFixture.cs:213-218`](../Tests/WoWSharpClient.Tests/Parity/PacketFlowTraceFixture.cs):
     wires the 5 missing inbound opcodes into the trace fixture's
     handler resolver.
   - 243/243 parity tests pass after this addition.

Audit deliverable, also new in this session:

3. **[`docs/physics/bg_movement_parity_audit.md`](physics/bg_movement_parity_audit.md)**
   — per-opcode pass/fail status for every BG-side outbound movement
   packet, every flag-toggle ACK, every speed-change ACK, every
   integrator constant, with VA citations and test references. Single
   divergence found and fixed (Track 1); everything else either passes
   binary parity outright or is partially pinned with no observed
   regression.

---

## Status of the original task list

| Task | Status |
|---|---|
| **Task A** — Pin Shodan character name | Done in v1 (commit `be6331fa`) |
| **Task B** — Teleport ACK byte-layout tests | Done in v1 (commit `1adf5096`) |
| **Task B-fix step 4** — Snapshot-side regression test | Done in v2 (commit `23d54795`) |
| **Task B-fix Track 1** — Drop ground-snap gate from ACK flush | Done in v3 (commit `eda32b09`) |
| **Task C** — Broader BG vs WoW.exe movement parity audit | Documented in v3 (`docs/physics/bg_movement_parity_audit.md`); single divergence found and fixed; 5-variant speed-change test added (commit `ebff10a4`) |

---

## What is still open (for the next session)

### Observer-side visual confirmation (still required)

Track 1 is grounded in `state_teleport.md` and the parity unit suite,
but neither the unit tests nor the live regression test asserts on a
**third-party WoW client's** rendered animation. The next session
should — if and only if a third client can be logged in nearby —
issue a teleport via the live regression test setup and visually
confirm the fall plays once. If you can't run an observer client,
note the blocker rather than declaring this closed.

If Track 1 alone turns out to be insufficient (i.e. the double-fall
still reproduces on the observer side), apply Option B from the v1
followup: at the start of the post-teleport ground snap, send one
corrective `MSG_MOVE_HEARTBEAT` with `MOVEFLAG_NONE` at the teleport
target, then keep the existing transient-FALLINGFAR suppression for
the rest of the snap. Update the parity tests in
`Tests/WoWSharpClient.Tests/Movement/MovementControllerTests.cs` that
currently `Assert.Empty(_sentPackets)` during the snap to allow that
single corrective packet. Commit separately.

### Movement-flag-toggle timing tests (PARTIAL in audit)

Layout fixtures exist in `AckBinaryParityTests.ForceMoveAckFixtures`
for `CMSG_MOVE_WATER_WALK_ACK`, `CMSG_MOVE_HOVER_ACK`, and
`CMSG_MOVE_FEATHER_FALL_ACK`, and the dispatch routes through the
same `QueueDeferredMovementChange` helper that
`PacketFlowParityTests` already covers for speed/root. But there is
no per-opcode timing test that asserts the queue-first behaviour for
the toggle pairs. If you have spare cycles, add a parametrized theory
covering `WATER_WALK / LAND_WALK`, `SET_HOVER / UNSET_HOVER`, and
`FEATHER_FALL / NORMAL_FALL` similar to the speed-change theory I
added in `ebff10a4`. Commit separately. This is regression
prevention only — no known divergence.

### Optional follow-up: narrow the heartbeat suppression

The audit didn't change `MovementController:379`. If the observer
visually confirms the fall *still* plays twice after Track 1, narrow
that suppression to a single corrective heartbeat per Option B above.
Don't apply this preemptively — it's a behaviour change the audit
doesn't justify on its own.

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
- **Binary parity is THE rule for movement/physics.** When the managed code is more conservative than WoW.exe (e.g. an extra readiness gate), that's a parity bug — fix the managed side, then update the test that pinned the divergence.
- **NEVER add `_needsGroundSnap` (or any physics-tick state) to the teleport ACK readiness gates.** Per `docs/physics/state_teleport.md` the binary gates only on `0x468570`. Re-introducing the gate will resurrect the third-party-client double-fall regression that `eda32b09` fixed.

### Bash CWD note

`bash` calls in this harness do **not** persist `cd`. Use absolute
paths or chain: `cd "e:/repos/Westworld of Warcraft" && git ...`.

### Docker sub-shell note

Calling `docker exec mangosd /bin/sh -c "..."` from this harness's
bash mangles `/...` paths. Either prefix `MSYS_NO_PATHCONV=1` or
invoke the binary directly:
`docker exec mangosd tail -50 /opt/vmangos/storage/logs/Server.log`.

---

## Working agreements with the user (carry over)

- The user prefers concise responses. State decisions and changes; don't narrate deliberation.
- The user expects you to keep iterating without asking permission for each phase. Only ask if you genuinely need a decision they haven't already given.
- For risky actions (force push, deleting branches, mass file deletion, schema migrations), always confirm. Code edits and small commits are local/reversible — proceed.
- Integration tests run against the always-live Docker MaNGOS stack. Confirm `docker ps` once at session start; don't keep re-checking.

---

## Starter checklist

1. `git status && git fetch origin && git log --oneline -8` — confirm
   you're on `main`, working tree clean. The audit doc + handoff v3
   commit should be at the tip of `origin/main`.
2. `docker ps` — confirm `mangosd`, `realmd`, `pathfinding-service`
   healthy.
3. Read in order:
   - This handoff (top to bottom).
   - [`docs/physics/bg_movement_parity_audit.md`](physics/bg_movement_parity_audit.md)
     — the audit deliverable; it's your map of which opcodes are pinned and which are partial.
   - [`docs/physics/state_teleport.md`](physics/state_teleport.md),
     [`docs/physics/msg_move_teleport_handler.md`](physics/msg_move_teleport_handler.md),
     [`docs/physics/packet_ack_timing.md`](physics/packet_ack_timing.md)
     — the binary-parity reference for teleport ACK gating.
   - [`CLAUDE.md`](../CLAUDE.md) and the per-repo `CLAUDE.md` for
     repo-wide rules.
   - [`memory/MEMORY.md`](C:/Users/lrhod/.claude/projects/e--repos-Westworld-of-Warcraft/memory/MEMORY.md)
     — the teleport-ACK gating rule is now load-bearing in memory.
4. **Open work, in order:**
   - **Observer-side confirmation** of Track 1 (if a third client is available).
     If not available, leave a note in `docs/handoff_session_bg_movement_parity_followup_v4.md` flagging the blocker.
   - **Optional**: parametrized timing test for movement-flag-toggle
     ACKs (`WATER_WALK`/`HOVER`/`FEATHER_FALL`).
   - **Optional**: heartbeat-suppression narrowing (Option B from v1)
     **only if** observer evidence shows Track 1 is insufficient.
5. Commit and push after each unit. Don't accumulate.

---

## When you near context exhaustion

Write the next followup handoff at
`docs/handoff_session_bg_movement_parity_followup_v4.md`,
self-contained as this one is. Include:

1. Concise restatement of the goal.
2. Current commit hash and a short summary of what's been done since this prompt was written.
3. The remaining tasks, with completed work moved to a "Done" appendix or referenced via prior handoff.
4. Any new blockers, surprises, or design pivots.
5. Repeat all the **Hard rules** above.
6. Repeat **this** "When you near context exhaustion" instruction so the next agent does the same.

The user copies/pastes the latest handoff into a fresh session.
**Make it self-contained** — assume the next agent has zero memory.
