using GameData.Core.Models;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace PathfindingService.Repository
{
    public class Navigation
    {
        private const string DLL_NAME = "Navigation.dll";
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

        private const string EnableLosFallbackEnv = "WWOW_ENABLE_LOS_FALLBACK";

        public XYZ[] CalculatePath(uint mapId, XYZ start, XYZ end, bool smoothPath)
        {
            try
            {
                var preferredPath = TryFindPathNative(mapId, start, end, smoothPath);
                if (preferredPath.Length > 0)
                    return preferredPath;

                var alternatePath = TryFindPathNative(mapId, start, end, !smoothPath);
                if (alternatePath.Length > 0)
                    return alternatePath;
            }
            catch
            {
            }

            // Keep native navmesh as the authoritative source by default.
            // LOS-grid fallback can create wall-hugging routes that are valid in LOS
            // but not walkable in practice (notably during corpse runback).
            if (IsLosFallbackEnabled())
                return BuildLosFallbackPath(mapId, start, end);

            return Array.Empty<XYZ>();
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
    }
}
