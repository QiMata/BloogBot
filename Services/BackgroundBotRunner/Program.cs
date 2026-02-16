using BackgroundBotRunner;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;

// Configure the Serilog static logger so that BotRunnerService's Log.Information() calls
// produce visible output (BotRunnerService uses Serilog.Log directly, not ILogger<T>).
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}")
    .CreateLogger();

var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddHostedService<BackgroundBotWorker>();
var host = builder.Build();
await host.RunAsync();
