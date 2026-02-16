# Tasks

> **Reference docs:**
> - `docs/ARCHIVE.md` — Completed task history (Phase 1, Phase 2, Phase 3, Phase 4.3)
> - `docs/TECHNICAL_NOTES.md` — Constants, env paths, recording mappings, known issues
> - `docs/PROJECT_STRUCTURE.md` — Directory layout and project descriptions

## Goal

Two WoW 1.12.1 clients: **Injected** (ForegroundBotRunner inside WoW.exe) and **Headless** (WoWSharpClient, standalone). Both must produce identical behavior for all supported operations: movement, combat, questing, NPC interaction. Coordinate both bots to work as a group — warrior tanks, shaman heals and deals damage.

**Completed:** Phase 1 (cleanup), Phase 2 (physics engine — 58 tests, 30K frames, avg<0.055y), Phase 3 partial, Phase 4.3 (bidirectional group formation)

---

## Phase 4.4 — Combat Coordination
**Priority: HIGH — current focus**

### Architecture

The **foreground bot** (ORWR1, Warrior "Dralrahgra") grinds autonomously via `GrindBot` — find target → move → attack → loot → rest. The **background bot** (ORSH1, Shaman "Lokgarn") is coordinated by `StateManager`. StateManager reads both bots' snapshots every tick and injects combat actions to the shaman based on the warrior's state.

```
┌────────────────┐           ┌──────────────────┐          ┌──────────────────┐
│ Foreground Bot  │ snapshot  │   StateManager    │ snapshot │  Background Bot   │
│ (Warrior)       │ ────────► │ CombatCoordinator │ ◄─────── │  (Shaman)         │
│                 │           │                   │          │                   │
│ GrindBot:       │           │ Reads both        │ actions  │ BotRunnerService: │
│ FindTarget      │           │ snapshots,        │ ────────►│ CastSpell         │
│ MoveToTarget    │           │ picks spells,     │          │ StartMeleeAttack  │
│ Combat (rotate) │           │ decides heal/dps  │          │ GoTo              │
│ Loot            │           │                   │          │                   │
│ Rest            │           │ Group formation   │          │ Follows warrior   │
└────────────────┘           │ already working   │          │ Heals when needed │
                             └──────────────────┘          └──────────────────┘
```

### Snapshot Data Available for Coordination

| Field | Source | Used For |
|-------|--------|----------|
| `player.unit.health/maxHealth` | Both bots | Heal decisions |
| `player.unit.power[0]/maxPower[0]` | Shaman | Mana management |
| `player.unit.power[1]` | Warrior | Rage tracking |
| `player.unit.targetGuid` | Warrior | Shared target |
| `player.unit.unitFlags` | Both | Combat detection (UNIT_FLAG_IN_COMBAT) |
| `player.unit.position` | Both | Range/follow decisions |
| `player.spellList` | Shaman | Available spells |
| `player.spellCooldowns` | Shaman | Cooldown tracking |
| `nearbyUnits` | Both | Threat awareness |
| `partyLeaderGuid` | Both | Group status verification |

---

### 4.4a Add StartMeleeAttack Action
**Priority: High — required for combat initiation**

The proto defines `START_MELEE_ATTACK = 32` but `MapProtoActionType` and `CharacterAction` don't include it. Add support so StateManager can tell the background bot to start melee auto-attack on a target.

**Changes:**
- `Exports/BotRunner/BotRunnerService.cs` — Add `CharacterAction.StartMeleeAttack`, mapping, and `BuildStartMeleeAttackSequence(ulong targetGuid)`
- Sequence: set target → face target → send CMSG_ATTACKSWING

---

### 4.4b CombatCoordinator State Machine
**Priority: High — replaces group test state machine**

Replace `InjectGroupCoordinationActions()` in `CharacterStateSocketListener` with a proper `CombatCoordinator` class. Handles group formation AND combat coordination.

**States:**
```
WaitingForBots          → both InWorld?        → FormGroup
FormGroup               → invite/accept done?  → GroupFormed
GroupFormed             → warrior in combat?   → CombatSupport
CombatSupport           → warrior target dead? → PostCombat
PostCombat              → looted?              → ReadyToGrind
ReadyToGrind            → warrior in combat?   → CombatSupport
                        → shaman mana low?     → ShamanResting
ShamanResting           → mana > 80%?          → ReadyToGrind
```

**CombatSupport Logic (per tick):**
1. Get warrior target from snapshot (`targetGuid`)
2. Check warrior health ratio (`health / maxHealth`)
3. **If warrior health < 50%**: Send `CAST_SPELL(HealingWave, warriorGuid)` to shaman
4. **If warrior health >= 50%**: Send `CAST_SPELL(LightningBolt, warriorTargetGuid)` to shaman
5. **If shaman mana < 10%**: Transition to ShamanResting (send no actions, let mana regen)
6. **If shaman not in range (> 30y from warrior)**: Send `GOTO(warrior.position)` to shaman

