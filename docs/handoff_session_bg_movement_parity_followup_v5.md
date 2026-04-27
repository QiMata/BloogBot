# Handoff (followup v5): BG bot teleport double-fall — pivot Stream 1 to FG packet capture + BG diff (no human-driven client needed)

> **How to use this:** copy/paste this file (top to bottom) into a fresh
> Claude Code session in `e:\repos\Westworld of Warcraft`. Read it in
> full before touching anything. Older context lives in
> [`handoff_session_bg_movement_parity.md`](handoff_session_bg_movement_parity.md)
> (the original), [`handoff_session_bg_movement_parity_followup.md`](handoff_session_bg_movement_parity_followup.md) (v1),
> [`handoff_session_bg_movement_parity_followup_v2.md`](handoff_session_bg_movement_parity_followup_v2.md) (v2),
> [`handoff_session_bg_movement_parity_followup_v3.md`](handoff_session_bg_movement_parity_followup_v3.md) (v3),
> and [`handoff_session_bg_movement_parity_followup_v4.md`](handoff_session_bg_movement_parity_followup_v4.md) (v4).
> Read v3 first if anything below is unclear — it has the deepest diagnosis.
> v4's "needs a human-driven WoW client" framing for Stream 1 is **superseded** by this v5 — see "Pivot" below.

---

## Pivot from v4

v4 framed Stream 1 (observer-side confirmation that Track 1 closes the
double-fall) as **blocked on a human-driven WoW client** that visually
watches the BG bot get teleported. That framing was wrong. The
authoritative answer to "is there a parity divergence" lives in **what
WoW.exe actually emits on the wire** during the post-teleport snap
window — not in what an observer's screen renders. Both pieces of data
are derivable from packet captures alone, and the FG bot is the
binary, hooked, and already wired for capture.

**New Stream 1 plan:** capture the FG bot's full inbound + outbound
packet stream during the same vertical-drop teleport scenario as the
BG live regression, then capture BG's stream against the same scenario,
then **diff**. The FG stream is the binary's authoritative behaviour;
BG is parity-correct iff its outbound stream matches FG's structure
through the snap window.

This is more rigorous than visual observation (no subjective rendering
calls), reproducible (no human in the loop), and produces a recorded
fixture that can be replayed as a regression test forever after.

---

## Why this work exists (restated)

A third-party WoW client observing a BG bot after teleport reports the
falling animation playing **twice** — once on local prediction
immediately after teleport, then a second time when the authoritative
position update arrives showing the bot still in mid-air.

Diagnosis (frozen at v2/v3): the BG bot was silent for 30–60 physics
frames after a teleport because `TryFlushPendingTeleportAck` gated on
`_movementController.NeedsGroundSnap`, which is strictly more
conservative than WoW.exe's `0x468570` readiness function (per
[`docs/physics/state_teleport.md`](physics/state_teleport.md)).

**Track 1 (commit `eda32b09`)** removed the rogue gate. The BG-side
unit + live regression suites confirm the BG bot now ACKs on the same
readiness gates as the binary. **Stream 1 is about confirming this is
sufficient** — i.e. that the binary doesn't *also* emit corrective
heartbeats during the snap window that BG's
`MovementController:379`-area suppression is currently dropping.

---

## Done since v3

Run `git log --oneline -10` for the actual tip. Recent commits:

1. **`d02a973a` — `test(bg-movement): pin queue-first deferred-ACK contract for movement-flag-toggle ACKs`** (v4)
   - Adds `[Theory] PacketFlowParityTests.MovementFlagToggleFamily_QueuesDeferredAck_ThenFlushesWithUpdatedFlag`
     covering `SMSG_MOVE_{WATER_WALK,LAND_WALK,SET_HOVER,UNSET_HOVER,FEATHER_FALL,NORMAL_FALL}`
     → `CMSG_MOVE_{WATER_WALK,HOVER,FEATHER_FALL}_ACK` with byte-exact
     payload (incl. trailing 1.0/0.0 marker) — closes the timing half of
     the audit's "PASS layout / PARTIAL timing" rows.
   - Wires the six missing inbound toggle opcodes into
     [`PacketFlowTraceFixture.ResolveHandler`](../Tests/WoWSharpClient.Tests/Parity/PacketFlowTraceFixture.cs).
   - [`docs/physics/bg_movement_parity_audit.md`](physics/bg_movement_parity_audit.md):
     toggle-ACK rows flipped to PASS; opcode VAs corrected against
     `Opcode.cs` (`CMSG_MOVE_WATER_WALK_ACK = 0x2D0`,
     `CMSG_MOVE_HOVER_ACK = 0x0F6`,
     `CMSG_MOVE_FEATHER_FALL_ACK = 0x2CF`).
   - Verification: 61/61 PacketFlowParity tests pass; live
     `BgPostTeleportStabilizationTests` passes (1 m 5 s) — Track 1
     still holds on the BG side.

