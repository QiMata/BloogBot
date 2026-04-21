# Systems.AppHost

Aspire AppHost for local WoW Vanilla infrastructure. It starts a MySQL container and a MaNGOS/vanilla server container, binds local config/game-data folders, and makes the WoW server wait for the database resource.

## Commands

```powershell
dotnet build UI/Systems/Systems.AppHost/Systems.AppHost.csproj --configuration Release
dotnet run --project UI/Systems/Systems.AppHost/Systems.AppHost.csproj --launch-profile local
dotnet run --project UI/Systems/Systems.AppHost/Systems.AppHost.csproj --configuration Release
```

Use the `local` launch profile for routine debugging. It uses the HTTP Aspire dashboard endpoint and does not open a browser automatically.

## Required Host Paths

By default, paths are resolved from `UI/Systems/Systems.AppHost`, even when the app is launched from the solution root.

Required files:

- `config/mangosd.conf.tpl`
- `config/realmd.conf.tpl`

Required directories:

- `data/dbc`
- `data/maps`
- `data/mmaps`
- `data/vmaps`

Startup fails before container creation if any required bind-mount source is missing. The preflight output prints the resolved base, config, and data paths.

## Configuration

Defaults live under `WowServer` in `appsettings.json` and can be overridden with environment variables or user secrets.

```json
{
  "WowServer": {
    "Database": {
      "User": "app",
      "Password": "app",
      "Image": "ragedunicorn/mysql",
      "Tag": "latest"
    },
    "Server": {
      "Image": "ragedunicorn/wow-vanilla",
      "Tag": "latest"
    },
    "Paths": {
      "BaseDirectory": "",
      "ConfigDir": "config",
      "DataDir": "data"
    }
  }
}
```

Common overrides:

```powershell
dotnet user-secrets set "WowServer:Database:Password" "app"
dotnet user-secrets set "WowServer:Paths:BaseDirectory" "E:\repos\Westworld of Warcraft\UI\Systems\Systems.AppHost"
```

Container image tags are optional. A blank or `latest` tag uses the image name as-is; any other tag is appended as `image:tag`.

## Services

- MySQL database: container `wow-vanilla-database`, target port `3306`
- WoW world server: container `wow-vanilla-server`, target port `8085`
- WoW realm server: container `wow-vanilla-server`, target port `3724`

The WoW server resource uses `WaitFor(database)` and logs the dependency contract at startup so failed DB readiness is visible in Aspire output.

## Troubleshooting

- Verify Docker Desktop is running.
- Verify ports `3306`, `3724`, and `8085` are free or adjust `WowServer:Ports`.
- Run the build command first to catch configuration-code errors.
- If startup fails during preflight, use the printed absolute paths to fix missing config or data mounts.
