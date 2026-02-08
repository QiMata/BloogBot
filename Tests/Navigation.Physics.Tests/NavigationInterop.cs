// NavigationInterop.cs - P/Invoke declarations for Navigation.dll physics functions
// This provides direct access to the C++ physics engine for testing.

using System.Runtime.InteropServices;

namespace Navigation.Physics.Tests;

/// <summary>
/// P/Invoke declarations for Navigation.dll physics and geometry functions.
/// These allow direct testing of the C++ physics engine from managed code.
/// </summary>
public static partial class NavigationInterop
{
    private const string NavigationDll = "Navigation.dll";

    // ==========================================================================
    // VECTOR3 STRUCTURE (matches G3D::Vector3)
    // ==========================================================================

    [StructLayout(LayoutKind.Sequential)]
    public struct Vector3
    {
        public float X;
        public float Y;
        public float Z;

        public Vector3(float x, float y, float z)
        {
            X = x;
            Y = y;
            Z = z;
        }

        public readonly float Length() => MathF.Sqrt(X * X + Y * Y + Z * Z);
        public readonly float LengthSquared() => X * X + Y * Y + Z * Z;

        public readonly Vector3 Normalized()
        {
            float len = Length();
            return len > 1e-6f ? new Vector3(X / len, Y / len, Z / len) : new Vector3(0, 0, 1);
        }

        public static float Dot(Vector3 a, Vector3 b) => a.X * b.X + a.Y * b.Y + a.Z * b.Z;

        public static Vector3 Cross(Vector3 a, Vector3 b) =>
            new(a.Y * b.Z - a.Z * b.Y, a.Z * b.X - a.X * b.Z, a.X * b.Y - a.Y * b.X);

        public static Vector3 operator +(Vector3 a, Vector3 b) => new(a.X + b.X, a.Y + b.Y, a.Z + b.Z);
        public static Vector3 operator -(Vector3 a, Vector3 b) => new(a.X - b.X, a.Y - b.Y, a.Z - b.Z);
        public static Vector3 operator *(Vector3 v, float s) => new(v.X * s, v.Y * s, v.Z * s);
        public static Vector3 operator *(float s, Vector3 v) => new(v.X * s, v.Y * s, v.Z * s);

        public override readonly string ToString() => $"({X:F4}, {Y:F4}, {Z:F4})";
    }

    // ==========================================================================
    // CAPSULE STRUCTURE
    // ==========================================================================

    [StructLayout(LayoutKind.Sequential)]
    public struct Capsule
    {
        public Vector3 P0;  // Bottom sphere center
        public Vector3 P1;  // Top sphere center
        public float Radius;

        /// <summary>
        /// Creates a capsule with feet at (x, y, z), given radius and total height.
        /// The capsule axis goes from (x, y, z + r) to (x, y, z + h - r).
        /// </summary>
        public static Capsule FromFeetPosition(float x, float y, float z, float radius, float height)
        {
            return new Capsule
            {
                P0 = new Vector3(x, y, z + radius),
                P1 = new Vector3(x, y, z + height - radius),
                Radius = radius
            };
        }
    }

    // ==========================================================================
    // TRIANGLE STRUCTURE
    // ==========================================================================

    [StructLayout(LayoutKind.Sequential)]
    public struct Triangle
    {
        public Vector3 A;
        public Vector3 B;
        public Vector3 C;

        public Triangle(Vector3 a, Vector3 b, Vector3 c)
        {
            A = a;
            B = b;
            C = c;
        }

        /// <summary>
        /// Computes the triangle's surface normal (right-hand rule: AB � AC)
        /// </summary>
        public readonly Vector3 Normal()
        {
            var ab = B - A;
            var ac = C - A;
            return Vector3.Cross(ab, ac).Normalized();
        }
    }

    // ==========================================================================
    // TERRAIN TRIANGLE (matches MapFormat::TerrainTriangle)
    // ==========================================================================

    [StructLayout(LayoutKind.Sequential)]
    public struct TerrainTriangle
    {
        public float Ax, Ay, Az;
        public float Bx, By, Bz;
        public float Cx, Cy, Cz;

        public readonly Triangle ToTriangle() => new(
            new Vector3(Ax, Ay, Az),
            new Vector3(Bx, By, Bz),
            new Vector3(Cx, Cy, Cz)
        );
    }

    // ==========================================================================
    // PHYSICS INPUT (matches PhysicsBridge.h PhysicsInput exactly)
    // ==========================================================================

    [StructLayout(LayoutKind.Sequential)]
    public struct PhysicsInput
    {
        public uint MoveFlags;
        public float X, Y, Z;
        public float Orientation;
        public float Pitch;
        public float Vx, Vy, Vz;
        public float WalkSpeed;
        public float RunSpeed;
        public float RunBackSpeed;
        public float SwimSpeed;
        public float SwimBackSpeed;
        public float FlightSpeed;
        public float TurnSpeed;
        public ulong TransportGuid;
        public float TransportX, TransportY, TransportZ, TransportO;
        public uint FallTime;
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
        public uint MapId;
        public float DeltaTime;
        public uint FrameCounter;
    }

