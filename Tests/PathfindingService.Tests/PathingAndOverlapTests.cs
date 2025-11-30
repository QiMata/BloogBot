using GameData.Core.Models;
using PathfindingService.Repository;

namespace PathfindingService.Tests
{
    /// <summary>
    /// End‑to‑end tests for the Navigation API.
    ///   • CalculatePath
    /// </summary>
    public class NavigationFixture : IDisposable
    {
        public Navigation Navigation { get; }

        public NavigationFixture() => Navigation = new Navigation();

        public void Dispose() { /* Navigation lives for the AppDomain – nothing to do. */ }
    }

    public class PathfindingTests(NavigationFixture fixture) : IClassFixture<NavigationFixture>
    {
        private readonly Navigation _navigation = fixture.Navigation;

        [Fact]
        public void CalculatePath_ShouldReturnValidPath()
        {
            uint mapId = 1;
            Position start = new(-616.2514f, -4188.0044f, 82.316719f);
            Position end = new(1629.36f, -4373.39f, 50.2564f);

            var path = _navigation.CalculatePath(mapId, start.ToXYZ(), end.ToXYZ(), smoothPath: true);

            Assert.NotNull(path);
            Assert.NotEmpty(path);
        }
    }
}
