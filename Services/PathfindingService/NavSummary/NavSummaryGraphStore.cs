using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace PathfindingService.NavSummary;

public sealed class NavSummaryGraphStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        AllowTrailingCommas = true,
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
    };

    private NavSummaryGraphStore(IReadOnlyList<NavSummaryLoadedGraph> graphs, string signature)
    {
        Graphs = graphs;
        Signature = signature;
    }

    public static NavSummaryGraphStore Empty { get; } = new([], "empty");

    public IReadOnlyList<NavSummaryLoadedGraph> Graphs { get; }
    public string Signature { get; }

    public static NavSummaryGraphStore Load(NavSummaryOptions options, ILogger? logger = null)
    {
        ArgumentNullException.ThrowIfNull(options);

        if (!options.Enabled)
            return Empty;

        var graphPath = options.GraphPath.Trim();
        if (string.IsNullOrWhiteSpace(graphPath))
        {
            logger?.LogWarning("[NAV_SUMMARY] enabled but no graph path was configured.");
            return Empty;
        }

        var files = DiscoverGraphFiles(graphPath).ToArray();
        if (files.Length == 0)
        {
            logger?.LogWarning("[NAV_SUMMARY] enabled but no *.navsummary.json graph files were found at {GraphPath}.", graphPath);
            return Empty;
        }

        var loaded = new List<NavSummaryLoadedGraph>();
        foreach (var file in files)
        {
            try
            {
                var json = File.ReadAllText(file);
                var graph = JsonSerializer.Deserialize<NavSummaryGraph>(json, JsonOptions);
                if (graph is null)
                {
                    logger?.LogWarning("[NAV_SUMMARY] skipped {File}: JSON deserialized to null.", file);
                    continue;
                }

                if (!TryValidate(graph, out var validationError))
                {
                    logger?.LogWarning("[NAV_SUMMARY] skipped {File}: {ValidationError}", file, validationError);
                    continue;
                }

                loaded.Add(new NavSummaryLoadedGraph(
                    graph,
                    file,
                    ComputeSignature(json)));
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
            {
                logger?.LogWarning(ex, "[NAV_SUMMARY] skipped {File}: failed to load graph.", file);
            }
        }

        if (loaded.Count == 0)
        {
            logger?.LogWarning("[NAV_SUMMARY] no valid graph files were loaded from {GraphPath}.", graphPath);
            return Empty;
        }

        logger?.LogInformation(
            "[NAV_SUMMARY] loaded {Count} graph(s) from {GraphPath}.",
            loaded.Count,
            graphPath);

        return new NavSummaryGraphStore(loaded, ComputeCombinedSignature(loaded));
    }

    public static NavSummaryGraphStore FromGraphs(IEnumerable<NavSummaryGraph> graphs)
    {
        ArgumentNullException.ThrowIfNull(graphs);

        var loaded = graphs.Select((graph, index) =>
        {
            if (!TryValidate(graph, out var validationError))
                throw new ArgumentException($"Invalid nav summary graph '{graph.Id}': {validationError}", nameof(graphs));

            var source = $"memory:{index}:{graph.Id}";
            var signature = ComputeSignature(JsonSerializer.Serialize(graph, JsonOptions));
            return new NavSummaryLoadedGraph(graph, source, signature);
        }).ToArray();

        return loaded.Length == 0
            ? Empty
            : new NavSummaryGraphStore(loaded, ComputeCombinedSignature(loaded));
    }

    private static IEnumerable<string> DiscoverGraphFiles(string graphPath)
    {
        if (File.Exists(graphPath))
        {
            yield return Path.GetFullPath(graphPath);
            yield break;
        }

        if (!Directory.Exists(graphPath))
            yield break;

        foreach (var file in Directory.EnumerateFiles(graphPath, "*.navsummary.json", SearchOption.TopDirectoryOnly)
            .OrderBy(static path => path, StringComparer.OrdinalIgnoreCase))
        {
            yield return Path.GetFullPath(file);
        }
    }

    private static bool TryValidate(NavSummaryGraph graph, out string error)
    {
        if (graph.SchemaVersion != 1)
        {
            error = $"unsupported schemaVersion {graph.SchemaVersion}";
            return false;
        }

        if (string.IsNullOrWhiteSpace(graph.Id))
        {
            error = "graph id is required";
            return false;
        }

        if (graph.Nodes.Count == 0)
        {
            error = "at least one node is required";
            return false;
        }

        var nodeIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var node in graph.Nodes)
        {
            if (string.IsNullOrWhiteSpace(node.Id))
            {
                error = "node id is required";
                return false;
            }

            if (!float.IsFinite(node.X) || !float.IsFinite(node.Y) || !float.IsFinite(node.Z))
            {
                error = $"node '{node.Id}' has non-finite coordinates";
                return false;
            }

            if (!nodeIds.Add(node.Id))
            {
                error = $"duplicate node id '{node.Id}'";
                return false;
            }
        }

        foreach (var edge in graph.Edges)
        {
            if (!nodeIds.Contains(edge.From))
            {
                error = $"edge references unknown from node '{edge.From}'";
                return false;
            }

            if (!nodeIds.Contains(edge.To))
            {
                error = $"edge references unknown to node '{edge.To}'";
                return false;
            }

            if (edge.Cost < 0f || !float.IsFinite(edge.Cost))
            {
                error = $"edge '{edge.From}->{edge.To}' has invalid cost";
                return false;
            }
        }

        error = string.Empty;
        return true;
    }

    private static string ComputeCombinedSignature(IEnumerable<NavSummaryLoadedGraph> graphs)
    {
        var builder = new StringBuilder("navsummary-v1");
        foreach (var graph in graphs.OrderBy(static graph => graph.Graph.Id, StringComparer.OrdinalIgnoreCase))
        {
            builder.Append('|');
            builder.Append(graph.Graph.Id);
            builder.Append(':');
            builder.Append(graph.Signature);
        }

        return ComputeSignature(builder.ToString());
    }

    private static string ComputeSignature(string content)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(content));
        return Convert.ToHexString(hash);
    }
}
