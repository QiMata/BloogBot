using GameData.Core.Constants;
using GameData.Core.Enums;
using PathfindingService.Repository; // Navigation wrapper
using static PathfindingService.Repository.Navigation;
using Xunit;
using MovementFlags = GameData.Core.Enums.MovementFlags;

namespace PathfindingService.Tests
{
    /// <summary>
    /// End-to-end tests for Navigation + PhysicsEngine.
    /// Uses InlineData for each idle-tick sample, preserving the NavigationFixture.
    /// </summary>
    public class PhysicsEngineTests(NavigationFixture fixture) : IClassFixture<NavigationFixture>
    {
        private readonly Navigation _nav = fixture.Navigation;
        private const float Dt = 0.05f; // one tick = 100 ms

        // Helper to compare PhysicsOutput with tolerance
        private static void AssertEqual(PhysicsOutput exp, PhysicsOutput act)
        {
            Console.WriteLine($"Movement Flags: {(MovementFlags)act.moveFlags}");
            Assert.Equal(exp.x, act.x, 3);
            Assert.Equal(exp.y, act.y, 3);
            Assert.Equal(exp.z, act.z, 3);
            Assert.Equal(exp.vx, act.vx, 3);
            Assert.Equal(exp.vy, act.vy, 3);
            Assert.Equal(exp.vz, act.vz, 3);
            Assert.Equal(exp.moveFlags, act.moveFlags);
        }

