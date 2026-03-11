# NpcInteractionTests

Live NPC-interaction coverage for vendor visibility, trainer learning via task-owned `VisitTrainer`, and flight-master visibility.

This suite currently exercises:
- `Exports/BotCommLayer/Models/ProtoDef/communication.proto`
- `Exports/GameData.Core/Enums/CharacterAction.cs`
- `Exports/BotRunner/BotRunnerService.ActionMapping.cs`
- `Exports/BotRunner/BotRunnerService.ActionDispatch.cs`
- `Exports/BotRunner/Tasks/TrainerVisitTask.cs`
- `Exports/BotRunner/Tasks/VendorVisitTask.cs`
- `Exports/BotRunner/Tasks/FlightMasterVisitTask.cs`
- `Exports/BotRunner/BotRunnerService.Sequences.Movement.cs`
- `Exports/WoWSharpClient/WoWSharpObjectManager.Network.cs`
- `Exports/WoWSharpClient/Networking/ClientComponents/GossipNetworkClientComponent.cs`
- `Exports/WoWSharpClient/Networking/ClientComponents/TrainerNetworkClientComponent.cs`
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
3. `Vendor_OpenAndSeeInventory`, `Vendor_SellJunkItems`, `Trainer_OpenAndSeeSpells`, and `FlightMaster_DiscoverNodes` still dispatch `ActionType.InteractWith`.
4. `Trainer_LearnAvailableSpells` dispatches `ActionType.VisitTrainer`, which maps to `CharacterAction.VisitTrainer` and queues `TrainerVisitTask`.
5. The trainer-learning path records trainer visibility, trainer distance, spell-count delta, specific-spell presence, coinage delta, and learn latency.
6. For the object-manager-only slice, assert at least one nearby unit exposes non-zero `NpcFlags`.

Per-test setup adds the minimum required preconditions:
- vendor sell: ensure bag items exist
- trainer learn: ensure money and level are high enough
- all scenarios: ensure the bot is strict-alive and within arrival distance

## Runtime Linkage

- Raw interaction path: `BotRunnerService.ActionDispatch` -> `BuildInteractWithSequence(guid)` -> `WoWSharpObjectManager.InteractWithNpcAsync(...)`.
- Task-owned trainer path: `communication.proto` `VisitTrainer` -> `CharacterAction.VisitTrainer` -> `BotRunnerService.ActionDispatch` -> `TrainerVisitTask` -> `WoWSharpObjectManager.LearnAllAvailableSpellsAsync(...)` -> `GossipNetworkClientComponent.NavigateToServiceAsync(GossipServiceType.Trainer)` / `TrainerNetworkClientComponent.RequestTrainerServicesAsync(...)`.
- NPC visibility path: `BotRunnerService.Snapshot` reads `NearbyUnits` / `NpcFlags` after object-manager updates.
- The remaining overhaul gap is finishing task ownership for vendor and flight-master coverage instead of keeping those checks at the raw `InteractWith` layer.

## Metrics

The live assertions require:
- nearby NPCs are visible with the expected `NpcFlags`
- `InteractWith` dispatch returns `Success`
- `VisitTrainer` dispatch returns `Success`
- trainer-learning metrics capture trainer presence, distance, spell delta, coinage delta, and latency
- FG/BG parity is preserved when FG is actionable

## Current Status

`2026-03-11`: the new NPC action contract is on disk:
- `VisitVendor = 64`
- `VisitTrainer = 65`
- `VisitFlightMaster = 66`

Latest validation:
- `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore --filter "FullyQualifiedName~ActionForwardingContractTests|FullyQualifiedName~NpcInteractionTests" --blame-hang --blame-hang-timeout 10m --logger "console;verbosity=minimal"` -> `28 passed, 1 skipped`
- `dotnet test Tests/BotRunner.Tests/BotRunner.Tests.csproj --configuration Release --no-build --no-restore --filter "FullyQualifiedName~QuestInteractionTests|FullyQualifiedName~StarterQuestTests|FullyQualifiedName~NpcInteractionTests" --blame-hang --blame-hang-timeout 10m --logger "console;verbosity=minimal"` -> `7 passed, 1 skipped`

Current boundary:
- vendor visibility/sell and flight-master visibility remain green
- `Trainer_LearnAvailableSpells` is a deterministic tracked skip under `BRT-OVR-006`
- the skip condition is the BG gossip-to-trainer-service gap: `VisitTrainer` dispatch succeeds, but the live client still closes gossip without surfacing `SMSG_TRAINER_LIST`, so there is no spell-count or coinage delta yet
