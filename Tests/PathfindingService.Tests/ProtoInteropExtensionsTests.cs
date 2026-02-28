using System;
using GameData.Core.Constants;
using GameData.Core.Enums;
using Google.Protobuf;
using PathfindingService;
using PathfindingService.Repository;

namespace PathfindingService.Tests;

public class ProtoInteropExtensionsTests
{
    [Fact]
    public void ToPhysicsInput_MapsProtobufToNativeStruct()
    {
        var proto = new Pathfinding.PhysicsInput
        {
            MovementFlags = 0x10203040,
            PosX = 100.1f,
            PosY = 200.2f,
            PosZ = 300.3f,
            Facing = 1.234f,
            TransportGuid = 9876543210UL,
            TransportOffsetX = 11.1f,
            TransportOffsetY = 22.2f,
            TransportOffsetZ = 33.3f,
            TransportOrientation = 2.345f,
            SwimPitch = -0.123f,
            FallTime = 444f,
            VelX = -7.7f,
            VelY = 8.8f,
            VelZ = -9.9f,
            WalkSpeed = 2.5f,
            RunSpeed = 7.0f,
            RunBackSpeed = 4.5f,
            SwimSpeed = 4.72f,
            SwimBackSpeed = 2.5f,
            Race = (uint)Race.Orc,
            Gender = (uint)Gender.Female,
            MapId = 1u,
            DeltaTime = 0.05f,
            FrameCounter = 123u,
            PrevGroundZ = 10.1f,
            PrevGroundNx = 0.1f,
            PrevGroundNy = 0.2f,
            PrevGroundNz = 0.97f,
            PendingDepenX = 1.1f,
            PendingDepenY = 1.2f,
            PendingDepenZ = 1.3f,
            StandingOnInstanceId = 42u,
            StandingOnLocalX = 2.1f,
            StandingOnLocalY = 2.2f,
            StandingOnLocalZ = 2.3f,
            PhysicsFlags = 0x1u
        };

        var native = proto.ToPhysicsInput();
        var expectedCapsule = RaceDimensions.GetCapsuleForRace(Race.Orc, Gender.Female);

        Assert.Equal(proto.MovementFlags, native.moveFlags);
        Assert.Equal(proto.PosX, native.x);
        Assert.Equal(proto.PosY, native.y);
        Assert.Equal(proto.PosZ, native.z);
        Assert.Equal(proto.Facing, native.orientation);
        Assert.Equal(proto.SwimPitch, native.pitch);
        Assert.Equal(proto.TransportGuid, native.transportGuid);
        Assert.Equal(proto.TransportOffsetX, native.transportX);
        Assert.Equal(proto.TransportOffsetY, native.transportY);
        Assert.Equal(proto.TransportOffsetZ, native.transportZ);
        Assert.Equal(proto.TransportOrientation, native.transportO);
        Assert.Equal((uint)proto.FallTime, native.fallTime);
        Assert.Equal(proto.MapId, native.mapId);
        Assert.Equal(proto.VelX, native.vx);
        Assert.Equal(proto.VelY, native.vy);
        Assert.Equal(proto.VelZ, native.vz);
        Assert.Equal(proto.WalkSpeed, native.walkSpeed);
        Assert.Equal(proto.RunSpeed, native.runSpeed);
        Assert.Equal(proto.RunBackSpeed, native.runBackSpeed);
        Assert.Equal(proto.SwimSpeed, native.swimSpeed);
        Assert.Equal(proto.SwimBackSpeed, native.swimBackSpeed);
        Assert.Equal(proto.DeltaTime, native.deltaTime);
        Assert.Equal(proto.FrameCounter, native.frameCounter);
        Assert.Equal(proto.PrevGroundZ, native.prevGroundZ);
        Assert.Equal(proto.PrevGroundNx, native.prevGroundNx);
        Assert.Equal(proto.PrevGroundNy, native.prevGroundNy);
        Assert.Equal(proto.PrevGroundNz, native.prevGroundNz);
        Assert.Equal(proto.PendingDepenX, native.pendingDepenX);
        Assert.Equal(proto.PendingDepenY, native.pendingDepenY);
        Assert.Equal(proto.PendingDepenZ, native.pendingDepenZ);
        Assert.Equal(proto.StandingOnInstanceId, native.standingOnInstanceId);
        Assert.Equal(proto.StandingOnLocalX, native.standingOnLocalX);
        Assert.Equal(proto.StandingOnLocalY, native.standingOnLocalY);
        Assert.Equal(proto.StandingOnLocalZ, native.standingOnLocalZ);
        Assert.Equal(proto.PhysicsFlags, native.physicsFlags);

        Assert.Equal(expectedCapsule.height, native.height);
        Assert.Equal(expectedCapsule.radius, native.radius);

        Assert.False(native.hasSplinePath);
        Assert.Equal(0f, native.splineSpeed);
        Assert.Equal(IntPtr.Zero, native.splinePoints);
        Assert.Equal(0, native.splinePointCount);
        Assert.Equal(0, native.currentSplineIndex);
    }

