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
| **Shodan BG-action** | FG, BG, and SHODAN launch together; SHODAN stages setup, BG receives the behavior action, and FG stays idle for topology parity. |
| **Shodan BG-action / tracked skip** | FG, BG, and SHODAN launch together; SHODAN stages setup, executable BG actions run, and blocked subcases skip with a documented runtime gap. |
| **Shodan BG-action / FG gap** | FG, BG, and SHODAN launch together; SHODAN stages setup, executable BG actions run, and foreground-dependent paths are explicit tracked skips until the FG runtime gap is fixed. |
| **Shodan FG+BG-action / tracked skip** | FG, BG, and SHODAN launch together; SHODAN stages setup, FG/BG receive behavior actions where supported, and blocked subcases skip with a documented runtime gap. |
| **BG-Only** | Only the BG bot runs. No FG observation. BG bugs have no reference comparison. |
| **CombatTest-Only** | Dedicated COMBATTEST account with account-level GM access only. No FG/BG parity comparison. |

## Test Class Index

| Test Class | Mode | Account(s) | FG Observation? | Notes |
|------------|------|-----------|-----------------|-------|
| AllianceNavigationTests | Shodan BG-action | BG + idle FG + SHODAN | **No behavior parity** | Human BG Alliance coordinate staging; snapshot assertions after Shodan-owned staging |
| BasicLoopTests | Dual-Bot Conditional | BG + FG | Yes (when available) | Login/physics health checks |
| BuffAndConsumableTests | Dual-Bot Conditional | BG + FG | Yes (when available) | FG-first, then BG |
| CombatLoopTests | CombatTest-Only | COMBATTEST | **No** | Account-level GM only; avoids runtime GM-mode corruption |
| CornerNavigationTests | Shodan BG-action | BG + idle FG + SHODAN | **No behavior parity** | Shodan-staged corner/obstacle probes; BG TravelTo route checks |
| CraftingProfessionTests | BG-Only | BG | **No** | FG excluded (legacy Lua/UI dependency) |
| DeathCorpseRunTests | BG default + FG opt-in | BG (+ opt-in FG) | Opt-in only | Historical CRASH-001 not reproduced on 2026-04-15; latest opt-in FG corpse-run passes |
| EconomyInteractionTests | Dual-Bot Conditional | BG + FG + SHODAN | Yes (when available) | Shodan-staged bank/AH/mail interaction |
| EquipmentEquipTests | Dual-Bot Conditional | BG + FG | Yes (when available) | Parallel equip with IsFgActionable |
| FishingProfessionTests | Dual-Bot Conditional | BG + FG | Yes (when available) | Ratchet fishing task path |
| GatheringProfessionTests | Dual-Bot Conditional | BG + FG | Yes (when available) | Mining + Herbalism routes |
| GatheringRouteSelectionTests | N/A (unit test) | None | N/A | Pure unit test, no live bots |
| GossipQuestTests | Shodan BG-action | BG + idle FG + SHODAN | **No behavior parity** | Shodan-staged gossip/quest-giver interaction |
| GroupFormationTests | Dual-Bot Sync | BG + FG | **Required** | Both bots must be available (invite/accept) |
| MailSystemTests | Shodan BG-action | BG + idle FG + SHODAN | **No behavior parity** | Shodan-staged mailbox plus SOAP money/item mail |
| MailParityTests | Shodan BG-action | BG + idle FG + SHODAN | **No behavior parity** | BG CheckMail baseline; FG mail collection tracked separately |
| TradingTests | Shodan BG-action / FG gap | BG + FG + SHODAN | BG cancel only | BG offer/decline passes; transfer skip tracks FG AcceptTrade ACK failure |
| TradeParityTests | Shodan BG-action / FG gap | BG + FG + SHODAN | **No behavior parity** | FG trade cancel/transfer paths skip after Shodan launch due foreground action ACK failures |
| LootCorpseTests | CombatTest-Only | COMBATTEST | **No** | Kill→loot with dedicated combat account |
| MapTransitionTests | Shodan BG-action | BG + idle FG + SHODAN | **No behavior parity** | Shodan-staged Deeprun Tram bounce; BG post-bounce action liveness |
| MountEnvironmentTests | Shodan BG-action | BG + idle FG + SHODAN | **No behavior parity** | Shodan-staged riding loadout and indoor/outdoor mount scene checks |
| MovementSpeedTests | Shodan BG-action | BG + idle FG + SHODAN | **No behavior parity** | Shodan-staged Durotar road start; BG Goto speed probe |
| NavigationTests | Shodan BG-action / tracked skip | BG + idle FG + SHODAN | **No behavior parity** | Shodan-staged Durotar road/winding probes; BG Goto route checks; Valley long diagonal tracked skip |
| NpcInteractionTests | Shodan FG+BG-action / tracked skip | BG + FG + SHODAN | Vendor/flight/object-manager yes | Shodan-staged NPC interactions; trainer subcase skips due live funding/mailbox staging gap |
| OrgrimmarGroundZAnalysisTests | Dual-Bot Conditional | BG + FG | Yes (when available) | Post-teleport ground Z |
| QuestInteractionTests | Shodan BG-action | BG + idle FG + SHODAN | **No behavior parity** | Shodan-staged add/complete/remove quest snapshot plumbing |
| QuestObjectiveTests | Shodan BG-action | BG + idle FG + SHODAN | **No behavior parity** | Shodan-staged quest objective combat action |
| SpellCastOnTargetTests | Dual-Bot Conditional | BG + FG | Yes (when available) | Battle Shout parallel cast |
| SpiritHealerTests | Shodan BG-action | BG + idle FG + SHODAN | **No behavior parity** | Shodan-staged corpse/release/recover flow |
| StarterQuestTests | Shodan BG-action | BG + idle FG + SHODAN | **No behavior parity** | Shodan-staged quest accept/turn-in baseline |
| TalentAllocationTests | Dual-Bot Conditional | BG + FG | Yes (when available) | Learn talent scenarios |
| TileBoundaryCrossingTests | Shodan BG-action | BG + idle FG + SHODAN | **No behavior parity** | Shodan-staged tile-boundary probes; BG TravelTo route checks |
| TravelPlannerTests | Shodan BG-action / tracked skip | BG + idle FG + SHODAN | **No behavior parity** | Shodan-staged street-level Orgrimmar start; short BG TravelTo passes, long Crossroads probes skip for no-movement gap |
| UnequipItemTests | Dual-Bot Conditional | BG + FG | Yes (when available) | Mainhand unequip |
| VendorBuySellTests | Shodan BG-action | BG + idle FG + SHODAN | **No behavior parity** | BG packet buy/sell baseline; FG launched for topology parity |

