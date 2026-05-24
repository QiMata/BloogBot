using System.Text.Json;
using NavDataAudit;

namespace PathfindingService.Tests;

public sealed class AnchorStageManifestAnalyzerTests
{
    [Fact]
    public void Analyze_ReportsFirstBadStageWhenLowerCompetitorWinsAtRegions()
    {
        const string manifestJson = """
        {
          "schemaVersion": 1,
          "mapId": 1,
          "tileX": 40,
          "tileY": 29,
          "anchors": [
            {
              "id": "hallway",
              "label": "1518.200,-4419.800,17.100",
              "wowX": 1518.2,
              "wowY": -4419.8,
              "wowZ": 17.1,
              "sourceSupport": {
                "found": true
              },
              "stages": [
                { "name": "rasterize", "upperSupportExists": true, "dominantLowerCandidate": false },
                { "name": "filterLowHanging", "upperSupportExists": true, "dominantLowerCandidate": false },
                { "name": "filterLedge", "upperSupportExists": true, "dominantLowerCandidate": false },
                { "name": "removeUseless", "upperSupportExists": true, "dominantLowerCandidate": false },
                { "name": "filterLowHeight", "upperSupportExists": true, "dominantLowerCandidate": false },
                { "name": "waterInheritance", "upperSupportExists": true, "dominantLowerCandidate": false },
                { "name": "buildCHF", "upperSupportExists": true, "dominantLowerCandidate": false },
                { "name": "markGameObjects", "upperSupportExists": true, "dominantLowerCandidate": false },
                { "name": "erode", "upperSupportExists": true, "dominantLowerCandidate": false },
                { "name": "median", "upperSupportExists": true, "dominantLowerCandidate": false },
                { "name": "regions", "upperSupportExists": true, "dominantLowerCandidate": true },
                { "name": "contours", "upperSupportExists": true, "dominantLowerCandidate": true },
                { "name": "polymesh", "upperSupportExists": true, "dominantLowerCandidate": true },
                {
                  "name": "finalDetour",
                  "upperSupportExists": true,
                  "dominantLowerCandidate": true,
                  "supportCandidateCount": 1,
                  "supportComponentCount": 2,
                  "lowerComponentCount": 1,
                  "finalWinner": {
                    "polyRef": "0x100001520ADA2",
                    "componentId": 7,
                    "componentPolyCount": 12,
                    "supportCandidate": false,
                    "competingLower": true
                  }
                }
              ]
            }
          ]
        }
        """;

        using var document = JsonDocument.Parse(manifestJson);
        var summary = StageManifestAnalyzer.Analyze(document);
        var anchor = Assert.Single(summary.Anchors);

        Assert.True(anchor.CoverageComplete);
        Assert.Equal("regions", anchor.FirstBadStage);
        Assert.Equal("lower_competitor_dominant", anchor.FirstBadReason);
        Assert.Equal("0x100001520ADA2", anchor.FinalWinnerPolyRef);
        Assert.Equal(7, anchor.FinalWinnerComponentId);
        Assert.Equal(12, anchor.FinalWinnerComponentPolyCount);
        Assert.True(anchor.FinalWinnerCompetingLower);
        Assert.False(anchor.FinalWinnerSupportCandidate);
        Assert.Equal(2, anchor.FinalSupportComponentCount);
        Assert.Equal(1, anchor.FinalLowerComponentCount);
    }

    [Fact]
    public void Analyze_ReportsMissingStagesAsCoverageFailure()
    {
        const string manifestJson = """
        {
          "schemaVersion": 1,
          "anchors": [
            {
              "id": "vertical",
              "label": "1545.000,-4434.500,11.100",
              "wowX": 1545.0,
              "wowY": -4434.5,
              "wowZ": 11.1,
              "sourceSupport": {
                "found": false
              },
              "stages": [
                { "name": "rasterize", "upperSupportExists": false, "dominantLowerCandidate": false }
              ]
            }
          ]
        }
        """;

        using var document = JsonDocument.Parse(manifestJson);
        var summary = StageManifestAnalyzer.Analyze(document);
        var anchor = Assert.Single(summary.Anchors);

        Assert.False(anchor.CoverageComplete);
        Assert.Equal("sourceSupport", anchor.FirstBadStage);
        Assert.Equal("no_source_support_probe", anchor.FirstBadReason);
        Assert.Contains("finalDetour", anchor.MissingStages);
    }

