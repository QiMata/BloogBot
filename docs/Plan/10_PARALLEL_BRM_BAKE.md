# Plan 10 — Parallel: Blackrock Bake-Fidelity

## Goal

Close the remaining MmapGen bake-fidelity gaps for the Blackrock
Mountain area (UBRS / LBRS / BRD), which fail BG/FG parity at multi-Z
polygon stack traps. This work runs in parallel to the main phase path
and never blocks Phase 1+.

## Background

The 2026-05-10 BRM iteration FINAL memo (`project_pfs_overhaul_006_brm_iteration_final.md`):
- 14 commits landed before the iteration paused.
- With correct polyIdx range [620,680] cull on tile 0004634.mmtile,
  UBRS/LBRS escape the original trap and stall at a NEW coord
  `(-7825.4,-1129.2,133.8)` — proving cull architecture works
  end-to-end but the trap re-forms via non-Detour paths
  (direct-fallback or stuck-recovery jump).
- Cull-only approach has hit its architecture limit. Remaining BRM
  stalls reach new traps via non-Detour paths.
- User redirected the loop to validation-harness work; BRM iteration
  paused.

This plan resumes the work as a parallel track once the main path is
stable enough not to need every available human cycle.

## Exit criteria

- [ ] BRM checkpoints (`BRM_blackrock_stairs`, UBRS entry, LBRS entry,
      BRD entry) all green in the FG/BG parity validator.
- [ ] No `BotTaskFailedException { reason = transport_missed |
      physics_stuck | physics_parity_break }` against BRM-tile polygons
      in a 24-hour automated-progression test.
- [ ] BRD/LBRS WoW.exe crash cluster has either a code patch or a
      documented guard in [`Plan/Crashes/`](Crashes/) (created on first
      crash slot).

## Slots

### S9.1 — Triage the post-cull stall coord

- **Owner:** `monorepo-worker` or `codex:codex-rescue`
- **Status:** open
- **Owned paths:** `tools/PathPhysicsProbe/**`,
  `tools/NavMeshTileEditor/**`
- **Goal:** Drive `PathPhysicsProbe` from
  `(-7825.4,-1129.2,133.8)` toward UBRS entry and classify every
  segment. Identify which trap the bot lands in via non-Detour path.

### S9.2 — Stuck-recovery jump policy

- **Owner:** `monorepo-worker`
- **Status:** open
- **Depends on:** S9.1
- **Owned paths:**
  - `Exports/BotRunner/Tasks/Recovery/StuckRecoveryTask.cs`
  - `Exports/WoWSharpClient/Movement/MovementController.cs`
- **Goal:** Determine if stuck-recovery jump is the culprit. If so,
  gate jump-recovery on `IsOnNavmesh()` check so jump cannot deposit
  the bot onto an off-mesh polygon.

### S9.3 — `direct-fallback` path policy

- **Owner:** `monorepo-worker`
- **Status:** open
- **Depends on:** S9.1
- **Goal:** If direct-fallback (straight-line walk when Detour
  returns NoPath) is reaching the trap, gate direct-fallback on
  per-tile-walkability validation.

### S9.4 — Multi-tile coordinated bake

- **Owner:** `monorepo-worker`
- **Status:** open
- **Owned paths:** `tools/MmapGen/**`
- **Goal:** Attempt a coordinated bake across tiles
  `(46,33), (46,34), (46,35), (47,34)` (BRM area) with the
  `agentMaxClimbTerrain` per-tile overrides used in OG iter-17e.

### S9.5 — Crash cluster triage

- **Owner:** `codex:codex-rescue` (for WER analysis)
- **Status:** open
- **Owned paths:** `docs/Plan/Crashes/brd-lbrs-cluster.md` (new)
- **Goal:** Capture WER dumps from the BRD/LBRS crashes; root-cause
  in WinDbg; ship either a code patch (anti-Warden / FastCall
  hardening) or a documented guard.

### S9.6 — BRM parity round

- **Owner:** `monorepo-test-runner`
- **Status:** open
- **Depends on:** S9.2, S9.3, S9.4
- **Goal:** Run the validation harness against all BRM checkpoints
  until all green.
