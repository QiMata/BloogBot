using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace PathfindingService.Tests.WaypointGeneration;

/// <summary>
/// JSON DTO for the canonical route manifests under
/// <c>tools/scripts/routes/</c>. Used by both the PowerShell probe-routes
/// orchestrator (<c>tools/scripts/probe-routes.ps1</c>) and the C#
/// WaypointGenerationTests, so a single source of truth governs which
/// (start,end) pairs the bake fix needs to make walkable.
/// </summary>
internal sealed record RouteManifest(
    [property: JsonPropertyName("schemaVersion")] int SchemaVersion,
    [property: JsonPropertyName("description")] string? Description,
    [property: JsonPropertyName("agent")] AgentConfig Agent,
    [property: JsonPropertyName("routes")] IReadOnlyList<RouteEntry> Routes);

internal sealed record AgentConfig(
    [property: JsonPropertyName("race")] string? Race,
    [property: JsonPropertyName("radius")] float Radius,
    [property: JsonPropertyName("height")] float Height);

internal sealed record RouteEntry(
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("description")] string? Description,
    [property: JsonPropertyName("map")] uint Map,
    [property: JsonPropertyName("start")] float[] Start,
    [property: JsonPropertyName("end")] float[] End,
    [property: JsonPropertyName("tilesAffected")] int[][]? TilesAffected,
    [property: JsonPropertyName("expectedCorners")] string? ExpectedCorners,
    [property: JsonPropertyName("expectedAffordance")] string? ExpectedAffordance,
    [property: JsonPropertyName("expectedFailureSegment")] string? ExpectedFailureSegment,
    [property: JsonPropertyName("expectedStallCoord")] float[]? ExpectedStallCoord,
    [property: JsonPropertyName("expectedStallReason")] string? ExpectedStallReason);

internal static class RouteManifestLoader
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    public static RouteManifest Load(string relativePath)
    {
        var path = Resolve(relativePath);
        var json = File.ReadAllText(path);
        var manifest = JsonSerializer.Deserialize<RouteManifest>(json, Options)
            ?? throw new InvalidOperationException($"RouteManifestLoader: failed to deserialize {path}");
        if (manifest.Routes is null || manifest.Routes.Count == 0)
            throw new InvalidOperationException($"RouteManifestLoader: {path} has no routes");
        return manifest;
    }

    /// <summary>
    /// Walks up from the test base directory looking for the requested
    /// manifest file. The probe-routes.ps1 script uses repo-relative paths
    /// like <c>tools/scripts/routes/og-zeppelin.json</c>; we mirror that.
    /// </summary>
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
            $"RouteManifestLoader: '{relativePath}' not found anywhere from '{baseDir}' up. "
            + "The manifest is expected at <repo-root>/Westworld of Warcraft/tools/scripts/routes/.");
    }
}
