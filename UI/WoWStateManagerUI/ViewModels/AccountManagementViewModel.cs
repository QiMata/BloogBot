using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows.Input;
using System.Windows.Threading;
using BotCommLayer;
using Microsoft.Extensions.Logging;
using WoWStateManagerUI.Handlers;
using WoWStateManagerUI.Services;

namespace WoWStateManagerUI.ViewModels
{
    public sealed class AccountManagementViewModel : INotifyPropertyChanged, IDisposable
    {
        private AccountService? _accountService;
        private MangosSOAPClient? _soapClient;
        private readonly DispatcherTimer _refreshTimer;
        private string _newAccountName = string.Empty;
        private string _newAccountPassword = "PASSWORD";
        private int _newAccountGmLevel = 0;
        private AccountInfo? _selectedAccount;
        private string _statusMessage = "Connecting to Realmd DB and SOAP...";
        private bool _isConnected;
        private bool _refreshInFlight;

        public ObservableCollection<AccountInfo> Accounts { get; } = [];

        public string NewAccountName
        {
            get => _newAccountName;
            set { _newAccountName = value; OnPropertyChanged(); }
        }

        public string NewAccountPassword
        {
            get => _newAccountPassword;
            set { _newAccountPassword = value; OnPropertyChanged(); }
        }

        public int NewAccountGmLevel
        {
            get => _newAccountGmLevel;
            set { _newAccountGmLevel = value; OnPropertyChanged(); }
        }

        public AccountInfo? SelectedAccount
        {
            get => _selectedAccount;
            set { _selectedAccount = value; OnPropertyChanged(); }
        }

        public string StatusMessage
        {
            get => _statusMessage;
            private set { _statusMessage = value; OnPropertyChanged(); }
        }

        public bool IsConnected
        {
            get => _isConnected;
            private set { _isConnected = value; OnPropertyChanged(); }
        }

        public int TotalAccounts => Accounts.Count;
        public int TotalCharacters => Accounts.Sum(a => a.NumCharacters);
        public int OnlineAccounts => Accounts.Count(a => a.Online);

        public ICommand RefreshCommand { get; }
        public ICommand CreateAccountCommand { get; }
        public ICommand DeleteAccountCommand { get; }
        public ICommand SetGmLevelCommand { get; }
        public ICommand BanAccountCommand { get; }

        public AccountManagementViewModel()
        {
            RefreshCommand = new AsyncCommandHandler(RefreshAccountsAsync, () => IsConnected);
            CreateAccountCommand = new AsyncCommandHandler(CreateAccountAsync, () => IsConnected);
            DeleteAccountCommand = new AsyncCommandHandler(DeleteAccountAsync, () => IsConnected && _selectedAccount != null);
            SetGmLevelCommand = new AsyncCommandHandler(SetGmLevelAsync, () => IsConnected && _selectedAccount != null);
            BanAccountCommand = new AsyncCommandHandler(BanAccountAsync, () => IsConnected && _selectedAccount != null);

            _refreshTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(UIConstants.AccountsRefreshSeconds)
            };
            _refreshTimer.Tick += async (_, _) => await TickAsync();

            _ = AutoConnectAsync();
        }

        private async Task AutoConnectAsync()
        {
            try
            {
                _accountService = new AccountService(UIConstants.RealmdConnectionString);
                var dbOk = await _accountService.TestConnectionAsync();

                _soapClient = new MangosSOAPClient(
                    UIConstants.MangosSoapUrl,
                    new MinimalLogger(),
                    UIConstants.MangosUsername,
                    UIConstants.MangosPassword);
                var soapOk = await _soapClient.CheckSOAPPortStatus();

                IsConnected = dbOk && soapOk;
                StatusMessage = IsConnected
                    ? "Connected to Realmd DB and SOAP — auto-refreshing"
                    : $"Connection partial — DB: {(dbOk ? "OK" : "FAIL")}, SOAP: {(soapOk ? "OK" : "FAIL")}";

                if (dbOk)
                    await RefreshAccountsAsync();

                if (IsConnected)
                    _refreshTimer.Start();

                RefreshCanExecute();
            }
            catch (Exception ex)
            {
                StatusMessage = $"Connect failed: {ex.Message}";
            }
        }

        private async Task TickAsync()
        {
            if (_refreshInFlight || !IsConnected) return;
            await RefreshAccountsAsync();
        }

