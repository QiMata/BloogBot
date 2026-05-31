---
name: docker-stack-extension
description: Add a new worker service / container to the WWoW stack with metrics + log rotation, and wire it into the .NET Aspire AppHost. Use when adding a background job or a new runnable service to the orchestrated stack.
trigger: add a new service, background job, worker service, new container, Aspire AppHost, docker compose service, wire a port, IHostedService, add metrics and log rotation
---

# Docker / Stack Extension

## Goal

Add a new runnable unit to the WWoW stack — a .NET worker service (background job)
and/or a container — wired into the Aspire AppHost with a stable port, telemetry,
and log rotation, without violating the layer dependency rules (`Services` may
depend on `Exports`; never the reverse, and never on `UI`).

## Inputs

- The job/service responsibility, its IPC surface (if any), and a port in the
  **9000–9099** WWoW block (avoid collisions with PathfindingService /
  WoWStateManager / SceneDataService).
- Key files / templates:
  - Service template (copy the closest): `Services/PathfindingService/`
    (`Program.cs`, `PathfindingServiceWorker.cs` `IHostedService`,
    `PathfindingService.csproj` `Microsoft.NET.Sdk.Worker`,
    `appsettings.PathfindingService.json`), `Services/WoWStateManager/`,
    `Services/SceneDataService/`.
  - IPC transport: `Exports/BotCommLayer/ProtobufSocketServer.cs` (length-framed
    protobuf/TCP) if the service exposes a socket.
  - Aspire host: `UI/Systems/Systems.AppHost/Program.cs` (the
    `builder.AddContainer(...)` + `WithEndpoint` / `WithVolume` / `WithBindMount`
    / `WithReference` / `WaitFor` pattern) and `WowServerConfig.cs`.
  - Shared defaults (telemetry/health/resilience):
    `UI/Systems/Systems.ServiceDefaults/Extensions.cs`.
  - Compose / infra: `docker-compose*.yml`, `docker/**`,
    `Directory.Build.{props,targets}` (output paths).
- Area rules: `.github/instructions/infrastructure.instructions.md` (docker/compose/
  CI/build-props) and `.github/instructions/services.instructions.md`.
- Reference: `UI/CLAUDE.md`, `docs/local-development.md`, `docs/IPC_COMMUNICATION.md`.

## Preconditions

- The work genuinely needs a separate process/container (not just another Task in
  `BotRunner` or a method in an existing service).
- Layer rule honored: the new service references only `Exports/*` and shared
  contracts, not `UI`.
- The solution builds: `dotnet build WestworldOfWarcraft.sln`.

## Procedure

1. **Scaffold the service** at `Services/<Name>Service/` by copying
   `PathfindingService`: `Program.cs` (`Host.CreateDefaultBuilder` →
   `ConfigureAppConfiguration` with `appsettings.<Name>Service.json` →
   `ConfigureServices`), a `<Name>ServiceWorker : IHostedService`, and a
   `Microsoft.NET.Sdk.Worker` csproj referencing
   `Exports/BotCommLayer` + `Exports/GameData.Core`.
2. **Assign a port** in `appsettings.<Name>Service.json` (9000–9099) and, if it
   serves IPC, host a `ProtobufSocketServer` registered as a singleton.
3. **Add telemetry**: call the Systems.ServiceDefaults wiring so the service emits
   OpenTelemetry metrics/health; add service-specific meters via
   [[metrics-instrumentation]].
4. **Add the project** to `WestworldOfWarcraft.sln` and confirm output lands under
   `Bot/<Configuration>/...` per `Directory.Build.{props,targets}`.
5. **Wire into the AppHost**: register the service in
   `UI/Systems/Systems.AppHost/Program.cs` following the existing `builder.Add*` +
   `WithEndpoint(targetPort: …)` + `WithReference(...)` + `WaitFor(...)` pattern;
   pull the port from config, do not hardcode it downstream.
6. **Container + log rotation** (if containerized): add a service to the compose
   file with a logging driver bound (`max-size` / `max-file`) per
   [[logging-noise-reduction]], and any required volumes/bind-mounts.
7. **Document** the new port/service in `docs/IPC_COMMUNICATION.md` /
   `docs/architecture.md` (AGENTS.md §9 requires it).

## Verification

- Build: `.\scripts\build.ps1` (or `dotnet build WestworldOfWarcraft.sln`).
- Service defaults: `dotnet test Tests/Systems.ServiceDefaults.Tests/Systems.ServiceDefaults.Tests.csproj --configuration Release`.
- Start the service standalone and confirm it binds its port and answers a health
  check; confirm metrics appear on the telemetry endpoint.
- Bring the stack up via the AppHost and confirm the new service starts after its
  dependencies (`WaitFor`).
- Process cleanup uses repo-scoped helpers only
  (`.\run-tests.ps1 -CleanupRepoScopedOnly`) — never blanket `taskkill`.

## Outputs

- `Services/<Name>Service/` (Program/Worker/csproj/appsettings) + solution entry.
- AppHost registration + endpoint wiring; optional compose service with log
  rotation.
- Doc updates (`docs/IPC_COMMUNICATION.md`, `docs/architecture.md`).

## Failure modes and recovery

- **Port collision** with an existing 9000-block service → service fails to bind;
  pick a free port and record it.
- **Hardcoding endpoints downstream** instead of wiring them in the AppHost — UI/
  services must read ports from config.
- **Upward dependency** (service → UI, or `Exports` → service) violates the layer
  rule; keep contracts in lower layers.
- **No log rotation** → unbounded container logs fill disk; always set
  `max-size`/`max-file`.
- **Blanket process kills** during local runs — D2Bot and other repos share this
  machine; kill only your PIDs.

## Related skills

- [[metrics-instrumentation]] — meters/labels for the new service.
- [[logging-noise-reduction]] — logging profile + rotation.
- [[wpf-dashboard-panel]] — surface the new service in the operator console.
- [[config-hot-reload-subscriber]] — make the service's config reloadable.
- Reference: `UI/CLAUDE.md`, `.github/instructions/infrastructure.instructions.md`.
