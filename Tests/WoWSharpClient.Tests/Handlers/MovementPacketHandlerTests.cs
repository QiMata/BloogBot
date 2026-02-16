using GameData.Core.Enums;
using GameData.Core.Models;
using System.IO;
using WoWSharpClient.Models;
using WoWSharpClient.Parsers;
using Xunit;

namespace WoWSharpClient.Tests.Handlers
{
    /// <summary>
    /// Round-trip tests: Build movement packets with known values → Parse back → Assert all fields identical.
    /// Covers all flag-dependent paths: standing, running, swimming, jumping/falling, transport, spline elevation.
    /// </summary>
    public class MovementPacketHandlerTests
    {
        private static WoWLocalPlayer CreatePlayer(
            float x, float y, float z, float facing,
            MovementFlags flags = MovementFlags.MOVEFLAG_NONE)
        {
            var player = new WoWLocalPlayer(new HighGuid(42))
            {
                Position = new Position(x, y, z),
                Facing = facing,
                MovementFlags = flags
            };
            return player;
        }

        private static MovementInfoUpdate RoundTrip(WoWLocalPlayer player, uint clientTime, uint fallTime)
        {
            byte[] buffer = MovementPacketHandler.BuildMovementInfoBuffer(player, clientTime, fallTime);
            using var ms = new MemoryStream(buffer);
            using var reader = new BinaryReader(ms);
            var result = MovementPacketHandler.ParseMovementInfo(reader);

            // Verify entire buffer was consumed (no trailing bytes, no short read)
            Assert.Equal(buffer.Length, ms.Position);
            return result;
        }

        [Fact]
        public void StandingStill_RoundTrips()
        {
            var player = CreatePlayer(-8949.95f, -132.493f, 83.5312f, 2.34f);
            uint clientTime = 100000;
            uint fallTime = 0;

            var parsed = RoundTrip(player, clientTime, fallTime);

            Assert.Equal(MovementFlags.MOVEFLAG_NONE, parsed.MovementFlags);
            Assert.Equal(clientTime, parsed.LastUpdated);
            Assert.Equal(-8949.95f, parsed.X);
            Assert.Equal(-132.493f, parsed.Y);
            Assert.Equal(83.5312f, parsed.Z);
            Assert.Equal(2.34f, parsed.Facing);
            Assert.Equal(fallTime, parsed.FallTime);

            // No optional blocks present
            Assert.Null(parsed.TransportGuid);
            Assert.Null(parsed.SwimPitch);
            Assert.Null(parsed.JumpVerticalSpeed);
            Assert.Null(parsed.SplineElevation);
        }

        [Fact]
        public void RunningForward_RoundTrips()
        {
            var player = CreatePlayer(1630.5f, -4420.3f, 17.85f, 1.57f,
                MovementFlags.MOVEFLAG_FORWARD);
            uint clientTime = 200000;
            uint fallTime = 0;

            var parsed = RoundTrip(player, clientTime, fallTime);

            Assert.Equal(MovementFlags.MOVEFLAG_FORWARD, parsed.MovementFlags);
            Assert.Equal(clientTime, parsed.LastUpdated);
            Assert.Equal(1630.5f, parsed.X);
            Assert.Equal(-4420.3f, parsed.Y);
            Assert.Equal(17.85f, parsed.Z);
            Assert.Equal(1.57f, parsed.Facing);
            Assert.Equal(fallTime, parsed.FallTime);
        }

        [Fact]
        public void RunningBackward_RoundTrips()
        {
            var player = CreatePlayer(100f, 200f, 50f, 3.14f,
                MovementFlags.MOVEFLAG_BACKWARD);
            uint clientTime = 300000;
            uint fallTime = 0;

            var parsed = RoundTrip(player, clientTime, fallTime);

            Assert.Equal(MovementFlags.MOVEFLAG_BACKWARD, parsed.MovementFlags);
            Assert.Equal(100f, parsed.X);
            Assert.Equal(200f, parsed.Y);
            Assert.Equal(50f, parsed.Z);
            Assert.Equal(3.14f, parsed.Facing);
        }

        [Fact]
        public void Swimming_WithPitch_RoundTrips()
        {
            var player = CreatePlayer(1810f, -4420f, -12f, 0.5f,
                MovementFlags.MOVEFLAG_FORWARD | MovementFlags.MOVEFLAG_SWIMMING);
            player.SwimPitch = -0.785f;
            uint clientTime = 400000;
            uint fallTime = 0;

            var parsed = RoundTrip(player, clientTime, fallTime);

            Assert.Equal(
                MovementFlags.MOVEFLAG_FORWARD | MovementFlags.MOVEFLAG_SWIMMING,
                parsed.MovementFlags);
            Assert.True(parsed.IsSwimming);
            Assert.Equal(-0.785f, parsed.SwimPitch);
            Assert.Equal(1810f, parsed.X);
            Assert.Equal(-4420f, parsed.Y);
            Assert.Equal(-12f, parsed.Z);
        }

