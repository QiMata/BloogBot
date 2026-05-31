---
name: fg-bg-physics-parity
description: Drive the ForegroundBotRunner→BackgroundBotRunner physics parity loop using the replay validator + bake pipeline, so headless movement matches the live client. Use when BG physics (ground Z, falls, slides) diverges from FG.
trigger: physics parity, FG BG mismatch, ground Z divergence, movement parity, replay validator, GetGroundZ, headless physics, parity harness
---

# FG↔BG Physics Parity

## Goal

Close a divergence between the foreground (in-client) and background (headless)
physics by capturing the mismatch, encoding it as a parity test, fixing the native
engine and/or mesh, and proving parity via the replay harness.

> Physics lives in C++ under `Exports/Navigation/`; mesh fixes go through
> `tools/MmapGen` (pathfinding freeze). Read `docs/physics/README.md` first.

## Inputs

- The mismatch coords `(x,y,z)` and the FG-observed vs BG-computed result.
- Key files (verified):
  - Harness doc: `docs/physics/10_PARITY_TEST_HARNESS.md`; spec
    `docs/Spec/07_PHYSICS.md`.
  - Reference parity test:
    `Tests/Navigation.Physics.Tests/OgZeppelinCliffFallParityTests.cs`.
  - Replay engine: `Tests/Navigation.Physics.Tests/Helpers/ReplayEngine.cs`;
    recording validation: `Tests/Navigation.Physics.Tests/RecordingValidationTests.cs`.
  - Native engine: `Exports/Navigation/` (e.g. ground-Z / collision code);
    rebuilt as `Navigation.dll`.
- Area rules: `.github/instructions/native.instructions.md`.

## Preconditions

- An FG recording (or live FG capture) of the divergent movement exists — BG must
  be validated against FG (monorepo contract).
- You have read `docs/physics/README.md` and the parity-harness doc.

## Procedure

1. Capture FG ground truth at the coords (GM teleport / live movement → snapshot).
2. Compute the BG equivalent (PathfindingService GetGroundZ / bake-stage anchor).
3. Add a unit test in `Tests/Navigation.Physics.Tests/` (model it on
   `OgZeppelinCliffFallParityTests`) pinning coords + expected Z delta.
4. Fix the cause: native engine code in `Exports/Navigation/` (rebuild
   `Navigation.dll`) and/or the mesh via [[pathfinding-bake-iteration]].
5. Rerun the unit test until parity holds; then validate end-to-end with
   `MovementParityTests`.

## Verification

- `dotnet test Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --filter "FullyQualifiedName~OgZeppelinCliffFallParity"` (and your new test).
- `.\run-tests.ps1 -Layer 2` (navigation physics).
- End-to-end:
  `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --filter "FullyQualifiedName~MovementParity"`.
- Use Codex to scan replay frame diffs rather than reading them inline.

## Outputs

- A new parity unit test; native and/or mesh fix; a calibration-doc entry
  (AGENTS.md §15).

## Failure modes and recovery

- **Fixing one tile, breaking another** — physics changes are global; run the full
  Layer-2 suite, log regressions under "Do Not Repeat".
- **No FG recording** — a BG-only "fix" can't be trusted; capture FG first.
- **Tuning more than one variable per run** — single-scope changes only.

## Related skills

- [[pathfinding-bake-iteration]] — the mesh half of a parity fix.
- [[route-pack-generation]] — re-validate routes after a physics change.
- [[crash-cluster-triage]] — physics edge cases that crash the client.
- Reference: `docs/physics/10_PARITY_TEST_HARNESS.md`, `docs/Spec/07_PHYSICS.md`.
