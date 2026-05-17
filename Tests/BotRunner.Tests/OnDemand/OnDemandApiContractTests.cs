using Xunit;

namespace BotRunner.Tests.OnDemand;

/// <summary>
/// Spec 23 contract tests for the OnDemand API surface.
///
/// All tests are <see cref="SkipAttribute"/>-marked with the slot-pending
/// reason until Phase 2 slots S2.5 / S2.6 land the launcher + whisper
/// handler, and Plan/14 S10.6 flips the activity_request advisor away
/// from Mode=Trivial.
///
/// Assertion contract (Spec/23 §10 + CLAUDE.md Test Isolation Rules):
/// tests assert against WoWActivitySnapshot.ondemand_instances[] (proto
/// field 43) and recent_ondemand_echoes[] (44). Never against
/// OnDemandActivityLauncher / OnDemandActivitiesModeHandler internal
/// state.
/// </summary>
public sealed class OnDemandApiContractTests
{
    private const string SlotPendingLauncher = "contract pending S2.5 (Plan/03)";
    private const string SlotPendingWhisper = "contract pending S2.6 (Plan/03)";
    private const string SlotPendingAdvisor = "contract pending S10.6 (Plan/14)";

    [Fact(Skip = SlotPendingLauncher)]
    public void RequestActivity_HappyPath_TransitionsToEngaged()
    {
        // GIVEN: a clean reserved pool with capacity for a 5-bot dungeon.
        // WHEN:  RequestActivity( dungeon.ragefire-chasm, humanRole=MEMBER ) fires.
        // THEN:  snapshot.ondemand_instances[0].stage progresses
        //        REQUESTED -> LEGALITY -> SPAWNING -> OUTFITTING ->
        //        PARTYING -> TRAVELLING -> ENGAGED with all stage_status
        //        == Succeeded; no Failed entries; expected_engaged_at
        //        from the response is within 30 s of the actual ENGAGED
        //        stage_entered_at_ms.
        Assert.Fail("S2.5 contract pending — see docs/Spec/23_ONDEMAND_API.md §15.");
    }

    [Fact(Skip = SlotPendingLauncher)]
    public void RequestActivity_PoolExhausted_RejectsWithAlternatives()
    {
        // GIVEN: reserved pool fully reserved.
        // WHEN:  RequestActivity( dungeon.ubrs, humanRole=MEMBER ) fires.
        // THEN:  snapshot.recent_ondemand_echoes[0].rejection_code ==
        //        POOL_EXHAUSTED AND the response's suggested_alternatives
        //        list contains >= 1 nearby catalog id from the same
        //        ActivityFamily (e.g. dungeon.lbrs, dungeon.brd-upper).
        Assert.Fail("S2.5 contract pending — see docs/Spec/23_ONDEMAND_API.md §15.");
    }

    [Fact(Skip = SlotPendingLauncher)]
    public void MonitorActivity_StreamsAllStageTransitions()
    {
        // GIVEN: an accepted RequestActivity with instance_id i.
        // WHEN:  the UI subscribes via MonitorActivity(i) and the
        //        Activity runs to DONE.
        // THEN:  every Stage in the §4 enum appears at least once in
        //        the stream, in order, with no gaps; the monotonic
        //        stage_entered_at_ms in snapshot.ondemand_instances
        //        matches the stream events.
        Assert.Fail("S2.5 contract pending — see docs/Spec/23_ONDEMAND_API.md §15.");
    }

    [Fact(Skip = SlotPendingLauncher)]
    public void CancelActivityInstance_DuringTravelling_StopsLaunch()
    {
        // GIVEN: an instance currently in TRAVELLING stage.
        // WHEN:  CancelActivityInstance(instance_id, reason="test") fires.
        // THEN:  within 5 s, snapshot.ondemand_instances[0].stage ==
        //        TEARDOWN; subsequent tick shows DONE.
        Assert.Fail("S2.5 contract pending — see docs/Spec/23_ONDEMAND_API.md §15.");
    }

