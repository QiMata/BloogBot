using System;
using System.Collections.Generic;
using RecordedTests.Shared.Tests.TestInfrastructure;

namespace RecordedTests.Shared.Tests.Scenarios;

internal sealed class WestfallDeadminesAttunementScenario : RecordedTestScenario
{
    public WestfallDeadminesAttunementScenario()
        : base("Westfall Deadmines attunement prep")
    {
    }

    public override BotScript CreateForegroundScript(ScenarioState state, ScenarioLog log)
    {
        return new BotScript(
            new TestStep[]
            {
                new TeleportToSentinelHillStep(),
                new GrantDefiasQuestlineStep(),
                new SummonPracticeOverseerStep()
            },
            Array.Empty<TestStep>(),
            new TestStep[]
            {
                new ResetDeadminesInstanceStep(),
                new RemoveKeyComponentsStep()
            },
            new TestStep[]
            {
                new DeadminesShutdownUiStep()
            });
    }

    public override BotScript CreateBackgroundScript(ScenarioState state, ScenarioLog log)
    {
        return new BotScript(
            Array.Empty<TestStep>(),
            new TestStep[]
            {
                new EscortToMoonbrookStep(),
                new AssembleDeadminesKeyStep()
            },
            Array.Empty<TestStep>(),
            Array.Empty<TestStep>());
    }

    public override IReadOnlyCollection<string> ExpectedStepIds => new[]
    {
        TeleportToSentinelHillStep.StepIdentifier,
        GrantDefiasQuestlineStep.StepIdentifier,
        SummonPracticeOverseerStep.StepIdentifier,
        ResetDeadminesInstanceStep.StepIdentifier,
        RemoveKeyComponentsStep.StepIdentifier,
        DeadminesShutdownUiStep.StepIdentifier,
        EscortToMoonbrookStep.StepIdentifier,
        AssembleDeadminesKeyStep.StepIdentifier
    };

    private sealed class TeleportToSentinelHillStep : TestStep
    {
        public const string StepIdentifier = "Deadmines.Prepare.Teleport";

        public TeleportToSentinelHillStep()
            : base(StepIdentifier, "Teleport party to Sentinel Hill and phase Moonbrook for attunement")
        {
        }
    }

    private sealed class GrantDefiasQuestlineStep : TestStep
    {
        public const string StepIdentifier = "Deadmines.Prepare.GrantQuestline";

        public GrantDefiasQuestlineStep()
            : base(StepIdentifier, "Grant 'The Defias Brotherhood' quest stages and key components")
        {
        }
    }

    private sealed class SummonPracticeOverseerStep : TestStep
    {
        public const string StepIdentifier = "Deadmines.Prepare.SummonPracticeEncounter";

        public SummonPracticeOverseerStep()
            : base(StepIdentifier, "Summon practice Overseer encounter outside Deadmines entrance")
        {
        }
    }

    private sealed class ResetDeadminesInstanceStep : TestStep
    {
        public const string StepIdentifier = "Deadmines.Reset.Instance";

        public ResetDeadminesInstanceStep()
            : base(StepIdentifier, "Reset instance state and despawn practice Overseer encounter")
        {
        }
    }

    private sealed class RemoveKeyComponentsStep : TestStep
    {
        public const string StepIdentifier = "Deadmines.Reset.RemoveKeyComponents";

        public RemoveKeyComponentsStep()
            : base(StepIdentifier, "Remove temporary key components and return party to Sentinel Hill")
        {
        }
    }

    private sealed class DeadminesShutdownUiStep : TestStep
    {
        public const string StepIdentifier = "Deadmines.Shutdown.GmUi";

        public DeadminesShutdownUiStep()
            : base(StepIdentifier, "Stop GM recording overlays and clean up tools")
        {
        }
    }

    private sealed class EscortToMoonbrookStep : TestStep
    {
        public const string StepIdentifier = "Deadmines.Execute.Escort";

        public EscortToMoonbrookStep()
            : base(StepIdentifier, "Escort contact from Sentinel Hill to Moonbrook while defending against ambushers")
        {
        }
    }

    private sealed class AssembleDeadminesKeyStep : TestStep
    {
        public const string StepIdentifier = "Deadmines.Execute.AssembleKey";

        public AssembleDeadminesKeyStep()
            : base(StepIdentifier, "Assemble key components and unlock Deadmines entrance door")
        {
        }
    }

}
