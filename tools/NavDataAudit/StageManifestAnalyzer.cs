using System.Globalization;
using System.Text.Json;

namespace NavDataAudit;

public sealed record AnchorStageManifestSummary(
    int SchemaVersion,
    string? ManifestPath,
    uint? MapId,
    int? TileX,
    int? TileY,
    IReadOnlyList<AnchorStageSummary> Anchors);

public sealed record AnchorStageSummary(
    string AnchorId,
    string Label,
    float WowX,
    float WowY,
    float WowZ,
    bool SourceSupportFound,
    int PresentStageCount,
    IReadOnlyList<string> MissingStages,
    string? FirstBadStage,
    string? FirstBadReason,
    string? FinalWinnerPolyRef,
    int? FinalWinnerComponentId,
    int? FinalWinnerComponentPolyCount,
    bool? FinalWinnerSupportCandidate,
    bool? FinalWinnerCompetingLower,
    int? FinalSupportComponentCount,
    int? FinalLowerComponentCount,
    bool CoverageComplete);

public static class StageManifestAnalyzer
{
    public static readonly string[] ExpectedStages =
    [
        "rasterize",
        "filterLowHanging",
        "filterLedge",
        "removeUseless",
        "filterLowHeight",
        "waterInheritance",
        "buildCHF",
        "markGameObjects",
        "erode",
        "median",
        "regions",
        "contours",
        "polymesh",
        "finalDetour",
    ];

    public static AnchorStageManifestSummary Analyze(string manifestPath)
    {
        using var document = JsonDocument.Parse(File.ReadAllText(manifestPath));
        return Analyze(document, manifestPath);
    }

    public static AnchorStageManifestSummary Analyze(JsonDocument document, string? manifestPath = null)
    {
        var root = document.RootElement;
        var schemaVersion = GetInt(root, "schemaVersion") ?? 0;
        var mapId = GetUInt(root, "mapId");
        var tileX = GetInt(root, "tileX");
        var tileY = GetInt(root, "tileY");
        var anchors = new List<AnchorStageSummary>();

        if (root.TryGetProperty("anchors", out var anchorsElement) && anchorsElement.ValueKind == JsonValueKind.Array)
        {
            foreach (var anchorElement in anchorsElement.EnumerateArray())
            {
                anchors.Add(AnalyzeAnchor(anchorElement));
            }
        }

        return new AnchorStageManifestSummary(schemaVersion, manifestPath, mapId, tileX, tileY, anchors);
    }

