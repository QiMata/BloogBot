using BotRunner.Clients;
using GameData.Core.Enums;
using GameData.Core.Models;
using Pathfinding;
using System.Numerics;
using WoWSharpClient.Client;
using WoWSharpClient.Models;
using WoWSharpClient.Parsers;

namespace WoWSharpClient.Movement
{
    public class MovementController
    {
        private readonly WoWClient _client;
        private readonly PathfindingClient _physics;
        private readonly WoWLocalPlayer _player;

        // Physics state
        private Vector3 _velocity = Vector3.Zero;
        private uint _fallTime = 0;

        // Network timing
        private uint _lastPacketTime;
        private MovementFlags _lastSentFlags;
        private const uint PACKET_INTERVAL_MS = 500;

        // Debug tracking
        private Vector3 _lastPhysicsPosition;
        private float _accumulatedDelta = 0;
        private int _frameCounter = 0;

        public MovementController(WoWClient client, PathfindingClient physics, WoWLocalPlayer player)
        {
            _client = client;
            _physics = physics;
            _player = player;
            _lastSentFlags = player.MovementFlags;
            _lastPhysicsPosition = new Vector3(player.Position.X, player.Position.Y, player.Position.Z);
        }

        // ======== MAIN UPDATE - Called every frame ========
        public void Update(float deltaSec, uint gameTimeMs)
        {
            _frameCounter++;

            if (_lastSentFlags == MovementFlags.MOVEFLAG_NONE && _player.MovementFlags == MovementFlags.MOVEFLAG_NONE)
            {
                return;
            }

            // Log pre-physics state
            Console.WriteLine($"\n[Frame {_frameCounter}] === PRE-PHYSICS ===");
            Console.WriteLine($"  Delta: {deltaSec:F4}s");
            Console.WriteLine($"  Input Pos: ({_player.Position.X:F3}, {_player.Position.Y:F3}, {_player.Position.Z:F3})");
            Console.WriteLine($"  Input Vel: ({_velocity.X:F3}, {_velocity.Y:F3}, {_velocity.Z:F3})");
            Console.WriteLine($"  Flags: {_player.MovementFlags}");

            // 1. Run physics based on current player state
            var physicsResult = RunPhysics(deltaSec);

            // Log physics output
            Console.WriteLine($"[Frame {_frameCounter}] === PHYSICS OUTPUT ===");
            Console.WriteLine($"  Output Pos: ({physicsResult.NewPosX:F3}, {physicsResult.NewPosY:F3}, {physicsResult.NewPosZ:F3})");
            Console.WriteLine($"  Output Vel: ({physicsResult.NewVelX:F3}, {physicsResult.NewVelY:F3}, {physicsResult.NewVelZ:F3})");
            Console.WriteLine($"  Output Flags: {(MovementFlags)physicsResult.MovementFlags}");

            // Calculate actual movement
            var deltaPos = new Vector3(
                physicsResult.NewPosX - _player.Position.X,
                physicsResult.NewPosY - _player.Position.Y,
                physicsResult.NewPosZ - _player.Position.Z
            );
            var moveDist = MathF.Sqrt(deltaPos.X * deltaPos.X + deltaPos.Y * deltaPos.Y);
            Console.WriteLine($"  Movement: {moveDist:F3} units (XY), {deltaPos.Z:F3} units (Z)");

            // Check for position mismatch
            var physicsPosDiff = new Vector3(
                _player.Position.X - _lastPhysicsPosition.X,
                _player.Position.Y - _lastPhysicsPosition.Y,
                _player.Position.Z - _lastPhysicsPosition.Z
            );
            if (physicsPosDiff.Length() > 0.01f)
            {
                Console.WriteLine($"  WARNING: Position changed outside physics by {physicsPosDiff.Length():F3} units!");
                Console.WriteLine($"    Last physics pos: ({_lastPhysicsPosition.X:F3}, {_lastPhysicsPosition.Y:F3}, {_lastPhysicsPosition.Z:F3})");
                Console.WriteLine($"    Current pos: ({_player.Position.X:F3}, {_player.Position.Y:F3}, {_player.Position.Z:F3})");
            }

            ApplyPhysicsResult(physicsResult);
            _lastPhysicsPosition = new Vector3(physicsResult.NewPosX, physicsResult.NewPosY, physicsResult.NewPosZ);

            // Track accumulated time
            _accumulatedDelta += deltaSec;

            // 2. Send network packet if needed
            if (ShouldSendPacket(gameTimeMs))
            {
                Console.WriteLine($"[Frame {_frameCounter}] === SENDING PACKET ===");
                Console.WriteLine($"  Accumulated time: {_accumulatedDelta:F1}ms");
                SendMovementPacket(gameTimeMs);
                _accumulatedDelta = 0;
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

                FallTime = _fallTime,

                Race = (uint)_player.Race,
                Gender = (uint)_player.Gender,
            };

            Console.WriteLine($"  Physics DeltaTime: {deltaSec:F4}s");

            return _physics.PhysicsStep(input);
        }

