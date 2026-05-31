# Agent Readability Audit

_Date: 2026-05-31. Scope: lower the risk of automated/coding-agent edits via
**small, high-confidence** changes — no broad rewrite._

This audit covers mechanical editability: how easy and safe it is for a coding
agent to change the right file without breaking a generated artifact, an
architectural boundary, or a frozen subsystem.

## TL;DR

The repo's **agent-documentation infrastructure is already mature** — 23
per-directory `CLAUDE.md` files, 13 `.github/instructions/*.instructions.md`
area-rule files (with `applyTo:` globs), a 20-skill `.claude/skills/` catalog
enforced by `SkillsContractTests`, an `.agent/PLANS.md` execution-plan convention,
and a strong file-based contract-test pattern (markdown/JSON drift tests). The
remaining gaps were narrow: a few missing in-folder markers/READMEs, one
documented-but-unenforced architectural invariant, and no audit of which files are
unsafe to mechanically edit. This pass closed those without touching risky code.

## Current pain points

### P1 — Large generated files committed to source (highest risk)
`Exports/BotCommLayer/Models/` holds ~130k lines of protobuf-generated C# checked
into the repo, in the **same folder** as one hand-written file:

| File | ~Lines | Status |
|------|-------:|--------|
| `Database.cs` | 103,000 | generated (`database.proto`) |
| `Game.cs` | 11,000 | generated (`game.proto`) |
| `Communication.cs` | 7,700 | generated (`communication.proto`) |
| `Pathfinding.cs` | 6,300 | generated (`pathfinding.proto`) |
| `Scenedata.cs` | 2,700 | generated (`scenedata.proto`) |
| `WoWActivitySnapshotExtensions.cs` | 17 | **hand-written** |

Risk: an agent that opens `Models/` directly (not via the `applyTo` glob) can't
tell the generated files from the editable one, and any hand edit is silently lost
on regen. There was no in-folder marker. **Resolved 2026-05-31:** the five
generated files now live in `Models/Generated/`, physically separated from the
hand-written `WoWActivitySnapshotExtensions.cs` in `Models/` (see the deferred-
refactor table below).

### P2 — Documented layering was not mechanically enforced
`CLAUDE.md` documents a strict top-to-bottom dependency flow
(`GameData.Core → BotCommLayer → … → Services → UI`) but nothing tested it. An
agent could add an upward `ProjectReference` (e.g. `Exports/*` → `Services/*`) and
no test would object. Verified current state (44 `.csproj`, 86 references):
`GameData.Core` has **0** project references, `Exports/*` reference only `Exports/*`,
and `Services/*` never reference `UI/` or `Tests/`. So the invariant holds today and
is safe to lock in.

### P3 — Other large hand-written files (medium risk, mostly off-limits)
- `Services/PathfindingService/Repository/Navigation.cs` (~7,600 lines) — inside the
  active pathfinding freeze.
- `Exports/Navigation/PhysicsEngine.cpp` (~6,800 lines), `PhysicsTestExports.cpp`
  (~5,000), `DllMain.cpp` (~3,500) — native, freeze-adjacent.
- Several 1,000–1,800-line service files (`DungeoneeringCoordinator.cs`,
  `ObjectManager.Interaction.cs`, `MangosRepository.*.cs`). These already use the
  partial-class split pattern, so they are split-friendly but not yet split.

### P4 — Missing human-facing READMEs for confusing folders
`tools/` (8 sub-projects with opaque names) had no top-level README; `BotProfiles/`,
`Tests/`, `Config/` had a `CLAUDE.md` but no `README.md`.

### P5 — Generated-looking content in a service tree
`Services/WoWStateManager/Activities/ActivityCatalogRows.Shard{1..5}.cs` were
tool-maintained rows owned by the `activity-catalog-bootstrap` skill, living beside
hand-written coordinator code. **Resolved 2026-05-31:** the shard files were
relocated to `Services/WoWStateManager/Activities/CatalogRows/`, separating them
from the hand-written `ActivityCatalog.cs` / `IActivityCatalog.cs` (which stay in
`Activities/`). The only consumer coupling — one `<Compile>` glob in
`PromptHandlingService.Api.csproj` — was updated to the new path.

