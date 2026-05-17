# Spec 21 — Social fabric

> **What this spec is.** The contract for the social-system surface that
> makes the WWoW server *feel alive* at the protocol level beyond
> combat / movement / quest progression. The acceptance criteria in
> [`Spec/00_VISION.md`](00_VISION.md) calls out "trade chat carries
> trade offers" and "mail moves between characters." This spec defines
> what those bots actually emit and how.
>
> Implementation slots: [`Plan/15_PHASE11_SOCIAL_FABRIC.md`](../Plan/15_PHASE11_SOCIAL_FABRIC.md).

## 1. Scope

In: chat channels (Trade, General, LFG, /1 World, Guild), whispers,
mail, guild events, group / party invites, AH posting *chatter* (the
short "WTS Stonescale Eel x20 100g PST" trade-chat posts that
accompany an AH listing), the city-services traffic that makes
capital cities feel populated (vendors, bankers, repair, mailbox).

Out: combat chat (handled by combat tasks via existing
`SAY_RAID_TARGET_*` patterns), in-game emote-cycling
(non-functional), Discord / out-of-game integrations.

## 2. Activity rows backed by this spec

| Catalog id | Family | Notes |
|---|---|---|
| `econ.ah-restock` | Economy | Adds Trade-chat post when a high-value item lists. |
| `econ.vendor-loop` | Economy | Mail retrieve + send at every city visit. |
| `social.trade-chat-cycle` *(new)* | Social | Posts WTB / WTS / LF1M based on the bot's roster goal. |
| `social.guild-events` *(new)* | Social | Guild invites, MOTD reads, guild-bank deposits if a guild is configured. |
| `social.lfg-cycle` *(new)* | Social | Posts to LFG channel when seeking a dungeon group; subscribes to LFG channel. |
| `social.city-ambient` *(new)* | Social | Idle wander between vendor / bank / mailbox / AH / inn while waiting for the next progression Objective. |

The three new social rows are added by
[`Plan/13_PHASE9_CATALOG_FILL.md`](../Plan/13_PHASE9_CATALOG_FILL.md).

## 3. Chat output

### 3.1 Channels in scope (1.12.1)

| Channel | WoW name | Default subscription |
|---|---|---|
| 1 — General | `General - <Zone>` | auto (every zone enter) |
| 2 — Trade | `Trade - City` | manual join in major cities |
| 3 — LocalDefense | `LocalDefense - <Zone>` | auto |
| 4 — WorldDefense | `WorldDefense` | manual |
| 5 — GuildRecruitment | `GuildRecruitment - City` | manual |
| 6 — LookingForGroup | `LookingForGroup` | manual |

Bots join channels 1, 2 (in cities), 3, 6 by default. World and Guild
recruitment channels are opt-in per bot per
[`Spec/24_BEHAVIORAL_VARIATION.md`](24_BEHAVIORAL_VARIATION.md)
personality knob.

### 3.2 Message rate budget

To avoid trade-chat flood — and to match human pacing — each bot
respects a per-channel posting budget:

| Channel | Max posts per bot per hour | Notes |
|---|---|---|
| Trade | 4 | per-bot variance ±2 (Spec/24) |
| LFG | 2 | only while actively seeking a group |
| General | 0 | bots do not initiate conversation |
| Guild | 6 | guild-event-driven (gz, gz, dinged) |
| Whisper response | unlimited | reactive only |

Aggregate server budget is then bot-count × per-bot budget. At 1000
bots the server sees ~4000 trade posts per hour — well within human
range for a live server.

### 3.3 Message generator

```csharp
public interface IChatGenerator
{
    Task<ChatPostPlan> GeneratePlanAsync(ChatContext ctx, CancellationToken ct);
}

public sealed record ChatPostPlan(
    string TemplateId,                            // basename under Bot/chat-templates/<channel>/
    string ResolvedText,                          // template with substitutions applied
    string AdvisorRationale,                      // empty when Phase 1 heuristic picked
    float  AdvisorConfidence);                    // 0 when Phase 1 heuristic picked

public sealed record ChatContext(
    ChatChannel Channel,
    string AccountName,
    string Class,
    int Level,
    Faction Faction,
    string Zone,
    string TriggerKind,                           // "wts" | "wtb" | "lfg-seeking" | "ding" | "epic-loot" | "whisper-reply-friendly" | "whisper-reply-stranger"
    IReadOnlyList<MarketSignal> MarketSignals,    // current AH listings the bot owns
    IReadOnlyList<RosterGoalGap> RosterGoalGaps); // current WTB priorities
```

