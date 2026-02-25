using BotRunner.Clients;
using GameData.Core.Enums;
using GameData.Core.Models;
using Moq;
using Pathfinding;
using System.Reflection;
using WoWSharpClient.Client;
using WoWSharpClient.Models;
using WoWSharpClient.Movement;
using WoWSharpClient.Parsers;

namespace WoWSharpClient.Tests.Movement
{
    /// <summary>
    /// Tests for MovementController: opcode selection, heartbeat timing, physics integration,
    /// and packet generation. References the WoW 1.12.1 movement protocol (see docs/server-protocol/movement-protocol.md).
    /// </summary>
    public class MovementControllerTests
    {
        private readonly Mock<WoWClient> _mockClient;
        private readonly Mock<PathfindingClient> _mockPhysics;
        private readonly WoWLocalPlayer _player;
        private readonly MovementController _controller;

        // Capture sent packets: (opcode, movementInfoBuffer)
        private readonly List<(Opcode opcode, byte[] buffer)> _sentPackets = [];

        public MovementControllerTests()
        {
            _mockClient = new Mock<WoWClient>();
            _mockPhysics = new Mock<PathfindingClient>();
            _player = new WoWLocalPlayer(new HighGuid(42))
            {
                Position = new Position(100f, 200f, 50f),
                Facing = 1.57f,
                WalkSpeed = 2.5f,
                RunSpeed = 7.0f,
                RunBackSpeed = 4.5f,
                SwimSpeed = 4.722f,
                SwimBackSpeed = 2.5f,
            };

            // Capture all movement packets sent
            _mockClient
                .Setup(c => c.SendMovementOpcodeAsync(
                    It.IsAny<Opcode>(),
                    It.IsAny<byte[]>(),
                    It.IsAny<CancellationToken>()))
                .Callback<Opcode, byte[], CancellationToken>((op, buf, _) =>
                    _sentPackets.Add((op, buf)))
                .Returns(Task.CompletedTask);

            // Default physics: echo back the input position unchanged (grounded)
            _mockPhysics
                .Setup(p => p.PhysicsStep(It.IsAny<PhysicsInput>()))
                .Returns<PhysicsInput>(input => new PhysicsOutput
                {
                    NewPosX = input.PosX,
                    NewPosY = input.PosY,
                    NewPosZ = input.PosZ,
                    NewVelX = 0, NewVelY = 0, NewVelZ = 0,
                    IsGrounded = true,
                    GroundZ = input.PosZ,
                    GroundNx = 0, GroundNy = 0, GroundNz = 1,
                    MovementFlags = input.MovementFlags,
                    FallTime = 0,
                });

            _controller = new MovementController(_mockClient.Object, _mockPhysics.Object, _player);
        }

        // ======== OPCODE SELECTION ========

        [Fact]
        public void StartForward_SendsMsgMoveStartForward()
        {
            _player.MovementFlags = MovementFlags.MOVEFLAG_FORWARD;

            _controller.Update(0.05f, 1000);

            Assert.Single(_sentPackets);
            Assert.Equal(Opcode.MSG_MOVE_START_FORWARD, _sentPackets[0].opcode);
        }

        [Fact]
        public void StartBackward_SendsMsgMoveStartBackward()
        {
            _player.MovementFlags = MovementFlags.MOVEFLAG_BACKWARD;

            _controller.Update(0.05f, 1000);

            Assert.Single(_sentPackets);
            Assert.Equal(Opcode.MSG_MOVE_START_BACKWARD, _sentPackets[0].opcode);
        }

        [Fact]
        public void StopMoving_SendsMsgMoveStop()
        {
            // Start moving
            _player.MovementFlags = MovementFlags.MOVEFLAG_FORWARD;
            _controller.Update(0.05f, 1000);
            _sentPackets.Clear();

            // Stop
            _player.MovementFlags = MovementFlags.MOVEFLAG_NONE;
            _controller.Update(0.05f, 1050);

            Assert.Single(_sentPackets);
            Assert.Equal(Opcode.MSG_MOVE_STOP, _sentPackets[0].opcode);
        }