2. **`f1755179` — `docs(handoff): v4`** — superseded by this v5.

---

## Status of the original task list

| Task | Status |
|---|---|
| Task A — Pin Shodan character name | Done in v1 (`be6331fa`) |
| Task B — Teleport ACK byte-layout tests | Done in v1 (`1adf5096`) |
| Task B-fix step 4 — Snapshot-side regression test | Done in v2 (`23d54795`) |
| Task B-fix Track 1 — Drop ground-snap gate from ACK flush | Done in v3 (`eda32b09`) |
| Task C — Broader BG vs WoW.exe movement parity audit | Documented in v3 + Stream 2A (`d02a973a`); single divergence found and fixed |
| Stream 2A — Movement-flag-toggle queue-first timing tests | Done in v4 (`d02a973a`) |
| **Stream 1** — FG packet capture + BG diff | **OPEN — see below** |
| Stream 2B — Heartbeat suppression narrowing | Conditional on Stream 1 outcome |

---

## Stream 1 — FG packet capture + BG diff (PRIMARY, ALL AUTOMATED)

### What you're answering

After the BG bot receives `MSG_MOVE_TELEPORT` and the physics
ground-snap begins, BG currently **suppresses all outbound packets**
until the snap completes (introduced in commit `49915f62`). The
question for parity:

> Does WoW.exe also go silent during the snap, or does it emit one
> (or more) corrective `MSG_MOVE_HEARTBEAT` packets at the teleport
> target with `MOVEFLAG_NONE` while the snap is in progress?

- **If FG goes silent** during the snap → BG's current behaviour
  matches the binary; Track 1 alone is the fix; close out the
  double-fall investigation and update the audit. Stream 2B is moot.
- **If FG emits N corrective heartbeats** with `MOVEFLAG_NONE` at the
  teleport target → BG diverges; this is the second contributing piece
  v3 hypothesised. Apply Stream 2B with the exact count + flag pattern
  observed in the FG capture.

### What infrastructure already exists

- [`Services/ForegroundBotRunner/Mem/Hooks/PacketLogger.cs`](../Services/ForegroundBotRunner/Mem/Hooks/PacketLogger.cs)
  (~991 lines) — captures **both** inbound (`NetClient::ProcessMessage`
  hook at `0x537AA0`) and outbound (`NetClientSend` hook at
  `0x005379A0`), opcode + size + timestamp + payload, into
  `WWoWLogs/packet_logger.log`. This is the BINARY's authoritative
  packet stream.
- [`Services/ForegroundBotRunner/Diagnostics/ForegroundAckCorpusRecorder.cs`](../Services/ForegroundBotRunner/Diagnostics/ForegroundAckCorpusRecorder.cs)
  (~272 lines) — when env `WWOW_CAPTURE_ACK_CORPUS=1`, persists ACK
  packets into the
  [`Tests/WoWSharpClient.Tests/Fixtures/ack_golden_corpus/`](../Tests/WoWSharpClient.Tests/Fixtures/ack_golden_corpus)
  tree as JSON fixtures (one per opcode subdirectory). MSG_MOVE_TELEPORT_ACK
  fixtures will land here.
- [`Tests/BotRunner.Tests/LiveValidation/AckCaptureTests.cs`](../Tests/BotRunner.Tests/LiveValidation/AckCaptureTests.cs)
  (~381 lines) — already-working pattern for FG-driven capture. The
  existing `Foreground_CrossMapTeleport_CapturesWorldportAckWhenCorpusEnabled`
  drives an FG bot Org → Ironforge to capture
  `MSG_MOVE_WORLDPORT_ACK`. **Clone this for a vertical-drop
  same-map teleport that captures `MSG_MOVE_TELEPORT_ACK` and the
  full surrounding packet window.**
- The BG-side regression that already exercises the exact scenario:
  [`Tests/BotRunner.Tests/.../BgPostTeleportStabilizationTests`](../Tests/BotRunner.Tests/) —
  teleports BG to `(-460, -4760, 48)` (~10y above Durotar road
  ground) and asserts snapshot stabilization.

### Concrete plan (do these in order)

