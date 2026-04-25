# BotRunner Tasks

## Scope
- Project: `Exports/BotRunner`
- Owns task orchestration for corpse-run, combat, gathering, questing, and shared navigation execution loops.
- Master tracker: `docs/TASKS.md`

## Execution Rules
1. Work the highest-signal unchecked task unless a blocker is recorded.
2. Keep live validation bounded and repo-scoped; never blanket-kill `dotnet` or `WoW.exe`.
3. Every navigation delta must land with focused deterministic tests before the next slice.
4. Update this file plus `docs/TASKS.md` in the same session as any shipped BotRunner delta.
5. `Session Handoff` must record `Pass result`, exact validation commands, files changed, and exactly one executable `Next command`.

## Environment Checklist
- [x] `Exports/BotRunner/BotRunner.csproj` builds in `Release`.
- [x] `Tests/BotRunner.Tests` targeted filters run without restore.
- [x] Repo-scoped cleanup commands are available.

## Active Tasks

### BR-NAV-006 Prove path ownership through combat and movement-controller handoff
Known remaining work in this owner: `0` items.
- [x] BG corpse-run live recording now persists the active `RetrieveCorpseTask` corridor snapshot to `navtrace_<account>.json`, and `DeathCorpseRunTests` asserts that the sidecar captured `RecordedTask=RetrieveCorpseTask`, `TaskStack=[RetrieveCorpseTask, IdleTask]`, and a non-null `TraceSnapshot`.
- [x] Session 188 redirect parity test proved FG/BG matched pause/resume packet timing with `Parity_Durotar_RoadPath_Redirect`. BG `SET_FACING` fix shipped so both clients emit `MSG_MOVE_SET_FACING` on mid-route direction changes.
- [x] Final live proof bundle (session 188): forced-turn Durotar, redirect, combat auto-attack, and corpse-run reclaim all pass on the same DLL baseline.

## Simple Command Set
1. `dotnet build Exports/BotRunner/BotRunner.csproj --configuration Release --no-restore`
2. `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore --filter "FullyQualifiedName~NavigationPathTests|FullyQualifiedName~GatheringRouteTaskTests" --logger "console;verbosity=minimal"`
3. `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore --filter "FullyQualifiedName~GatheringProfessionTests.Mining_GatherCopperVein_SkillIncreases" --blame-hang --blame-hang-timeout 10m --logger "console;verbosity=minimal"`
4. `powershell -ExecutionPolicy Bypass -File .\\run-tests.ps1 -CleanupRepoScopedOnly`

## Session Handoff
### 2026-04-25 (Trading Shodan migration observation)
- Pass result: `No BotRunner production code changed; deterministic dispatch coverage stayed green and migrated Trading/TradeParity live validation passed 1 with 3 tracked foreground trade action skips`
- Last delta:
  - `TradingTests` now dispatches only trade `ActionType` messages after Shodan-directed trade staging; the BG offer/decline cancel proof passed.
  - `TradeParityTests` and the item/gold transfer path remain explicit skips after Shodan launch/resolve because the foreground trade runtime currently ACKs `DeclineTrade`, `OfferItem`, and `AcceptTrade` as `Failed/behavior_tree_failed`.
- Validation/tests run:
  - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~FishingPoolActivationAnalyzerTests|FullyQualifiedName~LiveBotFixtureBotChatTests|FullyQualifiedName~GatheringRouteSelectionTests|FullyQualifiedName~BotRunnerServiceFishingDispatchTests" --logger "console;verbosity=minimal"` -> `passed (33/33)`.
  - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~ActionForwardingContractTests|FullyQualifiedName~BotRunnerServiceSnapshotTests|FullyQualifiedName~BotRunnerServiceFishingDispatchTests" --logger "console;verbosity=minimal"` -> `passed (60/60)`.
  - `$env:WWOW_DATA_DIR='D:/MaNGOS/data'; dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~TradingTests|FullyQualifiedName~TradeParityTests" --logger "console;verbosity=normal" --results-directory "tmp/test-runtime/results-live" --logger "trx;LogFileName=trading_shodan_final.trx"` -> `1 passed, 3 skipped`.
- Files changed:
  - `Exports/BotRunner/TASKS.md`
- Next command: `powershell -ExecutionPolicy Bypass -File .\run-tests.ps1 -CleanupRepoScopedOnly; rg -n "BotLearnSpellAsync|BotSetSkillAsync|BotAddItemAsync|BotTeleportAsync|SendGmChatCommand|ExecuteGMCommand|\\.learn|\\.additem|\\.setskill|\\.tele|\\.go|\\.send|modify money" Tests/BotRunner.Tests/LiveValidation/GossipQuestTests.cs Tests/BotRunner.Tests/LiveValidation/QuestObjectiveTests.cs Tests/BotRunner.Tests/LiveValidation/QuestInteractionTests.cs Tests/BotRunner.Tests/LiveValidation/StarterQuestTests.cs`

### 2026-04-25 (Mail Shodan migration observation)
- Pass result: `No BotRunner production code changed; deterministic dispatch coverage stayed green and migrated MailSystem/MailParity live validation passed 4/4`
- Last delta:
  - `MailSystemTests` and `MailParityTests` now dispatch only `ActionType.CheckMail` after Shodan-directed mailbox and SOAP mail staging.
  - The committed mail parity shape is BG-action-only: FG launches for topology parity, but foreground `CheckMail` collection under combined-suite load is tracked as a ForegroundBotRunner follow-up.
- Validation/tests run:
  - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~FishingPoolActivationAnalyzerTests|FullyQualifiedName~LiveBotFixtureBotChatTests|FullyQualifiedName~GatheringRouteSelectionTests|FullyQualifiedName~BotRunnerServiceFishingDispatchTests" --logger "console;verbosity=minimal"` -> `passed (33/33)`.
  - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~ActionForwardingContractTests|FullyQualifiedName~BotRunnerServiceSnapshotTests|FullyQualifiedName~BotRunnerServiceFishingDispatchTests" --logger "console;verbosity=minimal"` -> `passed (60/60)`.
  - `$env:WWOW_DATA_DIR='D:/MaNGOS/data'; dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~MailSystemTests|FullyQualifiedName~MailParityTests" --logger "console;verbosity=normal" --results-directory "tmp/test-runtime/results-live" --logger "trx;LogFileName=mail_shodan_bgonly.trx"` -> `passed (4/4)`.
