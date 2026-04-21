using Xunit;

namespace BotRunner.Tests.LiveValidation.Battlegrounds;

public class WarsongGulchObjectiveFixture : WarsongGulchFixture
{
    protected override bool UseForegroundHordeLeader => false;
}

public sealed class WarsongGulchFlagCaptureObjectiveFixture : WarsongGulchObjectiveFixture
{
}

[CollectionDefinition(Name)]
public sealed class WarsongGulchFlagCaptureObjectiveCollection : ICollectionFixture<WarsongGulchFlagCaptureObjectiveFixture>
{
    public const string Name = "WarsongGulchFlagCaptureObjectiveValidation";
}

public sealed class WarsongGulchFullGameObjectiveFixture : WarsongGulchObjectiveFixture
{
}

[CollectionDefinition(Name)]
public sealed class WarsongGulchFullGameObjectiveCollection : ICollectionFixture<WarsongGulchFullGameObjectiveFixture>
{
    public const string Name = "WarsongGulchFullGameObjectiveValidation";
}
