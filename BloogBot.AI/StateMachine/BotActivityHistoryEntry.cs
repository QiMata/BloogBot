using BloogBot.AI.States;

namespace BloogBot.AI.StateMachine;

public record BotActivityHistoryEntry(BotActivity Activity, TimeSpan Duration);
