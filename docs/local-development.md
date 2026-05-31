# Local Development

This page is the fast path for setting up and validating BloogBot (Westworld of
Warcraft) locally. **For everyday build/test/lint, use the stable `scripts/`
interface — do not guess `dotnet`/`MSBuild`/`run-tests.ps1` invocations.** Agents:
prefer these scripts; they are the contract.

See [`scripts/README.md`](../scripts/README.md) for the full reference.

## Prerequisites

- **.NET 8 SDK** — required for everything. <https://dotnet.microsoft.com/download/dotnet/8.0>
- **PowerShell 7+** (`pwsh`) — the default shell here; the bash shims also call it.
- **Visual Studio 2022/2025** with the *Desktop development with C++* workload —
  only needed for the native C++ components (`Exports/{Navigation,Loader,FastCall}`).
  Pure .NET work does not need it.
- **Git LFS** — large physics-recording fixtures are stored via LFS.

## Quick start

```powershell
.\scripts\bootstrap.ps1     # verify the SDK + restore packages
.\scripts\build.ps1         # build the .NET solution (Debug)
.\scripts\test-fast.ps1     # run the fast unit tests (no server needed)
```

From bash/git-bash the same commands work via the extensionless shims:

```bash
./scripts/bootstrap && ./scripts/build && ./scripts/test-fast
```

## Commands

| Command | Use it for |
| --- | --- |
| `scripts/bootstrap` | First-time setup: SDK check + `dotnet restore`. |
| `scripts/build` | Build the solution. `-Configuration Release`, `-Native` (C++). |
| `scripts/format` | Apply formatting (mutates files). |
| `scripts/lint` | Read-only formatting check. |
| `scripts/test-fast` | Unit tests only (Layer 3) — the inner loop. |
| `scripts/test` | Full layered suite (all layers). |
| `scripts/test-integration` | Live integration tests (Layer 4). Needs the MaNGOS stack. |
| `scripts/check` | **Pre-PR gate:** lint (advisory) → build → fast tests. |
| `scripts/clean` | Remove build artifacts (`bin`/`obj`, `Bot/<config>`, etc.). |

## Before opening a PR

```powershell
.\scripts\check.ps1
```

This runs the standard validation sequence (advisory lint, then build and fast tests
as hard gates). Lint is advisory because the repo has no `.editorconfig` baseline yet.

## Integration tests need a live server

`scripts/test-integration` (Layer 4, `Category=Integration`) drives the MaNGOS stack
and asserts against StateManager snapshots. Bring the server up first; do **not** use
the integration scripts without it. See the test-isolation and MaNGOS/SOAP rules in
[`CLAUDE.md`](../CLAUDE.md) and [`AGENTS.md`](../AGENTS.md).

## Native C++ builds are required for a full build

```powershell
.\scripts\build.ps1 -Native        # .NET solution + Navigation/Loader/FastCall via MSBuild
```

This is not optional for a *complete* build: `ForegroundBotRunner` copies the native
`FastCall.dll` / `Loader.dll` / `Navigation.dll` outputs, so a `dotnet`-only build
fails until they exist. Producing them needs Visual Studio with the *Desktop
development with C++* workload and the pinned toolset (`v145`). If MSBuild or the
toolset isn't found, the script explains what to install and exits non-zero. The
underlying MSBuild commands are documented in [`AGENTS.md`](../AGENTS.md) §5 and
[`docs/BUILD.md`](BUILD.md).
