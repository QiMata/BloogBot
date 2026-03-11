# CombatLoopTests

Tests full melee combat: weapon setup, mob targeting, auto-attack, and damage dealing. Uses dedicated COMBATTEST account that **never receives `.gm on`** — factionTemplate stays correct so mobs engage normally.

## Test Methods (1)

### Combat_AutoAttacksMob_DealsDamageInMeleeRange

**Bot:** COMBATTEST only (Background runner, GM level 6, NO `.gm on`)

**Why COMBATTEST?** `.gm on` corrupts the player's factionTemplate, causing mobs to evade instead of fighting. COMBATTEST has account-level GM access (for `.learn`, `.additem` etc.) but never has the GM player flag set.

**Fixture Setup:** LiveBotFixture identifies COMBATTEST by exact account name match. COMBATTEST is excluded from Orgrimmar staging teleport (rapid back-to-back SOAP teleports disconnect BG clients).

**Test Flow (RunCombatScenarioAsync):**

| Phase | Step | Action | Details |
|-------|------|--------|---------|
| Setup | 1 | Ensure alive | `EnsureStrictAliveAsync(account, "COMBAT")` |
| Setup | 2 | Learn weapon skill | `BotLearnSpellAsync(198)` — One Hand Maces proficiency |
| Setup | 3 | Set skill level | `BotSetSkillAsync(account, 54, 1, 300)` — Maces skill to 1/300 |
| Setup | 4 | Add weapon | `BotAddItemAsync(account, 36)` — Worn Mace |
| Setup | 5 | Equip weapon | **Dispatch `ActionType.EquipItem`** with `IntParam = 36` |
| Position | 6 | Teleport to mob area | `EnsureNearMobAreaAsync()` → Valley of Trials (Map=1, X=-284, Y=-4383, Z=57) |
| Position | 7 | Respawn mobs | `.respawn` via GM chat, wait for `NearbyUnits.Count > 0` |
| Target | 8 | Find mob | Filter NearbyUnits: creature GUID pattern (0xF000...), Health > 0, Entry in [3098, 3124, 3108], Level <= 10, MaxHealth <= 500, NpcFlags == 0, closest |
| Target | 9 | Fallback spawn | If none: `.npc add temp 3098` and retry |
| Engage | 10 | Walk to mob | **Dispatch `ActionType.Goto`** with `FloatParams = [mobX, mobY, mobZ, 0]` |
| Engage | 11 | Verify GM flag clear | Assert `(playerFlags & 0x08) == 0` — PLAYER_FLAGS_GM must NOT be set |
| Engage | 12 | Attack | **Dispatch `ActionType.StartMeleeAttack`** with `LongParam = targetGuid` |
| Verify | 13 | Wait for mob death | Poll NearbyUnits for target.Health == 0 (90s timeout). Log damage progression. Verify first damage confirmed (not instant death). Check no evade. |

**StateManager/BotRunner Action Flow:**

- **EquipItem:** `BuildEquipItemByIdSequence(36)` → find Worn Mace in bags → `_objectManager.EquipItem()` → CMSG_AUTOEQUIP_ITEM
- **Goto:** `BuildGoToSequence(x, y, z, 0)` → PathfindingService builds waypoint path → movement controller walks each segment
- **StartMeleeAttack:** `BuildStartMeleeAttackSequence(targetGuid)` → `_objectManager.SetTarget(guid)` + `_objectManager.StartMeleeAttack()` → CMSG_ATTACK_SWING packet

**Key IDs:**
- Item 36 = Worn Mace
- Spell 198 = One Hand Maces proficiency
- Skill 54 = Maces
- Mob entries: 3098 (Mottled Boar), 3124 (Scorpid Worker), 3108 (Vile Familiar)

**Diagnostic Checks:**
- Faction template read (expect 2 = Orc for COMBATTEST)
- GM flag verification (MUST be clear — 0x08 bit)
- Movement flags during approach

**Assertions:** GM flag clear. Mob HP decreases over time. First damage confirmed (not instant). Mob dies (Health == 0). No evade behavior.
