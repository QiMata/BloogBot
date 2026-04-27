# Handoff (followup v2): BG bot teleport double-fall + movement parity audit

> **How to use this:** copy/paste the contents of this file (top to bottom)
> into a fresh Claude Code session in `e:\repos\Westworld of Warcraft`.
> The receiving agent must read this in full before touching anything.
> Older context lives in
> [handoff_session_bg_movement_parity.md](handoff_session_bg_movement_parity.md)
> (the original) and
> [handoff_session_bg_movement_parity_followup.md](handoff_session_bg_movement_parity_followup.md)
> (the v1 followup). Read those if anything below is unclear.

---

## Why this work exists (restated)

A third-party WoW client observing a BG bot after teleport sees the
falling animation play **twice** — once on local prediction immediately
after the teleport, then a second time when the authoritative position
update arrives showing the bot still in mid-air. That indicates a
broadcast-cadence mismatch: while the BG bot is settling onto the
ground, observers receive no in-flight position updates, so their local
prediction races ahead of (then snaps back to) the authoritative state.

---

## Done since followup v1

Run `git log --oneline -5` for the actual tip. The new commit on top of
`908d37eb` is:

1. `test(bg-movement): pin BG post-teleport snapshot stabilization (Task B-fix step 4)`
   - Adds [`Tests/BotRunner.Tests/LiveValidation/BgPostTeleportStabilizationTests.cs`](../Tests/BotRunner.Tests/LiveValidation/BgPostTeleportStabilizationTests.cs).
   - Resolves the BG `BotRunnerActionTarget` (Shodan/FG stay director-only),
     stages it on flat Durotar road (-460, -4760, 38), then `BotTeleportAsync`
     to (-460, -4760, 48) — i.e. ~10y above ground — and asserts via snapshot
     polling that within 1.5s of the teleport call, the BG bot's
     `MOVEFLAG_FALLINGFAR | MOVEFLAG_JUMPING` are cleared and the reported
     XY position is within 1.5y of the teleport target.
   - This codifies the snapshot-side acceptance criterion from Task B-fix
     step 4 of the v1 handoff. It does **not** address the third-party
     observer animation by itself; the observer-side desync requires Option
     A or Option B from the v1 handoff plus manual visual confirmation.

That's the only delta. No code-side fix has been applied; the diagnostic
findings in v1 (heartbeat suppression at
[`MovementController.cs:379`](../Exports/WoWSharpClient/Movement/MovementController.cs))
and the new ACK-gate finding below are the load-bearing pieces of context
for the next session.

---

## Diagnostic findings (incremental over v1)

### NEW — `MSG_MOVE_TELEPORT_ACK` is gated on the ground snap, which violates binary parity

[`Exports/WoWSharpClient/WoWSharpObjectManager.Movement.cs:725-744`](../Exports/WoWSharpClient/WoWSharpObjectManager.Movement.cs):

```csharp
internal bool TryFlushPendingTeleportAck()
{
    if (_pendingTeleportAck is not PendingTeleportAck pendingAck
        || Player is not WoWLocalPlayer player
        || _movementController == null)
    {
        return false;
    }

    if (player.Guid != pendingAck.Guid
        || !HasEnteredWorld
        || HasPendingWorldEntry
        || !_isInControl
        || PendingUpdateCount > 0
        || _updateSemaphore.CurrentCount == 0
        || _movementController.NeedsGroundSnap                // <-- this gate
        || !IsTeleportTargetResolved(player, pendingAck))
    {
        return false;
    }
    ...
}
```

Per [docs/physics/state_teleport.md](physics/state_teleport.md) and
[docs/physics/msg_move_teleport_handler.md](physics/msg_move_teleport_handler.md),
WoW.exe gates the outbound `MSG_MOVE_TELEPORT_ACK` (`0x0C7`) on its
internal `0x468570` readiness function — **not** on a physics ground
snap. The current managed gate is therefore strictly more conservative
than the binary, which means we hold the ACK 30–60 frames longer than
the binary does after a teleport. This is the second, larger contributor
to the post-teleport silent window (the first being the heartbeat
suppression in `MovementController.Update` at line 379).

The gate is locked in by
[`Tests/WoWSharpClient.Tests/ObjectManagerWorldSessionTests.cs:2160`](../Tests/WoWSharpClient.Tests/ObjectManagerWorldSessionTests.cs)
(`TryFlushPendingTeleportAck_WaitsForUpdatesAndGroundSnap_ButNotSceneData`).
Removing the gate is therefore a deliberate **binary-parity correction**
that requires updating that test and is the cleanest fix track for the
double-fall.

### Restated from v1 — heartbeat suppression at MovementController:379

```csharp
// 2. Send network packet if needed.
// Suppress packets during post-teleport ground snap — physics is still settling
// and sending transient FALLINGFAR heartbeats confuses the server.
if (!_needsGroundSnap && ShouldSendPacket(gameTimeMs))
{
    SendMovementPacket(gameTimeMs);
}
```

