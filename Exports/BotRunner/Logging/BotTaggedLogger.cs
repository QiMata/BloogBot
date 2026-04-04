using Serilog;
using Serilog.Context;
using System;

namespace BotRunner.Logging;

/// <summary>
/// Replaces per-bot named pipe log servers with structured logging.
/// Each bot gets a tagged ILogger that includes the bot ID in every log entry.
/// Eliminates 3000 pipe threads for 3000-bot scenario.
///
/// Usage: var logger = BotTaggedLogger.ForBot("TESTBOT1");
/// All log entries include [BotId=TESTBOT1] for filtering.
/// </summary>
public static class BotTaggedLogger
{
    /// <summary>
    /// Create a logger tagged with a specific bot ID.
    /// All messages from this logger include the BotId property.
    /// </summary>
    public static ILogger ForBot(string botId)
    {
        return Log.Logger.ForContext("BotId", botId);
    }

    /// <summary>
    /// Create a logger tagged with bot ID and additional context.
    /// </summary>
    public static ILogger ForBot(string botId, string component)
    {
        return Log.Logger
            .ForContext("BotId", botId)
            .ForContext("Component", component);
    }

    /// <summary>
    /// Configure Serilog with bot-aware enrichment.
    /// Adds LogContext enrichment so bot ID properties flow through all sinks.
    /// File sink configuration should be done at the host level (BackgroundBotRunner/StateManager).
    /// </summary>
    public static LoggerConfiguration ConfigureBotLogging(this LoggerConfiguration config)
    {
        return config.Enrich.FromLogContext();
    }

    /// <summary>
    /// Push a bot ID scope for the current async context.
    /// All log entries within the scope include the BotId property.
    /// </summary>
    public static IDisposable BeginBotScope(string botId)
    {
        return LogContext.PushProperty("BotId", botId);
    }
}
