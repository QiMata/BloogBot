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
        private AccountDetailService? _detailService;
        private MangosSOAPClient? _soapClient;
        private readonly DispatcherTimer _refreshTimer;
        private string _newAccountName = string.Empty;
        private string _newAccountPassword = "PASSWORD";
        private AccountInfo? _selectedAccount;
        private CharacterInfo? _selectedCharacter;
        private string _statusMessage = "Connecting to Realmd DB and SOAP...";
        private bool _isConnected;
        private bool _refreshInFlight;

        public ObservableCollection<AccountInfo> Accounts { get; } = [];

        /// <summary>Realms the selected account has accessed (one row per realmlist entry).</summary>
        public ObservableCollection<RealmInfo> SelectedAccountRealms { get; } = [];

        /// <summary>Characters owned by the selected account (from the characters DB).</summary>
        public ObservableCollection<CharacterInfo> SelectedAccountCharacters { get; } = [];

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

        public AccountInfo? SelectedAccount
        {
            get => _selectedAccount;
            set
            {
                _selectedAccount = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(HasSelectedAccount));
                _ = LoadSelectedAccountDetailAsync();
            }
        }

        public bool HasSelectedAccount => _selectedAccount != null;

        public CharacterInfo? SelectedCharacter
        {
            get => _selectedCharacter;
            set { _selectedCharacter = value; OnPropertyChanged(); RefreshCanExecute(); }
        }

        private string _newCharacterName = string.Empty;
        public string NewCharacterName
        {
            get => _newCharacterName;
            set { _newCharacterName = value; OnPropertyChanged(); }
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
        public ICommand EraseCharacterCommand { get; }
        public ICommand CreateCharacterCommand { get; }

        public AccountManagementViewModel()
        {
            RefreshCommand = new AsyncCommandHandler(RefreshAccountsAsync, () => IsConnected);
            CreateAccountCommand = new AsyncCommandHandler(CreateAccountAsync, () => IsConnected);
            DeleteAccountCommand = new AsyncCommandHandler(DeleteAccountAsync, () => IsConnected && _selectedAccount != null);
            EraseCharacterCommand = new AsyncCommandHandler(EraseSelectedCharacterAsync,
                () => IsConnected && _selectedCharacter != null);
            CreateCharacterCommand = new AsyncCommandHandler(CreateCharacterAsync,
                () => IsConnected && _selectedAccount != null && !string.IsNullOrWhiteSpace(_newCharacterName));

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
                _detailService = new AccountDetailService(
                    UIConstants.RealmdConnectionString,
                    UIConstants.CharactersConnectionString);
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

                NewAccountName = string.Empty;
                await RefreshAccountsAsync();
            }
            catch (Exception ex)
            {
                StatusMessage = $"Create failed: {ex.Message}";
            }
        }

        private async Task LoadSelectedAccountDetailAsync()
        {
            SelectedAccountRealms.Clear();
            SelectedAccountCharacters.Clear();
            SelectedCharacter = null;

            if (_detailService == null || _selectedAccount == null) return;

            try
            {
                var realms = await _detailService.GetRealmsForAccountAsync(_selectedAccount.Id);
                foreach (var r in realms)
                    SelectedAccountRealms.Add(r);

                var chars = await _detailService.GetCharactersForAccountAsync(_selectedAccount.Id);
                foreach (var c in chars)
                    SelectedAccountCharacters.Add(c);
            }
            catch (Exception ex)
            {
                StatusMessage = $"Detail load failed: {ex.Message}";
            }
        }

        private async Task EraseSelectedCharacterAsync()
        {
            if (_soapClient == null || _detailService == null || _selectedCharacter == null) return;
            var name = _selectedCharacter.Name;

            try
            {
                var result = await _detailService.EraseCharacterAsync(_soapClient, name);
                StatusMessage = $"Erase '{name}': {result}";
                await LoadSelectedAccountDetailAsync();
                await RefreshAccountsAsync();
            }
            catch (Exception ex)
            {
                StatusMessage = $"Erase '{name}' failed: {ex.Message}";
            }
        }

        /// <summary>
        /// Stub for character creation. The user's design has a transient BG
        /// client issue <c>CMSG_CHAR_CREATE</c> — that's Phase 2 work that
        /// requires the BG client packet path to be reachable from the UI
        /// process. Today we report what would happen.
        /// </summary>
        private Task CreateCharacterAsync()
        {
            StatusMessage = $"Create '{NewCharacterName}' on account '{_selectedAccount?.Username}': " +
                            "Phase 2 — needs transient BG client to send CMSG_CHAR_CREATE. Not wired yet.";
            return Task.CompletedTask;
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

        private void RefreshCanExecute()
        {
            (RefreshCommand as AsyncCommandHandler)?.RaiseCanExecuteChanged();
            (CreateAccountCommand as AsyncCommandHandler)?.RaiseCanExecuteChanged();
            (DeleteAccountCommand as AsyncCommandHandler)?.RaiseCanExecuteChanged();
            (EraseCharacterCommand as AsyncCommandHandler)?.RaiseCanExecuteChanged();
            (CreateCharacterCommand as AsyncCommandHandler)?.RaiseCanExecuteChanged();
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