- Files changed:
  - `Exports/BotRunner/TASKS.md`
- Next command: `powershell -ExecutionPolicy Bypass -File .\run-tests.ps1 -CleanupRepoScopedOnly; rg -n "BotLearnSpellAsync|BotSetSkillAsync|BotAddItemAsync|BotTeleportAsync|SendGmChatCommand|ExecuteGMCommand|\\.learn|\\.additem|\\.setskill|\\.tele|\\.go|\\.send|modify money" Tests/BotRunner.Tests/LiveValidation/TradingTests.cs Tests/BotRunner.Tests/LiveValidation/TradeParityTests.cs`

### 2026-04-25 (EconomyInteraction Shodan migration observation)
- Pass result: `No BotRunner production code changed; deterministic dispatch coverage stayed green and migrated EconomyInteraction live validation passed 3/3`
- Last delta:
  - `EconomyInteractionTests` now dispatches only `ActionType.InteractWith` for banker/auctioneer and `ActionType.CheckMail` for mailbox collection after Shodan-directed staging.
  - FG and BG both passed the bank, AH, and mail interaction baselines.
- Validation/tests run:
  - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~FishingPoolActivationAnalyzerTests|FullyQualifiedName~LiveBotFixtureBotChatTests|FullyQualifiedName~GatheringRouteSelectionTests|FullyQualifiedName~BotRunnerServiceFishingDispatchTests" --logger "console;verbosity=minimal"` -> `passed (33/33)`.
  - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~ActionForwardingContractTests|FullyQualifiedName~BotRunnerServiceSnapshotTests|FullyQualifiedName~BotRunnerServiceFishingDispatchTests" --logger "console;verbosity=minimal"` -> `passed (60/60)`.
  - `$env:WWOW_DATA_DIR='D:/MaNGOS/data'; dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~EconomyInteractionTests" --logger "console;verbosity=normal" --results-directory "tmp/test-runtime/results-live" --logger "trx;LogFileName=economy_interaction_shodan.trx"` -> `passed (3/3)`.
- Files changed:
  - `Exports/BotRunner/TASKS.md`
- Next command: `powershell -ExecutionPolicy Bypass -File .\run-tests.ps1 -CleanupRepoScopedOnly; rg -n "BotLearnSpellAsync|BotSetSkillAsync|BotAddItemAsync|BotTeleportAsync|SendGmChatCommand|ExecuteGMCommand|\\.learn|\\.additem|\\.setskill|\\.tele|\\.go|\\.send|modify money" Tests/BotRunner.Tests/LiveValidation/MailSystemTests.cs Tests/BotRunner.Tests/LiveValidation/MailParityTests.cs`

### 2026-04-25 (VendorBuySell Shodan migration observation)
- Pass result: `No BotRunner production code changed; deterministic dispatch coverage stayed green and migrated VendorBuySell live validation passed 2/2`
- Last delta:
  - `VendorBuySellTests` now dispatches only `ActionType.BuyItem`, `ActionType.SellItem`, and post-buy `DestroyItem` cleanup after Shodan-directed vendor/item/money staging.
  - The suite remains a BG vendor packet baseline; FG is launched for Shodan topology parity but does not receive vendor buy/sell actions in this slice.
- Validation/tests run:
  - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~FishingPoolActivationAnalyzerTests|FullyQualifiedName~LiveBotFixtureBotChatTests|FullyQualifiedName~GatheringRouteSelectionTests|FullyQualifiedName~BotRunnerServiceFishingDispatchTests" --logger "console;verbosity=minimal"` -> `passed (33/33)`.
  - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~ActionForwardingContractTests|FullyQualifiedName~BotRunnerServiceSnapshotTests|FullyQualifiedName~BotRunnerServiceFishingDispatchTests" --logger "console;verbosity=minimal"` -> `passed (60/60)`.
  - `$env:WWOW_DATA_DIR='D:/MaNGOS/data'; dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~VendorBuySellTests" --logger "console;verbosity=normal" --results-directory "tmp/test-runtime/results-live" --logger "trx;LogFileName=vendor_buy_sell_shodan.trx"` -> `passed (2/2)`.
- Files changed:
  - `Exports/BotRunner/TASKS.md`
- Next command: `powershell -ExecutionPolicy Bypass -File .\run-tests.ps1 -CleanupRepoScopedOnly; rg -n "BotLearnSpellAsync|BotSetSkillAsync|BotAddItemAsync|BotTeleportAsync|SendGmChatCommand|\\.learn|\\.additem|\\.setskill|\\.tele|modify money" Tests/BotRunner.Tests/LiveValidation/EconomyInteractionTests.cs`

### 2026-04-25 (Bank Shodan migration observation)
- Pass result: `No BotRunner production code changed; deterministic dispatch coverage stayed green and migrated bank live validation passed 1 with 3 tracked skips`
- Last delta:
  - `BankInteractionTests` now dispatches only `ActionType.InteractWith` after Shodan-directed bank staging. Banker detection passes on FG/BG and the implemented banker interaction returns success.
  - `BankParityTests` now stages Linen Cloth through `StageBotRunnerLoadoutAsync`; deposit/withdraw and bank-slot purchase remain explicit missing-action skips because BotRunner has no bank deposit/withdraw/slot-purchase action surface yet.
- Validation/tests run:
  - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~FishingPoolActivationAnalyzerTests|FullyQualifiedName~LiveBotFixtureBotChatTests|FullyQualifiedName~GatheringRouteSelectionTests|FullyQualifiedName~BotRunnerServiceFishingDispatchTests" --logger "console;verbosity=minimal"` -> `passed (33/33)`.
  - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~ActionForwardingContractTests|FullyQualifiedName~BotRunnerServiceSnapshotTests|FullyQualifiedName~BotRunnerServiceFishingDispatchTests" --logger "console;verbosity=minimal"` -> `passed (60/60)`.
  - `$env:WWOW_DATA_DIR='D:/MaNGOS/data'; dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~BankInteractionTests|FullyQualifiedName~BankParityTests" --logger "console;verbosity=normal" --results-directory "tmp/test-runtime/results-live" --logger "trx;LogFileName=bank_shodan.trx"` -> `1 passed, 3 skipped`.
