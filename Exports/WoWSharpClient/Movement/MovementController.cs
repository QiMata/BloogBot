using BotRunner.Clients;
using GameData.Core.Enums;
using GameData.Core.Models;
using Pathfinding;
using Serilog;
using System;
using System.Numerics;
using WoWSharpClient.Client;
using WoWSharpClient.Models;
using WoWSharpClient.Parsers;

namespace WoWSharpClient.Movement
{
    public class MovementController(WoWClient client, PathfindingClient physics, WoWLocalPlayer player)
    {
        private readonly WoWClient _client = client;
        private readonly PathfindingClient _physics = physics;
        private readonly WoWLocalPlayer _player = player;

        // Physics state
        private Vector3 _velocity = Vector3.Zero;
        // Keep fall time in milliseconds to match WoW movement packet expectations.
        // PhysicsInput.fall_time in the proto is a float, but we track ms locally to avoid unit drift.
        private uint _fallTimeMs = 0;

        // StepV2 continuity state (must be round-tripped each tick)
        // Initialize prevGroundZ to player's current Z — NegativeInfinity fails the C++ hasPrevGround check
        private float _prevGroundZ = player.Position.Z;
        private Vector3 _prevGroundNormal = new Vector3(0, 0, 1);
        private Vector3 _pendingDepen = Vector3.Zero;
        private uint _standingOnInstanceId = 0;
        private Vector3 _standingOnLocal = Vector3.Zero;

        // Path-following state
        private Position[]? _currentPath;
        private int _currentWaypointIndex;
        private const float WAYPOINT_ARRIVE_DIST = 2.0f;
        private const float TARGET_WAYPOINT_REFRESH_DIST_2D = 0.35f;
        private const float TARGET_WAYPOINT_REFRESH_Z = 1.0f;

        // Network timing
        private uint _lastPacketTime;
        private MovementFlags _lastSentFlags = player.MovementFlags;
        private bool _forceStopAfterReset;
        private const uint PACKET_INTERVAL_MS = 500;
        private uint _latestGameTimeMs;
        private uint _staleForwardSuppressUntilMs;
        private int _staleForwardNoDisplacementTicks;
        private int _staleForwardRecoveryCount;
        private const float STALE_FORWARD_DISPLACEMENT_EPSILON = 0.05f;
        private const int STALE_FORWARD_NO_DISPLACEMENT_THRESHOLD = 30;
        private const uint STALE_FORWARD_SUPPRESS_AFTER_RESET_MS = 2000;
        private const uint STALE_FORWARD_SUPPRESS_AFTER_RECOVERY_MS = 1500;
        private const MovementFlags STALE_RECOVERY_MOVEMENT_MASK =
            MovementFlags.MOVEFLAG_FORWARD |
            MovementFlags.MOVEFLAG_BACKWARD |
            MovementFlags.MOVEFLAG_STRAFE_LEFT |
            MovementFlags.MOVEFLAG_STRAFE_RIGHT;

        // Debug tracking
        private Vector3 _lastPhysicsPosition = new Vector3(player.Position.X, player.Position.Y, player.Position.Z);
        // _accumulatedDelta removed — was causing π speed multiplier bug in dead-reckoning
        private int _frameCounter = 0;
        private int _movementDiagCounter = 0;
        private Vector3 _lastPacketPosition = new Vector3(player.Position.X, player.Position.Y, player.Position.Z);

