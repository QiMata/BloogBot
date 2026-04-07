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
    /// <summary>
    /// PathfindingClient — remote path requests + local physics/query operations.
    ///
    /// Path computation (FindPath, corridor) goes to the remote PathfindingService.
    /// All other operations (GroundZ, LOS, physics, navmesh queries) use the local
    /// in-process Navigation.dll directly.
    /// </summary>
    public class PathfindingClient : ProtobufSocketClient<PathfindingRequest, PathfindingResponse>
    {
        internal const int DefaultPathRequestTimeoutMs = 30_000;

        private readonly ILogger? _logger;
        private readonly int _pathRequestTimeoutMs;
        private int _consecutiveFailures;

        public PathfindingClient() : this(DefaultPathRequestTimeoutMs) { }

        protected PathfindingClient(int pathRequestTimeoutMs) : base()
        {
            _pathRequestTimeoutMs = pathRequestTimeoutMs;
        }

        public PathfindingClient(
            string ipAddress, int port, ILogger logger,
            int pathRequestTimeoutMs = DefaultPathRequestTimeoutMs)
            : base(ipAddress, port, logger)
        {
            _logger = logger;
            _pathRequestTimeoutMs = pathRequestTimeoutMs;
        }

        public bool IsAvailable => _consecutiveFailures == 0;

        // ════════════════════════════════════════════════════════════════
        //  REMOTE: Path computation (via PathfindingService)
        // ════════════════════════════════════════════════════════════════

        public virtual Position[] GetPath(uint mapId, Position start, Position end, bool smoothPath = false)
            => GetPath(mapId, start, end, nearbyObjects: null, smoothPath);

        public virtual Position[] GetPath(
            uint mapId, Position start, Position end,
            IReadOnlyList<DynamicObjectProto>? nearbyObjects,
            bool smoothPath = false, Race race = 0, Gender gender = 0)
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
                return response.Path.Corners.Select(p => new Position(p.X, p.Y, p.Z)).ToArray();
            }
            catch (Exception ex)
            {
                NoteFailure("path request", ex);
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

        // ═══════════════════════════════════════════════════════════════
        //  LOCAL: Spatial queries via NativeLocalPhysics (Physics.dll)
        // ═══════════════════════════════════════════════════════════════

        public virtual (float groundZ, bool found) GetGroundZ(uint mapId, Position position, float maxSearchDist = 10.0f)
        {
            try
            {
                return WoWSharpClient.Movement.NativeLocalPhysics.GetGroundZ(
                    mapId, position.X, position.Y, position.Z, maxSearchDist);
            }
            catch (Exception ex)
            {
                _logger?.LogWarning("Local GetGroundZ failed: {Error}", ex.Message);
                return (0f, false);
            }
        }

        public virtual (float groundZ, bool found)[] BatchGetGroundZ(uint mapId, Position[] positions, float maxSearchDist = 10.0f)
        {
            var results = new (float groundZ, bool found)[positions.Length];
            for (int i = 0; i < positions.Length; i++)
                results[i] = GetGroundZ(mapId, positions[i], maxSearchDist);
            return results;
        }

        public virtual bool IsInLineOfSight(uint mapId, Position from, Position to)
        {
            try
            {
                return WoWSharpClient.Movement.NativeLocalPhysics.LineOfSight(
                    mapId, from.X, from.Y, from.Z, to.X, to.Y, to.Z);
            }
            catch (Exception ex)
            {
                _logger?.LogWarning("Local LineOfSight failed: {Error}", ex.Message);
                return false;
            }
        }

        public virtual bool SegmentIntersectsDynamicObjects(uint mapId, Position from, Position to)
        {
            try
            {
                return WoWSharpClient.Movement.NativeLocalPhysics.SegmentIntersectsDynamicObjects(
                    mapId, from.X, from.Y, from.Z, to.X, to.Y, to.Z);
            }
            catch
            {
                return false;
            }
        }

        public virtual (bool onNavmesh, Position nearestPoint) IsPointOnNavmesh(uint mapId, Position position, float searchRadius = 4.0f)
        {
            // IsPointOnNavmesh requires Navigation.dll (navmesh data).
            // Physics.dll doesn't have it — always return false.
            // PathfindingService handles navmesh queries remotely.
            return (false, position);
        }

        public virtual (uint areaType, Position nearestPoint) FindNearestWalkablePoint(uint mapId, Position position, float searchRadius = 8.0f)
        {
            // FindNearestWalkablePoint requires Navigation.dll (navmesh data).
            // Physics.dll doesn't have it — return fallback.
            return (0, position);
        }

        // ════════════════════════════════════════════════════════════════
        //  Helpers
        // ══��═════════════════════════════════════════════════════════════

        // Test seams — subclasses can override to capture/mock remote requests
        protected virtual PathfindingResponse SendRequest(PathfindingRequest request) => SendMessage(request);
        protected virtual PathfindingResponse SendRequest(PathfindingRequest request, int timeoutMs)
            => SendMessage(request, timeoutMs, timeoutMs);
        protected virtual DateTime UtcNow => DateTime.UtcNow;

        private void NoteFailure(string operation, Exception ex)
        {
            _consecutiveFailures++;
            if (_consecutiveFailures <= 3)
                _logger?.LogWarning("PathfindingService {Operation} failed ({Count}): {Msg}", operation, _consecutiveFailures, ex.Message);
            else if (_consecutiveFailures == 4)
                _logger?.LogError("PathfindingService unreachable for {Operation}.", operation);
        }

        private static Game.Position ToProto(Position p) => new() { X = p.X, Y = p.Y, Z = p.Z };
    }
}