Three maturity phases (matches [`Spec/20 §5`](20_DECISION_ENGINE.md)
advisor phasing):

| Phase | Source | How it picks |
|---|---|---|
| 1 — Heuristic | `Services/DecisionEngineService/Heuristics/ChatTemplateHeuristic.cs` | round-robin among the eligible candidates with the lowest `candidate_recent_use_count` |
| 2 — Rules + lookup | `Config/decision-engine/chat-template-rules.json` per `(channel, trigger_kind, chattiness)` | rule-table pick weighted by personality knob from [`Spec/24`](24_BEHAVIORAL_VARIATION.md) |
| 3 — ML | `Services/DecisionEngineService/Models/chat_template/v1.onnx` ([`Spec/20 §4.2`](20_DECISION_ENGINE.md)) | ONNX inference over `ChatTemplateContext`; fail-soft to Phase 1 |

The generator's job is to **pick** a template from a hand-authored
library, not to author free-form text. Free-form text is what
`PromptHandlingService` is for — and it is post-filtered by §8 denylist.

Templates support placeholder substitution: `{{itemName}}`, `{{count}}`,
`{{pricePerStack}}`, `{{bot.classAbbrev}}`, `{{zone}}`,
`{{dungeon}}`, `{{level}}`.

Example templates:

```
trade/wts-stack.txt:
   WTS [{{itemName}}] x{{count}} {{pricePerStack}}g/stack pst.
   {{count}}x [{{itemName}}] selling now, {{pricePerStack}}g. /w me.
   [{{itemName}}] x{{count}} on AH - {{pricePerStack}}g/stack if you want to skip the AH cut.

lfg/seeking-tank.txt:
   LF tank for {{dungeon}}, have heals, pst.
   {{bot.classAbbrev}} {{level}} LF tank for {{dungeon}}, msg.
```

The generator's job is to **pick** a template that fits the context,
not to author free-form text. Free-form text is what
PromptHandlingService is for.

## 4. Mail

### 4.1 Mail traffic profile

Two kinds of mail in the living server:

| Kind | Source | Target |
|---|---|---|
| Self-mail | Bot mails surplus to its own alts | Same account, different character |
| Sale-payout | AH sells produce mail with the gold payout | The selling bot |

Sale-payout mail is server-driven and unavoidable. Self-mail is the
loop the bot opts into:

```
econ.vendor-loop / mail-stage:
   on each city visit:
     1. open mailbox
     2. claim every payout mail (CMSG_TAKE_MONEY / CMSG_TAKE_ITEM)
     3. claim every readable mail (skill-recipe drops, reward mails)
     4. for each item in BagOverflow:
          - if AccountRoster has an alt that needs this item: send to alt
          - else: bank or vendor (depending on item value)
     5. close mailbox
```

### 4.2 Mail rate budget

No per-bot rate cap; mail is bound by AH sale velocity + bag overflow.
At 1000 bots the server sees ~150 mail/hour (one per bot per major
city visit). Mailbox traffic is the *most* alive-feeling protocol
signal a passive observer notices.

## 5. Guild events

Off by default; per-realm config opts a roster into a guild. When
opted in:

| Event | Trigger | Action |
|---|---|---|
| `Dinged` | bot levels up | post "Ding {{level}}" in guild chat (rate-budgeted to 1/hr) |
| `EpicLoot` | bot acquires a purple item | post "[{{itemName}}] gz" |
| `RaidComp` | RaidCoordinator forms a group | guild MOTD post if the activity is raid-tier |
| `MotdRead` | bot logs in | read guild MOTD, ack via guild chat with bot's own |
| `BankDeposit` | bot has a deposit-eligible item per `CharacterRosterGoal.GuildBankPolicy` | deposit to guild bank (if configured) |

Guild configuration lives in `Config/guilds/<realm>-<name>.json`.

## 6. Whisper reactivity

The `Shodan` GM Liaison (see WWoW `CLAUDE.md → Shodan` section) routes
operator whispers. **Other bots** must respond to incoming whispers
within an SLA:

