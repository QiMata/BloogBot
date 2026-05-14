using System;
using System.Runtime.InteropServices;
using GameData.Core.Models;

namespace PathfindingService.Tests;

internal static class NavigationInterop
{
    private const string NavigationDll = "Navigation.dll";

    [DllImport(NavigationDll, EntryPoint = "FindPathForAgent", CallingConvention = CallingConvention.Cdecl)]
    private static extern IntPtr FindPathForAgent(
        uint mapId,
        XYZ start,
        XYZ end,
        [MarshalAs(UnmanagedType.I1)] bool smoothPath,
        float agentRadius,
        float agentHeight,
        out int length);

    [DllImport(NavigationDll, EntryPoint = "PathArrFree", CallingConvention = CallingConvention.Cdecl)]
    private static extern void PathArrFree(IntPtr pathArr);

    public readonly record struct SmoothPathResult(
        bool Success,
        int Length,
        XYZ[] Waypoints);

    /// <summary>
    /// Calls Navigation.dll's FindPathForAgent with smoothPath=true and
    /// returns the full smooth waypoint sequence the bot would follow. The
    /// bot's NavigationPath consumes this exact sequence, so a synthetic
    /// in-space waypoint here translates directly to a stuck bot.
    /// </summary>
    public static SmoothPathResult QuerySmoothPath(
        uint mapId,
        XYZ start,
        XYZ end,
        float agentRadius,
        float agentHeight)
    {
        IntPtr pathPtr = IntPtr.Zero;
        try
        {
            pathPtr = FindPathForAgent(mapId, start, end, smoothPath: true, agentRadius, agentHeight, out int length);
            if (pathPtr == IntPtr.Zero || length <= 0)
                return new SmoothPathResult(false, 0, Array.Empty<XYZ>());

            var floatBuf = new float[length * 3];
            Marshal.Copy(pathPtr, floatBuf, 0, floatBuf.Length);
            var waypoints = new XYZ[length];
            for (int i = 0; i < length; i++)
                waypoints[i] = new XYZ(floatBuf[i * 3 + 0], floatBuf[i * 3 + 1], floatBuf[i * 3 + 2]);
            return new SmoothPathResult(true, length, waypoints);
        }
        finally
        {
            if (pathPtr != IntPtr.Zero)
                PathArrFree(pathPtr);
        }
    }

    [DllImport(NavigationDll, EntryPoint = "FindPathPolygonsForAgent", CallingConvention = CallingConvention.Cdecl)]
    [return: MarshalAs(UnmanagedType.I1)]
    private static extern bool FindPathPolygonsForAgent(
        uint mapId,
        XYZ start,
        XYZ end,
        float agentRadius,
        float agentHeight,
        [Out] ulong[] outPolyRefs,
        [Out] byte[] outPolyTypes,
        int maxOut,
        out int outCount);

    public enum PolyType : byte
    {
        Ground = 0,
        OffMeshConnection = 1,
        Unknown = 0xFF,
    }

    public readonly record struct PolygonPathResult(
        bool Success,
        int TotalPolyCount,
        ulong[] PolyRefs,
        PolyType[] PolyTypes)
    {
        public bool ContainsOffMeshPoly
        {
            get
            {
                foreach (var t in PolyTypes)
                {
                    if (t == PolyType.OffMeshConnection)
                        return true;
                }
                return false;
            }
        }

        public int OffMeshPolyCount
        {
            get
            {
                int n = 0;
                foreach (var t in PolyTypes)
                {
                    if (t == PolyType.OffMeshConnection)
                        ++n;
                }
                return n;
            }
        }
    }

    [DllImport(NavigationDll, EntryPoint = "CountLinkedOffMeshPolysOnMap", CallingConvention = CallingConvention.Cdecl)]
    [return: MarshalAs(UnmanagedType.I1)]
    private static extern bool CountLinkedOffMeshPolysOnMap(
        uint mapId, out int outTotal, out int outLinked);

    public readonly record struct OffMeshLinkCounts(bool Success, int Total, int Linked);

