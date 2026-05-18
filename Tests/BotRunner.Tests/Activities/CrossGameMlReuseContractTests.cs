using Xunit;

namespace BotRunner.Tests.Activities;

/// <summary>
/// architecture/aota/07_PORTABILITY.md §11 contract tests for cross-game
/// ML reuse (§7 portability matrix + §8 worked example + §9 invariant).
///
/// These tests assert the PORTABLE properties of the ML surface — the
/// interface shapes, JSONL schema, and 8-axis distance vector that
/// must survive a game port. Per-game trained model files are out of
/// scope for these tests (each game's own test suite handles its
/// trained models).
///
/// All tests are <see cref="SkipAttribute"/>-marked until the
/// CrossGameModelWarmStart slot lands (Plan follow-up — 15th orphan
/// service) plus Plan/14 S10.0 (IDecisionEngineClient interface) and
/// S10.7 (trace JSONL schema).
///
/// Assertion contract (CLAUDE.md Test Isolation Rules): tests assert
/// against on-disk JSONL files and interface reflection across
/// assemblies. Never against advisor internal state.
/// </summary>
public sealed class CrossGameMlReuseContractTests
{
    private const string SlotPendingShim = "contract pending S10.0 (Plan/14) + FFXI port";
    private const string SlotPendingTrace = "contract pending S10.7 (Plan/14) + FFXI port";
    private const string SlotPendingWarmStart = "contract pending CrossGameModelWarmStart slot (Plan follow-up)";

    [Fact(Skip = SlotPendingShim)]
    public void CrossGameMl_ClientInterfaceShapeIsLiteralCopy()
    {
        // GIVEN: the WWoW IDecisionEngineClient interface from
        //        Exports/BotRunner/Clients/DecisionEngineClient.cs AND
        //        the FFXI port at
        //        Final Fantasy XI/FFXIBot/Exports/BotRunner/Clients/
        //          DecisionEngineClient.cs.
        // WHEN:  reflection enumerates both interfaces' methods.
        // THEN:  both expose the same 7 method names (Get*AdviceAsync
        //        for Rotation/Threat/Reward/Objective/ChatTemplate/
        //        ActivityRequest/PersonalityCluster); the method
        //        parameter types differ only in per-game proto
        //        namespace prefixes (wwow.DecisionEngine.RotationContext
        //        vs ffxi.DecisionEngine.RotationContext); all 7
        //        signatures align modulo namespace.
        Assert.Fail("S10.0 + FFXI port pending — see docs/architecture/aota/07_PORTABILITY.md §11.");
    }

    [Fact(Skip = SlotPendingTrace)]
    public void CrossGameMl_TraceJsonlSchemaIsGameAgnostic()
    {
        // GIVEN: a sample outcome.jsonl line from a WWoW LiveValidation
        //        trace AND a sample outcome.jsonl line from an FFXI
        //        LiveValidation trace.
        // WHEN:  both are validated against the Spec/20 §6.1 schema
        //        (the same JSON Schema file applies to both).
        // THEN:  both validate successfully. Required fields match:
        //          ts, kind, request_id, bot_account, snapshot_seq,
        //          (kind="outcome"): activity_id, completion,
        //          wall_clock_ms, xp_gained, gear_slots_filled,
        //          gold_delta_copper, roster_distance_delta.
        //        Per-game fields are populated; cross-game fields
        //        (roster_distance_delta) carry the same semantics.
        Assert.Fail("S10.7 + FFXI port pending — see docs/architecture/aota/07_PORTABILITY.md §11.");
    }

    [Fact(Skip = SlotPendingTrace)]
    public void CrossGameMl_RosterDistanceEightAxisSurvivesPort()
    {
        // GIVEN: the WWoW RosterPlanner.Distance(snapshot, goal) function
        //        AND a synthetic FFXI-shaped snapshot mapped per the §8
        //        step 2 axis table (Level / GearTier-relic / Attunement-
        //        Step-keyItemsAndMissions / ReputationTier-fameTiers /
        //        GoldTargetPct-gilTargetPct / MountTier-chocoboLicense /
        //        PvPRank-ballistaRank / ProfessionSkill-craftingSkillMax).
        // WHEN:  RosterPlanner.Distance is called.
        // THEN:  returns a structurally-valid RosterPlannerDistance
        //        record with 8 PerAxis entries (one per DistanceAxis
        //        enum value); sum_axis(DefaultWeights[axis] *
        //        PerAxis[axis]) equals TotalScalar within 1e-5.
        //        The function shape ports verbatim; the AXIS MAPPING is
        //        per-game but the FUNCTION SIGNATURE is identical.
        Assert.Fail("RosterPlanner + FFXI port pending — see docs/architecture/aota/07_PORTABILITY.md §8.");
    }

    [Fact(Skip = SlotPendingWarmStart)]
    public void CrossGameMl_WarmStartGuard_OutOfDomainModelDoesNotMakeAntiProgressivePicks()
    {
        // GIVEN: WWoW's trained Models/objective/v1.onnx model loaded
        //        into an FFXI DecisionEngineService test fixture, with
        //        advisors.objective.confidence_threshold = 0.7 (higher
        //        than the default 0.5 because the model is out-of-domain).
        // WHEN:  a synthetic FFXI trace is replayed through the fixture.
        // THEN:  for every "kind":"outcome" line emitted,
        //        roster_distance_delta is <= 0. The confidence floor
        //        rejects low-confidence WWoW-model picks; the §3
        //        deterministic fallback handles the rest. Out-of-domain
        //        ML inference CANNOT make anti-progressive picks reach
        //        the actual Activity.
        // See aota/07 §8 step 5 + §9 cross-game invariant.
        Assert.Fail("CrossGameModelWarmStart slot pending — see docs/architecture/aota/07_PORTABILITY.md §8.");
    }

    [Fact(Skip = SlotPendingWarmStart)]
    public void AotaPortability_DynamicProgressive_InvariantSurvivesCrossGamePortTest()
    {
        // GIVEN: two synthetic snapshots applied to BOTH WWoW and FFXI
        //        fixtures simultaneously:
        //          SnapshotA: AttunementStep axis dominant
        //          SnapshotB: Level axis dominant
        //        AND each game's advisors.objective.mode="Ml" with the
        //        appropriate trained model (WWoW's v1.onnx for WWoW;
        //        the warm-started + retrained v2.onnx for FFXI).
        // WHEN:  the composer + ObjectiveTieBreaker runs for each
        //        (game, snapshot) pair.
        // THEN:  (dynamic) within each game, SnapshotA and SnapshotB
        //        produce DIFFERENT first-Activity picks per the
        //        per-game roster_goal_distance axis-dominance feature.
        // AND:   (progressive) for every (game, snapshot) pair, the
        //        resulting outcome.roster_distance_delta is <= 0 in
        //        the produced trace JSONL.
        // AND:   the SAME dynamic-progressive invariant assertion code
        //        runs against BOTH games' traces without modification
        //        — proof that the invariant ports verbatim.
        // See aota/07 §9 and §11.
        Assert.Fail("CrossGameModelWarmStart slot pending — see docs/architecture/aota/07_PORTABILITY.md §9.");
    }
}
