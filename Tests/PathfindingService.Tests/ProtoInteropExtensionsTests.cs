using System;
using GameData.Core.Constants;
using GameData.Core.Enums;
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
}