        // ======== MAIN UPDATE - Called every frame ========
        public void Update(float deltaSec, uint gameTimeMs)
        {
            _frameCounter++;
            _latestGameTimeMs = gameTimeMs;

            if (_forceStopAfterReset)
            {
                SendForcedStopPacket(gameTimeMs);
            }

            if (_lastSentFlags == MovementFlags.MOVEFLAG_NONE
                && _player.MovementFlags == MovementFlags.MOVEFLAG_NONE)
            {
                return;
            }

            Log.Verbose("[MovementController] Frame {Frame} dt={Delta:F4}s Pos=({X:F1},{Y:F1},{Z:F1}) Flags={Flags}",
                _frameCounter, deltaSec, _player.Position.X, _player.Position.Y, _player.Position.Z, _player.MovementFlags);

            // 1. Run physics based on current player state
            var physicsResult = RunPhysics(deltaSec);

            // Check for position mismatch (external teleport, server correction, etc.)
            var physicsPosDiff = new Vector3(
                _player.Position.X - _lastPhysicsPosition.X,
                _player.Position.Y - _lastPhysicsPosition.Y,
                _player.Position.Z - _lastPhysicsPosition.Z
            );
            if (_frameCounter > 1 && physicsPosDiff.Length() > 100.0f)
            {
                // Large jump = teleport. Reset physics state so we don't carry stale velocity/ground.
                Log.Warning("[MovementController] Teleport detected ({Dist:F1} units). Resetting physics state.", physicsPosDiff.Length());
                Reset();
                _lastPhysicsPosition = new Vector3(_player.Position.X, _player.Position.Y, _player.Position.Z);
                return; // Skip this frame — let next frame start fresh
            }
            else if (physicsPosDiff.Length() > 0.01f)
            {
                Log.Warning("[MovementController] Position changed outside physics by {Dist:F3} units", physicsPosDiff.Length());
            }

            ApplyPhysicsResult(physicsResult, deltaSec);
            var newPhysicsPos = new Vector3(physicsResult.NewPosX, physicsResult.NewPosY, physicsResult.NewPosZ);
            var frameDelta = (newPhysicsPos - _lastPhysicsPosition).Length();
            // Log every 100th frame to avoid spam
            if (_frameCounter % 100 == 1 && _player.MovementFlags != MovementFlags.MOVEFLAG_NONE)
            {
                Log.Information("[MovementController] Frame#{Frame} dt={DeltaSec:F4}s frameDelta={FrameDelta:F3}y expected={Expected:F3}y flags=0x{Flags:X}",
                    _frameCounter, deltaSec, frameDelta, deltaSec * _player.RunSpeed, (uint)_player.MovementFlags);
            }

            ObserveStaleForwardAndRecover(frameDelta, gameTimeMs);
            _lastPhysicsPosition = new Vector3(_player.Position.X, _player.Position.Y, _player.Position.Z);

            // 2. Send network packet if needed
            if (ShouldSendPacket(gameTimeMs))
            {
                SendMovementPacket(gameTimeMs);
            }
        }

        // ======== PHYSICS ========
        private PhysicsOutput RunPhysics(float deltaSec)
        {
            // Build physics input from current player state
            var input = new PhysicsInput
            {
                DeltaTime = deltaSec,  // Physics expects seconds, not milliseconds
                MapId = _player.MapId,
                MovementFlags = (uint)_player.MovementFlags,

                PosX = _player.Position.X,
                PosY = _player.Position.Y,
                PosZ = _player.Position.Z,
                Facing = _player.Facing,
                SwimPitch = _player.SwimPitch,

                VelX = _velocity.X,
                VelY = _velocity.Y,
                VelZ = _velocity.Z,

                WalkSpeed = _player.WalkSpeed,
                RunSpeed = _player.RunSpeed,
                RunBackSpeed = _player.RunBackSpeed,
                SwimSpeed = _player.SwimSpeed,
                SwimBackSpeed = _player.SwimBackSpeed,

                // Proto field is float; preserve ms to match existing packet conventions in this client.
                FallTime = _fallTimeMs,

                Race = (uint)_player.Race,
                Gender = (uint)_player.Gender,

                FrameCounter = (uint)_frameCounter,

                // StepV2 continuity inputs
                PrevGroundZ = _prevGroundZ,
                PrevGroundNx = _prevGroundNormal.X,
                PrevGroundNy = _prevGroundNormal.Y,
                PrevGroundNz = _prevGroundNormal.Z,

                PendingDepenX = _pendingDepen.X,
                PendingDepenY = _pendingDepen.Y,
                PendingDepenZ = _pendingDepen.Z,

                StandingOnInstanceId = _standingOnInstanceId,
                StandingOnLocalX = _standingOnLocal.X,
                StandingOnLocalY = _standingOnLocal.Y,
                StandingOnLocalZ = _standingOnLocal.Z,
            };

            return _physics.PhysicsStep(input);
        }

        private int _deadReckonCount = 0;
        private int _physicsMovedCount = 0;