## Tests Without FG Observation (Priority for Adding FG Parity)

These tests run BG-only and have **no ground truth comparison**. Any BG protocol, movement, or state bug is invisible:

1. **CombatLoopTests** — Uses COMBATTEST account. Consider adding FG combat reference.
2. **CraftingProfessionTests** — Blocked on FG Lua/UI crafting parity.
3. **DeathCorpseRunTests** - FG is opt-in. Historical CRASH-001 was not reproduced on 2026-04-15; latest opt-in FG corpse-run validation passes and remains available for targeted regression proof.
4. **LootCorpseTests** — Uses COMBATTEST account. Consider adding FG loot reference.
5. **MapTransitionTests** - Shodan topology is in place, but the migrated slice is BG-action-only while FG stays idle for topology parity.
6. **MountEnvironmentTests** - Shodan topology is in place, but the migrated mount proof is BG-action-only while FG stays idle for topology parity.
7. **TravelPlannerTests** - Shodan topology is in place, but the migrated travel proof is BG-action-only; long Crossroads probes remain tracked skips until the no-movement `TravelTo` gap is fixed.
8. **CornerNavigationTests / TileBoundaryCrossingTests** - Shodan topology is in place, but the migrated movement probes are BG-action-only while FG stays idle for topology parity.
9. **MovementSpeedTests** - Shodan topology is in place, but the migrated speed probe is BG-action-only while FG stays idle for topology parity.
10. **NavigationTests / AllianceNavigationTests** - Shodan topology is in place, but the migrated navigation proofs are BG-action/snapshot-only while FG stays idle; the Valley long diagonal remains a tracked skip until the `GoToTask` `no_path_timeout` gap is fixed.
11. **StarterQuestTests / GossipQuestTests / QuestObjectiveTests / QuestInteractionTests** - Shodan topology is in place, but the migrated quest group is BG-action-only while FG stays idle for topology parity.
12. **VendorBuySellTests** - Shodan topology is in place, but the migrated slice is still a BG packet baseline; add FG behavior parity separately.
13. **MailSystemTests / MailParityTests** - Shodan topology is in place, but committed mail actions are BG-only until FG `CheckMail` collection is stable under combined-suite load.
14. **TradingTests / TradeParityTests** - Shodan topology is in place, but foreground `DeclineTrade`, `OfferItem`, and `AcceptTrade` currently ACK `Failed/behavior_tree_failed`; transfer/parity paths stay explicit skips until the FG trade action surface is stable.
15. **SpiritHealerTests** - Shodan topology is in place, but the migrated resurrection proof is BG-action-only while FG stays idle for topology parity.
