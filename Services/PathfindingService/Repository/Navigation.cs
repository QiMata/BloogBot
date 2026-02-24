using GameData.Core.Models;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace PathfindingService.Repository
{
    public class Navigation
    {
        private const string DLL_NAME = "Navigation.dll";
        private const float FallbackCellSize = 6f;
        private const float FallbackSearchMargin = 60f;
        private const float FallbackGoalDistance = 9f;
        private const int FallbackMaxExpandedNodes = 2500;

        private static readonly (int X, int Y)[] NeighborOffsets =
        [
            (1, 0), (-1, 0), (0, 1), (0, -1),
            (1, 1), (1, -1), (-1, 1), (-1, -1),
        ];

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

        public XYZ[] CalculatePath(uint mapId, XYZ start, XYZ end, bool smoothPath)
        {
            try
            {
                IntPtr pathPtr = FindPath(
                    mapId,
                    new NativeXyz(start),
                    new NativeXyz(end),
                    smoothPath,
                    out int length);

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
            }
            catch
            {
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
        }
    }
}