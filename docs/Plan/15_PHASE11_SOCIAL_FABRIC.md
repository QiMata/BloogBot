# Plan 15 — Phase 11: Social fabric

> **Goal.** Ship the chat / mail / guild / AH-chatter / city-ambient
> surface so the server *feels* alive at a protocol level. Acceptance
> criterion: a passive observer with chat logs from `Westworld` for an
> hour cannot distinguish the bot population from a thin-but-real human
> server.
>
> **Entry pre-requisite.** Phase 2 done (OnDemand launcher live so the
> social-service Activities have somewhere to land). Phase 9 in flight
> for the new `social.*` catalog rows.

## Exit criteria

- [ ] `IChatGenerator` ships with hand-authored template library at `Bot/chat-templates/`; templates cover Trade WTS / WTB, LFG, guild-ding, whisper-reply, raid-recruitment.
- [ ] `Network<Channel>Frame` BG adapters ship for Trade / Guild / LFG / Whisper channels; FG path drives Lua `JoinChannelByName` + `SendChatMessage` calls.
- [ ] `MailNetworkClientComponent` BG packet path + FG `IMailFrame` parity for `CMSG_SEND_MAIL`, `CMSG_TAKE_ITEM`, `CMSG_TAKE_MONEY`, `CMSG_RETURN_MAIL`.
- [ ] `social.trade-chat-cycle`, `social.lfg-cycle`, `social.guild-events`, `social.city-ambient`, `social.mage-port`, `social.warlock-summon` Activities live with end-to-end tests.
- [ ] Whisper-response handler ships with `Bot/chat-templates/whisper-reply/`; respects the 5-30 s friendly SLA from [`Spec/21_SOCIAL_FABRIC.md`](../Spec/21_SOCIAL_FABRIC.md).
- [ ] Per-bot hourly post budget enforced; LiveValidation confirms a bot never exceeds the budget over a 1-hour session.
- [ ] Server-wide budget metric `wwow.chat.posts_per_hour{channel}` published.
- [ ] Anti-griefing denylist regex in place at `Bot/chat-templates/_denylist.txt`; PR-review gate documented.

## Slots

### S11.1 — `IChatGenerator` + template library

