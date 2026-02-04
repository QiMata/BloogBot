# DecisionEngineService

A machine learning-powered decision-making service for the WWoW (Westworld of Warcraft) bot system that uses Microsoft ML.NET to predict optimal bot actions based on game state snapshots.

## Overview

The DecisionEngineService is an AI-powered decision-making component that provides intelligent combat and action prediction for bot automation. It uses machine learning to analyze game state snapshots and generate optimal action sequences based on real-time combat scenarios.

This service is a .NET 8 Worker Service that learns from experience by processing gameplay snapshots to improve decision-making over time, predicts actions using trained ML models based on current game state, monitors data directories for new training files and automatically retrains models, and persists trained models in SQLite for durability across restarts.

The service combines custom ML models for action prediction with ML.NET's advanced training pipelines, providing both simple rule-based decisions and sophisticated machine learning predictions for complex scenarios.

## Architecture

```
+-----------------------------------------------------------------------------+
|                      DecisionEngineService Architecture                      |
+-----------------------------------------------------------------------------+
|                                                                             |
|   +---------------------------------------------------------------------+   |
|   |                   DecisionEngineWorker (BackgroundService)          |   |
|   |               Main service loop - coordinates all components         |   |
|   +---------------------------------------------------------------------+   |
|                                      |                                      |
|         +----------------------------+----------------------------+         |
|         |                            |                            |         |
|         |                            |                            |         |
|   +--------------+   +------------------------+   +------------------+     |
|   |DecisionEngine|   | CombatPredictionService|   |  MangosRepository|     |
|   |              |   |                        |   |                  |     |
|   | - MLModel    |   | - ML.NET Integration   |   | - Game Database  |     |
|   | - Weight Adj.|   | - Model Training       |   | - Creature Data  |     |
|   | - File Watch |   | - Action Prediction    |   | - Spell Info     |     |
|   +--------------+   +------------------------+   +------------------+     |
|         |                            |                                      |
|         |                            |                                      |
|   +--------------+   +------------------------+                             |
|   |SQLiteDatabase|   |     FileSystemWatcher  |                             |
|   |              |   |                        |                             |
|   | - ModelWeights   | - .bin file monitoring |                             |
|   | - TrainedModel   | - Auto-retraining      |                             |
|   +--------------+   +------------------------+                             |
|                                                                             |
|   +---------------------------------------------------------------------+   |
|   |                      External Communication                          |   |
|   |  +-------------------+    +----------------------------------+      |   |
|   |  |  CombatModelClient|    |  CombatModelServiceListener      |      |   |
|   |  |  (Outbound Calls) |    |  (Inbound Requests)              |      |   |
|   |  +-------------------+    +----------------------------------+      |   |
|   +---------------------------------------------------------------------+   |
|                                                                             |
+-----------------------------------------------------------------------------+
```

## Project Structure

```
Services/DecisionEngineService/
+-- DecisionEngineService.csproj      # .NET 8 Worker Service project file
+-- README.md                          # This documentation
+-- DecisionEngineWorker.cs            # Main BackgroundService worker
+-- DecisionEngine.cs                  # Core ML model and prediction logic
+-- CombatPredictionService.cs         # ML.NET-based combat predictions
+-- Clients/
|   +-- CombatModelClient.cs           # Client for ML model service calls
+-- Listeners/
|   +-- CombatModelServiceListener.cs  # Handles incoming prediction requests
+-- Repository/
    +-- MangosRepository.cs            # MaNGOS database access for game data
```

## Key Components

### DecisionEngine

The core decision-making component that manages ML model weights and generates action recommendations:

```csharp
public class DecisionEngine
{
    // Initialize with training data directory and SQLite database
    public DecisionEngine(string binFileDirectory, SQLiteDatabase db)

    // Get recommended actions based on current game state
    public static List<ActionMap> GetNextActions(ActivitySnapshot snapshot)
}
```

**Features**:
- Watches for new `.bin` training files via `FileSystemWatcher`
- Automatically processes new data and retrains the model
- Persists model weights to SQLite for durability
- Provides action predictions based on game state

### MLModel

Internal ML model that learns from gameplay and predicts actions:

```csharp
public class MLModel
{
    // Learn from a single gameplay snapshot
    public void LearnFromSnapshot(ActivitySnapshot snapshot)

    // Predict next actions based on current state
    public static List<ActionMap> Predict(ActivitySnapshot snapshot)

    // Get current model weights for persistence
    public List<float> GetWeights()
}
```