        private void ApplyPhysicsResult(PhysicsOutput output, float deltaSec)
        {
            var oldPos = _player.Position;

            // Diagnostic: detect if physics returned unchanged position while movement was expected.
            // Dead-reckoning fallback has been REMOVED — the physics engine should handle all movement.
            // If physics returns same position, it means: no movement flags, collision blocked, or engine error.
            float dx = output.NewPosX - oldPos.X;
            float dy = output.NewPosY - oldPos.Y;
            bool physicsMovedUs = MathF.Abs(dx) >= 0.001f || MathF.Abs(dy) >= 0.001f;

            if (!physicsMovedUs && _player.MovementFlags != MovementFlags.MOVEFLAG_NONE)
            {
                _deadReckonCount++;
                if (_deadReckonCount % 50 == 1)
                {
                    Log.Warning("[MovementController] Physics returned same position (no dead-reckoning). " +
                        "Count={Count} flags=0x{Flags:X} dt={Dt:F4}",
                        _deadReckonCount, (uint)_player.MovementFlags, deltaSec);
                }
            }
            else if (physicsMovedUs)
            {
                _physicsMovedCount++;
            }

            // Z interpolation from path waypoints when physics doesn't provide valid ground
            bool physicsHasGround = output.GroundZ > -50000f;
            if (!physicsHasGround)
            {
                if (_currentPath != null && _currentWaypointIndex < _currentPath.Length)
                {
                    output.NewPosZ = InterpolatePathZ(output.NewPosX, output.NewPosY, oldPos.Z);
                }
                else
                {
                    // No ground data AND no path — keep current Z to prevent falling through world.
                    // This happens when terrain isn't loaded (e.g. after teleport before physics catches up).
                    output.NewPosZ = oldPos.Z;
                    output.NewVelX = 0;
                    output.NewVelY = 0;
                    output.NewVelZ = 0;
                    output.FallTime = 0;
                }
            }

            // Advance waypoint if we've arrived
            AdvanceWaypointIfNeeded(output.NewPosX, output.NewPosY);

            // Update position from physics
            _player.Position = new Position(output.NewPosX, output.NewPosY, output.NewPosZ);
            _player.SwimPitch = output.Pitch;

            // Update velocity
            _velocity = new Vector3(output.NewVelX, output.NewVelY, output.NewVelZ);

            // Update fall time
            // Keep ms internally; output fall_time is expected to be in the same units as input.
            // If native ever changes to seconds, this should be updated alongside the bridge.
            _fallTimeMs = (uint)MathF.Max(0, output.FallTime);

            // Persist StepV2 continuity outputs for next tick.
            // Don't store sentinel GroundZ (-100000) — keep last valid value so physics can recover.
            if (physicsHasGround)
            {
                _prevGroundZ = output.GroundZ;
                _prevGroundNormal = new Vector3(output.GroundNx, output.GroundNy, output.GroundNz);
            }
            _pendingDepen = new Vector3(output.PendingDepenX, output.PendingDepenY, output.PendingDepenZ);
            _standingOnInstanceId = output.StandingOnInstanceId;
            _standingOnLocal = new Vector3(output.StandingOnLocalX, output.StandingOnLocalY, output.StandingOnLocalZ);

            // Apply physics state flags (falling, swimming, etc)
            const MovementFlags PhysicsFlags =
                MovementFlags.MOVEFLAG_JUMPING |
                MovementFlags.MOVEFLAG_SWIMMING |
                MovementFlags.MOVEFLAG_FLYING |
                MovementFlags.MOVEFLAG_LEVITATING |
                MovementFlags.MOVEFLAG_FALLINGFAR;

            // Preserve input flags, update physics flags
            var inputFlags = _player.MovementFlags & ~PhysicsFlags;
            var newPhysicsFlags = (MovementFlags)(output.MovementFlags) & PhysicsFlags;
            _player.MovementFlags = inputFlags | newPhysicsFlags;

            Log.Verbose("[MovementController] Applied: ({OldX:F1},{OldY:F1},{OldZ:F1}) -> ({NewX:F1},{NewY:F1},{NewZ:F1}) Flags={Flags}",
                oldPos.X, oldPos.Y, oldPos.Z, _player.Position.X, _player.Position.Y, _player.Position.Z, _player.MovementFlags);
        }

