using Microsoft.Extensions.Logging;
using System;
using System.IO;
using System.IO.Pipes;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace WoWStateManager.Logging
{
    /// <summary>
    /// Reads newline-delimited JSON log entries from a named pipe written by
    /// an injected ForegroundBotRunner instance and re-emits them through
    /// the StateManager's ILogger infrastructure.
    /// 
    /// One server is created per bot account.  Pipe name: WWoW_Log_{accountName}
    /// </summary>
    public sealed class BotLogPipeServer(string accountName, ILoggerFactory loggerFactory) : IDisposable
    {
        private readonly string _accountName = accountName;
        private readonly string _pipeName = $"WWoW_Log_{accountName}";
        private readonly ILoggerFactory _loggerFactory = loggerFactory;
        private readonly ILogger _serverLogger = loggerFactory.CreateLogger<BotLogPipeServer>();
        private readonly ILogger _botLogger = loggerFactory.CreateLogger($"ForegroundBot.{accountName}");
        private readonly CancellationTokenSource _cts = new();
        private Task? _listenTask;
        private bool _disposed;

        /// <summary>
        /// Starts the pipe server loop on a background thread.
        /// The server will accept successive client connections (one at a time)
        /// until <see cref="Dispose"/> is called.
        /// </summary>
        public void Start()
        {
            _listenTask = Task.Run(() => ListenLoop(_cts.Token));
            _serverLogger.LogInformation($"BotLogPipeServer started for '{_accountName}' on pipe '{_pipeName}'");
        }

        private async Task ListenLoop(CancellationToken ct)
        {
            while (!ct.IsCancellationRequested)
            {
                NamedPipeServerStream? pipe = null;
                try
                {
                    pipe = new NamedPipeServerStream(
                        _pipeName,
                        PipeDirection.In,
                        1,
                        PipeTransmissionMode.Byte,
                        PipeOptions.Asynchronous);

                    await pipe.WaitForConnectionAsync(ct);
                    _serverLogger.LogInformation($"Bot log pipe client connected for '{_accountName}'");

                    using var reader = new StreamReader(pipe, Encoding.UTF8, leaveOpen: false);
                    while (!ct.IsCancellationRequested && pipe.IsConnected)
                    {
                        var line = await reader.ReadLineAsync(ct);
                        if (line == null) break; // client disconnected

                        ProcessLogLine(line);
                    }

                    _serverLogger.LogInformation($"Bot log pipe client disconnected for '{_accountName}'");
                }
                catch (OperationCanceledException)
                {
                    // Normal shutdown
                }
                catch (IOException)
                {
                    // Pipe broken — client crashed. Loop will re-create the pipe.
                }
                catch (Exception ex)
                {
                    _serverLogger.LogWarning(ex, $"BotLogPipeServer error for '{_accountName}'");
                }
                finally
                {
                    pipe?.Dispose();
                }
            }
        }

        private void ProcessLogLine(string line)
        {
            try
            {
                var entry = JsonSerializer.Deserialize<PipeLogEntry>(line);
                if (entry == null) return;

                var logLevel = entry.Level?.ToLowerInvariant() switch
                {
                    "trace" => LogLevel.Trace,
                    "debug" => LogLevel.Debug,
                    "information" or "info" => LogLevel.Information,
                    "warning" or "warn" => LogLevel.Warning,
                    "error" => LogLevel.Error,
                    "critical" => LogLevel.Critical,
                    _ => LogLevel.Information,
                };

                _botLogger.Log(logLevel, "[{Category}] {Message}", entry.Category ?? "", entry.Message ?? "");
            }
            catch
            {
                // Malformed line — write it raw so nothing is lost
                _botLogger.LogInformation(line);
            }
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _cts.Cancel();
            try { _listenTask?.Wait(TimeSpan.FromSeconds(2)); } catch { }
            _cts.Dispose();
        }

        /// <summary>
        /// JSON schema for a single log line sent by the injected bot.
        /// </summary>
        private sealed class PipeLogEntry
        {
            public string? Level { get; set; }
            public string? Message { get; set; }
            public string? Category { get; set; }
            public string? Timestamp { get; set; }
        }
    }
}