        [Fact]
        public void Jump_SendsMsgMoveJump()
        {
            // Start moving forward
            _player.MovementFlags = MovementFlags.MOVEFLAG_FORWARD;
            _controller.Update(0.05f, 1000);
            _sentPackets.Clear();

            // Jump while moving forward
            _player.MovementFlags = MovementFlags.MOVEFLAG_FORWARD | MovementFlags.MOVEFLAG_JUMPING;
            _controller.Update(0.05f, 1050);

            Assert.Single(_sentPackets);
            Assert.Equal(Opcode.MSG_MOVE_JUMP, _sentPackets[0].opcode);
        }

        [Fact]
        public void FallLand_SendsMsgMoveFallLand()
        {
            // Moving + jumping
            _player.MovementFlags = MovementFlags.MOVEFLAG_FORWARD | MovementFlags.MOVEFLAG_JUMPING;
            _controller.Update(0.05f, 1000);
            _sentPackets.Clear();

            // Land (jumping flag cleared)
            _player.MovementFlags = MovementFlags.MOVEFLAG_FORWARD;
            _controller.Update(0.05f, 1050);

            Assert.Single(_sentPackets);
            Assert.Equal(Opcode.MSG_MOVE_FALL_LAND, _sentPackets[0].opcode);
        }

        [Fact]
        public void StartSwim_SendsMsgMoveStartSwim()
        {
            _player.MovementFlags = MovementFlags.MOVEFLAG_SWIMMING;

            _controller.Update(0.05f, 1000);

            Assert.Single(_sentPackets);
            Assert.Equal(Opcode.MSG_MOVE_START_SWIM, _sentPackets[0].opcode);
        }

        [Fact]
        public void StopSwim_SendsMsgMoveStopSwim()
        {
            // Start swimming
            _player.MovementFlags = MovementFlags.MOVEFLAG_SWIMMING;
            _controller.Update(0.05f, 1000);
            _sentPackets.Clear();

            // Exit water
            _player.MovementFlags = MovementFlags.MOVEFLAG_NONE;
            _controller.Update(0.05f, 1050);

            // STOP takes priority (NONE vs non-NONE)
            Assert.Single(_sentPackets);
            Assert.Equal(Opcode.MSG_MOVE_STOP, _sentPackets[0].opcode);
        }

        [Fact]
        public void SwimToForward_SendsMsgMoveStopSwim()
        {
            // Start swimming
            _player.MovementFlags = MovementFlags.MOVEFLAG_FORWARD | MovementFlags.MOVEFLAG_SWIMMING;
            _controller.Update(0.05f, 1000);
            _sentPackets.Clear();

            // Leave water but keep moving forward
            _player.MovementFlags = MovementFlags.MOVEFLAG_FORWARD;
            _controller.Update(0.05f, 1050);

            Assert.Single(_sentPackets);
            Assert.Equal(Opcode.MSG_MOVE_STOP_SWIM, _sentPackets[0].opcode);
        }

        [Fact]
        public void StartStrafeLeft_SendsMsgMoveStartStrafeLeft()
        {
            _player.MovementFlags = MovementFlags.MOVEFLAG_STRAFE_LEFT;

            _controller.Update(0.05f, 1000);

            Assert.Single(_sentPackets);
            Assert.Equal(Opcode.MSG_MOVE_START_STRAFE_LEFT, _sentPackets[0].opcode);
        }

        [Fact]
        public void StartStrafeRight_SendsMsgMoveStartStrafeRight()
        {
            _player.MovementFlags = MovementFlags.MOVEFLAG_STRAFE_RIGHT;

            _controller.Update(0.05f, 1000);

            Assert.Single(_sentPackets);
            Assert.Equal(Opcode.MSG_MOVE_START_STRAFE_RIGHT, _sentPackets[0].opcode);
        }

        [Fact]
        public void StopStrafe_SendsMsgMoveStopStrafe()
        {
            // Start moving forward + strafing left
            _player.MovementFlags = MovementFlags.MOVEFLAG_FORWARD | MovementFlags.MOVEFLAG_STRAFE_LEFT;
            _controller.Update(0.05f, 1000);
            _sentPackets.Clear();

            // Stop strafing but keep moving forward
            _player.MovementFlags = MovementFlags.MOVEFLAG_FORWARD;
            _controller.Update(0.05f, 1050);

            Assert.Single(_sentPackets);
            Assert.Equal(Opcode.MSG_MOVE_STOP_STRAFE, _sentPackets[0].opcode);
        }

