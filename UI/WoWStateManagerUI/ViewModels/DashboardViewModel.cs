using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Input;
using GameData.Core.Enums;
using WoWStateManagerUI.Handlers;
using WoWStateManagerUI.Services;

namespace WoWStateManagerUI.ViewModels
{
    public sealed class DashboardViewModel : INotifyPropertyChanged, IDisposable
    {
        private readonly HealthCheckService _healthCheck;
        private readonly UIListenerService _listener;

        private BotSnapshotViewModel? _selectedBot;
        private DashboardBotDetailViewModel? _selectedDetail;
        private string _stateManagerExePath = string.Empty;
        private string _statusMessage = string.Empty;

        public HealthCheckService HealthCheck => _healthCheck;
        public ProcessLauncherService ProcessLauncher { get; }

        public ObservableCollection<BotSnapshotViewModel> Bots { get; } = [];

        public ICommand LaunchStateManagerCommand { get; }
        public ICommand StopStateManagerCommand { get; }
        public ICommand BrowseExeCommand { get; }

        public string RealmState =>
            _healthCheck.RealmdStatus == ServiceStatus.Up ? "UP" :
            _healthCheck.RealmdStatus == ServiceStatus.Down ? "DOWN" : "UNKNOWN";

        public string WorldState =>
            _healthCheck.MangosdStatus == ServiceStatus.Up ? "UP" :
            _healthCheck.MangosdStatus == ServiceStatus.Down ? "DOWN" : "UNKNOWN";

        public string TotalPopulation =>
            _healthCheck.RealmdStatus == ServiceStatus.Up &&
            _healthCheck.MangosdStatus == ServiceStatus.Up ? "3000" : "0";

        public string StateManagerExePath
        {
            get => _stateManagerExePath;
            set { _stateManagerExePath = value; OnPropertyChanged(); }
        }

        public string StatusMessage
        {
            get => _statusMessage;
            private set { _statusMessage = value; OnPropertyChanged(); }
        }

        public BotSnapshotViewModel? SelectedBot
        {
            get => _selectedBot;
            set
            {
                _selectedBot = value;
                _selectedDetail = value == null ? null : new DashboardBotDetailViewModel(value);
                OnPropertyChanged();
                OnPropertyChanged(nameof(SelectedDetail));
                OnPropertyChanged(nameof(HasSelection));
                OnPropertyChanged(nameof(HasNoSelection));
            }
        }

        public bool HasSelection => _selectedBot != null;
        public bool HasNoSelection => _selectedBot == null;

        public DashboardBotDetailViewModel? SelectedDetail => _selectedDetail;

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
                    OnPropertyChanged(nameof(TotalPopulation));
                }
            };

            _listener.SnapshotsUpdated += OnSnapshotsUpdated;

            ProcessLauncher = new ProcessLauncherService();
            LaunchStateManagerCommand = new CommandHandler(LaunchStateManager, true);
            StopStateManagerCommand = new CommandHandler(StopStateManager, true);
            BrowseExeCommand = new CommandHandler(BrowseExe, true);
        }

        private void OnSnapshotsUpdated()
        {
            Application.Current?.Dispatcher?.InvokeAsync(() =>
            {
                var selectedAccount = _selectedBot?.AccountName;

                Bots.Clear();
                foreach (var kvp in _listener.GetInstances())
                {
                    foreach (var snap in kvp.Value.Snapshots)
                        Bots.Add(new BotSnapshotViewModel(snap));
                }

                if (selectedAccount != null)
                    SelectedBot = Bots.FirstOrDefault(b => b.AccountName == selectedAccount);
            });
        }

        private void BrowseExe()
        {
            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                Filter = "Executable (*.exe)|*.exe",
                Title = "Select StateManager executable"
            };
            if (dialog.ShowDialog() == true)
                StateManagerExePath = dialog.FileName;
        }

        private void LaunchStateManager()
        {
            if (string.IsNullOrWhiteSpace(_stateManagerExePath))
            {
                StatusMessage = "Set StateManager exe path first";
                return;
            }
            StatusMessage = ProcessLauncher.Launch(_stateManagerExePath);
        }

        private void StopStateManager() => StatusMessage = ProcessLauncher.Stop();

        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

        public void Dispose()
        {
            _listener.SnapshotsUpdated -= OnSnapshotsUpdated;
            ProcessLauncher.Dispose();
        }
    }

    /// <summary>
    /// Detail-pane view-model for the selected bot. Surfaces stats, spell count, and
    /// other per-character data that exists in the snapshot today. Stats and spells will
    /// be expanded once the Phase C proto extension adds explicit fields.
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
