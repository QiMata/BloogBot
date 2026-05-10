using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace BotRunner.Tests.LiveValidation.Harness;

/// <summary>
/// Structured report emitted by <see cref="WaypointSettleValidator"/> after
/// it walks a fixture's expected-walkable, expected-holes, and smooth-path
/// expectations against a live FG (and optionally BG) bot. Serializable to
/// <c>bake-validation-&lt;route&gt;-&lt;timestamp&gt;.json</c>.
/// </summary>
public sealed record BakeValidationReport(
    [property: JsonPropertyName("route")] string Route,
    [property: JsonPropertyName("timestampUtc")] DateTime TimestampUtc,
    [property: JsonPropertyName("fgAccount")] string FgAccount,
    [property: JsonPropertyName("bgAccount")] string? BgAccount,
    [property: JsonPropertyName("walkable")] IReadOnlyList<BakeValidationCheckpointResult> Walkable,
    [property: JsonPropertyName("holes")] IReadOnlyList<BakeValidationCheckpointResult> Holes,
    [property: JsonPropertyName("smoothPath")] BakeValidationSmoothPathResult? SmoothPath,
    [property: JsonPropertyName("affordance")] BakeValidationAffordanceResult? Affordance,
    [property: JsonPropertyName("failures")] IReadOnlyList<BakeValidationFailure> Failures,
    [property: JsonPropertyName("passed")] bool Passed);

public sealed record BakeValidationCheckpointResult(
    [property: JsonPropertyName("label")] string Label,
    [property: JsonPropertyName("kind")] string Kind, // "walkable" | "hole"
    [property: JsonPropertyName("expectedXyz")] float[] ExpectedXyz,
    [property: JsonPropertyName("expectedSettleZ")] float? ExpectedSettleZ,
    [property: JsonPropertyName("settleToleranceY")] float SettleToleranceY,
    [property: JsonPropertyName("fgSettled")] float[]? FgSettled,
    [property: JsonPropertyName("bgSettled")] float[]? BgSettled,
    [property: JsonPropertyName("fgPolyRef")] string? FgPolyRefHex,
    [property: JsonPropertyName("bgPolyRef")] string? BgPolyRefHex,
    [property: JsonPropertyName("status")] string Status); // "OK" | "FAILED" | "MISSING_SAMPLE"

public sealed record BakeValidationSmoothPathResult(
    [property: JsonPropertyName("waypointCount")] int WaypointCount,
    [property: JsonPropertyName("expectedCount")] int? ExpectedCount,
    [property: JsonPropertyName("expectedTolerance")] int? ExpectedTolerance,
    [property: JsonPropertyName("endpointDistanceY")] float? EndpointDistanceY,
    [property: JsonPropertyName("endpointToleranceY")] float? EndpointToleranceY);

public sealed record BakeValidationAffordanceResult(
    [property: JsonPropertyName("evaluatedSegments")] int EvaluatedSegments,
    [property: JsonPropertyName("unsafeSegmentCount")] int UnsafeSegmentCount,
    [property: JsonPropertyName("firstUnsafeIndex")] int? FirstUnsafeIndex,
    [property: JsonPropertyName("firstUnsafeKind")] string? FirstUnsafeKind);

/// <summary>
/// Strongly-typed failure record. <see cref="Kind"/> is the canonical
/// tag (BAKE_REGRESSION_WALKABLE_LOST, PHANTOM_POLY,
/// WAYPOINT_COUNT_DRIFT, ENDPOINT_MISS, UNSAFE_AFFORDANCE,
/// FG_BG_PARITY_BREAK, TELEPORT_FAILED) so callers can pivot on the tag.
/// </summary>
public sealed record BakeValidationFailure(
    [property: JsonPropertyName("kind")] string Kind,
    [property: JsonPropertyName("label")] string Label,
    [property: JsonPropertyName("message")] string Message,
    [property: JsonPropertyName("expected")] string? Expected,
    [property: JsonPropertyName("actual")] string? Actual);

/// <summary>
/// Canonical failure-kind tags emitted in <see cref="BakeValidationFailure.Kind"/>.
/// </summary>
public static class BakeValidationFailureKinds
{
    public const string BakeRegressionWalkableLost = "BAKE_REGRESSION_WALKABLE_LOST";
    public const string PhantomPoly = "PHANTOM_POLY";
    public const string WaypointCountDrift = "WAYPOINT_COUNT_DRIFT";
    public const string EndpointMiss = "ENDPOINT_MISS";
    public const string UnsafeAffordance = "UNSAFE_AFFORDANCE";
    public const string FgBgParityBreak = "FG_BG_PARITY_BREAK";
    public const string TeleportFailed = "TELEPORT_FAILED";
    public const string SmoothPathQueryFailed = "SMOOTH_PATH_QUERY_FAILED";
}

public static class BakeValidationReportSerializer
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public static string Serialize(BakeValidationReport report)
        => JsonSerializer.Serialize(report, Options);
}
