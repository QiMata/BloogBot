# MapTransitionTests

Validates the headless bot survives a server-driven Deeprun Tram bounce without entering a broken snapshot state.

## Test Method

### MapTransition_DeeprunTramBounce_ClientSurvives

**Bot:** BG only.

**Why BG-only:** This is a behavior assertion, so it now follows the overhaul rule that live behavior tests validate the headless bot. FG remains useful for packet capture and crash diagnostics, but not as a required assertion target in this suite.

**Flow:**
1. `EnsureCleanSlateAsync()` for the BG bot.
2. `.go xyz -4838 -1317 505 0` to Ironforge near the Deeprun Tram entrance.
3. Assert the bot actually arrived near the Ironforge target.
4. `.go xyz -4838 -1317 502 369` into Deeprun Tram.
5. Poll for `ScreenState == "InWorld"` plus a valid position.
6. Assert the post-bounce snapshot is still in-world and not at the origin.
7. Return the bot to Orgrimmar.

**Code paths:**
- Test entry: `Tests/BotRunner.Tests/LiveValidation/MapTransitionTests.cs`
- Chat command dispatch: `Tests/BotRunner.Tests/LiveValidation/LiveBotFixture.BotChat.cs`
- Snapshot polling: `Tests/BotRunner.Tests/LiveValidation/LiveBotFixture.Snapshots.cs`
- BG world-transfer handling: `Exports/WoWSharpClient/Client/` and `Exports/WoWSharpClient/Handlers/`

**Assertions:**
- Teleport to Ironforge is reflected in snapshot position
- Deeprun Tram bounce leaves the client in `"InWorld"`
- Position after bounce is non-zero and therefore not obviously corrupted
