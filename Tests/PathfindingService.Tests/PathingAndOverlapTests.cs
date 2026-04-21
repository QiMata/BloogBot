using GameData.Core.Models;
using PathfindingService.Repository;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace PathfindingService.Tests
{
    public class PathfindingTests(NavigationFixture fixture) : IClassFixture<NavigationFixture>
    {
        private readonly Navigation _navigation = fixture.Navigation;

        /// <summary>
        /// Regression: the live Orgrimmar corpse-retrieve line should produce a detour
        /// around blocking city geometry rather than trying to follow the blocked direct segment.
        /// </summary>
        [Fact]
        public void CalculatePath_OrgrimmarCorpseRun_LiveRetrieveRoute_ReroutesAroundBlockedDirectLine()
        {
            uint mapId = 1;
            Position start = new(1177.8f, -4464.2f, 21.4f);
            Position end = new(1629.4f, -4373.4f, 31.3f);

            Assert.False(
                LineOfSight(mapId, start.ToXYZ(), end.ToXYZ()),
                $"Expected blocked direct line on map {mapId} for {start} -> {end}, but LOS was clear.");

            var path = _navigation.CalculatePath(mapId, start.ToXYZ(), end.ToXYZ(), smoothPath: true);

            var validationFailure = PathRouteAssertions.GetValidationFailure(
                mapId,
                start.ToXYZ(),
                end.ToXYZ(),
                path,
                maxStartDistance: 10.0f,
                maxEndDistance: 12.0f,
                maxSegmentLength: 200.0f,
                maxHeightJump: 25.0f);

            Assert.True(
                validationFailure is null,
                $"Blocked-corridor reroute produced an invalid path on map {mapId}: {validationFailure}\n{FormatPath(path)}");
            Assert.True(
                path.Length >= 3,
                $"Expected an intermediate detour waypoint on map {mapId}, got {path.Length} points.\n{FormatPath(path)}");

            var maxDeviation = GetMaxIntermediateDeviation(path, start.ToXYZ(), end.ToXYZ());
            Assert.True(
                maxDeviation >= 10.0f,
                $"Expected reroute to deviate from blocked direct segment on map {mapId}, but max deviation was {maxDeviation:F1}y.\n{FormatPath(path)}");
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



















        [DllImport("Navigation.dll", CallingConvention = CallingConvention.Cdecl)]
        [return: MarshalAs(UnmanagedType.I1)]
        private static extern bool LineOfSight(uint mapId, XYZ from, XYZ to);

        private static float GetMaxIntermediateDeviation(XYZ[] path, XYZ start, XYZ end)
        {
            if (path.Length <= 2)
                return 0f;

            var maxDeviation = 0f;
            for (int i = 1; i < path.Length - 1; i++)
            {
                var deviation = PerpendicularDistance2D(path[i], start, end);
                if (deviation > maxDeviation)
                    maxDeviation = deviation;
            }

            return maxDeviation;
        }

        private static float PerpendicularDistance2D(XYZ point, XYZ segStart, XYZ segEnd)
        {
            var dx = segEnd.X - segStart.X;
            var dy = segEnd.Y - segStart.Y;
            var lenSq = (dx * dx) + (dy * dy);
            if (lenSq < 1e-6f)
                return Distance2D(point, segStart);

            var t = ((point.X - segStart.X) * dx + (point.Y - segStart.Y) * dy) / lenSq;
            t = Math.Clamp(t, 0f, 1f);

            var projX = segStart.X + (t * dx);
            var projY = segStart.Y + (t * dy);
            var ex = point.X - projX;
            var ey = point.Y - projY;
            return MathF.Sqrt((ex * ex) + (ey * ey));
        }

        private static float Distance2D(XYZ a, XYZ b)
        {
            var dx = a.X - b.X;
            var dy = a.Y - b.Y;
            return MathF.Sqrt((dx * dx) + (dy * dy));
        }

        private static string FormatPath(XYZ[] path)
        {
            if (path.Length == 0)
                return "Path: <empty>";

            var lines = new List<string>(path.Length + 1) { $"Path ({path.Length} points):" };
            for (int i = 0; i < path.Length; i++)
                lines.Add($"  [{i}] ({path[i].X:F1},{path[i].Y:F1},{path[i].Z:F1})");

            return string.Join(Environment.NewLine, lines);
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
