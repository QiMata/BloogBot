using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows.Threading;
using System.Windows.Input;
using WoWStateManagerUI.Handlers;
using WoWStateManagerUI.Services;

namespace WoWStateManagerUI.ViewModels
{
    /// <summary>
    /// Docker container management for the running WWoW stack. Auto-connects on
    /// startup and auto-refreshes every <see cref="UIConstants.ServicesRefreshSeconds"/>.
    /// Project filtering is intentionally absent — this UI is WWoW-specific and the
    /// container list is filtered to Project=='WWoW' so other game stacks (FFXI, etc.)
    /// running alongside on the same Docker host don't show.
    /// </summary>
    public sealed class ServicesViewModel : INotifyPropertyChanged, IDisposable
    {
        private const string WwowProject = "WWoW";

        private readonly DockerService _docker = new();
        private readonly DispatcherTimer _refreshTimer;
        private ContainerInfo? _selectedContainer;
        private string _statusMessage = "Connecting to Docker...";
        private string _logOutput = string.Empty;
        private bool _isConnected;
        private bool _refreshInFlight;

        public ObservableCollection<ContainerInfo> Containers { get; } = [];

        public ContainerInfo? SelectedContainer
        {
            get => _selectedContainer;
            set { _selectedContainer = value; OnPropertyChanged(); OnPropertyChanged(nameof(SelectedName)); }
        }

        public string SelectedName => _selectedContainer?.Name ?? "(none)";

        public string StatusMessage
        {
            get => _statusMessage;
            private set { _statusMessage = value; OnPropertyChanged(); }
        }

        public string LogOutput
        {
            get => _logOutput;
            private set { _logOutput = value; OnPropertyChanged(); }
        }

        public bool IsConnected
        {
            get => _isConnected;
            private set { _isConnected = value; OnPropertyChanged(); }
        }

        public ICommand RefreshCommand { get; }
        public ICommand StartCommand { get; }
        public ICommand StopCommand { get; }
        public ICommand RestartCommand { get; }
        public ICommand ViewLogsCommand { get; }

        public ServicesViewModel()
        {
            RefreshCommand = new AsyncCommandHandler(RefreshAsync, () => IsConnected);
            StartCommand = new AsyncCommandHandler(StartSelectedAsync, () => IsConnected && _selectedContainer != null);
            StopCommand = new AsyncCommandHandler(StopSelectedAsync, () => IsConnected && _selectedContainer != null);
            RestartCommand = new AsyncCommandHandler(RestartSelectedAsync, () => IsConnected && _selectedContainer != null);
            ViewLogsCommand = new AsyncCommandHandler(ViewLogsAsync, () => IsConnected && _selectedContainer != null);

            _refreshTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(UIConstants.ServicesRefreshSeconds)
            };
            _refreshTimer.Tick += async (_, _) => await TickAsync();

            _ = AutoConnectAsync();
        }

        private async Task AutoConnectAsync()
        {
            IsConnected = await _docker.TestDockerAvailableAsync();
            StatusMessage = IsConnected ? "Docker connected — auto-refreshing" : "Docker not available — is Docker Desktop running?";
            if (IsConnected)
            {
                await RefreshAsync();
                _refreshTimer.Start();
            }
            RefreshCanExecute();
        }

        private async Task TickAsync()
        {
            if (_refreshInFlight || !IsConnected) return;
            await RefreshAsync();
        }

        private async Task RefreshAsync()
        {
            if (_refreshInFlight) return;
            _refreshInFlight = true;
            try
            {
                var all = await _docker.ListContainersAsync();
                var wwow = all.Where(c => string.Equals(c.Project, WwowProject, StringComparison.OrdinalIgnoreCase)).ToList();

                var selectedName = _selectedContainer?.Name;

                Containers.Clear();
                foreach (var c in wwow)
                    Containers.Add(c);

                // Preserve selection by name across refresh cycles
                if (selectedName != null)
                    SelectedContainer = Containers.FirstOrDefault(c => c.Name == selectedName);

                var running = wwow.Count(c => c.State == "running");
                var healthy = wwow.Count(c => c.IsHealthy);
                StatusMessage = $"{wwow.Count} WWoW containers ({running} running, {healthy} healthy)";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Refresh failed: {ex.Message}";
            }
            finally
            {
                _refreshInFlight = false;
            }
        }

        private async Task StartSelectedAsync()
        {
            if (_selectedContainer == null) return;
            StatusMessage = $"Starting {_selectedContainer.Name}...";
            var result = await _docker.StartContainerAsync(_selectedContainer.Name);
            StatusMessage = $"Start {_selectedContainer.Name}: {(string.IsNullOrEmpty(result) ? "OK" : result)}";
            await RefreshAsync();
        }

        private async Task StopSelectedAsync()
        {
            if (_selectedContainer == null) return;
            StatusMessage = $"Stopping {_selectedContainer.Name}...";
            var result = await _docker.StopContainerAsync(_selectedContainer.Name);
            StatusMessage = $"Stop {_selectedContainer.Name}: {(string.IsNullOrEmpty(result) ? "OK" : result)}";
            await RefreshAsync();
        }

        private async Task RestartSelectedAsync()
        {
            if (_selectedContainer == null) return;
            StatusMessage = $"Restarting {_selectedContainer.Name}...";
            var result = await _docker.RestartContainerAsync(_selectedContainer.Name);
            StatusMessage = $"Restart {_selectedContainer.Name}: {(string.IsNullOrEmpty(result) ? "OK" : result)}";
            await RefreshAsync();
        }

        private async Task ViewLogsAsync()
        {
            if (_selectedContainer == null) return;
            StatusMessage = $"Fetching logs for {_selectedContainer.Name}...";
            LogOutput = await _docker.GetLogsAsync(_selectedContainer.Name, 100);
            StatusMessage = $"Showing last 100 lines from {_selectedContainer.Name}";
        }

        private void RefreshCanExecute()
        {
            (RefreshCommand as AsyncCommandHandler)?.RaiseCanExecuteChanged();
            (StartCommand as AsyncCommandHandler)?.RaiseCanExecuteChanged();
            (StopCommand as AsyncCommandHandler)?.RaiseCanExecuteChanged();
            (RestartCommand as AsyncCommandHandler)?.RaiseCanExecuteChanged();
            (ViewLogsCommand as AsyncCommandHandler)?.RaiseCanExecuteChanged();
        }

        public void Dispose()
        {
            _refreshTimer.Stop();
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
            if (propertyName == nameof(SelectedContainer))
                RefreshCanExecute();
        }
    }
}