        // ======== HEARTBEAT TIMING ========

        [Fact]
        public void Heartbeat_SentAfter500ms()
        {
            // Start moving
            _player.MovementFlags = MovementFlags.MOVEFLAG_FORWARD;
            _controller.Update(0.05f, 1000); // Sends START_FORWARD
            _sentPackets.Clear();

            // 400ms later — no heartbeat yet
            _controller.Update(0.05f, 1400);
            Assert.Empty(_sentPackets);

            // 500ms total — heartbeat fires
            _controller.Update(0.05f, 1500);
            Assert.Single(_sentPackets);
            Assert.Equal(Opcode.MSG_MOVE_HEARTBEAT, _sentPackets[0].opcode);
        }

        [Fact]
        public void Heartbeat_NotSentWhenStandingStill()
        {
            // Player is not moving — no packets at all
            _player.MovementFlags = MovementFlags.MOVEFLAG_NONE;

            _controller.Update(0.05f, 1000);
            _controller.Update(0.05f, 1500);
            _controller.Update(0.05f, 2000);

            Assert.Empty(_sentPackets);
        }

        [Fact]
        public void FlagChange_ResetsHeartbeatTimer()
        {
            // Start forward at t=1000
            _player.MovementFlags = MovementFlags.MOVEFLAG_FORWARD;
            _controller.Update(0.05f, 1000); // Sends START_FORWARD
            _sentPackets.Clear();

            // At t=1200, start strafing (flag change → sends immediately)
            _player.MovementFlags = MovementFlags.MOVEFLAG_FORWARD | MovementFlags.MOVEFLAG_STRAFE_LEFT;
            _controller.Update(0.05f, 1200);
            Assert.Single(_sentPackets);
            Assert.Equal(Opcode.MSG_MOVE_START_STRAFE_LEFT, _sentPackets[0].opcode);
            _sentPackets.Clear();

            // At t=1600 (400ms since last packet) — no heartbeat
            _controller.Update(0.05f, 1600);
            Assert.Empty(_sentPackets);

            // At t=1700 (500ms since last packet) — heartbeat
            _controller.Update(0.05f, 1700);
            Assert.Single(_sentPackets);
            Assert.Equal(Opcode.MSG_MOVE_HEARTBEAT, _sentPackets[0].opcode);
        }

        // ======== PACKET CONTENT ========

        [Fact]
        public void SentPacket_ContainsCorrectPosition()
        {
            _player.Position = new Position(1630.5f, -4420.3f, 17.85f);
            _player.Facing = 1.57f;
            _player.MovementFlags = MovementFlags.MOVEFLAG_FORWARD;

            _controller.Update(0.05f, 5000);

            Assert.Single(_sentPackets);
            var buffer = _sentPackets[0].buffer;

            // Parse the buffer back to verify contents
            using var ms = new MemoryStream(buffer);
            using var reader = new BinaryReader(ms);
            var parsed = MovementPacketHandler.ParseMovementInfo(reader);

            Assert.Equal(MovementFlags.MOVEFLAG_FORWARD, parsed.MovementFlags);
            Assert.Equal(5000u, parsed.LastUpdated);
            // Position may differ from original due to dead-reckoning, but should be close
            Assert.InRange(parsed.X, 1620f, 1640f);
            Assert.InRange(parsed.Y, -4430f, -4410f);
            Assert.Equal(1.57f, parsed.Facing);
        }

