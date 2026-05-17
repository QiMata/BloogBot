using Xunit;

namespace BotRunner.Tests.Social;

/// <summary>
/// Spec 21 contract tests for social-fabric surface.
///
/// All tests are <see cref="SkipAttribute"/>-marked with the slot-pending
/// reason until Phase 11 slots S11.x (Plan/15) land. The chat-template
/// advisor extension also requires Spec/20 §2.2 and Plan/14 S10.6 to flip
/// the chat_template mode away from Trivial.
///
/// Assertion contract (Spec/21 §9 + CLAUDE.md Test Isolation Rules):
/// tests assert against WoWActivitySnapshot.chat_post_budgets[] (proto
/// field 38) and pending_mail_count (field 39); never against
/// PostBudgetTracker or NetworkChatFrame internal state.
/// </summary>
public sealed class SocialFabricContractTests
{
    private const string SlotPendingGenerator = "contract pending S11.1 (Plan/15)";
    private const string SlotPendingBudget = "contract pending S11.11 (Plan/15)";
    private const string SlotPendingMail = "contract pending S11.4 (Plan/15)";
    private const string SlotPendingGuild = "contract pending S11.7 (Plan/15)";
    private const string SlotPendingWhisper = "contract pending S11.10 (Plan/15)";
    private const string SlotPendingCity = "contract pending S11.8 (Plan/15)";
    private const string SlotPendingComposer = "contract pending S11.5 (Plan/15)";
    private const string SlotPendingDenylist = "contract pending S11.12 (Plan/15)";
    private const string SlotPendingLiveValidation = "contract pending S11.13 (Plan/15)";

    [Fact(Skip = SlotPendingBudget)]
    public void TradeChat_RespectsPerBotHourlyBudget()
    {
        // GIVEN: a bot assigned to "social.trade-chat-cycle" with
        //        ChattyLevel=Normal (Spec/24 personality knob).
        // WHEN:  the bot runs for a simulated rolling hour.
        // THEN:  snapshot.chat_post_budgets[Trade].posts_in_rolling_hour
        //        is <= 4 at every snapshot tick, and hourly_cap == 4.
        // See Spec/21 §3.2 and §9.
        Assert.Fail("S11.11 contract pending — see docs/Spec/21_SOCIAL_FABRIC.md §14.");
    }

    [Fact(Skip = SlotPendingMail)]
    public void MailFlow_AlternatesBetweenAccounts()
    {
        // GIVEN: a bot with an alt configured in CharacterRosterGoal whose
        //        bag inventory is over the overflow threshold.
        // WHEN:  MailSendTask runs as part of econ.vendor-loop mail-stage.
        // THEN:  snapshot.pending_mail_count on the alt monotonically
        //        increases by the number of items sent; the originating
        //        bot's bag count decreases by the same amount.
        Assert.Fail("S11.4 contract pending — see docs/Spec/21_SOCIAL_FABRIC.md §14.");
    }

    [Fact(Skip = SlotPendingGuild)]
    public void GuildDinged_PostsOncePerLevel()
    {
        // GIVEN: a bot with a guild configured and ChattyLevel != Quiet.
        // WHEN:  the bot levels up.
        // THEN:  exactly one guild-ding post fires; snapshot.chat_post_budgets[Guild].
        //        posts_in_rolling_hour increases by exactly 1, not once
        //        per snapshot tick.
        Assert.Fail("S11.7 contract pending — see docs/Spec/21_SOCIAL_FABRIC.md §14.");
    }

    [Fact(Skip = SlotPendingWhisper)]
    public void WhisperReply_RespondsWithinSla()
    {
        // GIVEN: a bot receives a friendly whisper (same faction, in
        //        friend list) at t=0.
        // WHEN:  WhisperReplyHandler picks up the SMSG_MESSAGECHAT event.
        // THEN:  snapshot.recent_chat_messages records a reply within
        //        30 s of receipt (5-30 s SLA, jittered by PersonalityProfile.
        //        WhisperReplyDelayMs).
        // AND:   stranger whisper variant asserts the 10-60 s SLA.
        Assert.Fail("S11.10 contract pending — see docs/Spec/21_SOCIAL_FABRIC.md §14.");
    }

    [Fact(Skip = SlotPendingCity)]
    public void CityAmbient_AdvancesObjectivesWithoutInterleaving()
    {
        // GIVEN: a bot assigned to "social.city-ambient" idling in
        //        Orgrimmar.
        // WHEN:  the bot ticks for 10 minutes of simulated time.
        // THEN:  snapshot.current_objective_id transitions through the
        //        6 ambient Objectives (mailbox -> vendor -> bank -> AH ->
        //        inn -> trainer) in order; no Objective fires twice
        //        before the loop completes.
        Assert.Fail("S11.8 contract pending — see docs/Spec/21_SOCIAL_FABRIC.md §14.");
    }

    [Fact(Skip = SlotPendingComposer)]
    public void ChatTemplate_AdvisorRespectsCandidateSet()
    {
        // GIVEN: an IChatGenerator wired to a DecisionEngine stub that
        //        returns ChatTemplateAdvice { RecommendedTemplateId="invalid.txt",
        //        Confidence=0.9 }.
        // WHEN:  GeneratePlanAsync runs with 4 candidates that do NOT
        //        include "invalid.txt".
        // THEN:  the returned ChatPostPlan.TemplateId is one of the
        //        actual candidate ids (Phase-1 heuristic fallback path).
        //        AdvisorRationale is empty and AdvisorConfidence == 0.
        // See Spec/21 §3.3 and Spec/20 §2.2.
        Assert.Fail("S11.5 contract pending — see docs/Spec/21_SOCIAL_FABRIC.md §14.");
    }

    [Fact(Skip = SlotPendingDenylist)]
    public void ChatTemplate_DenylistTripFallsBackToTemplate()
    {
        // GIVEN: a PromptHandlingService completion is requested AND its
        //        output trips a regex in Bot/chat-templates/_denylist.txt.
        // WHEN:  ChatPostFilter inspects the resolved text.
        // THEN:  the post is dropped, the bot emits a deterministic
        //        template-library entry instead, and the trip is logged
        //        with FailureReason.chat_denylist_rejection (Spec/12
        //        addition pending).
        Assert.Fail("S11.12 contract pending — see docs/Spec/21_SOCIAL_FABRIC.md §14.");
    }

    [Fact(Skip = SlotPendingLiveValidation)]
    public void SocialFabric_DynamicProgressive_TradeChatBuyersConvertTest()
    {
        // GIVEN: a directory of production-grade live-validation traces
        //        under tmp/test-runtime/traces/.
        // WHEN:  every "kind":"outcome" JSONL line referencing a wts
        //        chat-post is enumerated.
        // THEN:  >= 10% of those outcomes show a buyer-whisper-then-trade
        //        completion (the post produced a real economic event).
        //        Cosmetic chat alone fails this test.
        // See Spec/21 §12 (Dynamic-progressive invariant) and Spec/20 §6.1.
        Assert.Fail("S11.13 contract pending — see docs/Spec/21_SOCIAL_FABRIC.md §12.");
    }
}