- **Friendly whisper** (same faction, in the bot's friend list or
  guild): respond within 5–30 s using `Bot/chat-templates/whisper-reply/*.txt`.
- **Stranger whisper:** respond within 10–60 s.
- **Hostile whisper / spam:** ignore (no template fires).

The whisper-response handler is wired in
[`Plan/Activities/social.md`](../Plan/Activities/social.md). Response
latency variance is per-bot per [`Spec/24`](24_BEHAVIORAL_VARIATION.md).

## 7. City ambient traffic

The `social.city-ambient` Activity drives a bot through city services
when no higher-priority Objective is available:

```
city-ambient Objectives:
   travel-to-mailbox   →  retrieve-mail
   travel-to-vendor    →  sell-junk, repair
   travel-to-bank      →  rotate-bank-storage
   travel-to-AH        →  scan-listings, post-listings
   travel-to-inn       →  rebind-hearthstone (if needed)
   travel-to-trainer   →  train-pending-spells (if available)
   (loop)
```

This Activity is what *most* idle bots run when nothing demands their
attention. It feeds the Vision acceptance criterion "Vendors see
traffic. Banks see deposits. Mail moves between characters."

## 8. Anti-griefing

Trade chat / LFG chat / world chat carry no advertising, no
provocations, no spam phrases. Template library is hand-curated; new
templates are PR-reviewed. PromptHandlingService LLM completions are
post-filtered by a regex deny-list at `Bot/chat-templates/_denylist.txt`.

Per-bot rate limits prevent any single bot from being the chat-channel
loudest. Server-wide rate limits prevent the bot population from
crowding human posts.

## 9. Snapshot projection

