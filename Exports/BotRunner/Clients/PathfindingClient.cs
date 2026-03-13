using BotCommLayer;
using GameData.Core.Enums;
using GameData.Core.Models;
using Microsoft.Extensions.Logging;
using Pathfinding;
using System;
using System.Collections.Generic;
using System.Linq;
namespace BotRunner.Clients
{
    public class PathfindingClient : ProtobufSocketClient<PathfindingRequest, PathfindingResponse>
    {
        internal const int DefaultPathRequestTimeoutMs = 30_000;
        internal const int DefaultQueryTimeoutMs = 10_000;
        internal const int DefaultPhysicsTimeoutMs = 5_000;

        private readonly ILogger? _logger;
        private readonly int _pathRequestTimeoutMs;
        private readonly int _queryTimeoutMs;
        private readonly int _physicsTimeoutMs;
        private int _consecutiveFailures;

        public PathfindingClient()
            : this(
                DefaultPathRequestTimeoutMs,
                DefaultQueryTimeoutMs,
                DefaultPhysicsTimeoutMs)
        {
        }

        protected PathfindingClient(
            int pathRequestTimeoutMs,
            int queryTimeoutMs,
            int physicsTimeoutMs)
            : base()
        {
            _pathRequestTimeoutMs = pathRequestTimeoutMs;
            _queryTimeoutMs = queryTimeoutMs;
            _physicsTimeoutMs = physicsTimeoutMs;
        }

        public PathfindingClient(
            string ipAddress,
            int port,
            ILogger logger,
            int pathRequestTimeoutMs = DefaultPathRequestTimeoutMs,
            int queryTimeoutMs = DefaultQueryTimeoutMs,
            int physicsTimeoutMs = DefaultPhysicsTimeoutMs)
            : base(ipAddress, port, logger)
        {
            _logger = logger;
            _pathRequestTimeoutMs = pathRequestTimeoutMs;
            _queryTimeoutMs = queryTimeoutMs;
            _physicsTimeoutMs = physicsTimeoutMs;
        }

        /// <summary>
        /// True when the last request succeeded. Game loop can check this to decide
        /// whether to fall back to dead reckoning.
        /// </summary>
        public bool IsAvailable => _consecutiveFailures == 0;

        public virtual Position[] GetPath(uint mapId, Position start, Position end, bool smoothPath = false)
            => GetPath(mapId, start, end, nearbyObjects: null, smoothPath);

        public virtual Position[] GetPath(
            uint mapId,
            Position start,
            Position end,
            IReadOnlyList<DynamicObjectProto>? nearbyObjects,
            bool smoothPath = false)
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
            if (nearbyObjects is { Count: > 0 })
                request.Path.NearbyObjects.Add(nearbyObjects);

            var response = SendRequest(request, _pathRequestTimeoutMs);
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
            var response = SendRequest(request, _queryTimeoutMs);
            _consecutiveFailures = 0;
            if (response.PayloadCase == PathfindingResponse.PayloadOneofCase.Error)
                throw new Exception(response.Error.Message);
            return (response.GroundZ.GroundZ, response.GroundZ.Found);
        }

        /// <summary>
        /// Queries ground Z for multiple positions in a single IPC round-trip.
        /// Returns an array of (groundZ, found) results, one per input position, in the same order.
        /// Falls back to individual GetGroundZ calls if the batch request fails (e.g., older service).
        /// </summary>
        public virtual (float groundZ, bool found)[] BatchGetGroundZ(uint mapId, Position[] positions, float maxSearchDist = 10.0f)
        {
            if (positions.Length == 0)
                return [];

            try
            {
                var request = new PathfindingRequest
                {
                    BatchGroundZ = new BatchGroundZRequest
                    {
                        MapId = mapId,
                        MaxSearchDist = maxSearchDist
                    }
                };
                foreach (var pos in positions)
                    request.BatchGroundZ.Positions.Add(ToProto(pos));

                var response = SendRequest(request, _queryTimeoutMs);
                _consecutiveFailures = 0;

                if (response.PayloadCase == PathfindingResponse.PayloadOneofCase.Error)
                    throw new Exception(response.Error.Message);

                var results = new (float groundZ, bool found)[response.BatchGroundZ.Results.Count];
                for (int i = 0; i < results.Length; i++)
                {
                    var entry = response.BatchGroundZ.Results[i];
                    results[i] = (entry.GroundZ, entry.Found);
                }
                return results;
            }
            catch
            {
                // Fallback: individual queries (handles older PathfindingService without batch support)
                var results = new (float groundZ, bool found)[positions.Length];
                for (int i = 0; i < positions.Length; i++)
                {
                    try { results[i] = GetGroundZ(mapId, positions[i], maxSearchDist); }
                    catch { results[i] = (0f, false); }
                }
                return results;
            }
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
            var response = SendRequest(request, _queryTimeoutMs);
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
                var response = SendRequest(request, _physicsTimeoutMs);
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
        /// <summary>
        /// Check whether the segment (from → to) intersects any registered dynamic object
        /// (closed door, trophy pillar, etc.) on the given map.
        /// Returns false when no dynamic objects are registered (fast path).
        /// Does NOT check static geometry — use IsInLineOfSight for that.
        /// </summary>
        public virtual bool SegmentIntersectsDynamicObjects(uint mapId, Position from, Position to)
        {
            try
            {
                var request = new PathfindingRequest
                {
                    SegmentDynCheck = new SegmentDynCheckRequest
                    {
                        MapId = mapId,
                        From = ToProto(from),
                        To = ToProto(to)
                    }
                };
                var response = SendRequest(request, _queryTimeoutMs);
                _consecutiveFailures = 0;
                if (response.PayloadCase == PathfindingResponse.PayloadOneofCase.Error)
                    return false; // Non-fatal — treat as clear
                return response.SegmentDynCheck.Intersects;
            }
            catch
            {
                return false; // Pathfinding service unavailable — assume clear
            }
        }

        protected virtual PathfindingResponse SendRequest(PathfindingRequest request) => SendMessage(request);

        protected virtual PathfindingResponse SendRequest(PathfindingRequest request, int timeoutMs)
            => SendMessage(request, timeoutMs, timeoutMs);

        private static Game.Position ToProto(Position p) => new() { X = p.X, Y = p.Y, Z = p.Z };
    }
}
