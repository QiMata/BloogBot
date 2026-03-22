# PhysicsEngine Replay Calibration Log

This file is the handoff ledger for `PhysicsEngine` replay calibration.
Only this test is in scope:

- `PathfindingService.Tests.PhysicsEngineTests.StepPhysics_RecordingReplay_FallFromHeight_FrameByFrameVariance`

## Mandatory Preflight (Do This Before Any New Edit)

1. Read this file fully, top to bottom.
2. Check the latest run logs in `logs/` and confirm the best known metrics and last regression.
3. Verify your planned code edit is not already listed under "Do Not Repeat".
4. Make exactly one PhysicsEngine behavior change per run.
5. Append the run result (metrics + frame-pattern notes + log file name) immediately after the run.

If steps 1-3 are skipped, calibration work will likely repeat failed attempts.

## Current Scope Guardrails

- Focus only on `PhysicsEngine` calibration.
- Do not tune `MovementController` in this stream.
- Keep horizontal replay lock behavior intact unless explicitly changing that hypothesis.
- Always keep frame-by-frame trace logging enabled in test output.

## Current Handoff Snapshot (2026-02-25)

- Best observed run in this stream:
  - Log: `logs/physicsengine-variance-20260225-run1.txt`
  - `simulated=248 airborne=144 skippedDt=0 skippedTeleport=0`
  - `avg=0.0054 p95=0.0359 p99=0.0452 max=0.0490 (frame=247)`
- Current dominant error bands in best run:
  - Positive Z band: `f=207..213`, up to `dZ=+0.0407`
  - Negative Z tail: `f=230..247`, down to `dZ=-0.0490`
  - Horizontal error: effectively `0` across worst frames.
- Last attempted adjustment regressed mean error:
  - Log: `logs/physicsengine-variance-20260225-run2.txt`
  - `avg` worsened from `0.0054` to `0.0062`
  - `p95/p99/max` unchanged (`0.0359/0.0452/0.0490`)

## Adjustment History (Chronological)

### A. Baseline before replay-trust calibration in this pass

- Result:
  - `avg=0.0407 p95=0.1411 p99=0.1492 max=0.1707 (frame=45)`
- Main issue:
  - systematic grounded replay mismatch.

### B. Replay-trust overshoot regression

- Result:
  - `avg=0.0596 p95=0.2863 p99=0.2952 max=0.2995 (frame=226)`
- Main issue:
  - non-walkable slope over-lift (`dZ=+0.28..+0.30`) late in run.

### C. Clamp grounded trust-refine `chosenZ` around replay input

- Change:
  - grounded trust-refine clamp around replay `input.z`.
- Result:
  - `avg=0.0188 p95=0.0852 p99=0.1129 max=0.1277 (frame=45)`
- Impact:
  - removed the large late overshoot latch.

### D. Additional replay-trust tuning (current code lineage)

- Result (best):
  - `logs/physicsengine-variance-20260225-run1.txt`
  - `avg=0.0054 p95=0.0359 p99=0.0452 max=0.0490`
- Remaining issue:
  - Z lag on two bands (`f=207..213`, `f=230..247`).

### E. Failed tweak (Do Not Repeat As-Is)

- Change:
  - In `Exports/Navigation/PhysicsEngine.cpp` non-walkable support guardrail block,
    replaced prior trend handling with geometry-sampled support trend:
    `currentSupportZ/nextSupportZ` via `SceneQuery::GetGroundZ`.
- Result:
  - `logs/physicsengine-variance-20260225-run2.txt`
  - `avg` regressed from `0.0054` to `0.0062`, tails unchanged.
- Why it failed:
  - introduced broader small positive bias on grounded moving frames without reducing tail max error.

## Do Not Repeat

- Do not reapply the geometry-derived support-trend replacement from run2 as-is.
- Do not run multiple simultaneous hypotheses in one test pass.
- Do not recalibrate by re-scanning unrelated systems (MovementController/task-wide state) for this stream.

## Recommended Next Single Hypothesis

Target only non-walkable replay guardrail behavior in `PhysicsEngine.cpp`:

- Keep existing replay-horizontal lock behavior.
- Revert the run2 trend-resolution replacement.
- Apply one narrow slope-conditioned clamp tweak for late under-lift band (`f=230..247`) only.
- Re-run the single test and compare against run1/run2 metrics and frame bands.

## Commands

Run the single calibration test:

```powershell
dotnet test Tests/PathfindingService.Tests/PathfindingService.Tests.csproj --filter "FullyQualifiedName=PathfindingService.Tests.PhysicsEngineTests.StepPhysics_RecordingReplay_FallFromHeight_FrameByFrameVariance" --logger "console;verbosity=detailed"
```

Suggested log capture:

```powershell
dotnet test Tests/PathfindingService.Tests/PathfindingService.Tests.csproj --filter "FullyQualifiedName=PathfindingService.Tests.PhysicsEngineTests.StepPhysics_RecordingReplay_FallFromHeight_FrameByFrameVariance" --logger "console;verbosity=detailed" | Tee-Object logs/physicsengine-variance-YYYYMMDD-runN.txt
```
