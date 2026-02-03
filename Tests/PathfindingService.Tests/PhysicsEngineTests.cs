using GameData.Core.Constants;
using GameData.Core.Enums;
using PathfindingService.Repository; // Navigation wrapper
using MovementFlags = GameData.Core.Enums.MovementFlags;
using GameData.Core.Models;

namespace PathfindingService.Tests
{
    /// <summary>
    /// End‑to‑end tests for the consolidated Navigation API.
    ///   • CalculatePath
    ///   • IsLineOfSight
    ///   • GetTerrainProbe (capsule sweep + height + liquid)
    /// </summary>
    public class PhysicsFixture : IDisposable
    {
        public Physics Physics { get; }

        public PhysicsFixture() => Physics = new Physics();

        public void Dispose() { /* Navigation lives for the AppDomain – nothing to do. */ }
    }
    /// <summary>
    /// End-to-end tests for Navigation + PhysicsEngine.
    /// Uses InlineData for each idle-tick sample, preserving the NavigationFixture.
    /// </summary>
    public class PhysicsEngineTests(PhysicsFixture fixture) : IClassFixture<PhysicsFixture>
    {
        private readonly Physics _phy = fixture.Physics;
        private const float Dt = 0.05f; // one tick = 100 ms

        // Helper to compare PhysicsOutput with tolerance
        private static void AssertEqual(PhysicsOutput exp, PhysicsOutput act)
        {
            Assert.Equal(exp.x, act.x, 3);
            Assert.Equal(exp.y, act.y, 3);
            Assert.Equal(exp.z, act.z, 3);
            Assert.Equal(exp.vx, act.vx, 3);
            Assert.Equal(exp.vy, act.vy, 3);
            Assert.Equal(exp.vz, act.vz, 3);
            Assert.Equal((MovementFlags)exp.moveFlags, (MovementFlags)act.moveFlags);
            Assert.Equal((LiquidType)exp.liquidType, (LiquidType)act.liquidType);
            Assert.Equal(exp.liquidZ, act.liquidZ, 3);
        }

