using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace BotRunner.Tests.LiveValidation.Harness;

/// <summary>
/// PFS-OVERHAUL-006 (2026-05-10) — bake-fixture JSON schema and loader for
/// the bake-validation harness. A fixture declares the expected geometric
/// shape of a route's smooth path and a sample of points where the bake
/// should and should NOT have walkable polygons. The harness teleports
/// the bot to each point, settles, and asserts both directions:
///
/// • <c>expectedWalkable</c> — point that should land on a real walkable
///   surface within <c>settleToleranceY</c>. Failure means a polygon
///   disappeared since the fixture was recorded → <c>BAKE_REGRESSION_WALKABLE_LOST</c>.
///
/// • <c>expectedHoles</c> — point that is intentionally NOT walkable. The
///   bot should fall to <c>expectedSettleZ</c>. Failure means a phantom
///   polygon appeared (the BRM Z=171.24 class of bug) → <c>PHANTOM_POLY</c>.
///
/// Fixtures live at
/// <c>Westworld of Warcraft/tools/MmapGen/test-fixtures/&lt;route&gt;.json</c>
/// and are resolved by walking up from the test base directory.
/// </summary>
public sealed record BakeFixture(
    [property: JsonPropertyName("route")] string Route,
    [property: JsonPropertyName("description")] string? Description,
    [property: JsonPropertyName("mapId")] uint MapId,
    [property: JsonPropertyName("agent")] BakeFixtureAgent? Agent,
    [property: JsonPropertyName("endpoints")] BakeFixtureEndpoints Endpoints,
    [property: JsonPropertyName("expectedWalkable")] IReadOnlyList<BakeFixtureCheckpoint> ExpectedWalkable,
    [property: JsonPropertyName("expectedHoles")] IReadOnlyList<BakeFixtureHole> ExpectedHoles,
    [property: JsonPropertyName("goldenSmoothPath")] BakeFixtureSmoothPathExpectation? GoldenSmoothPath,
    [property: JsonPropertyName("tileInvariants")] IReadOnlyDictionary<string, BakeFixtureTileInvariant>? TileInvariants);

public sealed record BakeFixtureAgent(
    [property: JsonPropertyName("race")] string? Race,
    [property: JsonPropertyName("radius")] float Radius,
    [property: JsonPropertyName("height")] float Height);

public sealed record BakeFixtureEndpoints(
    [property: JsonPropertyName("start")] float[] Start,
    [property: JsonPropertyName("dest")] float[] Dest);

public sealed record BakeFixtureCheckpoint(
    [property: JsonPropertyName("label")] string Label,
    [property: JsonPropertyName("xyz")] float[] Xyz,
    [property: JsonPropertyName("settleToleranceY")] float SettleToleranceY,
    [property: JsonPropertyName("note")] string? Note);

public sealed record BakeFixtureHole(
    [property: JsonPropertyName("label")] string Label,
    [property: JsonPropertyName("xyz")] float[] Xyz,
    [property: JsonPropertyName("expectedSettleZ")] float ExpectedSettleZ,
    [property: JsonPropertyName("settleToleranceY")] float SettleToleranceY,
    [property: JsonPropertyName("rationale")] string? Rationale);

public sealed record BakeFixtureSmoothPathExpectation(
    [property: JsonPropertyName("waypointCount")] int WaypointCount,
    [property: JsonPropertyName("waypointTolerance")] int WaypointTolerance,
    [property: JsonPropertyName("endpointToleranceY")] float EndpointToleranceY);

public sealed record BakeFixtureTileInvariant(
    [property: JsonPropertyName("minPolyCount")] int? MinPolyCount,
    [property: JsonPropertyName("maxPolyCount")] int? MaxPolyCount,
    [property: JsonPropertyName("note")] string? Note);

/// <summary>
/// Loads <see cref="BakeFixture"/> documents from
/// <c>tools/MmapGen/test-fixtures/&lt;route&gt;.json</c>. Mirrors the
/// resolution strategy used by <c>RouteManifestLoader</c>: walks up from
/// the test base directory looking for the requested file.
/// </summary>
public static class BakeFixtureLoader
{
    public const string FixtureDirRelative = "tools/MmapGen/test-fixtures";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    /// <summary>
    /// Loads a fixture by route identifier. The identifier is the file's
    /// stem — e.g. <c>"og-zeppelin"</c> resolves
    /// <c>&lt;repo&gt;/Westworld of Warcraft/tools/MmapGen/test-fixtures/og-zeppelin.json</c>.
    /// </summary>
    public static BakeFixture LoadByRoute(string routeId)
    {
        if (string.IsNullOrWhiteSpace(routeId))
            throw new ArgumentException("routeId must be a non-empty fixture stem", nameof(routeId));
        var relative = Path.Combine(FixtureDirRelative, routeId + ".json").Replace('\\', '/');
        return LoadFromRelativePath(relative);
    }

