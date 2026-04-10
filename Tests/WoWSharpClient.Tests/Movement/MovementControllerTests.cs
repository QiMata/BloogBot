using GameData.Core.Enums;
using GameData.Core.Models;
using Moq;
using System;
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
    public class MovementControllerTests : IDisposable
    {
        private readonly Mock<WoWClient> _mockClient;
        private readonly WoWLocalPlayer _player;
        private readonly MovementController _controller;

        /// <summary>Tracks how many times TestStepOverride was invoked for verification.</summary>
        private int _physicsStepCallCount;

        // Capture sent packets: (opcode, movementInfoBuffer)
        private readonly List<(Opcode opcode, byte[] buffer)> _sentPackets = [];

        public MovementControllerTests()
        {
            _mockClient = new Mock<WoWClient>();
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

            // Prevent native DLL initialization in test environment
            NativeLocalPhysics.TestClearSceneCacheOverride ??= _ => { };

            // Default physics: echo back the input position unchanged (grounded)
            _physicsStepCallCount = 0;
            NativeLocalPhysics.TestStepOverride = input =>
            {
                _physicsStepCallCount++;
                return new NativePhysics.PhysicsOutput
                {
                    X = input.X,
                    Y = input.Y,
                    Z = input.Z,
                    Orientation = input.Orientation,
                    Pitch = input.Pitch,
                    Vx = 0, Vy = 0, Vz = 0,
                    GroundZ = input.Z,
                    GroundNx = 0, GroundNy = 0, GroundNz = 1,
                    MoveFlags = input.MoveFlags,
                    FallTime = 0,
                };
            };

            _controller = new MovementController(_mockClient.Object, _player);
        }

        public void Dispose()
        {
            NativeLocalPhysics.TestStepOverride = null;
            NativeLocalPhysics.TestClearSceneCacheOverride = null;
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
        public void Heartbeat_SentAfterPacketInterval()
        {
            // Start moving
            _player.MovementFlags = MovementFlags.MOVEFLAG_FORWARD;
            _controller.Update(0.05f, 1000); // Sends START_FORWARD
            _sentPackets.Clear();

            // 150ms later — no heartbeat yet (PACKET_INTERVAL_MS = 200)
            _controller.Update(0.05f, 1250);
            Assert.Empty(_sentPackets);

            // 200ms total — heartbeat fires
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

            // At t=1100, start strafing (flag change → sends immediately)
            _player.MovementFlags = MovementFlags.MOVEFLAG_FORWARD | MovementFlags.MOVEFLAG_STRAFE_LEFT;
            _controller.Update(0.05f, 1100);
            Assert.Single(_sentPackets);
            Assert.Equal(Opcode.MSG_MOVE_START_STRAFE_LEFT, _sentPackets[0].opcode);
            _sentPackets.Clear();

            // At t=1250 (150ms since last packet) — no heartbeat
            _controller.Update(0.05f, 1350);
            Assert.Empty(_sentPackets);

            // At t=1300 (200ms since last packet) — heartbeat
            _controller.Update(0.05f, 1600);
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
            NativeLocalPhysics.TestStepOverride = input => new NativePhysics.PhysicsOutput
            {
                X = input.X,
                Y = input.Y,
                Z = input.Z + 0.5f,
                MoveFlags = (uint)(MovementFlags.MOVEFLAG_FORWARD | MovementFlags.MOVEFLAG_JUMPING),
                FallTime = 350,
                GroundNz = 1,
            };

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
            NativePhysics.PhysicsInput? capturedInput = null;
            NativeLocalPhysics.TestStepOverride = input =>
            {
                capturedInput = input;
                return new NativePhysics.PhysicsOutput
                {
                    X = input.X,
                    Y = input.Y,
                    Z = input.Z,
                    GroundNz = 1,
                    MoveFlags = input.MoveFlags,
                };
            };

            _player.Position = new Position(500f, 600f, 100f);
            _player.Facing = 2.0f;
            _player.RunSpeed = 7.0f;
            _player.MovementFlags = MovementFlags.MOVEFLAG_FORWARD;

            _controller.Update(0.1f, 3000);

            Assert.NotNull(capturedInput);
            Assert.Equal(500f, capturedInput!.Value.X);
            Assert.Equal(600f, capturedInput.Value.Y);
            Assert.Equal(100f, capturedInput.Value.Z);
            Assert.Equal(2.0f, capturedInput.Value.Orientation);
            Assert.Equal(7.0f, capturedInput.Value.RunSpeed);
            Assert.Equal(0.1f, capturedInput.Value.DeltaTime, 3);
            Assert.Equal((uint)MovementFlags.MOVEFLAG_FORWARD, capturedInput.Value.MoveFlags);
        }

        [Fact]
        public void PhysicsStep_OnTransport_UsesLocalCoordinatesAndIncludesTransportObject()
        {
            NativePhysics.PhysicsInput? capturedInput = null;
            NativePhysics.DynamicObjectInfo[]? capturedNearbyObjects = null;
            NativeLocalPhysics.TestStepOverride = input =>
            {
                capturedInput = input;
                capturedNearbyObjects = NativeLocalPhysics.ReadNearbyObjectsForTest(input);
                return new NativePhysics.PhysicsOutput
                {
                    X = 110f,
                    Y = 210f,
                    Z = 55f,
                    Orientation = 2.2f,
                    GroundZ = 55f,
                    GroundNz = 1,
                    MoveFlags = input.MoveFlags,
                };
            };

            var transport = new WoWGameObject(new HighGuid(0xF120000000000001ul))
            {
                Position = new Position(100f, 200f, 50f),
                Facing = MathF.PI / 2f,
                DisplayId = 455,
                ScaleX = 1f,
            };

            _player.Transport = transport;
            _player.TransportGuid = transport.Guid;
            _player.Position = new Position(110f, 210f, 55f);
            _player.Facing = 2.2f;
            _player.TransportOffset = new Position(10f, -10f, 5f);
            _player.TransportOrientation = _player.Facing - transport.Facing;
            _player.MovementFlags = MovementFlags.MOVEFLAG_FORWARD | MovementFlags.MOVEFLAG_ONTRANSPORT;

            _controller.Update(0.1f, 3000);

            Assert.NotNull(capturedInput);
            Assert.Equal(10f, capturedInput!.Value.X, 3);
            Assert.Equal(-10f, capturedInput.Value.Y, 3);
            Assert.Equal(5f, capturedInput.Value.Z, 3);
            Assert.Equal(_player.TransportOrientation, capturedInput.Value.Orientation, 3);
            Assert.Equal(transport.Guid, capturedInput.Value.TransportGuid);
            Assert.NotNull(capturedNearbyObjects);
            Assert.Single(capturedNearbyObjects!);
            Assert.Equal(transport.Guid, capturedNearbyObjects[0].Guid);
            Assert.Equal(transport.Position.X, capturedNearbyObjects[0].X, 3);
            Assert.Equal(transport.Position.Y, capturedNearbyObjects[0].Y, 3);
            Assert.Equal(transport.Position.Z, capturedNearbyObjects[0].Z, 3);
        }

        [Fact]
        public void PhysicsStep_NearbyObjects_FiltersToFiniteCollidableSubset()
        {
            NativePhysics.PhysicsInput? capturedInput = null;
            NativePhysics.DynamicObjectInfo[]? capturedNearbyObjects = null;
            NativeLocalPhysics.TestStepOverride = input =>
            {
                capturedInput = input;
                capturedNearbyObjects = NativeLocalPhysics.ReadNearbyObjectsForTest(input);
                return new NativePhysics.PhysicsOutput
                {
                    X = input.X,
                    Y = input.Y,
                    Z = input.Z,
                    Orientation = input.Orientation,
                    GroundZ = input.Z,
                    GroundNz = 1f,
                    MoveFlags = input.MoveFlags,
                };
            };

            var includedDoor = new WoWGameObject(new HighGuid(0xF120000000000101ul))
            {
                Position = new Position(105f, 200f, 50f),
                Facing = 0.25f,
                DisplayId = 111,
                ScaleX = 1.25f,
                TypeId = (uint)GameObjectType.Door,
            };

            var filteredQuestGiver = new WoWGameObject(new HighGuid(0xF120000000000102ul))
            {
                Position = new Position(106f, 200f, 50f),
                Facing = 0.5f,
                DisplayId = 222,
                ScaleX = 1f,
                TypeId = (uint)GameObjectType.QuestGiver,
            };

            var filteredZeroDisplay = new WoWGameObject(new HighGuid(0xF120000000000103ul))
            {
                Position = new Position(107f, 200f, 50f),
                Facing = 0.75f,
                DisplayId = 0,
                ScaleX = 1f,
                TypeId = (uint)GameObjectType.Door,
            };

            var filteredInvalidPosition = new WoWGameObject(new HighGuid(0xF120000000000104ul))
            {
                Position = new Position(float.NaN, 200f, 50f),
                Facing = 0.9f,
                DisplayId = 333,
                ScaleX = 1f,
                TypeId = (uint)GameObjectType.Door,
            };

            var filteredFarDoor = new WoWGameObject(new HighGuid(0xF120000000000105ul))
            {
                Position = new Position(200f, 200f, 50f),
                Facing = 1.1f,
                DisplayId = 444,
                ScaleX = 1f,
                TypeId = (uint)GameObjectType.Door,
            };

            var snapshot = ReplaceTrackedObjects(
                includedDoor,
                filteredQuestGiver,
                filteredZeroDisplay,
                filteredInvalidPosition,
                filteredFarDoor);

            try
            {
                _player.Position = new Position(100f, 200f, 50f);
                _player.MovementFlags = MovementFlags.MOVEFLAG_FORWARD;

                _controller.Update(0.05f, 1000);

                Assert.NotNull(capturedInput);
                Assert.NotNull(capturedNearbyObjects);
                Assert.Single(capturedNearbyObjects!);
                Assert.Equal(includedDoor.Guid, capturedNearbyObjects[0].Guid);
                Assert.Equal(includedDoor.DisplayId, capturedNearbyObjects[0].DisplayId);
                Assert.Equal(includedDoor.ScaleX, capturedNearbyObjects[0].Scale, 3);
            }
            finally
            {
                RestoreTrackedObjects(snapshot);
            }
        }

        [Fact]
        public void PhysicsStep_NearbyObjects_CapsCountAndRetainsActiveTransport()
        {
            NativePhysics.PhysicsInput? capturedInput = null;
            NativePhysics.DynamicObjectInfo[]? capturedNearbyObjects = null;
            NativeLocalPhysics.TestStepOverride = input =>
            {
                capturedInput = input;
                capturedNearbyObjects = NativeLocalPhysics.ReadNearbyObjectsForTest(input);
                return new NativePhysics.PhysicsOutput
                {
                    X = 110f,
                    Y = 210f,
                    Z = 55f,
                    Orientation = 2.2f,
                    GroundZ = 55f,
                    GroundNz = 1f,
                    MoveFlags = input.MoveFlags,
                };
            };

            var transport = new WoWGameObject(new HighGuid(0xF120000000000106ul))
            {
                Position = new Position(100f, 200f, 50f),
                Facing = MathF.PI / 2f,
                DisplayId = 455,
                ScaleX = 1f,
                TypeId = (uint)GameObjectType.Transport,
            };

            var crowdedDoors = Enumerable.Range(0, 80)
                .Select(i => new WoWGameObject(new HighGuid(0xF120000000001000ul + (ulong)i))
                {
                    Position = new Position(110f + (i % 8) * 0.2f, 210f + (i / 8) * 0.2f, 55f),
                    Facing = 0.1f * i,
                    DisplayId = (uint)(600 + i),
                    ScaleX = 1f,
                    TypeId = (uint)GameObjectType.Door,
                })
                .Cast<WoWObject>()
                .ToArray();

            var snapshot = ReplaceTrackedObjects([transport, .. crowdedDoors]);

            try
            {
                _player.Transport = transport;
                _player.TransportGuid = transport.Guid;
                _player.Position = new Position(110f, 210f, 55f);
                _player.Facing = 2.2f;
                _player.TransportOffset = new Position(10f, -10f, 5f);
                _player.TransportOrientation = _player.Facing - transport.Facing;
                _player.MovementFlags = MovementFlags.MOVEFLAG_FORWARD | MovementFlags.MOVEFLAG_ONTRANSPORT;

                _controller.Update(0.1f, 3000);

                Assert.NotNull(capturedInput);
                Assert.NotNull(capturedNearbyObjects);
                Assert.Equal(64, capturedNearbyObjects!.Length);
                Assert.Contains(capturedNearbyObjects, obj => obj.Guid == transport.Guid);
            }
            finally
            {
                RestoreTrackedObjects(snapshot);
            }
        }

        [Fact]
        public void PhysicsResult_OnTransport_RecomputesLocalOffsetFromWorldOutput()
        {
            var transport = new WoWGameObject(new HighGuid(0xF120000000000002ul))
            {
                Position = new Position(100f, 200f, 50f),
                Facing = MathF.PI / 2f,
                DisplayId = 455,
                ScaleX = 1f,
            };

            _player.Transport = transport;
            _player.TransportGuid = transport.Guid;
            _player.Position = new Position(101f, 202f, 53f);
            _player.Facing = 2.1f;
            _player.TransportOffset = new Position(2f, -1f, 3f);
            _player.TransportOrientation = _player.Facing - transport.Facing;
            _player.MovementFlags = MovementFlags.MOVEFLAG_ONTRANSPORT;

            NativeLocalPhysics.TestStepOverride = _ => new NativePhysics.PhysicsOutput
            {
                X = 102f,
                Y = 204f,
                Z = 53f,
                Orientation = 2.1f,
                GroundZ = 53f,
                GroundNz = 1f,
                MoveFlags = (uint)MovementFlags.MOVEFLAG_ONTRANSPORT,
            };

            _controller.Update(0.05f, 1000);

            Assert.Equal(102f, _player.Position.X, 3);
            Assert.Equal(204f, _player.Position.Y, 3);
            Assert.Equal(53f, _player.Position.Z, 3);
            Assert.Equal(4f, _player.TransportOffset.X, 3);
            Assert.Equal(-2f, _player.TransportOffset.Y, 3);
            Assert.Equal(3f, _player.TransportOffset.Z, 3);
            Assert.Equal(2.1f - transport.Facing, _player.TransportOrientation, 3);
            Assert.Equal(2.1f, _player.Facing, 3);
        }

        [Fact]
        public void PhysicsResult_UpdatesPlayerPosition()
        {
            NativeLocalPhysics.TestStepOverride = _ => new NativePhysics.PhysicsOutput
            {
                X = 110f,
                Y = 210f,
                Z = 55f,
                GroundZ = 55f,
                GroundNz = 1,
                MoveFlags = (uint)MovementFlags.MOVEFLAG_FORWARD,
            };

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
            NativeLocalPhysics.TestStepOverride = _ => new NativePhysics.PhysicsOutput
            {
                X = 100f, Y = 200f, Z = 50f,
                GroundNz = 1,
                MoveFlags = (uint)(MovementFlags.MOVEFLAG_FORWARD | MovementFlags.MOVEFLAG_SWIMMING),
            };

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
        public void SendMovementStartFacingUpdate_SendsSetFacingOnly()
        {
            _controller.SendMovementStartFacingUpdate(5000);

            Assert.Single(_sentPackets);
            Assert.Equal(Opcode.MSG_MOVE_SET_FACING, _sentPackets[0].opcode);
        }

        [Fact]
        public void SendFacingUpdate_StandingStill_SendsSetFacingOnly()
        {
            _controller.SendFacingUpdate(5000);

            Assert.Single(_sentPackets);
            Assert.Equal(Opcode.MSG_MOVE_SET_FACING, _sentPackets[0].opcode);
        }

        [Fact]
        public void SendFacingUpdate_AfterMovement_SendsSetFacingOnly()
        {
            _player.MovementFlags = MovementFlags.MOVEFLAG_FORWARD;
            _controller.Update(0.05f, 1000);
            _sentPackets.Clear();

            _player.Facing = 2.25f;
            _controller.SendFacingUpdate(1100);

            Assert.Single(_sentPackets);
            Assert.Equal(Opcode.MSG_MOVE_SET_FACING, _sentPackets[0].opcode);
        }

        [Fact]
        public void SendStopPacket_SendsMsgMoveStop_AfterForwardMovementWasSent()
        {
            _player.MovementFlags = MovementFlags.MOVEFLAG_FORWARD;
            _controller.Update(0.05f, 1000);
            _sentPackets.Clear();

            _controller.SendStopPacket(5000);

            Assert.Single(_sentPackets);
            Assert.Equal(Opcode.MSG_MOVE_STOP, _sentPackets[0].opcode);
            Assert.Equal(MovementFlags.MOVEFLAG_NONE, _player.MovementFlags);
        }

        [Fact]
        public void SendStopPacket_PreservesFallingFlags_WhenClearingForwardIntent()
        {
            _player.MovementFlags = MovementFlags.MOVEFLAG_FORWARD | MovementFlags.MOVEFLAG_FALLINGFAR;
            _controller.Update(0.05f, 1000);
            _sentPackets.Clear();

            _controller.SendStopPacket(5000);

            Assert.Single(_sentPackets);
            Assert.Equal(Opcode.MSG_MOVE_HEARTBEAT, _sentPackets[0].opcode);
            Assert.Equal(MovementFlags.MOVEFLAG_FALLINGFAR, _player.MovementFlags);
        }

        [Fact]
        public void RequestGroundedStop_ClearsForwardIntent_OnFirstGroundedFrame()
        {
            _player.MovementFlags = MovementFlags.MOVEFLAG_FORWARD | MovementFlags.MOVEFLAG_FALLINGFAR;
            _controller.Update(0.05f, 1000);
            _sentPackets.Clear();

            _controller.RequestGroundedStop();

            NativeLocalPhysics.TestStepOverride = input => new NativePhysics.PhysicsOutput
            {
                X = input.X,
                Y = input.Y,
                Z = input.Z,
                Orientation = input.Orientation,
                Pitch = input.Pitch,
                Vx = 0,
                Vy = 0,
                Vz = 0,
                GroundZ = input.Z,
                GroundNx = 0,
                GroundNy = 0,
                GroundNz = 1,
                MoveFlags = (uint)MovementFlags.MOVEFLAG_FORWARD,
                FallTime = 0,
            };

            _controller.Update(0.05f, 1050);

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

            // Reset — schedules a stop packet to clear server-side movement flags
            _controller.Reset();

            // First update after reset sends MSG_MOVE_STOP to inform the server
            _player.MovementFlags = MovementFlags.MOVEFLAG_NONE;
            _controller.Update(0.05f, 2000);

            Assert.Equal(2, _sentPackets.Count);
            Assert.Equal(Opcode.MSG_MOVE_STOP, _sentPackets[0].opcode);
            Assert.Equal(Opcode.MSG_MOVE_HEARTBEAT, _sentPackets[1].opcode);

            // Second update should be clean — no additional packets
            _sentPackets.Clear();
            _controller.Update(0.05f, 3000);
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

        // ======== IDLE PHYSICS ========

        [Fact]
        public void Update_SkipsPhysicsOnTrulyIdleFrames()
        {
            // When movement flags are NONE and no ground snap is needed,
            // the idle guard short-circuits Update to avoid unnecessary physics.
            _physicsStepCallCount = 0;
            _player.MovementFlags = MovementFlags.MOVEFLAG_NONE;

            _controller.Update(0.05f, 1000);
            _controller.Update(0.05f, 1500);
            _controller.Update(0.05f, 2000);

            Assert.Equal(0, _physicsStepCallCount);
            Assert.Empty(_sentPackets);
        }

        [Fact]
        public void Update_SkipsLocalPhysicsWhileIdle_SeparateController()
        {
            // Verify that a separate MovementController instance (with null physics)
            // also skips physics on truly idle frames (idle guard).
            var localStepCount = 0;
            NativeLocalPhysics.TestStepOverride = input =>
            {
                localStepCount++;
                return new NativePhysics.PhysicsOutput
                {
                    X = input.X,
                    Y = input.Y,
                    Z = input.Z,
                    Orientation = input.Orientation,
                    Pitch = input.Pitch,
                    Vx = 0,
                    Vy = 0,
                    Vz = 0,
                    GroundZ = input.Z,
                    GroundNx = 0,
                    GroundNy = 0,
                    GroundNz = 1,
                    MoveFlags = input.MoveFlags,
                    FallTime = 0,
                };
            };

            var controller = new MovementController(_mockClient.Object, _player);
            _player.MovementFlags = MovementFlags.MOVEFLAG_NONE;

            controller.Update(0.05f, 1000);
            controller.Update(0.05f, 1500);
            controller.Update(0.05f, 2000);

            Assert.Equal(0, localStepCount);
            Assert.Empty(_sentPackets);
        }

        [Fact]
        public void Update_LocalNativePhysics_ForwardsNearbyObjectsToNavigationInput()
        {
            NativePhysics.PhysicsInput? capturedNativeInput = null;
            NativePhysics.DynamicObjectInfo[]? capturedNearbyObjects = null;

            var transport = new WoWGameObject(new HighGuid(0xF120000000000201ul))
            {
                Position = new Position(100f, 200f, 50f),
                Facing = MathF.PI / 2f,
                DisplayId = 455,
                ScaleX = 1f,
                TypeId = (uint)GameObjectType.Transport,
            };

            var nearbyDoor = new WoWGameObject(new HighGuid(0xF120000000000202ul))
            {
                Position = new Position(112f, 212f, 55f),
                Facing = 0.35f,
                DisplayId = 711,
                ScaleX = 1.15f,
                TypeId = (uint)GameObjectType.Door,
            };

            var snapshot = ReplaceTrackedObjects(transport, nearbyDoor);
            var controller = new MovementController(_mockClient.Object, _player);

            try
            {
                NativeLocalPhysics.TestStepOverride = input =>
                {
                    capturedNativeInput = input;
                    capturedNearbyObjects = NativeLocalPhysics.ReadNearbyObjectsForTest(input);
                    return new NativePhysics.PhysicsOutput
                    {
                        X = input.X,
                        Y = input.Y,
                        Z = input.Z,
                        Orientation = input.Orientation,
                        Pitch = input.Pitch,
                        Vx = 0f,
                        Vy = 0f,
                        Vz = 0f,
                        MoveFlags = input.MoveFlags,
                        GroundZ = input.Z,
                        GroundNx = 0f,
                        GroundNy = 0f,
                        GroundNz = 1f,
                    };
                };

                _player.Transport = transport;
                _player.TransportGuid = transport.Guid;
                _player.Position = new Position(110f, 210f, 55f);
                _player.Facing = 2.2f;
                _player.TransportOffset = new Position(10f, -10f, 5f);
                _player.TransportOrientation = _player.Facing - transport.Facing;
                _player.MovementFlags = MovementFlags.MOVEFLAG_FORWARD | MovementFlags.MOVEFLAG_ONTRANSPORT;

                controller.Update(0.1f, 3000);

                Assert.NotNull(capturedNativeInput);
                Assert.Equal(transport.Guid, capturedNativeInput!.Value.TransportGuid);
                Assert.NotNull(capturedNearbyObjects);
                Assert.Equal(2, capturedNearbyObjects!.Length);
                Assert.Contains(capturedNearbyObjects, obj => obj.Guid == transport.Guid && obj.DisplayId == transport.DisplayId);
                Assert.Contains(capturedNearbyObjects, obj => obj.Guid == nearbyDoor.Guid && obj.DisplayId == nearbyDoor.DisplayId);
            }
            finally
            {
                NativeLocalPhysics.TestStepOverride = null;
                NativeLocalPhysics.TestClearSceneCacheOverride = null;
                RestoreTrackedObjects(snapshot);
            }
        }

        [Fact]
        public void Update_LocalNativePhysics_ContinuesOnSceneRefreshFailure()
        {
            try
            {
                var sceneDataClient = new SceneDataClient(Mock.Of<Microsoft.Extensions.Logging.ILogger>());
                var controller = new MovementController(_mockClient.Object, _player, sceneDataClient);
                var stepCallCount = 0;

                SceneDataClient.TestEnsureSceneDataAroundOverride = (_, _, _) => false;
                NativeLocalPhysics.TestStepOverride = input =>
                {
                    stepCallCount++;
                    return new NativePhysics.PhysicsOutput
                    {
                        X = input.X,
                        Y = input.Y,
                        Z = input.Z - 1.5f,
                        Orientation = input.Orientation,
                        Pitch = input.Pitch,
                        Vx = 0f,
                        Vy = 0f,
                        Vz = -5f,
                        MoveFlags = (uint)MovementFlags.MOVEFLAG_FALLINGFAR,
                        FallTime = 50u,
                        GroundZ = -50001f,
                        GroundNx = 0f,
                        GroundNy = 0f,
                        GroundNz = 1f,
                    };
                };

                // Use FALLINGFAR to bypass idle guard, allowing physics to run
                _player.MovementFlags = MovementFlags.MOVEFLAG_FALLINGFAR;
                _player.Position = new Position(100f, 200f, 53f);

                controller.Update(0.05f, 1000);

                Assert.Equal(1, stepCallCount);
                Assert.Equal(51.5f, _player.Position.Z);
                Assert.True((_player.MovementFlags & MovementFlags.MOVEFLAG_FALLINGFAR) != 0);
                Assert.Single(_sentPackets);
                Assert.Equal(Opcode.MSG_MOVE_HEARTBEAT, _sentPackets[0].opcode);
            }
            finally
            {
                SceneDataClient.TestEnsureSceneDataAroundOverride = null;
                NativeLocalPhysics.TestStepOverride = null;
                NativeLocalPhysics.TestClearSceneCacheOverride = null;
            }
        }

        [Fact]
        public void Update_TeleportWithGroundSnap_RunsPhysics()
        {
            // Simulate a teleport that triggers ground snap. The idle guard
            // allows physics when _needsGroundSnap is true.
            NativeLocalPhysics.TestStepOverride = input => new NativePhysics.PhysicsOutput
            {
                X = input.X,
                Y = input.Y,
                Z = input.Z - 1.5f,
                Orientation = input.Orientation,
                Pitch = input.Pitch,
                Vx = 0f,
                Vy = 0f,
                Vz = -5f,
                GroundZ = -50001f,
                GroundNx = 0f,
                GroundNy = 0f,
                GroundNz = 1f,
                MoveFlags = (uint)MovementFlags.MOVEFLAG_FALLINGFAR,
                FallTime = 50,
            };

            _player.Position = new Position(100f, 200f, 53f);
            _player.MovementFlags = MovementFlags.MOVEFLAG_NONE;
            _controller.Reset(teleportDestZ: 53f);
            _sentPackets.Clear();

            _controller.Update(0.05f, 1500);

            Assert.Equal(51.5f, _player.Position.Z);
            Assert.True((_player.MovementFlags & MovementFlags.MOVEFLAG_FALLINGFAR) != 0);
        }

        [Fact]
        public void Update_PostTeleport_NoGroundBelow_AllowsGraceFall()
        {
            NativeLocalPhysics.TestStepOverride = input => new NativePhysics.PhysicsOutput
            {
                X = input.X,
                Y = input.Y,
                Z = input.Z - 1.5f,
                Orientation = input.Orientation,
                Pitch = input.Pitch,
                Vx = 0f,
                Vy = 0f,
                Vz = -5f,
                GroundZ = -50001f,
                GroundNx = 0f,
                GroundNy = 0f,
                GroundNz = 1f,
                MoveFlags = (uint)MovementFlags.MOVEFLAG_FALLINGFAR,
                FallTime = 50,
            };

            _player.Position = new Position(100f, 200f, 123.89f);
            _player.MovementFlags = MovementFlags.MOVEFLAG_NONE;
            _controller.Reset(teleportDestZ: 123.89f);
            _sentPackets.Clear();

            _controller.Update(0.05f, 1000);

            Assert.Equal(122.39f, _player.Position.Z, 2);
            Assert.True((_player.MovementFlags & MovementFlags.MOVEFLAG_FALLINGFAR) != 0);
            Assert.True(_controller.NeedsGroundSnap);
            Assert.Empty(_sentPackets);
        }

        [Fact]
        public void Update_PostTeleport_RejectsSupportAboveTeleportTarget_AndContinuesFalling()
        {
            var callCount = 0;
            NativeLocalPhysics.TestStepOverride = input =>
            {
                callCount++;
                if (callCount == 1)
                {
                    return new NativePhysics.PhysicsOutput
                    {
                        X = input.X,
                        Y = input.Y,
                        Z = input.Z + 7f,
                        Orientation = input.Orientation,
                        Pitch = input.Pitch,
                        Vx = 0f,
                        Vy = 0f,
                        Vz = 0f,
                        GroundZ = input.Z + 7f,
                        GroundNx = 0f,
                        GroundNy = 0f,
                        GroundNz = 1f,
                        MoveFlags = input.MoveFlags,
                        FallTime = 0,
                    };
                }

                return new NativePhysics.PhysicsOutput
                {
                    X = input.X,
                    Y = input.Y,
                    Z = input.Z - 1f,
                    Orientation = input.Orientation,
                    Pitch = input.Pitch,
                    Vx = 0f,
                    Vy = 0f,
                    Vz = -5f,
                    GroundZ = -50001f,
                    GroundNx = 0f,
                    GroundNy = 0f,
                    GroundNz = 1f,
                    MoveFlags = (uint)MovementFlags.MOVEFLAG_FALLINGFAR,
                    FallTime = 50,
                };
            };

            _player.Position = new Position(100f, 200f, 123.89f);
            _player.MovementFlags = MovementFlags.MOVEFLAG_NONE;
            _controller.Reset(teleportDestZ: 123.89f);
            _sentPackets.Clear();

            _controller.Update(0.05f, 1000);

            // Ground snap guard clamps Z back to teleport target and clears falling flags
            Assert.Equal(123.89f, _player.Position.Z, 2);
            // After clamping, FALLINGFAR is cleared and ground snap completes
            Assert.False((_player.MovementFlags & MovementFlags.MOVEFLAG_FALLINGFAR) != 0);
            Assert.False(_controller.NeedsGroundSnap);
        }

        [Fact]
        public void Update_IdleFreefallStillAppliesPhysics()
        {
            NativeLocalPhysics.TestStepOverride = input => new NativePhysics.PhysicsOutput
            {
                X = input.X,
                Y = input.Y,
                Z = input.Z - 1.5f,
                GroundZ = -50001f,
                GroundNx = 0,
                GroundNy = 0,
                GroundNz = 1,
                MoveFlags = (uint)MovementFlags.MOVEFLAG_FALLINGFAR,
                FallTime = 50,
            };

            _player.Position = new Position(100f, 200f, 50f);
            // Start with FALLINGFAR so the idle guard doesn't short-circuit.
            // This tests that physics applies when the player is already in freefall.
            _player.MovementFlags = MovementFlags.MOVEFLAG_FALLINGFAR;

            _controller.Update(0.05f, 1000);

            Assert.Equal(48.5f, _player.Position.Z);
            Assert.True((_player.MovementFlags & MovementFlags.MOVEFLAG_FALLINGFAR) != 0);
            Assert.Single(_sentPackets);
            Assert.Equal(Opcode.MSG_MOVE_HEARTBEAT, _sentPackets[0].opcode);
        }

        // ======== GROUND SNAPPING & PHYSICS POSITION ========

        [Fact]
        public void GroundSnap_PhysicsReturnsLowerZ_PlayerZUpdated()
        {
            // Physics engine finds ground at Z=45 when player is at Z=50
            NativeLocalPhysics.TestStepOverride = input => new NativePhysics.PhysicsOutput
            {
                X = input.X + MathF.Cos(input.Orientation) * input.RunSpeed * input.DeltaTime,
                Y = input.Y + MathF.Sin(input.Orientation) * input.RunSpeed * input.DeltaTime,
                Z = 45f, // Ground is below starting Z
                GroundZ = 45f,
                GroundNx = 0, GroundNy = 0, GroundNz = 1,
                MoveFlags = input.MoveFlags,
                FallTime = 0,
            };

            _player.Position = new Position(100f, 200f, 50f);
            _player.MovementFlags = MovementFlags.MOVEFLAG_FORWARD;

            _controller.Update(0.05f, 1000);

            Assert.Equal(45f, _player.Position.Z);
        }

        [Fact]
        public void GroundSnap_PhysicsReturnsHigherZ_PlayerZUpdated()
        {
            // Physics engine finds ground at Z=55 (uphill)
            NativeLocalPhysics.TestStepOverride = input => new NativePhysics.PhysicsOutput
            {
                X = input.X,
                Y = input.Y,
                Z = 55f, // Ground is above starting Z (step-up)
                GroundZ = 55f,
                GroundNx = 0, GroundNy = 0, GroundNz = 1,
                MoveFlags = input.MoveFlags,
                FallTime = 0,
            };

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
            NativeLocalPhysics.TestStepOverride = input =>
            {
                callCount++;
                float step = input.RunSpeed * input.DeltaTime;
                return new NativePhysics.PhysicsOutput
                {
                    X = input.X + MathF.Cos(input.Orientation) * step,
                    Y = input.Y + MathF.Sin(input.Orientation) * step,
                    Z = input.Z,
                    GroundZ = input.Z,
                    GroundNx = 0, GroundNy = 0, GroundNz = 1,
                    MoveFlags = input.MoveFlags,
                    FallTime = 0,
                };
            };

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
            // First tick: physics returns ground state.
            // Ground is within MaxGroundZDropPerFrame (5y) of initial player Z (50)
            // to avoid triggering the slope guard.
            NativePhysics.PhysicsInput? secondInput = null;
            int callCount = 0;
            NativeLocalPhysics.TestStepOverride = input =>
            {
                callCount++;
                if (callCount == 2) secondInput = input;
                return new NativePhysics.PhysicsOutput
                {
                    X = input.X,
                    Y = input.Y,
                    Z = 47f,
                    GroundZ = 46.5f,
                    GroundNx = 0.1f, GroundNy = 0.0f, GroundNz = 0.995f,
                    PendingDepenX = 0.01f, PendingDepenY = 0.02f, PendingDepenZ = 0.03f,
                    StandingOnInstanceId = 42,
                    StandingOnLocalX = 1f, StandingOnLocalY = 2f, StandingOnLocalZ = 3f,
                    MoveFlags = input.MoveFlags,
                    FallTime = 0,
                };
            };

            _player.MovementFlags = MovementFlags.MOVEFLAG_FORWARD;

            // Tick 1: returns ground state
            _controller.Update(0.05f, 1000);
            // Tick 2: should feed back the continuity state
            _controller.Update(0.05f, 1050);

            Assert.NotNull(secondInput);
            Assert.Equal(46.5f, secondInput!.Value.PrevGroundZ);
            Assert.Equal(0.1f, secondInput.Value.PrevGroundNx, 3);
            Assert.Equal(0.995f, secondInput.Value.PrevGroundNz, 3);
            Assert.Equal(0.01f, secondInput.Value.PendingDepenX, 3);
            Assert.Equal(42u, secondInput.Value.StandingOnInstanceId);
            Assert.Equal(1f, secondInput.Value.StandingOnLocalX);
        }

        [Fact]
        public void PhysicsPosition_OverridesDeadReckoning()
        {
            // Physics returns actual moved position → dead reckoning should NOT apply
            NativeLocalPhysics.TestStepOverride = input => new NativePhysics.PhysicsOutput
            {
                X = input.X + 0.5f, // Physics moved us
                Y = input.Y + 0.5f,
                Z = 48f, // Ground snapped Z
                GroundZ = 48f,
                GroundNx = 0, GroundNy = 0, GroundNz = 1,
                MoveFlags = input.MoveFlags,
                FallTime = 0,
            };

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
            // Start the player with FALLINGFAR already set, so wasGrounded=false
            // on tick 1. This avoids false freefall prevention (which requires
            // wasGrounded=true → nowFalling=true transition with a path).
            // Set GroundZ close to initial Z (50) to avoid triggering the slope guard
            // (MaxGroundZDropPerFrame = 5y threshold).
            NativePhysics.PhysicsInput? secondInput = null;
            int callCount = 0;
            NativeLocalPhysics.TestStepOverride = input =>
            {
                callCount++;
                if (callCount == 2) secondInput = input;
                return new NativePhysics.PhysicsOutput
                {
                    X = input.X,
                    Y = input.Y,
                    Z = input.Z - 0.5f, // Falling
                    Vx = 0, Vy = 0, Vz = -5.0f, // Falling velocity
                    GroundZ = 49.5f, // Ground nearby but not under feet
                    GroundNz = 1,
                    MoveFlags = (uint)(MovementFlags.MOVEFLAG_FORWARD | MovementFlags.MOVEFLAG_FALLINGFAR),
                    FallTime = callCount == 1 ? 100u : 200u,
                };
            };

            _player.MovementFlags = MovementFlags.MOVEFLAG_FORWARD | MovementFlags.MOVEFLAG_FALLINGFAR;

            _controller.Update(0.05f, 1000);
            _controller.Update(0.05f, 1050);

            Assert.NotNull(secondInput);
            Assert.Equal(-5.0f, secondInput!.Value.Vz, 1);
            Assert.Equal(100u, secondInput.Value.FallTime);
        }

        [Fact]
        public void PendingKnockback_OverridesDirectionalInputAndFeedsPhysicsVelocity()
        {
            ClearPendingKnockback();

            NativePhysics.PhysicsInput? capturedInput = null;
            NativeLocalPhysics.TestStepOverride = input =>
            {
                capturedInput = input;
                return new NativePhysics.PhysicsOutput
                {
                    X = input.X,
                    Y = input.Y,
                    Z = input.Z,
                    Vx = input.Vx,
                    Vy = input.Vy,
                    Vz = input.Vz,
                    GroundNz = 1f,
                    MoveFlags = (uint)MovementFlags.MOVEFLAG_FALLINGFAR,
                    FallTime = 50,
                };
            };

            _player.MovementFlags = MovementFlags.MOVEFLAG_FORWARD | MovementFlags.MOVEFLAG_STRAFE_LEFT;
            SetPendingKnockback(4f, -2f, 6f);

            _controller.Update(0.05f, 1000);

            Assert.NotNull(capturedInput);
            Assert.Equal(4f, capturedInput!.Value.Vx, 3);
            Assert.Equal(-2f, capturedInput.Value.Vy, 3);
            Assert.Equal(6f, capturedInput.Value.Vz, 3);
            Assert.Equal((uint)MovementFlags.MOVEFLAG_FALLINGFAR, capturedInput.Value.MoveFlags);
            Assert.True(_player.MovementFlags.HasFlag(MovementFlags.MOVEFLAG_FALLINGFAR));
            Assert.False(_player.MovementFlags.HasFlag(MovementFlags.MOVEFLAG_FORWARD));
            Assert.False(_player.MovementFlags.HasFlag(MovementFlags.MOVEFLAG_STRAFE_LEFT));

            Assert.False(WoWSharpObjectManager.Instance.TryConsumePendingKnockback(out _, out _, out _));
        }

        [Fact]
        public void SetTargetWaypoint_StoresSingleSteeringTarget()
        {
            _controller.SetTargetWaypoint(new Position(130f, 230f, 70f));
            var target = GetSteeringTarget(_controller);

            Assert.NotNull(target);
            Assert.Equal(130f, target!.X);
            Assert.Equal(230f, target.Y);
            Assert.Equal(70f, target.Z);
        }

        [Fact]
        public void SetTargetWaypoint_ReplacesExistingSteeringTarget()
        {
            _controller.SetTargetWaypoint(new Position(130f, 230f, 70f));
            _controller.SetTargetWaypoint(new Position(140f, 240f, 72f));

            var target = GetSteeringTarget(_controller);
            Assert.NotNull(target);
            Assert.Equal(140f, target!.X);
            Assert.Equal(240f, target.Y);
            Assert.Equal(72f, target.Z);
        }

        [Fact]
        public void ObserveStaleForwardAndRecover_Level2_SignalsCallerWithoutMutatingMovementOrWaypoint()
        {
            _player.MovementFlags = MovementFlags.MOVEFLAG_FORWARD;
            var steeringTarget = new Position(100f, 200f, 50f);
            SetPrivateField(_controller, "_steeringTarget", steeringTarget);
            SetPrivateField(_controller, "_consecutiveStuckLevels", 1);
            SetPrivateField(_controller, "_staleForwardNoDisplacementTicks", 14);
            SetPrivateField(_controller, "_staleForwardSuppressUntilMs", 0u);

            int? signaledLevel = null;
            Position? signaledPosition = null;
            _controller.OnStuckRecoveryRequired += (level, position) =>
            {
                signaledLevel = level;
                signaledPosition = position;
            };

            var method = typeof(MovementController).GetMethod(
                "ObserveStaleForwardAndRecover",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.NotNull(method);
            method!.Invoke(_controller, [0f, 5000u]);

            var currentWaypoint = _controller.CurrentWaypoint;
            Assert.NotNull(currentWaypoint);
            Assert.Equal(steeringTarget.X, currentWaypoint!.X);
            Assert.Equal(steeringTarget.Y, currentWaypoint.Y);
            Assert.Equal(steeringTarget.Z, currentWaypoint.Z);
            Assert.Equal(MovementFlags.MOVEFLAG_FORWARD, _player.MovementFlags);
            Assert.Equal(2, signaledLevel);
            Assert.NotNull(signaledPosition);
        }

        [Fact]
        public void ObserveStaleForwardAndRecover_Level3_SignalsCallerWithoutMutatingMovementOrWaypoint()
        {
            _player.MovementFlags = MovementFlags.MOVEFLAG_FORWARD;
            var steeringTarget = new Position(20.0f, 0f, 0f);
            SetPrivateField(_controller, "_steeringTarget", steeringTarget);
            SetPrivateField(_controller, "_consecutiveStuckLevels", 2);
            SetPrivateField(_controller, "_staleForwardNoDisplacementTicks", 14);
            SetPrivateField(_controller, "_staleForwardSuppressUntilMs", 0u);
            SetPrivateField(_controller, "_lastKnownGoodPosition", new Position(7f, 8f, 9f));

            int? signaledLevel = null;
            Position? signaledPosition = null;
            _controller.OnStuckRecoveryRequired += (level, position) =>
            {
                signaledLevel = level;
                signaledPosition = position;
            };

            var method = typeof(MovementController).GetMethod(
                "ObserveStaleForwardAndRecover",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.NotNull(method);
            method!.Invoke(_controller, [0f, 5000u]);

            var currentWaypoint = _controller.CurrentWaypoint;
            Assert.NotNull(currentWaypoint);
            Assert.Equal(steeringTarget.X, currentWaypoint!.X);
            Assert.Equal(steeringTarget.Y, currentWaypoint.Y);
            Assert.Equal(steeringTarget.Z, currentWaypoint.Z);
            Assert.Equal(MovementFlags.MOVEFLAG_FORWARD, _player.MovementFlags);
            Assert.Equal(3, signaledLevel);
            Assert.NotNull(signaledPosition);
            Assert.Equal(7f, signaledPosition!.X, 3);
            Assert.Equal(8f, signaledPosition.Y, 3);
            Assert.Equal(9f, signaledPosition.Z, 3);
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

            _controller.Update(0.05f, 0);
            Assert.Single(_sentPackets);

            _controller.Update(0.05f, 250);
            Assert.Single(_sentPackets);

            _controller.Update(0.05f, 500);
            Assert.Equal(2, _sentPackets.Count);
            Assert.Equal(Opcode.MSG_MOVE_HEARTBEAT, _sentPackets[1].opcode);

            _controller.Update(0.05f, 750);
            Assert.Equal(2, _sentPackets.Count);

            _controller.Update(0.05f, 1000);
            Assert.Equal(3, _sentPackets.Count);
            Assert.Equal(Opcode.MSG_MOVE_HEARTBEAT, _sentPackets[2].opcode);
        }

        private static Position? GetSteeringTarget(MovementController controller)
        {
            var field = typeof(MovementController).GetField("_steeringTarget", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.NotNull(field);
            return field!.GetValue(controller) as Position;
        }

        private static void SetPendingKnockback(float vx, float vy, float vz)
        {
            var objectManager = WoWSharpObjectManager.Instance;
            SetPrivateField(objectManager, "_pendingKnockbackVelX", vx);
            SetPrivateField(objectManager, "_pendingKnockbackVelY", vy);
            SetPrivateField(objectManager, "_pendingKnockbackVelZ", vz);
            SetPrivateField(objectManager, "_hasPendingKnockback", true);
        }

        private static void ClearPendingKnockback()
        {
            var objectManager = WoWSharpObjectManager.Instance;
            SetPrivateField(objectManager, "_pendingKnockbackVelX", 0f);
            SetPrivateField(objectManager, "_pendingKnockbackVelY", 0f);
            SetPrivateField(objectManager, "_pendingKnockbackVelZ", 0f);
            SetPrivateField(objectManager, "_hasPendingKnockback", false);
        }

        private static void SetPrivateField(object target, string fieldName, object value)
        {
            var type = target.GetType();
            FieldInfo? field = null;
            while (type != null && field == null)
            {
                field = type.GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
                type = type.BaseType;
            }

            Assert.NotNull(field);
            field!.SetValue(target, value);
        }

        private static T GetPrivateField<T>(object target, string fieldName)
        {
            var type = target.GetType();
            FieldInfo? field = null;
            while (type != null && field == null)
            {
                field = type.GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
                type = type.BaseType;
            }

            Assert.NotNull(field);
            return (T)field!.GetValue(target)!;
        }

        private static IReadOnlyList<WoWObject> ReplaceTrackedObjects(params WoWObject[] objects)
        {
            // P9.2: _objects and _objectsLock are now instance fields (not static)
            var objectsField = typeof(WoWSharpObjectManager).GetField("_objects", BindingFlags.Instance | BindingFlags.NonPublic);
            var objectsLockField = typeof(WoWSharpObjectManager).GetField("_objectsLock", BindingFlags.Instance | BindingFlags.NonPublic);

            Assert.NotNull(objectsField);
            Assert.NotNull(objectsLockField);

#pragma warning disable CS0618 // Instance still used via legacy singleton in tests
            var instance = WoWSharpObjectManager.Instance;
#pragma warning restore CS0618
            var trackedObjects = (List<WoWObject>)objectsField!.GetValue(instance)!;
            var syncRoot = objectsLockField!.GetValue(instance)!;

            lock (syncRoot)
            {
                var snapshot = trackedObjects.ToArray();
                trackedObjects.Clear();
                trackedObjects.AddRange(objects);
                return snapshot;
            }
        }

        private static void RestoreTrackedObjects(IReadOnlyList<WoWObject> snapshot)
        {
            var objectsField = typeof(WoWSharpObjectManager).GetField("_objects", BindingFlags.Instance | BindingFlags.NonPublic);
            var objectsLockField = typeof(WoWSharpObjectManager).GetField("_objectsLock", BindingFlags.Instance | BindingFlags.NonPublic);

            Assert.NotNull(objectsField);
            Assert.NotNull(objectsLockField);

#pragma warning disable CS0618
            var instance = WoWSharpObjectManager.Instance;
#pragma warning restore CS0618
            var trackedObjects = (List<WoWObject>)objectsField!.GetValue(instance)!;
            var syncRoot = objectsLockField!.GetValue(instance)!;

            lock (syncRoot)
            {
                trackedObjects.Clear();
                trackedObjects.AddRange(snapshot);
            }
        }
    }
}