        private async Task RefreshAccountsAsync()
        {
            if (_accountService == null) return;
            if (_refreshInFlight) return;
            _refreshInFlight = true;

            try
            {
                var selectedId = _selectedAccount?.Id;
                var accounts = await _accountService.GetAllAccountsAsync();
                Accounts.Clear();
                foreach (var a in accounts)
                    Accounts.Add(a);

                // Preserve selection by ID across refreshes
                if (selectedId.HasValue)
                    SelectedAccount = Accounts.FirstOrDefault(a => a.Id == selectedId.Value);

                OnPropertyChanged(nameof(TotalAccounts));
                OnPropertyChanged(nameof(TotalCharacters));
                OnPropertyChanged(nameof(OnlineAccounts));
                StatusMessage = $"{Accounts.Count} accounts ({TotalCharacters} characters, {OnlineAccounts} online)";
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

        private async Task CreateAccountAsync()
        {
            if (_soapClient == null || string.IsNullOrWhiteSpace(NewAccountName)) return;

            try
            {
                var result = await _soapClient.CreateAccountAsync(NewAccountName);
                StatusMessage = $"Create '{NewAccountName}': {(string.IsNullOrEmpty(result) ? "(no response)" : result)}";

                if (NewAccountGmLevel > 0)
                {
                    var gmResult = await _soapClient.SetGMLevelAsync(NewAccountName, NewAccountGmLevel);
                    StatusMessage += $" | GM level: {(string.IsNullOrEmpty(gmResult) ? "(no response)" : gmResult)}";
                }

                NewAccountName = string.Empty;
                await RefreshAccountsAsync();
            }
            catch (Exception ex)
            {
                StatusMessage = $"Create failed: {ex.Message}";
            }
        }

        private async Task DeleteAccountAsync()
        {
            if (_accountService == null || _selectedAccount == null) return;

            var name = _selectedAccount.Username;
            var id = _selectedAccount.Id;

            try
            {
                await _accountService.DeleteAccountAsync(id);
                StatusMessage = $"Deleted account '{name}' (ID {id})";
                await RefreshAccountsAsync();
            }
            catch (Exception ex)
            {
                StatusMessage = $"Delete failed: {ex.Message}";
            }
        }

        private async Task SetGmLevelAsync()
        {
            if (_soapClient == null || _selectedAccount == null) return;

            try
            {
                var result = await _soapClient.SetGMLevelAsync(_selectedAccount.Username, NewAccountGmLevel);
                StatusMessage = $"Set GM level {NewAccountGmLevel} for '{_selectedAccount.Username}': {(string.IsNullOrEmpty(result) ? "(no response)" : result)}";
                await RefreshAccountsAsync();
            }
            catch (Exception ex)
            {
                StatusMessage = $"Set GM level failed: {ex.Message}";
            }
        }

        private async Task BanAccountAsync()
        {
            if (_soapClient == null || _selectedAccount == null) return;

            try
            {
                var cmd = _selectedAccount.Banned
                    ? $".unban account {_selectedAccount.Username}"
                    : $".ban account {_selectedAccount.Username} 0 UI-ban";
                var result = await _soapClient.ExecuteGMCommandAsync(cmd);
                StatusMessage = $"{(_selectedAccount.Banned ? "Unban" : "Ban")} '{_selectedAccount.Username}': {(string.IsNullOrEmpty(result) ? "(no response)" : result)}";
                await RefreshAccountsAsync();
            }
            catch (Exception ex)
            {
                StatusMessage = $"Ban/unban failed: {ex.Message}";
            }
        }

        private void RefreshCanExecute()
        {
            (RefreshCommand as AsyncCommandHandler)?.RaiseCanExecuteChanged();
            (CreateAccountCommand as AsyncCommandHandler)?.RaiseCanExecuteChanged();
            (DeleteAccountCommand as AsyncCommandHandler)?.RaiseCanExecuteChanged();
            (SetGmLevelCommand as AsyncCommandHandler)?.RaiseCanExecuteChanged();
            (BanAccountCommand as AsyncCommandHandler)?.RaiseCanExecuteChanged();
        }

        public void Dispose()
        {
            _refreshTimer.Stop();
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
            if (propertyName == nameof(SelectedAccount))
                RefreshCanExecute();
        }

        private sealed class MinimalLogger : ILogger<MangosSOAPClient>
        {
            public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
            public bool IsEnabled(LogLevel logLevel) => logLevel >= LogLevel.Warning;
            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
            {
                if (logLevel >= LogLevel.Warning)
                    System.Diagnostics.Debug.WriteLine($"[AccountMgmt] {formatter(state, exception)}");
            }
        }
    }
}
