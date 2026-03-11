# CraftingProfessionTests

Tests crafting pipeline: learn profession + recipe, provide materials, cast recipe spell, verify output item.

## Test Methods (1)

### FirstAid_LearnAndCraft_ProducesLinenBandage

**Bots:** BG (TESTBOT2) + FG (TESTBOT1)

**Fixture Setup:** `EnsureCleanSlateAsync()`.

**Test Flow (RunCraftingScenario per bot):**

| Step | Action | Details |
|------|--------|---------|
| 1 | Verify position | If not near Orgrimmar (1629.4, -4373.4, 31.2), teleport. Arrival within 45y. |
| 2 | Learn spells | Check SpellList for 3273 (First Aid Apprentice) and 3275 (Linen Bandage recipe). If missing: `BotLearnSpellAsync()` + poll 5s for SMSG_LEARNED_SPELL. |
| 3 | Prepare inventory | Count item 1 (Linen Cloth) and item 1251 (Linen Bandage) in bags. If bandage preexists OR cloth missing + bags >= 15 items: `BotClearInventoryAsync()`. |
| 4 | Ensure material | If no Linen Cloth: `BotAddItemAsync(1, 1)`. Poll 3s for cloth in BagContents. |
| 5 | Cast recipe | **Dispatch `ActionType.CastSpell`** with `IntParam = 3275` (Linen Bandage recipe) |
| 6 | Wait for output | Poll BagContents for item 1251 count increase (8s, 200ms poll) |

**StateManager/BotRunner Action Flow:**

**CastSpell dispatch chain:**
1. ActionMessage with `ActionType.CastSpell`, `IntParam=3275`
2. `BuildCastSpellSequence(3275)` in BotRunnerService
3. **FG path:** Resolves spell ID 3275 → spell name via spell DB → `CastSpellByName("Linen Bandage")` via Lua. Note: `CastSpell(int)` is a **no-op** on FG — only string override works.
4. **BG path:** CMSG_CAST_SPELL packet with spellId=3275. Works directly with int.
5. Server processes craft: 3.5s channel → consumes 1x Linen Cloth → produces 1x Linen Bandage
6. Spell-cast lockout set for 20s (prevents GoTo from interrupting channel)

**Key IDs:**
- Item 1 = Linen Cloth (input)
- Item 1251 = Linen Bandage (output)
- Spell 3273 = First Aid Apprentice (proficiency)
- Spell 3275 = Linen Bandage (recipe/craft spell)

**Assertions:** Craft spell dispatches successfully. Bandage count increases after channeled cast completes.

**FG vs BG Difference:** FG resolves spell ID→name for Lua; BG sends packet directly. Both produce same server-side result.
