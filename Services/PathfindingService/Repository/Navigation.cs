using GameData.Core.Models;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;

namespace PathfindingService.Repository
{
    public readonly record struct NavigationPathResult(
        XYZ[] Path,
        XYZ[] RawPath,
        string Result,
        int? BlockedSegmentIndex,
        string BlockedReason = "none");

    public enum NativePathResolutionKind
    {
        Native,
        Corridor,
        CorridorFallback,
        CorridorFirst,
        CorridorFirstExpanded,
        SmoothFallbackAfterStraightStaticBreak
    }

    public readonly record struct NativePathResolution(
        XYZ[] Path,
        int? BlockedSegmentIndex = null,
        string BlockedReason = "none",
        bool WasRepairedAroundBlockedSegment = false,
        NativePathResolutionKind Kind = NativePathResolutionKind.Native)
    {
        public static NativePathResolution FromPath(
            XYZ[] path,
            NativePathResolutionKind kind = NativePathResolutionKind.Native)
            => new(path, null, "none", false, kind);
    }

    public enum NativeSegmentAffordance : uint
    {
        Walk = 0,
        StepUp = 1,
        SteepClimb = 2,
        Drop = 3,
        Cliff = 4,
        Vertical = 5,
        JumpGap = 6,
        SafeDrop = 7,
        UnsafeDrop = 8,
        Blocked = 9,
    }

    public readonly record struct NativeSegmentAffordanceResult(
        NativeSegmentAffordance Affordance,
        uint ValidationCode,
        float ClimbHeight,
        float GapDistance,
        float DropHeight,
        float SlopeAngleDeg,
        float ResolvedEndZ);

    public class Navigation
    {
        private const string DLL_NAME = "Navigation";
        private static readonly float[] RepairOffsetDistances = [2f, 4f, 6f, 8f, 10f, 12f];
        private static readonly float[] RepairAlongSegmentSamples = [0.05f, 0.12f, 0.35f, 0.5f, 0.65f];
        private static readonly TimeSpan DynamicOverlayRepairBudget = TimeSpan.FromSeconds(2);
        private static readonly TimeSpan EarlyStaticRepairBudget = TimeSpan.FromSeconds(5);
        private static readonly TimeSpan StaticRoutePackRepairBudget = TimeSpan.FromSeconds(15);
        private static readonly TimeSpan PostAffordanceStaticRepairBudget = TimeSpan.FromSeconds(3);
        private static readonly TimeSpan OverlayStraightStaticRepairBudget = TimeSpan.FromMilliseconds(750);
        private const float CombineWaypointEpsilon = 0.25f;
        private const float LongSegmentLosRepairThreshold = 35f;
        private const float LongSegmentDensifySpacing = 24f;
        private const float CorridorFirstPathMinDistance = 80f;
        private const float CorridorFirstPathMaxDistance = 450f;
        private const float CorridorFirstHighVerticalPathMaxDistance = 650f;
        private const float CorridorFirstHighVerticalMinDelta = 50f;
        private const int BoundedCorridorStaticRepairScanLimit = 96;
        private const int BoundedCorridorLocalPhysicsLayerProjectionScanLimit = 32;
        private const int BoundedCorridorLocalPhysicsRepairScanLimit = 48;
        private const int BoundedCorridorFloatingSupportProjectionLimit = 32;
        private const float FloatingSupportProjectionMinDrop = 1.0f;
        private const float FloatingSupportProjectionMaxDrop = 6.0f;
        private const float NativePathEndpointMaxDistance = 8.0f;
        private const float SmoothCorridorExpansionMinSegmentLength = 6f;
        private const float SmoothFallbackAfterStraightStaticBreakMinDistance = 50f;
        private const float SmoothPathDensifySpacing = 6f;
        private const int EarlyStaticRepairScanLimit = 256;
        private const int PostAffordanceStaticRepairScanLimit = 32;
        private const int OverlayStraightStaticRepairScanLimit = 32;
        private const float EarlyStaticRepairMinSegmentLength = 0.75f;
        private const float EarlyStaticRepairLosMinSegmentLength = 2.5f;
        private const float EarlyStaticRepairValidationMaxLength = 8f;
        private const int EarlySupportNormalizationLimit = 24;
        private const float EarlySupportProjectionThreshold = 0.75f;
        private const float EarlySupportDuplicateProjectionThreshold = 0.10f;
        private const float EarlySupportDuplicateHorizontalDistance = 0.35f;
        private const float EarlySupportProjectionMaxDelta = 4.0f;
        private const float EarlySupportGroundClearance = 0.05f;
        private const float EarlySupportProjectionReachabilityMaxRouteDistance = 500.0f;
        private const float EarlySupportProjectionReachableMaxEndpointDistance = 0.90f;
        private const float EarlySupportProjectionReachableMaxZDelta = 0.75f;
        private const float ShortVerticalLayerSpikeMinDelta = 2.5f;
        private const float ShortVerticalLayerSpikeNeighborZTolerance = 1.5f;
        private const float ShortVerticalLayerSpikeMaxLegLength = 5.0f;
        private const float ShortVerticalLayerSpikeMaxBridgeLength = 8.0f;
        private const float ShortHorizontalDetourSpikeMaxLegLength = 3.0f;
        private const float ShortHorizontalDetourSpikeMaxBridgeLength = 4.5f;
        private const float ShortHorizontalDetourSpikeMaxBridgeZDelta = 1.5f;
        private const float ShortHorizontalDetourSpikeMinDetourRatio = 1.8f;
        private const int LargeVerticalLayerExcursionMaxInteriorWaypoints = 12;
        private const float LargeVerticalLayerExcursionMinDelta = 12.0f;
        private const float LargeVerticalLayerExcursionMaxBridgeLength = 12.0f;
        private const float LargeVerticalLayerExcursionMaxEndpointZDelta = 8.0f;
        private const int LocalStaticRepairPointCandidateLimit = 12;
        private const int LocalStaticRepairRouteCandidateLimit = 16;
        private const int LocalStaticRepairRouteAnchorScanLimit = 12;
        private const int NativeSegmentRepairTailSegmentLimit = 8;
        private const float NativeSegmentRepairMaxHorizontalDistance = 18.0f;
        private static readonly float[] LongSegmentRepairForwardDistances = [8f, 14f, 22f];
        private static readonly float[] LongSegmentRepairLateralOffsets = [8f, -8f, 14f, -14f, 22f, -22f];
        private static readonly float[] LocalStaticRepairAlongSamples = [0.35f, 0.5f, 0.65f];
        private static readonly float[] LocalStaticRepairBaseOffsets = [1.25f, 2f, 3.5f, 5f, 8f, 12f, 16f];
        private static readonly float[] LocalStaticRepairEscapeDistances = [1.75f, 2.5f, 4f, 6f, 8f];
        private static readonly TimeSpan LongSegmentRepairBudget = TimeSpan.FromSeconds(2);
        private static readonly TimeSpan AffordanceRepairBudget = TimeSpan.FromSeconds(8);
        private static readonly TimeSpan AffordanceRepairNativeLegDirectSearchBudget = TimeSpan.FromSeconds(5);
        private static readonly float[] AffordanceRepairAlongSamples = [0.25f, 0.35f, 0.5f, 0.65f, 0.75f];
        private static readonly float[] AffordanceRepairLateralOffsets = [6f, -6f, 8f, -8f, 10f, -10f, 12f, -12f, 16f, -16f, 20f, -20f, 24f, -24f];
        private const float AffordanceRepairLookBehindDistance = 18f;
        private const float AffordanceRepairLookAheadDistance = 18f;
        private const float AffordanceRepairLocalLookBehindDistance = 6f;
        private const float AffordanceRepairLocalLookAheadDistance = 8f;
        private const float AffordanceRepairCumulativeLookBehindDistance = AffordanceRepairLookBehindDistance;
        private const float AffordanceRepairCumulativeLookAheadDistance = AffordanceRepairLookAheadDistance;
        private const float AffordanceRepairMinSegmentLength = 0.5f;
        private const float AffordanceRepairMinWindowLength = 4f;
        private const float AffordanceRepairMinInteriorSeparation = 4f;
        private const float AffordanceRepairMinCandidateRise = 0.75f;
        private const float AffordanceRepairMinCandidateSlopeRatio = 0.35f;
        private const int AffordanceRepairCumulativeMaxSegments = 6;
        private const float AffordanceRepairCumulativeMinRise = 3.0f;
        private const float AffordanceRepairCumulativeMinHorizontal = 3.0f;
        private const float AffordanceRepairCumulativeMaxHorizontal = 12.0f;
        private const float AffordanceRepairCumulativeMinSlopeRatio = 0.65f;
        private const float AffordanceRepairNativeLegMaxWindowLength = 64.0f;
        private const int AffordanceRepairNativeLegEndpointTrimLimit = 4;
        private const int AffordanceRepairCumulativeSmoothScanLimit = 384;
        private const int AffordanceRepairStraightScanLimit = 160;
        private const int AffordanceRepairMaxRepairs = 8;
        private const int LocalPhysicsLayerProjectionSmoothScanLimit = 384;
        private const int LocalPhysicsLayerProjectionMaxPasses = 2;
        private const float LocalPhysicsLayerProjectionMinSegmentLength = 0.75f;
        private const float LocalPhysicsLayerProjectionMaxSegmentLength = 8.0f;
        private const float LocalPhysicsLayerProjectionMinSpikeRise = 0.50f;
        private const float LocalPhysicsLayerProjectionMinDownwardZDelta = 0.45f;
        private const float LocalPhysicsLayerProjectionMinAppliedZDelta = 0.35f;
        private const float LocalPhysicsLayerProjectionMaxDownwardZDelta = 4.0f;
        private const float LocalPhysicsLayerProjectionSupportSnapTolerance = 0.25f;
        private const float LocalPhysicsLayerProjectionMaxEndpointDistance = 0.90f;
        private const float LocalPhysicsSimulationMaxDistance = 12.0f;
        private const float LocalPhysicsSimulationDeltaTime = 0.05f;
        private const float LocalPhysicsRunSpeed = 7.0f;
        private const float LocalPhysicsRouteLayerRejectZDelta = 5.0f;
        private const float LocalPhysicsRouteLateralRejectDistance = 3.0f;
        private const float LocalPhysicsBlockingWallProgressThreshold = 0.75f;
        private const float LocalPhysicsLowDisplacementRatio = 0.50f;
        private const float LocalPhysicsMovementStallAverageRatio = 0.45f;
        private const int LocalPhysicsMovementStallMinWallSteps = 4;
        private const int LocalPhysicsMovementStallMinLowDisplacementWallSteps = 4;
        private const int LocalPhysicsMovementStallMinConsecutiveLowProgressSteps = 3;
        private const float LocalPhysicsMovementStallProgressEpsilon = 0.01f;
        private static readonly TimeSpan LocalPhysicsReachabilityRepairBudget = TimeSpan.FromSeconds(3);
        private const int LocalPhysicsReachabilityRepairMaxRepairs = 8;
        private const float LocalPhysicsReachabilityRepairMaxDistance = 8.0f;
        private static readonly float[] LocalPhysicsReachabilityBridgeSamples = [0.35f, 0.5f, 0.65f];
        private static readonly float[] LocalPhysicsReachabilityBridgeLateralOffsets = [0.75f, -0.75f, 1.5f, -1.5f, 2.5f, -2.5f, 3.5f, -3.5f];
        private static readonly float[] LocalPhysicsReachabilityRepairDistances = [2.0f, 4.0f, 6.0f, 8.0f];
        private static readonly float[] LocalPhysicsReachabilityProgressDistances = [0.75f, 1.25f, 1.75f, 2.5f, 3.5f];
        private const float LocalPhysicsReachabilityProgressMinRise = 0.30f;
        private const float LocalPhysicsReachabilityProgressMaxOvershoot = 0.35f;
        private const float LocalPhysicsReachabilityProgressMaxLateralDeviation = 4.0f;
        private const float LocalPhysicsReachabilityProgressMaxBacktrack = 0.75f;
        private const float LocalPhysicsReachabilityRouteProgressMinAdvance = 0.75f;
        private const float LocalPhysicsReachabilityRouteProgressMinImprovement = 0.50f;
        private const float LocalPhysicsReachabilityRouteProgressMaxOvershoot = 1.50f;
        private const float LocalPhysicsReachabilityRouteProgressMaxZDelta = 2.0f;
        private const uint MoveFlagForward = 0x00000001;
        private const uint MoveFlagJumping = 0x00002000;
        private const uint MoveFlagFallingFar = 0x00004000;

        [StructLayout(LayoutKind.Sequential)]
        private struct NativeXyz
        {
            public float X;
            public float Y;
            public float Z;

            public NativeXyz(XYZ xyz)
            {
                X = xyz.X;
                Y = xyz.Y;
                Z = xyz.Z;
            }

            public XYZ ToManaged() => new(X, Y, Z);
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct NativePhysicsInput
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
        private struct NativePhysicsOutput
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
            public uint EnvironmentFlags;
        }

        private readonly record struct LocalPhysicsSimulation(
            bool Available,
            bool Compatible,
            float MaxUpwardRouteZDelta,
            float MaxAbsoluteRouteZDelta,
            float MaxLateralDistance,
            float AverageDisplacementRatio,
            int WallContactSteps,
            int LowDisplacementWallSteps,
            int MaxConsecutiveLowProgressSteps,
            XYZ FinalPosition,
            string Reason);

        // ── Legacy P/Invoke (used by DiagnosticFindPath, CalculatePath fallback) ──

        [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr FindPath(
            uint mapId,
            NativeXyz start,
            NativeXyz end,
            [MarshalAs(UnmanagedType.I1)] bool smoothPath,
            out int length);

        [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr FindPathForAgent(
            uint mapId,
            NativeXyz start,
            NativeXyz end,
            [MarshalAs(UnmanagedType.I1)] bool smoothPath,
            float agentRadius,
            float agentHeight,
            out int length);

        [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl)]
        [return: MarshalAs(UnmanagedType.I1)]
        private static extern bool LineOfSight(uint mapId, NativeXyz from, NativeXyz to);

        [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl)]
        private static extern void PathArrFree(IntPtr pathArr);

        [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl, EntryPoint = "PreloadMap")]
        private static extern void PreloadMapNative(uint mapId);

