# DecisionEngineService — ML-Based Combat Decision Engine

ML model for combat decision-making. Watches for `.bin` snapshot files, trains/updates model, serves predictions via Protobuf IPC.

## Key Files

| File | Lines | Purpose |
|------|-------|---------|
| `DecisionEngine.cs` | ~200 | Core engine: FileSystemWatcher for `.bin` files, model training pipeline |
| `MLModel.cs` | 130 | ML model wrapper: feature extraction, prediction, SQLite persistence |
| `DecisionEngineWorker.cs` | ~50 | BackgroundService host |
| `CombatPredictionService.cs` | ~80 | Prediction request handler |
| `Repository/MangosRepository.cs` | 6,952 | **LARGEST FILE** — Static MaNGOS DB query methods (items, spells, creatures, quests, world data) |
| `Clients/CombatModelClient.cs` | 11 | Client interface for combat model |
| `Listeners/CombatModelServiceListener.cs` | 30 | Protobuf socket listener for prediction requests |

## Architecture

```
.bin snapshot files (from StateManager)
  → FileSystemWatcher detects new files
    → DecisionEngine.ProcessBinFileAsync()
      → Deserialize ActivitySnapshot protobuf
        → MLModel.Train() or MLModel.Predict()
          → SQLite persistence (model weights)
```

## MangosRepository (Refactoring Target)

6,952-line static class with all MaNGOS MySQL read-only queries. Domains:
- **Items**: starter items, item templates, item cache lookups
- **Spells**: spell data, trainer spells, class spell lists
- **Creatures**: creature templates, creature loot, vendor lists
- **Quests**: quest templates, quest chains, quest givers
- **World**: area triggers, teleport locations, game objects
- **Characters**: character creation info, race/class combos

All methods are `static` with direct MySQL connections — no shared state. Safe to split into partial classes by domain.

## Dependencies

- Google.Protobuf, System.Data.SQLite
- GameData.Core (interfaces), BotCommLayer (IPC)

## Testing

- `Tests/DecisionEngineService.Tests/` — unit tests for ML model and repository queries
