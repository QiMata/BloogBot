# Live Validation Test Traces

Each test runs within the `LiveValidationCollection` — all tests share a single `LiveBotFixture` instance (see [FIXTURE_LIFECYCLE.md](FIXTURE_LIFECYCLE.md)). Tests execute sequentially. The fixture is fully initialized before any test runs and disposed after all complete.

**Common constructor pattern** (all 25 tests):
1. Store `LiveBotFixture` reference
2. Call `_bot.SetOutput(output)` for xUnit output capture
3. `Skip.IfNot(_bot.IsReady, ...)` — skips entire test class if fixture failed to initialize

**Common bot pattern**: BG (TESTBOT2) is always required. FG (TESTBOT1) is optional — runs in parallel when `IsFgActionable` is true. COMBATTEST is used only by CombatLoopTests.

---

## 1. BasicLoopTests

**File:** `BasicLoopTests.cs` | **Bots:** BG required, FG optional

### LoginAndEnterWorld_BothBotsPresent
| Phase | Actor | Action |
|-------|-------|--------|
| Test | Fixture | `RefreshSnapshotsAsync()` |
| Assert | Test | BG is InWorld, has name/account/GUID/position, `IsStrictAlive` |
| Assert | Test | If FG actionable: same assertions for FG |

No GM commands. No teleports. Read-only snapshot validation.

### Physics_PlayerNotFallingThroughWorld
| Phase | Actor | Action |
|-------|-------|--------|
| Setup | Fixture | `EnsureCleanSlateAsync` for BG (and FG) — revive, teleport to Orgrimmar, `.gm on` |
| Test | Fixture | Record BG initial Z position |
| Test | Fixture | `WaitForZStabilizationAsync` — polls Z samples, requires 3 consecutive within 0.1y |
| Assert | Test | Z > -500 (not fallen through world) and Z is stable |

### Teleport_PlayerMovesToNewPosition
| Phase | Actor | Action |
|-------|-------|--------|
| Setup | Fixture | `EnsureCleanSlateAsync` for BG (and FG) |
| Test | Fixture | Record before-position |
| Test | Fixture | `BotTeleportAsync` to Razor Hill (326, -4706, 15) — bot chat `.go xyz` |
| Test | Fixture | Poll for arrival within 35y for up to 12s |
| Assert | Test | Displacement > 5y (if was far), Z stable, MOVEFLAG_FORWARD cleared |

### Snapshot_SeesNearbyUnits
| Phase | Actor | Action |
|-------|-------|--------|
| Setup | Fixture | `EnsureCleanSlateAsync`, teleport near Razor Hill if needed |
| Test | Fixture | Poll for `NearbyUnits.Count > 0` up to 10s |
| Assert | Test | At least 1 nearby unit visible in snapshot |

### Snapshot_SeesNearbyGameObjects
Same as above but checks `NearbyObjects` instead of `NearbyUnits`.

### SetLevel_ChangesPlayerLevel
| Phase | Actor | Action |
|-------|-------|--------|
| Setup | Fixture | `EnsureCleanSlateAsync` |
| Test | Fixture | Check baseline level |
| Test | Bot Chat | `.character level {name} 10` (tracked, fallback to SOAP `SetLevelAsync`) |
| Test | Fixture | Poll up to 10s for level >= 10 in snapshot |
| Assert | Test | Level >= 10 |

---

## 2. BuffDismissTests

**File:** `BuffDismissTests.cs` | **Bots:** BG required, FG optional

### DismissBuff_LionsStrength_RemovedFromAuras
| Phase | Actor | Action |
|-------|-------|--------|
| Setup | Fixture | `EnsureCleanSlateAsync` |
| Setup | Bot Chat | `.unaura 2367`, `.unaura 2457` — remove stale buffs |
| Setup | Fixture | `BotClearInventoryAsync` — destroy all backpack items |
| Test | Fixture | `BotAddItemAsync(2454, 1)` — Elixir of Lion's Strength |
| Test | Fixture | Poll for item in bags |
| Test | Action | `UseItem(intParam=2454)` — use elixir |
| Test | Fixture | Poll up to 5s for Lion's Strength buff in auras |
| Test | Action | `DismissBuff(stringParam="Lion's Strength")` |
| Test | Fixture | Poll up to 3s for buff removal |
| Known Gap | — | BB-BUFF-001: BG WoWUnit.Buffs never populated from packets, DismissBuff is no-op |
| Cleanup | Bot Chat | `.unaura` if dismiss failed (within scenario) |

