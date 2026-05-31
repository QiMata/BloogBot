---
applyTo: "Exports/GameData.Core/**/*.cs,Exports/BotRunner/**/*.cs,Exports/BotCommLayer/**/*.cs,Exports/WoWSharpClient/**/*.cs,Exports/WinImports/**/*.cs"
---

# Shared core libraries (`Exports/*`)

The shared C# library layer every Service and the UI build on. Changes here
ripple across the whole tree — keep them minimal and contract-stable.

## Dependency flow (do not violate)

Strict top-to-bottom; **never add an upward dependency**:

```
GameData.Core  →  BotCommLayer  →  BotRunner  →  WoWSharpClient  →  Services  →  UI
```

- `GameData.Core` has **zero** dependencies — interfaces/contracts only
  (`IObjectManager`, `IWoWUnit`, `IWoWPlayer`, …). Keep implementations out.
- Interfaces live in the lowest layer that needs them; implementations live
  higher. An `Exports/*` project must never reference `Services/*` or `UI/*`.
- If you change a cross-layer contract, update every consumer and its tests in
  the **same** change.

## Conventions

- C# / .NET 8, `Nullable` + `ImplicitUsings` enabled (root `Directory.Build.props`).
- Use the canonical four-layer vocabulary — `Activity → Objective → Task → Action`.
  Do not coin "behavior tree" / "action mapping" synonyms. See
  `docs/Spec/18_TERMINOLOGY.md`.
- `IObjectManager` methods like `MoveToAsync`/`CastSpellAsync` are **Task-level
  wrappers**, not Actions. Atomic primitives (one memory read, one bit write,
  one opcode send) are Actions and never cross a wire.

## Validate with

```powershell
.\scripts\build.ps1                 # dotnet build the solution
.\scripts\test-fast.ps1             # unit tests (Layer 3, no server)
.\scripts\lint.ps1                  # hard format gate (root .editorconfig whitespace baseline)
```

Formatting follows the root `.editorconfig`; `.\scripts\lint.ps1` enforces the
whitespace baseline as a hard pre-PR gate (via `.\scripts\check.ps1`). Run
`.\scripts\format.ps1` to fix differences.

## Do NOT edit

- Generated protobuf `*.cs` under `Exports/BotCommLayer/Models/` — see
  [`protobuf.instructions.md`](protobuf.instructions.md).

## See also

- Root playbook: `AGENTS.md` §3 (Architecture Boundaries), §4 (Repository Map).
- Per-component context: each project's `CLAUDE.md`
  (e.g. `Exports/BotCommLayer/CLAUDE.md`, `Exports/BotRunner/CLAUDE.md`).
