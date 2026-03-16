using BackgroundBotRunner;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;

// Configure the Serilog static logger so that BotRunnerService's Log.Information() calls
// produce visible output (BotRunnerService uses Serilog.Log directly, not ILogger<T>).
var accountName = Environment.GetEnvironmentVariable("WWOW_ACCOUNT_NAME") ?? "BG";
var logDir = Path.Combine(AppContext.BaseDirectory, "WWoWLogs");
Directory.CreateDirectory(logDir);
var logFile = Path.Combine(logDir, $"bg_{accountName}.log");

Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}")
    .WriteTo.File(
        logFile,
        outputTemplate: "[{Timestamp:HH:mm:ss.fff} {Level:u3}] {Message:lj}{NewLine}{Exception}",
        rollingInterval: RollingInterval.Day,
        retainedFileCountLimit: 3,
        shared: true,
        flushToDiskInterval: TimeSpan.FromSeconds(1))
    .CreateLogger();

Log.Information("BG bot log file: {LogFile}", logFile);

var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddHostedService<BackgroundBotWorker>();
var host = builder.Build();
await host.RunAsync();