Added by commit `49915f62` (2026-03-17) to keep transient FALLINGFAR
out of recorded traces (`SteepDescent` recording). This window is ≤2s
and during it the BG bot sends zero packets. Existing parity tests
(`MovementControllerTests.Update_TeleportWithGroundSnap_*`,
`Update_PostTeleport_NoGroundBelow_AllowsGraceFall`,
`Update_TeleportWithGroundSnap_DiscardStalePhysicsResult_*`) assert
`Assert.Empty(_sentPackets)` while the snap is active — they pin the
current behaviour, so any change must update them too.

### Why the gate + suppression combine to produce the double-fall

While both gates are active the BG bot:

1. Receives `MSG_MOVE_TELEPORT` (server has already moved the bot),
2. Queues `_pendingTeleportAck`,
3. Runs ~30–60 physics frames of ground snap with **no outbound packets**,
4. Snap completes, `SendStopPacket` fires, then on next tick
   `TryFlushPendingTeleportAck` finally succeeds.

During steps 2–4 the server holds the teleport pending the ACK, so it
either delays broadcasting the new position to nearby observers or
broadcasts only the initial `SMSG_MOVE_TELEPORT` snapshot. The observer
client then has to pick between local prediction and a stale
authoritative state. When the bot's first post-snap packet finally
arrives, the observer snaps back to the authoritative state and
re-renders the fall.

---

## Remaining tasks (in order)

### Task B-fix-resume (new) — apply one of the two fix tracks and verify

The next session must pick one and verify with manual third-party-client
observation (the v1 handoff explicitly required this and that step has
not been done — I (the previous session) could not run an observing
client, so I deferred the fix and shipped only the regression test).

**Track 1 — Binary-parity correction (preferred, larger-blast).**

1. Remove `|| _movementController.NeedsGroundSnap` from
   [`WoWSharpObjectManager.Movement.cs:740`](../Exports/WoWSharpClient/WoWSharpObjectManager.Movement.cs).
2. Update
   [`Tests/WoWSharpClient.Tests/ObjectManagerWorldSessionTests.cs:2160`](../Tests/WoWSharpClient.Tests/ObjectManagerWorldSessionTests.cs)
   (`TryFlushPendingTeleportAck_WaitsForUpdatesAndGroundSnap_ButNotSceneData`)
   so it asserts the new behaviour: ACK fires once the position is
   resolved + `HasEnteredWorld + _isInControl + PendingUpdateCount==0`,
   regardless of `_needsGroundSnap`.
3. Run the unit suite that owns the parity model:
   `dotnet test Tests/WoWSharpClient.Tests/WoWSharpClient.Tests.csproj
   --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false
   --filter "FullyQualifiedName~ObjectManagerWorldSessionTests|FullyQualifiedName~PacketFlowParityTests|FullyQualifiedName~StateMachineParityTests|FullyQualifiedName~MovementControllerTests"`
   and confirm the suite still passes.
4. Run the new live regression test
   (`BgPostTeleportStabilizationTests`) against the BG bot.
5. With a third WoW client logged in nearby, observe a teleport: the
   fall should play **once** (or zero times if Z target == ground).
6. Commit:
   `fix(bg-movement): align teleport ACK gating with WoW.exe (drop ground-snap gate per state_teleport.md)`.

Optional follow-up if Track 1 alone is insufficient:

- Narrow the heartbeat suppression at
  [`MovementController.cs:379`](../Exports/WoWSharpClient/Movement/MovementController.cs)
  to a single corrective heartbeat at the start of the snap (Option B
  from the v1 handoff). Update the parity tests in
  `Tests/WoWSharpClient.Tests/Movement/MovementControllerTests.cs` that
  assert `Assert.Empty(_sentPackets)` during snap.

**Track 2 — Suppression-narrowing only (smaller-blast).**

If Track 1 turns out to break parity tests in ways that cannot be cleanly
reconciled, fall back to Option A or Option B from v1 (narrow window,
or single corrective post-Reset heartbeat) and leave the ACK gate alone.
Same observer-confirmation step applies.

Either track must keep the new live regression test
(`BgBot_TeleportAboveGround_FallingFlagsClearAndPositionStabilizesWithinBound`)
passing; that test only asserts the BG bot's *own* snapshot, not the
observer state, so it should already pass against current `main` (run it
to confirm and either way capture the elapsed time in test output).

### Task C — broader movement parity audit (still open)

Sweep every BG-side movement packet builder/parser against the WoW.exe
decompilation. Add byte-layout tests where missing. Reference scope from
v1 followup (Section 3 of "Diagnostic findings"):

- `MSG_MOVE_FALL_LAND` — sent on landing? Format vs. WoW.exe?
- `CMSG_FORCE_MOVE_ROOT_ACK` / `CMSG_FORCE_MOVE_UNROOT_ACK` — covered
  by `BuildForceMoveAck` byte-layout tests now; cross-check timing
  against `0x468570` gate in WoW.exe.
