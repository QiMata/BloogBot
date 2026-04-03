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
docker compose -f .\docker-compose.vmangos-linux.yml up -d --build realmd mangosd pathfinding-service scene-data-service
```

Follow logs:

```powershell
docker compose -f .\docker-compose.vmangos-linux.yml logs -f mangosd
docker compose -f .\docker-compose.vmangos-linux.yml logs -f pathfinding-service
docker compose -f .\docker-compose.vmangos-linux.yml logs -f scene-data-service
```

Stop the stack:

```powershell
docker compose -f .\docker-compose.vmangos-linux.yml down
```

## Runtime Configuration

- `pathfinding-service` loads nav data from `WWOW_DATA_DIR=/wwow-data`.
- `scene-data-service` loads collision/scene data from `WWOW_DATA_DIR=/wwow-data` and listens on `0.0.0.0:5003`.
- Both services mount `${WWOW_VMANGOS_DATA_DIR:-D:/MaNGOS/data}` read-only.
- Published ports:
  - `realmd`: `3724`
  - `mangosd`: `8085`
  - `SOAP`: `7878`
  - `pathfinding-service`: `5001`
  - `scene-data-service`: `5003`

## Current Validation (2026-04-03)

- `docker ps` confirms both split services are live:
  - `pathfinding-service` -> `0.0.0.0:5001->5001/tcp`
  - `scene-data-service` -> `0.0.0.0:5003->5003/tcp`
- `docker logs --tail 80 pathfinding-service` shows active map preload from mounted `/wwow-data`.
- `docker logs --tail 80 scene-data-service` shows ready state (`Ready and listening on 0.0.0.0:5003`) and initialized map coverage.

## Host-Side WoWStateManager

Run StateManager on host while services stay in Docker:

```powershell
$env:MangosServer__AutoLaunch='false'
Start-Process -FilePath .\Bot\Release\net8.0\WoWStateManager.exe `
  -WorkingDirectory .\Bot\Release\net8.0 `
  -RedirectStandardOutput .\logs\service-host\wowstatemanager.stdout.log `
  -RedirectStandardError .\logs\service-host\wowstatemanager.stderr.log
```

Validation signals:

- `pathfinding-service` listening on `127.0.0.1:5001`
- `scene-data-service` listening on `127.0.0.1:5003`
- `WoWStateManager` listening on `127.0.0.1:5002` and `127.0.0.1:8088`

## Migration Marker Sync

```powershell
powershell -ExecutionPolicy Bypass -File .\docker\linux\vmangos\Sync-MigrationMarkers.ps1 -FetchOrigin
```

## Notes

- `WoWStateManager` only manages WoW client instances; it does not launch/stop `PathfindingService` or `SceneDataService`.
- Split service endpoints are still `pathfinding-service:5001` and `scene-data-service:5003`.
