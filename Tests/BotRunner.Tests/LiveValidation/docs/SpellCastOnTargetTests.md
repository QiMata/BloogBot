# SpellCastOnTargetTests

Tests spell casting via ActionType dispatch: learn spell, grant resources, cast, verify aura appears.

## Test Methods (1)

### CastSpell_BattleShout_AuraApplied

**Bots:** BG (TESTBOT2) + FG (TESTBOT1)

**Fixture Setup:** `EnsureCleanSlateAsync()`. FG coverage only runs while `LiveBotFixture.IsFgActionable` remains true after the suite's earlier FG probes, and the test now re-runs `CheckFgActionableAsync()` before the FG scenario and after the FG clean-slate reset.

**Test Flow (per bot):**

| Step | Action | Details |
|------|--------|---------|
| 1 | Learn Battle Shout | `BotLearnSpellAsync(6673)` — warrior self-buff, +15 AP, instant cast, 10 rage cost |
| 2 | Verify learned | Poll 5s for spell 6673 in `Player.SpellList` (300ms poll) |
| 3 | Grant rage | `.modify rage 200` via GM chat (200 internal = 20 displayed rage, enough for 10-rage spell) |
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

**GM Commands:** `.learn 6673` (via BotLearnSpellAsync), `.modify rage 200`, `.unaura 6673`.

**FG vs BG Critical Difference:** FG `CastSpell(int)` is a no-op — BotRunnerService must resolve spell ID to name via spell DB for Lua `CastSpellByName()`. BG sends CMSG_CAST_SPELL with int directly.

**Assertions:** Spell learned. Aura appears after CastSpell dispatch. Aura absent after cleanup.

## Current Status

`2026-03-11` the late-suite Battle Shout failure was resolved as a fixture responsiveness issue rather than a `CastSpell` logic regression:
- `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore --filter "FullyQualifiedName~SpellCastOnTargetTests" --blame-hang --blame-hang-timeout 10m --logger "console;verbosity=minimal"` -> `1 passed`
- `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore --filter "FullyQualifiedName~LiveValidation" --blame-hang --blame-hang-timeout 10m --logger "console;verbosity=minimal"` -> `31 passed, 0 failed, 4 skipped`
- `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore --filter "FullyQualifiedName~BasicLoopTests|FullyQualifiedName~CharacterLifecycleTests|FullyQualifiedName~BuffAndConsumableTests|FullyQualifiedName~CraftingProfessionTests|FullyQualifiedName~EconomyInteractionTests|FullyQualifiedName~EquipmentEquipTests|FullyQualifiedName~GroupFormationTests|FullyQualifiedName~OrgrimmarGroundZAnalysisTests|FullyQualifiedName~SpellCastOnTargetTests|FullyQualifiedName~TalentAllocationTests" --blame-hang --blame-hang-timeout 10m --logger "console;verbosity=minimal"` -> `14 passed, 1 skipped`
- root cause: earlier suites could leave FG action forwarding alive while snapshot/world-state updates were stale; strengthening `LiveBotFixture.CheckFgActionableAsync()` and invoking it directly inside `SpellCastOnTargetTests` removed that stale-FG state before `CastSpell_BattleShout_AuraApplied`

Current boundary:
- the BG packet path and the FG Lua `CastSpellByName("Battle Shout")` path are currently green for this baseline
- this test now belongs in the default documented-stable slice; remaining overhaul work is in fishing task ownership/packet timing and the BG trainer handoff, not in the Battle Shout baseline
