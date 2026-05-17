using Xunit;

namespace BotRunner.Tests.Activities;

/// <summary>
/// Plan 13 (Phase 9) catalog-fill contract tests.
///
/// All tests are <see cref="SkipAttribute"/>-marked with the slot-pending
/// reason until Plan/13 slots land. Tests assert on the compiled
/// IActivityCatalog row set, the Plan/Activities/00_INDEX.md drift
/// invariant, and the trace-surface dynamic-progressive invariant.
///
/// Assertion contract (CLAUDE.md Test Isolation Rules): tests assert
/// against IActivityCatalog row presence and WoWActivitySnapshot
/// state, never against internal LegalityValidator or composer state.
/// </summary>
public sealed class Phase9CatalogContractTests
{
    private const string SlotPendingSm = "contract pending S9.1 (Plan/13)";
    private const string SlotPendingStockades = "contract pending S9.2 (Plan/13)";
    private const string SlotPendingDungeonQuests = "contract pending S9.3 (Plan/13)";
    private const string SlotPendingEscort = "contract pending S9.4 (Plan/13)";
    private const string SlotPendingHoliday = "contract pending S9.5 (Plan/13)";
    private const string SlotPendingSocial = "contract pending S9.6 (Plan/13)";
    private const string SlotPendingWpvp = "contract pending S9.7 (Plan/13)";
    private const string SlotPendingTestsUpdate = "contract pending S9.8 (Plan/13)";
    private const string SlotPendingLiveValidation = "contract pending S9.9 (Plan/13)";

    [Fact(Skip = SlotPendingSm)]
    public void Catalog_ContainsAllFourScarletMonasteryWings()
    {
        // GIVEN: compiled IActivityCatalog after Plan/13 S9.1 lands.
        // WHEN:  enumerated for "dungeon.sm-*" rows.
        // THEN:  exactly 4 rows present: -graveyard, -library, -armory,
        //        -cathedral. Each has Family=Dungeon, MapId=189, distinct
        //        EntryCoord, distinct Boss list.
        Assert.Fail("S9.1 contract pending — see docs/Plan/13_PHASE9_CATALOG_FILL.md");
    }

    [Fact(Skip = SlotPendingStockades)]
    public void Catalog_ContainsStockadesRow()
    {
        // GIVEN: compiled IActivityCatalog after S9.2.
        // WHEN:  queried for "dungeon.stockades".
        // THEN:  row present with MapId=34, LevelRange=[22,30],
        //        MinPlayers=5, FactionPolicy=AllianceFirstHordeFallback.
        Assert.Fail("S9.2 contract pending — see docs/Plan/13_PHASE9_CATALOG_FILL.md");
    }

    [Fact(Skip = SlotPendingDungeonQuests)]
    public void Catalog_DungeonQuestSubActivities_HaveUniquePickupTurninPairs()
    {
        // GIVEN: compiled IActivityCatalog after S9.3.
        // WHEN:  enumerated for Family=DungeonQuest rows.
        // THEN:  >=3 rows present (BRD/Gnomeregan/LBRS minimum); each
        //        has unique (PickupNpcEntry, TurninNpcEntry); each
        //        references a real quest_template row available via
        //        IMangosCatalog.
        Assert.Fail("S9.3 contract pending — see docs/Plan/13_PHASE9_CATALOG_FILL.md");
    }

    [Fact(Skip = SlotPendingEscort)]
    public void Catalog_EscortFamily_HasMinimumTenRows()
    {
        // GIVEN: compiled IActivityCatalog after S9.4.
        // WHEN:  enumerated for Family=Escort rows.
        // THEN:  >=10 rows present; each has TaskFamily="Escort";
        //        LockoutPolicy=None.
        Assert.Fail("S9.4 contract pending — see docs/Plan/13_PHASE9_CATALOG_FILL.md");
    }

    [Fact(Skip = SlotPendingHoliday)]
    public void Catalog_HolidayEvents_ReferenceValidGameEventRows()
    {
        // GIVEN: compiled IActivityCatalog after S9.5.
        // WHEN:  enumerated for Family=WorldEvent rows.
        // THEN:  every row's EligibilityGate references a non-zero
        //        event_id that exists in the MaNGOS game_event table
        //        OR the row is marked gated=true.
        Assert.Fail("S9.5 contract pending — see docs/Plan/13_PHASE9_CATALOG_FILL.md");
    }

    [Fact(Skip = SlotPendingSocial)]
    public void Catalog_SocialServiceRows_HaveSocialPrefix()
    {
        // GIVEN: compiled IActivityCatalog after S9.6.
        // WHEN:  enumerated for Family=Social rows.
        // THEN:  every row id starts with "social." (R-Social invariant
        //        from S9.8); >=6 rows present.
        Assert.Fail("S9.6 contract pending — see docs/Plan/13_PHASE9_CATALOG_FILL.md");
    }

    [Fact(Skip = SlotPendingWpvp)]
    public void Catalog_WorldPvpRows_GatedByServerCapability()
    {
        // GIVEN: ServerCapabilities.WorldPvp = false.
        // WHEN:  OnDemandActivityLauncher receives RequestActivity for
        //        "wpvp.epl-graveyards".
        // THEN:  response carries RejectionCode.SERVER_DISABLED.
        // AND:   when WorldPvp = true, the LegalityValidator returns Ok.
        Assert.Fail("S9.7 contract pending — see docs/Plan/13_PHASE9_CATALOG_FILL.md");
    }

    [Fact(Skip = SlotPendingTestsUpdate)]
    public void CatalogMarkdownDrift_AcceptsExpandedRowCount()
    {
        // GIVEN: catalog with 86 + Phase 9 expansion (target 130-180 rows).
        // WHEN:  CatalogMarkdownDriftTests runs.
        // THEN:  the test passes; the expected-row-count range is
        //        [130, 180] and the actual count falls within.
        // AND:   R-Social and R-WorldEvent invariants pass on the
        //        full catalog.
        Assert.Fail("S9.8 contract pending — see docs/Plan/13_PHASE9_CATALOG_FILL.md");
    }

    [Fact(Skip = SlotPendingLiveValidation)]
    public void Phase9CatalogFill_DynamicProgressive_ExpandedCatalogClosesGoalDistanceTest()
    {
        // GIVEN: the expanded Phase 9 catalog (>= 130 rows) AND >=2
        //        synthetic bot contexts that differ in (class, level,
        //        faction, zone, attunements) such that pre-Phase-9 the
        //        composer collapsed them onto the SAME Activity due to
        //        catalog limits.
        // WHEN:  IActivityComposer.Compose(...) runs against both
        //        contexts with the expanded catalog.
        // THEN:  (dynamic) at least one of the two contexts now resolves
        //        to a DIFFERENT Activity (the catalog expansion creates
        //        a previously-unavailable row that fits one context
        //        better).
        // AND:   (progressive) for each new row added by S9.1-S9.7, at
        //        least one trace under tmp/test-runtime/traces/ shows an
        //        outcome line with roster_distance_delta < 0 referencing
        //        that row's activity_id. Rows with NO such trace are
        //        flagged as decoration not gameplay and must be
        //        justified in Plan/Activities/<family>.md.
        // See Plan/13 Dynamic-progressive invariant section.
        Assert.Fail("S9.9 contract pending — see docs/Plan/13_PHASE9_CATALOG_FILL.md");
    }
}
