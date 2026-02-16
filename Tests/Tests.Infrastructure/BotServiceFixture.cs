using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;

namespace Tests.Infrastructure;

/// <summary>
/// xUnit class fixture that checks whether WoWStateManager and supporting
/// services (MaNGOS auth/world) are available for integration tests.
///
/// When MaNGOS auth and world servers are available but WoWStateManager is not,
/// the fixture will attempt to start StateManager automatically.
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
    private readonly IntegrationTestConfig _config;
    private readonly ServiceHealthChecker _healthChecker;
    private ITestOutputHelper? _output;
    private Process? _stateManagerProcess;

    /// <summary>
    /// Whether all required services are healthy and ready.
    /// </summary>
    public bool ServicesReady { get; private set; }

    /// <summary>
    /// Reason for service unavailability, if any.
    /// </summary>
    public string? UnavailableReason { get; private set; }

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

    public BotServiceFixture()
    {
        _config = IntegrationTestConfig.FromEnvironment();
        _healthChecker = new ServiceHealthChecker();
    }

    /// <summary>
    /// Sets the test output helper for logging. Call from your test constructor.
    /// </summary>
    public void SetOutput(ITestOutputHelper output) => _output = output;

    public async Task InitializeAsync()
    {
        Log("BotServiceFixture initializing...");

        // Verify FastCall.dll in the Bot output directory has the LuaCall export.
        // A stale 12KB copy without LuaCall causes the injected bot to silently fail
        // at login (EntryPointNotFoundException caught by ThreadSynchronizer).
        VerifyFastCallDll();

        // Check if MaNGOS auth server is available
        var authReady = await _healthChecker.IsRealmdAvailableAsync(_config);

        // Check if MaNGOS world server is available
        var worldReady = await _healthChecker.IsMangosdAvailableAsync(_config);

        // Check if StateManager is listening
        var smReady = await _healthChecker.IsServiceAvailableAsync(
            "127.0.0.1", 8088, _config.HealthCheckTimeoutMs);

        // If MaNGOS is available but StateManager is not, attempt to start it
        if (!smReady && authReady && worldReady)
        {
            Log("  [StateManager] Not running — attempting to start it...");
            smReady = await TryStartStateManagerAsync();
        }
        else if (!smReady)
        {
            // Retry a few times in case StateManager is still starting
            for (int attempt = 1; attempt <= 5 && !smReady; attempt++)
            {
                Log($"  [StateManager] Not ready yet (attempt {attempt}/5), waiting 3s...");
                await Task.Delay(3000);
                smReady = await _healthChecker.IsServiceAvailableAsync(
                    "127.0.0.1", 8088, _config.HealthCheckTimeoutMs);
            }
        }

        Log($"  StateManager (8088): {smReady}");
        Log($"  Auth server ({_config.AuthServerPort}): {authReady}");
        Log($"  World server ({_config.WorldServerPort}): {worldReady}");

        if (smReady && authReady && worldReady)
        {
            ServicesReady = true;
            Log("All services are ready!");
        }
        else
        {
            var missing = new List<string>();
            if (!smReady) missing.Add("WoWStateManager (port 8088)");
            if (!authReady) missing.Add($"MaNGOS auth ({_config.AuthServerIp}:{_config.AuthServerPort})");
            if (!worldReady) missing.Add($"MaNGOS world (port {_config.WorldServerPort})");

            UnavailableReason = $"Required services not running: {string.Join(", ", missing)}. Start them manually before running integration tests.";
            Log($"SKIP: {UnavailableReason}");
        }
    }

    /// <summary>
    /// Verifies that FastCall.dll in the Bot output directory contains the LuaCall export.
    /// If the file is suspiciously small (? 20KB), attempts to copy the correct version
    /// from Bot\net8.0\FastCall.dll. Logs a warning but does not fail the fixture.
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

    /// <summary>
    /// Attempts to copy the correct FastCall.dll from Bot\net8.0\ to the target path.
    /// </summary>
    private void TryCopyCorrectFastCall(string targetPath)
    {
        try
        {
            // Walk up from Bot\Debug\net8.0 to Bot\ then down to net8.0\
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
                Log($"  [FastCall] Source not found at {sourcePath} — cannot auto-fix. See SWIMMING-RECORDING-QUICKSTART.md");
            }
        }
        catch (Exception ex)
        {
            Log($"  [FastCall] Error copying FastCall.dll: {ex.Message}");
        }
    }

    public Task DisposeAsync()
    {
        // If we started StateManager, terminate it on cleanup
        if (_stateManagerProcess != null && !_stateManagerProcess.HasExited)
        {
            Log("Stopping StateManager process launched by fixture...");
            try
            {
                _stateManagerProcess.Kill(entireProcessTree: true);
                _stateManagerProcess.WaitForExit(5000);
            }
            catch (Exception ex)
            {
                Log($"  Warning: Could not stop StateManager: {ex.Message}");
            }
            _stateManagerProcess.Dispose();
            _stateManagerProcess = null;
        }
        return Task.CompletedTask;
    }

    /// <summary>
    /// Attempts to start WoWStateManager as a child process and waits for it to
    /// begin listening on port 8088. Returns true if StateManager becomes ready.
    /// </summary>
    private async Task<bool> TryStartStateManagerAsync()
    {
        try
        {
            // Prefer running the pre-built exe directly to avoid architecture issues
            // (WoWStateManager targets x86 for 32-bit WoW injection)
            var smExe = FindStateManagerExecutable();
            if (smExe == null)
            {
                Log("  [StateManager] Could not find WoWStateManager.exe — cannot auto-start.");
                Log("  [StateManager] Build the solution first, then re-run the test.");
                return false;
            }

            var smDir = Path.GetDirectoryName(smExe)!;
            Log($"  [StateManager] Starting exe: {smExe}");

            // Set the automated recording env var before launching
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

            // WoWStateManager is an x86 (32-bit) executable. Ensure it finds the
            // 32-bit .NET runtime by setting DOTNET_ROOT(x86).
            var x86DotnetRoot = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
                "dotnet");
            if (Directory.Exists(x86DotnetRoot))
            {
                psi.Environment["DOTNET_ROOT(x86)"] = x86DotnetRoot;
                Log($"  [StateManager] Set DOTNET_ROOT(x86) = {x86DotnetRoot}");
            }

            // Forward the automated recording environment variable
            if (!string.IsNullOrEmpty(envRecording))
                psi.Environment["BLOOGBOT_AUTOMATED_RECORDING"] = envRecording;

            _stateManagerProcess = Process.Start(psi);
            if (_stateManagerProcess == null)
            {
                Log("  [StateManager] Failed to start process.");
                return false;
            }

            Log($"  [StateManager] Process started (PID: {_stateManagerProcess.Id}). Waiting for port 8088...");

            // Drain stdout/stderr asynchronously to prevent deadlocks and capture diagnostics
            _stateManagerProcess.OutputDataReceived += (_, e) =>
            {
                if (!string.IsNullOrEmpty(e.Data))
                    Log($"  [StateManager-OUT] {e.Data}");
            };
            _stateManagerProcess.ErrorDataReceived += (_, e) =>
            {
                if (!string.IsNullOrEmpty(e.Data))
                    Log($"  [StateManager-ERR] {e.Data}");
            };
            _stateManagerProcess.BeginOutputReadLine();
            _stateManagerProcess.BeginErrorReadLine();

            // Wait up to 90 seconds for StateManager to start listening
            // (it may need to build + start PathfindingService + load nav data)
            const int maxWaitSeconds = 90;
            for (int i = 0; i < maxWaitSeconds; i++)
            {
                if (_stateManagerProcess.HasExited)
                {
                    Log($"  [StateManager] Process exited with code {_stateManagerProcess.ExitCode} before becoming ready.");
                    return false;
                }

                var ready = await _healthChecker.IsServiceAvailableAsync("127.0.0.1", 8088, 1000);
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

    /// <summary>
    /// Locates the WoWStateManager.exe by looking in the Bot output directory.
    /// </summary>
    private static string? FindStateManagerExecutable()
    {
        // First check the Bot output directory (unified build output)
        var botDir = BotOutputDirectory;
        var exeInBot = Path.Combine(botDir, "WoWStateManager.exe");
        if (File.Exists(exeInBot))
            return exeInBot;

        // Walk up from test output dir to find the Bot directory
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

        // Absolute fallback
        const string fallback = @"E:\repos\BloogBot\Bot\Debug\net8.0\WoWStateManager.exe";
        return File.Exists(fallback) ? fallback : null;
    }

    /// <summary>
    /// Locates the WoWStateManager.csproj by walking up from the test output directory.
    /// </summary>
    private static string? FindStateManagerProject()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null)
        {
            var candidate = Path.Combine(dir.FullName, "Services", "WoWStateManager", "WoWStateManager.csproj");
            if (File.Exists(candidate))
                return candidate;
            dir = dir.Parent;
        }
        // Absolute fallback
        const string fallback = @"E:\repos\BloogBot\Services\WoWStateManager\WoWStateManager.csproj";
        return File.Exists(fallback) ? fallback : null;
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
