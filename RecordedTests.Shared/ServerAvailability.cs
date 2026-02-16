namespace RecordedTests.Shared;

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using RecordedTests.Shared.Abstractions;
using RecordedTests.Shared.Abstractions.I;

public sealed class TrueNasAppServerAvailabilityChecker : IServerAvailabilityChecker
{
    private readonly IMangosAppsClient _client;
    private readonly IReadOnlyList<Candidate> _candidates;
    private readonly TimeSpan _pollInterval;
    private readonly ITestLogger _logger;

    public TrueNasAppServerAvailabilityChecker(IMangosAppsClient client, IEnumerable<string> serverDefinitions, TimeSpan? pollInterval = null, ITestLogger? logger = null)
    {
        _client = client ?? throw new ArgumentNullException(nameof(client));
        ArgumentNullException.ThrowIfNull(serverDefinitions);

        var parsed = serverDefinitions
            .Select(ParseCandidate)
            .ToArray();

        if (parsed.Length == 0)
        {
            throw new ArgumentException("At least one server definition must be provided.", nameof(serverDefinitions));
        }

        _candidates = parsed;
        _pollInterval = pollInterval ?? TimeSpan.FromSeconds(2);
        if (_pollInterval < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(pollInterval), "Poll interval must be non-negative.");
        }

        _logger = logger ?? new NullTestLogger();
    }

    public async Task<ServerInfo?> WaitForAvailableAsync(TimeSpan timeout, CancellationToken cancellationToken)
    {
        DateTimeOffset? deadline = null;
        if (timeout != Timeout.InfiniteTimeSpan)
        {
            try
            {
                deadline = DateTimeOffset.UtcNow + timeout;
            }
            catch (ArgumentOutOfRangeException)
            {
                deadline = DateTimeOffset.MaxValue;
            }
        }

        while (deadline is null || DateTimeOffset.UtcNow < deadline.Value)
        {
            foreach (var candidate in _candidates)
            {
                cancellationToken.ThrowIfCancellationRequested();

                TrueNasAppRelease? release;
                try
                {
                    release = await _client.GetReleaseAsync(candidate.ReleaseName, cancellationToken).ConfigureAwait(false);
                }
                catch (HttpRequestException ex)
                {
                    _logger.Warn($"[ServerAvailability] Failed to query TrueNAS release '{candidate.ReleaseName}': {ex.Message}");
                    continue;
                }

                if (release is null)
                {
                    _logger.Warn($"[ServerAvailability] TrueNAS release '{candidate.ReleaseName}' not found.");
                    continue;
                }

                if (release.IsCheckedOut)
                {
                    _logger.Info($"[ServerAvailability] Release '{candidate.ReleaseName}' is checked out; skipping.");
                    continue;
                }

                if (!release.IsRunning)
                {
                    try
                    {
                        _logger.Info($"[ServerAvailability] Starting release '{candidate.ReleaseName}'...");
                        await _client.StartReleaseAsync(candidate.ReleaseName, cancellationToken).ConfigureAwait(false);
                    }
                    catch (HttpRequestException ex)
                    {
                        _logger.Warn($"[ServerAvailability] Failed to start release '{candidate.ReleaseName}': {ex.Message}");
                    }

                    continue;
                }

                var host = !string.IsNullOrWhiteSpace(release.Host) ? release.Host : candidate.Host;
                var port = release.Port ?? candidate.Port;
                var realm = release.Realm ?? candidate.Realm;

                if (string.IsNullOrWhiteSpace(host))
                {
                    _logger.Warn($"[ServerAvailability] Release '{candidate.ReleaseName}' missing host information; skipping.");
                    continue;
                }

                _logger.Info($"[ServerAvailability] Selected release '{candidate.ReleaseName}' -> {host}:{port}.");
                return new ServerInfo(host, port, realm);
            }

            if (deadline is not null && DateTimeOffset.UtcNow >= deadline.Value)
            {
                break;
            }

            if (_pollInterval > TimeSpan.Zero)
            {
                try
                {
                    await Task.Delay(_pollInterval, cancellationToken).ConfigureAwait(false);
                }
                catch (TaskCanceledException)
                {
                    // Cancellation requested; loop will exit on next iteration.
                }
            }
        }

        return null;
    }

    // Definitions use "releaseName|host|port[|realm]" so we can pair chart releases with connection metadata.
    private static Candidate ParseCandidate(string definition)
    {
        if (string.IsNullOrWhiteSpace(definition))
        {
            throw new ArgumentException("Server definition cannot be null or whitespace.", nameof(definition));
        }

        var parts = definition.Split('|', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 3)
        {
            throw new ArgumentException("Server definition must follow 'releaseName|host|port[|realm]' format.", nameof(definition));
        }

        if (!int.TryParse(parts[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out var port))
        {
            throw new ArgumentException($"Unable to parse port from '{definition}'.", nameof(definition));
        }

        var realm = parts.Length > 3 ? parts[3] : null;
        return new Candidate(parts[0], parts[1], port, realm);
    }

    private readonly record struct Candidate(string ReleaseName, string Host, int Port, string? Realm);
}
