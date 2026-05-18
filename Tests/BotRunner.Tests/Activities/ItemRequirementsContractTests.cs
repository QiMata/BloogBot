using Xunit;

namespace BotRunner.Tests.Activities;

/// <summary>
/// architecture/aota/05_ITEM_REQUIREMENTS.md §13 contract tests for the
/// ML-aided cheapest-source learner (§9-§11).
///
/// The §4 deterministic provenance-DAG walker is asserted by the
/// existing §8 ItemSourceResolverTests (quest reward source preference,
/// drop-rate kill budget, multi-source cost picking, BoP-never-picks-AH).
/// This file covers the ML-aided source selection on top: fall-soft on
/// NoAdvice, cannot-invent-source guarantee, BoP guardrail preservation,
/// and the dynamic-progressive invariant guarding that ML cannot reverse
/// item acquisition.
///
/// All tests are <see cref="SkipAttribute"/>-marked until Plan/14 slots
/// S10.2 (ObjectiveTieBreaker extended for source candidates) / S10.6
/// (mode flip + item-source-rules.json) / S10.7 (Item_* trace pipeline)
/// land.
///
/// Assertion contract (CLAUDE.md Test Isolation Rules): tests assert
/// against WoWActivitySnapshot.current_objective_id (Spec/19 field 34)
/// + snapshot.advice_log[] entries with advisor="objective". Never
/// against ItemSourceResolver / MarketDataCache internal state.
/// </summary>
public sealed class ItemRequirementsContractTests
{
    private const string SlotPendingTieBreaker = "contract pending S10.2 (Plan/14)";
    private const string SlotPendingModeFlip = "contract pending S10.6 (Plan/14)";
    private const string SlotPendingTrace = "contract pending S10.7 (Plan/14)";

    [Fact(Skip = SlotPendingTieBreaker)]
    public void ItemSourceLearner_FallSoftOnNoAdvice_PicksHeuristicMin()
    {
        // GIVEN: an itemId with four candidate sources from the §4 walk
        //        (QUEST, DROP, VENDOR, AH) with heuristic costs
        //        (3h, 6h, 200g+30min, 30g) AND DecisionEngine stub
        //        returns NoAdvice with error=ServiceDown.
        // WHEN:  ResolveItemSource finalizes.
        // THEN:  the chosen Source.Kind matches the
        //        sources.minBy(s => s.cost) heuristic answer (whichever
        //        is cheapest given the bot's Coinage + Position);
        //        snapshot.advice_log entry's used_index == 0xFFFFFFFD
        //        (ServiceDown sentinel per Spec/20 §2 table).
        Assert.Fail("S10.2 contract pending — see docs/architecture/aota/05_ITEM_REQUIREMENTS.md §9.");
    }

    [Fact(Skip = SlotPendingTieBreaker)]
    public void ItemSourceLearner_CannotInventSourceOutsideProvenanceDag()
    {
        // GIVEN: an itemId with §4 candidate set =
        //          ["acquire-from-quest-4736", "acquire-from-drop-dragonkin"]
        //        AND DecisionEngine stub returns ObjectiveAdvice with
        //          RecommendedObjectiveId="acquire-from-imaginary-vendor"
        //          (NOT in the candidate set), Confidence=0.95.
        // WHEN:  ResolveItemSource finalizes.
        // THEN:  the chosen Source is from the §4 candidate set
        //        (NOT "acquire-from-imaginary-vendor"). The learner can
        //        only reorder DB-validated sources; it cannot invent
        //        one the provenance walk did not produce. advice_log
        //        used_index == 0xFFFFFFFF (discarded sentinel per
        //        Spec/19 §5).
        Assert.Fail("S10.2 contract pending — see docs/architecture/aota/05_ITEM_REQUIREMENTS.md §9.");
    }

    [Fact(Skip = SlotPendingModeFlip)]
    public void ItemSourceLearner_BoPItemNeverPicksAH()
    {
        // GIVEN: a soulbound item (e.g. Drakefire Amulet itemId=13348)
        //        AND advisors.objective.mode="Ml" AND a stub ML model
        //        that hypothetically would WANT to pick AH (e.g.
        //        confidence=0.99 on an AH source).
        // WHEN:  ResolveItemSource walks step (e) "AH listing fetch".
        // THEN:  step (e) emits NO AH source for the tied set because
        //        item_template.Flags & FLAGS_BIND_PICKUP != 0 (BoP);
        //        the advisor's candidate set never includes
        //        "acquire-from-ah-*"; ML cannot pick what isn't there.
        //        Snapshot.current_objective_id transitions through one
        //        of {quest, drop, craft} sources only.
        Assert.Fail("S10.6 contract pending — see docs/architecture/aota/05_ITEM_REQUIREMENTS.md §11.");
    }

    [Fact(Skip = SlotPendingTrace)]
    public void AotaItemRequirements_LearnerPicksDifferentSourcePerAxisDominanceTest()
    {
        // GIVEN: two synthetic L50 Holy Priest snapshots identical except
        //        for roster_goal_distance axis dominance:
        //          SnapshotA: ProfessionSkill axis dominant (0.45),
        //                     Level axis near-met (0.04).
        //          SnapshotB: Level axis dominant (0.55), ProfessionSkill
        //                     axis near-met (0.05).
        //        AND a §4 candidate set for itemId=3928 (Greater Healing
        //        Potion) = [CRAFT(Sungrass), DROP(EPL mob 1% drop),
        //                   AH(30g listing — bot only has 12g so AH
        //                   pre-eliminated)].
        //        AND advisors.objective.mode="Ml" with a Phase-3 model
        //        that has trained on >=50 Greater-Healing-Potion outcome
        //        traces.
        // WHEN:  ResolveItemSource runs for each snapshot in turn.
        // THEN:  (dynamic) SnapshotA picks CRAFT (Sungrass farming
        //        loop closes the ProfessionSkill axis as a side-effect);
        //        SnapshotB picks DROP (EPL mob grind closes the Level
        //        axis). The two snapshots produce DIFFERENT Source.Kind
        //        picks per the dynamic invariant.
        // AND:   (progressive) both chosen paths produce
        //        outcome.roster_distance_delta <= 0 in the resulting
        //        trace JSONL. The §4 deterministic fallback
        //        (Mode=Trivial) would also produce non-positive deltas
        //        for both snapshots — the learner only nudges which
        //        axis closes faster.
        // See aota/05 §11 dynamic-progressive invariant.
        Assert.Fail("S10.7 contract pending — see docs/architecture/aota/05_ITEM_REQUIREMENTS.md §11.");
    }
}
