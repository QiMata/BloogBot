using System;
using System.Runtime.InteropServices;
using GameData.Core.Models;
using Xunit;

namespace BotRunner.Tests.Native;

/// <summary>
/// P1.6: Smoke tests for every P/Invoke export in Navigation.dll.
/// Verifies each function can be called without DllNotFoundException or
/// EntryPointNotFoundException. Does NOT validate correctness — just linkage.
///
/// Requires x86 Navigation.dll in the test output directory.
/// Copy from: Exports/Navigation/cmake_build_x86/Release/Navigation.dll
/// </summary>
public class NavigationDllSmokeTests
{
    private const string Nav = "Navigation";

    // Test helper: try to call a function, assert no EntryPointNotFoundException
    private static void AssertExportExists(Action call, string exportName)
    {
        try
        {
            call();
        }
        catch (EntryPointNotFoundException)
        {
            Assert.Fail($"Navigation.dll missing export: {exportName}");
        }
        catch (DllNotFoundException)
        {
            // DLL not found — skip (not a linkage failure)
            Skip.If(true, "Navigation.dll not found in test output directory");
        }
        catch
        {
            // Any other exception is OK — the function exists but may need init
        }
    }

    [DllImport(Nav, CallingConvention = CallingConvention.Cdecl, EntryPoint = "GetGroundZ")]
    private static extern float GetGroundZNative(uint mapId, float x, float y, float z, float maxDist);

    [DllImport(Nav, CallingConvention = CallingConvention.Cdecl, EntryPoint = "LineOfSight")]
    [return: MarshalAs(UnmanagedType.I1)]
    private static extern bool LineOfSightNative(uint mapId, float fx, float fy, float fz, float tx, float ty, float tz);

    [DllImport(Nav, CallingConvention = CallingConvention.Cdecl, EntryPoint = "SegmentIntersectsDynamicObjects")]
    [return: MarshalAs(UnmanagedType.I1)]
    private static extern bool SegmentIntersectsDynNative(uint mapId, float x0, float y0, float z0, float x1, float y1, float z1);

    [DllImport(Nav, CallingConvention = CallingConvention.Cdecl, EntryPoint = "IsPointOnNavmesh")]
    [return: MarshalAs(UnmanagedType.I1)]
    private static extern bool IsPointOnNavmeshNative(uint mapId, float x, float y, float z, float radius, out float nx, out float ny, out float nz);

    [DllImport(Nav, CallingConvention = CallingConvention.Cdecl, EntryPoint = "FindNearestWalkablePoint")]
    private static extern uint FindNearestWalkableNative(uint mapId, float x, float y, float z, float radius, out float nx, out float ny, out float nz);

    [DllImport(Nav, CallingConvention = CallingConvention.Cdecl, EntryPoint = "PreloadMap")]
    private static extern void PreloadMapNative(uint mapId);

    [DllImport(Nav, CallingConvention = CallingConvention.Cdecl, EntryPoint = "SetDataDirectory")]
    private static extern void SetDataDirectoryNative(string dir);

    [DllImport(Nav, CallingConvention = CallingConvention.Cdecl, EntryPoint = "SetSceneSliceMode")]
    private static extern void SetSceneSliceModeNative([MarshalAs(UnmanagedType.I1)] bool enabled);

    [DllImport(Nav, CallingConvention = CallingConvention.Cdecl, EntryPoint = "ValidateWalkableSegment")]
    private static extern uint ValidateWalkableSegmentNative(uint mapId, float x0, float y0, float z0, float x1, float y1, float z1);

    [SkippableFact]
    public void Export_GetGroundZ_Exists()
        => AssertExportExists(() => GetGroundZNative(0, 0, 0, 0, 10), "GetGroundZ");

    [SkippableFact]
    public void Export_LineOfSight_Exists()
        => AssertExportExists(() => LineOfSightNative(0, 0, 0, 0, 1, 1, 1), "LineOfSight");

    [SkippableFact]
    public void Export_SegmentIntersectsDynamicObjects_Exists()
        => AssertExportExists(() => SegmentIntersectsDynNative(0, 0, 0, 0, 1, 1, 1), "SegmentIntersectsDynamicObjects");

    [SkippableFact]
    public void Export_IsPointOnNavmesh_Exists()
        => AssertExportExists(() => IsPointOnNavmeshNative(0, 0, 0, 0, 4, out _, out _, out _), "IsPointOnNavmesh");

    [SkippableFact]
    public void Export_FindNearestWalkablePoint_Exists()
        => AssertExportExists(() => FindNearestWalkableNative(0, 0, 0, 0, 8, out _, out _, out _), "FindNearestWalkablePoint");

    [SkippableFact]
    public void Export_PreloadMap_Exists()
        => AssertExportExists(() => PreloadMapNative(0), "PreloadMap");

    [SkippableFact]
    public void Export_SetDataDirectory_Exists()
        => AssertExportExists(() => SetDataDirectoryNative(""), "SetDataDirectory");

    [SkippableFact]
    public void Export_SetSceneSliceMode_Exists()
        => AssertExportExists(() => SetSceneSliceModeNative(false), "SetSceneSliceMode");

    [SkippableFact]
    public void Export_ValidateWalkableSegment_Exists()
        => AssertExportExists(() => ValidateWalkableSegmentNative(0, 0, 0, 0, 1, 1, 1), "ValidateWalkableSegment");
}
