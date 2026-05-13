using GameData.Core.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using PathfindingService.Repository;
using System;

namespace PathfindingService.NavSummary;

public sealed class NavSummaryRouteResolver
{
    private readonly NavSummaryOptions _options;
    private readonly NavSummaryGraphStore _store;
    private readonly NavSummaryRoutePlanner _planner;
    private readonly NavSummaryPathExpander _expander;
    private readonly ILogger? _logger;

    public NavSummaryRouteResolver(
        NavSummaryOptions options,
        NavSummaryGraphStore store,
        ILogger? logger = null)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _planner = new NavSummaryRoutePlanner(_options);
        _expander = new NavSummaryPathExpander(_options);
        _logger = logger;
    }

    public bool IsEnabled => _options.Enabled && _store.Graphs.Count > 0;

    public string Signature => IsEnabled
        ? $"NavSummary.v1:{_store.Signature}"
        : "NavSummary.disabled";

    public static NavSummaryRouteResolver FromConfiguration(
        IConfiguration? configuration,
        ILogger? logger = null)
    {
        var options = NavSummaryOptions.FromConfiguration(configuration);
        var store = NavSummaryGraphStore.Load(options, logger);
        return new NavSummaryRouteResolver(options, store, logger);
    }

    public string ApplyToRouteAlgorithmSignature(string baseSignature)
        => IsEnabled ? $"{baseSignature}|{Signature}" : baseSignature;

    public bool TryResolve(
        NavSummaryRouteRequest request,
        Func<XYZ, XYZ, NavigationPathResult> detailedPathResolver,
        out NavSummaryResolution resolution)
    {
        ArgumentNullException.ThrowIfNull(detailedPathResolver);

        resolution = default!;
        if (!IsEnabled)
            return false;

        if (request.DynamicOverlayCount != 0)
            return false;

        if (!_planner.TryPlan(_store, request, out var plan))
            return false;

        if (!_expander.TryExpand(plan, request, detailedPathResolver, out resolution, out var failureReason))
        {
            _logger?.LogDebug(
                "[NAV_SUMMARY] rejected graph={GraphId} reason={Reason}",
                plan.LoadedGraph.Graph.Id,
                failureReason);
            return false;
        }

        return true;
    }
}
