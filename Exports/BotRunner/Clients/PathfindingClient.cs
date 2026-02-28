using BotCommLayer;
using GameData.Core.Enums;
using GameData.Core.Models;
using Microsoft.Extensions.Logging;
using Pathfinding;
using System;
using System.Linq;
namespace BotRunner.Clients
{
    public class PathfindingClient : ProtobufSocketClient<PathfindingRequest, PathfindingResponse>
    {
        private readonly ILogger? _logger;
        private int _consecutiveFailures;

        public PathfindingClient() : base() { }
        public PathfindingClient(string ipAddress, int port, ILogger logger)
            : base(ipAddress, port, logger) { _logger = logger; }

        /// <summary>
        /// True when the last request succeeded. Game loop can check this to decide
        /// whether to fall back to dead reckoning.
        /// </summary>
        public bool IsAvailable => _consecutiveFailures == 0;

        public virtual Position[] GetPath(uint mapId, Position start, Position end, bool smoothPath = false)
        {
            var request = new PathfindingRequest
            {
                Path = new CalculatePathRequest
                {
                    MapId = mapId,
                    Start = ToProto(start),
                    End = ToProto(end),
                    Straight = smoothPath
                }
            };
            var response = SendMessage(request);
            _consecutiveFailures = 0;
            if (response.PayloadCase == PathfindingResponse.PayloadOneofCase.Error)
                throw new Exception(response.Error.Message);
            return response.Path.Corners
                .Select(p => new Position(p.X, p.Y, p.Z))
                .ToArray();
        }
        public virtual float GetPathingDistance(uint mapId, Position start, Position end)
        {
            var path = GetPath(mapId, start, end);
            float distance = 0f;
            for (int i = 0; i < path.Length - 1; i++)
                distance += path[i].DistanceTo(path[i + 1]);
            return distance;
        }
        public virtual (float groundZ, bool found) GetGroundZ(uint mapId, Position position, float maxSearchDist = 10.0f)
        {
            var request = new PathfindingRequest
            {
                GroundZ = new GetGroundZRequest
                {
                    MapId = mapId,
                    Position = ToProto(position),
                    MaxSearchDist = maxSearchDist
                }
            };
            var response = SendMessage(request);
            _consecutiveFailures = 0;
            if (response.PayloadCase == PathfindingResponse.PayloadOneofCase.Error)
                throw new Exception(response.Error.Message);
            return (response.GroundZ.GroundZ, response.GroundZ.Found);
        }

        public virtual bool IsInLineOfSight(uint mapId, Position from, Position to)
        {
            var request = new PathfindingRequest
            {
                Los = new LineOfSightRequest
                {
                    MapId = mapId,
                    From = ToProto(from),
                    To = ToProto(to)
                }
            };
            var response = SendMessage(request);
            _consecutiveFailures = 0;
            if (response.PayloadCase == PathfindingResponse.PayloadOneofCase.Error)
                throw new Exception(response.Error.Message);
            return response.Los.InLos;
        }

        /// <summary>
        /// Runs a physics step via the centralized PathfindingService.
        /// On failure, returns a dead-reckoning fallback (position unchanged, simple gravity)
        /// so the game loop never crashes.
        /// </summary>
        public virtual PhysicsOutput PhysicsStep(PhysicsInput physicsInput)
        {
            try
            {
                var request = new PathfindingRequest { Step = physicsInput };
                var response = SendMessage(request);
                _consecutiveFailures = 0;
                if (response.PayloadCase == PathfindingResponse.PayloadOneofCase.Error)
                    throw new Exception(response.Error.Message);
                return response.Step;
            }
            catch (Exception ex)
            {
                _consecutiveFailures++;
                if (_consecutiveFailures <= 3)
                    _logger?.LogWarning("PathfindingService physics call failed ({Count}): {Msg}", _consecutiveFailures, ex.Message);
                else if (_consecutiveFailures == 4)
                    _logger?.LogError("PathfindingService unreachable, using dead reckoning. Suppressing further warnings.");

                // Dead reckoning: apply simple forward/backward movement + gravity
                float dx = 0, dy = 0;
                var flags = (MovementFlags)physicsInput.MovementFlags;
                float facing = physicsInput.Facing;

                if (flags.HasFlag(MovementFlags.MOVEFLAG_FORWARD))
                {
                    dx += MathF.Cos(facing) * physicsInput.RunSpeed * physicsInput.DeltaTime;
                    dy += MathF.Sin(facing) * physicsInput.RunSpeed * physicsInput.DeltaTime;
                }
                if (flags.HasFlag(MovementFlags.MOVEFLAG_BACKWARD))
                {
                    dx -= MathF.Cos(facing) * physicsInput.RunBackSpeed * physicsInput.DeltaTime;
                    dy -= MathF.Sin(facing) * physicsInput.RunBackSpeed * physicsInput.DeltaTime;
                }

                return new PhysicsOutput
                {
                    NewPosX = physicsInput.PosX + dx,
                    NewPosY = physicsInput.PosY + dy,
                    NewPosZ = physicsInput.PosZ,
                    NewVelX = physicsInput.VelX,
                    NewVelY = physicsInput.VelY,
                    NewVelZ = physicsInput.VelZ - 19.2911f * physicsInput.DeltaTime,
                    MovementFlags = physicsInput.MovementFlags,
                    Orientation = physicsInput.Facing,
                    Pitch = physicsInput.SwimPitch,
                    FallTime = physicsInput.FallTime + physicsInput.DeltaTime * 1000f,
                };
            }
        }
        private static Game.Position ToProto(Position p) => new() { X = p.X, Y = p.Y, Z = p.Z };
    }
}