### P6 — Stale/known-issue naming
Originally left alone as P10 cosmetic issues. **Resolved 2026-05-31** (each a
self-contained, low-blast-radius rename): the `WoWSharpClient.NetworkTests`
namespace casing was fixed, and `BloogBot.AI` was renamed to `WWoW.AI`
(+ `WWoW.AI.Tests`). `BotRunner` (704 refs) and `BotCommLayer` (42 refs) remain
**deliberately un-renamed** — that rename risk still outweighs the cosmetic
benefit. The bare legacy product name `BloogBot` is a separate, ongoing phase-out.

## Safe improvements completed (this pass)

1. **`Exports/BotCommLayer/Models/README.md`** — in-folder marker listing the five
   generated files as do-not-hand-edit, naming `WoWActivitySnapshotExtensions.cs` as
   the only editable file, and pointing to the `.proto` regen flow. (P1)
2. **`Tests/BotRunner.Tests/Spec/ProjectLayeringTests.cs`** — a file-only contract
   test (parses `.csproj` XML, no assembly loading; same style as
   `SkillsContractTests`) asserting three verified-true invariants:
   - `INV-1` `GameData.Core` has zero in-repo `ProjectReference`s.
   - `INV-2` no `Exports/*` references `Services/`, `UI/`, or `Tests/`.
   - `INV-3` no `Services/*` references `UI/` or `Tests/`.
   It deliberately does **not** forbid "production → `Tests/`" because one real edge
   exists (`tools/RecordingMaintenance` → `Tests/Navigation.Physics.Tests`, reusing
   recording helpers). (P2)
3. **`scripts/check-project-layering.ps1`** — a toolchain-free PowerShell mirror of
   the same three invariants, for machines without the .NET/native test toolchain.
   Validated green here: _86 edges across 44 projects, all invariants hold._ (P2)
4. **Folder READMEs** — `tools/README.md` (index of the 8 tool projects),
   `BotProfiles/README.md`, `Tests/README.md`, `Config/README.md`. Each points to the
   existing `CLAUDE.md` for agent rules rather than duplicating it.
   (`WWoW.AI/README.md` — formerly `BloogBot.AI/README.md` — already existed and was left untouched.) (P4)
5. **`Tests/CLAUDE.md`** — corrected the stale "11 test projects" count to the actual
   15 projects (14 test/harness + `Tests.Infrastructure`).
6. **`AGENTS.md` + `CLAUDE.md`** — added an identical "Generated Code & Layering —
   Quick Rules for Agents" section (kept in sync per their stated contract) and a
   Key References row for this audit.

### Validation performed
- `scripts/check-project-layering.ps1` → exit 0, all three invariants green.
- Confirmed no generated file (`Database/Game/Communication/Pathfinding/Scenedata.cs`)
  was modified and `GameData.Core.csproj` still has 0 `ProjectReference`s.
- The new C# test was **not** run via `dotnet test` — this machine has no C++/native
  toolchain, so `BotRunner.Tests` (x86, needs `Navigation.dll`) cannot build here. The
  test mirrors `SkillsContractTests` byte-for-byte in style to minimize compile risk,
  and its assertion logic is validated by the PowerShell mirror above. It will run in
  CI where the toolchain exists.

## Larger refactors intentionally NOT done