    [Fact(Skip = SlotPendingLauncher)]
    public void RateLimit_BlocksAfterFifthRequest()
    {
        // GIVEN: a non-operator human GUID with 5 successful OnDemand
        //        requests in the last 10 minutes.
        // WHEN:  the 6th RequestActivity fires.
        // THEN:  snapshot.recent_ondemand_echoes[0].rejection_code ==
        //        RATE_LIMITED.
        Assert.Fail("S2.5 contract pending — see docs/Spec/23_ONDEMAND_API.md §15.");
    }

    [Fact(Skip = SlotPendingWhisper)]
    public void WhisperParser_RunRfc_MapsToDungeonRagefireChasmRequest()
    {
        // GIVEN: a Shodan whisper "!run rfc" from a level-15 Horde human.
        // WHEN:  OnExternalActivityRequestAsync handles it.
        // THEN:  the resulting OnDemandActivityRequest.activity_id ==
        //        "dungeon.ragefire-chasm" AND humanRole == MEMBER.
        Assert.Fail("S2.6 contract pending — see docs/Spec/23_ONDEMAND_API.md §15.");
    }

    [Fact(Skip = SlotPendingAdvisor)]
    public void WhisperParser_AmbiguousBrd_ReturnsAmbiguousRequest()
    {
        // GIVEN: a Shodan whisper "!run brd" from a level-55 Horde human
        //        AND DecisionEngine stub returns NoAdvice for the
        //        ActivityRequest advisor.
        // WHEN:  OnExternalActivityRequestAsync handles it.
        // THEN:  the response has accepted=false, rejection_code ==
        //        AMBIGUOUS_REQUEST, and suggested_alternatives lists
        //        all 3 BRD variants from the candidate set.
        Assert.Fail("S2.6 + S10.6 contract pending — see docs/Spec/23_ONDEMAND_API.md §12.");
    }

    [Fact(Skip = SlotPendingAdvisor)]
    public void WhisperParser_AmbiguousBrd_AdvisorPicksByContext()
    {
        // GIVEN: same whisper "!run brd" + level-55 Horde human in
        //        Burning Steppes AND DecisionEngine stub returns
        //        ActivityRequestAdvice{
        //           recommended_activity_id = "dungeon.brd-lower",
        //           confidence = 0.85,
        //           rationale = "level 55 prefers lower BRD wing"
        //        }.
        // WHEN:  OnExternalActivityRequestAsync handles it.
        // THEN:  the resulting OnDemandActivityRequest.activity_id ==
        //        "dungeon.brd-lower"; snapshot.advice_log contains an
        //        entry with advisor="activity_request" and
        //        rationale="level 55 prefers lower BRD wing".
        Assert.Fail("S2.6 + S10.6 contract pending — see docs/Spec/23_ONDEMAND_API.md §12.");
    }

    [Fact(Skip = SlotPendingAdvisor)]
    public void OnDemandApi_DynamicProgressive_AmbiguousWhisperResolvesPerContextTest()
    {
        // GIVEN: two synthetic operator contexts that differ in
        //        (requesting_human_level: 55 vs 60),
        //        (requesting_faction: Horde vs Alliance),
        //        (requesting_human_zone: Burning Steppes vs Searing Gorge)
        //        but issue the SAME whisper text "!run brd".
        // WHEN:  the advisor sees both contexts in turn.
        // THEN:  (dynamic) at least one of the two contexts produces a
        //        different RecommendedActivityId than the other (level-55
        //        prefers brd-lower; level-60 prefers brd-upper for the
        //        attunement chain), OR both produce the same answer when
        //        context features dominate.
        // AND:   (progressive) for each accepted launch, the post-
        //        completion roster_distance_delta_aggregate summed over
        //        the involved bot set is <= 0 (deterministic-progressive
        //        invariant preserved).
        // See Spec/23 §13.
        Assert.Fail("S10.6 contract pending — see docs/Spec/23_ONDEMAND_API.md §13.");
    }
}
