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
            Application.Current?.Dispatcher?.InvokeAsync(() =>
            {
                var snapshotsByInstance = _listener.GetInstances();

                // Build new grouping keyed by (instanceId, actionType)
                var newGroups = new Dictionary<string, List<BotSnapshotViewModel>>();
                foreach (var kvp in snapshotsByInstance)
                {
                    foreach (var snap in kvp.Value.Snapshots)
                    {
                        var actionType = snap.CurrentAction?.ActionType.ToString() ?? "(idle)";
                        var key = $"{kvp.Key} | {actionType}";
                        if (!newGroups.TryGetValue(key, out var list))
                            newGroups[key] = list = new List<BotSnapshotViewModel>();
                        list.Add(new BotSnapshotViewModel(snap));
                    }
                }

                // Rebuild Activities collection, preserving SelectedActivity if its key still exists
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
            });
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
