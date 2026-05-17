using Xunit;

namespace BotRunner.Tests.Activities;

/// <summary>
/// Spec 03 reward-selection contract tests.
///
/// Three maturity phases (per Spec/03 §Reward selection):
///   Phase 1: trivial first-valid (no advisor)
///   Phase 2: rules + lookup table (advisor with Mode=Rules)
///   Phase 3: ONNX inference (advisor with Mode=Ml)
///
/// All tests are <see cref="SkipAttribute"/>-marked with the slot-pending
/// reason until Plan/14 slots S10.1 (advisor wire) / S10.6 (mode flip) /
/// S10.7 (trace pipeline) / S10.8 (live-validation) land.
///
/// Assertion contract (CLAUDE.md Test Isolation Rules): tests assert
/// against WoWActivitySnapshot.advice_log (field 36 per Spec/19 §5) with
/// advisor="reward" entries, and against snapshot inventory deltas
/// (Player.Inventory) for the actual reward outcome. Never against
/// RewardSelector internal state.
/// </summary>
public sealed class RewardSelectorContractTests
{
    private const string SlotPendingWire = "contract pending S10.1 (Plan/14)";
    private const string SlotPendingRules = "contract pending S10.6 (Plan/14)";
    private const string SlotPendingTrivial = "contract pending S2.9 (Plan/03) trivial selector";
    private const string SlotPendingLive = "contract pending S10.8 (Plan/14)";

    [Fact(Skip = SlotPendingTrivial)]
    public void QuestTurnIn_NeverReturnsNull()
    {
        // GIVEN: a QuestRewardChoice with 4 reward options AND
        //        AdvisorMode=Trivial pinned for determinism.
        // WHEN:  IRewardSelector.SelectQuestRewardAsync runs.
        // THEN:  the returned int is in [0, 3]; never returns -1 / null.
        //        The "always picks" invariant from Spec/03 §Reward selection.
        Assert.Fail("S2.9 contract pending — see docs/Spec/03_BOTRUNNER.md §Reward selection.");
    }

    [Fact(Skip = SlotPendingWire)]
    public void PrefersDecisionEngineAdvice()
    {
        // GIVEN: DecisionEngine stub returns
        //        RewardAdvice { recommended_choice_index=2, confidence=0.9 }
        //        for a 4-choice quest reward.
        // WHEN:  SelectQuestRewardAsync runs.
        // THEN:  the returned index == 2; snapshot.advice_log[0] has
        //        advisor="reward", confidence~=0.9, used_index=2,
        //        rationale matches the stub's value.
        Assert.Fail("S10.1 contract pending — see docs/Spec/03_BOTRUNNER.md §Spec/20 wire.");
    }

    [Fact(Skip = SlotPendingWire)]
    public void FallsBackOnNoAdvice()
    {
        // GIVEN: DecisionEngine stub returns NoAdvice
        //        { error = AdviceError.ServiceDown } for any RewardContext.
        // WHEN:  SelectQuestRewardAsync runs against a 4-choice reward.
        // THEN:  the returned index matches the trivial Phase-1 fallback
        //        (lowest valid index); snapshot.advice_log[0].used_index
        //        == 0xFFFFFFFD (AdviceError.ServiceDown sentinel per
        //        Spec/20 §2 table).
        Assert.Fail("S10.1 contract pending — see docs/Spec/03_BOTRUNNER.md §Spec/20 wire.");
    }

    [Fact(Skip = SlotPendingWire)]
    public void RejectsAdviceForUnknownChoiceIndex()
    {
        // GIVEN: DecisionEngine stub returns
        //        RewardAdvice { recommended_choice_index=99, confidence=0.95 }
        //        AND the quest only has 4 reward options.
        // WHEN:  SelectQuestRewardAsync runs.
        // THEN:  the returned index matches the trivial Phase-1 fallback
        //        (not 99); snapshot.advice_log[0].used_index ==
        //        0xFFFFFFFFu (discarded-advice sentinel per Spec/19 §5).
        Assert.Fail("S10.1 contract pending — see docs/Spec/03_BOTRUNNER.md §Spec/20 wire.");
    }

    [Fact(Skip = SlotPendingRules)]
    public void PerSpecBiSSlotPreference()
    {
        // GIVEN: a Holy Priest with TargetGearSet[slot=Chest] =
        //        "Robe of the Exalted" (itemId=18814) AND a quest reward
        //        with 3 options, one of which is itemId=18814.
        //        AdvisorMode=Rules pinned; reward-rules.json has the
        //        Holy Priest BiS table.
        // WHEN:  SelectQuestRewardAsync runs.
        // THEN:  the returned index is the slot containing 18814;
        //        snapshot.advice_log[0] has advisor="reward",
        //        used_index=that-index, rationale matches the BiS rule.
        Assert.Fail("S10.6 contract pending — see docs/Spec/03_BOTRUNNER.md §Three maturity phases.");
    }

    [Fact(Skip = SlotPendingLive)]
    public void BotRunner_DynamicProgressive_RewardSelectionClosesBisDistanceTest()
    {
        // GIVEN: >=2 synthetic snapshots differing in
        //        (class, spec, currently_equipped_in_slot).
        // WHEN:  the same QuestRewardChoice is presented to each.
        // THEN:  (dynamic) at least one of the snapshots produces a
        //        DIFFERENT reward index than another (different bots
        //        prefer different rewards based on their gear state).
        // AND:   (progressive) each pick reduces the GearTier axis of
        //        RosterPlanner.Distance (Spec/05 PerAxis[DistanceAxis.GearTier])
        //        OR returns 0 delta when no option fits the bot's gear plan.
        //        Never positive.
        // AND:   replay with AdvisorMode=Trivial pinned across all
        //        snapshots still produces completed Activities with
        //        roster_distance_delta <= 0 in outcome.jsonl traces.
        // See Spec/03 §Tests and Spec/20 §10.
        Assert.Fail("S10.8 contract pending — see docs/Spec/03_BOTRUNNER.md §Tests.");
    }
}
