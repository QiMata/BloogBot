# Handoff (followup): BG bot teleport double-fall + movement parity audit

> **How to use this:** copy/paste the contents of this file (top to bottom)
> into a fresh Claude Code session in `e:\repos\Westworld of Warcraft`.
> The receiving agent must read this in full before touching anything.
> This is the followup to [handoff_session_bg_movement_parity.md](handoff_session_bg_movement_parity.md);
> read that first if anything below is unclear.

---

## Why this work exists (restated)

A third-party WoW client observing a BG bot after teleport sees the
falling animation play **twice** — once on local prediction immediately
after the teleport, then a second time when the authoritative position
update arrives showing the bot still in mid-air. That indicates a
broadcast-cadence mismatch: while the BG bot is settling onto the
ground, observers receive no in-flight position updates from the server,
so their local prediction races ahead of the authoritative state.

---

## Done since [`handoff_session_bg_movement_parity.md`](handoff_session_bg_movement_parity.md)

Current commit: see `git log --oneline -5`. The two relevant commits are:

1. `feat(shodan): pin Shodan character name and recreate Lilippidogeo as Shodan` (Task A — done end-to-end)
   - Adds optional `CharacterSettings.CharacterName` field; StateManager
     forwards via `WWOW_CHARACTER_NAME`; BotRunner consumes via
     `WoWNameGenerator.ResolveCharacterName`. TESTBOT* paths leave it
     unset → keep syllable generator (zero behaviour change for
     randomized accounts).
   - All 13 SHODAN-bearing config files now set `"CharacterName": "Shodan"`.
   - The existing `Lilippidogeo` character was erased via SOAP
     `.character erase Lilippidogeo`. (VMaNGOS `.character rename` only
     queues a forced-rename screen with no target name; it cannot do an
     inline rename. The bot will recreate as `Shodan` on next launch via
     the new pinned-name path.)
   - `LiveBotFixture.VerifyShodanCharacterNameMatchesConfigured()` fails
     fixture init with a clear, actionable error if the bot's reported
     Shodan name ever drifts from the configured value.
2. `test(movement): pin ACK builder byte layouts against WoW.exe 0x602FB0 disasm` (Task B — partial)
   - Adds `Tests/WoWSharpClient.Tests/Movement/MovementPacketHandlerAckTests.cs`
     with 5 unit tests pinning the byte layouts of `BuildMoveTeleportAckPayload`,
     `BuildForceMoveAck`, and `BuildForceSpeedChangeAck` against the
     decompilation reference.
   - Corrects the stale `MEMORY.md` note that claimed teleport ACK had
     to carry full MovementInfo (it doesn't — that pattern is only for
     root/unroot/knockback/speed-change ACKs).

---

## Diagnostic findings (Task B — partial)

### 1. Teleport ACK wire format is correct

`MovementPacketHandler.BuildMoveTeleportAckPayload` produces exactly
`uint64 guid + uint32 counter + uint32 clientTimeMs` (16 bytes after
the opcode 0xC7), matching WoW.exe's `0x602FB0` disasm. See:

- [docs/physics/0x602FB0_disasm.txt](physics/0x602FB0_disasm.txt) — relevant block at 0x603036–0x60308D
- [docs/physics/msg_move_teleport_handler.md](physics/msg_move_teleport_handler.md)
- [docs/physics/packet_ack_timing.md](physics/packet_ack_timing.md)

The earlier MEMORY.md note "Teleport ACK must include full MovementInfo"
was wrong (most likely conflated with `CMSG_FORCE_MOVE_ROOT_ACK` /
`CMSG_FORCE_*_SPEED_CHANGE_ACK` / knockback ACKs — those *do* carry
MovementInfo via `BuildForceMoveAck` / `BuildForceSpeedChangeAck`).
Memory updated; new tests pin the correct layout permanently.

### 2. Most likely root cause of the double-fall: ground-snap packet suppression

