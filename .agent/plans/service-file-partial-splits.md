# Execution Plan: Behavior-preserving partial-class splits of large service files

## Goal
Reduce the size of the largest hand-written service files (audit P3) by moving
cohesive member groups into additional `partial class` files in the same
project/namespace. Improves agent/human readability with **zero behavior
change**.

## Current behavior
Several 1,000-1,800-line single-file classes (`SqliteStorylineRepository.cs`
1,486; `DungeoneeringCoordinator.cs` 1,544; `ObjectManager.Interaction.cs`
1,427; `MangosRepository.*.cs`). The audit deferred these because their
protecting tests can't run on this machine (no native toolchain / live services).

## Proposed behavior
Each split moves whole members **verbatim** into `(<Name>).<Aspect>.cs`, with the
original type marked `partial`. Same namespace, same usings, no signature/body/
accessibility change -> compile-identical by construction. CI is the real gate.

## Pilot (this commit)
`Services/PromptHandlingService/Storylines/SqliteStorylineRepository.cs` — the
only large candidate with **no native/x86 or live-service dependency** (pure
SQLite-net + ImplicitUsings), so it is the safest to reason about without a
local build.
- Mark `SqliteStorylineRepository` `partial`.
- Extract the 17 nested DTO types (`StorylineSeedDocument` + 16 `*Row` table
  rows, lines 1231-1485, a self-contained block at the file end) into
  `SqliteStorylineRepository.Rows.cs`. They remain **nested in the same class**,
  so `_connection.Table<PersonaProfileRow>()` etc. still resolve. New file needs
  only `using SQLite;` (attributes); everything else is repo-wide ImplicitUsings.
- No method logic moves -> behavior is provably unchanged.

## Deferred to follow-up commits (gated on the pilot passing CI)
`DungeoneeringCoordinator.cs`, `ObjectManager.Interaction.cs`,
`MangosRepository.Utility.cs/World.cs`. Each needs the native/x86 toolchain or
live services to test, so do them one commit at a time **only after** CI proves
the partial-split pattern green on the pilot. If a candidate has no CI-runnable
protecting test, leave it unsplit and note it — do not split blind.
**`Navigation.cs` is excluded — pathfinding freeze.**

## Files likely to change (pilot)
- `Services/PromptHandlingService/Storylines/SqliteStorylineRepository.cs` (class
  -> `partial`, DTO block removed).
- `Services/PromptHandlingService/Storylines/SqliteStorylineRepository.Rows.cs` (new).

## Tests to add/update
None. `Tests/PromptHandlingService.Tests/` must stay green in CI.

## Compatibility concerns
None — internal nested types, same assembly/namespace, no public surface change.

## Migration concerns
None.

## Validation commands
```bash
pwsh scripts/check-project-layering.ps1                       # exit 0
# brace/structure sanity locally; CI is the real gate:
dotnet build Services/PromptHandlingService/PromptHandlingService.csproj
dotnet test  Tests/PromptHandlingService.Tests/PromptHandlingService.Tests.csproj
```

## Rollback plan
Isolated commit per file -> `git revert <sha>`. Partial-class splits revert
trivially.

## Open questions
- Confirm CI runs `PromptHandlingService.Tests` so the pilot is actually gated.
