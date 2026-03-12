using GameData.Core.Models;
using PathfindingService.Repository;

namespace PathfindingService.Tests
{
    public class PathfindingTests(NavigationFixture fixture) : IClassFixture<NavigationFixture>
    {
        private readonly Navigation _navigation = fixture.Navigation;

        [Fact]
        public void CalculatePath_ShouldReturnValidPath()
        {
            uint mapId = 1;
            Position start = new(-616.2514f, -4188.0044f, 82.316719f);
            Position end = new(1629.36f, -4373.39f, 50.2564f);

            var path = ExecuteWithNativeSegmentValidation(
                () => _navigation.CalculatePath(mapId, start.ToXYZ(), end.ToXYZ(), smoothPath: true));

            var validationFailure = PathRouteAssertions.GetValidationFailure(
                mapId,
                start.ToXYZ(),
                end.ToXYZ(),
                path,
                maxStartDistance: 8.0f,
                maxEndDistance: 20.0f,
                maxSegmentLength: 260.0f,
                maxHeightJump: 30.0f);

            Assert.Null(validationFailure);
        }

        /// <summary>
        /// Regression: graveyard to Orgrimmar corpse-run runback.
        /// Ghost spawns near the Durotar graveyard spirit healer and must
        /// path back into Orgrimmar. This exercises the entrance gate slopes
        /// and multi-level terrain transition that previously caused wall stalls.
        /// </summary>
        [Fact]
        public void CalculatePath_OrgrimmarCorpseRun_GraveyardToCenter()
        {
            uint mapId = 1;
            // Durotar graveyard (spirit healer)
            Position start = new(1543f, -4959f, 9f);
            // Orgrimmar center (near AH / The Drag)
            Position end = new(1680f, -4315f, 62f);

            var path = ExecuteWithNativeSegmentValidation(
                () => _navigation.CalculatePath(mapId, start.ToXYZ(), end.ToXYZ(), smoothPath: true));

            var validationFailure = PathRouteAssertions.GetValidationFailure(
                mapId,
                start.ToXYZ(),
                end.ToXYZ(),
                path,
                maxStartDistance: 8.0f,
                maxEndDistance: 12.0f,
                maxSegmentLength: 200.0f,
                maxHeightJump: 25.0f);

            Assert.Null(validationFailure);
            Assert.True(path.Length >= 3, $"Corpse-run path too short ({path.Length} points) for ~700y travel");
        }

        /// <summary>
        /// Regression: Orgrimmar entrance gate to Valley of Spirits.
        /// Inner-city navigation through tight corridors and elevation changes.
        /// </summary>
        [Fact]
        public void CalculatePath_OrgrimmarInnerCity_EntranceToValleyOfSpirits()
        {
            uint mapId = 1;
            // Orgrimmar front gate
            Position start = new(1394f, -4480f, 26f);
            // Valley of Spirits
            Position end = new(1862f, -4348f, -14f);

            var path = ExecuteWithNativeSegmentValidation(
                () => _navigation.CalculatePath(mapId, start.ToXYZ(), end.ToXYZ(), smoothPath: true));

            var validationFailure = PathRouteAssertions.GetValidationFailure(
                mapId,
                start.ToXYZ(),
                end.ToXYZ(),
                path,
                maxStartDistance: 8.0f,
                maxEndDistance: 15.0f,
                maxSegmentLength: 200.0f,
                maxHeightJump: 35.0f);

            Assert.Null(validationFailure);
        }

        /// <summary>
        /// Regression: reverse corpse-run direction (Orgrimmar to graveyard).
        /// Ensures path symmetry — if A to B works, B to A should also produce a valid route.
        /// </summary>
        [Fact]
        public void CalculatePath_OrgrimmarCorpseRun_ReverseDirection()
        {
            uint mapId = 1;
            Position start = new(1680f, -4315f, 62f);
            Position end = new(1543f, -4959f, 9f);

            var path = ExecuteWithNativeSegmentValidation(
                () => _navigation.CalculatePath(mapId, start.ToXYZ(), end.ToXYZ(), smoothPath: true));

            var validationFailure = PathRouteAssertions.GetValidationFailure(
                mapId,
                start.ToXYZ(),
                end.ToXYZ(),
                path,
                maxStartDistance: 8.0f,
                maxEndDistance: 12.0f,
                maxSegmentLength: 200.0f,
                maxHeightJump: 25.0f);

            Assert.Null(validationFailure);
            Assert.True(path.Length >= 3, $"Reverse corpse-run path too short ({path.Length} points)");
        }
        private static XYZ[] ExecuteWithNativeSegmentValidation(Func<XYZ[]> action)
        {
            const string key = "WWOW_ENABLE_NATIVE_SEGMENT_VALIDATION";
            var previous = Environment.GetEnvironmentVariable(key);
            Environment.SetEnvironmentVariable(key, "1");
            try
            {
                return action();
            }
            finally
            {
                Environment.SetEnvironmentVariable(key, previous);
            }
        }
    }
}
