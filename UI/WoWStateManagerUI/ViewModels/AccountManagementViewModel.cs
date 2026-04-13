using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows.Input;
using BotCommLayer;
using Microsoft.Extensions.Logging;
using WoWStateManagerUI.Handlers;
using WoWStateManagerUI.Services;

namespace WoWStateManagerUI.ViewModels
{
    public sealed class AccountManagementViewModel : INotifyPropertyChanged
    {
        private AccountService? _accountService;
        private MangosSOAPClient? _soapClient;
        private string _connectionString = "server=localhost;user=root;database=realmd;port=3306;password=root";
        private string _soapUrl = "http://localhost:7878";
        private string _newAccountName = string.Empty;
        private string _newAccountPassword = "PASSWORD";
        private int _newAccountGmLevel = 0;
        private AccountInfo? _selectedAccount;
        private string _statusMessage = string.Empty;
        private bool _isConnected;

        public ObservableCollection<AccountInfo> Accounts { get; } = [];

        public string ConnectionString
        {
            get => _connectionString;
            set { _connectionString = value; OnPropertyChanged(); }
        }

        public string SoapUrl
        {
            get => _soapUrl;
            set { _soapUrl = value; OnPropertyChanged(); }
        }

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

        public ICommand ConnectCommand { get; }
        public ICommand RefreshCommand { get; }
        public ICommand CreateAccountCommand { get; }
        public ICommand DeleteAccountCommand { get; }
        public ICommand SetGmLevelCommand { get; }
        public ICommand BanAccountCommand { get; }

        public AccountManagementViewModel()
        {
            ConnectCommand = new AsyncCommandHandler(ConnectAsync);
            RefreshCommand = new AsyncCommandHandler(RefreshAccountsAsync, () => IsConnected);
            CreateAccountCommand = new AsyncCommandHandler(CreateAccountAsync, () => IsConnected);
            DeleteAccountCommand = new AsyncCommandHandler(DeleteAccountAsync, () => IsConnected && _selectedAccount != null);
            SetGmLevelCommand = new AsyncCommandHandler(SetGmLevelAsync, () => IsConnected && _selectedAccount != null);
            BanAccountCommand = new AsyncCommandHandler(BanAccountAsync, () => IsConnected && _selectedAccount != null);
        }

        private async Task ConnectAsync()
        {
            try
            {
                _accountService = new AccountService(ConnectionString);
                var dbOk = await _accountService.TestConnectionAsync();

                _soapClient = new MangosSOAPClient(SoapUrl, new MinimalLogger());
                var soapOk = await _soapClient.CheckSOAPPortStatus();

                IsConnected = dbOk && soapOk;
                StatusMessage = IsConnected
                    ? "Connected to realmd DB and SOAP"
                    : $"Connection partial — DB: {(dbOk ? "OK" : "FAIL")}, SOAP: {(soapOk ? "OK" : "FAIL")}";

                if (dbOk)
                    await RefreshAccountsAsync();

                RefreshCanExecute();
            }
            catch (Exception ex)
            {
                StatusMessage = $"Connect failed: {ex.Message}";
            }
        }

        private async Task RefreshAccountsAsync()
        {
            if (_accountService == null) return;

            try
            {
                var accounts = await _accountService.GetAllAccountsAsync();
                Accounts.Clear();
                foreach (var a in accounts)
                    Accounts.Add(a);

                OnPropertyChanged(nameof(TotalAccounts));
                OnPropertyChanged(nameof(TotalCharacters));
                OnPropertyChanged(nameof(OnlineAccounts));
                StatusMessage = $"Loaded {Accounts.Count} accounts ({TotalCharacters} total characters, {OnlineAccounts} online)";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Refresh failed: {ex.Message}";
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

        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
            // Refresh CanExecute when selection changes
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
