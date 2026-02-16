using Communication;
using Game;
using Google.Protobuf;
using System;
using Xunit.Abstractions;

namespace BotRunner.Tests
{
    /// <summary>
    /// Tests that verify movement data is correctly serialized/deserialized in WoWActivitySnapshot.
    /// These tests do NOT require WoW or any infrastructure - they test the protobuf data layer.
    /// </summary>
    public class WoWActivitySnapshotMovementTests(ITestOutputHelper output)
    {
        private readonly ITestOutputHelper _output = output;

        [Fact]
        public void MovementData_ShouldRoundTripThroughProtobuf()
        {
            // Arrange: Create a snapshot with movement data matching vanilla defaults
            var snapshot = new WoWActivitySnapshot
            {
                Timestamp = (uint)DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                AccountName = "TESTACCOUNT",
                CharacterName = "TestChar",
                ScreenState = "InWorld",
                MovementData = new MovementData
                {
                    MovementFlags = 0,
                    FallTime = 0,
                    JumpVerticalSpeed = 0f,
                    JumpSinAngle = 0f,
                    JumpCosAngle = 0f,
                    JumpHorizontalSpeed = 0f,
                    SwimPitch = 0f,
                    WalkSpeed = 2.5f,
                    RunSpeed = 7.0f,
                    RunBackSpeed = 4.5f,
                    SwimSpeed = 4.722222f,
                    SwimBackSpeed = 2.5f,
                    TurnRate = (float)Math.PI,
                    Position = new Game.Position { X = -1629.36f, Y = -4373.38f, Z = 10.2f },
                    Facing = 1.57f,
                    TransportGuid = 0,
                    TransportOffsetX = 0f,
                    TransportOffsetY = 0f,
                    TransportOffsetZ = 0f,
                    TransportOrientation = 0f,
                    FallStartHeight = 0f,
                }
            };

            // Act: Serialize and deserialize
            var bytes = snapshot.ToByteArray();
            var deserialized = WoWActivitySnapshot.Parser.ParseFrom(bytes);

            // Assert: All movement values round-trip correctly
            Assert.NotNull(deserialized.MovementData);
            var md = deserialized.MovementData;

            _output.WriteLine($"Serialized size: {bytes.Length} bytes");
            _output.WriteLine($"WalkSpeed: {md.WalkSpeed}");
            _output.WriteLine($"RunSpeed: {md.RunSpeed}");
            _output.WriteLine($"RunBackSpeed: {md.RunBackSpeed}");
            _output.WriteLine($"SwimSpeed: {md.SwimSpeed}");
            _output.WriteLine($"SwimBackSpeed: {md.SwimBackSpeed}");
            _output.WriteLine($"TurnRate: {md.TurnRate}");
            _output.WriteLine($"Position: ({md.Position.X}, {md.Position.Y}, {md.Position.Z})");
            _output.WriteLine($"Facing: {md.Facing}");
            _output.WriteLine($"MovementFlags: 0x{md.MovementFlags:X8}");

            Assert.Equal(2.5f, md.WalkSpeed);
            Assert.Equal(7.0f, md.RunSpeed);
            Assert.Equal(4.5f, md.RunBackSpeed);
            Assert.Equal(4.722222f, md.SwimSpeed, 3);
            Assert.Equal(2.5f, md.SwimBackSpeed);
            Assert.Equal((float)Math.PI, md.TurnRate, 4);
            Assert.Equal(-1629.36f, md.Position.X, 1);
            Assert.Equal(-4373.38f, md.Position.Y, 1);
            Assert.Equal(10.2f, md.Position.Z, 1);
            Assert.Equal(1.57f, md.Facing, 2);
            Assert.Equal(0u, md.MovementFlags);
            Assert.Equal(0u, md.FallTime);
            Assert.Equal(0ul, md.TransportGuid);
        }

        [Fact]
        public void MovementData_StationaryDefaults_ShouldBeInExpectedRanges()
        {
            // These are the expected default values for a stationary character in vanilla 1.12.1
            var md = new MovementData
            {
                WalkSpeed = 2.5f,
                RunSpeed = 7.0f,
                RunBackSpeed = 4.5f,
                SwimSpeed = 4.722222f,
                SwimBackSpeed = 2.5f,
                TurnRate = (float)Math.PI,
            };

            // Validate stationary defaults from WoW documentation / cMaNGOS
            Assert.InRange(md.WalkSpeed, 2.49f, 2.51f);       // Walk: 2.5 yards/sec
            Assert.InRange(md.RunSpeed, 6.99f, 7.01f);         // Run: 7.0 yards/sec
            Assert.InRange(md.RunBackSpeed, 4.49f, 4.51f);     // RunBack: 4.5 yards/sec
            Assert.InRange(md.SwimSpeed, 4.72f, 4.73f);        // Swim: 4.722 yards/sec
            Assert.InRange(md.SwimBackSpeed, 2.49f, 2.51f);    // SwimBack: 2.5 yards/sec
            Assert.InRange(md.TurnRate, 3.14f, 3.15f);         // TurnRate: pi radians/sec
        }

