# Pathing Test List

This document defines all pathing tests for the WWoW.RecordedTests.PathingTests suite. Each test validates that the bot can successfully navigate from point A to point B, including complex scenarios like transport usage and cave navigation.

## Test Structure

Each test consists of:
- **Setup Commands**: GM commands executed by foreground runner to prepare server state
- **Test Execution**: Background runner pathfinding script
- **Teardown Commands**: GM commands to reset server state
- **Expected Outcome**: What should happen during the test

## Basic Point-to-Point Tests

### 1. Northshire_ElwynnForest_ShortDistance
**Description**: Navigate from Northshire Abbey to Goldshire (short distance, simple terrain)

**Setup Commands**:
```
.character level 1
.teleport name Northshire Valley
.modify money 1000000
```

**Execution**:
- Start position: Northshire Abbey entrance (approx -8914, -133, 81)
- End position: Goldshire town center (approx -9465, 64, 56)
- Expected path: Follow road through Northshire Valley gate, continue east to Goldshire

**Teardown Commands**:
```
.character delete
```

**Expected Duration**: ~3-5 minutes

---

### 2. Goldshire_Stormwind_MediumDistance
**Description**: Navigate from Goldshire to Stormwind City (medium distance, road following)

**Setup Commands**:
```
.character level 10
.teleport name Goldshire
.modify money 1000000
```

**Execution**:
- Start position: Goldshire town center (-9465, 64, 56)
- End position: Stormwind Trade District (approx -8833, 622, 94)
- Expected path: Follow main road northwest to Stormwind gates, enter city

**Teardown Commands**:
```
.character delete
```

**Expected Duration**: ~5-8 minutes

---

### 3. CrossContinents_Wetlands_To_IronForge
**Description**: Navigate from Menethil Harbor to Ironforge (cross-zone, elevation changes)

**Setup Commands**:
```
.character level 20
.teleport name Menethil Harbor
.modify money 1000000
.modify speed all 1.5
```

**Execution**:
- Start position: Menethil Harbor docks (approx -3792, -832, 10)
- End position: Ironforge city center (approx -4918, -941, 501)
- Expected path: Travel through Wetlands, into Loch Modan, follow road to Dun Morogh, enter Ironforge

**Teardown Commands**:
```
.character delete
```

**Expected Duration**: ~15-20 minutes

---

## Transport Tests

### 4. BoatTravel_Menethil_To_Auberdine
**Description**: Use boat transport from Menethil Harbor to Auberdine (Darkshore)

**Setup Commands**:
```
.character level 15
.teleport name Menethil Harbor
.modify money 1000000
```

**Execution**:
- Start position: Menethil Harbor town center
- Path to dock: Navigate to correct boat dock
- Wait for boat: Detect boat arrival and board
- End position: Auberdine dock (Darkshore)
- Expected behavior: Wait for boat, board, disembark at destination

**Teardown Commands**:
```
.character delete
```

**Expected Duration**: ~8-12 minutes (includes boat wait time)

---

### 5. BoatTravel_Ratchet_To_BootyBay
**Description**: Use boat from Ratchet to Booty Bay (cross-continent transport)

**Setup Commands**:
```
.character level 20
.teleport name Ratchet
.modify money 1000000
```

**Execution**:
- Start position: Ratchet town center
- Navigate to boat dock
- Board boat to Booty Bay
- Disembark at Booty Bay
- Expected behavior: Successful cross-continent boat travel

**Teardown Commands**:
```
.character delete
```

**Expected Duration**: ~10-15 minutes

---

### 6. ZeppelinTravel_Orgrimmar_To_Undercity
**Description**: Use zeppelin from Orgrimmar to Undercity

**Setup Commands**:
```
.character level 10
.teleport name Orgrimmar
.modify money 1000000
```

**Execution**:
- Start position: Orgrimmar city center
- Navigate to zeppelin tower
- Wait for Undercity zeppelin
- Board zeppelin
- Disembark at Undercity
- Expected behavior: Successful zeppelin travel, handle tower navigation

**Teardown Commands**:
```
.character delete
```

**Expected Duration**: ~8-12 minutes

---

### 7. ZeppelinTravel_Undercity_To_GromGol
**Description**: Use zeppelin from Undercity to Grom'gol Base Camp (STV)

**Setup Commands**:
```
.character level 15
.teleport name Undercity
.modify money 1000000
```

