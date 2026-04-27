# Handoff (followup v4): BG bot teleport double-fall — observer confirmation outstanding; flag-toggle timing tests landed

> **How to use this:** copy/paste this file (top to bottom) into a fresh
> Claude Code session in `e:\repos\Westworld of Warcraft`. Read it in
> full before touching anything. Older context lives in
> [`handoff_session_bg_movement_parity.md`](handoff_session_bg_movement_parity.md)
> (the original), [`handoff_session_bg_movement_parity_followup.md`](handoff_session_bg_movement_parity_followup.md) (v1),
> [`handoff_session_bg_movement_parity_followup_v2.md`](handoff_session_bg_movement_parity_followup_v2.md) (v2),
> and [`handoff_session_bg_movement_parity_followup_v3.md`](handoff_session_bg_movement_parity_followup_v3.md) (v3).
> Read v3 first if anything below is unclear — it has the deeper diagnosis.

---

## Why this work exists (restated)

A third-party WoW client observing a BG bot after teleport sees the
falling animation play **twice** — once on local prediction immediately
after the teleport, then a second time when the authoritative position
update arrives showing the bot still in mid-air.

Diagnosis (frozen at v2/v3): the BG bot was silent for 30–60 physics
frames after a teleport because `TryFlushPendingTeleportAck` gated on
`_movementController.NeedsGroundSnap`, which is strictly more
conservative than WoW.exe's `0x468570` readiness function (per
[`docs/physics/state_teleport.md`](physics/state_teleport.md)).

**Track 1 (commit `eda32b09`)** removed the rogue gate so the BG bot
sends `MSG_MOVE_TELEPORT_ACK` on the same readiness gates as the
binary. Whether Track 1 alone closes the visual regression on a
third-party observing client remains the open question that requires a
human-driven WoW client to confirm — see "Stream 1" below.

---

## Done since v3

Run `git log --oneline -8` for the actual tip. The single new commit on
top of `cf61ac52` is:

1. **`d02a973a` — `test(bg-movement): pin queue-first deferred-ACK contract for movement-flag-toggle ACKs`**
   - [`Tests/WoWSharpClient.Tests/Parity/PacketFlowParityTests.cs`](../Tests/WoWSharpClient.Tests/Parity/PacketFlowParityTests.cs):
     adds `[Theory] MovementFlagToggleFamily_QueuesDeferredAck_ThenFlushesWithUpdatedFlag`
     covering the six inbound opcodes
     (`SMSG_MOVE_{WATER_WALK,LAND_WALK,SET_HOVER,UNSET_HOVER,FEATHER_FALL,NORMAL_FALL}`)
     → three CMSG ACK opcodes
     (`CMSG_MOVE_{WATER_WALK,HOVER,FEATHER_FALL}_ACK`) with a trailing
     `1.0f`/`0.0f` marker per `BuildMovementFlagToggleAck`. Mirrors
     `ForceSpeedChangeFamily_QueuesDeferredAck_ThenFlushesWithUpdatedState`
     (commit `ebff10a4`) so the queue-first dispatch and deferred apply
     are pinned at the unit-test level for the full toggle family.
   - [`Tests/WoWSharpClient.Tests/Parity/PacketFlowTraceFixture.cs:213-218`](../Tests/WoWSharpClient.Tests/Parity/PacketFlowTraceFixture.cs):
     wires the six missing inbound toggle opcodes into the trace
     fixture's `ResolveHandler` dispatch.
   - [`docs/physics/bg_movement_parity_audit.md`](physics/bg_movement_parity_audit.md):
     flips the three toggle-ACK rows from "PASS layout / PARTIAL
     timing" to "PASS layout + timing — Stream 2A", and corrects the
     audit table's CMSG ACK opcode VAs (real values from
     `Opcode.cs`: `CMSG_MOVE_WATER_WALK_ACK = 0x2D0`,
     `CMSG_MOVE_HOVER_ACK = 0x0F6`,
     `CMSG_MOVE_FEATHER_FALL_ACK = 0x2CF`).
   - **Verification:** 61/61 PacketFlowParity-namespace tests pass
     (Duration: 3 s); the live regression
     `BgPostTeleportStabilizationTests.BgBot_TeleportAboveGround_FallingFlagsClearAndPositionStabilizesWithinBound`
     also passes (1 m 5 s) — Track 1 still holds on the BG side.

---

## Status of the original task list

| Task | Status |
|---|---|
| **Task A** — Pin Shodan character name | Done in v1 (commit `be6331fa`) |
| **Task B** — Teleport ACK byte-layout tests | Done in v1 (commit `1adf5096`) |
| **Task B-fix step 4** — Snapshot-side regression test | Done in v2 (commit `23d54795`) |
| **Task B-fix Track 1** — Drop ground-snap gate from ACK flush | Done in v3 (commit `eda32b09`) |
| **Task C** — Broader BG vs WoW.exe movement parity audit | Documented in v3 (`docs/physics/bg_movement_parity_audit.md`); single divergence found and fixed |
| **Stream 2A** — Movement-flag-toggle queue-first timing tests | Done in v4 (commit `d02a973a`) |
| **Stream 1** — Observer-side visual confirmation of Track 1 | **STILL OPEN** — see below |
| **Stream 2B** — Heartbeat suppression narrowing | Conditional on Stream 1 outcome — do not apply preemptively |

---

## What is still open (for the next session)

### Stream 1 — Observer-side visual confirmation (PRIMARY, BLOCKED IN AUTOMATED HARNESS)

