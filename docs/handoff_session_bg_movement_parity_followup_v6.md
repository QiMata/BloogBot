# Handoff (followup v6): BG bot teleport double-fall — Stream 2B (narrow MovementController suppression to FG packet pattern)

> **How to use this:** copy/paste this file (top to bottom) into a fresh
> Claude Code session in `e:\repos\Westworld of Warcraft`. Read it in
> full before touching anything. Older context lives in
> [`handoff_session_bg_movement_parity.md`](handoff_session_bg_movement_parity.md)
> (the original), [`handoff_session_bg_movement_parity_followup.md`](handoff_session_bg_movement_parity_followup.md) (v1),
> [`handoff_session_bg_movement_parity_followup_v2.md`](handoff_session_bg_movement_parity_followup_v2.md) (v2),
> [`handoff_session_bg_movement_parity_followup_v3.md`](handoff_session_bg_movement_parity_followup_v3.md) (v3),
> [`handoff_session_bg_movement_parity_followup_v4.md`](handoff_session_bg_movement_parity_followup_v4.md) (v4),
> and [`handoff_session_bg_movement_parity_followup_v5.md`](handoff_session_bg_movement_parity_followup_v5.md) (v5).

---

## Why this work exists (restated)

A third-party WoW client observing a BG bot after teleport reports the
falling animation playing **twice** — once on local prediction
immediately after teleport, then again when the authoritative position
update arrives showing the bot still in mid-air.

Track 1 (commit `eda32b09`) removed `_needsGroundSnap` from the
`MSG_MOVE_TELEPORT_ACK` readiness gates. Stream 1 (this v5 → v6
handoff) **confirmed via live FG packet capture** that the binary also
diverges from BG in a second place: WoW.exe broadcasts heartbeats
*and* a `MSG_MOVE_FALL_LAND` during the post-teleport fall, while BG's
[`MovementController.cs:379`](../Exports/WoWSharpClient/Movement/MovementController.cs)
suppresses all outbound packets for the entire `_needsGroundSnap`
window (commit `49915f62`). That gap is what Stream 2B closes.

---

## Done since v5

Run `git log --oneline -8` for the actual tip. Recent commits:

1. **`1ea7c29e` — `feat(fg-diagnostics): post-teleport packet window recorder`**
   - Adds [`Services/ForegroundBotRunner/Diagnostics/ForegroundPostTeleportWindowRecorder.cs`](../Services/ForegroundBotRunner/Diagnostics/ForegroundPostTeleportWindowRecorder.cs)
     — subscribes to `PacketLogger.OnPacketCapturedDetailed`, triggers
     on inbound `MSG_MOVE_TELEPORT` or `MSG_MOVE_TELEPORT_ACK`, and
     captures every subsequent inbound + outbound packet for a
     configurable window (default 2500 ms) into a JSON fixture under
     `Tests/WoWSharpClient.Tests/Fixtures/post_teleport_packet_window/`.
   - Wired into [`ForegroundBotWorker.InitializeObjectManager`](../Services/ForegroundBotRunner/ForegroundBotWorker.cs)
     beside `ForegroundAckCorpusRecorder`. Gated on
     `WWOW_ENABLE_RECORDING_ARTIFACTS=1` AND
     `WWOW_CAPTURE_POST_TELEPORT_WINDOW=1`.
   - 5 unit tests in [`Tests/ForegroundBotRunner.Tests/ForegroundPostTeleportWindowRecorderTests.cs`](../Tests/ForegroundBotRunner.Tests/ForegroundPostTeleportWindowRecorderTests.cs)
     pin trigger detection (theory over both 0xC5 and 0xC7 inbound),
     no-trigger noise suppression, outbound-ACK self-trigger
     suppression, and the disabled path.

2. **`a85ee419` — `test(ack-capture): FG vertical-drop teleport capture for Stream 1 diff`**
   - Adds `Foreground_VerticalDropTeleport_CapturesTeleportAckAndSnapWindow`
     in [`Tests/BotRunner.Tests/LiveValidation/AckCaptureTests.cs`](../Tests/BotRunner.Tests/LiveValidation/AckCaptureTests.cs).
     Stages the FG bot at the same Durotar road coordinates the BG
     regression test uses, then issues a same-map teleport to ground+10y.