**Execution**:
- Start position: Undercity entrance
- Navigate to zeppelin tower
- Board zeppelin to Grom'gol
- Disembark at jungle platform
- Expected behavior: Handle multi-level tower navigation, jungle terrain

**Teardown Commands**:
```
.character delete
```

**Expected Duration**: ~10-15 minutes

---

## Cave and Complex Terrain Tests

### 8. CaveNavigation_Fargodeep_Mine
**Description**: Navigate through Fargodeep Mine (simple cave with single entrance/exit)

**Setup Commands**:
```
.character level 5
.teleport name Fargodeep Mine
.modify money 1000000
```

**Execution**:
- Start position: Outside Fargodeep Mine entrance
- Enter cave
- Navigate to deepest point
- Return to entrance
- Expected behavior: Handle cave lighting, enclosed spaces

**Teardown Commands**:
```
.character delete
```

**Expected Duration**: ~5-8 minutes

---

### 9. CaveNavigation_Deadmines_Entrance_To_VanCleef
**Description**: Navigate through Deadmines instance (complex multi-level dungeon)

**Setup Commands**:
```
.character level 20
.teleport name The Deadmines
.modify hp 50000
.modify mana 50000
.gm visible off
```

**Execution**:
- Start position: Deadmines entrance
- Navigate through mine tunnels
- Handle wooden platforms
- Navigate to VanCleef's ship
- Expected behavior: Complex pathfinding through instance, handle multiple levels

**Teardown Commands**:
```
.gm visible on
.character delete
```

**Expected Duration**: ~15-20 minutes

---

### 10. CaveNavigation_WailingCaverns_Spiral
**Description**: Navigate Wailing Caverns spiral cave system

**Setup Commands**:
```
.character level 18
.teleport name Wailing Caverns
.modify hp 50000
.modify speed all 1.5
```

**Execution**:
- Start position: Wailing Caverns entrance
- Navigate spiral descent
- Reach bottom of cave system
- Return to entrance
- Expected behavior: Handle spiral geometry, multiple branching paths

**Teardown Commands**:
```
.character delete
```

**Expected Duration**: ~12-18 minutes

---

## Obstacle and Terrain Challenge Tests

### 11. MountainClimbing_ThousandNeedles_To_Feralas
**Description**: Navigate mountain pass from Thousand Needles to Feralas

**Setup Commands**:
```
.character level 40
.teleport name Thousand Needles
.modify money 1000000
.modify speed all 1.2
```

**Execution**:
- Start position: Thousand Needles (high plateau)
- Navigate down steep terrain
- Cross into Feralas
- Expected behavior: Handle steep slopes, avoid falling damage

**Teardown Commands**:
```
.character delete
```

**Expected Duration**: ~10-15 minutes

---

### 12. WaterNavigation_STV_Coast_Swim
**Description**: Navigate along Stranglethorn Vale coastline (swimming test)

**Setup Commands**:
```
.character level 30
.teleport name Stranglethorn Vale
.modify money 1000000
```

**Execution**:
- Start position: Booty Bay docks
- Swim north along coast
- Navigate around obstacles (islands, rocks)
- Reach northern coastline
- Expected behavior: Handle swimming, underwater navigation

**Teardown Commands**:
```
.character delete
```

**Expected Duration**: ~10-15 minutes

---

### 13. BridgeCrossing_Redridge_LakeEverstill
**Description**: Cross Lake Everstill via bridge (structure navigation)

**Setup Commands**:
```
.character level 15
.teleport name Redridge Mountains
.modify money 1000000
```

**Execution**:
- Start position: Lakeshire
- Navigate to bridge
- Cross Lake Everstill
- Reach opposite shore
- Expected behavior: Use bridge structure, avoid swimming

**Teardown Commands**:
```
.character delete
```

**Expected Duration**: ~5-8 minutes

---

## Advanced Multi-Segment Tests

### 14. GrandTour_Alliance_Capitals
**Description**: Visit all Alliance capital cities in sequence

**Setup Commands**:
```
.character level 40
.teleport name Stormwind City
.modify money 5000000
.modify speed all 2.0
```

**Execution**:
- Start: Stormwind
- Path: Stormwind → Ironforge → Darnassus (via boat) → Exodar (via boat)
- Expected behavior: Multi-zone navigation, multiple transports, complex routing

**Teardown Commands**:
```
.character delete
```

**Expected Duration**: ~30-40 minutes

---

### 15. GrandTour_Horde_Capitals
**Description**: Visit all Horde capital cities in sequence

