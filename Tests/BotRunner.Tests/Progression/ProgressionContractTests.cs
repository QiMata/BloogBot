using Xunit;

namespace BotRunner.Tests.Progression;

/// <summary>
/// Spec 05 contract tests for RosterPlanner.Distance + ProgressionPlanner
/// objective scoring.
///
/// RosterPlanner.Distance is the canonical progression metric referenced
/// by the dynamic-progressive invariant across Spec/19/20/21/22/23/24
/// (the "roster_distance_delta &lt;= 0" guard). This file pins the metric's
/// contract via tests.
///
/// All tests are <see cref="SkipAttribute"/>-marked with the slot-pending
/// reason until the RosterPlanner / ProgressionPlanner / Plan/14 S10.2
/// (objective composer tie-breaker) slots land.
///
/// Assertion contract (CLAUDE.md Test Isolation Rules): tests assert
/// against WoWActivitySnapshot.roster_distance (proto field 46) and
/// trace JSONL outcome.roster_distance_delta lines; never against
/// RosterPlanner / ProgressionPlanner private state.
/// </summary>
public sealed class ProgressionContractTests
{
    private const string SlotPendingRoster = "contract pending RosterPlanner slot (Plan follow-up)";
    private const string SlotPendingPlanner = "contract pending ProgressionPlanner slot (Plan follow-up)";
    private const string SlotPendingTieBreaker = "contract pending S10.2 (Plan/14)";
    private const string SlotPendingProjection = "contract pending S10.5 (Plan/14) + RosterPlanner";
    private const string SlotPendingLive = "contract pending S10.8 (Plan/14) + LiveValidation suite";

    [Fact(Skip = SlotPendingRoster)]
    public void RosterPlannerDistance_PureFunctionOfSnapshotAndGoal()
    {
        // GIVEN: a fixed (snapshot, goal) pair.
        // WHEN:  RosterPlanner.Distance(snapshot, goal) is called twice
        //        in a row.
        // THEN:  both calls return RosterPlannerDistance with identical
        //        TotalScalar AND identical PerAxis dictionaries. The
        //        function has no clock / DB / I/O dependence.
        // See Spec/05 §RosterPlanner.Distance.
        Assert.Fail("RosterPlanner slot pending — see docs/Spec/05_PROGRESSION.md");
    }

    [Fact(Skip = SlotPendingRoster)]
    public void RosterPlannerDistance_FullyAchievedGoalReturnsZero()
    {
        // GIVEN: a CharacterRosterGoal AND a snapshot where:
        //        - Player.Level == TargetLevel
        //        - GearTier matches TargetGearTier (every BiS slot filled)
        //        - All AttunementGoal entries in CompletedAttunements
        //        - All ReputationGoal entries met
        //        - Player.gold >= GoldTargetCopper
        //        - MountTier matches TargetMountTier
        //        - PvPRank matches PvPRankTarget
        //        - Every Profession at 300 skill
        // WHEN:  RosterPlanner.Distance(snapshot, goal).TotalScalar.
        // THEN:  returns 0.0f exactly (or within 1e-6 of zero).
        Assert.Fail("RosterPlanner slot pending — see docs/Spec/05_PROGRESSION.md");
    }

    [Fact(Skip = SlotPendingRoster)]
    public void RosterPlannerDistance_PerAxisSumEqualsTotalScalar()
    {
        // GIVEN: any (snapshot, goal) pair.
        // WHEN:  Distance(...) returns d.
        // THEN:  sum_axis(DefaultWeights[axis] * d.PerAxis[axis])
        //        equals d.TotalScalar within 1e-5.
        Assert.Fail("RosterPlanner slot pending — see docs/Spec/05_PROGRESSION.md");
    }

    [Fact(Skip = SlotPendingTieBreaker)]
    public void ProgressionPlanner_ObjectiveAdvisorRespectsTieSet()
    {
        // GIVEN: a ProgressionPlanner.PickNextObjective(...) call where
        //        2 candidate Activities tie within 1e-3 of expected
        //        roster_distance_delta AND DecisionEngine returns
        //        ObjectiveAdvice with RecommendedObjectiveId outside the
        //        tied set.
        // WHEN:  the planner finalizes its pick.
        // THEN:  the chosen Activity is the lowest-id-lex of the tied
        //        set (deterministic fallback). snapshot.advice_log[]
        //        entry shows used_index == 0xFFFFFFFF (discarded).
        Assert.Fail("S10.2 contract pending — see docs/Spec/05_PROGRESSION.md");
    }

    [Fact(Skip = SlotPendingProjection)]
    public void RosterDistance_SnapshotProjectionField46_PopulatedCorrectly()
    {
        // GIVEN: a bot with a partial CharacterRosterGoal completion
        //        (e.g. level 35, half BiS, no mount, mid-rep).
        // WHEN:  snapshot tick fires.
        // THEN:  snapshot.roster_distance (proto field 46) is non-null;
        //        total_scalar is in [0.0, 1.0]; the 8 axis_* fields sum
        //        with DefaultWeights to total_scalar within 1e-5;
        //        last_completion_delta is <= 0 if the bot just completed
        //        any Activity.
        Assert.Fail("S10.5 contract pending — see docs/Spec/05_PROGRESSION.md");
    }

    [Fact(Skip = SlotPendingLive)]
    public void Progression_DynamicProgressive_DistanceStrictlyDecreasesPerActivityTest()
    {
        // GIVEN: a representative production trace under
        //        tmp/test-runtime/traces/Progression_*/.
        // WHEN:  every "kind":"outcome" line where completion="complete"
        //        is enumerated.
        // THEN:  roster_distance_delta is <= 0 for EVERY such line;
        //        the median is strictly < 0 (most Activities make
        //        progress; cosmetic-only completions are a minority).
        // AND:   for >=2 synthetic snapshots that differ in
        //        (class, level, attunements), ProgressionPlanner.
        //        PickNextObjective(...) returns DIFFERENT catalog ids.
        // See Spec/05 §Dynamic-progressive invariant.
        Assert.Fail("LiveValidation pending — see docs/Spec/05_PROGRESSION.md");
    }
}