        [Theory]
        [InlineData(1, MovementFlags.MOVEFLAG_FORWARD, 1u, 1632.120f, -4372.715f, 30.786f, 0.5f, Gender.Male, Race.Orc, 1629.129f, -4374.037f, 30.674f, MovementFlags.MOVEFLAG_FORWARD, 0f, 0f, 0f, LiquidType.Water, 0f)] // Step down on top of Orgrimmar bank
        [InlineData(1, MovementFlags.MOVEFLAG_FORWARD, 1u, 1636.211f, -4375.267f, 28.748f, 0f, Gender.Female, Race.Orc, 1636.211f, -4375.267f, 28.748f, MovementFlags.MOVEFLAG_FORWARD, 0f, 0f, 0f, LiquidType.Water, 0f)] // Forward movement blocked by spike on Orgrimmar bank
        [InlineData(1, MovementFlags.MOVEFLAG_FORWARD, 1u, 1661.377f, -4369.652f, 24.740f, 0f, Gender.Female, Race.Orc, 1661.377f, -4369.652f, 24.740f, MovementFlags.MOVEFLAG_FORWARD, 0f, 0f, 0f, LiquidType.Water, 0f)] // Origmmar terrain block
        [InlineData(1, MovementFlags.MOVEFLAG_FORWARD, 1u, 1662.314f, -4371.963f, 24.925f, 0f, Gender.Female, Race.Orc, 1661.377f, -4369.652f, 24.740f, MovementFlags.MOVEFLAG_FORWARD, 0f, 0f, 0f, LiquidType.Water, 0f)] // Origmmar terrain step-up and slide down
        [InlineData(1, MovementFlags.MOVEFLAG_FORWARD, 1u, 1679.552f, -4372.284f, 27.385f, 6.151f, Gender.Female, Race.Orc, 1661.377f, -4369.652f, 24.740f, MovementFlags.MOVEFLAG_FORWARD, 0f, 0f, 0f, LiquidType.Water, 0f)] // Origmmar terrain step-up and slide down
        [InlineData(1, MovementFlags.MOVEFLAG_NONE, 0u, -6240.320f, 331.033f, 382.619f, 0f, Gender.Female, Race.Human, -6240.32f, 331.033f, 382.758f, MovementFlags.MOVEFLAG_NONE, 0f, 0f, 0f, LiquidType.None, -500f)]
        [InlineData(1, MovementFlags.MOVEFLAG_NONE, 0u, -8949.950f, -132.490f, 83.229f, 0f, Gender.Female, Race.Human, -8949.950f, -132.490f, 83.531f, MovementFlags.MOVEFLAG_NONE, 0f, 0f, 0f, LiquidType.None, -500f)]
        [InlineData(1, MovementFlags.MOVEFLAG_NONE, 0u, 524.311f, 312.037f, 31.260f, 0.002f, Gender.Female, Race.Orc, 524.311f, 312.037f, 31.26f, MovementFlags.MOVEFLAG_SWIMMING, 0f, 0f, 0f, LiquidType.Water, 32.934f)]
        [InlineData(1, MovementFlags.MOVEFLAG_NONE, 0u, 537.798f, 279.534f, 31.208f, 0f, Gender.Female, Race.Orc, 537.798f, 279.534f, 31.208f, MovementFlags.MOVEFLAG_SWIMMING, 0f, 0f, 0f, LiquidType.Water, 32.934f)]
        [InlineData(1, MovementFlags.MOVEFLAG_NONE, 0u, 538.0f, 279.0f, 31.237f, 0f, Gender.Female, Race.Orc, 538.0f, 279.0f, 31.237f, MovementFlags.MOVEFLAG_SWIMMING, 0f, 0f, 0f, LiquidType.Water, 32.934f)]
        [InlineData(1, MovementFlags.MOVEFLAG_NONE, 0u, 582.693f, 342.985f, 31.149f, 0f, Gender.Female, Race.Orc, 582.693f, 342.985f, 31.149f, MovementFlags.MOVEFLAG_SWIMMING, 0f, 0f, 0f, LiquidType.Water, 32.934f)]
        [InlineData(1, MovementFlags.MOVEFLAG_NONE, 0u, 623.246f, 349.184f, 31.149f, 0f, Gender.Female, Race.Orc, 623.246f, 349.184f, 31.149f, MovementFlags.MOVEFLAG_SWIMMING, 0f, 0f, 0f, LiquidType.Water, 32.934f)]
        [InlineData(1, MovementFlags.MOVEFLAG_NONE, 0u, 623.683f, 349.455f, 31.245f, 0f, Gender.Female, Race.Orc, 623.683f, 349.455f, 31.245f, MovementFlags.MOVEFLAG_SWIMMING, 0f, 0f, 0f, LiquidType.Water, 32.934f)]
        [InlineData(1, MovementFlags.MOVEFLAG_NONE, 1u, 10334.000f, 833.902f, 1326.110f, 0f, Gender.Female, Race.Orc, 10334.000f, 833.902f, 1326.107f, MovementFlags.MOVEFLAG_NONE, 0f, 0f, 0f, LiquidType.None, -500f)]
        [InlineData(1, MovementFlags.MOVEFLAG_NONE, 1u, 1629.359f, -4373.390f, 31.275f, 0f, Gender.Male, Race.Orc, 1629.359f, -4373.390f, 31.275f, MovementFlags.MOVEFLAG_NONE, 0f, 0f, 0f, LiquidType.None, -500f)] // Resting on top of Orgrimmar bank
        [InlineData(1, MovementFlags.MOVEFLAG_NONE, 1u, -2917.580f, -257.980f, 53.362f, 0f, Gender.Female, Race.Orc, -2917.580f, -257.980f, 53.362f, MovementFlags.MOVEFLAG_NONE, 0f, 0f, 0f, LiquidType.None, -500f)]
        [InlineData(1, MovementFlags.MOVEFLAG_NONE, 1u, -535.382f, -4204.233f, 76.994f, 0.126f, Gender.Male, Race.Orc, -535.382f, -4204.233f, 74.716f, MovementFlags.MOVEFLAG_NONE, 0f, 0f, 0f, LiquidType.None, -500f)]
        [InlineData(1, MovementFlags.MOVEFLAG_NONE, 1u, -535.382f, -4204.233f, 74.716f, 0f, Gender.Male, Race.Orc, -535.382f, -4204.233f, 74.716f, MovementFlags.MOVEFLAG_NONE, 0f, 0f, 0f, LiquidType.None, -500f)]
        [InlineData(1, MovementFlags.MOVEFLAG_NONE, 1u, -550.479f, -4194.069f, 49.271f, 0f, Gender.Female, Race.Orc, -550.479f, -4194.069f, 49.286f, MovementFlags.MOVEFLAG_NONE, 0f, 0f, 0f, LiquidType.None, -500f)]
        [InlineData(1, MovementFlags.MOVEFLAG_NONE, 1u, -557.773f, -4181.990f, 72.576f, 0f, Gender.Female, Race.Orc, -557.773f, -4181.990f, 72.499f, MovementFlags.MOVEFLAG_NONE, 0f, 0f, 0f, LiquidType.None, -500f)]
        [InlineData(1, MovementFlags.MOVEFLAG_NONE, 1u, -562.225f, -4189.092f, 70.789f, 0f, Gender.Female, Race.Orc, -562.225f, -4189.092f, 70.79f, MovementFlags.MOVEFLAG_NONE, 0f, 0f, 0f, LiquidType.None, -500f)]
        [InlineData(1, MovementFlags.MOVEFLAG_NONE, 1u, -576.927f, -4242.207f, 37.980f, 0f, Gender.Female, Race.Orc, -576.927f, -4242.207f, 38.203f, MovementFlags.MOVEFLAG_NONE, 0f, 0f, 0f, LiquidType.None, -500f)]
        [InlineData(1, MovementFlags.MOVEFLAG_NONE, 1u, -582.580f, -4236.643f, 38.044f, 0f, Gender.Female, Race.Orc, -582.580f, -4236.643f, 38.141f, MovementFlags.MOVEFLAG_NONE, 0f, 0f, 0f, LiquidType.None, -500f)]
        [InlineData(1, MovementFlags.MOVEFLAG_NONE, 1u, -601.294f, -4296.760f, 37.811f, 0f, Gender.Female, Race.Orc, -601.294f, -4296.760f, 37.811f, MovementFlags.MOVEFLAG_NONE, 0f, 0f, 0f, LiquidType.None, -500f)]
        [InlineData(1, MovementFlags.MOVEFLAG_NONE, 1u, -618.518f, -4251.67f, 38.718f, 0f, Gender.Female, Race.Orc, -618.518f, -4251.67f, 38.718f, MovementFlags.MOVEFLAG_NONE, 0f, 0f, 0f, LiquidType.None, -500f)]
        [InlineData(1, MovementFlags.MOVEFLAG_NONE, 389u, -158.395f, 5.857f, -42.873f, 0f, Gender.Male, Race.Orc, -158.395f, 5.857f, -43.699f, MovementFlags.MOVEFLAG_NONE, 0f, 0f, 0f, LiquidType.Magma, -64.49f)]
        [InlineData(1, MovementFlags.MOVEFLAG_NONE, 389u, -212.988f, -58.457f, -65.660f, 0f, Gender.Male, Race.Orc, -212.988f, -58.457f, -65.660f, MovementFlags.MOVEFLAG_NONE, 0f, 0f, 0f, LiquidType.Magma, -64.49f)] // Standing in VMAP lava
        [InlineData(1, MovementFlags.MOVEFLAG_NONE, 389u, -247.728f, -30.644f, -58.082f, 0f, Gender.Male, Race.Orc, -247.728f, -30.644f, -59.043f, MovementFlags.MOVEFLAG_NONE, 0f, 0f, 0f, LiquidType.None, -500f)]
        [InlineData(1, MovementFlags.MOVEFLAG_FORWARD, 1u, 1680.99f, -4371.30f, 26.54f, 2.6133f, Gender.Male, Race.Orc, 1680.99f, -4371.30f, 26.54f, MovementFlags.MOVEFLAG_FORWARD, 0f, 0f, 0f, LiquidType.None, -500f)] //Running into non-walkable wall in Orgrimmar. Should negate all intended movement.
        [InlineData(1, MovementFlags.MOVEFLAG_FORWARD, 1u, 1680.99f, -4371.30f, 26.54f, 4.1833f, Gender.Male, Race.Orc, 1680.99f, -4371.30f, 26.54f, MovementFlags.MOVEFLAG_FORWARD, 0f, 0f, 0f, LiquidType.None, -500f)] //Running into non-walkable wall in Orgrimmar at an angle. Should negate some movement.
        [InlineData(1, MovementFlags.MOVEFLAG_FORWARD, 1u, 1661.31f, -4343.53f, 26.54f, 4.2828f, Gender.Male, Race.Orc, 1681.015f, -4371.367f, 26.54f, MovementFlags.MOVEFLAG_FORWARD, 0f, 0f, 0f, LiquidType.None, -500f)] //Running off of flight tower in Orgrimmar.
        [InlineData(5, MovementFlags.MOVEFLAG_FORWARD, 1u, 1679.552f, -4372.284f, 27.385f, 6.151f, Gender.Female, Race.Orc, 1661.377f, -4369.652f, 24.740f, MovementFlags.MOVEFLAG_FORWARD, 0f, 0f, 0f, LiquidType.Water, 0f)] // Origmmar terrain step-up and slide down
        [InlineData(20, MovementFlags.MOVEFLAG_FORWARD, 0u, -8949.95f, -132.49f, 83.23f, 0.0f, Gender.Female, Race.Human, -8942.958f, -132.484f, 83.679f, MovementFlags.MOVEFLAG_FORWARD, 6.992f, -0.002f, 0f, LiquidType.None, -500f)]  // North facing
        [InlineData(20, MovementFlags.MOVEFLAG_FORWARD, 0u, -8949.95f, -132.49f, 83.23f, 1.570f, Gender.Female, Race.Human, -8949.95f, -125.499f, 83.319f, MovementFlags.MOVEFLAG_FORWARD, 0f, 7f, 0f, LiquidType.None, -500f)] // East facing  
        [InlineData(20, MovementFlags.MOVEFLAG_FORWARD, 0u, -8949.95f, -132.49f, 83.23f, -1.570f, Gender.Female, Race.Human, -8949.95f, -139.487f, 83.468f, MovementFlags.MOVEFLAG_FORWARD, 0.02f, -6.988f, 0f, LiquidType.None, -500f)] // West facing
        [InlineData(20, MovementFlags.MOVEFLAG_FORWARD, 0u, -8949.95f, -132.49f, 83.23f, 3.141f, Gender.Female, Race.Human, -8956.924f, -132.505f, 82.927f, MovementFlags.MOVEFLAG_FORWARD, -6.973f, -0.026f, 0f, LiquidType.None, -500f)] // South facing
        [InlineData(20, MovementFlags.MOVEFLAG_FORWARD, 1u, -562.225f, -4189.092f, 70.789f, 6.175f, Gender.Female, Race.Orc, -555.371f, -4189.969f, 72.64f, MovementFlags.MOVEFLAG_FORWARD, 6.947f, -0.752f, 0f, LiquidType.None, -500f)]
        [InlineData(20, MovementFlags.MOVEFLAG_FORWARD, 1u, -601.518f, -4602.816f, 41.294f, 1.612f, Gender.Female, Race.Orc, -601.813f, -4595.829f, 41.065f, MovementFlags.MOVEFLAG_FORWARD, -0.308f, 6.982f, 0f, LiquidType.None, -500f)]
        public void StepPhysicsV2_FrameMovement(
            uint frames,
            MovementFlags startFlags,
            uint mapId,
            float startX, float startY, float startZ,
            float orientation,
            Gender gender,
            Race race,
            float expX, float expY, float expZ,
            MovementFlags expFlags,
            float expVX, float expVY, float expVZ,
            LiquidType expLiquidType,
            float expLiquidZ)
        {
            var (radius, height) = RaceDimensions.GetCapsuleForRace(race, gender);

            var input = new PhysicsInput
            {
                mapId = mapId,
                x = startX,
                y = startY,
                z = startZ,
                orientation = orientation,
                moveFlags = (uint)startFlags,
                radius = radius,
                height = height,
                walkSpeed = 2.5f,
                runSpeed = 7.0f,
                runBackSpeed = 4.5f,
                swimSpeed = 4.72f,
                flightSpeed = 2.5f,
                frameCounter = 1,
            };

            PhysicsOutput expectedFinalOutput = new()
            {
                x = expX,
                y = expY,
                z = expZ,
                vx = expVX,
                vy = expVY,
                vz = expVZ,
                moveFlags = (uint)expFlags,
                liquidType = (uint)expLiquidType,
                liquidZ = expLiquidZ,
            };

            PhysicsOutput actualOutput = new();
            for (int i = 0; i < frames; i++)
            {
                actualOutput = _phy.StepPhysicsV2(input, Dt);

                // Update input for next iteration
                input.x = actualOutput.x;
                input.y = actualOutput.y;
                input.z = actualOutput.z;
                input.vx = actualOutput.vx;
                input.vy = actualOutput.vy;
                input.vz = actualOutput.vz;
                input.moveFlags = actualOutput.moveFlags;
                input.frameCounter++;
            }

            AssertEqual(expectedFinalOutput, actualOutput);
        }

        [Fact]
        public void LineOfSight_ShouldReturnTrue_WhenNoObstruction()
        {
            uint mapId = 1;
            Position from = new(1629.0f, -4373.0f, 53.0f);
            Position to = new(1630.0f, -4372.0f, 53.0f);

            Assert.True(_phy.LineOfSight(mapId, from.ToXYZ(), to.ToXYZ()));
        }

        [Fact]
        public void LineOfSight_ShouldReturnFalse_WhenObstructed()
        {
            uint mapId = 389;
            Position from = new(-247.728561f, -30.644503f, -58.082531f);
            Position to = new(-158.395340f, 5.857921f, -42.873611f);

            Assert.False(_phy.LineOfSight(mapId, from.ToXYZ(), to.ToXYZ()));
        }
    }
}
