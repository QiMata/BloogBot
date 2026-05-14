using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Microsoft.Extensions.Logging;
using WoWStateManagerUI.Services;

namespace WoWStateManagerUI.ViewModels
{
    public sealed class MainViewModel : INotifyPropertyChanged, IDisposable
    {
        public HealthCheckService HealthCheck { get; } = new();
        public UIListenerService Listener { get; }
        public DashboardViewModel Dashboard { get; }
        public ServiceManagementViewModel ServiceManagement { get; } = new();
        public ConfigEditorViewModel ConfigEditor { get; } = new();

        public MainViewModel()
        {
            // Start the snapshot listener up-front so any StateManager that comes online
            // pushes into it without user interaction. Bound to the known dev system address.
            Listener = new UIListenerService(
                UIConstants.ListenerAddress,
                UIConstants.ListenerPort,
                new ListenerLogger());

            Dashboard = new DashboardViewModel(HealthCheck, Listener);

            HealthCheck.Start();
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

        public void Dispose()
        {
            ServiceManagement.Dispose();
            Listener.Dispose();
            HealthCheck.Dispose();
            Dashboard.Dispose();
        }

        /// <summary>Minimal logger for the listener; warnings and errors only.</summary>
        private sealed class ListenerLogger : ILogger
        {
            public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
            public bool IsEnabled(LogLevel logLevel) => logLevel >= LogLevel.Warning;
            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
            {
                if (logLevel >= LogLevel.Warning)
                    System.Diagnostics.Debug.WriteLine($"[UIListener] {formatter(state, exception)}");
            }
        }
    }
}