    [Fact]
    public void ToPhysicsOutput_MapsNativeStructToProtobuf()
    {
        var native = new PathfindingService.Repository.PhysicsOutput
        {
            x = 10.1f,
            y = 20.2f,
            z = 30.3f,
            vx = -1.1f,
            vy = 2.2f,
            vz = -3.3f,
            moveFlags = 0x55667788,
            orientation = 0.75f,
            pitch = -0.25f,
            fallTime = 123.4f,
            currentSplineIndex = 9,
            splineProgress = 0.55f,
            groundZ = 40.4f,
            groundNx = 0.01f,
            groundNy = 0.02f,
            groundNz = 0.99f,
            liquidZ = 41.5f,
            liquidType = 3u,
            pendingDepenX = 4.1f,
            pendingDepenY = 4.2f,
            pendingDepenZ = 4.3f,
            standingOnInstanceId = 77u,
            standingOnLocalX = 5.1f,
            standingOnLocalY = 5.2f,
            standingOnLocalZ = 5.3f
        };

        var proto = native.ToPhysicsOutput();

        Assert.Equal(native.x, proto.NewPosX);
        Assert.Equal(native.y, proto.NewPosY);
        Assert.Equal(native.z, proto.NewPosZ);
        Assert.Equal(native.vx, proto.NewVelX);
        Assert.Equal(native.vy, proto.NewVelY);
        Assert.Equal(native.vz, proto.NewVelZ);
        Assert.Equal(native.moveFlags, proto.MovementFlags);
        Assert.Equal(native.orientation, proto.Orientation);
        Assert.Equal(native.pitch, proto.Pitch);
        Assert.Equal(native.fallTime, proto.FallTime);
        Assert.Equal(native.currentSplineIndex, proto.CurrentSplineIndex);
        Assert.Equal(native.splineProgress, proto.SplineProgress);
        Assert.Equal(native.groundZ, proto.GroundZ);
        Assert.Equal(native.groundNx, proto.GroundNx);
        Assert.Equal(native.groundNy, proto.GroundNy);
        Assert.Equal(native.groundNz, proto.GroundNz);
        Assert.Equal(native.liquidZ, proto.LiquidZ);
        Assert.Equal(native.liquidType, proto.LiquidType);
        Assert.Equal(native.pendingDepenX, proto.PendingDepenX);
        Assert.Equal(native.pendingDepenY, proto.PendingDepenY);
        Assert.Equal(native.pendingDepenZ, proto.PendingDepenZ);
        Assert.Equal(native.standingOnInstanceId, proto.StandingOnInstanceId);
        Assert.Equal(native.standingOnLocalX, proto.StandingOnLocalX);
        Assert.Equal(native.standingOnLocalY, proto.StandingOnLocalY);
        Assert.Equal(native.standingOnLocalZ, proto.StandingOnLocalZ);
    }