- Files changed:
  - `Exports/BotRunner/TASKS.md`
- Next command: `powershell -ExecutionPolicy Bypass -File .\run-tests.ps1 -CleanupRepoScopedOnly; rg -n "BotLearnSpellAsync|BotSetSkillAsync|BotAddItemAsync|BotTeleportAsync|SendGmChatCommand|\\.learn|\\.additem|\\.setskill|\\.tele" Tests/BotRunner.Tests/LiveValidation/VendorBuySellTests.cs`

### 2026-04-25 (AuctionHouse Shodan migration observation)
- Pass result: `No BotRunner production code changed; deterministic dispatch coverage stayed green and migrated AuctionHouse live validation passed 3 with 2 tracked skips`
- Last delta:
  - `AuctionHouseTests` now dispatches only `ActionType.InteractWith` after Shodan-directed AH staging. FG and BG auctioneer interactions return success.
  - `AuctionHouseParityTests` now stages Linen Cloth through `StageBotRunnerLoadoutAsync`; post/buy and cancel remain explicit missing-action skips because BotRunner has no auction post/buy/cancel action surface yet.
- Validation/tests run:
  - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~FishingPoolActivationAnalyzerTests|FullyQualifiedName~LiveBotFixtureBotChatTests|FullyQualifiedName~GatheringRouteSelectionTests|FullyQualifiedName~BotRunnerServiceFishingDispatchTests" --logger "console;verbosity=minimal"` -> `passed (33/33)`.
  - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~ActionForwardingContractTests|FullyQualifiedName~BotRunnerServiceSnapshotTests|FullyQualifiedName~BotRunnerServiceFishingDispatchTests" --logger "console;verbosity=minimal"` -> `passed (60/60)`.
  - `$env:WWOW_DATA_DIR='D:/MaNGOS/data'; dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~AuctionHouseTests|FullyQualifiedName~AuctionHouseParityTests" --logger "console;verbosity=normal" --results-directory "tmp/test-runtime/results-live" --logger "trx;LogFileName=auction_house_shodan.trx"` -> `3 passed, 2 skipped`.
- Files changed:
  - `Exports/BotRunner/TASKS.md`
- Next command: `powershell -ExecutionPolicy Bypass -File .\run-tests.ps1 -CleanupRepoScopedOnly; rg -n "BotLearnSpellAsync|BotSetSkillAsync|BotAddItemAsync|BotTeleportAsync|SendGmChatCommand|\\.learn|\\.additem|\\.setskill|\\.tele" Tests/BotRunner.Tests/LiveValidation/BankInteractionTests.cs Tests/BotRunner.Tests/LiveValidation/BankParityTests.cs`

### 2026-04-25 (PetManagement Shodan migration observation)
- Pass result: `No BotRunner production code changed; deterministic dispatch coverage stayed green and the migrated PetManagement live slice passed`
- Last delta:
  - `PetManagementTests` now dispatches `ActionType.CastSpell` only after Shodan-directed hunter pet setup. BG Call Pet and Dismiss Pet both return success.
  - FG remains launched but idle in this slice because foreground spell-id casting is not the validated pet-management path.
- Validation/tests run:
  - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~FishingPoolActivationAnalyzerTests|FullyQualifiedName~LiveBotFixtureBotChatTests|FullyQualifiedName~GatheringRouteSelectionTests|FullyQualifiedName~BotRunnerServiceFishingDispatchTests" --logger "console;verbosity=minimal"` -> `passed (33/33)`.
  - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~ActionForwardingContractTests|FullyQualifiedName~BotRunnerServiceSnapshotTests|FullyQualifiedName~BotRunnerServiceFishingDispatchTests" --logger "console;verbosity=minimal"` -> `passed (60/60)`.
  - `$env:WWOW_DATA_DIR='D:/MaNGOS/data'; dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~PetManagementTests" --logger "console;verbosity=normal" --results-directory "tmp/test-runtime/results-live" --logger "trx;LogFileName=pet_management_shodan.trx"` -> `passed (1/1)`.
- Files changed:
  - `Exports/BotRunner/TASKS.md`
- Next command: `powershell -ExecutionPolicy Bypass -File .\run-tests.ps1 -CleanupRepoScopedOnly; rg -n "BotLearnSpellAsync|BotSetSkillAsync|BotAddItemAsync|BotTeleportAsync|SendGmChatCommand|\\.learn|\\.additem|\\.setskill|\\.tele" Tests/BotRunner.Tests/LiveValidation/AuctionHouseTests.cs Tests/BotRunner.Tests/LiveValidation/AuctionHouseParityTests.cs`

### 2026-04-25 (Crafting Shodan migration observation)
- Pass result: `No BotRunner production code changed; deterministic dispatch coverage stayed green and the migrated Crafting live slice passed`
- Last delta:
  - `CraftingProfessionTests` now dispatches `ActionType.CastSpell` only after Shodan-directed First Aid staging. BG crafting produces one Linen Bandage from one Linen Cloth.
  - FG remains launched but idle in this slice because foreground spell-id casting is not the validated crafting path.
- Validation/tests run:
  - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~FishingPoolActivationAnalyzerTests|FullyQualifiedName~LiveBotFixtureBotChatTests|FullyQualifiedName~GatheringRouteSelectionTests|FullyQualifiedName~BotRunnerServiceFishingDispatchTests" --logger "console;verbosity=minimal"` -> `passed (33/33)`.
  - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~ActionForwardingContractTests|FullyQualifiedName~BotRunnerServiceSnapshotTests|FullyQualifiedName~BotRunnerServiceFishingDispatchTests" --logger "console;verbosity=minimal"` -> `passed (60/60)`.
  - `$env:WWOW_DATA_DIR='D:/MaNGOS/data'; dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~CraftingProfessionTests" --logger "console;verbosity=normal" --results-directory "tmp/test-runtime/results-live" --logger "trx;LogFileName=crafting_shodan.trx"` -> `passed (1/1)`.
