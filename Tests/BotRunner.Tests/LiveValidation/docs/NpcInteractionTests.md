# NpcInteractionTests

Live NPC-interaction baseline for vendor, trainer, and flight-master visibility plus `InteractWith` dispatch.

This suite currently exercises:
- `Exports/BotRunner/BotRunnerService.ActionDispatch.cs`
- `Exports/BotRunner/BotRunnerService.Sequences.Movement.cs`
- `Exports/WoWSharpClient/WoWSharpObjectManager.Network.cs`
- `Exports/WoWSharpClient/Networking/ClientComponents/GossipNetworkClientComponent.cs`
- `Exports/BotRunner/BotRunnerService.Snapshot.cs`

## Test Methods

### Vendor_OpenAndSeeInventory
### Vendor_SellJunkItems
### Trainer_OpenAndSeeSpells
### Trainer_LearnAvailableSpells
### FlightMaster_DiscoverNodes
### ObjectManager_DetectsNpcFlags

**Bots:** BG plus FG when FG is actionable.

## Test Flow

1. Bring each bot to Razor Hill vendor, Razor Hill trainer, or Orgrimmar flight-master positions.
2. Wait for snapshot visibility and filter `NearbyUnits` by the required `NpcFlags`.
3. Dispatch `ActionType.InteractWith` to the matched unit GUID.
4. Assert the dispatch succeeds.
5. For the object-manager-only slice, assert at least one nearby unit exposes non-zero `NpcFlags`.

Per-test setup adds the minimum required preconditions:
- vendor sell: ensure bag items exist
- trainer learn: ensure money and level are high enough
- all scenarios: ensure the bot is strict-alive and within arrival distance

## Runtime Linkage

- Interaction path: `BotRunnerService.ActionDispatch` -> `BuildInteractWithSequence(guid)` -> `WoWSharpObjectManager.InteractWithNpcAsync(...)`.
- NPC visibility path: `BotRunnerService.Snapshot` reads `NearbyUnits` / `NpcFlags` after object-manager updates.
- This suite is still a baseline around raw `InteractWith` dispatch. The planned overhaul endpoint is task ownership via `VendorVisitTask`, `TrainerVisitTask`, and `FlightMasterVisitTask`.

## Metrics

The live assertions require:
- nearby NPCs are visible with the expected `NpcFlags`
- `InteractWith` dispatch returns `Success`
- FG/BG parity is preserved when FG is actionable

## Current Status

`2026-03-11`: the focused quest/NPC validation slice passed `8/8`. The suite is stable, but it remains a dispatch-level baseline rather than the final task-driven NPC interaction coverage described in `OVERHAUL_PLAN.md`.
