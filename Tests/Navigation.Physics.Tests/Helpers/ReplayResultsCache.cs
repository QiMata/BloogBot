using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Xunit.Abstractions;
using static Navigation.Physics.Tests.NavigationInterop;

namespace Navigation.Physics.Tests.Helpers;

/// <summary>
/// Thread-safe cache for physics replay results. Each recording is replayed at most once
/// through the C++ engine; subsequent requests return the cached CalibrationResult.
///
/// Map loading uses SceneCache for fast startup:
/// - First run: Full VMAP load (slow) → bounded extract → save .scene file
/// - Subsequent runs: Load .scene file (~1-2s) → ready
///
/// This eliminates the main performance bottleneck: VMAP initialization takes 30-60s
/// per map on first load. With SceneCache, subsequent runs skip VMAP entirely.
/// </summary>
public class ReplayResultsCache
{
    private readonly ConcurrentDictionary<string, CalibrationResult> _cache = new();
    private readonly ConcurrentDictionary<uint, bool> _preloadedMaps = new();

    /// <summary>
    /// Get or compute the replay result for a recording loaded by filename pattern.
    /// The recording is replayed through the physics engine on first access; subsequent
    /// calls return the cached result.
    /// </summary>
    public CalibrationResult GetOrReplay(
        string filenamePattern,
        ITestOutputHelper output,
        bool isInitialized)
    {
        if (!isInitialized)
        {
            output.WriteLine("SKIP: Physics engine not initialized");
            return new CalibrationResult();
        }

        return _cache.GetOrAdd(filenamePattern, _ =>
        {
            var recording = RecordingTestHelpers.TryLoadByFilename(filenamePattern, output);
            if (recording == null)
            {
                output.WriteLine($"SKIP: Recording not available: {filenamePattern}");
                return new CalibrationResult();
            }

            EnsureMapPreloaded(recording.MapId, recording, output);
            var result = ReplayEngine.Replay(recording, filenamePattern);
            return result;
        });
    }

    /// <summary>
    /// Get or compute replay results for all recordings in the recordings directory.
    /// Each recording is replayed at most once (subsequent calls return cached results).
    /// Returns the list of (name, recording, result) tuples.
    ///
    /// Optimized: computes per-map bounding boxes across all recordings before loading,
    /// so scene caches cover all needed areas in a single extraction per map.
    /// </summary>
    public List<(string name, MovementRecording recording, CalibrationResult result)> GetOrReplayAll(
        ITestOutputHelper output,
        bool isInitialized)
    {
        if (!isInitialized)
        {
            output.WriteLine("SKIP: Physics engine not initialized");
            return [];
        }

        var recordings = RecordingTestHelpers.LoadAllRecordings(output);
        if (recordings.Count == 0)
        {
            output.WriteLine("SKIP: No recordings found");
            return [];
        }

        // Pre-compute per-map bounding boxes across ALL recordings
        // This ensures a single scene cache extraction covers all recordings for each map
        var mapBounds = ComputePerMapBounds(recordings);
        foreach (var (mapId, bounds) in mapBounds)
        {
            EnsureMapPreloadedWithBounds(mapId, bounds.minX, bounds.minY, bounds.maxX, bounds.maxY, output);
        }

        var results = new List<(string name, MovementRecording recording, CalibrationResult result)>();

        foreach (var (name, recording) in recordings)
        {
            var result = _cache.GetOrAdd(name, _ => ReplayEngine.Replay(recording, name));
            results.Add((name, recording, result));
        }

        return results;
    }

    /// <summary>
    /// Ensures map data is preloaded for a given mapId.
    /// Scans ALL available recordings for this map to compute merged bounds,
    /// ensuring the scene cache covers all test areas regardless of test execution order.
    /// </summary>
    private void EnsureMapPreloaded(uint mapId, MovementRecording recording, ITestOutputHelper output)
    {
        _preloadedMaps.GetOrAdd(mapId, id =>
        {
            // Load all recordings for this map and compute merged bounds
            // so the scene cache covers everything no matter which test runs first
            var allRecordings = RecordingTestHelpers.LoadAllRecordings(output);
            var sameMapRecordings = allRecordings.Where(r => r.rec.MapId == id).ToList();

            float minX, minY, maxX, maxY;
            if (sameMapRecordings.Count > 0)
            {
                var merged = ComputePerMapBounds(sameMapRecordings);
                (minX, minY, maxX, maxY) = merged[id];
            }
            else
            {
                (minX, minY, maxX, maxY) = ComputeRecordingBounds(recording);
            }

            EnsureMapPreloadedWithBounds(id, minX, minY, maxX, maxY, output);
            return true;
        });
    }