        [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl, EntryPoint = "SegmentIntersectsDynamicObjects")]
        [return: MarshalAs(UnmanagedType.I1)]
        private static extern bool SegmentIntersectsDynamicObjectsNative(
            uint mapId, float x0, float y0, float z0, float x1, float y1, float z1);

        [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl, EntryPoint = "SegmentIntersectsDynamicObjectsDetailed")]
        [return: MarshalAs(UnmanagedType.I1)]
        private static extern bool SegmentIntersectsDynamicObjectsDetailedNative(
            uint mapId,
            float x0,
            float y0,
            float z0,
            float x1,
            float y1,
            float z1,
            out uint blockingInstanceId,
            out ulong blockingGuid,
            out uint blockingDisplayId);

        [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl, EntryPoint = "GetDynamicObjectCount")]
        private static extern int GetDynamicObjectCountNative();

        // ── Corridor API P/Invoke ──

        private const int CORRIDOR_MAX_CORNERS = 96;

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        private struct CorridorResultNative
        {
            public uint Handle;
            public int CornerCount;
            // 16 corners × 3 floats = 48 floats
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = CORRIDOR_MAX_CORNERS * 3)]
            public float[] Corners;
            public float PosX, PosY, PosZ;
            public int BlockedSegmentIndex;
            public uint BlockingInstanceId;
            public ulong BlockingGuid;
            public uint BlockingDisplayId;
            public uint Flags;
        }

        private const uint CorridorResultFlagOverlayRepaired = 0x00000001u;

        [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl)]
        private static extern CorridorResultNative FindPathCorridor(
            uint mapId, NativeXyz start, NativeXyz end);

        [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl)]
        private static extern CorridorResultNative CorridorUpdate(
            uint handle, NativeXyz agentPos);

        [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl)]
        private static extern CorridorResultNative CorridorMoveTarget(
            uint handle, NativeXyz newTarget);

        [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl)]
        [return: MarshalAs(UnmanagedType.I1)]
        private static extern bool CorridorIsValid(uint handle);

        [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl)]
        private static extern void CorridorDestroy(uint handle);


        // ── Spatial Queries P/Invoke ──

        [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl, EntryPoint = "IsPointOnNavmesh")]
        [return: MarshalAs(UnmanagedType.I1)]
        private static extern bool IsPointOnNavmeshNative(
            uint mapId, float x, float y, float z, float searchRadius,
            out float nearestX, out float nearestY, out float nearestZ);

        [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl, EntryPoint = "FindNearestWalkablePoint")]
        private static extern uint FindNearestWalkablePointNative(
            uint mapId, float x, float y, float z, float searchRadius,
            out float outX, out float outY, out float outZ);

        // ── Segment Validation P/Invoke ──

        [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl)]
        private static extern uint ValidateWalkableSegment(
            uint mapId,
            NativeXyz start,
            NativeXyz end,
            float radius,
            float height,
            out float resolvedEndZ,
            out float supportDelta,
            out float travelFraction);

        [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl)]
        private static extern float GetGroundZ(
            uint mapId,
            float x,
            float y,
            float z,
            float maxSearchDist);

        [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl)]
        private static extern NativePhysicsOutput PhysicsStepV2(ref NativePhysicsInput input);

        [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl, EntryPoint = "ClassifyPathSegmentAffordance")]
        private static extern uint ClassifyPathSegmentAffordanceNative(
            uint mapId,
            NativeXyz start,
            NativeXyz end,
            float radius,
            float height,
            out float climbHeight,
            out float gapDistance,
            out float dropHeight,
            out float slopeAngleDeg,
            out float resolvedEndZ,
            out uint validationCode);

        private enum SegmentValidationCode : uint
        {
            Clear = 0,
            BlockedGeometry = 1,
            MissingSupport = 2,
            StepUpTooHigh = 3,
            StepDownTooFar = 4,
        }

        private readonly record struct OverlayPathAttempt(
            XYZ[] Path,
            string SuccessResult,
            int? BlockedSegmentIndex,
            string BlockedReason,
            bool PathBlocked,
            NativePathResolutionKind ResolutionKind)
        {
            public bool HasPath => Path.Length > 0;
            public bool IsUsable => HasPath && !PathBlocked;
        }

        private readonly Func<uint, XYZ, XYZ, bool, float, float, NativePathResolution> _findPathResolver;
        private readonly Func<uint, XYZ, XYZ, bool> _segmentBlocker;
        private readonly Func<uint, XYZ, XYZ, string?> _segmentBlockerReasonResolver;

        // ── Constructors ──

        public Navigation()
            : this(findPathResolver: (Func<uint, XYZ, XYZ, bool, XYZ[]>?)null, segmentBlocker: null, segmentBlockerReasonResolver: null)
        {
        }

        public Navigation(
            Func<uint, XYZ, XYZ, bool, XYZ[]>? findPathResolver,
            Func<uint, XYZ, XYZ, bool>? segmentBlocker,
            Func<uint, XYZ, XYZ, string?>? segmentBlockerReasonResolver = null)
        {
            _findPathResolver = findPathResolver is null
                ? FindPathCorridorNative
                : (mapId, start, end, smoothPath, _, _) => NativePathResolution.FromPath(findPathResolver(mapId, start, end, smoothPath));
            _segmentBlocker = segmentBlocker ?? SegmentIntersectsDynamicObjectsInternal;
            _segmentBlockerReasonResolver = segmentBlockerReasonResolver ?? TryResolveDynamicObjectBlockReasonInternal;
        }

        public Navigation(
            Func<uint, XYZ, XYZ, bool, NativePathResolution> findPathResolver,
            Func<uint, XYZ, XYZ, bool>? segmentBlocker,
            Func<uint, XYZ, XYZ, string?>? segmentBlockerReasonResolver = null)
            : this(
                (mapId, start, end, smoothPath, _, _) => findPathResolver(mapId, start, end, smoothPath),
                segmentBlocker,
                segmentBlockerReasonResolver)
        {
        }

        private Navigation(
            Func<uint, XYZ, XYZ, bool, float, float, NativePathResolution> findPathResolver,
            Func<uint, XYZ, XYZ, bool>? segmentBlocker,
            Func<uint, XYZ, XYZ, string?>? segmentBlockerReasonResolver = null)
        {
            _findPathResolver = findPathResolver ?? throw new ArgumentNullException(nameof(findPathResolver));
            _segmentBlocker = segmentBlocker ?? SegmentIntersectsDynamicObjectsInternal;
            _segmentBlockerReasonResolver = segmentBlockerReasonResolver ?? TryResolveDynamicObjectBlockReasonInternal;
        }

        /// <summary>
        /// Diagnostic: call native FindPath with known-good coordinates and report results.
        /// Used during startup to verify mmaps are actually loaded.
        /// </summary>
        public static (int length, bool success) DiagnosticFindPath(uint mapId, float startX, float startY, float startZ, float endX, float endY, float endZ)
        {
            IntPtr pathPtr = IntPtr.Zero;
            try
            {
                pathPtr = FindPath(mapId, new NativeXyz(new XYZ(startX, startY, startZ)),
                    new NativeXyz(new XYZ(endX, endY, endZ)), true, out int length);
                return (length, pathPtr != IntPtr.Zero && length > 0);
            }
            finally
            {
                if (pathPtr != IntPtr.Zero)
                    PathArrFree(pathPtr);
            }
        }

        // ── Public API ──

        public XYZ[] CalculatePath(uint mapId, XYZ start, XYZ end, bool smoothPath)
            => CalculateValidatedPath(mapId, start, end, smoothPath).Path;

        public void PreloadMap(uint mapId)
            => PreloadMapNative(mapId);

        public static NativeSegmentAffordanceResult ClassifySegmentAffordance(
            uint mapId,
            XYZ start,
            XYZ end,
            float agentRadius = 0.6f,
            float agentHeight = 2.0f)
        {
            var affordance = (NativeSegmentAffordance)ClassifyPathSegmentAffordanceNative(
                mapId,
                new NativeXyz(start),
                new NativeXyz(end),
                agentRadius,
                agentHeight,
                out var climbHeight,
                out var gapDistance,
                out var dropHeight,
                out var slopeAngleDeg,
                out var resolvedEndZ,
                out var validationCode);

            return new NativeSegmentAffordanceResult(
                affordance,
                validationCode,
                climbHeight,
                gapDistance,
                dropHeight,
                slopeAngleDeg,
                resolvedEndZ);
        }

        public static bool IsSegmentWalkableForAgent(
            uint mapId,
            XYZ start,
            XYZ end,
            float agentRadius = 0.6f,
            float agentHeight = 2.0f,
            float maxResolvedEndZDelta = 1.0f,
            bool requireLocalPhysicsReachability = true)
        {
            if (Distance2D(start, end) >= EarlyStaticRepairLosMinSegmentLength &&
                !HasLineOfSightStrict(mapId, start, end))
            {
                return false;
            }

            var validation = ValidateSegmentForAgent(mapId, start, end, agentRadius, agentHeight);
            if (!IsLocallyWalkable(validation, start, end))
                return false;

            if (!TryClassifyAffordance(mapId, start, end, agentRadius, agentHeight, out var affordance))
                return false;

            if (float.IsFinite(affordance.ResolvedEndZ) &&
                affordance.ResolvedEndZ > -100000f &&
                MathF.Abs(affordance.ResolvedEndZ - end.Z) > maxResolvedEndZDelta)
            {
                return false;
            }

            if (requireLocalPhysicsReachability &&
                IsLocalPhysicsReachabilityBreak(mapId, start, end, agentRadius, agentHeight))
            {
                return false;
            }

            return affordance.Affordance is NativeSegmentAffordance.Walk
                or NativeSegmentAffordance.StepUp
                or NativeSegmentAffordance.SafeDrop;
        }

        public static bool IsSegmentLocallyReachableForAgent(
            uint mapId,
            XYZ start,
            XYZ end,
            float agentRadius = 0.6f,
            float agentHeight = 2.0f)
            => !IsLocalPhysicsReachabilityBreak(mapId, start, end, agentRadius, agentHeight);

        public NavigationPathResult CalculateValidatedPath(uint mapId, XYZ start, XYZ end, bool smoothPath,
            float agentRadius = 0.6f, float agentHeight = 2.0f)
        {
            var stopwatch = Stopwatch.StartNew();
            var result = CalculateValidatedPathCore(mapId, start, end, smoothPath, agentRadius, agentHeight);
            NavigationPerformanceMetrics.RecordValidatedPath(stopwatch.Elapsed, result);
            return result;
        }

        /// <summary>
        /// PFS-OVERHAUL-006 (2026-05-07): raw Detour path with no managed
        /// repair pipeline. Returns whatever Detour's findStraightPath /
        /// smoothPath produced, wrapped as a NavigationPathResult with no
        /// validation metadata. Intended for the overhaul phase where we
        /// want path queries to honestly reflect the navmesh state, with
        /// no localLayer / steep-segment / static-LOS / affordance repairs
        /// transforming the path. Once bake fidelity is good enough that
        /// raw Detour produces walkable paths on its own, the entire
        /// CalculateValidatedPath repair pipeline can be deleted.
        /// </summary>
        public NavigationPathResult CalculateRawPath(uint mapId, XYZ start, XYZ end, bool smoothPath,
            float agentRadius = 0.6f, float agentHeight = 2.0f)
        {
            var stopwatch = Stopwatch.StartNew();
            var rawPath = TryFindPathNative(mapId, start, end, smoothPath, agentRadius, agentHeight);
            stopwatch.Stop();
            if (rawPath.Length == 0)
            {
                return new NavigationPathResult(
                    Array.Empty<XYZ>(),
                    Array.Empty<XYZ>(),
                    "no_path",
                    null);
            }
            return new NavigationPathResult(
                rawPath,
                rawPath,
                "raw_detour",
                null);
        }

        public NavigationPathResult CalculateRoutePackSeedPath(uint mapId, XYZ start, XYZ end, bool smoothPath,
            float agentRadius = 0.6f, float agentHeight = 2.0f)
        {
            var resolution = FindPathCorridorResolution(mapId, start, end);
            var rawPath = resolution.Path ?? Array.Empty<XYZ>();
            if (rawPath.Length == 0)
                return new NavigationPathResult(Array.Empty<XYZ>(), Array.Empty<XYZ>(), "no_path", null);

            var pathForPack = rawPath;
            var resultTag = "route_pack_corridor";

            return BuildBoundedCorridorValidationResult(
                mapId,
                rawPath,
                pathForPack,
                smoothPath,
                agentRadius,
                agentHeight,
                resultTag,
                "route_pack_corridor_static_los",
                "route_pack_corridor_local_physics_layer",
                resolution.BlockedSegmentIndex,
                resolution.BlockedReason);
        }

        public NavigationPathResult CalculateStaticRoutePackPath(uint mapId, XYZ start, XYZ end, bool smoothPath,
            float agentRadius = 0.6f, float agentHeight = 2.0f)
        {
            var generated = CalculateValidatedPath(mapId, start, end, smoothPath, agentRadius, agentHeight);
            return EnforceStaticRoutePackSupport(mapId, generated, smoothPath, agentRadius, agentHeight);
        }

        private static NavigationPathResult EnforceStaticRoutePackSupport(
            uint mapId,
            NavigationPathResult generated,
            bool smoothPath,
            float agentRadius,
            float agentHeight)
        {
            var rawPath = generated.RawPath.Length > 0 ? generated.RawPath : generated.Path;
            if (generated.Path.Length < 2 ||
                generated.BlockedSegmentIndex.HasValue ||
                !string.Equals(generated.BlockedReason, "none", StringComparison.OrdinalIgnoreCase))
            {
                return generated;
            }

            var pathForValidation = generated.Path;
            var resultTag = generated.Result;
            var repairCount = 0;

            if (smoothPath)
            {
                pathForValidation = DensifyPath(pathForValidation, SmoothPathDensifySpacing);
                pathForValidation = NormalizeEarlySupportLayer(mapId, pathForValidation, int.MaxValue, agentRadius, agentHeight);
                pathForValidation = RemoveShortVerticalLayerSpikes(mapId, pathForValidation, agentRadius, agentHeight, int.MaxValue);
                pathForValidation = RemoveShortHorizontalDetourSpikes(mapId, pathForValidation, agentRadius, agentHeight, int.MaxValue);
            }

            pathForValidation = RepairEarlyStaticBreaks(
                mapId,
                pathForValidation,
                agentRadius,
                agentHeight,
                out var staticRepairCount,
                out var staticBlockedIdx,
                out var staticBlockedReason,
                maxScanSegments: int.MaxValue,
                repairBudgetOverride: StaticRoutePackRepairBudget,
                allowRouteRepair: true);
            if (staticRepairCount > 0)
            {
                repairCount += staticRepairCount;
                resultTag = "route_pack_static_los";
            }

            pathForValidation = NormalizeEarlySupportLayer(mapId, pathForValidation, int.MaxValue, agentRadius, agentHeight);
            pathForValidation = RemoveShortVerticalLayerSpikes(mapId, pathForValidation, agentRadius, agentHeight, int.MaxValue);
            pathForValidation = RemoveShortHorizontalDetourSpikes(mapId, pathForValidation, agentRadius, agentHeight, int.MaxValue);
            pathForValidation = RepairAffordanceBreaks(
                mapId,
                pathForValidation,
                agentRadius,
                agentHeight,
                out var affordanceRepairCount,
                out var affordanceBlockedIdx,
                out var affordanceBlockedReason,
                includeCumulativeBreaks: true,
                maxScanSegments: int.MaxValue,
                includeLocalPhysicsReachabilityBreaks: true);
            if (affordanceRepairCount > 0)
            {
                repairCount += affordanceRepairCount;
                resultTag = "route_pack_local_physics";
            }

            pathForValidation = NormalizeEarlySupportLayer(mapId, pathForValidation, int.MaxValue, agentRadius, agentHeight);
            pathForValidation = RemoveShortVerticalLayerSpikes(mapId, pathForValidation, agentRadius, agentHeight, int.MaxValue);
            pathForValidation = RemoveShortHorizontalDetourSpikes(mapId, pathForValidation, agentRadius, agentHeight, int.MaxValue);
            pathForValidation = NormalizeLocalPhysicsReachableLayers(
                mapId,
                pathForValidation,
                agentRadius,
                agentHeight,
                out var localPhysicsProjectionCount,
                int.MaxValue);
            if (localPhysicsProjectionCount > 0)
            {
                repairCount += localPhysicsProjectionCount;
                resultTag = "route_pack_local_physics";
            }

            pathForValidation = RepairAffordanceBreaks(
                mapId,
                pathForValidation,
                agentRadius,
                agentHeight,
                out var finalRepairCount,
                out var finalBlockedIdx,
                out var finalBlockedReason,
                includeCumulativeBreaks: true,
                maxScanSegments: int.MaxValue,
                includeLocalPhysicsReachabilityBreaks: true);
            if (finalRepairCount > 0)
            {
                repairCount += finalRepairCount;
                resultTag = "route_pack_local_physics";
            }

            if (staticBlockedIdx.HasValue)
            {
                return new NavigationPathResult(pathForValidation, rawPath, resultTag, staticBlockedIdx, staticBlockedReason);
            }

            if (affordanceBlockedIdx.HasValue)
            {
                return new NavigationPathResult(pathForValidation, rawPath, resultTag, affordanceBlockedIdx, affordanceBlockedReason);
            }

            if (finalBlockedIdx.HasValue)
            {
                return new NavigationPathResult(pathForValidation, rawPath, resultTag, finalBlockedIdx, finalBlockedReason);
            }

            pathForValidation = DensifyPath(pathForValidation, SmoothPathDensifySpacing);

            if (FindFirstLineOfSightBreak(mapId, pathForValidation) is int losBlockedIdx)
            {
                return new NavigationPathResult(pathForValidation, rawPath, resultTag, losBlockedIdx, "static_los");
            }

            if (FindFirstLocalPhysicsReachabilityBreak(
                    mapId,
                    pathForValidation,
                    agentRadius,
                    agentHeight,
                    int.MaxValue,
                    out var localPhysicsBlockedIdx))
            {
                return new NavigationPathResult(pathForValidation, rawPath, resultTag, localPhysicsBlockedIdx, "local_physics_layer");
            }

            if (FindFirstStraightAffordanceBreak(
                    mapId,
                    pathForValidation,
                    agentRadius,
                    agentHeight,
                    int.MaxValue,
                    out var straightBlockedIdx,
                    out var straightBlockedReason))
            {
                return new NavigationPathResult(pathForValidation, rawPath, resultTag, straightBlockedIdx, straightBlockedReason);
            }

            return repairCount > 0
                ? new NavigationPathResult(pathForValidation, rawPath, resultTag, null, "none")
                : generated;
        }

        private static NavigationPathResult BuildBoundedCorridorValidationResult(
            uint mapId,
            XYZ[] rawPath,
            XYZ[] initialPath,
            bool smoothPath,
            float agentRadius,
            float agentHeight,
            string successResult,
            string staticRepairResult,
            string localPhysicsResult,
            int? blockedIdx,
            string? blockedReason)
        {
            if (initialPath.Length == 0)
                return new NavigationPathResult(Array.Empty<XYZ>(), rawPath, "no_path", null);

            var pathForValidation = initialPath;
            var resultTag = successResult;
            var normalizedBlockedReason = NormalizeBlockReason(blockedReason);

            if (smoothPath)
            {
                var losRepairedPath = RepairLongLineOfSightBreaks(
                    mapId,
                    pathForValidation,
                    out var losRepairCount,
                    out _,
                    out _);

                if (losRepairedPath.Length > 0)
                    pathForValidation = losRepairedPath;

                if (losRepairCount > 0)
                    resultTag = staticRepairResult;
            }

            pathForValidation = DensifyPath(pathForValidation, SmoothPathDensifySpacing);
            pathForValidation = NormalizeEarlySupportLayer(mapId, pathForValidation, int.MaxValue, agentRadius, agentHeight);
            pathForValidation = RemoveShortVerticalLayerSpikes(mapId, pathForValidation, agentRadius, agentHeight, int.MaxValue);
            pathForValidation = RemoveShortHorizontalDetourSpikes(mapId, pathForValidation, agentRadius, agentHeight, int.MaxValue);
            pathForValidation = RepairEarlyStaticBreaks(
                mapId,
                pathForValidation,
                agentRadius,
                agentHeight,
                out var earlyStaticRepairCount,
                out _,
                out _,
                maxScanSegments: BoundedCorridorStaticRepairScanLimit,
                repairBudgetOverride: TimeSpan.FromSeconds(3),
                allowRouteRepair: true);
            if (earlyStaticRepairCount > 0)
                resultTag = staticRepairResult;

            pathForValidation = NormalizeEarlySupportLayer(mapId, pathForValidation, int.MaxValue, agentRadius, agentHeight);
            pathForValidation = RemoveShortVerticalLayerSpikes(mapId, pathForValidation, agentRadius, agentHeight, int.MaxValue);
            pathForValidation = RemoveShortHorizontalDetourSpikes(mapId, pathForValidation, agentRadius, agentHeight, int.MaxValue);
            pathForValidation = RemoveLargeVerticalLayerExcursions(
                mapId,
                pathForValidation,
                agentRadius,
                agentHeight,
                int.MaxValue,
                out var largeLayerExcursionRepairCount);
            if (largeLayerExcursionRepairCount > 0)
                resultTag = localPhysicsResult;
            pathForValidation = NormalizeFloatingSupportLayers(
                mapId,
                pathForValidation,
                BoundedCorridorFloatingSupportProjectionLimit,
                out var floatingSupportProjectionCount);
            if (floatingSupportProjectionCount > 0)
                resultTag = localPhysicsResult;

            if (smoothPath)
            {
                pathForValidation = NormalizeLocalPhysicsReachableLayers(
                    mapId,
                    pathForValidation,
                    agentRadius,
                    agentHeight,
                    out var localPhysicsProjectionCount,
                    BoundedCorridorLocalPhysicsLayerProjectionScanLimit);
                if (localPhysicsProjectionCount > 0)
                {
                    resultTag = localPhysicsResult;
                    pathForValidation = RemoveShortVerticalLayerSpikes(mapId, pathForValidation, agentRadius, agentHeight, int.MaxValue);
                    pathForValidation = RemoveShortHorizontalDetourSpikes(mapId, pathForValidation, agentRadius, agentHeight, int.MaxValue);
                    pathForValidation = RemoveLargeVerticalLayerExcursions(
                        mapId,
                        pathForValidation,
                        agentRadius,
                        agentHeight,
                        int.MaxValue,
                        out var postLocalLayerExcursionRepairCount);
                    if (postLocalLayerExcursionRepairCount > 0)
                        resultTag = localPhysicsResult;
                    pathForValidation = NormalizeFloatingSupportLayers(
                        mapId,
                        pathForValidation,
                        BoundedCorridorFloatingSupportProjectionLimit,
                        out var postLocalFloatingSupportProjectionCount);
                    if (postLocalFloatingSupportProjectionCount > 0)
                        resultTag = localPhysicsResult;
                }
            }

            if (FindFirstLineOfSightBreak(mapId, pathForValidation).HasValue)
            {
                pathForValidation = DensifyStaticLineOfSightBreaks(mapId, pathForValidation);
                pathForValidation = RemoveLargeVerticalLayerExcursions(
                    mapId,
                    pathForValidation,
                    agentRadius,
                    agentHeight,
                    int.MaxValue,
                    out var postLosLayerExcursionRepairCount);
                if (postLosLayerExcursionRepairCount > 0)
                    resultTag = localPhysicsResult;
                if (FindFirstLineOfSightBreak(mapId, pathForValidation) is null)
                    resultTag = staticRepairResult;
            }

            if (smoothPath && ShouldProbeLocalPhysicsReachability(pathForValidation))
            {
                pathForValidation = RepairAffordanceBreaks(
                    mapId,
                    pathForValidation,
                    agentRadius,
                    agentHeight,
                    out var localPhysicsRepairCount,
                    out _,
                    out _,
                    includeCumulativeBreaks: false,
                    maxScanSegments: BoundedCorridorLocalPhysicsRepairScanLimit,
                    includeLocalPhysicsReachabilityBreaks: true);

                if (localPhysicsRepairCount > 0)
                {
                    resultTag = localPhysicsResult;
                    pathForValidation = NormalizeEarlySupportLayer(mapId, pathForValidation, int.MaxValue, agentRadius, agentHeight);
                    pathForValidation = RemoveShortVerticalLayerSpikes(mapId, pathForValidation, agentRadius, agentHeight, int.MaxValue);
                    pathForValidation = RemoveShortHorizontalDetourSpikes(mapId, pathForValidation, agentRadius, agentHeight, int.MaxValue);
                    pathForValidation = RemoveLargeVerticalLayerExcursions(
                        mapId,
                        pathForValidation,
                        agentRadius,
                        agentHeight,
                        int.MaxValue,
                        out var postRepairLayerExcursionCount);
                    if (postRepairLayerExcursionCount > 0)
                        resultTag = localPhysicsResult;
                    pathForValidation = DensifyStaticLineOfSightBreaks(mapId, pathForValidation);
                }
            }

            pathForValidation = DensifyPath(pathForValidation, SmoothPathDensifySpacing);
            if (smoothPath && ShouldProbeLocalPhysicsReachability(pathForValidation))
            {
                pathForValidation = RepairLocalPhysicsReachabilityBreaks(
                    mapId,
                    pathForValidation,
                    agentRadius,
                    agentHeight,
                    out var finalDensifiedLocalPhysicsRepairCount,
                    BoundedCorridorLocalPhysicsRepairScanLimit);

                if (finalDensifiedLocalPhysicsRepairCount > 0)
                {
                    resultTag = localPhysicsResult;
                    pathForValidation = NormalizeEarlySupportLayer(mapId, pathForValidation, int.MaxValue, agentRadius, agentHeight);
                    pathForValidation = RemoveShortVerticalLayerSpikes(mapId, pathForValidation, agentRadius, agentHeight, int.MaxValue);
                    pathForValidation = RemoveShortHorizontalDetourSpikes(mapId, pathForValidation, agentRadius, agentHeight, int.MaxValue);
                    pathForValidation = RemoveLargeVerticalLayerExcursions(
                        mapId,
                        pathForValidation,
                        agentRadius,
                        agentHeight,
                        int.MaxValue,
                        out var finalDensifiedLayerExcursionCount);
                    if (finalDensifiedLayerExcursionCount > 0)
                        resultTag = localPhysicsResult;
                    pathForValidation = DensifyStaticLineOfSightBreaks(mapId, pathForValidation);
                }
            }
            pathForValidation = DensifyPath(pathForValidation, SmoothPathDensifySpacing);

            if (smoothPath &&
                blockedIdx is null &&
                FindFirstLineOfSightBreak(mapId, pathForValidation) is int finalLosBlockedIdx)
            {
                blockedIdx = finalLosBlockedIdx;
                normalizedBlockedReason = "static_los";
            }

            if (smoothPath &&
                blockedIdx is null &&
                ShouldProbeLocalPhysicsReachability(pathForValidation) &&
                FindFirstLocalPhysicsReachabilityBreak(
                    mapId,
                    pathForValidation,
                    agentRadius,
                    agentHeight,
                    BoundedCorridorLocalPhysicsRepairScanLimit,
                    out var localPhysicsBlockedIdx))
            {
                blockedIdx = localPhysicsBlockedIdx;
                normalizedBlockedReason = "local_physics_layer";
            }

            return new NavigationPathResult(
                pathForValidation,
                rawPath,
                resultTag,
                blockedIdx,
                blockedIdx.HasValue ? normalizedBlockedReason : "none");
        }

        private NavigationPathResult CalculateValidatedPathCore(uint mapId, XYZ start, XYZ end, bool smoothPath,
            float agentRadius, float agentHeight)
        {
            var preferredAttempt = EvaluateOverlayAwarePath(mapId, start, end, smoothPath, agentRadius, agentHeight, "native_path");
            NavigationPathResult? preferredValidatedResult = null;
            if (preferredAttempt.IsUsable)
            {
                var preferredResult = BuildUsablePathResult(
                    mapId,
                    start,
                    end,
                    preferredAttempt,
                    smoothPath,
                    agentRadius,
                    agentHeight);
                preferredValidatedResult = preferredResult;
                LogPathSelectionCandidate("preferred", preferredResult);
                if (IsCompleteUsablePath(start, end, preferredResult))
                    return preferredResult;
            }

            var alternateAttempt = EvaluateOverlayAwarePath(mapId, start, end, !smoothPath, agentRadius, agentHeight, "native_path_alternate_mode");
            NavigationPathResult? alternateValidatedResult = null;
            if (alternateAttempt.IsUsable)
            {
                var alternateResult = BuildUsablePathResult(
                    mapId,
                    start,
                    end,
                    alternateAttempt,
                    !smoothPath,
                    agentRadius,
                    agentHeight);
                alternateValidatedResult = alternateResult;
                LogPathSelectionCandidate("alternate", alternateResult);
                if (IsCompleteUsablePath(start, end, alternateResult))
                    return alternateResult;
            }

            var corridorFallbackResolution = FindPathCorridorResolution(
                mapId,
                start,
                end,
                NativePathResolutionKind.CorridorFallback);
            var corridorFallbackAttempt = BuildOverlayPathAttempt(
                mapId,
                start,
                end,
                corridorFallbackResolution,
                agentRadius,
                agentHeight,
                "native_path_corridor_fallback");
            NavigationPathResult? corridorFallbackValidatedResult = null;
            if (corridorFallbackAttempt.IsUsable)
            {
                var corridorFallbackResult = BuildUsablePathResult(
                    mapId,
                    start,
                    end,
                    corridorFallbackAttempt,
                    smoothPath,
                    agentRadius,
                    agentHeight);
                corridorFallbackValidatedResult = corridorFallbackResult;
                LogPathSelectionCandidate("corridor_fallback", corridorFallbackResult);
                if (IsCompleteUsablePath(start, end, corridorFallbackResult))
                    return corridorFallbackResult;
            }

            var blockedResults = new[]
                {
                    preferredValidatedResult,
                    alternateValidatedResult,
                    corridorFallbackValidatedResult,
                }
                .Where(static result => result.HasValue)
                .Select(static result => result!.Value)
                .ToArray();
            if (blockedResults.Length > 0)
            {
                var selectedBlockedResult = SelectMoreAdvancedBlockedResult(blockedResults);
                LogPathSelectionCandidate("selected_blocked", selectedBlockedResult);
                return selectedBlockedResult;
            }

            var repairSource = SelectRepairSource(preferredAttempt, alternateAttempt);
            if (repairSource.Path.Length > 1 && repairSource.BlockedSegmentIndex is int blockedSegmentIndex)
            {
                var dynamicOverlayBlock = HasActiveDynamicObjectOverlay()
                    && IsDynamicOverlayBlockReason(repairSource.BlockedReason);
                var repairedPath = TryRepairPath(
                    mapId,
                    start,
                    end,
                    smoothPath,
                    agentRadius,
                    agentHeight,
                    repairSource.Path,
                    blockedSegmentIndex,
                    allowGlobalRepair: !dynamicOverlayBlock,
                    recordDynamicOverlayRepair: dynamicOverlayBlock);
                if (repairedPath.Length > 0)
                {
                    var repairedResult = ApplyNativeSegmentValidation(
                        mapId,
                        repairedPath,
                        smoothPath,
                        agentRadius,
                        agentHeight,
                        "repaired_dynamic_overlay");

                    return new NavigationPathResult(
                        repairedResult.Path,
                        repairSource.Path,
                        repairedResult.Result,
                        blockedSegmentIndex,
                        repairedResult.BlockedReason != "none"
                            ? repairedResult.BlockedReason
                            : "dynamic_overlay");
                }
            }

            if (!preferredAttempt.HasPath && !alternateAttempt.HasPath)
                return new NavigationPathResult(Array.Empty<XYZ>(), Array.Empty<XYZ>(), "no_path", null);

            var blockedAttempt = preferredAttempt.HasPath ? preferredAttempt : alternateAttempt;
            return new NavigationPathResult(
                Array.Empty<XYZ>(),
                blockedAttempt.Path,
                "blocked_by_dynamic_overlay",
                blockedAttempt.BlockedSegmentIndex,
                blockedAttempt.BlockedReason);
        }

        private static NavigationPathResult SelectMoreAdvancedBlockedResult(
            IReadOnlyList<NavigationPathResult> results)
        {
            var selected = results[0];
            var selectedIndex = selected.BlockedSegmentIndex ?? -1;
            for (var i = 1; i < results.Count; i++)
            {
                var candidate = results[i];
                var candidateIndex = candidate.BlockedSegmentIndex ?? -1;
                if (candidateIndex <= selectedIndex)
                    continue;

                selected = candidate;
                selectedIndex = candidateIndex;
            }

            return selected;
        }

        private NativePathResolution FindPathCorridorNative(
            uint mapId,
            XYZ start,
            XYZ end,
            bool smoothPath,
            float agentRadius,
            float agentHeight)
        {
            var dynamicOverlayActive = HasActiveDynamicObjectOverlay();
            var routeDistance2D = Distance2D(start, end);
            var routeVerticalDelta = MathF.Abs(end.Z - start.Z);
            var useCorridorFirst = routeDistance2D >= CorridorFirstPathMinDistance &&
                (routeDistance2D <= CorridorFirstPathMaxDistance ||
                    (routeDistance2D <= CorridorFirstHighVerticalPathMaxDistance &&
                        routeVerticalDelta >= CorridorFirstHighVerticalMinDelta));
            if (useCorridorFirst)
            {
                var mediumCorridorResolution = FindPathCorridorResolution(
                    mapId,
                    start,
                    end,
                    NativePathResolutionKind.CorridorFirst);
                Console.Error.WriteLine(
                    $"[PATH_NATIVE] map={mapId} mode=corridor_first_medium_long dist2D={routeDistance2D:F1} path=[{FormatPathPreview(mediumCorridorResolution.Path)}]");
                if (smoothPath && !dynamicOverlayActive && mediumCorridorResolution.Path.Length > 1)
                {
                    var expandedPath = TryExpandCorridorWithSmoothNativeSegments(
                        mapId,
                        mediumCorridorResolution.Path,
                        agentRadius,
                        agentHeight);
                    if (expandedPath.Length > 0)
                        return NativePathResolution.FromPath(
                            expandedPath,
                            NativePathResolutionKind.CorridorFirstExpanded);
                }

                return mediumCorridorResolution;
            }

            if (smoothPath && !dynamicOverlayActive)
            {
                var smoothNativePath = TryFindPathNative(mapId, start, end, smoothPath: true, agentRadius, agentHeight);
                if (smoothNativePath.Length > 0)
                {
                    Console.Error.WriteLine(
                        $"[PATH_NATIVE] map={mapId} mode=smooth path=[{FormatPathPreview(smoothNativePath)}]");
                    if (HasLongStaticLineOfSightBreak(mapId, smoothNativePath))
                    {
                        Console.Error.WriteLine(
                            $"[PATH_NATIVE] map={mapId} mode=smooth contains long static-LOS break; keeping smooth path for repair.");
                    }

                    return NativePathResolution.FromPath(smoothNativePath);
                }
            }

            if (!smoothPath)
            {
                var straightPath = TryFindPathNative(mapId, start, end, smoothPath: false, agentRadius, agentHeight);
                if (straightPath.Length > 0)
                {
                    Console.Error.WriteLine(
                        $"[PATH_NATIVE] map={mapId} mode=straight path=[{FormatPathPreview(straightPath)}]");
                    if (!HasLongStaticLineOfSightBreak(mapId, straightPath))
                        return NativePathResolution.FromPath(straightPath);

                    Console.Error.WriteLine(
                        $"[PATH_NATIVE] map={mapId} mode=straight contains long static-LOS break; retrying smooth native path.");
                    if (Distance2D(start, end) >= SmoothFallbackAfterStraightStaticBreakMinDistance)
                    {
                        var smoothFallbackPath = TryFindPathNative(mapId, start, end, smoothPath: true, agentRadius, agentHeight);
                        if (smoothFallbackPath.Length > 0)
                        {
                            Console.Error.WriteLine(
                                $"[PATH_NATIVE] map={mapId} mode=smooth_after_straight_static_break path=[{FormatPathPreview(smoothFallbackPath)}]");
                            return NativePathResolution.FromPath(
                                smoothFallbackPath,
                                NativePathResolutionKind.SmoothFallbackAfterStraightStaticBreak);
                        }
                    }

                    Console.Error.WriteLine(
                        $"[PATH_NATIVE] map={mapId} mode=smooth_after_straight_static_break unavailable; retrying corridor path.");
                }
            }
            else if (smoothPath && !dynamicOverlayActive)
            {
                var straightFallbackPath = TryFindPathNative(mapId, start, end, smoothPath: false, agentRadius, agentHeight);
                if (straightFallbackPath.Length > 0)
                {
                    Console.Error.WriteLine(
                        $"[PATH_NATIVE] map={mapId} mode=straight_fallback path=[{FormatPathPreview(straightFallbackPath)}]");
                    return NativePathResolution.FromPath(straightFallbackPath);
                }
            }

            var corridorResolution = FindPathCorridorResolution(
                mapId,
                start,
                end,
                NativePathResolutionKind.CorridorFallback);
            if (smoothPath && !dynamicOverlayActive && corridorResolution.Path.Length > 1)
            {
                var expandedPath = TryExpandCorridorWithSmoothNativeSegments(mapId, corridorResolution.Path, agentRadius, agentHeight);
                if (expandedPath.Length > 0)
                    return NativePathResolution.FromPath(
                        expandedPath,
                        NativePathResolutionKind.CorridorFallback);
            }

            return corridorResolution;
        }

        private static bool HasActiveDynamicObjectOverlay()
        {
            try
            {
                return GetDynamicObjectCountNative() > 0;
            }
            catch
            {
                return false;
            }
        }

        private NativePathResolution FindPathCorridorResolution(
            uint mapId,
            XYZ start,
            XYZ end,
            NativePathResolutionKind kind = NativePathResolutionKind.Corridor)
        {
            var nativeStart = new NativeXyz(start);
            var nativeEnd = new NativeXyz(end);
            var stopwatch = Stopwatch.StartNew();
            CorridorResultNative corridorResult = default;

            try
            {
                corridorResult = FindPathCorridor(mapId, nativeStart, nativeEnd);
                if (corridorResult.Handle == 0 || corridorResult.CornerCount == 0)
                {
                    Console.Error.WriteLine(
                        $"[CORRIDOR] No corridor path found for map={mapId} start=({start.X:F1},{start.Y:F1},{start.Z:F1}) end=({end.X:F1},{end.Y:F1},{end.Z:F1})");
                    return NativePathResolution.FromPath(Array.Empty<XYZ>(), kind);
                }

                var waypoints = new List<XYZ>(corridorResult.CornerCount + 1) { start };
                for (int i = 0; i < corridorResult.CornerCount; i++)
                {
                    var x = corridorResult.Corners[i * 3];
                    var y = corridorResult.Corners[i * 3 + 1];
                    var z = corridorResult.Corners[i * 3 + 2];
                    waypoints.Add(new XYZ(x, y, z));
                }

                if (Distance3D(waypoints[^1], end) > CombineWaypointEpsilon)
                    waypoints.Add(end);

                var rawPath = waypoints.ToArray();
                Console.Error.WriteLine(
                    $"[CORRIDOR] map={mapId} corners={corridorResult.CornerCount} path=[{FormatPathPreview(rawPath)}]");
                var blockedSegmentIndex = corridorResult.BlockedSegmentIndex >= 0
                    ? corridorResult.BlockedSegmentIndex
                    : (int?)null;
                var blockedReason = blockedSegmentIndex.HasValue
                    ? FormatDynamicOverlayBlockReason(
                        corridorResult.BlockingInstanceId,
                        corridorResult.BlockingGuid,
                        corridorResult.BlockingDisplayId)
                    : "none";
                var repaired = (corridorResult.Flags & CorridorResultFlagOverlayRepaired) != 0;

                return new NativePathResolution(rawPath, blockedSegmentIndex, blockedReason, repaired, kind);
            }
            finally
            {
                NavigationPerformanceMetrics.RecordCorridorQuery(stopwatch.Elapsed);
                if (corridorResult.Handle != 0)
                {
                    try { CorridorDestroy(corridorResult.Handle); } catch { }
                }
            }
        }

        private static bool HasLongStaticLineOfSightBreak(uint mapId, XYZ[] path)
        {
            for (var i = 0; i < path.Length - 1; i++)
            {
                var from = path[i];
                var to = path[i + 1];
                if (Distance2D(from, to) > LongSegmentLosRepairThreshold
                    && !HasLineOfSightSafe(mapId, from, to))
                {
                    return true;
                }
            }

            return false;
        }

        private static XYZ[] TryExpandCorridorWithSmoothNativeSegments(
            uint mapId,
            XYZ[] corridorPath,
            float agentRadius,
            float agentHeight)
        {
            if (corridorPath.Length < 2)
                return Array.Empty<XYZ>();

            var expanded = new List<XYZ>(corridorPath.Length * 2) { corridorPath[0] };
            var expandedSegments = 0;

            for (var i = 0; i < corridorPath.Length - 1; i++)
            {
                var segmentStart = expanded[^1];
                var segmentEnd = corridorPath[i + 1];
                if (Distance2D(segmentStart, segmentEnd) >= SmoothCorridorExpansionMinSegmentLength)
                {
                    var smoothSegment = TryFindPathNative(mapId, segmentStart, segmentEnd, smoothPath: true, agentRadius, agentHeight);
                    if (smoothSegment.Length > 1)
                    {
                        AppendPathSkippingDuplicateStart(expanded, smoothSegment);
                        expandedSegments++;
                        continue;
                    }
                }

                AppendWaypointIfDistinct(expanded, segmentEnd);
            }

            if (expandedSegments == 0 || expanded.Count <= corridorPath.Length)
                return Array.Empty<XYZ>();

            Console.Error.WriteLine(
                $"[PATH_NATIVE] map={mapId} mode=smooth_from_corridor corridorLen={corridorPath.Length} expandedLen={expanded.Count} expandedSegments={expandedSegments}");

            return expanded.ToArray();
        }

        private static void AppendPathSkippingDuplicateStart(List<XYZ> waypoints, XYZ[] path)
        {
            var startIndex = waypoints.Count > 0 && path.Length > 0 && Distance3D(waypoints[^1], path[0]) <= CombineWaypointEpsilon
                ? 1
                : 0;

            for (var i = startIndex; i < path.Length; i++)
                AppendWaypointIfDistinct(waypoints, path[i]);
        }

        private static string FormatPathPreview(XYZ[] path, int limit = 16)
        {
            if (path.Length == 0)
                return "<empty>";

            var preview = path
                .Take(limit)
                .Select(p => $"({p.X:F1},{p.Y:F1},{p.Z:F1})");
            var suffix = path.Length > limit ? $" -> ... +{path.Length - limit}" : string.Empty;
            return string.Join(" -> ", preview) + suffix;
        }

        private OverlayPathAttempt EvaluateOverlayAwarePath(
            uint mapId,
            XYZ start,
            XYZ end,
            bool smoothPath,
            float agentRadius,
            float agentHeight,
            string successResult)
        {
            var stopwatch = Stopwatch.StartNew();
            try
            {
                var resolution = _findPathResolver(mapId, start, end, smoothPath, agentRadius, agentHeight);
                NavigationPerformanceMetrics.RecordPathResolverAttempt(smoothPath, stopwatch.Elapsed);
                return BuildOverlayPathAttempt(mapId, start, end, resolution, agentRadius, agentHeight, successResult);
            }
            catch (Exception ex)
            {
                NavigationPerformanceMetrics.RecordPathResolverAttempt(smoothPath, stopwatch.Elapsed);
                Console.Error.WriteLine($"[CORRIDOR] Native path resolver failed: {ex.Message}");
                return new OverlayPathAttempt(
                    Array.Empty<XYZ>(),
                    successResult,
                    null,
                    "none",
                    PathBlocked: false,
                    ResolutionKind: NativePathResolutionKind.Native);
            }
        }

        private OverlayPathAttempt BuildOverlayPathAttempt(
            uint mapId,
            XYZ start,
            XYZ end,
            NativePathResolution resolution,
            float agentRadius,
            float agentHeight,
            string successResult)
        {
            var path = resolution.Path ?? Array.Empty<XYZ>();
            if (path.Length == 0)
            {
                return new OverlayPathAttempt(
                    Array.Empty<XYZ>(),
                    successResult,
                    null,
                    "none",
                    PathBlocked: false,
                    ResolutionKind: resolution.Kind);
            }

            if (!HasUsableNativeEndpointAnchors(start, end, path, out var endpointBlockReason))
            {
                return new OverlayPathAttempt(
                    path,
                    successResult,
                    0,
                    endpointBlockReason,
                    PathBlocked: true,
                    ResolutionKind: resolution.Kind);
            }

            if (resolution.BlockedSegmentIndex is int nativeBlockedSegmentIndex)
            {
                return new OverlayPathAttempt(
                    path,
                    resolution.WasRepairedAroundBlockedSegment ? "repaired_dynamic_overlay" : successResult,
                    nativeBlockedSegmentIndex,
                    NormalizeBlockReason(resolution.BlockedReason),
                    PathBlocked: !resolution.WasRepairedAroundBlockedSegment,
                    ResolutionKind: resolution.Kind);
            }

            var (blockedSegmentIndex, blockedReason) = FindBlockedSegment(mapId, path, agentRadius, agentHeight);
            return new OverlayPathAttempt(
                path,
                successResult,
                blockedSegmentIndex,
                blockedReason,
                PathBlocked: blockedSegmentIndex.HasValue,
                ResolutionKind: resolution.Kind);
        }

        private static bool HasUsableNativeEndpointAnchors(
            XYZ requestedStart,
            XYZ requestedEnd,
            XYZ[] path,
            out string blockedReason)
        {
            blockedReason = "none";
            if (path.Length == 0)
                return false;

            var startDistance = Distance2D(requestedStart, path[0]);
            if (startDistance > NativePathEndpointMaxDistance)
            {
                blockedReason = $"start_projection:{startDistance:F1}";
                return false;
            }

            var endDistance = Distance2D(requestedEnd, path[^1]);
            if (endDistance > NativePathEndpointMaxDistance)
            {
                blockedReason = $"end_projection:{endDistance:F1}";
                return false;
            }

            return true;
        }

        private static bool IsCompleteUsablePath(XYZ requestedStart, XYZ requestedEnd, NavigationPathResult result)
            => !result.BlockedSegmentIndex.HasValue
                && HasUsableNativeEndpointAnchors(requestedStart, requestedEnd, result.Path, out _);

        private static bool IsDynamicOverlayBlockReason(string? blockedReason)
            => !string.IsNullOrWhiteSpace(blockedReason)
                && blockedReason.StartsWith("dynamic_overlay", StringComparison.OrdinalIgnoreCase);

        private static bool IsManagedRepairResult(string? result)
            => !string.IsNullOrWhiteSpace(result)
                && result.StartsWith("repaired_", StringComparison.OrdinalIgnoreCase);

        private static bool IsBoundedCorridorFirstResolution(NativePathResolutionKind resolutionKind)
            => resolutionKind is NativePathResolutionKind.CorridorFirst
                or NativePathResolutionKind.CorridorFirstExpanded;

        private static bool ShouldUseSmoothValidation(
            bool requestedSmoothPath,
            NativePathResolutionKind resolutionKind)
            => requestedSmoothPath ||
                resolutionKind is NativePathResolutionKind.SmoothFallbackAfterStraightStaticBreak;

        private static bool IsAffordanceRepairDiagnosticsEnabled()
            => string.Equals(
                Environment.GetEnvironmentVariable("WWOW_AFFORDANCE_REPAIR_DIAGNOSTICS"),
                "1",
                StringComparison.OrdinalIgnoreCase);

        private static bool IsPathSelectionDiagnosticsEnabled()
            => string.Equals(
                Environment.GetEnvironmentVariable("WWOW_PATH_SELECTION_DIAGNOSTICS"),
                "1",
                StringComparison.OrdinalIgnoreCase);

        private static void LogPathSelectionCandidate(string label, NavigationPathResult result)
        {
            if (!IsPathSelectionDiagnosticsEnabled())
                return;

            Console.Error.WriteLine(
                $"[PATH-SELECT] {label} result={result.Result} pathLen={result.Path.Length} rawLen={result.RawPath.Length} " +
                $"blockedIdx={(result.BlockedSegmentIndex.HasValue ? result.BlockedSegmentIndex.Value.ToString() : "none")} " +
                $"blockedReason={result.BlockedReason}");
        }

        private static bool IsLocalPhysicsLayerDiagnosticsEnabled()
            => string.Equals(
                Environment.GetEnvironmentVariable("WWOW_LOCAL_PHYSICS_LAYER_DIAGNOSTICS"),
                "1",
                StringComparison.OrdinalIgnoreCase);

        private NavigationPathResult BuildUsablePathResult(
            uint mapId,
            XYZ start,
            XYZ end,
            OverlayPathAttempt attempt,
            bool smoothPath,
            float agentRadius,
            float agentHeight)
        {
            var dynamicOverlayActive = HasActiveDynamicObjectOverlay();
            var validationSmoothPath = ShouldUseSmoothValidation(smoothPath, attempt.ResolutionKind);
            var validatedResult = ApplyNativeSegmentValidation(
                mapId,
                attempt.Path,
                validationSmoothPath,
                agentRadius,
                agentHeight,
                attempt.SuccessResult,
                attempt.ResolutionKind);

            if (validatedResult.BlockedSegmentIndex is int staticBlockedSegmentIndex)
            {
                var dynamicOverlayBlock = IsDynamicOverlayBlockReason(validatedResult.BlockedReason);
                if (dynamicOverlayActive && dynamicOverlayBlock)
                    return validatedResult;

                var repairedPath = TryRepairPath(
                    mapId,
                    start,
                    end,
                    validationSmoothPath,
                    agentRadius,
                    agentHeight,
                    validatedResult.Path,
                    staticBlockedSegmentIndex,
                    allowGlobalRepair: !dynamicOverlayActive || !dynamicOverlayBlock,
                    recordDynamicOverlayRepair: dynamicOverlayActive && dynamicOverlayBlock);

                if (repairedPath.Length > 0)
                {
                    var repairedResult = ApplyNativeSegmentValidation(
                        mapId,
                        repairedPath,
                        validationSmoothPath,
                        agentRadius,
                        agentHeight,
                        "repaired_segment_validation");

                    if (IsCompleteUsablePath(start, end, repairedResult))
                        return repairedResult;
                }
            }

            if (!HasUsableNativeEndpointAnchors(start, end, validatedResult.Path, out var endpointBlockReason))
            {
                return new NavigationPathResult(
                    validatedResult.Path,
                    validatedResult.RawPath,
                    validatedResult.Result,
                    validatedResult.Path.Length >= 2 ? validatedResult.Path.Length - 2 : 0,
                    endpointBlockReason);
            }

            if (attempt.BlockedSegmentIndex is not int blockedSegmentIndex ||
                validatedResult.BlockedSegmentIndex.HasValue ||
                IsManagedRepairResult(validatedResult.Result))
            {
                return validatedResult;
            }

            return new NavigationPathResult(
                validatedResult.Path,
                validatedResult.RawPath,
                attempt.SuccessResult,
                blockedSegmentIndex,
                NormalizeBlockReason(attempt.BlockedReason));
        }

        private NavigationPathResult ApplyNativeSegmentValidation(
            uint mapId,
            XYZ[] rawPath,
            bool smoothPath,
            float agentRadius,
            float agentHeight,
            string successResult,
            NativePathResolutionKind resolutionKind = NativePathResolutionKind.Native)
        {
            var stopwatch = Stopwatch.StartNew();
            var result = ApplyNativeSegmentValidationCore(
                mapId,
                rawPath,
                smoothPath,
                agentRadius,
                agentHeight,
                successResult,
                resolutionKind);
            NavigationPerformanceMetrics.RecordManagedValidation(stopwatch.Elapsed);
            return result;
        }

        private NavigationPathResult ApplyNativeSegmentValidationCore(
            uint mapId,
            XYZ[] rawPath,
            bool smoothPath,
            float agentRadius,
            float agentHeight,
            string successResult,
            NativePathResolutionKind resolutionKind)
        {
            var dynamicOverlayActive = HasActiveDynamicObjectOverlay();
            if (!dynamicOverlayActive && IsBoundedCorridorFirstResolution(resolutionKind))
            {
                return BuildBoundedCorridorValidationResult(
                    mapId,
                    rawPath,
                    rawPath,
                    smoothPath,
                    agentRadius,
                    agentHeight,
                    successResult,
                    "repaired_static_los",
                    "repaired_local_physics_layer",
                    blockedIdx: null,
                    blockedReason: "none");
            }

            var losRepairCount = 0;
            int? losBlockedIdx = null;
            var losBlockedReason = "none";
            var pathForValidation = rawPath;
            if (smoothPath)
            {
                var losRepairedPath = RepairLongLineOfSightBreaks(
                    mapId,
                    rawPath,
                    out losRepairCount,
                    out losBlockedIdx,
                    out losBlockedReason);
                pathForValidation = losRepairedPath.Length > 0 ? losRepairedPath : rawPath;
            }
            if (smoothPath)
            {
                pathForValidation = DensifyPath(pathForValidation, SmoothPathDensifySpacing);
                pathForValidation = NormalizeEarlySupportLayer(mapId, pathForValidation, EarlySupportNormalizationLimit, agentRadius, agentHeight);
                pathForValidation = RemoveShortVerticalLayerSpikes(mapId, pathForValidation, agentRadius, agentHeight);
                pathForValidation = RemoveShortHorizontalDetourSpikes(mapId, pathForValidation, agentRadius, agentHeight);
                var earlyStaticRepairScanLimit = dynamicOverlayActive
                    ? OverlayStraightStaticRepairScanLimit
                    : EarlyStaticRepairScanLimit;
                var earlyStaticRepairBudget = dynamicOverlayActive
                    ? OverlayStraightStaticRepairBudget
                    : EarlyStaticRepairBudget;
                pathForValidation = RepairEarlyStaticBreaks(
                    mapId,
                    pathForValidation,
                    agentRadius,
                    agentHeight,
                    out var earlyRepairCount,
                    out var earlyBlockedIdx,
                    out var earlyBlockedReason,
                    earlyStaticRepairScanLimit,
                    earlyStaticRepairBudget,
                    allowRouteRepair: !dynamicOverlayActive);

                if (earlyRepairCount > 0)
                    successResult = "repaired_static_los";

                var allowCumulativeAffordanceRepair = pathForValidation.Length >= 2;
                var affordanceRepairScanLimit = allowCumulativeAffordanceRepair
                    ? AffordanceRepairCumulativeSmoothScanLimit
                    : int.MaxValue;
                pathForValidation = RepairAffordanceBreaks(
                    mapId,
                    pathForValidation,
                    agentRadius,
                    agentHeight,
                    out var affordanceRepairCount,
                    out var affordanceBlockedIdx,
                    out var affordanceBlockedReason,
                    includeCumulativeBreaks: allowCumulativeAffordanceRepair,
                    maxScanSegments: affordanceRepairScanLimit,
                    includeLocalPhysicsReachabilityBreaks: false);
                pathForValidation = NormalizeEarlySupportLayer(mapId, pathForValidation, int.MaxValue, agentRadius, agentHeight);
                pathForValidation = RemoveShortVerticalLayerSpikes(mapId, pathForValidation, agentRadius, agentHeight);
                pathForValidation = RemoveShortHorizontalDetourSpikes(mapId, pathForValidation, agentRadius, agentHeight);

                pathForValidation = RepairAffordanceBreaks(
                    mapId,
                    pathForValidation,
                    agentRadius,
                    agentHeight,
                    out var postNormalizeAffordanceRepairCount,
                    out var postNormalizeAffordanceBlockedIdx,
                    out var postNormalizeAffordanceBlockedReason,
                    includeCumulativeBreaks: allowCumulativeAffordanceRepair,
                    maxScanSegments: affordanceRepairScanLimit,
                    includeLocalPhysicsReachabilityBreaks: false);

                var totalAffordanceRepairCount = affordanceRepairCount + postNormalizeAffordanceRepairCount;
                var postAffordanceStaticRepairCount = 0;
                int? postAffordanceStaticBlockedIdx = null;
                var postAffordanceStaticBlockedReason = "none";
                var localPhysicsProjectionCount = 0;
                if (totalAffordanceRepairCount > 0)
                {
                    pathForValidation = RepairEarlyStaticBreaks(
                        mapId,
                        pathForValidation,
                        agentRadius,
                        agentHeight,
                        out postAffordanceStaticRepairCount,
                        out postAffordanceStaticBlockedIdx,
                        out postAffordanceStaticBlockedReason,
                        Math.Min(earlyStaticRepairScanLimit, PostAffordanceStaticRepairScanLimit),
                        PostAffordanceStaticRepairBudget,
                        allowRouteRepair: !dynamicOverlayActive);
                    pathForValidation = NormalizeEarlySupportLayer(mapId, pathForValidation, int.MaxValue, agentRadius, agentHeight);
                    pathForValidation = RemoveShortVerticalLayerSpikes(mapId, pathForValidation, agentRadius, agentHeight);
                    pathForValidation = RemoveShortHorizontalDetourSpikes(mapId, pathForValidation, agentRadius, agentHeight);
                    pathForValidation = DensifyStaticLineOfSightBreaks(mapId, pathForValidation);
                    if (postAffordanceStaticBlockedIdx.HasValue &&
                        FindFirstLineOfSightBreak(
                            mapId,
                            pathForValidation,
                            Math.Min(earlyStaticRepairScanLimit, PostAffordanceStaticRepairScanLimit)) is null)
                    {
                        postAffordanceStaticBlockedIdx = null;
                        postAffordanceStaticBlockedReason = "none";
                        postAffordanceStaticRepairCount++;
                    }
                }

                pathForValidation = NormalizeLocalPhysicsReachableLayers(
                    mapId,
                    pathForValidation,
                    agentRadius,
                    agentHeight,
                    out localPhysicsProjectionCount,
                    LocalPhysicsLayerProjectionSmoothScanLimit);
                if (localPhysicsProjectionCount > 0)
                {
                    pathForValidation = RemoveShortVerticalLayerSpikes(mapId, pathForValidation, agentRadius, agentHeight, int.MaxValue);
                    pathForValidation = RemoveShortHorizontalDetourSpikes(mapId, pathForValidation, agentRadius, agentHeight, int.MaxValue);
                    pathForValidation = DensifyStaticLineOfSightBreaks(mapId, pathForValidation);
                }

                var finalLocalPhysicsRepairCount = 0;
                int? finalLocalPhysicsBlockedIdx = null;
                var finalLocalPhysicsBlockedReason = "none";
                if (ShouldProbeLocalPhysicsReachability(pathForValidation))
                {
                    pathForValidation = RepairAffordanceBreaks(
                        mapId,
                        pathForValidation,
                        agentRadius,
                        agentHeight,
                        out finalLocalPhysicsRepairCount,
                        out finalLocalPhysicsBlockedIdx,
                        out finalLocalPhysicsBlockedReason,
                        includeCumulativeBreaks: false,
                        maxScanSegments: LocalPhysicsLayerProjectionSmoothScanLimit,
                        includeLocalPhysicsReachabilityBreaks: true);

                    if (finalLocalPhysicsRepairCount > 0)
                    {
                        pathForValidation = NormalizeEarlySupportLayer(mapId, pathForValidation, int.MaxValue, agentRadius, agentHeight);
                        pathForValidation = RemoveShortVerticalLayerSpikes(mapId, pathForValidation, agentRadius, agentHeight, int.MaxValue);
                        pathForValidation = RemoveShortHorizontalDetourSpikes(mapId, pathForValidation, agentRadius, agentHeight, int.MaxValue);
                        pathForValidation = DensifyStaticLineOfSightBreaks(mapId, pathForValidation);
                    }

                    pathForValidation = DensifyPath(pathForValidation, SmoothPathDensifySpacing);

                    if (FindFirstLocalPhysicsReachabilityBreak(
                            mapId,
                            pathForValidation,
                            agentRadius,
                            agentHeight,
                            LocalPhysicsLayerProjectionSmoothScanLimit,
                            out var localPhysicsBlockedIdx))
                    {
                        finalLocalPhysicsBlockedIdx ??= localPhysicsBlockedIdx;
                        finalLocalPhysicsBlockedReason = "local_physics_layer";
                    }
                }

                if (affordanceBlockedIdx.HasValue &&
                    !IsNativeSegmentValidationEnabled() &&
                    !ShouldDeferDownstreamAffordanceBlock(pathForValidation, affordanceBlockedIdx.Value))
                {
                    return new NavigationPathResult(
                        pathForValidation,
                        rawPath,
                        affordanceRepairCount > 0 ? "repaired_affordance" : successResult,
                        affordanceBlockedIdx,
                        affordanceBlockedReason);
                }

                if (postNormalizeAffordanceBlockedIdx.HasValue &&
                    !IsNativeSegmentValidationEnabled() &&
                    !ShouldDeferDownstreamAffordanceBlock(pathForValidation, postNormalizeAffordanceBlockedIdx.Value))
                {
                    return new NavigationPathResult(
                        pathForValidation,
                        rawPath,
                        postNormalizeAffordanceRepairCount > 0 ? "repaired_affordance" : successResult,
                        postNormalizeAffordanceBlockedIdx,
                        postNormalizeAffordanceBlockedReason);
                }

                if (postAffordanceStaticBlockedIdx.HasValue &&
                    !dynamicOverlayActive &&
                    !IsNativeSegmentValidationEnabled())
                {
                    return new NavigationPathResult(
                        pathForValidation,
                        rawPath,
                        totalAffordanceRepairCount > 0 ? "repaired_affordance" : successResult,
                        postAffordanceStaticBlockedIdx,
                        postAffordanceStaticBlockedReason);
                }

                if (finalLocalPhysicsBlockedIdx.HasValue &&
                    !IsNativeSegmentValidationEnabled())
                {
                    return new NavigationPathResult(
                        pathForValidation,
                        rawPath,
                        finalLocalPhysicsRepairCount > 0 ? "repaired_affordance" : successResult,
                        finalLocalPhysicsBlockedIdx,
                        finalLocalPhysicsBlockedReason);
                }

                if (totalAffordanceRepairCount > 0 || finalLocalPhysicsRepairCount > 0)
                    successResult = "repaired_affordance";
                else if (localPhysicsProjectionCount > 0)
                    successResult = "repaired_local_physics_layer";
                else if (postAffordanceStaticRepairCount > 0)
                    successResult = "repaired_static_los";

                if (earlyBlockedIdx.HasValue &&
                    affordanceRepairCount == 0 &&
                    postNormalizeAffordanceRepairCount == 0 &&
                    !dynamicOverlayActive &&
                    !IsNativeSegmentValidationEnabled())
                {
                    var blockedResult = earlyRepairCount > 0 ? "repaired_static_los" : successResult;
                    return new NavigationPathResult(
                        pathForValidation,
                        rawPath,
                        blockedResult,
                        earlyBlockedIdx,
                        earlyBlockedReason);
                }
            }

            if (!smoothPath)
            {
                // Straight-corner requests are the latency-sensitive alternate mode.
                // Keep them on the raw corridor so the caller can fall through quickly
                // instead of spending tens of seconds on full segment validation.
                pathForValidation = NormalizeEarlySupportLayer(mapId, pathForValidation, EarlySupportNormalizationLimit, agentRadius, agentHeight);
                pathForValidation = DensifyPath(pathForValidation, SmoothPathDensifySpacing);
                pathForValidation = RemoveShortVerticalLayerSpikes(mapId, pathForValidation, agentRadius, agentHeight);
                pathForValidation = RemoveShortHorizontalDetourSpikes(mapId, pathForValidation, agentRadius, agentHeight);
                var earlyStaticRepairScanLimit = dynamicOverlayActive
                    ? OverlayStraightStaticRepairScanLimit
                    : EarlyStaticRepairScanLimit;
                var earlyStaticRepairBudget = dynamicOverlayActive
                    ? OverlayStraightStaticRepairBudget
                    : EarlyStaticRepairBudget;
                pathForValidation = RepairEarlyStaticBreaks(
                    mapId,
                    pathForValidation,
                    agentRadius,
                    agentHeight,
                    out var earlyRepairCount,
                    out var earlyBlockedIdx,
                    out var earlyBlockedReason,
                    earlyStaticRepairScanLimit,
                    earlyStaticRepairBudget,
                    allowRouteRepair: !dynamicOverlayActive);

                var resultTag = losRepairCount > 0 ? "repaired_static_los" : successResult;
                var earlyLocalPhysicsReachabilityRepairCount = 0;
                if (ShouldProbeLocalPhysicsReachability(pathForValidation))
                {
                    pathForValidation = RepairLocalPhysicsReachabilityBreaks(
                        mapId,
                        pathForValidation,
                        agentRadius,
                        agentHeight,
                        out earlyLocalPhysicsReachabilityRepairCount,
                        maxScanSegments: 12);

                    if (earlyLocalPhysicsReachabilityRepairCount > 0)
                    {
                        resultTag = "repaired_local_physics_layer";

                        if (FindFirstLineOfSightBreak(mapId, pathForValidation, earlyStaticRepairScanLimit) is int recomputedStaticBlockedIdx)
                        {
                            earlyBlockedIdx = recomputedStaticBlockedIdx;
                            earlyBlockedReason = "static_los";
                        }
                        else if (string.Equals(earlyBlockedReason, "static_los", StringComparison.Ordinal))
                        {
                            earlyBlockedIdx = null;
                            earlyBlockedReason = "none";
                        }
                    }
                }

                if (earlyBlockedIdx.HasValue &&
                    earlyLocalPhysicsReachabilityRepairCount == 0 &&
                    !dynamicOverlayActive &&
                    !IsNativeSegmentValidationEnabled())
                {
                    return new NavigationPathResult(
                        pathForValidation,
                        rawPath,
                        earlyRepairCount > 0 ? "repaired_static_los" : resultTag,
                        earlyBlockedIdx,
                        earlyBlockedReason);
                }

                if (earlyRepairCount > 0)
                    resultTag = "repaired_static_los";

                pathForValidation = NormalizeEarlySupportLayer(mapId, pathForValidation, int.MaxValue, agentRadius, agentHeight);
                var routeStart = pathForValidation.Length > 0 ? pathForValidation[0] : rawPath.FirstOrDefault();
                var routeEnd = pathForValidation.Length > 0 ? pathForValidation[^1] : rawPath.LastOrDefault();
                var allowPrefixCumulativeRepair = Distance2D(routeStart, routeEnd) > 50.0f
                    || MathF.Abs(routeEnd.Z - routeStart.Z) < 15.0f;
                pathForValidation = RepairAffordanceBreaks(
                    mapId,
                    pathForValidation,
                    agentRadius,
                    agentHeight,
                    out var affordanceRepairCount,
                    out var affordanceBlockedIdx,
                    out var affordanceBlockedReason,
                    includeCumulativeBreaks: allowPrefixCumulativeRepair,
                    includeSegmentBreaks: false,
                    maxScanSegments: 12,
                    includeLocalPhysicsReachabilityBreaks: ShouldProbeLocalPhysicsReachability(pathForValidation));
                pathForValidation = NormalizeEarlySupportLayer(mapId, pathForValidation, int.MaxValue, agentRadius, agentHeight);
                pathForValidation = RemoveShortVerticalLayerSpikes(mapId, pathForValidation, agentRadius, agentHeight, int.MaxValue);
                pathForValidation = RemoveShortHorizontalDetourSpikes(mapId, pathForValidation, agentRadius, agentHeight, int.MaxValue);

                var localPhysicsReachabilityRepairCount = 0;
                if (IsLocalPhysicsLayerDiagnosticsEnabled())
                {
                    Console.Error.WriteLine(
                        $"[LOCAL-REPAIR-DBG] straight_probe pathLen={pathForValidation.Length} route=({pathForValidation[0].X:F1},{pathForValidation[0].Y:F1},{pathForValidation[0].Z:F1})->({pathForValidation[^1].X:F1},{pathForValidation[^1].Y:F1},{pathForValidation[^1].Z:F1}) shouldProbe={ShouldProbeLocalPhysicsReachability(pathForValidation)}");
                }

                if (ShouldProbeLocalPhysicsReachability(pathForValidation))
                {
                    pathForValidation = RepairLocalPhysicsReachabilityBreaks(
                        mapId,
                        pathForValidation,
                        agentRadius,
                        agentHeight,
                        out localPhysicsReachabilityRepairCount,
                        maxScanSegments: 12);

                    if (localPhysicsReachabilityRepairCount > 0)
                    {
                        resultTag = "repaired_local_physics_layer";

                        if (FindFirstLineOfSightBreak(mapId, pathForValidation, earlyStaticRepairScanLimit) is int recomputedStaticBlockedIdx)
                        {
                            earlyBlockedIdx = recomputedStaticBlockedIdx;
                            earlyBlockedReason = "static_los";
                        }
                        else if (string.Equals(earlyBlockedReason, "static_los", StringComparison.Ordinal))
                        {
                            earlyBlockedIdx = null;
                            earlyBlockedReason = "none";
                        }

                        if (affordanceBlockedIdx.HasValue &&
                            (string.Equals(affordanceBlockedReason, "local_physics_layer", StringComparison.Ordinal) ||
                                string.Equals(affordanceBlockedReason, "local_physics_movement", StringComparison.Ordinal)))
                        {
                            if (FindFirstLocalPhysicsReachabilityBreak(
                                    mapId,
                                    pathForValidation,
                                    agentRadius,
                                    agentHeight,
                                    12,
                                    out var remainingLocalPhysicsBlockedIdx))
                            {
                                affordanceBlockedIdx = remainingLocalPhysicsBlockedIdx;
                                affordanceBlockedReason = "local_physics_layer";
                            }
                            else
                            {
                                affordanceBlockedIdx = null;
                                affordanceBlockedReason = "none";
                            }
                        }
                    }
                }

                if (affordanceBlockedIdx.HasValue && !IsNativeSegmentValidationEnabled())
                {
                    return new NavigationPathResult(
                        pathForValidation,
                        rawPath,
                        affordanceRepairCount > 0 ? "repaired_affordance" : resultTag,
                        affordanceBlockedIdx,
                        affordanceBlockedReason);
                }

                if (affordanceRepairCount > 0)
                    resultTag = "repaired_affordance";

                var straightBlockedIdx = earlyBlockedIdx ?? affordanceBlockedIdx;
                var straightBlockedReason = earlyBlockedIdx.HasValue
                    ? earlyBlockedReason
                    : affordanceBlockedReason;
                if (!straightBlockedIdx.HasValue &&
                    Distance2D(routeStart, routeEnd) >= SmoothFallbackAfterStraightStaticBreakMinDistance &&
                    FindFirstStraightAffordanceBreak(
                        mapId,
                        pathForValidation,
                        agentRadius,
                        agentHeight,
                        AffordanceRepairStraightScanLimit,
                        out var detectedBlockedIdx,
                        out var detectedBlockedReason))
                {
                    straightBlockedIdx = detectedBlockedIdx;
                    straightBlockedReason = detectedBlockedReason;
                }

                return new NavigationPathResult(
                    pathForValidation,
                    rawPath,
                    resultTag,
                    straightBlockedIdx ?? losBlockedIdx,
                    straightBlockedIdx.HasValue ? straightBlockedReason : losBlockedReason);
            }

            if (!IsNativeSegmentValidationEnabled())
            {
                var resultTag = losRepairCount > 0 ? "repaired_static_los" : successResult;
                pathForValidation = NormalizeEarlySupportLayer(mapId, pathForValidation, int.MaxValue, agentRadius, agentHeight);
                if (ShouldProbeLocalPhysicsReachability(pathForValidation) &&
                    FindFirstLocalPhysicsReachabilityBreak(
                        mapId,
                        pathForValidation,
                        agentRadius,
                        agentHeight,
                        LocalPhysicsLayerProjectionSmoothScanLimit,
                        out var localPhysicsBlockedIdx))
                {
                    return new NavigationPathResult(
                        pathForValidation,
                        rawPath,
                        resultTag,
                        localPhysicsBlockedIdx,
                        "local_physics_layer");
                }

                return new NavigationPathResult(
                    pathForValidation,
                    rawPath,
                    resultTag,
                    losBlockedIdx,
                    losBlockedReason);
            }

            var validatedPath = ValidateCorridorSegments(
                mapId,
                pathForValidation,
                agentRadius,
                agentHeight,
                out var blockedIdx,
                out var blockedReason);
            if (RequiresNativeSegmentValidation(validatedPath))
                validatedPath = NormalizeEarlySupportLayer(mapId, validatedPath, int.MaxValue, agentRadius, agentHeight);

            var finalBlockedIdx = blockedIdx ?? losBlockedIdx;
            var finalBlockedReason = blockedIdx.HasValue
                ? blockedReason
                : losBlockedReason;
            var finalResultTag = blockedIdx.HasValue
                ? "repaired_segment_validation"
                : losRepairCount > 0
                    ? "repaired_static_los"
                    : successResult;
            return new NavigationPathResult(validatedPath, rawPath, finalResultTag, finalBlockedIdx, finalBlockedReason);
        }

        private static bool RequiresNativeSegmentValidation(XYZ[] path)
        {
            if (path.Length < 2)
                return false;

            var totalVerticalTravel = 0f;
            for (var i = 0; i < path.Length - 1; i++)
            {
                var from = path[i];
                var to = path[i + 1];
                var horizontal = Distance2D(from, to);
                var vertical = MathF.Abs(to.Z - from.Z);
                totalVerticalTravel += vertical;

                if (horizontal <= 24f && vertical >= 2.5f)
                    return true;
            }

            return totalVerticalTravel >= 35f;
        }

        private static XYZ[] RepairLongLineOfSightBreaks(
            uint mapId,
            XYZ[] path,
            out int repairCount,
            out int? firstBlockedIdx,
            out string firstBlockedReason)
        {
            repairCount = 0;
            firstBlockedIdx = null;
            firstBlockedReason = "none";
            if (path.Length < 2)
                return path;

            var repaired = new List<XYZ>(path.Length) { path[0] };
            var stopwatch = Stopwatch.StartNew();
            for (var i = 0; i < path.Length - 1; i++)
            {
                var segmentStart = repaired[^1];
                var segmentEnd = path[i + 1];
                var horizontal = Distance2D(segmentStart, segmentEnd);

                if (horizontal > LongSegmentLosRepairThreshold
                    && !HasLineOfSightSafe(mapId, segmentStart, segmentEnd))
                {
                    firstBlockedIdx ??= i;
                    firstBlockedReason = "static_los";

                    if (stopwatch.Elapsed <= LongSegmentRepairBudget
                        && TryBuildLongSegmentDetour(mapId, segmentStart, segmentEnd, stopwatch, out var detour))
                    {
                        AppendDensifiedSegment(repaired, segmentStart, detour, LongSegmentDensifySpacing);
                        segmentStart = repaired[^1];
                        repairCount++;
                    }
                }

                AppendDensifiedSegment(repaired, segmentStart, segmentEnd, LongSegmentDensifySpacing);
            }

            if (repairCount > 0)
            {
                NavigationPerformanceMetrics.RecordLongLineOfSightRepair(repairCount);
                Console.Error.WriteLine(
                    $"[CORRIDOR-LOS-REPAIR] repaired {repairCount} long static-LOS segment(s) pathLen={path.Length} repairedLen={repaired.Count}");
            }

            return repaired.ToArray();
        }

        private static XYZ[] DensifyPath(XYZ[] path, float spacing)
        {
            if (path.Length < 2)
                return path;

            var densified = new List<XYZ>(path.Length) { path[0] };
            for (var i = 0; i < path.Length - 1; i++)
                AppendDensifiedSegment(densified, densified[^1], path[i + 1], spacing);

            return densified.ToArray();
        }

        private static XYZ[] DensifyStaticLineOfSightBreaks(uint mapId, XYZ[] path)
        {
            if (path.Length < 2)
                return path;

            var densified = new List<XYZ>(path.Length) { path[0] };
            var spacing = EarlyStaticRepairLosMinSegmentLength * 0.8f;
            for (var i = 0; i < path.Length - 1; i++)
            {
                var from = densified[^1];
                var to = path[i + 1];
                if (Distance2D(from, to) >= EarlyStaticRepairLosMinSegmentLength &&
                    !HasLineOfSightStrict(mapId, from, to))
                {
                    AppendDensifiedSegment(densified, from, to, spacing);
                    continue;
                }

                AppendWaypointIfDistinct(densified, to);
            }

            return densified.ToArray();
        }

        private static XYZ[] NormalizeEarlySupportLayer(
            uint mapId,
            XYZ[] path,
            int maxWaypointIndex = EarlySupportNormalizationLimit,
            float agentRadius = 0f,
            float agentHeight = 0f)
        {
            if (path.Length < 2)
                return path;

            var normalized = (XYZ[])path.Clone();
            var checkEnd = Math.Min(normalized.Length, maxWaypointIndex + 1);
            var probeProjectedSupportReachability = ShouldProbeLocalPhysicsReachability(normalized);
            for (var i = 1; i < checkEnd; i++)
            {
                var candidate = normalized[i];
                if (!TryGetNearbyGroundZ(mapId, candidate, out var groundZ))
                    continue;

                var supportDelta = groundZ - candidate.Z;
                var absSupportDelta = MathF.Abs(supportDelta);
                var duplicateAnchor = Distance2D(normalized[i - 1], candidate) <= EarlySupportDuplicateHorizontalDistance;
                var projectionThreshold = duplicateAnchor
                    ? EarlySupportDuplicateProjectionThreshold
                    : EarlySupportProjectionThreshold;
                if (absSupportDelta <= projectionThreshold
                    || absSupportDelta > EarlySupportProjectionMaxDelta)
                {
                    continue;
                }

                var projectedZ = groundZ + EarlySupportGroundClearance;
                if (duplicateAnchor && MathF.Abs(normalized[i - 1].Z - groundZ) <= EarlySupportProjectionThreshold)
                    projectedZ = normalized[i - 1].Z;

                if (WouldCreateShortVerticalLayerSpikeProjection(normalized, i, projectedZ))
                    continue;

                var projected = new XYZ(candidate.X, candidate.Y, projectedZ);
                if (probeProjectedSupportReachability &&
                    projectedZ > candidate.Z + EarlySupportProjectionThreshold &&
                    agentRadius > 0f &&
                    agentHeight > 0f &&
                    !CanReachEarlySupportProjection(mapId, normalized, i, projected, agentRadius, agentHeight))
                {
                    continue;
                }

                normalized[i] = projected;
            }

            return CollapseDuplicateWaypoints(normalized);
        }

        private static bool CanReachEarlySupportProjection(
            uint mapId,
            IReadOnlyList<XYZ> path,
            int index,
            XYZ projected,
            float agentRadius,
            float agentHeight)
        {
            if (index <= 0 || index >= path.Count)
                return false;

            var previous = path[index - 1];
            var validation = ValidateSegmentForAgent(mapId, previous, projected, agentRadius, agentHeight);
            if (!IsLocallyWalkable(validation, previous, projected))
                return false;

            if (Distance2D(previous, projected) <= LocalPhysicsLayerProjectionMaxSegmentLength)
            {
                var simulation = SimulateLocalPhysicsSegment(mapId, previous, projected, agentRadius, agentHeight);
                if (!simulation.Available || !simulation.Compatible)
                    return false;

                if (Distance2D(simulation.FinalPosition, projected) > EarlySupportProjectionReachableMaxEndpointDistance)
                    return false;

                if (MathF.Abs(simulation.FinalPosition.Z - projected.Z) > EarlySupportProjectionReachableMaxZDelta)
                    return false;
            }

            if (index < path.Count - 1)
            {
                var next = path[index + 1];
                if (Distance2D(projected, next) <= LocalPhysicsLayerProjectionMaxSegmentLength)
                {
                    validation = ValidateSegmentForAgent(mapId, projected, next, agentRadius, agentHeight);
                    if (!IsLocallyWalkable(validation, projected, next))
                        return false;
                }
            }

            return true;
        }

        private static bool ShouldProbeLocalPhysicsReachability(IReadOnlyList<XYZ> path)
            => path.Count >= 2 &&
                Distance2D(path[0], path[^1]) <= EarlySupportProjectionReachabilityMaxRouteDistance;

        private static XYZ[] NormalizeFloatingSupportLayers(
            uint mapId,
            XYZ[] path,
            int maxWaypointIndex,
            out int projectionCount)
        {
            projectionCount = 0;
            if (path.Length < 2)
                return path;

            var normalized = (XYZ[])path.Clone();
            var checkEnd = Math.Min(normalized.Length, maxWaypointIndex + 1);
            for (var i = 1; i < checkEnd; i++)
            {
                var candidate = normalized[i];
                if (!TryGetNearbyGroundZ(mapId, candidate, out var groundZ))
                    continue;

                var unsupportedDrop = candidate.Z - groundZ;
                if (unsupportedDrop < FloatingSupportProjectionMinDrop ||
                    unsupportedDrop > FloatingSupportProjectionMaxDrop)
                {
                    continue;
                }

                normalized[i] = new XYZ(candidate.X, candidate.Y, groundZ + EarlySupportGroundClearance);
                projectionCount++;
            }

            if (projectionCount > 0)
            {
                Console.Error.WriteLine(
                    $"[CORRIDOR-FLOATING-SUPPORT] projected {projectionCount} unsupported waypoint(s) pathLen={path.Length}");
            }

            return projectionCount == 0
                ? path
                : CollapseDuplicateWaypoints(normalized);
        }

        private static bool WouldCreateShortVerticalLayerSpikeProjection(XYZ[] path, int index, float projectedZ)
        {
            if (index <= 0 || index >= path.Length - 1)
                return false;

            var previous = path[index - 1];
            var current = path[index];
            var next = path[index + 1];

            if (Distance2D(previous, current) > ShortVerticalLayerSpikeMaxLegLength ||
                Distance2D(current, next) > ShortVerticalLayerSpikeMaxLegLength ||
                Distance2D(previous, next) > ShortVerticalLayerSpikeMaxBridgeLength)
            {
                return false;
            }

            if (MathF.Abs(previous.Z - next.Z) > ShortVerticalLayerSpikeNeighborZTolerance)
                return false;

            var downwardSpike = MathF.Min(previous.Z, next.Z) - projectedZ;
            var upwardSpike = projectedZ - MathF.Max(previous.Z, next.Z);
            return downwardSpike >= ShortVerticalLayerSpikeMinDelta ||
                upwardSpike >= ShortVerticalLayerSpikeMinDelta;
        }

        private static XYZ[] CollapseDuplicateWaypoints(XYZ[] path)
        {
            if (path.Length < 2)
                return path;

            var collapsed = new List<XYZ>(path.Length);
            foreach (var waypoint in path)
                AppendWaypointIfDistinct(collapsed, waypoint);

            return collapsed.ToArray();
        }

        private static XYZ[] RemoveShortVerticalLayerSpikes(
            uint mapId,
            XYZ[] path,
            float agentRadius,
            float agentHeight,
            int maxWaypointIndex = EarlyStaticRepairScanLimit)
        {
            if (path.Length < 3)
                return path;

            var repaired = path.ToList();
            var scanEnd = Math.Min(repaired.Count - 1, maxWaypointIndex);
            for (var i = 1; i < scanEnd && i < repaired.Count - 1; i++)
            {
                var previous = repaired[i - 1];
                var current = repaired[i];
                var next = repaired[i + 1];

                if (Distance2D(previous, current) > ShortVerticalLayerSpikeMaxLegLength ||
                    Distance2D(current, next) > ShortVerticalLayerSpikeMaxLegLength ||
                    Distance2D(previous, next) > ShortVerticalLayerSpikeMaxBridgeLength)
                {
                    continue;
                }

                if (MathF.Abs(previous.Z - next.Z) > ShortVerticalLayerSpikeNeighborZTolerance)
                    continue;

                var downwardSpike = MathF.Min(previous.Z, next.Z) - current.Z;
                var upwardSpike = current.Z - MathF.Max(previous.Z, next.Z);
                if (downwardSpike < ShortVerticalLayerSpikeMinDelta && upwardSpike < ShortVerticalLayerSpikeMinDelta)
                    continue;

                if (!HasLineOfSightStrict(mapId, previous, next))
                    continue;

                var validation = ValidateSegmentForAgent(mapId, previous, next, agentRadius, agentHeight);
                if (!IsLocallyWalkable(validation, previous, next))
                    continue;

                repaired.RemoveAt(i);
                scanEnd = Math.Min(repaired.Count - 1, maxWaypointIndex);
                i = Math.Max(0, i - 2);
            }

            return repaired.Count == path.Length ? path : repaired.ToArray();
        }

        private static XYZ[] RemoveShortHorizontalDetourSpikes(
            uint mapId,
            XYZ[] path,
            float agentRadius,
            float agentHeight,
            int maxWaypointIndex = EarlyStaticRepairScanLimit)
        {
            if (path.Length < 3)
                return path;

            var repaired = path.ToList();
            var scanEnd = Math.Min(repaired.Count - 1, maxWaypointIndex);
            for (var i = 1; i < scanEnd && i < repaired.Count - 1; i++)
            {
                var previous = repaired[i - 1];
                var current = repaired[i];
                var next = repaired[i + 1];

                var firstLeg = Distance2D(previous, current);
                var secondLeg = Distance2D(current, next);
                var bridge = Distance2D(previous, next);
                if (firstLeg > ShortHorizontalDetourSpikeMaxLegLength
                    || secondLeg > ShortHorizontalDetourSpikeMaxLegLength
                    || bridge > ShortHorizontalDetourSpikeMaxBridgeLength
                    || bridge <= 0.01f)
                {
                    continue;
                }

                if (MathF.Abs(next.Z - previous.Z) > ShortHorizontalDetourSpikeMaxBridgeZDelta)
                    continue;

                var detourRatio = (firstLeg + secondLeg) / bridge;
                if (detourRatio < ShortHorizontalDetourSpikeMinDetourRatio)
                    continue;

                if (!HasLineOfSightStrict(mapId, previous, next))
                    continue;

                if (!IsDynamicOverlayBridgeClear(mapId, previous, next, agentRadius))
                    continue;

                var validation = ValidateSegmentForAgent(mapId, previous, next, agentRadius, agentHeight);
                if (!IsLocallyWalkable(validation, previous, next))
                    continue;

                if (TryClassifyAffordance(mapId, previous, next, agentRadius, agentHeight, out var affordance)
                    && affordance.Affordance is not (NativeSegmentAffordance.Walk
                        or NativeSegmentAffordance.StepUp
                        or NativeSegmentAffordance.SafeDrop))
                {
                    continue;
                }

                repaired.RemoveAt(i);
                scanEnd = Math.Min(repaired.Count - 1, maxWaypointIndex);
                i = Math.Max(0, i - 2);
            }

            return repaired.Count == path.Length ? path : repaired.ToArray();
        }

        private static XYZ[] RemoveLargeVerticalLayerExcursions(
            uint mapId,
            XYZ[] path,
            float agentRadius,
            float agentHeight,
            int maxWaypointIndex,
            out int repairCount)
        {
            repairCount = 0;
            if (path.Length < 4)
                return path;

            var repaired = path.ToList();
            var scanEnd = Math.Min(repaired.Count - 1, maxWaypointIndex);
            for (var i = 0; i < scanEnd - 2 && i < repaired.Count - 2; i++)
            {
                var start = repaired[i];
                var maxEnd = Math.Min(
                    Math.Min(repaired.Count - 1, scanEnd),
                    i + LargeVerticalLayerExcursionMaxInteriorWaypoints + 1);

                for (var endIndex = i + 2; endIndex <= maxEnd; endIndex++)
                {
                    var end = repaired[endIndex];
                    if (Distance2D(start, end) > LargeVerticalLayerExcursionMaxBridgeLength)
                        continue;

                    if (MathF.Abs(end.Z - start.Z) > LargeVerticalLayerExcursionMaxEndpointZDelta)
                        continue;

                    if (!ContainsLargeVerticalLayerExcursion(repaired, i, endIndex))
                        continue;

                    if (!HasLineOfSightStrict(mapId, start, end))
                        continue;

                    if (!IsAffordanceRepairLegWalkable(mapId, start, end, agentRadius, agentHeight))
                        continue;

                    repaired.RemoveRange(i + 1, endIndex - i - 1);
                    repairCount++;
                    scanEnd = Math.Min(repaired.Count - 1, maxWaypointIndex);
                    i = Math.Max(-1, i - 2);
                    break;
                }
            }

            if (repairCount > 0)
            {
                Console.Error.WriteLine(
                    $"[CORRIDOR-LAYER-EXCURSION] removed {repairCount} stacked-layer excursion(s) pathLen={path.Length} repairedLen={repaired.Count}");
            }

            return repairCount == 0
                ? path
                : CollapseDuplicateWaypoints(repaired.ToArray());
        }

        private static bool ContainsLargeVerticalLayerExcursion(
            IReadOnlyList<XYZ> path,
            int startIndex,
            int endIndex)
        {
            var start = path[startIndex];
            var end = path[endIndex];
            var lowerEndpoint = MathF.Min(start.Z, end.Z);
            var upperEndpoint = MathF.Max(start.Z, end.Z);

            for (var i = startIndex + 1; i < endIndex; i++)
            {
                var z = path[i].Z;
                if (z - upperEndpoint >= LargeVerticalLayerExcursionMinDelta ||
                    lowerEndpoint - z >= LargeVerticalLayerExcursionMinDelta)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool IsDynamicOverlayBridgeClear(uint mapId, XYZ from, XYZ to, float agentRadius)
        {
            if (!HasActiveDynamicObjectOverlay())
                return true;

            if (SegmentIntersectsDynamicObjectsNative(mapId, from.X, from.Y, from.Z, to.X, to.Y, to.Z))
                return false;

            var dx = to.X - from.X;
            var dy = to.Y - from.Y;
            var horizontal = MathF.Sqrt((dx * dx) + (dy * dy));
            if (horizontal <= 0.01f)
                return true;

            var perpX = -dy / horizontal;
            var perpY = dx / horizontal;
            foreach (var offset in EnumerateDynamicOverlayClearanceOffsets(agentRadius))
            {
                var leftFrom = new XYZ(from.X + (perpX * offset), from.Y + (perpY * offset), from.Z);
                var leftTo = new XYZ(to.X + (perpX * offset), to.Y + (perpY * offset), to.Z);
                if (SegmentIntersectsDynamicObjectsNative(mapId, leftFrom.X, leftFrom.Y, leftFrom.Z, leftTo.X, leftTo.Y, leftTo.Z))
                    return false;

                var rightFrom = new XYZ(from.X - (perpX * offset), from.Y - (perpY * offset), from.Z);
                var rightTo = new XYZ(to.X - (perpX * offset), to.Y - (perpY * offset), to.Z);
                if (SegmentIntersectsDynamicObjectsNative(mapId, rightFrom.X, rightFrom.Y, rightFrom.Z, rightTo.X, rightTo.Y, rightTo.Z))
                    return false;
            }

            return true;
        }

        private static bool TryGetNearbyGroundZ(uint mapId, XYZ point, out float groundZ)
        {
            groundZ = float.NaN;
            try
            {
                var value = GetGroundZ(mapId, point.X, point.Y, point.Z, maxSearchDist: 4.0f);
                if (!float.IsFinite(value) || value <= -100000f)
                    return false;

                groundZ = value;
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static XYZ[] NormalizeLocalPhysicsReachableLayers(
            uint mapId,
            XYZ[] path,
            float agentRadius,
            float agentHeight,
            out int projectionCount,
            int maxScanSegments)
        {
            projectionCount = 0;
            if (path.Length < 2)
                return path;

            var normalized = path.ToList();
            for (var pass = 0; pass < LocalPhysicsLayerProjectionMaxPasses; pass++)
            {
                var changedThisPass = false;
                var scanEnd = Math.Min(normalized.Count - 1, Math.Max(0, maxScanSegments));
                for (var i = 0; i < scanEnd && i < normalized.Count - 1; i++)
                {
                    var from = normalized[i];
                    var to = normalized[i + 1];
                    if (!ShouldProbeLocalPhysicsLayerProjection(normalized, i))
                        continue;

                    var simulation = SimulateLocalPhysicsSegment(mapId, from, to, agentRadius, agentHeight);
                    if (!ShouldProjectToLocalPhysicsLayer(
                            mapId,
                            normalized,
                            i,
                            simulation,
                            agentRadius,
                            agentHeight,
                            out var projected))
                        continue;

                    normalized[i + 1] = projected;
                    projectionCount++;
                    changedThisPass = true;
                }

                if (!changedThisPass)
                    break;
            }

            if (projectionCount > 0)
            {
                NavigationPerformanceMetrics.RecordLocalPhysicsLayerRepair(projectionCount);
                Console.Error.WriteLine(
                    $"[CORRIDOR-LOCAL-PHYSICS-LAYER] projected {projectionCount} waypoint layer(s) pathLen={path.Length}");
            }

            return projectionCount == 0
                ? path
                : CollapseDuplicateWaypoints(normalized.ToArray());
        }

        private static bool ShouldProbeLocalPhysicsLayerProjection(IReadOnlyList<XYZ> path, int segmentIndex)
        {
            if (segmentIndex < 0 || segmentIndex >= path.Count - 1)
                return false;

            var from = path[segmentIndex];
            var to = path[segmentIndex + 1];
            var horizontal = Distance2D(from, to);
            if (horizontal < LocalPhysicsLayerProjectionMinSegmentLength ||
                horizontal > LocalPhysicsLayerProjectionMaxSegmentLength)
            {
                return false;
            }

            return true;
        }

        private static bool ShouldProjectToLocalPhysicsLayer(
            uint mapId,
            IReadOnlyList<XYZ> path,
            int segmentIndex,
            LocalPhysicsSimulation simulation,
            float agentRadius,
            float agentHeight,
            out XYZ projected)
        {
            projected = path[segmentIndex + 1];
            var diagnostics = IsLocalPhysicsLayerDiagnosticsEnabled();
            if (!simulation.Available)
            {
                if (diagnostics)
                    Console.Error.WriteLine($"[LOCAL-LAYER-DBG] seg={segmentIndex} reject=simulation_unavailable");
                return false;
            }

            if (!simulation.Compatible)
            {
                if (diagnostics)
                    Console.Error.WriteLine($"[LOCAL-LAYER-DBG] seg={segmentIndex} reject=simulation_{simulation.Reason}");
                return false;
            }

            var from = path[segmentIndex];
            var to = path[segmentIndex + 1];
            var endpointDistance = Distance2D(simulation.FinalPosition, to);
            var downwardDelta = to.Z - simulation.FinalPosition.Z;
            if (endpointDistance > LocalPhysicsLayerProjectionMaxEndpointDistance)
            {
                if (diagnostics && downwardDelta >= LocalPhysicsLayerProjectionMinDownwardZDelta)
                {
                    Console.Error.WriteLine(
                        $"[LOCAL-LAYER-DBG] seg={segmentIndex} reject=endpoint_distance endpoint={endpointDistance:F2} down={downwardDelta:F2} from=({from.X:F1},{from.Y:F1},{from.Z:F1}) to=({to.X:F1},{to.Y:F1},{to.Z:F1}) final=({simulation.FinalPosition.X:F1},{simulation.FinalPosition.Y:F1},{simulation.FinalPosition.Z:F1})");
                }
                return false;
            }

            if (downwardDelta < LocalPhysicsLayerProjectionMinDownwardZDelta ||
                downwardDelta > LocalPhysicsLayerProjectionMaxDownwardZDelta)
            {
                if (diagnostics && downwardDelta >= LocalPhysicsLayerProjectionMinDownwardZDelta * 0.5f)
                {
                    Console.Error.WriteLine(
                        $"[LOCAL-LAYER-DBG] seg={segmentIndex} reject=downward_delta endpoint={endpointDistance:F2} down={downwardDelta:F2} from=({from.X:F1},{from.Y:F1},{from.Z:F1}) to=({to.X:F1},{to.Y:F1},{to.Z:F1}) final=({simulation.FinalPosition.X:F1},{simulation.FinalPosition.Y:F1},{simulation.FinalPosition.Z:F1})");
                }
                return false;
            }

            if (simulation.MaxUpwardRouteZDelta > LocalPhysicsRouteLayerRejectZDelta ||
                (simulation.MaxLateralDistance > LocalPhysicsRouteLateralRejectDistance &&
                    simulation.MaxAbsoluteRouteZDelta > LocalPhysicsRouteLayerRejectZDelta))
            {
                if (diagnostics)
                {
                    Console.Error.WriteLine(
                        $"[LOCAL-LAYER-DBG] seg={segmentIndex} reject=route_layer_mismatch up={simulation.MaxUpwardRouteZDelta:F2} abs={simulation.MaxAbsoluteRouteZDelta:F2} lat={simulation.MaxLateralDistance:F2}");
                }
                return false;
            }

            var projectedZ = simulation.FinalPosition.Z + EarlySupportGroundClearance;
            var supportProbe = new XYZ(to.X, to.Y, projectedZ);
            if (TryGetNearbyGroundZ(mapId, supportProbe, out var supportZ) &&
                MathF.Abs(supportZ - simulation.FinalPosition.Z) <= LocalPhysicsLayerProjectionSupportSnapTolerance)
            {
                projectedZ = supportZ + EarlySupportGroundClearance;
            }

            if (projectedZ >= to.Z - LocalPhysicsLayerProjectionMinAppliedZDelta)
            {
                if (diagnostics)
                {
                    Console.Error.WriteLine(
                        $"[LOCAL-LAYER-DBG] seg={segmentIndex} reject=projected_not_lower projectedZ={projectedZ:F2} toZ={to.Z:F2} down={downwardDelta:F2}");
                }
                return false;
            }

            if (WouldCreateShortVerticalLayerSpikeProjection(path.ToArray(), segmentIndex + 1, projectedZ))
            {
                if (diagnostics)
                    Console.Error.WriteLine($"[LOCAL-LAYER-DBG] seg={segmentIndex} reject=short_vertical_spike projectedZ={projectedZ:F2}");
                return false;
            }

            projected = new XYZ(to.X, to.Y, projectedZ);
            var validation = ValidateSegmentForAgent(mapId, from, projected, agentRadius, agentHeight);
            if (!IsLocallyWalkable(validation, from, projected))
            {
                if (diagnostics)
                    Console.Error.WriteLine($"[LOCAL-LAYER-DBG] seg={segmentIndex} reject=validation code={validation} projected=({projected.X:F1},{projected.Y:F1},{projected.Z:F1})");
                return false;
            }

            if (diagnostics)
            {
                Console.Error.WriteLine(
                    $"[LOCAL-LAYER-DBG] seg={segmentIndex} project down={downwardDelta:F2} endpoint={endpointDistance:F2} from=({from.X:F1},{from.Y:F1},{from.Z:F1}) to=({to.X:F1},{to.Y:F1},{to.Z:F1}) projected=({projected.X:F1},{projected.Y:F1},{projected.Z:F1})");
            }

            return true;
        }

        private static LocalPhysicsSimulation SimulateLocalPhysicsSegment(
            uint mapId,
            XYZ from,
            XYZ to,
            float agentRadius,
            float agentHeight)
        {
            var dx = to.X - from.X;
            var dy = to.Y - from.Y;
            var dz = to.Z - from.Z;
            var segmentDistance2D = MathF.Sqrt((dx * dx) + (dy * dy));
            if (segmentDistance2D <= 0.05f)
                return new LocalPhysicsSimulation(true, true, 0f, 0f, 0f, 1f, 0, 0, 0, from, "compatible");

            var travelDistance = MathF.Min(segmentDistance2D, MathF.Max(0.5f, LocalPhysicsSimulationMaxDistance));
            var horizonT = travelDistance / segmentDistance2D;
            var horizon = new XYZ(
                from.X + (dx * horizonT),
                from.Y + (dy * horizonT),
                from.Z + (dz * horizonT));
            var orientation = MathF.Atan2(dy, dx);
            var stepCount = Math.Clamp(
                (int)MathF.Ceiling(travelDistance / MathF.Max(LocalPhysicsRunSpeed * LocalPhysicsSimulationDeltaTime, 0.05f)) + 6,
                4,
                96);

            var pos = new XYZ(from.X, from.Y, from.Z);
            var velX = 0f;
            var velY = 0f;
            var velZ = 0f;
            var prevGroundZ = from.Z;
            var prevGroundNx = 0f;
            var prevGroundNy = 0f;
            var prevGroundNz = 1f;
            var pendingDepenX = 0f;
            var pendingDepenY = 0f;
            var pendingDepenZ = 0f;
            var standingOnInstanceId = 0u;
            var standingOnLocalX = 0f;
            var standingOnLocalY = 0f;
            var standingOnLocalZ = 0f;
            var fallTime = 0f;
            var fallStartZ = from.Z;
            var stepUpBaseZ = -200000f;
            var stepUpAge = 0u;
            var wasGrounded = true;
            var moveFlags = MoveFlagForward;
            var maxUpwardZDelta = 0f;
            var maxAbsZDelta = 0f;
            var maxLateralDistance = 0f;
            var hitWall = false;
            var wallContactSteps = 0;
            var lowDisplacementWallSteps = 0;
            var consecutiveLowProgressSteps = 0;
            var maxConsecutiveLowProgressSteps = 0;
            var totalRequestedDistance = 0f;
            var totalAchievedDistance = 0f;
            var bestProjectionT = 0f;

            try
            {
                for (var step = 0; step < stepCount; step++)
                {
                    var previousPos = pos;
                    var previousProjectionT = bestProjectionT;
                    var input = new NativePhysicsInput
                    {
                        MoveFlags = moveFlags | MoveFlagForward,
                        X = pos.X,
                        Y = pos.Y,
                        Z = pos.Z,
                        Orientation = orientation,
                        Pitch = 0f,
                        Vx = velX,
                        Vy = velY,
                        Vz = velZ,
                        WalkSpeed = 2.5f,
                        RunSpeed = LocalPhysicsRunSpeed,
                        RunBackSpeed = 4.5f,
                        SwimSpeed = 4.722222f,
                        SwimBackSpeed = 2.5f,
                        FlightSpeed = LocalPhysicsRunSpeed,
                        TurnSpeed = MathF.PI,
                        TransportGuid = 0,
                        TransportX = 0f,
                        TransportY = 0f,
                        TransportZ = 0f,
                        TransportO = 0f,
                        FallTime = (uint)fallTime,
                        FallStartZ = fallStartZ != 0f ? fallStartZ : -200000f,
                        Height = agentHeight,
                        Radius = agentRadius,
                        HasSplinePath = false,
                        SplineSpeed = 0f,
                        SplinePoints = IntPtr.Zero,
                        SplinePointCount = 0,
                        CurrentSplineIndex = 0,
                        PrevGroundZ = prevGroundZ,
                        PrevGroundNx = prevGroundNx,
                        PrevGroundNy = prevGroundNy,
                        PrevGroundNz = prevGroundNz,
                        PendingDepenX = pendingDepenX,
                        PendingDepenY = pendingDepenY,
                        PendingDepenZ = pendingDepenZ,
                        StandingOnInstanceId = standingOnInstanceId,
                        StandingOnLocalX = standingOnLocalX,
                        StandingOnLocalY = standingOnLocalY,
                        StandingOnLocalZ = standingOnLocalZ,
                        NearbyObjects = IntPtr.Zero,
                        NearbyObjectCount = 0,
                        MapId = mapId,
                        DeltaTime = LocalPhysicsSimulationDeltaTime,
                        FrameCounter = (uint)step,
                        PhysicsFlags = 0,
                        StepUpBaseZ = stepUpBaseZ,
                        StepUpAge = stepUpAge,
                        GroundedWallState = 0,
                        WasGrounded = wasGrounded ? 1u : 0u,
                    };

                    var output = PhysicsStepV2(ref input);
                    pos = new XYZ(output.X, output.Y, output.Z);
                    velX = output.Vx;
                    velY = output.Vy;
                    velZ = output.Vz;
                    moveFlags = output.MoveFlags | MoveFlagForward;
                    fallTime = output.FallTime;
                    fallStartZ = output.FallStartZ;
                    prevGroundZ = output.GroundZ;
                    prevGroundNx = output.GroundNx;
                    prevGroundNy = output.GroundNy;
                    prevGroundNz = output.GroundNz;
                    pendingDepenX = output.PendingDepenX;
                    pendingDepenY = output.PendingDepenY;
                    pendingDepenZ = output.PendingDepenZ;
                    standingOnInstanceId = output.StandingOnInstanceId;
                    standingOnLocalX = output.StandingOnLocalX;
                    standingOnLocalY = output.StandingOnLocalY;
                    standingOnLocalZ = output.StandingOnLocalZ;
                    stepUpBaseZ = output.StepUpBaseZ;
                    stepUpAge = output.StepUpAge;
                    wasGrounded = (output.MoveFlags & (MoveFlagFallingFar | MoveFlagJumping)) == 0;
                    hitWall |= output.HitWall &&
                        float.IsFinite(output.BlockedFraction) &&
                        output.BlockedFraction < LocalPhysicsBlockingWallProgressThreshold;
                    if (output.HitWall)
                        wallContactSteps++;

                    var requestedDistance = LocalPhysicsRunSpeed * LocalPhysicsSimulationDeltaTime;
                    var achievedDistance = Distance2D(previousPos, pos);
                    totalRequestedDistance += requestedDistance;
                    totalAchievedDistance += achievedDistance;
                    if (output.HitWall &&
                        requestedDistance > 0.001f &&
                        achievedDistance / requestedDistance < LocalPhysicsLowDisplacementRatio)
                    {
                        lowDisplacementWallSteps++;
                    }

                    var projectionT = Math.Clamp(
                        (((pos.X - from.X) * dx) + ((pos.Y - from.Y) * dy)) / (segmentDistance2D * segmentDistance2D),
                        0f,
                        1f);
                    if (projectionT > bestProjectionT)
                        bestProjectionT = projectionT;

                    var forwardProgress = MathF.Max(0f, projectionT - previousProjectionT) * segmentDistance2D;
                    if (forwardProgress <= LocalPhysicsMovementStallProgressEpsilon)
                    {
                        consecutiveLowProgressSteps++;
                        maxConsecutiveLowProgressSteps = Math.Max(
                            maxConsecutiveLowProgressSteps,
                            consecutiveLowProgressSteps);
                    }
                    else
                    {
                        consecutiveLowProgressSteps = 0;
                    }

                    var expectedZ = from.Z + (dz * projectionT);
                    var zDelta = pos.Z - expectedZ;
                    maxUpwardZDelta = MathF.Max(maxUpwardZDelta, zDelta);
                    maxAbsZDelta = MathF.Max(maxAbsZDelta, MathF.Abs(zDelta));

                    var projectedX = from.X + (dx * projectionT);
                    var projectedY = from.Y + (dy * projectionT);
                    var lateralX = pos.X - projectedX;
                    var lateralY = pos.Y - projectedY;
                    maxLateralDistance = MathF.Max(
                        maxLateralDistance,
                        MathF.Sqrt((lateralX * lateralX) + (lateralY * lateralY)));

                    if (Distance2D(pos, horizon) <= LocalPhysicsLayerProjectionMaxEndpointDistance)
                        break;
                }
            }
            catch
            {
                return new LocalPhysicsSimulation(false, true, 0f, 0f, 0f, 1f, 0, 0, 0, from, "unavailable");
            }

            var averageDisplacementRatio = totalRequestedDistance > 0.001f
                ? totalAchievedDistance / totalRequestedDistance
                : 1f;
            var movementStall = IsSustainedLocalPhysicsMovementStall(
                averageDisplacementRatio,
                wallContactSteps,
                lowDisplacementWallSteps,
                maxConsecutiveLowProgressSteps);

            return new LocalPhysicsSimulation(
                true,
                !hitWall && !movementStall,
                maxUpwardZDelta,
                maxAbsZDelta,
                maxLateralDistance,
                averageDisplacementRatio,
                wallContactSteps,
                lowDisplacementWallSteps,
                maxConsecutiveLowProgressSteps,
                pos,
                hitWall ? "hit_wall" : movementStall ? "movement_stall" : "simulated");
        }

        private static bool IsSustainedLocalPhysicsMovementStall(
            float averageDisplacementRatio,
            int wallContactSteps,
            int lowDisplacementWallSteps,
            int maxConsecutiveLowProgressSteps)
            => wallContactSteps >= LocalPhysicsMovementStallMinWallSteps &&
                lowDisplacementWallSteps >= LocalPhysicsMovementStallMinLowDisplacementWallSteps &&
                maxConsecutiveLowProgressSteps >= LocalPhysicsMovementStallMinConsecutiveLowProgressSteps &&
                averageDisplacementRatio < LocalPhysicsMovementStallAverageRatio;

        private static XYZ[] RepairEarlyStaticBreaks(
            uint mapId,
            XYZ[] path,
            float agentRadius,
            float agentHeight,
            out int repairCount,
            out int? firstBlockedIdx,
            out string firstBlockedReason,
            int maxScanSegments = EarlyStaticRepairScanLimit,
            TimeSpan? repairBudgetOverride = null,
            bool allowRouteRepair = true)
        {
            repairCount = 0;
            firstBlockedIdx = null;
            firstBlockedReason = "none";
            if (path.Length < 2)
                return path;

            var repaired = path.ToList();
            var stopwatch = Stopwatch.StartNew();
            var repairBudget = repairBudgetOverride ?? EarlyStaticRepairBudget;
            var scanLimit = Math.Max(0, maxScanSegments);
            var scanEnd = Math.Min(repaired.Count - 1, scanLimit);
            for (var i = 0; i < scanEnd && i < repaired.Count - 1; i++)
            {
                if (stopwatch.Elapsed > repairBudget)
                    break;

                var from = repaired[i];
                var to = repaired[i + 1];
                if (!RequiresLocalStaticRepair(mapId, from, to, agentRadius, agentHeight, out var reason))
                    continue;

                if (TryBuildLocalStaticRepairPoint(mapId, from, to, agentRadius, agentHeight, out var repairPoint))
                {
                    repaired.Insert(i + 1, repairPoint);
                    repairCount++;
                    scanEnd = Math.Min(repaired.Count - 1, scanLimit);
                    i++;
                    continue;
                }

                if (allowRouteRepair && TryBuildLocalStaticRepairRoute(
                    mapId,
                    repaired,
                    i,
                    agentRadius,
                    agentHeight,
                    stopwatch,
                    repairBudget,
                    out var repairedRoute))
                {
                    repaired = repairedRoute.ToList();
                    repairCount++;
                    scanEnd = Math.Min(repaired.Count - 1, scanLimit);
                    i = Math.Max(-1, i - 1);
                    continue;
                }

                if (stopwatch.Elapsed > repairBudget)
                    break;

                firstBlockedIdx = i;
                firstBlockedReason = reason;
                break;
            }

            if (repairCount > 0)
            {
                NavigationPerformanceMetrics.RecordStaticWallRepair(repairCount);
                Console.Error.WriteLine(
                    $"[CORRIDOR-STATIC-REPAIR] repaired {repairCount} early static/capsule segment(s) pathLen={path.Length} repairedLen={repaired.Count}");
            }

            return repaired.ToArray();
        }

        private static XYZ[] RepairAffordanceBreaks(
            uint mapId,
            XYZ[] path,
            float agentRadius,
            float agentHeight,
            out int repairCount,
            out int? firstBlockedIdx,
            out string firstBlockedReason,
            bool includeCumulativeBreaks = false,
            bool includeSegmentBreaks = true,
            int maxScanSegments = int.MaxValue,
            bool includeLocalPhysicsReachabilityBreaks = false)
        {
            repairCount = 0;
            firstBlockedIdx = null;
            firstBlockedReason = "none";
            if (path.Length < 3)
                return path;

            var repaired = path.ToList();
            var stopwatch = Stopwatch.StartNew();

            while (repairCount < AffordanceRepairMaxRepairs &&
                   stopwatch.Elapsed <= AffordanceRepairBudget)
            {
                var repairedThisPass = false;
                int? pendingBlockedIdx = null;
                var pendingBlockedReason = "none";
                var scanEnd = Math.Min(repaired.Count - 1, Math.Max(0, maxScanSegments));
                for (var i = 0; i < scanEnd && i < repaired.Count - 1 && stopwatch.Elapsed <= AffordanceRepairBudget; i++)
                {
                    var requiresRepair = false;
                    var reason = "none";
                    if (includeSegmentBreaks)
                    {
                        requiresRepair = RequiresAffordanceRepair(
                            mapId,
                            repaired[i],
                            repaired[i + 1],
                            agentRadius,
                            agentHeight,
                            out reason,
                            includeLocalPhysicsReachabilityBreaks);
                    }
                    if (!requiresRepair && includeCumulativeBreaks)
                    {
                        requiresRepair = TryFindCumulativeAffordanceBreak(
                            mapId,
                            repaired,
                            i,
                            agentRadius,
                            agentHeight,
                            out reason);
                    }

                    if (!requiresRepair)
                    {
                        continue;
                    }

                    var cumulativeRepair = string.Equals(reason, "cumulative_steep_climb", StringComparison.Ordinal);
                    var localPhysicsRepair = string.Equals(reason, "local_physics_layer", StringComparison.Ordinal) ||
                        string.Equals(reason, "local_physics_movement", StringComparison.Ordinal);
                    var allowNativeAffordanceLegRepair = cumulativeRepair ||
                        string.Equals(reason, "steep_climb", StringComparison.Ordinal) ||
                        string.Equals(reason, "step_up_limit", StringComparison.Ordinal) ||
                        localPhysicsRepair;
                    var lookBehindDistance = cumulativeRepair
                        ? AffordanceRepairCumulativeLookBehindDistance
                        : localPhysicsRepair
                            ? AffordanceRepairLocalLookBehindDistance
                        : AffordanceRepairLookBehindDistance;
                    var lookAheadDistance = cumulativeRepair
                        ? AffordanceRepairCumulativeLookAheadDistance
                        : localPhysicsRepair
                            ? AffordanceRepairLocalLookAheadDistance
                        : AffordanceRepairLookAheadDistance;
                    var windowStart = FindWindowStart(repaired, i, lookBehindDistance);
                    var windowEnd = FindWindowEnd(repaired, i + 1, lookAheadDistance);
                    if (windowEnd <= windowStart + 1 ||
                        Distance2D(repaired[windowStart], repaired[windowEnd]) < AffordanceRepairMinWindowLength)
                    {
                        pendingBlockedIdx ??= i;
                        if (pendingBlockedReason == "none")
                            pendingBlockedReason = reason;
                        continue;
                    }

                    if (IsAffordanceRepairDiagnosticsEnabled())
                    {
                        Console.Error.WriteLine(
                            $"[AFFORDANCE-DBG] segment={i} reason={reason} window={windowStart}->{windowEnd} allowNative={allowNativeAffordanceLegRepair} pathLen={repaired.Count} from=({repaired[windowStart].X:F1},{repaired[windowStart].Y:F1},{repaired[windowStart].Z:F1}) to=({repaired[windowEnd].X:F1},{repaired[windowEnd].Y:F1},{repaired[windowEnd].Z:F1})");
                    }

                    if (TryBuildAffordanceRepairRoute(
                        mapId,
                        repaired,
                        windowStart,
                        windowEnd,
                        agentRadius,
                        agentHeight,
                        stopwatch,
                        allowNativeLegRepair: allowNativeAffordanceLegRepair,
                        out var repairedRoute))
                    {
                        if (!IsMeaningfullyDifferentPath(repaired, repairedRoute))
                        {
                            pendingBlockedIdx ??= i;
                            if (pendingBlockedReason == "none")
                                pendingBlockedReason = reason;
                            continue;
                        }

                        repaired = repairedRoute.ToList();
                        repairCount++;
                        repairedThisPass = true;
                        pendingBlockedIdx = null;
                        pendingBlockedReason = "none";
                        Console.Error.WriteLine(
                            $"[CORRIDOR-AFFORDANCE-REPAIR] segment={i} reason={reason} window={windowStart}->{windowEnd} pathLen={path.Length} repairedLen={repaired.Count}");
                        break;
                    }

                    pendingBlockedIdx ??= i;
                    if (pendingBlockedReason == "none")
                        pendingBlockedReason = reason;
                }

                if (!repairedThisPass && pendingBlockedIdx.HasValue)
                {
                    firstBlockedIdx ??= pendingBlockedIdx;
                    firstBlockedReason = pendingBlockedReason;
                }

                if (!repairedThisPass)
                    break;
            }

            if (repairCount > 0)
            {
                NavigationPerformanceMetrics.RecordSteepAffordanceRepair(repairCount);
                Console.Error.WriteLine(
                    $"[CORRIDOR-AFFORDANCE-REPAIR] repaired {repairCount} steep/blocked segment window(s) pathLen={path.Length} repairedLen={repaired.Count}");
            }

            return repaired.ToArray();
        }

        private static bool RequiresAffordanceRepair(
            uint mapId,
            XYZ from,
            XYZ to,
            float agentRadius,
            float agentHeight,
            out string reason,
            bool includeLocalPhysicsReachabilityBreak = false)
        {
            reason = "none";
            var horizontal = Distance2D(from, to);
            if (horizontal <= AffordanceRepairMinSegmentLength)
                return false;

            if (includeLocalPhysicsReachabilityBreak &&
                IsLocalPhysicsReachabilityBreak(mapId, from, to, agentRadius, agentHeight))
            {
                reason = "local_physics_movement";
                return true;
            }

            var rise = to.Z - from.Z;
            if (rise < AffordanceRepairMinCandidateRise ||
                rise / horizontal < AffordanceRepairMinCandidateSlopeRatio)
            {
                return false;
            }

            var slopeAngleDeg = MathF.Atan2(rise, horizontal) * (180f / MathF.PI);
            if (slopeAngleDeg > 45.0f && rise >= 3.0f)
            {
                reason = "cumulative_steep_climb";
                return true;
            }

            if (!TryClassifyAffordance(mapId, from, to, agentRadius, agentHeight, out var affordance))
                return false;

            var validation = (SegmentValidationCode)affordance.ValidationCode;
            if (affordance.Affordance == NativeSegmentAffordance.SteepClimb)
            {
                reason = "steep_climb";
                return true;
            }

            if (validation == SegmentValidationCode.StepUpTooHigh &&
                affordance.ClimbHeight >= 1.0f &&
                affordance.SlopeAngleDeg >= 45.0f)
            {
                reason = "step_up_limit";
                return true;
            }

            if (affordance.Affordance == NativeSegmentAffordance.Blocked &&
                affordance.ClimbHeight >= 1.0f &&
                affordance.SlopeAngleDeg >= 52.0f)
            {
                reason = "steep_climb";
                return true;
            }

            return false;
        }

        private static bool IsLocalPhysicsReachabilityBreak(
            uint mapId,
            XYZ from,
            XYZ to,
            float agentRadius,
            float agentHeight)
        {
            if (agentRadius <= 0f || agentHeight <= 0f)
                return false;

            var horizontal = Distance2D(from, to);
            if (horizontal < LocalPhysicsLayerProjectionMinSegmentLength ||
                horizontal > LocalPhysicsLayerProjectionMaxSegmentLength)
            {
                return false;
            }

            var rise = to.Z - from.Z;
            if (horizontal <= 1.25f && rise >= 1.5f)
                return true;

            if (rise < AffordanceRepairMinCandidateRise)
                return false;

            var simulation = SimulateLocalPhysicsSegment(mapId, from, to, agentRadius, agentHeight);
            if (!simulation.Available || !simulation.Compatible)
                return true;

            if (Distance2D(simulation.FinalPosition, to) > LocalPhysicsLayerProjectionMaxEndpointDistance)
                return true;

            return MathF.Abs(simulation.FinalPosition.Z - to.Z) > EarlySupportProjectionReachableMaxZDelta;
        }

        private static bool FindFirstLocalPhysicsReachabilityBreak(
            uint mapId,
            IReadOnlyList<XYZ> path,
            float agentRadius,
            float agentHeight,
            int maxScanSegments,
            out int blockedSegmentIndex)
        {
            blockedSegmentIndex = -1;
            if (path.Count < 2)
                return false;

            var scanEnd = Math.Min(path.Count - 1, Math.Max(0, maxScanSegments));
            for (var i = 0; i < scanEnd; i++)
            {
                if (!IsLocalPhysicsReachabilityBreak(mapId, path[i], path[i + 1], agentRadius, agentHeight))
                    continue;

                blockedSegmentIndex = i;
                return true;
            }

            return false;
        }

        private static XYZ[] RepairLocalPhysicsReachabilityBreaks(
            uint mapId,
            XYZ[] path,
            float agentRadius,
            float agentHeight,
            out int repairCount,
            int maxScanSegments)
        {
            repairCount = 0;
            if (path.Length < 3)
                return path;

            var repaired = path.ToList();
            var stopwatch = Stopwatch.StartNew();
            var diagnostics = IsLocalPhysicsLayerDiagnosticsEnabled();
            var scanEnd = Math.Min(repaired.Count - 1, Math.Max(0, maxScanSegments));
            for (var i = 0; i < scanEnd && i < repaired.Count - 1; i++)
            {
                if (repairCount >= LocalPhysicsReachabilityRepairMaxRepairs ||
                    stopwatch.Elapsed > LocalPhysicsReachabilityRepairBudget)
                {
                    break;
                }

                var from = repaired[i];
                var to = repaired[i + 1];
                if (!IsLocalPhysicsReachabilityBreak(mapId, from, to, agentRadius, agentHeight))
                    continue;

                if (diagnostics)
                {
                    Console.Error.WriteLine(
                        $"[LOCAL-REPAIR-DBG] seg={i} from=({from.X:F1},{from.Y:F1},{from.Z:F1}) to=({to.X:F1},{to.Y:F1},{to.Z:F1}) pathLen={repaired.Count}");
                }

                var next = i + 2 < repaired.Count ? repaired[i + 2] : (XYZ?)null;
                if (TryBuildLocalPhysicsReachabilityProgressPoint(
                        mapId,
                        from,
                        to,
                        next,
                        agentRadius,
                        agentHeight,
                        out var progressPoint))
                {
                    if (diagnostics)
                    {
                        Console.Error.WriteLine(
                            $"[LOCAL-REPAIR-DBG] seg={i} insert_progress=({progressPoint.X:F1},{progressPoint.Y:F1},{progressPoint.Z:F1})");
                    }

                    repaired.Insert(i + 1, progressPoint);
                    repairCount++;
                    scanEnd = Math.Min(repaired.Count - 1, Math.Max(0, maxScanSegments));
                    i = Math.Max(-1, i - 2);
                    continue;
                }

                if (TryBuildLocalPhysicsReachabilityLateralBridgePoint(
                        mapId,
                        from,
                        to,
                        agentRadius,
                        agentHeight,
                        out var lateralBridgePoint))
                {
                    if (diagnostics)
                    {
                        Console.Error.WriteLine(
                            $"[LOCAL-REPAIR-DBG] seg={i} insert_lateral_bridge=({lateralBridgePoint.X:F1},{lateralBridgePoint.Y:F1},{lateralBridgePoint.Z:F1})");
                    }

                    repaired.Insert(i + 1, lateralBridgePoint);
                    repairCount++;
                    scanEnd = Math.Min(repaired.Count - 1, Math.Max(0, maxScanSegments));
                    i = Math.Max(-1, i - 2);
                    continue;
                }

                if (TryBuildLocalPhysicsReachabilityRouteProgressPoint(
                        mapId,
                        from,
                        to,
                        next,
                        agentRadius,
                        agentHeight,
                        out var routeProgressPoint))
                {
                    if (diagnostics)
                    {
                        Console.Error.WriteLine(
                            $"[LOCAL-REPAIR-DBG] seg={i} insert_route_progress=({routeProgressPoint.X:F1},{routeProgressPoint.Y:F1},{routeProgressPoint.Z:F1})");
                    }

                    repaired.Insert(i + 1, routeProgressPoint);
                    repairCount++;
                    scanEnd = Math.Min(repaired.Count - 1, Math.Max(0, maxScanSegments));
                    i = Math.Max(-1, i - 2);
                    continue;
                }

                if (TryBuildLocalPhysicsReachabilityBridgePoint(
                        mapId,
                        from,
                        to,
                        agentRadius,
                        agentHeight,
                        out var bridgePoint))
                {
                    if (diagnostics)
                    {
                        Console.Error.WriteLine(
                            $"[LOCAL-REPAIR-DBG] seg={i} insert_bridge=({bridgePoint.X:F1},{bridgePoint.Y:F1},{bridgePoint.Z:F1})");
                    }

                    repaired.Insert(i + 1, bridgePoint);
                    repairCount++;
                    scanEnd = Math.Min(repaired.Count - 1, Math.Max(0, maxScanSegments));
                    i = Math.Max(-1, i - 2);
                    continue;
                }

                if (TryFindLocalPhysicsReachableDownstreamAnchor(
                        mapId,
                        repaired,
                        i,
                        agentRadius,
                        agentHeight,
                        out var downstreamAnchorIndex))
                {
                    if (diagnostics)
                        Console.Error.WriteLine($"[LOCAL-REPAIR-DBG] seg={i} remove_to_anchor={downstreamAnchorIndex}");

                    repaired.RemoveRange(i + 1, downstreamAnchorIndex - i - 1);
                    repairCount++;
                    scanEnd = Math.Min(repaired.Count - 1, Math.Max(0, maxScanSegments));
                    i = Math.Max(-1, i - 2);
                    continue;
                }

                if (!next.HasValue)
                {
                    if (diagnostics)
                        Console.Error.WriteLine($"[LOCAL-REPAIR-DBG] seg={i} reject=no_next_anchor");

                    continue;
                }

                if (!TryBuildLocalPhysicsReachabilityRepairPoint(
                        mapId,
                        from,
                        to,
                        next,
                        agentRadius,
                        agentHeight,
                        out var replacement))
                {
                    if (diagnostics)
                        Console.Error.WriteLine($"[LOCAL-REPAIR-DBG] seg={i} reject=no_repair_candidate");

                    continue;
                }

                if (diagnostics)
                {
                    Console.Error.WriteLine(
                        $"[LOCAL-REPAIR-DBG] seg={i} replace=({replacement.X:F1},{replacement.Y:F1},{replacement.Z:F1})");
                }

                repaired[i + 1] = replacement;
                repairCount++;
                scanEnd = Math.Min(repaired.Count - 1, Math.Max(0, maxScanSegments));
                i = Math.Max(-1, i - 2);
            }

            if (repairCount > 0)
            {
                NavigationPerformanceMetrics.RecordLocalPhysicsLayerRepair(repairCount);
                Console.Error.WriteLine(
                    $"[CORRIDOR-LOCAL-PHYSICS-REPAIR] repaired {repairCount} local-physics segment(s) pathLen={path.Length} repairedLen={repaired.Count}");
            }

            return repairCount == 0
                ? path
                : CollapseDuplicateWaypoints(repaired.ToArray());
        }

        private static bool TryBuildLocalPhysicsReachabilityProgressPoint(
            uint mapId,
            XYZ from,
            XYZ rejectedTo,
            XYZ? next,
            float agentRadius,
            float agentHeight,
            out XYZ progressPoint)
        {
            progressPoint = from;
            var horizontal = Distance2D(from, rejectedTo);
            var rise = rejectedTo.Z - from.Z;
            if (horizontal < LocalPhysicsLayerProjectionMinSegmentLength ||
                horizontal > LocalPhysicsLayerProjectionMaxSegmentLength ||
                rise < AffordanceRepairMinCandidateRise)
            {
                return false;
            }

            foreach (var desired in EnumerateLocalPhysicsReachabilityProgressCandidates(from, rejectedTo, next))
            {
                if (!TryFindNearbyWalkablePoint(
                        mapId,
                        desired,
                        searchRadius: MathF.Max(3.0f, agentRadius + 2.0f),
                        maxHorizontalOffset: 2.5f,
                        maxVerticalOffset: MathF.Max(2.0f, rise + 1.0f),
                        out var snapped))
                {
                    continue;
                }

                if (Distance2D(from, snapped) <= CombineWaypointEpsilon ||
                    Distance2D(rejectedTo, snapped) <= CombineWaypointEpsilon ||
                    Distance2D(rejectedTo, snapped) > LocalPhysicsReachabilityRepairMaxDistance)
                {
                    continue;
                }

                var alongRejectedSegment = (((snapped.X - from.X) * (rejectedTo.X - from.X)) +
                    ((snapped.Y - from.Y) * (rejectedTo.Y - from.Y))) / horizontal;
                if (!next.HasValue && alongRejectedSegment < -LocalPhysicsReachabilityProgressMaxBacktrack)
                    continue;

                if (next.HasValue)
                {
                    var routeHorizontal = Distance2D(from, next.Value);
                    if (routeHorizontal > 0.001f)
                    {
                        var alongRoute = (((snapped.X - from.X) * (next.Value.X - from.X)) +
                            ((snapped.Y - from.Y) * (next.Value.Y - from.Y))) / routeHorizontal;
                        if (alongRoute < -LocalPhysicsReachabilityProgressMaxBacktrack)
                            continue;
                    }
                }

                if (snapped.Z - from.Z < LocalPhysicsReachabilityProgressMinRise ||
                    snapped.Z > rejectedTo.Z + LocalPhysicsReachabilityProgressMaxOvershoot)
                {
                    continue;
                }

                var routeAnchor = next ?? rejectedTo;
                if (DistancePointToSegment2D(snapped, from, routeAnchor) > LocalPhysicsReachabilityProgressMaxLateralDeviation)
                    continue;

                if (Distance2D(from, snapped) >= EarlyStaticRepairLosMinSegmentLength &&
                    !HasLineOfSightStrict(mapId, from, snapped))
                {
                    continue;
                }

                if (Distance2D(snapped, rejectedTo) >= EarlyStaticRepairLosMinSegmentLength &&
                    !HasLineOfSightStrict(mapId, snapped, rejectedTo))
                {
                    continue;
                }

                if (!IsAffordanceRepairLegWalkable(mapId, from, snapped, agentRadius, agentHeight))
                    continue;

                progressPoint = snapped;
                return true;
            }

            return false;
        }

        private static IEnumerable<XYZ> EnumerateLocalPhysicsReachabilityProgressCandidates(
            XYZ from,
            XYZ rejectedTo,
            XYZ? next)
        {
            var (dirX, dirY) = ResolveLocalPhysicsReachabilityRepairDirection(from, rejectedTo, null);
            var perpX = -dirY;
            var perpY = dirX;
            var rise = MathF.Max(0.0f, rejectedTo.Z - from.Z);

            foreach (var sample in LocalPhysicsReachabilityBridgeSamples)
            {
                yield return new XYZ(
                    from.X + ((rejectedTo.X - from.X) * sample),
                    from.Y + ((rejectedTo.Y - from.Y) * sample),
                    from.Z + (rise * sample));
            }

            foreach (var distance in LocalPhysicsReachabilityProgressDistances)
            {
                var desiredZ = from.Z + MathF.Min(rise, MathF.Max(LocalPhysicsReachabilityProgressMinRise, distance * 0.4f));
                foreach (var (offsetX, offsetY) in EnumerateLocalPhysicsReachabilityProgressOffsets(dirX, dirY, perpX, perpY))
                {
                    yield return new XYZ(
                        from.X + (offsetX * distance),
                        from.Y + (offsetY * distance),
                        desiredZ);
                }
            }

            if (next.HasValue)
            {
                var (routeDirX, routeDirY) = ResolveLocalPhysicsReachabilityRepairDirection(from, rejectedTo, next);
                var routePerpX = -routeDirY;
                var routePerpY = routeDirX;
                foreach (var distance in LocalPhysicsReachabilityProgressDistances)
                {
                    var desiredZ = from.Z + MathF.Min(rise, MathF.Max(LocalPhysicsReachabilityProgressMinRise, distance * 0.4f));
                    yield return new XYZ(from.X + (routePerpX * distance), from.Y + (routePerpY * distance), desiredZ);
                    yield return new XYZ(from.X - (routePerpX * distance), from.Y - (routePerpY * distance), desiredZ);
                }
            }
        }

        private static IEnumerable<(float X, float Y)> EnumerateLocalPhysicsReachabilityProgressOffsets(
            float dirX,
            float dirY,
            float perpX,
            float perpY)
        {
            yield return (perpX, perpY);
            yield return (-perpX, -perpY);
            yield return (dirX, dirY);
            yield return (-dirX, -dirY);

            var diagonalScale = 0.70710677f;
            yield return ((dirX + perpX) * diagonalScale, (dirY + perpY) * diagonalScale);
            yield return ((dirX - perpX) * diagonalScale, (dirY - perpY) * diagonalScale);
            yield return ((-dirX + perpX) * diagonalScale, (-dirY + perpY) * diagonalScale);
            yield return ((-dirX - perpX) * diagonalScale, (-dirY - perpY) * diagonalScale);
        }

        private static bool TryBuildLocalPhysicsReachabilityLateralBridgePoint(
            uint mapId,
            XYZ from,
            XYZ rejectedTo,
            float agentRadius,
            float agentHeight,
            out XYZ bridgePoint)
        {
            bridgePoint = from;
            var horizontal = Distance2D(from, rejectedTo);
            if (horizontal < LocalPhysicsLayerProjectionMinSegmentLength ||
                horizontal > LocalPhysicsLayerProjectionMaxSegmentLength)
            {
                return false;
            }

            var dirX = (rejectedTo.X - from.X) / horizontal;
            var dirY = (rejectedTo.Y - from.Y) / horizontal;
            var perpX = -dirY;
            var perpY = dirX;

            foreach (var sample in LocalPhysicsReachabilityBridgeSamples)
            {
                var along = new XYZ(
                    from.X + ((rejectedTo.X - from.X) * sample),
                    from.Y + ((rejectedTo.Y - from.Y) * sample),
                    from.Z + ((rejectedTo.Z - from.Z) * sample));

                foreach (var lateralOffset in LocalPhysicsReachabilityBridgeLateralOffsets)
                {
                    var desired = new XYZ(
                        along.X + (perpX * lateralOffset),
                        along.Y + (perpY * lateralOffset),
                        along.Z);
                    if (!TryFindNearbyWalkablePoint(
                            mapId,
                            desired,
                            searchRadius: MathF.Max(4.0f, MathF.Abs(lateralOffset) + 1.5f),
                            maxHorizontalOffset: MathF.Max(2.0f, MathF.Abs(lateralOffset) + 0.75f),
                            maxVerticalOffset: 3.0f,
                            out var snapped))
                    {
                        continue;
                    }

                    if (Distance2D(from, snapped) <= CombineWaypointEpsilon ||
                        Distance2D(rejectedTo, snapped) <= CombineWaypointEpsilon)
                    {
                        continue;
                    }

                    if (Distance2D(from, snapped) >= EarlyStaticRepairLosMinSegmentLength &&
                        !HasLineOfSightStrict(mapId, from, snapped))
                    {
                        continue;
                    }

                    if (Distance2D(snapped, rejectedTo) >= EarlyStaticRepairLosMinSegmentLength &&
                        !HasLineOfSightStrict(mapId, snapped, rejectedTo))
                    {
                        continue;
                    }

                    if (!IsAffordanceRepairLegWalkable(mapId, from, snapped, agentRadius, agentHeight) ||
                        !IsAffordanceRepairLegWalkable(mapId, snapped, rejectedTo, agentRadius, agentHeight))
                    {
                        continue;
                    }

                    bridgePoint = snapped;
                    return true;
                }
            }

            return false;
        }

        private static bool TryBuildLocalPhysicsReachabilityRouteProgressPoint(
            uint mapId,
            XYZ from,
            XYZ rejectedTo,
            XYZ? next,
            float agentRadius,
            float agentHeight,
            out XYZ progressPoint)
        {
            progressPoint = from;
            var rejectedHorizontal = Distance2D(from, rejectedTo);
            if (rejectedHorizontal < LocalPhysicsLayerProjectionMinSegmentLength ||
                rejectedHorizontal > LocalPhysicsLayerProjectionMaxSegmentLength)
            {
                return false;
            }

            var routeAnchor = next ?? rejectedTo;
            var routeHorizontal = Distance2D(from, routeAnchor);
            if (routeHorizontal < LocalPhysicsLayerProjectionMinSegmentLength)
                return false;

            foreach (var desired in EnumerateLocalPhysicsReachabilityRouteProgressCandidates(from, rejectedTo, routeAnchor))
            {
                if (!TryFindNearbyWalkablePoint(
                        mapId,
                        desired,
                        searchRadius: MathF.Max(4.0f, agentRadius + 2.0f),
                        maxHorizontalOffset: 2.75f,
                        maxVerticalOffset: MathF.Max(2.0f, LocalPhysicsReachabilityRouteProgressMaxZDelta),
                        out var snapped))
                {
                    continue;
                }

                if (Distance2D(from, snapped) <= CombineWaypointEpsilon ||
                    Distance2D(rejectedTo, snapped) <= CombineWaypointEpsilon ||
                    (next.HasValue && Distance2D(next.Value, snapped) <= CombineWaypointEpsilon))
                {
                    continue;
                }

                var snappedDistance = Distance2D(from, snapped);
                if (snappedDistance > LocalPhysicsSimulationMaxDistance ||
                    MathF.Abs(snapped.Z - from.Z) > LocalPhysicsReachabilityRouteProgressMaxZDelta)
                {
                    continue;
                }

                var alongRoute = (((snapped.X - from.X) * (routeAnchor.X - from.X)) +
                    ((snapped.Y - from.Y) * (routeAnchor.Y - from.Y))) / routeHorizontal;
                if (alongRoute < LocalPhysicsReachabilityRouteProgressMinAdvance ||
                    alongRoute > routeHorizontal + LocalPhysicsReachabilityRouteProgressMaxOvershoot)
                {
                    continue;
                }

                if (Distance2D(snapped, routeAnchor) >
                    routeHorizontal - LocalPhysicsReachabilityRouteProgressMinImprovement)
                {
                    continue;
                }

                if (DistancePointToSegment2D(snapped, from, routeAnchor) >
                    LocalPhysicsReachabilityProgressMaxLateralDeviation)
                {
                    continue;
                }

                if (snappedDistance >= EarlyStaticRepairLosMinSegmentLength &&
                    !HasLineOfSightStrict(mapId, from, snapped))
                {
                    continue;
                }

                if (!IsAffordanceRepairLegWalkable(mapId, from, snapped, agentRadius, agentHeight))
                    continue;

                progressPoint = snapped;
                return true;
            }

            return false;
        }

        private static IEnumerable<XYZ> EnumerateLocalPhysicsReachabilityRouteProgressCandidates(
            XYZ from,
            XYZ rejectedTo,
            XYZ routeAnchor)
        {
            var rejectedHorizontal = Distance2D(from, rejectedTo);
            if (rejectedHorizontal > 0.001f)
            {
                var rejectedDirX = (rejectedTo.X - from.X) / rejectedHorizontal;
                var rejectedDirY = (rejectedTo.Y - from.Y) / rejectedHorizontal;
                var rejectedPerpX = -rejectedDirY;
                var rejectedPerpY = rejectedDirX;

                foreach (var sample in LocalPhysicsReachabilityBridgeSamples)
                {
                    var along = new XYZ(
                        from.X + ((rejectedTo.X - from.X) * sample),
                        from.Y + ((rejectedTo.Y - from.Y) * sample),
                        from.Z + ((rejectedTo.Z - from.Z) * sample));
                    yield return along;

                    foreach (var lateralOffset in LocalPhysicsReachabilityBridgeLateralOffsets)
                    {
                        yield return new XYZ(
                            along.X + (rejectedPerpX * lateralOffset),
                            along.Y + (rejectedPerpY * lateralOffset),
                            along.Z);
                    }
                }
            }

            var routeHorizontal = Distance2D(from, routeAnchor);
            if (routeHorizontal <= 0.001f)
                yield break;

            var routeDirX = (routeAnchor.X - from.X) / routeHorizontal;
            var routeDirY = (routeAnchor.Y - from.Y) / routeHorizontal;
            var routePerpX = -routeDirY;
            var routePerpY = routeDirX;

            foreach (var sample in LocalPhysicsReachabilityBridgeSamples)
            {
                if (routeHorizontal * sample >
                    LocalPhysicsSimulationMaxDistance + LocalPhysicsReachabilityRouteProgressMaxOvershoot)
                {
                    continue;
                }

                yield return new XYZ(
                    from.X + ((routeAnchor.X - from.X) * sample),
                    from.Y + ((routeAnchor.Y - from.Y) * sample),
                    from.Z + ((routeAnchor.Z - from.Z) * sample));
            }

            foreach (var distance in LocalPhysicsReachabilityProgressDistances)
            {
                var clampedDistance = MathF.Min(
                    distance,
                    routeHorizontal + LocalPhysicsReachabilityRouteProgressMaxOvershoot);
                var t = Math.Clamp(clampedDistance / routeHorizontal, 0.0f, 1.0f);
                var center = new XYZ(
                    from.X + (routeDirX * clampedDistance),
                    from.Y + (routeDirY * clampedDistance),
                    from.Z + ((routeAnchor.Z - from.Z) * t));
                yield return center;

                foreach (var lateralOffset in LocalPhysicsReachabilityBridgeLateralOffsets)
                {
                    yield return new XYZ(
                        center.X + (routePerpX * lateralOffset),
                        center.Y + (routePerpY * lateralOffset),
                        center.Z);
                }
            }
        }

        private static bool TryBuildLocalPhysicsReachabilityBridgePoint(
            uint mapId,
            XYZ from,
            XYZ rejectedTo,
            float agentRadius,
            float agentHeight,
            out XYZ bridgePoint)
        {
            bridgePoint = from;
            foreach (var sample in LocalPhysicsReachabilityBridgeSamples)
            {
                var desired = new XYZ(
                    from.X + ((rejectedTo.X - from.X) * sample),
                    from.Y + ((rejectedTo.Y - from.Y) * sample),
                    from.Z + ((rejectedTo.Z - from.Z) * sample));
                var query = new XYZ(desired.X, desired.Y, from.Z);
                if (!TryFindNearbyWalkablePoint(
                        mapId,
                        query,
                        searchRadius: MathF.Max(4.0f, agentRadius + 2.0f),
                        maxHorizontalOffset: 3.0f,
                        maxVerticalOffset: 8.0f,
                        out var snapped))
                {
                    continue;
                }

                if (Distance2D(from, snapped) <= agentRadius + 0.5f ||
                    Distance2D(snapped, rejectedTo) <= agentRadius + 0.5f)
                {
                    continue;
                }

                if (!IsAffordanceRepairLegWalkable(mapId, from, snapped, agentRadius, agentHeight) ||
                    !IsAffordanceRepairLegWalkable(mapId, snapped, rejectedTo, agentRadius, agentHeight))
                {
                    continue;
                }

                bridgePoint = snapped;
                return true;
            }

            return false;
        }

        private static bool TryFindLocalPhysicsReachableDownstreamAnchor(
            uint mapId,
            IReadOnlyList<XYZ> path,
            int blockedSegmentIndex,
            float agentRadius,
            float agentHeight,
            out int downstreamAnchorIndex)
        {
            downstreamAnchorIndex = -1;
            var from = path[blockedSegmentIndex];
            var maxAnchorIndex = Math.Min(path.Count - 1, blockedSegmentIndex + 4);
            for (var anchorIndex = blockedSegmentIndex + 2; anchorIndex <= maxAnchorIndex; anchorIndex++)
            {
                var anchor = path[anchorIndex];
                if (Distance2D(from, anchor) > LocalPhysicsSimulationMaxDistance)
                    continue;

                if (Distance2D(from, anchor) >= EarlyStaticRepairLosMinSegmentLength &&
                    !HasLineOfSightStrict(mapId, from, anchor))
                {
                    continue;
                }

                if (!IsAffordanceRepairLegWalkable(mapId, from, anchor, agentRadius, agentHeight))
                    continue;

                downstreamAnchorIndex = anchorIndex;
                return true;
            }

            return false;
        }

        private static bool TryBuildLocalPhysicsReachabilityRepairPoint(
            uint mapId,
            XYZ from,
            XYZ rejectedTo,
            XYZ? next,
            float agentRadius,
            float agentHeight,
            out XYZ replacement)
        {
            replacement = rejectedTo;
            foreach (var desired in EnumerateLocalPhysicsReachabilityRepairCandidates(from, rejectedTo, next))
            {
                var query = new XYZ(desired.X, desired.Y, from.Z);
                if (!TryFindNearbyWalkablePoint(
                        mapId,
                        query,
                        searchRadius: MathF.Max(LocalPhysicsReachabilityRepairMaxDistance, agentRadius + 2.0f),
                        maxHorizontalOffset: LocalPhysicsReachabilityRepairMaxDistance,
                        maxVerticalOffset: 8.0f,
                        out var snapped))
                {
                    continue;
                }

                if (Distance2D(from, snapped) <= agentRadius + 0.5f ||
                    Distance2D(rejectedTo, snapped) > LocalPhysicsReachabilityRepairMaxDistance)
                {
                    continue;
                }

                if (Distance2D(from, snapped) >= EarlyStaticRepairLosMinSegmentLength &&
                    !HasLineOfSightStrict(mapId, from, snapped))
                {
                    continue;
                }

                if (!IsAffordanceRepairLegWalkable(mapId, from, snapped, agentRadius, agentHeight))
                    continue;

                if (next.HasValue)
                {
                    var nextValue = next.Value;
                    if (Distance2D(snapped, nextValue) <= agentRadius + 0.5f)
                        continue;

                    if (Distance2D(snapped, nextValue) >= EarlyStaticRepairLosMinSegmentLength &&
                        !HasLineOfSightStrict(mapId, snapped, nextValue))
                    {
                        continue;
                    }

                    if (!IsAffordanceRepairLegWalkable(mapId, snapped, nextValue, agentRadius, agentHeight))
                        continue;
                }

                replacement = snapped;
                return true;
            }

            return false;
        }

        private static IEnumerable<XYZ> EnumerateLocalPhysicsReachabilityRepairCandidates(
            XYZ from,
            XYZ rejectedTo,
            XYZ? next)
        {
            var (dirX, dirY) = ResolveLocalPhysicsReachabilityRepairDirection(from, rejectedTo, next);
            var perpX = -dirY;
            var perpY = dirX;

            foreach (var distance in LocalPhysicsReachabilityRepairDistances)
            {
                yield return new XYZ(rejectedTo.X + (perpX * distance), rejectedTo.Y + (perpY * distance), rejectedTo.Z);
                yield return new XYZ(rejectedTo.X - (perpX * distance), rejectedTo.Y - (perpY * distance), rejectedTo.Z);
                yield return new XYZ(rejectedTo.X + (dirX * distance), rejectedTo.Y + (dirY * distance), rejectedTo.Z);
                yield return new XYZ(rejectedTo.X - (dirX * distance), rejectedTo.Y - (dirY * distance), rejectedTo.Z);

                var diagonal = distance * 0.70710677f;
                yield return new XYZ(rejectedTo.X + ((dirX + perpX) * diagonal), rejectedTo.Y + ((dirY + perpY) * diagonal), rejectedTo.Z);
                yield return new XYZ(rejectedTo.X + ((dirX - perpX) * diagonal), rejectedTo.Y + ((dirY - perpY) * diagonal), rejectedTo.Z);
                yield return new XYZ(rejectedTo.X + ((-dirX + perpX) * diagonal), rejectedTo.Y + ((-dirY + perpY) * diagonal), rejectedTo.Z);
                yield return new XYZ(rejectedTo.X + ((-dirX - perpX) * diagonal), rejectedTo.Y + ((-dirY - perpY) * diagonal), rejectedTo.Z);
            }
        }

        private static (float X, float Y) ResolveLocalPhysicsReachabilityRepairDirection(
            XYZ from,
            XYZ rejectedTo,
            XYZ? next)
        {
            var dx = next.HasValue ? next.Value.X - from.X : rejectedTo.X - from.X;
            var dy = next.HasValue ? next.Value.Y - from.Y : rejectedTo.Y - from.Y;
            var length = MathF.Sqrt((dx * dx) + (dy * dy));
            if (length > 0.001f)
                return (dx / length, dy / length);

            if (next.HasValue)
            {
                dx = next.Value.X - rejectedTo.X;
                dy = next.Value.Y - rejectedTo.Y;
                length = MathF.Sqrt((dx * dx) + (dy * dy));
                if (length > 0.001f)
                    return (dx / length, dy / length);
            }

            return (1.0f, 0.0f);
        }

        private static bool TryFindCumulativeAffordanceBreak(
            uint mapId,
            IReadOnlyList<XYZ> path,
            int startIndex,
            float agentRadius,
            float agentHeight,
            out string reason)
        {
            reason = "none";
            if (startIndex < 0 || startIndex >= path.Count - 2)
                return false;

            var start = path[startIndex];
            var maxEndIndex = Math.Min(path.Count - 1, startIndex + AffordanceRepairCumulativeMaxSegments);
            for (var endIndex = startIndex + 2; endIndex <= maxEndIndex; endIndex++)
            {
                var end = path[endIndex];
                var rise = end.Z - start.Z;
                if (rise < AffordanceRepairCumulativeMinRise)
                    continue;

                var horizontal = Distance2D(start, end);
                if (horizontal < AffordanceRepairCumulativeMinHorizontal ||
                    horizontal > AffordanceRepairCumulativeMaxHorizontal ||
                    rise / horizontal < AffordanceRepairCumulativeMinSlopeRatio)
                {
                    continue;
                }

                if (HasLargeDropInsideWindow(path, startIndex, endIndex))
                    continue;

                var validation = ValidateSegmentForAgent(mapId, start, end, agentRadius, agentHeight);
                if (!IsLocallyWalkable(validation, start, end))
                {
                    reason = "cumulative_steep_climb";
                    return true;
                }

                if (TryClassifyAffordance(mapId, start, end, agentRadius, agentHeight, out var affordance) &&
                    affordance.Affordance is NativeSegmentAffordance.SteepClimb or NativeSegmentAffordance.Blocked)
                {
                    reason = "cumulative_steep_climb";
                    return true;
                }

                reason = "steep_climb";
                return true;
            }

            return false;
        }

        private static bool HasLargeDropInsideWindow(IReadOnlyList<XYZ> path, int startIndex, int endIndex)
        {
            for (var i = startIndex; i < endIndex; i++)
            {
                if (path[i].Z - path[i + 1].Z >= AffordanceRepairMinCandidateRise)
                    return true;
            }

            return false;
        }

        private static bool ShouldDeferDownstreamAffordanceBlock(IReadOnlyList<XYZ> path, int blockedSegmentIndex)
        {
            if (path.Count < 2 || blockedSegmentIndex <= AffordanceRepairStraightScanLimit)
                return false;

            return Distance2D(path[0], path[^1]) >= SmoothFallbackAfterStraightStaticBreakMinDistance;
        }

        private static int FindWindowStart(IReadOnlyList<XYZ> path, int segmentIndex, float lookBehindDistance)
        {
            var index = Math.Clamp(segmentIndex, 0, path.Count - 1);
            var remaining = lookBehindDistance;
            while (index > 0 && remaining > 0.0f)
            {
                remaining -= Distance2D(path[index - 1], path[index]);
                index--;
            }

            return index;
        }

        private static int FindWindowEnd(IReadOnlyList<XYZ> path, int segmentEndIndex, float lookAheadDistance)
        {
            var index = Math.Clamp(segmentEndIndex, 0, path.Count - 1);
            var remaining = lookAheadDistance;
            while (index < path.Count - 1 && remaining > 0.0f)
            {
                remaining -= Distance2D(path[index], path[index + 1]);
                index++;
            }

            return index;
        }

        private static bool TryBuildAffordanceRepairRoute(
            uint mapId,
            IReadOnlyList<XYZ> path,
            int windowStart,
            int windowEnd,
            float agentRadius,
            float agentHeight,
            Stopwatch stopwatch,
            bool allowNativeLegRepair,
            out XYZ[] repairedRoute)
        {
            repairedRoute = [];
            var from = path[windowStart];
            var to = path[windowEnd];
            var dx = to.X - from.X;
            var dy = to.Y - from.Y;
            var length = MathF.Sqrt((dx * dx) + (dy * dy));
            if (length <= 0.01f)
                return false;

            var perpX = -dy / length;
            var perpY = dx / length;
            var found = false;
            var bestScore = float.NegativeInfinity;
            var bestRoute = Array.Empty<XYZ>();
            var candidateSearchBudget = allowNativeLegRepair
                ? AffordanceRepairNativeLegDirectSearchBudget
                : AffordanceRepairBudget;

            foreach (var along in AffordanceRepairAlongSamples)
            {
                var basePoint = new XYZ(
                    from.X + (dx * along),
                    from.Y + (dy * along),
                    from.Z + ((to.Z - from.Z) * along));

                foreach (var lateral in AffordanceRepairLateralOffsets)
                {
                    if (stopwatch.Elapsed > candidateSearchBudget)
                    {
                        if (found)
                            return FinishBestRoute(bestRoute, out repairedRoute);

                        return TryBuildNativeAffordanceRepairRoute(
                            mapId,
                            path,
                            windowStart,
                            windowEnd,
                            agentRadius,
                            agentHeight,
                            allowNativeLegRepair,
                            out repairedRoute);
                    }

                    var absLateral = MathF.Abs(lateral);
                    var desired = new XYZ(
                        basePoint.X + (perpX * lateral),
                        basePoint.Y + (perpY * lateral),
                        basePoint.Z);

                    if (!TryFindNearbyWalkablePoint(
                        mapId,
                        desired,
                        searchRadius: MathF.Max(8f, absLateral + 4f),
                        maxHorizontalOffset: MathF.Max(6f, absLateral + 3f),
                        maxVerticalOffset: 12f,
                        out var candidate))
                    {
                        continue;
                    }

                    if (Distance2D(from, candidate) <= agentRadius + 0.5f ||
                        Distance2D(candidate, to) <= agentRadius + 0.5f)
                    {
                        continue;
                    }

                    if (!IsAffordanceRepairLegWalkable(mapId, from, candidate, agentRadius, agentHeight) ||
                        !IsAffordanceRepairLegWalkable(mapId, candidate, to, agentRadius, agentHeight))
                    {
                        continue;
                    }

                    var interiorSeparation = MinimumInteriorSeparationFromDetour(path, windowStart, windowEnd, candidate);
                    if (interiorSeparation < AffordanceRepairMinInteriorSeparation)
                        continue;

                    var detourLength = Distance2D(from, candidate) + Distance2D(candidate, to);
                    var directLength = MathF.Max(length, 0.01f);
                    var score = interiorSeparation - ((detourLength / directLength) * 0.15f);
                    if (found && score <= bestScore)
                        continue;

                    var composed = new List<XYZ>(path.Count - (windowEnd - windowStart) + 8);
                    for (var i = 0; i <= windowStart; i++)
                        AppendWaypointIfDistinct(composed, path[i]);

                    AppendDensifiedSegment(composed, path[windowStart], candidate, SmoothPathDensifySpacing);
                    AppendDensifiedSegment(composed, composed[^1], path[windowEnd], SmoothPathDensifySpacing);
                    for (var i = windowEnd + 1; i < path.Count; i++)
                        AppendWaypointIfDistinct(composed, path[i]);

                    found = true;
                    bestScore = score;
                    bestRoute = composed.ToArray();
                }
            }

            if (!found &&
                TryBuildNativeAffordanceRepairRoute(
                    mapId,
                    path,
                    windowStart,
                    windowEnd,
                    agentRadius,
                    agentHeight,
                    allowNativeLegRepair,
                    out repairedRoute))
            {
                return true;
            }

            foreach (var lateral in AffordanceRepairLateralOffsets)
            {
                if (stopwatch.Elapsed > candidateSearchBudget)
                {
                    if (found)
                        return FinishBestRoute(bestRoute, out repairedRoute);

                    return TryBuildNativeAffordanceRepairRoute(
                        mapId,
                        path,
                        windowStart,
                        windowEnd,
                        agentRadius,
                        agentHeight,
                        allowNativeLegRepair,
                        out repairedRoute);
                }

                for (var firstAlongIndex = 0; firstAlongIndex < AffordanceRepairAlongSamples.Length - 1; firstAlongIndex++)
                {
                    var firstAlong = AffordanceRepairAlongSamples[firstAlongIndex];
                    for (var secondAlongIndex = firstAlongIndex + 1; secondAlongIndex < AffordanceRepairAlongSamples.Length; secondAlongIndex++)
                    {
                        if (stopwatch.Elapsed > candidateSearchBudget)
                        {
                            if (found)
                                return FinishBestRoute(bestRoute, out repairedRoute);

                            return TryBuildNativeAffordanceRepairRoute(
                                mapId,
                                path,
                                windowStart,
                                windowEnd,
                                agentRadius,
                                agentHeight,
                                allowNativeLegRepair,
                                out repairedRoute);
                        }

                        var secondAlong = AffordanceRepairAlongSamples[secondAlongIndex];
                        if (secondAlong - firstAlong < 0.25f)
                            continue;

                        var firstDesired = BuildLateralAffordanceCandidate(from, to, perpX, perpY, lateral, firstAlong);
                        var secondDesired = BuildLateralAffordanceCandidate(from, to, perpX, perpY, lateral, secondAlong);
                        var absLateral = MathF.Abs(lateral);

                        if (!TryFindNearbyWalkablePoint(
                                mapId,
                                firstDesired,
                                searchRadius: MathF.Max(8f, absLateral + 4f),
                                maxHorizontalOffset: MathF.Max(6f, absLateral + 3f),
                                maxVerticalOffset: 12f,
                                out var firstCandidate) ||
                            !TryFindNearbyWalkablePoint(
                                mapId,
                                secondDesired,
                                searchRadius: MathF.Max(8f, absLateral + 4f),
                                maxHorizontalOffset: MathF.Max(6f, absLateral + 3f),
                                maxVerticalOffset: 12f,
                                out var secondCandidate))
                        {
                            continue;
                        }

                        if (Distance2D(from, firstCandidate) <= agentRadius + 0.5f ||
                            Distance2D(firstCandidate, secondCandidate) <= agentRadius + 0.5f ||
                            Distance2D(secondCandidate, to) <= agentRadius + 0.5f)
                        {
                            continue;
                        }

                        if (!IsAffordanceRepairLegWalkable(mapId, from, firstCandidate, agentRadius, agentHeight) ||
                            !IsAffordanceRepairLegWalkable(mapId, firstCandidate, secondCandidate, agentRadius, agentHeight) ||
                            !IsAffordanceRepairLegWalkable(mapId, secondCandidate, to, agentRadius, agentHeight))
                        {
                            continue;
                        }

                        var detour = new[] { firstCandidate, secondCandidate };
                        var interiorSeparation = MinimumInteriorSeparationFromDetour(path, windowStart, windowEnd, detour);
                        if (interiorSeparation < AffordanceRepairMinInteriorSeparation)
                            continue;

                        var detourLength = Distance2D(from, firstCandidate)
                            + Distance2D(firstCandidate, secondCandidate)
                            + Distance2D(secondCandidate, to);
                        var directLength = MathF.Max(length, 0.01f);
                        var score = interiorSeparation - ((detourLength / directLength) * 0.15f);
                        if (found && score <= bestScore)
                            continue;

                        var composed = new List<XYZ>(path.Count - (windowEnd - windowStart) + 16);
                        for (var i = 0; i <= windowStart; i++)
                            AppendWaypointIfDistinct(composed, path[i]);

                        AppendDensifiedSegment(composed, path[windowStart], firstCandidate, SmoothPathDensifySpacing);
                        AppendDensifiedSegment(composed, composed[^1], secondCandidate, SmoothPathDensifySpacing);
                        AppendDensifiedSegment(composed, composed[^1], path[windowEnd], SmoothPathDensifySpacing);
                        for (var i = windowEnd + 1; i < path.Count; i++)
                            AppendWaypointIfDistinct(composed, path[i]);

                        found = true;
                        bestScore = score;
                        bestRoute = composed.ToArray();
                    }
                }
            }

            return found && FinishBestRoute(bestRoute, out repairedRoute);
        }

        private static bool TryBuildNativeAffordanceRepairRoute(
            uint mapId,
            IReadOnlyList<XYZ> path,
            int windowStart,
            int windowEnd,
            float agentRadius,
            float agentHeight,
            bool allowNativeLegRepair,
            out XYZ[] repairedRoute)
        {
            repairedRoute = [];
            if (!allowNativeLegRepair)
                return false;

            var maxStart = Math.Min(windowEnd - 2, windowStart + AffordanceRepairNativeLegEndpointTrimLimit);
            for (var nativeStart = windowStart; nativeStart <= maxStart; nativeStart++)
            {
                if (TryBuildNativeAffordanceRepairRoute(
                        mapId,
                        path,
                        nativeStart,
                        windowEnd,
                        agentRadius,
                        agentHeight,
                        out repairedRoute))
                {
                    return true;
                }
            }

            var minEnd = Math.Max(windowStart + 2, windowEnd - AffordanceRepairNativeLegEndpointTrimLimit);
            for (var nativeEnd = windowEnd - 1; nativeEnd >= minEnd; nativeEnd--)
            {
                if (TryBuildNativeAffordanceRepairRoute(
                        mapId,
                        path,
                        windowStart,
                        nativeEnd,
                        agentRadius,
                        agentHeight,
                        out repairedRoute))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool TryBuildNativeAffordanceRepairRoute(
            uint mapId,
            IReadOnlyList<XYZ> path,
            int nativeStart,
            int nativeEnd,
            float agentRadius,
            float agentHeight,
            out XYZ[] repairedRoute)
        {
            repairedRoute = [];
            var from = path[nativeStart];
            var to = path[nativeEnd];
            if (Distance2D(from, to) > AffordanceRepairNativeLegMaxWindowLength)
                return false;

            if (!TryBuildAffordanceRepairLeg(
                    mapId,
                    from,
                    to,
                    agentRadius,
                    agentHeight,
                    allowNativeRoute: true,
                    out var nativeLeg))
            {
                return false;
            }

            var composed = new List<XYZ>(path.Count - (nativeEnd - nativeStart) + nativeLeg.Length);
            for (var i = 0; i <= nativeStart; i++)
                AppendWaypointIfDistinct(composed, path[i]);

            foreach (var waypoint in nativeLeg.Skip(1))
                AppendWaypointIfDistinct(composed, waypoint);

            for (var i = nativeEnd + 1; i < path.Count; i++)
                AppendWaypointIfDistinct(composed, path[i]);

            repairedRoute = composed.ToArray();
            return true;
        }

        private static XYZ BuildLateralAffordanceCandidate(
            XYZ from,
            XYZ to,
            float perpX,
            float perpY,
            float lateral,
            float along)
            => new(
                from.X + ((to.X - from.X) * along) + (perpX * lateral),
                from.Y + ((to.Y - from.Y) * along) + (perpY * lateral),
                from.Z + ((to.Z - from.Z) * along));

        private static bool FinishBestRoute(XYZ[] bestRoute, out XYZ[] repairedRoute)
        {
            repairedRoute = bestRoute;
            return repairedRoute.Length > 0;
        }

        private static bool TryBuildAffordanceRepairLeg(
            uint mapId,
            XYZ from,
            XYZ to,
            float agentRadius,
            float agentHeight,
            bool allowNativeRoute,
            out XYZ[] leg)
        {
            leg = [];
            if (IsAffordanceRepairLegWalkable(mapId, from, to, agentRadius, agentHeight))
            {
                leg = DensifyPath([from, to], SmoothPathDensifySpacing);
                return true;
            }

            if (!allowNativeRoute)
                return false;

            foreach (var smoothPath in new[] { true, false })
            {
                var nativePath = TryFindPathNative(mapId, from, to, smoothPath, agentRadius, agentHeight);
                if (nativePath.Length < 2)
                    continue;

                if (Distance3D(nativePath[0], from) > NativePathEndpointMaxDistance ||
                    Distance3D(nativePath[^1], to) > NativePathEndpointMaxDistance)
                {
                    continue;
                }

                var normalized = NormalizeEarlySupportLayer(
                    mapId,
                    DensifyPath(nativePath, SmoothPathDensifySpacing),
                    int.MaxValue,
                    agentRadius,
                    agentHeight);
                normalized = RemoveShortVerticalLayerSpikes(mapId, normalized, agentRadius, agentHeight, int.MaxValue);

                if (ContainsAffordanceBreak(mapId, normalized, agentRadius, agentHeight, includeLocalPhysicsReachabilityBreaks: true))
                    continue;

                if (!ValidateRepairSequence(mapId, normalized[0], normalized.Skip(1).ToArray(), agentRadius, agentHeight))
                    continue;

                leg = normalized;
                return true;
            }

            return false;
        }

        private static bool ContainsAffordanceBreak(
            uint mapId,
            IReadOnlyList<XYZ> path,
            float agentRadius,
            float agentHeight,
            bool includeLocalPhysicsReachabilityBreaks = false)
        {
            for (var i = 0; i < path.Count - 1; i++)
            {
                if (RequiresAffordanceRepair(
                        mapId,
                        path[i],
                        path[i + 1],
                        agentRadius,
                        agentHeight,
                        out _,
                        includeLocalPhysicsReachabilityBreaks))
                {
                    return true;
                }

                if (TryFindCumulativeAffordanceBreak(mapId, path, i, agentRadius, agentHeight, out _))
                    return true;
            }

            return false;
        }

        private static bool FindFirstStraightAffordanceBreak(
            uint mapId,
            IReadOnlyList<XYZ> path,
            float agentRadius,
            float agentHeight,
            int maxScanSegments,
            out int blockedSegmentIndex,
            out string blockedReason)
        {
            blockedSegmentIndex = -1;
            blockedReason = "none";
            var scanEnd = Math.Min(path.Count - 1, Math.Max(0, maxScanSegments));
            for (var i = 0; i < scanEnd; i++)
            {
                var from = path[i];
                var to = path[i + 1];
                if (RequiresLocalStaticRepair(mapId, from, to, agentRadius, agentHeight, out var staticReason))
                {
                    blockedSegmentIndex = i;
                    blockedReason = staticReason;
                    return true;
                }

                if (TryClassifyAffordance(mapId, from, to, agentRadius, agentHeight, out var affordance) &&
                    affordance.Affordance is NativeSegmentAffordance.SteepClimb
                        or NativeSegmentAffordance.Blocked
                        or NativeSegmentAffordance.Vertical)
                {
                    blockedSegmentIndex = i;
                    blockedReason = affordance.Affordance == NativeSegmentAffordance.Blocked
                        ? "blocked_geometry"
                        : "steep_climb";
                    return true;
                }

                if (TryFindCumulativeAffordanceBreak(mapId, path, i, agentRadius, agentHeight, out var cumulativeReason))
                {
                    blockedSegmentIndex = i;
                    blockedReason = cumulativeReason;
                    return true;
                }
            }

            return false;
        }

        private static bool IsAffordanceRepairLegWalkable(
            uint mapId,
            XYZ from,
            XYZ to,
            float agentRadius,
            float agentHeight)
        {
            var validation = ValidateSegmentForAgent(mapId, from, to, agentRadius, agentHeight);
            if (!IsLocallyWalkable(validation, from, to))
                return false;

            if (IsLocalPhysicsReachabilityBreak(mapId, from, to, agentRadius, agentHeight))
                return false;

            if (!TryClassifyAffordance(mapId, from, to, agentRadius, agentHeight, out var affordance))
                return false;

            return affordance.Affordance is NativeSegmentAffordance.Walk
                or NativeSegmentAffordance.StepUp
                or NativeSegmentAffordance.SafeDrop;
        }

        private static bool TryClassifyAffordance(
            uint mapId,
            XYZ from,
            XYZ to,
            float agentRadius,
            float agentHeight,
            out NativeSegmentAffordanceResult affordance)
        {
            try
            {
                affordance = ClassifySegmentAffordance(mapId, from, to, agentRadius, agentHeight);
                return true;
            }
            catch
            {
                affordance = default;
                return false;
            }
        }

        private static bool IsMeaningfullyDifferentPath(
            IReadOnlyList<XYZ> originalPath,
            IReadOnlyList<XYZ> candidatePath)
        {
            if (originalPath.Count != candidatePath.Count)
                return true;

            for (var i = 0; i < originalPath.Count; i++)
            {
                if (Distance3D(originalPath[i], candidatePath[i]) > CombineWaypointEpsilon)
                    return true;
            }

            return false;
        }

        private static float MinimumInteriorSeparationFromDetour(
            IReadOnlyList<XYZ> path,
            int windowStart,
            int windowEnd,
            XYZ candidate)
        {
            if (windowEnd <= windowStart + 1)
                return 0.0f;

            var from = path[windowStart];
            var to = path[windowEnd];
            var result = float.PositiveInfinity;
            for (var i = windowStart + 1; i < windowEnd; i++)
            {
                var firstLeg = DistancePointToSegment2D(path[i], from, candidate);
                var secondLeg = DistancePointToSegment2D(path[i], candidate, to);
                result = MathF.Min(result, MathF.Min(firstLeg, secondLeg));
            }

            return float.IsPositiveInfinity(result) ? 0.0f : result;
        }

        private static float MinimumInteriorSeparationFromDetour(
            IReadOnlyList<XYZ> path,
            int windowStart,
            int windowEnd,
            IReadOnlyList<XYZ> detour)
        {
            if (windowEnd <= windowStart + 1 || detour.Count == 0)
                return 0.0f;

            var route = new List<XYZ>(detour.Count + 2) { path[windowStart] };
            route.AddRange(detour);
            route.Add(path[windowEnd]);

            var result = float.PositiveInfinity;
            for (var i = windowStart + 1; i < windowEnd; i++)
            {
                for (var j = 0; j < route.Count - 1; j++)
                    result = MathF.Min(result, DistancePointToSegment2D(path[i], route[j], route[j + 1]));
            }

            return float.IsPositiveInfinity(result) ? 0.0f : result;
        }

        private static bool RequiresLocalStaticRepair(
            uint mapId,
            XYZ from,
            XYZ to,
            float agentRadius,
            float agentHeight,
            out string reason)
        {
            reason = "none";
            var horizontal = Distance2D(from, to);
            if (horizontal <= EarlyStaticRepairMinSegmentLength)
                return false;

            if (horizontal >= EarlyStaticRepairLosMinSegmentLength && !HasLineOfSightStrict(mapId, from, to))
            {
                reason = "static_los";
                return true;
            }

            if (horizontal <= EarlyStaticRepairValidationMaxLength && IsNativeSegmentValidationEnabled())
            {
                var validation = ValidateSegmentForAgent(mapId, from, to, agentRadius, agentHeight);
                if (validation is not SegmentValidationCode.Clear and not SegmentValidationCode.MissingSupport
                    && !IsInconsistentUphillStepDown(validation, from, to))
                {
                    reason = MapValidationCodeToReason(validation);
                    return true;
                }
            }

            return false;
        }

        private static bool TryBuildLocalStaticRepairPoint(
            uint mapId,
            XYZ from,
            XYZ to,
            float agentRadius,
            float agentHeight,
            out XYZ repairPoint)
        {
            repairPoint = from;
            var candidateCount = 0;
            foreach (var snapped in EnumerateLocalStaticRepairCandidates(mapId, from, to, agentRadius))
            {
                if (++candidateCount > LocalStaticRepairPointCandidateLimit)
                    break;

                if (!HasLineOfSightStrict(mapId, from, snapped)
                    || !HasLineOfSightStrict(mapId, snapped, to))
                {
                    continue;
                }

                repairPoint = snapped;
                return true;
            }

            return false;
        }

        private static IEnumerable<XYZ> EnumerateLocalStaticRepairCandidates(
            uint mapId,
            XYZ from,
            XYZ to,
            float agentRadius)
        {
            var dx = to.X - from.X;
            var dy = to.Y - from.Y;
            var length = MathF.Sqrt((dx * dx) + (dy * dy));
            if (length <= 0.01f)
                yield break;

            var dirX = dx / length;
            var dirY = dy / length;
            var perpX = -dirY;
            var perpY = dirX;

            foreach (var snapped in EnumerateLocalStaticEscapeCandidates(
                mapId,
                from,
                dirX,
                dirY,
                perpX,
                perpY,
                agentRadius))
            {
                yield return snapped;
            }

            foreach (var along in LocalStaticRepairAlongSamples)
            {
                var basePoint = new XYZ(
                    from.X + (dx * along),
                    from.Y + (dy * along),
                    from.Z + ((to.Z - from.Z) * along));

                foreach (var baseOffset in LocalStaticRepairBaseOffsets)
                {
                    var offset = MathF.Max(baseOffset, agentRadius + 0.75f);
                    foreach (var side in new[] { 1f, -1f })
                    {
                        var desired = new XYZ(
                            basePoint.X + (perpX * offset * side),
                            basePoint.Y + (perpY * offset * side),
                            basePoint.Z);

                        if (!TryFindNearbyWalkablePoint(
                            mapId,
                            desired,
                            searchRadius: MathF.Max(4f, offset + 2f),
                            maxHorizontalOffset: MathF.Max(3f, offset + 1f),
                            maxVerticalOffset: 6f,
                            out var snapped))
                        {
                            continue;
                        }

                        if (Distance2D(from, snapped) <= agentRadius
                            || Distance2D(snapped, to) <= agentRadius)
                        {
                            continue;
                        }

                        yield return snapped;
                    }
                }
            }
        }

        private static IEnumerable<XYZ> EnumerateLocalStaticEscapeCandidates(
            uint mapId,
            XYZ from,
            float dirX,
            float dirY,
            float perpX,
            float perpY,
            float agentRadius)
        {
            var directions = new (float X, float Y)[]
            {
                (-dirX, -dirY),
                (NormalizeX(-dirX + perpX, -dirY + perpY), NormalizeY(-dirX + perpX, -dirY + perpY)),
                (NormalizeX(-dirX - perpX, -dirY - perpY), NormalizeY(-dirX - perpX, -dirY - perpY)),
                (perpX, perpY),
                (-perpX, -perpY),
                (NormalizeX(dirX + perpX, dirY + perpY), NormalizeY(dirX + perpX, dirY + perpY)),
                (NormalizeX(dirX - perpX, dirY - perpY), NormalizeY(dirX - perpX, dirY - perpY)),
            };

            foreach (var distance in LocalStaticRepairEscapeDistances)
            {
                var offset = MathF.Max(distance, agentRadius + 0.75f);
                foreach (var direction in directions)
                {
                    if (MathF.Abs(direction.X) <= 0.001f && MathF.Abs(direction.Y) <= 0.001f)
                        continue;

                    var desired = new XYZ(
                        from.X + (direction.X * offset),
                        from.Y + (direction.Y * offset),
                        from.Z);

                    if (!TryFindNearbyWalkablePoint(
                        mapId,
                        desired,
                        searchRadius: MathF.Max(4f, offset + 2f),
                        maxHorizontalOffset: MathF.Max(3f, offset + 1f),
                        maxVerticalOffset: 6f,
                        out var snapped))
                    {
                        continue;
                    }

                    if (Distance2D(from, snapped) <= agentRadius)
                        continue;

                    yield return snapped;
                }
            }
        }

        private static float NormalizeX(float x, float y)
        {
            var length = MathF.Sqrt((x * x) + (y * y));
            return length <= 0.001f ? 0f : x / length;
        }

        private static float NormalizeY(float x, float y)
        {
            var length = MathF.Sqrt((x * x) + (y * y));
            return length <= 0.001f ? 0f : y / length;
        }

        private static bool TryBuildLocalStaticRepairRoute(
            uint mapId,
            IReadOnlyList<XYZ> path,
            int blockedSegmentIndex,
            float agentRadius,
            float agentHeight,
            Stopwatch stopwatch,
            TimeSpan repairBudget,
            out XYZ[] repairedRoute)
        {
            repairedRoute = [];
            var from = path[blockedSegmentIndex];
            var to = path[blockedSegmentIndex + 1];
            var maxAnchorIndex = Math.Min(
                path.Count - 1,
                blockedSegmentIndex + LocalStaticRepairRouteAnchorScanLimit);
            var candidateCount = 0;

            var nearRouteEndpoint = blockedSegmentIndex >= path.Count - 1 - NativeSegmentRepairTailSegmentLimit;
            if (nearRouteEndpoint
                && stopwatch.Elapsed <= repairBudget
                && TryBuildNativeSegmentRepairRoute(mapId, from, to, agentRadius, agentHeight, out var nativeSegmentRepair))
            {
                var composed = new List<XYZ>(path.Count + nativeSegmentRepair.Length);
                for (var i = 0; i <= blockedSegmentIndex; i++)
                    AppendWaypointIfDistinct(composed, path[i]);

                foreach (var waypoint in nativeSegmentRepair)
                    AppendWaypointIfDistinct(composed, waypoint);

                for (var i = blockedSegmentIndex + 1; i < path.Count; i++)
                    AppendWaypointIfDistinct(composed, path[i]);

                var normalized = NormalizeEarlySupportLayer(
                    mapId,
                    DensifyPath(composed.ToArray(), SmoothPathDensifySpacing),
                    int.MaxValue,
                    agentRadius,
                    agentHeight);
                if (FindFirstLineOfSightBreak(mapId, normalized, GetLocalStaticRepairValidationScanLimit(blockedSegmentIndex)) is null)
                {
                    repairedRoute = normalized;
                    return true;
                }
            }

            if (stopwatch.Elapsed <= repairBudget
                && TryBuildNativeStaticRepairRoute(
                    mapId,
                    path,
                    blockedSegmentIndex,
                    maxAnchorIndex,
                    agentRadius,
                    agentHeight,
                    stopwatch,
                    repairBudget,
                    out var nativeStaticRepair))
            {
                repairedRoute = nativeStaticRepair;
                return true;
            }

            foreach (var candidate in EnumerateLocalStaticRepairCandidates(mapId, from, to, agentRadius))
            {
                if (stopwatch.Elapsed > repairBudget)
                    break;

                if (++candidateCount > LocalStaticRepairRouteCandidateLimit)
                    break;

                var candidateDistance = Distance2D(from, candidate);
                if (candidateDistance > 2.5f && !HasLineOfSightStrict(mapId, from, candidate))
                    continue;

                for (var anchorIndex = blockedSegmentIndex + 1; anchorIndex <= maxAnchorIndex; anchorIndex++)
                {
                    var anchor = path[anchorIndex];
                    if (Distance2D(candidate, anchor) <= agentRadius)
                        continue;

                    if (!HasLineOfSightStrict(mapId, candidate, anchor))
                        continue;

                    var composed = new List<XYZ>(path.Count + 1);
                    for (var i = 0; i <= blockedSegmentIndex; i++)
                        AppendWaypointIfDistinct(composed, path[i]);

                    AppendWaypointIfDistinct(composed, candidate);
                    for (var i = anchorIndex; i < path.Count; i++)
                        AppendWaypointIfDistinct(composed, path[i]);

                    var normalized = NormalizeEarlySupportLayer(
                        mapId,
                        DensifyPath(composed.ToArray(), SmoothPathDensifySpacing),
                        EarlySupportNormalizationLimit,
                        agentRadius,
                        agentHeight);
                    if (FindFirstLineOfSightBreak(mapId, normalized, GetLocalStaticRepairValidationScanLimit(blockedSegmentIndex)) is null)
                    {
                        repairedRoute = normalized;
                        return true;
                    }
                }
            }

            return false;
        }

        private static bool TryBuildNativeStaticRepairRoute(
            uint mapId,
            IReadOnlyList<XYZ> path,
            int blockedSegmentIndex,
            int maxAnchorIndex,
            float agentRadius,
            float agentHeight,
            Stopwatch stopwatch,
            TimeSpan repairBudget,
            out XYZ[] repairedRoute)
        {
            repairedRoute = [];
            var from = path[blockedSegmentIndex];

            for (var anchorIndex = blockedSegmentIndex + 2; anchorIndex <= maxAnchorIndex; anchorIndex++)
            {
                if (stopwatch.Elapsed > repairBudget)
                    return false;

                var anchor = path[anchorIndex];
                if (Distance2D(from, anchor) <= agentRadius)
                    continue;

                if (Distance2D(from, anchor) > NativeSegmentRepairMaxHorizontalDistance)
                    continue;

                if (!TryBuildNativeSegmentRepairRoute(mapId, from, anchor, agentRadius, agentHeight, out var nativeRepair))
                    continue;

                var composed = new List<XYZ>(path.Count + nativeRepair.Length);
                for (var i = 0; i <= blockedSegmentIndex; i++)
                    AppendWaypointIfDistinct(composed, path[i]);

                foreach (var waypoint in nativeRepair)
                    AppendWaypointIfDistinct(composed, waypoint);

                for (var i = anchorIndex + 1; i < path.Count; i++)
                    AppendWaypointIfDistinct(composed, path[i]);

                var normalized = NormalizeEarlySupportLayer(
                    mapId,
                    DensifyPath(composed.ToArray(), SmoothPathDensifySpacing),
                    int.MaxValue,
                    agentRadius,
                    agentHeight);
                if (FindFirstLineOfSightBreak(mapId, normalized, GetLocalStaticRepairValidationScanLimit(blockedSegmentIndex)) is not null)
                    continue;

                repairedRoute = normalized;
                return true;
            }

            return false;
        }

        private static int GetLocalStaticRepairValidationScanLimit(int blockedSegmentIndex)
            => Math.Min(
                EarlyStaticRepairScanLimit,
                blockedSegmentIndex + LocalStaticRepairRouteAnchorScanLimit + 4);

        private static int? FindFirstEarlyLineOfSightBreak(uint mapId, XYZ[] path)
            => FindFirstLineOfSightBreak(mapId, path, EarlyStaticRepairScanLimit);

        private static int? FindFirstLineOfSightBreak(uint mapId, XYZ[] path, int maxSegmentIndex = int.MaxValue)
        {
            var scanEnd = Math.Min(path.Length - 1, maxSegmentIndex);
            for (var i = 0; i < scanEnd; i++)
            {
                if (Distance2D(path[i], path[i + 1]) >= EarlyStaticRepairLosMinSegmentLength
                    && !HasLineOfSightStrict(mapId, path[i], path[i + 1]))
                {
                    return i;
                }
            }

            return null;
        }

        private static bool TryBuildNativeSegmentRepairRoute(
            uint mapId,
            XYZ from,
            XYZ to,
            float agentRadius,
            float agentHeight,
            out XYZ[] repairRoute)
        {
            repairRoute = [];
            if (Distance2D(from, to) > NativeSegmentRepairMaxHorizontalDistance)
                return false;

            foreach (var smoothPath in new[] { true, false })
            {
                var candidate = TryFindPathNative(mapId, from, to, smoothPath, agentRadius, agentHeight);
                if (candidate.Length <= 2)
                    continue;

                var normalized = NormalizeEarlySupportLayer(
                    mapId,
                    DensifyPath(candidate, SmoothPathDensifySpacing),
                    int.MaxValue,
                    agentRadius,
                    agentHeight);
                if (FindFirstLineOfSightBreak(mapId, normalized) is not null)
                    continue;

                if (!ValidateRepairSequence(mapId, from, normalized, agentRadius, agentHeight))
                    continue;

                repairRoute = normalized;
                return true;
            }

            return false;
        }

        private static int? FindFirstUnrepairedEarlyBreak(
            uint mapId,
            XYZ[] path,
            float agentRadius,
            float agentHeight)
        {
            var scanEnd = Math.Min(path.Length - 1, EarlyStaticRepairScanLimit);
            for (var i = 0; i < scanEnd; i++)
            {
                if (RequiresLocalStaticRepair(mapId, path[i], path[i + 1], agentRadius, agentHeight, out _))
                    return i;
            }

            return null;
        }

        private static SegmentValidationCode ValidateSegmentForAgent(
            uint mapId,
            XYZ from,
            XYZ to,
            float agentRadius,
            float agentHeight)
        {
            try
            {
                return (SegmentValidationCode)ValidateWalkableSegment(
                    mapId,
                    new NativeXyz(from),
                    new NativeXyz(to),
                    agentRadius,
                    agentHeight,
                    out _,
                    out _,
                    out _);
            }
            catch
            {
                return SegmentValidationCode.Clear;
            }
        }

        private static bool IsLocallyWalkable(SegmentValidationCode validation, XYZ from, XYZ to)
            => validation is SegmentValidationCode.Clear or SegmentValidationCode.MissingSupport
                || IsInconsistentUphillStepDown(validation, from, to);

        private static bool TryBuildLongSegmentDetour(
            uint mapId,
            XYZ segmentStart,
            XYZ segmentEnd,
            Stopwatch stopwatch,
            out XYZ detour)
        {
            detour = segmentStart;

            var dx = segmentEnd.X - segmentStart.X;
            var dy = segmentEnd.Y - segmentStart.Y;
            var len = MathF.Sqrt((dx * dx) + (dy * dy));
            if (len <= 0.01f)
                return false;

            var dirX = dx / len;
            var dirY = dy / len;
            var perpX = -dirY;
            var perpY = dirX;

            XYZ? fallback = null;
            foreach (var forward in LongSegmentRepairForwardDistances)
            {
                foreach (var lateral in LongSegmentRepairLateralOffsets)
                {
                    if (stopwatch.Elapsed > LongSegmentRepairBudget)
                        return false;

                    var desired = new XYZ(
                        segmentStart.X + (dirX * forward) + (perpX * lateral),
                        segmentStart.Y + (dirY * forward) + (perpY * lateral),
                        segmentStart.Z + ((segmentEnd.Z - segmentStart.Z) * MathF.Min(1f, forward / len)));

                    if (!TryFindNearbyWalkablePoint(
                        mapId,
                        desired,
                        searchRadius: 10f,
                        maxHorizontalOffset: 8f,
                        maxVerticalOffset: 12f,
                        out var snapped))
                    {
                        continue;
                    }

                    var startDistance = Distance2D(segmentStart, snapped);
                    if (startDistance < 3f || startDistance > 30f)
                        continue;

                    if (!HasLineOfSightSafe(mapId, segmentStart, snapped))
                        continue;

                    if (HasLineOfSightSafe(mapId, snapped, segmentEnd))
                    {
                        detour = snapped;
                        return true;
                    }

                    fallback ??= snapped;
                }
            }

            if (fallback.HasValue)
            {
                detour = fallback.Value;
                return true;
            }

            return false;
        }

        private static void AppendDensifiedSegment(List<XYZ> waypoints, XYZ from, XYZ to, float spacing)
        {
            var horizontal = Distance2D(from, to);
            if (horizontal <= spacing)
            {
                AppendWaypointIfDistinct(waypoints, to);
                return;
            }

            var steps = Math.Max(1, (int)MathF.Ceiling(horizontal / spacing));
            for (var step = 1; step <= steps; step++)
            {
                var t = step / (float)steps;
                AppendWaypointIfDistinct(
                    waypoints,
                    new XYZ(
                        from.X + ((to.X - from.X) * t),
                        from.Y + ((to.Y - from.Y) * t),
                        from.Z + ((to.Z - from.Z) * t)));
            }
        }

        private static bool HasLineOfSightSafe(uint mapId, XYZ from, XYZ to)
        {
            try
            {
                return LineOfSight(mapId, new NativeXyz(from), new NativeXyz(to));
            }
            catch
            {
                return true;
            }
        }

        private static bool HasLineOfSightStrict(uint mapId, XYZ from, XYZ to)
        {
            try
            {
                return LineOfSight(mapId, new NativeXyz(from), new NativeXyz(to));
            }
            catch
            {
                return false;
            }
        }

        // ── Corridor Segment Validation ──

        /// <summary>
        /// Validate each corridor segment with a physics capsule sweep.
        /// If a segment is blocked, try lateral offsets to route around the obstacle.
        /// Returns the validated (potentially repaired) path.
        /// </summary>
        /// <summary>
        /// Maximum time budget for post-corridor segment validation.
        /// ValidateWalkableSegment runs physics capsule sweeps (~630ms per segment worst case).
        /// With repair attempts (6 offsets * 2 calls each), a 10-corner path can take 70+ seconds.
        /// If we exceed this budget, return the raw corridor path — it's already navmesh-constrained.
        /// </summary>
        private static readonly TimeSpan SegmentValidationBudget = TimeSpan.FromSeconds(5);
        private const float WaypointDuplicateEpsilon = 0.01f;

        private static bool IsNativeSegmentValidationEnabled()
        {
            var raw = Environment.GetEnvironmentVariable("WWOW_ENABLE_NATIVE_SEGMENT_VALIDATION");
            if (string.IsNullOrWhiteSpace(raw))
                return false;

            return raw.Trim() switch
            {
                "1" => true,
                _ when raw.Equals("true", StringComparison.OrdinalIgnoreCase) => true,
                _ when raw.Equals("yes", StringComparison.OrdinalIgnoreCase) => true,
                _ when raw.Equals("on", StringComparison.OrdinalIgnoreCase) => true,
                _ => false,
            };
        }

        private OverlayPathAttempt SelectRepairSource(OverlayPathAttempt preferredAttempt, OverlayPathAttempt alternateAttempt)
        {
            if (preferredAttempt.Path.Length > 0 && preferredAttempt.PathBlocked)
                return preferredAttempt;

            if (alternateAttempt.Path.Length > 0 && alternateAttempt.PathBlocked)
                return alternateAttempt;

            return preferredAttempt.Path.Length >= alternateAttempt.Path.Length
                ? preferredAttempt
                : alternateAttempt;
        }

        private XYZ[] TryRepairPath(
            uint mapId,
            XYZ start,
            XYZ end,
            bool smoothPath,
            float agentRadius,
            float agentHeight,
            XYZ[] rawPath,
            int blockedSegmentIndex,
            bool allowGlobalRepair = true,
            bool recordDynamicOverlayRepair = false)
        {
            if (rawPath.Length < 2 || blockedSegmentIndex < 0 || blockedSegmentIndex >= rawPath.Length - 1)
                return Array.Empty<XYZ>();

            var blockedStart = rawPath[blockedSegmentIndex];
            var blockedEnd = rawPath[blockedSegmentIndex + 1];
            var bestPath = Array.Empty<XYZ>();
            var bestCost = float.MaxValue;
            var stopwatch = Stopwatch.StartNew();

            foreach (var candidate in BuildRepairCandidates(blockedStart, blockedEnd))
            {
                if (stopwatch.Elapsed > DynamicOverlayRepairBudget)
                    break;

                var localRepair = TryComposeLocalRepairPath(mapId, rawPath, blockedSegmentIndex, candidate, agentRadius, agentHeight);
                if (localRepair.Length == 0)
                    continue;

                var cost = ComputePathCost(localRepair);
                if (cost < bestCost)
                {
                    bestCost = cost;
                    bestPath = localRepair;
                }
            }

            if (bestPath.Length > 0)
            {
                if (recordDynamicOverlayRepair)
                    NavigationPerformanceMetrics.RecordDynamicOverlayRepair();
                return bestPath;
            }

            if (!allowGlobalRepair)
                return Array.Empty<XYZ>();

            foreach (var candidate in BuildRepairCandidates(blockedStart, blockedEnd))
            {
                if (stopwatch.Elapsed > DynamicOverlayRepairBudget)
                    break;

                var repaired = TryComposeCandidatePath(mapId, start, end, candidate, smoothPath, agentRadius, agentHeight);
                if (repaired.Length == 0)
                    continue;

                if (FindBlockedSegment(mapId, repaired, agentRadius, agentHeight).BlockedSegmentIndex is not null)
                    continue;

                var cost = ComputePathCost(repaired);
                if (cost < bestCost)
                {
                    bestCost = cost;
                    bestPath = repaired;
                }
            }

            if (bestPath.Length > 0 && recordDynamicOverlayRepair)
                NavigationPerformanceMetrics.RecordDynamicOverlayRepair();

            return bestPath;
        }

        private XYZ[] TryComposeLocalRepairPath(
            uint mapId,
            XYZ[] rawPath,
            int blockedSegmentIndex,
            XYZ candidate,
            float agentRadius,
            float agentHeight)
        {
            var blockedStart = rawPath[blockedSegmentIndex];
            var blockedEnd = rawPath[blockedSegmentIndex + 1];
            var candidateOptions = new[] { candidate };

            foreach (var option in candidateOptions)
            {
                if (Distance2D(blockedStart, option) <= agentRadius
                    || Distance2D(option, blockedEnd) <= agentRadius)
                {
                    continue;
                }

                if (SegmentBlockedByDynamicOverlayForAgent(mapId, blockedStart, option, agentRadius, agentHeight, out _)
                    || SegmentBlockedByDynamicOverlayForAgent(mapId, option, blockedEnd, agentRadius, agentHeight, out _))
                {
                    continue;
                }

                var repaired = new List<XYZ>(rawPath.Length + 1);
                for (var i = 0; i <= blockedSegmentIndex; i++)
                    AppendWaypointIfDistinct(repaired, rawPath[i]);

                AppendWaypointIfDistinct(repaired, option);
                for (var i = blockedSegmentIndex + 1; i < rawPath.Length; i++)
                    AppendWaypointIfDistinct(repaired, rawPath[i]);

                var repairedPath = repaired.ToArray();
                if (FindBlockedSegment(mapId, repairedPath, agentRadius, agentHeight).BlockedSegmentIndex is null)
                    return repairedPath;
            }

            return Array.Empty<XYZ>();
        }

        private static IEnumerable<XYZ> BuildRepairCandidates(XYZ blockedStart, XYZ blockedEnd)
        {
            var dx = blockedEnd.X - blockedStart.X;
            var dy = blockedEnd.Y - blockedStart.Y;
            var length = MathF.Sqrt((dx * dx) + (dy * dy));
            if (length < 0.01f)
                yield break;

            var unitX = dx / length;
            var unitY = dy / length;
            var perpX = -unitY;
            var perpY = unitX;

            foreach (var along in RepairAlongSegmentSamples)
            {
                var basePoint = new XYZ(
                    blockedStart.X + (dx * along),
                    blockedStart.Y + (dy * along),
                    blockedStart.Z + ((blockedEnd.Z - blockedStart.Z) * along));

                foreach (var offset in RepairOffsetDistances)
                {
                    yield return new XYZ(basePoint.X + (perpX * offset), basePoint.Y + (perpY * offset), basePoint.Z);
                    yield return new XYZ(basePoint.X - (perpX * offset), basePoint.Y - (perpY * offset), basePoint.Z);
                }
            }
        }

        private XYZ[] TryComposeCandidatePath(
            uint mapId,
            XYZ start,
            XYZ end,
            XYZ candidate,
            bool smoothPath,
            float agentRadius,
            float agentHeight)
        {
            foreach (var firstLegSmooth in EnumeratePathModes(smoothPath))
            {
                XYZ[] firstLeg;
                try
                {
                    firstLeg = _findPathResolver(mapId, start, candidate, firstLegSmooth, agentRadius, agentHeight).Path ?? Array.Empty<XYZ>();
                }
                catch
                {
                    continue;
                }

                if (firstLeg.Length == 0)
                    continue;
                if (Distance3D(firstLeg[^1], candidate) > NativePathEndpointMaxDistance)
                    continue;

                foreach (var secondLegSmooth in EnumeratePathModes(smoothPath))
                {
                    XYZ[] secondLeg;
                    try
                    {
                        secondLeg = _findPathResolver(mapId, candidate, end, secondLegSmooth, agentRadius, agentHeight).Path ?? Array.Empty<XYZ>();
                    }
                    catch
                    {
                        continue;
                    }

                    if (secondLeg.Length == 0)
                        continue;
                    if (Distance3D(secondLeg[^1], end) > NativePathEndpointMaxDistance)
                        continue;

                    var combined = CombinePaths(firstLeg, secondLeg);
                    if (combined.Length > 0)
                        return combined;
                }
            }

            return Array.Empty<XYZ>();
        }

        private static IEnumerable<bool> EnumeratePathModes(bool preferredMode)
        {
            yield return preferredMode;
            yield return !preferredMode;
        }

        private static XYZ[] CombinePaths(XYZ[] firstLeg, XYZ[] secondLeg)
        {
            if (firstLeg.Length == 0 || secondLeg.Length == 0)
                return Array.Empty<XYZ>();

            var combined = new List<XYZ>(firstLeg.Length + secondLeg.Length);
            combined.AddRange(firstLeg);

            var secondStartIndex = Distance3D(firstLeg[^1], secondLeg[0]) <= CombineWaypointEpsilon ? 1 : 0;
            for (var i = secondStartIndex; i < secondLeg.Length; i++)
                combined.Add(secondLeg[i]);

            return combined.ToArray();
        }

        private (int? BlockedSegmentIndex, string BlockedReason) FindBlockedSegment(
            uint mapId,
            XYZ[] path,
            float agentRadius = 0.0f,
            float agentHeight = 0.0f)
        {
            if (path.Length < 2)
                return (null, "none");

            for (var i = 0; i < path.Length - 1; i++)
            {
                if (SegmentBlockedByDynamicOverlayForAgent(mapId, path[i], path[i + 1], agentRadius, agentHeight, out var blockedReason))
                {
                    return (i, blockedReason);
                }
            }

            return (null, "none");
        }

        private bool SegmentBlockedByDynamicOverlayForAgent(
            uint mapId,
            XYZ from,
            XYZ to,
            float agentRadius,
            float agentHeight,
            out string blockedReason)
        {
            if (_segmentBlocker(mapId, from, to))
            {
                var directReason = _segmentBlockerReasonResolver(mapId, from, to);
                blockedReason = string.IsNullOrWhiteSpace(directReason) ? "dynamic_overlay" : directReason;
                return true;
            }

            blockedReason = "none";
            if (agentRadius <= 0.0f || agentHeight <= 0.0f || !HasActiveDynamicObjectOverlay())
                return false;

            var dx = to.X - from.X;
            var dy = to.Y - from.Y;
            var horizontal = MathF.Sqrt((dx * dx) + (dy * dy));
            if (horizontal <= 0.01f)
                return false;

            var perpX = -dy / horizontal;
            var perpY = dx / horizontal;
            foreach (var offset in EnumerateDynamicOverlayClearanceOffsets(agentRadius))
            {
                var leftFrom = new XYZ(from.X + (perpX * offset), from.Y + (perpY * offset), from.Z);
                var leftTo = new XYZ(to.X + (perpX * offset), to.Y + (perpY * offset), to.Z);
                if (_segmentBlocker(mapId, leftFrom, leftTo))
                {
                    var reason = _segmentBlockerReasonResolver(mapId, leftFrom, leftTo);
                    blockedReason = string.IsNullOrWhiteSpace(reason) ? "dynamic_overlay_capsule_clearance" : reason;
                    return true;
                }

                var rightFrom = new XYZ(from.X - (perpX * offset), from.Y - (perpY * offset), from.Z);
                var rightTo = new XYZ(to.X - (perpX * offset), to.Y - (perpY * offset), to.Z);
                if (_segmentBlocker(mapId, rightFrom, rightTo))
                {
                    var reason = _segmentBlockerReasonResolver(mapId, rightFrom, rightTo);
                    blockedReason = string.IsNullOrWhiteSpace(reason) ? "dynamic_overlay_capsule_clearance" : reason;
                    return true;
                }
            }

            return false;
        }

        private static IEnumerable<float> EnumerateDynamicOverlayClearanceOffsets(float agentRadius)
        {
            yield return agentRadius;

            var halfRadius = agentRadius * 0.5f;
            if (halfRadius >= 0.25f)
                yield return halfRadius;
        }

        private bool SegmentIntersectsDynamicObjectsInternal(uint mapId, XYZ from, XYZ to)
            => SegmentIntersectsDynamicObjects(mapId, from.X, from.Y, from.Z, to.X, to.Y, to.Z);

        private string? TryResolveDynamicObjectBlockReasonInternal(uint mapId, XYZ from, XYZ to)
        {
            try
            {
                if (!SegmentIntersectsDynamicObjectsDetailedNative(
                        mapId,
                        from.X,
                        from.Y,
                        from.Z,
                        to.X,
                        to.Y,
                        to.Z,
                        out var blockingInstanceId,
                        out var blockingGuid,
                        out var blockingDisplayId))
                {
                    return null;
                }

                return FormatDynamicOverlayBlockReason(blockingInstanceId, blockingGuid, blockingDisplayId);
            }
            catch
            {
                return null;
            }
        }

        private static string FormatDynamicOverlayBlockReason(
            uint blockingInstanceId,
            ulong blockingGuid,
            uint blockingDisplayId)
        {
            var details = new List<string>(4) { "dynamic_overlay" };
            if (blockingDisplayId != 0)
                details.Add($"display={blockingDisplayId}");
            if (blockingGuid != 0)
                details.Add($"guid=0x{blockingGuid:X}");
            if (blockingInstanceId != 0)
                details.Add($"instance=0x{blockingInstanceId:X8}");

            return string.Join(",", details);
        }

        private static string NormalizeBlockReason(string? reason)
            => string.IsNullOrWhiteSpace(reason) ? "dynamic_overlay" : reason;

        private static float ComputePathCost(XYZ[] path)
        {
            if (path.Length < 2)
                return float.MaxValue;

            var cost = 0f;
            for (var i = 1; i < path.Length; i++)
                cost += Distance3D(path[i - 1], path[i]);

            return cost;
        }

        private static float Distance3D(XYZ from, XYZ to)
        {
            var dx = to.X - from.X;
            var dy = to.Y - from.Y;
            var dz = to.Z - from.Z;
            return MathF.Sqrt((dx * dx) + (dy * dy) + (dz * dz));
        }

        private static float Distance2D(XYZ from, XYZ to)
        {
            var dx = to.X - from.X;
            var dy = to.Y - from.Y;
            return MathF.Sqrt((dx * dx) + (dy * dy));
        }

        private static float DistancePointToSegment2D(XYZ point, XYZ start, XYZ end)
        {
            var dx = end.X - start.X;
            var dy = end.Y - start.Y;
            var lenSq = (dx * dx) + (dy * dy);
            if (lenSq <= 0.0001f)
                return Distance2D(point, start);

            var t = (((point.X - start.X) * dx) + ((point.Y - start.Y) * dy)) / lenSq;
            t = Math.Clamp(t, 0.0f, 1.0f);
            var closest = new XYZ(
                start.X + (dx * t),
                start.Y + (dy * t),
                start.Z);
            return Distance2D(point, closest);
        }

        private static XYZ[] ValidateCorridorSegments(
            uint mapId,
            XYZ[] path,
            float radius,
            float height,
            out int? firstBlockedIdx,
            out string firstBlockedReason)
        {
            firstBlockedIdx = null;
            firstBlockedReason = "none";
            if (path.Length < 2) return path;

            var sw = Stopwatch.StartNew();
            var result = new List<XYZ> { path[0] };
            int repairCount = 0;

            for (int i = 0; i < path.Length - 1; i++)
            {
                // Time budget exceeded — accept remaining segments as-is
                if (sw.Elapsed > SegmentValidationBudget)
                {
                    Console.Error.WriteLine($"[CORRIDOR-VALIDATE] Time budget exceeded ({sw.ElapsedMilliseconds}ms) at segment {i}/{path.Length - 1}. Accepting remaining segments as-is.");
                    for (int j = i + 1; j < path.Length; j++)
                        result.Add(path[j]);
                    break;
                }

                var segStart = result[^1]; // use the (potentially adjusted) current position
                var segEnd = path[i + 1];

                SegmentValidationCode validationCode;
                float resolvedEndZ;
                try
                {
                    validationCode = (SegmentValidationCode)ValidateWalkableSegment(
                        mapId,
                        new NativeXyz(segStart),
                        new NativeXyz(segEnd),
                        radius,
                        height,
                        out resolvedEndZ,
                        out _,
                        out _);
                }
                catch
                {
                    // Native call failed — accept the segment as-is
                    AppendWaypointIfDistinct(result, segEnd);
                    continue;
                }

                if (validationCode == SegmentValidationCode.Clear)
                {
                    var effectiveEnd = float.IsFinite(resolvedEndZ)
                        ? new XYZ(segEnd.X, segEnd.Y, resolvedEndZ)
                        : segEnd;
                    AppendWaypointIfDistinct(result, effectiveEnd);
                    continue;
                }

                if (validationCode == SegmentValidationCode.MissingSupport)
                {
                    // Match native path refinement and the test assertions: "missing support"
                    // on a short corridor segment is not treated as a hard block.
                    AppendWaypointIfDistinct(result, segEnd);
                    continue;
                }

                // Segment blocked — try lateral offsets to route around the obstacle,
                // but only if we have time budget remaining.
                if (validationCode == SegmentValidationCode.StepDownTooFar
                    && segEnd.Z >= segStart.Z - 0.25f)
                {
                    // The native sweep can report a step-down limit when a sloped or
                    // layered support probe resolves below the segment even though the
                    // requested endpoint is level or uphill. Keep rejecting true drops,
                    // but do not hard-block these inconsistent uphill reports.
                    AppendWaypointIfDistinct(result, segEnd);
                    continue;
                }

                if (!firstBlockedIdx.HasValue)
                {
                    firstBlockedIdx = i;
                    firstBlockedReason = MapValidationCodeToReason(validationCode);
                }

                if (sw.Elapsed > SegmentValidationBudget)
                {
                    Console.Error.WriteLine($"[CORRIDOR-BLOCKED] seg {i}: blocked code={validationCode}, no time for repair, accepting as-is");
                    AppendWaypointIfDistinct(result, segEnd);
                    continue;
                }

                if (TryRepairSegmentWithWalkableSnap(
                    mapId,
                    segStart,
                    segEnd,
                    radius,
                    height,
                    out var snappedRepair))
                {
                    foreach (var waypoint in snappedRepair)
                        AppendWaypointIfDistinct(result, waypoint);

                    repairCount++;
                    Console.Error.WriteLine(
                        $"[CORRIDOR-REPAIR] seg {i}: blocked code={validationCode}, repaired with nearest-walkable support");
                    continue;
                }

                float dx = segEnd.X - segStart.X;
                float dy = segEnd.Y - segStart.Y;
                float segLen = MathF.Sqrt(dx * dx + dy * dy);
                if (segLen < 0.01f) { result.Add(segEnd); continue; }

                // Perpendicular direction for lateral offsets
                float perpX = -dy / segLen;
                float perpY = dx / segLen;

                bool repaired = false;
                float[] offsets = { 1.5f, 3.0f, -1.5f, -3.0f, 5.0f, -5.0f };

                foreach (float offset in offsets)
                {
                    if (sw.Elapsed > SegmentValidationBudget) break;

                    // Offset the midpoint laterally
                    float midX = (segStart.X + segEnd.X) * 0.5f + perpX * offset;
                    float midY = (segStart.Y + segEnd.Y) * 0.5f + perpY * offset;
                    float midZ = (segStart.Z + segEnd.Z) * 0.5f;
                    var midPoint = new XYZ(midX, midY, midZ);

                    // Validate start→mid and mid→end
                    try
                    {
                        uint v1 = ValidateWalkableSegment(mapId, new NativeXyz(segStart), new NativeXyz(midPoint),
                            radius, height, out _, out _, out _);
                        uint v2 = ValidateWalkableSegment(mapId, new NativeXyz(midPoint), new NativeXyz(segEnd),
                            radius, height, out _, out _, out _);

                        if (v1 == 0 && v2 == 0)
                        {
                            AppendWaypointIfDistinct(result, midPoint);
                            AppendWaypointIfDistinct(result, segEnd);
                            repairCount++;
                            Console.Error.WriteLine($"[CORRIDOR-REPAIR] seg {i}: blocked code={validationCode}, repaired with offset={offset:F1}y mid=({midX:F1},{midY:F1},{midZ:F1})");
                            repaired = true;
                            break;
                        }
                    }
                    catch { /* try next offset */ }
                }

                if (!repaired)
                {
                    // Could not repair — include the segment as-is and let runtime physics handle it
                    Console.Error.WriteLine($"[CORRIDOR-BLOCKED] seg {i}: blocked code={validationCode}, no repair found, accepting as-is");
                    AppendWaypointIfDistinct(result, segEnd);
                }
            }

            if (repairCount > 0)
            {
                NavigationPerformanceMetrics.RecordSegmentValidationRepair(repairCount);
                Console.Error.WriteLine($"[CORRIDOR-VALIDATE] {repairCount} segments repaired out of {path.Length - 1} ({sw.ElapsedMilliseconds}ms)");
            }

            AppendWaypointIfDistinct(result, path[^1]);
            return result.ToArray();
        }

        private static bool TryRepairSegmentWithWalkableSnap(
            uint mapId,
            XYZ segStart,
            XYZ segEnd,
            float radius,
            float height,
            out XYZ[] repairWaypoints)
        {
            repairWaypoints = [];

            var midPoint = new XYZ(
                (segStart.X + segEnd.X) * 0.5f,
                (segStart.Y + segEnd.Y) * 0.5f,
                (segStart.Z + segEnd.Z) * 0.5f);

            var hasSnappedMid = TryFindNearbyWalkablePoint(
                mapId,
                midPoint,
                searchRadius: 6f,
                maxHorizontalOffset: 4f,
                maxVerticalOffset: 8f,
                out var snappedMid);
            var hasSnappedEnd = TryFindNearbyWalkablePoint(
                mapId,
                segEnd,
                searchRadius: 6f,
                maxHorizontalOffset: 4f,
                maxVerticalOffset: 8f,
                out var snappedEnd);

            var candidates = new List<XYZ[]>();
            if (hasSnappedEnd)
                candidates.Add([snappedEnd]);
            if (hasSnappedMid && hasSnappedEnd)
                candidates.Add([snappedMid, snappedEnd]);
            if (hasSnappedMid)
                candidates.Add([snappedMid, segEnd]);

            foreach (var candidate in candidates)
            {
                if (ValidateRepairSequence(mapId, segStart, candidate, radius, height))
                {
                    repairWaypoints = candidate;
                    return true;
                }
            }

            return false;
        }

        private static bool TryFindNearbyWalkablePoint(
            uint mapId,
            XYZ point,
            float searchRadius,
            float maxHorizontalOffset,
            float maxVerticalOffset,
            out XYZ snapped)
        {
            snapped = point;

            try
            {
                var area = FindNearestWalkablePointNative(
                    mapId,
                    point.X,
                    point.Y,
                    point.Z,
                    searchRadius,
                    out var x,
                    out var y,
                    out var z);
                if (area == 0)
                    return false;

                var candidate = new XYZ(x, y, z);
                if (!float.IsFinite(candidate.X) || !float.IsFinite(candidate.Y) || !float.IsFinite(candidate.Z))
                    return false;

                if (Distance2D(point, candidate) > maxHorizontalOffset)
                    return false;

                if (MathF.Abs(point.Z - candidate.Z) > maxVerticalOffset)
                    return false;

                snapped = candidate;
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static bool ValidateRepairSequence(
            uint mapId,
            XYZ start,
            IReadOnlyList<XYZ> waypoints,
            float radius,
            float height)
        {
            var current = start;
            foreach (var waypoint in waypoints)
            {
                var validation = (SegmentValidationCode)ValidateWalkableSegment(
                    mapId,
                    new NativeXyz(current),
                    new NativeXyz(waypoint),
                    radius,
                    height,
                    out _,
                    out _,
                    out _);
                if (validation is not SegmentValidationCode.Clear and not SegmentValidationCode.MissingSupport)
                    return false;

                if (IsLocalPhysicsReachabilityBreak(mapId, current, waypoint, radius, height))
                    return false;

                current = waypoint;
            }

            return true;
        }

        private static string MapValidationCodeToReason(SegmentValidationCode validationCode)
            => validationCode switch
            {
                SegmentValidationCode.BlockedGeometry => "capsule_validation",
                SegmentValidationCode.MissingSupport => "support_surface",
                SegmentValidationCode.StepUpTooHigh => "step_up_limit",
                SegmentValidationCode.StepDownTooFar => "step_down_limit",
                _ => "none",
            };

        private static bool IsInconsistentUphillStepDown(SegmentValidationCode validationCode, XYZ from, XYZ to)
            => validationCode == SegmentValidationCode.StepDownTooFar
                && to.Z >= from.Z - 0.25f;

        private static void AppendWaypointIfDistinct(List<XYZ> waypoints, XYZ candidate)
        {
            if (waypoints.Count == 0)
            {
                waypoints.Add(candidate);
                return;
            }

            var current = waypoints[^1];
            if (MathF.Abs(current.X - candidate.X) <= WaypointDuplicateEpsilon &&
                MathF.Abs(current.Y - candidate.Y) <= WaypointDuplicateEpsilon &&
                MathF.Abs(current.Z - candidate.Z) <= WaypointDuplicateEpsilon)
            {
                return;
            }

            waypoints.Add(candidate);
        }

        // ── Utility ──

        public bool SegmentIntersectsDynamicObjects(uint mapId, float x0, float y0, float z0, float x1, float y1, float z1)
        {
            try
            {
                return SegmentIntersectsDynamicObjectsNative(mapId, x0, y0, z0, x1, y1, z1);
            }
            catch
            {
                return false;
            }
        }

        private static XYZ[] TryFindPathNative(
            uint mapId,
            XYZ start,
            XYZ end,
            bool smoothPath,
            float agentRadius,
            float agentHeight)
        {
            IntPtr pathPtr = IntPtr.Zero;
            var stopwatch = Stopwatch.StartNew();
            var length = 0;
            try
            {
                pathPtr = FindPathForAgent(
                    mapId,
                    new NativeXyz(start),
                    new NativeXyz(end),
                    smoothPath,
                    agentRadius,
                    agentHeight,
                    out length);

                if (pathPtr == IntPtr.Zero || length <= 0)
                    return Array.Empty<XYZ>();

                XYZ[] path = new XYZ[length];
                for (int i = 0; i < length; i++)
                {
                    IntPtr currentPtr = IntPtr.Add(pathPtr, i * Marshal.SizeOf<NativeXyz>());
                    path[i] = Marshal.PtrToStructure<NativeXyz>(currentPtr).ToManaged();
                }

                return path;
            }
            finally
            {
                NavigationPerformanceMetrics.RecordNativeFindPath(smoothPath, stopwatch.Elapsed, length);
                if (pathPtr != IntPtr.Zero)
                    PathArrFree(pathPtr);
            }
        }


        /// <summary>
        /// Check if a point is on or near the navmesh (within searchRadius).
        /// </summary>
        public (bool onNavmesh, XYZ nearestPoint) IsPointOnNavmesh(uint mapId, XYZ position, float searchRadius = 4.0f)
        {
            try
            {
                bool result = IsPointOnNavmeshNative(mapId, position.X, position.Y, position.Z,
                    searchRadius, out float nx, out float ny, out float nz);
                return (result, new XYZ(nx, ny, nz));
            }
            catch
            {
                return (false, position);
            }
        }

        /// <summary>
        /// Find the nearest walkable point within searchRadius.
        /// Returns the navmesh area type (0=not found, 1=ground, 3=steep_slope, 6=water).
        /// </summary>
        public (uint areaType, XYZ nearestPoint) FindNearestWalkablePoint(uint mapId, XYZ position, float searchRadius = 8.0f)
        {
            try
            {
                uint area = FindNearestWalkablePointNative(mapId, position.X, position.Y, position.Z,
                    searchRadius, out float ox, out float oy, out float oz);
                return (area, new XYZ(ox, oy, oz));
            }
            catch
            {
                return (0, position);
            }
        }

        // ── Types kept for external compatibility ──

        public readonly record struct SegmentEvaluation(
            XYZ EffectiveEnd,
            SegmentBlockReason Reason);

        public enum SegmentBlockReason
        {
            None = 0,
            DynamicOverlay,
            CapsuleValidation,
            SupportSurface,
            StepUpLimit,
            StepDownLimit,
        }
    }
}