        // ======== NETWORKING ========
        private bool ShouldSendPacket(uint gameTimeMs)
        {
            // Send if movement state changed
            if (_player.MovementFlags != _lastSentFlags)
                return true;

            // Send periodic heartbeat while moving
            if (_player.MovementFlags != MovementFlags.MOVEFLAG_NONE &&
                gameTimeMs - _lastPacketTime >= PACKET_INTERVAL_MS)
                return true;

            return false;
        }

        private void SendMovementPacket(uint gameTimeMs)
        {
            if (_forceStopAfterReset)
            {
                SendForcedStopPacket(gameTimeMs);
                return;
            }

            var opcode = DetermineOpcode(_player.MovementFlags, _lastSentFlags);

            // Build and send packet
            var buffer = MovementPacketHandler.BuildMovementInfoBuffer(_player, gameTimeMs, _fallTimeMs);
            _ = _client.SendMovementOpcodeAsync(opcode, buffer);

            // Diagnostic: log position delta between packets
            _movementDiagCounter++;
            var curPos = new Vector3(_player.Position.X, _player.Position.Y, _player.Position.Z);
            var packetDelta = (curPos - _lastPacketPosition).Length();
            var timeSinceLastMs = gameTimeMs - _lastPacketTime;
            if (_movementDiagCounter % 5 == 1)  // Every 5th packet
            {
                Log.Information("[MovementController] {Opcode} Pos=({X:F1},{Y:F1},{Z:F1}) delta={Delta:F2}y dt={DeltaMs}ms speed={Speed:F1} flags=0x{Flags:X}",
                    opcode, _player.Position.X, _player.Position.Y, _player.Position.Z,
                    packetDelta, timeSinceLastMs, _player.RunSpeed, (uint)_player.MovementFlags);
            }
            _lastPacketPosition = curPos;

            // Update tracking
            _lastPacketTime = gameTimeMs;
            _lastSentFlags = _player.MovementFlags;
            _forceStopAfterReset = false;
        }

        private void SendForcedStopPacket(uint gameTimeMs)
        {
            // Always emit a true MSG_MOVE_STOP before resuming movement after reset/recovery.
            // Do not immediately restore pre-stop movement flags in this tick.
            // Let bot logic re-issue movement intent on the next tick so stale forward/strafe
            // state is fully cleared server-side first.
            _player.MovementFlags = MovementFlags.MOVEFLAG_NONE;

            var buffer = MovementPacketHandler.BuildMovementInfoBuffer(_player, gameTimeMs, _fallTimeMs);
            _ = _client.SendMovementOpcodeAsync(Opcode.MSG_MOVE_STOP, buffer);

            _lastPacketPosition = new Vector3(_player.Position.X, _player.Position.Y, _player.Position.Z);
            _lastPacketTime = gameTimeMs;
            _lastSentFlags = MovementFlags.MOVEFLAG_NONE;
            _forceStopAfterReset = false;
            _staleForwardNoDisplacementTicks = 0;
            _staleForwardSuppressUntilMs = AddMs(gameTimeMs, STALE_FORWARD_SUPPRESS_AFTER_RECOVERY_MS);
            Log.Information("[MovementController] Forced MSG_MOVE_STOP dispatched; movement flags remain cleared for clean resume.");
        }

        private static bool IsBefore(uint nowMs, uint targetMs)
            => unchecked((int)(nowMs - targetMs)) < 0;

        private static uint AddMs(uint nowMs, uint durationMs)
            => unchecked(nowMs + durationMs);