3. **`3de17c5b` — `fix(fg-diagnostics): widen post-teleport recorder trigger to MSG_MOVE_TELEPORT_ACK`**
   - The `.go xyz` self-teleport flow uses inbound `MSG_MOVE_TELEPORT_ACK`
     (0xC7, 37-byte payload), not `MSG_MOVE_TELEPORT` (0xC5). The
     recorder now triggers on either bidirectional MSG_* opcode.
   - **Commits the canonical baseline fixture**
     [`Tests/WoWSharpClient.Tests/Fixtures/post_teleport_packet_window/foreground_durotar_vertical_drop_baseline.json`](../Tests/WoWSharpClient.Tests/Fixtures/post_teleport_packet_window/foreground_durotar_vertical_drop_baseline.json)
     — the binary-parity oracle for Stream 2B.
   - `.gitignore` excludes raw timestamped captures from future runs.

4. **`e66b0acc` — `test(bg-movement): pin FG baseline + BG ack-shape parity for Stream 1`**
   - Adds [`Tests/WoWSharpClient.Tests/Parity/PostTeleportPacketWindowParityTests.cs`](../Tests/WoWSharpClient.Tests/Parity/PostTeleportPacketWindowParityTests.cs)
     with three diagnostics:
     1. `ForegroundBaseline_ReportsExpectedTeleportPacketSequence` —
        passing — pins the FG fixture shape (>=4 outbound packets:
        immediate ACK + >=2 heartbeats + 1 FALL_LAND).
     2. `Background_AfterTeleportTrigger_EmitsOutboundTeleportAckMatchingForegroundShape`
        — passing — confirms the immediate 16-byte client ACK is
        byte-equivalent.
     3. `Background_AfterTeleportTrigger_OutboundStream_StructurallyMatchesForegroundBaseline`
        — `[Skip]` — full ordered-opcode parity. This is the
        Stream 2B exit criterion.

5. **(this commit)** — audit doc update + this v6 handoff.

---

## Stream 1 conclusion (frozen)

**Divergence found.** WoW.exe broadcasts during the post-teleport fall:

| Δms | Direction | Opcode | Notes |
|---|---|---|---|
| 0 | Recv | `MSG_MOVE_TELEPORT_ACK` (37 B) | server-pushed teleport notification |
| 6 | Send | `MSG_MOVE_TELEPORT_ACK` (20 B) | client ACK; already matches BG |
| 491 | Send | `MSG_MOVE_HEARTBEAT` (48 B) | flags `0x6000` (`FALLINGFAR | JUMPING`) |
| 991 | Send | `MSG_MOVE_HEARTBEAT` (48 B) | same FALLING flags, ~500ms cadence |
| 1271 | Send | `MSG_MOVE_FALL_LAND` (32 B) | landing |

BG suppresses heartbeats and FALL_LAND from
[`MovementController.cs:379`](../Exports/WoWSharpClient/Movement/MovementController.cs)
during `_needsGroundSnap`. Stream 2B narrows that suppression.

---

## Stream 2B — Narrow MovementController suppression to FG packet pattern

### What you're doing

Replace the unconditional packet suppression at
[`MovementController.cs:377-382`](../Exports/WoWSharpClient/Movement/MovementController.cs)
(the block introduced by commit `49915f62`):

```csharp
// 2. Send network packet if needed.
// Suppress packets during post-teleport ground snap — physics is still settling
// and sending transient FALLINGFAR heartbeats confuses the server.
if (!_needsGroundSnap && ShouldSendPacket(gameTimeMs))
{
    SendMovementPacket(gameTimeMs);
}
```

The replacement should let `MovementController` emit normal heartbeats
during the snap window so its outbound stream matches the FG fixture
exactly:

- Client sends `MSG_MOVE_TELEPORT_ACK` immediately after the inbound
  trigger (already correct — Track 1).
- Client emits `MSG_MOVE_HEARTBEAT` at the normal ~500ms cadence with
  whatever the current physics-derived `MovementFlags` are
  (`FALLINGFAR | JUMPING` while falling).
- Client emits `MSG_MOVE_FALL_LAND` on landing (the existing flag-delta
  selector in `SendMovementPacket` already handles this).

### Concrete plan (do these in order)