| Refactor | Why deferred |
|----------|--------------|
| ~~Move/split the 5 generated protobuf files into a `Generated/` subfolder~~ — **done 2026-05-31** | The 5 generated `*.cs` moved to `Models/Generated/`; `WoWActivitySnapshotExtensions.cs` stays in `Models/`. `BotCommLayer.csproj` is SDK-default-glob (no edit); `protocsharp.bat` default output + the `.editorconfig` `generated_code` glob retargeted to `Generated/`; `protocpp.bat` writes outside the repo so it was untouched. Plan: `.agent/plans/protobuf-generated-subfolder.md`. |
| Split `Services/PathfindingService/Repository/Navigation.cs` (~6,756 lines) | Inside the active pathfinding freeze (`docs/physics/README.md`). **Ready-to-run plan staged** (post-freeze): `.agent/plans/split-navigation-cs.md`. |
| Touch `Exports/Navigation/PhysicsEngine.cpp` and other native files | Freeze-adjacent **and** unbuildable/unverifiable without the native toolchain. **Ready-to-run plan staged** (post-freeze + toolchain): `.agent/plans/physicsengine-cpp-modularization.md`. |
| ~~Rename `WowSharpClient.NetworkTests` casing / `BloogBot.AI` prefix~~ — **done 2026-05-31** | Casing fixed (namespaces) and `BloogBot.AI` -> `WWoW.AI` (+ `WWoW.AI.Tests`, plan `.agent/plans/rename-bloogbot-ai.md`). Both self-contained. `BotRunner` (704 refs) / `BotCommLayer` (42 refs) **stay un-renamed** — risk still outweighs benefit. |
| ~~Relocate `ActivityCatalogRows.Shard*.cs`~~ — **done 2026-05-31** | Moved to `Services/WoWStateManager/Activities/CatalogRows/`; one `<Compile>` glob in `PromptHandlingService.Api.csproj` + the skill doc updated. See P5 above. |
| Extract helper functions from the 1,000–1,800-line service files | Partial-class **pilot done 2026-05-31** on `SqliteStorylineRepository` (DTOs → `.Rows.cs`); behavior-preserving. Remaining candidates (`DungeoneeringCoordinator`, `ObjectManager.Interaction`, `MangosRepository.*`) gated on the pilot passing CI — their tests need the native/x86 toolchain or live services. Plan: `.agent/plans/service-file-partial-splits.md`. |

## Recommended future refactors (when the freezes lift / a toolchain is available)

1. **Run `ProjectLayeringTests` in CI** and extend it as the architecture grows
   (e.g. assert `UI/*` is referenced by nothing below it; add a `tools/` rule once the
   `RecordingMaintenance` → test-project edge is removed).
2. ~~**Move generated protobuf into `Exports/BotCommLayer/Models/Generated/`**~~ —
   **done 2026-05-31** (plan `.agent/plans/protobuf-generated-subfolder.md`):
   `protocsharp.bat` default output, the `.editorconfig` `generated_code` glob,
   and `protobuf.instructions.md` updated together. `BotCommLayer.csproj` needed
   no change (SDK-default glob).
3. **Split `Navigation.cs`** by responsibility (route-pack cache vs. query vs. repair)
   once the pathfinding overhaul lands — the partial-class pattern (proven on
   `SqliteStorylineRepository`, 2026-05-31) makes this mechanical. Ready-to-run plan
   staged: `.agent/plans/split-navigation-cs.md` (and
   `.agent/plans/physicsengine-cpp-modularization.md` for the native side).
4. ~~**Resolve the P10 naming issues**~~ — **partially done 2026-05-31**: the
   self-contained ones (`WoWSharpClient.NetworkTests` casing, `BloogBot.AI` →
   `WWoW.AI`, `pfprobe`/`wwow-path-probe`) are resolved. `BotRunner` (704 refs) /
   `BotCommLayer` (42 refs) remain intentionally un-renamed, and the bare legacy
   product name `BloogBot` is a separate, ongoing phase-out.
5. **Generalize the file-only contract-test + PowerShell-mirror pattern** into a small
   shared helper so future drift tests (enum↔spec, catalog↔markdown, layering) share
   one repo-root-locator and one runner.

## See also
- `.github/instructions/protobuf.instructions.md` — protobuf/generated-code rules.
- `Tests/BotRunner.Tests/Spec/SkillsContractTests.cs`,
  `FailureReasonCatalogTests.cs` — the file-based contract-test style this pass reused.
- `.agent/PLANS.md` — execution-plan convention for the deferred refactors above.
- `docs/physics/README.md` — the pathfinding freeze.