    public static OffMeshLinkCounts QueryOffMeshLinkCounts(uint mapId)
    {
        bool ok = CountLinkedOffMeshPolysOnMap(mapId, out int total, out int linked);
        return new OffMeshLinkCounts(ok, total, linked);
    }

    public static PolygonPathResult QueryPathPolygons(
        uint mapId,
        XYZ start,
        XYZ end,
        float agentRadius,
        float agentHeight,
        int maxOut = 740)
    {
        if (maxOut <= 0)
            throw new ArgumentOutOfRangeException(nameof(maxOut));

        var refs = new ulong[maxOut];
        var types = new byte[maxOut];
        bool ok = FindPathPolygonsForAgent(
            mapId, start, end, agentRadius, agentHeight, refs, types, maxOut, out int total);

        if (!ok)
            return new PolygonPathResult(false, 0, Array.Empty<ulong>(), Array.Empty<PolyType>());

        int written = total < maxOut ? total : maxOut;
        var refsOut = new ulong[written];
        var typesOut = new PolyType[written];
        for (int i = 0; i < written; i++)
        {
            refsOut[i] = refs[i];
            typesOut[i] = (PolyType)types[i];
        }

        return new PolygonPathResult(true, total, refsOut, typesOut);
    }

    [DllImport(NavigationDll, EntryPoint = "FindPathCornersForAgent", CallingConvention = CallingConvention.Cdecl)]
    [return: MarshalAs(UnmanagedType.I1)]
    private static extern bool FindPathCornersForAgent(
        uint mapId,
        XYZ start,
        XYZ end,
        float agentRadius,
        float agentHeight,
        [Out] XYZ[] outCorners,
        int maxCorners,
        out int outCount,
        int straightPathOptions);

    [Flags]
    public enum StraightPathOptions
    {
        None = 0,
        AreaCrossings = 0x01,
        AllCrossings = 0x02,
    }

    public readonly record struct CornerPathResult(
        bool Success,
        int CornerCount,
        XYZ[] Corners);

    public static CornerPathResult QueryPathCorners(
        uint mapId,
        XYZ start,
        XYZ end,
        float agentRadius,
        float agentHeight,
        int maxCorners = 96,
        StraightPathOptions options = StraightPathOptions.None)
    {
        if (maxCorners <= 0)
            throw new ArgumentOutOfRangeException(nameof(maxCorners));

        var corners = new XYZ[maxCorners];
        bool ok = FindPathCornersForAgent(
            mapId, start, end, agentRadius, agentHeight, corners, maxCorners, out int count, (int)options);

        if (!ok)
            return new CornerPathResult(false, 0, Array.Empty<XYZ>());

        int written = count < maxCorners ? count : maxCorners;
        var trimmed = new XYZ[written];
        Array.Copy(corners, trimmed, written);
        return new CornerPathResult(true, written, trimmed);
    }

    [DllImport(NavigationDll, EntryPoint = "GetPolyFlagsForRef", CallingConvention = CallingConvention.Cdecl)]
    [return: MarshalAs(UnmanagedType.I1)]
    private static extern bool GetPolyFlagsForRef(
        uint mapId,
        ulong polyRef,
        out ushort outFlags,
        out byte outArea);

    public readonly record struct PolyFlagsResult(
        bool Success,
        ushort Flags,
        byte Area)
    {
        // Bits from MoveMapSharedDefines.h (kept inline so test code stays
        // self-contained — the C++ enum is not visible to managed code).
        public const ushort NavGround       = 0x01;
        public const ushort NavMagma        = 0x02;
        public const ushort NavSlime        = 0x04;
        public const ushort NavWater        = 0x08;
        public const ushort NavSteepSlopes  = 0x10;

        public bool HasSteepSlopes => Success && (Flags & NavSteepSlopes) != 0;
    }