---

## 3. CharacterLifecycleTests

**File:** `CharacterLifecycleTests.cs` | **Bots:** BG required, FG optional

### Equipment_AddItemToInventory
| Phase | Actor | Action |
|-------|-------|--------|
| Setup | Fixture | `EnsureStrictAliveAsync` |
| Setup | Test | Clear inventory if bag near full or item already present |
| Test | Bot Chat | `.additem {LinenCloth} {count}` (tracked) |
| Test | Fixture | Poll up to 10s for item in bag snapshot |
| Assert | Test | Item slot count increased |

### Consumable_AddPotionToInventory
Same flow as above with item=MinorHealingPotion (118), count=5.

### Death_KillAndRevive
| Phase | Actor | Action |
|-------|-------|--------|
| Setup | Fixture | `EnsureStrictAliveAsync`, verify baseline alive |
| Test | Fixture | `InduceDeathForTestAsync` — tries `.die`, `.kill`, `.damage 5000` via chat, SOAP fallback (15s timeout) |
| Test | Fixture | Poll up to 10s for dead/ghost state |
| Test | SOAP | `RevivePlayerAsync` — `.revive {name}` |
| Test | Fixture | `WaitForSnapshotConditionAsync(IsStrictAlive)` up to 20s |
| Assert | Test | Health > 0, strict-alive |

### CharacterCreation_InfoAvailable
| Phase | Actor | Action |
|-------|-------|--------|
| Test | Fixture | `RefreshSnapshotsAsync` |
| Assert | Test | BG InWorld, has name/account/position/GUID |
| Assert | Test | FG same (if available) |

Read-only. No commands.

---

## 4. CombatLoopTests

**File:** `CombatLoopTests.cs` | **Bots:** COMBATTEST only (dedicated non-GM account)

### Combat_AutoAttacksMob_DealsDamageInMeleeRange
| Phase | Actor | Action |
|-------|-------|--------|
| Skip | Test | If `CombatTestAccountName == null` |
| Setup | Fixture | `EnsureStrictAliveAsync` |
| Phase 1 | Bot Chat | `.learn 198` (One Hand Maces), `.setskill 54 1 300`, `.additem 36` (Worn Mace) |
| Phase 1 | Action | `EquipItem(intParam=36)` |
| Phase 2 | Fixture | `EnsureNearMobAreaAsync` — teleport to Valley of Trials (-284, -4383, 57) if > 80y away |
| Phase 2 | Fixture | `WaitForSnapshotConditionAsync(NearbyUnits > 0)` up to 5s |
| Phase 3 | Test | `FindLivingMobAsync` — searches for entries {3098, 3124, 3108}, HP > 0, maxHP <= 500, level <= 10, NpcFlags=0. If none: `.npc add temp 3098`. |
| Diagnostic | Test | Verify PLAYER_FLAGS_GM is CLEAR (0x08 bit), check factionTemplate (expect 2=Orc). Abort if GM flag set. |
| Phase 4 | Fixture | `BotTeleportAsync` to mob's exact position, wait 2s settle |
| Phase 5 | Test | Re-verify target alive, retry if dead |
| Phase 5 | Action | `StartMeleeAttack(longParam=targetGuid)` |
| Phase 6 | Test | `WaitForMobDeathAsync` — polls up to 90s. Logs HP changes swing-by-swing. Detects evade (target cleared + HP full). Diagnostics every 5s (distance, GM flag, faction). |
| Assert | Test | Mob killed (HP reached 0) with at least 1 confirmed hit |

No `.gm on`, no `.gm off`, no logout/relog. COMBATTEST never has GM mode — factionTemplate stays correct.

---

## 5. CombatRangeTests

**File:** `CombatRangeTests.cs` | **Bots:** BG required, FG optional for parity

### CombatReach_PopulatedInSnapshot_ForPlayerAndMobs
| Phase | Actor | Action |
|-------|-------|--------|
| Setup | Test | `EnsureAliveAndNearMobsAsync` — teleport to mob area (-620, -4385, 44) |
| Assert | Test | Player CombatReach in 0.5-5.0 range |
| Assert | Test | Log nearby mob CombatReach/BoundingRadius values |

