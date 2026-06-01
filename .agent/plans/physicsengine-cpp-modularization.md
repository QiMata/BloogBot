# Execution Plan (DEFERRED — freeze + no toolchain): PhysicsEngine.cpp modularization

> **STATUS: NOT YET ACTIONABLE.** `Exports/Navigation/PhysicsEngine.cpp` (~6,047
> lines) is freeze-adjacent (pathfinding freeze, `docs/physics/README.md`) **and**
> cannot be built/verified on this machine — there is no C++/native toolchain
> here (`BotRunner.Tests` is x86 and needs `Navigation.dll`). **Do not execute**
> until both the freeze lifts and a native toolchain + CI path are available.

## Goal
Continue the already-started extraction of core physics algorithms out of the
~6,047-line `PhysicsEngine.cpp` into focused translation units, for readability.
Behavior must remain bit-for-bit identical (FG/BG physics parity).

## Current behavior
`PhysicsEngine.cpp` documents its own layout: includes/namespace, singleton
management, anonymous-namespace helpers, delegating wrappers to already-extracted
modules (`PhysicsCollideSlide.{h,cpp}`, `PhysicsGroundSnap.{h,cpp}`,
`PhysicsMovement.{h,cpp}`), a ground-movement entry point, and the `StepV2` main
entry. So modularization is partly done; the remaining bulk is helpers + entry
glue.

## Proposed behavior (post-freeze, with toolchain)
Extract further cohesive helper groups from the anonymous namespace into new
`Physics*.{h,cpp}` modules following the existing
CollideSlide/GroundSnap/Movement precedent, leaving `PhysicsEngine.cpp` as the
singleton + `StepV2` orchestration. No algorithm changes.

## Files likely to change
`Exports/Navigation/PhysicsEngine.cpp` + new `Physics*.{h,cpp}` + the
`Navigation.vcxproj` file list. **FROZEN + NATIVE** — highest-caution area.

## Tests to add/update
None added. `Tests/Navigation.Physics.Tests/` + the FG→BG replay-parity
validator are the gate — require the native build + replay recordings.

## Compatibility concerns
Any numerical drift breaks FG/BG physics parity. Each extraction must be
validated against recorded FG replays before merge.

## Migration concerns
None (no persisted state). Build-system: new TUs must be added to the vcxproj.

## Validation commands
```bash
# native build (VS 2025 / MSBuild, v145 toolset) — CI / toolchain box only:
MSBuild Exports/Navigation/Navigation.vcxproj -p:Configuration=Release -p:Platform=x64 -p:PlatformToolset=v145
dotnet test Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release
# + FG->BG replay parity validator
```

## Rollback plan
Isolated commit -> `git revert`. Pure code-move TUs revert trivially; re-run the
parity validator after revert.

## Open questions
- Confirm the freeze is lifted (`docs/physics/README.md`) and a native toolchain
  + replay recordings are available before starting.
- Decide extraction boundaries from the post-overhaul `PhysicsEngine.cpp`.