        private void ObserveStaleForwardAndRecover(float frameDelta, uint gameTimeMs)
        {
            if (IsBefore(gameTimeMs, _staleForwardSuppressUntilMs))
            {
                _staleForwardNoDisplacementTicks = 0;
                return;
            }

            var flags = _player.MovementFlags;
            var hasHorizontalIntent = (flags & STALE_RECOVERY_MOVEMENT_MASK) != MovementFlags.MOVEFLAG_NONE;
            var transientMotionState =
                flags.HasFlag(MovementFlags.MOVEFLAG_ONTRANSPORT)
                || flags.HasFlag(MovementFlags.MOVEFLAG_JUMPING)
                || flags.HasFlag(MovementFlags.MOVEFLAG_FALLINGFAR);

            if (!hasHorizontalIntent || transientMotionState)
            {
                _staleForwardNoDisplacementTicks = 0;
                return;
            }

            if (frameDelta < STALE_FORWARD_DISPLACEMENT_EPSILON)
                _staleForwardNoDisplacementTicks++;
            else
                _staleForwardNoDisplacementTicks = 0;

            if (_staleForwardNoDisplacementTicks < STALE_FORWARD_NO_DISPLACEMENT_THRESHOLD)
                return;

            var stuckFlags = flags;
            _staleForwardNoDisplacementTicks = 0;
            _staleForwardRecoveryCount++;

            _velocity = Vector3.Zero;
            _fallTimeMs = 0;
            _pendingDepen = Vector3.Zero;
            _standingOnInstanceId = 0;
            _standingOnLocal = Vector3.Zero;
            _currentPath = null;
            _currentWaypointIndex = 0;

            _player.MovementFlags = MovementFlags.MOVEFLAG_NONE;
            _lastSentFlags = stuckFlags;
            _forceStopAfterReset = _lastSentFlags != MovementFlags.MOVEFLAG_NONE;
            _lastPacketTime = 0;
            _staleForwardSuppressUntilMs = AddMs(gameTimeMs, STALE_FORWARD_SUPPRESS_AFTER_RECOVERY_MS);

            Log.Warning("[MovementController] Recovered stale forward movement (recoveries={Recoveries}, frameDelta={FrameDelta:F3}, flags=0x{Flags:X}). Scheduled stop/reset packet.",
                _staleForwardRecoveryCount, frameDelta, (uint)stuckFlags);
        }

        private Opcode DetermineOpcode(MovementFlags current, MovementFlags previous)
        {
            // Stopped moving entirely
            if (current == MovementFlags.MOVEFLAG_NONE && previous != MovementFlags.MOVEFLAG_NONE)
                return Opcode.MSG_MOVE_STOP;

            // Started jumping
            if (current.HasFlag(MovementFlags.MOVEFLAG_JUMPING) && !previous.HasFlag(MovementFlags.MOVEFLAG_JUMPING))
                return Opcode.MSG_MOVE_JUMP;

            // Landed
            if (!current.HasFlag(MovementFlags.MOVEFLAG_JUMPING) && previous.HasFlag(MovementFlags.MOVEFLAG_JUMPING))
                return Opcode.MSG_MOVE_FALL_LAND;

            // Started/stopped swimming
            if (current.HasFlag(MovementFlags.MOVEFLAG_SWIMMING) && !previous.HasFlag(MovementFlags.MOVEFLAG_SWIMMING))
                return Opcode.MSG_MOVE_START_SWIM;
            if (!current.HasFlag(MovementFlags.MOVEFLAG_SWIMMING) && previous.HasFlag(MovementFlags.MOVEFLAG_SWIMMING))
                return Opcode.MSG_MOVE_STOP_SWIM;

            // Started moving forward
            if (current.HasFlag(MovementFlags.MOVEFLAG_FORWARD) && !previous.HasFlag(MovementFlags.MOVEFLAG_FORWARD))
                return Opcode.MSG_MOVE_START_FORWARD;

            // Started moving backward
            if (current.HasFlag(MovementFlags.MOVEFLAG_BACKWARD) && !previous.HasFlag(MovementFlags.MOVEFLAG_BACKWARD))
                return Opcode.MSG_MOVE_START_BACKWARD;

            // Started strafing
            if (current.HasFlag(MovementFlags.MOVEFLAG_STRAFE_LEFT) && !previous.HasFlag(MovementFlags.MOVEFLAG_STRAFE_LEFT))
                return Opcode.MSG_MOVE_START_STRAFE_LEFT;
            if (current.HasFlag(MovementFlags.MOVEFLAG_STRAFE_RIGHT) && !previous.HasFlag(MovementFlags.MOVEFLAG_STRAFE_RIGHT))
                return Opcode.MSG_MOVE_START_STRAFE_RIGHT;

            // Stopped strafing (while still moving)
            if (!current.HasFlag(MovementFlags.MOVEFLAG_STRAFE_LEFT) && !current.HasFlag(MovementFlags.MOVEFLAG_STRAFE_RIGHT)
                && (previous.HasFlag(MovementFlags.MOVEFLAG_STRAFE_LEFT) || previous.HasFlag(MovementFlags.MOVEFLAG_STRAFE_RIGHT)))
                return Opcode.MSG_MOVE_STOP_STRAFE;

            // Default to heartbeat
            return Opcode.MSG_MOVE_HEARTBEAT;
        }

