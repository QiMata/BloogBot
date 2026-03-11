# ConsumableUsageTests

Tests item consumption and buff application via UseItem action.

## Test Methods (1)

### UseConsumable_ElixirOfLionsStrength_BuffApplied

**Bots:** BG (TESTBOT2) + FG (TESTBOT1)

**Fixture Setup:** Standard `LiveBotFixture` init.

**Test Flow (RunConsumableScenario per bot):**

| Step | Action | Details |
|------|--------|---------|
| 0 | Clean slate | `.unaura 2367` + `.unaura 2457`. `BotClearInventoryAsync()`. Record aura count before. |
| 1 | Add elixir | `BotAddItemAsync(account, 2454, 3)` — 3x Elixir of Lion's Strength |
| 2 | Wait for item | Poll BagContents for item 2454 (15 polls x 200ms = 3s max) |
| 3 | Use elixir | **Dispatch `ActionType.UseItem`** with `IntParam = 2454` |
| 4 | Assert dispatch | `ResponseResult.Success` (AST-4: don't silent-fail UseItem) |
| 5 | Wait for buff | Poll `Player.Unit.Auras` for spell 2367 or 2457 (3s timeout, 200ms poll) |

**StateManager/BotRunner Action Flow:**

**UseItem dispatch chain:**
1. StateManager receives ActionMessage with `ActionType.UseItem`, `IntParam=2454`
2. BotRunnerService `ConvertActionMessageToCharacterActions()` → `(CharacterAction.UseItem, [2454])`
3. `BuildBehaviorTreeFromActions()` → single-param path → `BuildUseItemByIdSequence(2454)`
4. Sequence: scan BagContents for itemId 2454 → resolve (bag, slot) → `_objectManager.UseItem(bag, slot)`
5. FG: Lua `UseContainerItem(bag, slot)` on main thread via ThreadSynchronizer
6. BG: CMSG_USE_ITEM packet with bag/slot/spellCast fields

**Key IDs:**
- Item 2454 = Elixir of Lion's Strength
- Spell 2367 = use effect spell
- Spell 2457 = buff aura (Lion's Strength)

**Assertions:** Dispatch succeeds. Aura 2367 or 2457 appears in `Player.Unit.Auras` after use.