        [Fact]
        public void Jumping_WithFallBlock_RoundTrips()
        {
            var player = CreatePlayer(500f, 600f, 100f, 1.0f,
                MovementFlags.MOVEFLAG_FORWARD | MovementFlags.MOVEFLAG_JUMPING);
            player.JumpVerticalSpeed = 7.956f;
            player.JumpCosAngle = 0.707f;
            player.JumpSinAngle = 0.707f;
            player.JumpHorizontalSpeed = 7.0f;
            uint clientTime = 500000;
            uint fallTime = 350;

            var parsed = RoundTrip(player, clientTime, fallTime);

            Assert.Equal(
                MovementFlags.MOVEFLAG_FORWARD | MovementFlags.MOVEFLAG_JUMPING,
                parsed.MovementFlags);
            Assert.True(parsed.IsFalling);
            Assert.Equal(fallTime, parsed.FallTime);
            Assert.Equal(7.956f, parsed.JumpVerticalSpeed);
            Assert.Equal(0.707f, parsed.JumpCosAngle);
            Assert.Equal(0.707f, parsed.JumpSinAngle);
            Assert.Equal(7.0f, parsed.JumpHorizontalSpeed);
        }

        [Fact]
        public void Transport_RoundTrips()
        {
            var player = CreatePlayer(10.5f, -3.2f, 8.1f, 0.0f,
                MovementFlags.MOVEFLAG_ONTRANSPORT);

            // Set up transport object with GUID and position
            var transport = new WoWGameObject(new HighGuid(0x0000F0011234ABCDul))
            {
                Position = new Position(1568.3f, -4395.1f, 17.2f),
                Facing = 3.14f
            };
            player.Transport = transport;

            uint clientTime = 600000;
            uint fallTime = 0;

            var parsed = RoundTrip(player, clientTime, fallTime);

            Assert.Equal(MovementFlags.MOVEFLAG_ONTRANSPORT, parsed.MovementFlags);
            Assert.True(parsed.HasTransport);
            Assert.Equal(0x0000F0011234ABCDul, parsed.TransportGuid);
            Assert.NotNull(parsed.TransportOffset);
            Assert.Equal(1568.3f, parsed.TransportOffset.X);
            Assert.Equal(-4395.1f, parsed.TransportOffset.Y);
            Assert.Equal(17.2f, parsed.TransportOffset.Z);
            Assert.Equal(3.14f, parsed.TransportOrientation);

            // Player local coords
            Assert.Equal(10.5f, parsed.X);
            Assert.Equal(-3.2f, parsed.Y);
            Assert.Equal(8.1f, parsed.Z);
        }

        [Fact]
        public void SplineElevation_RoundTrips()
        {
            var player = CreatePlayer(0f, 0f, 100f, 0f,
                MovementFlags.MOVEFLAG_SPLINE_ELEVATION);
            player.SplineElevation = 42.5f;
            uint clientTime = 700000;
            uint fallTime = 0;

            var parsed = RoundTrip(player, clientTime, fallTime);

            Assert.Equal(MovementFlags.MOVEFLAG_SPLINE_ELEVATION, parsed.MovementFlags);
            Assert.True(parsed.HasSplineElevation);
            Assert.Equal(42.5f, parsed.SplineElevation);
        }

        [Fact]
        public void AllOptionalFields_Combined_RoundTrips()
        {
            // Swimming + jumping + transport + spline elevation all at once
            var flags = MovementFlags.MOVEFLAG_FORWARD
                      | MovementFlags.MOVEFLAG_SWIMMING
                      | MovementFlags.MOVEFLAG_JUMPING
                      | MovementFlags.MOVEFLAG_ONTRANSPORT
                      | MovementFlags.MOVEFLAG_SPLINE_ELEVATION;

            var player = CreatePlayer(5.0f, -2.0f, 3.0f, 1.5f, flags);
            player.SwimPitch = 0.45f;
            player.JumpVerticalSpeed = 5.0f;
            player.JumpCosAngle = 0.866f;
            player.JumpSinAngle = 0.5f;
            player.JumpHorizontalSpeed = 4.0f;
            player.SplineElevation = 10.0f;

            var transport = new WoWGameObject(new HighGuid(999ul))
            {
                Position = new Position(100f, 200f, 300f),
                Facing = 2.0f
            };
            player.Transport = transport;

            uint clientTime = 999999;
            uint fallTime = 1200;

            var parsed = RoundTrip(player, clientTime, fallTime);

            // Core fields
            Assert.Equal(flags, parsed.MovementFlags);
            Assert.Equal(clientTime, parsed.LastUpdated);
            Assert.Equal(5.0f, parsed.X);
            Assert.Equal(-2.0f, parsed.Y);
            Assert.Equal(3.0f, parsed.Z);
            Assert.Equal(1.5f, parsed.Facing);

            // Transport
            Assert.Equal(999ul, parsed.TransportGuid);
            Assert.Equal(100f, parsed.TransportOffset.X);
            Assert.Equal(200f, parsed.TransportOffset.Y);
            Assert.Equal(300f, parsed.TransportOffset.Z);
            Assert.Equal(2.0f, parsed.TransportOrientation);

            // Swim pitch
            Assert.Equal(0.45f, parsed.SwimPitch);

            // Fall time + jump block
            Assert.Equal(fallTime, parsed.FallTime);
            Assert.Equal(5.0f, parsed.JumpVerticalSpeed);
            Assert.Equal(0.866f, parsed.JumpCosAngle);
            Assert.Equal(0.5f, parsed.JumpSinAngle);
            Assert.Equal(4.0f, parsed.JumpHorizontalSpeed);

            // Spline elevation
            Assert.Equal(10.0f, parsed.SplineElevation);
        }

