# DeathCorpseRunTests

Tests the full death recovery cycle: kill → release spirit → ghost run → retrieve corpse → resurrect.

**Prerequisite:** PathfindingService must be ready on port 5001 (`_bot.IsPathfindingReady`).

## Test Methods (1)

### Death_ReleaseAndRetrieve_ResurrectsPlayer

**Bots:** BG (TESTBOT2) + FG (TESTBOT1)

**Test Flow (RunCorpseRunScenario per bot):**

| Step | Action | Details |
|------|--------|---------|
| 1 | Ensure alive | `EnsureCleanSlateAsync()` |
| 2 | Teleport to Orgrimmar | `BotTeleportToNamedAsync("Orgrimmar")` — bank area, flat terrain, short corpse run |
| 3 | Kill | `InduceDeathForTestAsync()` — tries `.die`, `.kill`, `.damage 5000`; fallback SOAP (15s timeout, requireCorpseTransition=true) |
| 4 | Release corpse | **Dispatch `ActionType.ReleaseCorpse`** — assert Success |
| 5 | Wait for ghost | Poll for `(PlayerFlags & 0x10) != 0` (GHOST flag), 10s timeout |
| 6 | Wait for graveyard | Poll for position change (ghost teleported to graveyard), 5s |
| 7 | Calculate distance | `distToCorpse = Distance2D(ghostPos, corpsePos)`. If > 39y (RetrieveRange) → step 8 |
| 8 | Run back | **Dispatch `ActionType.RetrieveCorpse`** — triggers pathfinding to corpse. Poll every 2s, log progress every 10s. Timeout 60s. |
| 9 | Wait for reclaim | Poll `CorpseRecoveryDelaySeconds <= 0`, 45s timeout |
| 10 | Retrieve corpse | **Dispatch `ActionType.RetrieveCorpse`** — assert Success |
| 11 | Confirm alive | Poll `IsStrictAlive()` (health > 0, no ghost, alive state), 15s |

**StateManager/BotRunner Action Flow:**

**ReleaseCorpse dispatch chain:**
1. `CharacterAction.ReleaseCorpse` → inline `Do()` node (not a builder method)
2. Checks: not already ghost, cooldown not active (2s `ReleaseSpiritCommandCooldown`)
3. `_objectManager.ReleaseCorpse()` → CMSG_REPOP_REQUEST packet
4. Server respawns player as ghost at nearest graveyard

**RetrieveCorpse dispatch chain:**
1. `CharacterAction.RetrieveCorpse` → inline `Do()` node
2. If corpse position known and distance > RetrieveRange: enqueues `RetrieveCorpseTask` on BotTasks stack
3. `RetrieveCorpseTask` uses PathfindingService to build path from graveyard → corpse location
4. When within RetrieveRange: `_objectManager.RetrieveCorpse()` → CMSG_RECLAIM_CORPSE packet
5. Server resurrects player with penalties (HP/mana loss, resurrection sickness if applicable)

**Note:** Env var `WWOW_DISABLE_AUTORELEASE_CORPSE_TASK=1` prevents BotRunnerService from auto-releasing during tests. The test controls each step explicitly.

**Key Constants:**
- RetrieveRange = 39.0y (must be within this distance to reclaim)
- MaxRunbackSeconds = 60
- MaxReclaimWaitSeconds = 45
- PLAYER_FLAGS_GHOST = 0x10

**Cleanup:** `RevivePlayerAsync()` + teleport back to Orgrimmar in finally block.

**Assertions:** Ghost state entered after release. Distance to corpse decreases during runback. Player alive after retrieve.
