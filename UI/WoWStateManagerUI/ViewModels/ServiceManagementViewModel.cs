using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace WoWStateManagerUI.ViewModels
{
    /// <summary>
    /// Aggregates the three operational surfaces under a single "Service Management"
    /// tab: Docker containers (Services), realmd accounts (Accounts), and free-form
    /// SOAP/GM commands (MaNGOS Console). All three auto-connect on startup.
    /// </summary>
    public sealed class ServiceManagementViewModel : INotifyPropertyChanged, IDisposable
    {
        public ServicesViewModel Services { get; } = new();
        public AccountManagementViewModel Accounts { get; } = new();
        public MangosConsoleViewModel MangosConsole { get; } = new();

        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

        public void Dispose()
        {
            Services.Dispose();
            Accounts.Dispose();
        }
    }
}
