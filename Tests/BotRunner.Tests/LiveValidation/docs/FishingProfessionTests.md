# FishingProfessionTests

Tests the full fishing pipeline: equip pole, learn spells, cast at water, wait for auto-catch, verify skill increase.

## Test Methods (1)

### Fishing_CatchFish_SkillIncreases

**Bots:** BG (TESTBOT2) primary. FG (TESTBOT1) parked at Orgrimmar during BG test.

**Fixture Setup:** `EnsureCleanSlateAsync()`.

**Test Flow:**

**Phase 1 — Bot Preparation (per bot):**

| Step | Action | Details |
|------|--------|---------|
| 1 | Ensure alive | `RevivePlayerAsync()` |
| 2 | Teleport safe | Orgrimmar if needed |
| 3 | Learn fishing spells | 7733 (Apprentice), 7734 (Journeyman), 7620 (Rank 1), 7731 (Rank 2), 7732 (Expert), 18248 (Artisan), 7738 (Pole Proficiency) |
| 4 | Set skill | `BotSetSkillAsync(FishingSkillId, 1, 300)` — skill 1/300 |
| 5 | Clear equipment | `.reset items {charName}` |
| 6 | Add fishing pole | `BotAddItemAsync(5041)`, poll 8s |
| 7 | Equip pole | **Dispatch `ActionType.EquipItem`** with `IntParam = 5041` |
| 8 | Add bauble | `BotAddItemAsync(6529)` — Shiny Bauble |

**Phase 2 — Fishing at 4 Locations (sequential until success):**

| # | Location | Coordinates (X, Y, Z) | Facing (rad) |
|---|----------|----------------------|---------------|
| 1 | Ratchet dock | (-988.5, -3834, 5.7) | 6.21 (east) |
| 2 | Ratchet alt | (-985.7, -3827, 5.7) | 5.50 |
| 3 | Durotar coast | (-995, -3850, 4) | 6.28 |
| 4 | Sen'jin Village | (-820, -4885, 8) | 0.0 |

**Per location:**
1. `BotTeleportAsync()` to shore position
2. Wait for Z stabilization (4s)
3. **Dispatch `ActionType.SetFacing`** with `FloatParam = facing_radians`
4. Wait 500ms
5. `CastAndWaitForCatch()` — up to 3 attempts:
   - **Dispatch `ActionType.CastSpell`** with `IntParam = 7620` (Fishing Rank 1)
   - Wait 3s for channel to start
   - Check `Player.Unit.ChannelSpellId` is active
   - Find bobber in NearbyObjects (DisplayId=668 or GameObjectType=17)
   - Wait 22s for auto-catch (server bites 5-20s into 20s channel)
   - Poll every 3s for skill increase
   - If skill increases → success

**StateManager/BotRunner Action Flow:**

- **SetFacing:** inline `Do()` → `_objectManager.SetFacing(angle)` → MSG_MOVE_SET_FACING packet
- **CastSpell(7620):** `BuildCastSpellSequence(7620)` → FG: spell DB lookup → `CastSpellByName("Fishing")` via Lua / BG: CMSG_CAST_SPELL with spellId=7620. 20s channel. Spell-cast lockout set for 20s.
- **Auto-catch:** Server-side — bobber gets a fish bite event → server completes the channel → loot window opens → auto-loot if enabled

**Key IDs:**
- Item 5041 = Fishing Pole
- Item 6529 = Shiny Bauble
- Spell 7620 = Fishing Rank 1
- Bobber DisplayId = 668
- Channel duration = 20s (22s with margin)

**Assertions:** Test passes if any location catches fish. Skill increase logged but RNG-dependent in vanilla WoW.
