using Communication;
using Google.Protobuf;
using Microsoft.Extensions.Logging;
using System;
using System.IO;
using System.IO.Pipes;
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

                    while (!ct.IsCancellationRequested && pipe.IsConnected)
                    {
                        var entry = await ReadProtobufEntryAsync(pipe, ct);
                        if (entry == null) break; // client disconnected

                        ProcessLogEntry(entry);
                    }

                    _serverLogger.LogInformation($"Bot log pipe client disconnected for '{_accountName}'");
                }
                catch (OperationCanceledException)
                {
                    // Normal shutdown
                }
                catch (IOException)
                {
                    // Pipe broken � client crashed. Loop will re-create the pipe.
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

        private static async Task<LogEntry?> ReadProtobufEntryAsync(Stream stream, CancellationToken ct)
        {
            var lengthBuffer = new byte[4];
            var totalRead = 0;
            while (totalRead < 4)
            {
                var bytesRead = await stream.ReadAsync(lengthBuffer.AsMemory(totalRead, 4 - totalRead), ct);
                if (bytesRead == 0) return null; // client disconnected
                totalRead += bytesRead;
            }

            var length = BitConverter.ToInt32(lengthBuffer, 0);
            if (length <= 0 || length > 1024 * 1024) return null; // sanity: max 1MB

            var buffer = new byte[length];
            totalRead = 0;
            while (totalRead < length)
            {
                var bytesRead = await stream.ReadAsync(buffer.AsMemory(totalRead, length - totalRead), ct);
                if (bytesRead == 0) return null;
                totalRead += bytesRead;
            }

            var entry = new LogEntry();
            entry.MergeFrom(buffer);
            return entry;
        }

        private void ProcessLogEntry(LogEntry entry)
        {
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

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _cts.Cancel();
            try { _listenTask?.Wait(TimeSpan.FromSeconds(2)); } catch { }
            _cts.Dispose();
        }
    }
}