    /// <summary>
    /// Ensures map data is preloaded for a given mapId with specified XY bounds.
    /// Uses SceneCache: checks for .scene file on disk first, extracts if missing.
    /// </summary>
    private void EnsureMapPreloadedWithBounds(
        uint mapId, float minX, float minY, float maxX, float maxY,
        ITestOutputHelper output)
    {
        if (_preloadedMaps.ContainsKey(mapId))
            return;

        _preloadedMaps.TryAdd(mapId, true);

        // 1. Check if scene cache already loaded in memory
        try
        {
            if (HasSceneCache(mapId))
            {
                output.WriteLine($"Scene cache already loaded for map {mapId}");
                return;
            }
        }
        catch { /* HasSceneCache not available */ }

        // 2. Check for .scene file on disk
        var dataDir = Environment.GetEnvironmentVariable("WWOW_DATA_DIR") ?? "";
        var scenesDir = string.IsNullOrEmpty(dataDir)
            ? "scenes"
            : Path.Combine(dataDir, "scenes");
        var scenePath = Path.Combine(scenesDir, $"{mapId}.scene");

        if (File.Exists(scenePath))
        {
            try
            {
                var sw = System.Diagnostics.Stopwatch.StartNew();
                if (LoadSceneCache(mapId, scenePath))
                {
                    sw.Stop();
                    output.WriteLine($"Loaded scene cache for map {mapId} from {scenePath} ({sw.ElapsedMilliseconds}ms)");
                    return;
                }
            }
            catch (Exception ex)
            {
                output.WriteLine($"Failed to load scene cache: {ex.Message}");
            }
        }

        // 3. No scene cache — do full VMAP load (slow, first run only)
        output.WriteLine($"No scene cache for map {mapId}, loading VMAP (first run, will cache for next time)...");
        var swVmap = System.Diagnostics.Stopwatch.StartNew();
        RecordingTestHelpers.TryPreloadMap(mapId, output);
        swVmap.Stop();
        output.WriteLine($"VMAP loaded in {swVmap.Elapsed.TotalSeconds:F1}s");

        // 4. Extract bounded scene cache and save for next time
        try
        {
            Directory.CreateDirectory(scenesDir);
            var swExtract = System.Diagnostics.Stopwatch.StartNew();
            bool ok = ExtractSceneCache(mapId, scenePath, minX, minY, maxX, maxY);
            swExtract.Stop();
            if (ok)
            {
                var fileSize = new FileInfo(scenePath).Length;
                output.WriteLine($"Extracted scene cache for map {mapId}: " +
                    $"bounds=({minX:F0},{minY:F0})→({maxX:F0},{maxY:F0}), " +
                    $"size={fileSize / 1024 / 1024}MB, time={swExtract.Elapsed.TotalSeconds:F1}s");
            }
            else
            {
                output.WriteLine($"Scene cache extraction failed for map {mapId}");
            }
        }
        catch (Exception ex)
        {
            output.WriteLine($"Scene cache extraction error: {ex.Message}");
        }
    }

    /// <summary>
    /// Computes per-map bounding boxes with margin from a set of recordings.
    /// </summary>
    private static Dictionary<uint, (float minX, float minY, float maxX, float maxY)> ComputePerMapBounds(
        List<(string name, MovementRecording rec)> recordings)
    {
        var bounds = new Dictionary<uint, (float minX, float minY, float maxX, float maxY)>();

        foreach (var (_, rec) in recordings)
        {
            var (rMinX, rMinY, rMaxX, rMaxY) = ComputeRecordingBounds(rec);

            if (bounds.TryGetValue(rec.MapId, out var existing))
            {
                bounds[rec.MapId] = (
                    Math.Min(existing.minX, rMinX),
                    Math.Min(existing.minY, rMinY),
                    Math.Max(existing.maxX, rMaxX),
                    Math.Max(existing.maxY, rMaxY));
            }
            else
            {
                bounds[rec.MapId] = (rMinX, rMinY, rMaxX, rMaxY);
            }
        }

        return bounds;
    }

    /// <summary>
    /// Computes the XY bounding box of a recording's frames with a generous margin.
    /// The margin (200 yards) covers surrounding buildings, terrain features, and
    /// model instances that contribute to collision geometry near the recording path.
    /// </summary>
    private static (float minX, float minY, float maxX, float maxY) ComputeRecordingBounds(
        MovementRecording recording)
    {
        const float MARGIN = 200.0f;

        float minX = float.MaxValue, minY = float.MaxValue;
        float maxX = float.MinValue, maxY = float.MinValue;

        foreach (var frame in recording.Frames)
        {
            if (frame.Position.X < minX) minX = frame.Position.X;
            if (frame.Position.Y < minY) minY = frame.Position.Y;
            if (frame.Position.X > maxX) maxX = frame.Position.X;
            if (frame.Position.Y > maxY) maxY = frame.Position.Y;
        }

        return (minX - MARGIN, minY - MARGIN, maxX + MARGIN, maxY + MARGIN);
    }

    /// <summary>Number of cached replay results.</summary>
    public int Count => _cache.Count;
}
