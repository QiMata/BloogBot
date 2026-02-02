# DecisionEngineService

## Overview

The **DecisionEngineService** is a machine learning-powered decision-making service for the WWoW (Westworld of Warcraft) bot system. It uses Microsoft ML.NET to predict optimal bot actions based on game state snapshots, providing intelligent combat decisions and action recommendations.
The **DecisionEngineService** is an AI-powered decision-making component of the BloogBot ecosystem that provides intelligent combat and action prediction for World of Warcraft automation. It uses machine learning to analyze game state snapshots and generate optimal action sequences based on real-time combat scenarios.

This service is a .NET 8 Worker Service that:
- **Learns from Experience**: Processes gameplay snapshots to improve decision-making over time
- **Predicts Actions**: Uses trained ML models to recommend next actions based on current game state
- **Monitors Data**: Watches for new training data files and automatically retrains models
- **Persists Models**: Stores trained models in SQLite for persistence across restarts
## Features

## Architecture
- **Machine Learning Engine**: Custom ML model for action prediction based on game state analysis
- **Combat Prediction**: Specialized combat scenario analysis and response generation
- **Real-time Learning**: Continuous model improvement from live gameplay data
- **Persistent Storage**: SQLite-based model weight persistence and MySQL game data integration
- **File Monitoring**: Automatic processing of binary game data files
- **Dynamic Adaptation**: Real-time model updates based on action success/failure feedback

```
???????????????????????????????????????????????????????????????????????????????
?                      DecisionEngineService Architecture                      ?
???????????????????????????????????????????????????????????????????????????????
?                                                                             ?
?   ???????????????????????????????????????????????????????????????????????   ?
?   ?                   DecisionEngineWorker (BackgroundService)          ?   ?
?   ?               Main service loop - coordinates all components         ?   ?
?   ???????????????????????????????????????????????????????????????????????   ?
?                                      ?                                      ?
?         ???????????????????????????????????????????????????????????        ?
?         ?                            ?                            ?        ?
?         ?                            ?                            ?        ?
?   ?????????????????   ???????????????????????????   ????????????????????? ?
?   ?DecisionEngine ?   ? CombatPredictionService ?   ?  MangosRepository ? ?
?   ?               ?   ?                         ?   ?                   ? ?
?   ? • MLModel     ?   ? • ML.NET Integration    ?   ? • Game Database   ? ?
?   ? • Weight Adj. ?   ? • Model Training        ?   ? • Creature Data   ? ?
?   ? • File Watch  ?   ? • Action Prediction     ?   ? • Spell Info      ? ?
?   ?????????????????   ???????????????????????????   ????????????????????? ?
?         ?                            ?                                      ?
?         ?                            ?                                      ?
?   ?????????????????   ???????????????????????????                          ?
?   ?SQLiteDatabase ?   ?     FileSystemWatcher   ?                          ?
?   ?               ?   ?                         ?                          ?
?   ? • ModelWeights?   ? • .bin file monitoring  ?                          ?
?   ? • TrainedModel?   ? • Auto-retraining       ?                          ?
?   ?????????????????   ???????????????????????????                          ?
?                                                                             ?
?   ???????????????????????????????????????????????????????????????????????   ?
?   ?                      External Communication                          ?   ?
?   ?  ????????????????????????    ?????????????????????????????????????? ?   ?
?   ?  ?  CombatModelClient   ?    ?  CombatModelServiceListener       ? ?   ?
?   ?  ?  (Outbound Calls)    ?    ?  (Inbound Requests)               ? ?   ?
?   ?  ????????????????????????    ?????????????????????????????????????? ?   ?
?   ???????????????????????????????????????????????????????????????????????   ?
?                                                                             ?
???????????????????????????????????????????????????????????????????????????????
```
## Architecture

## Project Structure
### Core Components

