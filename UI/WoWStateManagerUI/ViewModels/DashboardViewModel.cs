using Communication;
using WoWStateManagerUI.Handlers;
using WoWStateManagerUI.Services;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows.Input;

namespace WoWStateManagerUI.ViewModels
{
    public sealed class DashboardViewModel : INotifyPropertyChanged, IDisposable
    {
        private readonly HealthCheckService _healthCheck;

        private readonly Dictionary<CharacterDefinition, CharacterDefinition> _characterStates = [];
        private CharacterDefinition[]? _characterCache;
        private int _currentPageIndex = -1;
        private int _selectedCharacterIndex = -1;
        private string _stateManagerExePath = string.Empty;
        private string _statusMessage = string.Empty;

        public HealthCheckService HealthCheck => _healthCheck;
        public ProcessLauncherService ProcessLauncher { get; }

        public ICommand LocalStateManagerLoadCommand { get; }
        public ICommand StateManagerConnectCommand { get; }
        public ICommand StateManagerDisconnectCommand { get; }
        public ICommand LaunchStateManagerCommand { get; }
        public ICommand StopStateManagerCommand { get; }
        public ICommand BrowseExeCommand { get; }

        public string StateManagerUrl { get; set; } = "http://localhost:8088";
        public string MangosUrl { get; set; } = "http://localhost:7878";
        public string AdminUsername { get; set; } = "ADMINISTRATOR";
        public string AdminPassword { get; set; } = "PASSWORD";

        public string RealmState
        {
            get => _healthCheck.RealmdStatus == ServiceStatus.Up ? "UP" : _healthCheck.RealmdStatus == ServiceStatus.Down ? "DOWN" : "UNKNOWN";
        }

        public string WorldState
        {
            get => _healthCheck.MangosdStatus == ServiceStatus.Up ? "UP" : _healthCheck.MangosdStatus == ServiceStatus.Down ? "DOWN" : "UNKNOWN";
        }

        public string TotalPopulation
        {
            get => _healthCheck.RealmdStatus == ServiceStatus.Up && _healthCheck.MangosdStatus == ServiceStatus.Up ? "3000" : "0";
        }

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

        private bool _isConnected;
        public bool IsConnected
        {
            get => _isConnected;
            set { if (_isConnected != value) { _isConnected = value; OnPropertyChanged(); } }
        }

        public int SelectCharacterIndex => _selectedCharacterIndex;
        public int CurrentPageIndex => _currentPageIndex;

        public float OpennessValue
        {
            get => GetSelectedCharacter()?.Openness ?? 0f;
            set { if (TryGetSelectedCharacter(out var c)) { c.Openness = value; OnPropertyChanged(); } }
        }

        public float ConscientiousnessValue
        {
            get => GetSelectedCharacter()?.Conscientiousness ?? 0f;
            set { if (TryGetSelectedCharacter(out var c)) { c.Conscientiousness = value; OnPropertyChanged(); } }
        }

        public float ExtraversionValue
        {
            get => GetSelectedCharacter()?.Extraversion ?? 0f;
            set { if (TryGetSelectedCharacter(out var c)) { c.Extraversion = value; OnPropertyChanged(); } }
        }

        public float AgreeablenessValue
        {
            get => GetSelectedCharacter()?.Agreeableness ?? 0f;
            set { if (TryGetSelectedCharacter(out var c)) { c.Agreeableness = value; OnPropertyChanged(); } }
        }

        public float NeuroticismValue
        {
            get => GetSelectedCharacter()?.Neuroticism ?? 0f;
            set { if (TryGetSelectedCharacter(out var c)) { c.Neuroticism = value; OnPropertyChanged(); } }
        }

        public string[] AvailableTemplates { get; } =
        [
            "",
            "FuryWarriorPreRaid",
            "HolyPriestMCReady",
            "FrostMageAoEFarmer",
            "ProtectionWarriorTank",
        ];

        public string SelectedBuildTemplate
        {
            get => GetSelectedCharacter()?.BuildTemplate ?? "";
            set { if (TryGetSelectedCharacter(out var c)) { c.BuildTemplate = value; OnPropertyChanged(); } }
        }

        public DashboardViewModel(HealthCheckService healthCheck)
        {
            _healthCheck = healthCheck;
            _healthCheck.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName is nameof(HealthCheckService.RealmdStatus) or nameof(HealthCheckService.MangosdStatus))
                {
                    OnPropertyChanged(nameof(RealmState));
                    OnPropertyChanged(nameof(WorldState));
                    OnPropertyChanged(nameof(TotalPopulation));
                }
            };
            ProcessLauncher = new ProcessLauncherService();

            LocalStateManagerLoadCommand = new CommandHandler(() => { }, true);
            StateManagerConnectCommand = new CommandHandler(() => { }, true);
            StateManagerDisconnectCommand = new CommandHandler(() => { }, true);
            LaunchStateManagerCommand = new CommandHandler(LaunchStateManager, true);
            StopStateManagerCommand = new CommandHandler(StopStateManager, true);
            BrowseExeCommand = new CommandHandler(BrowseExe, true);
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

        private void StopStateManager()
        {
            StatusMessage = ProcessLauncher.Stop();
        }

        private CharacterDefinition? GetSelectedCharacter() => TryGetSelectedCharacter(out var c) ? c : null;

        private bool TryGetSelectedCharacter(out CharacterDefinition character)
        {
            character = default!;
            if (_characterStates.Count == 0) return false;
            EnsureCharacterCache();
            var index = 20 * _currentPageIndex + _selectedCharacterIndex;
            if (index < 0 || _characterCache == null || index >= _characterCache.Length) return false;
            character = _characterCache[index];
            return true;
        }

        private void EnsureCharacterCache()
        {
            if (_characterCache == null || _characterCache.Length != _characterStates.Count)
                _characterCache = _characterStates.Keys.ToArray();
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

        public void Dispose()
        {
            ProcessLauncher.Dispose();
        }
    }
}
