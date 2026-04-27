# Handoff (followup v8): BG bot teleport double-fall — Stream 2C closed; remaining work is Stream 2D (BG-side baseline), Stream 3 (live re-verification), Stream 4 (other teleport scenarios)

> **How to use this:** copy/paste this file (top to bottom) into a fresh
> Claude Code session in `e:\repos\Westworld of Warcraft`. Read it in
> full before touching anything. Older context lives in
> [`handoff_session_bg_movement_parity.md`](handoff_session_bg_movement_parity.md)
> (the original) and the v1–v7 follow-ups
> ([v1](handoff_session_bg_movement_parity_followup.md),
> [v2](handoff_session_bg_movement_parity_followup_v2.md),
> [v3](handoff_session_bg_movement_parity_followup_v3.md),
> [v4](handoff_session_bg_movement_parity_followup_v4.md),
> [v5](handoff_session_bg_movement_parity_followup_v5.md),
> [v6](handoff_session_bg_movement_parity_followup_v6.md),
> [v7](handoff_session_bg_movement_parity_followup_v7.md)).

---

## Why this work exists (restated)

A third-party WoW client observing a BG bot after teleport reported the
falling animation playing **twice** — once on local prediction
immediately after teleport, then again when the authoritative position
update arrived showing the bot still mid-air.

**Current parity state (post-Stream-2C):** the BG outbound packet
stream during the post-teleport window is byte-for-opcode-name
identical to WoW.exe (FG fixture). The local-vs-authoritative
double-render should no longer occur. **What remains is verification
and coverage expansion**, not a known parity bug.

---

## Done since v7 (this session)

Run `git log --oneline -10` for the actual tip. Recent commits on `main`:

1. **`03e7a204` — `feat(bg-movement): expose NotifyExternalPacketSent + flag-change suppression`**
   - Adds `MovementController.NotifyExternalPacketSent()` public API and
     a `_suppressFlagChangeUntilMs` field. Behaviourally inert until
     wired up (suppression defaults to 0; `IsBefore(gameTimeMs, 0)` is
     false for any positive `gameTimeMs`).
   - Modifies `ShouldSendPacket` so the flag-change branch returns true
     only when `!IsBefore(gameTimeMs, _suppressFlagChangeUntilMs)`. The
     cadence and auto-attack branches are unchanged.

2. **`e248f1ce` — `fix(bg-movement): cadence-gate post-teleport first heartbeat (Stream 2C)`**
   - Calls `MovementController.NotifyExternalPacketSent()` from
     [`WoWSharpObjectManager.Movement.cs:765`](../Exports/WoWSharpClient/WoWSharpObjectManager.Movement.cs)
     immediately after `_woWClient.SendMSGPackedAsync(MSG_MOVE_TELEPORT_ACK, ...)`.
   - Updates `Background_AfterTeleportTrigger_OutboundStream_StructurallyMatchesForegroundBaseline`
     to assert `Assert.Equal(fgOutboundOpcodes, bgOutboundOpcodes)` —
     ordered byte-for-opcode-name equality.
   - **Implementation note:** `NotifyExternalPacketSent()` deliberately
     takes no parameter and uses `_latestGameTimeMs` internally to
     bind the suppression window to the controller's own frame
     timebase. This is robust to clock-source mismatches between
     `_worldTimeTracker.NowMS` (used by the ACK builder) and the
     `gameTimeMs` passed to `Update`.

3. **`9665dfe5` — `fix(fg-diagnostics): correct swapped opcode names in PacketLogger.GetOpcodeName`**
   - `0x00C9` was labelled as `MSG_MOVE_TELEPORT_ACK` and `0x00C7` was
     missing entirely. Per `GameData.Core/Enums/Opcode.cs`:
     `0x0C7 = MSG_MOVE_TELEPORT_ACK`, `0x0C9 = MSG_MOVE_FALL_LAND`.
     Diag-log only — corpus recorder uses the typed `Opcode` enum.

4. **`0b193514` — `docs(bg-movement-parity): mark Stream 2C closed in parity audit`**
   - Updates [`docs/physics/bg_movement_parity_audit.md`](physics/bg_movement_parity_audit.md)
     to reflect the +1 HB gap is closed.

5. **`2c852553` — `test(ack-corpus): expand MSG_MOVE_TELEPORT_ACK golden oracle (+9 captures)`**
   - Commits 9 `ForegroundAckCorpusRecorder` captures from 2026-04-27
     that had been accumulating uncommitted in the working tree.
     All 21 (12 prior + 9 new) pass `TeleportAck_MatchesWoWExeBytes`.

