using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace WoWStateManagerUI.Services
{
    public enum ServiceStatus
    {
        Unknown,
        Up,
        Down
    }

    /// <summary>
    /// Polls TCP ports for realmd, mangosd, SOAP, PathfindingService, and SceneDataService.
    /// Fires PropertyChanged so WPF bindings update automatically.
    /// </summary>
    public sealed class HealthCheckService : INotifyPropertyChanged, IDisposable
    {
        private Timer? _pollTimer;
        private readonly TimeSpan _pollInterval = TimeSpan.FromSeconds(5);

        private ServiceStatus _realmdStatus = ServiceStatus.Unknown;
        private ServiceStatus _mangosdStatus = ServiceStatus.Unknown;
        private ServiceStatus _soapStatus = ServiceStatus.Unknown;
        private ServiceStatus _pathfindingStatus = ServiceStatus.Unknown;
        private ServiceStatus _sceneDataStatus = ServiceStatus.Unknown;

        public ServiceStatus RealmdStatus { get => _realmdStatus; private set { if (_realmdStatus != value) { _realmdStatus = value; OnPropertyChanged(); } } }
        public ServiceStatus MangosdStatus { get => _mangosdStatus; private set { if (_mangosdStatus != value) { _mangosdStatus = value; OnPropertyChanged(); } } }
        public ServiceStatus SoapStatus { get => _soapStatus; private set { if (_soapStatus != value) { _soapStatus = value; OnPropertyChanged(); } } }
        public ServiceStatus PathfindingStatus { get => _pathfindingStatus; private set { if (_pathfindingStatus != value) { _pathfindingStatus = value; OnPropertyChanged(); } } }
        public ServiceStatus SceneDataStatus { get => _sceneDataStatus; private set { if (_sceneDataStatus != value) { _sceneDataStatus = value; OnPropertyChanged(); } } }

        public void Start()
        {
            _pollTimer ??= new Timer(async _ => await PollAllAsync(), null, TimeSpan.Zero, _pollInterval);
        }

        public void Stop()
        {
            _pollTimer?.Dispose();
            _pollTimer = null;
        }

        private async Task PollAllAsync()
        {
            var realmd = CheckPortAsync(3724);
            var mangosd = CheckPortAsync(8085);
            var soap = CheckPortAsync(7878);
            var pathfinding = CheckPortAsync(5001);
            var sceneData = CheckPortAsync(5003);

            await Task.WhenAll(realmd, mangosd, soap, pathfinding, sceneData);

            RealmdStatus = realmd.Result ? ServiceStatus.Up : ServiceStatus.Down;
            MangosdStatus = mangosd.Result ? ServiceStatus.Up : ServiceStatus.Down;
            SoapStatus = soap.Result ? ServiceStatus.Up : ServiceStatus.Down;
            PathfindingStatus = pathfinding.Result ? ServiceStatus.Up : ServiceStatus.Down;
            SceneDataStatus = sceneData.Result ? ServiceStatus.Up : ServiceStatus.Down;
        }

        private static async Task<bool> CheckPortAsync(int port, int timeoutMs = 1000)
        {
            try
            {
                using var client = new TcpClient();
                var connectTask = client.ConnectAsync("127.0.0.1", port);
                var completed = await Task.WhenAny(connectTask, Task.Delay(timeoutMs));
                return ReferenceEquals(completed, connectTask) && client.Connected;
            }
            catch
            {
                return false;
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

        public void Dispose() => Stop();
    }
}
