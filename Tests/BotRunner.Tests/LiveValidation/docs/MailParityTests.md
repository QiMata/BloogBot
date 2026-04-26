# MailParityTests

`MailParityTests` uses the Shodan test-director topology with both foreground
and background mail behavior active. SHODAN stages mailbox location and mail
payloads, and the test body dispatches only `ActionType.CheckMail` to the FG/BG
BotRunner targets.

## Shodan Shape

1. `Economy.config.json` launches:
   - `ECONFG1` as the Foreground Orc Warrior action target.
   - `ECONBG1` as the Background Orc Warrior action target.
   - `SHODAN` as the Background Gnome Mage director.
2. `AssertConfiguredCharactersMatchAsync(...)` verifies the live roster.
3. `StageBotRunnerAtOrgrimmarMailboxAsync(...)` stages each BotRunner target
   at a visible mailbox.
4. `StageBotRunnerMailboxMoneyAsync(...)` sends the parity gold mail via SOAP.
5. `StageBotRunnerMailboxItemAsync(...)` sends the parity Linen Cloth mail via
   SOAP.
6. The test body dispatches only `ActionType.CheckMail` with the detected
   mailbox GUID.

## Test Methods

### Mail_SendGold_FgBgParity

- BotRunner action targets: `ECONBG1`, `ECONFG1`.
- Director: `SHODAN`.
- Under-test action dispatch: `ActionType.CheckMail`.
- Result: passed. The test asserts BG coinage increases and foreground either
  reports a coinage increase or emits a fresh `[MAIL-COLLECT]` marker for the
  staged gold mail.

### Mail_SendItem_FgBgParity

- BotRunner action targets: `ECONBG1`, `ECONFG1`.
- Director: `SHODAN`.
- Under-test action dispatch: `ActionType.CheckMail`.
- Result: passed. The test asserts Linen Cloth `2589` appears in bags, or for
  foreground that a fresh `[MAIL-COLLECT]` marker confirms collection of the
  staged item mail.

## Foreground Stabilization

The original migration attempt exposed a foreground mailbox timing issue under
the full combined suite. That follow-up is now closed: foreground
`CollectAllMailWithResultAsync(...)` keeps the mailbox action alive while
visible inbox rows wait for metadata to hydrate, and BotRunner emits
`[MAIL-COLLECT]` markers that the live assertions use as completion evidence.

## Current Status

Final slice validation artifact:
`tmp/test-runtime/results-live/mail_fg_shodan_director_extendedpoll.trx`.

- `Mail_SendGold_FgBgParity`: passed.
- `Mail_SendItem_FgBgParity`: passed.

## Validation

- `dotnet build Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false` -> passed with `0` errors and existing warnings.
- `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~FishingPoolActivationAnalyzerTests|FullyQualifiedName~LiveBotFixtureBotChatTests|FullyQualifiedName~GatheringRouteSelectionTests|FullyQualifiedName~BotRunnerServiceFishingDispatchTests" --logger "console;verbosity=minimal"` -> passed `33/33`.
- `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~ActionForwardingContractTests|FullyQualifiedName~BotRunnerServiceSnapshotTests|FullyQualifiedName~BotRunnerServiceFishingDispatchTests" --logger "console;verbosity=minimal"` -> passed `60/60`.
- `$env:WWOW_DATA_DIR='D:/MaNGOS/data'; dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:UseSharedCompilation=false --filter "FullyQualifiedName~MailSystemTests|FullyQualifiedName~MailParityTests" --logger "console;verbosity=normal" --results-directory "tmp/test-runtime/results-live" --logger "trx;LogFileName=mail_fg_shodan_director_extendedpoll.trx"` -> passed `4/4`.
- Repo-scoped cleanup before and after live validation reported `No repo-scoped processes to stop.`

## Code Paths

- Test entry: `Tests/BotRunner.Tests/LiveValidation/MailParityTests.cs`
- Shodan staging helper: `Tests/BotRunner.Tests/LiveValidation/LiveBotFixture.TestDirector.cs`
- Action dispatch: `Exports/BotRunner/BotRunnerService.ActionMapping.cs`
- Check-mail implementation: `Exports/BotRunner/ActionDispatcher.cs`
- FG mail follow-up surface: `Services/ForegroundBotRunner/Statics/ObjectManager.Interaction.cs`
