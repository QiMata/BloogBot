using Xunit;

namespace BotRunner.Tests.Metrics;

/// <summary>
/// Spec 10 §Anomaly detection contract tests.
///
/// Anomaly detection is OBSERVATIONAL ML (not decision ML) — it watches
/// the metric stream + Spec/20 §6 trace pipeline and emits Anomaly events
/// on snapshot field 47. It does NOT extend the seven advisor RPCs from
/// Spec/20 §2; the Phase-3 ONNX model is service-internal to
/// DecisionEngineService.
///
/// All tests are <see cref="SkipAttribute"/>-marked until the
/// AnomalyDetector slot lands (Plan follow-up).
///
/// Assertion contract (CLAUDE.md Test Isolation Rules): tests assert
/// against WoWActivitySnapshot.active_anomalies[] (proto field 47) per
/// Spec/10 §Snapshot projection. Never against AnomalyDetector internal
/// state.
/// </summary>
public sealed class AnomalyDetectionContractTests
{
    private const string SlotPendingDetector = "contract pending AnomalyDetector slot (Plan follow-up)";
    private const string SlotPendingProjection = "contract pending S10.5 (Plan/14) + AnomalyDetector";

    [Fact(Skip = SlotPendingDetector)]
    public void AnomalyKindEnum_ContainsExpectedKindsAndIsClosedSet()
    {
        // GIVEN: the AnomalyKind enum from Spec/10 §AnomalyKind enum.
        // WHEN:  iterating values.
        // THEN:  values include Unknown=0 and 15 named kinds
        //        (XpRateBelowMedian, GoldRateBelowMedian, KillRateBelowMedian,
        //         DeathRateAboveMedian, StuckRateAboveMedian, PathTimeoutSpike,
        //         PhysicsParityBreakCluster, ActivityFailureCluster,
        //         AdvisorTimeoutSpike, AhListingsZeroGrowth,
        //         ChatBudgetExhaustionPattern, LoadoutStuckPattern,
        //         SnapshotIngestLatencyP99Spike, OndemandPoolExhaustionPattern,
        //         RosterDistanceRegression) — exactly 16 values total,
        //        no gaps in the proto encoding.
        Assert.Fail("AnomalyDetector slot pending — see docs/Spec/10_METRICS.md");
    }

    [Fact(Skip = SlotPendingProjection)]
    public void AnomalyDetector_StaticThresholdRulesFireOnSyntheticBadInput()
    {
        // GIVEN: Config/anomaly-thresholds.json declares
        //        XpRateBelowMedian.LevelBand_15.threshold = median * 0.25
        //        AND a synthetic metric stream where a level-15 bot's
        //        wwow.statemanager.bot.xp_delta_total falls to 20% of
        //        the level-band median.
        // WHEN:  the next snapshot tick fires.
        // THEN:  snapshot.active_anomalies contains exactly one entry
        //        with kind=XpRateBelowMedian, severity=Warning,
        //        subject=<account>, observed_value < expected_value,
        //        source=RuleEngine.
        Assert.Fail("AnomalyDetector slot pending — see docs/Spec/10_METRICS.md");
    }

    [Fact(Skip = SlotPendingProjection)]
    public void AnomalyDetector_LevelBandBucketing_NoCrossBandFalsePositive()
    {
        // GIVEN: AnomalyDetector configured with level-15 XP/hr threshold
        //        AND a level-50 bot whose XP/hr is at typical level-50
        //        rate (which is LOWER than the level-15 threshold).
        // WHEN:  detector runs.
        // THEN:  snapshot.active_anomalies contains NO XpRateBelowMedian
        //        entry for the level-50 bot. Bucketing by level_band
        //        prevents the false positive.
        Assert.Fail("AnomalyDetector slot pending — see docs/Spec/10_METRICS.md");
    }

    [Fact(Skip = SlotPendingProjection)]
    public void AnomalyDetector_HealthyTraceProducesNoRegressionAnomaliesTest()
    {
        // GIVEN: a production trace under tmp/test-runtime/traces/Healthy_*/
        //        where every "kind":"outcome" line has
        //        roster_distance_delta <= 0.
        // WHEN:  detector replays the trace.
        // THEN:  no AnomalyKind.RosterDistanceRegression events emitted;
        //        snapshot.active_anomalies stays empty across the run.
        // See Spec/10 §Live-validation guard.
        Assert.Fail("AnomalyDetector slot pending — see docs/Spec/10_METRICS.md");
    }

    [Fact(Skip = SlotPendingProjection)]
    public void AnomalyDetector_RegressionTraceTriggersRegressionAnomaly()
    {
        // GIVEN: an injected trace with one "kind":"outcome" line
        //        carrying roster_distance_delta = +0.05 (anti-progressive).
        // WHEN:  detector consumes the trace.
        // THEN:  snapshot.active_anomalies contains exactly one
        //        AnomalyKind.RosterDistanceRegression entry with
        //        observed_value ~ 0.05, expected_value <= 0,
        //        severity >= Warning.
        Assert.Fail("AnomalyDetector slot pending — see docs/Spec/10_METRICS.md");
    }

    [Fact(Skip = SlotPendingDetector)]
    public void AnomalyDetector_FailSoftOnConfigMissing()
    {
        // GIVEN: Config/anomaly-thresholds.json is absent.
        // WHEN:  detector runs against any input.
        // THEN:  no anomalies emitted; no exceptions; service continues.
        //        Silent fallback per Spec/10 §Fail-soft fallback (false
        //        negatives are preferred over false positives in the
        //        alerting domain).
        Assert.Fail("AnomalyDetector slot pending — see docs/Spec/10_METRICS.md");
    }

    [Fact(Skip = SlotPendingProjection)]
    public void Anomaly_DynamicProgressive_HealthyTraceClearMatchesRegressionTraceTriggersTest()
    {
        // GIVEN: two production-grade traces:
        //        - HealthyTrace: every outcome line has
        //          roster_distance_delta <= 0.
        //        - RegressionTrace: at least one outcome line has
        //          roster_distance_delta > 0.
        // WHEN:  the same AnomalyDetector configuration is applied to
        //        both traces sequentially.
        // THEN:  (dynamic) the two traces produce DIFFERENT anomaly
        //        outputs: zero RosterDistanceRegression events on
        //        Healthy; >= 1 on Regression.
        // AND:   (progressive) the detector is sensitive to the
        //        invariant violation specifically — it does NOT fire on
        //        metric noise that is unrelated to roster distance.
        // See Spec/10 §Dynamic-progressive invariant.
        Assert.Fail("AnomalyDetector slot pending — see docs/Spec/10_METRICS.md");
    }
}
