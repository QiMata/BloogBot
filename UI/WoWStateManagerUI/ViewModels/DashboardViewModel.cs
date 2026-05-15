using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using GameData.Core.Enums;
using WoWStateManagerUI.Handlers;
using WoWStateManagerUI.Models;
using WoWStateManagerUI.Services;

namespace WoWStateManagerUI.ViewModels
{
    /// <summary>
    /// Hierarchical dashboard: shows the loaded <see cref="ConfigModel"/> as
    /// Activities, each Activity as a bucket of configured characters, and
    /// joins live snapshot data from <see cref="UIListenerService"/> onto the
    /// matching character rows by AccountName.
    /// </summary>
    public sealed class DashboardViewModel : INotifyPropertyChanged, IDisposable
    {
        private readonly HealthCheckService _healthCheck;
        private readonly UIListenerService _listener;
        private readonly Dictionary<string, DashboardCharacterViewModel> _byAccount =
            new(StringComparer.OrdinalIgnoreCase);

        private ConfigModel _config = new();
        private string? _configPath;
        private DashboardCharacterViewModel? _selectedCharacter;
        private DashboardActivityViewModel? _selectedActivity;
        private string _statusMessage = "No config loaded. Browse to a config file to begin.";

        public HealthCheckService HealthCheck => _healthCheck;

        /// <summary>Configured-Activity buckets bound by the dashboard view.</summary>
        public ObservableCollection<DashboardActivityViewModel> Activities { get; } = [];

        /// <summary>Live snapshots that arrived for accounts not in the loaded config.</summary>
        public ObservableCollection<BotSnapshotViewModel> UnassignedSnapshots { get; } = [];

        public ConfigModel Config
        {
            get => _config;
            private set { _config = value; OnPropertyChanged(); OnPropertyChanged(nameof(ConfigName)); }
        }

        public string ConfigName => string.IsNullOrEmpty(_config.Name) ? "(no config)" : _config.Name;

        public string? ConfigPath
        {
            get => _configPath;
            private set { _configPath = value; OnPropertyChanged(); OnPropertyChanged(nameof(ConfigFileDisplay)); }
        }

        public string ConfigFileDisplay => _configPath != null ? Path.GetFileName(_configPath) : "(none)";

        public DashboardCharacterViewModel? SelectedCharacter
        {
            get => _selectedCharacter;
            set
            {
                _selectedCharacter = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(HasSelection));
                OnPropertyChanged(nameof(SelectedDetail));
            }
        }

        public DashboardActivityViewModel? SelectedActivity
        {
            get => _selectedActivity;
            set { _selectedActivity = value; OnPropertyChanged(); }
        }

        public bool HasSelection => _selectedCharacter != null;

        /// <summary>Stats / spells panel for the currently selected character.</summary>
        public DashboardBotDetailViewModel? SelectedDetail =>
            _selectedCharacter?.LiveSnapshot != null
                ? new DashboardBotDetailViewModel(_selectedCharacter.LiveSnapshot)
                : null;

        public string StatusMessage
        {
            get => _statusMessage;
            private set { _statusMessage = value; OnPropertyChanged(); }
        }

        public string RealmState =>
            _healthCheck.RealmdStatus == ServiceStatus.Up ? "UP" :
            _healthCheck.RealmdStatus == ServiceStatus.Down ? "DOWN" : "UNKNOWN";

        public string WorldState =>
            _healthCheck.MangosdStatus == ServiceStatus.Up ? "UP" :
            _healthCheck.MangosdStatus == ServiceStatus.Down ? "DOWN" : "UNKNOWN";

        public ICommand BrowseConfigCommand { get; }

