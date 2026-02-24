using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace BotRunner.Tests.Helpers
{
    /// <summary>
    /// Specifies what mode the injected ForegroundBotRunner should operate in.
    /// This is communicated to the bot via environment variables on the StateManager process.
    /// </summary>
    public enum BotTaskMode
    {
        /// <summary>Default mode - idles after entering world, awaiting StateManager actions.</summary>
        Default,
        /// <summary>Manual recording mode - user controls the character and toggles recording via spells.</summary>
        ManualRecording,
        /// <summary>Automated recording mode - runs scripted movement scenarios.</summary>
        AutomatedRecording,
        /// <summary>Idle mode - enters world but does not run any bot logic. Useful for state tracking tests.</summary>
        Idle,
        /// <summary>Hijack mode - injects into an existing WoW process instead of spawning a new one.</summary>
        Hijack
    }

    /// <summary>
    /// Configuration for launching a StateManager process from tests.
    /// Centralizes settings file writing, environment variable setup, and process launch.
    /// </summary>
    public class StateManagerLaunchConfig
    {
        /// <summary>Account name to use.</summary>
        public string AccountName { get; set; } = "ORWR1";

        /// <summary>What mode the bot should operate in once injected.</summary>
        public BotTaskMode TaskMode { get; set; } = BotTaskMode.Default;

        /// <summary>Runner type - "Foreground" or "Background".</summary>
        public string RunnerType { get; set; } = "Foreground";

        /// <summary>If set, injects into this existing WoW process instead of spawning a new one.</summary>
        public int? TargetProcessId { get; set; }

        /// <summary>Additional environment variables to set on the StateManager process.</summary>
        public Dictionary<string, string> ExtraEnvironmentVariables { get; set; } = new();
    }

    /// <summary>
    /// Manages the lifecycle of a single WoWStateManager.exe process for integration tests.
    /// Ensures only one StateManager is running at a time and provides consistent
    /// settings file management and process cleanup.
    /// </summary>
    public class StateManagerProcessHelper : IDisposable
    {
        private Process? _stateManagerProcess;
        private readonly StringBuilder _logs = new();
        private readonly object _logLock = new();
        private string? _originalSettings;
        private string? _settingsPath;

        /// <summary>Gets the captured StateManager log output.</summary>
        public string CapturedLogs
        {
            get { lock (_logLock) { return _logs.ToString(); } }
        }

        /// <summary>Gets the StateManager process, if running.</summary>
        public Process? Process => _stateManagerProcess;

        /// <summary>Gets whether the StateManager process is still running.</summary>
        public bool IsRunning => _stateManagerProcess != null && !_stateManagerProcess.HasExited;

        /// <summary>Event raised for each line of StateManager stdout.</summary>
        public event Action<string>? OnOutputLine;

        /// <summary>Event raised for each line of StateManager stderr.</summary>
        public event Action<string>? OnErrorLine;

        /// <summary>
        /// Kills any lingering WoWStateManager and WoW processes left over from previous test runs.
        /// Should be called before launching a new StateManager to prevent conflicts.
        /// </summary>
        public static void KillLingeringProcesses(Action<string>? logger = null)
        {
            foreach (var name in new[] { "WoWStateManager", "WoW" })
            {
                try
                {
                    var procs = System.Diagnostics.Process.GetProcessesByName(name);
                    foreach (var proc in procs)
                    {
                        try
                        {
                            logger?.Invoke($"Killing lingering {name} process (PID: {proc.Id})...");
                            proc.Kill(entireProcessTree: true);
                            proc.WaitForExit(5000);
                            logger?.Invoke($"  Killed PID {proc.Id}");
                        }
                        catch (Exception ex)
                        {
                            logger?.Invoke($"  Warning: Could not kill PID {proc.Id}: {ex.Message}");
                        }
                        finally
                        {
                            proc.Dispose();
                        }
                    }
                }
                catch { }
            }
        }

        /// <summary>
        /// Finds the solution root directory by walking up from a start directory.
        /// </summary>
        public static string? FindSolutionRoot(string startDirectory)
        {
            var dir = new DirectoryInfo(startDirectory);
            while (dir != null)
            {
                if (File.Exists(Path.Combine(dir.FullName, "WestworldOfWarcraft.sln")) ||
                    (Directory.Exists(Path.Combine(dir.FullName, "Services")) &&
                     Directory.Exists(Path.Combine(dir.FullName, "Exports"))))
                {
                    return dir.FullName;
                }
                dir = dir.Parent;
            }
            return null;
        }

        /// <summary>
        /// Gets the path to the StateManager build output directory (Bot/Debug/net8.0).
        /// </summary>
        public static string GetStateManagerBuildPath(string solutionRoot)
        {
            return Path.Combine(solutionRoot, "Bot", "Debug", "net8.0");
        }

        /// <summary>
        /// Writes the StateManagerSettings.json file with the given configuration.
        /// Backs up the original settings for restoration on dispose.
        /// </summary>
        public void WriteSettings(string stateManagerBuildPath, StateManagerLaunchConfig config)
        {
            var settingsDir = Path.Combine(stateManagerBuildPath, "Settings");
            _settingsPath = Path.Combine(settingsDir, "StateManagerSettings.json");
            _originalSettings = File.Exists(_settingsPath) ? File.ReadAllText(_settingsPath) : null;

            Directory.CreateDirectory(settingsDir);

            var targetProcessIdLine = config.TargetProcessId.HasValue
                ? $@",
    ""TargetProcessId"": {config.TargetProcessId.Value}"
                : "";

            var settingsJson = $@"[
  {{
    ""AccountName"": ""{config.AccountName}"",
    ""Openness"": 1.0,
    ""Conscientiousness"": 1.0,
    ""Extraversion"": 1.0,
    ""Agreeableness"": 1.0,
    ""Neuroticism"": 1.0,
    ""ShouldRun"": true,
    ""RunnerType"": ""{config.RunnerType}""{targetProcessIdLine}
  }}
]";
            File.WriteAllText(_settingsPath, settingsJson);
        }

        /// <summary>
        /// Cleans up stale breadcrumb files from previous test runs.
        /// </summary>
        public static void CleanupBreadcrumbFiles(string basePath, Action<string>? logger = null)
        {
            var breadcrumbFiles = new[]
            {
                "testentry_stdcall.txt",
                "testentry_cdecl.txt",
                "bot_startup.txt",
                "bot_status.txt",
                "bot_service_init.txt",
                "bot_service_running.txt",
                "objectmanager_init.txt",
                "wow_init.txt"
            };

            foreach (var file in breadcrumbFiles)
            {
                var path = Path.Combine(basePath, file);
                if (File.Exists(path))
                {
                    try
                    {
                        File.Delete(path);
                        logger?.Invoke($"  Cleaned up stale breadcrumb: {file}");
                    }
                    catch { }
                }
            }

            // Also clean up WWoWLogs directory
            var logsDir = Path.Combine(basePath, "WWoWLogs");
            if (Directory.Exists(logsDir))
            {
                try
                {
                    foreach (var logFile in Directory.GetFiles(logsDir, "*.log"))
                    {
                        File.Delete(logFile);
                    }
                    logger?.Invoke("  Cleaned up WWoWLogs directory");
                }
                catch { }
            }
        }

        /// <summary>
        /// Launches the WoWStateManager.exe process with the given configuration.
        /// </summary>
        /// <returns>True if the process was started successfully.</returns>
        public bool Launch(string stateManagerBuildPath, StateManagerLaunchConfig config)
        {
            if (_stateManagerProcess != null && !_stateManagerProcess.HasExited)
            {
                throw new InvalidOperationException("A StateManager process is already running. Stop it first.");
            }

            var stateManagerExe = Path.Combine(stateManagerBuildPath, "WoWStateManager.exe");
            if (!File.Exists(stateManagerExe))
            {
                var stateManagerDll = Path.Combine(stateManagerBuildPath, "WoWStateManager.dll");
                if (!File.Exists(stateManagerDll))
                {
                    throw new FileNotFoundException(
                        $"StateManager not found at: {stateManagerExe} or {stateManagerDll}. Build the solution first.");
                }
                stateManagerExe = stateManagerDll;
            }

            var psi = new ProcessStartInfo
            {
                FileName = stateManagerExe.EndsWith(".dll") ? "dotnet" : stateManagerExe,
                Arguments = stateManagerExe.EndsWith(".dll") ? $"\"{stateManagerExe}\"" : "",
                WorkingDirectory = stateManagerBuildPath,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            // Standard env vars
            psi.Environment["Logging__LogLevel__Default"] = "Debug";

            // Task mode env vars
            switch (config.TaskMode)
            {
                case BotTaskMode.AutomatedRecording:
                    psi.Environment["BLOOGBOT_AUTOMATED_RECORDING"] = "1";
                    break;
                case BotTaskMode.Idle:
                    psi.Environment["BLOOGBOT_IDLE_MODE"] = "1";
                    break;
                // ManualRecording and Default don't need special env vars
            }

            // Additional env vars from config
            foreach (var kvp in config.ExtraEnvironmentVariables)
            {
                psi.Environment[kvp.Key] = kvp.Value;
            }

            _stateManagerProcess = new Process { StartInfo = psi };

            _stateManagerProcess.OutputDataReceived += (sender, args) =>
            {
                if (!string.IsNullOrEmpty(args.Data))
                {
                    lock (_logLock) { _logs.AppendLine($"[OUT] {args.Data}"); }
                    OnOutputLine?.Invoke(args.Data);
                }
            };

            _stateManagerProcess.ErrorDataReceived += (sender, args) =>
            {
                if (!string.IsNullOrEmpty(args.Data))
                {
                    lock (_logLock) { _logs.AppendLine($"[ERR] {args.Data}"); }
                    OnErrorLine?.Invoke(args.Data);
                }
            };

            if (!_stateManagerProcess.Start())
                return false;

            _stateManagerProcess.BeginOutputReadLine();
            _stateManagerProcess.BeginErrorReadLine();

            return true;
        }

        /// <summary>
        /// Waits for a specific pattern to appear in the StateManager logs.
        /// </summary>
        public async Task<bool> WaitForLogPattern(Regex pattern, TimeSpan timeout, CancellationToken ct = default)
        {
            var sw = Stopwatch.StartNew();
            while (sw.Elapsed < timeout && !ct.IsCancellationRequested)
            {
                if (_stateManagerProcess?.HasExited == true)
                    return false;

                string logs;
                lock (_logLock) { logs = _logs.ToString(); }

                if (pattern.IsMatch(logs))
                    return true;

                await Task.Delay(500, ct);
            }
            return false;
        }

        /// <summary>
        /// Waits for any of the specified strings to appear in the StateManager logs.
        /// Returns the first matching string, or null on timeout.
        /// </summary>
        public async Task<string?> WaitForAnyLogMessage(string[] messages, TimeSpan timeout, CancellationToken ct = default)
        {
            var sw = Stopwatch.StartNew();
            while (sw.Elapsed < timeout && !ct.IsCancellationRequested)
            {
                if (_stateManagerProcess?.HasExited == true)
                    return null;

                string logs;
                lock (_logLock) { logs = _logs.ToString(); }

                foreach (var msg in messages)
                {
                    if (logs.Contains(msg))
                        return msg;
                }

                await Task.Delay(500, ct);
            }
            return null;
        }

        /// <summary>
        /// Stops the StateManager process and restores original settings.
        /// </summary>
        public void Stop()
        {
            try
            {
                if (_stateManagerProcess != null && !_stateManagerProcess.HasExited)
                {
                    _stateManagerProcess.Kill(entireProcessTree: true);
                    _stateManagerProcess.WaitForExit(10000);
                }
            }
            catch { }
            finally
            {
                _stateManagerProcess?.Dispose();
                _stateManagerProcess = null;
            }

            // Restore original settings
            RestoreSettings();
        }

        /// <summary>
        /// Restores the original StateManagerSettings.json if it was backed up.
        /// </summary>
        public void RestoreSettings()
        {
            if (_settingsPath != null && _originalSettings != null)
            {
                try
                {
                    File.WriteAllText(_settingsPath, _originalSettings);
                }
                catch { }
                _originalSettings = null;
            }
        }

        public void Dispose()
        {
            Stop();
            // Belt-and-suspenders: also kill any lingering processes by name
            KillLingeringProcesses();
        }
    }
}