6. **(this commit)** — v8 handoff doc.

**Validation:** `dotnet test Tests/WoWSharpClient.Tests` →
`Passed: 1611, Skipped: 1, Failed: 0`. Plus
`Tests/Navigation.Physics.Tests` (152 passed, 1 skipped) and
`Tests/RecordedTests.PathingTests.Tests` (135 passed).

---

## Stream 2C — Frozen result

After Stream 2C, the BG and FG outbound streams during the
post-teleport window are identical:

| Step | FG (WoW.exe) | BG (after Stream 2C) |
|------|--------------|----------------------|
| 1 | `MSG_MOVE_TELEPORT_ACK` (deltaMs=6) | `MSG_MOVE_TELEPORT_ACK` |
| 2 | `MSG_MOVE_HEARTBEAT` (≈491ms) | `MSG_MOVE_HEARTBEAT` |
| 3 | `MSG_MOVE_HEARTBEAT` (≈991ms) | `MSG_MOVE_HEARTBEAT` |
| 4 | `MSG_MOVE_FALL_LAND` (≈1271ms) | `MSG_MOVE_FALL_LAND` |

Pinned by
[`Tests/WoWSharpClient.Tests/Parity/PostTeleportPacketWindowParityTests.cs`](../Tests/WoWSharpClient.Tests/Parity/PostTeleportPacketWindowParityTests.cs)
`Background_AfterTeleportTrigger_OutboundStream_StructurallyMatchesForegroundBaseline`
via `Assert.Equal(fgOutboundOpcodes, bgOutboundOpcodes)`.

---

## Open work, in priority order

### Stream 2D — Capture a BG-side post-teleport baseline as a second oracle

**Why first:** BG parity today is pinned only against the FG fixture.
That makes the test useful for *FG drift* detection but not for
*BG drift* detection — if BG starts diverging from itself silently
(e.g., a refactor shifts a heartbeat by one tick), the FG fixture
won't notice until the divergence becomes large enough to break
ordered equality. A captured BG fixture closes that loop.

**What you're doing:** add a BG-side recorder analogous to
[`Services/ForegroundBotRunner/Diagnostics/ForegroundPostTeleportWindowRecorder.cs`](../Services/ForegroundBotRunner/Diagnostics/ForegroundPostTeleportWindowRecorder.cs),
gate it on `WWOW_CAPTURE_BG_POST_TELEPORT_WINDOW=1`, and run it during
a live BG bot teleport to produce
`Tests/WoWSharpClient.Tests/Fixtures/post_teleport_packet_window/background_durotar_vertical_drop_baseline.json`.
Then add a `BackgroundBaseline_*` parity test that pins the BG
fixture's expected shape (analogous to
`ForegroundBaseline_ReportsExpectedTeleportPacketSequence`).

**Concrete steps:**

1. **Read the FG recorder.** Open
   [`ForegroundPostTeleportWindowRecorder.cs`](../Services/ForegroundBotRunner/Diagnostics/ForegroundPostTeleportWindowRecorder.cs)
   end-to-end. It hooks `NetClient::Send`/`NetClient::ProcessMessage`
   via `PacketLogger`, opens a 2.5s window when an inbound
   `MSG_MOVE_TELEPORT_ACK` arrives, captures every packet in/out, and
   writes a JSON fixture matching the schema the parity test
   deserializes (`PostTeleportWindowFixture` in
   `PostTeleportPacketWindowParityTests.cs`).

2. **Find the BG-side equivalents of `NetClient::Send` /
   `ProcessMessage`.** BG sends via `WoWClient.SendOpcodeAsync` /
   `SendMSGPackedAsync`; receives via the dispatcher in
   [`Exports/WoWSharpClient/Networking/`](../Exports/WoWSharpClient/Networking/).
   Look for a single chokepoint that sees both inbound and outbound
   packets — likely
   [`OpCodeDispatcher`](../Exports/WoWSharpClient/OpCodeDispatcher.cs)
   for inbound and `WoWClient` outbound.

3. **Implement `BackgroundPostTeleportWindowRecorder`** in
   `Services/BackgroundBotRunner/Diagnostics/` (new directory if
   needed). Mirror the FG recorder's API:
   - `EnableEnvVar = "WWOW_CAPTURE_BG_POST_TELEPORT_WINDOW"`
   - `OutputDirEnvVar = "WWOW_BG_POST_TELEPORT_OUTPUT"`
   - Trigger on inbound `MSG_MOVE_TELEPORT_ACK` (or
     `MSG_MOVE_TELEPORT`, matching the FG recorder).
   - Write the same `PostTeleportWindowFixture` JSON schema so the
     parity test can deserialize either FG or BG fixtures with the
     same loader.

