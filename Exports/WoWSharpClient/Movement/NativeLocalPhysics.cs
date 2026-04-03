using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using GameData.Core.Constants;
using GameData.Core.Enums;
using Pathfinding;

namespace WoWSharpClient.Movement;

internal static class NativeLocalPhysics
{
    private static bool _initialized;
    private static bool _mapsPreloaded;
    private static bool _sceneSliceModeConfigured;
    private static bool _sceneSliceModeEnabled;
    private static readonly object _preloadLock = new();
    private static List<uint> _preloadedMapIds = [];
    private static HashSet<uint> _preloadedMapIdSet = [];
    internal static Func<NativePhysics.PhysicsInput, NativePhysics.PhysicsOutput>? TestStepOverride { get; set; }
    internal static Action<uint>? TestClearSceneCacheOverride { get; set; }
    internal static Action<bool>? TestSetSceneSliceModeOverride { get; set; }
    internal static Action<uint>? TestPreloadMapOverride { get; set; }
    internal static Action<string>? TestSetDataDirectoryOverride { get; set; }
    internal static Func<string?>? TestResolveDataDirectoryOverride { get; set; }
    public static IReadOnlyList<uint> PreloadedMapIds => _preloadedMapIds;

    public static PhysicsOutput Step(PhysicsInput proto)
    {
        if (TestStepOverride == null || TestPreloadMapOverride != null)
            EnsureMapPreloaded(proto.MapId);

        var (radius, height) = RaceDimensions.GetCapsuleForRace(
            (Race)proto.Race, (Gender)proto.Gender);

        var input = new NativePhysics.PhysicsInput
        {
            MoveFlags = proto.MovementFlags,
            X = proto.PosX,
            Y = proto.PosY,
            Z = proto.PosZ,
            Orientation = proto.Facing,
            Pitch = proto.SwimPitch,
            Vx = proto.VelX,
            Vy = proto.VelY,
            Vz = proto.VelZ,
            WalkSpeed = proto.WalkSpeed,
            RunSpeed = proto.RunSpeed,
            RunBackSpeed = proto.RunBackSpeed,
            SwimSpeed = proto.SwimSpeed,
            SwimBackSpeed = proto.SwimBackSpeed,
            FlightSpeed = 7.0f,
            TransportGuid = proto.TransportGuid,
            TransportX = proto.TransportOffsetX,
            TransportY = proto.TransportOffsetY,
            TransportZ = proto.TransportOffsetZ,
            TransportO = proto.TransportOrientation,
            FallTime = (uint)proto.FallTime,
            FallStartZ = proto.FallStartZ != 0 ? proto.FallStartZ : -200000f,
            Height = height,
            Radius = radius,
            HasSplinePath = false,
            SplinePoints = IntPtr.Zero,
            SplinePointCount = 0,
            CurrentSplineIndex = 0,
            PrevGroundZ = proto.PrevGroundZ,
            PrevGroundNx = proto.PrevGroundNx,
            PrevGroundNy = proto.PrevGroundNy,
            PrevGroundNz = proto.PrevGroundNz,
            PendingDepenX = proto.PendingDepenX,
            PendingDepenY = proto.PendingDepenY,
            PendingDepenZ = proto.PendingDepenZ,
            StandingOnInstanceId = proto.StandingOnInstanceId,
            StandingOnLocalX = proto.StandingOnLocalX,
            StandingOnLocalY = proto.StandingOnLocalY,
            StandingOnLocalZ = proto.StandingOnLocalZ,
            NearbyObjects = IntPtr.Zero,
            NearbyObjectCount = 0,
            MapId = proto.MapId,
            DeltaTime = proto.DeltaTime,
            FrameCounter = proto.FrameCounter,
            PhysicsFlags = proto.PhysicsFlags,
            StepUpBaseZ = proto.StepUpBaseZ,
            StepUpAge = proto.StepUpAge,
            GroundedWallState = 0,
            WasGrounded = proto.WasGrounded ? 1u : 0u,
        };

        GCHandle nearbyObjectsHandle = default;
        try
        {
            if (proto.NearbyObjects.Count > 0)
            {
                var nearbyObjects = BuildNearbyObjects(proto);
                nearbyObjectsHandle = GCHandle.Alloc(nearbyObjects, GCHandleType.Pinned);
                input.NearbyObjects = nearbyObjectsHandle.AddrOfPinnedObject();
                input.NearbyObjectCount = nearbyObjects.Length;
            }

            var output = TestStepOverride != null
                ? TestStepOverride(input)
                : NativePhysics.PhysicsStepV2(ref input);

            var outFlags = (MovementFlags)output.MoveFlags;
            outFlags &= ~MovementFlags.MOVEFLAG_MOVED;
            output.MoveFlags = (uint)outFlags;

            return new PhysicsOutput
            {
                NewPosX = output.X,
                NewPosY = output.Y,
                NewPosZ = output.Z,
                Orientation = output.Orientation,
                Pitch = output.Pitch,
                NewVelX = output.Vx,
                NewVelY = output.Vy,
                NewVelZ = output.Vz,
                MovementFlags = output.MoveFlags,
                FallTime = output.FallTime,
                FallStartZ = output.FallStartZ,
                FallDistance = output.FallDistance,
                GroundZ = output.GroundZ,
                GroundNx = output.GroundNx,
                GroundNy = output.GroundNy,
                GroundNz = output.GroundNz,
                LiquidZ = output.LiquidZ,
                LiquidType = output.LiquidType,
                PendingDepenX = output.PendingDepenX,
                PendingDepenY = output.PendingDepenY,
                PendingDepenZ = output.PendingDepenZ,
                StandingOnInstanceId = output.StandingOnInstanceId,
                StandingOnLocalX = output.StandingOnLocalX,
                StandingOnLocalY = output.StandingOnLocalY,
                StandingOnLocalZ = output.StandingOnLocalZ,
                HitWall = output.HitWall,
                WallNormalX = output.WallNormalX,
                WallNormalY = output.WallNormalY,
                WallNormalZ = output.WallNormalZ,
                BlockedFraction = output.BlockedFraction,
                StepUpBaseZ = output.StepUpBaseZ,
                StepUpAge = output.StepUpAge,
            };
        }
        finally
        {
            if (nearbyObjectsHandle.IsAllocated)
                nearbyObjectsHandle.Free();
        }
    }

