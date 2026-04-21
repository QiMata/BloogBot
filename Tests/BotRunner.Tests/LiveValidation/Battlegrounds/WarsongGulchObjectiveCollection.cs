using System.Threading.Tasks;
using Xunit;

namespace BotRunner.Tests.LiveValidation.Battlegrounds;

public class WarsongGulchObjectiveFixture : WarsongGulchFixture
{
    protected override bool UseForegroundHordeLeader => true;

    protected override bool UseForegroundAllianceLeader => true;

    // Objective fixtures drive prep end-to-end during fixture init; the
    // BattlegroundCoordinator now hands off per-bot loadouts via
    // ActionType.ApplyLoadout (P3.4) so tests just wait on coordinator state.
    protected override bool PrepareDuringInitialization => true;
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