**Decision Logic Examples**:
- If player health < 50%: Recommend healing spell
- If nearby hostile units > 2: Recommend AoE attack
- Adjust weights based on action success/failure feedback

### CombatPredictionService

ML.NET-powered service for sophisticated combat predictions:

```csharp
public class CombatPredictionService
{
    // Predict optimal action based on combat data
    public ActivitySnapshot PredictAction(ActivitySnapshot inputData)

    // Update model with latest trained version
    public void UpdateModel()
}
```

**ML Pipeline**:
- Uses SDCA Maximum Entropy trainer for multiclass classification
- Features: health, position, facing, target, nearby units
- Automatically retrains when new data arrives
- Stores trained models as binary blobs in SQLite

### MangosRepository

Database access layer for retrieving game world information:

```csharp
public class MangosRepository
{
    // Query creature templates, spell data, zone info, etc.
    // Supports both SQLite and MySQL connections
}
```

## Data Flow

```
+----------------+     +---------------+     +--------------+
|  Bot Instance  | --> |  .bin Files   | --> | FileWatcher  |
|  (Gameplay)    |     | (Snapshots)   |     |              |
+----------------+     +---------------+     +--------------+
                                                     |
                                                     |
+----------------+     +---------------+     +--------------+
|  Prediction    | <-- |  MLModel      | <-- | Training     |
|  Request       |     |  Weights      |     | Pipeline     |
+----------------+     +---------------+     +--------------+
        |                                           |
        |                                           |
+----------------+                         +--------------+
|  ActionMap     |                         |  SQLite DB   |
|  (Response)    |                         | (Persistence)|
+----------------+                         +--------------+
```

## Machine Learning Pipeline

### Data Flow

1. **Data Ingestion**: Monitor for `.bin` files containing `ActivitySnapshot` data
2. **Feature Extraction**: Parse snapshots into ML-ready features (health, position, nearby units, etc.)
3. **Model Training**: Update weights based on action success/failure feedback
4. **Prediction**: Generate action recommendations with confidence scores
5. **Persistence**: Save updated model weights to database

### Decision Logic

The ML model considers multiple factors for action prediction:

- **Player State**: Health, mana, position, facing direction
- **Target Information**: Enemy health, type, abilities
- **Environmental Context**: Nearby units, terrain, threat level
- **Historical Performance**: Previous action success rates
- **Combat Patterns**: Recognized engagement scenarios

### Example Decision Scenarios

```csharp
// Low health scenario
if (snapshot.Player.Unit.Health < snapshot.Player.Unit.MaxHealth * 0.5)
{
    // Recommend healing action
    actions.Add(new ActionMessage
    {
        ActionType = ActionType.CastSpell,
        Parameters = { new RequestParameter { IntParam = HEALING_SPELL_ID } }
    });
}

// Multiple enemies scenario
if (nearbyHostileUnits.Count > 2)
{
    // Recommend AoE attack
    actions.Add(new ActionMessage
    {
        ActionType = ActionType.CastSpell,
        Parameters = { new RequestParameter { IntParam = AOE_SPELL_ID } }
    });
}
```

## Dependencies

### NuGet Packages

| Package | Version | Purpose |
|---------|---------|---------|
| Microsoft.ML | 3.0.1 | Machine learning framework |
| Microsoft.Data.Sqlite.Core | 8.0.8 | SQLite database access for model storage |
| System.Data.SQLite | 1.0.118 | Additional SQLite support |
| MySql.Data | 9.0.0 | MySQL/MaNGOS database access |
| Microsoft.Extensions.Hosting | 8.0.0 | Worker service hosting |

### Project References

- **BotCommLayer**: Protobuf message definitions (`ActivitySnapshot`, `ActionMap`, etc.)

## Configuration

Configure via `appsettings.json`:

```json
{
  "DecisionEngine": {
    "DataDirectory": "./data/snapshots",
    "ProcessedDirectory": "./data/processed",
    "SqliteConnection": "Data Source=decision_engine.db",
    "MangosConnection": "Server=localhost;Database=mangos;Uid=root;Pwd=;"
  }
}
```

## Usage

### Making Predictions

