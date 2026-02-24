using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;

namespace Tests.Infrastructure;

/// <summary>
/// xUnit class fixture that checks whether WoWStateManager and supporting
/// services (MaNGOS auth/world/MySQL) are available for integration tests.
///
/// Composes <see cref="MangosServerFixture"/> for auth/world/MySQL checks,
/// then adds StateManager auto-start and FastCall.dll verification on top.
///
/// Safety:
///   - Named mutex prevents concurrent test processes from racing
///   - Stale processes (StateManager, WoW.exe, PathfindingService) are killed before launch
///   - Port 8088 is verified free before starting a new StateManager
///   - MaNGOS session cleanup delay after killing WoW.exe
///
/// Usage:
///   public class MyTest : IClassFixture&lt;BotServiceFixture&gt;
///   {
///       public MyTest(BotServiceFixture fixture, ITestOutputHelper output)
///       {
///           fixture.SetOutput(output);
///           Skip.IfNot(fixture.ServicesReady, fixture.UnavailableReason ?? "Services not available");
///       }
///   }
/// </summary>
public class BotServiceFixture : IAsyncLifetime
{
    private readonly MangosServerFixture _mangosFixture = new();
    private ITestOutputHelper? _output;
    private Process? _stateManagerProcess;
    private readonly List<int> _managedWoWPids = [];
    private static readonly System.Text.RegularExpressions.Regex WoWPidRegex =
        new(@"WoW\.exe started.*Process ID: (\d+)", System.Text.RegularExpressions.RegexOptions.Compiled);

    /// <summary>
    /// Machine-wide mutex to prevent multiple test processes from concurrently
    /// launching StateManagers. Only one BotServiceFixture can be initializing at a time.
    /// </summary>
    private static Mutex? _globalMutex;

    /// <summary>
    /// Whether all required services (MaNGOS + StateManager) are healthy and ready.
    /// </summary>
    public bool ServicesReady { get; private set; }

    /// <summary>
    /// Reason for service unavailability, if any.
    /// </summary>
    public string? UnavailableReason { get; private set; }

    /// <summary>
    /// The underlying MaNGOS fixture for callers that need individual service status.
    /// </summary>
    public MangosServerFixture MangosFixture => _mangosFixture;