        [Fact]
        public void SentPacket_IncludesFallTime()
        {
            // Simulate jumping: physics returns non-zero fall time
            _mockPhysics
                .Setup(p => p.PhysicsStep(It.IsAny<PhysicsInput>()))
                .Returns<PhysicsInput>(input => new PhysicsOutput
                {
                    NewPosX = input.PosX,
                    NewPosY = input.PosY,
                    NewPosZ = input.PosZ + 0.5f,
                    IsGrounded = false,
                    MovementFlags = (uint)(MovementFlags.MOVEFLAG_FORWARD | MovementFlags.MOVEFLAG_JUMPING),
                    FallTime = 350,
                    GroundNz = 1,
                });

            _player.MovementFlags = MovementFlags.MOVEFLAG_FORWARD | MovementFlags.MOVEFLAG_JUMPING;
            _controller.Update(0.05f, 2000);

            Assert.Single(_sentPackets);
            var buffer = _sentPackets[0].buffer;

            using var ms = new MemoryStream(buffer);
            using var reader = new BinaryReader(ms);
            var parsed = MovementPacketHandler.ParseMovementInfo(reader);

            Assert.Equal(350u, parsed.FallTime);
        }

        // ======== PHYSICS INTEGRATION ========

        [Fact]
        public void PhysicsStep_CalledWithPlayerState()
        {
            PhysicsInput? capturedInput = null;
            _mockPhysics
                .Setup(p => p.PhysicsStep(It.IsAny<PhysicsInput>()))
                .Callback<PhysicsInput>(input => capturedInput = input)
                .Returns<PhysicsInput>(input => new PhysicsOutput
                {
                    NewPosX = input.PosX,
                    NewPosY = input.PosY,
                    NewPosZ = input.PosZ,
                    IsGrounded = true,
                    GroundNz = 1,
                    MovementFlags = input.MovementFlags,
                });

            _player.Position = new Position(500f, 600f, 100f);
            _player.Facing = 2.0f;
            _player.RunSpeed = 7.0f;
            _player.MovementFlags = MovementFlags.MOVEFLAG_FORWARD;

            _controller.Update(0.1f, 3000);

            Assert.NotNull(capturedInput);
            Assert.Equal(500f, capturedInput.PosX);
            Assert.Equal(600f, capturedInput.PosY);
            Assert.Equal(100f, capturedInput.PosZ);
            Assert.Equal(2.0f, capturedInput.Facing);
            Assert.Equal(7.0f, capturedInput.RunSpeed);
            Assert.Equal(0.1f, capturedInput.DeltaTime, 3);
            Assert.Equal((uint)MovementFlags.MOVEFLAG_FORWARD, capturedInput.MovementFlags);
        }

        [Fact]
        public void PhysicsResult_UpdatesPlayerPosition()
        {
            _mockPhysics
                .Setup(p => p.PhysicsStep(It.IsAny<PhysicsInput>()))
                .Returns(new PhysicsOutput
                {
                    NewPosX = 110f,
                    NewPosY = 210f,
                    NewPosZ = 55f,
                    IsGrounded = true,
                    GroundZ = 55f,
                    GroundNz = 1,
                    MovementFlags = (uint)MovementFlags.MOVEFLAG_FORWARD,
                });

            _player.MovementFlags = MovementFlags.MOVEFLAG_FORWARD;
            _controller.Update(0.05f, 1000);

            Assert.Equal(110f, _player.Position.X);
            Assert.Equal(210f, _player.Position.Y);
            Assert.Equal(55f, _player.Position.Z);
        }

        [Fact]
        public void PhysicsFlags_MergedWithInputFlags()
        {
            // Physics reports that player is now in water
            _mockPhysics
                .Setup(p => p.PhysicsStep(It.IsAny<PhysicsInput>()))
                .Returns(new PhysicsOutput
                {
                    NewPosX = 100f, NewPosY = 200f, NewPosZ = 50f,
                    IsGrounded = false,
                    GroundNz = 1,
                    MovementFlags = (uint)(MovementFlags.MOVEFLAG_FORWARD | MovementFlags.MOVEFLAG_SWIMMING),
                });

            _player.MovementFlags = MovementFlags.MOVEFLAG_FORWARD;
            _controller.Update(0.05f, 1000);

            // Forward is preserved from input, swimming is added from physics
            Assert.True(_player.MovementFlags.HasFlag(MovementFlags.MOVEFLAG_FORWARD));
            Assert.True(_player.MovementFlags.HasFlag(MovementFlags.MOVEFLAG_SWIMMING));
        }

