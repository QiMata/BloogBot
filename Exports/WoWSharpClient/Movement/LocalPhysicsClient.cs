using System;
using System.Runtime.InteropServices;
using BotRunner.Clients;
using GameData.Core.Constants;
using GameData.Core.Enums;
using Microsoft.Extensions.Logging;
using ProtoPhysicsInput = Pathfinding.PhysicsInput;
using ProtoPhysicsOutput = Pathfinding.PhysicsOutput;

namespace WoWSharpClient.Movement;

/// <summary>
/// Direct P/Invoke physics client — calls Navigation.dll's PhysicsStepV2 in-process
/// with zero TCP/IPC latency. This matches WoW.exe's architecture where CMovement::Update
/// runs physics synchronously on the render thread.
///
/// Eliminates the 5-20ms TCP round-trip that PathfindingClient adds per physics frame,
/// enabling true 60 FPS physics matching the original binary.
/// </summary>
public sealed class LocalPhysicsClient : IPhysicsClient, IDisposable
{
    private readonly ILogger _logger;
    private SceneDataClient? _sceneDataClient;
    private bool _initialized;
    private uint _currentMapId;
    private float _lastSceneX, _lastSceneY;
    private bool _disposed;

    public LocalPhysicsClient(ILogger logger, SceneDataClient? sceneDataClient = null)
    {
        _logger = logger;
        _sceneDataClient = sceneDataClient;
    }

    /// <summary>
    /// Set the SceneDataClient for on-demand terrain loading.
    /// If not set, the client relies on pre-loaded scene data from disk.
    /// </summary>
    public void SetSceneDataClient(SceneDataClient client) => _sceneDataClient = client;

    /// <summary>
    /// Ensure Navigation.dll has loaded the specified map's collision data.
    /// Called automatically on first PhysicsStep or when map changes.
    /// </summary>
    public void EnsureMapLoaded(uint mapId)
    {
        if (_initialized && _currentMapId == mapId) return;

        _logger.LogInformation("[LocalPhysics] Loading map {MapId} collision data...", mapId);
        NativePhysics.PreloadMap(mapId);
        _currentMapId = mapId;
        _initialized = true;
        _logger.LogInformation("[LocalPhysics] Map {MapId} loaded.", mapId);
    }

    public ProtoPhysicsOutput PhysicsStep(ProtoPhysicsInput proto)
    {
        if (!_initialized || proto.MapId != _currentMapId)
            EnsureMapLoaded(proto.MapId);

        // Request scene data around current position if SceneDataClient is available.
        // Only re-request when position moves >100y from last request.
        if (_sceneDataClient != null)
        {
            float dx = proto.PosX - _lastSceneX;
            float dy = proto.PosY - _lastSceneY;
            if (dx * dx + dy * dy > 100f * 100f || (_lastSceneX == 0 && _lastSceneY == 0))
            {
                _sceneDataClient.EnsureSceneDataAround(proto.MapId, proto.PosX, proto.PosY);
                _lastSceneX = proto.PosX;
                _lastSceneY = proto.PosY;
            }
        }

        var (radius, height) = RaceDimensions.GetCapsuleForRace(
            (Race)proto.Race, (Gender)proto.Gender);

        var input = new NativePhysics.PhysicsInput
        {
            MoveFlags = proto.MovementFlags,
            X = proto.PosX,
            Y = proto.PosY,
            Z = proto.PosZ,
            Orientation = proto.Facing,
            Pitch = proto.SwimPitch,
            Vx = proto.VelX,
            Vy = proto.VelY,
            Vz = proto.VelZ,
            WalkSpeed = proto.WalkSpeed,
            RunSpeed = proto.RunSpeed,
            RunBackSpeed = proto.RunBackSpeed,
            SwimSpeed = proto.SwimSpeed,
            SwimBackSpeed = proto.SwimBackSpeed,
            FlightSpeed = 7.0f,
            TransportGuid = proto.TransportGuid,
            TransportX = proto.TransportOffsetX,
            TransportY = proto.TransportOffsetY,
            TransportZ = proto.TransportOffsetZ,
            TransportO = proto.TransportOrientation,
            FallTime = (uint)proto.FallTime,
            FallStartZ = proto.FallStartZ != 0 ? proto.FallStartZ : -200000f,
            Height = height,
            Radius = radius,
            HasSplinePath = false,
            SplinePoints = IntPtr.Zero,
            SplinePointCount = 0,
            CurrentSplineIndex = 0,
            PrevGroundZ = proto.PrevGroundZ,
            PrevGroundNx = proto.PrevGroundNx,
            PrevGroundNy = proto.PrevGroundNy,
            PrevGroundNz = proto.PrevGroundNz,
            PendingDepenX = proto.PendingDepenX,
            PendingDepenY = proto.PendingDepenY,
            PendingDepenZ = proto.PendingDepenZ,
            StandingOnInstanceId = proto.StandingOnInstanceId,
            StandingOnLocalX = proto.StandingOnLocalX,
            StandingOnLocalY = proto.StandingOnLocalY,
            StandingOnLocalZ = proto.StandingOnLocalZ,
            NearbyObjects = IntPtr.Zero,
            NearbyObjectCount = 0,
            MapId = proto.MapId,
            DeltaTime = proto.DeltaTime,
            FrameCounter = proto.FrameCounter,
            PhysicsFlags = proto.PhysicsFlags,
            StepUpBaseZ = proto.StepUpBaseZ,
            StepUpAge = proto.StepUpAge,
            GroundedWallState = 0, // TODO: wire from proto when activated
            WasGrounded = proto.WasGrounded ? 1u : 0u,
        };

        // TODO: marshal nearby objects for dynamic collision (elevators, doors)
        // For now, dynamic objects are not passed. This matches the unit test behavior.
        // Full implementation needs GCHandle.Alloc + pinning.

        var output = NativePhysics.PhysicsStepV2(ref input);

        // Sanitize output flags
        var outFlags = (MovementFlags)output.MoveFlags;
        outFlags &= ~MovementFlags.MOVEFLAG_MOVED;
        output.MoveFlags = (uint)outFlags;

        return new ProtoPhysicsOutput
        {
            NewPosX = output.X,
            NewPosY = output.Y,
            NewPosZ = output.Z,
            Orientation = output.Orientation,
            Pitch = output.Pitch,
            NewVelX = output.Vx,
            NewVelY = output.Vy,
            NewVelZ = output.Vz,
            MovementFlags = output.MoveFlags,
            FallTime = output.FallTime,
            FallStartZ = output.FallStartZ,
            FallDistance = output.FallDistance,
            GroundZ = output.GroundZ,
            GroundNx = output.GroundNx,
            GroundNy = output.GroundNy,
            GroundNz = output.GroundNz,
            LiquidZ = output.LiquidZ,
            LiquidType = output.LiquidType,
            PendingDepenX = output.PendingDepenX,
            PendingDepenY = output.PendingDepenY,
            PendingDepenZ = output.PendingDepenZ,
            StandingOnInstanceId = output.StandingOnInstanceId,
            StandingOnLocalX = output.StandingOnLocalX,
            StandingOnLocalY = output.StandingOnLocalY,
            StandingOnLocalZ = output.StandingOnLocalZ,
            HitWall = output.HitWall,
            WallNormalX = output.WallNormalX,
            WallNormalY = output.WallNormalY,
            WallNormalZ = output.WallNormalZ,
            BlockedFraction = output.BlockedFraction,
            StepUpBaseZ = output.StepUpBaseZ,
            StepUpAge = output.StepUpAge,
            // GroundedWallState flows through the C struct but not the proto output.
            // It's carried in the input via PhysicsInput.GroundedWallState.
        };
    }

