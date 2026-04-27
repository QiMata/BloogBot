#if NET8_0_OR_GREATER
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Serilog;
using Serilog.Events;
using System;
using System.Collections.Generic;
using System.IO;
using BotRunner;
using ForegroundBotRunner.Diagnostics;

namespace ForegroundBotRunner;

public static class Program
{
    private static LogLevel ResolveMinimumLogLevel()
    {
        var rawValue = Environment.GetEnvironmentVariable("WWOW_LOG_LEVEL");
        return Enum.TryParse<LogLevel>(rawValue, ignoreCase: true, out var parsed)
            ? parsed
            : LogLevel.Information;
    }

    public static void Main(string[] args)
    {
        ConfigureSerilogStaticLogger();
        try
        {
            Console.WriteLine("=== ForegroundBotRunner.exe Main() called directly ===");

            var logPath = RecordingFileArtifactGate.ResolveBaseDirectoryPath("BloogBotLogs", "injection.log");
            if (!string.IsNullOrWhiteSpace(logPath))
            {
                File.AppendAllText(logPath, $"\n=== DIRECT EXECUTION - Program.Main() called at {DateTime.Now:yyyy-MM-dd HH:mm:ss} ===\n");
            }

            var currentProcess = System.Diagnostics.Process.GetCurrentProcess();
            if (currentProcess.ProcessName.Contains("wow", StringComparison.OrdinalIgnoreCase))
            {
                Console.WriteLine("ERROR: Program.Main() is running in WoW process! Use StartInjected() instead.");
                if (!string.IsNullOrWhiteSpace(logPath))
                {
                    File.AppendAllText(logPath, "ERROR: Program.Main() running in WoW process - use StartInjected() entry point!\n");
                }
                return;
            }

            Console.WriteLine("Running ForegroundBotRunner in standalone mode...");
            DisplayProcessInfo();

            CreateHostBuilder(args).Build().Run();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Fatal error in ForegroundBotRunner Program.Main(): {ex}");
            var logPath = RecordingFileArtifactGate.ResolveBaseDirectoryPath("BloogBotLogs", "injection.log");
            if (!string.IsNullOrWhiteSpace(logPath))
            {
                File.AppendAllText(logPath, $"FATAL ERROR in Program.Main(): {ex}\n");
            }
        }
    }

    private static LogEventLevel ResolveSerilogLevel(string? rawValue, LogEventLevel fallback)
        => Enum.TryParse<LogEventLevel>(rawValue, ignoreCase: true, out var parsed)
            ? parsed
            : fallback;

    /// <summary>
    /// Configures the <see cref="Serilog.Log"/> static logger with a file-only sink
    /// so calls like <c>Serilog.Log.Information(...)</c> from <see cref="BotRunner.BotRunnerService"/>
    /// and <see cref="BotRunner.Tasks.LoadoutTask"/> produce visible output. FG cannot
    /// write to a console (it runs inside WoW.exe), so the sink is file-only and
    /// mirrors the pattern in <c>Services/BackgroundBotRunner/Program.cs</c>.
    /// </summary>
    private static void ConfigureSerilogStaticLogger()
    {
        try
        {
            var accountName = Environment.GetEnvironmentVariable("WWOW_ACCOUNT_NAME") ?? "FG";
            var logDir = Path.Combine(AppContext.BaseDirectory, "WWoWLogs");
            Directory.CreateDirectory(logDir);
            var logFile = Path.Combine(logDir, $"fg_{accountName}.log");
            var defaultLevel = ResolveSerilogLevel(Environment.GetEnvironmentVariable("WWOW_LOG_LEVEL"), LogEventLevel.Information);
            var fileLevel = ResolveSerilogLevel(Environment.GetEnvironmentVariable("WWOW_FILE_LOG_LEVEL"), defaultLevel);
            var disableFileLogs = Environment.GetEnvironmentVariable("WWOW_DISABLE_FILE_LOGS") == "1";

            var loggerConfiguration = new LoggerConfiguration().MinimumLevel.Verbose();

            if (!disableFileLogs)
            {
                loggerConfiguration = loggerConfiguration.WriteTo.File(
                    logFile,
                    outputTemplate: "[{Timestamp:HH:mm:ss.fff} {Level:u3}] {Message:lj}{NewLine}{Exception}",
                    rollingInterval: RollingInterval.Day,
                    retainedFileCountLimit: 3,
                    shared: true,
                    flushToDiskInterval: TimeSpan.FromSeconds(1),
                    restrictedToMinimumLevel: fileLevel);
            }

            Log.Logger = loggerConfiguration.CreateLogger();

            if (!disableFileLogs)
                Log.Information("FG bot log file: {LogFile}", logFile);
            Log.Information(
                "FG bot logging levels: default={DefaultLevel}, file={FileLevel}, fileEnabled={FileEnabled}",
                defaultLevel, fileLevel, !disableFileLogs);
        }
        catch
        {
            // Best-effort — never let a logging-init failure crash the injected host.
        }
    }