```
Services/DecisionEngineService/
??? DecisionEngineService.csproj      # Project file
??? README.md                          # This documentation
??? DecisionEngineWorker.cs            # Main BackgroundService worker
??? DecisionEngine.cs                  # Core ML model and prediction logic
??? CombatPredictionService.cs         # ML.NET-based combat predictions
??? Clients/
?   ??? CombatModelClient.cs           # Client for ML model service calls
??? Listeners/
?   ??? CombatModelServiceListener.cs  # Handles incoming prediction requests
??? Repository/
    ??? MangosRepository.cs            # MaNGOS database access for game data
```
```
DecisionEngineService/
??? DecisionEngine.cs              # Main ML decision engine
??? DecisionEngineWorker.cs        # Background service worker
??? CombatPredictionService.cs     # Combat-specific ML predictions
??? Repository/
?   ??? MangosRepository.cs        # Game database access layer
??? Listeners/
?   ??? CombatModelServiceListener.cs
??? Clients/
    ??? CombatModelClient.cs
```

## Key Components
### Key Classes

### DecisionEngine
#### **DecisionEngine**
- Manages ML model lifecycle and training
- Processes binary snapshot files via `FileSystemWatcher`
- Generates action predictions based on `ActivitySnapshot` data
- Handles model persistence to SQLite database

The core decision-making component that manages ML model weights and generates action recommendations:
#### **CombatPredictionService**
- Specialized ML.NET-based combat prediction
- Real-time model retraining with new combat data
- Binary file processing and data pipeline management
- Model versioning and rollback capabilities

```csharp
public class DecisionEngine
{
    // Initialize with training data directory and SQLite database
    public DecisionEngine(string binFileDirectory, SQLiteDatabase db)
    
    // Get recommended actions based on current game state
    public static List<ActionMap> GetNextActions(ActivitySnapshot snapshot)
}
```
#### **MangosRepository**
- Comprehensive World of Warcraft database access
- Supports 100+ game data tables including creatures, spells, items, and quests
- MySQL connection management with error handling
- Optimized queries for game state validation

## Getting Started

**Features**:
- Watches for new `.bin` training files via `FileSystemWatcher`
- Automatically processes new data and retrains the model
- Persists model weights to SQLite for durability
- Provides action predictions based on game state
### Prerequisites

### MLModel
- **.NET 8.0** or later
- **MySQL Database** (Mangos/TrinityCore compatible)
- **SQLite** for model storage
- **BotCommLayer** dependency for communication protocols

Internal ML model that learns from gameplay and predicts actions:
### Installation

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
1. **Clone the BloogBot repository**
2. **Configure database connections** in `appsettings.json`
3. **Build the project**:
   ```bash
   dotnet build Services/DecisionEngineService/DecisionEngineService.csproj
   ```

### Configuration

Update your configuration with appropriate connection strings:

```json
{
  "ConnectionStrings": {
    "MangosDatabase": "server=localhost;user=app;database=mangos;port=3306;password=app",
    "MLModelStorage": "Data Source=models.db"
  },
  "DecisionEngine": {
    "DataDirectory": "./data",
    "ProcessedDirectory": "./processed",
    "ModelUpdateInterval": "00:05:00"
  }
}
```

## Usage

### Basic Implementation

```csharp
// Initialize the decision engine
var decisionEngine = new DecisionEngine(binFileDirectory, sqliteDatabase);

// Get action predictions
var predictions = DecisionEngine.GetNextActions(currentSnapshot);

// Process predictions
foreach (var actionMap in predictions)
{
    foreach (var action in actionMap.Actions)
    {
        // Execute recommended actions
        await ExecuteAction(action);
    }
}
```

### Combat Prediction Service

```csharp
// Initialize combat prediction service
var combatService = new CombatPredictionService(
    connectionString, 
    dataDirectory, 
    processedDirectory
);

// Predict optimal combat actions
var prediction = combatService.PredictAction(combatSnapshot);
```

## Machine Learning Pipeline

**Decision Logic Examples**:
- If player health < 50%: Recommend healing spell
- If nearby hostile units > 2: Recommend AoE attack
- Adjust weights based on action success/failure feedback
### Data Flow

### CombatPredictionService
1. **Data Ingestion**: Monitor for `.bin` files containing `ActivitySnapshot` data
2. **Feature Extraction**: Parse snapshots into ML-ready features (health, position, nearby units, etc.)
3. **Model Training**: Update weights based on action success/failure feedback
4. **Prediction**: Generate action recommendations with confidence scores
5. **Persistence**: Save updated model weights to database