[Exports/WoWSharpClient/Movement/MovementController.cs:377-382](../Exports/WoWSharpClient/Movement/MovementController.cs):

```csharp
// 2. Send network packet if needed.
// Suppress packets during post-teleport ground snap — physics is still settling
// and sending transient FALLINGFAR heartbeats confuses the server.
if (!_needsGroundSnap && ShouldSendPacket(gameTimeMs))
{
    SendMovementPacket(gameTimeMs);
}
```

This was added by commit `49915f62` (2026-03-17, "Suppress packets
during post-teleport ground snap") to avoid sending transient
`MOVEFLAG_FALLINGFAR` heartbeats to MaNGOS. The trade-off is now
visible: with no in-flight updates broadcast during the snap, the
third-party WoW client's local prediction races ahead of the
authoritative state and animates the fall twice (once predictively,
once on landed-position arrival).

Two paths forward (pick one in the next session, but verify with
manual third-party-client observation):

**Option A — narrow the suppression window.** Keep the suppression
only for the very first frame or two after teleport (where flags are
still settling), then resume normal heartbeats. The current snap can
take many frames (`GROUND_SNAP_MAX_FRAMES`).

**Option B — replace suppression with a single corrective stop
packet.** Right after the teleport applies and `Reset()` runs, send
one MSG_MOVE_HEARTBEAT (or equivalent) with the *teleport target
position + MOVEFLAG_NONE*, regardless of physics. Then keep
suppressing during the snap. This gives observers a definitive
"bot is landed at exactly here" frame, matching their local prediction.

Option B is closer to what WoW.exe does for self-teleports per
`docs/physics/state_teleport.md` (steps 4–5: "Once ready, BG sends
MSG_MOVE_TELEPORT_ACK, clears _pendingTeleportAck, and clears
_isBeingTeleported"). The ACK *itself* has no MovementInfo, but the
client's first post-teleport heartbeat does.

### 3. Where the broader audit (Task C) should start

The same packet-suppression / fall-flag investigation should be applied
to the rest of the movement opcodes per the original handoff scope:

- `MSG_MOVE_FALL_LAND` — sent on landing? Format vs. WoW.exe?
- `CMSG_FORCE_MOVE_ROOT_ACK` / `CMSG_FORCE_MOVE_UNROOT_ACK` — covered
  by `BuildForceMoveAck` byte-layout test now; cross-check timing
  against `0x468570` gate in WoW.exe.
- `CMSG_MOVE_KNOCK_BACK_ACK` — same builder. Test covers layout;
  check that the BG bot's knockback impulse application
  ([MovementController.cs:200-211](../Exports/WoWSharpClient/Movement/MovementController.cs))
  matches WoW.exe's `0x602670` (SMSG_MOVE_KNOCK_BACK) inbound staging.
- `CMSG_FORCE_*_SPEED_CHANGE_ACK` — speed-echo correctness covered
  by the byte-layout test; check that incoming SMSG_FORCE_*_SPEED_CHANGE
  actually triggers the ACK on the BG bot.
- `MovementController` integrator constants (gravity, terminal velocity,
  step-up height) — compare to `memory/wow_exe_physics_decompilation.md`
  CMovement struct (+0x68 forwardSpeed, +0x78 fallTime, +0xA0 fallStartVelocity, +0xB0 collisionSkin=1/3, +0xB4 stepHeight=2.028).
  Recorded-trace comparison via `Tests/Navigation.Physics.Tests/`
  is the right harness.

---

## Remaining tasks (do in order)

### Task B-fix (resume) — kill the double-fall

1. Run a BG-only test that issues `BotTeleportAsync` to a Z above
   ground (e.g. `.go xyz X Y Z+10 mapId`) and keeps polling
   snapshots. Concurrently log in a third WoW client nearby and
   observe whether the fall plays once or twice. Note the
   timestamps of the teleport ack send, snapshot MovementFlags
   transitions, and the client's animations.
2. Capture both BG outbound and inbound packets via the existing
   `WoWSharpClient` packet logging. Verify
   - the teleport ACK is sent at the expected time (matches
     `_pendingTeleportAck` flush condition);
   - which packets DO go out during the ground-snap window
     (currently: none).
3. Apply Option A or Option B above (probably B). Confirm with the
   third-party client that the fall now plays once.
4. Add a live regression test in
   `Tests/BotRunner.Tests/LiveValidation/` that issues a teleport
   and asserts:
   - bot's snapshot reports cleared `MOVEFLAG_FALLING*` within
     1.5 s of teleport,
   - position stabilises within a 1.5 yd radius of teleport target
     within 1.5 s.
5. Commit per logical fix. Don't bundle. Suggested message:
   `fix(bg-movement): broadcast post-teleport landed position so observers don't double-render the fall`

### Task C — broader movement parity audit (still open)

Sweep every BG-side movement packet builder/parser against the
WoW.exe decompilation. Add byte-layout tests where missing.
Reference scope above (Section 3 of "Diagnostic findings").

Method, deliverables, and acceptance criteria are unchanged from
the original handoff — see `handoff_session_bg_movement_parity.md`
"Task C — Audit BG bot vs WoW.exe decompilation parity".

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
   you're on `main`, working tree clean. The `1adf5096` (test commit)
   and `be6331fa` (Shodan rename commit) should be at the tip of
   `origin/main`.
2. `docker ps` — confirm `mangosd`, `realmd`, `pathfinding-service` healthy.
3. Read in order:
   - This handoff (top to bottom).
   - The original handoff: [handoff_session_bg_movement_parity.md](handoff_session_bg_movement_parity.md).
   - [`CLAUDE.md`](../CLAUDE.md) for repo-wide rules.
   - [`memory/MEMORY.md`](C:/Users/lrhod/.claude/projects/e--repos-Westworld-of-Warcraft/memory/MEMORY.md) — note the corrected teleport-ACK entry.
   - [`memory/wow_exe_physics_decompilation.md`](C:/Users/lrhod/.claude/projects/e--repos-Westworld-of-Warcraft/memory/wow_exe_physics_decompilation.md)
     and [`memory/movement_physics.md`](C:/Users/lrhod/.claude/projects/e--repos-Westworld-of-Warcraft/memory/movement_physics.md).
   - [`docs/physics/state_teleport.md`](physics/state_teleport.md),
     [`docs/physics/msg_move_teleport_handler.md`](physics/msg_move_teleport_handler.md),
     [`docs/physics/packet_ack_timing.md`](physics/packet_ack_timing.md).
4. **Tackle in order: Task B-fix → Task C.**
   - Task A is fully done; don't redo it. Verify at session start
     that the SHODAN account's character has been recreated as
     "Shodan" (`docker exec maria-db mysql -uroot -proot -N -e "SELECT c.name FROM characters.characters c JOIN realmd.account a ON a.id=c.account WHERE a.username='SHODAN';"`).
     If empty, the bot hasn't logged in yet — fine; happens on next test run.
   - Task B-fix needs the third-party-client observation step. Don't
     skip it — that's where the evidence lives.
   - Task C is broad; commit per fix, don't bundle.
5. Commit and push after each unit. Don't accumulate.

---

## When you near context exhaustion

Write the next followup handoff at
`docs/handoff_session_bg_movement_parity_followup_v2.md`,
self-contained as this one is. Include:

1. Concise restatement of the goal.
2. Current commit hash and a short summary of what's been done since this prompt was written.
3. The remaining tasks, with completed work moved to a "Done" appendix or referenced via prior handoff.
4. Any new blockers, surprises, or design pivots discovered.
5. Repeat all the **Hard rules** above.
6. Repeat **this** "When you near context exhaustion" instruction so the next agent does the same.

The user copies/pastes the latest handoff into a fresh session.
**Make it self-contained** — assume the next agent has zero memory.
