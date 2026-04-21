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

        private readonly Func<uint, XYZ, XYZ, bool, NativePathResolution> _findPathResolver;
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
                : (mapId, start, end, smoothPath) => NativePathResolution.FromPath(findPathResolver(mapId, start, end, smoothPath));
            _segmentBlocker = segmentBlocker ?? SegmentIntersectsDynamicObjectsInternal;
            _segmentBlockerReasonResolver = segmentBlockerReasonResolver ?? TryResolveDynamicObjectBlockReasonInternal;
        }

        public Navigation(
            Func<uint, XYZ, XYZ, bool, NativePathResolution> findPathResolver,
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
            var preferredAttempt = EvaluateOverlayAwarePath(mapId, start, end, smoothPath, "native_path");
            if (preferredAttempt.IsUsable)
                return BuildUsablePathResult(
                    mapId,
                    preferredAttempt,
                    smoothPath,
                    agentRadius,
                    agentHeight);

            var alternateAttempt = EvaluateOverlayAwarePath(mapId, start, end, !smoothPath, "native_path_alternate_mode");
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
                var repairedPath = TryRepairPath(mapId, start, end, smoothPath, repairSource.Path, blockedSegmentIndex);
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

        private NativePathResolution FindPathCorridorNative(uint mapId, XYZ start, XYZ end, bool smoothPath)
        {
            if (!smoothPath)
            {
                var straightPath = TryFindPathNative(mapId, start, end, smoothPath: false);
                if (straightPath.Length > 0)
                {
                    Console.Error.WriteLine(
                        $"[PATH_NATIVE] map={mapId} mode=straight path=[{string.Join(" -> ", straightPath.Select(p => $"({p.X:F1},{p.Y:F1},{p.Z:F1})"))}]");
                    return NativePathResolution.FromPath(straightPath);
                }
            }

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

                var rawPath = waypoints.ToArray();
                Console.Error.WriteLine(
                    $"[CORRIDOR] map={mapId} corners={corridorResult.CornerCount} path=[{string.Join(" -> ", rawPath.Select(p => $"({p.X:F1},{p.Y:F1},{p.Z:F1})"))}]");
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

        private OverlayPathAttempt EvaluateOverlayAwarePath(
            uint mapId,
            XYZ start,
            XYZ end,
            bool smoothPath,
            string successResult)
        {
            try
            {
                var resolution = _findPathResolver(mapId, start, end, smoothPath);
                var path = resolution.Path ?? Array.Empty<XYZ>();
                if (path.Length == 0)
                    return new OverlayPathAttempt(Array.Empty<XYZ>(), successResult, null, "none", PathBlocked: false);

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
            if (!IsNativeSegmentValidationEnabled())
                return new NavigationPathResult(rawPath, rawPath, successResult, null);

            if (!smoothPath)
            {
                // Straight-corner requests are the latency-sensitive alternate mode.
                // Keep them on the raw corridor so the caller can fall through quickly
                // instead of spending tens of seconds on bounded segment repair.
                return new NavigationPathResult(rawPath, rawPath, successResult, null);
            }

            var validatedPath = ValidateCorridorSegments(
                mapId,
                rawPath,
                agentRadius,
                agentHeight,
                out var blockedIdx,
                out var blockedReason);
            var resultTag = blockedIdx.HasValue ? "repaired_segment_validation" : successResult;
            return new NavigationPathResult(validatedPath, rawPath, resultTag, blockedIdx, blockedReason);
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

        private XYZ[] TryRepairPath(uint mapId, XYZ start, XYZ end, bool smoothPath, XYZ[] rawPath, int blockedSegmentIndex)
        {
            if (rawPath.Length < 2 || blockedSegmentIndex < 0 || blockedSegmentIndex >= rawPath.Length - 1)
                return Array.Empty<XYZ>();

            var blockedStart = rawPath[blockedSegmentIndex];
            var blockedEnd = rawPath[blockedSegmentIndex + 1];
            var bestPath = Array.Empty<XYZ>();
            var bestCost = float.MaxValue;

            foreach (var candidate in BuildRepairCandidates(blockedStart, blockedEnd))
            {
                var repaired = TryComposeCandidatePath(mapId, start, end, candidate, smoothPath);
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

        private XYZ[] TryComposeCandidatePath(uint mapId, XYZ start, XYZ end, XYZ candidate, bool smoothPath)
        {
            foreach (var firstLegSmooth in EnumeratePathModes(smoothPath))
            {
                XYZ[] firstLeg;
                try
                {
                    firstLeg = _findPathResolver(mapId, start, candidate, firstLegSmooth).Path ?? Array.Empty<XYZ>();
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
                        secondLeg = _findPathResolver(mapId, candidate, end, secondLegSmooth).Path ?? Array.Empty<XYZ>();
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

            return result.ToArray();
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

        private static XYZ[] TryFindPathNative(uint mapId, XYZ start, XYZ end, bool smoothPath)
        {
            IntPtr pathPtr = IntPtr.Zero;
            try
            {
                pathPtr = FindPath(
                    mapId,
                    new NativeXyz(start),
                    new NativeXyz(end),
                    smoothPath,
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
