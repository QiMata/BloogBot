using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using WoWStateManagerUI.Services;

namespace WoWStateManagerUI.ViewModels
{
    public sealed class MainViewModel : INotifyPropertyChanged, IDisposable
    {
        public HealthCheckService HealthCheck { get; } = new();
        public DashboardViewModel Dashboard { get; }
        public MangosConsoleViewModel MangosConsole { get; } = new();
        public ConfigEditorViewModel ConfigEditor { get; } = new();
        public AccountManagementViewModel AccountManagement { get; } = new();
        public ServicesViewModel Services { get; } = new();
        public InstancesViewModel Instances { get; } = new();

        public MainViewModel()
        {
            Dashboard = new DashboardViewModel(HealthCheck);
            HealthCheck.Start();
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

        public void Dispose()
        {
            Instances.Dispose();
            HealthCheck.Dispose();
            Dashboard.Dispose();
        }
    }
}
