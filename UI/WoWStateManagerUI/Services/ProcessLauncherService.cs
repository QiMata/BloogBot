using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;

namespace WoWStateManagerUI.Services
{
    /// <summary>
    /// Launches and monitors the StateManager process.
    /// </summary>
    public sealed class ProcessLauncherService : INotifyPropertyChanged, IDisposable
    {
        private Process? _stateManagerProcess;
        private bool _isRunning;

        public bool IsRunning
        {
            get => _isRunning;
            private set { if (_isRunning != value) { _isRunning = value; OnPropertyChanged(); } }
        }

        public int? ProcessId => _stateManagerProcess?.Id;

        /// <summary>
        /// Launch StateManager from the given executable path.
        /// </summary>
        public string Launch(string executablePath)
        {
            if (_stateManagerProcess != null && !_stateManagerProcess.HasExited)
                return $"StateManager already running (PID {_stateManagerProcess.Id})";

            if (!File.Exists(executablePath))
                return $"Executable not found: {executablePath}";

            try
            {
                var workingDir = Path.GetDirectoryName(executablePath) ?? ".";
                _stateManagerProcess = Process.Start(new ProcessStartInfo
                {
                    FileName = executablePath,
                    WorkingDirectory = workingDir,
                    UseShellExecute = false,
                });

                if (_stateManagerProcess == null)
                    return "Failed to start process";

                _stateManagerProcess.EnableRaisingEvents = true;
                _stateManagerProcess.Exited += (_, _) =>
                {
                    IsRunning = false;
                    _stateManagerProcess = null;
                };

                IsRunning = true;
                return $"StateManager launched (PID {_stateManagerProcess.Id})";
            }
            catch (Exception ex)
            {
                return $"Launch failed: {ex.Message}";
            }
        }

        /// <summary>
        /// Stop the StateManager process if it was launched by this service.
        /// </summary>
        public string Stop()
        {
            if (_stateManagerProcess == null || _stateManagerProcess.HasExited)
            {
                IsRunning = false;
                _stateManagerProcess = null;
                return "StateManager is not running";
            }

            var pid = _stateManagerProcess.Id;
            try
            {
                _stateManagerProcess.Kill(entireProcessTree: true);
                _stateManagerProcess = null;
                IsRunning = false;
                return $"StateManager stopped (PID {pid})";
            }
            catch (Exception ex)
            {
                return $"Stop failed: {ex.Message}";
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

        public void Dispose()
        {
            // Don't kill the process on UI close — let it run independently
            _stateManagerProcess = null;
        }
    }
}
