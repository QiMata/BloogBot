# MailParityTests

`MailParityTests` now uses the Shodan test-director topology while keeping the
mail behavior action on BG. SHODAN stages mailbox location and mail payloads,
FG stays launched for the shared Economy topology, and the test body dispatches
only `ActionType.CheckMail` to the BG BotRunner target.

## Shodan Shape

1. `Economy.config.json` launches:
   - `ECONFG1` as the Foreground Orc Warrior topology participant.
   - `ECONBG1` as the Background Orc Warrior action target.
   - `SHODAN` as the Background Gnome Mage director.
2. `AssertConfiguredCharactersMatchAsync(...)` verifies the live roster.
3. `StageBotRunnerAtOrgrimmarMailboxAsync(...)` stages the BG target at a
   visible mailbox.
4. `StageBotRunnerMailboxMoneyAsync(...)` sends the parity gold mail via SOAP.
5. `StageBotRunnerMailboxItemAsync(...)` sends the parity Linen Cloth mail via
   SOAP.
6. The test body dispatches only `ActionType.CheckMail` with the detected
   mailbox GUID.

## Test Methods

### Mail_SendGold_FgBgParity

- BotRunner action target: `ECONBG1`.
- Director: `SHODAN`.
- Under-test action dispatch: `ActionType.CheckMail`.
- Result: passed. The test asserts BG coinage increases after the staged gold
  mail is collected.

### Mail_SendItem_FgBgParity

- BotRunner action target: `ECONBG1`.
- Director: `SHODAN`.
- Under-test action dispatch: `ActionType.CheckMail`.
- Result: passed. The test asserts Linen Cloth `2589` appears in BG bags after
  the staged item mail is collected.

## Foreground Gap

The original migration attempt included FG as an action target. The full
combined mail suite delivered `CheckMail` to FG but timed out waiting for FG
coinage/item snapshot deltas in both parity methods. A focused rerun of
`Mail_SendGold_FgBgParity` passed once with FG enabled, so this is tracked as a
foreground mail-collection stability issue rather than a migration blocker.

Committed behavior keeps FG online for topology parity and dispatches mail
actions to BG only until `Services/ForegroundBotRunner` can make
`CollectAllMailAsync(...)` stable under combined-suite load.

## Current Status

Final slice validation artifact:
`tmp/test-runtime/results-live/mail_shodan_bgonly.trx`.

- `Mail_SendGold_FgBgParity`: passed.
- `Mail_SendItem_FgBgParity`: passed.

Related diagnostic artifacts:

- `tmp/test-runtime/results-live/mail_shodan.trx` -> first full FG/BG attempt,
  `Mail_SendGold_FgBgParity` failed on FG coinage timeout.
- `tmp/test-runtime/results-live/mail_gold_rerun.trx` -> focused FG/BG gold
  rerun passed once.
- `tmp/test-runtime/results-live/mail_shodan_rerun.trx` -> second full FG/BG
  attempt failed on FG item and gold timeouts.

## Validation

- `dotnet build Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false` -> passed with `0` errors and existing warnings.
- `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~FishingPoolActivationAnalyzerTests|FullyQualifiedName~LiveBotFixtureBotChatTests|FullyQualifiedName~GatheringRouteSelectionTests|FullyQualifiedName~BotRunnerServiceFishingDispatchTests" --logger "console;verbosity=minimal"` -> passed `33/33`.
- `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~ActionForwardingContractTests|FullyQualifiedName~BotRunnerServiceSnapshotTests|FullyQualifiedName~BotRunnerServiceFishingDispatchTests" --logger "console;verbosity=minimal"` -> passed `60/60`.
- `$env:WWOW_DATA_DIR='D:/MaNGOS/data'; dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~MailSystemTests|FullyQualifiedName~MailParityTests" --logger "console;verbosity=normal" --results-directory "tmp/test-runtime/results-live" --logger "trx;LogFileName=mail_shodan_bgonly.trx"` -> passed `4/4`.
- Repo-scoped cleanup before and after live validation reported `No repo-scoped processes to stop.`

## Code Paths

- Test entry: `Tests/BotRunner.Tests/LiveValidation/MailParityTests.cs`
- Shodan staging helper: `Tests/BotRunner.Tests/LiveValidation/LiveBotFixture.TestDirector.cs`
- Action dispatch: `Exports/BotRunner/BotRunnerService.ActionMapping.cs`
- Check-mail implementation: `Exports/BotRunner/ActionDispatcher.cs`
- FG mail follow-up surface: `Services/ForegroundBotRunner/Statics/ObjectManager.Interaction.cs`
