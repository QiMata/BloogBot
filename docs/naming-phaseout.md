# `BloogBot` → WWoW product-name phase-out (roadmap)

> Status: **open, low-priority, staged.** This tracks the *bare legacy product
> name* `BloogBot` flagged as "a separate, ongoing phase-out" in
> [`CLAUDE.md` → Project Naming Conventions (P10)](../CLAUDE.md#project-naming-conventions).
> The `BloogBot.AI → WWoW.AI` project rename and the `.github`/README guidance-doc
> cleanup are **already done** (2026-05-31). What remains is below.

## Scope reality check

The often-cited "~12,000 occurrences" figure is **gitignored `obj/` build
output**, not source. The real source-controlled footprint is **~193
occurrences across 48 tracked files**. `namespace`/`using`/`*.csproj`/`*.sln`
are already **clean** (0 source hits). So this is a docs + string + comment
phase-out, not a code-symbol rename. Sequence by risk:

## Tier R1 — Runtime data-path contract (DEFER; needs a migration design)

The hard core. `Documents/BloogBot/…` and `BloogBotLogs` are a de-facto on-disk
data contract — renaming any one literal orphans existing user/test data:

- Movement recordings — `Services/ForegroundBotRunner/MovementRecorder.cs:921`,
  `Services/WoWStateManager/Services/MovementRecordingService.cs:241`,
  `Tests/Navigation.Physics.Tests/RecordingLoader.cs:95`,
  `Tests/BotRunner.Tests/LiveValidation/LiveBotFixture.Diagnostics.cs:11`
- Dungeon configs — `Services/ForegroundBotRunner/Grouping/DungeonData.cs:137`
- Logs — `Services/ForegroundBotRunner/Program.cs:33,58`,
  `Services/ForegroundBotRunner/InjectedBotHost.cs:12` (`BloogBotLogs`),
  `Services/ForegroundBotRunner/Statics/WoWEventHandler.cs:382`,
  `Services/ForegroundBotRunner/Statics/ObjectManager.PlayerState.cs:263`
- **SQLite quest DB** — `Documents/BloogBot/database.db`
  (`Exports/BotRunner/Repository/SqliteQuestRepository.cs:45`)

**Design before touching:** centralize the literal in ONE constant (e.g. a
`WWoWDataPaths.RootFolderName`), then choose migrate-on-startup (move old dir →
new) **or** dual-read fallback (read new, fall back to old). Carries its own
test/migration verification.

## Tier R2 — Safe code comments / console banners (optional fast-follow)

No behavior change; pure text:
`MovementRecordingService.cs:218`, `MovementRecorder.cs:895`,
`Grouping/DungeonData.cs:93`, `SqliteQuestRepository.cs:26`,
`Statics/ObjectManager.ScreenDetection.cs:559`, and the banner
`RecordedTests.PathingTests/Program.cs:34`.

## Tier R3 — Stale machine-specific absolute paths (low priority)

Hardcoded local-dev fallbacks — stale regardless of name; prefer an env var:
`Tests/Tests.Infrastructure/BotServiceFixture.cs:156,1215,1219`
(`E:\repos\BloogBot\…`) and
`Services/PathfindingService/Properties/launchSettings.json:9`.

## Tier R4 — Build identity (careful)

`CMakeLists.txt:5,142,145,147` — `project(BloogBot)` + CPACK
package/vendor/install-dir. Renaming changes build/installer artifact names;
coordinate with any packaging consumers first.

## Tier R5 — Docs prose (safe)

~25 `.md` files (e.g. `CLAUDE.md:1` title, `AGENTS.md`,
`docs/PROJECT_STRUCTURE.md`, `docs/DEVELOPMENT_GUIDE.md`,
`docs/CODING_STANDARDS.md`, `docs/architecture.md`, `docs/BUILD.md`).

**Do NOT rewrite archival/historical docs** — they record what happened:
`docs/ARCHIVE.md`, `docs/TASKS_ARCHIVE.md`, `docs/Archive/*`,
`WWoW.AI/TASKS_ARCHIVE.md`, `.agent/plans/rename-bloogbot-ai.md`. Likewise
**keep** legitimate upstream attribution to Drew Kestell's original *BloogBot*
project (e.g. `README.md`) and the real git remote `github.com/QiMata/BloogBot.git`
until the GitHub repo is actually renamed. `.github/workflows/ci.yml:1` is a
one-line comment — trivial, optional.

## Deferred from the 2026-05-31 guidance-doc pass

`README.md` Installation/Usage (`:57-117`) describes a **stale architecture**
(.NET 4.6.1, `Bootstrapper`, `BloogBot/botSettings.json` `:76`, `BloogBot.exe`
`:88`). A name-swap there yields "correct names on wrong instructions" — these
two artifact-name tokens were intentionally left for a separate doc-accuracy
pass that updates the section to the current .NET 8 / Aspire design.