    public static void ClearSceneCache(uint mapId)
    {
        if (TestClearSceneCacheOverride != null)
        {
            TestClearSceneCacheOverride(mapId);
            return;
        }

        EnsureInitialized();
        NativePhysics.ClearSceneCache(mapId);
    }

    public static void SetSceneSliceMode(bool enabled)
    {
        if (TestSetSceneSliceModeOverride != null)
            TestSetSceneSliceModeOverride(enabled);

        if (_sceneSliceModeConfigured && _sceneSliceModeEnabled == enabled)
            return;

        if (TestSetSceneSliceModeOverride == null)
        {
            EnsureInitialized();
            NativePhysics.SetSceneSliceMode(enabled);
        }

        _sceneSliceModeConfigured = true;
        _sceneSliceModeEnabled = enabled;
    }

    public static IReadOnlyList<uint> PreloadAvailableMaps()
    {
        EnsureInitialized();

        lock (_preloadLock)
        {
            if (_mapsPreloaded)
                return _preloadedMapIds;

            var dataDir = ResolveDataDirectory();
            var mapIds = DiscoverMapIds(dataDir);
            foreach (var mapId in mapIds)
                EnsureMapPreloadedCore(mapId);

            _mapsPreloaded = true;
            return _preloadedMapIds;
        }
    }

    public static void EnsureMapPreloaded(uint mapId)
    {
        EnsureInitialized();

        lock (_preloadLock)
        {
            EnsureMapPreloadedCore(mapId);
        }
    }

    private static void EnsureMapPreloadedCore(uint mapId)
    {
        if (_preloadedMapIdSet.Contains(mapId))
            return;

        if (TestPreloadMapOverride != null)
            TestPreloadMapOverride(mapId);
        else
            NativePhysics.PreloadMap(mapId);

        _preloadedMapIdSet.Add(mapId);
        _preloadedMapIds.Add(mapId);
    }

    internal static NativePhysics.DynamicObjectInfo[] ReadNearbyObjectsForTest(NativePhysics.PhysicsInput input)
    {
        if (input.NearbyObjects == IntPtr.Zero || input.NearbyObjectCount <= 0)
            return [];

        var result = new NativePhysics.DynamicObjectInfo[input.NearbyObjectCount];
        int elementSize = Marshal.SizeOf<NativePhysics.DynamicObjectInfo>();
        for (int i = 0; i < input.NearbyObjectCount; i++)
        {
            var elementPtr = IntPtr.Add(input.NearbyObjects, i * elementSize);
            result[i] = Marshal.PtrToStructure<NativePhysics.DynamicObjectInfo>(elementPtr);
        }

        return result;
    }

