// NavigationInterop.cs - P/Invoke declarations for Navigation.dll physics functions
// This provides direct access to the C++ physics engine for testing.

using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Reflection;

namespace Navigation.Physics.Tests;

/// <summary>
/// P/Invoke declarations for Navigation.dll physics and geometry functions.
/// These allow direct testing of the C++ physics engine from managed code.
/// </summary>
public static partial class NavigationInterop
{
    private const string NavigationDll = "Navigation.dll";

    static NavigationInterop()
    {
        NativeLibrary.SetDllImportResolver(typeof(NavigationInterop).Assembly, ResolveNavigationLibrary);
    }

    private static IntPtr ResolveNavigationLibrary(string libraryName, Assembly assembly, DllImportSearchPath? searchPath)
    {
        if (!string.Equals(libraryName, NavigationDll, StringComparison.OrdinalIgnoreCase))
        {
            return IntPtr.Zero;
        }

        var baseDir = AppContext.BaseDirectory;
        var arch = RuntimeInformation.ProcessArchitecture;

        // Try platform-specific subdirectory first (x86/ or x64/)
        var subdir = arch == Architecture.X86 ? "x86" : "x64";
        var platformPath = Path.Combine(baseDir, subdir, NavigationDll);
        if (File.Exists(platformPath) && NativeLibrary.TryLoad(platformPath, out var handle))
            return handle;

        // Fall back to default location
        string preferredPath = Path.Combine(baseDir, NavigationDll);
        if (File.Exists(preferredPath) && NativeLibrary.TryLoad(preferredPath, out handle))
            return handle;

        return IntPtr.Zero;
    }

    // ==========================================================================
    // VECTOR3 STRUCTURE (matches G3D::Vector3)
    // ==========================================================================

    [StructLayout(LayoutKind.Sequential)]
    public struct Vector3(float x, float y, float z)
    {
        public float X = x;
        public float Y = y;
        public float Z = z;

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
    public struct Triangle(NavigationInterop.Vector3 a, NavigationInterop.Vector3 b, NavigationInterop.Vector3 c)
    {
        public Vector3 A = a;
        public Vector3 B = b;
        public Vector3 C = c;

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

    [StructLayout(LayoutKind.Sequential)]
    public struct TerrainAabbContact
    {
        public Vector3 Point;
        public Vector3 Normal;
        public Vector3 RawNormal;
        public Vector3 TriangleA;
        public Vector3 TriangleB;
        public Vector3 TriangleC;
        public float PlaneDistance;
        public float Distance;
        public uint InstanceId;
        public uint SourceType;
        public uint Walkable;

