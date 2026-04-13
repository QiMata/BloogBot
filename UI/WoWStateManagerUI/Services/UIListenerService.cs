using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using BotCommLayer;
using Communication;
using Microsoft.Extensions.Logging;

namespace WoWStateManagerUI.Services
{
    /// <summary>
    /// Listens on a TCP port for incoming StateManager snapshot pushes.
    /// StateManagers connect as clients and send StateChangeResponse messages
    /// containing their current bot snapshots. The UI stores these for display.
    /// </summary>
    public sealed class UIListenerService : ProtobufPipelineSocketServer<StateChangeResponse, StateChangeResponse>
    {
        /// <summary>
        /// Keyed by StateManager identifier (derived from first snapshot's accountName or remote endpoint).
        /// Value is the latest batch of snapshots from that StateManager.
        /// </summary>
        private readonly ConcurrentDictionary<string, InstanceData> _instances = new();

        /// <summary>Raised when snapshot data changes from any instance.</summary>
        public event Action? SnapshotsUpdated;

        public UIListenerService(string ipAddress, int port, ILogger logger)
            : base(ipAddress, port, logger, maxConcurrency: 64)
        {
        }

        protected override StateChangeResponse HandleRequest(StateChangeResponse incoming)
        {
            if (incoming.Snapshots.Count == 0)
            {
                // Heartbeat / empty push — acknowledge
                return new StateChangeResponse { Response = ResponseResult.Success };
            }

            // Use the first snapshot's accountName as instance key, or fall back to a generated ID
            var firstSnapshot = incoming.Snapshots[0];
            var instanceId = !string.IsNullOrEmpty(firstSnapshot.AccountName)
                ? DeriveInstanceId(firstSnapshot.AccountName)
                : $"unknown-{ActiveConnections}";

            var snapshots = incoming.Snapshots.ToList();

            _instances.AddOrUpdate(
                instanceId,
                _ => new InstanceData(instanceId, snapshots, DateTime.UtcNow, DateTime.UtcNow),
                (_, existing) => existing with { Snapshots = snapshots, LastUpdate = DateTime.UtcNow }
            );

            _logger.LogDebug("Received {Count} snapshots from instance {Id}", snapshots.Count, instanceId);

            try { SnapshotsUpdated?.Invoke(); } catch { /* UI callback failures shouldn't crash the server */ }

            return new StateChangeResponse { Response = ResponseResult.Success };
        }

        /// <summary>
        /// Derive a StateManager instance ID from an account name.
        /// Convention: all accounts from the same StateManager share a naming pattern.
        /// We group by stripping trailing digits (e.g., TESTBOT1, TESTBOT2 → TESTBOT).
        /// </summary>
        private static string DeriveInstanceId(string accountName)
        {
            var trimmed = accountName.TrimEnd('0', '1', '2', '3', '4', '5', '6', '7', '8', '9');
            return string.IsNullOrEmpty(trimmed) ? accountName : trimmed;
        }

        /// <summary>Get all currently tracked instances and their latest snapshots.</summary>
        public IReadOnlyDictionary<string, InstanceData> GetInstances()
            => _instances;

        /// <summary>Remove instances that haven't updated in the given timespan.</summary>
        public void PruneStaleInstances(TimeSpan maxAge)
        {
            var cutoff = DateTime.UtcNow - maxAge;
            foreach (var kvp in _instances)
            {
                if (kvp.Value.LastUpdate < cutoff)
                    _instances.TryRemove(kvp.Key, out _);
            }
        }
    }

    /// <summary>Immutable snapshot of a connected StateManager instance.</summary>
    public record InstanceData(
        string InstanceId,
        List<WoWActivitySnapshot> Snapshots,
        DateTime ConnectedAt,
        DateTime LastUpdate
    );
}
