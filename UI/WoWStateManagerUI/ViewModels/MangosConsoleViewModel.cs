using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows.Input;
using BotCommLayer;
using Microsoft.Extensions.Logging;
using WoWStateManagerUI.Handlers;
using WoWStateManagerUI.Services;

namespace WoWStateManagerUI.ViewModels
{
    /// <summary>
    /// Raw SOAP command console for the running MaNGOS server. Account creation
    /// and GM-level management live under the Accounts surface, so this view is
    /// just a free-form GM-command tool now.
    /// </summary>
    public sealed class MangosConsoleViewModel : INotifyPropertyChanged
    {
        private MangosSOAPClient? _soapClient;
        private string _commandText = string.Empty;
        private bool _isConnected;

        public ObservableCollection<string> OutputLog { get; } = [];

        public string CommandText { get => _commandText; set { _commandText = value; OnPropertyChanged(); } }

        public bool IsConnected
        {
            get => _isConnected;
            private set { if (_isConnected != value) { _isConnected = value; OnPropertyChanged(); } }
        }

        public ICommand ExecuteCommand { get; }
        public ICommand ServerInfoCommand { get; }
        public ICommand ClearLogCommand { get; }

        public MangosConsoleViewModel()
        {
            ExecuteCommand = new AsyncCommandHandler(ExecuteGMCommandAsync, () => IsConnected);
            ServerInfoCommand = new AsyncCommandHandler(GetServerInfoAsync, () => IsConnected);
            ClearLogCommand = new CommandHandler(() => OutputLog.Clear(), true);

            _ = AutoConnectAsync();
        }

        private async Task AutoConnectAsync()
        {
            _soapClient = new MangosSOAPClient(
                UIConstants.MangosSoapUrl,
                new BasicLoggerAdapter(),
                UIConstants.MangosUsername,
                UIConstants.MangosPassword);

            var up = await _soapClient.CheckSOAPPortStatus();
            IsConnected = up;
            AppendLog(up
                ? $"SOAP connected at {UIConstants.MangosSoapUrl}"
                : $"SOAP not reachable at {UIConstants.MangosSoapUrl} — will retry on next command");
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
            (ServerInfoCommand as AsyncCommandHandler)?.RaiseCanExecuteChanged();
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

        private sealed class BasicLoggerAdapter : ILogger<MangosSOAPClient>
        {
            public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
            public bool IsEnabled(LogLevel logLevel) => logLevel >= LogLevel.Warning;
            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
            {
                if (logLevel >= LogLevel.Warning)
                    System.Diagnostics.Debug.WriteLine($"[SOAP] {formatter(state, exception)}");
            }
        }
    }
}