4. **Capture a real baseline.** With Docker MaNGOS running
   (`docker ps`), launch a BG bot, run a teleport that drops it into
   open air (Durotar Z+10 worked for the FG capture), let the
   recorder write the fixture, copy it into
   `Tests/WoWSharpClient.Tests/Fixtures/post_teleport_packet_window/background_durotar_vertical_drop_baseline.json`.

   **Caveat:** the captured packet bytes will differ from FG (different
   GUID, different `clientTimeMs`, possibly different
   payload-meaningless padding). What you want is the **opcode shape +
   timing** to match FG. The parity test at the schema level only
   compares opcode names and direction, so byte-level differences are
   fine.

5. **Add a `BackgroundBaseline_*` parity test** alongside
   `ForegroundBaseline_*` that asserts the BG fixture's packet
   sequence is `[TELEPORT_ACK, HEARTBEAT, HEARTBEAT, FALL_LAND]` with
   timing comparable to FG (first HB ~500ms, FALL_LAND between 1-2s).
   Then update
   `Background_AfterTeleportTrigger_OutboundStream_StructurallyMatchesForegroundBaseline`
   to ALSO load the BG fixture and assert the synthetic
   `RunPhysicsFor` stream matches the live BG fixture (not just FG).

6. **Run the WoWSharpClient + Navigation.Physics + RecordedTests
   suites** to confirm no regression. Expect the new BG-baseline
   test to pass.

7. **Commit per logical units:**
   - `feat(bg-diag): add BackgroundPostTeleportWindowRecorder`
   - `test(bg-movement-parity): add BG-side post-teleport baseline + parity assertions`

### Stream 3 — Re-verify live (no code, just live capture)

**Why second:** the v7 handoff explicitly asked for a live re-run with
`WWOW_CAPTURE_POST_TELEPORT_WINDOW=1` to confirm Stream 2C didn't
drift the FG oracle. Strictly speaking, Stream 2C only changed BG
code (`MovementController` + `WoWSharpObjectManager.Movement`), so FG
cannot have drifted — but capturing a fresh FG baseline against the
live MaNGOS stack and diffing it against
[`foreground_durotar_vertical_drop_baseline.json`](../Tests/WoWSharpClient.Tests/Fixtures/post_teleport_packet_window/foreground_durotar_vertical_drop_baseline.json)
is the clean way to prove that.

**Concrete steps:**

1. `docker ps` — confirm `mangosd`, `realmd`,
   `pathfinding-service`, `maria-db` healthy.
2. Kill any running WoW.exe (`tasklist //FI "IMAGENAME eq WoW.exe"
   //FO LIST` then `taskkill //F //PID <pid>` for YOUR PIDs only).
3. Set `WWOW_CAPTURE_POST_TELEPORT_WINDOW=1` in the env, run
   `Foreground_VerticalDropTeleport_CapturesTeleportAckAndSnapWindow`
   (in
   [`Tests/BotRunner.Tests/LiveValidation/AckCaptureTests.cs:105`](../Tests/BotRunner.Tests/LiveValidation/AckCaptureTests.cs)).
4. Diff the freshly-captured fixture against the committed baseline
   — same opcodes in the same order, similar timing (±50ms is fine).
   If they match, no further action. If they don't, escalate
   immediately because something drifted.
5. **Optionally** run the live BG vertical-drop test (if one exists,
   or once Stream 2D ships one) with a third-party-client observer
   open and confirm the falling animation no longer plays twice.

This stream is mostly verification — don't write code unless the live
capture surfaces a real regression.

### Stream 4 — Other teleport scenarios

**Why third:** the parity infrastructure only covers vertical-drop
teleport. Cross-map teleports, transport mounting, and
knockback-driven teleports have not been characterized against
WoW.exe and may have analogous double-fall (or worse) issues.

**Concrete steps for each scenario:**

1. **Capture an FG baseline** with
   `WWOW_CAPTURE_POST_TELEPORT_WINDOW=1` in the relevant scenario
   (cross-map teleport via `.tele name`, transport mount via boarding
   a zeppelin, knockback via `.knockback` GM command). Save as
   `foreground_<scenario>_baseline.json`.
