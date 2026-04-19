using Xunit;

namespace BotRunner.Tests.LiveValidation.Battlegrounds;

public sealed class WarsongGulchObjectiveFixture : WarsongGulchFixture
{
    protected override bool UseForegroundHordeLeader => false;
}

[CollectionDefinition(Name)]
public sealed class WarsongGulchObjectiveCollection : ICollectionFixture<WarsongGulchObjectiveFixture>
{
    public const string Name = "WarsongGulchObjectiveValidation";
}
