# `scripts/` — the stable command interface

A small, predictable set of commands for setup, build, lint, test, and validation.
**Humans and coding agents should use these instead of guessing stack-specific
`dotnet` / `MSBuild` / `run-tests.ps1` invocations.** They are thin wrappers over the
canonical commands, so behavior stays consistent and discoverable.

## Two ways to invoke

Every command exists as a PowerShell script (the real logic) plus an extensionless
POSIX shim that just `exec`s PowerShell, so the same name works from either shell:

```powershell
# PowerShell (primary on this Windows repo)
.\scripts\build.ps1
.\scripts\test-fast.ps1
```

```bash
# bash / git-bash (the shim runs pwsh under the hood; requires PowerShell 7+ on PATH)
./scripts/build
./scripts/test-fast
```

Each script is strict (`Set-StrictMode` + `$ErrorActionPreference = 'Stop'`), exits
non-zero on failure, prints progress, and runs from the repo root no matter where you
invoke it from.

## Commands

| Command | What it does |
| --- | --- |
| `bootstrap` | Verify the .NET 8 SDK, `dotnet restore` the solution, and report whether native C++ (MSBuild) builds are available. Run this first. |
| `build` | `dotnet build WestworldOfWarcraft.sln` (Debug by default). `-Configuration Release` for release; `-Native` also builds the C++ projects (Navigation/Loader/FastCall) via MSBuild. Warns if `WoW.exe` is locking output DLLs. |
| `format` | `dotnet format` — applies formatting. **Mutates source files.** No `.editorconfig` baseline exists yet, so default rules apply (can produce a large diff). |
| `lint` | `dotnet format --verify-no-changes` — read-only formatting check. Exits non-zero if changes would be needed. |
| `test` | Full layered suite via `run-tests.ps1` (all layers). Forwards extra args, e.g. `-SkipBuild`, `-TestTimeoutMinutes 10`. |
| `test-fast` | Unit tests only — `run-tests.ps1 -Layer 3` (no MaNGOS/WoW server needed). The quick inner-loop. |
| `test-integration` | Live integration tests — `run-tests.ps1 -Layer 4` (`Category=Integration`). **Requires the MaNGOS stack** up (SOAP at `http://127.0.0.1:7878/`). |
| `check` | The pre-PR validation sequence: **lint (advisory)** → **build** → **fast tests**. Fails only on build/test failure. Run this before opening a PR. |
| `clean` | Remove build artifacts: `dotnet clean`, `Bot/<config>`, CMake `build/`, test scratch, and every per-project `bin/`/`obj/`. Destructive (build outputs only — never source or `.git`). |

## Notes

- **Layers** come from the repo's existing `run-tests.ps1`: L1 native-DLL checks, L2
  physics + pathfinding, L3 unit tests, L4 integration. The test scripts delegate to it
  rather than re-implementing the layering or its repo-scoped process cleanup.
- **Lint is advisory** in `check` because the repo has no `.editorconfig`/StyleCop
  baseline yet; `lint`/`format` use the SDK's built-in `dotnet format`. Once a baseline
  is adopted, lint can be promoted to a hard gate.
- **A full build (and therefore the tests) requires the native C++ DLLs.**
  `ForegroundBotRunner` copies `FastCall.dll` / `Loader.dll` / `Navigation.dll`, so a
  `dotnet`-only build fails until those exist. Produce them with `build -Native`, which
  needs Visual Studio with the “Desktop development with C++” workload and the pinned
  toolset (`v145`); the script explains and exits non-zero if MSBuild isn't found.
- **CI** (`.github/workflows/ci.yml`) always runs `bootstrap` + advisory `lint` on
  `windows-latest`. Because stock hosted runners lack the `v145` toolchain, the
  `build` + `test-fast` steps are gated behind the repo variable
  `RUN_NATIVE_BUILD=true` (set it on a provisioned/self-hosted Windows runner).