```csharp
// Create a game state snapshot
var snapshot = new ActivitySnapshot
{
    Player = new WoWPlayerData
    {
        Unit = new WoWUnitData
        {
            Health = 800,
            MaxHealth = 1200,
            // ... other stats
        }
    },
    NearbyUnits = { /* hostile units */ },
    CurrentAction = new ActionMessage { /* current action */ }
};

// Get recommended actions
var actions = DecisionEngine.GetNextActions(snapshot);

foreach (var actionMap in actions)
{
    foreach (var action in actionMap.Actions)
    {
        Console.WriteLine($"Recommended: {action.ActionType}");
    }
}
```

### Training Data Format

Training data is stored in `.bin` files using Protobuf serialization:

```csharp
// ActivitySnapshot messages are written with length-delimited encoding
using var stream = new FileStream("training_data.bin", FileMode.Create);
snapshot.WriteDelimitedTo(stream);
```

### Database Schema

**ModelWeights Table**:
```sql
CREATE TABLE ModelWeights (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    weights TEXT NOT NULL,  -- Comma-separated float values
    created_at DATETIME DEFAULT CURRENT_TIMESTAMP
);
```

**TrainedModel Table** (for ML.NET models):
```sql
CREATE TABLE TrainedModel (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    ModelData BLOB NOT NULL,  -- Serialized ITransformer
    created_at DATETIME DEFAULT CURRENT_TIMESTAMP
);
```

## Action Types

The service can recommend various actions defined in `ActionType` enum (from BotCommLayer):

| Action | Description |
|--------|-------------|
| `CastSpell` | Cast a spell by ID |
| `Goto` | Move to coordinates |
| `Attack` | Begin attacking target |
| `Heal` | Use healing ability |
| `Flee` | Retreat from combat |
| `Loot` | Loot nearby corpses |
| And 50+ more... | See `communication.proto` |

## Extending the Service

### Adding New Features

To add new input features for the ML model:

1. Update the `ActivitySnapshot` in `communication.proto`
2. Regenerate C# classes in BotCommLayer
3. Modify the ML pipeline in `CombatPredictionService`:

```csharp
var pipeline = _mlContext.Transforms.Concatenate("Features",
    "self.health",
    "self.max_health",
    "self.mana",           // New feature
    "self.rage",           // New feature
    // ... etc
);
```

### Custom Decision Logic

Override decision logic in `MLModel.GenerateActionMap()`:

```csharp
// Add class-specific logic
if (snapshot.Player.Unit.Class == Class.Warrior &&
    snapshot.Player.Unit.Rage > 50)
{
    actionMaps.Add(new ActionMap
    {
        Actions = {
            new ActionMessage
            {
                ActionType = ActionType.CastSpell,
                Parameters = { new RequestParameter { IntParam = HEROIC_STRIKE_ID } }
            }
        }
    });
}
```

## Integration with Other Services

The DecisionEngineService integrates with:

- **BackgroundBotRunner**: Receives prediction requests, returns action recommendations
- **StateManager**: Coordinates state transitions based on predictions
- **PathfindingService**: Movement actions use pathfinding for navigation

## Performance Considerations

- **Async Processing**: Non-blocking file monitoring and model updates
- **Memory Management**: Efficient snapshot processing and cleanup
- **Database Optimization**: Connection pooling and query optimization
- **Model Efficiency**: Lightweight prediction engine with fast inference

## Error Handling

The service implements comprehensive error handling:

- **Database Connectivity**: Automatic retry logic for connection failures
- **File Processing**: Graceful handling of corrupted or incomplete data files
- **Model Training**: Fallback to previous model versions on training failures
- **Prediction Errors**: Default action recommendations when ML model fails

## Monitoring & Debugging

### Logging

The service provides detailed logging for:

- Model training progress and accuracy metrics
- Action prediction confidence scores
- File processing statistics
- Database query performance
- Error diagnostics and stack traces

### Metrics

Track important performance indicators:

- **Prediction Accuracy**: Success rate of recommended actions
- **Model Training Time**: Time taken for model updates
- **Processing Latency**: Snapshot to action prediction time
- **Memory Usage**: Model size and memory consumption

## Related Documentation

- See [ARCHITECTURE.md](../../ARCHITECTURE.md) for system overview
- See [BotCommLayer README](../../Exports/BotCommLayer/README.md) for message definitions
- See [StateManager README](../StateManager/README.md) for state machine integration
- See [BackgroundBotRunner README](../BackgroundBotRunner/README.md) for bot execution engine
- See [PathfindingService README](../PathfindingService/README.md) for navigation integration

---

*This component is part of the WWoW (Westworld of Warcraft) simulation platform.*