2. **Add a parity test** that loads the new fixture and asserts the
   captured opcode shape is what we expect. (Often this surfaces a
   surprise — a heartbeat we didn't know about, an extra inbound
   packet, etc.)
3. **Run the BG-side equivalent** through `PacketFlowTraceFixture`
   and the synthetic physics override, and pin the BG outbound
   stream against the FG fixture (the same
   `PostTeleportPacketWindowParityTests` shape, parameterized over
   scenario name).
4. **If a divergence shows up**, that's a new parity bug — root-cause
   it in MovementController / WoWSharpObjectManager / handlers and
   close the gap, just like Stream 2A/B/C did for the
   vertical-drop case.

This stream is open-ended — could be 1 commit per scenario or many
depending on what's found.

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

1. `git status && git fetch origin && git log --oneline -10` — confirm
   you're on `main`, working tree clean. Tip should be the v8 doc
   commit landing on top of `2c852553 test(ack-corpus): expand
   MSG_MOVE_TELEPORT_ACK golden oracle (+9 captures)`.
2. `docker ps` — confirm `mangosd`, `realmd`, `pathfinding-service`,
   `maria-db` healthy.
3. Read in order:
   - This handoff (top to bottom).
   - [`docs/physics/bg_movement_parity_audit.md`](physics/bg_movement_parity_audit.md)
     — current per-opcode status (Stream 2B + 2C closed).
   - [`Tests/WoWSharpClient.Tests/Parity/PostTeleportPacketWindowParityTests.cs`](../Tests/WoWSharpClient.Tests/Parity/PostTeleportPacketWindowParityTests.cs)
     — what FG vs BG parity looks like today.
   - [`Tests/WoWSharpClient.Tests/Fixtures/post_teleport_packet_window/foreground_durotar_vertical_drop_baseline.json`](../Tests/WoWSharpClient.Tests/Fixtures/post_teleport_packet_window/foreground_durotar_vertical_drop_baseline.json)
     — the FG oracle.
   - [`Services/ForegroundBotRunner/Diagnostics/ForegroundPostTeleportWindowRecorder.cs`](../Services/ForegroundBotRunner/Diagnostics/ForegroundPostTeleportWindowRecorder.cs)
     — the recorder you're cloning for the BG side (Stream 2D).
   - [`Exports/WoWSharpClient/Movement/MovementController.cs`](../Exports/WoWSharpClient/Movement/MovementController.cs)
     — `NotifyExternalPacketSent` (line ~1019), `ShouldSendPacket`
     (line ~1037), `Reset` (line ~1356).
   - [`Exports/WoWSharpClient/WoWSharpObjectManager.Movement.cs`](../Exports/WoWSharpClient/WoWSharpObjectManager.Movement.cs)
     — `TryFlushPendingTeleportAck` (line ~725) where the new hook is
     wired up.
   - [`docs/physics/state_teleport.md`](physics/state_teleport.md),
     [`memory/movement_physics.md`](C:/Users/lrhod/.claude/projects/e--repos-Westworld-of-Warcraft/memory/movement_physics.md).
   - [`CLAUDE.md`](../CLAUDE.md) and per-repo `CLAUDE.md` for
     repo-wide rules.
   - [`memory/MEMORY.md`](C:/Users/lrhod/.claude/projects/e--repos-Westworld-of-Warcraft/memory/MEMORY.md).
4. **Open work, in priority order:**
   - **Stream 2D** — BG-side post-teleport baseline + parity test
     (concrete steps above).
   - **Stream 3** — live re-verification of FG baseline drift (cheap
     verification step, requires WoW.exe + env flag).
   - **Stream 4** — apply recorder + diff workflow to other teleport
     scenarios (cross-map, transport, knockback). Open-ended.
5. Commit and push after each unit. Don't accumulate.

---

## When you near context exhaustion

Write the next followup handoff at
`docs/handoff_session_bg_movement_parity_followup_v9.md`,
self-contained as this one is. Include:

1. Concise restatement of the goal.
2. Current commit hash and a short summary of what's been done since
   this prompt was written.
3. The remaining tasks, with completed work moved to a "Done"
   appendix or referenced via prior handoff.
4. Any new blockers, surprises, or design pivots.
5. Repeat all the **Hard rules** above.
6. Repeat **this** "When you near context exhaustion" instruction so
   the next agent does the same.

The user copies/pastes the latest handoff into a fresh session.
**Make it self-contained** — assume the next agent has zero memory.
