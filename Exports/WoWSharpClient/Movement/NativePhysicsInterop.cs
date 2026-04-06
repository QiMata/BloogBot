using System;
using System.Runtime.InteropServices;

namespace WoWSharpClient.Movement;

/// <summary>
/// P/Invoke declarations for Navigation.dll's scene and physics exports.
/// Struct layout must match PhysicsBridge.h exactly.
/// </summary>
internal static class NativePhysics
{
    private const string DllName = "Navigation";

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    public static extern void SetDataDirectory(string dataDir);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    public static extern void SetSceneSliceMode([MarshalAs(UnmanagedType.I1)] bool enabled);

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
    public struct DynamicObjectInfo
    {
        public ulong Guid;
        public uint DisplayId;
        public float X, Y, Z;
        public float Orientation;
        public float Scale;
        public uint GoState;
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
        public float GroundZ;
        public float LiquidZ;
        public uint LiquidType;
        public float GroundNx, GroundNy, GroundNz;
        public float PendingDepenX, PendingDepenY, PendingDepenZ;
        public uint StandingOnInstanceId;
        public float StandingOnLocalX, StandingOnLocalY, StandingOnLocalZ;
        public float FallDistance;
        public float FallStartZ;
        public float FallTime;
        public int CurrentSplineIndex;
        public float SplineProgress;
        [MarshalAs(UnmanagedType.I1)]
        public bool HitWall;
        public float WallNormalX, WallNormalY, WallNormalZ;
        public float BlockedFraction;
        public float StepUpBaseZ;
        public uint StepUpAge;
        public uint GroundedWallState;
    }
}
