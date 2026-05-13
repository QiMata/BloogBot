using GameData.Core.Models;
using System;
using System.Collections.Generic;
using System.Linq;

namespace PathfindingService.NavSummary;

public readonly record struct NavSummaryRouteRequest(
    uint MapId,
    XYZ Start,
    XYZ End,
    bool SmoothPath,
    float AgentRadius,
    float AgentHeight,
    float HorizontalDistance,
    int DynamicOverlayCount);

public sealed record NavSummaryRoutePlan(
    NavSummaryLoadedGraph LoadedGraph,
    IReadOnlyList<XYZ> Anchors,
    float EstimatedCost);

public sealed class NavSummaryRoutePlanner(NavSummaryOptions options)
{
    public bool TryPlan(
        NavSummaryGraphStore store,
        NavSummaryRouteRequest request,
        out NavSummaryRoutePlan plan)
    {
        ArgumentNullException.ThrowIfNull(store);

        plan = default!;
        if (!options.Enabled
            || request.DynamicOverlayCount != 0
            || request.HorizontalDistance < options.MinDistance
            || store.Graphs.Count == 0)
        {
            return false;
        }

        NavSummaryRoutePlan? best = null;
        foreach (var loadedGraph in store.Graphs.Where(graph => graph.Graph.MapId == request.MapId))
        {
            if (TryPlanGraph(loadedGraph, request, out var candidate)
                && (best is null || candidate.EstimatedCost < best.EstimatedCost))
            {
                best = candidate;
            }
        }

        if (best is null)
            return false;

        plan = best;
        return true;
    }

    private bool TryPlanGraph(
        NavSummaryLoadedGraph loadedGraph,
        NavSummaryRouteRequest request,
        out NavSummaryRoutePlan plan)
    {
        plan = default!;

        var graph = loadedGraph.Graph;
        var indexById = graph.Nodes
            .Select((node, index) => (node.Id, Index: index))
            .ToDictionary(static pair => pair.Id, static pair => pair.Index, StringComparer.OrdinalIgnoreCase);
        var positions = graph.Nodes.Select(static node => node.Position).ToArray();
        var adjacency = BuildAdjacency(graph, indexById, positions);

        var startCandidates = FindNearestCandidates(positions, request.Start).ToArray();
        var endCandidates = FindNearestCandidates(positions, request.End).ToArray();
        if (startCandidates.Length == 0 || endCandidates.Length == 0)
            return false;

        float bestCost = float.PositiveInfinity;
        int[]? bestPath = null;
        foreach (var startCandidate in startCandidates)
        {
            foreach (var endCandidate in endCandidates)
            {
                if (!TryFindNodePath(adjacency, startCandidate.Index, endCandidate.Index, out var nodePath, out var graphCost))
                    continue;

                var totalCost = startCandidate.Distance + graphCost + endCandidate.Distance;
                if (totalCost < bestCost)
                {
                    bestCost = totalCost;
                    bestPath = nodePath;
                }
            }
        }

        if (bestPath is null)
            return false;

        plan = new NavSummaryRoutePlan(
            loadedGraph,
            bestPath.Select(index => positions[index]).ToArray(),
            bestCost);
        return true;
    }

    private IEnumerable<(int Index, float Distance)> FindNearestCandidates(XYZ[] positions, XYZ point)
        => positions
            .Select((position, index) => (Index: index, Distance: Distance3D(position, point)))
            .Where(candidate => candidate.Distance <= options.MaxAnchorDistance)
            .OrderBy(static candidate => candidate.Distance)
            .Take(options.NearestAnchorCandidateCount);

    private static List<(int To, float Cost)>[] BuildAdjacency(
        NavSummaryGraph graph,
        IReadOnlyDictionary<string, int> indexById,
        IReadOnlyList<XYZ> positions)
    {
        var adjacency = Enumerable.Range(0, graph.Nodes.Count)
            .Select(_ => new List<(int To, float Cost)>())
            .ToArray();

        foreach (var edge in graph.Edges)
        {
            var from = indexById[edge.From];
            var to = indexById[edge.To];
            var cost = edge.Cost > 0f
                ? edge.Cost
                : Distance3D(positions[from], positions[to]);
            adjacency[from].Add((to, cost));
            if (edge.Bidirectional)
                adjacency[to].Add((from, cost));
        }

        return adjacency;
    }

    private static bool TryFindNodePath(
        IReadOnlyList<List<(int To, float Cost)>> adjacency,
        int start,
        int end,
        out int[] path,
        out float cost)
    {
        path = [];
        cost = float.PositiveInfinity;

        var distance = Enumerable.Repeat(float.PositiveInfinity, adjacency.Count).ToArray();
        var previous = Enumerable.Repeat(-1, adjacency.Count).ToArray();
        var queue = new PriorityQueue<int, float>();

        distance[start] = 0f;
        queue.Enqueue(start, 0f);

        while (queue.TryDequeue(out var current, out var priority))
        {
            if (priority > distance[current] + 0.001f)
                continue;

            if (current == end)
                break;

            foreach (var edge in adjacency[current])
            {
                var nextDistance = distance[current] + edge.Cost;
                if (nextDistance >= distance[edge.To])
                    continue;

                distance[edge.To] = nextDistance;
                previous[edge.To] = current;
                queue.Enqueue(edge.To, nextDistance);
            }
        }

        if (!float.IsFinite(distance[end]))
            return false;

        var reversed = new List<int>();
        for (var at = end; at >= 0; at = previous[at])
        {
            reversed.Add(at);
            if (at == start)
                break;
        }

        reversed.Reverse();
        path = reversed.ToArray();
        cost = distance[end];
        return path.Length > 0 && path[0] == start && path[^1] == end;
    }

    internal static float Distance3D(XYZ a, XYZ b)
    {
        var dx = a.X - b.X;
        var dy = a.Y - b.Y;
        var dz = a.Z - b.Z;
        return MathF.Sqrt((dx * dx) + (dy * dy) + (dz * dz));
    }
}
