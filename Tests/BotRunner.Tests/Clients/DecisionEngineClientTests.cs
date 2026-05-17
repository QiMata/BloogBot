using Xunit;

namespace BotRunner.Tests.Clients;

/// <summary>
/// Spec 20 contract tests for IDecisionEngineClient + DecisionEngineService
/// wire shape.
///
/// All tests are <see cref="SkipAttribute"/>-marked with the slot-pending
/// reason until Phase 10 slot S10.0 lands the
/// <c>Exports/BotRunner/Clients/DecisionEngineClient.cs</c> shim and the
/// <c>decision-engine.proto</c> wire shape.
///
/// Assertion contract (Spec/20 §9 + CLAUDE.md Test Isolation Rules):
/// tests assert against client-returned <c>NoAdvice</c> shape and the bot's
/// <c>WoWActivitySnapshot.advice_log[]</c> (Spec/19 proto field 36); never
/// against private DecisionEngineService state.
/// </summary>
public sealed class DecisionEngineClientTests
{
    private const string SlotPendingTransport = "contract pending S10.0 (Plan/14)";
    private const string SlotPendingProjection = "contract pending S10.5 (Plan/14)";
    private const string SlotPendingTrace = "contract pending S10.7 (Plan/14)";

    [Fact(Skip = SlotPendingTransport)]
    public void NoAdvice_OnTimeout()
    {
        // GIVEN: a DecisionEngineClient with timeoutMs=50 wired to a stub
        //        service that sleeps 250 ms before responding.
        // WHEN:  GetRotationAdviceAsync(...) is invoked.
        // THEN:  the returned RotationAdvice has RecommendedSpellId=null
        //        AND the AdviceError surfaced via the snapshot AdviceLog
        //        entry equals AdviceError.Timeout (used_index=0xFFFFFFFE).
        // See Spec/20 §2 table and §9.
        Assert.Fail("S10.0 contract pending — see docs/Spec/20_DECISION_ENGINE.md §9.");
    }

    [Fact(Skip = SlotPendingTransport)]
    public void NoAdvice_OnServiceDown()
    {
        // GIVEN: a DecisionEngineClient pointed at a port with no listener.
        // WHEN:  GetRewardAdviceAsync(...) is invoked.
        // THEN:  the call returns within timeoutMs (50ms default) and
        //        the AdviceLog entry's used_index=0xFFFFFFFD
        //        (AdviceError.ServiceDown).
        Assert.Fail("S10.0 contract pending — see docs/Spec/20_DECISION_ENGINE.md §9.");
    }

    [Fact(Skip = SlotPendingTransport)]
    public void NoAdvice_OnLowConfidence()
    {
        // GIVEN: a DecisionEngineClient wired to a stub service that
        //        returns ObjectiveAdvice{ confidence=0.3 }.
        // WHEN:  GetObjectiveAdviceAsync(...) is invoked.
        // THEN:  the returned ObjectiveAdvice is treated as NoAdvice by
        //        the client; the AdviceLog used_index=0xFFFFFFFA
        //        (AdviceError.LowConfidence).
        // Note: the wire actually carries the advice, but the client-side
        // confidence floor (default 0.5) rejects it before the caller sees it.
        Assert.Fail("S10.0 contract pending — see docs/Spec/20_DECISION_ENGINE.md §9.");
    }

    [Fact(Skip = SlotPendingProjection)]
    public void AdviceAppearsInSnapshotAdviceLog()
    {
        // GIVEN: a DecisionEngineClient wired to a stub service that
        //        returns RewardAdvice{ choice_index=2, confidence=0.9,
        //        rationale="set-bonus prefers chest" }.
        // WHEN:  a snapshot is produced after the bot consults reward
        //        advice on a 4-choice quest reward.
        // THEN:  snapshot.advice_log contains one entry with
        //        advisor="reward", confidence~=0.9, used_index=2,
        //        rationale starting "set-bonus".
        // See Spec/19 §5 (proto field 36) and Spec/20 §2.1 (oneof body).
        Assert.Fail("S10.5 contract pending — see docs/Spec/20_DECISION_ENGINE.md §9.");
    }

    [Fact(Skip = SlotPendingTrace)]
    public void DecisionEngine_DynamicProgressive_RosterDistanceDeltaIsNonPositiveTest()
    {
        // GIVEN: a directory of production-grade live-validation traces
        //        under tmp/test-runtime/traces/.
        // WHEN:  TraceFileContractTests scans every .jsonl file and finds
        //        every "kind":"outcome" line.
        // THEN:  for each outcome where completion="complete",
        //        roster_distance_delta MUST be <= 0.0.
        //        (Per Spec/20 §10, a complete Activity strictly closes
        //         distance to the CharacterRosterGoal — Spec/05.)
        // AND:   the same suite re-run with all advisors forced to
        //        AdvisorMode.Trivial (NoAdvice for every call) MUST
        //        still produce roster_distance_delta <= 0.0 outcomes
        //        for the same Activities. This is the correctness guard
        //        proving ML cannot break the deterministic-progressive
        //        stack.
        // See Spec/20 §10 and Spec/19 §10.
        Assert.Fail("S10.7 contract pending — see docs/Spec/20_DECISION_ENGINE.md §10.");
    }
}
