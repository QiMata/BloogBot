using Xunit;

namespace BotRunner.Tests.Activities;

/// <summary>
/// Spec 19 contract tests for IActivity / IObjective runtime.
///
/// All tests are <see cref="SkipAttribute"/>-marked with the slot-pending
/// reason until Phase 2 slot S2.0 lands the IActivity / IObjective
/// interfaces (see docs/Plan/03_PHASE2_ONDEMAND_ENGINE.md).
///
/// Assertion contract (Spec/19 §11): tests assert against
/// WoWActivitySnapshot fields (current_activity_id, current_objective_id,
/// current_objective_type, advice_log[]), never against private IActivity
/// or IObjective state. This honors the CLAUDE.md "Test Isolation Rules"
/// section.
/// </summary>
public sealed class IActivityContractTests
{
    private const string SlotPending = "contract pending S2.0 (Plan/03)";

    [Fact(Skip = SlotPending)]
    public void NextObjective_ReturnsTopologicalNext()
    {
        // GIVEN: a freshly-composed IActivity for ActivityDefinition "dungeon.ubrs"
        //        and a starting snapshot at FlameCrest with QuestsCompleted={}.
        // WHEN:  IActivityComposer.Compose(...) walks the per-encounter list.
        // THEN:  snapshot.current_objective_id == "ubrs.reach-flame-crest"
        //        (the one Objective with no unmet predecessors and the
        //         highest priority).
        Assert.Fail("S2.0 contract pending — see docs/Spec/19_AOTA_RUNTIME.md §11.");
    }

    [Fact(Skip = SlotPending)]
    public void NextObjective_SkipsCompletedObjectives()
    {
        // GIVEN: an IActivity at "dungeon.ubrs"
        //        and a snapshot reflecting reach-flame-crest already
        //        complete (Player.Position inside the FlameCrest radius,
        //        QuestsCompleted unchanged).
        // WHEN:  next TickAsync emits ActivityTickResult.
        // THEN:  snapshot.current_objective_id == "ubrs.enter-instance-portal"
        //        (Objective[1]), not the now-no-op Objective[0].
        Assert.Fail("S2.0 contract pending — see docs/Spec/19_AOTA_RUNTIME.md §11.");
    }

    [Fact(Skip = SlotPending)]
    public void ComposeObjectives_HonorsEntryRequirements()
    {
        // GIVEN: an IActivity at "raid.molten-core" and a snapshot where
        //        the bot is missing the Onyxia attunement step "Eitrigg
        //        confidence" (Spec/22 lockout/attunement source).
        // WHEN:  Compose(...) runs.
        // THEN:  the prefix of snapshot.current_objective_id values seen
        //        over the first N composer ticks contains the Onyxia
        //        precondition Objective IDs *before* any
        //        "raid.molten-core.*" Objective ID. Composer does NOT
        //        return an empty list; FailureReason.EntryRequirementMissing
        //        is NOT raised.
        Assert.Fail("S2.0 contract pending — see docs/Spec/19_AOTA_RUNTIME.md §11.");
    }

    [Fact(Skip = SlotPending)]
    public void AotaRuntime_DynamicProgressive_ComposerProducesDifferentOrderingsPerSnapshotTest()
    {
        // GIVEN: three distinct synthetic snapshots that differ only in
        //        (Race, Class, Level, QuestsCompleted) for the same
        //        ActivityDefinition "zoning.westfall".
        // WHEN:  IActivityComposer.Compose(...) is invoked for each.
        // THEN:  (dynamic) at least two of the three composed
        //        IReadOnlyList<IObjective> sequences differ in either
        //        the Objective.Id ordering or the Objective set.
        // AND:   (deterministic) re-running Compose with the same
        //        snapshot inputs returns an identical sequence.
        // AND:   (progressive) for each snapshot, after ActivityCompletion
        //        IsComplete=true, RosterPlanner.Distance(snapshot, goal)
        //        is strictly less than the pre-Activity baseline.
        // See Spec/19 §10 (Dynamic-progressive invariant) and
        // Spec/05_PROGRESSION.md (RosterPlanner.Distance).
        Assert.Fail("S2.0 contract pending — see docs/Spec/19_AOTA_RUNTIME.md §10-§11.");
    }
}
