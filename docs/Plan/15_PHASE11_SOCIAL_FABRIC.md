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
- **Owned paths:**
  - `Exports/BotRunner/Social/IChatGenerator.cs`
  - `Exports/BotRunner/Social/TemplateChatGenerator.cs`
  - `Bot/chat-templates/**`
  - `Bot/chat-templates/README.md` (PR-review gate doc)
- **Read-only paths:** `Spec/21 §3.3`, `Spec/24 §2` (`ChattyLevel`, `WhisperReplyDelayMs`), Plan/14 S10.9 (DecisionEngine wire).
- **Spec contracts:** [`Spec/21_SOCIAL_FABRIC.md §3-3-message-generator`](../Spec/21_SOCIAL_FABRIC.md#33-message-generator), [`Spec/20 §2.2`](../Spec/20_DECISION_ENGINE.md#22-chattemplate-advisor).
- **Goal:** Implement `IChatGenerator.GeneratePlanAsync(ChatContext ctx, CancellationToken ct)` returning a `ChatPostPlan { TemplateId, ResolvedText, AdvisorRationale, AdvisorConfidence }`. Ship a hand-authored template library covering:
  - `trade/wts-stack.txt`, `trade/wtb-mat.txt`, `trade/lfg-bg.txt`
  - `lfg/seeking-tank.txt`, `lfg/seeking-heals.txt`, `lfg/joining-rfc.txt`
  - `whisper-reply/friendly-acknowledge.txt`, `whisper-reply/declining.txt`
  - `guild/ding.txt`, `guild/epic-loot.txt`, `guild/motd-ack.txt`
- **Procedure:**
  1. Define `IChatGenerator` interface + `ChatContext` + `ChatPostPlan` records per Spec/21 §3.3.
  2. Implement `TemplateChatGenerator` that (a) scans `Bot/chat-templates/<channel>/*.txt` for `(channel, trigger_kind)`-matching files, (b) calls Plan/14 S10.9 `GetChatTemplateAdviceAsync` when |candidates| ≥ 2, (c) falls back to lowest-recent-use template on `NoAdvice`.
  3. Substitute placeholders (`{{itemName}}`, `{{count}}`, `{{pricePerStack}}`, `{{bot.classAbbrev}}`, `{{zone}}`, `{{dungeon}}`, `{{level}}`) before returning the plan.
  4. Write `Bot/chat-templates/README.md` documenting the PR-review gate and the placeholder set.
- **Success criteria:** `IChatGeneratorTests` unit tests cover all 11 named template files; templates parse against the placeholder set; placeholder substitution leaves no `{{...}}` in `ResolvedText`.
- **Failure modes:** template file missing → return empty `ChatPostPlan` and emit a warning (do NOT crash the calling Task); placeholder values null/missing → leave `{{...}}` markers and log (test fails on this so CI catches it).
- **ML integration sub-bullet:** Plan/14 S10.9 owns the advisor wire; this slot consumes it. Phase 1 = recent-use round-robin (this slot's heuristic). Phase 2 = `Config/decision-engine/chat-template-rules.json`. Phase 3 = ONNX trained on labeled engagement traces (off-line).

### S11.2 — `Network<Channel>Frame` BG adapters

- **Owner:** `monorepo-worker`
- **Status:** open
- **Owned paths:**
  - `Exports/WoWSharpClient/Frames/NetworkChatFrame.cs`
  - `Exports/WoWSharpClient/Frames/NetworkLfgFrame.cs`
  - `Exports/WoWSharpClient/Frames/NetworkGuildFrame.cs`
- **Read-only paths:** `Exports/WoWSharpClient/Frames/NetworkTradeFrame.cs` (reference pattern from S1.15), `Exports/WoWSharpClient/WoWSharpObjectManager.cs:229-234` (wire-in site).
- **Spec contracts:** [`Spec/21 §3.1`](../Spec/21_SOCIAL_FABRIC.md#31-channels-in-scope-1121).
- **Goal:** Follow the established `Network<X>Frame` pattern (memory: wwow_bg_frame_pattern). Wire in `WoWSharpObjectManager`. BG packet path for `CMSG_MESSAGECHAT` (opcode 0x95), `CMSG_JOIN_CHANNEL` (0x97), `CMSG_LEAVE_CHANNEL` (0x98). Each frame implements a lazy resolver of the matching `I<X>NetworkClientComponent`.
- **Procedure:**
  1. For each channel kind, create `Network<X>Frame.cs` mirroring `NetworkTradeFrame.cs` shape.
  2. Add `protected set` to any contract DTOs missing constructors (per wwow_bg_frame_pattern memo).
  3. Wire each frame into `WoWSharpObjectManager:229-234` lazy-init block.
  4. Implement `NetworkChatFrameTests` / `NetworkLfgFrameTests` / `NetworkGuildFrameTests` following the `NetworkTradeFrameTests` shape.
- **Success criteria:** Frame unit tests green; FG path drives Lua `JoinChannelByName(...)` + `SendChatMessage(...)` calls and observes them via the new frames; BG path emits the matching CMSG opcodes verified by packet-capture tests.
- **Failure modes:** missing client-component implementation → frame returns null on resolver; calling task must handle null per Spec/21 §10 failure mapping.
- **ML integration sub-bullet:** none — pure wire-plumbing slot.

### S11.3 — Mail packet/frame parity

- **Owner:** `monorepo-worker`
- **Status:** open
- **Owned paths:**
  - `Exports/WoWSharpClient/Networking/ClientComponents/MailNetworkClientComponent.cs`
  - `Exports/WoWSharpClient/Frames/NetworkMailFrame.cs`
  - `Exports/WoWSharpClient/Frames/IMailFrame.cs` (contract)
- **Read-only paths:** existing trade-frame pair (`MailNetworkClientComponent` neighbors), Spec/21 §4.
- **Spec contracts:** [`Spec/21 §4`](../Spec/21_SOCIAL_FABRIC.md#4-mail).
- **Goal:** BG packet path + FG frame for the mail opcodes. `IMailFrame` exposes:
  - `OpenMailbox()` → CMSG_GET_MAIL_LIST (0x23E)
  - `TakeMail(int slot)` → CMSG_MAIL_TAKE_ITEM (0x245)
  - `TakeMailItem(int slot)` → same opcode with item flag
  - `TakeMailMoney(int slot)` → CMSG_MAIL_TAKE_MONEY (0x244)
  - `ReturnMail(int slot)` → CMSG_MAIL_RETURN_TO_SENDER (0x246)
  - `SendMail(string recipient, string subject, string body, ...)` → CMSG_SEND_MAIL (0x238)
- **Procedure:**
  1. Implement `MailNetworkClientComponent` parsing SMSG_MAIL_LIST_RESULT (0x23F) and SMSG_MAIL_COMMAND_RESULT (0x24A).
  2. `NetworkMailFrame` implements `IMailFrame` via the lazy-resolver pattern.
  3. Wire into `WoWSharpObjectManager`.
  4. Send/receive tests via `NetworkMailFrameTests`.
- **Success criteria:** mail opcode tests green; round-trip `SendMail` → `OpenMailbox` → `TakeMail` returns the same item count.
- **Failure modes:** server returns `MAIL_ERR_RECIPIENT_NOT_FOUND` (37) → component raises mail-fail event; task maps to `mail_recipient_invalid` Spec/12 follow-up.
- **ML integration sub-bullet:** none.

### S11.4 — `MailRetrieveTask` + `MailSendTask`

- **Owner:** `monorepo-worker`
- **Status:** open
- **Depends on:** S11.3
- **Owned paths:**
  - `Exports/BotRunner/Tasks/Economy/MailRetrieveTask.cs`
  - `Exports/BotRunner/Tasks/Economy/MailSendTask.cs`
  - `Tests/BotRunner.Tests/Economy/MailRetrieveTaskTests.cs`
  - `Tests/BotRunner.Tests/Economy/MailSendTaskTests.cs`
- **Read-only paths:** `Spec/03 §catalog-of-task-families`, `Spec/21 §4`, `IMailFrame` (S11.3 owner).
- **Spec contracts:** [`Spec/03_BOTRUNNER.md`](../Spec/03_BOTRUNNER.md), [`Spec/21 §4.1`](../Spec/21_SOCIAL_FABRIC.md#41-mail-traffic-profile).
- **Goal:** Land the two mail tasks from the family catalog (Spec/03). Integrate into `econ.vendor-loop` as Objective steps via the catalog's mail-stage sequence.
- **Procedure:**
  1. `MailRetrieveTask`: open mailbox; iterate every mail slot; for each, `TakeMailItem` then `TakeMailMoney`; close mailbox. Surface `pending_mail_count` to snapshot (Spec/21 §9 field 39).
  2. `MailSendTask`: parameters `(recipient, subject, body, itemSlots[])`; validates recipient exists via `IAccountRoster` query; falls back to bank-deposit on `MAIL_ERR_RECIPIENT_NOT_FOUND`.
  3. Register both tasks in the `Tasks/Economy/` family head per Spec/03.
- **Success criteria:** unit tests green; integration with `econ.vendor-loop` works in a 1-bot LiveValidation.
- **Failure modes:**
  - Recipient invalid → `FailureReason.mail_recipient_invalid` (Spec/12 follow-up); MailSendTask routes the item to bank instead.
  - Mailbox gone (NPC despawn mid-task) → `task_precondition_failed`; task aborts.
- **ML integration sub-bullet:** none.

### S11.5 — `social.trade-chat-cycle` Activity composer

- **Owner:** `monorepo-worker`
- **Status:** open
- **Depends on:** S11.1, S11.2, S11.11 (post-budget tracker), Plan/13 S9.6 (social shard rows).
- **Owned paths:**
  - `Services/WoWStateManager/Activities/Composers/SocialComposer.cs`
  - `Exports/BotRunner/Tasks/Social/TradeChatPostTask.cs`
- **Read-only paths:** `Spec/21 §3`, `Spec/24` `ChattyLevel` + `AhPostingUnderscutPercent`, `IChatGenerator` (S11.1), `IActivityCatalog` social rows.
- **Spec contracts:** [`Spec/21 §3`](../Spec/21_SOCIAL_FABRIC.md#3-chat-output), [`Spec/21 §11`](../Spec/21_SOCIAL_FABRIC.md#11-ml-integration), [`Spec/19 §4`](../Spec/19_AOTA_RUNTIME.md#4-objectivetype-enum-closed-set-proto-mirrored) (Objective types).
- **Goal:** Composer produces Objective sequence: `travel-to-city → join-trade-channel → for each MarketSignal: emit-trade-post (rate-budgeted) → linger`. `TradeChatPostTask` reads from `IChatGenerator`, respects the hourly budget, applies the personality knob from [`Spec/24`](../Spec/24_BEHAVIORAL_VARIATION.md).
- **Procedure:**
  1. `SocialComposer.ComposeTradeChat(...)` emits the Objective sequence above; per-Objective gates check `PostBudgetTracker` (S11.11) before adding a post Objective.
  2. `TradeChatPostTask`: build `ChatContext` with `MarketSignals` (current AH listings the bot owns) + `RosterGoalGaps` (current WTB needs); call `IChatGenerator.GeneratePlanAsync(...)`; send via `NetworkChatFrame` (S11.2).
  3. Apply `ChatPostFilter` (S11.12) before sending; on denylist trip, drop the post AND log `chat_denylist_rejection`.
  4. Increment `PostBudgetTracker[Trade]` on successful post.
- **Success criteria:** `SocialFabricContractTests.TradeChat_RespectsPerBotHourlyBudget` green; `MarketSignals` placeholder substitution correct.
- **Failure modes:** trade channel full (server-side cap) → exponential backoff per [Failure recovery](#failure-recovery); budget exceeded → drop the post AND log (NOT an error).
- **ML integration sub-bullet:** consumes Plan/14 S10.9 chat-template advisor via S11.1's generator. No new advisor here.

### S11.6 — `social.lfg-cycle` Activity composer

- **Owner:** `monorepo-worker`
- **Status:** open
- **Depends on:** S11.1, S11.2
- **Owned paths:**
  - `Services/WoWStateManager/Activities/Composers/SocialComposer.cs` (extend with `ComposeLfgCycle`; shares file with S11.5)
  - `Exports/BotRunner/Tasks/Social/LfgPostTask.cs`
  - `Exports/BotRunner/Tasks/Social/AcceptGroupInviteTask.cs`
- **Read-only paths:** `Spec/21 §3.1` LFG channel row, `IActivityCatalog` (level-band-matched dungeon Activities).
- **Spec contracts:** [`Spec/21 §3.1`](../Spec/21_SOCIAL_FABRIC.md#31-channels-in-scope-1121), [`Spec/19`](../Spec/19_AOTA_RUNTIME.md) Objective sequencing.
- **Goal:** Bot at a level-band-matched dungeon Activity posts to LFG channel + listens for invites + accepts on quorum. Interleaves with `econ.vendor-loop` so the bot stays at a sensible idle location.
- **Procedure:**
  1. `ComposeLfgCycle` emits: `travel-to-city → join-lfg-channel → emit-lfg-post → wait-for-invite OR linger`.
  2. `LfgPostTask` uses `IChatGenerator` with `trigger_kind="lfg-seeking"`; budget cap 2/hr per Spec/21 §3.2.
  3. `AcceptGroupInviteTask` subscribes to `SMSG_GROUP_INVITE` (Spec/communication has `has_pending_group_invite` field 32); accepts within 30 s if the inviting party matches the bot's level band and role policy.
  4. On group form, transition to the matching dungeon Activity (composer hands off).
- **Success criteria:** LiveValidation: a level-15 Horde bot in LFG receives a manual invite from an Alliance toon and is rejected (faction mismatch); same bot receives a Horde invite and accepts within 30 s.
- **Failure modes:** invite from blacklisted account → declined silently; channel unavailable → exp backoff per failure recovery.
- **ML integration sub-bullet:** template selection via S11.1; no new advisor.

### S11.7 — `social.guild-events` Activity composer

- **Owner:** `monorepo-worker`
- **Status:** open
- **Depends on:** S11.1, S11.2, S11.3
- **Owned paths:**
  - `Services/WoWStateManager/Activities/Composers/SocialComposer.cs` (extend with `ComposeGuildEvents`)
  - `Exports/BotRunner/Tasks/Social/GuildDingTask.cs`
  - `Exports/BotRunner/Tasks/Social/GuildBankDepositTask.cs`
  - `Config/guilds/<realm>-<name>.json` (schema doc)
- **Read-only paths:** `Spec/21 §5` (guild event triggers), `NetworkGuildFrame` (S11.2).
- **Spec contracts:** [`Spec/21 §5`](../Spec/21_SOCIAL_FABRIC.md#5-guild-events).
- **Goal:** Bot acks guild MOTD on login; emits guild-ding on level-up; posts epic-loot guild-chat line on purple acquisition; deposits to guild bank if `GuildBankPolicy` configured.
- **Procedure:**
  1. `ComposeGuildEvents` is event-driven, not scheduled: composer listens for snapshot deltas (level up, item acquire, login).
  2. `GuildDingTask` fires on level-delta event; rate-budgeted to 1/hr per Spec/21 §3.2.
  3. `GuildBankDepositTask` runs at every guild-bank visit per `CharacterRosterGoal.GuildBankPolicy`.
  4. Config schema: `Config/guilds/<realm>-<name>.json` with `members[]`, `motd`, `bank_policy`.
- **Success criteria:** `SocialFabricContractTests.GuildDinged_PostsOncePerLevel` green.
- **Failure modes:** guild config missing → all Guild Activities are no-ops (NOT errors); guild full → join-task fails with `task_unrecoverable`.
- **ML integration sub-bullet:** template selection via S11.1; no new advisor.

### S11.8 — `social.city-ambient` Activity composer

- **Owner:** `monorepo-worker`
- **Status:** open
- **Depends on:** S11.4 (mail tasks)
- **Owned paths:**
  - `Services/WoWStateManager/Activities/Composers/SocialComposer.cs` (extend with `ComposeCityAmbient`)
  - `Exports/BotRunner/Tasks/Social/CityAmbientLoopTask.cs` (orchestrator)
- **Read-only paths:** `Spec/21 §7`, existing vendor/bank/AH/inn tasks (Spec/03 family heads), `Bot/named-locations.json` for city-service coords.
- **Spec contracts:** [`Spec/21 §7`](../Spec/21_SOCIAL_FABRIC.md#7-city-ambient-traffic).
- **Goal:** Idle-city service loop. Bot rotates mailbox → vendor → bank → AH → inn → trainer in a stable order, with personality jitter from `Spec/24 NpcInteractApproachMs`.
- **Procedure:**
  1. `ComposeCityAmbient` emits 6 Objectives in fixed order; each Objective is a `travel-to-<service>` + `interact-with-<service>` pair.
  2. `CityAmbientLoopTask` re-enters the loop when the last Objective completes (continuous Activity until a higher-priority Activity preempts).
  3. Honor existing tasks: `OpenMailboxTask` (S11.4), `VendorRepairTask`, `BankRotateTask`, `AhScanListingsTask`, `RebindHearthTask`, `TrainPendingSpellsTask`.
- **Success criteria:** `SocialFabricContractTests.CityAmbient_AdvancesObjectivesWithoutInterleaving` green; 10-min simulated loop completes ≥1 full rotation.
- **Failure modes:** NPC despawn (trainer / vendor missing) → skip that Objective, continue loop.
- **ML integration sub-bullet:** none — Activity is intentionally deterministic in its rotation.

### S11.9 — `social.mage-port` + `social.warlock-summon`

- **Owner:** `monorepo-worker`
- **Status:** open
- **Depends on:** S11.10 (whisper handler delivers `!port`/`!summon` to OnDemand), Phase 2 OnDemand launcher, Plan/03 S2.6 (`OnDemandActivitiesModeHandler`).
- **Owned paths:**
  - `Services/WoWStateManager/Coordination/PortServiceCoordinator.cs`
  - `Services/WoWStateManager/Coordination/WarlockSummonCoordinator.cs`
- **Read-only paths:** `Spec/23_ONDEMAND_API.md §5` (whisper DSL), `IActivityCatalog.social.mage-port` + `social.warlock-summon` rows (Plan/13 S9.6).
- **Spec contracts:** [`Spec/23 §5`](../Spec/23_ONDEMAND_API.md#5-shodan-whisper-parser), [`Spec/02_STATEMANAGER.md#ondemand-activity-launcher`](../Spec/02_STATEMANAGER.md#ondemand-activity-launcher).
- **Goal:** Both Activities accept a human's whisper request `!port <city>` / `!summon`. The launcher spawns a single mage (port) or pair of warlock+supporter (summon), travels to the human's position via teleport, casts the port / summon, and dismisses.
- **Procedure:**
  1. `PortServiceCoordinator` implements `IActivityCoordinator`; on launch, reserves 1 mage from the pool, sets `AssignedActivity="social.mage-port"`, teleports to human, casts portal spell, dismisses.
  2. `WarlockSummonCoordinator` similar but with 2 bots (warlock + supporter); summons human across realm.
  3. Both Activities are dispatched via the Plan/03 S2.6 whisper handler → S2.5 launcher.
- **Success criteria:** `OnDemand_MagePort_SocialService` LiveValidation green; portal spell visible to a test human within 60 s of whisper.
- **Failure modes:** pool lacks mage / warlock+supporter → `RejectionCode.POOL_EXHAUSTED`; human cross-realm → `HUMAN_CROSS_FACTION_NO_PATH`.
- **ML integration sub-bullet:** consumes Plan/14 S10.10 activity_request advisor via Plan/03 S2.6 for `!port <city>` disambiguation. No new advisor.

### S11.10 — Whisper response handler

- **Owner:** `monorepo-worker`
- **Status:** open
- **Depends on:** S11.1, S11.2
- **Owned paths:**
  - `Exports/BotRunner/Social/WhisperReplyHandler.cs`
  - `Exports/BotRunner/Social/WhisperIntentClassifier.cs` (Phase-1 heuristic classifier; see ML sub-bullet)
- **Read-only paths:** `Spec/21 §6` (SLA), `Spec/24` `WhisperReplyDelayMs`, `IChatGenerator` (S11.1), `IFriendList` / `IGuildRoster` for friend/guild classification.
- **Spec contracts:** [`Spec/21 §6`](../Spec/21_SOCIAL_FABRIC.md#6-whisper-reactivity).
- **Goal:** Subscribe to `SMSG_MESSAGECHAT` whisper events; classify intent (friendly / stranger / hostile); reply within the SLA from Spec/21 §6, using personality jitter from `Spec/24.WhisperReplyDelayMs`. Shodan-routed whispers bypass this handler (they go to `OnExternalActivityRequestAsync` per Plan/03 S2.6).
- **Procedure:**
  1. Subscribe to `SMSG_MESSAGECHAT` via `NetworkChatFrame` (S11.2).
  2. `WhisperIntentClassifier.Classify(whisperText, sender)` returns one of `Friendly | Stranger | Hostile` per the heuristic decision table (friend-list/guild → Friendly; keyword-deny → Hostile; else Stranger).
  3. Build `ChatContext { TriggerKind = "whisper-reply-" + intent.ToLower() }`; call `IChatGenerator.GeneratePlanAsync` to pick a template.
  4. Schedule the reply with `PersonalityProfile.WhisperReplyDelayMs` jitter; bound by SLA: 5-30 s friendly, 10-60 s stranger, never reply hostile.
  5. Apply `ChatPostFilter` (S11.12) on the resolved text before sending.
- **Success criteria:** `SocialFabricContractTests.WhisperReply_RespondsWithinSla` green; intent classification accuracy on a sample set ≥90% (Phase 1 heuristic; Phase 3 ML target higher).
- **Failure modes:** SLA missed → drop the reply (no retry); `_denylist.txt` trip → fall back to a fixed-safe template.
- **ML integration sub-bullet:** Phase 1 intent classifier = friend-list + keyword-deny heuristic (this slot). Phase 2 rules at `Config/decision-engine/whisper-intent-rules.json`. Phase 3 = LEARNED intent classifier — trained on labeled `whisper-reply` traces; **fits inside the existing Spec/20 §2.2 chat_template advisor** by extending `ChatTemplateContext.trigger_kind` granularity (more `whisper-reply-*` sub-kinds). No new Spec/20 RPC needed.

### S11.11 — Per-bot post-budget enforcement

- **Owner:** `monorepo-worker`
- **Status:** open
- **Owned paths:**
  - `Exports/BotRunner/Social/PostBudgetTracker.cs`
  - `Exports/BotRunner/SnapshotBuilder.cs` (extend for `chat_post_budgets` field 38 per Spec/21 §9)
- **Read-only paths:** `Spec/21 §3.2`, `Spec/24 ChattyLevel` (variance ±2 per Spec/21).
- **Spec contracts:** [`Spec/21 §3.2`](../Spec/21_SOCIAL_FABRIC.md#32-message-rate-budget), [`Spec/21 §9`](../Spec/21_SOCIAL_FABRIC.md#9-snapshot-projection).
- **Goal:** Rolling-hour budget per bot per channel. Tasks that emit chat consult the tracker before posting. Tracker is bot-local (no IPC). Projects per-channel state to `WoWActivitySnapshot.chat_post_budgets[]` (field 38).
- **Procedure:**
  1. `PostBudgetTracker` keeps a per-channel `Queue<DateTime>` of recent post times; `CanPost(channel)` returns true if `count < cap` within the rolling hour.
  2. Caps: Trade=4, LFG=2, General=0, Guild=6, Whisper=∞ — adjusted by `Spec/24.ChattyLevel` ±2 per bot.
  3. `RecordPost(channel)` enqueues `now`; on each call, expire entries older than 1 hour.
  4. `SnapshotBuilder` reads from tracker and emits `ChatPostBudget` messages on field 38.
- **Success criteria:** `SocialFabricContractTests.TradeChat_RespectsPerBotHourlyBudget` green over a 1-hour simulated session; ≤4 trade posts.
- **Failure modes:** clock skew (system clock jumped back) → entries expire correctly when clock recovers; transient over-cap impossible by construction (CanPost checked before RecordPost).
- **ML integration sub-bullet:** none — budget is a hard rate-limit, not a decision point.

### S11.12 — Anti-griefing denylist

- **Owner:** `monorepo-worker`
- **Status:** open
- **Owned paths:**
  - `Bot/chat-templates/_denylist.txt`
  - `Exports/BotRunner/Social/ChatPostFilter.cs`
- **Read-only paths:** `Spec/21 §8`, `Spec/21 §10` (failure-reason mapping).
- **Spec contracts:** [`Spec/21 §8`](../Spec/21_SOCIAL_FABRIC.md#8-anti-griefing), [`Spec/21 §10`](../Spec/21_SOCIAL_FABRIC.md#10-failure-reason-mapping).
- **Goal:** Regex denylist post-filter applied AFTER template substitution but BEFORE the wire send. Hand-curated list of forbidden phrases (slurs, advertising patterns, server-name leaks). PR-review gate documented in `Bot/chat-templates/README.md` (owned by S11.1).
- **Procedure:**
  1. `_denylist.txt` is one regex per line (Perl-compatible; case-insensitive); blank lines and `#`-prefixed comments ignored.
  2. `ChatPostFilter.IsClean(text)` compiles the regex set once at boot; hot-reloads on file change.
  3. On trip, return `false` AND emit `FailureReason.chat_denylist_rejection` (Spec/12 follow-up); calling task drops the post + falls back to the deterministic template-library entry.
  4. Document the PR-review gate: changes to `_denylist.txt` require ≥1 reviewer with a "Social" CODEOWNERS tag.
- **Success criteria:** `SocialFabricContractTests.ChatTemplate_DenylistTripFallsBackToTemplate` green; regex compile errors caught at boot (test enforces).
- **Failure modes:** regex compile fail → boot-time error; service refuses to start (FAIL-FAST per the framework's principle, NOT silent fallback).
- **ML integration sub-bullet:** none — denylist is hard-rejection, not advisory.

### S11.13 — LiveValidation suite

- **Owner:** `monorepo-test-runner`
- **Status:** open
- **Depends on:** S11.1..S11.12
- **Owned paths:**
  - `Tests/BotRunner.Tests/LiveValidation/SocialFabric/` (new folder)
- **Read-only paths:** all S11.1-S11.12 implementations, `Spec/21`.
- **Spec contracts:** [`Spec/13_TESTING.md`](../Spec/13_TESTING.md), [`Spec/21 §14`](../Spec/21_SOCIAL_FABRIC.md#14-test-surface).
- **Goal:** Land six LiveValidation tests:
  - `SocialFabricTests.TradeChat_RespectsPerBotHourlyBudget` — 1-hour session; assert via `snapshot.chat_post_budgets[Trade]`.
  - `SocialFabricTests.MailFlow_AlternatesBetweenAccounts` — alt mail traffic over a 30-min session; assert via `snapshot.pending_mail_count` on alt monotonically rising.
  - `SocialFabricTests.GuildDinged_PostsOncePerLevel` — level-up event fires exactly one guild post.
  - `SocialFabricTests.WhisperReply_RespondsWithinSla` — friendly + stranger paths.
  - `SocialFabricTests.MagePort_DeliversHumanToCity` — full OnDemand flow.
  - `SocialFabric_DynamicProgressive_TradeChatBuyersConvertTest` — Spec/21 §12 invariant (≥10% wts → trade conversion).
- **Procedure:**
  1. Each test stages via `LiveBotFixture.StageBotRunner*Async` per Test Isolation Rules.
  2. `Westworld-Test` accelerated timers used for 1-hour budget test.
  3. Trace each run to `tmp/test-runtime/traces/SocialFabric_*/` for off-line Spec/20 §6 consumption.
- **Success criteria:** all 6 tests green; traces produced.
- **Failure modes:** Plan/14 S10.9 advisor down → tests pass with NoAdvice fallback (verify the fall-soft path).
- **ML integration sub-bullet:** The `SocialFabric_DynamicProgressive_TradeChatBuyersConvertTest` is the correctness guard that any chat-template ML pass cannot break.

## Dynamic-progressive invariant

Per [`Spec/21 §12`](../Spec/21_SOCIAL_FABRIC.md#12-dynamic-progressive-invariant),
Phase 11's social-fabric output MUST satisfy:

1. **Dynamic.** Two bots with the same `(channel, trigger_kind)` but
   different `(class, level, faction, chattiness, AccountName)` MUST
   sometimes emit different template choices. Identical inputs produce
   identical outputs (deterministic given fixed `mode_used`).
2. **Progressive.** Chat is a *progression* surface, not decoration —
   ≥10% of `wts` posts in any production-grade trace MUST produce a
   buyer-whisper-then-trade outcome (inventory → gold conversion);
   `lfg-seeking` posts MUST produce a group invite at observable rate
   (≥5% per trace); cosmetic chatter alone fails the test.

Asserted at slot S11.13 via
`SocialFabric_DynamicProgressive_TradeChatBuyersConvertTest`.

## ML integration umbrella

Plan/15 consumes Plan/14 S10.9 (chat_template advisor) for template
selection across three Activity families:

- `social.trade-chat-cycle` (S11.5) — picks WTS / WTB templates.
- `social.lfg-cycle` (S11.6) — picks `lfg-seeking` template variants.
- Whisper response handler (S11.10) — picks `whisper-reply-friendly` /
  `whisper-reply-stranger` template variants.

**Whisper-intent classification** (tracker ML hook for this row) is a
sub-step inside S11.10 that produces `ChatTemplateContext.trigger_kind`.
Phase 1 = friend-list + keyword heuristic. Phase 3 = learned classifier
trained on labeled whisper traces. The classifier output feeds the
existing Spec/20 §2.2 advisor — no new RPC.

Off-line tools consuming the Spec/20 §6 trace pipeline:

- Template-engagement scorer ranks which templates produce the most
  buyer-whisper / group-invite outcomes; used to prune unused templates
  and prioritize new ones.
- Whisper-intent labeler bootstraps the Phase-3 classifier training
  set from operator-confirmed traces.

## Plan-slot cross-reference

| Slot | Spec contracts |
|---|---|
| S11.1 | [`Spec/21 §3.3`](../Spec/21_SOCIAL_FABRIC.md#33-message-generator), [`Spec/20 §2.2`](../Spec/20_DECISION_ENGINE.md#22-chattemplate-advisor) |
| S11.2 | [`Spec/21 §3.1`](../Spec/21_SOCIAL_FABRIC.md#31-channels-in-scope-1121) |
| S11.3 | [`Spec/21 §4`](../Spec/21_SOCIAL_FABRIC.md#4-mail) |
| S11.4 | [`Spec/03_BOTRUNNER.md`](../Spec/03_BOTRUNNER.md), [`Spec/21 §4.1`](../Spec/21_SOCIAL_FABRIC.md#41-mail-traffic-profile) |
| S11.5 | [`Spec/21 §3`](../Spec/21_SOCIAL_FABRIC.md#3-chat-output), [`Spec/19 §4`](../Spec/19_AOTA_RUNTIME.md#4-objectivetype-enum-closed-set-proto-mirrored) |
| S11.6 | [`Spec/21 §3.1`](../Spec/21_SOCIAL_FABRIC.md#31-channels-in-scope-1121) |
| S11.7 | [`Spec/21 §5`](../Spec/21_SOCIAL_FABRIC.md#5-guild-events) |
| S11.8 | [`Spec/21 §7`](../Spec/21_SOCIAL_FABRIC.md#7-city-ambient-traffic) |
| S11.9 | [`Spec/23 §5`](../Spec/23_ONDEMAND_API.md#5-shodan-whisper-parser), [`Spec/02`](../Spec/02_STATEMANAGER.md) |
| S11.10 | [`Spec/21 §6`](../Spec/21_SOCIAL_FABRIC.md#6-whisper-reactivity), [`Spec/20 §2.2`](../Spec/20_DECISION_ENGINE.md#22-chattemplate-advisor) |
| S11.11 | [`Spec/21 §3.2`](../Spec/21_SOCIAL_FABRIC.md#32-message-rate-budget), [`Spec/21 §9`](../Spec/21_SOCIAL_FABRIC.md#9-snapshot-projection) |
| S11.12 | [`Spec/21 §8`](../Spec/21_SOCIAL_FABRIC.md#8-anti-griefing), [`Spec/21 §10`](../Spec/21_SOCIAL_FABRIC.md#10-failure-reason-mapping) |
| S11.13 | [`Spec/21 §14`](../Spec/21_SOCIAL_FABRIC.md#14-test-surface), [`Spec/13`](../Spec/13_TESTING.md) |
| (Plan/14 S10.9) | [`Spec/20 §2.2`](../Spec/20_DECISION_ENGINE.md#22-chattemplate-advisor) advisor wire that S11.5/S11.6/S11.10 consume |

## Failure recovery

- **Chat channel full** → bot retries channel join with exponential backoff (cap 5 min). Failure surfaces in snapshot `RecentErrors`.
- **Mail send fails** (recipient does not exist / banned name) → task fails as `FailureReason.MailRecipientInvalid`; bot abandons that mail item, banks instead.
- **Whisper response timeout** → bot drops the reply (no retry; the SLA window already expired).
- **Anti-griefing denylist trips** during PromptHandlingService completion → bot falls back to the hand-authored template (always permitted) and logs the trip for review.

## Related specs

- [`Spec/21_SOCIAL_FABRIC.md`](../Spec/21_SOCIAL_FABRIC.md) — contract.
- [`Spec/24_BEHAVIORAL_VARIATION.md`](../Spec/24_BEHAVIORAL_VARIATION.md) — personality knobs that consume this fabric.
- [`Plan/13_PHASE9_CATALOG_FILL.md`](13_PHASE9_CATALOG_FILL.md) — adds the `social.*` catalog rows that this phase fills with task implementations.