- Files changed:
  - `Exports/BotRunner/TASKS.md`
- Next command: `powershell -ExecutionPolicy Bypass -File .\run-tests.ps1 -CleanupRepoScopedOnly; rg -n "BotLearnSpellAsync|BotSetSkillAsync|BotAddItemAsync|BotTeleportAsync|SendGmChatCommand|\\.learn|\\.additem|\\.setskill|\\.tele" Tests/BotRunner.Tests/LiveValidation/PetManagementTests.cs`

### 2026-04-25 (Gathering Shodan migration observation)
- Pass result: `No BotRunner production code changed; deterministic dispatch coverage stayed green and the migrated Gathering live slice documents a foreground mining gap`
- Last delta:
  - `GatheringProfessionTests` now dispatches `ActionType.StartGatheringRoute` only after Shodan-directed staging. BG mining and herbalism pass on the corrected route center.
  - FG mining receives the action and moves around active copper candidates, but never reports gather success, bag delta, or skill delta before timeout. This is documented in the slice doc and inventory as a foreground gathering functional gap, not a BotRunner code delta in this slice.
- Validation/tests run:
  - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~FishingPoolActivationAnalyzerTests|FullyQualifiedName~LiveBotFixtureBotChatTests|FullyQualifiedName~GatheringRouteSelectionTests|FullyQualifiedName~BotRunnerServiceFishingDispatchTests" --logger "console;verbosity=minimal"` -> `passed (33/33)`.
  - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~ActionForwardingContractTests|FullyQualifiedName~BotRunnerServiceSnapshotTests|FullyQualifiedName~BotRunnerServiceFishingDispatchTests" --logger "console;verbosity=minimal"` -> `passed (60/60)`.
  - `$env:WWOW_DATA_DIR='D:/MaNGOS/data'; dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~GatheringProfessionTests" --logger "console;verbosity=normal" --results-directory "tmp/test-runtime/results-live" --logger "trx;LogFileName=gathering_shodan_level20.trx"` -> `2 passed, 1 skipped, 1 failed`; FG mining failure documented.
- Files changed:
  - `Exports/BotRunner/TASKS.md`
- Next command: `powershell -ExecutionPolicy Bypass -File .\run-tests.ps1 -CleanupRepoScopedOnly; rg -n "BotLearnSpellAsync|BotSetSkillAsync|BotAddItemAsync|BotTeleportAsync|SendGmChatCommand|\\.learn|\\.additem|\\.setskill|\\.tele" Tests/BotRunner.Tests/LiveValidation/CraftingProfessionTests.cs`

### 2026-04-24 (Wand action dispatch support for Shodan migration)
- Pass result: `BotRunner wand dispatch coverage green; Equipment/Wand migrated live slice passed (2/2)`
- Last delta:
  - `BuildStartWandAttackSequence(targetGuid)` now primes the target action by selecting the target, stopping movement, and facing the target on the first tick, then faces again and starts Shoot on the next tick. This prevents foreground "target not in front" failures while preserving action-dispatched behavior.
  - Added `BotRunnerServiceCombatDispatchTests.BuildBehaviorTreeFromActions_StartWandAttack_FacesTargetBeforeShoot` to pin the two-tick face-before-shoot sequence.
  - The live `WandAttackTests` now proves FG and BG both receive wand actions on mage accounts while SHODAN stays director-only.
- Validation/tests run:
  - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~SpellDataTests|FullyQualifiedName~BotRunnerServiceCombatDispatchTests" --logger "console;verbosity=minimal"` -> `passed (118/118)`.
  - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~FishingPoolActivationAnalyzerTests|FullyQualifiedName~LiveBotFixtureBotChatTests|FullyQualifiedName~GatheringRouteSelectionTests|FullyQualifiedName~BotRunnerServiceFishingDispatchTests" --logger "console;verbosity=minimal"` -> `passed (33/33)`.
  - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~ActionForwardingContractTests|FullyQualifiedName~BotRunnerServiceSnapshotTests|FullyQualifiedName~BotRunnerServiceFishingDispatchTests" --logger "console;verbosity=minimal"` -> `passed (60/60)`.
  - `$env:WWOW_DATA_DIR='D:/MaNGOS/data'; dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~EquipmentEquipTests|FullyQualifiedName~WandAttackTests" --logger "console;verbosity=normal" --results-directory "tmp/test-runtime/results-live" --logger "trx;LogFileName=equipment_wand_action_plan_fresh8.trx" *> "tmp/test-runtime/results-live/equipment_wand_action_plan_fresh8.console.txt"` -> `passed (2/2)`.
- Files changed:
  - `Exports/BotRunner/SequenceBuilders/CombatSequenceBuilder.cs`
  - `Tests/BotRunner.Tests/BotRunnerServiceCombatDispatchTests.cs`
  - `Exports/BotRunner/TASKS.md`
- Next command: `powershell -ExecutionPolicy Bypass -File .\run-tests.ps1 -CleanupRepoScopedOnly; rg -n "BotLearnSpellAsync|BotSetSkillAsync|BotAddItemAsync|BotTeleportAsync|SendGmChatCommand|\\.learn|\\.additem|\\.setskill|\\.tele" Tests/BotRunner.Tests/LiveValidation/MageTeleportTests.cs`

### 2026-04-24 (Heartbeat readiness for action dispatch)
- Pass result: `FG/BG action-dispatched Ratchet fishing is green in one shared Fishing.config.json launch; deterministic BotRunner dispatch/snapshot coverage stayed green`
- Last delta:
  - `BotRunnerService` now includes lightweight readiness fields on heartbeat-only snapshots (`ScreenState`, `ConnectionState`, `IsObjectManagerValid`, `IsMapTransition`). This gives StateManager current transition/readiness state before it consumes a queued one-shot action.
  - The fix preserves the simplified action-driven fishing flow and leaves `FishingTask.TryResolveCastPosition(...)` pathfinding-first. No `AssignedActivity: "Fishing[Ratchet]"` workaround was reintroduced.
  - Root cause from FG diag: `StartFishing` could be returned while FG was in a transition-skip loop; `UpdateBehaviorTree(...)` was skipped and the next object-manager snapshot cleared `CurrentAction`, so no `[ACTION-RECV]` appeared.