    private static void EnsureInitialized()
    {
        if (_initialized)
            return;

        var dataDir = ResolveDataDirectory();

        if (!string.IsNullOrEmpty(dataDir))
        {
            if (TestSetDataDirectoryOverride != null)
                TestSetDataDirectoryOverride(dataDir);
            else
                NativePhysics.SetDataDirectory(dataDir);
        }

        _initialized = true;
    }

    private static string? ResolveDataDirectory()
    {
        if (TestResolveDataDirectoryOverride != null)
            return TestResolveDataDirectoryOverride();

        var dataDir = Environment.GetEnvironmentVariable("WWOW_DATA_DIR");
        if (!string.IsNullOrEmpty(dataDir) && Directory.Exists(dataDir))
            return Path.GetFullPath(dataDir);

        var baseDir = AppDomain.CurrentDomain.BaseDirectory;
        var candidates = new[]
        {
            Path.Combine(baseDir, "Data"),
            Path.Combine(baseDir, "..", "Data"),
            Path.Combine(baseDir, "..", "..", "Data"),
            Path.Combine(baseDir, "..", "..", "..", "Data"),
            baseDir,
            Path.Combine(baseDir, ".."),
            Path.Combine(baseDir, "..", ".."),
            Path.Combine(baseDir, "..", "..", ".."),
        };

        foreach (var candidate in candidates)
        {
            var resolved = Path.GetFullPath(candidate);
            if (Directory.Exists(resolved) &&
                (Directory.Exists(Path.Combine(resolved, "maps")) ||
                 Directory.Exists(Path.Combine(resolved, "vmaps")) ||
                 Directory.Exists(Path.Combine(resolved, "scenes"))))
            {
                return resolved;
            }
        }

        return null;
    }

    private static List<uint> DiscoverMapIds(string? dataRoot)
    {
        var ids = new HashSet<uint>();

        if (!string.IsNullOrWhiteSpace(dataRoot) && Directory.Exists(dataRoot))
        {
            AddIdsFromDirectory(Path.Combine(dataRoot, "scenes"), "*.scene", ParseWholeStem, ids);
            AddIdsFromDirectory(Path.Combine(dataRoot, "mmaps"), "*.mmap", ParseWholeStem, ids);
            AddIdsFromDirectory(Path.Combine(dataRoot, "maps"), "*.map", ParseFirstThreeDigits, ids);
        }

        if (ids.Count == 0)
        {
            ids.Add(0);
            ids.Add(1);
            ids.Add(30);
            ids.Add(389);
            ids.Add(489);
            ids.Add(529);
            ids.Add(566);
        }

        return ids.OrderBy(id => id).ToList();
    }

    private static void AddIdsFromDirectory(string directory, string pattern, Func<string, uint?> parser, HashSet<uint> ids)
    {
        if (!Directory.Exists(directory))
            return;

        foreach (var file in Directory.GetFiles(directory, pattern))
        {
            var id = parser(Path.GetFileNameWithoutExtension(file));
            if (id.HasValue)
                ids.Add(id.Value);
        }
    }

    private static uint? ParseWholeStem(string stem)
        => uint.TryParse(stem, NumberStyles.None, CultureInfo.InvariantCulture, out var mapId) ? mapId : null;

    private static uint? ParseFirstThreeDigits(string stem)
    {
        if (stem.Length < 3)
            return null;

        return uint.TryParse(stem[..3], NumberStyles.None, CultureInfo.InvariantCulture, out var mapId) ? mapId : null;
    }

    private static NativePhysics.DynamicObjectInfo[] BuildNearbyObjects(PhysicsInput proto)
    {
        var result = new NativePhysics.DynamicObjectInfo[proto.NearbyObjects.Count];
        for (int i = 0; i < proto.NearbyObjects.Count; i++)
        {
            var nearbyObject = proto.NearbyObjects[i];
            result[i] = new NativePhysics.DynamicObjectInfo
            {
                Guid = nearbyObject.Guid,
                DisplayId = nearbyObject.DisplayId,
                X = nearbyObject.X,
                Y = nearbyObject.Y,
                Z = nearbyObject.Z,
                Orientation = nearbyObject.Orientation,
                Scale = nearbyObject.Scale,
                GoState = nearbyObject.GoState,
            };
        }

        return result;
    }

    internal static void ResetCachedStateForTests()
    {
        _initialized = false;
        _mapsPreloaded = false;
        _sceneSliceModeConfigured = false;
        _sceneSliceModeEnabled = false;
        _preloadedMapIds = [];
        _preloadedMapIdSet = [];
    }
}
