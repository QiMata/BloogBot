using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Input;
using WoWStateManagerUI.Handlers;
using WoWStateManagerUI.Services;

namespace WoWStateManagerUI.ViewModels
{
    public sealed class InstancesViewModel : INotifyPropertyChanged, IDisposable
    {
        private UIListenerService? _listener;
        private string _listenAddress = "127.0.0.1";
        private int _listenPort = 9090;
        private bool _isListening;
        private string _statusMessage = string.Empty;
        private InstanceViewModel? _selectedInstance;

        public ObservableCollection<InstanceViewModel> Instances { get; } = [];

        public string ListenAddress { get => _listenAddress; set { _listenAddress = value; OnPropertyChanged(); } }
        public int ListenPort { get => _listenPort; set { _listenPort = value; OnPropertyChanged(); } }

        public bool IsListening
        {
            get => _isListening;
            private set { _isListening = value; OnPropertyChanged(); }
        }

        public string StatusMessage
        {
            get => _statusMessage;
            private set { _statusMessage = value; OnPropertyChanged(); }
        }

        public InstanceViewModel? SelectedInstance
        {
            get => _selectedInstance;
            set { _selectedInstance = value; OnPropertyChanged(); }
        }

        public ICommand StartListenerCommand { get; }
        public ICommand StopListenerCommand { get; }

        public InstancesViewModel()
        {
            StartListenerCommand = new CommandHandler(StartListener, true);
            StopListenerCommand = new CommandHandler(StopListener, true);
        }

        private void StartListener()
        {
            if (_isListening) return;

            try
            {
                var logger = new DebugLogger();
                _listener = new UIListenerService(_listenAddress, _listenPort, logger);
                _listener.SnapshotsUpdated += OnSnapshotsUpdated;
                IsListening = true;
                StatusMessage = $"Listening on {_listenAddress}:{_listenPort}";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Failed to start listener: {ex.Message}";
            }
        }

        private void StopListener()
        {
            if (_listener != null)
            {
                _listener.SnapshotsUpdated -= OnSnapshotsUpdated;
                _listener.Dispose();
                _listener = null;
            }
            IsListening = false;
            Instances.Clear();
            StatusMessage = "Listener stopped";
        }

        private void OnSnapshotsUpdated()
        {
            if (_listener == null) return;

            // Marshal to UI thread
            Application.Current?.Dispatcher?.InvokeAsync(() =>
            {
                var instanceData = _listener.GetInstances();

                // Update or add instances
                foreach (var kvp in instanceData)
                {
                    var existing = Instances.FirstOrDefault(i => i.InstanceId == kvp.Key);
                    if (existing != null)
                    {
                        existing.UpdateSnapshots(kvp.Value);
                    }
                    else
                    {
                        var vm = new InstanceViewModel(kvp.Value);
                        Instances.Add(vm);
                    }
                }

                // Remove stale instances
                var currentIds = instanceData.Keys.ToHashSet();
                for (int i = Instances.Count - 1; i >= 0; i--)
                {
                    if (!currentIds.Contains(Instances[i].InstanceId))
                        Instances.RemoveAt(i);
                }

                StatusMessage = $"Listening — {Instances.Count} instance(s), {Instances.Sum(i => i.Bots.Count)} bot(s)";
            });
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

        public void Dispose() => StopListener();

        private sealed class DebugLogger : Microsoft.Extensions.Logging.ILogger
        {
            public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
            public bool IsEnabled(Microsoft.Extensions.Logging.LogLevel logLevel) => logLevel >= Microsoft.Extensions.Logging.LogLevel.Warning;
            public void Log<TState>(Microsoft.Extensions.Logging.LogLevel logLevel, Microsoft.Extensions.Logging.EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
            {
                if (logLevel >= Microsoft.Extensions.Logging.LogLevel.Warning)
                    System.Diagnostics.Debug.WriteLine($"[UIListener] {formatter(state, exception)}");
            }
        }
    }

    /// <summary>Represents a connected StateManager instance with its bot snapshots.</summary>
    public sealed class InstanceViewModel : INotifyPropertyChanged
    {
        private string _instanceId;
        private DateTime _connectedAt;
        private DateTime _lastUpdate;

        public string InstanceId { get => _instanceId; private set { _instanceId = value; OnPropertyChanged(); } }
        public DateTime ConnectedAt { get => _connectedAt; private set { _connectedAt = value; OnPropertyChanged(); } }
        public DateTime LastUpdate { get => _lastUpdate; private set { _lastUpdate = value; OnPropertyChanged(); } }

        public ObservableCollection<BotSnapshotViewModel> Bots { get; } = [];

        public InstanceViewModel(InstanceData data)
        {
            _instanceId = data.InstanceId;
            _connectedAt = data.ConnectedAt;
            _lastUpdate = data.LastUpdate;
            UpdateSnapshots(data);
        }

        public void UpdateSnapshots(InstanceData data)
        {
            LastUpdate = data.LastUpdate;

            // Rebuild bot list from snapshots
            Bots.Clear();
            foreach (var snapshot in data.Snapshots)
                Bots.Add(new BotSnapshotViewModel(snapshot));
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    /// <summary>A single bot's snapshot data for display.</summary>
    public sealed class BotSnapshotViewModel
    {
        public string AccountName { get; }
        public string CharacterName { get; }
        public string ScreenState { get; }
        public uint Level { get; }
        public uint MapId { get; }
        public int Health { get; }
        public string ConnectionState { get; }
        public string CurrentAction { get; }
        public string Position { get; }

        public BotSnapshotViewModel(Communication.WoWActivitySnapshot snapshot)
        {
            AccountName = snapshot.AccountName ?? "";
            CharacterName = snapshot.CharacterName ?? "";
            ScreenState = snapshot.ScreenState ?? "";
            // Proto hierarchy: WoWPlayer -> Unit (WoWUnit) -> GameObject (WoWGameObject) -> Base (WoWObject)
            var unit = snapshot.Player?.Unit;
            var baseObj = unit?.GameObject?.Base;
            Level = unit?.GameObject?.Level ?? 0;
            MapId = snapshot.CurrentMapId;
            Health = (int)(unit?.Health ?? 0);
            ConnectionState = snapshot.ConnectionState.ToString();
            CurrentAction = snapshot.CurrentAction?.ActionType.ToString() ?? "";
            Position = baseObj?.Position != null
                ? $"({baseObj.Position.X:F0}, {baseObj.Position.Y:F0}, {baseObj.Position.Z:F0})"
                : "";
        }
    }
}
