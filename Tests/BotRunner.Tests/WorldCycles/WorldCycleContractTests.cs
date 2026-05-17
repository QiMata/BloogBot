using Xunit;

namespace BotRunner.Tests.WorldCycles;

/// <summary>
/// Spec 22 contract tests for world-cycle services (lockouts, world-buff
/// windows, world-boss respawn, game events, daily / weekly reset).
///
/// All tests are <see cref="SkipAttribute"/>-marked until the matching
/// slot lands. Some services (GameEventProvider, WorldBossWatcher,
/// WorldBuffWindowTracker, CalendarResetService) have no Plan slot yet -
/// see Plan/SPEC_FILL_LOOP.md follow-ups.
///
/// Assertion contract (Spec/22 §9 + CLAUDE.md Test Isolation Rules):
/// tests assert against WoWActivitySnapshot.world_buff_windows[] (proto
/// field 40), current_lockouts[] (41), active_game_events[] (42),
/// current_activity_id, current_objective_id. Never against
/// LockoutVerifier / WorldBossWatcher internal state, never against
/// MaNGOS DB directly from a test.
/// </summary>
public sealed class WorldCycleContractTests
{
    private const string SlotPendingLockout = "contract pending S2.4 (Plan/03)";
    private const string SlotPendingCalendar = "contract pending CalendarResetService (no slot — Plan follow-up)";
    private const string SlotPendingBoss = "contract pending WorldBossWatcher (no slot — Plan follow-up)";
    private const string SlotPendingBuff = "contract pending WorldBuffWindowTracker (no slot — Plan follow-up)";
    private const string SlotPendingEvent = "contract pending GameEventProvider (no slot — Plan follow-up)";
    private const string SlotPendingAdvisor = "contract pending S10.6 (Plan/14) Mode=Rules";

    [Fact(Skip = SlotPendingLockout)]
    public void WeeklyReset_ClearsRaidLockouts()
    {
        // GIVEN: a bot snapshot with current_lockouts containing
        //        instance row referencing Molten Core (mapId=409).
        // WHEN:  simulated weekly reset fires (Tuesday 04:00 server-local
        //        on the Westworld-Test accelerated timer).
        // THEN:  snapshot.current_lockouts on the test bot is empty AND
        //        snapshot.current_activity_id transitions away from
        //        "econ.vendor-loop" to a raid catalog id like "raid.molten-core"
        //        within the composer's next tick.
        // See Spec/22 §8.
        Assert.Fail("S2.4 + CalendarResetService pending — see docs/Spec/22_WORLD_CYCLES.md §14.");
    }

    [Fact(Skip = SlotPendingBuff)]
    public void WorldBuffWindow_HoldsRaidQuorum()
    {
        // GIVEN: a candidate raid roster of 20 bots all attuned for ZG
        //        AND snapshot.world_buff_windows on each bot indicates
        //        the Onyxia/Nef/Rend slaughter cry is OPEN with
        //        expected_next_raid_at_ms 15 min in the future
        //        (within the 60-min decay budget).
        // WHEN:  RaidCompositionService evaluates quorum policy
        //        per Spec/22 §6 step 2a.
        // THEN:  quorum forms; raid starts.
        // AND:   the parallel case where world_buff_windows.opens_at_ms
        //        is 90 min in the future (outside decay budget) causes
        //        bots to pivot - snapshot.current_objective_id becomes
        //        one of {"pivot-to-dm-tribute", "pivot-to-songflower",
        //         "pivot-to-onyxia-rend-pickup"}.
        // See Spec/22 §3, §6, §11.
        Assert.Fail("WorldBuffWindowTracker pending — see docs/Spec/22_WORLD_CYCLES.md §14.");
    }

    [Fact(Skip = SlotPendingEvent)]
    public void GameEvent_EnablesEventActivity()
    {
        // GIVEN: STV Fishing Extravaganza (event_id=26) row is NOT in
        //        the MaNGOS game_event active set.
        // WHEN:  composer evaluates Activity eligibility for any bot.
        // THEN:  "event.stv-fishing-extravaganza" does NOT appear in
        //        the composed Objective sequence.
        // AND:   when the row transitions to active (snapshot.active_game_events
        //        contains event_id=26), the Activity becomes selectable
        //        for bots in Background/opportunistic priority band.
        Assert.Fail("GameEventProvider pending — see docs/Spec/22_WORLD_CYCLES.md §14.");
    }

    [Fact(Skip = SlotPendingBoss)]
    public void WorldBossWatcher_SpawnedEventTriggersCoordinatorQuorum()
    {
        // GIVEN: 20 bots attuned for Azuregos and idling in nearby zones.
        // WHEN:  the MaNGOS `creature` row for Azuregos transitions
        //        dead -> alive (entry=6109).
        // THEN:  within 30 s of the transition, all 20 bot snapshots
        //        show current_activity_id == "boss.azuregos" and the
        //        Coordinator-formed raid group is intact.
        Assert.Fail("WorldBossWatcher pending — see docs/Spec/22_WORLD_CYCLES.md §14.");
    }

    [Fact(Skip = SlotPendingAdvisor)]
    public void BuffPivot_AdvisorRespectsCandidateSet()
    {
        // GIVEN: a DecisionEngine stub returns ObjectiveAdvice with
        //        RecommendedObjectiveId="invalid-pivot" (not in the tied
        //        set) and Confidence=0.9.
        // WHEN:  RaidCompositionService consults the buff-window
        //        advisor at the quorum-formed-outside-buff-window step.
        // THEN:  the recommendation is discarded; the bot picks the
        //        Phase-1 heuristic (lowest travel cost * highest decay
        //        budget). snapshot.advice_log entry's used_index ==
        //        0xFFFFFFFF (discarded).
        // See Spec/22 §11 + Spec/20 §2.1.
        Assert.Fail("S10.6 pending — see docs/Spec/22_WORLD_CYCLES.md §11.");
    }

    [Fact(Skip = SlotPendingCalendar)]
    public void WorldCycle_DynamicProgressive_WeeklyResetTriggersRaidPickupTest()
    {
        // GIVEN: >=2 distinct synthetic snapshots that differ only in
        //        (class, attunement set, raid lockout history, currently
        //         active buffs).
        // WHEN:  simulated weekly reset fires and the composer re-runs.
        // THEN:  (dynamic) the post-reset chosen Activity differs across
        //        the two snapshots in either Activity id or parameters
        //        (e.g. one bot picks "raid.molten-core", another picks
        //        "raid.zul-gurub" or "social.city-ambient" if no raid is
        //        attuned).
        // AND:   (progressive) for each bot's chosen Activity, the
        //        post-completion RosterPlanner.Distance is strictly less
        //        than the pre-reset baseline; failed Activities are
        //        allowed but the *selection* must be progressive in
        //        expectation (highest expected delta first).
        // See Spec/22 §12, Spec/05 RosterPlanner, Spec/19 §10.
        Assert.Fail("CalendarResetService pending — see docs/Spec/22_WORLD_CYCLES.md §12.");
    }
}
