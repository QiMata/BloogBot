using System;
using System.IO;

namespace ForegroundBotRunner
{
    /// <summary>
    /// Resolves per-account log directories for ForegroundBotRunner.
    /// When multiple WoW.exe instances run simultaneously, each needs its own
    /// log directory to avoid file conflicts. Uses WWOW_ACCOUNT_NAME env var
    /// to create WWoWLogs/{accountName}/ subdirectories.
    /// </summary>
    public static class FgLogPaths
    {
        private static string? _logsDir;
        private static string? _accountName;

        /// <summary>
        /// The account name from the WWOW_ACCOUNT_NAME environment variable, or "unknown".
        /// </summary>
        public static string AccountName => _accountName ??= InitAccountName();

        /// <summary>
        /// Per-account log directory: {baseDir}/WWoWLogs/{accountName}/
        /// Falls back to {baseDir}/WWoWLogs/ if account name is unavailable.
        /// </summary>
        public static string LogsDir => _logsDir ??= InitLogsDir();

        private static string InitAccountName()
        {
            var name = Environment.GetEnvironmentVariable("WWOW_ACCOUNT_NAME");
            return string.IsNullOrWhiteSpace(name) ? "unknown" : name;
        }

        private static string InitLogsDir()
        {
            string baseDir;
            try
            {
                baseDir = Path.GetDirectoryName(System.Diagnostics.Process.GetCurrentProcess().MainModule?.FileName)
                          ?? AppContext.BaseDirectory;
            }
            catch
            {
                baseDir = AppContext.BaseDirectory;
            }

            var dir = Path.Combine(baseDir, "WWoWLogs", AccountName);
            try { Directory.CreateDirectory(dir); } catch { }
            return dir;
        }

        /// <summary>
        /// Writes a breadcrumb file for injection verification.
        /// Per-account: {baseDir}/WWoWLogs/{accountName}/{fileName}
        /// </summary>
        public static void WriteBreadcrumb(string fileName, string message)
        {
            try
            {
                var path = Path.Combine(LogsDir, fileName);
                File.AppendAllText(path, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] {message}");
            }
            catch { }
        }
    }
}