- **Owner:** `monorepo-worker`
- **Status:** open
- **Owned paths:** `Exports/BotRunner/Social/IChatGenerator.cs`, `Exports/BotRunner/Social/TemplateChatGenerator.cs`, `Bot/chat-templates/**`
- **Spec contracts:** [`Spec/21_SOCIAL_FABRIC.md#3-chat-output`](../Spec/21_SOCIAL_FABRIC.md#3-chat-output)
- **Goal:** Implement the generator interface + ship template library covering:
  - `trade/wts-stack.txt`, `trade/wtb-mat.txt`, `trade/lfg-bg.txt`
  - `lfg/seeking-tank.txt`, `lfg/seeking-heals.txt`, `lfg/joining-rfc.txt`
  - `whisper-reply/friendly-acknowledge.txt`, `whisper-reply/declining.txt`
  - `guild/ding.txt`, `guild/epic-loot.txt`, `guild/motd-ack.txt`

### S11.2 — `Network<Channel>Frame` BG adapters

- **Owner:** `monorepo-worker`
- **Status:** open
- **Owned paths:** `Exports/WoWSharpClient/Frames/NetworkChatFrame.cs`, `Exports/WoWSharpClient/Frames/NetworkLfgFrame.cs`, `Exports/WoWSharpClient/Frames/NetworkGuildFrame.cs`
- **Goal:** Follow the established `Network<X>Frame` pattern from S1.15/17/19 ([wwow_bg_frame_pattern]). Wire in `WoWSharpObjectManager`. BG packet path for `CMSG_MESSAGECHAT`, `CMSG_JOIN_CHANNEL`, `CMSG_LEAVE_CHANNEL`. Tests follow `NetworkTradeFrameTests` shape.

### S11.3 — Mail packet/frame parity

- **Owner:** `monorepo-worker`
- **Status:** open
- **Owned paths:** `Exports/WoWSharpClient/Networking/ClientComponents/MailNetworkClientComponent.cs`, `Exports/WoWSharpClient/Frames/NetworkMailFrame.cs`
- **Goal:** BG packet path + FG frame for the mail opcodes. `IMailFrame` exposes `OpenMailbox`, `TakeMail(int slot)`, `TakeMailItem(int slot)`, `TakeMailMoney(int slot)`, `ReturnMail(int slot)`, `SendMail(...)`.

### S11.4 — `MailRetrieveTask` + `MailSendTask`

- **Owner:** `monorepo-worker`
- **Status:** open
- **Depends on:** S11.3
- **Owned paths:** `Exports/BotRunner/Tasks/Economy/MailRetrieveTask.cs`, `Exports/BotRunner/Tasks/Economy/MailSendTask.cs`
- **Goal:** Land the two mail tasks from the family catalog (Spec/03). Integrate into `econ.vendor-loop` as Objective steps.

### S11.5 — `social.trade-chat-cycle` Activity composer

- **Owner:** `monorepo-worker`
- **Status:** open
- **Depends on:** S11.1, S11.2, Phase 9 social-shard rows.
- **Owned paths:** `Services/WoWStateManager/Activities/Composers/SocialComposer.cs`, `Exports/BotRunner/Tasks/Social/TradeChatPostTask.cs`
- **Goal:** Composer produces Objective sequence: `travel-to-city → join-trade-channel → for each MarketSignal: emit-trade-post (rate-budgeted) → linger`. `TradeChatPostTask` reads from `IChatGenerator`, respects the hourly budget, applies the personality knob from [`Spec/24`](../Spec/24_BEHAVIORAL_VARIATION.md).

### S11.6 — `social.lfg-cycle` Activity composer

- **Owner:** `monorepo-worker`
- **Status:** open
- **Depends on:** S11.1, S11.2
- **Goal:** Bot in a level-band-matched bot for a dungeon Activity posts to LFG channel + listens for invites + accepts on quorum. Interleaves with `econ.vendor-loop` so the bot stays at a sensible idle location.

### S11.7 — `social.guild-events` Activity composer

- **Owner:** `monorepo-worker`
- **Status:** open
- **Depends on:** S11.1, S11.2, S11.3
- **Goal:** Bot ack guild MOTD on login; emit guild-ding on level; post epic-loot guild-chat line on purple acquisition; deposit-to-guild-bank if `GuildBankPolicy` configured.

### S11.8 — `social.city-ambient` Activity composer

- **Owner:** `monorepo-worker`
- **Status:** open
- **Depends on:** S11.4 (mail tasks)
- **Goal:** Idle-city service loop per [`Spec/21#7`](../Spec/21_SOCIAL_FABRIC.md#7-city-ambient-traffic). Bot rotates mailbox → vendor → bank → AH → inn → trainer.

### S11.9 — `social.mage-port` + `social.warlock-summon`

- **Owner:** `monorepo-worker`
- **Status:** open
- **Depends on:** S11.1 (whisper handling), Phase 2 OnDemand launcher.
- **Owned paths:** `Services/WoWStateManager/Coordination/PortServiceCoordinator.cs`, `Services/WoWStateManager/Coordination/WarlockSummonCoordinator.cs`
- **Goal:** Both Activities accept a human's whisper request `!port <city>` / `!summon`. The launcher spawns a single mage (port) or pair of warlock+supporter (summon), travels to the human's position via teleport, casts the port / summon, and dismisses.

### S11.10 — Whisper response handler

- **Owner:** `monorepo-worker`
- **Status:** open
- **Depends on:** S11.1, S11.2
- **Owned paths:** `Exports/BotRunner/Social/WhisperReplyHandler.cs`
- **Goal:** Subscribe to `SMSG_MESSAGECHAT` whisper events. Reply within the SLA from Spec/21 §6, using personality jitter from Spec/24. Shodan-routed whispers bypass this handler (they go to `OnExternalActivityRequestAsync`).

### S11.11 — Per-bot post-budget enforcement

- **Owner:** `monorepo-worker`
- **Status:** open
- **Owned paths:** `Exports/BotRunner/Social/PostBudgetTracker.cs`
- **Goal:** Rolling-hour budget per bot per channel. Tasks that emit chat consult the tracker before posting. Tracker is bot-local (no IPC).

### S11.12 — Anti-griefing denylist

- **Owner:** `monorepo-worker`
- **Status:** open
- **Owned paths:** `Bot/chat-templates/_denylist.txt`, `Exports/BotRunner/Social/ChatPostFilter.cs`
- **Goal:** Regex denylist post-filter. PR-review gate documented in `Bot/chat-templates/README.md`.

### S11.13 — LiveValidation suite

- **Owner:** `monorepo-test-runner`
- **Status:** open
- **Depends on:** S11.1..S11.12
- **Goal:** Five tests:
  - `SocialFabricTests.TradeChat_RespectsPerBotHourlyBudget` — 1-hour session, ≤4 posts.
  - `SocialFabricTests.MailFlow_AlternatesBetweenAccounts` — alt mail traffic over a 30-min session.
  - `SocialFabricTests.GuildDinged_PostsOncePerLevel`.
  - `SocialFabricTests.WhisperReply_RespondsWithinSla` — friendly + stranger paths.
  - `SocialFabricTests.MagePort_DeliversHumanToCity` — full OnDemand flow.

## Failure recovery

- **Chat channel full** → bot retries channel join with exponential backoff (cap 5 min). Failure surfaces in snapshot `RecentErrors`.
- **Mail send fails** (recipient does not exist / banned name) → task fails as `FailureReason.MailRecipientInvalid`; bot abandons that mail item, banks instead.
- **Whisper response timeout** → bot drops the reply (no retry; the SLA window already expired).
- **Anti-griefing denylist trips** during PromptHandlingService completion → bot falls back to the hand-authored template (always permitted) and logs the trip for review.

## Related specs

- [`Spec/21_SOCIAL_FABRIC.md`](../Spec/21_SOCIAL_FABRIC.md) — contract.
- [`Spec/24_BEHAVIORAL_VARIATION.md`](../Spec/24_BEHAVIORAL_VARIATION.md) — personality knobs that consume this fabric.
- [`Plan/13_PHASE9_CATALOG_FILL.md`](13_PHASE9_CATALOG_FILL.md) — adds the `social.*` catalog rows that this phase fills with task implementations.