**Step 1 — Add an FG vertical-drop capture test.** Clone
`Foreground_CrossMapTeleport_CapturesWorldportAckWhenCorpusEnabled` in
`AckCaptureTests.cs` into a new `Foreground_VerticalDropTeleport_CapturesTeleportAckAndSnapWindow`
that:

- Stages the FG bot at Durotar road `(-460, -4760, 38)` via
  `StageForegroundCapturePointAsync` with `cleanSlate: true`.
- Triggers a same-map teleport ~10y above ground:
  `BotTeleportAsync(target.AccountName, mapId: 1, -460f, -4760f, 48f)`
  (matches the BG regression target exactly).
- After the teleport, polls the snapshot for stabilization with the
  same predicate as the BG regression.
- Asserts that an `MSG_MOVE_TELEPORT_ACK` fixture appears under
  `Tests/WoWSharpClient.Tests/Fixtures/ack_golden_corpus/MSG_MOVE_TELEPORT_ACK/`
  (when `WWOW_CAPTURE_ACK_CORPUS=1`).
- Returns the FG bot to Org via the same `try/finally` pattern.

Run with `WWOW_CAPTURE_ACK_CORPUS=1` set in the test environment (the
existing AckCaptureTests already document the pattern).

**Step 2 — Capture the surrounding packet window from
`packet_logger.log`.** The corpus fixture only captures the ACK
itself; you need the **non-ACK outbound packets immediately after**
to answer the corrective-heartbeat question. After Step 1's run:

- Read `WWoWLogs/packet_logger.log` (it's the FG-side log; check
  `Services/ForegroundBotRunner/Program.cs` and
  `StateManagerWorker.BotManagement.cs` for the actual write path —
  search for `WWoWLogs` to confirm).
- Slice the window from the inbound `MSG_MOVE_TELEPORT` line to ~2 s
  after the outbound `MSG_MOVE_TELEPORT_ACK`.
- Tabulate every outbound opcode in that window: timestamp delta from
  the ACK, opcode name, size. You're particularly looking for
  `MSG_MOVE_HEARTBEAT`, `MSG_MOVE_FALL_LAND`, and any movement state
  transition.