    [Fact]
    public void Analyze_ReportsFinalDetourWhenSupportSurvivesBakeButWinnerDropsToLowerBasin()
    {
        const string manifestJson = """
        {
          "schemaVersion": 1,
          "anchors": [
            {
              "id": "vertical-underpass",
              "label": "1546.600,-4435.900,11.500",
              "wowX": 1546.6,
              "wowY": -4435.9,
              "wowZ": 11.5,
              "sourceSupport": {
                "found": true
              },
              "stages": [
                { "name": "rasterize", "upperSupportExists": true, "dominantLowerCandidate": false },
                { "name": "filterLowHanging", "upperSupportExists": true, "dominantLowerCandidate": false },
                { "name": "filterLedge", "upperSupportExists": true, "dominantLowerCandidate": false },
                { "name": "removeUseless", "upperSupportExists": true, "dominantLowerCandidate": false },
                { "name": "filterLowHeight", "upperSupportExists": true, "dominantLowerCandidate": false },
                { "name": "waterInheritance", "upperSupportExists": true, "dominantLowerCandidate": false },
                { "name": "buildCHF", "upperSupportExists": true, "dominantLowerCandidate": false },
                { "name": "markGameObjects", "upperSupportExists": true, "dominantLowerCandidate": false },
                { "name": "erode", "upperSupportExists": true, "dominantLowerCandidate": false },
                { "name": "median", "upperSupportExists": true, "dominantLowerCandidate": false },
                { "name": "regions", "upperSupportExists": true, "dominantLowerCandidate": false },
                { "name": "contours", "upperSupportExists": true, "dominantLowerCandidate": false },
                { "name": "polymesh", "upperSupportExists": true, "dominantLowerCandidate": false },
                {
                  "name": "finalDetour",
                  "upperSupportExists": true,
                  "dominantLowerCandidate": true,
                  "supportCandidateCount": 0,
                  "supportBandCandidateCount": 1,
                  "finalWinner": {
                    "polyRef": "0x1000000000BE35",
                    "supportCandidate": false,
                    "competingLower": true
                  }
                }
              ]
            }
          ]
        }
        """;

        using var document = JsonDocument.Parse(manifestJson);
        var summary = StageManifestAnalyzer.Analyze(document);
        var anchor = Assert.Single(summary.Anchors);

        Assert.True(anchor.CoverageComplete);
        Assert.Equal("finalDetour", anchor.FirstBadStage);
        Assert.Equal("lower_competitor_dominant", anchor.FirstBadReason);
        Assert.Equal("0x1000000000BE35", anchor.FinalWinnerPolyRef);
        Assert.False(anchor.FinalWinnerSupportCandidate);
        Assert.True(anchor.FinalWinnerCompetingLower);
    }

    [Fact]
    public void Analyze_ReportsSupportFootprintMissWhenSupportSurvivesNearbyButNotAtAnchor()
    {
        const string manifestJson = """
        {
          "schemaVersion": 1,
          "anchors": [
            {
              "id": "hallway-footprint",
              "label": "1523.800,-4425.900,17.100",
              "wowX": 1523.8,
              "wowY": -4425.9,
              "wowZ": 17.1,
              "sourceSupport": {
                "found": true
              },
              "stages": [
                { "name": "rasterize", "upperSupportExists": true, "dominantLowerCandidate": false },
                { "name": "filterLowHanging", "upperSupportExists": true, "dominantLowerCandidate": false },
                { "name": "filterLedge", "upperSupportExists": true, "dominantLowerCandidate": false },
                { "name": "removeUseless", "upperSupportExists": true, "dominantLowerCandidate": false },
                { "name": "filterLowHeight", "upperSupportExists": true, "dominantLowerCandidate": false },
                { "name": "waterInheritance", "upperSupportExists": true, "dominantLowerCandidate": false },
                { "name": "buildCHF", "upperSupportExists": true, "dominantLowerCandidate": false },
                { "name": "markGameObjects", "upperSupportExists": true, "dominantLowerCandidate": false },
                { "name": "erode", "upperSupportExists": true, "dominantLowerCandidate": false },
                { "name": "median", "upperSupportExists": true, "dominantLowerCandidate": false },
                { "name": "regions", "upperSupportExists": true, "dominantLowerCandidate": false },
                { "name": "contours", "upperSupportExists": true, "dominantLowerCandidate": false },
                { "name": "polymesh", "upperSupportExists": true, "dominantLowerCandidate": false },
                {
                  "name": "finalDetour",
                  "upperSupportExists": true,
                  "dominantLowerCandidate": false,
                  "supportCandidateCount": 0,
                  "supportBandCandidateCount": 2,
                  "finalWinner": {
                    "polyRef": "0x1000000000ADAB",
                    "supportCandidate": false,
                    "competingLower": false
                  }
                }
              ]
            }
          ]
        }
        """;

        using var document = JsonDocument.Parse(manifestJson);
        var summary = StageManifestAnalyzer.Analyze(document);
        var anchor = Assert.Single(summary.Anchors);

        Assert.True(anchor.CoverageComplete);
        Assert.Equal("finalDetour", anchor.FirstBadStage);
        Assert.Equal("support_footprint_missed_anchor", anchor.FirstBadReason);
        Assert.Equal("0x1000000000ADAB", anchor.FinalWinnerPolyRef);
        Assert.False(anchor.FinalWinnerSupportCandidate);
        Assert.False(anchor.FinalWinnerCompetingLower);
    }
}