**Files:**
- NEW: `Services/WoWStateManager/Coordination/CombatCoordinator.cs`
- MODIFY: `Services/WoWStateManager/Listeners/CharacterStateSocketListener.cs` — delegate to CombatCoordinator

---

### 4.4c Shaman Spell Database
**Priority: High — needed for spell ID resolution**

Static lookup of vanilla 1.12.1 shaman spell IDs by category and rank. At runtime, cross-reference with shaman's `spellList` from snapshot to find highest available rank.

**Spell categories:**
| Category | Spells | Use Case |
|----------|--------|----------|
| Heal | Healing Wave (331, 332, 547, 913, 939, 959, 8005, 10395, 10396, 25357) | Heal warrior when health < 50% |
| DirectDamage | Lightning Bolt (403, 529, 548, 915, 943, 6041, 10391, 10392, 15207, 15208) | DPS warrior's target |
| Interrupt | Earth Shock (8042, 8044, 8045, 8046, 10412, 10413, 10414) | Interrupt enemy casts |
| DoT | Flame Shock (8050, 8052, 8053, 10447, 10448, 29228) | Apply DoT on target |

**Files:**
- NEW: `Services/WoWStateManager/Coordination/ShamanSpells.cs`

---

### 4.4d Follow Behavior
**Priority: Medium — shaman must stay near warrior**

When not in combat, send `GOTO` actions to the shaman to keep it within ~15y of the warrior. Use warrior's position from snapshot. Only send new GOTO when shaman is > 20y away (avoid constant re-pathing).

**Implementation:** Built into CombatCoordinator's `ReadyToGrind` and `GroupFormed` states.

---

### 4.4e Live Test Validation
**Priority: High — verify the system works**

Launch dual-bot test via StateManager. Verify:
1. Both bots login and enter world
2. Group forms automatically
3. Warrior finds and attacks a mob
4. Shaman casts Lightning Bolt on warrior's target
5. If warrior takes damage, shaman heals
6. Warrior loots, both bots ready for next mob
7. Cycle repeats for at least 3 kills

---

## Phase 3 — Headless Client Validation (remaining tasks, deprioritized)

### Test Baseline (1003 passing, 1 skipped, 0 failed)

| Test Suite | Passed | Failed | Notes |
|-----------|--------|--------|-------|
| Handler tests (SMSG_* + Movement + Timeline) | 53 | 0 | +15 MovementPacketHandler round-trip, +4 Session Timeline Replay |
| Agent tests (NetworkClientComponents) | 903 | 0 | All 26 network components |
| Network tests (packet pipeline) | 40 | 0 | FIXED: all 12 pre-existing failures resolved |
| Simulation tests | 7 | 0 | FIXED: EventHistory objectId bug |

**Resources:** 264 pre-recorded .bin files across 28 opcode types (2024-08-15 session)

### Remaining Tasks (Low Priority)
- **3.2b** MovementController Mock Test — physics→packet pipeline
- **3.1a** Strengthen SMSG_UPDATE_OBJECT Test — specific object validation
- **3.1b** PARTIAL Update Test — incremental field updates
- **3.1c** OUT_OF_RANGE_OBJECTS Test — object removal
- **3.2c** Opcode Selection Test — DetermineOpcode() state machine
- **3.2d** SMSG_COMPRESSED_MOVES Parsing Test — NPC spline decompression
- **3.3a** Login Sequence Replay — full session replay
- **3.3b** World Entry Validation — SMSG_LOGIN_VERIFY_WORLD

---

## Phase 4 — Dual-Client Integration Testing (earlier tasks)

### 4.1-4.2 (Deferred)
Headless Client Test Mirror and Behavioral Parity Assertions. Superseded by live dual-bot testing.

### 4.3 Multi-Bot Coordination Smoke Test — DONE
Bidirectional group formation: Background invites Foreground → verify → leave → Foreground invites Background → verify. Both phases successful, PartyLeaderGuid confirmed non-zero.

---

## Phase 5-7 — Deferred

- **Phase 5** (Packet Capture from Injected Client): Likely unnecessary — 937+ protocol tests cover CMSG/SMSG
- **Phase 6** (Baseline Packet Sequences): Likely complete — test fixtures serve as baselines
- **Phase 7** (Zone Transition Recordings): Partial — existing Blackrock Spire/zeppelin/swimming recordings. Still need logout, death/resurrect

---

## Long-Term Vision — Group Content Orchestration

*"Set up a 5-man for Dire Maul North tribute run"* — auto-provision accounts/characters, coordinate group behavior, handle dungeon mechanics. Build on Phase 4.4 CombatCoordinator pattern, extend to multi-class roles and dungeon-specific logic.
