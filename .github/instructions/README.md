# Path-specific agent instructions

Each `*.instructions.md` here carries an `applyTo:` glob in its frontmatter.
Tools that honor that convention (GitHub Copilot, VS Code, and other
`applyTo`-aware agents) auto-apply the file's rules when you edit a matching
file. They hold **cross-cutting area rules + validation commands**; they do not
restate the root `AGENTS.md`/`CLAUDE.md`. Per-directory `CLAUDE.md` files cover
component context separately.

| File | Applies to |
|------|-----------|
| `shared-libraries.instructions.md` | `Exports/{GameData.Core,BotRunner,BotCommLayer,WoWSharpClient,WinImports}/**/*.cs` |
| `services.instructions.md` | `Services/**/*.cs` |
| `native.instructions.md` | C++ in `Exports/{Navigation,Loader,FastCall,Physics}`, `tools/MmapGen`, all `*.vcxproj` |
| `bot-profiles.instructions.md` | `BotProfiles/**/*.cs` |
| `tests.instructions.md` | `Tests/**/*.cs` |
| `ui.instructions.md` | `UI/**/*.{cs,xaml,razor}` |
| `protobuf.instructions.md` | `Exports/BotCommLayer/Models/ProtoDef/*.proto` + the 5 generated `*.cs` |
| `config.instructions.md` | `Config/**/*.json` |
| `docs.instructions.md` | `docs/**/*.md`, `**/TASKS.md`, `**/TASKS_ARCHIVE.md` |
| `infrastructure.instructions.md` | `docker/**`, `compose.yaml`, `docker-compose*.yml`, `.github/workflows/*.yml`, `Directory.Build.{props,targets}`, `.editorconfig` |
| `tools.instructions.md` | `tools/**/*.{cs,csproj}` (C++ `tools/MmapGen` → `native`) |
| `ai.instructions.md` | `BloogBot.AI/**/*.cs` |

Whole-repo rules stay in [`../../AGENTS.md`](../../AGENTS.md),
[`../../CLAUDE.md`](../../CLAUDE.md), and
[`../copilot-instructions.md`](../copilot-instructions.md).