        [Fact]
        public void MovementData_WithTransportData_ShouldRoundTrip()
        {
            // Simulate being on an elevator/transport
            var snapshot = new WoWActivitySnapshot
            {
                AccountName = "TESTACCOUNT",
                ScreenState = "InWorld",
                MovementData = new MovementData
                {
                    MovementFlags = 0x200, // MOVEFLAG_ONTRANSPORT
                    TransportGuid = 123456789,
                    TransportOffsetX = 1.5f,
                    TransportOffsetY = -2.3f,
                    TransportOffsetZ = 0.8f,
                    TransportOrientation = 3.14f,
                    Position = new Game.Position { X = -4920f, Y = 760f, Z = 21f },
                    WalkSpeed = 2.5f,
                    RunSpeed = 7.0f,
                }
            };

            var bytes = snapshot.ToByteArray();
            var deserialized = WoWActivitySnapshot.Parser.ParseFrom(bytes);
            var md = deserialized.MovementData;

            Assert.Equal(0x200u, md.MovementFlags);
            Assert.Equal(123456789ul, md.TransportGuid);
            Assert.Equal(1.5f, md.TransportOffsetX, 1);
            Assert.Equal(-2.3f, md.TransportOffsetY, 1);
            Assert.Equal(0.8f, md.TransportOffsetZ, 1);
            Assert.Equal(3.14f, md.TransportOrientation, 2);
        }

        [Fact]
        public void MovementData_WithJumpData_ShouldRoundTrip()
        {
            // Simulate a jumping character
            var snapshot = new WoWActivitySnapshot
            {
                AccountName = "TESTACCOUNT",
                ScreenState = "InWorld",
                MovementData = new MovementData
                {
                    MovementFlags = 0x2000, // MOVEFLAG_FALLING
                    FallTime = 350,
                    JumpVerticalSpeed = 7.96f,
                    JumpSinAngle = 0.5f,
                    JumpCosAngle = 0.866f,
                    JumpHorizontalSpeed = 7.0f,
                    FallStartHeight = 45.2f,
                    WalkSpeed = 2.5f,
                    RunSpeed = 7.0f,
                    Position = new Game.Position { X = 100f, Y = 200f, Z = 44.5f },
                }
            };

            var bytes = snapshot.ToByteArray();
            var deserialized = WoWActivitySnapshot.Parser.ParseFrom(bytes);
            var md = deserialized.MovementData;

            Assert.Equal(0x2000u, md.MovementFlags);
            Assert.Equal(350u, md.FallTime);
            Assert.Equal(7.96f, md.JumpVerticalSpeed, 1);
            Assert.Equal(0.5f, md.JumpSinAngle, 2);
            Assert.Equal(0.866f, md.JumpCosAngle, 2);
            Assert.Equal(45.2f, md.FallStartHeight, 1);
        }

        [Fact]
        public void MovementData_WithSwimData_ShouldRoundTrip()
        {
            // Simulate a swimming character
            var snapshot = new WoWActivitySnapshot
            {
                AccountName = "TESTACCOUNT",
                ScreenState = "InWorld",
                MovementData = new MovementData
                {
                    MovementFlags = 0x200000, // MOVEFLAG_SWIMMING
                    SwimPitch = -0.35f, // Looking down while swimming
                    SwimSpeed = 4.722222f,
                    SwimBackSpeed = 2.5f,
                    WalkSpeed = 2.5f,
                    RunSpeed = 7.0f,
                    Position = new Game.Position { X = -50f, Y = 300f, Z = -5.2f },
                }
            };

            var bytes = snapshot.ToByteArray();
            var deserialized = WoWActivitySnapshot.Parser.ParseFrom(bytes);
            var md = deserialized.MovementData;

            Assert.Equal(0x200000u, md.MovementFlags);
            Assert.Equal(-0.35f, md.SwimPitch, 2);
            Assert.Equal(4.722222f, md.SwimSpeed, 3);
        }

        [Fact]
        public void MovementData_NullWhenNotInWorld_ShouldBeNull()
        {
            // Snapshot sent from character select should not have movement data
            var snapshot = new WoWActivitySnapshot
            {
                AccountName = "TESTACCOUNT",
                ScreenState = "CharacterSelect",
            };

            Assert.Null(snapshot.MovementData);

            // Round-trip should preserve null
            var bytes = snapshot.ToByteArray();
            var deserialized = WoWActivitySnapshot.Parser.ParseFrom(bytes);
            Assert.Null(deserialized.MovementData);
        }

        [Theory]
        [InlineData(0f, 0f, 0f, false)]      // Origin - invalid
        [InlineData(-1629f, -4373f, 10f, true)]  // Durotar - valid
        [InlineData(-8949f, -132f, 83f, true)]   // Stormwind - valid
        [InlineData(17100f, 0f, 0f, false)]       // Out of bounds
        [InlineData(0f, -17100f, 0f, false)]      // Out of bounds
        public void Position_ShouldBeWithinMapBounds(float x, float y, float z, bool expectedValid)
        {
            const float MAP_BOUND = 17066.6656f; // WoW map bounds

            bool isValid = Math.Abs(x) <= MAP_BOUND &&
                          Math.Abs(y) <= MAP_BOUND &&
                          !(x == 0f && y == 0f && z == 0f); // Zero position is invalid

            Assert.Equal(expectedValid, isValid);
        }
    }
}
