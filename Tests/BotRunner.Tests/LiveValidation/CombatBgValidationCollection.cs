using Xunit;

namespace BotRunner.Tests.LiveValidation;

/// <summary>
/// Collection for BG combat tests — TESTBOT1 (FG) + COMBATTEST (BG).
/// COMBATTEST is the combat actor; TESTBOT1 is the FG observer.
/// </summary>
[CollectionDefinition(Name)]
public class CombatBgValidationCollection : ICollectionFixture<CombatBgBotFixture>
{
    public const string Name = "CombatBgValidation";
}
