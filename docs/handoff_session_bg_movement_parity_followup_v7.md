# Handoff (followup v7): BG bot teleport double-fall — Stream 2C (cadence-gate the post-teleport first heartbeat)

> **How to use this:** copy/paste this file (top to bottom) into a fresh
> Claude Code session in `e:\repos\Westworld of Warcraft`. Read it in
> full before touching anything. Older context lives in
> [`handoff_session_bg_movement_parity.md`](handoff_session_bg_movement_parity.md)
> (the original), and the v1–v6 follow-ups
> ([v1](handoff_session_bg_movement_parity_followup.md),
> [v2](handoff_session_bg_movement_parity_followup_v2.md),
> [v3](handoff_session_bg_movement_parity_followup_v3.md),
> [v4](handoff_session_bg_movement_parity_followup_v4.md),
> [v5](handoff_session_bg_movement_parity_followup_v5.md),
> [v6](handoff_session_bg_movement_parity_followup_v6.md)).

---

## Why this work exists (restated)

A third-party WoW client observing a BG bot after teleport reports the
falling animation playing **twice** — once on local prediction
immediately after teleport, then again when the authoritative position
update arrives showing the bot still in mid-air.

Track 1 (commit `eda32b09`) removed `_needsGroundSnap` from the
`MSG_MOVE_TELEPORT_ACK` readiness gates. Stream 2B (this v6 → v7
handoff) **dropped the ground-snap packet suppression** in
`MovementController` and **fixed the `MSG_MOVE_FALL_LAND` opcode
selector**. After Stream 2B, the BG outbound stream is one heartbeat
ahead of the FG fixture — the **only remaining parity gap**.

---

## Done since v6 (Stream 2B)

Run `git log --oneline -8` for the actual tip. Recent commits on `main`:

1. **`472170e3` — `fix(bg-movement): drop ground-snap packet suppression to match FG fixture`**
   - Removes `!_needsGroundSnap &&` guard at
     [`MovementController.cs:379`](../Exports/WoWSharpClient/Movement/MovementController.cs)
     so heartbeats and `MSG_MOVE_FALL_LAND` flow through during the
     snap window. Cites the FG baseline JSON in the comment.

2. **`9cca7808` — `test(movement-controller): permit heartbeats during post-teleport snap`**
   - Updates `Update_PostTeleport_NoGroundBelow_AllowsGraceFall` to
     expect a single `MSG_MOVE_HEARTBEAT` instead of `Empty(_sentPackets)`.
     The airborne flag transition correctly emits a heartbeat now.

3. **`4771d931` — `fix(bg-movement): emit FALL_LAND for FALLINGFAR/JUMPING -> grounded transitions`**
   - Reorders `DetermineOpcode` so the landing rules fire before the
     `current==NONE && previous!=NONE → MSG_MOVE_STOP` rule. Without
     this, BG emitted `MSG_MOVE_STOP` when the snap-completion path
     called `SendStopPacket` with `previous=FALLINGFAR`, instead of the
     `MSG_MOVE_FALL_LAND` that WoW.exe emits.
   - Updates `RequestGroundedStop_ClearsForwardIntent_OnFirstGroundedFrame`
     to expect `MSG_MOVE_FALL_LAND` instead of `MSG_MOVE_STOP` for the
     `FORWARD|FALLINGFAR → NONE` transition (landing wins over stop).
   - Other STOP-asserting tests (`StopForward`, `StopSwim`) are
     unaffected because their `previous` flags don't include
     `FALLINGFAR/JUMPING`.

4. **`c792d276` — `test(bg-movement): unblock post-teleport parity test vs FG baseline`**
   - Replaces the `[Skip]` on
     `Background_AfterTeleportTrigger_OutboundStream_StructurallyMatchesForegroundBaseline`
     with a real driver. Extends `PacketFlowTraceFixture` with
     `RunPhysicsFor(uint durationMs, uint stepMs = 33,
     uint startGameTimeMs = 0)` that calls `MovementController.Update`
     repeatedly using `NativeLocalPhysics.TestStepOverride`-scripted
     physics output.
   - Pins the BG-today opcode stream so any future regression that
     drops or reorders packets fails loudly.

