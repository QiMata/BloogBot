# Handoff (followup v9): BG bot teleport double-fall — Streams 2C & 2D & 3 closed; next is Stream 2E (FG/BG live drift) and Stream 4 (other teleport scenarios)

> **How to use this:** copy/paste this file (top to bottom) into a fresh
> Claude Code session in `e:\repos\Westworld of Warcraft`. Read it in
> full before touching anything. Older context lives in
> [`handoff_session_bg_movement_parity.md`](handoff_session_bg_movement_parity.md)
> and the v1–v8 follow-ups
> ([v1](handoff_session_bg_movement_parity_followup.md),
> [v2](handoff_session_bg_movement_parity_followup_v2.md),
> [v3](handoff_session_bg_movement_parity_followup_v3.md),
> [v4](handoff_session_bg_movement_parity_followup_v4.md),
> [v5](handoff_session_bg_movement_parity_followup_v5.md),
> [v6](handoff_session_bg_movement_parity_followup_v6.md),
> [v7](handoff_session_bg_movement_parity_followup_v7.md),
> [v8](handoff_session_bg_movement_parity_followup_v8.md)).

---

## Why this work exists (restated)

A third-party WoW client observing a BG bot after teleport reported the
falling animation playing **twice** — once on local prediction
immediately after teleport, then again when the authoritative position
update arrived showing the bot still mid-air.

**Current parity state (post-Stream-2D + post-Stream-3):**

- Stream 2C (synthetic parity) is closed: BG's outbound stream under a
  scripted free-fall physics override is byte-for-opcode-name identical
  to the FG baseline (`[TELEPORT_ACK, HEARTBEAT, HEARTBEAT, FALL_LAND]`).
- Stream 2D (BG live baseline) is closed: a BG-side recorder now exists
  and a live BG-captured baseline is committed at
  `Tests/WoWSharpClient.Tests/Fixtures/post_teleport_packet_window/background_durotar_vertical_drop_baseline.json`,
  pinned by `BackgroundBaseline_ReportsLiveCapturedTeleportPacketSequence`.
- Stream 3 is closed: a fresh FG capture (this session, 2026-04-27) was
  diffed against the committed FG baseline and matches in outbound
  opcode order with timing drift ≤±10ms — Stream 2C did not drift the
  FG oracle.
- **Stream 2E (NEW) is open:** the BG live capture revealed that BG and
  FG diverge in real-world teleports despite passing the synthetic
  parity test. The BG live shape is
  `[CMSG_SET_ACTIVE_MOVER, MSG_MOVE_HEARTBEAT, MSG_MOVE_TELEPORT_ACK]`
  (no FALL_LAND in the 2.5s window). Investigation of root cause is the
  primary open work for the next session.
- Stream 4 (other teleport scenarios) remains open.

---

## Done since v8 (this session)

Run `git log --oneline -10` for the actual tip. Recent commits on `main`:

1. **`9d9fc121` — `feat(bg-diag): add BackgroundPostTeleportWindowRecorder`**
   - New `Services/BackgroundBotRunner/Diagnostics/BackgroundPostTeleportWindowRecorder.cs`.
     Mirrors the FG recorder shape: hooks `WoWClient.PacketSent` /
     `PacketReceived`, opens a 2.5s window on inbound MSG_MOVE_TELEPORT(_ACK),
     writes JSON matching the existing `PostTeleportWindowFixture` schema.
   - Gated on `WWOW_ENABLE_RECORDING_ARTIFACTS=1` +
     `WWOW_CAPTURE_BG_POST_TELEPORT_WINDOW=1`. Output dir resolves
     `WWOW_BG_POST_TELEPORT_OUTPUT` then
     `$WWOW_REPO_ROOT/Tests/WoWSharpClient.Tests/Fixtures/post_teleport_packet_window/`.
   - `PayloadHex` is intentionally empty: `WoWClient` events expose only
     `(opcode, size)`. The fixture's purpose is opcode shape + timing parity.
   - Wired in `BackgroundBotWorker` alongside `BackgroundPacketTraceRecorder`
     with matching dispose order (post-teleport disposed first).

