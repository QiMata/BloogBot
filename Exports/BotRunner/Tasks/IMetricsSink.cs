using System;
using System.Collections.Generic;

namespace BotRunner.Tasks;

/// <summary>
/// Narrow Phase 1 metrics surface per R22. Phase 5 (Observability) either
/// extends this interface or adds adjacent sinks; the two-method surface is
/// forward-compatible.
/// </summary>
/// <remarks>
/// Production wiring lives in <c>BotRunnerService</c> (null-object fallback
/// until Phase 5 wires Prometheus). Tests pass an in-memory sink.
/// </remarks>
public interface IMetricsSink
{
    /// <summary>Increment a counter metric (no-op for unrecognised counters).</summary>
    void IncrementCounter(string name, IReadOnlyDictionary<string, string>? labels = null);

    /// <summary>Record a duration sample.</summary>
    void RecordDuration(string name, TimeSpan duration, IReadOnlyDictionary<string, string>? labels = null);
}

/// <summary>
/// No-op <see cref="IMetricsSink"/> used as the null-object default for
/// production wiring + tests until Phase 5 Observability lands.
/// </summary>
public sealed class NoOpMetricsSink : IMetricsSink
{
    /// <summary>Shared singleton instance.</summary>
    public static readonly NoOpMetricsSink Instance = new();

    private NoOpMetricsSink() { }

    /// <inheritdoc/>
    public void IncrementCounter(string name, IReadOnlyDictionary<string, string>? labels = null) { }

    /// <inheritdoc/>
    public void RecordDuration(string name, TimeSpan duration, IReadOnlyDictionary<string, string>? labels = null) { }
}