        // ======== SPECIAL PACKETS ========
        public void SendFacingUpdate(uint gameTimeMs)
        {
            // Called by bot when it changes facing directly
            var buffer = MovementPacketHandler.BuildMovementInfoBuffer(_player, gameTimeMs, _fallTimeMs);
            _ = _client.SendMovementOpcodeAsync(Opcode.MSG_MOVE_SET_FACING, buffer);
            Log.Debug("[MovementController] MSG_MOVE_SET_FACING Facing={Facing:F2}", _player.Facing);
        }

        public void SendStopPacket(uint gameTimeMs)
        {
            // Force send a stop packet (useful after teleports or when bot stops)
            _player.MovementFlags = MovementFlags.MOVEFLAG_NONE;
            var buffer = MovementPacketHandler.BuildMovementInfoBuffer(_player, gameTimeMs, _fallTimeMs);
            _ = _client.SendMovementOpcodeAsync(Opcode.MSG_MOVE_STOP, buffer);
            _lastSentFlags = MovementFlags.MOVEFLAG_NONE;
            _forceStopAfterReset = false;
            _staleForwardNoDisplacementTicks = 0;
            _staleForwardSuppressUntilMs = AddMs(gameTimeMs, STALE_FORWARD_SUPPRESS_AFTER_RECOVERY_MS);
            Log.Debug("[MovementController] MSG_MOVE_STOP (forced)");
        }

        // ======== PATH FOLLOWING ========

        /// <summary>
        /// Sets a navigation path for the controller to follow.
        /// The controller will interpolate Z from waypoint heights and auto-advance waypoints.
        /// </summary>
        public void SetPath(Position[] path)
        {
            if (path == null || path.Length == 0)
            {
                _currentPath = null;
                _currentWaypointIndex = 0;
                return;
            }
            _currentPath = path;
            // Always start from waypoint 0; callers may provide paths that do not include current position.
            _currentWaypointIndex = 0;
            Log.Debug("[MovementController] Path set: {Count} waypoints, starting at index {Idx}", path.Length, _currentWaypointIndex);
        }

        /// <summary>
        /// Sets a single target waypoint (convenience for MoveToward calls).
        /// </summary>
        public void SetTargetWaypoint(Position target)
        {
            if (target == null)
            {
                _currentPath = null;
                _currentWaypointIndex = 0;
                return;
            }

            // Corpse-run and nav-driven loops call this every tick.
            // Skip rebuilding a one-segment path when the target is effectively unchanged.
            if (_currentPath != null
                && _currentPath.Length == 2
                && _currentWaypointIndex == 1)
            {
                var existingTarget = _currentPath[1];
                var targetDistance2D = HorizontalDistance(existingTarget.X, existingTarget.Y, target.X, target.Y);
                var targetDeltaZ = MathF.Abs(existingTarget.Z - target.Z);
                if (targetDistance2D <= TARGET_WAYPOINT_REFRESH_DIST_2D
                    && targetDeltaZ <= TARGET_WAYPOINT_REFRESH_Z)
                {
                    return;
                }
            }

            _currentPath = [new Position(_player.Position.X, _player.Position.Y, _player.Position.Z), target];
            _currentWaypointIndex = 1;
        }

        /// <summary>
        /// Clears the current path.
        /// </summary>
        public void ClearPath()
        {
            _currentPath = null;
            _currentWaypointIndex = 0;
        }

        /// <summary>
        /// Returns the current target waypoint, or null if no path is set.
        /// </summary>
        public Position? CurrentWaypoint =>
            _currentPath != null && _currentWaypointIndex < _currentPath.Length
                ? _currentPath[_currentWaypointIndex]
                : null;

