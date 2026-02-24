using BotRunner.Clients;
using GameData.Core.Constants;
using GameData.Core.Enums;
using System;
using static Navigation.Physics.Tests.NavigationInterop;
using ProtoPhysicsInput = Pathfinding.PhysicsInput;
using ProtoPhysicsOutput = Pathfinding.PhysicsOutput;

namespace Navigation.Physics.Tests.Helpers;

/// <summary>
/// PathfindingClient subclass that calls the native PhysicsEngine (Navigation.dll)
/// directly via P/Invoke instead of going through the PathfindingService socket.
/// Used for integration testing MovementController with real physics.
/// </summary>
public class NativePathfindingClient : PathfindingClient
{
    /// <summary>
    /// Overrides the socket-based PhysicsStep to call Navigation.dll directly.
    /// Converts proto → native struct, calls StepPhysicsV2, converts back.
    /// </summary>
    public override ProtoPhysicsOutput PhysicsStep(ProtoPhysicsInput proto)
    {
        var (radius, height) = RaceDimensions.GetCapsuleForRace(
            (Race)proto.Race, (Gender)proto.Gender);

        var input = new PhysicsInput
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
            PhysicsFlags = 0
        };

        // Call real C++ physics engine
        var output = StepPhysicsV2(ref input);

        // Sanitize (remove legacy flags, zero velocities when no intended movement)
        var outFlags = (MovementFlags)output.MoveFlags;
        outFlags &= ~MovementFlags.MOVEFLAG_MOVED;
        bool intendedMove = ((MovementFlags)input.MoveFlags & MovementFlags.MOVEFLAG_MASK_MOVING_OR_TURN) != 0;
        if (!intendedMove)
        {
            output.Vx = 0f;
            output.Vy = 0f;
            output.Vz = 0f;
        }
        output.MoveFlags = (uint)outFlags;

        // Convert native output to proto
        return new ProtoPhysicsOutput
        {
            NewPosX = output.X,
            NewPosY = output.Y,
            NewPosZ = output.Z,
            NewVelX = output.Vx,
            NewVelY = output.Vy,
            NewVelZ = output.Vz,
            MovementFlags = output.MoveFlags,
            Orientation = output.Orientation,
            Pitch = output.Pitch,
            FallTime = output.FallTime,
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
            CurrentSplineIndex = output.CurrentSplineIndex,
            SplineProgress = output.SplineProgress
        };
    }
}
