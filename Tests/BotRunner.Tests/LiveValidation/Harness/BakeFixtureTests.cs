using System;
using System.IO;
using BotRunner.Tests.LiveValidation.Harness;
using Xunit;

namespace BotRunner.Tests.LiveValidation;

/// <summary>
/// Unit tests for <see cref="BakeFixtureLoader"/>. Covers schema parsing,
/// validation errors, on-disk fixture round-trip, and the two seed
/// fixtures shipped with this commit (og-zeppelin, flamecrest-to-brm).
/// </summary>
public class BakeFixtureTests
{
    [Fact]
    public void Deserialize_FullExample_RoundTrips()
    {
        const string json = @"
        {
          ""route"": ""SampleRoute"",
          ""description"": ""sanity check"",
          ""mapId"": 0,
          ""agent"": { ""race"": ""TaurenMale"", ""radius"": 1.0247, ""height"": 2.625 },
          ""endpoints"": {
            ""start"": [-7518.7, -2159.9, 131.9],
            ""dest"":  [-7524.0, -1233.0, 287.0]
          },
          ""expectedWalkable"": [
            { ""label"": ""trail-mid"", ""xyz"": [-7600, -1500, 200], ""settleToleranceY"": 0.5 }
          ],
          ""expectedHoles"": [
            {
              ""label"": ""south-cliff-face"",
              ""xyz"": [-7949.7, -1162.8, 170.8],
              ""expectedSettleZ"": 158.4,
              ""settleToleranceY"": 1.0,
              ""rationale"": ""cliff face, no platform""
            }
          ],
          ""goldenSmoothPath"": {
            ""waypointCount"": 1072,
            ""waypointTolerance"": 5,
            ""endpointToleranceY"": 1.0
          },
          ""tileInvariants"": {
            ""0004634"": { ""minPolyCount"": 11800, ""maxPolyCount"": 12100, ""note"": ""BRM south face"" }
          }
        }";

        var fixture = BakeFixtureLoader.Deserialize(json);

        Assert.NotNull(fixture);
        Assert.Equal("SampleRoute", fixture!.Route);
        Assert.Equal(0u, fixture.MapId);
        Assert.NotNull(fixture.Agent);
        Assert.Equal("TaurenMale", fixture.Agent!.Race);
        Assert.Equal(1.0247f, fixture.Agent.Radius, 4);
        Assert.Equal(3, fixture.Endpoints.Start.Length);
        Assert.Equal(-7524.0f, fixture.Endpoints.Dest[0], 4);
        Assert.Single(fixture.ExpectedWalkable);
        Assert.Equal("trail-mid", fixture.ExpectedWalkable[0].Label);
        Assert.Single(fixture.ExpectedHoles);
        Assert.Equal(158.4f, fixture.ExpectedHoles[0].ExpectedSettleZ, 4);
        Assert.NotNull(fixture.GoldenSmoothPath);
        Assert.Equal(1072, fixture.GoldenSmoothPath!.WaypointCount);
        Assert.NotNull(fixture.TileInvariants);
        Assert.Contains("0004634", fixture.TileInvariants!.Keys);
        Assert.Equal(11800, fixture.TileInvariants["0004634"].MinPolyCount);
    }

    [Fact]
    public void Deserialize_AllowsCommentsAndTrailingCommas()
    {
        const string json = @"
        {
          // human-friendly comment
          ""route"": ""WithComments"",
          ""mapId"": 0,
          ""endpoints"": {
            ""start"": [0, 0, 0],
            ""dest"":  [10, 0, 0],
          },
          ""expectedWalkable"": [],
          ""expectedHoles"": [],
        }";

        var fixture = BakeFixtureLoader.Deserialize(json);
        Assert.NotNull(fixture);
        Assert.Equal("WithComments", fixture!.Route);
    }

