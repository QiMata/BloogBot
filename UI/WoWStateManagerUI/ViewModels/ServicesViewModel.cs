using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using WoWStateManagerUI.Handlers;
using WoWStateManagerUI.Services;

namespace WoWStateManagerUI.ViewModels
{
    public sealed class ServicesViewModel : INotifyPropertyChanged
    {
        private readonly DockerService _docker = new();
        private ContainerInfo? _selectedContainer;
        private string _statusMessage = string.Empty;
        private string _logOutput = string.Empty;
        private bool _isConnected;
        private string _filterProject = "All";

        public ObservableCollection<ContainerInfo> Containers { get; } = [];
        public ObservableCollection<ContainerInfo> FilteredContainers { get; } = [];
        public ObservableCollection<string> ProjectFilters { get; } = ["All"];

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

        public string FilterProject
        {
            get => _filterProject;
            set { _filterProject = value; OnPropertyChanged(); ApplyFilter(); }
        }

        public ICommand ConnectCommand { get; }
        public ICommand RefreshCommand { get; }
        public ICommand StartCommand { get; }
        public ICommand StopCommand { get; }
        public ICommand RestartCommand { get; }
        public ICommand ViewLogsCommand { get; }
        public ICommand RestartWwowStackCommand { get; }
        public ICommand StopWwowStackCommand { get; }
        public ICommand StartWwowStackCommand { get; }

        public ServicesViewModel()
        {
            ConnectCommand = new AsyncCommandHandler(ConnectAsync);
            RefreshCommand = new AsyncCommandHandler(RefreshAsync, () => IsConnected);
            StartCommand = new AsyncCommandHandler(StartSelectedAsync, () => IsConnected && _selectedContainer != null);
            StopCommand = new AsyncCommandHandler(StopSelectedAsync, () => IsConnected && _selectedContainer != null);
            RestartCommand = new AsyncCommandHandler(RestartSelectedAsync, () => IsConnected && _selectedContainer != null);
            ViewLogsCommand = new AsyncCommandHandler(ViewLogsAsync, () => IsConnected && _selectedContainer != null);
            RestartWwowStackCommand = new AsyncCommandHandler(() => StackOperationAsync("WWoW", "restart"), () => IsConnected);
            StopWwowStackCommand = new AsyncCommandHandler(() => StackOperationAsync("WWoW", "stop"), () => IsConnected);
            StartWwowStackCommand = new AsyncCommandHandler(() => StackOperationAsync("WWoW", "start"), () => IsConnected);
        }

        private async Task ConnectAsync()
        {
            IsConnected = await _docker.TestDockerAvailableAsync();
            StatusMessage = IsConnected ? "Docker CLI connected" : "Docker not available — is Docker Desktop running?";
            if (IsConnected)
                await RefreshAsync();
            RefreshCanExecute();
        }

        private async Task RefreshAsync()
        {
            try
            {
                var containers = await _docker.ListContainersAsync();
                Containers.Clear();
                ProjectFilters.Clear();
                ProjectFilters.Add("All");

                foreach (var c in containers)
                    Containers.Add(c);

                foreach (var project in containers.Select(c => c.Project).Distinct().OrderBy(p => p))
                {
                    if (!ProjectFilters.Contains(project))
                        ProjectFilters.Add(project);
                }

                ApplyFilter();

                var running = containers.Count(c => c.State == "running");
                var healthy = containers.Count(c => c.IsHealthy);
                StatusMessage = $"{containers.Count} containers ({running} running, {healthy} healthy)";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Refresh failed: {ex.Message}";
            }
        }

        private void ApplyFilter()
        {
            FilteredContainers.Clear();
            foreach (var c in Containers)
            {
                if (_filterProject == "All" || c.Project == _filterProject)
                    FilteredContainers.Add(c);
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

        private async Task StackOperationAsync(string project, string operation)
        {
            var targets = Containers.Where(c => c.Project == project).ToList();
            if (targets.Count == 0)
            {
                StatusMessage = $"No {project} containers found";
                return;
            }

            // Order matters for start (DB first) and stop (services first)
            var ordered = operation == "start"
                ? targets.OrderBy(c => c.Name.Contains("maria") || c.Name.Contains("db") ? 0 : 1)
                : targets.OrderBy(c => c.Name.Contains("maria") || c.Name.Contains("db") ? 1 : 0);

            StatusMessage = $"{operation} {targets.Count} {project} containers...";

            foreach (var container in ordered)
            {
                StatusMessage = $"{operation} {container.Name}...";
                switch (operation)
                {
                    case "start": await _docker.StartContainerAsync(container.Name); break;
                    case "stop": await _docker.StopContainerAsync(container.Name); break;
                    case "restart": await _docker.RestartContainerAsync(container.Name); break;
                }
            }

            StatusMessage = $"{operation} complete for {targets.Count} {project} containers";
            await RefreshAsync();
        }

        private void RefreshCanExecute()
        {
            (RefreshCommand as AsyncCommandHandler)?.RaiseCanExecuteChanged();
            (StartCommand as AsyncCommandHandler)?.RaiseCanExecuteChanged();
            (StopCommand as AsyncCommandHandler)?.RaiseCanExecuteChanged();
            (RestartCommand as AsyncCommandHandler)?.RaiseCanExecuteChanged();
            (ViewLogsCommand as AsyncCommandHandler)?.RaiseCanExecuteChanged();
            (RestartWwowStackCommand as AsyncCommandHandler)?.RaiseCanExecuteChanged();
            (StopWwowStackCommand as AsyncCommandHandler)?.RaiseCanExecuteChanged();
            (StartWwowStackCommand as AsyncCommandHandler)?.RaiseCanExecuteChanged();
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
