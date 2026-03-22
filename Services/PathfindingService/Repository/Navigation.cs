using GameData.Core.Models;
using System;
using System.Collections.Generic;
<<<<<<< HEAD
=======
using System.Diagnostics;
using System.Linq;
>>>>>>> cpp_physics_system
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
        private const float FallbackCellSize = 6f;
        private const float FallbackSearchMargin = 60f;
        private const float FallbackGoalDistance = 9f;
        private const int FallbackMaxExpandedNodes = 2500;

<<<<<<< HEAD
        private static readonly (int X, int Y)[] NeighborOffsets =
        [
            (1, 0), (-1, 0), (0, 1), (0, -1),
            (1, 1), (1, -1), (-1, 1), (-1, -1),
        ];

=======
>>>>>>> cpp_physics_system
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

<<<<<<< HEAD
        private readonly record struct GridNode(int X, int Y);
=======
        // ── Legacy P/Invoke (used by DiagnosticFindPath, CalculatePath fallback) ──
>>>>>>> cpp_physics_system

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

<<<<<<< HEAD
        public XYZ[] CalculatePath(uint mapId, XYZ start, XYZ end, bool smoothPath)
        {
            try
            {
                IntPtr pathPtr = FindPath(
=======
        [DllImport(DLL_NAME, CallingConvention = CallingConvention.Cdecl, EntryPoint = "SegmentIntersectsDynamicObjects")]
        [return: MarshalAs(UnmanagedType.I1)]
        private static extern bool SegmentIntersectsDynamicObjectsNative(
            uint mapId, float x0, float y0, float z0, float x1, float y1, float z1);

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
        }

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

        // ── Constructors ──

        public Navigation() { }

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

        public NavigationPathResult CalculateValidatedPath(uint mapId, XYZ start, XYZ end, bool smoothPath,
            float agentRadius = 0.6f, float agentHeight = 2.0f)
        {
            // Corridor-based pathfinding: uses Detour's dtPathCorridor for collision-aware
            // path generation constrained to the navmesh surface. Replaces the old
            // ValidateWalkableSegment physics sweep pipeline.
            var nativeStart = new NativeXyz(start);
            var nativeEnd = new NativeXyz(end);

            CorridorResultNative corridorResult;
            try
            {
                corridorResult = FindPathCorridor(mapId, nativeStart, nativeEnd);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"[CORRIDOR] FindPathCorridor native call failed: {ex.Message}");
                return new NavigationPathResult(Array.Empty<XYZ>(), Array.Empty<XYZ>(), "corridor_native_error", null);
            }

            if (corridorResult.Handle == 0 || corridorResult.CornerCount == 0)
            {
                Console.Error.WriteLine($"[CORRIDOR] No corridor path found for map={mapId} start=({start.X:F1},{start.Y:F1},{start.Z:F1}) end=({end.X:F1},{end.Y:F1},{end.Z:F1})");
                return new NavigationPathResult(Array.Empty<XYZ>(), Array.Empty<XYZ>(), "no_path", null);
            }

            // Extract corners from the corridor result into XYZ waypoints.
            var waypoints = new List<XYZ>(corridorResult.CornerCount + 1);

            // Use the original requested start as the first waypoint, not the navmesh-snapped
            // position. The navmesh ground may be several yards below the actual player Z.
            waypoints.Add(start);

            for (int i = 0; i < corridorResult.CornerCount; i++)
            {
                var x = corridorResult.Corners[i * 3];
                var y = corridorResult.Corners[i * 3 + 1];
                var z = corridorResult.Corners[i * 3 + 2];
                waypoints.Add(new XYZ(x, y, z));
            }

            // Destroy the corridor — stateless per-request for now.
            // Future: keep corridor alive for incremental updates via CorridorUpdate.
            try { CorridorDestroy(corridorResult.Handle); } catch { /* ignore cleanup errors */ }

            var rawPath = waypoints.ToArray();
            Console.Error.WriteLine($"[CORRIDOR] map={mapId} corners={corridorResult.CornerCount} path=[{string.Join(" -> ", rawPath.Select(p => $"({p.X:F1},{p.Y:F1},{p.Z:F1})"))}]");

            // Post-corridor segment validation DISABLED.
            // ValidateWalkableSegment physics capsule sweeps cost 5-28 SECONDS per segment,
            // causing PathfindingService hangs and crashes during extended test runs.
            // The corridor path is already navmesh-constrained via dtPathCorridor — walkable
            // by construction. Runtime physics (collide-and-slide) handles any minor obstacles
            // the navmesh doesn't account for. Re-enable only if corridor paths reliably route
            // through solid geometry (not observed).
            return new NavigationPathResult(rawPath, rawPath, "corridor_path", null);
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

        private static XYZ[] ValidateCorridorSegments(uint mapId, XYZ[] path, float radius, float height, out int? firstBlockedIdx)
        {
            firstBlockedIdx = null;
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

                uint validationCode;
                try
                {
                    validationCode = ValidateWalkableSegment(
                        mapId,
                        new NativeXyz(segStart),
                        new NativeXyz(segEnd),
                        radius,
                        height,
                        out _,
                        out _,
                        out _);
                }
                catch
                {
                    // Native call failed — accept the segment as-is
                    result.Add(segEnd);
                    continue;
                }

                if (validationCode == 0) // SegmentValidationClear
                {
                    result.Add(segEnd);
                    continue;
                }

                // Segment blocked — try lateral offsets to route around the obstacle,
                // but only if we have time budget remaining.
                if (!firstBlockedIdx.HasValue) firstBlockedIdx = i;

                if (sw.Elapsed > SegmentValidationBudget)
                {
                    Console.Error.WriteLine($"[CORRIDOR-BLOCKED] seg {i}: blocked code={validationCode}, no time for repair, accepting as-is");
                    result.Add(segEnd);
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
                            result.Add(midPoint);
                            result.Add(segEnd);
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
                    result.Add(segEnd);
                }
            }

            if (repairCount > 0)
                Console.Error.WriteLine($"[CORRIDOR-VALIDATE] {repairCount} segments repaired out of {path.Length - 1} ({sw.ElapsedMilliseconds}ms)");

            return result.ToArray();
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
>>>>>>> cpp_physics_system
                    mapId,
                    new NativeXyz(start),
                    new NativeXyz(end),
                    smoothPath,
                    out int length);

<<<<<<< HEAD
                if (pathPtr == IntPtr.Zero || length == 0)
                    return BuildLosFallbackPath(mapId, start, end);

                try
                {
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
                    PathArrFree(pathPtr);
                }
=======
                if (pathPtr == IntPtr.Zero || length <= 0)
                    return Array.Empty<XYZ>();

                XYZ[] path = new XYZ[length];
                for (int i = 0; i < length; i++)
                {
                    IntPtr currentPtr = IntPtr.Add(pathPtr, i * Marshal.SizeOf<NativeXyz>());
                    path[i] = Marshal.PtrToStructure<NativeXyz>(currentPtr).ToManaged();
                }

                return path;
>>>>>>> cpp_physics_system
            }
            catch
            {
<<<<<<< HEAD
                return BuildLosFallbackPath(mapId, start, end);
            }
        }

        private static XYZ[] BuildLosFallbackPath(uint mapId, XYZ start, XYZ end)
        {
            if (Distance2D(start, end) < 0.5f)
                return [start, end];

            if (TryHasLos(mapId, start, end))
                return BuildSegmentedPath(start, end, FallbackCellSize);

            var minX = MathF.Min(start.X, end.X) - FallbackSearchMargin;
            var maxX = MathF.Max(start.X, end.X) + FallbackSearchMargin;
            var minY = MathF.Min(start.Y, end.Y) - FallbackSearchMargin;
            var maxY = MathF.Max(start.Y, end.Y) + FallbackSearchMargin;

            static int ToGrid(float value, float min, float cell) =>
                (int)MathF.Round((value - min) / cell);

            static float ToWorld(int grid, float min, float cell) =>
                min + (grid * cell);

            XYZ GridToWorld(GridNode node)
            {
                var x = ToWorld(node.X, minX, FallbackCellSize);
                var y = ToWorld(node.Y, minY, FallbackCellSize);
                var total = Distance2D(start, end);
                var traveled = Distance2D(start, new XYZ(x, y, start.Z));
                var t = total <= 0.001f ? 0f : Math.Clamp(traveled / total, 0f, 1f);
                var z = start.Z + ((end.Z - start.Z) * t);
                return new XYZ(x, y, z);
            }

            bool InBounds(GridNode node)
            {
                var x = ToWorld(node.X, minX, FallbackCellSize);
                var y = ToWorld(node.Y, minY, FallbackCellSize);
                return x >= minX && x <= maxX && y >= minY && y <= maxY;
            }

            var startNode = new GridNode(ToGrid(start.X, minX, FallbackCellSize), ToGrid(start.Y, minY, FallbackCellSize));
            var targetNode = new GridNode(ToGrid(end.X, minX, FallbackCellSize), ToGrid(end.Y, minY, FallbackCellSize));

            var open = new PriorityQueue<GridNode, float>();
            var closed = new HashSet<GridNode>();
            var cameFrom = new Dictionary<GridNode, GridNode>();
            var gScore = new Dictionary<GridNode, float> { [startNode] = 0f };

            open.Enqueue(startNode, Heuristic(startNode, targetNode));

            var expanded = 0;
            GridNode? reached = null;

            while (open.Count > 0 && expanded < FallbackMaxExpandedNodes)
            {
                var current = open.Dequeue();
                if (!closed.Add(current))
                    continue;

                expanded++;
                var currentWorld = GridToWorld(current);

                if (Distance2D(currentWorld, end) <= FallbackGoalDistance && TryHasLos(mapId, currentWorld, end))
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
                    if (!TryHasLos(mapId, currentWorld, nextWorld))
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
                    if (TryHasLos(mapId, points[anchor], points[i]))
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

        private static float Heuristic(GridNode a, GridNode b)
            => MathF.Abs(a.X - b.X) + MathF.Abs(a.Y - b.Y);

        private static float Distance2D(XYZ a, XYZ b)
        {
            var dx = a.X - b.X;
            var dy = a.Y - b.Y;
            return MathF.Sqrt((dx * dx) + (dy * dy));
=======
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
>>>>>>> cpp_physics_system
        }
    }
}
