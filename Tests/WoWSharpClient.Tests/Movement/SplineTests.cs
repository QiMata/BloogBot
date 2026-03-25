using GameData.Core.Enums;
using GameData.Core.Models;
using System.Collections.Concurrent;
using WoWSharpClient.Movement;

namespace WoWSharpClient.Tests.Movement
{
    public class SplineTests
    {
        [Fact]
        public void Constructor_StoresAllProperties()
        {
            var points = new List<Position>
            {
                new(0, 0, 0),
                new(10, 0, 0),
                new(20, 0, 0)
            };

            var spline = new Spline(42UL, 1, 5000, SplineFlags.Runmode, points, 3000);

            Assert.Equal(42UL, spline.OwnerGuid);
            Assert.Equal(1u, spline.Id);
            Assert.Equal(5000u, spline.StartMs);
            Assert.Equal(SplineFlags.Runmode, spline.Flags);
            Assert.Equal(3, spline.Points.Count);
            Assert.Equal(3000u, spline.DurationMs);
        }

        [Fact]
        public void Points_AreReadOnly()
        {
            var points = new List<Position>
            {
                new(0, 0, 0),
                new(10, 0, 0)
            };
            var spline = new Spline(1, 1, 0, SplineFlags.None, points, 1000);

            Assert.IsAssignableFrom<IReadOnlyList<Position>>(spline.Points);
        }

        [Fact]
        public void Flags_CanCombineMultiple()
        {
            var points = new List<Position> { new(0, 0, 0), new(10, 10, 10) };
            var spline = new Spline(1, 1, 0,
                SplineFlags.Flying | SplineFlags.Cyclic,
                points, 5000);

            Assert.True(spline.Flags.HasFlag(SplineFlags.Flying));
            Assert.True(spline.Flags.HasFlag(SplineFlags.Cyclic));
        }

        [Fact]
        public void DurationMs_StoresCorrectValue()
        {
            var points = new List<Position> { new(0, 0, 0), new(100, 0, 0) };
            var spline = new Spline(1, 1, 0, SplineFlags.None, points, 134139);

            Assert.Equal(134139u, spline.DurationMs);
        }

        [Fact]
        public void StartMs_StoresServerTimestamp()
        {
            var points = new List<Position> { new(0, 0, 0), new(10, 0, 0) };
            var spline = new Spline(1, 1, 500000, SplineFlags.None, points, 1000);

            Assert.Equal(500000u, spline.StartMs);
        }

        [Fact]
        public void Points_MatchInput()
        {
            var points = new List<Position>
            {
                new(-439.7f, -2595.2f, 99.9f),
                new(-500.0f, -2600.0f, 110.0f),
                new(-600.0f, -2700.0f, 120.0f)
            };
            var spline = new Spline(1, 1, 0, SplineFlags.None, points, 5000);

            Assert.Equal(3, spline.Points.Count);
            Assert.Equal(-439.7f, spline.Points[0].X);
            Assert.Equal(-2595.2f, spline.Points[0].Y);
            Assert.Equal(99.9f, spline.Points[0].Z);
            Assert.Equal(-600.0f, spline.Points[2].X);
        }
    }

    public class SplineControllerRegistryTests
    {
        [Fact]
        public void AddOrUpdate_NewEntry_DoesNotThrow()
        {
            var controller = new SplineController();
            var points = new List<Position> { new(0, 0, 0), new(10, 0, 0) };
            var spline = new Spline(42, 1, 0, SplineFlags.None, points, 1000);

            controller.AddOrUpdate(spline);
        }

        [Fact]
        public void AddOrUpdate_SameGuid_ReplacesExisting()
        {
            var controller = new SplineController();
            var points1 = new List<Position> { new(0, 0, 0), new(10, 0, 0) };
            var points2 = new List<Position> { new(50, 50, 50), new(100, 100, 100) };

            controller.AddOrUpdate(new Spline(42, 1, 0, SplineFlags.None, points1, 1000));
            controller.AddOrUpdate(new Spline(42, 2, 0, SplineFlags.None, points2, 2000));
        }

        [Fact]
        public void Remove_ExistingGuid_DoesNotThrow()
        {
            var controller = new SplineController();
            var points = new List<Position> { new(0, 0, 0), new(10, 0, 0) };
            controller.AddOrUpdate(new Spline(42, 1, 0, SplineFlags.None, points, 1000));

            controller.Remove(42);
        }

        [Fact]
        public void Remove_NonExistentGuid_DoesNotThrow()
        {
            var controller = new SplineController();
            controller.Remove(999);
        }

