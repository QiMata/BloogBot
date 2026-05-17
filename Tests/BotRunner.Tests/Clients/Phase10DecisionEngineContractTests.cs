using Xunit;

namespace BotRunner.Tests.Clients;

/// <summary>
/// Plan 14 (Phase 10) decision-engine integration contract tests.
///
/// Complements DecisionEngineClientTests.cs (Spec/20 client-shim tests
/// from pass 2). This file covers Plan/14 slot-level wiring contracts:
/// mode-aware activation (S10.6), trace plumbing (S10.7), and the
/// dynamic-progressive invariant (S10.8) across all seven advisors.
///
/// All tests are <see cref="SkipAttribute"/>-marked with the slot-pending
/// reason until Plan/14 slots land.
///
/// Assertion contract: tests assert against WoWActivitySnapshot.advice_log
/// (Spec/19 field 36) and trace JSONL files at tmp/test-runtime/traces/
/// per Spec/20 §6.1; never against DecisionEngineService internal state.
/// </summary>
public sealed class Phase10DecisionEngineContractTests
{
    private const string SlotPendingMode = "contract pending S10.6 (Plan/14)";
    private const string SlotPendingTrace = "contract pending S10.7 (Plan/14)";
    private const string SlotPendingLive = "contract pending S10.8 (Plan/14)";
    private const string SlotPendingObjectiveWire = "contract pending S10.2 (Plan/14)";
    private const string SlotPendingChatWire = "contract pending S10.9 (Plan/14)";
    private const string SlotPendingActivityWire = "contract pending S10.10 (Plan/14)";
    private const string SlotPendingPersonalityWire = "contract pending S10.11 (Plan/14)";

    [Fact(Skip = SlotPendingMode)]
    public void ModeAwareActivation_HotReloadFlipsRotationFromTrivialToRules()
    {
        // GIVEN: Config/decision-engine.json with advisors.rotation.mode="Trivial".
        // WHEN:  the config is hot-reloaded with advisors.rotation.mode="Rules"
        //        AND a Config/decision-engine/rotation-rules.json is present.
        // THEN:  next GetRotationAdviceAsync response carries
        //        mode_used=ADVISOR_MODE_RULES (Spec/20 §2.1) in the
        //        AdviceLogEntry projection. Tests pin Mode=Trivial elsewhere.
        Assert.Fail("S10.6 contract pending — see docs/Plan/14_PHASE10_DECISION_ENGINE_INTEGRATION.md");
    }

    [Fact(Skip = SlotPendingMode)]
    public void ModeAwareActivation_MissingRulesFile_FallsBackToTrivial()
    {
        // GIVEN: config sets advisors.objective.mode="Rules" but
        //        Config/decision-engine/objective-tie-rules.json is missing.
        // WHEN:  GetObjectiveAdviceAsync fires.
        // THEN:  service falls back to Phase-1 heuristic; the response
        //        carries mode_used=ADVISOR_MODE_TRIVIAL and a warning is
        //        logged. Recommended id is still valid (in tied set).
        Assert.Fail("S10.6 contract pending — see docs/Plan/14_PHASE10_DECISION_ENGINE_INTEGRATION.md");
    }

    [Fact(Skip = SlotPendingTrace)]
    public void TraceWriter_EveryAdviceRequestHasMatchingResponse()
    {
        // GIVEN: a LiveValidation test that calls all 7 advisors at least
        //        once each.
        // WHEN:  the test finishes and TraceWriter flushes
        //        tmp/test-runtime/traces/<test>/<ts>.jsonl.
        // THEN:  every "kind":"advice_request" line has a matching
        //        "kind":"advice_response" line with the same request_id.
        //        Spec/20 §6.2.
        Assert.Fail("S10.7 contract pending — see docs/Plan/14_PHASE10_DECISION_ENGINE_INTEGRATION.md");
    }

    [Fact(Skip = SlotPendingTrace)]
    public void TraceWriter_ObjectiveTransitionPrecededBySnapshot()
    {
        // GIVEN: same trace setup.
        // WHEN:  the trace is scanned for "kind":"objective_transition" lines.
        // THEN:  every such line is preceded by a "kind":"snapshot" line
        //        whose snapshot_delta.current_objective_id matches
        //        from_objective_id. Spec/20 §6.2.
        Assert.Fail("S10.7 contract pending — see docs/Plan/14_PHASE10_DECISION_ENGINE_INTEGRATION.md");
    }