        [Theory]
        [InlineData(1, 1u, -601.518f, -4602.816f, 41.294f, MovementFlags.MOVEFLAG_FORWARD, 1.612f, Gender.Female, Race.Orc, -601.817f, -4595.827f, 41.065f, MovementFlags.MOVEFLAG_FORWARD, 0f, 0f, 0f)]
        [InlineData(1, 1u, -562.225f, -4189.092f, 70.789f, MovementFlags.MOVEFLAG_FORWARD, 6.175f, Gender.Female, Race.Orc, -555.441f, -4190.048f, 72.641f, MovementFlags.MOVEFLAG_FORWARD, 0f, 0f, 0f)]
        [InlineData(1, 1u, -535.151f, -4200.184f, 76.994f, MovementFlags.MOVEFLAG_FORWARD, 0.126f, Gender.Female, Race.Orc, -528.697f, -4199.830f, 76.993f, MovementFlags.MOVEFLAG_FORWARD, 0f, 0f, 0f)]
        [InlineData(1, 0u, -8949.95f, -132.49f, 83.23f, MovementFlags.MOVEFLAG_FORWARD, 0.0f, Gender.Female, Race.Human, -8942.958f, -132.485f, 83.679f, MovementFlags.MOVEFLAG_FORWARD, 0f, 0f, 0f)]  // North facing
        [InlineData(1, 0u, -8949.95f, -132.49f, 83.23f, MovementFlags.MOVEFLAG_FORWARD, 1.570f, Gender.Female, Race.Human, -8949.930f, -125.498f, 83.332f, MovementFlags.MOVEFLAG_FORWARD, 0f, 0f, 0f)] // East facing  
        [InlineData(1, 0u, -8949.95f, -132.49f, 83.23f, MovementFlags.MOVEFLAG_FORWARD, 3.141f, Gender.Female, Race.Human, -8956.923f, -132.509f, 82.927f, MovementFlags.MOVEFLAG_FORWARD, 0f, 0f, 0f)] // South facing
        [InlineData(1, 0u, -8949.95f, -132.49f, 83.23f, MovementFlags.MOVEFLAG_FORWARD, -1.570f, Gender.Female, Race.Human, -8949.949f, -139.487f, 83.467f, MovementFlags.MOVEFLAG_FORWARD, 0f, 0f, 0f)] // West facing
        [InlineData(1, 1u, -562.225f, -4189.092f, 70.789f, MovementFlags.MOVEFLAG_FORWARD, 0f, Gender.Female, Race.Orc, -562.225f, -4189.092f, 70.789f, MovementFlags.MOVEFLAG_NONE, 0f, 0f, 0f)]
        [InlineData(1, 0u, -8949.950f, -132.490f, 83.229f, MovementFlags.MOVEFLAG_FORWARD, 0f, Gender.Female, Race.Human, -8949.950f, -132.490f, 83.229f, MovementFlags.MOVEFLAG_NONE, 0f, 0f, 0f)]
        [InlineData(1, 0u, -6240.320f, 331.033f, 382.619f, MovementFlags.MOVEFLAG_FORWARD, 0f, Gender.Female, Race.Human, -6240.320f, 331.033f, 382.619f, MovementFlags.MOVEFLAG_NONE, 0f, 0f, 0f)]
        [InlineData(1, 0u, 524.311f, 312.037f, 31.260f, MovementFlags.MOVEFLAG_NONE, 0.002f, Gender.Female, Race.Orc, 524.311f, 312.037f, 31.260f, MovementFlags.MOVEFLAG_NONE, 0f, 0f, 0f)]
        [InlineData(1, 0u, 537.798f, 279.534f, 31.208f, MovementFlags.MOVEFLAG_NONE, 0f, Gender.Female, Race.Orc, 537.798f, 279.534f, 31.208f, MovementFlags.MOVEFLAG_NONE, 0f, 0f, 0f)]
        [InlineData(1, 0u, 538.0f, 279.0f, 31.237f, MovementFlags.MOVEFLAG_NONE, 0f, Gender.Female, Race.Orc, 538.0f, 279.0f, 31.237f, MovementFlags.MOVEFLAG_NONE, 0f, 0f, 0f)]
        [InlineData(1, 0u, 582.693f, 342.985f, 31.149f, MovementFlags.MOVEFLAG_NONE, 0f, Gender.Female, Race.Orc, 582.693f, 342.985f, 31.149f, MovementFlags.MOVEFLAG_SWIMMING, 0f, 0f, 0f)]
        [InlineData(1, 0u, 623.246f, 349.184f, 31.149f, MovementFlags.MOVEFLAG_NONE, 0f, Gender.Female, Race.Orc, 623.246f, 349.184f, 31.149f, MovementFlags.MOVEFLAG_SWIMMING, 0f, 0f, 0f)]
        [InlineData(1, 0u, 623.683f, 349.455f, 31.245f, MovementFlags.MOVEFLAG_NONE, 0f, Gender.Female, Race.Orc, 623.683f, 349.455f, 31.245f, MovementFlags.MOVEFLAG_NONE, 0f, 0f, 0f)]
        [InlineData(1, 1u, -2917.580f, -257.980f, 53.362f, MovementFlags.MOVEFLAG_NONE, 0f, Gender.Female, Race.Orc, -2917.580f, -257.980f, 53.362f, MovementFlags.MOVEFLAG_NONE, 0f, 0f, 0f)]
        [InlineData(1, 1u, -618.518f, -4251.67f, 38.718f, MovementFlags.MOVEFLAG_NONE, 0f, Gender.Female, Race.Orc, -618.518f, -4251.67f, 38.718f, MovementFlags.MOVEFLAG_NONE, 0f, 0f, 0f)]
        [InlineData(1, 1u, -601.294f, -4296.760f, 37.811f, MovementFlags.MOVEFLAG_NONE, 0f, Gender.Female, Race.Orc, -601.294f, -4296.760f, 37.811f, MovementFlags.MOVEFLAG_NONE, 0f, 0f, 0f)]
        [InlineData(1, 1u, -582.580f, -4236.643f, 38.044f, MovementFlags.MOVEFLAG_NONE, 0f, Gender.Female, Race.Orc, -582.580f, -4236.643f, 38.044f, MovementFlags.MOVEFLAG_NONE, 0f, 0f, 0f)]
        [InlineData(1, 1u, -576.927f, -4242.207f, 37.980f, MovementFlags.MOVEFLAG_NONE, 0f, Gender.Female, Race.Orc, -576.927f, -4242.207f, 37.980f, MovementFlags.MOVEFLAG_NONE, 0f, 0f, 0f)]
        [InlineData(1, 1u, -550.479f, -4194.069f, 49.271f, MovementFlags.MOVEFLAG_NONE, 0f, Gender.Female, Race.Orc, -550.479f, -4194.069f, 49.271f, MovementFlags.MOVEFLAG_NONE, 0f, 0f, 0f)]
        [InlineData(1, 1u, -535.382f, -4204.233f, 74.716f, MovementFlags.MOVEFLAG_NONE, 0f, Gender.Female, Race.Orc, -535.382f, -4204.233f, 74.716f, MovementFlags.MOVEFLAG_NONE, 0f, 0f, 0f)]
        [InlineData(1, 1u, -557.773f, -4181.990f, 72.576f, MovementFlags.MOVEFLAG_NONE, 0f, Gender.Female, Race.Orc, -557.773f, -4181.990f, 72.576f, MovementFlags.MOVEFLAG_NONE, 0f, 0f, 0f)]
        [InlineData(1, 1u, 1629.359f, -4373.380f, 31.255f, MovementFlags.MOVEFLAG_NONE, 0f, Gender.Female, Race.Orc, 1629.359f, -4373.380f, 31.255f, MovementFlags.MOVEFLAG_NONE, 0f, 0f, 0f)]
        [InlineData(1, 1u, 10334.000f, 833.902f, 1326.110f, MovementFlags.MOVEFLAG_NONE, 0f, Gender.Female, Race.Orc, 10334.000f, 833.902f, 1326.110f, MovementFlags.MOVEFLAG_NONE, 0f, 0f, 0f)]
        [InlineData(1, 1u, 1632.825f, -4372.532f, 29.364f, MovementFlags.MOVEFLAG_FORWARD, 0f, Gender.Female, Race.Orc, 1629.129f, -4374.037f, 30.674f, MovementFlags.MOVEFLAG_FORWARD, 0f, 0f, 0f)] // Step up blocked on Orgrimmar bank
        [InlineData(1, 1u, 1636.211f, -4375.267f, 28.748f, MovementFlags.MOVEFLAG_FORWARD, 0f, Gender.Female, Race.Orc, 1636.211f, -4375.267f, 28.748f, MovementFlags.MOVEFLAG_FORWARD, 0f, 0f, 0f)] // Forward movement blocked by spike on Orgrimmar bank
        [InlineData(1, 1u, 1661.377f, -4369.652f, 24.740f, MovementFlags.MOVEFLAG_FORWARD, 0f, Gender.Female, Race.Orc, 1661.377f, -4369.652f, 24.740f, MovementFlags.MOVEFLAG_FORWARD, 0f, 0f, 0f)] // Origmmar terrain block
        [InlineData(1, 1u, 1662.314f, -4371.963f, 24.925f, MovementFlags.MOVEFLAG_FORWARD, 0f, Gender.Female, Race.Orc, 1661.377f, -4369.652f, 24.740f, MovementFlags.MOVEFLAG_FORWARD, 0f, 0f, 0f)] // Origmmar terrain step-up and slide down
        [InlineData(1, 1u, 1679.552f, -4372.284f, 27.385f, MovementFlags.MOVEFLAG_FORWARD, 6.151f, Gender.Female, Race.Orc, 1661.377f, -4369.652f, 24.740f, MovementFlags.MOVEFLAG_FORWARD, 0f, 0f, 0f)] // Origmmar terrain step-up and slide down
        [InlineData(1, 389u, -247.728f, -30.644f, -58.082f, MovementFlags.MOVEFLAG_NONE, 0f, Gender.Female, Race.Orc, -247.728f, -30.644f, -58.082f, MovementFlags.MOVEFLAG_NONE, 0f, 0f, 0f)]
        [InlineData(1, 389u, -158.395f, 5.857f, -42.873f, MovementFlags.MOVEFLAG_NONE, 0f, Gender.Female, Race.Orc, -158.395f, 5.857f, -42.873f, MovementFlags.MOVEFLAG_NONE, 0f, 0f, 0f)]
        [InlineData(1, 389u, -212.988f, -58.457f, -65.660f, MovementFlags.MOVEFLAG_NONE, 0f, Gender.Female, Race.Orc, -212.988f, -58.457f, -65.660f, MovementFlags.MOVEFLAG_NONE, 0f, 0f, 0f)] // Standing in VMAP lava
        public void StepPhysics_FrameMovement(
            uint frames,
            uint mapId,
            float startX, float startY, float startZ,
            MovementFlags startFlags,
            float orientation,
            Gender gender,
            Race race,
            float expX, float expY, float expZ,
            MovementFlags expFlags,
            float expVX, float expVY, float expVZ)
        {
            var (radius, height) = RaceDimensions.GetCapsuleForRace(race, gender);
            // Setup input with FORWARD movement flag
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
            };


            PhysicsOutput expectedFinalOutput = new()
            {
                x = expX,
                y = expY,
                z = expZ,
                vx = expVX,
                vy = expVY,
                vz = expVZ,
                moveFlags = (uint)expFlags
            };

            PhysicsOutput actualOutput = new();
            for (int i = 0; i < frames; i++)
            {
                actualOutput = _nav.StepPhysics(input, Dt);

                // Update input for next iteration
                input.x = actualOutput.x;
                input.y = actualOutput.y;
                input.z = actualOutput.z;
                input.vx = actualOutput.vx;
                input.vy = actualOutput.vy;
                input.vz = actualOutput.vz;
                input.moveFlags = actualOutput.moveFlags;
            }

            AssertEqual(expectedFinalOutput, actualOutput);
        }
    }
}