**Step 1 — read the existing FG baseline.** Open
[`Tests/WoWSharpClient.Tests/Fixtures/post_teleport_packet_window/foreground_durotar_vertical_drop_baseline.json`](../Tests/WoWSharpClient.Tests/Fixtures/post_teleport_packet_window/foreground_durotar_vertical_drop_baseline.json)
in full. The `Packets` array is your parity oracle. Note the
`MovementFlags` byte (offset 4 of each heartbeat payload) — `0x6000` =
`FALLINGFAR | JUMPING`. Confirm against
[`memory/movement_physics.md`](C:/Users/lrhod/.claude/projects/e--repos-Westworld-of-Warcraft/memory/movement_physics.md).

**Step 2 — drop the suppression.** Simplest implementation:

```csharp
// 2. Send network packet if needed. Per FG packet capture
// (foreground_durotar_vertical_drop_baseline.json) the binary continues
// emitting heartbeats with current FALLING flags during the post-teleport
// fall, plus MSG_MOVE_FALL_LAND on landing. Suppressing them here causes
// observers' local prediction to race ahead of the authoritative state.
if (ShouldSendPacket(gameTimeMs))
{
    SendMovementPacket(gameTimeMs);
}
```

This relies on `SendMovementPacket` correctly selecting between
heartbeat / fall-land based on the actual physics state. Verify by
reading [`Exports/WoWSharpClient/Movement/MovementController.cs:1100-1300`](../Exports/WoWSharpClient/Movement/MovementController.cs)
(the `SendMovementPacket` implementation and selectors).

**Step 3 — update unit tests in
[`Tests/WoWSharpClient.Tests/Movement/MovementControllerTests.cs`](../Tests/WoWSharpClient.Tests/Movement/MovementControllerTests.cs)**.
Search for any test that asserts `Assert.Empty(_sentPackets)` during a
ground-snap scenario and update it to allow the heartbeat/FALL_LAND
sequence the FG fixture proves. Each updated assertion should cite the
FG fixture path in a comment.

**Step 4 — drive the full parity test.** The skipped
`Background_AfterTeleportTrigger_OutboundStream_StructurallyMatchesForegroundBaseline`
in
[`Tests/WoWSharpClient.Tests/Parity/PostTeleportPacketWindowParityTests.cs`](../Tests/WoWSharpClient.Tests/Parity/PostTeleportPacketWindowParityTests.cs)
needs a way to drive `MovementController.Update(deltaSec, gameTimeMs)`
through the post-teleport snap window. Options:

- **Option A (preferred)**: extend `PacketFlowTraceFixture` with a
  `RunPhysicsFor(uint durationMs, uint stepMs = 33)` helper that calls
  `MovementController.Update` repeatedly. Uses the controller already
  created by `EnsureTeleportAckFlushSupport`. Keeps `NativeLocalPhysics`
  usage scoped to the test.
- **Option B**: leave the test skipped and rely on
  `MovementControllerTests` (Step 3) + the FG capture's
  `ForegroundBaseline` assertion to cover the parity story. The FG
  recorder is reusable so future regressions get caught the next time
  the live capture test runs.

Option A is the right finished state but Option B is acceptable to land
the suppression fix faster. Either way, **unblock the Skip and assert
the full sequence parity** before closing Stream 2B.

**Step 5 — re-run the live FG capture test** with
`WWOW_CAPTURE_POST_TELEPORT_WINDOW=1` and confirm the BG bot's symptom
(third-party-observer double-fall) is gone. Capture a second baseline
from the BG side using whatever recording mechanism is most natural
(probably extending the existing `ForegroundPacketTraceRecorder` or
adding a BG-side counterpart) and commit it as
`background_durotar_vertical_drop_baseline.json` so future regressions
fail loudly against the BG-side fixture too. (Sequencing this last is
fine — the FG fixture alone is sufficient to drive Stream 2B.)

**Step 6 — commit each unit separately.** Don't bundle the `:379`
suppression edit, the test updates, and the parity-test unblock into
one commit. Suggested split:

1. `fix(bg-movement): drop ground-snap packet suppression to match FG fixture`
2. `test(movement-controller): permit heartbeats during post-teleport snap`
3. `test(bg-movement): unblock full ordered-opcode parity vs FG baseline`
4. (optional) `test(bg-movement): commit BG-side fixture as second oracle`

---

## Open follow-up work after Stream 2B

