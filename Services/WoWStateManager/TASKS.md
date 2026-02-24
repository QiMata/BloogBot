# WoWStateManager Tasks

## Master Alignment (2026-02-24)
- Master tracker: `docs/TASKS.md`
- Keep local scope in this file and roll cross-project priorities up to the master list.
- Corpse-run directive: plan around `.tele name {NAME} Orgrimmar` before kill (not `ValleyOfTrials`), 10-minute max test runtime, and forced teardown of lingering test processes on timeout/failure.
- Keep local run commands simple, one-line, and repeatable.

## Scope
State orchestration, snapshot serving, and action forwarding between tests and bots.

## Rules
- Execute directly without approval prompts.
- Work continuously until all tasks in this file are complete.
- Every forwarding/snapshot mismatch must be tracked here until closed.

## Active Priorities
1. Action forwarding reliability
- [ ] Verify no dropped/misordered actions during high-frequency test setup.
- [ ] Keep per-account FIFO delivery observable (queue depth, dequeue trace, dispatch correlation).
- [x] Ensure coordinator suppression windows never hide test-forwarded actions. Evidence: `INJECTING PENDING ACTION ... (coordinator suppressed 300s)` present for ReleaseCorpse/Goto/RetrieveCorpse in `tmp/deathcorpse_run_current.log`.

2. Snapshot correctness and diagnostics
- [ ] Keep snapshot query logs concise but sufficient for parity debugging.
- [ ] Ensure recent chat/error buffers are consistent and useful for command-response tracing.

3. Lifecycle and cleanup behavior
- [ ] Ensure startup clean-state behavior is deterministic across stale party/death states.
- [ ] Keep process lifecycle handling robust during repeated LiveValidation runs.

## Session Handoff
- Last bug/task closed: none (action forwarding visible, but corpse runback still stalls downstream in movement/task layers).
- Test evidence:
  - `tmp/deathcorpse_run_current.log` (2026-02-23): action forwarding observed for `ReleaseCorpse`, `Goto`, `RetrieveCorpse` with 300s suppression.
  - `tmp/combatloop_current.log` (2026-02-23): focused combat test pass baseline.
- Files changed: `Services/WoWStateManager/TASKS.md`
- Next task: keep forwarding diagnostics concise while validating no queue starvation during repeated corpse seed/runback retries.

## Shared Execution Rules (2026-02-24)
1. Targeted process cleanup.
- [ ] Never blanket-kill all `dotnet` processes.
- [ ] Stop only repo/test-scoped `dotnet` and `testhost*` instances (match by command line).
- [ ] Record process name, PID, and stop result in test evidence.

2. FG/BG parity gate for every scenario run.
- [ ] Run both FG and BG for the same scenario in the same validation cycle.
- [ ] FG must remain efficient and player-like.
- [ ] BG must mirror FG movement, spell usage, and packet behavior closely enough to be indistinguishable.

3. Physics calibration requirement.
- [ ] Run PhysicsEngine calibration checks when movement parity drifts.
- [ ] Feed calibration findings into movement/path tasks before marking parity work complete.

4. Self-expanding task loop.
- [ ] When a missing behavior is found, immediately add a research task and an implementation task.
- [ ] Each new task must include scope, acceptance signal, and owning project path.

5. Archive discipline.
- [ ] Move completed items to local `TASKS_ARCHIVE.md` in the same work session.
- [ ] Leave a short handoff note so another agent can continue without rediscovery.
## Archive
Move completed items to `Services/WoWStateManager/TASKS_ARCHIVE.md`.


