// GeometryExtractionTests.cs - Tests that extract and validate actual game geometry
// These tests require map data to be available.

namespace Navigation.Physics.Tests;

using static NavigationInterop;
using static GeometryTestHelpers;

/// <summary>
/// Tests that extract geometry from actual WMO/ADT data for validation.
/// These tests require the game data files to be available.
/// </summary>
public class GeometryExtractionTests
{
    // Known test locations in WoW that have specific geometry characteristics

    /// <summary>
    /// Elwynn Forest - Northshire Abbey area
    /// Map ID: 0 (Eastern Kingdoms)
    /// </summary>
    private static class NorthshireAbbey
    {
        public const uint MapId = 0;
        public const float CenterX = -8949.95f;
        public const float CenterY = -132.493f;
        public const float GroundZ = 83.5312f;
        public const uint TileX = 48;
        public const uint TileY = 31;
    }

    /// <summary>
    /// Durotar - Valley of Trials area
    /// Map ID: 1 (Kalimdor)
    /// </summary>
    private static class ValleyOfTrials
    {
        public const uint MapId = 1;
        public const float CenterX = -618.518f;
        public const float CenterY = -4251.67f;
        public const float GroundZ = 38.718f;
        public const uint TileX = 35;
        public const uint TileY = 33;
    }

    // ==========================================================================
    // TERRAIN TRIANGLE EXTRACTION TESTS
    // ==========================================================================

    [Fact(Skip = "Requires game data files")]
    public void ExtractTerrainTriangles_NorthshireAbbey_ReturnsValidGeometry()
    {
        // Arrange
        Assert.True(InitializeMapLoader("maps/"), "Map loader should initialize");
        Assert.True(LoadMapTile(NorthshireAbbey.MapId, NorthshireAbbey.TileX, NorthshireAbbey.TileY),
            "Should load Northshire Abbey tile");

        const float queryRadius = 10.0f;
        var triangles = new TerrainTriangle[256];

        // Act
        int count = QueryTerrainTriangles(
            NorthshireAbbey.MapId,
            NorthshireAbbey.CenterX - queryRadius,
            NorthshireAbbey.CenterY - queryRadius,
            NorthshireAbbey.CenterX + queryRadius,
            NorthshireAbbey.CenterY + queryRadius,
            triangles,
            triangles.Length);

        // Assert
        Assert.True(count > 0, "Should find terrain triangles");
        
        // Validate triangle normals - terrain should mostly be walkable
        int walkableCount = 0;
        for (int i = 0; i < count; i++)
        {
            var tri = triangles[i].ToTriangle();
            var normal = tri.Normal();
            
            if (IsWalkableNormal(normal))
                walkableCount++;
            
            // Validate triangle is not degenerate
            float area = CalculateTriangleArea(tri);
            Assert.True(area > 0.001f, $"Triangle {i} should not be degenerate");
        }

        // Most terrain should be walkable in this area
        float walkableRatio = (float)walkableCount / count;
        Assert.True(walkableRatio > 0.7f, 
            $"Expected >70% walkable terrain, got {walkableRatio:P}");
    }

    [Fact(Skip = "Requires game data files")]
    public void ExtractTerrainTriangles_VerifySlopeAngles()
    {
        // This test extracts terrain and bins triangles by slope angle

        Assert.True(InitializeMapLoader("maps/"));
        Assert.True(LoadMapTile(NorthshireAbbey.MapId, NorthshireAbbey.TileX, NorthshireAbbey.TileY));

        var triangles = new TerrainTriangle[1024];
        int count = QueryTerrainTriangles(
            NorthshireAbbey.MapId,
            NorthshireAbbey.CenterX - 50,
            NorthshireAbbey.CenterY - 50,
            NorthshireAbbey.CenterX + 50,
            NorthshireAbbey.CenterY + 50,
            triangles,
            triangles.Length);

        // Bin by slope angle (in 5 degree increments)
        var slopeBins = new int[18]; // 0-5, 5-10, ..., 85-90

        for (int i = 0; i < count; i++)
        {
            var tri = triangles[i].ToTriangle();
            var normal = tri.Normal();
            
            // Calculate slope angle from normal
            float slopeAngle = MathF.Acos(MathF.Abs(normal.Z)) * 180.0f / MathF.PI;
            int bin = (int)(slopeAngle / 5.0f);
            if (bin >= slopeBins.Length) bin = slopeBins.Length - 1;
            slopeBins[bin]++;
        }

        // The 60 degree threshold should encompass most walkable terrain
        int walkableCount = 0;
        for (int i = 0; i < 12; i++) // 0-60 degrees
            walkableCount += slopeBins[i];
        
        Assert.True(count > 0);
        float walkableRatio = (float)walkableCount / count;
        Assert.True(walkableRatio > 0.9f, $"Expected >90% under 60 degrees, got {walkableRatio:P}");
    }

    // ==========================================================================
    // TERRAIN HEIGHT TESTS
    // ==========================================================================

    [Fact(Skip = "Requires game data files")]
    public void GetTerrainHeight_AtKnownLocation_ReturnsExpectedValue()
    {
        // Arrange
        Assert.True(InitializeMapLoader("maps/"));
        Assert.True(LoadMapTile(NorthshireAbbey.MapId, NorthshireAbbey.TileX, NorthshireAbbey.TileY));

        // Act
        float height = GetTerrainHeight(
            NorthshireAbbey.MapId,
            NorthshireAbbey.CenterX,
            NorthshireAbbey.CenterY);

        // Assert
        Assert.True(MathF.Abs(height - NorthshireAbbey.GroundZ) < 1.0f,
            $"Height should be near {NorthshireAbbey.GroundZ}, got {height}");
    }

    // ==========================================================================
    // SLOPE DISTRIBUTION ANALYSIS
    // ==========================================================================

    [Fact(Skip = "Requires game data files")]
    public void TerrainTriangles_MeasureSlopeDistribution_ForWalkabilityCalibration()
    {
        // This test analyzes real terrain to help calibrate the walkability threshold
        
        Assert.True(InitializeMapLoader("maps/"));
        
        var tilesToTest = new[]
        {
            (NorthshireAbbey.MapId, NorthshireAbbey.TileX, NorthshireAbbey.TileY),
        };

        var allSlopes = new List<float>();
        
        foreach (var (mapId, tileX, tileY) in tilesToTest)
        {
            if (!LoadMapTile(mapId, tileX, tileY))
                continue;

            var triangles = new TerrainTriangle[4096];
            int count = QueryTerrainTriangles(
                mapId, -100000, -100000, 100000, 100000,
                triangles, triangles.Length);

            for (int i = 0; i < count; i++)
            {
                var normal = triangles[i].ToTriangle().Normal();
                float slopeAngle = MathF.Acos(MathF.Abs(normal.Z)) * 180.0f / MathF.PI;
                allSlopes.Add(slopeAngle);
            }
        }

        if (allSlopes.Count > 0)
        {
            allSlopes.Sort();
            
            // The 60 degree threshold should encompass the vast majority of walkable terrain
            int below60 = allSlopes.Count(s => s < 60);
            float percentBelow60 = (float)below60 / allSlopes.Count;
            
            Assert.True(percentBelow60 > 0.95f,
                $"Expected >95% of terrain under 60 degrees, got {percentBelow60:P}");
        }
    }

    // ==========================================================================
    // HELPER METHODS
    // ==========================================================================

    private static float CalculateTriangleArea(Triangle tri)
    {
        var ab = tri.B - tri.A;
        var ac = tri.C - tri.A;
        var cross = Vector3.Cross(ab, ac);
        return cross.Length() * 0.5f;
    }
}
