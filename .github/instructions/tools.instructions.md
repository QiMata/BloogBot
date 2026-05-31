---
applyTo: "tools/**/*.cs,tools/**/*.csproj"
---

# Developer tools (`tools/*`)

.NET 8 console utilities used for diagnostics, data export, and CI — **not**
shipped in the bot runtime.

| Tool | Purpose |
|------|---------|
| `tools/NavDataAudit` | validate generated nav data / stage manifests |
| `tools/MmapVisualize`, `tools/NavMeshPhysicsValidator`, `tools/PathPhysicsProbe` | inspect/validate navmesh + physics |
| `tools/WwowRecastBridge` | Recast integration bridge |
| `tools/GameObjectExporter` | extract game-object data |
| `tools/RecordingMaintenance` | manage packet/state recordings |

> The C++ `tools/MmapGen` navmesh generator is governed by
> `native.instructions.md` (this file's glob is `.cs`/`.csproj` only).

## Conventions

- Reuse contracts from `Exports/*` (e.g. `GameData.Core`) rather than forking
  parallel models inside a tool.
- Nav/physics tools obey the **pathfinding freeze** — they *diagnose*, they do
  not add managed route-repair logic. Mesh fixes belong in `tools/MmapGen/`
  (`docs/physics/README.md`).
- Keep each tool self-contained with a clear `Program.cs` entry point; read
  inputs from args/config, not hardcoded machine paths.

## Validate with

```powershell
.\scripts\build.ps1
dotnet run --project tools/<Name>           # smoke-run the tool
```

## Do NOT

- Add runtime bot behavior here — that belongs in `Exports/` or `Services/`.
- Hardcode machine-specific absolute paths; accept them as args/config.

## See also

- `native.instructions.md` (MmapGen), `docs/physics/README.md`, root `AGENTS.md`.
