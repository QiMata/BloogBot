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

    public readonly record struct NativePathResolution(
        XYZ[] Path,
        int? BlockedSegmentIndex = null,
        string BlockedReason = "none",
        bool WasRepairedAroundBlockedSegment = false)
    {
        public static NativePathResolution FromPath(XYZ[] path) => new(path, null, "none", false);
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
        private static readonly float[] RepairAlongSegmentSamples = [0.35f, 0.5f, 0.65f];
        private const float CombineWaypointEpsilon = 0.25f;
        private const float LongSegmentLosRepairThreshold = 35f;
        private const float LongSegmentDensifySpacing = 24f;
        private const float NativePathEndpointMaxDistance = 8.0f;
        private const float SmoothCorridorExpansionMinSegmentLength = 6f;
        private const float SmoothPathDensifySpacing = 6f;
        private const int EarlyStaticRepairScanLimit = 24;
        private const float EarlyStaticRepairMinSegmentLength = 0.75f;
        private const float EarlyStaticRepairLosMinSegmentLength = 2.5f;
        private const float EarlyStaticRepairValidationMaxLength = 8f;
        private const int EarlySupportNormalizationLimit = 24;
        private const float EarlySupportProjectionThreshold = 0.75f;
        private const float EarlySupportDuplicateProjectionThreshold = 0.10f;
        private const float EarlySupportDuplicateHorizontalDistance = 0.35f;
        private const float EarlySupportProjectionMaxDelta = 4.0f;
        private const float EarlySupportGroundClearance = 0.05f;
        private static readonly float[] LongSegmentRepairForwardDistances = [8f, 14f, 22f];
        private static readonly float[] LongSegmentRepairLateralOffsets = [8f, -8f, 14f, -14f, 22f, -22f];
        private static readonly float[] LocalStaticRepairAlongSamples = [0.35f, 0.5f, 0.65f];
        private static readonly float[] LocalStaticRepairBaseOffsets = [1.25f, 2f, 3.5f, 5f, 8f, 12f, 16f];
        private static readonly float[] LocalStaticRepairEscapeDistances = [1.75f, 2.5f, 4f, 6f, 8f];

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
            bool PathBlocked)
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

        public NavigationPathResult CalculateValidatedPath(uint mapId, XYZ start, XYZ end, bool smoothPath,
            float agentRadius = 0.6f, float agentHeight = 2.0f)
        {
            var preferredAttempt = EvaluateOverlayAwarePath(mapId, start, end, smoothPath, agentRadius, agentHeight, "native_path");
            if (preferredAttempt.IsUsable)
                return BuildUsablePathResult(
                    mapId,
                    preferredAttempt,
                    smoothPath,
                    agentRadius,
                    agentHeight);

            var alternateAttempt = EvaluateOverlayAwarePath(mapId, start, end, !smoothPath, agentRadius, agentHeight, "native_path_alternate_mode");
            if (alternateAttempt.IsUsable)
                return BuildUsablePathResult(
                    mapId,
                    alternateAttempt,
                    !smoothPath,
                    agentRadius,
                    agentHeight);

            var repairSource = SelectRepairSource(preferredAttempt, alternateAttempt);
            if (repairSource.Path.Length > 1 && repairSource.BlockedSegmentIndex is int blockedSegmentIndex)
            {
                var repairedPath = TryRepairPath(mapId, start, end, smoothPath, agentRadius, agentHeight, repairSource.Path, blockedSegmentIndex);
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

        private NativePathResolution FindPathCorridorNative(
            uint mapId,
            XYZ start,
            XYZ end,
            bool smoothPath,
            float agentRadius,
            float agentHeight)
        {
            if (smoothPath)
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
                        $"[PATH_NATIVE] map={mapId} mode=straight contains long static-LOS break; retrying corridor path.");
                }
            }

            var corridorResolution = FindPathCorridorResolution(mapId, start, end);
            if (smoothPath && corridorResolution.Path.Length > 1)
            {
                var expandedPath = TryExpandCorridorWithSmoothNativeSegments(mapId, corridorResolution.Path, agentRadius, agentHeight);
                if (expandedPath.Length > 0)
                    return NativePathResolution.FromPath(expandedPath);
            }

            return corridorResolution;
        }

        private NativePathResolution FindPathCorridorResolution(uint mapId, XYZ start, XYZ end)
        {
            var nativeStart = new NativeXyz(start);
            var nativeEnd = new NativeXyz(end);
            var corridorResult = FindPathCorridor(mapId, nativeStart, nativeEnd);

            try
            {
                if (corridorResult.Handle == 0 || corridorResult.CornerCount == 0)
                {
                    Console.Error.WriteLine(
                        $"[CORRIDOR] No corridor path found for map={mapId} start=({start.X:F1},{start.Y:F1},{start.Z:F1}) end=({end.X:F1},{end.Y:F1},{end.Z:F1})");
                    return NativePathResolution.FromPath(Array.Empty<XYZ>());
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

                return new NativePathResolution(rawPath, blockedSegmentIndex, blockedReason, repaired);
            }
            finally
            {
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
            try
            {
                var resolution = _findPathResolver(mapId, start, end, smoothPath, agentRadius, agentHeight);
                var path = resolution.Path ?? Array.Empty<XYZ>();
                if (path.Length == 0)
                    return new OverlayPathAttempt(Array.Empty<XYZ>(), successResult, null, "none", PathBlocked: false);

                if (!HasUsableNativeEndpointAnchors(start, end, path, out var endpointBlockReason))
                {
                    return new OverlayPathAttempt(
                        path,
                        successResult,
                        0,
                        endpointBlockReason,
                        PathBlocked: true);
                }

                if (resolution.BlockedSegmentIndex is int nativeBlockedSegmentIndex)
                {
                    return new OverlayPathAttempt(
                        path,
                        resolution.WasRepairedAroundBlockedSegment ? "repaired_dynamic_overlay" : successResult,
                        nativeBlockedSegmentIndex,
                        NormalizeBlockReason(resolution.BlockedReason),
                        PathBlocked: !resolution.WasRepairedAroundBlockedSegment);
                }

                var (blockedSegmentIndex, blockedReason) = FindBlockedSegment(mapId, path);
                return new OverlayPathAttempt(
                    path,
                    successResult,
                    blockedSegmentIndex,
                    blockedReason,
                    PathBlocked: blockedSegmentIndex.HasValue);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"[CORRIDOR] Native path resolver failed: {ex.Message}");
                return new OverlayPathAttempt(Array.Empty<XYZ>(), successResult, null, "none", PathBlocked: false);
            }
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

        private NavigationPathResult BuildUsablePathResult(
            uint mapId,
            OverlayPathAttempt attempt,
            bool smoothPath,
            float agentRadius,
            float agentHeight)
        {
            var validatedResult = ApplyNativeSegmentValidation(
                mapId,
                attempt.Path,
                smoothPath,
                agentRadius,
                agentHeight,
                attempt.SuccessResult);

            if (attempt.BlockedSegmentIndex is not int blockedSegmentIndex ||
                validatedResult.BlockedSegmentIndex.HasValue)
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
            string successResult)
        {
            var losRepairedPath = RepairLongLineOfSightBreaks(
                mapId,
                rawPath,
                out var losRepairCount,
                out var losBlockedIdx,
                out var losBlockedReason);
            var pathForValidation = losRepairedPath.Length > 0 ? losRepairedPath : rawPath;
            if (smoothPath)
            {
                pathForValidation = DensifyPath(pathForValidation, SmoothPathDensifySpacing);
                pathForValidation = NormalizeEarlySupportLayer(mapId, pathForValidation);
                pathForValidation = RepairEarlyStaticBreaks(
                    mapId,
                    pathForValidation,
                    agentRadius,
                    agentHeight,
                    out var earlyRepairCount,
                    out var earlyBlockedIdx,
                    out var earlyBlockedReason);

                if (earlyBlockedIdx.HasValue && !IsNativeSegmentValidationEnabled())
                {
                    var blockedResult = earlyRepairCount > 0 ? "repaired_static_los" : successResult;
                    return new NavigationPathResult(
                        pathForValidation,
                        rawPath,
                        blockedResult,
                        earlyBlockedIdx,
                        earlyBlockedReason);
                }

                if (earlyRepairCount > 0)
                    successResult = "repaired_static_los";
            }

            if (!smoothPath)
            {
                // Straight-corner requests are the latency-sensitive alternate mode.
                // Keep them on the raw corridor so the caller can fall through quickly
                // instead of spending tens of seconds on bounded segment repair.
                var resultTag = losRepairCount > 0 ? "repaired_static_los" : successResult;
                return new NavigationPathResult(
                    pathForValidation,
                    rawPath,
                    resultTag,
                    losBlockedIdx,
                    losBlockedReason);
            }

            if (!IsNativeSegmentValidationEnabled())
            {
                var resultTag = losRepairCount > 0 ? "repaired_static_los" : successResult;
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

                    if (TryBuildLongSegmentDetour(mapId, segmentStart, segmentEnd, out var detour))
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

        private static XYZ[] NormalizeEarlySupportLayer(uint mapId, XYZ[] path)
        {
            if (path.Length < 2)
                return path;

            var normalized = (XYZ[])path.Clone();
            var checkEnd = Math.Min(normalized.Length, EarlySupportNormalizationLimit + 1);
            for (var i = 1; i < checkEnd; i++)
            {
                var candidate = normalized[i];
                if (!TryGetNearbyGroundZ(mapId, candidate, out var groundZ))
                    continue;

                var floatDelta = candidate.Z - groundZ;
                var duplicateAnchor = Distance2D(normalized[i - 1], candidate) <= EarlySupportDuplicateHorizontalDistance;
                var projectionThreshold = duplicateAnchor
                    ? EarlySupportDuplicateProjectionThreshold
                    : EarlySupportProjectionThreshold;
                if (floatDelta <= projectionThreshold
                    || floatDelta > EarlySupportProjectionMaxDelta)
                {
                    continue;
                }

                var projectedZ = groundZ + EarlySupportGroundClearance;
                if (duplicateAnchor && MathF.Abs(normalized[i - 1].Z - groundZ) <= EarlySupportProjectionThreshold)
                    projectedZ = normalized[i - 1].Z;

                normalized[i] = new XYZ(candidate.X, candidate.Y, projectedZ);
            }

            return CollapseDuplicateWaypoints(normalized);
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

        private static XYZ[] RepairEarlyStaticBreaks(
            uint mapId,
            XYZ[] path,
            float agentRadius,
            float agentHeight,
            out int repairCount,
            out int? firstBlockedIdx,
            out string firstBlockedReason)
        {
            repairCount = 0;
            firstBlockedIdx = null;
            firstBlockedReason = "none";
            if (path.Length < 2)
                return path;

            var repaired = path.ToList();
            var scanEnd = Math.Min(repaired.Count - 1, EarlyStaticRepairScanLimit);
            for (var i = 0; i < scanEnd && i < repaired.Count - 1; i++)
            {
                var from = repaired[i];
                var to = repaired[i + 1];
                if (!RequiresLocalStaticRepair(mapId, from, to, agentRadius, agentHeight, out var reason))
                    continue;

                if (TryBuildLocalStaticRepairPoint(mapId, from, to, agentRadius, agentHeight, out var repairPoint))
                {
                    repaired.Insert(i + 1, repairPoint);
                    repairCount++;
                    scanEnd = Math.Min(repaired.Count - 1, EarlyStaticRepairScanLimit);
                    i++;
                    continue;
                }

                if (TryBuildLocalStaticRepairRoute(
                    mapId,
                    repaired,
                    i,
                    agentRadius,
                    agentHeight,
                    out var repairedRoute))
                {
                    repairCount++;
                    return repairedRoute;
                }

                firstBlockedIdx = i;
                firstBlockedReason = reason;
                break;
            }

            if (repairCount > 0)
            {
                Console.Error.WriteLine(
                    $"[CORRIDOR-STATIC-REPAIR] repaired {repairCount} early static/capsule segment(s) pathLen={path.Length} repairedLen={repaired.Count}");
            }

            return repaired.ToArray();
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

            if (horizontal <= EarlyStaticRepairValidationMaxLength)
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
            foreach (var snapped in EnumerateLocalStaticRepairCandidates(mapId, from, to, agentRadius))
            {
                if (!HasLineOfSightStrict(mapId, from, snapped)
                    || !HasLineOfSightStrict(mapId, snapped, to))
                {
                    continue;
                }

                var firstLeg = ValidateSegmentForAgent(mapId, from, snapped, agentRadius, agentHeight);
                var secondLeg = ValidateSegmentForAgent(mapId, snapped, to, agentRadius, agentHeight);
                if (IsLocallyWalkable(firstLeg, from, snapped)
                    && IsLocallyWalkable(secondLeg, snapped, to))
                {
                    repairPoint = snapped;
                    return true;
                }
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
            out XYZ[] repairedRoute)
        {
            repairedRoute = [];
            var from = path[blockedSegmentIndex];
            var to = path[blockedSegmentIndex + 1];
            var finalDestination = path[^1];

            foreach (var candidate in EnumerateLocalStaticRepairCandidates(mapId, from, to, agentRadius))
            {
                var candidateDistance = Distance2D(from, candidate);
                if (candidateDistance > 2.5f && !HasLineOfSightStrict(mapId, from, candidate))
                    continue;

                var firstLeg = ValidateSegmentForAgent(mapId, from, candidate, agentRadius, agentHeight);
                if (!IsLocallyWalkable(firstLeg, from, candidate))
                    continue;

                var candidatePath = TryFindPathNative(
                    mapId,
                    candidate,
                    finalDestination,
                    smoothPath: true,
                    agentRadius,
                    agentHeight);
                if (candidatePath.Length < 2)
                    continue;
                if (Distance3D(candidatePath[^1], finalDestination) > NativePathEndpointMaxDistance)
                    continue;

                var composed = new List<XYZ>(path.Count + candidatePath.Length);
                for (var i = 0; i <= blockedSegmentIndex; i++)
                    AppendWaypointIfDistinct(composed, path[i]);

                AppendWaypointIfDistinct(composed, candidate);
                AppendPathSkippingDuplicateStart(composed, candidatePath);
                var normalized = NormalizeEarlySupportLayer(mapId, DensifyPath(composed.ToArray(), SmoothPathDensifySpacing));
                if (FindFirstUnrepairedEarlyBreak(mapId, normalized, agentRadius, agentHeight) is null)
                {
                    repairedRoute = normalized;
                    return true;
                }
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

        private static bool TryBuildLongSegmentDetour(uint mapId, XYZ segmentStart, XYZ segmentEnd, out XYZ detour)
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
            int blockedSegmentIndex)
        {
            if (rawPath.Length < 2 || blockedSegmentIndex < 0 || blockedSegmentIndex >= rawPath.Length - 1)
                return Array.Empty<XYZ>();

            var blockedStart = rawPath[blockedSegmentIndex];
            var blockedEnd = rawPath[blockedSegmentIndex + 1];
            var bestPath = Array.Empty<XYZ>();
            var bestCost = float.MaxValue;

            foreach (var candidate in BuildRepairCandidates(blockedStart, blockedEnd))
            {
                var repaired = TryComposeCandidatePath(mapId, start, end, candidate, smoothPath, agentRadius, agentHeight);
                if (repaired.Length == 0)
                    continue;

                if (FindBlockedSegment(mapId, repaired).BlockedSegmentIndex is not null)
                    continue;

                var cost = ComputePathCost(repaired);
                if (cost < bestCost)
                {
                    bestCost = cost;
                    bestPath = repaired;
                }
            }

            return bestPath;
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

        private (int? BlockedSegmentIndex, string BlockedReason) FindBlockedSegment(uint mapId, XYZ[] path)
        {
            if (path.Length < 2)
                return (null, "none");

            for (var i = 0; i < path.Length - 1; i++)
            {
                if (_segmentBlocker(mapId, path[i], path[i + 1]))
                {
                    var reason = _segmentBlockerReasonResolver(mapId, path[i], path[i + 1]);
                    return (i, string.IsNullOrWhiteSpace(reason) ? "dynamic_overlay" : reason);
                }
            }

            return (null, "none");
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
                Console.Error.WriteLine($"[CORRIDOR-VALIDATE] {repairCount} segments repaired out of {path.Length - 1} ({sw.ElapsedMilliseconds}ms)");

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
            try
            {
                pathPtr = FindPathForAgent(
                    mapId,
                    new NativeXyz(start),
                    new NativeXyz(end),
                    smoothPath,
                    agentRadius,
                    agentHeight,
                    out int length);

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