        [Fact]
        public void PhysicsEchoesPosition_PlayerDoesNotMove()
        {
            // Physics echoes back position unchanged (e.g., map not loaded).
            // Dead reckoning was removed — physics engine handles all movement.
            _player.Position = new Position(100f, 200f, 50f);
            _player.Facing = 0f; // Facing east (cos=1, sin=0)
            _player.RunSpeed = 7.0f;
            _player.MovementFlags = MovementFlags.MOVEFLAG_FORWARD;

            _controller.Update(0.1f, 1000);

            // Position should NOT change when physics returns same coords
            Assert.Equal(100f, _player.Position.X);
        }

        // ======== SPECIAL PACKETS ========

        [Fact]
        public void SendFacingUpdate_SendsMsgMoveSetFacing()
        {
            _controller.SendFacingUpdate(5000);

            Assert.Single(_sentPackets);
            Assert.Equal(Opcode.MSG_MOVE_SET_FACING, _sentPackets[0].opcode);
        }

        [Fact]
        public void SendStopPacket_SendsMsgMoveStop_AndClearsFlags()
        {
            _player.MovementFlags = MovementFlags.MOVEFLAG_FORWARD;

            _controller.SendStopPacket(5000);

            Assert.Single(_sentPackets);
            Assert.Equal(Opcode.MSG_MOVE_STOP, _sentPackets[0].opcode);
            Assert.Equal(MovementFlags.MOVEFLAG_NONE, _player.MovementFlags);
        }

        // ======== STATE MANAGEMENT ========

        [Fact]
        public void Reset_ClearsAllPhysicsState()
        {
            // Move forward to establish state
            _player.MovementFlags = MovementFlags.MOVEFLAG_FORWARD;
            _controller.Update(0.05f, 1000);
            _sentPackets.Clear();

            // Reset
            _controller.Reset();

            // After reset, no packet should be sent when standing still
            _player.MovementFlags = MovementFlags.MOVEFLAG_NONE;
            _controller.Update(0.05f, 2000);

            Assert.Empty(_sentPackets);
        }

        [Fact]
        public void Reset_AllowsCleanRestart()
        {
            // Move forward, stop, reset
            _player.MovementFlags = MovementFlags.MOVEFLAG_FORWARD;
            _controller.Update(0.05f, 1000);
            _player.MovementFlags = MovementFlags.MOVEFLAG_NONE;
            _controller.Update(0.05f, 1050);
            _sentPackets.Clear();

            _controller.Reset();

            // Start forward again — should send START_FORWARD (not HEARTBEAT)
            _player.MovementFlags = MovementFlags.MOVEFLAG_FORWARD;
            _controller.Update(0.05f, 3000);

            Assert.Single(_sentPackets);
            Assert.Equal(Opcode.MSG_MOVE_START_FORWARD, _sentPackets[0].opcode);
        }

        // ======== NO-OP WHEN IDLE ========

        [Fact]
        public void Update_NoPhysicsOrPackets_WhenIdleAndPreviouslyIdle()
        {
            // Both current and last flags are NONE — early return
            _player.MovementFlags = MovementFlags.MOVEFLAG_NONE;

            _controller.Update(0.05f, 1000);
            _controller.Update(0.05f, 1500);
            _controller.Update(0.05f, 2000);

            // Physics should not be called (optimization path)
            _mockPhysics.Verify(p => p.PhysicsStep(It.IsAny<PhysicsInput>()), Times.Never());
            Assert.Empty(_sentPackets);
        }

        // ======== GROUND SNAPPING & PHYSICS POSITION ========

        [Fact]
        public void GroundSnap_PhysicsReturnsLowerZ_PlayerZUpdated()
        {
            // Physics engine finds ground at Z=45 when player is at Z=50
            _mockPhysics
                .Setup(p => p.PhysicsStep(It.IsAny<PhysicsInput>()))
                .Returns<PhysicsInput>(input => new PhysicsOutput
                {
                    NewPosX = input.PosX + MathF.Cos(input.Facing) * input.RunSpeed * input.DeltaTime,
                    NewPosY = input.PosY + MathF.Sin(input.Facing) * input.RunSpeed * input.DeltaTime,
                    NewPosZ = 45f, // Ground is below starting Z
                    IsGrounded = true,
                    GroundZ = 45f,
                    GroundNx = 0, GroundNy = 0, GroundNz = 1,
                    MovementFlags = input.MovementFlags,
                    FallTime = 0,
                });

            _player.Position = new Position(100f, 200f, 50f);
            _player.MovementFlags = MovementFlags.MOVEFLAG_FORWARD;

            _controller.Update(0.05f, 1000);

            Assert.Equal(45f, _player.Position.Z);
        }