**Blocker:** confirming the third-party-client double-fall is closed
requires a human-driven WoW.exe instance logged in to the local
VMaNGOS realm and positioned near the test target so an observer can
*visually* watch the BG bot get teleported. The BG bot's own
`BgPostTeleportStabilizationTests` snapshot regression confirms the
*server-side* effect of Track 1 (the bot stabilizes after the
teleport), but it cannot rule out the *client-side rendering* effect
of a second falling animation on an observer client. Two prior
sessions (v3 and this one) tried and could not run an observer client
from the automated harness.

**What the next session needs to do, exactly:**

1. Bring up an observer WoW client logged in to the same VMaNGOS realm
   (Shodan or any non-test character). Position it near
   Durotar road `(-460, -4760, 38)` so the BG bot teleport target
   `(-460, -4760, 48)` is on screen.
2. Drive the BG bot through the same teleport scenario via the live
   regression test:

   ```bash
   dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj \
     --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false \
     --filter "FullyQualifiedName~BgPostTeleportStabilizationTests"
   ```

   (The test teleports the BG bot ~10y above ground at
   `(-460, -4760, 48)` and asserts snapshot stabilization. The
   observer is watching the BG bot's character on their own screen.)
3. Visually confirm:
   - **PASS:** fall animation plays ONCE on the observer (or zero
     times if the snap-down was small enough for the server to never
     broadcast a falling state).
   - **FAIL:** fall animation still plays twice → escalate to
     **Stream 2B** below.
4. **If PASS:** add a one-paragraph note to a new
   `docs/handoff_session_bg_movement_parity_followup_v5.md` confirming
   observer-side closure, with the test run timestamp + observer
   character name. Commit:
   `docs(bg-movement): observer-side confirmation of Track 1 closes BG bot double-fall`.
   Strike Stream 1 from the open-task table and consider the original
   double-fall investigation closed.
5. **If FAIL or you can't run an observer client:** add a v5 handoff
   noting the blocker (or the negative observer outcome), and either
   stop here for the user to triage manually or proceed to Stream 2B.

**Do NOT mark the double-fall closed without observer evidence.**
The BG-side snapshot test passes today, but that's not the symptom.

### Stream 2B — Heartbeat suppression narrowing (CONDITIONAL on Stream 1 outcome)

Apply only if Stream 1 visually confirms the double-fall **still
reproduces** after Track 1.

The change: at
[`Exports/WoWSharpClient/Movement/MovementController.cs:379`](../Exports/WoWSharpClient/Movement/MovementController.cs)
(approximately — find the
all-packets-suppressed-while-`_needsGroundSnap` block added in commit
`49915f62`), instead of suppressing every outbound packet, send a
single corrective `MSG_MOVE_HEARTBEAT` with `MOVEFLAG_NONE` at the
teleport target on the first snap frame, then keep the existing
transient-`FALLINGFAR` suppression for the rest of the snap.

Update the parity tests in
[`Tests/WoWSharpClient.Tests/Movement/MovementControllerTests.cs`](../Tests/WoWSharpClient.Tests/Movement/MovementControllerTests.cs)
that currently `Assert.Empty(_sentPackets)` during the snap to allow
exactly one corrective packet (assert byte-exact MovementInfo + flags
== MOVEFLAG_NONE).

Commit separately as
`fix(bg-movement): emit single corrective heartbeat at start of post-teleport ground snap`.

**DO NOT do this preemptively.** It is a behaviour change the audit
does not justify on its own — narrow it only with observer evidence.

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
   you're on `main`, working tree clean. Tip should be `d02a973a
   test(bg-movement): pin queue-first deferred-ACK contract for
   movement-flag-toggle ACKs`.
2. `docker ps` — confirm `mangosd`, `realmd`, `pathfinding-service`,
   `maria-db` healthy.
3. Read in order:
   - This handoff (top to bottom).
   - [`docs/physics/bg_movement_parity_audit.md`](physics/bg_movement_parity_audit.md)
     — every BG-side outbound packet is now PASS (layout + timing).
   - [`docs/physics/state_teleport.md`](physics/state_teleport.md),
     [`docs/physics/msg_move_teleport_handler.md`](physics/msg_move_teleport_handler.md),
     [`docs/physics/packet_ack_timing.md`](physics/packet_ack_timing.md)
     — the binary-parity reference for teleport ACK gating.
   - [`CLAUDE.md`](../CLAUDE.md) and the per-repo `CLAUDE.md` for
     repo-wide rules.
   - [`memory/MEMORY.md`](C:/Users/lrhod/.claude/projects/e--repos-Westworld-of-Warcraft/memory/MEMORY.md)
     — the teleport-ACK gating rule is now load-bearing in memory.
4. **Open work, in order:**
   - **Stream 1 — Observer-side confirmation** of Track 1 (if a
     human-driven WoW client is available — see explicit steps above).
     If not available, leave a note in `v5` flagging the blocker.
   - **Stream 2B — Heartbeat suppression narrowing** — only if
     Stream 1 visually confirms the double-fall *still* reproduces
     after Track 1. Do not apply preemptively.
5. Commit and push after each unit. Don't accumulate.

---

## When you near context exhaustion

Write the next followup handoff at
`docs/handoff_session_bg_movement_parity_followup_v5.md`,
self-contained as this one is. Include:

1. Concise restatement of the goal.
2. Current commit hash and a short summary of what's been done since this prompt was written.
3. The remaining tasks, with completed work moved to a "Done" appendix or referenced via prior handoff.
4. Any new blockers, surprises, or design pivots.
5. Repeat all the **Hard rules** above.
6. Repeat **this** "When you near context exhaustion" instruction so the next agent does the same.

The user copies/pastes the latest handoff into a fresh session.
**Make it self-contained** — assume the next agent has zero memory.
