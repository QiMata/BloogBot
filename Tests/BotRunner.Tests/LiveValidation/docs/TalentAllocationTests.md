# TalentAllocationTests

Tests that passive talent spells learned via GM appear in the snapshot SpellList.

## Test Methods (1)

### Talent_LearnViaGM_SpellAppearsInKnownSpells

**Bots:** BG (TESTBOT2) + FG (TESTBOT1)

**Fixture Setup:** `EnsureCleanSlateAsync()`.

**Test Flow (per bot):**

| Step | Action | Details |
|------|--------|---------|
| 1 | Ensure level >= 10 | `EnsureLevelAtLeastAsync()` — if level < 10: `.character level 10`, poll 15s |
| 2 | Unlearn spell | `TryEnsureSpellAbsentAsync()` — `.unlearn 16462` (Deflection rank 1). Allows "spell not learned" response. Retries if dropped (dead/ghost). |
| 3 | Self-target | `BotSelectSelfAsync()`. Wait 300ms. |
| 4 | Learn spell | `.learn 16462` via `SendGmChatCommandTrackedAsync()`. If dispatch fails (dead/ghost): re-clean-slate and retry. |
| 5 | Diagnostic snapshot | Immediately read snapshot: log screen state, health, spell count, has16462 |
| 6 | Wait for spell | Poll 12s for spell 16462 in `Player.SpellList` (500ms poll). Log state every 2s. |

**StateManager/BotRunner Role:**

**No ActionType dispatches.** All operations use GM chat commands. BotRunnerService processes `SendChat` actions. The test validates the snapshot spell tracking pipeline for passive talent spells.

**Passive Talent Spell Detection (Critical):**
Passive talent spells (like Deflection 16462) are **NOT in the static spell array** — they're detected via:
1. Volatile `_lastKnownSpellIds` snapshot (updated each tick from ObjectManager)
2. `_forceSpellRefresh` flag triggered by SMSG_LEARNED_SPELL (no-args hook)
3. FG only: Lua `GetTalentInfo()` STEP 5 for passive talents

**Key IDs:**
- Spell 16462 = Deflection Rank 1 (passive warrior talent, +1% parry)

**GM Commands:** `.character level 10`, `.unlearn 16462`, `.learn 16462`.

**Assertions:** Spell absent after unlearn. Spell present in SpellList after learn. Level >= 10 (talent points available).

**Known complexity:** Passive talent detection requires multiple code paths to work in concert. This test validates the full pipeline from SMSG_LEARNED_SPELL → spell list refresh → snapshot inclusion.
