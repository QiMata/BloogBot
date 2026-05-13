using Microsoft.Extensions.Configuration;
using System;
using System.Globalization;
using System.IO;

namespace PathfindingService.NavSummary;

public sealed record NavSummaryOptions
{
    public const string EnableEnvironmentVariable = "WWOW_ENABLE_NAV_SUMMARY";
    public const string PathEnvironmentVariable = "WWOW_NAV_SUMMARY_PATH";
    public const string DirectoryEnvironmentVariable = "WWOW_NAV_SUMMARY_DIR";
    public const string MinDistanceEnvironmentVariable = "WWOW_NAV_SUMMARY_MIN_DISTANCE";
    public const string MaxAnchorDistanceEnvironmentVariable = "WWOW_NAV_SUMMARY_MAX_ANCHOR_DISTANCE";

    public bool Enabled { get; init; }
    public string GraphPath { get; init; } = string.Empty;
    public float MinDistance { get; init; } = 600f;
    public float MaxAnchorDistance { get; init; } = 120f;
    public float MaxDetailEndpointDistance { get; init; } = 20f;
    public int NearestAnchorCandidateCount { get; init; } = 8;
    public int MaxExpandedSegments { get; init; } = 96;

    public static NavSummaryOptions FromConfiguration(IConfiguration? configuration)
    {
        var enabled = ResolveBool(
            configuration,
            EnableEnvironmentVariable,
            "Navigation:NavSummary:Enabled",
            "PathfindingService:Navigation:NavSummary:Enabled",
            defaultValue: false);

        var graphPath = ResolveString(
            configuration,
            PathEnvironmentVariable,
            "Navigation:NavSummary:GraphPath",
            "PathfindingService:Navigation:NavSummary:GraphPath");

        if (string.IsNullOrWhiteSpace(graphPath))
        {
            graphPath = ResolveString(
                configuration,
                DirectoryEnvironmentVariable,
                "Navigation:NavSummary:Directory",
                "PathfindingService:Navigation:NavSummary:Directory");
        }

        if (enabled && string.IsNullOrWhiteSpace(graphPath))
        {
            var dataDir = Environment.GetEnvironmentVariable("WWOW_DATA_DIR");
            graphPath = string.IsNullOrWhiteSpace(dataDir)
                ? Path.Combine(AppContext.BaseDirectory, "navsummary")
                : Path.Combine(dataDir, "navsummary");
        }

        return new NavSummaryOptions
        {
            Enabled = enabled,
            GraphPath = graphPath ?? string.Empty,
            MinDistance = ResolveFloat(
                configuration,
                MinDistanceEnvironmentVariable,
                "Navigation:NavSummary:MinDistance",
                "PathfindingService:Navigation:NavSummary:MinDistance",
                defaultValue: 600f,
                minValue: 1f),
            MaxAnchorDistance = ResolveFloat(
                configuration,
                MaxAnchorDistanceEnvironmentVariable,
                "Navigation:NavSummary:MaxAnchorDistance",
                "PathfindingService:Navigation:NavSummary:MaxAnchorDistance",
                defaultValue: 120f,
                minValue: 1f),
            MaxDetailEndpointDistance = ResolveFloat(
                configuration,
                "WWOW_NAV_SUMMARY_MAX_DETAIL_ENDPOINT_DISTANCE",
                "Navigation:NavSummary:MaxDetailEndpointDistance",
                "PathfindingService:Navigation:NavSummary:MaxDetailEndpointDistance",
                defaultValue: 20f,
                minValue: 1f),
            NearestAnchorCandidateCount = ResolveInt(
                configuration,
                "WWOW_NAV_SUMMARY_NEAREST_ANCHORS",
                "Navigation:NavSummary:NearestAnchorCandidateCount",
                "PathfindingService:Navigation:NavSummary:NearestAnchorCandidateCount",
                defaultValue: 8,
                minValue: 1),
            MaxExpandedSegments = ResolveInt(
                configuration,
                "WWOW_NAV_SUMMARY_MAX_EXPANDED_SEGMENTS",
                "Navigation:NavSummary:MaxExpandedSegments",
                "PathfindingService:Navigation:NavSummary:MaxExpandedSegments",
                defaultValue: 96,
                minValue: 1),
        };
    }

    private static bool ResolveBool(
        IConfiguration? configuration,
        string environmentVariable,
        string configurationKey,
        string nestedConfigurationKey,
        bool defaultValue)
    {
        var configured = Environment.GetEnvironmentVariable(environmentVariable)
            ?? configuration?[configurationKey]
            ?? configuration?[nestedConfigurationKey];

        if (string.IsNullOrWhiteSpace(configured))
            return defaultValue;

        var value = configured.Trim();
        return value.Equals("1", StringComparison.OrdinalIgnoreCase)
            || value.Equals("true", StringComparison.OrdinalIgnoreCase)
            || value.Equals("yes", StringComparison.OrdinalIgnoreCase)
            || value.Equals("on", StringComparison.OrdinalIgnoreCase);
    }

    private static string? ResolveString(
        IConfiguration? configuration,
        string environmentVariable,
        string configurationKey,
        string nestedConfigurationKey)
        => Environment.GetEnvironmentVariable(environmentVariable)
            ?? configuration?[configurationKey]
            ?? configuration?[nestedConfigurationKey];

    private static float ResolveFloat(
        IConfiguration? configuration,
        string environmentVariable,
        string configurationKey,
        string nestedConfigurationKey,
        float defaultValue,
        float minValue)
    {
        var configured = ResolveString(configuration, environmentVariable, configurationKey, nestedConfigurationKey);
        if (!float.TryParse(configured, NumberStyles.Float, CultureInfo.InvariantCulture, out var value)
            || !float.IsFinite(value)
            || value < minValue)
        {
            return defaultValue;
        }

        return value;
    }

    private static int ResolveInt(
        IConfiguration? configuration,
        string environmentVariable,
        string configurationKey,
        string nestedConfigurationKey,
        int defaultValue,
        int minValue)
    {
        var configured = ResolveString(configuration, environmentVariable, configurationKey, nestedConfigurationKey);
        if (!int.TryParse(configured, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value)
            || value < minValue)
        {
            return defaultValue;
        }

        return value;
    }
}
