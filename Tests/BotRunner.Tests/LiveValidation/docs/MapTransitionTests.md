# MapTransitionTests

Tests client survival during server-rejected map transitions (Horde entering Alliance-only Deeprun Tram).

## Test Methods (1)

### MapTransition_DeeprunTramBounce_ClientSurvives

**Bots:** BG (TESTBOT2) + FG (TESTBOT1)

**Test Flow:**

**Setup:** `.gm on` via chat for both bots (Horde safe in Alliance city with GM mode).

**Per bot (RunSingleMapTransitionTest):**

| Step | Action | Details |
|------|--------|---------|
| 1 | Teleport to Ironforge | `.go xyz -4838 -1317 505 0` (Map 0 = Eastern Kingdoms, Tinker Town) |
| 2 | Verify arrival | Assert distance from Ironforge <= 50y (not still at Orgrimmar) |
| 3 | Enter Deeprun Tram | `.go xyz -4838 -1317 502 369` (Map 369 = Deeprun Tram) |
| 4 | Server bounces | Server rejects Horde → teleports back to hearthstone (Orgrimmar). Poll 10s for `ScreenState == "InWorld"` + valid position. |
| 5 | Verify survival | Assert snapshot != null, ScreenState == "InWorld", position != (0,0,0) |
| 6 | Return to Orgrimmar | `.go xyz 1629 -4373 18 1` (Map 1 = Kalimdor) |

**StateManager/BotRunner Role:**

This test uses **no ActionType dispatches** — all movement is via GM chat commands (`.go xyz`). BotRunnerService processes `SendChat` actions for the GM commands. The test validates that the client (especially BG headless) survives the server-initiated cross-map bounce without crashing or entering a broken state.

**Key concern:** The BG headless client must correctly handle:
1. SMSG_TRANSFER_PENDING (map 369 transition)
2. Server rejection → SMSG_TRANSFER_ABORT or forced teleport
3. Return to Kalimdor (map 1) without losing connection

**Map IDs:**
| Map | Name | Role |
|-----|------|------|
| 0 | Eastern Kingdoms | Ironforge location |
| 1 | Kalimdor | Orgrimmar hearthstone |
| 369 | Deeprun Tram | Server bounces Horde |

**GM Commands:** `.gm on`, `.go xyz X Y Z MapId`.

**Assertions:** Client survives bounce. ScreenState remains "InWorld". Position is valid (not 0,0,0). No crash or disconnect.