        [Fact]
        public void GroundSnap_PhysicsReturnsHigherZ_PlayerZUpdated()
        {
            // Physics engine finds ground at Z=55 (uphill)
            _mockPhysics
                .Setup(p => p.PhysicsStep(It.IsAny<PhysicsInput>()))
                .Returns<PhysicsInput>(input => new PhysicsOutput
                {
                    NewPosX = input.PosX,
                    NewPosY = input.PosY,
                    NewPosZ = 55f, // Ground is above starting Z (step-up)
                    IsGrounded = true,
                    GroundZ = 55f,
                    GroundNx = 0, GroundNy = 0, GroundNz = 1,
                    MovementFlags = input.MovementFlags,
                    FallTime = 0,
                });

            _player.Position = new Position(100f, 200f, 50f);
            _player.MovementFlags = MovementFlags.MOVEFLAG_FORWARD;

            _controller.Update(0.05f, 1000);

            Assert.Equal(55f, _player.Position.Z);
        }

        [Fact]
        public void MultiTick_PositionAccumulatesFromPhysics()
        {
            // Physics moves forward 0.35y per tick (7.0 * 0.05)
            int callCount = 0;
            _mockPhysics
                .Setup(p => p.PhysicsStep(It.IsAny<PhysicsInput>()))
                .Returns<PhysicsInput>(input =>
                {
                    callCount++;
                    float step = input.RunSpeed * input.DeltaTime;
                    return new PhysicsOutput
                    {
                        NewPosX = input.PosX + MathF.Cos(input.Facing) * step,
                        NewPosY = input.PosY + MathF.Sin(input.Facing) * step,
                        NewPosZ = input.PosZ,
                        IsGrounded = true,
                        GroundZ = input.PosZ,
                        GroundNx = 0, GroundNy = 0, GroundNz = 1,
                        MovementFlags = input.MovementFlags,
                        FallTime = 0,
                    };
                });

            _player.Position = new Position(100f, 200f, 50f);
            _player.Facing = 0f; // East (cos=1, sin=0)
            _player.MovementFlags = MovementFlags.MOVEFLAG_FORWARD;

            // Run 10 ticks at 50ms each = 0.5s, should move ~3.5y
            for (int i = 0; i < 10; i++)
                _controller.Update(0.05f, (uint)(1000 + i * 50));

            Assert.Equal(10, callCount);
            float expectedX = 100f + 7.0f * 0.5f; // 103.5
            Assert.InRange(_player.Position.X, expectedX - 0.1f, expectedX + 0.1f);
            Assert.Equal(50f, _player.Position.Z); // Z unchanged on flat ground
        }

        [Fact]
        public void PhysicsEchoesPosition_ZStaysConstant()
        {
            // Default mock returns position unchanged — no dead reckoning fallback.
            // Position should remain completely unchanged.
            _player.Position = new Position(100f, 200f, 50f);
            _player.Facing = 0f;
            _player.MovementFlags = MovementFlags.MOVEFLAG_FORWARD;

            _controller.Update(0.1f, 1000);

            Assert.Equal(100f, _player.Position.X);
            Assert.Equal(50f, _player.Position.Z, 2);
        }

