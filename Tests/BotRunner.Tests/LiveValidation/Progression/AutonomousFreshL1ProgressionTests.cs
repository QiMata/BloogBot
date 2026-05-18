using Xunit;

namespace BotRunner.Tests.LiveValidation.Progression;

/// <summary>
/// Autonomous-progression LiveValidation: fresh-L1 first-Objective.
///
/// The canonical "the autonomous loop knows where to start" test.
/// Stages a TESTBOT account at a true L1 baseline (no GM-command
/// circumvention — see Spec/13 §Test staging mode for the policy
/// distinction with the rest of the LiveValidation suite) and
/// asserts that the BotRunner's first Objective request after world
/// entry returns a quest-pickup Objective for the bot's starter
/// zone, NOT a raid attempt / attunement step / gear chase.
///
/// All tests are <see cref="SkipAttribute"/>-marked with the slot-
/// pending reason until the autonomous runtime substrate lands:
///   - Phase 2 slot S2.0 (IActivity / IObjective runtime contracts)
///   - The RosterPlanner / ProgressionPlanner orphan-service Plan
///     slot (currently flagged as a Plan/18 follow-up per pass 11)
///   - Plan/14 S10.5 snapshot projection (advice_log field 36)
///   - Plan/14 S10.7 trace writer
///
/// Assertion contract (CLAUDE.md Test Isolation Rules):
///   - tests assert against WoWActivitySnapshot fields ONLY:
///     current_activity_id (field 33), current_objective_id (34),
///     current_objective_type (35), advice_log (36),
///     roster_distance (46), Player.Level, Player.QuestLog
///   - tests must NOT construct ActionMessage directly; the
///     composer-driven IBotTask chain produces the Actions
///   - tests must NOT issue .character level / .additem /
///     .modify reputation as part of the assertion phase
///     (only .reset all at fixture init is allowed)
/// </summary>
[Collection(LiveValidationCollection.Name)]
public sealed class AutonomousFreshL1ProgressionTests
{
    private const string SlotPendingComposer = "contract pending S2.0 (Plan/03) + ProgressionPlanner orphan slot (Plan follow-up)";
    private const string SlotPendingTrace = "contract pending S10.5 + S10.7 (Plan/14) + composer";
    private const string SlotPendingLive = "contract pending S2.0 + S10.8 LiveValidation guard";

    [Fact(Skip = SlotPendingComposer)]
    [Trait("Category", "RequiresInfrastructure")]
    [Trait("StagingMode", "AutonomousProgression")]
    public void FreshL1_AutonomousComposer_PicksQuestingActivity()
    {
        // GIVEN: a TESTBOT Alliance Human Mage just reset to L1 via
        //        .reset all (only allowed GM command in this test
        //        class), logged in to Northshire spawn pad.
        //        CharacterRosterGoal has TargetLevel=60, TargetGearTier=PreRaid,
        //        Attunements=["attune.mc"], MountTier=Epic,
        //        GoldTargetCopper=100_0000 (10 gold target),
        //        PvPRankTarget=null, RareItemTargets=[].
        // WHEN:  the bot enters world AND the AutomatedModeHandler
        //        dispatches the first PickNextObjective call via
        //        ProgressionPlanner.
        // THEN:  snapshot.current_activity_id is one of the starter-
        //        questing catalog rows for the bot's race/faction
        //        (e.g. "quest.starter.elwynn-forest" for Human).
        // AND:   NOT a raid id, NOT an attunement id, NOT a dungeon id,
        //        NOT a profession id (those are correctly filtered
        //        by the §3 algorithm's per-bot DAG filter at L1).
        // See aota/03 §3 ComposeObjectives algorithm + Spec/05
        //   §ProgressionPlanner priority bands.
        Assert.Fail("S2.0 + ProgressionPlanner pending — see docs/Spec/13_TESTING.md §Test staging mode.");
    }

    [Fact(Skip = SlotPendingComposer)]
    [Trait("Category", "RequiresInfrastructure")]
    [Trait("StagingMode", "AutonomousProgression")]
    public void FreshL1_AutonomousComposer_FirstObjectiveIsQuestPickup()
    {
        // GIVEN: same fresh-L1 setup as above.
        // WHEN:  the composer synthesizes the Objective sequence per
        //        aota/03 §4 ComposeQuestingObjectives.
        // THEN:  the FIRST emitted current_objective_id is one of
        //          - "travel-to-<pickup-npc>" (an Interact-NPC
        //            Objective if the bot needs to walk to a
        //            quest-giver), OR
        //          - "accept-<quest-entry>" (a direct AcceptQuest
        //            Objective if the bot is already standing within
        //            interact range of a starter quest-giver, e.g.
        //            Marshal McBride in Northshire Abbey).
        // AND:   snapshot.current_objective_type == ObjectiveType.Travel
        //        OR ObjectiveType.AcceptQuest (Spec/19 §4 enum).
        // AND:   snapshot.Player.QuestLog has zero entries at this
        //        point (bot has not accepted anything yet).
        Assert.Fail("S2.0 contract pending — see docs/Spec/19_AOTA_RUNTIME.md §4.");
    }