- `CMSG_MOVE_KNOCK_BACK_ACK` — same builder. Test covers layout; check
  that the BG bot's knockback impulse application
  ([`MovementController.cs:200-211`](../Exports/WoWSharpClient/Movement/MovementController.cs))
  matches WoW.exe's `0x602670` (SMSG_MOVE_KNOCK_BACK) inbound staging.
- `CMSG_FORCE_*_SPEED_CHANGE_ACK` — speed-echo correctness covered;
  check that incoming SMSG_FORCE_*_SPEED_CHANGE actually triggers the
  ACK on the BG bot.
- `MovementController` integrator constants (gravity, terminal velocity,
  step-up height) — compare to `memory/wow_exe_physics_decompilation.md`
  (CMovement struct +0x68 forwardSpeed, +0x78 fallTime,
  +0xA0 fallStartVelocity, +0xB0 collisionSkin=1/3, +0xB4 stepHeight=2.028).
  Recorded-trace comparison via `Tests/Navigation.Physics.Tests/`
  is the right harness.

Method, deliverables, and acceptance criteria are unchanged from
the original handoff.

---

## Hard rules (DO NOT VIOLATE)

(Unchanged — load-bearing across the repo.)

- **R1 — No blind sequences/counters/timing hacks.** Gate on snapshot signal.
- **R2 — Poll snapshots, don't sleep.** Use `WaitForSnapshotConditionAsync(...)`.
- **R3 — Fail fast.** Per-milestone tight timeouts. `onClientCrashCheck` everywhere.
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
- **When you locally flip a test for verification, REVERT IT before committing** unless the flip is the commit.
- **Read `snapshot.RecentChatMessages` BEFORE diagnosing.** Server-side errors flow back through the snapshot.
- **Binary parity is THE rule for movement/physics.** When the managed code
  is more conservative than WoW.exe (e.g. an extra readiness gate), that's
  a parity bug — fix the managed side, then update the test that pinned
  the divergence.

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

1. `git status && git fetch origin && git log --oneline -5` — confirm
   you're on `main`, working tree clean. The new test commit and
   followup-v2 commit should be at the tip of `origin/main`.
2. `docker ps` — confirm `mangosd`, `realmd`, `pathfinding-service` healthy.
3. Read in order:
   - This handoff (top to bottom).
   - The original handoff: [handoff_session_bg_movement_parity.md](handoff_session_bg_movement_parity.md).
   - The v1 followup: [handoff_session_bg_movement_parity_followup.md](handoff_session_bg_movement_parity_followup.md).
   - [`CLAUDE.md`](../CLAUDE.md) for repo-wide rules.
   - [`memory/MEMORY.md`](C:/Users/lrhod/.claude/projects/e--repos-Westworld-of-Warcraft/memory/MEMORY.md) — note the corrected teleport-ACK entry (16-byte payload, no MovementInfo).
   - [`memory/wow_exe_physics_decompilation.md`](C:/Users/lrhod/.claude/projects/e--repos-Westworld-of-Warcraft/memory/wow_exe_physics_decompilation.md)
     and [`memory/movement_physics.md`](C:/Users/lrhod/.claude/projects/e--repos-Westworld-of-Warcraft/memory/movement_physics.md).
   - [`docs/physics/state_teleport.md`](physics/state_teleport.md),
     [`docs/physics/msg_move_teleport_handler.md`](physics/msg_move_teleport_handler.md),
     [`docs/physics/packet_ack_timing.md`](physics/packet_ack_timing.md).
4. **Tackle in order: Task B-fix-resume → Task C.**
   - Task A is fully done; don't redo it. Verify at session start
     that the SHODAN account's character is recreated as "Shodan"
     (`docker exec maria-db mysql -uroot -proot -N -e "SELECT c.name FROM characters.characters c JOIN realmd.account a ON a.id=c.account WHERE a.username='SHODAN';"`).
     If empty, the bot hasn't logged in yet — fine; happens on next test run.
   - **Task B-fix-resume** still needs the third-party-client observation
     step. Don't skip it. Track 1 (binary-parity) is the recommended
     starting point because it's grounded in `state_teleport.md`.
   - Task C is broad; commit per fix, don't bundle.
5. Commit and push after each unit. Don't accumulate.

---

## When you near context exhaustion

Write the next followup handoff at
`docs/handoff_session_bg_movement_parity_followup_v3.md`,
self-contained as this one is. Include:

1. Concise restatement of the goal.
2. Current commit hash and a short summary of what's been done since this prompt was written.
3. The remaining tasks, with completed work moved to a "Done" appendix or referenced via prior handoff.
4. Any new blockers, surprises, or design pivots discovered.
5. Repeat all the **Hard rules** above.
6. Repeat **this** "When you near context exhaustion" instruction so the next agent does the same.

The user copies/pastes the latest handoff into a fresh session.
**Make it self-contained** — assume the next agent has zero memory.