        [Fact]
        public void PhysicsContinuityState_RoundTripped()
        {
            // First tick: physics returns ground state
            PhysicsInput? secondInput = null;
            int callCount = 0;
            _mockPhysics
                .Setup(p => p.PhysicsStep(It.IsAny<PhysicsInput>()))
                .Returns<PhysicsInput>(input =>
                {
                    callCount++;
                    if (callCount == 2) secondInput = input;
                    return new PhysicsOutput
                    {
                        NewPosX = input.PosX,
                        NewPosY = input.PosY,
                        NewPosZ = 45f,
                        IsGrounded = true,
                        GroundZ = 44.5f,
                        GroundNx = 0.1f, GroundNy = 0.0f, GroundNz = 0.995f,
                        PendingDepenX = 0.01f, PendingDepenY = 0.02f, PendingDepenZ = 0.03f,
                        StandingOnInstanceId = 42,
                        StandingOnLocalX = 1f, StandingOnLocalY = 2f, StandingOnLocalZ = 3f,
                        MovementFlags = input.MovementFlags,
                        FallTime = 0,
                    };
                });

            _player.MovementFlags = MovementFlags.MOVEFLAG_FORWARD;

            // Tick 1: returns ground state
            _controller.Update(0.05f, 1000);
            // Tick 2: should feed back the continuity state
            _controller.Update(0.05f, 1050);

            Assert.NotNull(secondInput);
            Assert.Equal(44.5f, secondInput.PrevGroundZ);
            Assert.Equal(0.1f, secondInput.PrevGroundNx, 3);
            Assert.Equal(0.995f, secondInput.PrevGroundNz, 3);
            Assert.Equal(0.01f, secondInput.PendingDepenX, 3);
            Assert.Equal(42u, secondInput.StandingOnInstanceId);
            Assert.Equal(1f, secondInput.StandingOnLocalX);
        }

        [Fact]
        public void PhysicsPosition_OverridesDeadReckoning()
        {
            // Physics returns actual moved position → dead reckoning should NOT apply
            _mockPhysics
                .Setup(p => p.PhysicsStep(It.IsAny<PhysicsInput>()))
                .Returns<PhysicsInput>(input => new PhysicsOutput
                {
                    NewPosX = input.PosX + 0.5f, // Physics moved us
                    NewPosY = input.PosY + 0.5f,
                    NewPosZ = 48f, // Ground snapped Z
                    IsGrounded = true,
                    GroundZ = 48f,
                    GroundNx = 0, GroundNy = 0, GroundNz = 1,
                    MovementFlags = input.MovementFlags,
                    FallTime = 0,
                });

            _player.Position = new Position(100f, 200f, 50f);
            _player.Facing = 0f;
            _player.MovementFlags = MovementFlags.MOVEFLAG_FORWARD;

            _controller.Update(0.05f, 1000);

            // Should use physics position, not dead reckoning
            Assert.Equal(100.5f, _player.Position.X);
            Assert.Equal(200.5f, _player.Position.Y);
            Assert.Equal(48f, _player.Position.Z);
        }

        [Fact]
        public void FallVelocity_PreservedBetweenTicks()
        {
            PhysicsInput? secondInput = null;
            int callCount = 0;
            _mockPhysics
                .Setup(p => p.PhysicsStep(It.IsAny<PhysicsInput>()))
                .Returns<PhysicsInput>(input =>
                {
                    callCount++;
                    if (callCount == 2) secondInput = input;
                    return new PhysicsOutput
                    {
                        NewPosX = input.PosX,
                        NewPosY = input.PosY,
                        NewPosZ = input.PosZ - 0.5f, // Falling
                        NewVelX = 0, NewVelY = 0, NewVelZ = -5.0f, // Falling velocity
                        IsGrounded = false,
                        GroundNz = 1,
                        MovementFlags = (uint)(MovementFlags.MOVEFLAG_FORWARD | MovementFlags.MOVEFLAG_FALLINGFAR),
                        FallTime = callCount == 1 ? 100 : 200,
                    };
                });

            _player.MovementFlags = MovementFlags.MOVEFLAG_FORWARD;

            _controller.Update(0.05f, 1000);
            _controller.Update(0.05f, 1050);

            Assert.NotNull(secondInput);
            Assert.Equal(-5.0f, secondInput.VelZ, 1);
            Assert.Equal(100u, (uint)secondInput.FallTime);
        }

        [Fact]
        public void SetPath_StartsAtFirstWaypoint()
        {
            var path = new[]
            {
                new Position(110f, 210f, 51f),
                new Position(120f, 220f, 52f),
            };

            _controller.SetPath(path);

            var waypoint = _controller.CurrentWaypoint;
            Assert.NotNull(waypoint);
            Assert.Equal(110f, waypoint!.X);
            Assert.Equal(210f, waypoint.Y);
            Assert.Equal(51f, waypoint.Z);
        }