ML.NET-powered service for sophisticated combat predictions:
### Decision Logic

```csharp
public class CombatPredictionService
{
    // Predict optimal action based on combat data
    public ActivitySnapshot PredictAction(ActivitySnapshot inputData)
    
    // Update model with latest trained version
    public void UpdateModel()
}
```
The ML model considers multiple factors for action prediction:

- **Player State**: Health, mana, position, facing direction
- **Target Information**: Enemy health, type, abilities
- **Environmental Context**: Nearby units, terrain, threat level
- **Historical Performance**: Previous action success rates
- **Combat Patterns**: Recognized engagement scenarios

**ML Pipeline**:
- Uses SDCA Maximum Entropy trainer for multiclass classification
- Features: health, position, facing, target, nearby units
- Automatically retrains when new data arrives
- Stores trained models as binary blobs in SQLite
### Example Decision Scenarios

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
?????????????????     ????????????????     ??????????????????
?  Bot Instance ??????? .bin Files   ??????? FileWatcher    ?
?  (Gameplay)   ?     ? (Snapshots)  ?     ?                ?
?????????????????     ????????????????     ??????????????????
                                                     ?
                                                     ?
?????????????????     ????????????????     ??????????????????
?  Prediction   ???????  MLModel     ??????? Training       ?
?  Request      ?     ?  Weights     ?     ? Pipeline       ?
?????????????????     ????????????????     ??????????????????
        ?                                           ?
        ?                                           ?
