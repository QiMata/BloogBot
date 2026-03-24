# Docker Stack

This repo now has a Windows-container stack for the local VMaNGOS server plus the service binaries that can run outside the game client.

## Linux Engine VMaNGOS Stack

The local Docker engine on this machine is currently running Linux containers, so the separate `realmd` / `mangosd` deployment for the server itself now lives in `docker-compose.vmangos-linux.yml`.

### Scope

- `vmangos-realmd`: auth server container using the local `D:\vmangos-server\realmd.conf` as the source config.
- `vmangos-mangosd`: world server container using the local `D:\vmangos-server\mangosd.conf` and `D:\MaNGOS\data` extracted client data.
- Host MySQL: the existing `D:\MaNGOS\mysql5` installation serves the `realmd`, `mangos`, `characters`, and `logs` databases on `127.0.0.1:3306`.

### Commands

Start the existing host MySQL install:

```powershell
Start-Process -FilePath D:\MaNGOS\mysql5\bin\mysqld.exe `
  -ArgumentList '--console --max_allowed_packet=128M' `
  -WorkingDirectory D:\MaNGOS\mysql5 `
  -RedirectStandardOutput .\logs\service-host\vmangos-mysql.stdout.log `
  -RedirectStandardError .\logs\service-host\vmangos-mysql.stderr.log
```

If the host DB is behind the current vmangos server image, sync the missing migrations first:

```powershell
powershell -ExecutionPolicy Bypass -File .\docker\linux\vmangos\Sync-MigrationMarkers.ps1 -FetchOrigin
```

Start the Linux vmangos stack:

```powershell
docker compose -f .\docker-compose.vmangos-linux.yml up -d vmangos-realmd vmangos-mangosd
```

Follow world-server logs:

```powershell
docker compose -f .\docker-compose.vmangos-linux.yml logs -f vmangos-mangosd
```

Stop the Linux vmangos stack:

```powershell
docker compose -f .\docker-compose.vmangos-linux.yml down
```

### Notes

- The compose file rewrites only the container-specific config values at startup:
  - DB host/credentials
  - `DataDir`
  - `LogsDir`
  - `HonorDir`
  - `SOAP.IP`
- The Linux stack now uses the existing host MySQL install at `host.docker.internal:3306` and publishes:
  - MariaDB on `127.0.0.1:3306`
  - `realmd` on `127.0.0.1:3724`
  - `mangosd` on `127.0.0.1:8085`
  - SOAP on `127.0.0.1:7878`
- `realmd` and `mangosd` now default `WWOW_VMANGOS_DB_HOST` to `host.docker.internal`, with an explicit `host-gateway` mapping so the Linux containers can reach the host DB.
- `Sync-MigrationMarkers.ps1` now defaults to the host MySQL install (`D:\MaNGOS\mysql5\bin\mysql.exe`, `127.0.0.1:3306`) and can still target a DB container by passing `-DbMode docker -DbContainerName <name>`.
- Default host paths are:
  - `D:/vmangos-server`
  - `D:/MaNGOS/data`
  - `D:/MaNGOS/mysql5`
- Override them with:
  - `WWOW_VMANGOS_SERVER_CONFIG_DIR`
  - `WWOW_VMANGOS_DATA_DIR`
  - `WWOW_VMANGOS_DB_HOST`
  - `WWOW_VMANGOS_DB_PORT`
  - `WWOW_VMANGOS_DB_ROOT_PASSWORD`

## Host-Side Service Execution

`WoWStateManager` must be able to launch local `WoW.exe` clients, so it should run on the host rather than in Docker.

The current Docker engine on this machine is also running Linux containers, so `PathfindingService` is host-side right now too because it still depends on the native Windows `Navigation.dll`.

- `PathfindingService` depends on the native Windows `Navigation.dll`.
- `WoWStateManager` must launch local game clients and should keep direct host access to `WoW.exe`.

Run the published host-side outputs against the live vmangos stack:

