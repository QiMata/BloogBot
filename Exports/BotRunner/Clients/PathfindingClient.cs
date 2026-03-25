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
            bool smoothPath = false,
            Race race = 0,
            Gender gender = 0)
        {
            try
            {
                var request = new PathfindingRequest
                {
                    Path = new CalculatePathRequest
                    {
                        MapId = mapId,
                        Start = ToProto(start),
                        End = ToProto(end),
                        Straight = smoothPath,
                        Race = (uint)race,
                        Gender = (uint)gender,
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
            catch (Exception ex)
            {
                NoteFailure("path request", ex, "returning an empty path until the service recovers.");
                return [];
            }
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
            try
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
            catch (Exception ex)
            {
                NoteFailure("ground query", ex, "treating the ground probe as unavailable until the service recovers.");
                return (0f, false);
            }
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
            try
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
            catch (Exception ex)
            {
                NoteFailure("line-of-sight query", ex, "treating LOS as blocked until the service recovers.");
                return false;
            }
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
                // Guard against empty/unexpected response (oneof not set → Step is null)
                var result = response.Step ?? throw new Exception("PathfindingService returned empty Step response");

                // Zero-delta with movement flags means the physics engine blocked us at a wall.
                // This is CORRECT behavior — do NOT fall back to dead reckoning, which has no
                // wall collision and causes the bot to walk through dungeon walls.
                // The NavigationPath layer handles repath/avoidance when the bot is stuck.

                return result;
            }
            catch (Exception ex)
            {
                _consecutiveFailures++;
                if (_consecutiveFailures <= 3)
                    _logger?.LogWarning("PathfindingService physics call failed ({Count}): {Msg}", _consecutiveFailures, ex.Message);
                else if (_consecutiveFailures == 4)
                    _logger?.LogError("PathfindingService unreachable — holding position until service recovers.");

                // Hold position instead of dead-reckoning. Dead reckoning has NO wall collision
                // and causes bots to walk through dungeon walls. Setting GroundZ to -999999
                // signals to MovementController that no physics ground data is available,
                // which triggers the Z interpolation fallback (using navmesh waypoint Z)
                // while keeping XY frozen until the physics service is available.
                return HoldPosition(physicsInput);
            }
        }

        /// <summary>
        /// Hold position: return the same XY position with GroundZ = -999999 to signal
        /// "no physics data available." MovementController will freeze XY and interpolate Z
        /// from navmesh waypoints. This prevents the bot from walking through walls when
        /// the PathfindingService is still initializing or unreachable.
        /// </summary>
        private static PhysicsOutput HoldPosition(PhysicsInput physicsInput)
        {
            return new PhysicsOutput
            {
                NewPosX = physicsInput.PosX,
                NewPosY = physicsInput.PosY,
                NewPosZ = physicsInput.PosZ,
                NewVelX = 0,
                NewVelY = 0,
                NewVelZ = 0,
                MovementFlags = physicsInput.MovementFlags,
                Orientation = physicsInput.Facing,
                Pitch = physicsInput.SwimPitch,
                FallTime = 0,
                GroundZ = -999999f, // Signal: no physics ground data
            };
        }

        private void NoteFailure(string operation, Exception ex, string terminalFallback)
        {
            _consecutiveFailures++;
            if (_consecutiveFailures <= 3)
            {
                _logger?.LogWarning("PathfindingService {Operation} failed ({Count}): {Msg}",
                    operation, _consecutiveFailures, ex.Message);
            }
            else if (_consecutiveFailures == 4)
            {
                _logger?.LogError("PathfindingService unreachable — {Fallback}", terminalFallback);
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

        /// <summary>
        /// Check if a position is on or near the navmesh within searchRadius.
        /// Returns (onNavmesh, nearestPoint on the navmesh surface).
        /// </summary>
        public virtual (bool onNavmesh, Position nearestPoint) IsPointOnNavmesh(uint mapId, Position position, float searchRadius = 4.0f)
        {
            try
            {
                var request = new PathfindingRequest
                {
                    NavmeshPoint = new NavmeshPointRequest
                    {
                        MapId = mapId,
                        Position = ToProto(position),
                        SearchRadius = searchRadius
                    }
                };
                var response = SendRequest(request, _queryTimeoutMs);
                _consecutiveFailures = 0;
                if (response.PayloadCase == PathfindingResponse.PayloadOneofCase.Error)
                    return (false, position);
                var r = response.NavmeshPoint;
                return (r.OnNavmesh, new Position(r.NearestPoint.X, r.NearestPoint.Y, r.NearestPoint.Z));
            }
            catch
            {
                return (false, position);
            }
        }

        /// <summary>
        /// Find the nearest walkable point within searchRadius.
        /// Returns (areaType, nearestPoint). areaType: 0=not found, 1=ground, 3=steep_slope, 6=water.
        /// </summary>
        public virtual (uint areaType, Position nearestPoint) FindNearestWalkablePoint(uint mapId, Position position, float searchRadius = 8.0f)
        {
            try
            {
                var request = new PathfindingRequest
                {
                    NearestWalkable = new NearestWalkableRequest
                    {
                        MapId = mapId,
                        Position = ToProto(position),
                        SearchRadius = searchRadius
                    }
                };
                var response = SendRequest(request, _queryTimeoutMs);
                _consecutiveFailures = 0;
                if (response.PayloadCase == PathfindingResponse.PayloadOneofCase.Error)
                    return (0, position);
                var r = response.NearestWalkable;
                return (r.AreaType, new Position(r.NearestPoint.X, r.NearestPoint.Y, r.NearestPoint.Z));
            }
            catch
            {
                return (0, position);
            }
        }

        protected virtual PathfindingResponse SendRequest(PathfindingRequest request) => SendMessage(request);

        protected virtual PathfindingResponse SendRequest(PathfindingRequest request, int timeoutMs)
            => SendMessage(request, timeoutMs, timeoutMs);

        private static Game.Position ToProto(Position p) => new() { X = p.X, Y = p.Y, Z = p.Z };
    }
}
