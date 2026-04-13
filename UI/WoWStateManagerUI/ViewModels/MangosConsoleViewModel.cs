using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows.Input;
using BotCommLayer;
using Microsoft.Extensions.Logging;
using WoWStateManagerUI.Handlers;

namespace WoWStateManagerUI.ViewModels
{
    public sealed class MangosConsoleViewModel : INotifyPropertyChanged
    {
        private MangosSOAPClient? _soapClient;
        private string _soapUrl = "http://localhost:7878";
        private string _username = "ADMINISTRATOR";
        private string _password = "PASSWORD";
        private string _commandText = string.Empty;
        private string _accountName = string.Empty;
        private int _gmLevel = 6;
        private bool _isConnected;

        public ObservableCollection<string> OutputLog { get; } = [];

        public string SoapUrl { get => _soapUrl; set { _soapUrl = value; OnPropertyChanged(); } }
        public string Username { get => _username; set { _username = value; OnPropertyChanged(); } }
        public string Password { get => _password; set { _password = value; OnPropertyChanged(); } }
        public string CommandText { get => _commandText; set { _commandText = value; OnPropertyChanged(); } }
        public string AccountName { get => _accountName; set { _accountName = value; OnPropertyChanged(); } }
        public int GmLevel { get => _gmLevel; set { _gmLevel = value; OnPropertyChanged(); } }

        public bool IsConnected
        {
            get => _isConnected;
            private set { if (_isConnected != value) { _isConnected = value; OnPropertyChanged(); } }
        }

        public ICommand ConnectCommand { get; }
        public ICommand ExecuteCommand { get; }
        public ICommand CreateAccountCommand { get; }
        public ICommand SetGMLevelCommand { get; }
        public ICommand ServerInfoCommand { get; }
        public ICommand ClearLogCommand { get; }

        public MangosConsoleViewModel()
        {
            ConnectCommand = new AsyncCommandHandler(ConnectAsync);
            ExecuteCommand = new AsyncCommandHandler(ExecuteGMCommandAsync, () => IsConnected);
            CreateAccountCommand = new AsyncCommandHandler(CreateAccountAsync, () => IsConnected);
            SetGMLevelCommand = new AsyncCommandHandler(SetGMLevelAsync, () => IsConnected);
            ServerInfoCommand = new AsyncCommandHandler(GetServerInfoAsync, () => IsConnected);
            ClearLogCommand = new CommandHandler(() => OutputLog.Clear(), true);
        }

        private async Task ConnectAsync()
        {
            _soapClient = new MangosSOAPClient(SoapUrl, new BasicLoggerAdapter(), Username, Password);
            var up = await _soapClient.CheckSOAPPortStatus();
            IsConnected = up;
            AppendLog(up ? $"Connected to SOAP at {SoapUrl}" : $"Failed to connect to SOAP at {SoapUrl}");
            RefreshCanExecute();
        }

        private async Task ExecuteGMCommandAsync()
        {
            if (_soapClient == null || string.IsNullOrWhiteSpace(CommandText)) return;
            AppendLog($"> {CommandText}");
            try
            {
                var result = await _soapClient.ExecuteGMCommandAsync(CommandText);
                AppendLog(string.IsNullOrEmpty(result) ? "(no response)" : result);
            }
            catch (Exception ex)
            {
                AppendLog($"ERROR: {ex.Message}");
            }
            CommandText = string.Empty;
        }

        private async Task CreateAccountAsync()
        {
            if (_soapClient == null || string.IsNullOrWhiteSpace(AccountName)) return;
            AppendLog($"> .account create {AccountName}");
            try
            {
                var result = await _soapClient.CreateAccountAsync(AccountName);
                AppendLog(string.IsNullOrEmpty(result) ? "(no response)" : result);
            }
            catch (Exception ex)
            {
                AppendLog($"ERROR: {ex.Message}");
            }
        }

        private async Task SetGMLevelAsync()
        {
            if (_soapClient == null || string.IsNullOrWhiteSpace(AccountName)) return;
            AppendLog($"> .account set gmlevel {AccountName} {GmLevel}");
            try
            {
                var result = await _soapClient.SetGMLevelAsync(AccountName, GmLevel);
                AppendLog(string.IsNullOrEmpty(result) ? "(no response)" : result);
            }
            catch (Exception ex)
            {
                AppendLog($"ERROR: {ex.Message}");
            }
        }

        private async Task GetServerInfoAsync()
        {
            if (_soapClient == null) return;
            AppendLog("> server info");
            try
            {
                var result = await _soapClient.ExecuteGMCommandAsync(MangosSOAPClient.ServerInfoCommand);
                AppendLog(string.IsNullOrEmpty(result) ? "(no response)" : result);
            }
            catch (Exception ex)
            {
                AppendLog($"ERROR: {ex.Message}");
            }
        }

        private void AppendLog(string message)
        {
            var timestamp = DateTime.Now.ToString("HH:mm:ss");
            OutputLog.Add($"[{timestamp}] {message}");
        }

        private void RefreshCanExecute()
        {
            (ExecuteCommand as AsyncCommandHandler)?.RaiseCanExecuteChanged();
            (CreateAccountCommand as AsyncCommandHandler)?.RaiseCanExecuteChanged();
            (SetGMLevelCommand as AsyncCommandHandler)?.RaiseCanExecuteChanged();
            (ServerInfoCommand as AsyncCommandHandler)?.RaiseCanExecuteChanged();
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

        /// <summary>
        /// Minimal ILogger adapter so MangosSOAPClient can log to the output panel.
        /// </summary>
        private sealed class BasicLoggerAdapter : ILogger<MangosSOAPClient>
        {
            public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
            public bool IsEnabled(LogLevel logLevel) => logLevel >= LogLevel.Warning;
            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
            {
                // Warnings and errors will show in debug output; routine logs suppressed
                if (logLevel >= LogLevel.Warning)
                    System.Diagnostics.Debug.WriteLine($"[SOAP] {formatter(state, exception)}");
            }
        }
    }
}
