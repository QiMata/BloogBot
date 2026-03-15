using GameData.Core.Models;
using PathfindingService.Repository;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

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

        /// <summary>
        /// Regression: exact live corpse-run points captured after the BG hydration fix.
        /// This is the route `RetrieveCorpseTask` asked for when the live rerun fell back
        /// to repeated socket timeouts and eventually reported `no_path`.
        /// </summary>
        [Fact]
        public void CalculatePath_OrgrimmarCorpseRun_LiveRetrieveRoute()
        {
            uint mapId = 1;
            Position start = new(1177.8f, -4464.2f, 21.4f);
            Position end = new(1629.4f, -4373.4f, 31.3f);

            var path = ExecuteWithNativeSegmentValidation(
                () => _navigation.CalculatePath(mapId, start.ToXYZ(), end.ToXYZ(), smoothPath: true));

            var validationFailure = PathRouteAssertions.GetValidationFailure(
                mapId,
                start.ToXYZ(),
                end.ToXYZ(),
                path,
                maxStartDistance: 10.0f,
                maxEndDistance: 12.0f,
                maxSegmentLength: 200.0f,
                maxHeightJump: 25.0f);

            Assert.Null(validationFailure);
            Assert.True(path.Length >= 3, $"Live retrieve route too short ({path.Length} points)");
        }

        /// <summary>
        /// Regression: corpse run requests this route in straight-corner mode. That mode must
        /// not spend 30s+ in native route shaping before the service can try the alternate mode.
        /// </summary>
        [Fact]
        public async Task CalculatePath_OrgrimmarCorpseRun_LiveRetrieveRoute_StraightRequestCompletesWithinBudget()
        {
            uint mapId = 1;
            Position start = new(1177.8f, -4464.2f, 21.4f);
            Position end = new(1629.4f, -4373.4f, 31.3f);
            var task = Task.Run(() => ExecuteWithNativeSegmentValidation(
                () => _navigation.CalculatePath(mapId, start.ToXYZ(), end.ToXYZ(), smoothPath: false)));

            var completedTask = await Task.WhenAny(task, Task.Delay(TimeSpan.FromSeconds(25)));
            Assert.Same(task, completedTask);

            var path = await task;
            var validationFailure = PathRouteAssertions.GetValidationFailure(
                mapId,
                start.ToXYZ(),
                end.ToXYZ(),
                path,
                maxStartDistance: 10.0f,
                maxEndDistance: 12.0f,
                maxSegmentLength: 200.0f,
                maxHeightJump: 25.0f);

            Assert.Null(validationFailure);
            Assert.True(path.Length >= 3, $"Straight-request live retrieve route too short ({path.Length} points)");
        }

        /// <summary>
        /// FG corpse run: Razor Hill graveyard → corpse near Razor Hill.
        /// This is the exact route the FG bot takes after dying at (340, -4686).
        /// The ghost spawns at the graveyard (~233, -4794) and must navigate back.
        /// Validates that no path segment passes through collidable objects (rocks, braziers).
        /// </summary>
        [Fact]
        public void CalculatePath_RazorHillCorpseRun_GraveyardToCorpse_NoCollision()
        {
            uint mapId = 1;
            // Razor Hill graveyard (ghost spawn after release)
            Position start = new(233.5f, -4793.7f, 10.2f);
            // Corpse location (death area near Razor Hill)
            Position end = new(340.0f, -4686.0f, 16.5f);

            var path = _navigation.CalculatePath(mapId, start.ToXYZ(), end.ToXYZ(), smoothPath: true);

            // Basic path validation
            var validationFailure = PathRouteAssertions.GetValidationFailure(
                mapId,
                start.ToXYZ(),
                end.ToXYZ(),
                path,
                maxStartDistance: 8.0f,
                maxEndDistance: 10.0f,
                maxSegmentLength: 200.0f,
                maxHeightJump: 15.0f);

            Assert.Null(validationFailure);
            Assert.True(path.Length >= 3, $"Corpse-run path too short ({path.Length} points) for ~150y travel");

            // Diagnostic: dump path waypoints
            var pathDump = new List<string>();
            for (int i = 0; i < path.Length; i++)
                pathDump.Add($"  [{i}] ({path[i].X:F1},{path[i].Y:F1},{path[i].Z:F1})");
            Console.Error.WriteLine($"[PATH] Razor Hill corpse run: {path.Length} waypoints:\n{string.Join("\n", pathDump)}");

            // Check each segment for LOS obstruction — if a segment fails LOS,
            // the path is routing through a collidable object (rock, brazier, building).
            var losFailures = new List<string>();
            for (int i = 0; i < path.Length - 1; i++)
            {
                var segStart = path[i];
                var segEnd = path[i + 1];

                // Test LOS at multiple heights along the segment to catch objects
                // at different elevations. Use player eye height (~1.5m above ground).
                var eyeStart = new XYZ(segStart.X, segStart.Y, segStart.Z + 1.5f);
                var eyeEnd = new XYZ(segEnd.X, segEnd.Y, segEnd.Z + 1.5f);

                if (!LineOfSight(mapId, eyeStart, eyeEnd))
                {
                    // Also test at ground level to confirm it's not just a ceiling
                    var groundBlocked = !LineOfSight(mapId, segStart, segEnd);
                    var label = groundBlocked ? "GROUND+EYE" : "EYE_ONLY";
                    losFailures.Add(
                        $"Segment {i}->{i + 1} [{label}]: ({segStart.X:F1},{segStart.Y:F1},{segStart.Z:F1}) -> " +
                        $"({segEnd.X:F1},{segEnd.Y:F1},{segEnd.Z:F1}) " +
                        $"dist2D={MathF.Sqrt((segEnd.X - segStart.X) * (segEnd.X - segStart.X) + (segEnd.Y - segStart.Y) * (segEnd.Y - segStart.Y)):F1}y");
                }
            }

            Assert.True(losFailures.Count == 0,
                $"Path has {losFailures.Count} segment(s) with LOS obstruction (path passes through collidable objects):\n" +
                string.Join("\n", losFailures));
        }

        /// <summary>
        /// Verify that the previously blocked Razor Hill brazier area is now clear
        /// after baking server-spawned gameobject meshes into the navmesh.
        /// The path should route around the brazier at (~273, -4729) instead of through it.
        /// </summary>
        [Fact]
        public void RazorHillCorpseRun_BrazierArea_NowClear()
        {
            uint mapId = 1;
            // Path from graveyard to corpse should avoid the brazier area
            Position start = new(233.5f, -4793.7f, 10.2f);
            Position end = new(340.0f, -4686.0f, 16.5f);

            var path = _navigation.CalculatePath(mapId, start.ToXYZ(), end.ToXYZ(), smoothPath: true);
            Assert.NotNull(path);
            Assert.True(path.Length >= 3, "Path too short");

            // Verify every segment has clear LOS (no collidable objects in the way)
            var losFailures = new List<string>();
            for (int i = 0; i < path.Length - 1; i++)
            {
                var eyeStart = new XYZ(path[i].X, path[i].Y, path[i].Z + 1.5f);
                var eyeEnd = new XYZ(path[i + 1].X, path[i + 1].Y, path[i + 1].Z + 1.5f);

                if (!LineOfSight(mapId, eyeStart, eyeEnd))
                {
                    losFailures.Add(
                        $"Segment {i}->{i + 1}: ({path[i].X:F1},{path[i].Y:F1},{path[i].Z:F1}) -> " +
                        $"({path[i + 1].X:F1},{path[i + 1].Y:F1},{path[i + 1].Z:F1})");
                }
            }

            Assert.True(losFailures.Count == 0,
                $"Path has {losFailures.Count} segment(s) with LOS obstruction:\n" +
                string.Join("\n", losFailures));
        }

        [DllImport("Navigation.dll", CallingConvention = CallingConvention.Cdecl)]
        [return: MarshalAs(UnmanagedType.I1)]
        private static extern bool LineOfSight(uint mapId, XYZ from, XYZ to);

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
