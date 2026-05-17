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
    string Generate(ChatContext ctx);            // returns the literal post text
}

public sealed record ChatContext(
    ChatChannel Channel,
    string AccountName,
    string Class,
    int Level,
    Faction Faction,
    string Zone,
    IReadOnlyList<MarketSignal> MarketSignals,   // current AH listings the bot owns
    IReadOnlyList<RosterGoalGap> RosterGoalGaps); // current WTB priorities
```

Phase 1 generator: hand-authored template library (`Bot/chat-templates/<channel>/*.txt`).
Phase 3 generator: `PromptHandlingService` LLM completion (opt-in per
[`Spec/24`](24_BEHAVIORAL_VARIATION.md)).

Templates support placeholder substitution: `{{itemName}}`, `{{count}}`,
`{{pricePerStack}}`, `{{bot.classAbbrev}}`, `{{zone}}`.

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

## 9. Test surface

- **`SocialFabricTests.TradeChat_RespectsPerBotHourlyBudget`** — a bot
  in `social.trade-chat-cycle` never posts > 4 trade posts in any
  rolling hour.
- **`SocialFabricTests.MailFlow_AlternatesBetweenAccounts`** — alt
  inventories sync via mail over a representative session.
- **`SocialFabricTests.GuildDinged_PostsOncePerLevel`** — guild ding
  post fires once per ding, not per snapshot.
- **`SocialFabricTests.WhisperReply_RespondsWithinSla`** — friendly
  whisper triggers a reply via `Network<X>Frame` packet path within
  30 s.
- **`SocialFabricTests.CityAmbient_AdvancesObjectivesWithoutInterleaving`** —
  a bot in city-ambient loops through the 6 Objectives without falling
  back into idle.

Live validation runs against a small (10-bot) population on
`Westworld-Test` and asserts on chat-protocol observations.
