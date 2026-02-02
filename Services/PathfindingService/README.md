# PathfindingService

A .NET 8 Worker Service that provides navigation pathfinding and physics simulation for the WWoW (Westworld of Warcraft) bot system. This service uses native C++ libraries (Detour/Recast) for efficient navmesh-based pathfinding and WoW-accurate physics calculations.

## Overview

PathfindingService provides:
- **A* Pathfinding**: Navmesh-based path calculation using Detour
- **Physics Simulation**: WoW-accurate character movement physics (gravity, jumping, swimming)
- **Line of Sight**: Raycast-based visibility checks
- **Socket Server**: Protobuf IPC for remote clients (BackgroundBotRunner, ForegroundBotRunner)

## Architecture

```
???????????????????????????????????????????????????????????????????????
?                     PathfindingService                               ?
?                                                                      ?
?  ?????????????????????????????????????????????????????????????????? ?
?  ?              PathfindingServiceWorker (BackgroundService)       ? ?
?  ?                 Main service loop - hosts socket server         ? ?
?  ?????????????????????????????????????????????????????????????????? ?
?                                   ?                                  ?
?  ?????????????????????????????????????????????????????????????????? ?
?  ?              PathfindingSocketServer                            ? ?
?  ?           Protobuf TCP server for IPC requests                  ? ?
?  ?                                                                 ? ?
?  ?   PathfindingRequest ??? HandleRequest() ??? PathfindingResponse? ?
?  ?                              ?                                  ? ?
?  ?        ?????????????????????????????????????????????           ? ?
?  ?        ?                     ?                     ?           ? ?
?  ?        ?                     ?                     ?           ? ?
?  ?  ????????????         ????????????         ????????????       ? ?
?  ?  ?  Path    ?         ?   LoS    ?         ? Physics  ?       ? ?
?  ?  ? Handler  ?         ? Handler  ?         ? Handler  ?       ? ?
?  ?  ????????????         ????????????         ????????????       ? ?
?  ????????????????????????????????????????????????????????????????? ?
?          ?                    ?                    ?                ?
?  ????????????????????????????????????????????????????????????????? ?
?  ?                     Repository Layer                           ? ?
?  ?   ????????????????????       ????????????????????             ? ?
?  ?   ?    Navigation    ?       ?     Physics      ?             ? ?
?  ?   ?  P/Invoke to DLL ?       ?  P/Invoke to DLL ?             ? ?
?  ?   ????????????????????       ????????????????????             ? ?
?  ????????????????????????????????????????????????????????????????? ?
????????????????????????????????????????????????????????????????????????
                ?                          ?
                ?                          ?
        ??????????????????????????????????????????
        ?           Navigation.dll (C++)          ?
        ?  • Detour pathfinding                   ?
        ?  • Recast navmesh loading               ?
        ?  • Physics simulation (collide-slide)   ?
        ?  • Line of sight raycasting             ?
        ??????????????????????????????????????????
                         ?
                         ?
                ????????????????????
                ?   mmaps/*.mmap   ?
                ?   Navigation     ?
                ?   mesh files     ?
                ????????????????????
```

## Project Structure

```
Services/PathfindingService/
??? PathfindingService.csproj       # .NET 8 Worker Service project
??? Program.cs                       # Host builder entry point
??? PathfindingServiceWorker.cs      # BackgroundService implementation
??? PathfindingSocketServer.cs       # Protobuf socket server + request routing
??? Repository/
?   ??? Navigation.cs                # P/Invoke wrapper for pathfinding
?   ??? Physics.cs                   # P/Invoke wrapper for physics + LoS
??? README.md                        # This documentation
```

## Key Components

### PathfindingServiceWorker

The main `BackgroundService` that hosts the socket server:

```csharp
public class PathfindingServiceWorker : BackgroundService
{
    private readonly PathfindingSocketServer _pathfindingSocketServer;
    
    public PathfindingServiceWorker(
        ILogger<PathfindingServiceWorker> logger,
        ILoggerFactory loggerFactory,
        IConfiguration configuration)
    {
        _pathfindingSocketServer = new PathfindingSocketServer(
            configuration["PathfindingService:IpAddress"],
            int.Parse(configuration["PathfindingService:Port"]),
            loggerFactory.CreateLogger<PathfindingSocketServer>()
        );
    }
    
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            await Task.Delay(1000, stoppingToken);
        }
    }
}
```

### PathfindingSocketServer

Handles incoming Protobuf requests and routes to appropriate handlers:

```csharp
public class PathfindingSocketServer : ProtobufSocketServer<PathfindingRequest, PathfindingResponse>
{
    private readonly Navigation _navigation = new();
    private readonly Physics _physics = new();
    
    protected override PathfindingResponse HandleRequest(PathfindingRequest request)
    {
        return request.PayloadCase switch
        {
            PathfindingRequest.PayloadOneofCase.Path => HandlePath(request.Path),
            PathfindingRequest.PayloadOneofCase.Los => HandleLineOfSight(request.Los),
            PathfindingRequest.PayloadOneofCase.Step => HandlePhysics(request.Step),
            _ => ErrorResponse("Unknown request type.")
        };
    }
}
```

### Navigation Repository

P/Invoke wrapper for the native pathfinding library:

```csharp
public class Navigation
{
    [DllImport("Navigation.dll", CallingConvention = CallingConvention.Cdecl)]
    private static extern IntPtr FindPath(uint mapId, XYZ start, XYZ end, bool smoothPath, out int length);
    
    public XYZ[] CalculatePath(uint mapId, XYZ start, XYZ end, bool smoothPath)
    {
        IntPtr pathPtr = FindPath(mapId, start, end, smoothPath, out int length);
        // Marshal path points from native memory...
    }
}
```

