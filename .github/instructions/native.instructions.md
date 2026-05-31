---
applyTo: "Exports/Navigation/**/*.cpp,Exports/Navigation/**/*.h,Exports/Navigation/**/*.hpp,Exports/Loader/**/*.cpp,Exports/Loader/**/*.h,Exports/FastCall/**/*.cpp,Exports/FastCall/**/*.h,Exports/Physics/**/*.cpp,Exports/Physics/**/*.h,tools/MmapGen/**/*.cpp,tools/MmapGen/**/*.h,**/*.vcxproj"
---

# Native C++ components

Native code: navigation/physics, the injection loader, and fast native↔C# calls.
Built with MSBuild + MSVC toolset **v145** (VS 2025 Community), not CMake from the
solution.

| Project | Platform | Role |
|---------|----------|------|
| `Exports/Navigation` | x64 (primary) | pathfinding + physics engine |
| `Exports/Loader` | x86 | DLL injection / CLR bootstrap into `WoW.exe` |
| `Exports/FastCall` | x86 | SEH-wrapped native↔C# fast calls |
| `Exports/Physics` | — | physics glue |
| `tools/MmapGen` | tool | navmesh generator (authority for routes) |

## Build (kill `WoW.exe` first — it locks injected DLLs → MSB3027)

```powershell
$MSBUILD = "C:/Program Files/Microsoft Visual Studio/18/Community/MSBuild/Current/Bin/MSBuild.exe"
& $MSBUILD Exports/Navigation/Navigation.vcxproj -p:Configuration=Release -p:Platform=x64 -p:PlatformToolset=v145 -v:minimal
& $MSBUILD Exports/Loader/Loader.vcxproj       -p:Configuration=Release -p:Platform=x86 -p:PlatformToolset=v145 -v:minimal
& $MSBUILD Exports/FastCall/FastCall.vcxproj   -p:Configuration=Release -p:Platform=x86 -p:PlatformToolset=v145 -v:minimal
```

Or `.\scripts\build.ps1 -Native -Configuration Release`. Validate physics with
`Tests/Navigation.Physics.Tests`.

## Critical rules

- ⚠️ **Pathfinding freeze (2026-05-06).** Read the physics doc index
  `docs/physics/README.md` (pathfinding-overhaul section) before touching
  nav/physics. Static world collision belongs in **generated navmesh data** — fix
  `tools/MmapGen/` mesh generation, never hardcode route-specific blocker coords,
  clearance cylinders, or detour waypoints to make a route pass.
- Calibration work: follow the **anti-loop rule** in `AGENTS.md` §15 — one
  behavioral change per run, log every outcome to the calibration doc.
- Large files (`PhysicsEngine.cpp`, etc.) — locate the symbol with search, then
  read surrounding lines; do not read whole files into context.

## See also

- `Exports/Navigation/CLAUDE.md`, `tools/MmapGen/CLAUDE.md` + `tools/MmapGen/AGENTS.md`,
  `docs/physics/README.md`, root `AGENTS.md` §5 (native builds).