5. **(this commit)** — v7 handoff doc for Stream 2C.

Validation: `dotnet test Tests/WoWSharpClient.Tests` →
`Passed: 1611, Skipped: 1, Failed: 0` plus
`Tests/Navigation.Physics.Tests` (137 passed) and
`Tests/RecordedTests.PathingTests.Tests` (135 passed).

---

## Stream 2B conclusion (frozen)

After Stream 2B, the BG and FG outbound streams during the post-teleport
window are:

| Step | FG (WoW.exe) | BG (after Stream 2B) |
|------|--------------|----------------------|
| 1 | `MSG_MOVE_TELEPORT_ACK` | `MSG_MOVE_TELEPORT_ACK` |
| 2 | — | **`MSG_MOVE_HEARTBEAT`** ← extra |
| 3 | `MSG_MOVE_HEARTBEAT` (≈491ms) | `MSG_MOVE_HEARTBEAT` |
| 4 | `MSG_MOVE_HEARTBEAT` (≈991ms) | `MSG_MOVE_HEARTBEAT` |
| 5 | `MSG_MOVE_FALL_LAND` (≈1271ms) | `MSG_MOVE_FALL_LAND` |

The +1 heartbeat at step 2 is the remaining parity gap.

---

## Stream 2C — Cadence-gate the post-teleport first heartbeat

### What you're doing

Make BG match the FG outbound stream byte-for-opcode-name during the
post-teleport window. The current divergence is a single extra
`MSG_MOVE_HEARTBEAT` at the start of the fall.

### Root cause

[`MovementController.cs:1019`](../Exports/WoWSharpClient/Movement/MovementController.cs)
`ShouldSendPacket` returns `true` immediately when
`_player.MovementFlags != _lastSentFlags`, with **no cadence gate**:

```csharp
private bool ShouldSendPacket(uint gameTimeMs)
{
    // Send if movement state changed
    if (_player.MovementFlags != _lastSentFlags)
        return true;

    // Send periodic heartbeat while moving
    if (_player.MovementFlags != MovementFlags.MOVEFLAG_NONE &&
        gameTimeMs - _lastPacketTime >= PACKET_INTERVAL_MS)
        return true;
    ...
}
```

After teleport:
- `Reset(teleportDestZ)` sets `_lastSentFlags = NONE` (assuming
  pre-teleport flags were also NONE). It also sets
  `_lastPacketTime = _latestGameTimeMs` (typically 0).
- `TryFlushPendingTeleportAck` sends the outbound `MSG_MOVE_TELEPORT_ACK`
  via `WoWClient.SendMSGPackedAsync`, which **bypasses
  MovementController** entirely — `_lastPacketTime` is never updated.
- First physics tick: `ApplyPhysicsResult` sets
  `_player.MovementFlags |= FALLINGFAR`. `ShouldSendPacket` sees
  `FALLINGFAR != NONE` and returns true → heartbeat fires immediately.

WoW.exe by contrast cadence-gates even flag changes (the FG fixture
shows the first heartbeat at deltaMs≈491, exactly `PACKET_INTERVAL_MS`
≈500ms after the outbound ACK at deltaMs=6).

### Concrete plan (do these in order)

**Step 1 — pin the FG cadence-from-ACK observation.** Open
[`Tests/WoWSharpClient.Tests/Fixtures/post_teleport_packet_window/foreground_durotar_vertical_drop_baseline.json`](../Tests/WoWSharpClient.Tests/Fixtures/post_teleport_packet_window/foreground_durotar_vertical_drop_baseline.json).
Confirm the outbound ACK is at deltaMs=6 and the first outbound HB is
at deltaMs=491 — exactly ≈500ms (`PACKET_INTERVAL_MS`). The first HB
firing on cadence-from-ACK rather than flag-change is the WoW.exe
behaviour to mimic.

