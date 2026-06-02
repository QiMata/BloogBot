# Local Development

This page is the fast path for setting up and validating BloogBot (Westworld of
Warcraft) locally. **For everyday build/test/lint, use the stable `scripts/`
interface — do not guess `dotnet`/`MSBuild`/`run-tests.ps1` invocations.** Agents:
prefer these scripts; they are the contract.

See [`scripts/README.md`](../scripts/README.md) for the full reference. For *where
code lives and how the flows work*, start at [`architecture.md`](architecture.md);
for tests see [`testing.md`](testing.md).

## Prerequisites

- **.NET 8 SDK** — required for everything. <https://dotnet.microsoft.com/download/dotnet/8.0>
  The SDK is pinned by [`global.json`](../global.json) to the **8.0.x** line
  (`rollForward: latestFeature`), which matches CI's `actions/setup-dotnet@v4`
  (`dotnet-version: 8.0.x`). If only a newer SDK (9.x / 10.x) is installed,
  `dotnet` and `scripts/bootstrap` stop with an explicit "compatible SDK not
  found" message until you install the .NET 8 SDK — local builds can no longer
  silently diverge from CI.
- **PowerShell 7+** (`pwsh`) — the default shell here; the bash shims also call it.
- **Visual Studio 2022/2025** with the *Desktop development with C++* workload —
  only needed for the native C++ components (`Exports/{Navigation,Loader,FastCall}`).
  Pure .NET work does not need it. The native projects pin the **`v145`** toolset.
- **Git LFS** — large physics-recording fixtures are stored via LFS.
- **Docker Desktop** *(optional)* — only for the local service dependencies
  (database / MaNGOS stack) used by integration tests and for running the bot.
  The inner loop (`scripts/build` + `scripts/test-fast`) does not need it.
- **`aspire` workload** *(optional)* — only to run the .NET Aspire orchestration
  in `UI/Systems/Systems.AppHost` (`dotnet workload install aspire`).

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

## Local services & ports

WWoW runs entirely on **local** services — there is no cloud dependency (see
below). The game server ports below are exactly what the Docker stacks publish;
the WWoW microservice ports come from the service configs (`docs/TECHNICAL_NOTES.md`
has the full map).

| Service | Port | Source of truth |
| --- | --- | --- |
| MaNGOS database (MariaDB) | `3306` | `compose.yaml`, `docker/database/docker-compose.yml` |
| MaNGOS realmd (auth) | `3724` | `docker-compose.vmangos-linux.yml` / `docker-compose.windows.yml` |
| MaNGOS world (mangosd) | `8085` | same |
| MaNGOS SOAP API | `7878` | same — used by the GM/test-director (see `CLAUDE.md`) |
| PathfindingService | `9002` | `docker-compose.vmangos-linux.yml` / `docs/TECHNICAL_NOTES.md` |
| SceneDataService | `9003` | same |
| WoWStateManager (+ UI sidecar) | `9001` (+ `9000`) | `docs/TECHNICAL_NOTES.md` |

## Local service dependencies (Docker)

The **inner loop** needs **no services at all** — `scripts/bootstrap` →
`scripts/build` → `scripts/test-fast` runs fully offline.

For a quick local **database only**, the self-contained root [`compose.yaml`](../compose.yaml)
has zero prerequisites (no pre-created network, no host-path mounts):

```powershell
docker compose up -d        # empty MariaDB 10.11 on :3306 (root/root), data in the wwow-db-data volume
docker compose down         # stop it; the named volume persists
```

Override the password with the `WWOW_VMANGOS_DB_ROOT_PASSWORD` env var. This DB
starts **empty** — loading an actual VMaNGOS world dump is the separate step below.

For the **full MaNGOS stack** that `scripts/test-integration` (Layer 4) and the
bot need — realmd + mangosd + SOAP with the world databases loaded — use the
existing platform stacks, which read paths and credentials from [`.env`](../.env)
(`WWOW_VMANGOS_DATA_DIR`, `WWOW_VMANGOS_DB_DUMP_DIR`, `WWOW_VMANGOS_SERVER_CONFIG_DIR`, …):

```powershell
# Windows (all-in-one server container):
docker compose -f docker-compose.windows.yml up -d

# Linux containers (separate DB + server; needs the shared network first):
docker network create gameserver-net
docker compose -f docker/database/docker-compose.yml up -d   # MariaDB + dump import
docker compose -f docker-compose.vmangos-linux.yml up -d     # realmd + mangosd + SOAP (+ pathfinding/scene)
```

See [`docs/DOCKER_STACK.md`](DOCKER_STACK.md) and
[`docs/DEVELOPMENT_GUIDE.md`](DEVELOPMENT_GUIDE.md) for the full walkthrough and
the `.env` values these stacks expect.

## Running without cloud credentials

WWoW uses **no cloud services and no cloud credentials**. The game server, the
SOAP API, and the database all run locally. The only credentials anywhere are:

- the **local database** — `root` / `root` by default (override via
  `WWOW_VMANGOS_DB_ROOT_PASSWORD`), and
- the **local SOAP `ADMINISTRATOR`** account the integration test director uses
  (see the MaNGOS/SOAP section in [`CLAUDE.md`](../CLAUDE.md)).

The inner loop — `scripts/bootstrap`, `scripts/build`, `scripts/test-fast` —
needs none of them.

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
