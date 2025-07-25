﻿using BotCommLayer;
using GameData.Core.Models;
using Pathfinding; // Proto-generated C# files
using PathfindingService.Repository;

namespace PathfindingService
{
    public class PathfindingSocketServer(string ipAddress, int port, ILogger logger)
        : ProtobufSocketServer<PathfindingRequest, PathfindingResponse>(ipAddress, port, logger)
    {
        private readonly Navigation _navigation = new();

        protected override PathfindingResponse HandleRequest(PathfindingRequest request)
        {
            try
            {
                return request.PayloadCase switch
                {
                    PathfindingRequest.PayloadOneofCase.Path => HandlePath(request.Path),
                    PathfindingRequest.PayloadOneofCase.Los => HandleLineOfSight(request.Los),
                    PathfindingRequest.PayloadOneofCase.Step => HandlePhysics(request.Step),
                    _ => ErrorResponse("Unknown or unset request type.")
                };
            }
            catch (Exception ex)
            {
                logger.LogError($"[PathfindingSocketServer] Error: {ex.Message}\n{ex.StackTrace}");
                return ErrorResponse($"Internal error: {ex.Message}");
            }
        }

        private PathfindingResponse HandlePhysics(PhysicsInput step)
        {
            Navigation.PhysicsOutput physicsOutput = _navigation.StepPhysics(step.ToPhysicsInput(), step.DeltaTime);
            return new PathfindingResponse { Step = physicsOutput.ToPhysicsOutput() };
        }

        private PathfindingResponse HandlePath(CalculatePathRequest req)
        {
            if (!CheckPosition(req.MapId, req.Start, req.End, out var err))
                return err;

            var start = new Position(req.Start.ToXYZ());
            var end = new Position(req.End.ToXYZ());
            var path = _navigation.CalculatePath(req.MapId, start, end, req.Straight);

            var resp = new CalculatePathResponse();
            resp.Corners.AddRange(path.Select(p => p.ToXYZ().ToProto()));

            return new PathfindingResponse { Path = resp };
        }

        private PathfindingResponse HandleLineOfSight(LineOfSightRequest req)
        {
            if (!CheckPosition(req.MapId, req.From, req.To, out var err))
                return err;

            var from = new Position(req.From.ToXYZ());
            var to = new Position(req.To.ToXYZ());

            bool hasLOS = _navigation.IsLineOfSight(req.MapId, from, to);

            return new PathfindingResponse
            {
                Los = new LineOfSightResponse { InLos = hasLOS }
            };
        }

        // ------------- Validation and Helpers ----------------

        private static bool CheckPosition(uint mapId, Game.Position a, Game.Position b, out PathfindingResponse error)
        {
            if (mapId == 0 || a == null || b == null)
            {
                error = ErrorResponse("Missing or invalid MapId/start/end.");
                return false;
            }
            error = null!;
            return true;
        }

        private static PathfindingResponse ErrorResponse(string msg)
        {
            return new PathfindingResponse
            {
                Error = new Error { Message = msg }
            };
        }
    }

    public static class ProtoInteropExtensions
    {
        public static Game.Position ToProto(this XYZ xyz) =>
            new() { X = xyz.X, Y = xyz.Y, Z = xyz.Z };

        public static XYZ ToXYZ(this Game.Position p) =>
            new(p.X, p.Y, p.Z);

        public static Navigation.PhysicsInput ToPhysicsInput(this PhysicsInput physicsInput) =>
            new()
            {
                movementFlags = physicsInput.MovementFlags,
                posX = physicsInput.PosX,
                posY = physicsInput.PosY,
                posZ = physicsInput.PosZ,
                facing = physicsInput.Facing,
                transportGuid = physicsInput.TransportGuid,
                transportOffsetX = physicsInput.TransportOffsetX,
                transportOffsetY = physicsInput.TransportOffsetY,
                transportOffsetZ = physicsInput.TransportOffsetZ,
                transportOrientation = physicsInput.TransportOrientation,
                swimPitch = physicsInput.SwimPitch,
                fallTime = physicsInput.FallTime,
                jumpVerticalSpeed = physicsInput.JumpVerticalSpeed,
                jumpCosAngle = physicsInput.JumpCosAngle,
                jumpSinAngle = physicsInput.JumpSinAngle,
                jumpHorizontalSpeed = physicsInput.JumpHorizontalSpeed,
                splineElevation = physicsInput.SplineElevation,
                walkSpeed = physicsInput.WalkSpeed,
                runSpeed = physicsInput.RunSpeed,
                runBackSpeed = physicsInput.RunBackSpeed,
                swimSpeed = physicsInput.SwimSpeed,
                swimBackSpeed = physicsInput.SwimBackSpeed,
                velX = physicsInput.VelX,
                velY = physicsInput.VelY,
                velZ = physicsInput.VelZ,
                radius = physicsInput.Radius,
                height = physicsInput.Height,
                gravity = physicsInput.Gravity,
                adtGroundZ = physicsInput.AdtGroundZ,
                adtLiquidZ = physicsInput.AdtLiquidZ,
                mapId = physicsInput.MapId
            };

        public static PhysicsOutput ToPhysicsOutput(this Navigation.PhysicsOutput physics) =>
            new()
            {
                NewPosX = physics.newPosX,
                NewPosY = physics.newPosY,
                NewPosZ = physics.newPosZ,
                NewVelX = physics.newVelX,
                NewVelY = physics.newVelY,
                NewVelZ = physics.newVelZ,
                MovementFlags = physics.movementFlags
            };
    }
}
