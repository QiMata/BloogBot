using WWoW.AI.States;

namespace WWoW.AI.StateMachine;

public record BotActivityHistoryEntry(BotActivity Activity, TimeSpan Duration);
