using System;
using System.Runtime.InteropServices;
using GameData.Core.Models;

namespace PathfindingService.Tests;

internal static class NavigationInterop
{
    private const string NavigationDll = "Navigation.dll";

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
        out int outCount);

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
        int maxCorners = 96)
    {
        if (maxCorners <= 0)
            throw new ArgumentOutOfRangeException(nameof(maxCorners));

        var corners = new XYZ[maxCorners];
        bool ok = FindPathCornersForAgent(
            mapId, start, end, agentRadius, agentHeight, corners, maxCorners, out int count);

        if (!ok)
            return new CornerPathResult(false, 0, Array.Empty<XYZ>());

        int written = count < maxCorners ? count : maxCorners;
        var trimmed = new XYZ[written];
        Array.Copy(corners, trimmed, written);
        return new CornerPathResult(true, written, trimmed);
    }
}
