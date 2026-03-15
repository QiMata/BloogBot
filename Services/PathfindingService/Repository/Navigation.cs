using GameData.Core.Models;
using System;
using System.Collections.Generic;
using System.Linq;
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

            var path = waypoints.ToArray();
            Console.Error.WriteLine($"[CORRIDOR] map={mapId} corners={corridorResult.CornerCount} path=[{string.Join(" -> ", path.Select(p => $"({p.X:F1},{p.Y:F1},{p.Z:F1})"))}]");

            return new NavigationPathResult(path, path, "corridor_path", null);
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
