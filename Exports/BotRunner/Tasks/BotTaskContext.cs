using System.Threading;
using BotRunner.Clients;
using BotRunner.Interfaces;
using GameData.Core.Interfaces;

namespace BotRunner.Tasks;

/// <summary>
/// Per-tick execution context handed to <see cref="IBotTask.TickAsync"/> and
/// the surrounding lifecycle hooks. Phase 1 substrate per slot S1.0 and the
/// resolved R22/R23 sink shapes.
/// </summary>
/// <param name="ObjectManager">Game state surface (FG memory reader or BG packet emulator).</param>
/// <param name="Pathfinding">Recast/Detour client. Null only in tests that do not exercise navigation.</param>
/// <param name="Chat">In-game chat sink (R23). Two channels in use today: <c>"chat"</c> and <c>"whisper"</c>.</param>
/// <param name="Metrics">Counter + duration sink (R22). Tests use the <see cref="NoOpMetricsSink"/>.</param>
/// <param name="Bot">
/// Bridge to the legacy <see cref="IBotContext"/> during the shim migration.
/// Carries <c>Container</c>, <c>EventHandler</c>, <c>BotTasks</c>, <c>Config</c>,
/// and <c>LoggerFactory</c>. Family slots (S1.4..S1.13) replace direct
/// <c>Bot.*</c> reads with narrow context fields as their representative task
/// is converted to a native <see cref="IBotTask.TickAsync"/> override.
/// </param>
/// <param name="Cancellation">Cancellation token. Loops should observe it directly OR delegate to the
/// <see cref="System.Threading.CancellationToken"/> passed to <see cref="IBotTask.TickAsync"/>.</param>
public sealed record BotTaskContext(
    IObjectManager ObjectManager,
    PathfindingClient? Pathfinding,
    ChatSink Chat,
    IMetricsSink Metrics,
    IBotContext Bot,
    CancellationToken Cancellation);

/// <summary>
/// Chat-sink delegate (R23). Channels in use today: <c>"chat"</c> (bot chat
/// emitted via <see cref="GameData.Core.Interfaces.IObjectManager.SendChatMessage"/>)
/// and <c>"whisper"</c> (direct whisper).
/// </summary>
public delegate void ChatSink(string channel, string text);