    public static BakeFixture LoadFromRelativePath(string relativePath)
    {
        var path = Resolve(relativePath);
        return LoadFromPath(path);
    }

    public static BakeFixture LoadFromPath(string absolutePath)
    {
        var json = File.ReadAllText(absolutePath);
        var fixture = Deserialize(json)
            ?? throw new InvalidOperationException(
                $"BakeFixtureLoader: failed to deserialize {absolutePath}");
        Validate(fixture, absolutePath);
        return fixture;
    }

    /// <summary>
    /// Deserializes a fixture from a JSON string. Used by tests; production
    /// callers should use <see cref="LoadByRoute"/>.
    /// </summary>
    public static BakeFixture? Deserialize(string json)
        => JsonSerializer.Deserialize<BakeFixture>(json, JsonOptions);

    public static string Serialize(BakeFixture fixture)
    {
        var opts = new JsonSerializerOptions(JsonOptions)
        {
            WriteIndented = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        };
        return JsonSerializer.Serialize(fixture, opts);
    }

    private static void Validate(BakeFixture fixture, string source)
    {
        if (string.IsNullOrWhiteSpace(fixture.Route))
            throw new InvalidOperationException($"{source}: 'route' is required.");
        if (fixture.Endpoints == null)
            throw new InvalidOperationException($"{source}: 'endpoints' is required.");
        if (fixture.Endpoints.Start is not { Length: 3 })
            throw new InvalidOperationException($"{source}: 'endpoints.start' must be [x,y,z].");
        if (fixture.Endpoints.Dest is not { Length: 3 })
            throw new InvalidOperationException($"{source}: 'endpoints.dest' must be [x,y,z].");
        if (fixture.ExpectedWalkable == null)
            throw new InvalidOperationException($"{source}: 'expectedWalkable' is required (may be empty array).");
        if (fixture.ExpectedHoles == null)
            throw new InvalidOperationException($"{source}: 'expectedHoles' is required (may be empty array).");

        for (int i = 0; i < fixture.ExpectedWalkable.Count; i++)
        {
            var c = fixture.ExpectedWalkable[i];
            if (string.IsNullOrWhiteSpace(c.Label))
                throw new InvalidOperationException($"{source}: expectedWalkable[{i}].label is required.");
            if (c.Xyz is not { Length: 3 })
                throw new InvalidOperationException($"{source}: expectedWalkable[{i}].xyz must be [x,y,z].");
            if (c.SettleToleranceY <= 0f)
                throw new InvalidOperationException($"{source}: expectedWalkable[{i}].settleToleranceY must be > 0.");
        }

        for (int i = 0; i < fixture.ExpectedHoles.Count; i++)
        {
            var h = fixture.ExpectedHoles[i];
            if (string.IsNullOrWhiteSpace(h.Label))
                throw new InvalidOperationException($"{source}: expectedHoles[{i}].label is required.");
            if (h.Xyz is not { Length: 3 })
                throw new InvalidOperationException($"{source}: expectedHoles[{i}].xyz must be [x,y,z].");
            if (h.SettleToleranceY <= 0f)
                throw new InvalidOperationException($"{source}: expectedHoles[{i}].settleToleranceY must be > 0.");
        }
    }

    private static string Resolve(string relativePath)
    {
        var baseDir = AppContext.BaseDirectory;
        var dir = new DirectoryInfo(baseDir);
        while (dir != null)
        {
            var candidate = Path.Combine(dir.FullName, relativePath);
            if (File.Exists(candidate))
                return candidate;
            dir = dir.Parent;
        }
        throw new FileNotFoundException(
            $"BakeFixtureLoader: '{relativePath}' not found anywhere from '{baseDir}' up. " +
            "Fixtures live under <repo>/Westworld of Warcraft/tools/MmapGen/test-fixtures/.");
    }
}