- Validation/tests run:
  - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~ActionForwardingContractTests|FullyQualifiedName~BotRunnerServiceSnapshotTests|FullyQualifiedName~BotRunnerServiceFishingDispatchTests" --logger "console;verbosity=minimal"` -> `passed (60/60)`
  - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~FishingPoolActivationAnalyzerTests|FullyQualifiedName~LiveBotFixtureBotChatTests|FullyQualifiedName~GatheringRouteSelectionTests|FullyQualifiedName~BotRunnerServiceFishingDispatchTests" --logger "console;verbosity=minimal"` -> `passed (33/33)`
  - `$env:WWOW_DATA_DIR='D:/MaNGOS/data'; dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~FishingProfessionTests.Fishing_CatchFish_BgAndFg_RatchetStagedPool" --logger "console;verbosity=normal" --results-directory "tmp/test-runtime/results-live" --logger "trx;LogFileName=fishing_action_driven_single_launch_pathfinding_first_8_after_ready_heartbeat.trx" *> "tmp/test-runtime/results-live/fishing_action_driven_single_launch_pathfinding_first_8_after_ready_heartbeat.console.txt"` -> `passed (1/1)`; FG diag shows `[ACTION-RECV] type=StartFishing params=3 ready=True` followed by `tasks=2(FishingTask)`, and the TRX shows FG/BG `fishing_loot_success`.
  - `powershell -ExecutionPolicy Bypass -File .\run-tests.ps1 -CleanupRepoScopedOnly` -> `No repo-scoped processes to stop.`
- Files changed:
  - `Exports/BotRunner/BotRunnerService.cs`
  - `Services/WoWStateManager/Listeners/CharacterStateSocketListener.cs`
  - `Tests/BotRunner.Tests/ActionForwardingContractTests.cs`
  - `docs/TASKS.md`
  - `Exports/BotRunner/TASKS.md`
  - `Services/WoWStateManager/TASKS.md`
  - `Tests/BotRunner.Tests/TASKS.md`
- Next command: `git status --short`

### 2026-04-24 (Fishing single-launch follow-up)
- Pass result: `StartFishing metadata forwarding shipped; pathfinding-first fishing standoff restored; deterministic coverage green and the focused live Ratchet slice is green twice after the BG LOS regression fix`
- Last delta:
  - `ActionDispatcher.StartFishing` now accepts the metadata shape `[location, useGmCommands, masterPoolId, waypoint floats...]` and forwards those values into `FishingTask`. Legacy float-only waypoint payloads still dispatch unchanged.
  - `FishingTask.TryResolveCastPosition(...)` is pathfinding-first again. This directly fixes the current BG Ratchet regression where `castSource=native` was selecting a too-far-inboard dock candidate that passed coarse LOS but cast into the pier.
- Validation/tests run:
  - `dotnet build Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release -v minimal -m:1 -p:UseSharedCompilation=false` -> `succeeded`
  - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~FishingPoolActivationAnalyzerTests|FullyQualifiedName~LiveBotFixtureBotChatTests|FullyQualifiedName~GatheringRouteSelectionTests|FullyQualifiedName~BotRunnerServiceFishingDispatchTests" --logger "console;verbosity=minimal"` -> `passed (33/33)`
  - `$env:WWOW_DATA_DIR='D:/MaNGOS/data'; dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~FishingProfessionTests.Fishing_CatchFish_BgAndFg_RatchetStagedPool" --logger "console;verbosity=normal" --results-directory "tmp/test-runtime/results-live" --logger "trx;LogFileName=fishing_action_driven_single_launch_pathfinding_first_1.trx" *> "tmp/test-runtime/results-live/fishing_action_driven_single_launch_pathfinding_first_1.console.txt"` -> `passed (1/1)`; TRX shows FG/BG `castSource=pathfinding` and both `fishing_loot_success`.
  - `$env:WWOW_DATA_DIR='D:/MaNGOS/data'; dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~FishingProfessionTests.Fishing_CatchFish_BgAndFg_RatchetStagedPool" --logger "console;verbosity=normal" --results-directory "tmp/test-runtime/results-live" --logger "trx;LogFileName=fishing_action_driven_single_launch_pathfinding_first_2.trx" *> "tmp/test-runtime/results-live/fishing_action_driven_single_launch_pathfinding_first_2.console.txt"` -> `passed (1/1)`; TRX again shows FG/BG `castSource=pathfinding`.
- Files changed:
  - `Exports/BotRunner/ActionDispatcher.cs`
  - `Exports/BotRunner/Tasks/FishingTask.cs`
  - `Exports/BotRunner/TASKS.md`
  - `Tests/BotRunner.Tests/BotRunnerServiceFishingDispatchTests.cs`
  - `docs/TASKS.md`
- Next command: `powershell -ExecutionPolicy Bypass -File .\run-tests.ps1 -CleanupRepoScopedOnly; $env:WWOW_DATA_DIR='D:/MaNGOS/data'; dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~FishingProfessionTests.Fishing_CatchFish_BgAndFg_RatchetStagedPool" --logger "console;verbosity=normal" --results-directory "tmp/test-runtime/results-live" --logger "trx;LogFileName=fishing_action_driven_single_launch_pathfinding_first_4.trx" *> "tmp/test-runtime/results-live/fishing_action_driven_single_launch_pathfinding_first_4.console.txt"`

