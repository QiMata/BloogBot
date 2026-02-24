using System.Collections.Concurrent;

namespace BloogBot.AI.Advisory;

/// <summary>
/// In-memory implementation of advisory override logging.
/// Suitable for development and testing. For production, use a database-backed implementation.
/// </summary>
public sealed class InMemoryAdvisoryOverrideLog : IAdvisoryOverrideLog
{
    private readonly ConcurrentQueue<AdvisoryResolution> _overrides = new();
    private readonly ConcurrentDictionary<string, int> _overrideCountsByRule = new();
    private readonly int _maxEntries;
    private int _totalCount;

    /// <summary>
    /// Creates a new in-memory override log with the specified maximum entry count.
    /// </summary>
    /// <param name="maxEntries">Maximum number of entries to retain (oldest are discarded).</param>
    public InMemoryAdvisoryOverrideLog(int maxEntries = 1000)
    {
        _maxEntries = maxEntries;
    }

    /// <inheritdoc />
    public int TotalOverrideCount => _totalCount;

    /// <inheritdoc />
    public void LogOverride(AdvisoryResolution resolution)
    {
        if (!resolution.WasOverridden) return;

        _overrides.Enqueue(resolution);
        Interlocked.Increment(ref _totalCount);

        // Track by rule name
        if (!string.IsNullOrEmpty(resolution.OverrideRule))
        {
            _overrideCountsByRule.AddOrUpdate(
                resolution.OverrideRule,
                1,
                (_, count) => count + 1);
        }

        // Trim if over max
        while (_overrides.Count > _maxEntries)
        {
            _overrides.TryDequeue(out _);
        }
    }

    /// <inheritdoc />
    public IReadOnlyList<AdvisoryResolution> GetRecentOverrides(int count = 100)
    {
        return _overrides
            .Reverse()
            .Take(count)
            .ToList()
            .AsReadOnly();
    }

    /// <inheritdoc />
    public IReadOnlyList<AdvisoryResolution> GetOverridesByRule(string ruleName)
    {
        return _overrides
            .Where(r => r.OverrideRule == ruleName)
            .Reverse()
            .ToList()
            .AsReadOnly();
    }

    /// <inheritdoc />
    public IReadOnlyDictionary<string, int> GetOverrideCountsByRule()
    {
        return new Dictionary<string, int>(_overrideCountsByRule);
    }
}