        [Fact]
        public void MultipleSplines_IndependentGuids()
        {
            var controller = new SplineController();
            var points = new List<Position> { new(0, 0, 0), new(10, 0, 0) };

            controller.AddOrUpdate(new Spline(1, 1, 0, SplineFlags.None, points, 1000));
            controller.AddOrUpdate(new Spline(2, 2, 0, SplineFlags.None, points, 2000));
            controller.AddOrUpdate(new Spline(3, 3, 0, SplineFlags.None, points, 3000));

            controller.Remove(2);
        }

        [Fact]
        public void HasActiveSpline_TrueAfterAdd()
        {
            var controller = new SplineController();
            var points = new List<Position> { new(0, 0, 0), new(10, 0, 0) };
            controller.AddOrUpdate(new Spline(42, 1, 0, SplineFlags.None, points, 1000));

            Assert.True(controller.HasActiveSpline(42));
        }

        [Fact]
        public void HasActiveSpline_FalseForUnknownGuid()
        {
            var controller = new SplineController();

            Assert.False(controller.HasActiveSpline(999));
        }

        [Fact]
        public void HasActiveSpline_FalseAfterRemove()
        {
            var controller = new SplineController();
            var points = new List<Position> { new(0, 0, 0), new(10, 0, 0) };
            controller.AddOrUpdate(new Spline(42, 1, 0, SplineFlags.None, points, 1000));
            controller.Remove(42);

            Assert.False(controller.HasActiveSpline(42));
        }

        [Fact]
        public void HasActiveSpline_TrueAfterReplacement()
        {
            var controller = new SplineController();
            var points1 = new List<Position> { new(0, 0, 0), new(10, 0, 0) };
            var points2 = new List<Position> { new(50, 50, 50), new(100, 100, 100) };

            controller.AddOrUpdate(new Spline(42, 1, 0, SplineFlags.None, points1, 1000));
            controller.AddOrUpdate(new Spline(42, 2, 0, SplineFlags.None, points2, 2000));

            Assert.True(controller.HasActiveSpline(42));
        }

        [Fact]
        public void Remove_FiresOnSplineCompleted()
        {
            var controller = new SplineController();
            var points = new List<Position> { new(0, 0, 0), new(10, 0, 0) };
            controller.AddOrUpdate(new Spline(42, 1, 0, SplineFlags.None, points, 1000));

            ulong? completedGuid = null;
            controller.OnSplineCompleted += guid => completedGuid = guid;

            controller.Remove(42);

            Assert.Equal(42UL, completedGuid);
        }

        [Fact]
        public void Remove_NonExistent_DoesNotFireEvent()
        {
            var controller = new SplineController();
            bool eventFired = false;
            controller.OnSplineCompleted += _ => eventFired = true;

            controller.Remove(999);

            Assert.False(eventFired);
        }

        [Fact]
        public void Remove_OnlyRemovesTargetGuid()
        {
            var controller = new SplineController();
            var points = new List<Position> { new(0, 0, 0), new(10, 0, 0) };
            controller.AddOrUpdate(new Spline(1, 1, 0, SplineFlags.None, points, 1000));
            controller.AddOrUpdate(new Spline(2, 2, 0, SplineFlags.None, points, 1000));

            controller.Remove(1);

            Assert.False(controller.HasActiveSpline(1));
            Assert.True(controller.HasActiveSpline(2));
        }

        [Fact]
        public async Task Update_ConcurrentMutation_DoesNotThrow()
        {
            var controller = new SplineController();
            var points = new List<Position> { new(0, 0, 0) };
            var failures = new ConcurrentQueue<Exception>();
            using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(250));

            var updater = Task.Run(() =>
            {
                while (!cts.IsCancellationRequested)
                {
                    try
                    {
                        controller.Update(16f);
                    }
                    catch (Exception ex)
                    {
                        failures.Enqueue(ex);
                        cts.Cancel();
                    }
                }
            });

            var writers = Enumerable.Range(0, 4).Select(worker => Task.Run(() =>
            {
                ulong guid = (ulong)(worker + 1);
                while (!cts.IsCancellationRequested)
                {
                    try
                    {
                        controller.AddOrUpdate(new Spline(guid, 1, 0, SplineFlags.None, points, 0));
                        controller.Remove(guid);
                        guid += 4;
                        if (guid > 128)
                            guid = (ulong)(worker + 1);
                    }
                    catch (Exception ex)
                    {
                        failures.Enqueue(ex);
                        cts.Cancel();
                    }
                }
            })).ToArray();

            await Task.WhenAll(writers.Append(updater));

            Assert.Empty(failures);
        }
    }
}