    /// <summary>
    /// Looks up the user-flag bits and area id of a polygon identified by
    /// <paramref name="polyRef"/>. Pair with <see cref="QueryPolyAtCoord"/> to
    /// classify the polygon a smooth-path corner sits on (e.g. assert no
    /// corner lands on a NAV_STEEP_SLOPES poly after the 2026-05-13 runtime
    /// filter change).
    /// </summary>
    public static PolyFlagsResult QueryPolyFlags(uint mapId, ulong polyRef)
    {
        if (polyRef == 0)
            return new PolyFlagsResult(false, 0, 0);

        bool ok = GetPolyFlagsForRef(mapId, polyRef, out ushort flags, out byte area);
        return new PolyFlagsResult(ok, flags, area);
    }

    [DllImport(NavigationDll, EntryPoint = "GetPolyAtCoord", CallingConvention = CallingConvention.Cdecl)]
    [return: MarshalAs(UnmanagedType.I1)]
    private static extern bool GetPolyAtCoord(
        uint mapId,
        XYZ coord,
        float searchExtentXY,
        float searchExtentZ,
        out ulong outPolyRef,
        out byte outPolyType,
        out XYZ outNearestPoint,
        out float outSurfaceZ,
        out byte outPosOverPoly);

    /// <summary>
    /// Whether the requested XY lies inside the polygon's base convex
    /// footprint (Phase 3 Surface J). When false, the polygon's surface Z
    /// reported in <see cref="PolyAtCoordResult.SurfaceZ"/> is at the
    /// boundary-snapped nearest point, not at the requested XY — useful for
    /// distinguishing "polygon edge clip" (sub-meter) from "polygon absent"
    /// (multi-yard) without re-reading recon JSON.
    /// </summary>
    public enum PosOverPoly : byte
    {
        Outside = 0,
        Inside = 1,
        Unknown = 0xFF,
    }

    public readonly record struct PolyAtCoordResult(
        bool Success,
        bool HasPoly,
        ulong PolyRef,
        PolyType PolyType,
        XYZ NearestPoint,
        float SurfaceZ,
        PosOverPoly PosOverPolyStatus)
    {
        public bool HasSurface => HasPoly && !float.IsNaN(SurfaceZ);

        public bool RequestedXyInsidePoly => PosOverPolyStatus == PosOverPoly.Inside;
    }

    /// <summary>
    /// Looks up the nearest navmesh polygon to <paramref name="coord"/> within
    /// the given horizontal/vertical search extent and returns the polygon's
    /// surface Z at that horizontal location. Used by waypoint-correctness
    /// tests to verify a smooth-path corner sits on a real walkable polygon.
    ///
    /// Default extents:
    ///   searchExtentXY = agentRadius (1.0247y for Tauren M)
    ///   searchExtentZ  = walkableClimb (1.8y harvested from client)
    /// A corner whose Z is more than walkableClimb above/below any poly
    /// surface is "in space" — a synthetic interpolation that the bot
    /// cannot stand on.
    ///
    /// Surface J: when the requested XY is outside the polygon's base
    /// convex footprint, the native side retries getPolyHeight on the
    /// snapped nearest point. <see cref="PolyAtCoordResult.PosOverPoly"/>
    /// distinguishes the two cases.
    /// </summary>
    public static PolyAtCoordResult QueryPolyAtCoord(
        uint mapId,
        XYZ coord,
        float searchExtentXY = 1.0247f,
        float searchExtentZ = 1.8f)
    {
        bool ok = GetPolyAtCoord(
            mapId, coord, searchExtentXY, searchExtentZ,
            out ulong polyRef, out byte polyType, out XYZ nearest, out float surfaceZ,
            out byte posOverPoly);

        if (!ok)
        {
            return new PolyAtCoordResult(
                Success: false,
                HasPoly: false,
                PolyRef: 0,
                PolyType: PolyType.Unknown,
                NearestPoint: default,
                SurfaceZ: float.NaN,
                PosOverPolyStatus: PosOverPoly.Unknown);
        }

        return new PolyAtCoordResult(
            Success: true,
            HasPoly: polyRef != 0,
            PolyRef: polyRef,
            PolyType: (PolyType)polyType,
            NearestPoint: nearest,
            SurfaceZ: surfaceZ,
            PosOverPolyStatus: (PosOverPoly)posOverPoly);
    }
}