Social-fabric state surfaces on `WoWActivitySnapshot` via two additive
proto fields (S11.2/S11.11 deliver — extend the file after the AOTA
runtime fields land at 33-37 per [`Spec/19 §5`](19_AOTA_RUNTIME.md#5-snapshot-projection)):

```protobuf
message ChatPostBudget {
    uint32 channel              = 1;   // ChatChannel enum
    uint32 posts_in_rolling_hour = 2;
    uint32 hourly_cap            = 3;
    uint64 last_post_at_ms       = 4;
}

// New fields on WoWActivitySnapshot (continuing after Spec/19 §5):
repeated ChatPostBudget chat_post_budgets = 38;   // one per channel the bot subscribes to
uint32                  pending_mail_count = 39;  // server-reported pending mail count after last open
```

The `chat_post_budgets` projection is the only way tests assert on
per-bot rate limiting (per CLAUDE.md Test Isolation Rules); a test
that counts `SendChatMessage` packets directly bypasses the contract.

## 10. Failure-reason mapping

Social-fabric failures map onto Spec/12's `FailureReason` enum. Two
new values are **needed** (additions to Spec/12 tracked as follow-ups
in [`Plan/SPEC_FILL_LOOP.md`](../Plan/SPEC_FILL_LOOP.md)):

| Failure | Spec/12 reason | Notes |
|---|---|---|
| Mail recipient does not exist / banned | `mail_recipient_invalid` *(new)* | also fires when target name violates the denylist |
| Chat post denied by `_denylist.txt` regex | `chat_denylist_rejection` *(new)* | task records the trip; falls back to template-library always-permitted entry |
| Channel full / join failed | `social_channel_join_failed` *(new — or alias `server_rejected`)* | exponential backoff per [`Plan/15 Failure recovery`](../Plan/15_PHASE11_SOCIAL_FABRIC.md#failure-recovery) |
| Whisper SLA window expired | (no failure raised — drop on floor) | by design; SLA is a quality target not a correctness gate |
| Anti-griefing trip from PromptHandlingService | `chat_denylist_rejection` *(new)* | retries with deterministic template |

## 11. ML integration

**Surface.** `IChatGenerator.GeneratePlanAsync(ctx, ct)` calls
`IDecisionEngineClient.GetChatTemplateAdviceAsync(ChatTemplateContext, ct)`
([`Spec/20 §2.1`](20_DECISION_ENGINE.md#21-proto-wire-shapes)) when
the local candidate-template set has ≥2 eligible entries. The advisor
returns a `ChatTemplateAdvice.RecommendedTemplateId` that **must equal**
one of the `candidate_template_ids` it was given; mismatches are
discarded and the heuristic Phase-1 round-robin applies.

**Why advisory not authoritative.** Templates are hand-curated for
anti-griefing safety. The advisor cannot author free-form text; it can
only **pick** which canned template fires for this `(channel,
trigger_kind, chattiness)` situation. The `Bot/chat-templates/_denylist.txt`
post-filter still gates every emitted post regardless of advice.

**Input feature vector.** `ChatTemplateContext` — see
[`Spec/20 §2.1`](20_DECISION_ENGINE.md#21-proto-wire-shapes). The
service-side ONNX tensor shape is `[1, 48]` per
[`Spec/20 §4.2`](20_DECISION_ENGINE.md#42-onnx-feature-tensor-shapes-per-advisor).

**Output shape.** `ChatTemplateAdvice` — see Spec/20 §2.1.

**Three maturity phases** per [`Spec/20 §5`](20_DECISION_ENGINE.md):

| Phase | Source | Owned by |
|---|---|---|
| 1 — Heuristic | `Services/DecisionEngineService/Heuristics/ChatTemplateHeuristic.cs` — round-robin over lowest `candidate_recent_use_count` | Plan/15 slot S11.1 |
| 2 — Rules + lookup | `Config/decision-engine/chat-template-rules.json` per `(channel, trigger_kind, chattiness)` precedence table | Plan/15 slot S11.5 |
| 3 — ONNX | `Services/DecisionEngineService/Models/chat_template/v1.onnx` trained on labeled traces under `tmp/test-runtime/traces/<test-name>/<timestamp>.jsonl` (Spec/20 §6) | Plan/14 slot S10.6 (Mode=Ml flip) |

**Fail-soft fallback.** Generator falls back to the Phase-1 heuristic
when `ChatTemplateAdvice.RecommendedTemplateId` is empty, has
`Confidence < 0.5`, or is not in the candidate set.

**Live-validation guard.** Replaying any production chat-emission
trace with `chat_template` advisor forced to `NoAdvice` MUST produce
chat posts that still satisfy the denylist + per-bot budget invariants.
The advisor cannot break safety, only nudge style.

## 12. Dynamic-progressive invariant

A bot's chat output MUST satisfy two properties on every emission:

1. **Dynamic.** Two bots with the same `(channel, trigger_kind)` but
   different `(class, level, faction, chattiness, AccountName)`
   parameters MUST sometimes emit different `TemplateId` choices.
   Identical inputs (including the bot-local recent-use counter)
   produce identical choices (deterministic given fixed `mode_used`).
   Asserted via the `advisor_rationale` strings in trace lines.
2. **Progressive.** Social-fabric chatter MUST close `roster_distance`
   *indirectly* by surfacing trade opportunities: a `wts` post that
   produces a buyer-whisper reduces `inventory_value_copper`-to-gold
   conversion friction; a `lfg-seeking` post that produces a group
   invite reduces `dungeon-completion` distance on `CharacterRosterGoal`.
   The contract is at the trace surface only:
   `SocialFabric_DynamicProgressive_TradeChatBuyersConvert` asserts
   that ≥10% of `wts` posts produce a buyer-whisper-then-trade outcome
   in any production-grade trace; cosmetic chat alone is **not**
   progressive and would fail this test.

## 13. Plan-slot cross-reference

| Slot | Owns | Section here |
|---|---|---|
| [`Plan/15/S11.1`](../Plan/15_PHASE11_SOCIAL_FABRIC.md#s111--ichatgenerator--template-library) | `IChatGenerator`, `TemplateChatGenerator`, `Bot/chat-templates/**` | §3.3 |
| [`Plan/15/S11.2`](../Plan/15_PHASE11_SOCIAL_FABRIC.md#s112--networkchannelframe-bg-adapters) | `NetworkChatFrame.cs`, `NetworkLfgFrame.cs`, `NetworkGuildFrame.cs` | §3.1, §9 chat_post_budgets emission path |
| [`Plan/15/S11.3`](../Plan/15_PHASE11_SOCIAL_FABRIC.md#s113--mail-packetframe-parity) | `MailNetworkClientComponent.cs`, `NetworkMailFrame.cs` | §4, §9 pending_mail_count |
| [`Plan/15/S11.4`](../Plan/15_PHASE11_SOCIAL_FABRIC.md#s114--mailretrievetask--mailsendtask) | `MailRetrieveTask.cs`, `MailSendTask.cs` | §4.1 |
| [`Plan/15/S11.5`](../Plan/15_PHASE11_SOCIAL_FABRIC.md#s115--socialtrade-chat-cycle-activity-composer) | `SocialComposer.cs`, `TradeChatPostTask.cs` | §3, §11 Phase 2 rules table |
| [`Plan/15/S11.6`](../Plan/15_PHASE11_SOCIAL_FABRIC.md#s116--sociallfg-cycle-activity-composer) | LFG composer | §3.1 LFG row |
| [`Plan/15/S11.7`](../Plan/15_PHASE11_SOCIAL_FABRIC.md#s117--socialguild-events-activity-composer) | guild-events composer | §5 |
| [`Plan/15/S11.8`](../Plan/15_PHASE11_SOCIAL_FABRIC.md#s118--socialcity-ambient-activity-composer) | city-ambient composer | §7 |
| [`Plan/15/S11.10`](../Plan/15_PHASE11_SOCIAL_FABRIC.md#s1110--whisper-response-handler) | `WhisperReplyHandler.cs` | §6 |
| [`Plan/15/S11.11`](../Plan/15_PHASE11_SOCIAL_FABRIC.md#s1111--per-bot-post-budget-enforcement) | `PostBudgetTracker.cs` | §3.2, §9 |
| [`Plan/15/S11.12`](../Plan/15_PHASE11_SOCIAL_FABRIC.md#s1112--anti-griefing-denylist) | `ChatPostFilter.cs`, `_denylist.txt` | §8, §10 `chat_denylist_rejection` |
| [`Plan/14/S10.6`](../Plan/14_PHASE10_DECISION_ENGINE_INTEGRATION.md#s106--mode-aware-advisor-activation) | `chat_template` advisor mode flip | §11 Phase 3 |

## 14. Test surface

Unit / contract tests live under
`Tests/BotRunner.Tests/Social/SocialFabricContractTests.cs`. All are
`Skip("contract pending S11.x (Plan/15)")` until the slot lands.

- **`TradeChat_RespectsPerBotHourlyBudget`** — a bot in
  `social.trade-chat-cycle` never posts > 4 trade posts in any rolling
  hour. Asserts via `snapshot.chat_post_budgets[Trade].posts_in_rolling_hour`.
  Slot S11.11.
- **`MailFlow_AlternatesBetweenAccounts`** — alt inventories sync via
  mail over a representative session. Asserts via
  `snapshot.pending_mail_count` transitions. Slot S11.4.
- **`GuildDinged_PostsOncePerLevel`** — guild ding post fires once per
  ding, not per snapshot. Slot S11.7.
- **`WhisperReply_RespondsWithinSla`** — friendly whisper triggers a
  reply via `Network<X>Frame` packet path within 30 s. Stranger path
  asserted up to 60 s. Slot S11.10.
- **`CityAmbient_AdvancesObjectivesWithoutInterleaving`** — a bot in
  city-ambient loops through the 6 Objectives without falling back
  into idle. Asserts via `snapshot.current_objective_id` transitions.
  Slot S11.8.
- **`ChatTemplate_AdvisorRespectsCandidateSet`** — when DecisionEngine
  returns an advisor `RecommendedTemplateId` outside the
  `candidate_template_ids` set, the generator falls back to Phase-1
  heuristic. Slot S11.5.
- **`ChatTemplate_DenylistTripFallsBackToTemplate`** — when
  PromptHandlingService completion trips `_denylist.txt`, the bot
  emits the deterministic template instead and the trip is logged.
  Slot S11.12.
- **`SocialFabric_DynamicProgressive_TradeChatBuyersConvertTest`** —
  the dynamic-progressive invariant from §12. Over a representative
  trace, ≥10% of `wts` posts produce a buyer-whisper-then-trade
  outcome (asserts via JSONL `outcome` lines from Spec/20 §6.1). Slot
  S11.13.

Live validation runs against a small (10-bot) population on
`Westworld-Test` and asserts on chat-protocol observations.
