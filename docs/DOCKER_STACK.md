# Docker Stack

The default stack is Linux-container based and now includes:

- `realmd`
- `mangosd`
- `pathfinding-service`
- `scene-data-service`

`WoWStateManager` remains host-side because it launches local `WoW.exe` processes.

## Prerequisites

`gameserver-net` and `maria-db` must already be available.

```powershell
cd "E:\repos\Final Fantasy XI\Docker\database"
docker compose up -d
```

## Linux Service Stack

Start all game + split navigation services:

```powershell
docker compose -f .\docker-compose.vmangos-linux.yml up -d --build wow-realmd wow-mangosd wwow-pathfinding wwow-scene-data
```

Follow logs:

```powershell
docker compose -f .\docker-compose.vmangos-linux.yml logs -f wow-mangosd
docker compose -f .\docker-compose.vmangos-linux.yml logs -f wwow-pathfinding
docker compose -f .\docker-compose.vmangos-linux.yml logs -f wwow-scene-data
```

Stop the stack:

```powershell
docker compose -f .\docker-compose.vmangos-linux.yml down
```

## Runtime Configuration

- `wwow-pathfinding` loads nav data from `WWOW_DATA_DIR=/wwow-data`.
- `wwow-pathfinding` mmap preload is config-driven. The Linux compose service
  currently sets `Navigation__PreloadMaps=all` and
  `WWOW_NAVIGATION_PRELOAD_MAPS=all` so the Docker runtime loads every
  available `.mmap` once at startup. Use `none` for focused test or fast
  iteration overrides.
- `wwow-scene-data` loads collision/scene data from `WWOW_DATA_DIR=/wwow-data` and listens on `0.0.0.0:9003`.
- Both services mount `${WWOW_VMANGOS_DATA_DIR:-D:/MaNGOS/data}` read-only.
- Published ports:
  - `realmd`: `3724`
  - `mangosd`: `8085`
  - `SOAP`: `7878`
  - `pathfinding-service`: `9002`
  - `scene-data-service`: `9003`

## Current Validation (2026-05-05)

- `docker ps` confirms both split services are live:
  - `wwow-pathfinding` -> `0.0.0.0:9002->9002/tcp`
  - `wwow-scene-data` -> `0.0.0.0:9003->9003/tcp`
- `docker logs --since 5m wwow-pathfinding` shows `[Navigation] preloading 41 configured map(s)` from mounted `/wwow-data`, `Navigation loaded in 117.7s`, and the service ready message.
- `docker exec wwow-pathfinding cat /app/pathfinding_status.json` reports `IsReady=true` with all 41 discovered map IDs in `LoadedMaps`.
- `docker logs --tail 80 wwow-scene-data` shows ready state and initialized map coverage.

## Host-Side WoWStateManager

Run StateManager on host while services stay in Docker:

```powershell
Start-Process -FilePath .\Bot\Release\net8.0\WoWStateManager.exe `
  -WorkingDirectory .\Bot\Release\net8.0 `
  -RedirectStandardOutput .\logs\service-host\wowstatemanager.stdout.log `
  -RedirectStandardError .\logs\service-host\wowstatemanager.stderr.log
```

`MangosServer:AutoLaunch` defaults to `false`. Local Windows MaNGOS process launch is legacy opt-in only:

```powershell
$env:MangosServer__AutoLaunch='true'
$env:MangosServer__MangosDirectory='C:\Mangos\server'
```

Validation signals:

- `wwow-pathfinding` listening on `127.0.0.1:9002`
- `wwow-scene-data` listening on `127.0.0.1:9003`
- `WoWStateManager` listening on `127.0.0.1:9001` and `127.0.0.1:9000`

## Migration Marker Sync

```powershell
powershell -ExecutionPolicy Bypass -File .\docker\linux\vmangos\Sync-MigrationMarkers.ps1 -FetchOrigin
```

## Notes

- `WoWStateManager` only manages WoW client instances; it does not launch/stop `PathfindingService` or `SceneDataService`.
- Split service endpoints are still `wwow-pathfinding:9002` and `wwow-scene-data:9003` on the compose network.
