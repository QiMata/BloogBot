using BotCommLayer;
using Communication;
using Microsoft.Extensions.Logging;
using System;

namespace WoWStateManager.Clients
{
    /// <summary>
    /// Pushes snapshot data to the UI listener service.
    /// Connects to the UI's ProtobufPipelineSocketServer on a configurable port (default 9090).
    /// Failures are non-fatal — the UI may not be running.
    /// </summary>
    public class UIUpdateClient : IDisposable
    {
        private readonly ILogger<UIUpdateClient> _logger;
        private readonly string _ipAddress;
        private readonly int _port;
        private ProtobufSocketClient<StateChangeResponse, StateChangeResponse>? _client;
        private bool _connected;
        private DateTime _lastFailure = DateTime.MinValue;
        private static readonly TimeSpan ReconnectCooldown = TimeSpan.FromSeconds(15);

        public bool IsConnected => _connected;

        public UIUpdateClient(string ipAddress, int port, ILogger<UIUpdateClient> logger)
        {
            _ipAddress = ipAddress;
            _port = port;
            _logger = logger;
        }

        /// <summary>
        /// Push snapshots to the UI. Non-blocking on failure — logs and moves on.
        /// </summary>
        public void PushSnapshots(StateChangeResponse snapshots)
        {
            try
            {
                EnsureConnected();
                if (_client == null) return;

                _client.SendMessage(snapshots);
            }
            catch (Exception ex)
            {
                _connected = false;
                _lastFailure = DateTime.UtcNow;
                _logger.LogDebug("UI push failed (UI may not be running): {Message}", ex.Message);
                DisposeClient();
            }
        }

        private void EnsureConnected()
        {
            if (_connected && _client != null) return;

            // Cooldown to avoid spamming reconnect attempts when UI is not running
            if (DateTime.UtcNow - _lastFailure < ReconnectCooldown) return;

            try
            {
                DisposeClient();
                _client = new ProtobufSocketClient<StateChangeResponse, StateChangeResponse>(
                    _ipAddress, _port, _logger, connectImmediately: true, initialConnectBudgetMs: 2000);
                _connected = true;
                _logger.LogInformation("Connected to UI listener at {Ip}:{Port}", _ipAddress, _port);
            }
            catch (Exception ex)
            {
                _connected = false;
                _lastFailure = DateTime.UtcNow;
                _logger.LogDebug("Cannot connect to UI listener at {Ip}:{Port}: {Message}", _ipAddress, _port, ex.Message);
                DisposeClient();
            }
        }

        private void DisposeClient()
        {
            try { _client?.Dispose(); } catch { }
            _client = null;
        }

        public void Dispose()
        {
            DisposeClient();
        }
    }
}