        public DashboardViewModel(HealthCheckService healthCheck, UIListenerService listener)
        {
            _healthCheck = healthCheck;
            _listener = listener;

            _healthCheck.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName is nameof(HealthCheckService.RealmdStatus) or nameof(HealthCheckService.MangosdStatus))
                {
                    OnPropertyChanged(nameof(RealmState));
                    OnPropertyChanged(nameof(WorldState));
                }
            };

            // Subscribe to listener pushes so live snapshots route into the right buckets.
            _listener.Bots.CollectionChanged += OnListenerBotsChanged;
            foreach (var bot in _listener.Bots)
                AttachBotToBucket(bot);

            BrowseConfigCommand = new CommandHandler(BrowseConfig, true);

            // Auto-load Default.config.json from the centralized configs folder if present.
            if (Directory.Exists(UIConstants.ConfigsDirectory))
            {
                var defaultPath = Path.Combine(UIConstants.ConfigsDirectory, UIConstants.DefaultConfigFileName);
                if (File.Exists(defaultPath))
                {
                    ConfigPath = defaultPath;
                    LoadConfig();
                }
            }
        }

        private void BrowseConfig()
        {
            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                Filter = "JSON files (*.json)|*.json|All files (*.*)|*.*",
                Title = "Select active StateManager config",
                InitialDirectory = Directory.Exists(UIConstants.ConfigsDirectory)
                    ? UIConstants.ConfigsDirectory
                    : null
            };

            if (dialog.ShowDialog() == true)
            {
                ConfigPath = dialog.FileName;
                LoadConfig();
            }
        }

        private void LoadConfig()
        {
            if (string.IsNullOrEmpty(_configPath)) return;

            try
            {
                Config = ConfigFileService.Load(_configPath);
                RebuildActivityBuckets();
                StatusMessage = $"Loaded config '{Config.Name}' — {Config.Activities.Count} activities, {Activities.Sum(a => a.ConfiguredCount)} characters.";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Load failed: {ex.Message}";
            }
        }

        /// <summary>
        /// Throw away the current Activity buckets and rebuild them from the loaded config.
        /// After rebuilding, re-attach any live snapshots that match a configured character.
        /// </summary>
        private void RebuildActivityBuckets()
        {
            Activities.Clear();
            _byAccount.Clear();

            foreach (var activity in Config.Activities)
            {
                var bucket = new DashboardActivityViewModel(activity);
                Activities.Add(bucket);

                foreach (var row in bucket.Characters)
                    _byAccount[row.AccountName] = row;
            }

            // Re-attach existing live snapshots to whichever bucket their account belongs to.
            UnassignedSnapshots.Clear();
            foreach (var bot in _listener.Bots)
                AttachBotToBucket(bot);
        }

        private void OnListenerBotsChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.NewItems != null)
            {
                foreach (BotSnapshotViewModel bot in e.NewItems)
                    AttachBotToBucket(bot);
            }
            if (e.OldItems != null)
            {
                foreach (BotSnapshotViewModel bot in e.OldItems)
                    DetachBotFromBucket(bot);
            }
            if (e.Action == NotifyCollectionChangedAction.Reset)
            {
                foreach (var row in _byAccount.Values)
                    row.LiveSnapshot = null;
                UnassignedSnapshots.Clear();
            }
        }

        private void AttachBotToBucket(BotSnapshotViewModel bot)
        {
            if (string.IsNullOrEmpty(bot.AccountName))
                return;

            if (_byAccount.TryGetValue(bot.AccountName, out var row))
            {
                row.LiveSnapshot = bot;
                FindOwningActivity(row)?.RaiseLiveChanged();
            }
            else if (!UnassignedSnapshots.Contains(bot))
            {
                UnassignedSnapshots.Add(bot);
            }
        }

        private void DetachBotFromBucket(BotSnapshotViewModel bot)
        {
            if (string.IsNullOrEmpty(bot.AccountName))
                return;

            if (_byAccount.TryGetValue(bot.AccountName, out var row))
            {
                row.LiveSnapshot = null;
                FindOwningActivity(row)?.RaiseLiveChanged();
            }
            else
            {
                UnassignedSnapshots.Remove(bot);
            }
        }

        private DashboardActivityViewModel? FindOwningActivity(DashboardCharacterViewModel row)
            => Activities.FirstOrDefault(a => a.Characters.Contains(row));

        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

        public void Dispose()
        {
            _listener.Bots.CollectionChanged -= OnListenerBotsChanged;
        }
    }

    /// <summary>
    /// Detail-pane view-model for the selected character's live snapshot.
    /// Stats / spells / travel come from the underlying snapshot.
    /// </summary>
    public sealed class DashboardBotDetailViewModel
    {
        public BotSnapshotViewModel Snapshot { get; }
        public List<StatRow> Stats { get; }
        public int SpellCount { get; }
        public string Travel { get; }
        public string LoadoutStatus { get; }

        public DashboardBotDetailViewModel(BotSnapshotViewModel snap)
        {
            Snapshot = snap;
            var raw = snap.Raw;
            var statMap = raw.Player?.Unit?.Stats;
            Stats = [];
            if (statMap != null)
            {
                foreach (var kvp in statMap.OrderBy(k => k.Key))
                {
                    var label = Enum.IsDefined(typeof(StatType), (int)kvp.Key)
                        ? ((StatType)kvp.Key).ToString()
                        : $"stat {kvp.Key}";
                    Stats.Add(new StatRow(label, kvp.Value));
                }
            }
            SpellCount = raw.Player?.SpellList?.Count ?? 0;
            Travel = raw.TravelObjective != null
                ? $"map {raw.TravelObjective.TargetMapId} → {raw.TravelObjective.TargetLocationName}"
                : "(none)";
            LoadoutStatus = raw.LoadoutStatus.ToString();
        }

        public sealed record StatRow(string Name, uint Value);
    }
}