        public readonly Triangle ToTriangle() => new(TriangleA, TriangleB, TriangleC);
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct InjectedTriangle
    {
        public float V0X, V0Y, V0Z;
        public float V1X, V1Y, V1Z;
        public float V2X, V2Y, V2Z;
        public uint SourceType;
        public uint InstanceId;
        public uint GroupFlags;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct TerrainQueryPairPayload
    {
        public float First;
        public float Second;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct TerrainQueryChunkSpan
    {
        public int CellMinX;
        public int CellMaxX;
        public int CellMinY;
        public int CellMaxY;
        public int ChunkMinX;
        public int ChunkMaxX;
        public int ChunkMinY;
        public int ChunkMaxY;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct TerrainQueryChunkCoordinate
    {
        public int Primary;
        public int Secondary;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct TerrainQueryMergedQueryTrace
    {
        public Vector3 QueryBoundsMin;
        public Vector3 QueryBoundsMax;
        public Vector3 MergedBoundsMin;
        public Vector3 MergedBoundsMax;
        public uint CacheContainsBoundsMin;
        public uint CacheContainsBoundsMax;
        public uint ReusedCachedQuery;
        public uint BuiltMergedBounds;
        public uint BuiltQueryMask;
        public uint QueryInvoked;
        public uint QueryDispatchSucceeded;
        public uint ReturnedSuccess;
        public uint QueryMask;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct TerrainQuerySelectedContactContainerTrace
    {
        public TerrainQueryMergedQueryTrace MergedQuery;
        public uint ReusedExistingContainer;
        public uint CopiedQueryResults;
        public uint ReturnedSuccess;
        public uint OutputContactCount;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct SelectorSupportPlane
    {
        public Vector3 Normal;
        public float PlaneDistance;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct SelectorCandidateValidationTrace
    {
        public float InputBestRatio;
        public float CandidateBestRatio;
        public float OutputBestRatio;
        public uint FirstPassAllBelowLooseThreshold;
        public uint RebuildExecuted;
        public uint RebuildSucceeded;
        public uint SecondPassAllBelowStrictThreshold;
        public uint ImprovedBestRatio;
        public uint FinalStripCount;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct SelectorCandidateRecord
    {
        public SelectorSupportPlane FilterPlane;
        public Vector3 Point0;
        public Vector3 Point1;
        public Vector3 Point2;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct SelectorSourceScanWindow
    {
        public int RowMin;
        public int ColumnMin;
        public int RowMax;
        public int ColumnMax;
        public int PointStartIndex;
        public int RowAdvancePointCount;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct SelectorBvhNodeRecord
    {
        public ushort ControlWord;
        public ushort LowChildIndex;
        public ushort HighChildIndex;
        public ushort LeafTriangleCount;
        public uint LeafTriangleStartIndex;
        public float SplitCoordinate;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct SelectorBvhChildTraversal
    {
        public uint Axis;
        public float SplitCoordinate;
        public uint LowChildIndex;
        public uint HighChildIndex;
        public uint VisitLow;
        public uint VisitHigh;
        public Vector3 LowBoundsMin;
        public Vector3 LowBoundsMax;
        public Vector3 HighBoundsMin;
        public Vector3 HighBoundsMax;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct SelectorObjectRouterEntryRecord
    {
        public Vector3 BoundsMin;
        public Vector3 BoundsMax;
        public ulong NodeToken;
        public uint NodeEnabled;
        public uint CallbackReturn;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct SelectorObjectRouterTrace
    {
        public uint OverlapRejectedCount;
        public uint NodeRejectedCount;
        public uint DispatchedCount;
        public uint AccumulatorUpdatedCount;
        public uint Result;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct SelectorObjectNoCallbackState
    {
        public uint HitResult;
        public uint RecordCount;
        public uint OutputFlags;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct SelectorLeafQueueMutationTrace
    {
        public uint SkippedByMask;
        public uint Overflowed;
        public uint PendingEnqueued;
        public uint VisitedBitSet;
        public uint PredicateRejected;
        public uint AcceptedEnqueued;
        public uint StateByteBefore;
        public uint StateByteAfter;
        public uint PendingCountAfter;
        public uint AcceptedCountAfter;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct SelectorNodeTraversalRecord
    {
        public ulong TraversalBaseToken;
        public ulong ExtraNodeToken;
        public ulong StateBytesToken;
        public ulong VertexBufferToken;
        public ulong TriangleIndexToken;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct SelectorNodeTraversalPayload
    {
        public Vector3 QueryBoundsMin;
        public Vector3 QueryBoundsMax;
        public uint CallbackMaskWord;
        public uint AcceptedCount;
        public ulong TraversalBaseToken;
        public ulong ExtraNodeToken;
        public ulong StateBytesToken;
        public ulong VertexBufferToken;
        public ulong TriangleIndexToken;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct SelectorRecordEvaluationTrace
    {
        public float InputBestRatio;
        public float OutputBestRatio;
        public float SelectedBestRatio;
        public uint RecordCount;
        public uint DotRejectedCount;
        public uint ClipRejectedCount;
        public uint ValidationRejectedCount;
        public uint ValidationAcceptedCount;
        public uint UpdatedBestRatio;
        public uint SelectedRecordIndex;
        public uint SelectedStripCount;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct SelectorSourceRankingTrace
    {
        public float InputBestRatio;
        public float OutputBestRatio;
        public uint DotRejectedCount;
        public uint BuilderRejectedCount;
        public uint EvaluatorRejectedCount;
        public uint AcceptedSourceCount;
        public uint OverwriteCount;
        public uint AppendCount;
        public uint BestRatioUpdatedCount;
        public uint SwappedBestToFront;
        public uint FinalCandidateCount;
        public uint SelectedSourceIndex;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct SelectorDirectionRankingTrace
    {
        public float InputBestRatio;
        public float OutputBestRatio;
        public float ReportedBestRatio;
        public uint DotRejectedCount;
        public uint BuilderRejectedCount;
        public uint EvaluatorRejectedCount;
        public uint AcceptedDirectionCount;
        public uint OverwriteCount;
        public uint AppendCount;
        public uint BestRatioUpdatedCount;
        public uint SwappedBestToFront;
        public uint ZeroClampedOutput;
        public uint FinalCandidateCount;
        public uint SelectedDirectionIndex;
        public uint SelectedRecordIndex;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct SelectorTriangleEdgeDirectionTrace
    {
        public float BestScore;
        public uint ZeroLengthRejectedCount;
        public uint PointToLineScoredCount;
        public uint PlaneScoredCount;
        public uint SelectedEdgeIndex;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct SelectorTwoCandidateWorkingVectorTrace
    {
        public uint ReturnedSelectedNormal;
        public uint ReturnedNegatedFirstCandidate;
        public uint ReturnedConstructedVector;
        public uint RejectedByLineZGate;
        public uint RejectedBySelectedPlaneDotGate;
        public uint RejectedByFootprintMismatch;
        public uint OrientationNegated;
        public uint SelectedEdgeIndex;
        public Vector3 LineDirection;
        public Vector3 EdgeDirection;
        public Vector3 WorkingVector;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct SelectorAlternatePairTrace
    {
        public uint UsedNegatedInputWorkingVector;
        public uint UsedNegatedFirstCandidate;
        public uint UsedTwoCandidateBuilder;
        public uint UsedSelectedContactNormal;
        public uint NormalizedHorizontal;
        public float HorizontalMagnitude;
        public float Denominator;
        public float Scale;
        public Vector3 WorkingVector;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct SelectorPair
    {
        public float First;
        public float Second;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct SelectorPairConsumerTrace
    {
        public float RequestedDistance;
        public int SelectedIndex;
        public uint SelectedCount;
        public uint DirectionRankingAccepted;
        public uint DirectGateAccepted;
        public uint DirectGateState;
        public uint AlternateUnitZState;
        public uint ReturnedDirectPair;
        public uint ReturnedAlternatePair;
        public uint ReturnedZeroPair;
        public uint PreservedInputMove;
        public uint ZeroedMoveOnRankingFailure;
        public int ReturnCode;
        public Vector3 InputMove;
        public Vector3 OutputMove;
        public SelectorPair OutputPair;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct SelectorTriangleSourceWrapperTrace
    {
        public uint SupportPlaneInitCount;
        public uint ValidationPlaneInitCount;
        public uint ScratchPointZeroCount;
        public uint UsedOverridePosition;
        public uint TerrainQueryInvoked;
        public uint TerrainQuerySucceeded;
        public uint ReturnedSuccess;
        public uint QueryFailureZeroedOutput;
        public Vector3 SelectedPosition;
        public Vector3 TestPoint;
        public Vector3 CandidateDirection;
        public float InitialBestRatio;
        public float InputBestRatio;
        public float ReportedBestRatio;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct SelectorTriangleSourceVariableTransactionTrace
    {
        public uint SupportPlaneInitCount;
        public uint ValidationPlaneInitCount;
        public uint ScratchPointZeroCount;
        public uint UsedOverridePosition;
        public uint TerrainQueryInvoked;
        public uint TerrainQuerySucceeded;
        public uint TerrainQueryReusedCachedQuery;
        public uint TerrainQueryBuiltMergedBounds;
        public uint TerrainQueryBuiltQueryMask;
        public uint RankingInvoked;
        public uint RankingAccepted;
        public uint ZeroClampedOutput;
        public uint ReturnedSuccess;
        public uint QueryFailureZeroedOutput;
        public Vector3 SelectedPosition;
        public Vector3 ProjectedPosition;
        public Vector3 TestPoint;
        public Vector3 CandidateDirection;
        public float InitialBestRatio;
        public float RankingReportedBestRatio;
        public float OutputReportedBestRatio;
        public uint RankingCandidateCount;
        public int RankingSelectedRecordIndex;
        public uint TerrainQueryMask;
    }

    public enum SelectorAlternateWorkingVectorMode : uint
    {
        NegatedFirstCandidate = 0,
        TwoCandidateBuilder = 1,
        SelectedContactNormal = 2,
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct GroundedWallSelectionTrace
    {
        public uint QueryContactCount;
        public uint CandidateCount;
        public uint SelectedContactIndex;
        public uint SelectedInstanceId;
        public uint SelectedSourceType;
        public uint RawWalkable;
        public uint WalkableWithoutState;
        public uint WalkableWithState;
        public uint GroundedWallStateBefore;
        public uint GroundedWallStateAfter;
        public uint UsedPositionReorientation;
        public uint UsedWalkableSelectedContact;
        public uint UsedNonWalkableVertical;
        public uint UsedUphillDiscard;
        public uint UsedPrimaryAxisFallback;
        public uint BranchKind;
        public Vector3 SelectedPoint;
        public Vector3 SelectedNormal;
        public Vector3 OrientedNormal;
        public Vector3 PrimaryAxis;
        public Vector3 MergedWallNormal;
        public Vector3 FinalWallNormal;
        public Vector3 HorizontalProjectedMove;
        public Vector3 BranchProjectedMove;
        public Vector3 FinalProjectedMove;
        public float RawOpposeScore;
        public float OrientedOpposeScore;
        public float Requested2D;
        public float HorizontalResolved2D;
        public float SlopedResolved2D;
        public float FinalResolved2D;
        public float BlockedFraction;
        public uint SelectedInstanceFlags;
        public uint SelectedModelFlags;
        public uint SelectedGroupFlags;
        public int SelectedRootId;
        public int SelectedGroupId;
        public uint SelectedGroupMatchFound;
        public uint SelectedResolvedModelFlags;
        public uint SelectedMetadataSource;
        public uint SelectedCurrentPositionInsidePrism;
        public uint SelectedProjectedPositionInsidePrism;
        public uint SelectedThresholdSensitiveStandard;
        public uint SelectedThresholdSensitiveRelaxed;
        public uint SelectedWouldUseDirectPairStandard;
        public uint SelectedWouldUseDirectPairRelaxed;
        public Vector3 SelectedThresholdPoint;
        public float SelectedThresholdNormalZ;
    }

    public enum GroundedWallBranchKind : uint
    {
        None = 0,
        Horizontal = 1,
        WalkableSelectedVertical = 2,
        NonWalkableVertical = 3,
    }

    // ==========================================================================
    // DYNAMIC OBJECT INFO (matches PhysicsBridge.h DynamicObjectInfo exactly)
    // ==========================================================================

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
        public IntPtr NearbyObjects;      // DynamicObjectInfo* — pinned array
        public int NearbyObjectCount;
        public uint MapId;
        public float DeltaTime;
        public uint FrameCounter;
        public uint PhysicsFlags;
        public float StepUpBaseZ;       // step-up height to maintain (-200000 = inactive)
        public uint StepUpAge;          // frames since step-up detected
        public uint GroundedWallState;  // internal selected-contact walkability state
        public uint WasGrounded;        // CMovement grounded state persistence (1=grounded, 0=airborne)
    }

    public const uint PHYSICS_FLAG_TRUST_INPUT_VELOCITY = 0x1;

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
        public float FallStartZ;
        public float FallTime;
        public int CurrentSplineIndex;
        public float SplineProgress;
        [MarshalAs(UnmanagedType.I1)]
        public bool HitWall;
        public float WallNormalX, WallNormalY, WallNormalZ;
        public float BlockedFraction;
        public float StepUpBaseZ;       // step-up height to maintain (-200000 = inactive)
        public uint StepUpAge;          // frames since step-up detected
        public uint GroundedWallState;  // internal selected-contact walkability state
        public uint EnvironmentFlags;
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
        Jumping = 0x00002000,
        Falling = 0x00004000,
        FallingFar = 0x00004000,
        Swimming = 0x00200000,
        Flying = 0x01000000,
        OnTransport = 0x02000000,
        SplineElevation = 0x04000000,
        SafeFall = 0x20000000,
        Hover = 0x40000000,
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
    /// Gets terrain height at a world position (ADT grid only, no VMAP)
    /// </summary>
    [DllImport(NavigationDll, EntryPoint = "GetTerrainHeight", CallingConvention = CallingConvention.Cdecl)]
    public static extern float GetTerrainHeight(uint mapId, float x, float y);

    /// <summary>
    /// Gets ground Z combining VMAP (WMO/M2 models) + ADT terrain.
    /// Returns highest walkable surface at or below z + 0.5.
    /// Query from different z heights to detect multi-level geometry.
    /// </summary>
    [DllImport(NavigationDll, EntryPoint = "GetGroundZ", CallingConvention = CallingConvention.Cdecl)]
    public static extern float GetGroundZ(uint mapId, float x, float y, float z, float maxSearchDist);

    [DllImport(NavigationDll, EntryPoint = "FindPath", CallingConvention = CallingConvention.Cdecl)]
    private static extern IntPtr FindPathNative(
        uint mapId,
        in Vector3 start,
        in Vector3 end,
        [MarshalAs(UnmanagedType.I1)] bool smoothPath,
        out int length);

    [DllImport(NavigationDll, EntryPoint = "PathArrFree", CallingConvention = CallingConvention.Cdecl)]
    private static extern void PathArrFree(IntPtr pathArr);

    public enum SegmentValidationResult : uint
    {
        Clear = 0,
        BlockedGeometry = 1,
        MissingSupport = 2,
        StepUpTooHigh = 3,
        StepDownTooFar = 4,
    }

    /// <summary>
    /// Validates whether an agent-sized capsule can traverse the segment while remaining
    /// on a supported surface, using the same step-up / step-down thresholds as the
    /// native movement stack.
    /// </summary>
    [DllImport(NavigationDll, EntryPoint = "ValidateWalkableSegment", CallingConvention = CallingConvention.Cdecl)]
    public static extern SegmentValidationResult ValidateWalkableSegment(
        uint mapId,
        in Vector3 start,
        in Vector3 end,
        float radius,
        float height,
        out float resolvedEndZ,
        out float supportDelta,
        out float travelFraction);

    public static Vector3[] FindPath(uint mapId, in Vector3 start, in Vector3 end, bool smoothPath)
    {
        var pathPtr = IntPtr.Zero;
        try
        {
            pathPtr = FindPathNative(mapId, start, end, smoothPath, out var length);
            if (pathPtr == IntPtr.Zero || length <= 0)
                return [];

            var path = new Vector3[length];
            var stride = Marshal.SizeOf<Vector3>();
            for (var i = 0; i < length; i++)
            {
                var currentPtr = IntPtr.Add(pathPtr, i * stride);
                path[i] = Marshal.PtrToStructure<Vector3>(currentPtr);
            }

            return path;
        }
        finally
        {
            if (pathPtr != IntPtr.Zero)
                PathArrFree(pathPtr);
        }
    }

    /// <summary>
    /// Diagnostic: bypass scene cache and query VMAP ray + ADT + BIH directly.
    /// Forces VMAP initialization if not already loaded.
    /// </summary>
    [DllImport(NavigationDll, EntryPoint = "GetGroundZBypassCache", CallingConvention = CallingConvention.Cdecl)]
    public static extern float GetGroundZBypassCache(
        uint mapId, float x, float y, float z, float maxSearchDist,
        out float vmapZ, out float adtZ, out float bihZ, out float sceneCacheZ);

    /// <summary>
    /// Diagnostic: enumerate ALL surfaces (triangles) at (x,y) from scene cache.
    /// No Z acceptance window filtering — returns all surfaces at any height.
    /// </summary>
    [DllImport(NavigationDll, EntryPoint = "EnumerateAllSurfacesAt", CallingConvention = CallingConvention.Cdecl)]
    public static extern int EnumerateAllSurfacesAt(
        uint mapId, float x, float y,
        [Out] float[] zValues, [Out] uint[] instanceIds, int maxResults);

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

    [DllImport(NavigationDll, EntryPoint = "EvaluateWoWCheckWalkable", CallingConvention = CallingConvention.Cdecl)]
    [return: MarshalAs(UnmanagedType.I1)]
    public static extern bool EvaluateWoWCheckWalkable(
        in Triangle triangle,
        in Vector3 contactNormal,
        in Vector3 position,
        float collisionRadius,
        float boundingHeight,
        [MarshalAs(UnmanagedType.I1)] bool useStandardWalkableThreshold,
        [MarshalAs(UnmanagedType.I1)] bool groundedWallFlagBefore,
        [MarshalAs(UnmanagedType.I1)] out bool walkableState,
        [MarshalAs(UnmanagedType.I1)] out bool groundedWallFlagAfter);

    [DllImport(NavigationDll, EntryPoint = "EvaluateTerrainAABBContactOrientation", CallingConvention = CallingConvention.Cdecl)]
    [return: MarshalAs(UnmanagedType.I1)]
    public static extern bool EvaluateTerrainAABBContactOrientation(
        in Triangle triangle,
        in Vector3 boxMin,
        in Vector3 boxMax,
        out Vector3 normal,
        out float planeDistance,
        [MarshalAs(UnmanagedType.I1)] out bool walkable);

    [DllImport(NavigationDll, EntryPoint = "EvaluateWoWSelectedContactThresholdGate", CallingConvention = CallingConvention.Cdecl)]
    [return: MarshalAs(UnmanagedType.I1)]
    public static extern bool EvaluateWoWSelectedContactThresholdGate(
        in Triangle triangle,
        in Vector3 contactNormal,
        in Vector3 currentPosition,
        in Vector3 projectedPosition,
        [MarshalAs(UnmanagedType.I1)] bool useStandardWalkableThreshold,
        [MarshalAs(UnmanagedType.I1)] out bool currentPositionInsidePrism,
        [MarshalAs(UnmanagedType.I1)] out bool projectedPositionInsidePrism,
        [MarshalAs(UnmanagedType.I1)] out bool thresholdSensitive,
        out float normalZ);

    [DllImport(NavigationDll, EntryPoint = "EvaluateWoWPointInsideAabbInclusive", CallingConvention = CallingConvention.Cdecl)]
    [return: MarshalAs(UnmanagedType.I1)]
    public static extern bool EvaluateWoWPointInsideAabbInclusive(
        in Vector3 boundsMin,
        in Vector3 boundsMax,
        in Vector3 point);

    [DllImport(NavigationDll, EntryPoint = "EvaluateWoWAabbOverlapInclusive", CallingConvention = CallingConvention.Cdecl)]
    [return: MarshalAs(UnmanagedType.I1)]
    public static extern bool EvaluateWoWAabbOverlapInclusive(
        in Vector3 boundsMinA,
        in Vector3 boundsMaxA,
        in Vector3 boundsMinB,
        in Vector3 boundsMaxB);

    [DllImport(NavigationDll, EntryPoint = "BuildWoWAabbOutcode", CallingConvention = CallingConvention.Cdecl)]
    public static extern uint BuildWoWAabbOutcode(
        in Vector3 point,
        in Vector3 boundsMin,
        in Vector3 boundsMax);

    [DllImport(NavigationDll, EntryPoint = "EvaluateWoWTriangleAabbOutcodeReject", CallingConvention = CallingConvention.Cdecl)]
    [return: MarshalAs(UnmanagedType.I1)]
    public static extern bool EvaluateWoWTriangleAabbOutcodeReject(
        uint firstOutcode,
        uint secondOutcode,
        uint thirdOutcode);

    [DllImport(NavigationDll, EntryPoint = "EvaluateWoWSelectorTrianglePlaneOutcodeReject", CallingConvention = CallingConvention.Cdecl)]
    [return: MarshalAs(UnmanagedType.I1)]
    public static extern bool EvaluateWoWSelectorTrianglePlaneOutcodeReject(
        uint firstOutcode,
        uint secondOutcode,
        uint thirdOutcode);

    [DllImport(NavigationDll, EntryPoint = "CountWoWTrianglesPassingAabbOutcodeReject", CallingConvention = CallingConvention.Cdecl)]
    public static extern uint CountWoWTrianglesPassingAabbOutcodeReject(
        [In] ushort[] triangleIndices,
        int triangleIndexCount,
        [In] uint[] vertexOutcodes,
        int vertexOutcodeCount);

    [DllImport(NavigationDll, EntryPoint = "EvaluateWoWTerrainQueryMask", CallingConvention = CallingConvention.Cdecl)]
    public static extern uint EvaluateWoWTerrainQueryMask(
        [MarshalAs(UnmanagedType.I1)] bool modelPropertyFlagSet,
        uint movementFlags,
        float field20Value,
        [MarshalAs(UnmanagedType.I1)] bool rootTreeFlagSet,
        [MarshalAs(UnmanagedType.I1)] bool childTreeFlagSet);

    [DllImport(NavigationDll, EntryPoint = "EvaluateWoWTerrainQueryPayloadEnabled", CallingConvention = CallingConvention.Cdecl)]
    [return: MarshalAs(UnmanagedType.I1)]
    public static extern bool EvaluateWoWTerrainQueryPayloadEnabled(
        uint movementFlags,
        in TerrainQueryPairPayload payload);

    [DllImport(NavigationDll, EntryPoint = "EvaluateWoWShouldRunDynamicCallbackProducer", CallingConvention = CallingConvention.Cdecl)]
    [return: MarshalAs(UnmanagedType.I1)]
    public static extern bool EvaluateWoWShouldRunDynamicCallbackProducer(
        [MarshalAs(UnmanagedType.I1)] bool callbackPresent,
        uint movementFlags);

    [DllImport(NavigationDll, EntryPoint = "EvaluateWoWShouldVisitTerrainQueryStampedEntry", CallingConvention = CallingConvention.Cdecl)]
    [return: MarshalAs(UnmanagedType.I1)]
    public static extern bool EvaluateWoWShouldVisitTerrainQueryStampedEntry(
        uint entryVisitStamp,
        uint currentVisitStamp);

    [DllImport(NavigationDll, EntryPoint = "BeginWoWTerrainQueryProducerPass", CallingConvention = CallingConvention.Cdecl)]
    public static extern uint BeginWoWTerrainQueryProducerPass(
        uint currentVisitStamp,
        int inputRecordCount,
        out uint nextVisitStamp);

    [DllImport(NavigationDll, EntryPoint = "BuildWoWTerrainQueryChunkSpan", CallingConvention = CallingConvention.Cdecl)]
    [return: MarshalAs(UnmanagedType.I1)]
    public static extern bool BuildWoWTerrainQueryChunkSpan(
        in Vector3 worldBoundsMin,
        in Vector3 worldBoundsMax,
        out TerrainQueryChunkSpan span);

    [DllImport(NavigationDll, EntryPoint = "EnumerateWoWTerrainQueryChunkCoordinates", CallingConvention = CallingConvention.Cdecl)]
    public static extern int EnumerateWoWTerrainQueryChunkCoordinates(
        in TerrainQueryChunkSpan span,
        [Out] TerrainQueryChunkCoordinate[] outCoordinates,
        int maxOutputCount);

    [DllImport(NavigationDll, EntryPoint = "BuildWoWOptionalSelectorChildDispatchMask", CallingConvention = CallingConvention.Cdecl)]
    public static extern uint BuildWoWOptionalSelectorChildDispatchMask(
        [In] uint[] childPresenceFlags,
        int childCount,
        uint movementFlags);

    [DllImport(NavigationDll, EntryPoint = "EvaluateWoWTerrainQueryEntryDispatch", CallingConvention = CallingConvention.Cdecl)]
    public static extern uint EvaluateWoWTerrainQueryEntryDispatch(
        [MarshalAs(UnmanagedType.I1)] bool entryFlagMaskedOut,
        [MarshalAs(UnmanagedType.I1)] bool alreadyVisited,
        [MarshalAs(UnmanagedType.I1)] bool hasSourceGeometry,
        uint movementFlags,
        in TerrainQueryPairPayload payload,
        [MarshalAs(UnmanagedType.I1)] bool traversalAllowsDispatch,
        in Vector3 entryBoundsMin,
        in Vector3 entryBoundsMax,
        in Vector3 queryBoundsMin,
        in Vector3 queryBoundsMax);

    [DllImport(NavigationDll, EntryPoint = "EvaluateWoWDynamicTerrainQueryEntryDispatch", CallingConvention = CallingConvention.Cdecl)]
    [return: MarshalAs(UnmanagedType.I1)]
    public static extern bool EvaluateWoWDynamicTerrainQueryEntryDispatch(
        [MarshalAs(UnmanagedType.I1)] bool entryFlagEnabled,
        [MarshalAs(UnmanagedType.I1)] bool alreadyVisited,
        [MarshalAs(UnmanagedType.I1)] bool callbackSucceeded,
        in Vector3 entryBoundsMin,
        in Vector3 entryBoundsMax,
        in Vector3 queryBoundsMin,
        in Vector3 queryBoundsMax);

    [DllImport(NavigationDll, EntryPoint = "BuildWoWTerrainQueryBounds", CallingConvention = CallingConvention.Cdecl)]
    [return: MarshalAs(UnmanagedType.I1)]
    public static extern bool BuildWoWTerrainQueryBounds(
        in Vector3 projectedPosition,
        float collisionRadius,
        float boundingHeight,
        out Vector3 boundsMin,
        out Vector3 boundsMax);

    [DllImport(NavigationDll, EntryPoint = "MergeWoWAabbBounds", CallingConvention = CallingConvention.Cdecl)]
    [return: MarshalAs(UnmanagedType.I1)]
    public static extern bool MergeWoWAabbBounds(
        in Vector3 boundsMinA,
        in Vector3 boundsMaxA,
        in Vector3 boundsMinB,
        in Vector3 boundsMaxB,
        out Vector3 boundsMin,
        out Vector3 boundsMax);

    [DllImport(NavigationDll, EntryPoint = "AddScalarToWoWVector3", CallingConvention = CallingConvention.Cdecl)]
    [return: MarshalAs(UnmanagedType.I1)]
    public static extern bool AddScalarToWoWVector3(ref Vector3 vector, float scalar);

    [DllImport(NavigationDll, EntryPoint = "SubtractScalarFromWoWVector3", CallingConvention = CallingConvention.Cdecl)]
    [return: MarshalAs(UnmanagedType.I1)]
    public static extern bool SubtractScalarFromWoWVector3(ref Vector3 vector, float scalar);

    [DllImport(NavigationDll, EntryPoint = "BuildWoWTerrainQueryCacheMissBounds", CallingConvention = CallingConvention.Cdecl)]
    [return: MarshalAs(UnmanagedType.I1)]
    public static extern bool BuildWoWTerrainQueryCacheMissBounds(
        in Vector3 projectedPosition,
        float collisionRadius,
        float boundingHeight,
        in Vector3 cachedBoundsMin,
        in Vector3 cachedBoundsMax,
        out Vector3 boundsMin,
        out Vector3 boundsMax);

    [DllImport(NavigationDll, EntryPoint = "EvaluateWoWTerrainQueryMergedQueryTransaction", CallingConvention = CallingConvention.Cdecl)]
    [return: MarshalAs(UnmanagedType.I1)]
    public static extern bool EvaluateWoWTerrainQueryMergedQueryTransaction(
        in Vector3 projectedPosition,
        float collisionRadius,
        float boundingHeight,
        in Vector3 cachedBoundsMin,
        in Vector3 cachedBoundsMax,
        [MarshalAs(UnmanagedType.I1)] bool modelPropertyFlagSet,
        uint movementFlags,
        float field20Value,
        [MarshalAs(UnmanagedType.I1)] bool rootTreeFlagSet,
        [MarshalAs(UnmanagedType.I1)] bool childTreeFlagSet,
        [MarshalAs(UnmanagedType.I1)] bool queryDispatchSucceeded,
        out TerrainQueryMergedQueryTrace trace);

    [DllImport(NavigationDll, EntryPoint = "EvaluateWoWTerrainQuerySelectedContactContainerTransaction", CallingConvention = CallingConvention.Cdecl)]
    public static extern int EvaluateWoWTerrainQuerySelectedContactContainerTransaction(
        in Vector3 projectedPosition,
        float collisionRadius,
        float boundingHeight,
        in Vector3 cachedBoundsMin,
        in Vector3 cachedBoundsMax,
        [MarshalAs(UnmanagedType.I1)] bool modelPropertyFlagSet,
        uint movementFlags,
        float field20Value,
        [MarshalAs(UnmanagedType.I1)] bool rootTreeFlagSet,
        [MarshalAs(UnmanagedType.I1)] bool childTreeFlagSet,
        [In] TerrainAabbContact[] existingContacts,
        [In] TerrainQueryPairPayload[] existingPairs,
        int existingCount,
        [In] TerrainAabbContact[] queryContacts,
        [In] TerrainQueryPairPayload[] queryPairs,
        int queryCount,
        [MarshalAs(UnmanagedType.I1)] bool queryDispatchSucceeded,
        [Out] TerrainAabbContact[] outContacts,
        [Out] TerrainQueryPairPayload[] outPairs,
        int maxOutputCount,
        out TerrainQuerySelectedContactContainerTrace trace);

    [DllImport(NavigationDll, EntryPoint = "BuildWoWNegatedPlane", CallingConvention = CallingConvention.Cdecl)]
    [return: MarshalAs(UnmanagedType.I1)]
    public static extern bool BuildWoWNegatedPlane(
        in Vector3 normal,
        float planeDistance,
        out SelectorSupportPlane plane);

    [DllImport(NavigationDll, EntryPoint = "BuildWoWPlaneFromNormalAndPoint", CallingConvention = CallingConvention.Cdecl)]
    [return: MarshalAs(UnmanagedType.I1)]
    public static extern bool BuildWoWPlaneFromNormalAndPoint(
        in Vector3 normal,
        in Vector3 point,
        out SelectorSupportPlane plane);

    [DllImport(NavigationDll, EntryPoint = "BuildWoWPlaneFromTrianglePoints", CallingConvention = CallingConvention.Cdecl)]
    [return: MarshalAs(UnmanagedType.I1)]
    public static extern bool BuildWoWPlaneFromTrianglePoints(
        in Vector3 point0,
        in Vector3 point1,
        in Vector3 point2,
        out SelectorSupportPlane plane);

    [DllImport(NavigationDll, EntryPoint = "TranslateWoWSelectorSourceGeometry", CallingConvention = CallingConvention.Cdecl)]
    [return: MarshalAs(UnmanagedType.I1)]
    public static extern bool TranslateWoWSelectorSourceGeometry(
        in Vector3 translation,
        [In, Out] SelectorSupportPlane[] ioPlanes,
        int planeCount,
        [In, Out] Vector3[] ioPoints,
        int pointCount,
        ref Vector3 ioAnchorPoint0,
        ref Vector3 ioAnchorPoint1);

    [DllImport(NavigationDll, EntryPoint = "BuildWoWSelectorHullSourceGeometry", CallingConvention = CallingConvention.Cdecl)]
    [return: MarshalAs(UnmanagedType.I1)]
    public static extern bool BuildWoWSelectorHullSourceGeometry(
        [In] Vector3[] supportPoints,
        int supportPointCount,
        [Out] SelectorSupportPlane[] outPlanes,
        int planeCount,
        [Out] Vector3[] outPoints,
        int outPointCount,
        out Vector3 outAnchorPoint0,
        out Vector3 outAnchorPoint1);

    [DllImport(NavigationDll, EntryPoint = "TransformWoWSelectorSupportPointBuffer", CallingConvention = CallingConvention.Cdecl)]
    [return: MarshalAs(UnmanagedType.I1)]
    public static extern bool TransformWoWSelectorSupportPointBuffer(
        [In] Vector3[] inputPoints,
        int pointCount,
        in Vector3 basisRow0,
        in Vector3 basisRow1,
        in Vector3 basisRow2,
        in Vector3 translation,
        [Out] Vector3[] outPoints,
        int outPointCount);

    [DllImport(NavigationDll, EntryPoint = "BuildWoWSelectorObjectCallbackMask", CallingConvention = CallingConvention.Cdecl)]
    public static extern uint BuildWoWSelectorObjectCallbackMask(
        uint movementFlags);

    [DllImport(NavigationDll, EntryPoint = "EvaluateWoWShouldResolveSelectorObjectNode", CallingConvention = CallingConvention.Cdecl)]
    [return: MarshalAs(UnmanagedType.I1)]
    public static extern bool EvaluateWoWShouldResolveSelectorObjectNode(
        [MarshalAs(UnmanagedType.I1)] bool selectorEnabled,
        [MarshalAs(UnmanagedType.I1)] bool nodeEnabled,
        [MarshalAs(UnmanagedType.I1)] bool allowInactiveNode);

    [DllImport(NavigationDll, EntryPoint = "ResolveWoWSelectorObjectNodePointer", CallingConvention = CallingConvention.Cdecl)]
    public static extern IntPtr ResolveWoWSelectorObjectNodePointer(
        [MarshalAs(UnmanagedType.I1)] bool selectorEnabled,
        IntPtr nodePointer,
        [MarshalAs(UnmanagedType.I1)] bool nodeEnabled,
        [MarshalAs(UnmanagedType.I1)] bool allowInactiveNode);

    [DllImport(NavigationDll, EntryPoint = "EvaluateWoWSelectorObjectRouterEntries", CallingConvention = CallingConvention.Cdecl)]
    public static extern uint EvaluateWoWSelectorObjectRouterEntries(
        [In] SelectorObjectRouterEntryRecord[] entries,
        int entryCount,
        [MarshalAs(UnmanagedType.I1)] bool selectorEnabled,
        in Vector3 queryBoundsMin,
        in Vector3 queryBoundsMax,
        out SelectorObjectRouterTrace trace);

    [DllImport(NavigationDll, EntryPoint = "EvaluateWoWShouldUseSelectorObjectCallback", CallingConvention = CallingConvention.Cdecl)]
    [return: MarshalAs(UnmanagedType.I1)]
    public static extern bool EvaluateWoWShouldUseSelectorObjectCallback(
        ulong callbackToken);

    [DllImport(NavigationDll, EntryPoint = "FinalizeWoWSelectorObjectNoCallbackState", CallingConvention = CallingConvention.Cdecl)]
    [return: MarshalAs(UnmanagedType.I1)]
    public static extern bool FinalizeWoWSelectorObjectNoCallbackState(
        uint inputHitResult,
        uint inputRecordCount,
        uint inputOutputFlags,
        out SelectorObjectNoCallbackState state);

    [DllImport(NavigationDll, EntryPoint = "EvaluateWoWSelectorLeafQueueMutation", CallingConvention = CallingConvention.Cdecl)]
    public static extern uint EvaluateWoWSelectorLeafQueueMutation(
        uint triangleIndex,
        uint stateMaskByte,
        [MarshalAs(UnmanagedType.I1)] bool predicateRejected,
        uint inputOverflowFlags,
        [In] ushort[] inputPendingIds,
        int pendingIdCapacity,
        uint inputPendingCount,
        [In] ushort[] inputAcceptedIds,
        int acceptedIdCapacity,
        uint inputAcceptedCount,
        [In] byte[] inputStateBytes,
        int stateByteCount,
        out uint outputOverflowFlags,
        [Out] ushort[] outputPendingIds,
        int outputPendingIdCapacity,
        out uint outputPendingCount,
        [Out] ushort[] outputAcceptedIds,
        int outputAcceptedIdCapacity,
        out uint outputAcceptedCount,
        [Out] byte[] outputStateBytes,
        int outputStateByteCount,
        out SelectorLeafQueueMutationTrace trace);

    [DllImport(NavigationDll, EntryPoint = "BuildWoWSelectorNodeTraversalPayload", CallingConvention = CallingConvention.Cdecl)]
    [return: MarshalAs(UnmanagedType.I1)]
    public static extern bool BuildWoWSelectorNodeTraversalPayload(
        in SelectorNodeTraversalRecord node,
        [In] Vector3[] querySupportPoints,
        int supportPointCount,
        uint callbackMask,
        out SelectorNodeTraversalPayload payload);

    [DllImport(NavigationDll, EntryPoint = "BuildWoWSelectorSupportPointBounds", CallingConvention = CallingConvention.Cdecl)]
    [return: MarshalAs(UnmanagedType.I1)]
    public static extern bool BuildWoWSelectorSupportPointBounds(
        [In] Vector3[] points,
        int pointCount,
        out Vector3 outBoundsMin,
        out Vector3 outBoundsMax);

    [DllImport(NavigationDll, EntryPoint = "BuildWoWSelectorDynamicObjectHullSourceGeometry", CallingConvention = CallingConvention.Cdecl)]
    [return: MarshalAs(UnmanagedType.I1)]
    public static extern bool BuildWoWSelectorDynamicObjectHullSourceGeometry(
        [In] SelectorSupportPlane[] sourcePlanes,
        int planeCount,
        in Vector3 objectBoundsMin,
        in Vector3 objectBoundsMax,
        [In] Vector3[] localSupportPoints,
        int supportPointCount,
        in Vector3 basisRow0,
        in Vector3 basisRow1,
        in Vector3 basisRow2,
        in Vector3 translation,
        [Out] SelectorSupportPlane[] outPlanes,
        int outPlaneCount,
        [Out] Vector3[] outPoints,
        int outPointCount,
        out Vector3 outAnchorPoint0,
        out Vector3 outAnchorPoint1);

    [DllImport(NavigationDll, EntryPoint = "BuildWoWSelectorBvhChildTraversal", CallingConvention = CallingConvention.Cdecl)]
    [return: MarshalAs(UnmanagedType.I1)]
    public static extern bool BuildWoWSelectorBvhChildTraversal(
        in SelectorBvhNodeRecord node,
        in Vector3 boundsMin,
        in Vector3 boundsMax,
        out SelectorBvhChildTraversal traversal);

    [DllImport(NavigationDll, EntryPoint = "BuildWoWSelectorSourcePlaneOutcode", CallingConvention = CallingConvention.Cdecl)]
    public static extern uint BuildWoWSelectorSourcePlaneOutcode(
        [In] SelectorSupportPlane[] planes,
        int planeCount,
        in Vector3 point);

    [DllImport(NavigationDll, EntryPoint = "EvaluateWoWSelectorSourceAabbCull", CallingConvention = CallingConvention.Cdecl)]
    public static extern uint EvaluateWoWSelectorSourceAabbCull(
        [In] SelectorSupportPlane[] planes,
        int planeCount,
        in Vector3 boundsMin,
        in Vector3 boundsMax);

    [DllImport(NavigationDll, EntryPoint = "EvaluateWoWSelectorHullTransformedBoundsCull", CallingConvention = CallingConvention.Cdecl)]
    public static extern uint EvaluateWoWSelectorHullTransformedBoundsCull(
        [In] SelectorSupportPlane[] planes,
        int planeCount,
        in Vector3 localBoundsMin,
        in Vector3 localBoundsMax,
        in Vector3 basisRow0,
        in Vector3 basisRow1,
        in Vector3 basisRow2,
        in Vector3 translation);

    [DllImport(NavigationDll, EntryPoint = "EvaluateWoWSelectorHullPointWithMargin", CallingConvention = CallingConvention.Cdecl)]
    public static extern uint EvaluateWoWSelectorHullPointWithMargin(
        [In] SelectorSupportPlane[] planes,
        int planeCount,
        in Vector3 point,
        float margin);

    [DllImport(NavigationDll, EntryPoint = "EvaluateWoWSelectorHullPointEpsilon", CallingConvention = CallingConvention.Cdecl)]
    public static extern uint EvaluateWoWSelectorHullPointEpsilon(
        [In] SelectorSupportPlane[] planes,
        int planeCount,
        in Vector3 point);

    [DllImport(NavigationDll, EntryPoint = "CountWoWSelectorSourceTrianglesPassingPlaneOutcodes", CallingConvention = CallingConvention.Cdecl)]
    public static extern uint CountWoWSelectorSourceTrianglesPassingPlaneOutcodes(
        [In] SelectorSupportPlane[] planes,
        int planeCount,
        [In] Vector3[] samplePoints,
        int samplePointCount);

    [DllImport(NavigationDll, EntryPoint = "BuildWoWSelectorSourceScanWindow", CallingConvention = CallingConvention.Cdecl)]
    [return: MarshalAs(UnmanagedType.I1)]
    public static extern bool BuildWoWSelectorSourceScanWindow(
        int cellRowIndex,
        int cellColumnIndex,
        int queryRowMin,
        int queryColumnMin,
        int queryRowMax,
        int queryColumnMax,
        out SelectorSourceScanWindow window);

    [DllImport(NavigationDll, EntryPoint = "BuildWoWObjectLocalQueryBounds", CallingConvention = CallingConvention.Cdecl)]
    [return: MarshalAs(UnmanagedType.I1)]
    public static extern bool BuildWoWObjectLocalQueryBounds(
        in Vector3 worldBoundsMin,
        in Vector3 worldBoundsMax,
        in Vector3 objectPosition,
        out Vector3 localBoundsMin,
        out Vector3 localBoundsMax);

    [DllImport(NavigationDll, EntryPoint = "BuildWoWLocalBoundsAabbOutcode", CallingConvention = CallingConvention.Cdecl)]
    public static extern uint BuildWoWLocalBoundsAabbOutcode(
        in Vector3 localBoundsMin,
        in Vector3 localBoundsMax,
        in Vector3 point);

    [DllImport(NavigationDll, EntryPoint = "EvaluateWoWTriangleLocalBoundsAabbReject", CallingConvention = CallingConvention.Cdecl)]
    [return: MarshalAs(UnmanagedType.I1)]
    public static extern bool EvaluateWoWTriangleLocalBoundsAabbReject(
        in Vector3 localBoundsMin,
        in Vector3 localBoundsMax,
        in Vector3 point0,
        in Vector3 point1,
        in Vector3 point2);

    [DllImport(NavigationDll, EntryPoint = "BuildWoWSelectorSourceScanWindowCandidateRecords", CallingConvention = CallingConvention.Cdecl)]
    public static extern int BuildWoWSelectorSourceScanWindowCandidateRecords(
        [In] SelectorSupportPlane[] planes,
        int planeCount,
        [In] Vector3[] pointGrid,
        int pointGridPointCount,
        in SelectorSourceScanWindow scanWindow,
        uint cellMaskFlags,
        in Vector3 translation,
        [MarshalAs(UnmanagedType.I1)] bool useApproximatePlaneBuildPath,
        [Out] SelectorCandidateRecord[] outRecords,
        int maxRecords);

    [DllImport(NavigationDll, EntryPoint = "BuildWoWLocalBoundsScanWindowCandidateRecords", CallingConvention = CallingConvention.Cdecl)]
    public static extern int BuildWoWLocalBoundsScanWindowCandidateRecords(
        in Vector3 localBoundsMin,
        in Vector3 localBoundsMax,
        [In] Vector3[] pointGrid,
        int pointGridPointCount,
        in SelectorSourceScanWindow scanWindow,
        uint cellMaskFlags,
        in Vector3 translation,
        [MarshalAs(UnmanagedType.I1)] bool useApproximatePlaneBuildPath,
        [Out] SelectorCandidateRecord[] outRecords,
        int maxRecords);

    [DllImport(NavigationDll, EntryPoint = "BuildWoWSelectorSourceSubcellMask", CallingConvention = CallingConvention.Cdecl)]
    public static extern uint BuildWoWSelectorSourceSubcellMask(
        uint rowIndex,
        uint columnIndex);

    [DllImport(NavigationDll, EntryPoint = "EvaluateWoWSelectorSourceSubcellMask", CallingConvention = CallingConvention.Cdecl)]
    [return: MarshalAs(UnmanagedType.I1)]
    public static extern bool EvaluateWoWSelectorSourceSubcellMask(
        uint rowIndex,
        uint columnIndex,
        uint cellMaskFlags);

    [DllImport(NavigationDll, EntryPoint = "BuildWoWSelectorSourceTriangleCandidateRecords", CallingConvention = CallingConvention.Cdecl)]
    public static extern int BuildWoWSelectorSourceTriangleCandidateRecords(
        [In] SelectorSupportPlane[] planes,
        int planeCount,
        [In] Vector3[] samplePoints,
        int samplePointCount,
        in Vector3 translation,
        [MarshalAs(UnmanagedType.I1)] bool useApproximatePlaneBuildPath,
        [Out] SelectorCandidateRecord[] outRecords,
        int maxRecords);

    [DllImport(NavigationDll, EntryPoint = "BuildWoWAabbBoundarySelectorCandidateRecords", CallingConvention = CallingConvention.Cdecl)]
    public static extern int BuildWoWAabbBoundarySelectorCandidateRecords(
        in Vector3 boundaryMin,
        in Vector3 boundaryMax,
        in Vector3 queryBoundsMin,
        in Vector3 queryBoundsMax,
        [Out] SelectorCandidateRecord[] outRecords,
        int maxRecords);

    [DllImport(NavigationDll, EntryPoint = "BuildWoWTransformedTriangleSelectorRecord", CallingConvention = CallingConvention.Cdecl)]
    [return: MarshalAs(UnmanagedType.I1)]
    public static extern bool BuildWoWTransformedTriangleSelectorRecord(
        [In] Vector3[] transformBasisRows,
        int transformBasisRowCount,
        in Vector3 localNormal,
        in Vector3 point0,
        in Vector3 point1,
        in Vector3 point2,
        out SelectorCandidateRecord outRecord);

    [DllImport(NavigationDll, EntryPoint = "TransformWoWWorldPointToTransportLocal", CallingConvention = CallingConvention.Cdecl)]
    [return: MarshalAs(UnmanagedType.I1)]
    public static extern bool TransformWoWWorldPointToTransportLocal(
        in Vector3 worldPoint,
        in Vector3 transportPosition,
        float transportOrientation,
        out Vector3 localPoint);

    [DllImport(NavigationDll, EntryPoint = "BuildWoWTransportLocalPlane", CallingConvention = CallingConvention.Cdecl)]
    [return: MarshalAs(UnmanagedType.I1)]
    public static extern bool BuildWoWTransportLocalPlane(
        in Vector3 worldNormal,
        in Vector3 worldPoint,
        in Vector3 transportPosition,
        float transportOrientation,
        out SelectorSupportPlane plane);

    [DllImport(NavigationDll, EntryPoint = "TransformWoWSelectorCandidateRecordToTransportLocal", CallingConvention = CallingConvention.Cdecl)]
    [return: MarshalAs(UnmanagedType.I1)]
    public static extern bool TransformWoWSelectorCandidateRecordToTransportLocal(
        in SelectorCandidateRecord worldRecord,
        in Vector3 transportPosition,
        float transportOrientation,
        out SelectorCandidateRecord localRecord);

    [DllImport(NavigationDll, EntryPoint = "TransformWoWSelectorCandidateRecordBufferToTransportLocal", CallingConvention = CallingConvention.Cdecl)]
    [return: MarshalAs(UnmanagedType.I1)]
    public static extern bool TransformWoWSelectorCandidateRecordBufferToTransportLocal(
        uint transportGuidLow,
        uint transportGuidHigh,
        in Vector3 transportPosition,
        float transportOrientation,
        [In, Out] SelectorCandidateRecord[] ioRecords,
        uint recordCount);

    [DllImport(NavigationDll, EntryPoint = "InitializeWoWSelectorSupportPlane", CallingConvention = CallingConvention.Cdecl)]
    [return: MarshalAs(UnmanagedType.I1)]
    public static extern bool InitializeWoWSelectorSupportPlane(out SelectorSupportPlane plane);

    [DllImport(NavigationDll, EntryPoint = "EvaluateWoWSelectorReportedBestRatioClamp", CallingConvention = CallingConvention.Cdecl)]
    public static extern float EvaluateWoWSelectorReportedBestRatioClamp(float bestRatio);

    [DllImport(NavigationDll, EntryPoint = "EvaluateWoWSelectorTriangleSourceWrapperGates", CallingConvention = CallingConvention.Cdecl)]
    [return: MarshalAs(UnmanagedType.I1)]
    public static extern bool EvaluateWoWSelectorTriangleSourceWrapperGates(
        [MarshalAs(UnmanagedType.I1)] bool hasOverridePosition,
        [MarshalAs(UnmanagedType.I1)] bool terrainQuerySucceeded,
        float inputBestRatio,
        out float reportedBestRatio);

    [DllImport(NavigationDll, EntryPoint = "InitializeWoWSelectorTriangleSourceWrapperSeeds", CallingConvention = CallingConvention.Cdecl)]
    [return: MarshalAs(UnmanagedType.I1)]
    public static extern bool InitializeWoWSelectorTriangleSourceWrapperSeeds(
        out Vector3 testPoint,
        out Vector3 candidateDirection,
        out float bestRatio);

    [DllImport(NavigationDll, EntryPoint = "EvaluateWoWSelectorTriangleSourceWrapperTransaction", CallingConvention = CallingConvention.Cdecl)]
    [return: MarshalAs(UnmanagedType.I1)]
    private static extern bool EvaluateWoWSelectorTriangleSourceWrapperTransactionNative(
        in Vector3 defaultPosition,
        IntPtr overridePosition,
        [MarshalAs(UnmanagedType.I1)] bool terrainQuerySucceeded,
        float inputBestRatio,
        out SelectorTriangleSourceWrapperTrace trace);

    public static bool EvaluateWoWSelectorTriangleSourceWrapperTransaction(
        in Vector3 defaultPosition,
        Vector3? overridePosition,
        bool terrainQuerySucceeded,
        float inputBestRatio,
        out SelectorTriangleSourceWrapperTrace trace)
    {
        IntPtr overridePositionPtr = IntPtr.Zero;
        try
        {
            if (overridePosition.HasValue)
            {
                overridePositionPtr = Marshal.AllocHGlobal(Marshal.SizeOf<Vector3>());
                Marshal.StructureToPtr(overridePosition.Value, overridePositionPtr, false);
            }

            return EvaluateWoWSelectorTriangleSourceWrapperTransactionNative(
                in defaultPosition,
                overridePositionPtr,
                terrainQuerySucceeded,
                inputBestRatio,
                out trace);
        }
        finally
        {
            if (overridePositionPtr != IntPtr.Zero)
            {
                Marshal.FreeHGlobal(overridePositionPtr);
            }
        }
    }

    [DllImport(NavigationDll, EntryPoint = "EvaluateWoWSelectorTriangleSourceVariableTransaction", CallingConvention = CallingConvention.Cdecl)]
    [return: MarshalAs(UnmanagedType.I1)]
    private static extern bool EvaluateWoWSelectorTriangleSourceVariableTransactionNative(
        in Vector3 defaultPosition,
        IntPtr overridePosition,
        in Vector3 projectedPosition,
        uint supportPlaneInitCount,
        uint validationPlaneInitCount,
        uint scratchPointZeroCount,
        in Vector3 testPoint,
        in Vector3 candidateDirection,
        float initialBestRatio,
        float collisionRadius,
        float boundingHeight,
        in Vector3 cachedBoundsMin,
        in Vector3 cachedBoundsMax,
        [MarshalAs(UnmanagedType.I1)] bool modelPropertyFlagSet,
        uint movementFlags,
        float field20Value,
        [MarshalAs(UnmanagedType.I1)] bool rootTreeFlagSet,
        [MarshalAs(UnmanagedType.I1)] bool childTreeFlagSet,
        [MarshalAs(UnmanagedType.I1)] bool queryDispatchSucceeded,
        [MarshalAs(UnmanagedType.I1)] bool rankingAccepted,
        uint rankingCandidateCount,
        int rankingSelectedRecordIndex,
        float rankingReportedBestRatio,
        out SelectorTriangleSourceVariableTransactionTrace trace);

    public static bool EvaluateWoWSelectorTriangleSourceVariableTransaction(
        in Vector3 defaultPosition,
        Vector3? overridePosition,
        in Vector3 projectedPosition,
        uint supportPlaneInitCount,
        uint validationPlaneInitCount,
        uint scratchPointZeroCount,
        in Vector3 testPoint,
        in Vector3 candidateDirection,
        float initialBestRatio,
        float collisionRadius,
        float boundingHeight,
        in Vector3 cachedBoundsMin,
        in Vector3 cachedBoundsMax,
        bool modelPropertyFlagSet,
        uint movementFlags,
        float field20Value,
        bool rootTreeFlagSet,
        bool childTreeFlagSet,
        bool queryDispatchSucceeded,
        bool rankingAccepted,
        uint rankingCandidateCount,
        int rankingSelectedRecordIndex,
        float rankingReportedBestRatio,
        out SelectorTriangleSourceVariableTransactionTrace trace)
    {
        IntPtr overridePositionPtr = IntPtr.Zero;
        try
        {
            if (overridePosition.HasValue)
            {
                overridePositionPtr = Marshal.AllocHGlobal(Marshal.SizeOf<Vector3>());
                Marshal.StructureToPtr(overridePosition.Value, overridePositionPtr, false);
            }

            return EvaluateWoWSelectorTriangleSourceVariableTransactionNative(
                in defaultPosition,
                overridePositionPtr,
                in projectedPosition,
                supportPlaneInitCount,
                validationPlaneInitCount,
                scratchPointZeroCount,
                in testPoint,
                in candidateDirection,
                initialBestRatio,
                collisionRadius,
                boundingHeight,
                in cachedBoundsMin,
                in cachedBoundsMax,
                modelPropertyFlagSet,
                movementFlags,
                field20Value,
                rootTreeFlagSet,
                childTreeFlagSet,
                queryDispatchSucceeded,
                rankingAccepted,
                rankingCandidateCount,
                rankingSelectedRecordIndex,
                rankingReportedBestRatio,
                out trace);
        }
        finally
        {
            if (overridePositionPtr != IntPtr.Zero)
            {
                Marshal.FreeHGlobal(overridePositionPtr);
            }
        }
    }

    [DllImport(NavigationDll, EntryPoint = "BuildWoWSelectorSupportPlanes", CallingConvention = CallingConvention.Cdecl)]
    public static extern int BuildWoWSelectorSupportPlanes(
        in Vector3 position,
        float verticalOffset,
        float horizontalRadius,
        [Out] SelectorSupportPlane[] outPlanes,
        int maxPlanes);

    [DllImport(NavigationDll, EntryPoint = "HasWoWSelectorCandidateWithUnitZ", CallingConvention = CallingConvention.Cdecl)]
    [return: MarshalAs(UnmanagedType.I1)]
    public static extern bool HasWoWSelectorCandidateWithUnitZ(
        [In] SelectorSupportPlane[] candidates,
        int candidateCount);

    [DllImport(NavigationDll, EntryPoint = "HasWoWSelectorCandidateWithNegativeDiagonalZ", CallingConvention = CallingConvention.Cdecl)]
    [return: MarshalAs(UnmanagedType.I1)]
    public static extern bool HasWoWSelectorCandidateWithNegativeDiagonalZ(
        [In] SelectorSupportPlane[] candidates,
        int candidateCount);

    [DllImport(NavigationDll, EntryPoint = "EvaluateWoWSelectorAlternateUnitZFallbackGate", CallingConvention = CallingConvention.Cdecl)]
    [return: MarshalAs(UnmanagedType.I1)]
    public static extern bool EvaluateWoWSelectorAlternateUnitZFallbackGate(
        float airborneTimeScalar,
        float elapsedTimeScalar,
        float horizontalSpeedScale,
        float requestedDistance);

    [DllImport(NavigationDll, EntryPoint = "EvaluateWoWJumpTimeScalar", CallingConvention = CallingConvention.Cdecl)]
    public static extern float EvaluateWoWJumpTimeScalar(
        uint movementFlags,
        float verticalSpeed);

    [DllImport(NavigationDll, EntryPoint = "EvaluateWoWSelectorPairFollowupGate", CallingConvention = CallingConvention.Cdecl)]
    [return: MarshalAs(UnmanagedType.I1)]
    public static extern bool EvaluateWoWSelectorPairFollowupGate(
        float windowStartScalar,
        float windowSpanScalar,
        in Vector3 moveVector,
        [MarshalAs(UnmanagedType.I1)] bool alternateUnitZState,
        uint movementFlags,
        float verticalSpeed,
        float horizontalSpeedScale);

    [DllImport(NavigationDll, EntryPoint = "EvaluateWoWSelectorContactWithinAlternateWorkingVectorBand", CallingConvention = CallingConvention.Cdecl)]
    [return: MarshalAs(UnmanagedType.I1)]
    public static extern bool EvaluateWoWSelectorContactWithinAlternateWorkingVectorBand(
        float normalZ);

    [DllImport(NavigationDll, EntryPoint = "EvaluateWoWSelectorAlternateWorkingVectorMode", CallingConvention = CallingConvention.Cdecl)]
    public static extern SelectorAlternateWorkingVectorMode EvaluateWoWSelectorAlternateWorkingVectorMode(
        float selectedNormalZ,
        uint candidateCount);

    [DllImport(NavigationDll, EntryPoint = "EvaluateWoWSelectorPlaneFootprintMismatch", CallingConvention = CallingConvention.Cdecl)]
    [return: MarshalAs(UnmanagedType.I1)]
    public static extern bool EvaluateWoWSelectorPlaneFootprintMismatch(
        in Vector3 position,
        float collisionRadius,
        in SelectorSupportPlane selectedPlane);

    [DllImport(NavigationDll, EntryPoint = "BuildWoWSelectorPlaneIntersectionPoint", CallingConvention = CallingConvention.Cdecl)]
    [return: MarshalAs(UnmanagedType.I1)]
    public static extern bool BuildWoWSelectorPlaneIntersectionPoint(
        in SelectorSupportPlane selectedPlane,
        in SelectorSupportPlane firstCandidatePlane,
        in SelectorSupportPlane secondCandidatePlane,
        out Vector3 outPoint);

    [DllImport(NavigationDll, EntryPoint = "BuildWoWSelectorTriangleEdgeDirection", CallingConvention = CallingConvention.Cdecl)]
    [return: MarshalAs(UnmanagedType.I1)]
    public static extern bool BuildWoWSelectorTriangleEdgeDirection(
        in SelectorCandidateRecord selectedRecord,
        in Vector3 intersectionPoint,
        in Vector3 lineDirection,
        out Vector3 outDirection,
        out SelectorTriangleEdgeDirectionTrace trace);

    [DllImport(NavigationDll, EntryPoint = "BuildWoWSelectorTwoCandidateWorkingVector", CallingConvention = CallingConvention.Cdecl)]
    [return: MarshalAs(UnmanagedType.I1)]
    public static extern bool BuildWoWSelectorTwoCandidateWorkingVector(
        in Vector3 position,
        float collisionRadius,
        in SelectorCandidateRecord selectedRecord,
        in SelectorSupportPlane firstCandidatePlane,
        in SelectorSupportPlane secondCandidatePlane,
        out Vector3 outVector,
        out SelectorTwoCandidateWorkingVectorTrace trace);

    [DllImport(NavigationDll, EntryPoint = "BuildWoWSelectorAlternatePair", CallingConvention = CallingConvention.Cdecl)]
    [return: MarshalAs(UnmanagedType.I1)]
    public static extern bool BuildWoWSelectorAlternatePair(
        in Vector3 position,
        float collisionRadius,
        in SelectorCandidateRecord selectedRecord,
        [In] SelectorSupportPlane[] candidatePlanes,
        uint candidateCount,
        in Vector3 inputMove,
        float windowStartScalar,
        float windowEndScalar,
        out SelectorPair outPair,
        out SelectorAlternatePairTrace trace);

    [DllImport(NavigationDll, EntryPoint = "EvaluateWoWVerticalTravelTimeScalar", CallingConvention = CallingConvention.Cdecl)]
    public static extern float EvaluateWoWVerticalTravelTimeScalar(
        float verticalDistance,
        [MarshalAs(UnmanagedType.I1)] bool preferEarlierPositiveRoot,
        uint movementFlags,
        float verticalSpeed);

    [DllImport(NavigationDll, EntryPoint = "EvaluateWoWSelectorPairWindowAdjustment", CallingConvention = CallingConvention.Cdecl)]
    public static extern float EvaluateWoWSelectorPairWindowAdjustment(
        float windowSpanScalar,
        float windowStartScalar,
        ref Vector3 moveVector,
        ref float outMoveMagnitude,
        [MarshalAs(UnmanagedType.I1)] bool alternateUnitZState,
        float horizontalReferenceMagnitude,
        uint movementFlags,
        float verticalSpeed,
        float horizontalSpeedScale,
        float referenceZ,
        float positionZ);

    [DllImport(NavigationDll, EntryPoint = "EvaluateWoWSelectorPairConsumer", CallingConvention = CallingConvention.Cdecl)]
    [return: MarshalAs(UnmanagedType.I1)]
    public static extern bool EvaluateWoWSelectorPairConsumer(
        float requestedDistance,
        in Vector3 inputMove,
        [MarshalAs(UnmanagedType.I1)] bool directionRankingAccepted,
        int selectedIndex,
        int selectedCount,
        [MarshalAs(UnmanagedType.I1)] bool directGateAccepted,
        [MarshalAs(UnmanagedType.I1)] bool hasNegativeDiagonalCandidate,
        [MarshalAs(UnmanagedType.I1)] bool alternateUnitZFallbackGateAccepted,
        [MarshalAs(UnmanagedType.I1)] bool hasUnitZCandidate,
        in SelectorPair directPair,
        in SelectorPair alternatePair,
        out SelectorPairConsumerTrace trace);

    [DllImport(NavigationDll, EntryPoint = "BuildWoWSelectorNeighborhood", CallingConvention = CallingConvention.Cdecl)]
    [return: MarshalAs(UnmanagedType.I1)]
    public static extern bool BuildWoWSelectorNeighborhood(
        in Vector3 position,
        float verticalOffset,
        float horizontalRadius,
        [Out] Vector3[] outPoints,
        int maxPoints,
        [Out] byte[] outSelectorIndices,
        int maxSelectorIndices);

    [DllImport(NavigationDll, EntryPoint = "EvaluateWoWSelectorPlaneRatio", CallingConvention = CallingConvention.Cdecl)]
    public static extern float EvaluateWoWSelectorPlaneRatio(
        in Vector3 candidatePoint,
        in SelectorSupportPlane plane,
        in Vector3 testPoint);

    [DllImport(NavigationDll, EntryPoint = "ClipWoWSelectorPointStripAgainstPlane", CallingConvention = CallingConvention.Cdecl)]
    [return: MarshalAs(UnmanagedType.I1)]
    public static extern bool ClipWoWSelectorPointStripAgainstPlane(
        in SelectorSupportPlane plane,
        uint clipPlaneIndex,
        [In, Out] Vector3[] ioPoints,
        [In, Out] uint[] ioSourceIndices,
        int maxCapacity,
        ref int ioCount);

    [DllImport(NavigationDll, EntryPoint = "ClipWoWSelectorPointStripAgainstPlanePrefix", CallingConvention = CallingConvention.Cdecl)]
    [return: MarshalAs(UnmanagedType.I1)]
    public static extern bool ClipWoWSelectorPointStripAgainstPlanePrefix(
        [In] SelectorSupportPlane[] planes,
        int planeCount,
        [In, Out] Vector3[] ioPoints,
        [In, Out] uint[] ioSourceIndices,
        int maxCapacity,
        ref int ioCount);

    [DllImport(NavigationDll, EntryPoint = "EvaluateWoWSelectorCandidateValidation", CallingConvention = CallingConvention.Cdecl)]
    [return: MarshalAs(UnmanagedType.I1)]
    public static extern bool EvaluateWoWSelectorCandidateValidation(
        [In] SelectorSupportPlane[] planes,
        int planeCount,
        int planeIndex,
        in Vector3 testPoint,
        [In, Out] Vector3[] ioPoints,
        [In, Out] uint[] ioSourceIndices,
        int maxCapacity,
        ref int ioCount,
        ref float inOutBestRatio,
        out SelectorCandidateValidationTrace trace);

    [DllImport(NavigationDll, EntryPoint = "EvaluateWoWSelectorCandidateRecordSet", CallingConvention = CallingConvention.Cdecl)]
    [return: MarshalAs(UnmanagedType.I1)]
    public static extern bool EvaluateWoWSelectorCandidateRecordSet(
        [In] SelectorCandidateRecord[] records,
        int recordCount,
        in Vector3 testPoint,
        [In] SelectorSupportPlane[] clipPlanes,
        int clipPlaneCount,
        [In] SelectorSupportPlane[] validationPlanes,
        int validationPlaneCount,
        int validationPlaneIndex,
        ref float inOutBestRatio,
        ref int inOutBestRecordIndex,
        out SelectorRecordEvaluationTrace trace);

    [DllImport(NavigationDll, EntryPoint = "EvaluateWoWSelectorTriangleSourceRanking", CallingConvention = CallingConvention.Cdecl)]
    [return: MarshalAs(UnmanagedType.I1)]
    public static extern bool EvaluateWoWSelectorTriangleSourceRanking(
        [In] SelectorCandidateRecord[] records,
        int recordCount,
        in Vector3 testPoint,
        in Vector3 candidateDirection,
        [In] Vector3[] points,
        int pointCount,
        [In] SelectorSupportPlane[] supportPlanes,
        int planeCount,
        [In] byte[] selectorIndices,
        int selectorIndexCount,
        [In, Out] SelectorSupportPlane[] ioBestCandidates,
        int maxBestCandidates,
        ref int ioCandidateCount,
        ref float ioBestRatio,
        out SelectorSourceRankingTrace trace);

    [DllImport(NavigationDll, EntryPoint = "EvaluateWoWSelectorDirectionRanking", CallingConvention = CallingConvention.Cdecl)]
    [return: MarshalAs(UnmanagedType.I1)]
    public static extern bool EvaluateWoWSelectorDirectionRanking(
        [In] SelectorCandidateRecord[] records,
        int recordCount,
        in Vector3 testPoint,
        in Vector3 candidateDirection,
        [In] Vector3[] points,
        int pointCount,
        [In] SelectorSupportPlane[] supportPlanes,
        int planeCount,
        [In] byte[] selectorIndices,
        int selectorIndexCount,
        [In, Out] SelectorSupportPlane[] ioBestCandidates,
        int maxBestCandidates,
        ref int ioCandidateCount,
        ref float ioBestRatio,
        ref float outReportedBestRatio,
        ref int ioBestRecordIndex,
        out SelectorDirectionRankingTrace trace);

    [DllImport(NavigationDll, EntryPoint = "BuildWoWSelectorCandidatePlaneRecord", CallingConvention = CallingConvention.Cdecl)]
    [return: MarshalAs(UnmanagedType.I1)]
    public static extern bool BuildWoWSelectorCandidatePlaneRecord(
        [In] Vector3[] points,
        int pointCount,
        [In] byte[] selectorIndices,
        int selectorIndexCount,
        in Vector3 translation,
        in SelectorSupportPlane sourcePlane,
        [Out] SelectorSupportPlane[] outPlanes,
        int maxPlanes);

    [DllImport(NavigationDll, EntryPoint = "BuildWoWSelectorCandidateQuadPlaneRecord", CallingConvention = CallingConvention.Cdecl)]
    [return: MarshalAs(UnmanagedType.I1)]
    public static extern bool BuildWoWSelectorCandidateQuadPlaneRecord(
        [In] Vector3[] points,
        int pointCount,
        [In] byte[] selectorIndices,
        int selectorIndexCount,
        in Vector3 translation,
        in SelectorSupportPlane sourcePlane,
        [Out] SelectorSupportPlane[] outPlanes,
        int maxPlanes);

    [DllImport(NavigationDll, EntryPoint = "QueryTerrainAABBContacts", CallingConvention = CallingConvention.Cdecl)]
    public static extern int QueryTerrainAABBContacts(
        uint mapId,
        in Vector3 boxMin,
        in Vector3 boxMax,
        [Out] TerrainAabbContact[] contacts,
        int maxContacts);

    [DllImport(NavigationDll, EntryPoint = "CopyWoWTerrainQueryWalkableContactsAndPairs", CallingConvention = CallingConvention.Cdecl)]
    public static extern int CopyWoWTerrainQueryWalkableContactsAndPairs(
        [In] TerrainAabbContact[] inputContacts,
        [In] TerrainQueryPairPayload[] inputPairs,
        int inputCount,
        [Out] TerrainAabbContact[] outputContacts,
        [Out] TerrainQueryPairPayload[] outputPairs,
        int maxOutputCount);

    [DllImport(NavigationDll, EntryPoint = "AppendWoWTerrainQueryPairPayloadRange", CallingConvention = CallingConvention.Cdecl)]
    public static extern int AppendWoWTerrainQueryPairPayloadRange(
        [In] TerrainQueryPairPayload[] inputPairs,
        int inputPairCount,
        uint previousRecordCount,
        uint currentRecordCount,
        in TerrainQueryPairPayload payload,
        [Out] TerrainQueryPairPayload[] outputPairs,
        int maxOutputCount);

    [DllImport(NavigationDll, EntryPoint = "ZeroWoWTerrainQueryPairPayloadRange", CallingConvention = CallingConvention.Cdecl)]
    public static extern int ZeroWoWTerrainQueryPairPayloadRange(
        [In] TerrainQueryPairPayload[] inputPairs,
        int inputPairCount,
        uint previousRecordCount,
        uint currentRecordCount,
        [Out] TerrainQueryPairPayload[] outputPairs,
        int maxOutputCount);

    [DllImport(NavigationDll, EntryPoint = "EvaluateGroundedWallSelection", CallingConvention = CallingConvention.Cdecl)]
    [return: MarshalAs(UnmanagedType.I1)]
    public static extern bool EvaluateGroundedWallSelection(
        uint mapId,
        in Vector3 boxMin,
        in Vector3 boxMax,
        in Vector3 currentPosition,
        in Vector3 requestedMove,
        float collisionRadius,
        float boundingHeight,
        [MarshalAs(UnmanagedType.I1)] bool groundedWallFlagBefore,
        out GroundedWallSelectionTrace trace);

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

    [DllImport(NavigationDll, EntryPoint = "InjectSceneTriangles", CallingConvention = CallingConvention.Cdecl)]
    [return: MarshalAs(UnmanagedType.I1)]
    public static extern bool InjectSceneTriangles(
        uint mapId,
        float minX,
        float minY,
        float maxX,
        float maxY,
        [In] InjectedTriangle[] triangles,
        int triangleCount);

    [DllImport(NavigationDll, EntryPoint = "ClearSceneCache", CallingConvention = CallingConvention.Cdecl)]
    public static extern void ClearSceneCache(uint mapId);

    // ==========================================================================
    // DYNAMIC OBJECT REGISTRY (elevators, doors, chests)
    // ==========================================================================

    /// <summary>
    /// Loads the displayId→model mapping from the vmaps directory.
    /// Must be called once before RegisterDynamicObject.
    /// </summary>
    [DllImport(NavigationDll, EntryPoint = "LoadDynamicObjectMapping", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    [return: MarshalAs(UnmanagedType.I1)]
    public static extern bool LoadDynamicObjectMapping(string vmapsBasePath);

    /// <summary>
    /// Registers a dynamic object by displayId. Loads the real .vmo model mesh.
    /// Returns true if the model was found and registered successfully.
    /// </summary>
    [DllImport(NavigationDll, EntryPoint = "RegisterDynamicObject", CallingConvention = CallingConvention.Cdecl)]
    [return: MarshalAs(UnmanagedType.I1)]
    public static extern bool RegisterDynamicObject(
        ulong guid, uint entry, uint displayId,
        uint mapId, float scale);

    /// <summary>
    /// Updates the world position and orientation of a dynamic object.
    /// Rebuilds world-space collision triangles from the cached model mesh.
    /// </summary>
    [DllImport(NavigationDll, EntryPoint = "UpdateDynamicObjectPosition", CallingConvention = CallingConvention.Cdecl)]
    public static extern void UpdateDynamicObjectPosition(
        ulong guid, float x, float y, float z, float orientation, uint goState);

    /// <summary>
    /// Removes a single dynamic object by GUID.
    /// </summary>
    [DllImport(NavigationDll, EntryPoint = "UnregisterDynamicObject", CallingConvention = CallingConvention.Cdecl)]
    public static extern void UnregisterDynamicObject(ulong guid);

    /// <summary>
    /// Removes all dynamic objects on a given map.
    /// </summary>
    [DllImport(NavigationDll, EntryPoint = "ClearDynamicObjects", CallingConvention = CallingConvention.Cdecl)]
    public static extern void ClearDynamicObjects(uint mapId);

    /// <summary>
    /// Removes all dynamic objects (keeps model cache).
    /// </summary>
    [DllImport(NavigationDll, EntryPoint = "ClearAllDynamicObjects", CallingConvention = CallingConvention.Cdecl)]
    public static extern void ClearAllDynamicObjects();

    /// <summary>
    /// Returns number of active dynamic objects.
    /// </summary>
    [DllImport(NavigationDll, EntryPoint = "GetDynamicObjectCount", CallingConvention = CallingConvention.Cdecl)]
    public static extern int GetDynamicObjectCount();

    /// <summary>
    /// Returns number of cached model meshes.
    /// </summary>
    [DllImport(NavigationDll, EntryPoint = "GetCachedModelCount", CallingConvention = CallingConvention.Cdecl)]
    public static extern int GetCachedModelCount();

    // ==========================================================================
    // SCENE CACHE (pre-processed collision geometry)
    // ==========================================================================

    /// <summary>
    /// Extracts collision geometry for a map and saves to a .scene file.
    /// Pass 0 for all bounds to extract the entire map.
    /// Requires VMAP + MapLoader to be initialized (slow, one-time).
    /// </summary>
    [DllImport(NavigationDll, EntryPoint = "ExtractSceneCache", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    [return: MarshalAs(UnmanagedType.I1)]
    public static extern bool ExtractSceneCache(
        uint mapId, string outPath,
        float minX, float minY, float maxX, float maxY);

    /// <summary>
    /// Loads a pre-cached .scene file (fast, ~10ms).
    /// </summary>
    [DllImport(NavigationDll, EntryPoint = "LoadSceneCache", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    [return: MarshalAs(UnmanagedType.I1)]
    public static extern bool LoadSceneCache(uint mapId, string path);

    /// <summary>
    /// Checks if a map has a loaded scene cache.
    /// </summary>
    [DllImport(NavigationDll, EntryPoint = "HasSceneCache", CallingConvention = CallingConvention.Cdecl)]
    [return: MarshalAs(UnmanagedType.I1)]
    public static extern bool HasSceneCache(uint mapId);

    /// <summary>
    /// Unloads the scene cache for a map.
    /// </summary>
    [DllImport(NavigationDll, EntryPoint = "UnloadSceneCache", CallingConvention = CallingConvention.Cdecl)]
    public static extern void UnloadSceneCache(uint mapId);

    /// <summary>
    /// Enables the thin scene-slice runtime so collision queries stay on explicitly
    /// injected nearby geometry instead of auto-loading full-map data on misses.
    /// </summary>
    [DllImport(NavigationDll, EntryPoint = "SetSceneSliceMode", CallingConvention = CallingConvention.Cdecl)]
    public static extern void SetSceneSliceMode([MarshalAs(UnmanagedType.I1)] bool enabled);

    /// <summary>
    /// Sets the scenes directory for auto-discovery during EnsureMapLoaded.
    /// </summary>
    [DllImport(NavigationDll, EntryPoint = "SetScenesDir", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    public static extern void SetScenesDir(string dir);

    // ==========================================================================
    // WMO DOODAD EXTRACTION
    // ==========================================================================

    /// <summary>
    /// Extract WMO doodad placement data from MPQ archives.
    /// Reads raw .wmo files from MPQ, writes .doodads files to vmaps directory.
    /// Returns number of .doodads files written, or -1 on error.
    /// </summary>
    [DllImport(NavigationDll, EntryPoint = "ExtractWmoDoodads", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    public static extern int ExtractWmoDoodads(string mpqDataDir, string vmapsDir);
}