?????????????????                         ??????????????????
?  ActionMap    ?                         ?  SQLite DB     ?
?  (Response)   ?                         ?  (Persistence) ?
?????????????????                         ??????????????????
```

## Dependencies

| Package | Version | Purpose |
|---------|---------|---------|
| Microsoft.ML | 3.0.1 | Machine learning framework |
| Microsoft.Data.Sqlite.Core | 8.0.8 | SQLite database access |
| System.Data.SQLite | 1.0.118 | Additional SQLite support |
| MySql.Data | 9.0.0 | MySQL/MaNGOS database access |
| Microsoft.Extensions.Hosting | 8.0.0 | Worker service hosting |

## Project References

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

### Training Data Format
## Technical Details

### Dependencies

Training data is stored in `.bin` files using Protobuf serialization:
- **Microsoft.ML** (3.0.1) - Machine learning framework
- **Microsoft.Data.Sqlite.Core** (8.0.8) - Model persistence
- **MySql.Data** (9.0.0) - Game database access
- **System.Data.SQLite** (1.0.118) - Legacy SQLite support
- **BotCommLayer** - Communication protocols

```csharp
// ActivitySnapshot messages are written with length-delimited encoding
using var stream = new FileStream("training_data.bin", FileMode.Create);
snapshot.WriteDelimitedTo(stream);
```
### Performance Considerations

### Database Schema
- **Async Processing**: Non-blocking file monitoring and model updates
- **Memory Management**: Efficient snapshot processing and cleanup
- **Database Optimization**: Connection pooling and query optimization
- **Model Efficiency**: Lightweight prediction engine with fast inference

**ModelWeights Table**:
```sql
CREATE TABLE ModelWeights (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    weights TEXT NOT NULL,  -- Comma-separated float values
    created_at DATETIME DEFAULT CURRENT_TIMESTAMP
);
```
### Error Handling

**TrainedModel Table** (for ML.NET models):
```sql
CREATE TABLE TrainedModel (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    ModelData BLOB NOT NULL,  -- Serialized ITransformer
    created_at DATETIME DEFAULT CURRENT_TIMESTAMP
);
```
The service implements comprehensive error handling:

## Action Types
- **Database Connectivity**: Automatic retry logic for connection failures
- **File Processing**: Graceful handling of corrupted or incomplete data files
- **Model Training**: Fallback to previous model versions on training failures
- **Prediction Errors**: Default action recommendations when ML model fails

The service can recommend various actions defined in `ActionType` enum (from BotCommLayer):
## Integration

| Action | Description |
|--------|-------------|
| `CastSpell` | Cast a spell by ID |
| `Goto` | Move to coordinates |
| `Attack` | Begin attacking target |
| `Heal` | Use healing ability |
| `Flee` | Retreat from combat |
| `Loot` | Loot nearby corpses |
| And 50+ more... | See `communication.proto` |
### BloogBot Ecosystem

The DecisionEngineService integrates with:

- **[StateManager](../StateManager/README.md)**: Coordinates decision-making across multiple bot instances
- **[BotRunner](../../Exports/BotRunner/README.md)**: Executes recommended actions
- **[PromptHandlingService](../PromptHandlingService/README.md)**: AI-enhanced decision context
- **[PathfindingService](../PathfindingService/README.md)**: Movement decision optimization

### Communication Protocols

- **Protocol Buffers**: Structured data exchange via BotCommLayer
- **SQLite**: Local model storage and caching
- **MySQL**: Game world data queries and validation
- **File System**: Binary snapshot processing

## Monitoring & Debugging

## Extending the Service
### Logging

### Adding New Features
The service provides detailed logging for:

To add new input features for the ML model:
- Model training progress and accuracy metrics
- Action prediction confidence scores
- File processing statistics
- Database query performance
- Error diagnostics and stack traces

1. Update the `ActivitySnapshot` in `communication.proto`
2. Regenerate C# classes in BotCommLayer
3. Modify the ML pipeline in `CombatPredictionService`:
### Metrics

```csharp
var pipeline = _mlContext.Transforms.Concatenate("Features",
    "self.health",
    "self.max_health",
    "self.mana",           // New feature
    "self.rage",           // New feature
    // ... etc
);
```
Track important performance indicators:

### Custom Decision Logic

Override decision logic in `MLModel.GenerateActionMap()`:
- **Prediction Accuracy**: Success rate of recommended actions
- **Model Training Time**: Time taken for model updates
- **Processing Latency**: Snapshot to action prediction time
- **Memory Usage**: Model size and memory consumption

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
## Development

## Integration with Other Services
### Adding New Decision Logic

1. **Extend ActivitySnapshot**: Add new data fields to the protobuf schema
2. **Update Feature Extraction**: Modify the ML pipeline to use new features
3. **Enhance Prediction Logic**: Add new decision rules to the model
4. **Test Integration**: Validate with live game scenarios

### Testing

```bash
# Run unit tests
dotnet test Tests/DecisionEngineService.Tests

The DecisionEngineService integrates with:
# Run integration tests with live data
dotnet test Tests/DecisionEngineService.Integration.Tests
```

- **BackgroundBotRunner**: Receives prediction requests, returns action recommendations
- **StateManager**: Coordinates state transitions based on predictions
- **PathfindingService**: Movement actions use pathfinding for navigation
## License

This project is part of the BloogBot ecosystem. Please refer to the main project license for usage terms.

## Contributing

1. **Fork the repository** and create a feature branch
2. **Follow .NET coding standards** and conventions
3. **Add comprehensive tests** for new functionality
4. **Update documentation** for new features or changes
5. **Ensure performance** doesn't degrade with changes

## Related Documentation
## Related Projects

- See `ARCHITECTURE.md` for system overview
- See `Exports/BotCommLayer/README.md` for message definitions
- See `Services/StateManager/README.md` for state machine integration
- See `BloogBot.AI/README.md` for Semantic Kernel AI integration
- **[BloogBot.AI](../../BloogBot.AI/README.md)**: Core AI behavior and state management
- **[BackgroundBotRunner](../BackgroundBotRunner/README.md)**: Bot execution engine
- **[GameData.Core](../../Exports/GameData.Core/README.md)**: Game data structures
- **[WoWSharpClient](../../Exports/WoWSharpClient/README.md)**: Game client communication

---

*This component is part of the WWoW (Westworld of Warcraft) simulation platform.*
*DecisionEngineService provides the cognitive foundation for intelligent World of Warcraft automation, combining machine learning with real-time game analysis for optimal bot decision-making.*