    /// <summary>
    /// Entry point for injected execution inside WoW.exe.
    /// Called by Loader.Load() instead of Main() when running injected.
    /// </summary>
    public static void StartInjected()
    {
        ConfigureSerilogStaticLogger();
        string logPath = "";
        try
        {
            var baseDir = AppDomain.CurrentDomain.BaseDirectory ?? Environment.CurrentDirectory;
            var diagnosticsEnabled = RecordingArtifactsFeature.IsEnabled();
            logPath = diagnosticsEnabled
                ? RecordingFileArtifactGate.ResolveWoWLogsPath("startinjected.log")
                : string.Empty;

            if (!string.IsNullOrWhiteSpace(logPath))
            {
                File.AppendAllText(logPath, $"\n=== StartInjected() at {DateTime.Now:yyyy-MM-dd HH:mm:ss} ===\n");
                File.AppendAllText(logPath, $"BaseDir: {baseDir}\n");
            }

            Console.WriteLine("=== ForegroundBotRunner StartInjected() - Running inside WoW ===");

            // Skip DisplayProcessInfo - it can crash with Access Denied
            if (!string.IsNullOrWhiteSpace(logPath))
            {
                File.AppendAllText(logPath, "STEP 1: Skipping DisplayProcessInfo\n");
            }

            // Enable FastCall crash diagnostics — logs all SEH exceptions to WWoWLogs/fastcall_crash.log
            // In production mode (letCrash=false), AVs are still caught but now logged with faulting address
            if (diagnosticsEnabled)
            {
                if (!string.IsNullOrWhiteSpace(logPath))
                {
                    File.AppendAllText(logPath, "STEP 1.5: Enabling FastCall crash diagnostics\n");
                }

                try { Mem.Functions.EnableCrashDiagnostics(letCrash: false); }
                catch (EntryPointNotFoundException ex)
                {
                    if (!string.IsNullOrWhiteSpace(logPath))
                    {
                        File.AppendAllText(logPath, $"STEP 1.5: FastCall.dll missing diagnostic exports (old DLL?): {ex.Message}\n");
                    }
                }
            }

            // Build and run the host - this will block until shutdown
            if (!string.IsNullOrWhiteSpace(logPath))
            {
                File.AppendAllText(logPath, "STEP 2: About to call CreateHostBuilder().Build().Run()\n");
            }
            CreateHostBuilder([]).Build().Run();
            if (!string.IsNullOrWhiteSpace(logPath))
            {
                File.AppendAllText(logPath, "STEP 3: Host exited normally\n");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Fatal error in StartInjected(): {ex}");
            try { if (!string.IsNullOrEmpty(logPath)) File.AppendAllText(logPath, $"EXCEPTION: {ex}\n"); } catch { }
        }
    }

    public static IHostBuilder CreateHostBuilder(string[] args) =>
        Host.CreateDefaultBuilder(args)
            .ConfigureAppConfiguration((context, builder) =>
            {
                var configDict = new Dictionary<string, string?>
                {
                    ["PathfindingService:IpAddress"] = "127.0.0.1",
                    ["PathfindingService:Port"] = "5001",
                    ["CharacterStateListener:IpAddress"] = "127.0.0.1",
                    ["CharacterStateListener:Port"] = "5002",
                    ["LoginServer:IpAddress"] = "127.0.0.1"
                };
                builder.AddInMemoryCollection(configDict);
                builder.AddEnvironmentVariables();
            })
            .ConfigureServices((hostContext, services) =>
            {
                services.AddSingleton<BotRunner.Clients.PathfindingClient>(sp =>
                {
                    var config = hostContext.Configuration;
                    var ip = config["PathfindingService:IpAddress"] ?? "127.0.0.1";
                    var port = int.Parse(config["PathfindingService:Port"] ?? "5001");
                    var logger = sp.GetRequiredService<ILoggerFactory>().CreateLogger<BotRunner.Clients.PathfindingClient>();
                    return new BotRunner.Clients.PathfindingClient(ip, port, logger);
                });
                services.AddHostedService<ForegroundBotWorker>();
            })
            .ConfigureLogging((context, builder) =>
            {
                builder.AddConsole();
                builder.SetMinimumLevel(ResolveMinimumLogLevel());
            });

    private static void DisplayProcessInfo()
    {
        try
        {
            var currentProcess = System.Diagnostics.Process.GetCurrentProcess();
            Console.WriteLine("=== PROCESS INFORMATION ===");
            Console.WriteLine($"Timestamp: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            Console.WriteLine($"Process Name: {currentProcess.ProcessName}");
            Console.WriteLine($"Process ID: {currentProcess.Id}");
            Console.WriteLine($"Main Module: {currentProcess.MainModule?.FileName ?? "N/A"}");
            Console.WriteLine($"Working Set (MB): {currentProcess.WorkingSet64 / 1024 / 1024:N2}");
            Console.WriteLine($"Thread Count: {currentProcess.Threads.Count}");
            Console.WriteLine("==============================");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error displaying process info: {ex.Message}");
        }
    }
}
#endif