    [Fact]
    public void PhysicsInput_RoundTrip_ProtoToNativeToProto_PreservesTransportFields()
    {
        var proto = new Pathfinding.PhysicsInput
        {
            TransportGuid = 12345678901234UL,
            TransportOffsetX = 11.5f,
            TransportOffsetY = -22.75f,
            TransportOffsetZ = 33.125f,
            TransportOrientation = 3.14f,
            PosX = 0f, PosY = 0f, PosZ = 0f,
            Race = (uint)Race.Orc, Gender = (uint)Gender.Male,
        };

        var native = proto.ToPhysicsInput();

        Assert.Equal(proto.TransportGuid, native.transportGuid);
        Assert.Equal(proto.TransportOffsetX, native.transportX);
        Assert.Equal(proto.TransportOffsetY, native.transportY);
        Assert.Equal(proto.TransportOffsetZ, native.transportZ);
        Assert.Equal(proto.TransportOrientation, native.transportO);
    }

    [Fact]
    public void PhysicsInput_RoundTrip_ProtoToNativeToProto_PreservesStandingOnFields()
    {
        var proto = new Pathfinding.PhysicsInput
        {
            StandingOnInstanceId = 999u,
            StandingOnLocalX = 5.5f,
            StandingOnLocalY = -6.25f,
            StandingOnLocalZ = 7.75f,
            PosX = 0f, PosY = 0f, PosZ = 0f,
            Race = (uint)Race.Orc, Gender = (uint)Gender.Male,
        };

        var native = proto.ToPhysicsInput();

        Assert.Equal(proto.StandingOnInstanceId, native.standingOnInstanceId);
        Assert.Equal(proto.StandingOnLocalX, native.standingOnLocalX);
        Assert.Equal(proto.StandingOnLocalY, native.standingOnLocalY);
        Assert.Equal(proto.StandingOnLocalZ, native.standingOnLocalZ);
    }

    [Fact]
    public void PhysicsOutput_RoundTrip_NativeToProto_PreservesFallAndLiquidFields()
    {
        var native = new PathfindingService.Repository.PhysicsOutput
        {
            x = 100f, y = 200f, z = 300f,
            fallTime = 555.5f,
            fallDistance = 12.3f,
            fallStartZ = 312.3f,
            liquidZ = 295.0f,
            liquidType = 2u,
            groundZ = 298.5f,
            groundNx = 0.0f, groundNy = 0.0f, groundNz = 1.0f,
        };

        var proto = native.ToPhysicsOutput();

        Assert.Equal(native.fallTime, proto.FallTime);
        Assert.Equal(native.fallDistance, proto.FallDistance);
        Assert.Equal(native.fallStartZ, proto.FallStartZ);
        Assert.Equal(native.liquidZ, proto.LiquidZ);
        Assert.Equal(native.liquidType, proto.LiquidType);
    }

    [Fact]
    public void PhysicsInput_ZeroTransportGuid_MapsCleanly()
    {
        var proto = new Pathfinding.PhysicsInput
        {
            TransportGuid = 0UL,
            TransportOffsetX = 0f,
            TransportOffsetY = 0f,
            TransportOffsetZ = 0f,
            TransportOrientation = 0f,
            PosX = 10f, PosY = 20f, PosZ = 30f,
            Race = (uint)Race.Orc, Gender = (uint)Gender.Male,
        };

        var native = proto.ToPhysicsInput();

        Assert.Equal(0UL, native.transportGuid);
        Assert.Equal(0f, native.transportX);
        Assert.Equal(0f, native.transportY);
        Assert.Equal(0f, native.transportZ);
        Assert.Equal(0f, native.transportO);
    }

    [Fact]
    public void PhysicsOutput_ZeroStandingOn_MapsCleanly()
    {
        var native = new PathfindingService.Repository.PhysicsOutput
        {
            standingOnInstanceId = 0u,
            standingOnLocalX = 0f,
            standingOnLocalY = 0f,
            standingOnLocalZ = 0f,
            x = 1f, y = 2f, z = 3f,
        };

        var proto = native.ToPhysicsOutput();

        Assert.Equal(0u, proto.StandingOnInstanceId);
        Assert.Equal(0f, proto.StandingOnLocalX);
        Assert.Equal(0f, proto.StandingOnLocalY);
        Assert.Equal(0f, proto.StandingOnLocalZ);
    }