- **Audit the `0x00C9 => "MSG_MOVE_TELEPORT_ACK"` mislabel** in
  [`PacketLogger.cs:878`](../Services/ForegroundBotRunner/Mem/Hooks/PacketLogger.cs).
  0xC9 is `MSG_MOVE_FALL_LAND`; the `GetOpcodeName` switch has these
  swapped. Doesn't affect the corpus recorder (which uses the `Opcode`
  enum) or the new post-teleport recorder, but the diag log is
  misleading.
- **Re-run the live `Foreground_VerticalDropTeleport_*` test** after
  Stream 2B to capture an updated FG baseline and verify nothing has
  drifted. Same coordinates so the fixture filenames stay stable.
- **Apply the same recorder + diff workflow to other teleport
  scenarios** — cross-map, transport mounting, knockback-driven
  teleports — to surface any other binary-parity gaps. The recorder
  triggers on any inbound MSG_MOVE_TELEPORT(_ACK) so it's already
  scenario-agnostic.

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
- **NEVER add `_needsGroundSnap` (or any physics-tick state) to the teleport ACK readiness gates.** Per [`docs/physics/state_teleport.md`](physics/state_teleport.md) the binary gates only on `0x468570`. Re-introducing the gate will resurrect the third-party-client double-fall regression that `eda32b09` fixed.
- **Kill WoW.exe before building.** FG injects DLLs into WoW.exe from the build output; a running WoW.exe locks them and causes MSB3027. `tasklist //FI "IMAGENAME eq WoW.exe" //FO LIST` then `taskkill //F //PID <pid>` — kill only YOUR PIDs (per the process safety rules; never blanket-kill).

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
   you're on `main`, working tree clean. Tip should be the v6 doc commit
   landing on top of `e66b0acc test(bg-movement): pin FG baseline + BG
   ack-shape parity for Stream 1`.
2. `docker ps` — confirm `mangosd`, `realmd`, `pathfinding-service`,
   `maria-db` healthy.
3. Read in order:
   - This handoff (top to bottom).
   - [`docs/physics/bg_movement_parity_audit.md`](physics/bg_movement_parity_audit.md)
     — Finding 6 (Stream 1) is the divergence Stream 2B closes.
   - [`Tests/WoWSharpClient.Tests/Fixtures/post_teleport_packet_window/foreground_durotar_vertical_drop_baseline.json`](../Tests/WoWSharpClient.Tests/Fixtures/post_teleport_packet_window/foreground_durotar_vertical_drop_baseline.json)
     — the parity oracle.
   - [`Tests/WoWSharpClient.Tests/Parity/PostTeleportPacketWindowParityTests.cs`](../Tests/WoWSharpClient.Tests/Parity/PostTeleportPacketWindowParityTests.cs)
     — the existing pinned tests + the Skipped exit criterion.
   - [`Exports/WoWSharpClient/Movement/MovementController.cs`](../Exports/WoWSharpClient/Movement/MovementController.cs)
     — the suppression at line ~379 is what you're narrowing.
   - [`docs/physics/state_teleport.md`](physics/state_teleport.md),
     [`memory/movement_physics.md`](C:/Users/lrhod/.claude/projects/e--repos-Westworld-of-Warcraft/memory/movement_physics.md).
   - [`CLAUDE.md`](../CLAUDE.md) and per-repo `CLAUDE.md` for
     repo-wide rules.
   - [`memory/MEMORY.md`](C:/Users/lrhod/.claude/projects/e--repos-Westworld-of-Warcraft/memory/MEMORY.md).
4. **Open work, in order:**
   - **Stream 2B — narrow MovementController suppression** (Steps 1–6
     above). The FG baseline + parity test infrastructure is fully in
     place; the only remaining change is on the BG side.
5. Commit and push after each unit. Don't accumulate.

---

## When you near context exhaustion

Write the next followup handoff at
`docs/handoff_session_bg_movement_parity_followup_v7.md`,
self-contained as this one is. Include:

1. Concise restatement of the goal.
2. Current commit hash and a short summary of what's been done since this prompt was written.
3. The remaining tasks, with completed work moved to a "Done" appendix or referenced via prior handoff.
4. Any new blockers, surprises, or design pivots.
5. Repeat all the **Hard rules** above.
6. Repeat **this** "When you near context exhaustion" instruction so the next agent does the same.

The user copies/pastes the latest handoff into a fresh session.
**Make it self-contained** — assume the next agent has zero memory.
