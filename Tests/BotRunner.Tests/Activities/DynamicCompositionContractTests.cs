using Xunit;

namespace BotRunner.Tests.Activities;

/// <summary>
/// architecture/aota/03_DYNAMIC_COMPOSITION.md contract tests for the
/// composer learning loop (§9-§11).
///
/// The §3 deterministic algorithm contract is asserted by the existing
/// IActivityContractTests.cs (added in pass 1). This file covers the
/// ML-aided composition layer on top: tie detection, advisor wire,
/// fail-soft fallback, and the dynamic-progressive invariant guarding
/// that ML cannot reverse the deterministic stack's distance closure.
///
/// All tests are <see cref="SkipAttribute"/>-marked until the matching
/// Plan/14 slots (S10.2 composer tie-breaker, S10.6 mode flip, S10.7
/// training-trace pipeline) land.
///
/// Assertion contract (CLAUDE.md Test Isolation Rules): tests assert
/// against WoWActivitySnapshot.current_objective_id (Spec/19 field 34)
/// and snapshot.advice_log[] (field 36) entries with advisor="objective".
/// Never against IActivityComposer / ObjectiveTieBreaker internal state.
/// </summary>
public sealed class DynamicCompositionContractTests
{
    private const string SlotPendingTieBreaker = "contract pending S10.2 (Plan/14)";
    private const string SlotPendingModeFlip = "contract pending S10.6 (Plan/14)";
    private const string SlotPendingTrace = "contract pending S10.7 (Plan/14)";

    [Fact(Skip = SlotPendingTieBreaker)]
    public void ComposerTieDetection_FiresOnlyWhenPriorityKeysMatchWithinEpsilon()
    {
        // GIVEN: a synthetic Objective candidate set where two Objectives
        //        have identical priority (500) AND travel cost (delta < 1e-3
        //        epsilon) AND unlock fanout, plus a third Objective with
        //        priority 400.
        // WHEN:  IActivityComposer.Compose(...) runs.
        // THEN:  ObjectiveTieBreaker detects the tie (only the two
        //        priority-500 Objectives are in the tied_set sent to the
        //        advisor); the priority-400 Objective is NOT in the tied
        //        set even though it would tie with the 500s under a
        //        loose epsilon.
        Assert.Fail("S10.2 contract pending — see docs/architecture/aota/03_DYNAMIC_COMPOSITION.md §9.");
    }

    [Fact(Skip = SlotPendingTieBreaker)]
    public void ComposerLearningLoop_NoAdviceFallsBackToLexTieBreak()
    {
        // GIVEN: a tied set of Objectives ["b-objective", "a-objective",
        //        "c-objective"] AND DecisionEngine stub returns NoAdvice
        //        with error=ServiceDown.
        // WHEN:  the composer finalizes.
        // THEN:  the chosen head is "a-objective" (lex-lowest id);
        //        snapshot.advice_log entry's used_index == 0xFFFFFFFD
        //        (ServiceDown sentinel per Spec/20 §2 table).
        Assert.Fail("S10.2 contract pending — see docs/architecture/aota/03_DYNAMIC_COMPOSITION.md §9.");
    }

    [Fact(Skip = SlotPendingTieBreaker)]
    public void ComposerLearningLoop_AdviceOutsideTieSetIsIgnored()
    {
        // GIVEN: tied set ["a-objective", "b-objective"] AND DecisionEngine
        //        stub returns ObjectiveAdvice with RecommendedObjectiveId=
        //        "z-objective" (NOT in tied set) and Confidence=0.99.
        // WHEN:  the composer finalizes.
        // THEN:  the chosen head is "a-objective" (lex fallback, not "z");
        //        snapshot.advice_log entry's used_index == 0xFFFFFFFF
        //        (discarded sentinel per Spec/19 §5).
        Assert.Fail("S10.2 contract pending — see docs/architecture/aota/03_DYNAMIC_COMPOSITION.md §9.");
    }

    [Fact(Skip = SlotPendingTieBreaker)]
    public void ComposerLearningLoop_LowConfidenceAdviceIsIgnored()
    {
        // GIVEN: tied set ["a-objective", "b-objective"] AND DecisionEngine
        //        stub returns ObjectiveAdvice with RecommendedObjectiveId=
        //        "b-objective" and Confidence=0.3 (below the 0.5 floor).
        // WHEN:  the composer finalizes.
        // THEN:  the chosen head is "a-objective" (lex fallback);
        //        snapshot.advice_log entry's used_index == 0xFFFFFFFA
        //        (LowConfidence sentinel per Spec/20 §2 table).
        Assert.Fail("S10.2 contract pending — see docs/architecture/aota/03_DYNAMIC_COMPOSITION.md §9.");
    }

    [Fact(Skip = SlotPendingModeFlip)]
    public void ComposerLearningLoop_ModeRulesFileMissingFallsBackToTrivial()
    {
        // GIVEN: config sets advisors.objective.mode="Rules" but
        //        Config/decision-engine/objective-tie-rules.json is absent.
        // WHEN:  the composer runs against a tied set.
        // THEN:  the service-side handler falls back to Phase-1 lex
        //        tie-break; response mode_used=ADVISOR_MODE_TRIVIAL;
        //        composer behavior matches the no-config baseline.
        Assert.Fail("S10.6 contract pending — see docs/architecture/aota/03_DYNAMIC_COMPOSITION.md §9.");
    }

    [Fact(Skip = SlotPendingModeFlip)]
    public void ComposerLearningLoop_ML_AidedPickStillInTiedSet()
    {
        // GIVEN: config sets advisors.objective.mode="Ml" AND a trained
        //        Models/objective/v1.onnx model exists; tied set is
        //        ["transition-to-stv", "transition-to-hillsbrad"] for the
        //        Westfall-exhausted L32 Alliance Paladin scenario.
        // WHEN:  the composer runs.
        // THEN:  the chosen Objective is one of the two tied ids (ML
        //        cannot invent an Objective outside the deterministic
        //        composer's output); snapshot.advice_log shows
        //        advisor="objective" and used_index == 0 or 1 (NOT 0xFFFFFFFF).
        Assert.Fail("S10.6 contract pending — see docs/architecture/aota/03_DYNAMIC_COMPOSITION.md §10.");
    }

    [Fact(Skip = SlotPendingTrace)]
    public void AotaDynamicComposition_LearningLoop_AdviceRespectsTieSetAndProgressesTest()
    {
        // GIVEN: >=2 synthetic snapshots that differ in
        //        (Race, Class, Level, Faction, QuestsCompleted) but
        //        share the same ActivityDefinition "zone.westfall" and
        //        the same composer §3 algorithm output (tied set).
        // WHEN:  IActivityComposer.Compose(...) runs for each snapshot
        //        with the ML advisor enabled.
        // THEN:  (dynamic) the chosen tied-head id differs between
        //        snapshots when the advisor's input features
        //        (especially roster_goal_distance[8]) differ. Identical
        //        snapshots produce identical picks.
        // AND:   (progressive) for each snapshot, after Activity
        //        completion, RosterPlanner.Distance(snapshot_post, goal)
        //        is strictly less than the pre-Activity baseline.
        //        Replaying the same scenario with the advisor pinned to
        //        NoAdvice still produces non-positive delta — ML
        //        accelerates closure but cannot reverse it.
        // See docs/architecture/aota/03_DYNAMIC_COMPOSITION.md §11.
        Assert.Fail("S10.7 contract pending — see docs/architecture/aota/03_DYNAMIC_COMPOSITION.md §11.");
    }
}
