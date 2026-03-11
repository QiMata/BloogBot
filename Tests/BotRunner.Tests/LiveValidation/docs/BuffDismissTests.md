# BuffDismissTests

Tests buff application via item use and removal via DismissBuff action.

## Test Methods (1)

### DismissBuff_LionsStrength_RemovedFromAuras

**Bots:** BG (TESTBOT2) + FG (TESTBOT1)

**Fixture Setup:** Standard `LiveBotFixture` init.

**Test Flow (RunDismissBuffScenario per bot):**

| Step | Action | Details |
|------|--------|---------|
| 0 | Clean slate | `.unaura 2367` + `.unaura 2457` (remove stale buff). `BotClearInventoryAsync()` |
| 1 | Add elixir | `BotAddItemAsync(account, 2454, 1)` — Elixir of Lion's Strength |
| 2 | Wait for item | `WaitForBagItemAsync()` — poll snapshot BagContents for item 2454 |
| 3 | Use elixir | **Dispatch `ActionType.UseItem`** with `IntParam = 2454` |
| 4 | Wait for buff | Poll `snapshot.Player.Unit.Auras` for spell 2367 or 2457 (5s, 200ms poll) |
| 5 | Dismiss buff | **Dispatch `ActionType.DismissBuff`** with `StringParam = "Lion's Strength"` |
| 6 | Verify removal | Poll auras for absence of 2367/2457 (3s, 200ms poll) |

**StateManager/BotRunner Action Flow:**

- **UseItem (step 3):** `BuildUseItemByIdSequence(2454)` → finds item in bags → `_objectManager.UseItem(bagSlot)` → FG: Lua `UseContainerItem(bag, slot)` / BG: CMSG_USE_ITEM packet
- **DismissBuff (step 5):** `BuildDismissBuffSequence("Lion's Strength")` → iterates player auras → matches by name → `_objectManager.CancelAura(spellId)` → FG: Lua `CancelPlayerBuff` / BG: CMSG_CANCEL_AURA packet

**Known Issue — BB-BUFF-001:**
BG bot's `WoWUnit.Buffs` list is never populated from packets → `HasBuff(name)` always false → DismissBuff is a no-op for BG. Test skips BG assertion: `Skip.If(!bgPassed, "BG bot: DismissBuff failed — WoWUnit.Buffs list empty (BB-BUFF-001)...")`

**Key IDs:**
- Item 2454 = Elixir of Lion's Strength
- Spell 2367 = use effect spell
- Spell 2457 = buff aura spell

**Assertions:** Aura appears after UseItem. Aura disappears after DismissBuff.