### 2026-04-21 (P4.5)
- Pass result: `P4.5 coordinator + test migration to structured ACKs shipped; Phase P4 closed`
- Last delta:
  - `BattlegroundCoordinator.LastAckStatus(correlationId, snapshots)` scans every bot's `RecentCommandAcks` ring and returns the most recent status (terminal beats Pending).
  - `LiveBotFixture.BotChat.SendGmChatCommandTrackedAsync` stamps a `test:<account>:<seq>` correlation id on every tracked dispatch; `GmChatCommandTrace` exposes `CorrelationId` / `AckStatus` / `AckFailureReason`.
  - `LiveBotFixture.AssertTraceCommandSucceeded` prefers the ACK status when present and falls back to `ContainsCommandRejection`. `IntegrationValidationTests` and `TalentAllocationTests` now delegate their `AssertCommandSucceeded` helpers to it.
  - `BattlegroundCoordinatorAckTests` pins the `LastAckStatus` contract (null / Pending / terminal-over-Pending / cross-snapshot scan).
- Validation/tests run:
  - `dotnet build Services/WoWStateManager/WoWStateManager.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false -nodeReuse:false -v:minimal` -> `succeeded (0 errors)`
  - `dotnet build Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false -nodeReuse:false -v:minimal` -> `succeeded (0 errors)`
  - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~BattlegroundCoordinator|FullyQualifiedName~BotRunnerServiceSnapshotTests|FullyQualifiedName~BotRunnerServiceLoadoutDispatchTests|FullyQualifiedName~LoadoutTaskExecutorTests|FullyQualifiedName~ActionForwardingContractTests" --logger "console;verbosity=minimal"` -> `passed (109/109)`
- Commits:
  - `4c39065c` `feat(coord): P4.5.1 add LastAckStatus helper on BattlegroundCoordinator`
  - `e8306a9f` `test(botrunner): P4.5.2/P4.5.3 expose AckStatus in GmChatCommandTrace`
- Next command: `rg -n "^- \\[ \\]|Active task:" docs/TASKS.md`

### 2026-04-21 (P4.4)
- Pass result: `P4.4 structured per-command ACKs shipped`
- Last delta:
  - `BotRunnerService` now tracks action correlation ids end-to-end, buffers a cap-10 `RecentCommandAcks` ring, stamps correlated `CurrentAction` clones into `_activitySnapshot`, and includes `RecentCommandAckCount` in `SnapshotChangeSignature` so ACK arrivals force immediate full snapshots without reintroducing the `P4.2` chat churn.
  - `HandleApplyLoadoutAction` seeds correlated step ids for `LoadoutTask`, and `LoadoutTask` now emits per-step `Pending`/`Success`/`TimedOut` `CommandAckEvent`s. Duplicate `ApplyLoadout` requests fail the duplicate correlation id without clobbering the original in-flight loadout ACK.
  - `CharacterStateSocketListener` now stamps `account:sequence` correlation ids on outbound actions when StateManager hands BotRunner an unstamped command.
- Validation/tests run:
  - `dotnet build Exports/BotRunner/BotRunner.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false -nodeReuse:false -v:minimal` -> `succeeded (0 errors)`
  - `dotnet build Services/WoWStateManager/WoWStateManager.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false -nodeReuse:false -v:minimal` -> `succeeded (0 errors; benign vcpkg applocal 'dumpbin' warning emitted)`
  - `dotnet build Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false -nodeReuse:false -v:minimal` -> `succeeded (0 errors; benign vcpkg applocal 'dumpbin' warning emitted)`
  - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~LoadoutTaskExecutorTests|FullyQualifiedName~LoadoutTaskTests" --logger "console;verbosity=minimal"` -> `passed (36/36)`
  - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~BotRunnerServiceSnapshotTests|FullyQualifiedName~BotRunnerServiceLoadoutDispatchTests" --logger "console;verbosity=minimal"` -> `passed (22/22)`
- Files changed:
  - `Exports/BotRunner/BotRunnerService.cs`
  - `Exports/BotRunner/BotRunnerService.Messages.cs`
  - `Exports/BotRunner/Tasks/LoadoutTask.cs`
  - `Services/WoWStateManager/Listeners/CharacterStateSocketListener.cs`
  - `Exports/BotRunner/TASKS.md`
- Commits:
  - `9232c83f` `feat(comm): P4.4 add command ack proto schema`
  - `4d1b7489` `feat(botrunner): P4.4 plumb correlated command acks`
  - `3f800ed9` `test(botrunner): P4.4 cover command ack round-trips`
- Next command: `rg -n "LastAckStatus|SendGmChatCommandTrackedAsync|RecentCommandAcks|ContainsCommandRejection" Services/WoWStateManager Tests/BotRunner.Tests docs/TASKS.md`

### 2026-04-21 (P4.3)
- Pass result: `P4.3 LoadoutTask event-driven step advancement shipped`
- Last delta:
  - `Exports/BotRunner/Tasks/LoadoutTask.cs`: `LoadoutStep` gained `AttachExpectedAck`/`DetachExpectedAck` plus the `AckFired`/`MarkAckFired` plumbing. `LearnSpellStep`, `SetSkillStep`, and `AddItemStep` override `OnAttachExpectedAck` to install filtered subscriptions on `IWoWEventHandler.OnLearnedSpell` / `OnSkillUpdated` / `OnItemAddedToBag`. `LoadoutTask.Update` attaches all acks once on first tick, detaches per-step when `IsSatisfied` flips, and detaches everything on terminal (Ready/Failed). `_acksAttached` guards against double-subscribing on re-entry.
  - Polling still runs every tick; the event handle is an optional latency optimization that flips `IsSatisfied` on the very next `Update()` without waiting for the 100ms pacing tick. No SMSG-less command (`.levelup`, `.additemset`, `.use`) changed behavior.
- Validation/tests run:
  - `dotnet build Exports/BotRunner/BotRunner.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false -nodeReuse:false -v:minimal` -> `succeeded (0 errors)`
  - `dotnet build Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false -nodeReuse:false -v:minimal` -> `succeeded (0 errors)`
  - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~LoadoutTaskExecutorTests|FullyQualifiedName~LoadoutTaskTests" --logger "console;verbosity=minimal"` -> `passed (36/36)`
  - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~BotRunnerServiceSnapshotTests|FullyQualifiedName~BotRunnerServiceLoadoutDispatchTests" --logger "console;verbosity=minimal"` -> `passed (19/19)`