        /// <summary>
        /// Interpolates Z height based on progress between the previous waypoint and current target.
        /// Uses the path waypoint Z values which come from the navmesh (correct ground height).
        /// </summary>
        private float InterpolatePathZ(float newX, float newY, float currentZ)
        {
            if (_currentPath == null || _currentWaypointIndex >= _currentPath.Length)
                return currentZ;

            var target = _currentPath[_currentWaypointIndex];

            // Get the previous waypoint (or use current path start)
            var prev = _currentWaypointIndex > 0
                ? _currentPath[_currentWaypointIndex - 1]
                : new Position(_player.Position.X, _player.Position.Y, _player.Position.Z);

            float totalDistXY = HorizontalDistance(prev.X, prev.Y, target.X, target.Y);
            if (totalDistXY < 0.5f)
                return target.Z; // Very close waypoints, just snap Z

            float currentDistXY = HorizontalDistance(newX, newY, target.X, target.Y);
            float t = 1.0f - MathF.Min(currentDistXY / totalDistXY, 1.0f);
            t = MathF.Max(0, t);

            return prev.Z + t * (target.Z - prev.Z);
        }

        /// <summary>
        /// Checks if we've arrived at the current waypoint and advances to the next one.
        /// Also updates facing toward the next waypoint.
        /// </summary>
        private void AdvanceWaypointIfNeeded(float posX, float posY)
        {
            if (_currentPath == null || _currentWaypointIndex >= _currentPath.Length)
                return;

            var wp = _currentPath[_currentWaypointIndex];
            float dist = HorizontalDistance(posX, posY, wp.X, wp.Y);

            if (dist <= WAYPOINT_ARRIVE_DIST)
            {
                _currentWaypointIndex++;
                if (_currentWaypointIndex < _currentPath.Length)
                {
                    // Update facing toward next waypoint
                    var next = _currentPath[_currentWaypointIndex];
                    _player.Facing = MathF.Atan2(next.Y - posY, next.X - posX);
                    Log.Debug("[MovementController] Advanced to waypoint {Idx}/{Total} ({X:F0},{Y:F0},{Z:F0})",
                        _currentWaypointIndex, _currentPath.Length, next.X, next.Y, next.Z);
                }
                else
                {
                    Log.Debug("[MovementController] Reached end of path");
                }
            }
        }

        private static float HorizontalDistance(float x1, float y1, float x2, float y2)
        {
            float dx = x2 - x1;
            float dy = y2 - y1;
            return MathF.Sqrt(dx * dx + dy * dy);
        }

        // ======== STATE MANAGEMENT ========
        public void Reset()
        {
            // Reset physics state (after teleport, death, etc)
            var preResetFlags = _player.MovementFlags;
            var hadMovementToClear = _lastSentFlags != MovementFlags.MOVEFLAG_NONE || preResetFlags != MovementFlags.MOVEFLAG_NONE;
            var stopSeedFlags = _lastSentFlags != MovementFlags.MOVEFLAG_NONE
                ? _lastSentFlags
                : preResetFlags;

            _velocity = Vector3.Zero;
            _fallTimeMs = 0;
            _player.MovementFlags = MovementFlags.MOVEFLAG_NONE;
            _lastSentFlags = hadMovementToClear ? stopSeedFlags : MovementFlags.MOVEFLAG_NONE;
            _forceStopAfterReset = hadMovementToClear;
            _lastPacketTime = 0;

            _prevGroundZ = _player.Position.Z;
            _prevGroundNormal = new Vector3(0, 0, 1);
            _pendingDepen = Vector3.Zero;
            _standingOnInstanceId = 0;
            _standingOnLocal = Vector3.Zero;

            _currentPath = null;
            _currentWaypointIndex = 0;
            _staleForwardNoDisplacementTicks = 0;
            _staleForwardSuppressUntilMs = AddMs(_latestGameTimeMs, STALE_FORWARD_SUPPRESS_AFTER_RESET_MS);

            if (_forceStopAfterReset)
            {
                Log.Information("[MovementController] Reset scheduled stop packet to clear stale movement flags (seed=0x{Flags:X})",
                    (uint)_lastSentFlags);
            }
        }
    }
}
