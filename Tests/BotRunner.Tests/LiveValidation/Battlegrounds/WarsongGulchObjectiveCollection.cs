using System.Threading.Tasks;
using Xunit;

namespace BotRunner.Tests.LiveValidation.Battlegrounds;

public class WarsongGulchObjectiveFixture : WarsongGulchFixture
{
    protected override bool UseForegroundHordeLeader => true;

    protected override bool UseForegroundAllianceLeader => true;

    // Objective fixtures drive prep + loadout end-to-end during fixture init,
    // so tests do not need to call EnsureLoadoutPreparedAsync / ReprepareAsync
    // themselves. They just wait on the BattlegroundCoordinator state.
    protected override bool PrepareDuringInitialization => true;

    protected override async Task AfterPrepareAsync()
    {
        await base.AfterPrepareAsync();
        await RunLoadoutPrepAsync();
    }
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