- Files changed:
  - `Exports/BotRunner/Tasks/LoadoutTask.cs`
  - `Tests/BotRunner.Tests/LoadoutTaskExecutorTests.cs`
  - `Exports/BotRunner/TASKS.md`
  - `Tests/BotRunner.Tests/TASKS.md`
  - `docs/TASKS.md`
- Commits: `8add32e9 feat(botrunner): P4.3 event-driven LoadoutTask step advancement`
- Scope note: `P4.4` (correlation ids + `CommandAckEvent`) and `P4.5` (coordinator + test migration) are still open in `docs/TASKS.md` and were intentionally not started.
- Next command: `rg -n "correlation_id|CommandAckEvent|RecentCommandAcks" Exports/BotCommLayer docs/TASKS.md`

### 2026-04-21 (P4.1/P4.2)
- Pass result: `P4.1/P4.2 BotRunner plumbing shipped`
- Last delta:
  - `BotRunnerService.Messages` now buffers learned/unlearned spell, skill-update, item-added, error, and system-message events through the shared FG/BG event surface.
  - `SnapshotChangeSignature` no longer counts recent chat/error buffer lengths, and `BotRunnerServiceSnapshotTests.Start_WhenOnlyDiagnosticMessagesChange_KeepsHeartbeatOnlyUntilHeartbeatInterval` pins the no-churn heartbeat behavior.
- Validation/tests run:
  - `dotnet build Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false -nodeReuse:false` -> `succeeded`
  - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~BotRunnerServiceSnapshotTests" --logger "console;verbosity=minimal"` -> `passed (13/13)`
- Files changed:
  - `Exports/BotRunner/BotRunnerService.Messages.cs`
  - `Exports/BotRunner/BotRunnerService.cs`
  - `Exports/BotRunner/TASKS.md`
- Next command: `rg -n "LoadoutTask|LearnSpellStep|AddItemStep|SetSkillStep|ExpectedAck" Exports/BotRunner Tests/BotRunner.Tests docs/TASKS.md`
- Previous handoff preserved below.

- Last updated: 2026-04-20
- Active task: carry the now-green WSG desired-party/live-objective path forward into the next battleground objective slice.
- Last delta:
  - `BotRunnerService.DesiredParty.GetCurrentGroupSize(...)` now treats the local player as the fifth member when `PartyAgent.GroupSize == 4` / `GetGroupMembers().Count == 4`, matching the live `SMSG_GROUP_LIST` contract that excludes self. That fixes the Horde 5-player-party ceiling that previously prevented WSG leaders from converting to raid and inviting the last five queue members.
  - `BotRunnerServiceDesiredPartyTests` now pins that exact `PartyAgent` contract and verifies it still drives the current `IObjectManager.ConvertToRaid()` execution path.
  - `BgTestHelper.WaitForBotsAsync(...)` now prints the specific raw snapshot(s) missing from `AllBots` whenever live hydration stalls, which turns the old `19/20` aggregate into actionable account-level diagnostics.
  - The WSG objective scenarios now run as `WsgFlagCaptureObjectiveTests` and `WsgFullGameObjectiveTests` on separate fixture collections, so each destructive live scenario gets a fresh 20-bot roster instead of inheriting the previous match's transfer residue.
- Pass result: `BotRunner desired-party reconciliation is proven by green live WSG objective coverage`
- Validation/tests run:
  - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~BotRunnerServiceDesiredPartyTests" --logger "console;verbosity=minimal"` -> `passed (10/10)`
  - `powershell -ExecutionPolicy Bypass -File .\run-tests.ps1 -CleanupRepoScopedOnly` -> `No repo-scoped processes to stop.`
  - `$env:WWOW_DATA_DIR='D:\MaNGOS\data'; dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~BotRunner.Tests.LiveValidation.Battlegrounds.WsgObjectiveTests.WSG_FullGame_CompletesToVictoryOrDefeat" --logger "console;verbosity=minimal" --results-directory "E:\repos\Westworld of Warcraft\tmp\test-runtime\results-live" --logger "trx;LogFileName=wsg_fullgame_after_group_size_fix_20260421_0210.trx"` -> `passed (1/1)`
  - `$env:WWOW_DATA_DIR='D:\MaNGOS\data'; dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~BotRunner.Tests.LiveValidation.Battlegrounds.WsgObjectiveTests.WSG_FlagCapture_HordeCarrier_CompletesSingleCaptureCycle" --logger "console;verbosity=minimal" --results-directory "E:\repos\Westworld of Warcraft\tmp\test-runtime\results-live" --logger "trx;LogFileName=wsg_single_capture_isolated_after_diagnostics_20260421_0320.trx"` -> `passed (1/1)`
  - `$env:WWOW_DATA_DIR='D:\MaNGOS\data'; dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false --filter "(FullyQualifiedName~BotRunner.Tests.LiveValidation.Battlegrounds.WsgFlagCaptureObjectiveTests|FullyQualifiedName~BotRunner.Tests.LiveValidation.Battlegrounds.WsgFullGameObjectiveTests)" --logger "console;verbosity=minimal" --results-directory "E:\repos\Westworld of Warcraft\tmp\test-runtime\results-live" --logger "trx;LogFileName=wsg_objective_split_fixtures_20260421_0337.trx"` -> `passed (2/2)`
- Files changed:
  - `Exports/BotRunner/BotRunnerService.DesiredParty.cs`
  - `Tests/BotRunner.Tests/BotRunnerServiceDesiredPartyTests.cs`
  - `Tests/BotRunner.Tests/LiveValidation/Battlegrounds/BattlegroundEntryTests.cs`
  - `Tests/BotRunner.Tests/LiveValidation/Battlegrounds/WarsongGulchFixture.cs`
  - `Tests/BotRunner.Tests/LiveValidation/Battlegrounds/WarsongGulchObjectiveCollection.cs`
  - `Tests/BotRunner.Tests/LiveValidation/Battlegrounds/WsgObjectiveTests.cs`
  - `Exports/BotRunner/TASKS.md`
