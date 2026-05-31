# Deployment / Running the Stack

> WWoW "deploys" to a **local developer machine**, not to a cloud. This page is
> a thin index of how the pieces are brought up; the full walkthroughs are in
> [`local-development.md`](local-development.md), [`DOCKER_STACK.md`](DOCKER_STACK.md),
> and [`BUILD.md`](BUILD.md).

## What runs where

| Component | How it's run |
|---|---|
| MaNGOS DB + world + realmd + SOAP | Docker (see compose files below) or a native server install |
| WWoW services (StateManager, Pathfinding, SceneData) | `dotnet`/published exes, or their per-service `Dockerfile`s |
| Foreground bot | Injected into a local `WoW.exe` by StateManager |
| Background bot | Headless `dotnet` process / container |
| Desktop UIs | `UI/WoWStateManagerUI`, `UI/StorylineManager` (WPF, Windows only) |

## Compose / orchestration files

| File | Brings up |
|---|---|
| [`../compose.yaml`](../compose.yaml) | Self-contained empty MariaDB on `:3306` — zero prerequisites, for a quick local DB. |
| [`../docker/database/docker-compose.yml`](../docker/database/docker-compose.yml) | MariaDB + world-dump import (needs the shared `gameserver-net` network). |
| [`../docker-compose.vmangos-linux.yml`](../docker-compose.vmangos-linux.yml) | Linux containers: realmd + mangosd + SOAP (+ pathfinding/scene). |
| [`../docker-compose.windows.yml`](../docker-compose.windows.yml) | Windows all-in-one server container. |

The per-service container builds are
`Services/{WoWStateManager,PathfindingService,SceneDataService,BackgroundBotRunner}/Dockerfile`.

## .NET Aspire

`UI/Systems/Systems.AppHost` orchestrates the Docker stack + services for dev/test
via .NET Aspire (optional `aspire` workload). See
[`local-development.md`](local-development.md) and
[`UI/Systems/Systems.AppHost/Program.cs`](../UI/Systems/Systems.AppHost/Program.cs).

## Build artifacts

A complete build needs the **native C++ DLLs** (`scripts/build -Native`) — the
foreground runner copies `FastCall.dll`/`Loader.dll`/`Navigation.dll`, so a
`dotnet`-only build is incomplete. Output paths (`Bot/<Configuration>/...`) and
CI gating are documented in [`BUILD.md`](BUILD.md) and
[`local-development.md`](local-development.md).
