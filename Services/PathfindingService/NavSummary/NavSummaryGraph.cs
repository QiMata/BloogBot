using GameData.Core.Models;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace PathfindingService.NavSummary;

public sealed class NavSummaryGraph
{
    [JsonPropertyName("schemaVersion")]
    public int SchemaVersion { get; init; } = 1;

    [JsonPropertyName("id")]
    public string Id { get; init; } = string.Empty;

    [JsonPropertyName("description")]
    public string? Description { get; init; }

    [JsonPropertyName("mapId")]
    public uint MapId { get; init; }

    [JsonPropertyName("nodes")]
    public List<NavSummaryNode> Nodes { get; init; } = [];

    [JsonPropertyName("edges")]
    public List<NavSummaryEdge> Edges { get; init; } = [];
}

public sealed class NavSummaryNode
{
    [JsonPropertyName("id")]
    public string Id { get; init; } = string.Empty;

    [JsonPropertyName("x")]
    public float X { get; init; }

    [JsonPropertyName("y")]
    public float Y { get; init; }

    [JsonPropertyName("z")]
    public float Z { get; init; }

    public XYZ Position => new(X, Y, Z);
}

public sealed class NavSummaryEdge
{
    [JsonPropertyName("from")]
    public string From { get; init; } = string.Empty;

    [JsonPropertyName("to")]
    public string To { get; init; } = string.Empty;

    [JsonPropertyName("cost")]
    public float Cost { get; init; }

    [JsonPropertyName("bidirectional")]
    public bool Bidirectional { get; init; } = true;
}

public sealed record NavSummaryLoadedGraph(
    NavSummaryGraph Graph,
    string Source,
    string Signature);