- Next command: `powershell -ExecutionPolicy Bypass -File .\run-tests.ps1 -CleanupRepoScopedOnly; $env:WWOW_DATA_DIR='D:\MaNGOS\data'; dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~BotRunner.Tests.LiveValidation.Battlegrounds.AbObjectiveTests" --logger "console;verbosity=minimal" --results-directory "E:\repos\Westworld of Warcraft\tmp\test-runtime\results-live" --logger "trx;LogFileName=ab_objective_suite_next.trx"`
- Previous handoff notes:
  - `ActionDispatcher.JoinBattleground` now upserts `BattlegroundQueueTask` instead of unconditionally pushing, so repeated queue dispatch cannot stack duplicate battleground queue tasks on the BotRunner stack.
  - Added deterministic regression coverage in `Tests/BotRunner.Tests/BotRunnerServiceBattlegroundDispatchTests.cs` for both the first queue push and the duplicate-dispatch no-growth case.
  - The same battleground slice also depended on the AB fixture changes that moved the live queue/entry rerun fully onto background runners; the fresh live AB rerun passed after the earlier foreground-transfer crash.
  - `PathfindingClient` exposes a local short-horizon segment simulation hook backed by `NativeLocalPhysics.Step`.
  - `NavigationPath` now rejects service route segments that local physics proves climb onto the wrong route layer and repairs them through nearby same-layer detour candidates.
  - The repair path keeps strict local-physics/support/width checks for the short detour leg and avoids using the noisy downstream lateral-width probe as a veto on the longer ramp stitch-back leg.
  - Long service segments are no longer rejected solely because the short-horizon local simulation reports `hit_wall` when route-layer metrics remain consistent.
  - Corpse-run routes now advance close waypoints without the standard probe-corridor shortcut veto because `NavigationRoutePolicy.CorpseRun` deliberately disables probe heuristics.
  - The live Orgrimmar bank-to-auction-house route now arrives instead of looping back over the corner waypoint.
  - Foreground ghost forward input mitigation remains guarded and unit-covered. The opt-in FG corpse-run rerun now passes and restores strict-alive state.
- Pass result: `JoinBattleground queue-task upsert is pinned and the AB queue-entry rerun is green`
- Validation/tests run:
  - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~BattlegroundFixtureConfigurationTests|FullyQualifiedName~BotRunnerServiceBattlegroundDispatchTests" --logger "console;verbosity=minimal"` -> `passed (19/19)`
  - `$env:WWOW_ENABLE_RECORDING_ARTIFACTS='1'; $env:WWOW_DATA_DIR='D:/MaNGOS/data'; dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~BotRunner.Tests.LiveValidation.Battlegrounds.ArathiBasinTests.AB_QueueAndEnterBattleground" --logger "console;verbosity=minimal" --results-directory "E:/repos/Westworld of Warcraft/tmp/test-runtime/results-live" --logger "trx;LogFileName=ab_queue_entry_background_only_recheck.trx"` -> `passed (1/1)`
  - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~NavigationPathTests.GetNextWaypoint_RepairsLocalPhysicsLayerTrap_WhenDownstreamRampWidthProbeIsNoisy|FullyQualifiedName~NavigationPathTests.GetNextWaypoint_RepairsLocalPhysicsLayerTrap_WithNearbySameLayerDetour|FullyQualifiedName~NavigationPathTests.GetNextWaypoint_SelectsAlternate_WhenLocalPhysicsClimbsOffRouteLayer" --logger "console;verbosity=minimal"` -> `passed (3/3)`
  - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~NavigationPathTests.GetNextWaypoint_AcceptsLongLocalPhysicsHorizonHit_WhenRouteLayerRemainsConsistent|FullyQualifiedName~NavigationPathTests.GetNextWaypoint_RejectsShortLocalPhysicsHitWall|FullyQualifiedName~NavigationPathTests.GetNextWaypoint_ProbeDisabled_AdvancesCloseWaypointEvenWhenShortcutProbeFails" --logger "console;verbosity=minimal"` -> `passed (3/3)`
  - `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~NavigationPathTests" --logger "console;verbosity=minimal"` -> `passed (80/80)`
  - `$env:WWOW_DATA_DIR='D:\MaNGOS\data'; dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~CornerNavigationTests.Navigate_OrgBankToAH_ArrivesWithoutStall" --logger "console;verbosity=minimal" --results-directory "E:\repos\Westworld of Warcraft\tmp\test-runtime\results-live" --logger "trx;LogFileName=corner_navigation_after_corpse_probe_policy.trx"` -> `passed (1/1)`
  - `dotnet test Tests/ForegroundBotRunner.Tests/ForegroundBotRunner.Tests.csproj --configuration Release --no-restore --settings Tests/test.runsettings --filter "FullyQualifiedName~ObjectManagerMovementTests" --logger "console;verbosity=minimal"` -> `passed (6/6)`
  - `$env:WWOW_DATA_DIR='D:\MaNGOS\data'; $env:WWOW_RETRY_FG_CRASH001='1'; dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~DeathCorpseRunTests.Death_ReleaseAndRetrieve_ResurrectsForegroundPlayer" --blame-hang --blame-hang-timeout 5m --logger "console;verbosity=minimal" --results-directory "E:\repos\Westworld of Warcraft\tmp\test-runtime\results-live" --logger "trx;LogFileName=fg_corpse_run_after_corpse_probe_policy.trx"` -> `passed (1/1); alive after 30s, bestDist=34y`
- Files changed:
  - `Exports/BotRunner/Clients/PathfindingClient.cs`
  - `Exports/BotRunner/Movement/NavigationPath.cs`
  - `Tests/BotRunner.Tests/Movement/NavigationPathTests.cs`
  - `Services/ForegroundBotRunner/Statics/ObjectManager.Movement.cs`
  - `Tests/ForegroundBotRunner.Tests/ObjectManagerMovementTests.cs`
  - `Tests/BotRunner.Tests/LiveValidation/DeathCorpseRunTests.cs`
  - `Exports/BotRunner/TASKS.md`
  - `Exports/BotRunner/TASKS_ARCHIVE.md`
  - `Tests/BotRunner.Tests/TASKS.md`
  - `docs/TASKS.md`
- Next command: `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~WsgObjectiveTests" --logger "console;verbosity=minimal"`
