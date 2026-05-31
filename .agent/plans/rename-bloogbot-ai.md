# Execution Plan: Rename BloogBot.AI -> WWoW.AI

## Goal
Resolve the P10 legacy-prefix mismatch: the AI decision/memory layer is named
`BloogBot.AI` (legacy product name) while the repo brand is WWoW. Rename the
project + its test project to `WWoW.AI` / `WWoW.AI.Tests` so the naming matches
the rest of the solution. Closes the `BloogBot.AI` half of audit P6.

## Current behavior
- `BloogBot.AI/BloogBot.AI.csproj` (root-level project, references only
  `GameData.Core`). Implicit `RootNamespace`/`AssemblyName` = `BloogBot.AI`.
- `Tests/BloogBot.AI.Tests/BloogBot.AI.Tests.csproj` is the **only** consumer
  (ProjectReference). Neither project is in `WestworldOfWarcraft.sln`.
- 87 `namespace BloogBot.AI` / `using BloogBot.AI` lines across ~49 `.cs` files.
- No `InternalsVisibleTo`, no string-literal/fully-qualified `BloogBot.AI`
  references in source (only the test ProjectReference + generated `obj/`
  AssemblyInfo, which is gitignored and regenerates).

## Proposed behavior
- `WWoW.AI/WWoW.AI.csproj` and `Tests/WWoW.AI.Tests/WWoW.AI.Tests.csproj`.
- All namespaces/usings under `WWoW.AI`. Implicit `RootNamespace`/`AssemblyName`
  becomes `WWoW.AI` (derived from the new filename — same implicit mechanism as
  before, no explicit properties added). Output DLL becomes `WWoW.AI.dll`.

## Files likely to change
- **Folder + project renames (git mv):** `BloogBot.AI/` -> `WWoW.AI/`,
  `BloogBot.AI.csproj` -> `WWoW.AI.csproj`; `Tests/BloogBot.AI.Tests/` ->
  `Tests/WWoW.AI.Tests/`, `BloogBot.AI.Tests.csproj` -> `WWoW.AI.Tests.csproj`.
- **Test ProjectReference path:** `..\..\BloogBot.AI\BloogBot.AI.csproj` ->
  `..\..\WWoW.AI\WWoW.AI.csproj`.
- **Namespace sweep:** `BloogBot.AI` -> `WWoW.AI` across all tracked `.cs` in both
  trees (87 lines). `obj/bin` excluded (gitignored; regenerates).
- **Docs:** root `CLAUDE.md` naming-table P10 bullet; `.github/instructions/ai.instructions.md`
  (`applyTo:` glob + body); `BloogBot.AI/CLAUDE.md` (-> `WWoW.AI/CLAUDE.md`);
  `BloogBot.AI/TASKS.md` / `TASKS_ARCHIVE.md` identity headers; audit P6 + table.
- **No `.sln` change** (projects not in the solution). **No external consumer**
  (no Service/UI/Export references it).

## Tests to add/update
None added. The existing `WWoW.AI.Tests` (renamed) suite must build + pass in CI.

## Compatibility concerns
- The assembly name changes `BloogBot.AI.dll` -> `WWoW.AI.dll`. Nothing references
  it by DLL name (only the test project, via ProjectReference identity), so this
  is safe. Semantic-Kernel plugin discovery uses the loaded assembly via
  reflection (no hardcoded assembly-name string found), so it is unaffected.
- This is the **highest-uncertainty item** because it cannot be built/tested
  locally (no toolchain). Mitigation: keep it an isolated commit; CI build +
  `WWoW.AI.Tests` is the gate; revert cleanly if red.

## Migration concerns
One-time: contributors' local `obj/bin` for the old name are stale; a clean
build regenerates `WWoW.AI.*`. No data/schema/wire state involved.

## Validation commands
```bash
pwsh scripts/check-project-layering.ps1                 # exit 0
grep -rn "BloogBot.AI" --include=*.cs --include=*.csproj WWoW.AI Tests/WWoW.AI.Tests  # expect none (obj excluded)
# CI:
dotnet build Tests/WWoW.AI.Tests/WWoW.AI.Tests.csproj
dotnet test  Tests/WWoW.AI.Tests/WWoW.AI.Tests.csproj
```

## Rollback plan
Single isolated commit -> `git revert <sha>`. Renames revert cleanly; no
migration to reverse. Watch CI build + `WWoW.AI.Tests` after revert.

## Open questions
- None blocking. Target name `WWoW.AI` chosen over `WWoWBot.AI` to match the
  no-`Bot`-suffix core-library convention and the audit's `WWoW.AI.Tests` form.
