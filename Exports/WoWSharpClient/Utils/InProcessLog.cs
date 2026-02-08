using System;
using System.IO;
using System.Runtime.CompilerServices;
using System.Threading;

namespace WoWSharpClient.Utils
{
    /// <summary>
    /// Very lightweight logging that writes to the WoW process console and an injection log file.
    /// Safe for frequent small calls; avoids allocations & exceptions where possible.
    /// </summary>
    public static class InProcessLog
    {
        private static readonly object _lock = new();
        private static readonly string _logDir = InitLogDirectory();
        private static readonly string _logFile = Path.Combine(_logDir, "bot_runtime.log");
        private const long MaxBytes = 2 * 1024 * 1024; // 2 MB simple rollover

        private static string InitLogDirectory()
        {
            try
            {
                var env = Environment.GetEnvironmentVariable("WWOW_INJECT_LOG_DIR");
                string baseDir = !string.IsNullOrWhiteSpace(env)
                    ? env
                    : (AppContext.BaseDirectory ?? AppDomain.CurrentDomain.BaseDirectory ?? Environment.CurrentDirectory);
                var dir = Path.Combine(baseDir, "WWoWLogs");
                Directory.CreateDirectory(dir);
                return dir;
            }
            catch
            {
                return Environment.CurrentDirectory;
            }
        }

        public static void Info(string message, [CallerMemberName] string? member = null) => Write("INFO", message, member);
        public static void Warn(string message, [CallerMemberName] string? member = null) => Write("WARN", message, member);
        public static void Error(string message, Exception? ex = null, [CallerMemberName] string? member = null) => Write("ERR", message + (ex != null ? " :: " + ex : string.Empty), member);

        private static void Write(string level, string message, string? member)
        {
            try
            {
                var ts = DateTime.Now.ToString("HH:mm:ss.fff");
                var line = $"[{ts}] [{level}] [T{Thread.CurrentThread.ManagedThreadId}] [{member}] {message}";
                lock (_lock)
                {
                    RotateIfNeeded_NoLock();
                    File.AppendAllText(_logFile, line + Environment.NewLine);
                }
                Console.WriteLine(line);
            }
            catch { /* swallow */ }
        }

        private static void RotateIfNeeded_NoLock()
        {
            try
            {
                if (!File.Exists(_logFile)) return;
                var len = new FileInfo(_logFile).Length;
                if (len < MaxBytes) return;
                var archive = Path.Combine(_logDir, $"bot_runtime_{DateTime.Now:yyyyMMdd_HHmmss}.log");
                File.Move(_logFile, archive, overwrite: false);
            }
            catch { }
        }
    }
}
