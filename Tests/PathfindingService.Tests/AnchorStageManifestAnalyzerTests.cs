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
                  "finalWinner": {
                    "polyRef": "0x100001520ADA2",
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
        Assert.True(anchor.FinalWinnerCompetingLower);
        Assert.False(anchor.FinalWinnerSupportCandidate);
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
                  "upperSupportExists": false,
                  "dominantLowerCandidate": false,
                  "supportCandidateCount": 1,
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
        Assert.Equal("upper_support_lost", anchor.FirstBadReason);
        Assert.Equal("0x1000000000BE35", anchor.FinalWinnerPolyRef);
        Assert.False(anchor.FinalWinnerSupportCandidate);
        Assert.True(anchor.FinalWinnerCompetingLower);
    }
}
