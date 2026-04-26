# Task Archive

Completed items moved from TASKS.md.

## Archived Snapshot (2026-04-26) - Foreground Trade Shodan Stabilization

- [x] Stabilize foreground trade action dispatch under Shodan trade validation.
- Completion notes:
  - Foreground trade actions now route through object-manager async helpers and a popup-aware `FgTradeFrame`, closing the earlier `DeclineTrade`, `OfferItem`, and `AcceptTrade` foreground ACK failures.
  - `TradeParityTests` now keeps foreground cancel and foreground-initiated item/gold transfer active under Shodan.
  - `TradingTests.Trade_GoldAndItem_TransferSuccessful` remains an explicit tracked skip because BG-initiated transfer actions all ACK `Success`, but the server leaves item/copper with the BG initiator.
- Validation:
  - `dotnet test Tests/WoWSharpClient.Tests/WoWSharpClient.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~TradeNetworkClientComponentTests" --logger "console;verbosity=minimal"` -> `passed (48/48)`
  - `dotnet test Tests/ForegroundBotRunner.Tests/ForegroundBotRunner.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~ForegroundInteractionFrameTests.TradeFrame_UsesLuaVisibilityAndRoutesTradeActionsThroughExpectedLua" --logger "console;verbosity=minimal"` -> `passed (1/1)`
  - Safety bundle -> `passed (33/33)`
  - Dispatch/readiness bundle -> `passed (60/60)`
  - `trading_fg_shodan_final.trx` -> `passed (3), skipped (1)`
  - `fishing_shodan_anchor.trx` -> known Ratchet anchor failure on FG `loot_window_timeout` / `max_casts_reached`

## Archived Snapshot (2026-04-25) - Foreground Mail Shodan Stabilization

- [x] Stabilize foreground `CollectAllMailAsync(...)` under combined-suite Shodan mail validation.
- Completion notes:
  - Foreground mailbox collection now waits through the delayed client mailbox refresh window instead of ending after visible stale inbox rows expose no ready money/items.
  - Unread empty-looking rows are not deleted, SOAP money/item mail staging has a delivery-settle delay, and BotRunner emits `[MAIL-COLLECT]` diagnostic markers backed by `MailCollectionResult`.
  - `MailSystemTests` and `MailParityTests` now dispatch `ActionType.CheckMail` to both FG and BG in the Shodan topology and passed the full mail suite.
- Validation:
  - `dotnet test Tests/ForegroundBotRunner.Tests/ForegroundBotRunner.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~ForegroundInteractionFrameTests.CollectInboxAttachmentsLua|FullyQualifiedName~ForegroundInteractionFrameTests.DeleteEmptyInboxItemsLua|FullyQualifiedName~ForegroundInteractionFrameTests.WaitForInboxPendingAttachmentsAsync" --logger "console;verbosity=minimal"` -> `passed (3/3)`
  - `dotnet build Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false -v:minimal` -> `passed (0 errors; existing warnings)`
  - `$env:WWOW_DATA_DIR='D:/MaNGOS/data'; dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~MailSystemTests|FullyQualifiedName~MailParityTests" --logger "console;verbosity=normal" --results-directory "tmp/test-runtime/results-live" --logger "trx;LogFileName=mail_fg_shodan_director_extendedpoll.trx"` -> `passed (4/4)`
  - Safety bundle -> `passed (33/33)`
  - Dispatch/readiness bundle -> `passed (60/60)`

## Archived Snapshot (2026-04-09) - Realm Wizard No-Sweep Stabilization

- [x] Replaced realm-wizard Lua fallback sweeps with state-based, named-control actions for English selection, realm suggestion, and suggestion confirmation.
- [x] Kept realm-wizard handoff detection state-based (`charselect`) so empty character lists are treated as valid post-realm transitions.
- [x] Revalidated dedicated FG new-account/new-character live flow across repeated runs (`fg_new_account_flow_latest/rerun1/rerun2/no_sweep.trx`).

## Archived Snapshot (2026-03-23) - Services/ForegroundBotRunner/TASKS.md

- [x] Finish the remaining FG runtime parity surfaces that still inherited defaults: `QuestGreetingFrame`, `TradeFrame`, and the task-owned bank/AH/craft helper methods.

## Archived Snapshot (2026-02-24 19:43:32) - Services/ForegroundBotRunner/TASKS.md

- [x] Ensure FG death/ghost state detection is stable enough for corpse-run parity.
- [x] Implement missing descriptor-backed life-state fields (`WoWPlayer.PlayerFlags`, `WoWPlayer.Bytes/Bytes3`, `WoWUnit.Bytes0/1/2`) used by `ActivitySnapshot`.
- [x] Reduce remaining Lua-only FG life-state paths (`LocalPlayer.InGhostForm`, reclaim-delay fallbacks) now that descriptor fields are available.
- [x] Implement descriptor-backed FG `WoWPlayer.QuestLog` reads so quest log slots flow into snapshots.
- [x] Guarantee non-null `PathfindingClient` injection into FG `ClassContainer`.
