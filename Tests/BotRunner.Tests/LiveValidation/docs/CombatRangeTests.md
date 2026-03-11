# CombatRangeTests

Tests melee/ranged combat range validation, CombatReach/BoundingRadius snapshot fields, and the attack start/stop lifecycle.

## Test Methods (8)

### 1. CombatReach_PopulatedInSnapshot_ForPlayerAndMobs

**Bots:** BG + FG

**Test Flow:**
1. Teleport to boar area (X=-620, Y=-4385, Z=44)
2. `.respawn`, find boar (entry 3098)
3. Assert: `player.CombatReach > 0`, `player.BoundingRadius > 0`
4. Assert: `mob.CombatReach > 0`, `mob.BoundingRadius > 0`

**StateManager/BotRunner Role:** Passive — CombatReach/BoundingRadius read from ObjectManager update fields.

---

### 2. MeleeAttack_WithinRange_TargetIsSelected

**Bots:** BG + FG

**Test Flow:**
1. `EnsureCleanSlateAsync()`, teleport to boar area
2. `.respawn`, `FindLivingBoarAsync()` — entry 3098, level <= 10, MaxHealth <= 500, distance <= 45y
3. **Dispatch `ActionType.Goto`** with mob position — walk to melee range
4. **Dispatch `ActionType.StartMeleeAttack`** with `LongParam = targetGuid`
5. Poll 8s for: target GUID in snapshot OR mob HP decrease OR mob dead

**StateManager/BotRunner Action Flow:**
- **Goto:** `BuildGoToSequence()` → PathfindingService path → walk waypoints
- **StartMeleeAttack:** `BuildStartMeleeAttackSequence(guid)` → target + CMSG_ATTACK_SWING

---

### 3. MeleeAttack_OutsideRange_DoesNotStartCombat

**Bots:** BG + FG (negative test)

**Test Flow:**
1. Find boar near mob area
2. **Dispatch `ActionType.StopAttack`**, `.combatstop` GM command
3. Teleport 200y away (X=-620, Y=-4585, Z=44)
4. Verify distance from mob > 100y
5. Attempt `ActionType.StartMeleeAttack` at 200y range
6. Assert: bot NOT in combat (no target GUID, distance still > melee range)

---

### 4. MeleeRange_Formula_MatchesCombatDistanceCalculation

**Bots:** BG + FG

**Test Flow:** Validates `CombatDistance.GetMeleeAttackRange()` formula:
```
max(NOMINAL_MELEE_RANGE, attacker.CombatReach + target.CombatReach + 4/3 + leeway)
leeway = 2.0y only if both moving (MOVEFLAG_MASK_XZ) AND neither walking
```

---

### 5. AutoAttack_StartAndStop_StopsCorrectly

**Bots:** BG + FG

**Test Flow:**
1. Find boar, walk to range
2. **Dispatch `ActionType.StartMeleeAttack`** — verify target set
3. **Dispatch `ActionType.StopAttack`** — verify combat ends

**StateManager/BotRunner Action Flow:**
- **StopAttack:** `StopAttackSequence` → `_objectManager.StopAttack()` → CMSG_ATTACK_STOP

---

### 6. RangedAttack_WithinRange_TargetIsSelected

**Bots:** BG + FG

**Test Flow:**
1. `.additem 2947 100` (Throwing Knife)
2. Find boar, position at 20y
3. **Dispatch `ActionType.StartRangedAttack`** with `LongParam = targetGuid`
4. Poll 6s for target GUID
5. Skip if not selected (AST-7: don't false-pass)
6. Cleanup: `.damage 5000` to kill mob

**StateManager/BotRunner Action Flow:**
- **StartRangedAttack:** `BuildStartRangedAttackSequence(guid)` → target + enable auto-shot

---

### 7. RangedAttack_OutsideRange_DoesNotStartCombat

Same as melee out-of-range but with ranged attack at 200y. Assert mob not targeted or distance > 30y (thrown max range).

---

### 8. InteractionDistance_UsesBoundingRadius

Validates `CombatDistance.GetInteractionDistance(boundingRadius)` = INTERACTION_DISTANCE (5.0) + radius.

---

## Key Constants

| Constant | Value | Description |
|----------|-------|-------------|
| NOMINAL_MELEE_RANGE | 5.0y | Minimum melee range |
| DEFAULT_PLAYER_COMBAT_REACH | 1.5y | Default player reach |
| MELEE_LEEWAY | 2.0y | Extra range when both moving |
| INTERACTION_DISTANCE | 5.0y | Base NPC interaction distance |

**Key IDs:** Item 2947 = Throwing Knife. Mob 3098 = Mottled Boar.

**Coordinates:** Melee area: (-620, -4385, 44). Far position: (-620, -4585, 44) — 200y south.