    private static AnchorStageSummary AnalyzeAnchor(JsonElement anchorElement)
    {
        var anchorId = GetString(anchorElement, "id") ?? "<unknown-anchor>";
        var label = GetString(anchorElement, "label") ?? anchorId;
        var wowX = GetFloat(anchorElement, "wowX") ?? 0.0f;
        var wowY = GetFloat(anchorElement, "wowY") ?? 0.0f;
        var wowZ = GetFloat(anchorElement, "wowZ") ?? 0.0f;
        var sourceSupportFound = false;
        if (anchorElement.TryGetProperty("sourceSupport", out var sourceSupportElement))
            sourceSupportFound = GetBool(sourceSupportElement, "found") ?? false;

        var stagesByName = new Dictionary<string, JsonElement>(StringComparer.Ordinal);
        if (anchorElement.TryGetProperty("stages", out var stagesElement) && stagesElement.ValueKind == JsonValueKind.Array)
        {
            foreach (var stageElement in stagesElement.EnumerateArray())
            {
                var stageName = GetString(stageElement, "name");
                if (!string.IsNullOrWhiteSpace(stageName))
                    stagesByName[stageName] = stageElement;
            }
        }

        var missingStages = new List<string>();
        string? firstBadStage = null;
        string? firstBadReason = null;

        if (!sourceSupportFound)
        {
            firstBadStage = "sourceSupport";
            firstBadReason = "no_source_support_probe";
        }

        foreach (var expectedStage in ExpectedStages)
        {
            if (!stagesByName.TryGetValue(expectedStage, out var stageElement))
            {
                missingStages.Add(expectedStage);
                if (firstBadStage is null)
                {
                    firstBadStage = expectedStage;
                    firstBadReason = "missing_stage";
                }

                continue;
            }

            var upperSupportExists = GetBool(stageElement, "upperSupportExists") ?? false;
            var dominantLowerCandidate = GetBool(stageElement, "dominantLowerCandidate") ?? false;

            if (firstBadStage is null && !upperSupportExists)
            {
                firstBadStage = expectedStage;
                firstBadReason = "upper_support_lost";
            }
            else if (firstBadStage is null && dominantLowerCandidate)
            {
                firstBadStage = expectedStage;
                firstBadReason = "lower_competitor_dominant";
            }
        }

        string? finalWinnerPolyRef = null;
        int? finalWinnerComponentId = null;
        int? finalWinnerComponentPolyCount = null;
        bool? finalWinnerSupport = null;
        bool? finalWinnerLower = null;
        int? finalSupportComponentCount = null;
        int? finalLowerComponentCount = null;
        if (stagesByName.TryGetValue("finalDetour", out var finalStage) &&
            finalStage.TryGetProperty("finalWinner", out var finalWinner))
        {
            finalWinnerPolyRef = GetString(finalWinner, "polyRef");
            finalWinnerComponentId = GetInt(finalWinner, "componentId");
            finalWinnerComponentPolyCount = GetInt(finalWinner, "componentPolyCount");
            finalWinnerSupport = GetBool(finalWinner, "supportCandidate");
            finalWinnerLower = GetBool(finalWinner, "competingLower");
            var finalSupportCount = GetInt(finalStage, "supportCandidateCount") ?? 0;
            var finalSupportBandCount = GetInt(finalStage, "supportBandCandidateCount") ?? finalSupportCount;
            finalSupportComponentCount = GetInt(finalStage, "supportComponentCount");
            finalLowerComponentCount = GetInt(finalStage, "lowerComponentCount");

            if (firstBadStage is null &&
                (finalWinnerLower ?? false) &&
                finalSupportBandCount > 0)
            {
                firstBadStage = "finalDetour";
                firstBadReason = "lower_competitor_dominant";
            }
            else if (firstBadStage is null &&
                !(finalWinnerSupport ?? false) &&
                !(finalWinnerLower ?? false) &&
                finalSupportBandCount > 0 &&
                finalSupportCount == 0)
            {
                firstBadStage = "finalDetour";
                firstBadReason = "support_footprint_missed_anchor";
            }
        }

        return new AnchorStageSummary(
            AnchorId: anchorId,
            Label: label,
            WowX: wowX,
            WowY: wowY,
            WowZ: wowZ,
            SourceSupportFound: sourceSupportFound,
            PresentStageCount: stagesByName.Count,
            MissingStages: missingStages,
            FirstBadStage: firstBadStage,
            FirstBadReason: firstBadReason,
            FinalWinnerPolyRef: finalWinnerPolyRef,
            FinalWinnerComponentId: finalWinnerComponentId,
            FinalWinnerComponentPolyCount: finalWinnerComponentPolyCount,
            FinalWinnerSupportCandidate: finalWinnerSupport,
            FinalWinnerCompetingLower: finalWinnerLower,
            FinalSupportComponentCount: finalSupportComponentCount,
            FinalLowerComponentCount: finalLowerComponentCount,
            CoverageComplete: missingStages.Count == 0);
    }

    private static string? GetString(JsonElement element, string name)
        => element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    private static bool? GetBool(JsonElement element, string name)
        => element.TryGetProperty(name, out var value) && (value.ValueKind == JsonValueKind.True || value.ValueKind == JsonValueKind.False)
            ? value.GetBoolean()
            : null;

    private static int? GetInt(JsonElement element, string name)
        => element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var result)
            ? result
            : null;

    private static uint? GetUInt(JsonElement element, string name)
        => element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.Number && value.TryGetUInt32(out var result)
            ? result
            : null;

    private static float? GetFloat(JsonElement element, string name)
        => element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.Number && value.TryGetSingle(out var result)
            ? result
            : null;
}
