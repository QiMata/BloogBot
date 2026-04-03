using BackgroundBotRunner;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;
using Serilog.Events;

static LogEventLevel ResolveSerilogLevel(string? rawValue, LogEventLevel fallback)
    => Enum.TryParse<LogEventLevel>(rawValue, ignoreCase: true, out var parsed)
        ? parsed
        : fallback;

// Configure the Serilog static logger so that BotRunnerService's Log.Information() calls
// produce visible output (BotRunnerService uses Serilog.Log directly, not ILogger<T>).
var accountName = Environment.GetEnvironmentVariable("WWOW_ACCOUNT_NAME") ?? "BG";
var logDir = Path.Combine(AppContext.BaseDirectory, "WWoWLogs");
Directory.CreateDirectory(logDir);
var logFile = Path.Combine(logDir, $"bg_{accountName}.log");
var defaultLevel = ResolveSerilogLevel(Environment.GetEnvironmentVariable("WWOW_LOG_LEVEL"), LogEventLevel.Information);
var consoleLevel = ResolveSerilogLevel(Environment.GetEnvironmentVariable("WWOW_CONSOLE_LOG_LEVEL"), defaultLevel);
var fileLevel = ResolveSerilogLevel(Environment.GetEnvironmentVariable("WWOW_FILE_LOG_LEVEL"), defaultLevel);
var disableFileLogs = Environment.GetEnvironmentVariable("WWOW_DISABLE_FILE_LOGS") == "1";

var loggerConfiguration = new LoggerConfiguration()
    .MinimumLevel.Verbose()
    .WriteTo.Console(
        outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}",
        restrictedToMinimumLevel: consoleLevel);

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
    Log.Information("BG bot log file: {LogFile}", logFile);
Log.Information("BG bot logging levels: default={DefaultLevel}, console={ConsoleLevel}, file={FileLevel}, fileEnabled={FileEnabled}",
    defaultLevel, consoleLevel, fileLevel, !disableFileLogs);

var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddHostedService<BackgroundBotWorker>();
var host = builder.Build();
await host.RunAsync();
