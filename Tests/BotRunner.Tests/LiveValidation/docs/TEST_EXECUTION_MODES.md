# LiveValidation Test Execution Modes

## Why This Matters

The FG (Foreground/injected) bot is our **gold standard** — it runs inside the real WoW client with native memory access. The BG (Background/headless) bot emulates the protocol in pure C#. Running both synchronously lets us compare BG behavior against FG as a reference. **Tests that only run BG have no FG observation window** — bugs in packet handling, movement, or state tracking have no ground truth to compare against.

## BG Bot Log Observability

BG bot logs are written to `WWoWLogs/bg_{accountName}.log` (e.g., `bg_TESTBOT2.log`). Tail this file in a separate terminal during test runs:

```bash
tail -f Bot/Release/net8.0/WWoWLogs/bg_TESTBOT2.log
```

StateManager also forwards BG stdout to test output with `[TESTBOT2]` prefix.

## Execution Mode Legend

| Mode | Description |
|------|-------------|
| **Dual-Bot Sync** | Both FG and BG run the same scenario simultaneously. FG serves as ground truth for BG behavior. |
| **Dual-Bot Conditional** | BG always runs. FG runs only when `IsFgActionable` is true. FG observation is best-effort. |
| **BG-Only** | Only the BG bot runs. No FG observation. BG bugs have no reference comparison. |
| **CombatTest-Only** | Dedicated COMBATTEST account (never receives `.gm on`). No FG/BG parity comparison. |

## Test Class Index

| Test Class | Mode | Account(s) | FG Observation? | Notes |
|------------|------|-----------|-----------------|-------|
| BasicLoopTests | Dual-Bot Conditional | BG + FG | Yes (when available) | Login/physics health checks |
| BuffAndConsumableTests | Dual-Bot Conditional | BG + FG | Yes (when available) | FG-first, then BG |
| CombatLoopTests | CombatTest-Only | COMBATTEST | **No** | Non-GM account, avoids faction corruption |
| CraftingProfessionTests | BG-Only | BG | **No** | FG excluded (legacy Lua/UI dependency) |
| DeathCorpseRunTests | BG-Only (FG skipped) | BG (+ FG skipped) | **No** | FG permanently skipped: CRASH-001 |
| EconomyInteractionTests | Dual-Bot Conditional | BG + FG | Yes (when available) | Bank/AH parallel validation |
| EquipmentEquipTests | Dual-Bot Conditional | BG + FG | Yes (when available) | Parallel equip with IsFgActionable |
| FishingProfessionTests | Dual-Bot Conditional | BG + FG | Yes (when available) | Ratchet fishing task path |
| GatheringProfessionTests | Dual-Bot Conditional | BG + FG | Yes (when available) | Mining + Herbalism routes |
| GatheringRouteSelectionTests | N/A (unit test) | None | N/A | Pure unit test, no live bots |
| GroupFormationTests | Dual-Bot Sync | BG + FG | **Required** | Both bots must be available (invite/accept) |
| LootCorpseTests | CombatTest-Only | COMBATTEST | **No** | Kill→loot with dedicated combat account |
| MapTransitionTests | BG-Only | BG | **No** | Deeprun Tram bounce validation |
| NavigationTests | BG-Only | BG | **No** | Pathfinding + Z-trace (some runs probe both) |
| NpcInteractionTests | Dual-Bot Conditional | BG + FG | Yes (when available) | Vendor/Trainer/FlightMaster |
| OrgrimmarGroundZAnalysisTests | Dual-Bot Conditional | BG + FG | Yes (when available) | Post-teleport ground Z |
| QuestInteractionTests | Dual-Bot Conditional | BG + FG | Yes (when available) | Add/Complete/Remove quest |
| SpellCastOnTargetTests | Dual-Bot Conditional | BG + FG | Yes (when available) | Battle Shout parallel cast |
| StarterQuestTests | BG-Only | BG | **No** | Quest accept/turn-in baseline |
| TalentAllocationTests | Dual-Bot Conditional | BG + FG | Yes (when available) | Learn talent scenarios |
| UnequipItemTests | Dual-Bot Conditional | BG + FG | Yes (when available) | Mainhand unequip |
| VendorBuySellTests | BG-Only | BG | **No** | FG excluded (merchant-frame legacy) |

## Tests Without FG Observation (Priority for Adding FG Parity)

These tests run BG-only and have **no ground truth comparison**. Any BG protocol, movement, or state bug is invisible:

1. **CombatLoopTests** — Uses COMBATTEST account. Consider adding FG combat reference.
2. **CraftingProfessionTests** — Blocked on FG Lua/UI crafting parity.
3. **DeathCorpseRunTests** — Blocked on FG WoW.exe crash (CRASH-001).
4. **LootCorpseTests** — Uses COMBATTEST account. Consider adding FG loot reference.
5. **MapTransitionTests** — BG-only. FG map transition observation would catch desync.
6. **NavigationTests** — BG-only. FG position comparison would catch movement divergence.
7. **StarterQuestTests** — BG-only baseline. Low priority for FG parity.
8. **VendorBuySellTests** — Blocked on FG merchant-frame implementation.
