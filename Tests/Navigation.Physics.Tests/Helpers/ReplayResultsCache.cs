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
/// This eliminates the main performance bottleneck: diagnostic tests that replay ALL
/// recordings were doing 4x redundant work (each taking ~9 minutes), because each
/// StepV2 call performs 17-ray VMAP/ADT ground probing per frame.
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

            EnsureMapPreloaded(recording.MapId, output);
            var result = ReplayEngine.Replay(recording, filenamePattern);
            return result;
        });
    }

    /// <summary>
    /// Get or compute replay results for all recordings in the recordings directory.
    /// Each recording is replayed at most once (subsequent calls return cached results).
    /// Returns the list of (name, recording, result) tuples.
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

        var results = new List<(string name, MovementRecording recording, CalibrationResult result)>();

        foreach (var (name, recording) in recordings)
        {
            var result = _cache.GetOrAdd(name, _ =>
            {
                EnsureMapPreloaded(recording.MapId, output);
                return ReplayEngine.Replay(recording, name);
            });

            results.Add((name, recording, result));
        }

        return results;
    }

    /// <summary>
    /// Ensures map data is preloaded for a given mapId (only calls PreloadMap once per map).
    /// </summary>
    private void EnsureMapPreloaded(uint mapId, ITestOutputHelper output)
    {
        _preloadedMaps.GetOrAdd(mapId, id =>
        {
            RecordingTestHelpers.TryPreloadMap(id, output);
            return true;
        });
    }

    /// <summary>Number of cached replay results.</summary>
    public int Count => _cache.Count;
}
