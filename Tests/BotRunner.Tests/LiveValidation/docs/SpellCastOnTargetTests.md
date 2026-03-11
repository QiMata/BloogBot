# SpellCastOnTargetTests

Tests spell casting via ActionType dispatch: learn spell, grant resources, cast, verify aura appears.

## Test Methods (1)

### CastSpell_BattleShout_AuraApplied

**Bots:** BG (TESTBOT2) + FG (TESTBOT1)

**Fixture Setup:** `EnsureCleanSlateAsync()`.

**Test Flow (per bot):**

| Step | Action | Details |
|------|--------|---------|
| 1 | Learn Battle Shout | `BotLearnSpellAsync(6673)` — warrior self-buff, +15 AP, instant cast, 10 rage cost |
| 2 | Verify learned | Poll 5s for spell 6673 in `Player.SpellList` (300ms poll) |
| 3 | Grant rage | `.modify rage 1000` via GM chat (1000 internal = 100 displayed rage) |
| 4 | Remove stale aura | `.unaura 6673`. Wait 500ms. Verify absent from `Player.Unit.Auras`. |
| 5 | Cast spell | **Dispatch `ActionType.CastSpell`** with `IntParam = 6673` |
| 6 | Wait for aura | Poll 8s for spell 6673 in `Player.Unit.Auras` (300ms poll) |
| 7 | Cleanup | `.unaura 6673` |

**StateManager/BotRunner Action Flow:**

**CastSpell dispatch chain:**
1. ActionMessage with `ActionType.CastSpell`, `IntParam=6673`
2. `BuildCastSpellSequence(6673)` in BotRunnerService
3. **FG path:** Spell DB lookup → resolves 6673 to "Battle Shout" → `CastSpellByName("Battle Shout")` via Lua on main thread. Note: `CastSpell(int)` is a **no-op** on FG.
4. **BG path:** CMSG_CAST_SPELL packet with spellId=6673. Works directly with int.
5. Server validates: rage >= 10 (100 internal), spell known, no cooldown
6. Server applies aura → SMSG_AURA_UPDATE packet → ObjectManager updates Auras list → snapshot captures it

**Key IDs:**
- Spell 6673 = Battle Shout (Rank 1, warrior, instant, self-buff)
- Rage cost: 10 rage (100 internal units)

**GM Commands:** `.learn 6673` (via BotLearnSpellAsync), `.modify rage 1000`, `.unaura 6673`.

**FG vs BG Critical Difference:** FG `CastSpell(int)` is a no-op — BotRunnerService must resolve spell ID to name via spell DB for Lua `CastSpellByName()`. BG sends CMSG_CAST_SPELL with int directly.

**Assertions:** Spell learned. Aura appears after CastSpell dispatch. Aura absent after cleanup.