    [Fact(Skip = SlotPendingObjectiveWire)]
    public void ObjectiveComposer_NoAdvice_FallsThroughToDeterministicTieBreak()
    {
        // GIVEN: composer with two tied Objectives "a-objective" and
        //        "b-objective" AND a DecisionEngine stub returning NoAdvice.
        // WHEN:  the composer's tie-break path runs.
        // THEN:  snapshot.current_objective_id == "a-objective"
        //        (lex sort tie-break) AND snapshot.advice_log entry's
        //        used_index is 0xFFFFFFFD or similar service-down sentinel.
        Assert.Fail("S10.2 contract pending — see docs/Plan/14_PHASE10_DECISION_ENGINE_INTEGRATION.md");
    }

    [Fact(Skip = SlotPendingObjectiveWire)]
    public void ObjectiveComposer_AdviceOutsideTieSet_IsIgnored()
    {
        // GIVEN: composer with tied set ["a-objective", "b-objective"]
        //        AND DecisionEngine stub returning ObjectiveAdvice with
        //        RecommendedObjectiveId="c-objective" (NOT in tied set).
        // WHEN:  the tie-break path runs.
        // THEN:  composer falls back to lex tie-break ("a-objective");
        //        advice_log entry's used_index == 0xFFFFFFFF (discarded).
        Assert.Fail("S10.2 contract pending — see docs/Plan/14_PHASE10_DECISION_ENGINE_INTEGRATION.md");
    }

    [Fact(Skip = SlotPendingChatWire)]
    public void ChatTemplateWire_AdvisorRespectsCandidateSet()
    {
        // GIVEN: IChatGenerator with 4 candidate templates AND DecisionEngine
        //        stub returning ChatTemplateAdvice with template id NOT in
        //        the candidate set.
        // WHEN:  GeneratePlanAsync runs.
        // THEN:  returned ChatPostPlan.TemplateId is one of the actual
        //        candidates; AdvisorRationale is empty.
        // (Equivalent to Spec/21 SocialFabricContractTests.ChatTemplate_*
        //  but tested at the wire-slot level here.)
        Assert.Fail("S10.9 contract pending — see docs/Plan/14_PHASE10_DECISION_ENGINE_INTEGRATION.md");
    }

    [Fact(Skip = SlotPendingActivityWire)]
    public void ActivityRequestWire_NoAdviceOnAmbiguousReturnsAmbiguousRequest()
    {
        // GIVEN: OnDemandActivitiesModeHandler receives a whisper "!run brd"
        //        with candidate set size 3 AND DecisionEngine returns NoAdvice
        //        AND Config/whisper-parser.json has NO clear static default
        //        for "brd".
        // WHEN:  OnExternalActivityRequestAsync handles it.
        // THEN:  snapshot.recent_ondemand_echoes[0].rejection_code ==
        //        AMBIGUOUS_REQUEST (Spec/23 §3 enum). suggested_alternatives
        //        contains all 3 BRD variants.
        Assert.Fail("S10.10 contract pending — see docs/Plan/14_PHASE10_DECISION_ENGINE_INTEGRATION.md");
    }

    [Fact(Skip = SlotPendingPersonalityWire)]
    public void PersonalityClusterWire_DeterminismHoldsAcrossAdvisorRuns()
    {
        // GIVEN: same accountName + same advisor output across two
        //        IPersonalityFactory.Create runs.
        // WHEN:  both calls complete.
        // THEN:  snapshot.personality.personality_hash is identical AND
        //        snapshot.personality.cluster_id is identical between
        //        the two runs. The advisor's input (accountName + bot
        //        context) is fixed; the output is therefore fixed too.
        Assert.Fail("S10.11 contract pending — see docs/Plan/14_PHASE10_DECISION_ENGINE_INTEGRATION.md");
    }

    [Fact(Skip = SlotPendingLive)]
    public void Phase10DecisionEngine_DynamicProgressive_AllAdvisorsForcedToNoAdviceTest()
    {
        // GIVEN: a directory of production-grade traces from any
        //        prior LiveValidation run AND a fresh test session that
        //        replays the same Activities with the seven advisors
        //        ALL pinned to Mode=Trivial AND the DecisionEngine
        //        service stubbed to return NoAdvice for every call.
        // WHEN:  the replay completes.
        // THEN:  every "kind":"outcome" JSONL line emitted has
        //        roster_distance_delta <= 0 (deterministic stack closes
        //        goal distance without ML).
        // AND:   wall_clock_ms per Activity is within 1.5x the original
        //        advisor-enabled run baseline (advice nudges, doesn't
        //        gate, performance).
        // See Spec/20 §10, Spec/19 §10, Plan/14 dynamic-progressive
        // section.
        Assert.Fail("S10.8 contract pending — see docs/Plan/14_PHASE10_DECISION_ENGINE_INTEGRATION.md");
    }
}