### MeleeAttack_WithinRange_TargetIsSelected
| Phase | Actor | Action |
|-------|-------|--------|
| Setup | Fixture | `EnsureCleanSlateAsync`, teleport to mob area |
| Setup | Bot Chat | `.respawn` — ensure fresh mobs |
| Test | Test | Find living boar |
| Test | Action | `Goto` to mob (walk, not teleport — syncs position with server) |
| Test | Action | `StartMeleeAttack(targetGuid)` |
| Test | Fixture | Poll up to 8s for TargetGuid match OR mob HP decrease |
| Assert | Test | Attack succeeded |
| Cleanup | Action | `StopAttack` + `.damage 5000` to kill mob |

### MeleeAttack_OutsideRange_DoesNotStartCombat
| Phase | Actor | Action |
|-------|-------|--------|
| Setup | Test | Find boar near mob area |
| Setup | Action/Chat | `StopAttack` + `.combatstop` |
| Test | Fixture | Teleport 200y south (out of melee range) |
| Test | Action | `StartMeleeAttack` on distant mob |
| Assert | Test | Bot > 100y from mob, distance > 10y (outside melee range) |
| Cleanup | Test | `StopAttack`, teleport back to mob area |

### MeleeRange_Formula_MatchesCombatDistanceCalculation
| Phase | Actor | Action |
|-------|-------|--------|
| Test | Test | Get nearby mobs, compute `CombatDistance` static/leeway range for each |
| Assert | Test | Range >= NOMINAL_MELEE_RANGE, < 20y. Leeway = static + 2.0 |

No teleports. Pure calculation validation.

### AutoAttack_StartAndStop_StopsCorrectly
| Phase | Actor | Action |
|-------|-------|--------|
| Setup | Fixture | Teleport to mob area, `.respawn`, find boar |
| Test | Action | `Goto` to mob, `StartMeleeAttack` |
| Test | Fixture | Wait for target GUID or HP decrease (8s) |
| Test | Action | `StopAttack` |
| Cleanup | Bot Chat | `.damage 5000` to kill mob |

### RangedAttack_WithinRange_TargetIsSelected
| Phase | Actor | Action |
|-------|-------|--------|
| Setup | Test | `EnsureAliveAndNearMobsAsync`, `.additem 2947 100` (thrown knives) |
| Test | Action | `StartRangedAttack(targetGuid)`, poll 6s for TargetGuid |
| Assert | Test | Target selected (Skip if no ranged weapon equipped) |
| Cleanup | Action/Chat | `StopAttack` + `.damage 5000` |

### RangedAttack_OutsideRange_DoesNotStartCombat
Same pattern as melee out-of-range but checks > 30y distance for ranged.

### InteractionDistance_UsesBoundingRadius
Pure calculation validation — `CombatDistance.GetInteractionDistance(boundingRadius)` >= 5.0 and < 10y.

---

## 6. ConsumableUsageTests

**File:** `ConsumableUsageTests.cs` | **Bots:** BG required, FG optional

### UseConsumable_ElixirOfLionsStrength_BuffApplied
| Phase | Actor | Action |
|-------|-------|--------|
| Setup | Bot Chat | `.unaura 2367`, `.unaura 2457` — remove stale buffs |
| Setup | Fixture | `BotClearInventoryAsync` |
| Setup | Fixture | Record aura count before |
| Test | Fixture | `BotAddItemAsync(2454, 3)` — 3x Elixir of Lion's Strength |
| Test | Fixture | Poll up to 3s for item in bags |
| Test | Action | `UseItem(intParam=2454)` |
| Test | Fixture | Poll up to 3s for buff aura (checks spell IDs 2367 and 2457) |
| Assert | Test | Buff detected in auras |

---

## 7. CraftingProfessionTests

**File:** `CraftingProfessionTests.cs` | **Bots:** BG required, FG optional

### FirstAid_LearnAndCraft_ProducesLinenBandage
| Phase | Actor | Action |
|-------|-------|--------|
| Setup | Fixture | `EnsureCleanSlateAsync` |
| Setup | Fixture | Check SpellList for First Aid (3273) and Linen Bandage recipe (3275). Learn if missing via `BotLearnSpellAsync`. |
| Setup | Fixture | Clear bags if bandages present or no cloth + near-full. Add Linen Cloth via `BotAddItemAsync` if missing. |
| Test | Action | `CastSpell(intParam=3275)` — craft Linen Bandage |
| Test | Fixture | Poll up to 8s for Linen Bandage (1251) in bag snapshot |
| Assert | Test | Bandage crafted |
| Diagnostic | Fixture | `DumpSnapshotDiagnostics` on failure |

---

## 8. DeathCorpseRunTests

**File:** `DeathCorpseRunTests.cs` | **Bots:** BG required, FG optional | **Requires:** PathfindingService