        private void ApplyPhysicsResult(PhysicsOutput output)
        {
            var oldPos = _player.Position;

            // Update position from physics
            _player.Position = new Position(output.NewPosX, output.NewPosY, output.NewPosZ);

            // Update velocity
            _velocity = new Vector3(output.NewVelX, output.NewVelY, output.NewVelZ);

            // Update fall time
            _fallTime = (uint)(output.FallTime * 1000); // Convert back to milliseconds

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

            Console.WriteLine($"[Frame {_frameCounter}] === POST-APPLY ===");
            Console.WriteLine($"  Position changed: ({oldPos.X:F3}, {oldPos.Y:F3}, {oldPos.Z:F3}) -> ({_player.Position.X:F3}, {_player.Position.Y:F3}, {_player.Position.Z:F3})");
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
            var opcode = DetermineOpcode(_player.MovementFlags, _lastSentFlags);

            // Build and send packet
            var buffer = MovementPacketHandler.BuildMovementInfoBuffer(_player, gameTimeMs, _fallTime);
            _client.SendMovementOpcode(opcode, buffer);

            // Update tracking
            _lastPacketTime = gameTimeMs;
            _lastSentFlags = _player.MovementFlags;

            Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] {opcode} - Pos({_player.Position.X:F1}, {_player.Position.Y:F1}, {_player.Position.Z:F1}) Flags: {_player.MovementFlags}");
        }

        private Opcode DetermineOpcode(MovementFlags current, MovementFlags previous)
        {
            // Stopped moving
            if (current == MovementFlags.MOVEFLAG_NONE && previous != MovementFlags.MOVEFLAG_NONE)
                return Opcode.MSG_MOVE_STOP;

            // Started jumping
            if (current.HasFlag(MovementFlags.MOVEFLAG_JUMPING) && !previous.HasFlag(MovementFlags.MOVEFLAG_JUMPING))
                return Opcode.MSG_MOVE_JUMP;

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

            // Landed
            if (!current.HasFlag(MovementFlags.MOVEFLAG_JUMPING) && previous.HasFlag(MovementFlags.MOVEFLAG_JUMPING))
                return Opcode.MSG_MOVE_FALL_LAND;

            // Default to heartbeat
            return Opcode.MSG_MOVE_HEARTBEAT;
        }

        // ======== SPECIAL PACKETS ========
        public void SendFacingUpdate(uint gameTimeMs)
        {
            // Called by bot when it changes facing directly
            var buffer = MovementPacketHandler.BuildMovementInfoBuffer(_player, gameTimeMs, _fallTime);
            _client.SendMovementOpcode(Opcode.MSG_MOVE_SET_FACING, buffer);
            Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] MSG_MOVE_SET_FACING - Facing: {_player.Facing:F2}");
        }

        public void SendStopPacket(uint gameTimeMs)
        {
            // Force send a stop packet (useful after teleports or when bot stops)
            _player.MovementFlags = MovementFlags.MOVEFLAG_NONE;
            var buffer = MovementPacketHandler.BuildMovementInfoBuffer(_player, gameTimeMs, _fallTime);
            _client.SendMovementOpcode(Opcode.MSG_MOVE_STOP, buffer);
            _lastSentFlags = MovementFlags.MOVEFLAG_NONE;
            Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] MSG_MOVE_STOP (forced)");
        }
    }
}