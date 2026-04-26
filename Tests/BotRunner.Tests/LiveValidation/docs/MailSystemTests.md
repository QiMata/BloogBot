# MailSystemTests

`MailSystemTests` uses the Shodan test-director topology for the baseline mail
receive flows. The test body issues no GM setup commands; SHODAN stages the
mailbox location and SOAP mail payloads through fixture helpers, while the FG
and BG BotRunner targets receive only `ActionType.CheckMail`.

## Shodan Shape

1. `Economy.config.json` launches:
   - `ECONFG1` as the Foreground Orc Warrior action target.
   - `ECONBG1` as the Background Orc Warrior action target.
   - `SHODAN` as the Background Gnome Mage director.
2. `AssertConfiguredCharactersMatchAsync(...)` verifies the live roster.
3. `StageBotRunnerAtOrgrimmarMailboxAsync(...)` stages each BotRunner target
   beside the Orgrimmar mailbox and verifies a visible mailbox object.
4. `StageBotRunnerMailboxMoneyAsync(...)` sends the gold-mail payload via SOAP.
5. `StageBotRunnerMailboxItemAsync(...)` sends the Linen Cloth payload via
   SOAP.
6. The test body dispatches only `ActionType.CheckMail` with the detected
   mailbox GUID.

## Test Methods

### Mail_SendGold_RecipientReceives

- BotRunner action targets: `ECONBG1`, `ECONFG1`.
- Director: `SHODAN`.
- Under-test action dispatch: `ActionType.CheckMail`.
- Result: passed. The test asserts BG coinage increases and foreground either
  reports a coinage increase or emits a fresh `[MAIL-COLLECT]` marker for the
  staged money mail.

### Mail_SendItem_RecipientReceivesItem

- BotRunner action targets: `ECONBG1`, `ECONFG1`.
- Director: `SHODAN`.
- Under-test action dispatch: `ActionType.CheckMail`.
- Result: passed. The test asserts Linen Cloth `2589` appears in bags, or for
  foreground that a fresh `[MAIL-COLLECT]` marker confirms collection of the
  staged item mail.

## Current Status

Final slice validation artifact:
`tmp/test-runtime/results-live/mail_fg_shodan_director_extendedpoll.trx`.

- `Mail_SendGold_RecipientReceives`: passed.
- `Mail_SendItem_RecipientReceivesItem`: passed.

Foreground mail behavior is now covered in the same combined suite. The FG
runtime keeps `CheckMail` active while visible inbox rows wait for money/item
metadata to hydrate, and BotRunner emits `[MAIL-COLLECT]` markers for action
completion evidence.

## Validation

- `dotnet build Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false` -> passed with `0` errors and existing warnings.
- `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~FishingPoolActivationAnalyzerTests|FullyQualifiedName~LiveBotFixtureBotChatTests|FullyQualifiedName~GatheringRouteSelectionTests|FullyQualifiedName~BotRunnerServiceFishingDispatchTests" --logger "console;verbosity=minimal"` -> passed `33/33`.
- `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~ActionForwardingContractTests|FullyQualifiedName~BotRunnerServiceSnapshotTests|FullyQualifiedName~BotRunnerServiceFishingDispatchTests" --logger "console;verbosity=minimal"` -> passed `60/60`.
- `$env:WWOW_DATA_DIR='D:/MaNGOS/data'; dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~MailSystemTests|FullyQualifiedName~MailParityTests" --logger "console;verbosity=normal" --results-directory "tmp/test-runtime/results-live" --logger "trx;LogFileName=mail_fg_shodan_director_extendedpoll.trx"` -> passed `4/4`.
- Repo-scoped cleanup before and after live validation reported `No repo-scoped processes to stop.`

## Code Paths

- Test entry: `Tests/BotRunner.Tests/LiveValidation/MailSystemTests.cs`
- Shodan staging helper: `Tests/BotRunner.Tests/LiveValidation/LiveBotFixture.TestDirector.cs`
- Action dispatch: `Exports/BotRunner/BotRunnerService.ActionMapping.cs`
- Check-mail implementation: `Exports/BotRunner/ActionDispatcher.cs`
- BG mail component: `Exports/WoWSharpClient/Networking/ClientComponents/MailNetworkClientComponent.cs`
- FG mail component: `Services/ForegroundBotRunner/Statics/ObjectManager.Interaction.cs`
