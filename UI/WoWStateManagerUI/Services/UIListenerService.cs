using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using BotCommLayer;
using Communication;
using Microsoft.Extensions.Logging;
using WoWStateManagerUI.ViewModels;

namespace WoWStateManagerUI.Services
{
    /// <summary>
    /// Listens on a TCP port for incoming StateManager snapshot pushes. StateManagers
    /// connect as clients and send <see cref="StateChangeResponse"/> messages carrying
    /// the bots whose state has changed since the last push.
    ///
    /// The listener owns a long-lived per-account <see cref="BotSnapshotViewModel"/>
    /// cache. Incoming snapshots are merged into the matching VM in-place so WPF
    /// bindings see PropertyChanged only for the fields that actually changed.
    /// </summary>
    public sealed class UIListenerService : ProtobufPipelineSocketServer<StateChangeResponse, StateChangeResponse>
    {
        /// <summary>
        /// Keyed by StateManager identifier (derived from first snapshot's accountName
        /// or remote endpoint). Value is the latest batch of snapshots from that
        /// StateManager. Kept for compatibility with the previous Instances surface.
        /// </summary>
        private readonly ConcurrentDictionary<string, InstanceData> _instances = new();

        /// <summary>
        /// Per-account cache of mutable bot VMs. The same VM instance is updated in
        /// place across pushes, so bindings (selection, detail panel) stay stable.
        /// </summary>
        private readonly Dictionary<string, BotSnapshotViewModel> _botsByAccount = new(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Observable bot list bound directly by Dashboard / Activities. The collection
        /// is mutated on the WPF dispatcher thread.
        /// </summary>
        public ObservableCollection<BotSnapshotViewModel> Bots { get; } = [];

        /// <summary>Raised after a snapshot batch is processed (legacy hook).</summary>
        public event Action? SnapshotsUpdated;

        public UIListenerService(string ipAddress, int port, ILogger logger)
            : base(ipAddress, port, logger, maxConcurrency: 64)
        {
        }

        protected override StateChangeResponse HandleRequest(StateChangeResponse incoming)
        {
            if (incoming.Snapshots.Count == 0)
            {
                return new StateChangeResponse { Response = ResponseResult.Success };
            }

            // Track per-StateManager-instance metadata for the legacy InstanceData surface.
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

            // Apply the per-bot updates on the WPF dispatcher so collection mutations
            // and PropertyChanged events stay on the UI thread.
            var dispatcher = Application.Current?.Dispatcher;
            if (dispatcher != null)
            {
                dispatcher.InvokeAsync(() => ApplyBotUpdates(snapshots));
            }
            else
            {
                // Test/headless: apply directly.
                ApplyBotUpdates(snapshots);
            }

            try { SnapshotsUpdated?.Invoke(); } catch { /* UI callback failures shouldn't crash the server */ }

            return new StateChangeResponse { Response = ResponseResult.Success };
        }

        private void ApplyBotUpdates(List<WoWActivitySnapshot> snapshots)
        {
            foreach (var snap in snapshots)
            {
                if (string.IsNullOrEmpty(snap.AccountName)) continue;

                if (_botsByAccount.TryGetValue(snap.AccountName, out var existing))
                {
                    existing.Update(snap);
                }
                else
                {
                    var vm = new BotSnapshotViewModel(snap);
                    _botsByAccount[snap.AccountName] = vm;
                    Bots.Add(vm);
                }
            }
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
