using System;
using System.Collections.Concurrent;
using System.IO;
using System.IO.Pipes;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;

#if NET8_0_OR_GREATER
namespace ForegroundBotRunner.Logging
{
    /// <summary>
    /// Sends structured log entries to the StateManager process over a named pipe.
    /// Pipe name convention: WWoW_Log_{accountName}
    ///
    /// Falls back silently when the pipe is unavailable so the injected bot
    /// never crashes due to logging failures.
    /// </summary>
    public sealed class NamedPipeLoggerProvider : ILoggerProvider
    {
        private readonly string _pipeName;
        private readonly ConcurrentDictionary<string, NamedPipeLogger> _loggers = new(StringComparer.OrdinalIgnoreCase);

        private NamedPipeClientStream? _pipe;
        private StreamWriter? _writer;
        private readonly object _connectLock = new();
        private volatile bool _connected;
        private volatile bool _disposed;

        // Limit reconnect attempts to avoid flooding
        private DateTime _lastConnectAttempt = DateTime.MinValue;
        private static readonly TimeSpan ReconnectCooldown = TimeSpan.FromSeconds(5);

        public NamedPipeLoggerProvider(string accountName)
        {
            _pipeName = $"WWoW_Log_{accountName}";
            TryConnect();
        }

        /// <summary>
        /// Whether the pipe is currently connected.  Checked by DiagLog()
        /// to decide whether to fall back to file writes.
        /// </summary>
        public bool IsConnected => _connected;

        public ILogger CreateLogger(string categoryName)
        {
            return _loggers.GetOrAdd(categoryName, name => new NamedPipeLogger(name, this));
        }

        internal void WriteEntry(string level, string category, string message)
        {
            if (_disposed) return;

            // Try to reconnect if needed
            if (!_connected)
            {
                TryConnect();
                if (!_connected) return;
            }

            try
            {
                var entry = new
                {
                    Level = level,
                    Category = category,
                    Message = message,
                    Timestamp = DateTime.UtcNow.ToString("o")
                };

                var json = JsonSerializer.Serialize(entry);

                lock (_connectLock)
                {
                    if (_writer == null || !_connected) return;
                    _writer.WriteLine(json);
                    _writer.Flush();
                }
            }
            catch (IOException)
            {
                // Pipe broken
                _connected = false;
            }
            catch
            {
                // Swallow — logging must never throw
            }
        }

        private void TryConnect()
        {
            if (_disposed) return;
            if (DateTime.UtcNow - _lastConnectAttempt < ReconnectCooldown) return;

            lock (_connectLock)
            {
                if (_connected) return;
                _lastConnectAttempt = DateTime.UtcNow;

                try
                {
                    // Dispose old pipe if any
                    _writer?.Dispose();
                    _pipe?.Dispose();

                    _pipe = new NamedPipeClientStream(".", _pipeName, PipeDirection.Out, PipeOptions.Asynchronous);
                    _pipe.Connect(timeout: 500); // short timeout — don't block the bot
                    _writer = new StreamWriter(_pipe, Encoding.UTF8, leaveOpen: false) { AutoFlush = false };
                    _connected = true;
                }
                catch
                {
                    _connected = false;
                    _writer?.Dispose();
                    _pipe?.Dispose();
                    _writer = null;
                    _pipe = null;
                }
            }
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _connected = false;

            lock (_connectLock)
            {
                _writer?.Dispose();
                _pipe?.Dispose();
            }

            _loggers.Clear();
        }
    }

    /// <summary>
    /// Individual logger instance created per category by the provider.
    /// </summary>
    internal sealed class NamedPipeLogger(string category, NamedPipeLoggerProvider provider) : ILogger
    {
        private readonly string _category = category;
        private readonly NamedPipeLoggerProvider _provider = provider;

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => logLevel >= LogLevel.Debug;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            if (!IsEnabled(logLevel)) return;

            var message = formatter(state, exception);
            if (exception != null)
                message += $" | Exception: {exception.Message}";

            var levelStr = logLevel switch
            {
                LogLevel.Trace => "Trace",
                LogLevel.Debug => "Debug",
                LogLevel.Information => "Information",
                LogLevel.Warning => "Warning",
                LogLevel.Error => "Error",
                LogLevel.Critical => "Critical",
                _ => "Information"
            };

            _provider.WriteEntry(levelStr, _category, message);
        }
    }
}
#endif