    [Fact]
    public void Validate_MissingEndpoints_Throws()
    {
        const string json = @"
        {
          ""route"": ""NoEndpoints"",
          ""mapId"": 0,
          ""endpoints"": null,
          ""expectedWalkable"": [],
          ""expectedHoles"": []
        }";

        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".json");
        File.WriteAllText(path, json);
        try
        {
            Assert.Throws<InvalidOperationException>(() => BakeFixtureLoader.LoadFromPath(path));
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void Validate_BadXyzLength_Throws()
    {
        const string json = @"
        {
          ""route"": ""BadXyz"",
          ""mapId"": 0,
          ""endpoints"": { ""start"": [0,0,0], ""dest"": [1,1,1] },
          ""expectedWalkable"": [
            { ""label"": ""only-two-coords"", ""xyz"": [1,2], ""settleToleranceY"": 0.5 }
          ],
          ""expectedHoles"": []
        }";

        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".json");
        File.WriteAllText(path, json);
        try
        {
            var ex = Assert.Throws<InvalidOperationException>(() => BakeFixtureLoader.LoadFromPath(path));
            Assert.Contains("expectedWalkable[0].xyz", ex.Message);
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void Validate_NonPositiveTolerance_Throws()
    {
        const string json = @"
        {
          ""route"": ""ZeroTol"",
          ""mapId"": 0,
          ""endpoints"": { ""start"": [0,0,0], ""dest"": [1,1,1] },
          ""expectedWalkable"": [
            { ""label"": ""bad-tol"", ""xyz"": [1,2,3], ""settleToleranceY"": 0 }
          ],
          ""expectedHoles"": []
        }";

        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".json");
        File.WriteAllText(path, json);
        try
        {
            var ex = Assert.Throws<InvalidOperationException>(() => BakeFixtureLoader.LoadFromPath(path));
            Assert.Contains("settleToleranceY", ex.Message);
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void Validate_MissingLabel_Throws()
    {
        const string json = @"
        {
          ""route"": ""NoLabel"",
          ""mapId"": 0,
          ""endpoints"": { ""start"": [0,0,0], ""dest"": [1,1,1] },
          ""expectedWalkable"": [],
          ""expectedHoles"": [
            { ""label"": """", ""xyz"": [1,2,3], ""expectedSettleZ"": 1.0, ""settleToleranceY"": 0.5 }
          ]
        }";

        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".json");
        File.WriteAllText(path, json);
        try
        {
            var ex = Assert.Throws<InvalidOperationException>(() => BakeFixtureLoader.LoadFromPath(path));
            Assert.Contains("expectedHoles[0].label", ex.Message);
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void LoadByRoute_OgZeppelin_FixturePresent()
    {
        var fixture = BakeFixtureLoader.LoadByRoute("og-zeppelin");

        Assert.Equal("ClimbOrgrimmarTowerToFrezza", fixture.Route);
        Assert.Equal(1u, fixture.MapId);
        Assert.NotEmpty(fixture.ExpectedWalkable);
        // Sanity: at least one of the OgRampWaypointInspect candidate coords
        // is preserved as a seed.
        Assert.Contains(fixture.ExpectedWalkable, c => c.Label.StartsWith("smooth-wp", StringComparison.Ordinal));
    }

    [Fact]
    public void LoadByRoute_FlameCrestBrm_FixturePresent()
    {
        var fixture = BakeFixtureLoader.LoadByRoute("flamecrest-to-brm");

        Assert.Equal("FlameCrestToBrmDungeonEntrance", fixture.Route);
        Assert.Equal(0u, fixture.MapId);
        Assert.NotEmpty(fixture.ExpectedWalkable);
        Assert.NotEmpty(fixture.ExpectedHoles);
        // The BRM south-face WMO trap is the canonical phantom-poly hole.
        Assert.Contains(fixture.ExpectedHoles, h => h.Label.Contains("brm-south", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Serialize_RoundTripsExactly()
    {
        var original = new BakeFixture(
            Route: "RT",
            Description: "round-trip",
            MapId: 0,
            Agent: new BakeFixtureAgent("TaurenMale", 1.0247f, 2.625f),
            Endpoints: new BakeFixtureEndpoints(new[] { 0f, 0f, 0f }, new[] { 1f, 1f, 1f }),
            ExpectedWalkable: new[]
            {
                new BakeFixtureCheckpoint("wp-0", new[] { 1f, 2f, 3f }, 0.5f, "test"),
            },
            ExpectedHoles: new[]
            {
                new BakeFixtureHole("hole-0", new[] { 4f, 5f, 6f }, 1.5f, 0.75f, "the void"),
            },
            GoldenSmoothPath: new BakeFixtureSmoothPathExpectation(100, 5, 1f),
            TileInvariants: null);

        var json = BakeFixtureLoader.Serialize(original);
        var rehydrated = BakeFixtureLoader.Deserialize(json);

        Assert.NotNull(rehydrated);
        Assert.Equal(original.Route, rehydrated!.Route);
        Assert.Equal(original.Endpoints.Start, rehydrated.Endpoints.Start);
        Assert.Equal(original.ExpectedWalkable[0].Label, rehydrated.ExpectedWalkable[0].Label);
        Assert.Equal(original.ExpectedHoles[0].ExpectedSettleZ, rehydrated.ExpectedHoles[0].ExpectedSettleZ, 4);
    }
}
