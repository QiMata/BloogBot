using GameData.Core.Models;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace PathfindingService
{
    public static class PathRouteDiagnostics
    {
        public const float ShortRouteDistanceThreshold = 40.0f;
        public const int MaxLoggedCorners = 12;
        private const int SamplingInterval = 50;

        public static bool ShouldLogRoute(
            float distance2D,
            int sanitizedCornerCount,
            int rawCornerCount,
            string result,
            int requestOrdinal)
        {
            if (sanitizedCornerCount <= 1)
                return true;

            if (distance2D <= ShortRouteDistanceThreshold)
                return true;

            if (!string.Equals(result, "native_path", StringComparison.Ordinal))
                return true;

            if (rawCornerCount != sanitizedCornerCount)
                return true;

            return requestOrdinal % SamplingInterval == 1;
        }

        public static string GetReason(
            float distance2D,
            int sanitizedCornerCount,
            int rawCornerCount,
            string result,
            int requestOrdinal)
        {
            var reasons = new List<string>();

            if (sanitizedCornerCount <= 1)
                reasons.Add("sparse_result");

            if (distance2D <= ShortRouteDistanceThreshold)
                reasons.Add("short_route");

            if (!string.Equals(result, "native_path", StringComparison.Ordinal))
                reasons.Add(result);

            if (rawCornerCount != sanitizedCornerCount)
                reasons.Add("sanitized_corners");

            if (reasons.Count == 0 && requestOrdinal % SamplingInterval == 1)
                reasons.Add("sampled");

            return reasons.Count > 0 ? string.Join(",", reasons) : "none";
        }

        public static string FormatCorners(IEnumerable<XYZ> corners, int maxCorners = MaxLoggedCorners)
        {
            if (corners is null)
                return string.Empty;

            var points = corners.Take(maxCorners + 1).ToArray();
            if (points.Length == 0)
                return string.Empty;

            var formatted = points
                .Take(maxCorners)
                .Select(point => string.Create(
                    CultureInfo.InvariantCulture,
                    $"({point.X:F1},{point.Y:F1},{point.Z:F1})"));

            var result = string.Join(" -> ", formatted);
            return points.Length > maxCorners ? $"{result} -> ..." : result;
        }
    }
}