2. **`90fbc395` — `test(bg-diag): add live BG vertical-drop capture test`**
   - Adds `Background_VerticalDropTeleport_CapturesPostTeleportWindow`
     in `Tests/BotRunner.Tests/LiveValidation/AckCaptureTests.cs`.
     Skips unless `WWOW_CAPTURE_BG_POST_TELEPORT_WINDOW=1`. Stages the BG
     bot on Durotar ground (Z=38), teleports to Z=48, polls for a new
     `background_*.json` fixture in the window dir.

3. **`3beb08a4` — `test(bg-movement-parity): pin BG live post-teleport baseline (Stream 2D)`**
   - Commits `background_durotar_vertical_drop_baseline.json` (captured
     live this session at 21:29:05.399 UTC during ECONBG1 vertical-drop).
   - Adds `BackgroundBaseline_ReportsLiveCapturedTeleportPacketSequence`
     parity test that pins:
     - Trigger: inbound MSG_MOVE_TELEPORT_ACK at deltaMs=0.
     - Outbound: `CMSG_SET_ACTIVE_MOVER` (≤100ms, 8b) →
       `MSG_MOVE_HEARTBEAT` (28b) → `MSG_MOVE_TELEPORT_ACK` (16b).
     - SET_ACTIVE_MOVER fires before outbound TELEPORT_ACK.
   - Updates the dir `.gitignore` to also ignore raw
     `background_20*.json` captures.
   - All 4 parity tests pass; full WoWSharpClient.Tests suite is
     1612 passed / 1 skipped.

4. **(this commit)** — v9 handoff doc.

**Stream 3 verification (this session):** ran
`Foreground_VerticalDropTeleport_CapturesTeleportAckAndSnapWindow` with
`WWOW_CAPTURE_POST_TELEPORT_WINDOW=1` against live Docker MaNGOS,
diffed three fresh FG captures against the committed baseline. The
ground-stage capture (`foreground_20260427_213842_877.json`) matches
the baseline opcode shape exactly:

| Step | Baseline | Fresh | Drift |
|------|----------|-------|-------|
| Send TELEPORT_ACK | 6ms | 13ms | +7ms |
| Send HEARTBEAT (1) | 491ms | 493ms | +2ms |
| Send HEARTBEAT (2) | 991ms | 992ms | +1ms |
| Send FALL_LAND | 1271ms | 1276ms | +5ms |

All within ±10ms of incidental server timing drift. Outbound order
identical. **The FG baseline is stable; Stream 2C did not drift it.**

---

## Stream 2E (NEW, primary open work) — Investigate FG/BG live divergence

The synthetic parity test passes (Stream 2C closed), but the live BG
capture committed in Stream 2D shows a different shape than live FG:

**FG live (committed `foreground_durotar_vertical_drop_baseline.json`):**

```
Send  6ms   20b  MSG_MOVE_TELEPORT_ACK
Send  491ms 48b  MSG_MOVE_HEARTBEAT
Send  991ms 48b  MSG_MOVE_HEARTBEAT
Send  1271ms 32b MSG_MOVE_FALL_LAND
```

**BG live (committed `background_durotar_vertical_drop_baseline.json`):**

```
Send  13ms  8b   CMSG_SET_ACTIVE_MOVER     ← not in FG
Send  60ms  28b  MSG_MOVE_HEARTBEAT
Send  60ms  16b  MSG_MOVE_TELEPORT_ACK     ← FG fires this at 6ms
                                           ← no further outbound packets
                                           ← no MSG_MOVE_FALL_LAND in 2.5s window
```

Two anomalies to root-cause:

1. **`CMSG_SET_ACTIVE_MOVER` on every BG teleport.** WoW.exe doesn't
   re-affirm active mover on same-map teleports. Find where BG sends it
   (grep `CMSG_SET_ACTIVE_MOVER` /
   `SendSetActiveMoverAsync` in `Exports/WoWSharpClient/`) and decide
   whether it's necessary (e.g., for the headless ObjectManager state)
   or removable. If removable, removing it matches FG.

