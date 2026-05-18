using Xunit;

namespace BotRunner.Tests.Activities;

/// <summary>
/// docs/Plan/Activities/00_INDEX.md §Test surface contract tests for the
/// per-row training-trace plan (§Training-trace contract, §Phase 9
/// expansion, §Dynamic-progressive invariant).
///
/// Complements the existing CatalogMarkdownDriftTests.cs (Plan/13 S9.8)
/// which enforces row-count + invariant drift between the index and the
/// compiled IActivityCatalog. This file covers the training-trace
/// contract layer on top: which advisors each family triggers, and
/// whether every row's representative-suite traces close roster
/// distance at least once.
///
/// All tests are <see cref="SkipAttribute"/>-marked until the
/// WoWStateManagerUIFixture (test host, 9th orphan per pass 16) +
/// TraceWriter (Plan/14 S10.7) land, plus the LiveValidation suite
/// that produces representative-run trace coverage.
///
/// Assertion contract (CLAUDE.md Test Isolation Rules): tests assert
/// against on-disk markdown structure (this index file) and trace
/// JSONL files. Never against composer / IActivityCatalog internal
/// state.
/// </summary>
public sealed class IndexTrainingTracePlanContractTests
{
    private const string SlotPendingMarkdown = "contract pending Plan/13 S9.8 + index trace-contract section";
    private const string SlotPendingTrace = "contract pending S10.7 (Plan/14) + WoWStateManagerUIFixture (pass 16 orphan)";

    [Fact(Skip = SlotPendingMarkdown)]
    public void IndexTrainingTracePlan_EveryFamilyAppearsInTraceContractTable()
    {
        // GIVEN: docs/Plan/Activities/00_INDEX.md parsed with the regex
        //        for top-level "^## " headings AND the
        //        §Training-trace contract table's "Family" column
        //        extracted.
        // WHEN:  comparing the two sets.
        // THEN:  every family heading from the row-listing sections
        //        (Starter questing, Zone questing, Dungeons, Raids,
        //        Battlegrounds, Profession farming, Profession crafting,
        //        Economy, Reputations, Attunements, World events,
        //        World bosses, plus Plan/13 forward-referenced Social,
        //        Escorts, Holiday events) appears as a row in the
        //        §Training-trace contract table. New families added
        //        to the catalog without a corresponding contract row
        //        fail this test.
        Assert.Fail("Plan/13 S9.8 + trace-contract section pending — see docs/Plan/Activities/00_INDEX.md §Test surface.");
    }

    [Fact(Skip = SlotPendingMarkdown)]
    public void IndexTrainingTracePlan_AdvisorsListedAreSubsetOfSpec20Surface()
    {
        // GIVEN: the §Training-trace contract table parsed for advisor
        //        names in each row's "Spec/20 advisors triggered" column.
        // WHEN:  each token is checked against the allowed advisor set.
        // THEN:  every token is in
        //          { "rotation", "threat", "reward", "objective",
        //            "chat_template", "activity_request",
        //            "personality_cluster", "cheapest-source-learner" }
        //        (where "cheapest-source-learner" is the §aota/05-defined
        //        alias for "objective" over source candidates).
        //        No advisor name typos.
        Assert.Fail("Plan/13 S9.8 + trace-contract section pending — see docs/Plan/Activities/00_INDEX.md §Test surface.");
    }

    [Fact(Skip = SlotPendingTrace)]
    public void IndexTrainingTracePlan_EveryRowProducesOutcomeLineAcrossSample()
    {
        // GIVEN: the IActivityCatalog (compiled C# static class) AND the
        //        union of tmp/test-runtime/traces/*/*.jsonl files from
        //        the most recent representative-suite LiveValidation
        //        run.
        // WHEN:  enumerating every catalog ActivityDefinition.Id.
        // THEN:  for EVERY id, at least one JSONL file under
        //        tmp/test-runtime/traces/ contains at least one line
        //        with kind="outcome" AND activity_id == that id.
        //        Rows that never produce an outcome line in the
        //        representative suite indicate a LiveValidation
        //        coverage gap and must either be tested or marked as
        //        gated.
        Assert.Fail("Trace pipeline + LiveValidation coverage pending — see docs/Plan/Activities/00_INDEX.md §Test surface.");
    }

    [Fact(Skip = SlotPendingTrace)]
    public void Activities00Index_DynamicProgressive_NonPositiveRosterDeltaPerRowTest()
    {
        // GIVEN: for every catalog row id, the union of "kind":"outcome"
        //        lines referencing that activity_id across all
        //        tmp/test-runtime/traces/*/*.jsonl files from the most
        //        recent representative-suite run.
        // WHEN:  enumerating those outcomes.
        // THEN:  for every row id:
        //          (a) EVERY outcome line with completion="complete" has
        //              roster_distance_delta <= 0 (the per-completion
        //              invariant — no anti-progressive completions);
        //          (b) AT LEAST ONE outcome line has
        //              roster_distance_delta < 0 (strictly negative —
        //              proof that the row closes goal distance at
        //              least sometimes; not just cosmetic 0-deltas).
        //        Rows that fail (b) — every outcome is 0-delta — are
        //        flagged as DECORATION and must either be justified in
        //        their family file with an explicit Rationale block OR
        //        the Activity's Objective sequence must be adjusted to
        //        close at least one RosterPlanner.Distance axis on
        //        completion.
        // See docs/Plan/Activities/00_INDEX.md §Dynamic-progressive
        //   invariant + Spec/05 §RosterPlanner.Distance.
        Assert.Fail("Representative-suite trace coverage pending — see docs/Plan/Activities/00_INDEX.md §Test surface.");
    }
}