    [Fact(Skip = SlotPendingComposer)]
    [Trait("Category", "RequiresInfrastructure")]
    [Trait("StagingMode", "AutonomousProgression")]
    public void FreshL1_AutonomousComposer_DoesNotCircumventProgression()
    {
        // GIVEN: same fresh-L1 setup.
        // WHEN:  the bot runs autonomously for 5 simulated minutes
        //        (accelerated timers on Westworld-Test per Spec/16).
        // THEN:  the bot's level changes ONLY through real quest XP
        //        and kill XP — verified by:
        //          - NO .character level GM command appears in the
        //            test's GM-command audit log (per Spec/13 fixture
        //            audit).
        //          - NO .additem GM command appears.
        //          - NO .modify reputation GM command appears.
        //        snapshot.Player.Level grows monotonically from 1
        //        based on quest turn-ins and kill events.
        // The point: the autonomous side honors real progression.
        // Compare with the OnDemand-equivalent staging mode (Spec/13
        // §Test staging mode) where ~95% of LiveValidation tests
        // DO issue these GM commands — that's correct for those
        // tests; it's wrong for this one.
        Assert.Fail("S2.0 + audit hook pending — see docs/Spec/13_TESTING.md §Test staging mode.");
    }

    [Fact(Skip = SlotPendingTrace)]
    [Trait("Category", "RequiresInfrastructure")]
    [Trait("StagingMode", "AutonomousProgression")]
    public void FreshL1_AutonomousComposer_PicksHighestExpectedValueQuestGiver()
    {
        // GIVEN: the bot is in a starter zone with multiple eligible
        //        L1-3 quest-givers (e.g. Northshire Abbey has Marshal
        //        McBride, Brother Sammuel, Llane Beshere, Drusilla
        //        La Salle, and others). The §3 algorithm's per-bot
        //        DAG filter (aota/04 §4) returns multiple concurrently-
        //        eligible chain heads.
        // WHEN:  the chain-ordering optimizer (aota/04 §11) runs.
        // THEN:  the chosen FIRST accept-* Objective points at the
        //        quest-giver whose downstream chain produces the
        //        HIGHEST expected roster_distance_delta. The
        //        rationale field in snapshot.advice_log[0] when
        //        advisor="objective" includes a phrase referencing
        //        "downstream chain length" / "expected xp" /
        //        "expected gold" / "BiS upgrade path" — proof the
        //        composer considered the full chain, not just the
        //        first lex-sorted quest id.
        // The Phase-1 heuristic fallback (lex sort) is acceptable
        // — see aota/04 §11 fail-soft fallback. But the composer
        // MUST have CONSIDERED the chain depth (the chain-ordering
        // optimizer queries with all eligible chain heads in the
        // tied_objective_ids set), even if it falls back to lex order.
        Assert.Fail("S2.0 + chain optimizer pending — see docs/architecture/aota/04_QUEST_CHAINS.md §11.");
    }

    [Fact(Skip = SlotPendingLive)]
    [Trait("Category", "RequiresInfrastructure")]
    [Trait("StagingMode", "AutonomousProgression")]
    public void AutonomousProgression_DynamicProgressive_FreshL1RosterDistanceClosesTest()
    {
        // GIVEN: the fresh-L1 setup AND a 30-minute autonomous
        //        simulated session on Westworld-Test accelerated
        //        timers.
        // WHEN:  the test concludes.
        // THEN:  (dynamic) the bot transitioned through at least
        //        ONE distinct current_activity_id (proving the
        //        composer responds to snapshot changes; if the bot
        //        stayed on "quest.starter.elwynn-forest" for 30 min
        //        without ever flipping to a follow-on Activity,
        //        the composer is broken).
        // AND:   (progressive) snapshot.roster_distance.total_scalar
        //        at t=30min is STRICTLY less than at t=0
        //        (the L1 baseline gives total_scalar approx 0.62
        //        with Level axis dominant; a successful 30-min
        //        autonomous run should close it by at least 0.05).
        // AND:   the produced trace JSONL at
        //          tmp/test-runtime/traces/AutonomousFreshL1Progression_*/
        //        has at least one "kind":"outcome" line with
        //        completion="complete" and roster_distance_delta < 0.
        // AND:   replaying the same scenario with all seven Spec/20
        //        advisors forced to NoAdvice (Mode=Trivial) still
        //        produces non-positive roster_distance_delta and
        //        still completes at least one Activity — the
        //        deterministic stack closes goal distance without ML
        //        (the §Example 5 cross-example takeaway #5 from
        //        aota/06 applied to the autonomous side).
        // See Spec/05 §Dynamic-progressive invariant + Spec/13
        //   §Dynamic-progressive invariant + aota/06 §Example 5.
        Assert.Fail("LiveValidation pending — see docs/Spec/13_TESTING.md §Pivotal autonomous test.");
    }
}
