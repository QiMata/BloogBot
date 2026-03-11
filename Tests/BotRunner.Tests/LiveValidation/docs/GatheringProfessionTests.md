# GatheringProfessionTests

Tests mining and herbalism gathering: navigate to node, cast gather spell, verify skill increase.

## Test Methods (2)

### 1. Mining_GatherCopperVein_SkillIncreases

**Bots:** FG (TESTBOT1) first (up to 3 nearest nodes), then BG (TESTBOT2, all nodes).

**Test Flow:**

**Phase 1 — Query spawn locations:**
- MySQL read-only query for Copper Vein (entry 1731) spawns on Map 1 (Kalimdor), limit 25
- Sort by distance from Orgrimmar (1629.4, -4373.4, 31.2)
- Skip test if none found (all on respawn timer)

**Phase 2 — Prepare bot (PrepareMining):**

| Step | Action | Details |
|------|--------|---------|
| 1 | Ensure alive | `EnsureAliveAndAtSetupLocationAsync()` — revive, teleport Orgrimmar if >80y |
| 2 | GM mode | `.gm on` via chat (FG only) |
| 3 | Clear bags | If >= 12 items: `BotClearInventoryAsync()` |
| 4 | Self-target | `BotSelectSelfAsync()` |
| 5 | Learn mining | `BotLearnSpellAsync(2575)` — Mining Apprentice/Gather spell |
| 6 | Set skill | `.setskill 45 1 300` — Mining skill to 1/300 |
| 7 | Add tool | `BotAddItemAsync(2901)` — Mining Pick |

**Phase 3 — Gather at spawns (TryGatherAtSpawns):**

For each spawn (FG: max 3 nearest, BG: all):
1. `BotTeleportAsync(map, spawnX, spawnY, spawnZ+3)` — Z+3 to avoid undermap detection
2. Wait for Z stabilization (2s)
3. Verify within 50y of target
4. Scan NearbyObjects 3s for entry 1731 within 100y
5. If node found and distance > 4y: **Dispatch `ActionType.Goto`** with `(nodeX, nodeY, nodeZ, 4.0)`, poll 30s
6. Wait 1.2s for movement settle
7. **Dispatch `ActionType.GatherNode`** with `LongParam = nodeGuid`, `IntParam = 2575`
8. Wait 9s (5s channel + 3s post-cooldown)
9. Poll 40s for skill increase (handles FG crash+reconnect)
10. If skill > initial OR node despawned: return success

**StateManager/BotRunner Action Flow:**

- **Goto:** `BuildGoToSequence(x, y, z, 4.0)` → PathfindingService path → walk waypoints to within 4y
- **GatherNode:** `BuildGatherNodeSequence(nodeGuid, 2575)` → face node → `_objectManager.CastSpell(2575)` with target=nodeGuid → 3.2s mining channel → loot window → auto-loot

---

### 2. Herbalism_GatherHerb_SkillIncreases

Same structure as mining with different entries:

| Entry | Name | Spell | Skill |
|-------|------|-------|-------|
| 1617 | Peacebloom | 2366 (Herbalism, instant) | 21 (Herbalism) |
| 1618 | Silverleaf | 2366 | 21 |
| 1619 | Earthroot | 2366 | 21 |

Tries each herb type sequentially until success. Herbalism spell (2366) is instant (no channel), unlike Mining (3.2s).

---

## Key IDs

| ID | Type | Name |
|----|------|------|
| 2575 | Spell | Mining (Apprentice/Gather, 3.2s channel) |
| 2366 | Spell | Herbalism (instant gather) |
| 2901 | Item | Mining Pick |
| 45 | Skill | Mining |
| 21 | Skill | Herbalism |
| 1731 | GO Entry | Copper Vein |
| 1617/1618/1619 | GO Entry | Peacebloom/Silverleaf/Earthroot |

**GM Commands:** `.gm on`, `.targetself`, `.setskill`.

**Assertions:** Gathering skill increases after successful gather. Node despawns (proof of gather).