**Step 2 — pick the fix shape.** Two viable approaches:

- **Option A (preferred)**: have `TryFlushPendingTeleportAck` notify
  the `MovementController` so it can update its `_lastPacketTime` and
  `_lastSentFlags` after sending the outbound ACK. Add a small public
  method like `MovementController.NotifyExternalPacketSent(uint
  gameTimeMs, MovementFlags flagsAtSend)` and call it from
  [`WoWSharpObjectManager.Movement.cs:760`](../Exports/WoWSharpClient/WoWSharpObjectManager.Movement.cs)
  right after `SendMSGPackedAsync`. This keeps `ShouldSendPacket`'s
  flag-change shortcut intact (real player input still needs immediate
  responsiveness), but resets the cadence so the post-teleport
  flag-change-to-FALLINGFAR doesn't re-fire within the same 500ms
  window. Risk: low. Behaviour: BG matches FG exactly.

  Caveat: the outbound ACK happens ~6ms after the trigger; the first
  physics tick fires shortly after. If `NotifyExternalPacketSent` sets
  `_lastSentFlags` to whatever the player's flags are AT THE TIME
  (still `NONE` because physics hasn't run yet), then the first
  physics tick still sees the flag transition. We'd need to either:
  - (a) Suppress the flag-change shortcut for one window
    (`PACKET_INTERVAL_MS`) after `NotifyExternalPacketSent`, OR
  - (b) Set `_lastSentFlags` optimistically to the post-physics
    expected value (FALLINGFAR) so the diff is zero on the first
    physics tick. This is heuristic and risky.

  Approach (a) is cleaner: introduce
  `_suppressFlagChangeUntilMs = AddMs(gameTimeMs, PACKET_INTERVAL_MS)`
  inside `NotifyExternalPacketSent`. Modify `ShouldSendPacket` so the
  flag-change shortcut is gated on `IsBefore(gameTimeMs,
  _suppressFlagChangeUntilMs)` — when suppressed, fall through to the
  cadence check. Since the cadence still requires `MovementFlags !=
  NONE`, the new behaviour matches FG: the FALLINGFAR transition is
  noted but the actual packet waits for cadence to elapse.

- **Option B**: gate the flag-change shortcut globally on
  `PACKET_INTERVAL_MS` since `_lastPacketTime`. Riskier — affects all
  movement scenarios (jump, swim, strafe), not just post-teleport.
  Could break responsiveness tests.

Option A targeted at the post-teleport window is the right call.

**Step 3 — implementation sketch (Option A).**

Edit [`Exports/WoWSharpClient/Movement/MovementController.cs`](../Exports/WoWSharpClient/Movement/MovementController.cs):

```csharp
private uint _suppressFlagChangeUntilMs;

/// <summary>
/// Notified by ObjectManager when an external code path (currently
/// TryFlushPendingTeleportAck) sends a movement-related opcode that
/// MovementController didn't construct. Resets cadence tracking so the
/// next physics-tick flag change doesn't double-fire a heartbeat
/// inside the cadence window. Per FG packet capture
/// (foreground_durotar_vertical_drop_baseline.json) WoW.exe emits the
/// first post-teleport heartbeat ~PACKET_INTERVAL_MS after the
/// outbound TELEPORT_ACK, not on the immediate FALLINGFAR transition.
/// </summary>
public void NotifyExternalPacketSent(uint gameTimeMs)
{
    _lastPacketTime = gameTimeMs;
    _suppressFlagChangeUntilMs = AddMs(gameTimeMs, PACKET_INTERVAL_MS);
}

private bool ShouldSendPacket(uint gameTimeMs)
{
    if (_player.MovementFlags != _lastSentFlags
        && !IsBefore(gameTimeMs, _suppressFlagChangeUntilMs))
        return true;
    // ...rest unchanged
}
```

Edit [`Exports/WoWSharpClient/WoWSharpObjectManager.Movement.cs`](../Exports/WoWSharpClient/WoWSharpObjectManager.Movement.cs)
inside `TryFlushPendingTeleportAck`, right after `SendMSGPackedAsync`:

```csharp
_movementController.NotifyExternalPacketSent(
    (uint)_worldTimeTracker.NowMS.TotalMilliseconds);
```

**Step 4 — update the parity test.** In
[`Tests/WoWSharpClient.Tests/Parity/PostTeleportPacketWindowParityTests.cs`](../Tests/WoWSharpClient.Tests/Parity/PostTeleportPacketWindowParityTests.cs)
the v6→v7 commit hard-pins the BG-today stream as
`MSG_MOVE_TELEPORT_ACK,MSG_MOVE_HEARTBEAT,MSG_MOVE_HEARTBEAT,MSG_MOVE_HEARTBEAT,MSG_MOVE_FALL_LAND`
(one extra HB vs FG). Once Stream 2C lands, that string should drop
the extra HB and equal the FG sequence:

```csharp
Assert.Equal(fgOpcodeSet, bgOpcodeSet);
```

The asymmetry-pinning sanity assertion at the end of the test
(`Assert.Equal(fgOutboundOpcodes.Length + 1, bgOutboundOpcodes.Length)`)
needs to drop too — change to `Assert.Equal(fgOutboundOpcodes,
bgOutboundOpcodes)`.

**Step 5 — sweep for regressions.** Run:

```bash
dotnet test Tests/WoWSharpClient.Tests/WoWSharpClient.Tests.csproj --configuration Release -v minimal
dotnet test Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release -v minimal
dotnet test Tests/RecordedTests.PathingTests.Tests/RecordedTests.PathingTests.Tests.csproj --configuration Release -v minimal
```

Expect 1611+137+135 to pass with no new failures. If
`MovementControllerTests` reveals a flag-change immediacy regression
(e.g. a player-input scenario expects a packet within one tick of
pressing forward), that test was already passing pre-Stream-2C, so the
suppression window timing or the
`NotifyExternalPacketSent`-only-from-teleport scoping needs review.

**Step 6 — commit each unit separately.** Suggested split:

1. `feat(bg-movement): expose MovementController.NotifyExternalPacketSent for cadence sync`
2. `fix(bg-movement): suppress flag-change packet for one cadence window after external send`
3. `fix(bg-movement): reset MovementController cadence on outbound TELEPORT_ACK`
4. `test(bg-movement): pin full ordered-opcode parity vs FG baseline (Stream 2C)`

(Combining 1 and 2 is fine if the API is only used here.)

**Step 7 — re-run the live FG capture test** with
`WWOW_CAPTURE_POST_TELEPORT_WINDOW=1` to confirm the FG baseline is
unchanged (we didn't drift the oracle), and run the BG vertical-drop
regression test with a third-party-client observer to verify the
double-fall animation no longer appears.

---

## Open follow-up work after Stream 2C

- **Audit the `0x00C9 => "MSG_MOVE_TELEPORT_ACK"` mislabel** in
  [`PacketLogger.cs:878`](../Services/ForegroundBotRunner/Mem/Hooks/PacketLogger.cs).
  0xC9 is `MSG_MOVE_FALL_LAND`; the `GetOpcodeName` switch has these
  swapped. Doesn't affect the corpus recorder (which uses the `Opcode`
  enum) or the post-teleport recorder, but the diag log is misleading.
- **Capture a BG-side post-teleport baseline** as a second oracle so
  any future regression fails against the BG fixture too. Path:
  `Tests/WoWSharpClient.Tests/Fixtures/post_teleport_packet_window/background_durotar_vertical_drop_baseline.json`.
  Probably extends `ForegroundPacketTraceRecorder` or adds a
  BG-specific counterpart.
- **Re-run the live `Foreground_VerticalDropTeleport_*` test** after
  Stream 2C to capture an updated FG baseline and verify nothing drifted.
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
- **Binary parity is THE rule for movement/physics.** When the managed code is more conservative or more aggressive than WoW.exe, that's a parity bug — fix the managed side, then update the test that pinned the divergence.
- **NEVER add `_needsGroundSnap` (or any physics-tick state) to the teleport ACK readiness gates.** Per [`docs/physics/state_teleport.md`](physics/state_teleport.md) the binary gates only on `0x468570`. Re-introducing the gate will resurrect the third-party-client double-fall regression that `eda32b09` fixed.
- **NEVER re-introduce the `!_needsGroundSnap &&` guard at MovementController.cs:379.** The FG fixture proves WoW.exe broadcasts during the snap window; suppressing those packets is the original Stream 2B regression that this v6→v7 work closed.
- **NEVER reorder `DetermineOpcode` to put the MOVE_STOP rule before the FALL_LAND rules.** The FG fixture proves landing wins over stop for `FALLINGFAR/JUMPING → grounded` transitions.
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
   you're on `main`, working tree clean. Tip should be the v7 doc commit
   landing on top of `c792d276 test(bg-movement): unblock post-teleport
   parity test vs FG baseline`.
2. `docker ps` — confirm `mangosd`, `realmd`, `pathfinding-service`,
   `maria-db` healthy.
3. Read in order:
   - This handoff (top to bottom).
   - [`Tests/WoWSharpClient.Tests/Fixtures/post_teleport_packet_window/foreground_durotar_vertical_drop_baseline.json`](../Tests/WoWSharpClient.Tests/Fixtures/post_teleport_packet_window/foreground_durotar_vertical_drop_baseline.json)
     — the FG parity oracle.
   - [`Tests/WoWSharpClient.Tests/Parity/PostTeleportPacketWindowParityTests.cs`](../Tests/WoWSharpClient.Tests/Parity/PostTeleportPacketWindowParityTests.cs)
     — the parity test currently pinning the +1 HB divergence.
   - [`Exports/WoWSharpClient/Movement/MovementController.cs`](../Exports/WoWSharpClient/Movement/MovementController.cs)
     — `ShouldSendPacket` (line ~1016) and the suppression-removed
     packet-send block at line ~376 are the points to extend.
   - [`Exports/WoWSharpClient/WoWSharpObjectManager.Movement.cs`](../Exports/WoWSharpClient/WoWSharpObjectManager.Movement.cs)
     — `TryFlushPendingTeleportAck` (line ~725) is where Stream 2C
     should call into the new `NotifyExternalPacketSent` hook.
   - [`docs/physics/state_teleport.md`](physics/state_teleport.md),
     [`docs/physics/bg_movement_parity_audit.md`](physics/bg_movement_parity_audit.md),
     [`memory/movement_physics.md`](C:/Users/lrhod/.claude/projects/e--repos-Westworld-of-Warcraft/memory/movement_physics.md).
   - [`CLAUDE.md`](../CLAUDE.md) and per-repo `CLAUDE.md` for
     repo-wide rules.
   - [`memory/MEMORY.md`](C:/Users/lrhod/.claude/projects/e--repos-Westworld-of-Warcraft/memory/MEMORY.md).
4. **Open work, in order:**
   - **Stream 2C — cadence-gate the post-teleport first heartbeat**
     (Steps 1–7 above). The parity test infrastructure is in place;
     change is in MovementController + WoWSharpObjectManager.Movement.
5. Commit and push after each unit. Don't accumulate.

---

## When you near context exhaustion

Write the next followup handoff at
`docs/handoff_session_bg_movement_parity_followup_v8.md`,
self-contained as this one is. Include:

1. Concise restatement of the goal.
2. Current commit hash and a short summary of what's been done since this prompt was written.
3. The remaining tasks, with completed work moved to a "Done" appendix or referenced via prior handoff.
4. Any new blockers, surprises, or design pivots.
5. Repeat all the **Hard rules** above.
6. Repeat **this** "When you near context exhaustion" instruction so the next agent does the same.

The user copies/pastes the latest handoff into a fresh session.
**Make it self-contained** — assume the next agent has zero memory.
