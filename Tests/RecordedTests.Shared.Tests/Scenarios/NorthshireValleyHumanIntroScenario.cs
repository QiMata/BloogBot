using System;
using System.Collections.Generic;
using RecordedTests.Shared.Tests.TestInfrastructure;

namespace RecordedTests.Shared.Tests.Scenarios;

internal sealed class NorthshireValleyHumanIntroScenario : RecordedTestScenario
{
    public NorthshireValleyHumanIntroScenario()
        : base("Northshire Valley human intro quest chain")
    {
    }

    public override BotScript CreateForegroundScript(ScenarioState state, ScenarioLog log)
    {
        return new BotScript(
            new TestStep[]
            {
                new ResetNorthshireCharacterStep(),
                new SummonNorthshireQuestGiversStep(),
                new RestockNorthshireSuppliesStep()
            },
            Array.Empty<TestStep>(),
            new TestStep[]
            {
                new NorthshireResetQuestStateStep(),
                new NorthshireReturnToSpawnStep()
            },
            new TestStep[]
            {
                new NorthshireShutdownUiStep()
            });
    }

    public override BotScript CreateBackgroundScript(ScenarioState state, ScenarioLog log)
    {
        return new BotScript(
            Array.Empty<TestStep>(),
            new TestStep[]
            {
                new NorthshireAcceptQuestsStep(),
                new NorthshireClearKoboldsStep(),
                new NorthshireTurnInQuestsStep()
            },
            Array.Empty<TestStep>(),
            Array.Empty<TestStep>());
    }

    public override IReadOnlyCollection<string> ExpectedStepIds => new[]
    {
        ResetNorthshireCharacterStep.StepIdentifier,
        SummonNorthshireQuestGiversStep.StepIdentifier,
        RestockNorthshireSuppliesStep.StepIdentifier,
        NorthshireResetQuestStateStep.StepIdentifier,
        NorthshireReturnToSpawnStep.StepIdentifier,
        NorthshireShutdownUiStep.StepIdentifier,
        NorthshireAcceptQuestsStep.StepIdentifier,
        NorthshireClearKoboldsStep.StepIdentifier,
        NorthshireTurnInQuestsStep.StepIdentifier
    };

    private sealed class ResetNorthshireCharacterStep : TestStep
    {
        public const string StepIdentifier = "Northshire.Prepare.ResetCharacter";

        public ResetNorthshireCharacterStep()
            : base(StepIdentifier, "Reset Human Mage test character to level 1 at the Northshire Abbey spawn pad")
        {
        }

        public override System.Threading.Tasks.Task ExecuteAsync(TestStepExecutionContext context, System.Threading.CancellationToken cancellationToken)
        {
            context.LogInfo("Clearing quests and hearthstone cooldowns for the Human Mage.");
            return base.ExecuteAsync(context, cancellationToken);
        }
    }

    private sealed class SummonNorthshireQuestGiversStep : TestStep
    {
        public const string StepIdentifier = "Northshire.Prepare.SummonQuestGivers";

        public SummonNorthshireQuestGiversStep()
            : base(StepIdentifier, "Summon Marshal McBride and tutorial quest givers inside the abbey")
        {
        }

        public override System.Threading.Tasks.Task ExecuteAsync(TestStepExecutionContext context, System.Threading.CancellationToken cancellationToken)
        {
            context.LogInfo("Ensuring Marshal McBride and Sergeant Willem are spawned for recordings.");
            return base.ExecuteAsync(context, cancellationToken);
        }
    }

    private sealed class RestockNorthshireSuppliesStep : TestStep
    {
        public const string StepIdentifier = "Northshire.Prepare.RestockSupplies";

        public RestockNorthshireSuppliesStep()
            : base(StepIdentifier, "Restock tutorial reagents and hearthstone charges for the intro run")
        {
        }

        public override System.Threading.Tasks.Task ExecuteAsync(TestStepExecutionContext context, System.Threading.CancellationToken cancellationToken)
        {
            context.LogInfo("Providing frost reagents and starting consumables.");
            return base.ExecuteAsync(context, cancellationToken);
        }
    }

    private sealed class NorthshireResetQuestStateStep : TestStep
    {
        public const string StepIdentifier = "Northshire.Reset.ResetQuestState";

        public NorthshireResetQuestStateStep()
            : base(StepIdentifier, "Clear quest completion flags and remove temporary rewards")
        {
        }
    }

    private sealed class NorthshireReturnToSpawnStep : TestStep
    {
        public const string StepIdentifier = "Northshire.Reset.ReturnToSpawn";

        public NorthshireReturnToSpawnStep()
            : base(StepIdentifier, "Teleport character back to the Northshire Abbey spawn point")
        {
        }
    }

    private sealed class NorthshireShutdownUiStep : TestStep
    {
        public const string StepIdentifier = "Northshire.Shutdown.GmUi";

        public NorthshireShutdownUiStep()
            : base(StepIdentifier, "Close GM tooling and stop foreground capture overlays")
        {
        }
    }

    private sealed class NorthshireAcceptQuestsStep : TestStep
    {
        public const string StepIdentifier = "Northshire.Execute.AcceptQuests";

        public NorthshireAcceptQuestsStep()
            : base(StepIdentifier, "Accept 'A Threat Within' and 'Kobold Camp Cleanup'")
        {
        }

        public override System.Threading.Tasks.Task ExecuteAsync(TestStepExecutionContext context, System.Threading.CancellationToken cancellationToken)
        {
            context.LogInfo("Interacting with Marshal McBride and Sergeant Willem for quest pickup.");
            return base.ExecuteAsync(context, cancellationToken);
        }
    }

    private sealed class NorthshireClearKoboldsStep : TestStep
    {
        public const string StepIdentifier = "Northshire.Execute.ClearKobolds";

        public NorthshireClearKoboldsStep()
            : base(StepIdentifier, "Defeat the required Kobold Workers near the vineyard")
        {
        }

        public override System.Threading.Tasks.Task ExecuteAsync(TestStepExecutionContext context, System.Threading.CancellationToken cancellationToken)
        {
            context.LogInfo("Executing Frost Mage combat rotation on Kobold Workers.");
            return base.ExecuteAsync(context, cancellationToken);
        }
    }

    private sealed class NorthshireTurnInQuestsStep : TestStep
    {
        public const string StepIdentifier = "Northshire.Execute.TurnInQuests";

        public NorthshireTurnInQuestsStep()
            : base(StepIdentifier, "Return to Marshal McBride and Sergeant Willem to complete quests")
        {
        }
    }

}