    public void Dispose()
    {
        _disposed = true;
    }
}

/// <summary>
/// P/Invoke declarations for Navigation.dll's physics exports.
/// Struct layout must match PhysicsBridge.h exactly.
/// </summary>
internal static class NativePhysics
{
    private const string DllName = "Navigation";

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    public static extern void PreloadMap(uint mapId);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    public static extern PhysicsOutput PhysicsStepV2(ref PhysicsInput input);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    public static extern float GetGroundZ(uint mapId, float x, float y, float queryZ, float maxSearchDist);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    public static extern bool InjectSceneTriangles(uint mapId, float minX, float minY, float maxX, float maxY,
        IntPtr triangles, int triangleCount);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    public static extern void ClearSceneCache(uint mapId);

    [StructLayout(LayoutKind.Sequential)]
    public struct InjectedTriangle
    {
        public float V0X, V0Y, V0Z;
        public float V1X, V1Y, V1Z;
        public float V2X, V2Y, V2Z;
        public float NX, NY, NZ;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct PhysicsInput
    {
        public uint MoveFlags;
        public float X, Y, Z;
        public float Orientation;
        public float Pitch;
        public float Vx, Vy, Vz;
        public float WalkSpeed, RunSpeed, RunBackSpeed, SwimSpeed, SwimBackSpeed;
        public float FlightSpeed;
        public float TurnSpeed;
        public ulong TransportGuid;
        public float TransportX, TransportY, TransportZ, TransportO;
        public uint FallTime;
        public float FallStartZ;
        public float Height;
        public float Radius;
        [MarshalAs(UnmanagedType.I1)]
        public bool HasSplinePath;
        public float SplineSpeed;
        public IntPtr SplinePoints;
        public int SplinePointCount;
        public int CurrentSplineIndex;
        public float PrevGroundZ;
        public float PrevGroundNx, PrevGroundNy, PrevGroundNz;
        public float PendingDepenX, PendingDepenY, PendingDepenZ;
        public uint StandingOnInstanceId;
        public float StandingOnLocalX, StandingOnLocalY, StandingOnLocalZ;
        public IntPtr NearbyObjects;
        public int NearbyObjectCount;
        public uint MapId;
        public float DeltaTime;
        public uint FrameCounter;
        public uint PhysicsFlags;
        public float StepUpBaseZ;
        public uint StepUpAge;
        public uint GroundedWallState;
        public uint WasGrounded;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct PhysicsOutput
    {
        public float X, Y, Z;
        public float Orientation;
        public float Pitch;
        public float Vx, Vy, Vz;
        public uint MoveFlags;
        public float FallTime;
        public float FallStartZ;
        public float FallDistance;
        public float GroundZ;
        public float GroundNx, GroundNy, GroundNz;
        public float LiquidZ;
        public uint LiquidType;
        public float PendingDepenX, PendingDepenY, PendingDepenZ;
        public uint StandingOnInstanceId;
        public float StandingOnLocalX, StandingOnLocalY, StandingOnLocalZ;
        [MarshalAs(UnmanagedType.I1)]
        public bool HitWall;
        public float WallNormalX, WallNormalY, WallNormalZ;
        public float BlockedFraction;
        public float StepUpBaseZ;
        public uint StepUpAge;
        public uint GroundedWallState;
    }
}
