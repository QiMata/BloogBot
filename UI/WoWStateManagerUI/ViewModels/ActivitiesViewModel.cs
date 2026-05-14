using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;
using WoWStateManagerUI.Services;

namespace WoWStateManagerUI.ViewModels
{
    /// <summary>
    /// Lists Activities currently running across all connected StateManager instances.
    /// Today an "Activity" is approximated from each bot's <c>currentAction</c> ActionType;
    /// when the proto carries an explicit assigned-activity ID (Phase C) that becomes the
    /// grouping key. The single-instance / single-activity case (today's test setup) shows
    /// one row; multi-StateManager and multi-Activity surfaces appear naturally as the
    /// system grows.
    /// </summary>
    public sealed class ActivitiesViewModel : INotifyPropertyChanged, IDisposable
    {
        private readonly UIListenerService _listener;
        private ActivityGroupViewModel? _selectedActivity;
        private string _statusMessage = "Waiting for StateManager...";

        public ObservableCollection<ActivityGroupViewModel> Activities { get; } = [];

        public ActivityGroupViewModel? SelectedActivity
        {
            get => _selectedActivity;
            set { _selectedActivity = value; OnPropertyChanged(); }
        }

        public string StatusMessage
        {
            get => _statusMessage;
            private set { _statusMessage = value; OnPropertyChanged(); }
        }

        public ActivitiesViewModel(UIListenerService listener)
        {
            _listener = listener;
            _listener.SnapshotsUpdated += OnSnapshotsUpdated;
        }

        private void OnSnapshotsUpdated()
        {
            var dispatcher = Application.Current?.Dispatcher;
            if (dispatcher != null)
                dispatcher.InvokeAsync(Rebuild);
            else
                Rebuild();
        }

        private void Rebuild()
        {
            // Group the listener's cached VMs by (instanceId | action). Same bot
            // appears under a different key when its CurrentAction changes; each
            // group references the live mutable VM (no clone) so binding updates
            // continue to flow.
            var instances = _listener.GetInstances();
            var instanceByAccount = BuildAccountToInstanceMap(instances);

            var newGroups = new Dictionary<string, List<BotSnapshotViewModel>>();
            foreach (var bot in _listener.Bots)
            {
                if (!instanceByAccount.TryGetValue(bot.AccountName, out var instanceId))
                    instanceId = "unknown";

                var actionType = string.IsNullOrEmpty(bot.CurrentAction) ? "(idle)" : bot.CurrentAction;
                var key = $"{instanceId} | {actionType}";

                if (!newGroups.TryGetValue(key, out var list))
                    newGroups[key] = list = new List<BotSnapshotViewModel>();
                list.Add(bot);
            }

            var previousKey = _selectedActivity?.Key;
            Activities.Clear();
            foreach (var kvp in newGroups.OrderBy(g => g.Key))
                Activities.Add(new ActivityGroupViewModel(kvp.Key, kvp.Value));

            if (previousKey != null)
                SelectedActivity = Activities.FirstOrDefault(a => a.Key == previousKey);

            var totalBots = newGroups.Values.Sum(v => v.Count);
            StatusMessage = Activities.Count == 0
                ? "No StateManager connected."
                : $"{Activities.Count} activit{(Activities.Count == 1 ? "y" : "ies")}, {totalBots} bot{(totalBots == 1 ? "" : "s")}";
        }

        private static Dictionary<string, string> BuildAccountToInstanceMap(IReadOnlyDictionary<string, InstanceData> instances)
        {
            var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var kvp in instances)
            {
                foreach (var snap in kvp.Value.Snapshots)
                {
                    if (!string.IsNullOrEmpty(snap.AccountName))
                        map[snap.AccountName] = kvp.Key;
                }
            }
            return map;
        }

        public void Dispose()
        {
            _listener.SnapshotsUpdated -= OnSnapshotsUpdated;
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    /// <summary>A single Activity row: the activity key + the bots running it.</summary>
    public sealed class ActivityGroupViewModel
    {
        public string Key { get; }
        public string Label { get; }
        public ObservableCollection<BotSnapshotViewModel> Bots { get; }

        public ActivityGroupViewModel(string key, List<BotSnapshotViewModel> bots)
        {
            Key = key;
            Label = $"{key} ({bots.Count} bot{(bots.Count == 1 ? "" : "s")})";
            Bots = new ObservableCollection<BotSnapshotViewModel>(bots);
        }
    }
}
