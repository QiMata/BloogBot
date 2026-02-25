using GameData.Core.Enums;
using PathfindingService.Repository;
using MovementFlags = GameData.Core.Enums.MovementFlags;
using GameData.Core.Models;
using System;
using System.IO;

namespace PathfindingService.Tests
{
    public class PhysicsFixture : IDisposable
    {
        public Physics Physics { get; }

        public PhysicsFixture()
        {
            // Preflight checks similar to NavigationFixture
            VerifyNavigationDll();
            Physics = new Physics();
        }

        private static void VerifyNavigationDll()
        {
            var assemblyLocation = System.Reflection.Assembly.GetExecutingAssembly().Location;
            var testOutputDir = Path.GetDirectoryName(assemblyLocation);

            if (testOutputDir == null)
                throw new InvalidOperationException("Cannot determine test output directory");

            var navigationDllPath = Path.Combine(testOutputDir, "Navigation.dll");

            if (!File.Exists(navigationDllPath))
            {
                throw new FileNotFoundException(
                    $"Navigation.dll not found in test output directory: {testOutputDir}");
            }
        }

        public void Dispose() { }
    }

    public class PhysicsEngineTests(PhysicsFixture fixture) : IClassFixture<PhysicsFixture>
    {
        private readonly Physics _phy = fixture.Physics;
        private const float Dt = 0.05f;

        [Theory]
        [InlineData(1u, -562.225f, -4189.092f, 70.789f, Race.Orc, 0f, MovementFlags.MOVEFLAG_NONE)]
        [InlineData(0u, -8949.950000f, -132.490000f, 83.229485f, Race.Human, 0f, MovementFlags.MOVEFLAG_NONE)]
        [InlineData(0u, -6240.320000f, 331.033000f, 382.619171f, Race.Human, 0f, MovementFlags.MOVEFLAG_NONE)]
        [InlineData(1u, -2917.580000f, -257.980000f, 53.362350f, Race.Orc, 0f, MovementFlags.MOVEFLAG_NONE)]
        [InlineData(1u, 1629.359985f, -4373.380377f, 31.255800f, Race.Orc, 3.548300f, MovementFlags.MOVEFLAG_NONE)]
        public void StepPhysics_IdleExpectations(
            uint mapId,
            float x, float y, float z,
            Race race,
            float orientation,
            MovementFlags expectedFlags)
        {
            // Default heights and radii for races
            float height = race == Race.Orc ? 2.0f : 1.8f;
            float radius = race == Race.Orc ? 0.6f : 0.5f;
            
            var input = new PhysicsInput
            {
                mapId = mapId,
                x = x,
                y = y,
                z = z,
                orientation = orientation,
                moveFlags = (uint)MovementFlags.MOVEFLAG_NONE,
                deltaTime = Dt,
                height = height,
                radius = radius,
                runSpeed = 7.0f,
                walkSpeed = 2.5f
            };

            var output = _phy.StepPhysicsV2(input, Dt);

            Assert.NotNull(output);
            // Position should be stable (not falling through world)
            Assert.True(Math.Abs(output.z - z) < 5f, $"Character fell too far: {output.z} vs {z}");
        }

        [Fact]
        public void LineOfSight_ShouldReturnTrue_WhenNoObstruction()
        {
            // Test line of sight in open area
            var from = new XYZ(-8949.95f, -132.49f, 83.53f);
            var to = new XYZ(-8945.0f, -132.0f, 83.53f);

            var result = _phy.LineOfSight(0, from, to);

            Assert.True(result);
        }

        [Fact]
        public void LineOfSight_ShouldReturnFalse_WhenObstructed()
        {
            // Deterministic blocked LOS route in Stormwind area (map 0).
            // Same-XY vertical probes are often clear in this engine path and are not stable as obstruction tests.
            var from = new XYZ(-8949.95f, -132.49f, 83.53f);
            var to = new XYZ(-8880.00f, -220.00f, 83.53f);

            var result = _phy.LineOfSight(0, from, to);

            Assert.False(result);
        }
    }
}