- Persist this slice to disk under
  `Tests/WoWSharpClient.Tests/Fixtures/post_teleport_packet_window/foreground_baseline.json`
  (or similar — pick a path that's clearly tied to this scenario).
  The shape should be `{ direction, opcodeName, timestampMs, size,
  payloadHex }[]` so it can be replayed as a regression fixture.
  **Keep it small** — only the post-teleport window, not the whole
  log.

**Step 3 — Capture the BG bot's equivalent window.** Either reuse
`BgPostTeleportStabilizationTests` (if it already records a
sufficient packet trace via `_woWClient.SendMSGPackedAsync` hooks) or
add a parallel capture path. The simplest:

- Wire a `Mock<IWorldClient>.Setup(x => x.SendMSGPackedAsync(...))`
  callback into the BG live test that records each outbound packet
  with timestamp into the same JSON shape as Step 2.
- Persist as
  `Tests/WoWSharpClient.Tests/Fixtures/post_teleport_packet_window/background_actual.json`.

(If `IWorldClient` does not surface `SendMSGPackedAsync` cleanly in
the live path, hook one level deeper at the `WoWClient` layer.
`PacketFlowTraceFixture` shows the pattern for capturing outbound via
a Moq callback — same shape.)

**Step 4 — Diff.** Add a unit test in
`Tests/WoWSharpClient.Tests/Parity/` named something like
`PostTeleportPacketWindowParityTests` that:

- Loads both fixtures.
- Aligns on the `MSG_MOVE_TELEPORT_ACK` event in each.
- Asserts: outbound opcode sequence post-ACK is structurally equal
  (count + opcode names; payload byte-equality is too strict because
  client times will differ).
- If equal → record this in the audit doc and **commit** the
  fixtures so they pin the binary's behaviour as a regression
  test. **This is the desired outcome and is the answer the user is
  looking for.**

**Step 5 — Decide based on the diff.**

- **FG outbound count == BG outbound count == 0 in the snap window**
  → Track 1 alone is the binary-parity fix. Update the audit doc to
  state "Stream 1 confirmed via FG packet capture; binary goes
  silent during snap; BG matches." Skip Stream 2B entirely. Commit:
  `docs(bg-movement): FG packet capture confirms Track 1 closes BG
  bot double-fall (binary goes silent during snap)`.
- **FG emits N corrective MSG_MOVE_HEARTBEATs** at the teleport
  target with `MOVEFLAG_NONE` → proceed to Stream 2B with the exact
  observed count + flag pattern. The fixture you captured in Step 2
  becomes the parity oracle.
- **FG emits something else entirely** (e.g. MSG_MOVE_FALL_LAND
  immediately, no heartbeats) → that's the binary's actual snap
  protocol; mirror exactly. Update the audit doc to describe the
  new shape.

### Where to drive the test from

The existing `AckCaptureTests` runs in the
`LiveValidationCollection`. The Docker MaNGOS stack is always
running (`docker ps` to confirm) so this is just:

```bash
docker ps  # confirm mangosd, realmd, pathfinding-service, maria-db are up
cd "e:/repos/Westworld of Warcraft"
WWOW_CAPTURE_ACK_CORPUS=1 dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj \
  --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false \
  --filter "FullyQualifiedName~Foreground_VerticalDropTeleport_CapturesTeleportAckAndSnapWindow"
```

Note the `WWOW_CAPTURE_ACK_CORPUS=1` env var — without it, the ACK
corpus recorder does not persist fixtures.

---

## Stream 2B — Heartbeat suppression narrowing (CONDITIONAL)

Apply only if Stream 1's diff shows FG emits corrective packets that
BG suppresses.

The change: at
[`Exports/WoWSharpClient/Movement/MovementController.cs`](../Exports/WoWSharpClient/Movement/MovementController.cs)
(approximately line 379 — find the all-packets-suppressed-while-`_needsGroundSnap`
block added in commit `49915f62`), instead of suppressing every
outbound packet, emit the **exact pattern** observed in the FG capture
(typically a single `MSG_MOVE_HEARTBEAT` with `MOVEFLAG_NONE` at the
teleport target on the first snap frame, then continue suppressing
transient `FALLINGFAR` for the remainder of the snap).

Update the parity tests in
[`Tests/WoWSharpClient.Tests/Movement/MovementControllerTests.cs`](../Tests/WoWSharpClient.Tests/Movement/MovementControllerTests.cs)
that currently `Assert.Empty(_sentPackets)` during the snap to allow
exactly the observed packet(s) (assert byte-exact MovementInfo + flags
matching the FG capture).

Commit separately as
`fix(bg-movement): emit corrective heartbeat at start of post-teleport ground snap to match FG packet capture`.

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
   you're on `main`, working tree clean. Tip should include
   `f1755179 docs(handoff): v4` and (after committing this) the v5
   doc commit.
2. `docker ps` — confirm `mangosd`, `realmd`, `pathfinding-service`,
   `maria-db` healthy.
3. Read in order:
   - This handoff (top to bottom).
   - [`Tests/BotRunner.Tests/LiveValidation/AckCaptureTests.cs`](../Tests/BotRunner.Tests/LiveValidation/AckCaptureTests.cs)
     — the existing FG-driven capture pattern to clone (read at
     least the first ~170 lines for the helper APIs).
   - [`Services/ForegroundBotRunner/Diagnostics/ForegroundAckCorpusRecorder.cs`](../Services/ForegroundBotRunner/Diagnostics/ForegroundAckCorpusRecorder.cs)
     — what the corpus recorder captures, what env var enables it,
     where fixtures land.
   - [`Services/ForegroundBotRunner/Mem/Hooks/PacketLogger.cs`](../Services/ForegroundBotRunner/Mem/Hooks/PacketLogger.cs)
     — the raw packet log that captures EVERY direction of EVERY
     opcode. This is your source of truth for the post-teleport
     packet window in Step 2.
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
   - **Stream 1 — FG vertical-drop teleport capture + window slice + BG diff** (Steps 1–5 above). All automated; no human in the loop. The desired finished artifact is a committed parity fixture that pins the FG behaviour and a `PostTeleportPacketWindowParityTests` (or similarly named) test that asserts BG matches.
   - **Stream 2B** — only if the Stream 1 diff reveals a divergence.
5. Commit and push after each unit. Don't accumulate.

---

## When you near context exhaustion

Write the next followup handoff at
`docs/handoff_session_bg_movement_parity_followup_v6.md`,
self-contained as this one is. Include:

1. Concise restatement of the goal.
2. Current commit hash and a short summary of what's been done since this prompt was written.
3. The remaining tasks, with completed work moved to a "Done" appendix or referenced via prior handoff.
4. Any new blockers, surprises, or design pivots.
5. Repeat all the **Hard rules** above.
6. Repeat **this** "When you near context exhaustion" instruction so the next agent does the same.

The user copies/pastes the latest handoff into a fresh session.
**Make it self-contained** — assume the next agent has zero memory.