    [Fact]
    public void PathCorners_RoundTripThroughProto_PreservesCountOrderAndPrecision()
    {
        // Simulate a native path result: 5 corners with varied coordinates
        var corners = new[]
        {
            new Game.Position { X = 1629.123f, Y = -4373.456f, Z = 9.789f },
            new Game.Position { X = 1640.001f, Y = -4380.999f, Z = 12.345f },
            new Game.Position { X = 1655.555f, Y = -4390.111f, Z = 15.678f },
            new Game.Position { X = 1670.0f,   Y = -4400.0f,   Z = 18.0f },
            new Game.Position { X = 1685.999f, Y = -4410.001f, Z = 20.123f },
        };

        var resp = new Pathfinding.CalculatePathResponse();
        resp.Corners.AddRange(corners);
        resp.Result = "native_path";
        resp.RawCornerCount = (uint)corners.Length;

        // Serialize and deserialize through protobuf
        var bytes = resp.ToByteArray();
        var deserialized = Pathfinding.CalculatePathResponse.Parser.ParseFrom(bytes);

        // Corner count preserved
        Assert.Equal(corners.Length, deserialized.Corners.Count);

        // Provenance metadata preserved
        Assert.Equal("native_path", deserialized.Result);
        Assert.Equal((uint)corners.Length, deserialized.RawCornerCount);

        // Each corner's coordinates preserved exactly (float precision)
        for (int i = 0; i < corners.Length; i++)
        {
            Assert.Equal(corners[i].X, deserialized.Corners[i].X);
            Assert.Equal(corners[i].Y, deserialized.Corners[i].Y);
            Assert.Equal(corners[i].Z, deserialized.Corners[i].Z);
        }
    }

    [Fact]
    public void PathCorners_EmptyPath_PreservesNoPathResult()
    {
        var resp = new Pathfinding.CalculatePathResponse();
        resp.Result = "no_path";
        resp.RawCornerCount = 0;

        var bytes = resp.ToByteArray();
        var deserialized = Pathfinding.CalculatePathResponse.Parser.ParseFrom(bytes);

        Assert.Empty(deserialized.Corners);
        Assert.Equal("no_path", deserialized.Result);
        Assert.Equal(0u, deserialized.RawCornerCount);
    }

    [Fact]
    public void PathCorners_OrderPreserved_NotSortedOrShuffled()
    {
        // Create corners in a specific order (not sorted by any axis)
        var resp = new Pathfinding.CalculatePathResponse();
        resp.Corners.Add(new Game.Position { X = 100f, Y = 200f, Z = 10f });
        resp.Corners.Add(new Game.Position { X = 50f,  Y = 300f, Z = 5f });
        resp.Corners.Add(new Game.Position { X = 200f, Y = 100f, Z = 20f });
        resp.Corners.Add(new Game.Position { X = 75f,  Y = 250f, Z = 15f });

        var bytes = resp.ToByteArray();
        var deserialized = Pathfinding.CalculatePathResponse.Parser.ParseFrom(bytes);

        // Verify exact order: X values must be 100, 50, 200, 75 (not sorted)
        Assert.Equal(100f, deserialized.Corners[0].X);
        Assert.Equal(50f,  deserialized.Corners[1].X);
        Assert.Equal(200f, deserialized.Corners[2].X);
        Assert.Equal(75f,  deserialized.Corners[3].X);
    }

    [Fact]
    public void PathCorners_ExtremePrecision_NoTruncation()
    {
        // Use values that would fail if truncated to lower precision
        var resp = new Pathfinding.CalculatePathResponse();
        resp.Corners.Add(new Game.Position { X = 1234.5678f, Y = -9876.5432f, Z = 0.001f });
        resp.Corners.Add(new Game.Position { X = float.MaxValue / 2f, Y = float.MinValue / 2f, Z = float.Epsilon });

        var bytes = resp.ToByteArray();
        var deserialized = Pathfinding.CalculatePathResponse.Parser.ParseFrom(bytes);

        Assert.Equal(1234.5678f, deserialized.Corners[0].X);
        Assert.Equal(-9876.5432f, deserialized.Corners[0].Y);
        Assert.Equal(0.001f, deserialized.Corners[0].Z);
        Assert.Equal(float.MaxValue / 2f, deserialized.Corners[1].X);
        Assert.Equal(float.MinValue / 2f, deserialized.Corners[1].Y);
        Assert.Equal(float.Epsilon, deserialized.Corners[1].Z);
    }
}
