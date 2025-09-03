# DecisionEngineService

## Overview

The **DecisionEngineService** is an AI-powered decision-making component of the BloogBot ecosystem that provides intelligent combat and action prediction for World of Warcraft automation. It uses machine learning to analyze game state snapshots and generate optimal action sequences based on real-time combat scenarios.

## Features

- **Machine Learning Engine**: Custom ML model for action prediction based on game state analysis
- **Combat Prediction**: Specialized combat scenario analysis and response generation
- **Real-time Learning**: Continuous model improvement from live gameplay data
- **Persistent Storage**: SQLite-based model weight persistence and MySQL game data integration
- **File Monitoring**: Automatic processing of binary game data files
- **Dynamic Adaptation**: Real-time model updates based on action success/failure feedback

## Architecture

### Core Components

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

### Key Classes

#### **DecisionEngine**
- Manages ML model lifecycle and training
- Processes binary snapshot files via `FileSystemWatcher`
- Generates action predictions based on `ActivitySnapshot` data
- Handles model persistence to SQLite database

#### **CombatPredictionService**
- Specialized ML.NET-based combat prediction
- Real-time model retraining with new combat data
- Binary file processing and data pipeline management
- Model versioning and rollback capabilities

#### **MangosRepository**
- Comprehensive World of Warcraft database access
- Supports 100+ game data tables including creatures, spells, items, and quests
- MySQL connection management with error handling
- Optimized queries for game state validation

## Getting Started

### Prerequisites

- **.NET 8.0** or later
- **MySQL Database** (Mangos/TrinityCore compatible)
- **SQLite** for model storage
- **BotCommLayer** dependency for communication protocols

### Installation

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

## Technical Details

### Dependencies

- **Microsoft.ML** (3.0.1) - Machine learning framework
- **Microsoft.Data.Sqlite.Core** (8.0.8) - Model persistence
- **MySql.Data** (9.0.0) - Game database access
- **System.Data.SQLite** (1.0.118) - Legacy SQLite support
- **BotCommLayer** - Communication protocols

### Performance Considerations

- **Async Processing**: Non-blocking file monitoring and model updates
- **Memory Management**: Efficient snapshot processing and cleanup
- **Database Optimization**: Connection pooling and query optimization
- **Model Efficiency**: Lightweight prediction engine with fast inference

### Error Handling

The service implements comprehensive error handling:

- **Database Connectivity**: Automatic retry logic for connection failures
- **File Processing**: Graceful handling of corrupted or incomplete data files
- **Model Training**: Fallback to previous model versions on training failures
- **Prediction Errors**: Default action recommendations when ML model fails

## Integration

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

## Development

### Adding New Decision Logic

1. **Extend ActivitySnapshot**: Add new data fields to the protobuf schema
2. **Update Feature Extraction**: Modify the ML pipeline to use new features
3. **Enhance Prediction Logic**: Add new decision rules to the model
4. **Test Integration**: Validate with live game scenarios

### Testing

```bash
# Run unit tests
dotnet test Tests/DecisionEngineService.Tests

# Run integration tests with live data
dotnet test Tests/DecisionEngineService.Integration.Tests
```

## License

This project is part of the BloogBot ecosystem. Please refer to the main project license for usage terms.

## Contributing

1. **Fork the repository** and create a feature branch
2. **Follow .NET coding standards** and conventions
3. **Add comprehensive tests** for new functionality
4. **Update documentation** for new features or changes
5. **Ensure performance** doesn't degrade with changes

## Related Projects

- **[BloogBot.AI](../../BloogBot.AI/README.md)**: Core AI behavior and state management
- **[BackgroundBotRunner](../BackgroundBotRunner/README.md)**: Bot execution engine
- **[GameData.Core](../../Exports/GameData.Core/README.md)**: Game data structures
- **[WoWSharpClient](../../Exports/WoWSharpClient/README.md)**: Game client communication

---

*DecisionEngineService provides the cognitive foundation for intelligent World of Warcraft automation, combining machine learning with real-time game analysis for optimal bot decision-making.*