using GameData.Core.Models;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace PathfindingService.Repository
{
    public readonly record struct NavigationPathResult(
        XYZ[] Path,
        XYZ[] RawPath,
        string Result,
        int? BlockedSegmentIndex);

    public class Navigation
    {
        private const string DLL_NAME = "Navigation.dll";
        private const float DefaultAgentRadius = 0.6f;
        private const float DefaultAgentHeight = 2.0f;
        private const float NativeWalkabilityMaxSegmentLength = 20f;
        private const string EnableNativeSegmentValidationEnv = "WWOW_ENABLE_NATIVE_SEGMENT_VALIDATION";
        private const float StraightPathLongRouteOverrideDistance2D = 200f;
        private const float FallbackMinCellSize = 6f;
        private const float FallbackMediumCellSize = 8f;
        private const float FallbackLongRouteCellSize = 10f;
        private const float FallbackVeryLongRouteCellSize = 12f;
        private const float FallbackMinSearchMargin = 60f;
        private const float FallbackMaxSearchMargin = 1200f;
        private const float FallbackGoalDistance = 9f;
        private const int FallbackMinExpandedNodes = 2500;
        private const int FallbackMaxExpandedNodesCap = 120000;

        private static readonly (int X, int Y)[] NeighborOffsets =
        [
            (1, 0), (-1, 0), (0, 1), (0, -1),
            (1, 1), (1, -1), (-1, 1), (-1, -1),
        ];
        private static readonly float[] FallbackLosZOffsets = [0f, 1.0f, 2.0f];
        private static readonly float[] RepairOffsetDistances = [1f, 2f, 3f, 4f, 6f, 8f, 10f, 12f, 16f];
        private static readonly float[] RepairAlongSegmentSamples = [0.25f, 0.35f, 0.5f, 0.65f, 0.75f];
        private const int MaxRepairIterations = 3;
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

        private readonly record struct GridNode(int X, int Y);
        private readonly record struct FallbackAttempt(float CellSize, float SearchMargin, float ExpansionFactor);

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

        [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl, EntryPoint = "ValidateWalkableSegment")]
        private static extern SegmentValidationCode ValidateWalkableSegmentNative(
            uint mapId,
            NativeXyz start,
            NativeXyz end,
            float radius,
            float height,
            out float resolvedEndZ,
            out float supportDelta,
            out float travelFraction);

        private const string EnableLosFallbackEnv = "WWOW_ENABLE_LOS_FALLBACK";
        private readonly Func<uint, XYZ, XYZ, bool, XYZ[]> _findPathResolver;
        private readonly Func<uint, XYZ, XYZ, SegmentEvaluation> _segmentEvaluator;

        public Navigation()
            : this(TryFindPathNative, EvaluateSegmentTraversalInternal)
        {
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

        public Navigation(
            Func<uint, XYZ, XYZ, bool, XYZ[]> findPathResolver,
            Func<uint, XYZ, XYZ, SegmentBlockReason> segmentBlockEvaluator)
            : this(
                findPathResolver,
                (mapId, from, to) => new SegmentEvaluation(
                    to,
                    (segmentBlockEvaluator ?? throw new ArgumentNullException(nameof(segmentBlockEvaluator)))(mapId, from, to)))
        {
        }

        public Navigation(
            Func<uint, XYZ, XYZ, bool, XYZ[]> findPathResolver,
            Func<uint, XYZ, XYZ, SegmentEvaluation> segmentEvaluator)
        {
            _findPathResolver = findPathResolver ?? throw new ArgumentNullException(nameof(findPathResolver));
            _segmentEvaluator = segmentEvaluator ?? throw new ArgumentNullException(nameof(segmentEvaluator));
        }

        public XYZ[] CalculatePath(uint mapId, XYZ start, XYZ end, bool smoothPath)
            => CalculateValidatedPath(mapId, start, end, smoothPath).Path;

        public NavigationPathResult CalculateValidatedPath(uint mapId, XYZ start, XYZ end, bool smoothPath,
            float agentRadius = DefaultAgentRadius, float agentHeight = DefaultAgentHeight)
        {
            // Build a scoped segment evaluator that uses the caller's capsule dimensions.
            // This avoids changing the constructor-injected delegate signature (which tests use).
            var scopedEvaluator = BuildScopedEvaluator(agentRadius, agentHeight);

            // Long straight-corner requests can spend tens of seconds in native route shaping
            // before the service ever gets to its alternate-mode fallback. For corpse-style
            // routes, prefer the validated smooth path first so the service stays responsive.
            var useAlternateModeFirst = ShouldTryAlternateModeFirst(start, end, smoothPath);
            var firstMode = useAlternateModeFirst ? !smoothPath : smoothPath;
            var firstResult = useAlternateModeFirst ? "native_path_alternate_mode" : "native_path";
            var secondMode = !firstMode;
            var secondResult = useAlternateModeFirst ? "native_path" : "native_path_alternate_mode";

            var firstAttempt = EvaluateNativePath(mapId, start, end, firstMode, firstResult, scopedEvaluator);
            Console.Error.WriteLine($"[NAV_DIAG] map={mapId} firstAttempt: mode={firstMode} pathLen={firstAttempt.Path.Length} blocked={firstAttempt.BlockedSegment?.Index.ToString() ?? "none"} reason={firstAttempt.BlockedSegment?.Reason.ToString() ?? "none"} usable={firstAttempt.IsUsable}");
            if (firstAttempt.IsUsable)
            {
                return new NavigationPathResult(
                    firstAttempt.Path,
                    firstAttempt.Path,
                    firstAttempt.SuccessResult,
                    null);
            }
            // If segment 0 is blocked, skip it — the player is already standing at
            // the start position, so the start→first-waypoint segment is traversable
            // by definition (Z mismatch or navmesh-edge capsule overlap).
            // Trust the navmesh for remaining segments — full re-validation is too expensive
            // (each segment costs ~800ms in native validator, 26+ segments = 20+ seconds).
            // The bot re-requests paths as it moves, so stale segments get re-evaluated.
            if (firstAttempt.Path.Length >= 2 && firstAttempt.BlockedSegment?.Index == 0)
            {
                Console.Error.WriteLine($"[NAV_DIAG] map={mapId} firstAttempt segment-0-skip accepted, pathLen={firstAttempt.Path.Length}");
                return new NavigationPathResult(firstAttempt.Path, firstAttempt.Path, firstAttempt.SuccessResult + "_seg0skip", null);
            }

            var secondAttempt = EvaluateNativePath(mapId, start, end, secondMode, secondResult, scopedEvaluator);
            Console.Error.WriteLine($"[NAV_DIAG] map={mapId} secondAttempt: mode={secondMode} pathLen={secondAttempt.Path.Length} blocked={secondAttempt.BlockedSegment?.Index.ToString() ?? "none"} reason={secondAttempt.BlockedSegment?.Reason.ToString() ?? "none"} usable={secondAttempt.IsUsable}");
            if (secondAttempt.IsUsable)
            {
                return new NavigationPathResult(
                    secondAttempt.Path,
                    secondAttempt.Path,
                    secondAttempt.SuccessResult,
                    null);
            }
            // Same segment-0 skip for the second attempt.
            if (secondAttempt.Path.Length >= 2 && secondAttempt.BlockedSegment?.Index == 0)
            {
                Console.Error.WriteLine($"[NAV_DIAG] map={mapId} secondAttempt segment-0-skip accepted, pathLen={secondAttempt.Path.Length}");
                return new NavigationPathResult(secondAttempt.Path, secondAttempt.Path, secondAttempt.SuccessResult + "_seg0skip", null);
            }

            var repairSource = SelectRepairSource(firstAttempt, secondAttempt);
            if (repairSource.Path.Length > 1 && repairSource.BlockedSegment is BlockedSegmentInfo repairBlockedSegment)
            {
                // Recursive repair: if repaired path also has blocked segments, try repairing again.
                // Time-box to prevent service hangs on expensive repair attempts.
                var repairDeadline = System.Diagnostics.Stopwatch.StartNew();
                const long RepairTimeoutMs = 3000;
                var currentPath = repairSource.Path;
                var currentBlocked = repairBlockedSegment;
                for (var iteration = 0; iteration < MaxRepairIterations; iteration++)
                {
                    if (repairDeadline.ElapsedMilliseconds > RepairTimeoutMs)
                    {
                        Console.Error.WriteLine($"[NAV_DIAG] map={mapId} repair timeout after {repairDeadline.ElapsedMilliseconds}ms");
                        break;
                    }
                    Console.Error.WriteLine($"[NAV_DIAG] map={mapId} repair iteration {iteration} at segment {currentBlocked.Index} reason={currentBlocked.Reason}");
                    var repairedPath = TryRepairPath(mapId, start, end, smoothPath, currentPath, currentBlocked.Index, scopedEvaluator);
                    Console.Error.WriteLine($"[NAV_DIAG] map={mapId} repair result: pathLen={repairedPath.Length}");
                    if (repairedPath.Length == 0)
                        break;

                    var nextBlocked = FindBlockedSegment(mapId, repairedPath, scopedEvaluator);
                    if (nextBlocked is null)
                    {
                        return new NavigationPathResult(
                            repairedPath,
                            repairSource.Path,
                            GetRepairResult(currentBlocked.Reason),
                            repairBlockedSegment.Index);
                    }

                    currentPath = repairedPath;
                    currentBlocked = nextBlocked.Value;
                }
            }

            // LOS fallback as last resort — always enabled.
            // When navmesh path + repair both fail, use grid-based A* with LOS checks.
            {
                Console.Error.WriteLine($"[NAV_DIAG] map={mapId} trying LOS fallback");
                var fallbackPath = BuildLosFallbackPath(mapId, start, end);
                Console.Error.WriteLine($"[NAV_DIAG] map={mapId} LOS fallback: pathLen={fallbackPath.Length}");
                if (fallbackPath.Length > 0 && FindBlockedSegment(mapId, fallbackPath, scopedEvaluator) is null)
                {
                    return new NavigationPathResult(
                        fallbackPath,
                        fallbackPath,
                        "los_fallback_path",
                        null);
                }
            }

            BlockedSegmentInfo? blockedSegment = firstAttempt.BlockedSegment is not null
                ? firstAttempt.BlockedSegment
                : secondAttempt.BlockedSegment;
            var rawPath = repairSource.Path.Length > 0 ? repairSource.Path : Array.Empty<XYZ>();
            var result = blockedSegment is null ? "no_path" : GetBlockedResult(blockedSegment.Value.Reason);
            Console.Error.WriteLine($"[NAV_DIAG] map={mapId} FINAL: result={result} rawPathLen={rawPath.Length} blocked={blockedSegment?.Index.ToString() ?? "none"} reason={blockedSegment?.Reason.ToString() ?? "none"}");
            return new NavigationPathResult(
                Array.Empty<XYZ>(),
                rawPath,
                result,
                blockedSegment?.Index);
        }

        /// <summary>
        /// Builds a segment evaluator that uses the specified capsule dimensions.
        /// When dimensions match the defaults, returns the constructor-injected evaluator unchanged.
        /// </summary>
        private Func<uint, XYZ, XYZ, SegmentEvaluation> BuildScopedEvaluator(float agentRadius, float agentHeight)
        {
            // If using default dimensions, return the constructor-injected evaluator as-is.
            // This preserves existing test behavior and avoids unnecessary closures.
            if (MathF.Abs(agentRadius - DefaultAgentRadius) < 0.001f
                && MathF.Abs(agentHeight - DefaultAgentHeight) < 0.001f)
            {
                return _segmentEvaluator;
            }

            // Build a new evaluator that calls the native validation with the actual capsule.
            return (mapId, from, to) => EvaluateSegmentWithDimensions(mapId, from, to, agentRadius, agentHeight);
        }

        private static bool ShouldTryAlternateModeFirst(XYZ start, XYZ end, bool smoothPath)
        {
            if (smoothPath)
                return false;

            return ComputeDistance2D(start, end) >= StraightPathLongRouteOverrideDistance2D;
        }

        private static bool IsLosFallbackEnabled()
        {
            var value = Environment.GetEnvironmentVariable(EnableLosFallbackEnv);
            if (string.IsNullOrWhiteSpace(value))
                return false;

            return value.Equals("1", StringComparison.OrdinalIgnoreCase)
                || value.Equals("true", StringComparison.OrdinalIgnoreCase)
                || value.Equals("yes", StringComparison.OrdinalIgnoreCase)
                || value.Equals("on", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsNativeSegmentValidationEnabled()
        {
            // ON by default — capsule-aware validation ensures paths are physically traversable.
            // Set WWOW_ENABLE_NATIVE_SEGMENT_VALIDATION=0 to disable.
            var value = Environment.GetEnvironmentVariable(EnableNativeSegmentValidationEnv);
            if (string.IsNullOrWhiteSpace(value))
                return true;

            return !value.Equals("0", StringComparison.OrdinalIgnoreCase)
                && !value.Equals("false", StringComparison.OrdinalIgnoreCase)
                && !value.Equals("off", StringComparison.OrdinalIgnoreCase)
                && !value.Equals("no", StringComparison.OrdinalIgnoreCase);
        }

        private static float ComputeDistance2D(XYZ start, XYZ end)
        {
            var dx = end.X - start.X;
            var dy = end.Y - start.Y;
            return MathF.Sqrt((dx * dx) + (dy * dy));
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
        /// Maximum number of leading segments to validate in the initial path check.
        /// Full-path validation is too expensive (~1.5s per segment); validating only the first
        /// few segments keeps the service responsive.  The bot re-requests paths as it moves,
        /// so later segments get validated on subsequent requests when they become segment 0/1.
        /// </summary>
        private const int MaxLeadingSegmentsToValidate = 3;

        private PathAttempt EvaluateNativePath(uint mapId, XYZ start, XYZ end, bool smoothPath, string successResult,
            Func<uint, XYZ, XYZ, SegmentEvaluation>? scopedEvaluator = null)
        {
            try
            {
                var path = _findPathResolver(mapId, start, end, smoothPath);
                if (path.Length == 0)
                    return new PathAttempt(Array.Empty<XYZ>(), successResult, null);

                // Only validate the first few segments — full-path validation is too expensive
                // for smooth paths (48 segments × ~1.5s each = 72s).  The navmesh guarantees
                // structural traversability; we validate the leading edge to catch start-position
                // issues (Z mismatch, capsule overlap near navmesh edges).
                var maxSegment = Math.Min(MaxLeadingSegmentsToValidate, path.Length - 1);
                var evaluator = scopedEvaluator ?? _segmentEvaluator;
                BlockedSegmentInfo? blockedSegment = null;
                var current = path[0];
                for (var i = 0; i < maxSegment; i++)
                {
                    var evaluation = evaluator(mapId, current, path[i + 1]);
                    if (evaluation.Reason != SegmentBlockReason.None)
                    {
                        blockedSegment = new BlockedSegmentInfo(i, evaluation.Reason);
                        break;
                    }
                    current = evaluation.EffectiveEnd;
                }

                return new PathAttempt(path, successResult, blockedSegment);
            }
            catch
            {
                return new PathAttempt(Array.Empty<XYZ>(), successResult, null);
            }
        }

        private static PathAttempt SelectRepairSource(PathAttempt preferredAttempt, PathAttempt alternateAttempt)
        {
            if (preferredAttempt.Path.Length > 0 && preferredAttempt.BlockedSegment is not null)
                return preferredAttempt;

            if (alternateAttempt.Path.Length > 0 && alternateAttempt.BlockedSegment is not null)
                return alternateAttempt;

            return preferredAttempt.Path.Length >= alternateAttempt.Path.Length
                ? preferredAttempt
                : alternateAttempt;
        }

        private XYZ[] TryRepairPath(uint mapId, XYZ start, XYZ end, bool smoothPath, XYZ[] rawPath, int blockedSegmentIndex,
            Func<uint, XYZ, XYZ, SegmentEvaluation>? scopedEvaluator = null)
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

                var blockedCandidateSegment = FindBlockedSegment(mapId, repaired, scopedEvaluator);
                if (blockedCandidateSegment is not null)
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

        private IEnumerable<XYZ> BuildRepairCandidates(XYZ blockedStart, XYZ blockedEnd)
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
                var firstLeg = _findPathResolver(mapId, start, candidate, firstLegSmooth);
                if (firstLeg.Length == 0)
                    continue;

                foreach (var secondLegSmooth in EnumeratePathModes(smoothPath))
                {
                    var secondLeg = _findPathResolver(mapId, candidate, end, secondLegSmooth);
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
            {
                combined.Add(secondLeg[i]);
            }

            return combined.ToArray();
        }

        private BlockedSegmentInfo? FindBlockedSegment(uint mapId, XYZ[] path,
            Func<uint, XYZ, XYZ, SegmentEvaluation>? scopedEvaluator = null,
            int startIndex = 0)
        {
            if (path.Length < 2)
                return null;

            var evaluator = scopedEvaluator ?? _segmentEvaluator;
            var start = Math.Max(0, Math.Min(startIndex, path.Length - 2));
            var current = path[start];
            for (var i = start; i < path.Length - 1; i++)
            {
                var evaluation = evaluator(mapId, current, path[i + 1]);
                if (evaluation.Reason != SegmentBlockReason.None)
                    return new BlockedSegmentInfo(i, evaluation.Reason);

                current = evaluation.EffectiveEnd;
            }

            return null;
        }

        private static float ComputePathCost(XYZ[] path)
        {
            if (path.Length < 2)
                return float.MaxValue;

            var cost = 0f;
            for (var i = 1; i < path.Length; i++)
            {
                cost += Distance3D(path[i - 1], path[i]);
            }

            return cost;
        }

        private static XYZ[] BuildLosFallbackPath(uint mapId, XYZ start, XYZ end)
        {
            var routeDistance = Distance2D(start, end);
            if (routeDistance < 0.5f)
                return [start, end];

            if (TryHasLosForFallback(mapId, start, end))
                return BuildSegmentedPath(start, end, FallbackMinCellSize);

            foreach (var attempt in BuildFallbackAttempts(routeDistance))
            {
                var path = TryBuildLosFallbackPath(mapId, start, end, attempt);
                if (path.Length > 0)
                    return path;
            }

            return Array.Empty<XYZ>();
        }

        private static FallbackAttempt[] BuildFallbackAttempts(float routeDistance)
        {
            var firstCell = routeDistance > 2200f
                ? FallbackVeryLongRouteCellSize
                : (routeDistance > 1200f ? FallbackLongRouteCellSize : (routeDistance > 700f ? FallbackMediumCellSize : FallbackMinCellSize));
            var secondCell = MathF.Max(FallbackMinCellSize, firstCell - 2f);

            var baseMargin = Math.Clamp(routeDistance * 0.35f, FallbackMinSearchMargin, 700f);
            var widenedMargin = Math.Clamp(baseMargin + 220f, FallbackMinSearchMargin, 950f);
            var maxMargin = Math.Clamp(baseMargin + 420f, FallbackMinSearchMargin, FallbackMaxSearchMargin);

            return
            [
                new FallbackAttempt(firstCell, baseMargin, 0.55f),
                new FallbackAttempt(secondCell, widenedMargin, 0.75f),
                new FallbackAttempt(FallbackMinCellSize, maxMargin, 0.95f),
            ];
        }

        private static XYZ[] TryBuildLosFallbackPath(uint mapId, XYZ start, XYZ end, FallbackAttempt attempt)
        {
            var cellSize = attempt.CellSize;
            var searchMargin = attempt.SearchMargin;

            var minX = MathF.Min(start.X, end.X) - searchMargin;
            var maxX = MathF.Max(start.X, end.X) + searchMargin;
            var minY = MathF.Min(start.Y, end.Y) - searchMargin;
            var maxY = MathF.Max(start.Y, end.Y) + searchMargin;

            static int ToGrid(float value, float min, float cell) =>
                (int)MathF.Round((value - min) / cell);

            static float ToWorld(int grid, float min, float cell) =>
                min + (grid * cell);

            XYZ GridToWorld(GridNode node)
            {
                var x = ToWorld(node.X, minX, cellSize);
                var y = ToWorld(node.Y, minY, cellSize);
                var total = Distance2D(start, end);
                var traveled = Distance2D(start, new XYZ(x, y, start.Z));
                var t = total <= 0.001f ? 0f : Math.Clamp(traveled / total, 0f, 1f);
                var z = start.Z + ((end.Z - start.Z) * t);
                return new XYZ(x, y, z);
            }

            bool InBounds(GridNode node)
            {
                var x = ToWorld(node.X, minX, cellSize);
                var y = ToWorld(node.Y, minY, cellSize);
                return x >= minX && x <= maxX && y >= minY && y <= maxY;
            }

            var startNode = new GridNode(ToGrid(start.X, minX, cellSize), ToGrid(start.Y, minY, cellSize));
            var targetNode = new GridNode(ToGrid(end.X, minX, cellSize), ToGrid(end.Y, minY, cellSize));

            var gridWidth = Math.Max(1, (int)MathF.Ceiling((maxX - minX) / cellSize) + 1);
            var gridHeight = Math.Max(1, (int)MathF.Ceiling((maxY - minY) / cellSize) + 1);
            var maxExpandedNodes = Math.Clamp(
                (int)(gridWidth * gridHeight * attempt.ExpansionFactor),
                FallbackMinExpandedNodes,
                FallbackMaxExpandedNodesCap);

            var open = new PriorityQueue<GridNode, float>();
            var closed = new HashSet<GridNode>();
            var cameFrom = new Dictionary<GridNode, GridNode>();
            var gScore = new Dictionary<GridNode, float> { [startNode] = 0f };

            open.Enqueue(startNode, Heuristic(startNode, targetNode));

            var expanded = 0;
            GridNode? reached = null;
            var completionRadius = MathF.Max(FallbackGoalDistance, cellSize * 1.5f);

            while (open.Count > 0 && expanded < maxExpandedNodes)
            {
                var current = open.Dequeue();
                if (!closed.Add(current))
                    continue;

                expanded++;
                var currentWorld = GridToWorld(current);

                if (Distance2D(currentWorld, end) <= completionRadius && TryHasLosForFallback(mapId, currentWorld, end))
                {
                    reached = current;
                    break;
                }

                foreach (var (dx, dy) in NeighborOffsets)
                {
                    var next = new GridNode(current.X + dx, current.Y + dy);
                    if (closed.Contains(next) || !InBounds(next))
                        continue;

                    var nextWorld = GridToWorld(next);
                    if (!TryHasLosForFallback(mapId, currentWorld, nextWorld))
                        continue;

                    var tentative = gScore[current] + Distance2D(currentWorld, nextWorld);
                    if (gScore.TryGetValue(next, out var existing) && tentative >= existing)
                        continue;

                    gScore[next] = tentative;
                    cameFrom[next] = current;
                    var priority = tentative + Heuristic(next, targetNode);
                    open.Enqueue(next, priority);
                }
            }

            if (reached == null)
                return Array.Empty<XYZ>();

            var chain = new List<GridNode>();
            var cursor = reached.Value;
            chain.Add(cursor);
            while (cameFrom.TryGetValue(cursor, out var parent))
            {
                cursor = parent;
                chain.Add(cursor);
            }
            chain.Reverse();

            var waypoints = new List<XYZ>(chain.Count + 2) { start };
            foreach (var node in chain)
            {
                var point = GridToWorld(node);
                if (Distance2D(waypoints[^1], point) > 0.5f)
                    waypoints.Add(point);
            }

            if (Distance2D(waypoints[^1], end) > 0.5f)
                waypoints.Add(end);

            return SimplifyByLos(mapId, waypoints);
        }

        private static XYZ[] BuildSegmentedPath(XYZ start, XYZ end, float segmentLength)
        {
            var distance = Distance2D(start, end);
            if (distance < 0.5f)
                return [start, end];

            var segmentCount = Math.Max(1, (int)MathF.Ceiling(distance / segmentLength));
            var path = new XYZ[segmentCount + 1];
            path[0] = start;

            for (int i = 1; i < segmentCount; i++)
            {
                var t = i / (float)segmentCount;
                path[i] = new XYZ(
                    start.X + ((end.X - start.X) * t),
                    start.Y + ((end.Y - start.Y) * t),
                    start.Z + ((end.Z - start.Z) * t));
            }

            path[^1] = end;
            return path;
        }

        private static XYZ[] SimplifyByLos(uint mapId, List<XYZ> points)
        {
            if (points.Count <= 2)
                return points.ToArray();

            var simplified = new List<XYZ>(points.Count) { points[0] };
            var anchor = 0;

            while (anchor < points.Count - 1)
            {
                var next = anchor + 1;
                for (int i = points.Count - 1; i > anchor + 1; i--)
                {
                    if (TryHasLosForFallback(mapId, points[anchor], points[i]))
                    {
                        next = i;
                        break;
                    }
                }

                simplified.Add(points[next]);
                anchor = next;
            }

            return simplified.ToArray();
        }

        private static bool TryHasLos(uint mapId, XYZ from, XYZ to)
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

        private static bool TryHasLosForFallback(uint mapId, XYZ from, XYZ to)
        {
            if (TryHasLos(mapId, from, to))
                return true;

            foreach (var fromOffset in FallbackLosZOffsets)
            {
                foreach (var toOffset in FallbackLosZOffsets)
                {
                    if (fromOffset == 0f && toOffset == 0f)
                        continue;

                    var fromProbe = new XYZ(from.X, from.Y, from.Z + fromOffset);
                    var toProbe = new XYZ(to.X, to.Y, to.Z + toOffset);
                    if (TryHasLos(mapId, fromProbe, toProbe))
                        return true;
                }
            }

            return false;
        }

        private static float Heuristic(GridNode a, GridNode b)
            => MathF.Abs(a.X - b.X) + MathF.Abs(a.Y - b.Y);

        private static float Distance2D(XYZ a, XYZ b)
        {
            var dx = a.X - b.X;
            var dy = a.Y - b.Y;
            return MathF.Sqrt((dx * dx) + (dy * dy));
        }

        private static float Distance3D(XYZ a, XYZ b)
        {
            var dx = a.X - b.X;
            var dy = a.Y - b.Y;
            var dz = a.Z - b.Z;
            return MathF.Sqrt((dx * dx) + (dy * dy) + (dz * dz));
        }

        public bool SegmentIntersectsDynamicObjects(uint mapId, float x0, float y0, float z0, float x1, float y1, float z1)
            => SegmentIntersectsDynamicObjectsInternal(mapId, new XYZ(x0, y0, z0), new XYZ(x1, y1, z1));

        private static bool SegmentIntersectsDynamicObjectsInternal(uint mapId, XYZ from, XYZ to)
        {
            try
            {
                return SegmentIntersectsDynamicObjectsNative(mapId, from.X, from.Y, from.Z, to.X, to.Y, to.Z);
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Evaluates a path segment using the specified capsule dimensions instead of defaults.
        /// Used when race/gender provide actual bot dimensions.
        /// </summary>
        private static SegmentEvaluation EvaluateSegmentWithDimensions(uint mapId, XYZ from, XYZ to,
            float agentRadius, float agentHeight)
        {
            if (!IsNativeSegmentValidationEnabled())
                return new SegmentEvaluation(to, SegmentBlockReason.None);

            if (Distance2D(from, to) > NativeWalkabilityMaxSegmentLength)
                return new SegmentEvaluation(to, SegmentBlockReason.None);

            try
            {
                var validation = ValidateWalkableSegmentNative(
                    mapId,
                    new NativeXyz(from),
                    new NativeXyz(to),
                    agentRadius,
                    agentHeight,
                    out var resolvedEndZ,
                    out _,
                    out _);

                var effectiveEnd = validation == SegmentValidationCode.Clear && float.IsFinite(resolvedEndZ)
                    ? new XYZ(to.X, to.Y, resolvedEndZ)
                    : to;

                var reason = validation switch
                {
                    SegmentValidationCode.Clear => SegmentBlockReason.None,
                    SegmentValidationCode.BlockedGeometry => SegmentBlockReason.CapsuleValidation,
                    SegmentValidationCode.MissingSupport => SegmentBlockReason.None,
                    SegmentValidationCode.StepUpTooHigh => SegmentBlockReason.StepUpLimit,
                    SegmentValidationCode.StepDownTooFar => SegmentBlockReason.StepDownLimit,
                    _ => SegmentBlockReason.None,
                };

                return new SegmentEvaluation(effectiveEnd, reason);
            }
            catch
            {
                return new SegmentEvaluation(to, SegmentBlockReason.None);
            }
        }

        private static SegmentEvaluation EvaluateSegmentTraversalInternal(uint mapId, XYZ from, XYZ to)
        {
            // Dynamic object intersection is diagnostic-only for path validation.
            // Small objects (campfires, decorations) have collision geometry that
            // intersects paths but doesn't actually block player movement. The overlay
            // system is primarily for LOS checks and physics simulation, not path rejection.
            // Blocking here caused ALL paths to be rejected in areas with any nearby objects.

            if (!IsNativeSegmentValidationEnabled())
                return new SegmentEvaluation(to, SegmentBlockReason.None);

            if (Distance2D(from, to) > NativeWalkabilityMaxSegmentLength)
                return new SegmentEvaluation(to, SegmentBlockReason.None);

            try
            {
                var validation = ValidateWalkableSegmentNative(
                    mapId,
                    new NativeXyz(from),
                    new NativeXyz(to),
                    DefaultAgentRadius,
                    DefaultAgentHeight,
                    out var resolvedEndZ,
                    out _,
                    out _);

                var effectiveEnd = validation == SegmentValidationCode.Clear && float.IsFinite(resolvedEndZ)
                    ? new XYZ(to.X, to.Y, resolvedEndZ)
                    : to;

                var reason = validation switch
                {
                    SegmentValidationCode.Clear => SegmentBlockReason.None,
                    SegmentValidationCode.BlockedGeometry => SegmentBlockReason.CapsuleValidation,
                    // Support probes still have known false negatives on some valid mmap
                    // segments, so keep them diagnostic-only until the native query is
                    // proven stable enough to block service paths outright.
                    SegmentValidationCode.MissingSupport => SegmentBlockReason.None,
                    SegmentValidationCode.StepUpTooHigh => SegmentBlockReason.StepUpLimit,
                    SegmentValidationCode.StepDownTooFar => SegmentBlockReason.StepDownLimit,
                    _ => SegmentBlockReason.None,
                };

                return new SegmentEvaluation(effectiveEnd, reason);
            }
            catch
            {
                return new SegmentEvaluation(to, SegmentBlockReason.None);
            }
        }

        private static string GetRepairResult(SegmentBlockReason reason)
            => reason == SegmentBlockReason.DynamicOverlay
                ? "repaired_dynamic_overlay"
                : "repaired_segment_validation";

        private static string GetBlockedResult(SegmentBlockReason reason)
            => reason switch
            {
                SegmentBlockReason.DynamicOverlay => "blocked_by_dynamic_overlay",
                SegmentBlockReason.SupportSurface => "blocked_by_support_surface",
                SegmentBlockReason.StepUpLimit => "blocked_by_step_up_limit",
                SegmentBlockReason.StepDownLimit => "blocked_by_step_down_limit",
                SegmentBlockReason.CapsuleValidation => "blocked_by_capsule_validation",
                _ => "no_path",
            };

        private readonly record struct BlockedSegmentInfo(
            int Index,
            SegmentBlockReason Reason);

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

        private enum SegmentValidationCode : uint
        {
            Clear = 0,
            BlockedGeometry = 1,
            MissingSupport = 2,
            StepUpTooHigh = 3,
            StepDownTooFar = 4,
        }

        private readonly record struct PathAttempt(
            XYZ[] Path,
            string SuccessResult,
            BlockedSegmentInfo? BlockedSegment)
        {
            public bool IsUsable => Path.Length > 0 && BlockedSegment is null;
        }
    }
}
