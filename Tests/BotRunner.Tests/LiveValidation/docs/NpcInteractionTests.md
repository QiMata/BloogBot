# NpcInteractionTests

Task-driven NPC interaction coverage for vendor sell/repair, trainer learning, and flight-master discovery.

This suite exercises BotTask dispatch for all three NPC interaction types:
- `Exports/BotRunner/Tasks/VendorVisitTask.cs` — find vendor, sell junk, repair, buy consumables
- `Exports/BotRunner/Tasks/TrainerVisitTask.cs` — find trainer, purchase available spells
- `Exports/BotRunner/Tasks/FlightMasterVisitTask.cs` — find flight master, discover taxi nodes

Supporting code paths:
- `Exports/BotCommLayer/Models/ProtoDef/communication.proto`
- `Exports/GameData.Core/Enums/CharacterAction.cs`
- `Exports/BotRunner/BotRunnerService.ActionDispatch.cs`
- `Exports/WoWSharpClient/Networking/ClientComponents/GossipNetworkClientComponent.cs`
- `Exports/WoWSharpClient/Networking/ClientComponents/TrainerNetworkClientComponent.cs`
- `Exports/BotRunner/BotRunnerService.Snapshot.cs`

## Test Methods

### Vendor_VisitTask_FindsAndInteracts
Task-driven vendor visit. Dispatches `ActionType.VisitVendor` which queues `VendorVisitTask`.
Asserts vendor found (NPC flag detection), distance within range, and task completion.

### Vendor_SellJunkItems_CoinageIncreases
Task-driven vendor sell. Adds junk items to inventory, dispatches `VisitVendor`, asserts coinage increases (items sold).

### Trainer_LearnAvailableSpells
Task-driven trainer visit. Dispatches `ActionType.VisitTrainer` which queues `TrainerVisitTask`.
Asserts spell learned, spell count growth, coinage decrease, and learn latency metrics.

### FlightMaster_VisitTask_DiscoversPaths
Task-driven flight master discovery. Dispatches `ActionType.VisitFlightMaster` which queues `FlightMasterVisitTask`.
Asserts flight master found and task completion.

### ObjectManager_DetectsNpcFlags
Snapshot-level NPC flag detection. Verifies at least one nearby unit has non-zero `NpcFlags`.

**Bots:** BG plus FG when FG is actionable.

## Test Flow

1. `EnsureCleanSlateAsync` + teleport to NPC location (Razor Hill or Orgrimmar).
2. Wait for NPC visibility via `WaitForNearbyUnitAsync` with the required `NpcFlags`.
3. Record pre-action snapshot metrics (coinage, spell count, distance).
4. Dispatch the task-owned action (`VisitVendor`/`VisitTrainer`/`VisitFlightMaster`).
5. Wait for task outcome (coinage change, spell learned, or timeout).
6. Record post-action snapshot metrics and assert the expected changes.

Per-test setup adds the minimum required preconditions:
- vendor sell: ensure bag items exist
- trainer learn: ensure money and level are high enough, unlearn target spell
- all scenarios: ensure the bot is strict-alive and within arrival distance

## Runtime Linkage

- Vendor path: `VisitVendor` -> `CharacterAction.VisitVendor` -> `VendorVisitTask` (FindVendor -> MoveToVendor -> InteractVendor -> VendorActions -> Done)
- Trainer path: `VisitTrainer` -> `CharacterAction.VisitTrainer` -> `TrainerVisitTask` -> `LearnAllAvailableSpellsAsync(...)`
- Flight master path: `VisitFlightMaster` -> `CharacterAction.VisitFlightMaster` -> `FlightMasterVisitTask` -> `DiscoverTaxiNodesAsync(...)`
- NPC visibility: `BotRunnerService.Snapshot` reads `NearbyUnits` / `NpcFlags` after object-manager updates.

## Metrics

The live assertions require:
- Vendor: found with correct NpcFlags, within interact range, task completes, coinage increases after sell
- Trainer: found with correct NpcFlags, spell absent before / present after, spell count grows, coinage decreases
- Flight Master: found with correct NpcFlags, task completes
- FG/BG parity is preserved when FG is actionable