### Physics Repository

P/Invoke wrapper for physics simulation and line of sight:

```csharp
public class Physics
{
    [DllImport("Navigation.dll", CallingConvention = CallingConvention.Cdecl)]
    private static extern PhysicsOutput PhysicsStepV2(ref PhysicsInput input);
    
    [DllImport("Navigation.dll", CallingConvention = CallingConvention.Cdecl)]
    private static extern bool LineOfSight(uint mapId, XYZ from, XYZ to);
    
    public PhysicsOutput StepPhysicsV2(PhysicsInput input, float deltaTime)
    {
        input.deltaTime = deltaTime;
        return SanitizeOutput(input, PhysicsStepV2(ref input));
    }
}
```

## Request Types

### CalculatePathRequest

Computes an A* path between two points:

| Field | Type | Description |
|-------|------|-------------|
| `MapId` | uint32 | WoW map ID (0=Eastern Kingdoms, 1=Kalimdor, etc.) |
| `Start` | Position | Starting coordinates (X, Y, Z) |
| `End` | Position | Destination coordinates |
| `Straight` | bool | If true, attempts direct path without navmesh |

**Response**: List of `Position` waypoints

### LineOfSightRequest

Checks visibility between two points:

| Field | Type | Description |
|-------|------|-------------|
| `MapId` | uint32 | WoW map ID |
| `From` | Position | Observer position |
| `To` | Position | Target position |

**Response**: `InLos` (bool)

### PhysicsInput

Simulates one physics tick for character movement:

| Field | Type | Description |
|-------|------|-------------|
| `MapId` | uint32 | Current map |
| `PosX/Y/Z` | float | Current position |
| `VelX/Y/Z` | float | Current velocity |
| `MovementFlags` | uint32 | Movement state flags |
| `WalkSpeed/RunSpeed/SwimSpeed` | float | Movement speeds |
| `Race/Gender` | uint32 | For collision capsule size |
| `DeltaTime` | float | Time step (typically 0.016s) |

**Response**: `PhysicsOutput` with new position, velocity, and flags

## Configuration

Configure via `appsettings.json`:

```json
{
  "PathfindingService": {
    "IpAddress": "127.0.0.1",
    "Port": 5000
  }
}
```

## Dependencies

| Package | Version | Purpose |
|---------|---------|---------|
| Microsoft.Extensions.Hosting | 8.0.0 | Worker service hosting |
| Google.Protobuf | 3.27.3 | IPC message serialization |

## Project References

- **BotCommLayer**: Protobuf message definitions (`PathfindingRequest`, `PathfindingResponse`)
- **GameData.Core**: Shared models (`XYZ`, `Position`, `MovementFlags`, race dimensions)

## Native Dependencies

The service requires `Navigation.dll` (C++) in the output directory:

| Component | Purpose |
|-----------|---------|
| `Navigation.dll` | Detour/Recast pathfinding + physics engine |
| `mmaps/*.mmap` | Precomputed navmesh data per map zone |

## Usage Examples

### Client-Side (via PathfindingClient)

```csharp
using BotRunner.Clients;

var client = new PathfindingClient("localhost", 5000);

// Calculate path
var path = await client.CalculatePathAsync(
    mapId: 0,
    start: new Position(100, 200, 50),
    end: new Position(500, 600, 55)
);

foreach (var waypoint in path)
{
    Console.WriteLine($"Waypoint: {waypoint.X}, {waypoint.Y}, {waypoint.Z}");
}

// Check line of sight
var hasLos = await client.CheckLineOfSightAsync(
    mapId: 0,
    from: playerPosition,
    to: targetPosition
);
```

### Direct Protobuf Request

```csharp
var request = new PathfindingRequest
{
    Path = new CalculatePathRequest
    {
        MapId = 0,
        Start = new Position { X = 100, Y = 200, Z = 50 },
        End = new Position { X = 500, Y = 600, Z = 55 },
        Straight = false
    }
};

var response = await client.SendAsync(request);
var waypoints = response.Path.Corners;
```

## Physics Constants

The physics engine uses WoW-accurate constants:

| Constant | Value | Description |
|----------|-------|-------------|
| `GRAVITY` | 19.29 | Gravity (yards/s²) |
| `JUMP_VELOCITY` | 7.96 | Initial jump velocity |
| `STEP_HEIGHT` | 2.125 | Max auto-step height |
| `STEP_DOWN_HEIGHT` | 4.0 | Max ground snap distance |

## Running the Service

### Development

```bash
dotnet run --project Services/PathfindingService
```

### With Aspire (Orchestrated)

The service is registered in `WWoW.Systems.AppHost`:

```csharp
var pathfinding = builder.AddProject<Projects.PathfindingService>("pathfinding");
```

### Standalone

```bash
dotnet publish -c Release -o ./publish
./publish/PathfindingService.exe
```

## Consumers

This service is used by:
- **BackgroundBotRunner**: Headless bot navigation
- **ForegroundBotRunner**: In-process bot navigation
- **WoWSharpClient**: Client-side movement prediction

## Related Documentation

- See `Exports/Navigation/README.md` for native library details
- See `Exports/BotCommLayer/README.md` for IPC protocol
- See `BotRunner/Clients/PathfindingClient.cs` for client implementation
- See `ARCHITECTURE.md` for system overview

---

*This component is part of the WWoW (Westworld of Warcraft) simulation platform.*