**Setup Commands**:
```
.character level 40
.teleport name Orgrimmar
.modify money 5000000
.modify speed all 2.0
```

**Execution**:
- Start: Orgrimmar
- Path: Orgrimmar → Thunder Bluff → Undercity (via zeppelin) → Silvermoon (via teleport orb)
- Expected behavior: Multi-zone navigation, zeppelin usage, teleport handling

**Teardown Commands**:
```
.character delete
```

**Expected Duration**: ~30-40 minutes

---

### 16. CrossContinent_Kalimdor_To_EasternKingdoms
**Description**: Travel from Teldrassil to Eastern Plaguelands

**Setup Commands**:
```
.character level 50
.teleport name Teldrassil
.modify money 5000000
.modify speed all 1.5
```

**Execution**:
- Start: Teldrassil
- Path: Teldrassil → Darnassus → Boat to Auberdine → Boat to Menethil → Wetlands → Eastern Plaguelands
- Expected behavior: Maximum route complexity, multiple zones, multiple transports

**Teardown Commands**:
```
.character delete
```

**Expected Duration**: ~40-50 minutes

---

## Edge Case and Stress Tests

### 17. StuckRecovery_WesternPlaguelands_River
**Description**: Test recovery from getting stuck in river terrain

**Setup Commands**:
```
.character level 45
.teleport xyz 1744 -1723 60 0
.modify speed all 0.5
```

**Execution**:
- Start position: Middle of river with complex terrain
- Navigate to nearest road
- Expected behavior: Detect stuck state, execute recovery, reach valid path

**Teardown Commands**:
```
.character delete
```

**Expected Duration**: ~5-10 minutes

---

### 18. AggroAvoidance_STV_HighLevel_Mobs
**Description**: Navigate through high-level mob area while avoiding aggro

**Setup Commands**:
```
.character level 25
.teleport name Stranglethorn Vale
.modify hp 10000
```

**Execution**:
- Start position: Grom'gol Base Camp
- Navigate to Booty Bay through hostile territory
- Expected behavior: Avoid high-level mobs, use stealth/pathing to survive

**Teardown Commands**:
```
.character delete
```

**Expected Duration**: ~10-15 minutes

---

### 19. NightNavigation_Duskwood_NoLight
**Description**: Navigate Duskwood at night (low visibility test)

**Setup Commands**:
```
.character level 20
.teleport name Duskwood
.wchange 1 night
```

**Execution**:
- Start position: Darkshire
- Navigate to Raven Hill Cemetery
- Return to Darkshire
- Expected behavior: Handle reduced visibility, stay on path

**Teardown Commands**:
```
.wchange 1 day
.character delete
```

**Expected Duration**: ~8-12 minutes

---

### 20. RapidPathRecalculation_Barrens_Oasis_Loop
**Description**: Navigate circular path with frequent recalculation triggers

**Setup Commands**:
```
.character level 15
.teleport name The Barrens
.modify speed all 1.8
```

**Execution**:
- Start position: Crossroads
- Path: Crossroads → Ratchet → Camp Taurajo → Crossroads (circular)
- Inject random position changes to force recalculation
- Expected behavior: Handle frequent path recalculations, maintain progress

**Teardown Commands**:
```
.character delete
```

**Expected Duration**: ~15-20 minutes

---

## Summary Statistics

- **Total Tests**: 20
- **Basic Tests**: 3
- **Transport Tests**: 4
- **Cave Tests**: 3
- **Terrain Challenge Tests**: 3
- **Advanced Multi-Segment Tests**: 3
- **Edge Case Tests**: 4

**Estimated Total Test Suite Duration**: ~6-8 hours (running all tests sequentially)

## GM Command Reference

Common GM commands used across tests:

- `.character level <level>` - Set character level
- `.teleport name <location>` - Teleport to named location
- `.teleport xyz <x> <y> <z> <map>` - Teleport to coordinates
- `.modify money <copper>` - Add money to character
- `.modify hp <value>` - Set max HP
- `.modify mana <value>` - Set max mana
- `.modify speed <type> <multiplier>` - Modify movement speed
- `.gm visible <on|off>` - Toggle GM visibility
- `.wchange <map> <weather>` - Change weather/time
- `.character delete` - Delete current character

## Notes

1. All tests assume a clean server state before execution
2. Test execution times are estimates and may vary based on server performance
3. Some tests require specific server configurations (e.g., transport schedules)
4. Edge case tests may need manual verification of recovery behaviors
5. Advanced tests may need to be broken into smaller segments for recording purposes
