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
    public readonly record struct LocalSegmentSimulationResult(
        bool Available,
        bool Compatible,
        float MaxUpwardRouteZDelta,
        float MaxAbsoluteRouteZDelta,
        float MaxLateralDistance,
        Position FinalPosition,
        string Reason)
    {
        public static LocalSegmentSimulationResult Unavailable(Position from) => new(
            Available: false,
            Compatible: true,
            MaxUpwardRouteZDelta: 0f,
            MaxAbsoluteRouteZDelta: 0f,
            MaxLateralDistance: 0f,
            FinalPosition: from,
            Reason: "unavailable");

        public static LocalSegmentSimulationResult CompatibleResult(Position finalPosition) => new(
            Available: true,
            Compatible: true,
            MaxUpwardRouteZDelta: 0f,
            MaxAbsoluteRouteZDelta: 0f,
            MaxLateralDistance: 0f,
            FinalPosition: finalPosition,
            Reason: "compatible");
    }

    public readonly record struct PathfindingRouteResult(
        Position[] Corners,
        string Result,
        uint RawCornerCount,
        int? BlockedSegmentIndex,
        string BlockedReason,
        PathSegmentAffordance MaxAffordance,
        bool PathSupported,
        uint StepUpCount,
        uint DropCount,
        uint CliffCount,
        uint VerticalCount,
        float TotalZGain,
        float TotalZLoss,
        float MaxSlopeAngleDeg,
        uint JumpGapCount,
        uint SafeDropCount,
        uint UnsafeDropCount,
        uint BlockedCount,
        float MaxClimbHeight,
        float MaxGapDistance,
        float MaxDropHeight)
    {
        public static PathfindingRouteResult Empty => new(
            Corners: [],
            Result: "error",
            RawCornerCount: 0,
            BlockedSegmentIndex: null,
            BlockedReason: "none",
            MaxAffordance: PathSegmentAffordance.Walk,
            PathSupported: false,
            StepUpCount: 0,
            DropCount: 0,
            CliffCount: 0,
            VerticalCount: 0,
            TotalZGain: 0f,
            TotalZLoss: 0f,
            MaxSlopeAngleDeg: 0f,
            JumpGapCount: 0,
            SafeDropCount: 0,
            UnsafeDropCount: 0,
            BlockedCount: 0,
            MaxClimbHeight: 0f,
            MaxGapDistance: 0f,
            MaxDropHeight: 0f);
    }

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
        private DateTime _lastFailureTime = DateTime.MinValue;
        private const int FailureResetTimeoutSeconds = 60;

        /// <summary>
        /// True when Physics.dll is available for local spatial queries (BG bots).
        /// False in FG bots (running inside WoW.exe — game handles physics natively).
        /// </summary>
        private readonly bool _hasLocalPhysics;

        public PathfindingClient() : this(DefaultPathRequestTimeoutMs) { }

        protected PathfindingClient(int pathRequestTimeoutMs) : base()
        {
            _pathRequestTimeoutMs = pathRequestTimeoutMs;
        }

        public PathfindingClient(
            string ipAddress, int port, ILogger logger,
            int pathRequestTimeoutMs = DefaultPathRequestTimeoutMs,
            bool hasLocalPhysics = false)
            : base(ipAddress, port, logger)
        {
            _logger = logger;
            _pathRequestTimeoutMs = pathRequestTimeoutMs;
            _hasLocalPhysics = hasLocalPhysics;
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
            => GetPathResult(mapId, start, end, nearbyObjects, smoothPath, race, gender).Corners;

        public virtual PathfindingRouteResult GetPathResult(
            uint mapId, Position start, Position end, bool smoothPath = false)
            => GetPathResult(mapId, start, end, nearbyObjects: null, smoothPath, race: 0, gender: 0);

        public virtual PathfindingRouteResult GetPathResult(
            uint mapId, Position start, Position end,
            IReadOnlyList<DynamicObjectProto>? nearbyObjects,
            bool smoothPath = false, Race race = 0, Gender gender = 0)
        {
            ResetFailureWindow();

            try
            {
                var response = SendRequest(
                    BuildPathRequest(mapId, start, end, nearbyObjects, smoothPath, race, gender),
                    _pathRequestTimeoutMs);
                _consecutiveFailures = 0;
                if (response.PayloadCase == PathfindingResponse.PayloadOneofCase.Error)
                    throw new Exception(response.Error.Message);
                if (response.PayloadCase != PathfindingResponse.PayloadOneofCase.Path)
                    throw new Exception($"Unexpected pathfinding response payload: {response.PayloadCase}");

                return ToRouteResult(response.Path);
            }
            catch (Exception ex)
            {
                NoteFailure("path request", ex);
                return PathfindingRouteResult.Empty;
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
            if (!_hasLocalPhysics) return (0f, false);
            return WoWSharpClient.Movement.NativeLocalPhysics.GetGroundZ(
                mapId, position.X, position.Y, position.Z, maxSearchDist);
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
            if (!_hasLocalPhysics) return true; // No local collision data — assume clear LOS
            return WoWSharpClient.Movement.NativeLocalPhysics.LineOfSight(
                mapId, from.X, from.Y, from.Z, to.X, to.Y, to.Z);
        }

        public virtual bool SegmentIntersectsDynamicObjects(uint mapId, Position from, Position to)
        {
            if (!_hasLocalPhysics) return false;
            return WoWSharpClient.Movement.NativeLocalPhysics.SegmentIntersectsDynamicObjects(
                mapId, from.X, from.Y, from.Z, to.X, to.Y, to.Z);
        }

        public virtual LocalSegmentSimulationResult SimulateLocalSegment(
            uint mapId,
            Position from,
            Position to,
            Race race = 0,
            Gender gender = 0,
            float maxDistance = 12.0f,
            float deltaTime = 0.05f)
        {
            if (!_hasLocalPhysics)
                return LocalSegmentSimulationResult.Unavailable(from);

            var dx = to.X - from.X;
            var dy = to.Y - from.Y;
            var dz = to.Z - from.Z;
            var segmentDistance2D = MathF.Sqrt((dx * dx) + (dy * dy));
            if (segmentDistance2D <= 0.05f)
                return LocalSegmentSimulationResult.CompatibleResult(from);

            var travelDistance = MathF.Min(segmentDistance2D, MathF.Max(0.5f, maxDistance));
            var horizonT = travelDistance / segmentDistance2D;
            var horizon = new Position(
                from.X + dx * horizonT,
                from.Y + dy * horizonT,
                from.Z + dz * horizonT);
            var orientation = MathF.Atan2(dy, dx);
            var runSpeed = 7.0f;
            var stepCount = Math.Clamp(
                (int)MathF.Ceiling(travelDistance / MathF.Max(runSpeed * deltaTime, 0.05f)) + 6,
                4,
                96);

            var pos = new Position(from.X, from.Y, from.Z);
            var velX = 0f;
            var velY = 0f;
            var velZ = 0f;
            var prevGroundZ = from.Z;
            var prevGroundNx = 0f;
            var prevGroundNy = 0f;
            var prevGroundNz = 1f;
            var pendingDepenX = 0f;
            var pendingDepenY = 0f;
            var pendingDepenZ = 0f;
            var standingOnInstanceId = 0u;
            var standingOnLocalX = 0f;
            var standingOnLocalY = 0f;
            var standingOnLocalZ = 0f;
            var fallTime = 0f;
            var fallStartZ = from.Z;
            var stepUpBaseZ = -200000f;
            var stepUpAge = 0u;
            var wasGrounded = true;
            var moveFlags = (uint)MovementFlags.MOVEFLAG_FORWARD;
            var maxUpwardZDelta = 0f;
            var maxAbsZDelta = 0f;
            var maxLateralDistance = 0f;
            var hitWall = false;

            try
            {
                for (var step = 0; step < stepCount; step++)
                {
                    var input = new PhysicsInput
                    {
                        MovementFlags = moveFlags | (uint)MovementFlags.MOVEFLAG_FORWARD,
                        PosX = pos.X,
                        PosY = pos.Y,
                        PosZ = pos.Z,
                        Facing = orientation,
                        SwimPitch = 0f,
                        VelX = velX,
                        VelY = velY,
                        VelZ = velZ,
                        WalkSpeed = 2.5f,
                        RunSpeed = runSpeed,
                        RunBackSpeed = 4.5f,
                        SwimSpeed = 4.722222f,
                        SwimBackSpeed = 2.5f,
                        Race = (uint)race,
                        Gender = (uint)gender,
                        MapId = mapId,
                        DeltaTime = deltaTime,
                        FrameCounter = (uint)step,
                        FallTime = fallTime,
                        FallStartZ = fallStartZ,
                        PrevGroundZ = prevGroundZ,
                        PrevGroundNx = prevGroundNx,
                        PrevGroundNy = prevGroundNy,
                        PrevGroundNz = prevGroundNz,
                        PendingDepenX = pendingDepenX,
                        PendingDepenY = pendingDepenY,
                        PendingDepenZ = pendingDepenZ,
                        StandingOnInstanceId = standingOnInstanceId,
                        StandingOnLocalX = standingOnLocalX,
                        StandingOnLocalY = standingOnLocalY,
                        StandingOnLocalZ = standingOnLocalZ,
                        StepUpBaseZ = stepUpBaseZ,
                        StepUpAge = stepUpAge,
                        WasGrounded = wasGrounded,
                    };

                    var output = WoWSharpClient.Movement.NativeLocalPhysics.Step(input);
                    pos = new Position(output.NewPosX, output.NewPosY, output.NewPosZ);
                    velX = output.NewVelX;
                    velY = output.NewVelY;
                    velZ = output.NewVelZ;
                    moveFlags = output.MovementFlags | (uint)MovementFlags.MOVEFLAG_FORWARD;
                    fallTime = output.FallTime;
                    fallStartZ = output.FallStartZ;
                    prevGroundZ = output.GroundZ;
                    prevGroundNx = output.GroundNx;
                    prevGroundNy = output.GroundNy;
                    prevGroundNz = output.GroundNz;
                    pendingDepenX = output.PendingDepenX;
                    pendingDepenY = output.PendingDepenY;
                    pendingDepenZ = output.PendingDepenZ;
                    standingOnInstanceId = output.StandingOnInstanceId;
                    standingOnLocalX = output.StandingOnLocalX;
                    standingOnLocalY = output.StandingOnLocalY;
                    standingOnLocalZ = output.StandingOnLocalZ;
                    stepUpBaseZ = output.StepUpBaseZ;
                    stepUpAge = output.StepUpAge;
                    wasGrounded = (output.MovementFlags & (uint)(MovementFlags.MOVEFLAG_FALLINGFAR | MovementFlags.MOVEFLAG_JUMPING)) == 0;
                    hitWall |= output.HitWall && output.BlockedFraction >= 0.75f;

                    var projectionT = Math.Clamp(
                        (((pos.X - from.X) * dx) + ((pos.Y - from.Y) * dy)) / (segmentDistance2D * segmentDistance2D),
                        0f,
                        1f);
                    var expectedZ = from.Z + dz * projectionT;
                    var zDelta = pos.Z - expectedZ;
                    maxUpwardZDelta = MathF.Max(maxUpwardZDelta, zDelta);
                    maxAbsZDelta = MathF.Max(maxAbsZDelta, MathF.Abs(zDelta));

                    var projectedX = from.X + dx * projectionT;
                    var projectedY = from.Y + dy * projectionT;
                    var lateralX = pos.X - projectedX;
                    var lateralY = pos.Y - projectedY;
                    maxLateralDistance = MathF.Max(
                        maxLateralDistance,
                        MathF.Sqrt((lateralX * lateralX) + (lateralY * lateralY)));

                    if (pos.DistanceTo2D(horizon) <= 0.75f)
                        break;
                }
            }
            catch (Exception ex) when (
                ex is DllNotFoundException
                || ex is EntryPointNotFoundException
                || ex is BadImageFormatException
                || ex is InvalidOperationException)
            {
                _logger?.LogDebug("Local segment simulation unavailable: {Msg}", ex.Message);
                return LocalSegmentSimulationResult.Unavailable(from);
            }

            return new LocalSegmentSimulationResult(
                Available: true,
                Compatible: !hitWall,
                MaxUpwardRouteZDelta: maxUpwardZDelta,
                MaxAbsoluteRouteZDelta: maxAbsZDelta,
                MaxLateralDistance: maxLateralDistance,
                FinalPosition: pos,
                Reason: hitWall ? "hit_wall" : "simulated");
        }

        public virtual (bool onNavmesh, Position nearestPoint) IsPointOnNavmesh(uint mapId, Position position, float searchRadius = 4.0f)
        {
            // Navmesh not available locally (Physics.dll has no mmaps).
            // TODO: Route through PathfindingService (network).
            return (true, position);
        }

        public virtual (uint areaType, Position nearestPoint) FindNearestWalkablePoint(uint mapId, Position position, float searchRadius = 8.0f)
        {
            // Navmesh not available locally (Physics.dll has no mmaps).
            // TODO: Route through PathfindingService (network).
            return (1, position);
        }

        // ════════════════════════════════════════════════════════════════
        //  Helpers
        // ══��═════════════════════════════════════════════════════════════

        // Test seams — subclasses can override to capture/mock remote requests
        protected virtual PathfindingResponse SendRequest(PathfindingRequest request) => SendMessage(request);
        protected virtual PathfindingResponse SendRequest(PathfindingRequest request, int timeoutMs)
            => SendMessage(request, timeoutMs, timeoutMs);
        protected virtual DateTime UtcNow => DateTime.UtcNow;

        private void ResetFailureWindow()
        {
            if (_consecutiveFailures > 0
                && (UtcNow - _lastFailureTime).TotalSeconds > FailureResetTimeoutSeconds)
            {
                _consecutiveFailures = 0;
            }
        }

        private static PathfindingRequest BuildPathRequest(
            uint mapId,
            Position start,
            Position end,
            IReadOnlyList<DynamicObjectProto>? nearbyObjects,
            bool smoothPath,
            Race race,
            Gender gender)
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
            {
                request.Path.NearbyObjects.Add(nearbyObjects);
            }

            return request;
        }

        private static PathfindingRouteResult ToRouteResult(CalculatePathResponse response)
        {
            var blockedSegmentIndex = response.HasBlockedSegment
                ? response.BlockedSegmentIndex
                : (int?)null;

            return new PathfindingRouteResult(
                Corners: response.Corners.Select(p => new Position(p.X, p.Y, p.Z)).ToArray(),
                Result: response.Result,
                RawCornerCount: response.RawCornerCount,
                BlockedSegmentIndex: blockedSegmentIndex,
                BlockedReason: response.BlockedReason,
                MaxAffordance: response.MaxAffordance,
                PathSupported: response.PathSupported,
                StepUpCount: response.StepUpCount,
                DropCount: response.DropCount,
                CliffCount: response.CliffCount,
                VerticalCount: response.VerticalCount,
                TotalZGain: response.TotalZGain,
                TotalZLoss: response.TotalZLoss,
                MaxSlopeAngleDeg: response.MaxSlopeAngleDeg,
                JumpGapCount: response.JumpGapCount,
                SafeDropCount: response.SafeDropCount,
                UnsafeDropCount: response.UnsafeDropCount,
                BlockedCount: response.BlockedCount,
                MaxClimbHeight: response.MaxClimbHeight,
                MaxGapDistance: response.MaxGapDistance,
                MaxDropHeight: response.MaxDropHeight);
        }

        private void NoteFailure(string operation, Exception ex)
        {
            _consecutiveFailures++;
            _lastFailureTime = UtcNow;
            if (_consecutiveFailures <= 3)
                _logger?.LogWarning("PathfindingService {Operation} failed ({Count}): {Msg}", operation, _consecutiveFailures, ex.Message);
            else if (_consecutiveFailures == 4)
                _logger?.LogError("PathfindingService unreachable for {Operation}.", operation);
        }

        private static Game.Position ToProto(Position p) => new() { X = p.X, Y = p.Y, Z = p.Z };
    }
}