### Death_ReleaseAndRetrieve_ResurrectsPlayer
| Phase | Actor | Action |
|-------|-------|--------|
| Skip | Constructor | `Skip.IfNot(IsPathfindingReady)` |
| Setup | Fixture | `EnsureCleanSlateAsync` |
| Setup | Fixture | Teleport to Orgrimmar via `BotTeleportToNamedAsync`, poll for position |
| Kill | Fixture | `InduceDeathForTestAsync(requireCorpseTransition: true)`, capture corpse position |
| Release | Action | `ReleaseCorpse`, poll up to 10s for ghost state (PlayerFlags & 0x10) |
| Ghost | Fixture | Wait for graveyard teleport, record ghost position, compute distance to corpse |
| Runback | Action | `RetrieveCorpse` (triggers pathfinding runback). Poll every 2s up to 60s. Log progress every 10s. Wait for distance <= 39y. |
| Reclaim | Fixture | Wait for reclaim delay <= 0 (up to 45s) |
| Resurrect | Action | `RetrieveCorpse` again |
| Assert | Fixture | `IsStrictAlive` within 15s |
| **Cleanup** | SOAP/Fixture | **Always runs (finally):** `RevivePlayerAsync` for both chars, teleport both to Orgrimmar |

---

## 9. EconomyInteractionTests

**File:** `EconomyInteractionTests.cs` | **Bots:** BG required, FG optional

### Bank_OpenAndDeposit
| Phase | Actor | Action |
|-------|-------|--------|
| Setup | Test | Ensure alive + teleport to Orgrimmar bank (1627, -4376, 11.8) |
| Test | Action | Find NPC with `UNIT_NPC_FLAG_BANKER`, send `InteractWith(npcGuid)` |
| Assert | Test | Interaction returned `Success` |

### AuctionHouse_OpenAndList
Same pattern at AH coords (1687, -4464, 20.1) with `UNIT_NPC_FLAG_AUCTIONEER`.

### Mail_OpenMailbox
| Phase | Actor | Action |
|-------|-------|--------|
| Setup | Test | Teleport to mailbox coords (1615, -4391, 10.1) |
| Test | Fixture | Poll NearbyObjects for 5s, find object with name containing "mail" |
| Test | Action | `InteractWith(mailboxGuid)` |
| Assert | Test | Interaction returned `Success` |

---

## 10. EquipmentEquipTests

**File:** `EquipmentEquipTests.cs` | **Bots:** BG required, FG optional

### EquipItem_AddWeaponAndEquip_AppearsInEquipmentSlot
| Phase | Actor | Action |
|-------|-------|--------|
| Setup | Fixture | `EnsureCleanSlateAsync` |
| Setup | Test | If mainhand occupied: `.reset items` via SOAP. If missing mace proficiency: `.learn 198` + `.setskill 54 1 300`. |
| Setup | Fixture | Clear bags if near full. `BotAddItemAsync(36)` — Worn Mace. Poll for bag appearance. |
| Test | Action | `EquipItem(intParam=36)` |
| Test | Fixture | Poll up to 3s for mainhand slot change (GUID changed + bag count dropped) |
| Assert | Test | Mainhand equipped with Worn Mace |
| Diagnostic | Fixture | `DumpSnapshotDiagnostics` on failure |

---

## 11. FishingProfessionTests

**File:** `FishingProfessionTests.cs` | **Bots:** BG primary, FG parked at Orgrimmar