2. **No FALL_LAND in BG live window despite 10y drop.** The BG bot
   uses `NativeLocalPhysics`. Either:
   - Server-pushed `SMSG_MONSTER_MOVE` updates are landing the bot on
     ground before the local fall sequence completes → no FALLINGFAR
     transition observed → no FALL_LAND emitted.
   - Local physics ground-snap is too aggressive — when ground appears
     within snap range, the bot teleports directly to ground without
     emitting FALLINGFAR + FALL_LAND.
   - The teleport ACK gates wait too long, so by the time outbound ACK
     fires (at 60ms), the bot is already grounded by an earlier
     SMSG_MONSTER_MOVE.

   Compare:
   - `MovementController.cs` — search for `_needsGroundSnap`,
     `MOVEFLAG_FALLINGFAR`, fall-land emission paths.
   - `WoWSharpObjectManager.Movement.cs` —
     `TryFlushPendingTeleportAck` and the ACK readiness gates.
   - `Exports/WoWSharpClient/Handlers/MSG_MOVE_TELEPORT_ACK.cs` (or
     wherever inbound TELEPORT_ACK lands) — check if it triggers a
     direct ground-snap that bypasses the fall sequence.

**Concrete first steps:**

1. `grep -rn "CMSG_SET_ACTIVE_MOVER\|SendSetActiveMoverAsync" Exports/WoWSharpClient/`
   to find the BG sender. Add a `[ACTION-PLAN]` log line at the call
   site so future captures show which code path emits it. Decide
   keep/remove based on whether `WoWSharpObjectManager`'s logic
   actually needs the re-affirmation.

2. Capture another BG live fixture with a much higher Z (e.g., Z=100,
   guaranteed airborne even with terrain variance) to confirm whether
   FALLINGFAR / FALL_LAND ever emits in any live BG teleport. If they
   never do, this is a real BG physics emission gap; if they do at
   high Z, the 10y test is just below the local-snap threshold.

3. If FALL_LAND is genuinely missing, the synthetic parity test is
   misleading us — its scripted physics override forces FALLINGFAR
   for 38 frames, which never happens live. Either (a) make the
   synthetic test ALSO exercise the live ground-snap path, or (b)
   accept that synthetic and live test different things.

---

## Stream 4 — Other teleport scenarios (open)

**Why second:** the parity infrastructure only covers vertical-drop
same-map teleport. Cross-map (`.tele name`), transport mounting
(zeppelin), and knockback teleports have not been characterized.

**Important: extend the recorder triggers first.** Both recorders
currently trigger on `Opcode.MSG_MOVE_TELEPORT` /
`Opcode.MSG_MOVE_TELEPORT_ACK` only. Cross-map teleport uses
`SMSG_NEW_WORLD → MSG_MOVE_WORLDPORT_ACK`; knockbacks use
`SMSG_FORCE_RUN_SPEED_CHANGE` etc. Update `IsInboundTeleportTrigger`
in both `ForegroundPostTeleportWindowRecorder` and
`BackgroundPostTeleportWindowRecorder` to also fire on:

- `Opcode.SMSG_NEW_WORLD` (cross-map teleport)
- `Opcode.SMSG_TRANSFER_PENDING` (cross-map handshake)
- (Optionally) knockback opcodes — confer with `docs/server-protocol/`
  for the canonical list.

**Then for each scenario:**

1. **Capture an FG baseline** with `WWOW_CAPTURE_POST_TELEPORT_WINDOW=1`
   in the relevant scenario:
   - **Cross-map**: existing `Foreground_CrossMapTeleport_CapturesWorldportAckWhenCorpusEnabled`
     (`Tests/BotRunner.Tests/LiveValidation/AckCaptureTests.cs:43`)
     stages Org→Ironforge. Run it with both
     `WWOW_CAPTURE_ACK_CORPUS=1` and `WWOW_CAPTURE_POST_TELEPORT_WINDOW=1`
     so the recorder picks up the cross-map sequence.
   - **Transport mount**: write a new staging test that boards a
     zeppelin (or use an existing one if we have it) and capture the
     transport-mount packet sequence.
   - **Knockback**: trigger via `.knockback` GM command.
   Save fresh captures to `foreground_<scenario>_baseline.json`.