        [Fact]
        public void FallTimeAlwaysPresent_EvenWithNoFlags()
        {
            var player = CreatePlayer(0, 0, 0, 0);
            uint clientTime = 100;
            uint fallTime = 12345;

            var parsed = RoundTrip(player, clientTime, fallTime);

            Assert.Equal(12345u, parsed.FallTime);
        }

        [Fact]
        public void LegacyTwoArgOverload_UsesFallTimeFromPlayer()
        {
            var player = CreatePlayer(1f, 2f, 3f, 0f);
            player.FallTime = 777;
            uint clientTime = 100;

            byte[] buffer = MovementPacketHandler.BuildMovementInfoBuffer(player, clientTime);
            using var ms = new MemoryStream(buffer);
            using var reader = new BinaryReader(ms);
            var parsed = MovementPacketHandler.ParseMovementInfo(reader);

            Assert.Equal(777u, parsed.FallTime);
        }

        [Fact]
        public void BufferSize_StandingStill_Is28Bytes()
        {
            // Flags(4) + Time(4) + X(4) + Y(4) + Z(4) + Facing(4) + FallTime(4) = 28
            var player = CreatePlayer(0, 0, 0, 0);
            byte[] buffer = MovementPacketHandler.BuildMovementInfoBuffer(player, 0, 0);
            Assert.Equal(28, buffer.Length);
        }

        [Fact]
        public void BufferSize_Swimming_Includes4BytePitch()
        {
            // 28 base + 4 swim pitch = 32
            var player = CreatePlayer(0, 0, 0, 0, MovementFlags.MOVEFLAG_SWIMMING);
            byte[] buffer = MovementPacketHandler.BuildMovementInfoBuffer(player, 0, 0);
            Assert.Equal(32, buffer.Length);
        }

        [Fact]
        public void BufferSize_Jumping_Includes16ByteJumpBlock()
        {
            // 28 base + 16 jump block (4 floats) = 44
            var player = CreatePlayer(0, 0, 0, 0, MovementFlags.MOVEFLAG_JUMPING);
            byte[] buffer = MovementPacketHandler.BuildMovementInfoBuffer(player, 0, 0);
            Assert.Equal(44, buffer.Length);
        }

        [Fact]
        public void BufferSize_Transport_Includes24ByteTransportBlock()
        {
            // 28 base + 24 transport (guid8 + xyz12 + facing4) = 52
            var player = CreatePlayer(0, 0, 0, 0, MovementFlags.MOVEFLAG_ONTRANSPORT);
            player.Transport = new WoWGameObject(new HighGuid(1ul))
            {
                Position = new Position(0, 0, 0)
            };
            byte[] buffer = MovementPacketHandler.BuildMovementInfoBuffer(player, 0, 0);
            Assert.Equal(52, buffer.Length);
        }

        [Fact]
        public void BufferSize_AllOptional_CorrectTotal()
        {
            // 28 base + 24 transport + 4 swim pitch + 16 jump + 4 spline elevation = 76
            var flags = MovementFlags.MOVEFLAG_ONTRANSPORT
                      | MovementFlags.MOVEFLAG_SWIMMING
                      | MovementFlags.MOVEFLAG_JUMPING
                      | MovementFlags.MOVEFLAG_SPLINE_ELEVATION;
            var player = CreatePlayer(0, 0, 0, 0, flags);
            player.Transport = new WoWGameObject(new HighGuid(1ul))
            {
                Position = new Position(0, 0, 0)
            };
            byte[] buffer = MovementPacketHandler.BuildMovementInfoBuffer(player, 0, 0);
            Assert.Equal(76, buffer.Length);
        }
    }
}