### Fishing_CatchFish_SkillIncreases
| Phase | Actor | Action |
|-------|-------|--------|
| Setup | Test | `PrepareBot` — revive, learn all fishing spells (7733, 7734, 7620, 7731, 7732, 18248, 7738), `.setskill fishing 1 300`, `.reset items`, add fishing pole + Shiny Bauble, equip pole |
| Setup | Test | Park FG at Orgrimmar (reduce coordinator interference) |
| Test | Test | Iterate 4 fishing spots (Ratchet dock x2, Durotar coast, Sen'jin Village) |
| Per Spot | Fixture | Teleport, wait for Z stabilization (4s), set facing |
| Per Spot | Action | `CastSpell(fishingRank1)`, wait 3s |
| Per Spot | Fixture | Check channel spell + bobber (displayId 668 or gameObjectType 17) |
| Per Spot | Fixture | Poll up to 22s for skill increase (3s intervals) |
| Assert | Test | At least one catch across all spots (skill increase not asserted — RNG) |

---

## 12. GatheringProfessionTests

**File:** `GatheringProfessionTests.cs` | **Bots:** FG first (gold standard), then BG

### Mining_GatherCopperVein_SkillIncreases
| Phase | Actor | Action |
|-------|-------|--------|
| Setup | MySQL | Query `mangos.gameobject` for Copper Vein (1731) spawns on Kalimdor, sorted by distance from Orgrimmar |
| Skip | Test | If no spawns found |
| FG Phase | Test | Park BG at Orgrimmar. `PrepareMining` (learn 2575, set skill, add mining pick). Iterate up to 3 nearest spawns. |
| BG Phase | Test | Park FG at Orgrimmar. Same flow. |
| Per Spawn | Fixture | Teleport to spawn +3 Z, wait for Z stabilization |
| Per Spawn | Fixture | Scan NearbyObjects for node entry up to 3s |
| Per Spawn | Action | If > 4y away: `Goto` action, poll up to 30s for arrival within 5y |
| Per Spawn | Action | `GatherNode(guid, gatherSpellId)`, wait 9s |
| Per Spawn | Fixture | Poll skill up to 40s for increase |
| Assert | Test | Gathered at least once (or skip if no nodes spawned) |
| **Cleanup** | Fixture | **Finally:** `ReturnToSafeZoneAsync` — teleport back to Orgrimmar if > 80y away |

### Herbalism_GatherHerb_SkillIncreases
Same flow but queries Peacebloom (1617), Silverleaf (1618), Earthroot (1619) spawns. Uses herbalism spells.

---

## 13. GroupFormationTests

**File:** `GroupFormationTests.cs` | **Bots:** Both BG and FG REQUIRED

### GroupFormation_InviteAccept_StateIsTrackedAndCleanedUp
| Phase | Actor | Action |
|-------|-------|--------|
| Skip | Test | If FG not actionable |
| Setup | Test | `EnsureNotGroupedAsync` for both — DisbandGroup/LeaveGroup if PartyLeaderGuid != 0 |
| Assert | Test | Both PartyLeaderGuid == 0 |
| Test | Action (FG) | `SendGroupInvite(stringParam=bgCharName)`, wait 1.2s |
| Test | Action (BG) | `AcceptGroupInvite`, wait 1.5s |
| Test | Fixture | `WaitForGroupFormationAsync` — poll up to 20s for matching non-zero PartyLeaderGuid == FG GUID |
| Assert | Test | Group formed |
| Cleanup | Test | `EnsureNotGroupedAsync` for both (deterministic, not in finally block) |

---

## 14. LootCorpseTests

**File:** `LootCorpseTests.cs` | **Bots:** BG required, FG optional

### Loot_KillAndLootMob_InventoryChanges
| Phase | Actor | Action |
|-------|-------|--------|
| Setup | Fixture | `EnsureCleanSlateAsync`, `BotClearInventoryAsync`, record baseline bag count |
| Setup | Fixture | Teleport to Valley of Trials boar area (-620, -4385, 44) |
| Find | Test | Scan for living boar (name "Boar" or entry 3098, HP > 0). If none: `.respawn` + retry. Skip if still none. |
| Position | Fixture | Teleport to boar position (+2 X, +3 Z) |
| Kill | Bot Chat/Action | `.targetself`, `StartMeleeAttack(boarGuid)`, `.damage 500`, poll up to 20s for HP=0, `StopAttack` |
| Loot | Action | `LootCorpse(boarGuid)`, wait 500ms |
| Assert | Fixture | Poll up to 10s for bag count > baseline. WARNING only if no loot (empty loot table). Returns true if kill succeeded. |

---

## 15. MapTransitionTests

**File:** `MapTransitionTests.cs` | **Bots:** BG required, FG optional

### MapTransition_DeeprunTramBounce_ClientSurvives
| Phase | Actor | Action |
|-------|-------|--------|
| Setup | Bot Chat | `.gm on` for both bots (Horde in Alliance city) |
| Test | Bot Chat | `.go xyz -4838 -1317 505 0` — teleport to Ironforge Tinker Town |
| Test | Fixture | `WaitForTeleportSettledAsync` (5s), verify near Ironforge |
| Test | Bot Chat | `.go xyz -4838 -1317 502 369` — teleport INTO Deeprun Tram (map 369) |
| Test | Fixture | Poll up to 10s for ScreenState == "InWorld" with valid position |
| Assert | Test | Client didn't crash (snapshot not null), InWorld, position not (0,0) |
| Return | Bot Chat | `.go xyz 1629 -4373 18 1` — back to Orgrimmar |

---

## 16. NavigationTests

**File:** `NavigationTests.cs` | **Bots:** BG required, FG optional | **Requires:** PathfindingService

### Navigation_ShortPath_ArrivesAtDestination
| Phase | Actor | Action |
|-------|-------|--------|
| Setup | Fixture | `EnsureCleanSlateAsync`, teleport to start (340, -4686, 16.5) |
| Test | Action | `Goto(endX=310, endY=-4720, endZ=11, tolerance=0)` |
| Test | Fixture | Poll every 1.5s up to 20s. Log position + distance each poll. Track best distance + cumulative travel. |
| Assert | Test | Distance to destination <= 8y (ArrivalRadius) |

### Navigation_CityPath_ArrivesAtDestination
Same flow. Start: (1629, -4373, 34), End: (1660, -4420, 34). Timeout: 45s. Through Orgrimmar Valley of Strength.

---

## 17. NpcInteractionTests

**File:** `NpcInteractionTests.cs` | **Bots:** BG required, FG optional

### Vendor_OpenAndSeeInventory
| Phase | Actor | Action |
|-------|-------|--------|
| Setup | Test | Revive if dead, teleport to Razor Hill vendor (340, -4686, 16.5) if > 40y |
| Test | Fixture | Find NPC with `UNIT_NPC_FLAG_VENDOR` (up to 3 snapshot refreshes) |
| Test | Action | `InteractWith(vendorGuid)` |
| Assert | Test | Interaction returned `Success` |

### Vendor_SellJunkItems
Same as above + pre-adds 5x Linen Cloth via `BotAddItemAsync`.

### Trainer_OpenAndSeeSpells
Same pattern at trainer coords (311, -4827, 9.7) with `UNIT_NPC_FLAG_TRAINER`.

### Trainer_LearnAvailableSpells
Same + ensures money >= 10000 copper (`.modify money`) and level >= 10 (`.character level`).

### FlightMaster_DiscoverNodes
Same pattern at Orgrimmar FM coords (1676, -4313, 61.7) with `UNIT_NPC_FLAG_FLIGHTMASTER`.

### ObjectManager_DetectsNpcFlags
| Phase | Actor | Action |
|-------|-------|--------|
| Setup | Test | Teleport both bots to Razor Hill vendor area |
| Test | Fixture | Refresh snapshots up to 3x (1s between retries) |
| Assert | Test | BG NearbyUnits count > 0, at least one unit has NpcFlags != 0 |

---

## 18. OrgrimmarGroundZAnalysisTests

**File:** `OrgrimmarGroundZAnalysisTests.cs` | **Bots:** BG required, FG optional

### DualClient_OrgrimmarGroundZ_PostTeleportSnap
| Phase | Actor | Action |
|-------|-------|--------|
| Per Probe | Fixture | Teleport both bots to probe position at RecZ+3 (3 positions in Orgrimmar) |
| Per Probe | Fixture | `WaitForTeleportSettledAsync` for BG |
| Per Probe | Test | Read BG Z and FG Z, compute deltas against known SimZ values |
| Assert | Test | Failures list empty. Pass criteria: |bgSimDelta| <= 1.5 (OK) or <= 5.0 (warning). > 5.0 = Z_DRIFT fail. |

### DualClient_OrgrimmarGroundZ_StandAndWalk
| Phase | Actor | Action |
|-------|-------|--------|
| Setup | Fixture | Teleport both bots to ValleyOfStrength_A at RecZ+3 |
| Test | Fixture | Sample position 10x at 1s intervals, log BG/FG X/Y/Z and delta |

**No assertions.** Purely diagnostic — outputs a table for manual review.

---

## 19. QuestInteractionTests

**File:** `QuestInteractionTests.cs` | **Bots:** BG required, FG optional

### Quest_AddCompleteAndRemove_AreReflectedInSnapshots
| Phase | Actor | Action |
|-------|-------|--------|
| Setup | Fixture | `EnsureCleanSlateAsync` |
| Setup | Test | `EnsureQuestAbsentAsync` — `.quest remove 786` if present, poll up to 12s |
| Step 1 | Bot Chat | `.targetself` + `.quest add 786` (Encroachment). Poll up to 12s for quest in QuestLogEntries. |
| Step 2 | Bot Chat | `.quest complete 786`. Poll up to 12s for quest log change. |
| Step 3 | Bot Chat | `.quest remove 786`. Poll up to 12s for quest absence. |
| **Cleanup** | **Finally** | If quest still in snapshot, `.quest remove 786` to prevent contamination. |

---

## 20. SpellCastOnTargetTests

**File:** `SpellCastOnTargetTests.cs` | **Bots:** BG required, FG optional

### CastSpell_BattleShout_AuraApplied
| Phase | Actor | Action |
|-------|-------|--------|
| Setup | Fixture | `EnsureCleanSlateAsync` |
| Setup | Fixture | `BotLearnSpellAsync(6673)` — Battle Shout Rank 1. Poll up to 5s for SpellList. |
| Setup | Bot Chat | `.modify rage 1000` (100 displayed rage), `.unaura 6673` (remove existing buff) |
| Test | Action | `CastSpell(intParam=6673)` — self-buff, no target |
| Test | Fixture | Poll up to 8s for aura 6673 in player Auras |
| Assert | Test | Buff applied |
| Cleanup | Bot Chat | `.unaura 6673` (not in finally block) |

---

## 21. StarterQuestTests

**File:** `StarterQuestTests.cs` | **Bots:** BG required, FG optional

### Quest_AcceptAndTurnIn_StarterQuest
| Phase | Actor | Action |
|-------|-------|--------|
| Setup | Fixture | `EnsureCleanSlateAsync`, teleport to Orgrimmar safe zone to stabilize |
| Setup | Test | `EnsureQuestAbsentAsync` — force-remove quest 4641, poll up to 8s |
| Step 1 | Fixture | Teleport to Kaltunk (-607, -4251, 39) in Valley of Trials at Z+3 |
| Step 2 | Test | `FindNpcByEntryAsync(10176)` — search NearbyUnits up to 3 attempts |
| Step 3 | Action | `AcceptQuest(kaltunkGuid, questId=4641)`. Up to 2 attempts (re-remove + retry). Poll up to 10s for quest in snapshot. |
| Step 5 | Fixture | Teleport to Gornek (-600, -4186, 41) at Z+3 |
| Step 6 | Test | `FindNpcByEntryAsync(3143)`. If not found: `.respawn` + retry. |
| Step 7 | Action | `CompleteQuest(gornekGuid, questId=4641)` |
| Step 8 | Fixture | Poll up to 10s for quest absent from snapshot |
| **Cleanup** | **Finally** | Remove quest if still present. Teleport back to Orgrimmar safe zone. |

---

## 22. TalentAllocationTests

**File:** `TalentAllocationTests.cs` | **Bots:** BG required, FG optional

### Talent_LearnViaGM_SpellAppearsInKnownSpells
| Phase | Actor | Action |
|-------|-------|--------|
| Setup | Fixture | `EnsureCleanSlateAsync` |
| Setup | Test | `EnsureLevelAtLeastAsync(10)` — `.character level 10` if below, poll up to 15s |
| Setup | Bot Chat | `.unlearn 16462` (Deflection) — always sends regardless of snapshot (client-server desync) |
| Test | Bot Chat | `.targetself` + `.learn 16462` (tracked). Assert dispatch + no rejection. |
| Test | Fixture | Poll up to 12s for spell 16462 in SpellList (logs every 2s) |
| Assert | Test | Spell appears in known spells |

---

## 23. UnequipItemTests

**File:** `UnequipItemTests.cs` | **Bots:** BG required, FG optional

### UnequipItem_MainhandWeapon_MovesToBags
| Phase | Actor | Action |
|-------|-------|--------|
| Setup | Fixture | `EnsureCleanSlateAsync`, `BotClearInventoryAsync` (ensure bag space) |
| Setup | Fixture | `BotLearnSpellAsync(198)` + `BotSetSkillAsync(54, 1, 300)` — mace proficiency |
| Setup | Fixture | `BotAddItemAsync(36)` — Worn Mace. Poll for bag. `EquipItem(36)`. Poll for mainhand slot filled. |
| Pre-state | Test | Record mainhand GUID + mace count in bags |
| Test | Action | `UnequipItem(intParam=16)` — MainHand slot |
| Test | Fixture | Poll up to 5s for mainhand GUID to become 0 |
| Assert | Test | Mainhand empty, mace back in bags |

---

## 24. VendorBuySellTests

**File:** `VendorBuySellTests.cs` | **Bots:** BG required, FG optional

### Vendor_BuyItem_AppearsInInventory
| Phase | Actor | Action |
|-------|-------|--------|
| Setup | Fixture | `EnsureCleanSlateAsync`, `BotClearInventoryAsync` |
| Setup | Fixture | Teleport to Grimtak vendor (305, -4665, 16.5) at Z+1 |
| Setup | Test | `FindNpcByFlagAsync(VENDOR)` — 3 attempts, 1s retry |
| Setup | Bot Chat | If coinage < 1000: `.modify money 1000` |
| Pre-state | Test | Record Refreshing Spring Water count in bags |
| Test | Action | `BuyItem(vendorGuid, itemId=159, qty=1)` |
| Test | Fixture | Poll up to 12s for item count increase |
| Assert | Test | Item appeared in inventory |
| Cleanup | Test | `DestroyItemByIdAsync` — finds item slot, sends `DestroyItem` action (try/catch) |

### Vendor_SellItem_RemovedFromInventory
| Phase | Actor | Action |
|-------|-------|--------|
| Setup | Fixture | `EnsureCleanSlateAsync`, teleport to Grimtak |
| Setup | Test | Find vendor NPC |
| Setup | Bot Chat | `.additem 2589 1` (Linen Cloth). Poll for bag appearance. |
| Pre-state | Test | `FindItemBagSlot` — locate slot (bagId=0xFF for backpack, absolute slotId) |
| Test | Action | `SellItem(vendorGuid, bagId, slotId, qty=1)` |
| Test | Fixture | Poll up to 8s for Linen Cloth count to reach 0 |
| Assert | Test | Item removed from inventory |

---

## 25. FishingProfessionTests (detailed above as #11)

---

## Cross-Test Summary

| Test | GM Setup | Teleports | Actions Tested | Cleanup |
|------|----------|-----------|----------------|---------|
| BasicLoop (6 tests) | EnsureCleanSlate | Razor Hill | Read-only, teleport, level | None |
| BuffDismiss | EnsureCleanSlate | None | UseItem, DismissBuff | `.unaura` in scenario |
| CharacterLifecycle (4 tests) | EnsureStrictAlive | None | AddItem, Death/Revive | None |
| CombatLoop | **None (COMBATTEST)** | Valley of Trials | EquipItem, StartMeleeAttack | None |
| CombatRange (8 tests) | EnsureCleanSlate | Mob area + 200y south | StartMelee/RangedAttack, StopAttack, Goto | StopAttack + .damage |
| ConsumableUsage | unaura + clear inv | None | UseItem | None |
| CraftingProfession | EnsureCleanSlate | None | CastSpell (craft) | None |
| DeathCorpseRun | EnsureCleanSlate | Orgrimmar | ReleaseCorpse, RetrieveCorpse | **Finally: Revive + teleport** |
| EconomyInteraction (3 tests) | Revive | Orgrimmar bank/AH/mail | InteractWith | None |
| EquipmentEquip | EnsureCleanSlate | None | EquipItem | None |
| Fishing | Learn spells + equip | 4 fishing spots | CastSpell (fishing) | None |
| Gathering (2 tests) | Learn spells + equip | DB-queried spawns | Goto, GatherNode | **Finally: ReturnToSafeZone** |
| GroupFormation | EnsureNotGrouped | None | SendGroupInvite, AcceptGroupInvite, Disband/Leave | Deterministic disband |
| LootCorpse | EnsureCleanSlate | Valley of Trials | StartMeleeAttack, StopAttack, LootCorpse | None |
| MapTransition | `.gm on` | Ironforge, Deeprun, Orgrimmar | None (teleport-only) | Return to Orgrimmar |
| Navigation (2 tests) | EnsureCleanSlate | Start position | Goto | None |
| NpcInteraction (6 tests) | Revive | Razor Hill, Orgrimmar | InteractWith | None |
| OrgrimmarGroundZ (2 tests) | None | 3 Orgrimmar probes | None (diagnostic) | None |
| QuestInteraction | EnsureCleanSlate | None | None (GM quest cmds) | **Finally: quest remove** |
| SpellCast | EnsureCleanSlate | None | CastSpell | `.unaura` (not in finally) |
| StarterQuest | EnsureCleanSlate | Valley of Trials NPCs | AcceptQuest, CompleteQuest | **Finally: quest remove + teleport** |
| TalentAllocation | EnsureCleanSlate | None | None (GM learn cmds) | None |
| UnequipItem | EnsureCleanSlate | None | EquipItem, UnequipItem | None |
| VendorBuySell (2 tests) | EnsureCleanSlate | Grimtak vendor | BuyItem, SellItem | DestroyItem (try/catch) |
