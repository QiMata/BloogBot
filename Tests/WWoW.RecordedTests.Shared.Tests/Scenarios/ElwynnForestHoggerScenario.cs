using System;
using System.Collections.Generic;
using WWoW.RecordedTests.Shared.Tests.TestInfrastructure;

namespace WWoW.RecordedTests.Shared.Tests.Scenarios;

internal sealed class ElwynnForestHoggerScenario : RecordedTestScenario
{
    public ElwynnForestHoggerScenario()
        : base("Elwynn Forest Hogger elite takedown")
    {
    }

    public override BotScript CreateForegroundScript(ScenarioState state, ScenarioLog log)
    {
        return new BotScript(
            new TestStep[]
            {
                new StageHoggerCampStep(),
                new SummonHoggerForcesStep(),
                new IssueHoggerQuestAndTrapsStep()
            },
            Array.Empty<TestStep>(),
            new TestStep[]
            {
                new DespawnHoggerEncounterStep(),
                new ResetHoggerQuestFlagsStep()
            },
            new TestStep[]
            {
                new HoggerShutdownUiStep()
            });
    }

    public override BotScript CreateBackgroundScript(ScenarioState state, ScenarioLog log)
    {
        return new BotScript(
            Array.Empty<TestStep>(),
            new TestStep[]
            {
                new TravelToHoggerCampStep(),
                new CaptureHoggerStep()
            },
            Array.Empty<TestStep>(),
            Array.Empty<TestStep>());
    }

    public override IReadOnlyCollection<string> ExpectedStepIds => new[]
    {
        StageHoggerCampStep.StepIdentifier,
        SummonHoggerForcesStep.StepIdentifier,
        IssueHoggerQuestAndTrapsStep.StepIdentifier,
        DespawnHoggerEncounterStep.StepIdentifier,
        ResetHoggerQuestFlagsStep.StepIdentifier,
        HoggerShutdownUiStep.StepIdentifier,
        TravelToHoggerCampStep.StepIdentifier,
        CaptureHoggerStep.StepIdentifier
    };

    private sealed class StageHoggerCampStep : TestStep
    {
        public const string StepIdentifier = "Hogger.Prepare.StageCamp";

        public StageHoggerCampStep()
            : base(StepIdentifier, "Phase Eastvale Logging Camp and clear ambient mobs for the encounter")
        {
        }

        public override System.Threading.Tasks.Task ExecuteAsync(TestStepExecutionContext context, System.Threading.CancellationToken cancellationToken)
        {
            context.LogInfo("Phasing Eastvale Logging Camp for Hogger showcase.");
            return base.ExecuteAsync(context, cancellationToken);
        }
    }

    private sealed class SummonHoggerForcesStep : TestStep
    {
        public const string StepIdentifier = "Hogger.Prepare.SummonForces";

        public SummonHoggerForcesStep()
            : base(StepIdentifier, "Summon Hogger, lieutenants, and supporting Alliance guards")
        {
        }
    }

    private sealed class IssueHoggerQuestAndTrapsStep : TestStep
    {
        public const string StepIdentifier = "Hogger.Prepare.IssueQuest";

        public IssueHoggerQuestAndTrapsStep()
            : base(StepIdentifier, "Provide 'Wanted: Hogger' quest and capture traps to the runner")
        {
        }
    }

    private sealed class DespawnHoggerEncounterStep : TestStep
    {
        public const string StepIdentifier = "Hogger.Reset.DespawnEncounter";

        public DespawnHoggerEncounterStep()
            : base(StepIdentifier, "Despawn Hogger, lieutenants, and temporary guards after recording")
        {
        }
    }

    private sealed class ResetHoggerQuestFlagsStep : TestStep
    {
        public const string StepIdentifier = "Hogger.Reset.ResetQuestFlags";

        public ResetHoggerQuestFlagsStep()
            : base(StepIdentifier, "Reset quest completion flags and restock capture traps")
        {
        }
    }

    private sealed class HoggerShutdownUiStep : TestStep
    {
        public const string StepIdentifier = "Hogger.Shutdown.GmUi";

        public HoggerShutdownUiStep()
            : base(StepIdentifier, "Close GM observer tools and finalize encounter logging")
        {
        }
    }

    private sealed class TravelToHoggerCampStep : TestStep
    {
        public const string StepIdentifier = "Hogger.Execute.Travel";

        public TravelToHoggerCampStep()
            : base(StepIdentifier, "Ride from Goldshire to Hogger camp clearing lieutenants")
        {
        }

        public override System.Threading.Tasks.Task ExecuteAsync(TestStepExecutionContext context, System.Threading.CancellationToken cancellationToken)
        {
            context.LogInfo("Clearing lieutenant waves and positioning guard reinforcements.");
            return base.ExecuteAsync(context, cancellationToken);
        }
    }

    private sealed class CaptureHoggerStep : TestStep
    {
        public const string StepIdentifier = "Hogger.Execute.Capture";

        public CaptureHoggerStep()
            : base(StepIdentifier, "Engage Hogger and capture him using supplied traps")
        {
        }
    }

}