2. **Add a parity test** that loads the new fixture and asserts the
   captured opcode shape is what we expect.

3. **Run the BG-side equivalent** — extend
   `Background_VerticalDropTeleport_CapturesPostTeleportWindow` for
   each scenario, capture
   `background_<scenario>_baseline.json`, and add a `BackgroundBaseline_*`
   parity test that pins the BG-today live shape.

4. **If a divergence shows up**, that's a new parity bug — root-cause
   it in MovementController / WoWSharpObjectManager / handlers, just
   like Stream 2A/B/C did for vertical-drop.

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
   you're on `main`, working tree clean. Tip should be the v9 doc commit
   landing on top of `3beb08a4 test(bg-movement-parity): pin BG live
   post-teleport baseline (Stream 2D)`.
2. `docker ps` — confirm `mangosd`, `realmd`, `pathfinding-service`,
   `maria-db` healthy.
3. Read in order:
   - This handoff (top to bottom).
   - [`Tests/WoWSharpClient.Tests/Fixtures/post_teleport_packet_window/foreground_durotar_vertical_drop_baseline.json`](../Tests/WoWSharpClient.Tests/Fixtures/post_teleport_packet_window/foreground_durotar_vertical_drop_baseline.json)
   - [`Tests/WoWSharpClient.Tests/Fixtures/post_teleport_packet_window/background_durotar_vertical_drop_baseline.json`](../Tests/WoWSharpClient.Tests/Fixtures/post_teleport_packet_window/background_durotar_vertical_drop_baseline.json)
   - [`Tests/WoWSharpClient.Tests/Parity/PostTeleportPacketWindowParityTests.cs`](../Tests/WoWSharpClient.Tests/Parity/PostTeleportPacketWindowParityTests.cs)
     — note `BackgroundBaseline_ReportsLiveCapturedTeleportPacketSequence` is the new oracle.
   - [`Services/BackgroundBotRunner/Diagnostics/BackgroundPostTeleportWindowRecorder.cs`](../Services/BackgroundBotRunner/Diagnostics/BackgroundPostTeleportWindowRecorder.cs)
   - [`Services/BackgroundBotRunner/BackgroundBotWorker.cs`](../Services/BackgroundBotRunner/BackgroundBotWorker.cs)
     (recorder wire-up).
   - [`Tests/BotRunner.Tests/LiveValidation/AckCaptureTests.cs`](../Tests/BotRunner.Tests/LiveValidation/AckCaptureTests.cs)
     — `Background_VerticalDropTeleport_CapturesPostTeleportWindow` is
     the live capture entry point.
   - [`docs/physics/bg_movement_parity_audit.md`](physics/bg_movement_parity_audit.md)
     — current per-opcode status (Stream 2B + 2C closed; needs an
     update to mention 2D + 2E).
   - [`docs/physics/state_teleport.md`](physics/state_teleport.md),
     [`memory/movement_physics.md`](C:/Users/lrhod/.claude/projects/e--repos-Westworld-of-Warcraft/memory/movement_physics.md).
   - [`CLAUDE.md`](../CLAUDE.md) and per-repo `CLAUDE.md` for repo-wide rules.
   - [`memory/MEMORY.md`](C:/Users/lrhod/.claude/projects/e--repos-Westworld-of-Warcraft/memory/MEMORY.md).
4. **Open work, in priority order:**
   - **Stream 2E** — investigate the FG/BG live divergence (concrete
     first steps above). This is the deepest open question — the
     synthetic test passes but live BG diverges.
   - **Stream 4** — apply recorder + diff workflow to other teleport
     scenarios (cross-map, transport, knockback). First step is
     extending `IsInboundTeleportTrigger` in both recorders to fire on
     `SMSG_NEW_WORLD` etc.
5. Commit and push after each unit. Don't accumulate.

---

## When you near context exhaustion

Write the next followup handoff at
`docs/handoff_session_bg_movement_parity_followup_v10.md`,
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