        [Fact]
        public void SetTargetWaypoint_SimilarTarget_DoesNotRebuildActiveSingleSegmentPath()
        {
            _controller.SetTargetWaypoint(new Position(130f, 230f, 70f));
            var initialPath = GetCurrentPath(_controller);
            Assert.NotNull(initialPath);
            Assert.Equal(2, initialPath!.Length);

            // Simulate movement since last tick; if path is rebuilt, this position becomes new segment start.
            _player.Position = new Position(101f, 201f, 51f);
            _controller.SetTargetWaypoint(new Position(130.2f, 230.1f, 70.4f));

            var refreshedPath = GetCurrentPath(_controller);
            Assert.Same(initialPath, refreshedPath);
            Assert.Equal(100f, refreshedPath![0].X);
            Assert.Equal(200f, refreshedPath[0].Y);
            Assert.Equal(50f, refreshedPath[0].Z);
        }

        [Fact]
        public void SetTargetWaypoint_DifferentTarget_RebuildsSingleSegmentPathFromCurrentPosition()
        {
            _controller.SetTargetWaypoint(new Position(130f, 230f, 70f));
            var initialPath = GetCurrentPath(_controller);
            Assert.NotNull(initialPath);

            _player.Position = new Position(101f, 201f, 51f);
            _controller.SetTargetWaypoint(new Position(140f, 240f, 72f));

            var rebuiltPath = GetCurrentPath(_controller);
            Assert.NotNull(rebuiltPath);
            Assert.NotSame(initialPath, rebuiltPath);
            Assert.Equal(101f, rebuiltPath![0].X);
            Assert.Equal(201f, rebuiltPath[0].Y);
            Assert.Equal(51f, rebuiltPath[0].Z);
            Assert.Equal(140f, rebuiltPath[1].X);
            Assert.Equal(240f, rebuiltPath[1].Y);
            Assert.Equal(72f, rebuiltPath[1].Z);
        }

        // ======== COMPOUND TRANSITIONS ========

        [Fact]
        public void ForwardToBackward_SendsCorrectOpcodeSequence()
        {
            // Start forward
            _player.MovementFlags = MovementFlags.MOVEFLAG_FORWARD;
            _controller.Update(0.05f, 1000);

            // Change to backward (stop first, then backward — but controller sends on each Update)
            _player.MovementFlags = MovementFlags.MOVEFLAG_BACKWARD;
            _controller.Update(0.05f, 1050);

            // First: START_FORWARD, Second: START_BACKWARD (flags changed from FORWARD to BACKWARD)
            Assert.Equal(2, _sentPackets.Count);
            Assert.Equal(Opcode.MSG_MOVE_START_FORWARD, _sentPackets[0].opcode);
            Assert.Equal(Opcode.MSG_MOVE_START_BACKWARD, _sentPackets[1].opcode);
        }

        [Fact]
        public void MultipleHeartbeats_SentAtCorrectIntervals()
        {
            _player.MovementFlags = MovementFlags.MOVEFLAG_FORWARD;

            // t=0: START_FORWARD
            _controller.Update(0.05f, 0);
            Assert.Single(_sentPackets);

            // t=250: no heartbeat
            _controller.Update(0.05f, 250);
            Assert.Single(_sentPackets);

            // t=500: first heartbeat
            _controller.Update(0.05f, 500);
            Assert.Equal(2, _sentPackets.Count);
            Assert.Equal(Opcode.MSG_MOVE_HEARTBEAT, _sentPackets[1].opcode);

            // t=750: no heartbeat
            _controller.Update(0.05f, 750);
            Assert.Equal(2, _sentPackets.Count);

            // t=1000: second heartbeat
            _controller.Update(0.05f, 1000);
            Assert.Equal(3, _sentPackets.Count);
            Assert.Equal(Opcode.MSG_MOVE_HEARTBEAT, _sentPackets[2].opcode);
        }

        private static Position[]? GetCurrentPath(MovementController controller)
        {
            var field = typeof(MovementController).GetField("_currentPath", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.NotNull(field);
            return field!.GetValue(controller) as Position[];
        }
    }
}
