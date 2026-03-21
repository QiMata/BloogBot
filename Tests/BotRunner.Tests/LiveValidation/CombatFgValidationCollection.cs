using Xunit;

namespace BotRunner.Tests.LiveValidation;

/// <summary>
/// Collection for FG combat tests — TESTBOT1 (BG) + COMBATTEST (FG).
/// COMBATTEST is the FG combat actor (injected WoW.exe) — gold standard.
/// </summary>
[CollectionDefinition(Name)]
public class CombatFgValidationCollection : ICollectionFixture<CombatFgBotFixture>
{
    public const string Name = "CombatFgValidation";
}