    /// <summary>
    /// Resolves the Bot\$(Configuration)\net8.0\ output directory relative to the solution root.
    /// </summary>
    public static string BotOutputDirectory
    {
        get
        {
            var envDir = Environment.GetEnvironmentVariable("WWOW_BOT_OUTPUT_DIR");
            if (!string.IsNullOrEmpty(envDir) && Directory.Exists(envDir))
                return envDir;

            // Walk from test output (e.g. Tests\Foo\bin\Debug\net8.0\) up to solution root
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir != null)
            {
                var candidate = Path.Combine(dir.FullName, "Bot");
                if (Directory.Exists(candidate))
                {
                    // Determine configuration from our own output path
                    var config = AppContext.BaseDirectory.Contains("Release",
                        StringComparison.OrdinalIgnoreCase) ? "Release" : "Debug";
                    return Path.Combine(candidate, config, "net8.0");
                }
                dir = dir.Parent;
            }

            // Absolute fallback
            return @"E:\repos\BloogBot\Bot\Debug\net8.0";
        }
    }

    /// <summary>
    /// Sets the test output helper for logging. Call from your test constructor.
    /// </summary>
    public void SetOutput(ITestOutputHelper output) => _output = output;

    public async Task InitializeAsync()
    {
        Log("BotServiceFixture initializing...");

        // Acquire machine-wide mutex — prevents concurrent test processes from racing.
        // If another test process is already setting up, we wait up to 2 minutes for it.
        const string mutexName = "Global\\WWoW_BotServiceFixture_Mutex";
        try
        {
            _globalMutex = new Mutex(false, mutexName);
            Log("  [Mutex] Acquiring machine-wide lock (prevents concurrent StateManager launches)...");
            if (!_globalMutex.WaitOne(TimeSpan.FromMinutes(2)))
            {
                UnavailableReason = "Another test process is already running BotServiceFixture. Wait for it to finish.";
                Log($"SKIP: {UnavailableReason}");
                return;
            }
            Log("  [Mutex] Lock acquired.");
        }
        catch (AbandonedMutexException)
        {
            // Previous holder crashed — we now own the mutex, which is fine.
            Log("  [Mutex] Acquired (previous holder crashed).");
        }

        // Kill stale StateManager / WoW.exe from previous test runs so we get a clean slate.
        await KillStaleProcessesAsync();

        // Verify FastCall.dll in the Bot output directory has the LuaCall export.
        VerifyFastCallDll();

        // Delegate auth/world/MySQL checks to MangosServerFixture
        await _mangosFixture.InitializeAsync();

        if (!_mangosFixture.IsAvailable)
        {
            UnavailableReason = _mangosFixture.UnavailableReason;
            Log($"SKIP: {UnavailableReason}");
            return;
        }

        Log($"  Auth server ({_mangosFixture.Config.AuthServerPort}): {_mangosFixture.IsAuthAvailable}");
        Log($"  World server ({_mangosFixture.Config.WorldServerPort}): {_mangosFixture.IsWorldAvailable}");
        Log($"  MySQL ({_mangosFixture.Config.MySqlPort}): {_mangosFixture.IsMySqlAvailable}");

        // Verify port 8088 is free before starting
        if (IsPortInUse(8088))
        {
            Log("  [StateManager] WARNING: Port 8088 still in use after stale cleanup. Waiting 5s...");
            await Task.Delay(5000);
            if (IsPortInUse(8088))
            {
                UnavailableReason = "Port 8088 is occupied by another process after cleanup. Cannot start StateManager.";
                Log($"SKIP: {UnavailableReason}");
                return;
            }
        }

        // Always start a fresh StateManager (stale ones were killed above)
        Log("  [StateManager] Starting fresh instance...");
        var smReady = await TryStartStateManagerAsync();

        Log($"  StateManager (8088): {smReady}");

        if (smReady)
        {
            ServicesReady = true;
            Log("All services are ready!");
        }
        else
        {
            UnavailableReason = "WoWStateManager (port 8088) not available. Start it manually before running integration tests.";
            Log($"SKIP: {UnavailableReason}");
        }
    }

    /// <summary>Check if a TCP port is currently in use.</summary>
    private static bool IsPortInUse(int port)
    {
        try
        {
            using var listener = new TcpListener(IPAddress.Loopback, port);
            listener.Start();
            listener.Stop();
            return false; // Port is free
        }
        catch (SocketException)
        {
            return true; // Port in use
        }
    }

    /// <summary>
    /// Verifies that FastCall.dll in the Bot output directory contains the LuaCall export.
    /// If the file is suspiciously small (< 20KB), attempts to copy the correct version.
    /// </summary>
    private void VerifyFastCallDll()
    {
        try
        {
            var fastCallPath = Path.Combine(BotOutputDirectory, "FastCall.dll");
            if (!File.Exists(fastCallPath))
            {
                Log($"  [FastCall] WARNING: FastCall.dll not found at {fastCallPath}");
                TryCopyCorrectFastCall(fastCallPath);
                return;
            }

            var fileInfo = new FileInfo(fastCallPath);
            Log($"  [FastCall] {fastCallPath}: {fileInfo.Length} bytes, modified {fileInfo.LastWriteTime:yyyy-MM-dd HH:mm:ss}");

            // The correct FastCall.dll with LuaCall is ~62KB. The stale version is ~12KB.
            if (fileInfo.Length < 20_000)
            {
                Log($"  [FastCall] WARNING: FastCall.dll is only {fileInfo.Length} bytes — likely missing LuaCall export!");
                TryCopyCorrectFastCall(fastCallPath);
            }
        }
        catch (Exception ex)
        {
            Log($"  [FastCall] Error verifying FastCall.dll: {ex.Message}");
        }
    }

    private void TryCopyCorrectFastCall(string targetPath)
    {
        try
        {
            var botDir = Path.GetDirectoryName(Path.GetDirectoryName(targetPath));
            if (botDir == null) return;
            var sourcePath = Path.Combine(Path.GetDirectoryName(botDir)!, "net8.0", "FastCall.dll");

            if (File.Exists(sourcePath))
            {
                var sourceInfo = new FileInfo(sourcePath);
                if (sourceInfo.Length > 20_000)
                {
                    File.Copy(sourcePath, targetPath, overwrite: true);
                    Log($"  [FastCall] Copied correct FastCall.dll ({sourceInfo.Length} bytes) from {sourcePath}");
                }
                else
                {
                    Log($"  [FastCall] Source FastCall.dll is also small ({sourceInfo.Length} bytes) — cannot auto-fix");
                }
            }
            else
            {
                Log($"  [FastCall] Source not found at {sourcePath} — cannot auto-fix");
            }
        }
        catch (Exception ex)
        {
            Log($"  [FastCall] Error copying FastCall.dll: {ex.Message}");
        }
    }

    public Task DisposeAsync()
    {
        int wowKilled = 0, pfKilled = 0;

        try
        {
            // 1. Kill StateManager FIRST — prevents its monitoring loop (every 5s) from
            //    relaunching WoW.exe after we kill bot processes. StateManager detects
            //    terminated bots and re-creates them via ApplyDesiredWorkerState.
            if (_stateManagerProcess != null && !_stateManagerProcess.HasExited)
            {
                Log("Stopping StateManager (must die first to prevent WoW.exe relaunch)...");
                try
                {
                    _stateManagerProcess.Kill(entireProcessTree: true);
                    _stateManagerProcess.WaitForExit(5000);
                    Log("  StateManager process terminated.");
                }
                catch (Exception ex)
                {
                    Log($"  Warning: Could not stop StateManager: {ex.Message}");
                }
                _stateManagerProcess.Dispose();
                _stateManagerProcess = null;
            }

            // 2. Brief delay for the process tree to finish dying
            Thread.Sleep(1000);

            // 3. Kill ALL WoW.exe processes using ForceKillProcess (tries Process.Kill,
            //    then taskkill /F, then CloseMainWindow). WoW.exe is created via native
            //    CreateProcess by StateManager, so .NET's Process.Kill() often gets
            //    "Access is denied" — the taskkill fallback handles this.
            foreach (var proc in Process.GetProcessesByName("WoW"))
            {
                try
                {
                    Log($"Cleanup: killing WoW.exe PID {proc.Id}");
                    if (ForceKillProcess(proc, "WoW"))
                        wowKilled++;
                }
                finally { proc.Dispose(); }
            }

            // 4. Kill orphaned PathfindingService (supports both self-hosted exe and dotnet-hosted dll)
            pfKilled += KillPathfindingServiceProcesses("PF");

            lock (_managedWoWPids)
                _managedWoWPids.Clear();

            if (wowKilled + pfKilled > 0)
                Log($"Dispose cleanup summary: {wowKilled} WoW.exe, {pfKilled} PathfindingService killed.");
        }
        finally
        {
            // Always release the machine-wide mutex so the next test run can proceed
            try
            {
                _globalMutex?.ReleaseMutex();
                _globalMutex?.Dispose();
                _globalMutex = null;
            }
            catch (ApplicationException)
            {
                // Mutex was not owned — this is fine (e.g., InitializeAsync failed before acquiring)
            }
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// Kill any WoWStateManager.exe, WoW.exe, and PathfindingService processes
    /// left over from previous test runs. This prevents stale StateManagers from
    /// intercepting new bot connections. WoW.exe kills include a MaNGOS session
    /// cooldown so the auth server doesn't reject the next login attempt.
    /// </summary>
    private async Task KillStaleProcessesAsync()
    {
        int smKilled = 0, wowKilled = 0, pfKilled = 0;

        // 1. Kill stale StateManagers (they manage the entire bot lifecycle)
        foreach (var proc in Process.GetProcessesByName("WoWStateManager"))
        {
            try
            {
                Log($"  [Cleanup] Killing stale WoWStateManager.exe PID {proc.Id} (started {proc.StartTime:HH:mm:ss})");
                proc.Kill(entireProcessTree: true);
                proc.WaitForExit(5000);
                smKilled++;
            }
            catch (Exception ex)
            {
                Log($"  [Cleanup] Could not kill WoWStateManager PID {proc.Id}: {ex.Message}");
            }
            finally { proc.Dispose(); }
        }

        // 2. Kill orphaned WoW.exe (FG bot shells from previous StateManager runs)
        foreach (var proc in Process.GetProcessesByName("WoW"))
        {
            try
            {
                Log($"  [Cleanup] Killing orphaned WoW.exe PID {proc.Id}");
                if (ForceKillProcess(proc, "Cleanup-WoW"))
                    wowKilled++;
            }
            finally { proc.Dispose(); }
        }

        // 3. Kill orphaned PathfindingService (supports both self-hosted exe and dotnet-hosted dll)
        pfKilled += KillPathfindingServiceProcesses("Cleanup-PF");

        int totalKilled = smKilled + wowKilled + pfKilled;
        if (totalKilled > 0)
        {
            Log($"  [Cleanup] Killed: {smKilled} StateManager, {wowKilled} WoW.exe, {pfKilled} PathfindingService");

            // Wait for MaNGOS auth server to clear stale sessions (rejects duplicates otherwise)
            // Also wait for port release after StateManager kill
            int waitMs = wowKilled > 0 ? 5000 : 3000;
            Log($"  [Cleanup] Waiting {waitMs / 1000}s for MaNGOS session cleanup and port release...");
            await Task.Delay(waitMs);
        }
        else
        {
            Log("  [Cleanup] No stale processes found — clean environment.");
        }
    }

    private async Task<bool> TryStartStateManagerAsync()
    {
        try
        {
            var smExe = FindStateManagerExecutable();
            if (smExe == null)
            {
                Log("  [StateManager] Could not find WoWStateManager.exe — cannot auto-start.");
                Log("  [StateManager] Build the solution first, then re-run the test.");
                return false;
            }

            var smDir = Path.GetDirectoryName(smExe)!;
            Log($"  [StateManager] Starting exe: {smExe}");

            var envRecording = Environment.GetEnvironmentVariable("BLOOGBOT_AUTOMATED_RECORDING");

            var psi = new ProcessStartInfo
            {
                FileName = smExe,
                WorkingDirectory = smDir,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };

            var x86DotnetRoot = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
                "dotnet");
            if (Directory.Exists(x86DotnetRoot))
            {
                psi.Environment["DOTNET_ROOT(x86)"] = x86DotnetRoot;
                Log($"  [StateManager] Set DOTNET_ROOT(x86) = {x86DotnetRoot}");
            }

            if (!string.IsNullOrEmpty(envRecording))
                psi.Environment["BLOOGBOT_AUTOMATED_RECORDING"] = envRecording;

            _stateManagerProcess = Process.Start(psi);
            if (_stateManagerProcess == null)
            {
                Log("  [StateManager] Failed to start process.");
                return false;
            }

            Log($"  [StateManager] Process started (PID: {_stateManagerProcess.Id}). Waiting for port 8088...");

            _stateManagerProcess.OutputDataReceived += (_, e) =>
            {
                if (!string.IsNullOrEmpty(e.Data))
                {
                    Log($"  [StateManager-OUT] {e.Data}");
                    var match = WoWPidRegex.Match(e.Data);
                    if (match.Success && int.TryParse(match.Groups[1].Value, out var wowPid))
                    {
                        lock (_managedWoWPids)
                            _managedWoWPids.Add(wowPid);
                        Log($"  [BotServiceFixture] Tracking WoW.exe PID {wowPid} for cleanup");
                    }
                }
            };
            _stateManagerProcess.ErrorDataReceived += (_, e) =>
            {
                if (!string.IsNullOrEmpty(e.Data))
                    Log($"  [StateManager-ERR] {e.Data}");
            };
            _stateManagerProcess.BeginOutputReadLine();
            _stateManagerProcess.BeginErrorReadLine();

            const int maxWaitSeconds = 90;
            for (int i = 0; i < maxWaitSeconds; i++)
            {
                if (_stateManagerProcess.HasExited)
                {
                    Log($"  [StateManager] Process exited with code {_stateManagerProcess.ExitCode} before becoming ready.");
                    return false;
                }

                var ready = await _mangosFixture.Health.IsServiceAvailableAsync("127.0.0.1", 8088, 1000);
                if (ready)
                {
                    Log($"  [StateManager] Ready on port 8088 after {i + 1}s.");
                    return true;
                }

                if (i % 10 == 9)
                    Log($"  [StateManager] Still waiting... ({i + 1}s)");

                await Task.Delay(1000);
            }

            Log($"  [StateManager] Did not become ready within {maxWaitSeconds}s.");
            return false;
        }
        catch (Exception ex)
        {
            Log($"  [StateManager] Error starting: {ex.Message}");
            return false;
        }
    }

    private static string? FindStateManagerExecutable()
    {
        var botDir = BotOutputDirectory;
        var exeInBot = Path.Combine(botDir, "WoWStateManager.exe");
        if (File.Exists(exeInBot))
            return exeInBot;

        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null)
        {
            var candidate = Path.Combine(dir.FullName, "Bot", "Debug", "net8.0", "WoWStateManager.exe");
            if (File.Exists(candidate))
                return candidate;
            candidate = Path.Combine(dir.FullName, "Bot", "net8.0", "WoWStateManager.exe");
            if (File.Exists(candidate))
                return candidate;
            dir = dir.Parent;
        }

        const string fallback = @"E:\repos\BloogBot\Bot\Debug\net8.0\WoWStateManager.exe";
        return File.Exists(fallback) ? fallback : null;
    }

    /// <summary>
    /// Reliably kill a process by PID. Uses "taskkill /F /PID" as the primary method
    /// (proven to work for WoW.exe created via native CreateProcess), with Process.Kill()
    /// and CloseMainWindow() as fallbacks. Verifies the process is actually dead before
    /// returning success.
    /// </summary>
    private bool ForceKillProcess(Process proc, string label)
    {
        var pid = proc.Id;
        try
        {
            if (proc.HasExited) return true;
        }
        catch { /* can't check — proceed with kill attempts */ }

        // Attempt 1: taskkill /F /PID — most reliable for WoW.exe (native CreateProcess child)
        try
        {
            var taskKill = Process.Start(new ProcessStartInfo
            {
                FileName = "taskkill",
                Arguments = $"/F /PID {pid}",
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            });
            if (taskKill != null)
            {
                taskKill.WaitForExit(5000);
                var stdout = taskKill.StandardOutput.ReadToEnd();
                var stderr = taskKill.StandardError.ReadToEnd();
                var exitCode = taskKill.ExitCode;
                taskKill.Dispose();
                Log($"  [{label}] taskkill /F /PID {pid}: exit={exitCode} stdout='{stdout.Trim()}' stderr='{stderr.Trim()}'");

                if (exitCode == 0)
                {
                    // Wait for the process to actually terminate
                    Thread.Sleep(1000);
                    if (IsProcessDead(pid))
                        return true;
                    Log($"  [{label}] taskkill reported success but PID {pid} still alive!");
                }
            }
        }
        catch (Exception ex)
        {
            Log($"  [{label}] taskkill failed for PID {pid}: {ex.Message}");
        }

        // Attempt 2: .NET Process.Kill()
        try
        {
            proc.Kill();
            proc.WaitForExit(3000);
        }
        catch (Exception ex)
        {
            Log($"  [{label}] Process.Kill() failed for PID {pid}: {ex.Message}");
        }
        if (IsProcessDead(pid))
        {
            Log($"  [{label}] Process.Kill() worked for PID {pid}");
            return true;
        }

        // Attempt 3: CloseMainWindow (sends WM_CLOSE — graceful)
        try
        {
            proc.CloseMainWindow();
            Thread.Sleep(2000);
        }
        catch (Exception ex)
        {
            Log($"  [{label}] CloseMainWindow failed for PID {pid}: {ex.Message}");
        }
        if (IsProcessDead(pid))
        {
            Log($"  [{label}] CloseMainWindow worked for PID {pid}");
            return true;
        }

        Log($"  [{label}] FAILED to kill PID {pid} after all attempts!");
        return false;
    }

    /// <summary>
    /// Kill PathfindingService regardless of hosting model:
    /// 1) direct PathfindingService.exe process name
    /// 2) dotnet-hosted PathfindingService.dll discovered via listening port
    /// </summary>
    private int KillPathfindingServiceProcesses(string label)
    {
        var killed = 0;
        var seenPids = new HashSet<int>();

        foreach (var proc in Process.GetProcessesByName("PathfindingService"))
        {
            try
            {
                seenPids.Add(proc.Id);
                Log($"  [Cleanup] Killing PathfindingService PID {proc.Id}");
                if (ForceKillProcess(proc, label))
                    killed++;
            }
            finally { proc.Dispose(); }
        }

        foreach (var pid in GetListeningPidsForPort(5001))
        {
            if (seenPids.Contains(pid))
                continue;

            Process? proc = null;
            try
            {
                proc = Process.GetProcessById(pid);
                seenPids.Add(pid);
                Log($"  [Cleanup] Killing process on PathfindingService port 5001 (PID {pid}, Name '{proc.ProcessName}')");
                if (ForceKillProcess(proc, $"{label}-Port5001"))
                    killed++;
            }
            catch (ArgumentException)
            {
                // Process already exited.
            }
            finally { proc?.Dispose(); }
        }

        return killed;
    }

    /// <summary>
    /// Parses `netstat -ano -p tcp` and returns PIDs listening on the specified local TCP port.
    /// </summary>
    private static IEnumerable<int> GetListeningPidsForPort(int port)
    {
        var pids = new HashSet<int>();
        try
        {
            using var netstat = Process.Start(new ProcessStartInfo
            {
                FileName = "netstat",
                Arguments = "-ano -p tcp",
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            });

            if (netstat == null)
                return pids;

            var output = netstat.StandardOutput.ReadToEnd();
            netstat.WaitForExit(5000);

            var matches = System.Text.RegularExpressions.Regex.Matches(
                output,
                @"^\s*TCP\s+\S+:(?<port>\d+)\s+\S+\s+LISTENING\s+(?<pid>\d+)\s*$",
                System.Text.RegularExpressions.RegexOptions.Multiline | System.Text.RegularExpressions.RegexOptions.IgnoreCase);

            foreach (System.Text.RegularExpressions.Match match in matches)
            {
                if (!int.TryParse(match.Groups["port"].Value, out var parsedPort) || parsedPort != port)
                    continue;
                if (!int.TryParse(match.Groups["pid"].Value, out var parsedPid) || parsedPid <= 0)
                    continue;

                pids.Add(parsedPid);
            }
        }
        catch
        {
            // Best-effort cleanup helper; ignore parse failures.
        }

        return pids;
    }

    /// <summary>Check if a process has actually terminated (by PID).</summary>
    private static bool IsProcessDead(int pid)
    {
        try
        {
            Process.GetProcessById(pid);
            return false; // still alive
        }
        catch (ArgumentException)
        {
            return true; // no process with this PID — it's dead
        }
    }

    private void Log(string message)
    {
        if (_output != null)
        {
            try
            {
                _output.WriteLine(message);
                return;
            }
            catch
            {
                // ITestOutputHelper may throw if the test has finished.
            }
        }

        Console.WriteLine($"[BotServiceFixture] {message}");
    }
}