    // ==========================================================================
    // PHYSICS OUTPUT (matches PhysicsBridge.h PhysicsOutput exactly)
    // ==========================================================================

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
        public float FallTime;
        public int CurrentSplineIndex;
        public float SplineProgress;
    }

    // ==========================================================================
    // MOVE FLAGS (matches PhysicsBridge.h)
    // ==========================================================================

    [Flags]
    public enum MoveFlags : uint
    {
        None = 0x00000000,
        Forward = 0x00000001,
        Backward = 0x00000002,
        StrafeLeft = 0x00000004,
        StrafeRight = 0x00000008,
        TurnLeft = 0x00000010,
        TurnRight = 0x00000020,
        PitchUp = 0x00000040,
        PitchDown = 0x00000080,
        Walking = 0x00000100,
        OnTransport = 0x00000200,
        Jumping = 0x00002000,
        Falling = 0x00004000,
        Swimming = 0x00200000,
        Flying = 0x02000000,
        FallingFar = 0x01000000,
    }

    // ==========================================================================
    // PHYSICS ENGINE FUNCTIONS (using DllImport for complex types)
    // ==========================================================================

    /// <summary>
    /// Initializes the physics engine (call once at startup)
    /// </summary>
    [DllImport(NavigationDll, EntryPoint = "InitializePhysics", CallingConvention = CallingConvention.Cdecl)]
    [return: MarshalAs(UnmanagedType.I1)]
    public static extern bool InitializePhysics();

    /// <summary>
    /// Shuts down the physics engine
    /// </summary>
    [DllImport(NavigationDll, EntryPoint = "ShutdownPhysics", CallingConvention = CallingConvention.Cdecl)]
    public static extern void ShutdownPhysics();

    // ==========================================================================
    // MAP/TERRAIN FUNCTIONS
    // ==========================================================================

    /// <summary>
    /// Initializes the map loader with the data path
    /// </summary>
    [DllImport(NavigationDll, EntryPoint = "InitializeMapLoader", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    [return: MarshalAs(UnmanagedType.I1)]
    public static extern bool InitializeMapLoader(string dataPath);

    /// <summary>
    /// Loads a specific map tile
    /// </summary>
    [DllImport(NavigationDll, EntryPoint = "LoadMapTile", CallingConvention = CallingConvention.Cdecl)]
    [return: MarshalAs(UnmanagedType.I1)]
    public static extern bool LoadMapTile(uint mapId, uint tileX, uint tileY);

    /// <summary>
    /// Gets terrain height at a world position
    /// </summary>
    [DllImport(NavigationDll, EntryPoint = "GetTerrainHeight", CallingConvention = CallingConvention.Cdecl)]
    public static extern float GetTerrainHeight(uint mapId, float x, float y);

    // ==========================================================================
    // GEOMETRY QUERY FUNCTIONS (for testing)
    // ==========================================================================

    /// <summary>
    /// Queries terrain triangles in a world-space bounding box.
    /// Returns the number of triangles written to the buffer.
    /// </summary>
    [DllImport(NavigationDll, EntryPoint = "QueryTerrainTriangles", CallingConvention = CallingConvention.Cdecl)]
    public static extern int QueryTerrainTriangles(
        uint mapId,
        float minX, float minY,
        float maxX, float maxY,
        [Out] TerrainTriangle[] triangles,
        int maxTriangles);

    // ==========================================================================
    // PURE GEOMETRY TESTS (no map data needed)
    // ==========================================================================

    /// <summary>
    /// Tests intersection between a capsule and a single triangle.
    /// This is a pure geometric test with no map data dependency.
    /// </summary>
    [DllImport(NavigationDll, EntryPoint = "IntersectCapsuleTriangle", CallingConvention = CallingConvention.Cdecl)]
    [return: MarshalAs(UnmanagedType.I1)]
    public static extern bool IntersectCapsuleTriangle(
        in Capsule capsule,
        in Triangle triangle,
        out float depth,
        out Vector3 normal,
        out Vector3 point);

    /// <summary>
    /// Performs a capsule sweep against a single triangle.
    /// This is a pure geometric test with no map data dependency.
    /// </summary>
    [DllImport(NavigationDll, EntryPoint = "SweepCapsuleTriangle", CallingConvention = CallingConvention.Cdecl)]
    [return: MarshalAs(UnmanagedType.I1)]
    public static extern bool SweepCapsuleTriangle(
        in Capsule capsule,
        in Vector3 velocity,
        in Triangle triangle,
        out float toi,
        out Vector3 normal,
        out Vector3 impactPoint);

    /// <summary>
    /// Gets physics constants for validation
    /// </summary>
    [DllImport(NavigationDll, EntryPoint = "GetPhysicsConstants", CallingConvention = CallingConvention.Cdecl)]
    public static extern void GetPhysicsConstants(
        out float gravity,
        out float jumpVelocity,
        out float stepHeight,
        out float stepDownHeight,
        out float walkableMinNormalZ);

    // ==========================================================================
    // PHYSICS STEP FUNCTION
    // ==========================================================================

    /// <summary>
    /// Steps the physics simulation by one frame. deltaTime is embedded in PhysicsInput.
    /// </summary>
    [DllImport(NavigationDll, EntryPoint = "PhysicsStepV2", CallingConvention = CallingConvention.Cdecl)]
    public static extern PhysicsOutput StepPhysicsV2(ref PhysicsInput input);

    /// <summary>
    /// Preloads map data for a given map ID.
    /// </summary>
    [DllImport(NavigationDll, EntryPoint = "PreloadMap", CallingConvention = CallingConvention.Cdecl)]
    public static extern void PreloadMap(uint mapId);
}
