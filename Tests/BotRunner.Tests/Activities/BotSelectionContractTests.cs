using Xunit;

namespace BotRunner.Tests.Activities;

/// <summary>
/// Spec 04 contract tests for BotSelectionScorer + BotSelectionPolicy.
///
/// Selection is OnDemand-only (autonomous progression has no scheduler
/// per Spec/05 §Group formation is organic).
///
/// All tests are <see cref="SkipAttribute"/>-marked until the
/// BotSelectionScorer slot lands (Plan follow-up) and Plan/03 S2.5/S2.7
/// ship the launcher consumer + per-activity config overrides.
///
/// Assertion contract (CLAUDE.md Test Isolation Rules): tests assert
/// against snapshot.ondemand_instances[].selection_results[] (Spec/23
/// §10 field 43.10) per the Spec/04 §Snapshot projection extension.
/// Never against BotSelectionScorer / PoolBot private state.
/// </summary>
public sealed class BotSelectionContractTests
{
    private const string SlotPendingScorer = "contract pending BotSelectionScorer slot (Plan follow-up)";
    private const string SlotPendingConfig = "contract pending S2.7 (Plan/03)";
    private const string SlotPendingLauncher = "contract pending S2.5 (Plan/03)";
    private const string SlotPendingLearner = "contract pending S10.8 (Plan/14) + weight-learner Plan follow-up";

    [Fact(Skip = SlotPendingScorer)]
    public void BotSelectionScorer_PureFunctionOfBotAndActivity()
    {
        // GIVEN: a PoolBot with RecentFailureCount=0 and a fixed
        //        ActivityDefinition.
        // WHEN:  BotSelectionScorer.Score(bot, activity) called twice.
        // THEN:  identical integer returned both times. No clock/random/
        //        I/O dependence.
        // See Spec/04 §Scoring formula.
        Assert.Fail("BotSelectionScorer slot pending — see docs/Spec/04_ACTIVITIES.md");
    }

    [Fact(Skip = SlotPendingScorer)]
    public void BotSelectionScorer_RoleFitWeightDominates()
    {
        // GIVEN: two PoolBots A and B for a Dungeon activity:
        //        - A has RoleFit=true, LevelFit=true, ClassUtility=0
        //        - B has RoleFit=false, LevelFit=true, ClassUtility=2
        //        AND BotSelectionPolicy defaults (RoleFitWeight=100,
        //        LevelFitWeight=50, ClassUtilityWeight=20).
        // WHEN:  Score(A, activity) vs Score(B, activity).
        // THEN:  Score(A) > Score(B). Role-fit dominates class-utility
        //        under default weights. RoleFitWeight not configurable
        //        downward to the point where class-utility alone wins
        //        under the homogeneous defaults.
        Assert.Fail("BotSelectionScorer slot pending — see docs/Spec/04_ACTIVITIES.md");
    }

    [Fact(Skip = SlotPendingScorer)]
    public void BotSelectionScorer_TieBreakIsLowestRecentFailureThenLexAccountName()
    {
        // GIVEN: three PoolBots TIE1, TIE2, TIE3 with identical raw
        //        scores AND RecentFailureCount(activity) = 2 / 1 / 1
        //        respectively.
        // WHEN:  the selector picks the top candidate.
        // THEN:  TIE2 is picked (lower RecentFailureCount than TIE1;
        //        lex-lower AccountName than TIE3 among the tied-on-failure
        //        candidates). Deterministic across runs.
        Assert.Fail("BotSelectionScorer slot pending — see docs/Spec/04_ACTIVITIES.md");
    }

    [Fact(Skip = SlotPendingConfig)]
    public void BotSelectionPolicy_PerFamilyOverrideRespected()
    {
        // GIVEN: Config/activities/dungeon.stratholme-undead.json declares
        //        classUtility = { "Priest": 2, "Mage": 2 } AND two
        //        candidate bots with otherwise identical (role, level)
        //        but classes Priest and Rogue.
        // WHEN:  Score(priest, stratholme_undead) vs Score(rogue,
        //        stratholme_undead).
        // THEN:  Score(priest) > Score(rogue). The per-activity override
        //        applied as expected.
        // AND:   snapshot.ondemand_instances[0].selection_results contains
        //        the Priest with score_components carrying
        //        "ClassUtility:40" (2 * ClassUtilityWeight=20).
        Assert.Fail("S2.7 contract pending — see docs/Spec/04_ACTIVITIES.md");
    }

    [Fact(Skip = SlotPendingLauncher)]
    public void OnDemandLauncher_SelectionResultsProjection_PopulatedAtSpawning()
    {
        // GIVEN: an OnDemand request for "dungeon.ragefire-chasm" with
        //        20 candidate pool bots.
        // WHEN:  the launcher enters the Spawning stage.
        // THEN:  snapshot.ondemand_instances[0].selection_results contains
        //        the top 8 candidates by score; selected=true on exactly
        //        5 entries (RoleTemplate.StandardDungeon5); each entry's
        //        score_components is a non-empty list with "RoleFit:..."
        //        as the first element.
        Assert.Fail("S2.5 contract pending — see docs/Spec/04_ACTIVITIES.md");
    }

    [Fact(Skip = SlotPendingLearner)]
    public void BotSelection_DefaultWeights_StillCompleteActivityTest()
    {
        // GIVEN: a representative OnDemand trace under
        //        tmp/test-runtime/traces/OnDemand_*/.
        // WHEN:  the trace is replayed with BotSelectionPolicy weights
        //        reset to the homogeneous defaults (all per-activity
        //        overrides ignored).
        // THEN:  the OnDemand activity still reaches stage=DONE within
        //        1.5x its original wall-clock; learned-weight delta is
        //        a quality improvement, not a correctness gate.
        // See Spec/04 §Live-validation guard.
        Assert.Fail("Off-line weight-learner pending — see docs/Spec/04_ACTIVITIES.md");
    }

    [Fact(Skip = SlotPendingLearner)]
    public void Activities_DynamicProgressive_BotSelectionRespectsProgressionCostTest()
    {
        // GIVEN: two OnDemand requests for "social.mage-port":
        //        - Request A: pool has 3 idle mages AND 0 mid-progression mages
        //        - Request B: pool has 0 idle mages AND 3 mid-progression
        //          mages (currently in econ.vendor-loop / questing)
        // WHEN:  both requests run through selection.
        // THEN:  (dynamic) Request A picks an idle mage; Request B picks
        //        the mid-progression mage with the LOWEST expected
        //        roster_distance_delta cost AND only when the human's
        //        distance reduction exceeds the bot's interruption cost.
        // AND:   (progressive) the resulting
        //        outcome.roster_distance_delta_aggregate (Spec/23 §13)
        //        is <= 0 in both cases.
        // See Spec/04 §Dynamic-progressive invariant.
        Assert.Fail("Selection-progression integration pending — see docs/Spec/04_ACTIVITIES.md");
    }
}