```powershell
$env:WWOW_DATA_DIR='D:\MaNGOS\data'
Start-Process -FilePath .\Bot\Release\net8.0\PathfindingService.exe `
  -WorkingDirectory .\Bot\Release\net8.0 `
  -RedirectStandardOutput .\logs\service-host\pathfindingservice.stdout.log `
  -RedirectStandardError .\logs\service-host\pathfindingservice.stderr.log
```

For an idle `WoWStateManager` that does not auto-launch MaNGOS or any bots:

```powershell
$env:MangosServer__AutoLaunch='false'
$env:WWOW_SETTINGS_OVERRIDE='E:\repos\Westworld of Warcraft\Services\WoWStateManager\Settings\StateManagerSettings.Idle.json'
Start-Process -FilePath .\Bot\Release\net8.0\WoWStateManager.exe `
  -WorkingDirectory .\Bot\Release\net8.0 `
  -RedirectStandardOutput .\logs\service-host\wowstatemanager.stdout.log `
  -RedirectStandardError .\logs\service-host\wowstatemanager.stderr.log
```

Validation signals:

- `Bot\Release\net8.0\pathfinding_status.json` reports `IsReady=true`.
- `PathfindingService` listens on `127.0.0.1:5001`.
- `WoWStateManager` listens on `127.0.0.1:5002` and `127.0.0.1:8088`.
- `wowstatemanager.stdout.log` shows `CharacterSettings count: 0` and `MaNGOS auto-launch disabled.`.

## Scope

- `vmangos-server`: wraps the existing local `C:\Mangos\server`, `C:\Mangos\mysql`, and `C:\Mangos\data` installation inside a Windows container.
- `pathfinding-service`: runs `Services/PathfindingService` in Docker and mounts nav data from `C:\Mangos\data`.
- `wow-state-manager`: host-side only; it launches local `WoW.exe` clients and should not run in Docker.
- `background-bot-runner`: optional standalone container/profile for one BG bot instance.

## Requirements

- Docker Desktop must be in Windows container mode.
- Local VMaNGOS assets must exist at the paths already documented in `docs/TECHNICAL_NOTES.md`.
- If your paths differ, set these environment variables before launch:
  - `WWOW_MANGOS_SERVER_DIR`
  - `WWOW_MANGOS_MYSQL_DIR`
  - `WWOW_MANGOS_DATA_DIR`
  - `WWOW_BG_ACCOUNT_NAME`
  - `WWOW_BG_ACCOUNT_PASSWORD`

## Commands

Start vmangos + PathfindingService containers:

```powershell
docker compose -f .\docker-compose.windows.yml up --build vmangos-server pathfinding-service
```

Start `WoWStateManager` on the host:

```powershell
$env:MangosServer__AutoLaunch='false'
Start-Process -FilePath .\Bot\Release\net8.0\WoWStateManager.exe `
  -WorkingDirectory .\Bot\Release\net8.0 `
  -RedirectStandardOutput .\logs\service-host\wowstatemanager.stdout.log `
  -RedirectStandardError .\logs\service-host\wowstatemanager.stderr.log
```

Start the optional standalone background bot container too:

```powershell
docker compose -f .\docker-compose.windows.yml --profile bgbot up --build
```

Stop the stack:

```powershell
docker compose -f .\docker-compose.windows.yml down
```

## Notes

- `WoWStateManager` should stay host-side so it can launch local `WoW.exe` clients.
- The optional `background-bot-runner` container now targets the host-side `WoWStateManager` listener through `WWOW_STATE_MANAGER_HOST` / `WWOW_STATE_MANAGER_PORT` (defaults: `host.docker.internal:5002`).
- `ReamldRepository` and `DecisionEngineService.Repository.MangosRepository` now honor Docker-safe connection strings through environment variables/config-backed environment export.
- `PathfindingService` expects `WWOW_DATA_DIR` to contain `maps`, `mmaps`, and `vmaps`; the compose file mounts that from the local VMaNGOS data folder.
