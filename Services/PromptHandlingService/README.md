# PromptHandlingService

A .NET 8 Worker Service that provides AI/LLM prompt execution capabilities for the WWoW (Westworld of Warcraft) bot system, abstracting multiple AI provider backends and providing reusable prompt functions for bot decision-making.

## Overview

PromptHandlingService is a comprehensive AI prompt processing service that integrates with various AI providers (Azure OpenAI, OpenAI, Ollama) to process prompts and generate intelligent responses. It serves as the brain of the bot ecosystem, enabling natural language understanding and intelligent decision making.

The service provides multi-provider support for Azure AI, OpenAI, Ollama, and a Fake provider for testing. It offers reusable, composable prompt functions for specialized tasks, response caching to reduce API calls, intent parsing for natural language to structured command conversion, and AI-assisted game master command construction.

This flexible framework allows creating specialized prompt functions that handle different aspects of automation, from understanding user intent to generating game commands, prioritizing character skills, and editing server configurations.

## Architecture

```
+----------------------------------------------------------------------+
|                     PromptHandlingService                            |
|                                                                      |
|  +----------------------------------------------------------------+ |
|  |           PromptHandlingServiceWorker (BackgroundService)       | |
|  |                      Main service host                          | |
|  +----------------------------------------------------------------+ |
|                                   |                                  |
|  +----------------------------------------------------------------+ |
|  |                     PromptRunnerFactory                         | |
|  |              Creates appropriate IPromptRunner instance         | |
|  +----------------------------------------------------------------+ |
|                                   |                                  |
|     +-----------------------------+-----------------------------+   |
|     |             |               |               |             |   |
|  +-------+   +----------+   +----------+   +-------------+         |
|  |Azure  |   | OpenAI   |   |  Ollama  |   |   Fake      |         |
|  |  AI   |   | Runner   |   |  Runner  |   |  Runner     |         |
|  |Runner |   |          |   |          |   | (Testing)   |         |
|  +-------+   +----------+   +----------+   +-------------+         |
|                                                                      |
|  +----------------------------------------------------------------+ |
|  |                    Predefined Prompt Functions                  | |
|  |  +---------------+  +---------------+  +---------------+        | |
|  |  |IntentParser   |  | GMCommand     |  | ConfigEditor  |        | |
|  |  |  Function     |  | Constructor   |  |   Function    |        | |
|  |  +---------------+  +---------------+  +---------------+        | |
|  |  +---------------+                                              | |
|  |  |CharacterSkill |                                              | |
|  |  |Prioritization |                                              | |
|  |  +---------------+                                              | |
|  +----------------------------------------------------------------+ |
|                                                                      |
|  +----------------------------------------------------------------+ |
|  |                       PromptCache                               | |
|  |                Caches responses to reduce API calls             | |
|  +----------------------------------------------------------------+ |
+----------------------------------------------------------------------+
```

## Key Features

### Multi-Provider AI Support

- **Azure OpenAI**: Enterprise-grade AI with Azure integration
- **OpenAI**: Direct OpenAI API access with GPT models
- **Ollama**: Local AI model execution for privacy and offline use
- **Fake Provider**: Testing and development mock provider

### Specialized Prompt Functions

- **Intent Parser**: Analyzes user requests and routes them to appropriate handlers
- **GM Command Constructor**: Generates game master commands for server administration
- **Character Skill Prioritizer**: Determines optimal skill progression for characters
- **Config Editor**: Handles MaNGOS server configuration modifications

### Intelligent Caching

- **Prompt Cache**: Stores and retrieves previous prompt results for efficiency
- **Chat History Management**: Maintains conversation context across sessions
- **JSON and Text Export**: Save conversations for analysis and debugging

### Advanced Utilities

- **String Parsing**: Extract content from markdown code blocks and structured text
- **SQL Parser**: Handle database table definitions and queries
- **List Processing**: Parse bulleted and structured lists

## Project Structure

```
Services/PromptHandlingService/
+-- PromptHandlingService.csproj        # .NET 8 Worker Service project
+-- PromptHandlingServiceWorker.cs      # BackgroundService implementation
+-- IPromptRunner.cs                    # Provider abstraction interface
+-- IPromptFunction.cs                  # Prompt function interface
+-- PromptFunctionBase.cs               # Base class for prompt functions
+-- PromptRunnerFactory.cs              # Factory for creating runners
+-- Providers/
|   +-- AzureAIPromptRunner.cs          # Azure AI endpoint integration
|   +-- OpenAIPromptRunner.cs           # OpenAI API integration
|   +-- OllamaPromptRunner.cs           # Local Ollama integration
|   +-- FakePromptRunner.cs             # Mock runner for testing
+-- Predefined/
|   +-- IntentParser/
|   |   +-- IntentionParserFunction.cs  # NL -> structured intent
|   +-- GMCommands/
|   |   +-- GMCommandConstructionFunction.cs
|   +-- MaNGOSConfigHandlers/
|   |   +-- ConfigEditorFunction.cs
|   +-- CharacterSkills/
|       +-- CharacterSkillPrioritizationFunction.cs
+-- Cache/
|   +-- PromptCache.cs                  # Response caching
|   +-- Models/
|       +-- PreviousPrompts.cs          # Cache entry model
+-- Utilities/
|   +-- StringParserUtilities.cs        # Response parsing helpers
+-- README.md                           # This documentation
```

## Key Components

### IPromptRunner Interface

The core abstraction for AI providers:

```csharp
public interface IPromptRunner : IDisposable
{
    /// <summary>
    /// Runs a chat completion with the given history.
    /// </summary>
    /// <param name="chatHistory">Messages as role/content pairs</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The assistant's response</returns>
    Task<string?> RunChatAsync(
        IEnumerable<KeyValuePair<string, string?>> chatHistory,
        CancellationToken cancellationToken);

    /// <summary>
    /// Maximum concurrent requests supported by this provider.
    /// </summary>
    int MaxConcurrent { get; }
}
```

### Prompt Providers

#### AzureAIPromptRunner

Connects to Azure AI endpoints (Azure OpenAI, Azure ML):

```csharp
var runner = new AzureAIPromptRunner(
    baseAddress: new Uri("https://your-endpoint.openai.azure.com/"),
    apiKey: "your-api-key"
);

var response = await runner.RunChatAsync(new[]
{
    KeyValuePair.Create("system", "You are a WoW bot assistant."),
    KeyValuePair.Create("user", "What spell should I cast next?")
}, cancellationToken);
```

#### OllamaPromptRunner

Connects to local Ollama instance for self-hosted models:

```csharp
var runner = new OllamaPromptRunner(
    baseAddress: new Uri("http://localhost:11434/"),
    modelName: "llama2"
);
```

#### FakePromptRunner

Returns predefined responses for testing without API calls:

```csharp
var runner = new FakePromptRunner();
// Returns mock responses for testing prompt functions
```

### Predefined Prompt Functions

#### IntentionParserFunction

Converts natural language commands into structured intents:

```csharp
// Input: "Go grind murlocs near Goldshire"
// Output: { Intent: "Grind", Target: "Murloc", Location: "Goldshire" }
```

#### GMCommandConstructionFunction

Generates MaNGOS GM commands from natural language:

```csharp
// Input: "Give me 100 gold"
// Output: ".modify money 1000000"
```

#### CharacterSkillPrioritizationFunction

Determines skill usage priority based on class and situation:

```csharp
// Input: Current game state (health, mana, targets, etc.)
// Output: Ordered list of skills to use
```

### PromptCache

Caches responses to reduce API calls for repeated queries:

```csharp
public class PromptCache
{
    public bool TryGetCachedResponse(string prompt, out string? response);
    public void CacheResponse(string prompt, string response);
}
```

## Dependencies

### NuGet Packages

| Package | Version | Purpose |
|---------|---------|---------|
| Azure.AI.OpenAI | 1.0.0-beta.17 | Azure OpenAI integration |
| Newtonsoft.Json | 13.0.3 | JSON serialization and parsing |
| OllamaSharp | 3.0.4 | Local Ollama model integration |
| SQLite | 3.13.0 | Database functionality |
| sqlite-net-pcl | 1.9.172 | SQLite .NET integration |
| Microsoft.Extensions.Hosting | 8.0.0 | Worker service hosting |
| System.Text.Json | - | JSON serialization for API requests |

### Project References

- **BotCommLayer**: Communication infrastructure for service coordination

## Configuration

### AI Provider Configuration

The service supports multiple AI providers that can be configured through dependency injection:

```csharp
// Azure OpenAI
services.AddSingleton<IPromptRunner>(provider =>
    PromptRunnerFactory.GetAzureOpenAiPromptRunner(azureUri, apiKey));

// Local Ollama
services.AddSingleton<IPromptRunner>(provider =>
    PromptRunnerFactory.GetOllamaPromptRunner(ollamaUri, "llama3"));
```

Configure via `appsettings.json`:

```json
{
  "PromptHandling": {
    "Provider": "AzureAI",
    "AzureAI": {
      "Endpoint": "https://your-endpoint.openai.azure.com/",
      "ApiKey": "your-api-key",
      "DeploymentName": "gpt-4"
    },
    "OpenAI": {
      "ApiKey": "sk-your-key",
      "Model": "gpt-4"
    },
    "Ollama": {
      "BaseUrl": "http://localhost:11434/",
      "Model": "llama2"
    },
    "Cache": {
      "Enabled": true,
      "MaxEntries": 1000,
      "ExpirationMinutes": 60
    }
  }
}
```

### Worker Service Integration

The service integrates with the StateManager as a hosted service:

```csharp
services.AddHostedService<PromptHandlingServiceWorker>();
```

## Usage Examples

### Basic Chat Completion

```csharp
// Create runner
var runner = new AzureAIPromptRunner(endpoint, apiKey);

// Build chat history
var messages = new List<KeyValuePair<string, string?>>
{
    KeyValuePair.Create("system", "You are a WoW combat advisor."),
    KeyValuePair.Create("user", "I'm a level 30 warrior with low health, facing 2 murlocs. What should I do?")
};

// Get response
var response = await runner.RunChatAsync(messages, cancellationToken);
Console.WriteLine(response);
// Output: "Use Intimidating Shout to fear the murlocs, then bandage or use a health potion..."
```

### Using Prompt Functions

```csharp
// Create a prompt function
var intentParser = new IntentionParserFunction(runner);

// Parse user intent
var intent = await intentParser.ParseAsync("Go kill wolves in Elwynn Forest");
// Returns structured intent object

// Use with decision engine
var gmCommand = new GMCommandConstructionFunction(runner);
var command = await gmCommand.GenerateAsync("teleport me to Stormwind");
// Returns: ".tele Stormwind"
```

### Using the Intent Parser

```csharp
var ollamaRunner = new OllamaPromptRunner(uri, "llama3");
var request = new IntentionParserFunction.UserRequest
{
    Request = "Can you teleport me to Orgrimmar?"
};

var result = await IntentionParserFunction.ParsePromptIntent(
    ollamaRunner, request, cancellationToken);
// Result: "Send to DataQueryRunner: Fetch teleport command for Orgrimmar"
```

### Character Skill Prioritization

```csharp
var description = new CharacterSkillPrioritizationFunction.CharacterDescription
{
    ClassName = "Warrior",
    Race = "Human",
    Level = 25,
    Skills = ["Swords", "Defense", "Mining", "Blacksmithing"]
};

var prioritizedSkill = await CharacterSkillPrioritizationFunction
    .GetPrioritizedCharacterSkill(promptRunner, description, cancellationToken);
// Result: "Defense" (based on AI analysis)
```

### With Caching

```csharp
var cache = new PromptCache();

// Check cache first
if (!cache.TryGetCachedResponse(prompt, out var response))
{
    response = await runner.RunChatAsync(messages, cancellationToken);
    cache.CacheResponse(prompt, response);
}
```

## Advanced Features

### Chat History Management

Prompt functions maintain conversation context and can transfer history between functions:

```csharp
public void TransferHistory(IPromptFunction transferTarget)
{
    // Transfer conversation context to another function
    sourceFunction.TransferHistory(targetFunction);
}
```

### Parameter Management

Type-safe parameter handling with compile-time names:

```csharp
// Set parameters
SetParameter<string>(value: "MyValue"); // Uses caller member name
SetParameter<int>("CustomName", 42);

// Get parameters
var value = GetParameter<string>(); // Uses caller member name
var custom = GetParameter<int>("CustomName");
```

### Chat Persistence

Save conversations for debugging and analysis:

```csharp
await promptFunction.SaveChat(
    directoryPath: @"C:\ChatLogs",
    filePath: "conversation.txt",
    cancellationToken);
// Saves both .txt and .json versions
```

## Provider Comparison

| Provider | Latency | Cost | Offline | Best For |
|----------|---------|------|---------|----------|
| Azure AI | Low | $$$ | No | Production, enterprise |
| OpenAI | Low | $$$ | No | General use |
| Ollama | Medium | Free | Yes | Development, privacy |
| Fake | None | Free | Yes | Testing |

## Integration with WWoW Ecosystem

### Service Communication

The PromptHandlingService integrates with other WWoW services through:
- **StateManager**: Central coordination and service orchestration
- **BotCommLayer**: Protocol Buffers messaging for distributed communication
- **BackgroundBotRunner**: AI-driven bot decision making

### Use Cases in Bot Automation

- **Natural Language Commands**: Process user requests in natural language
- **Dynamic Strategy Adaptation**: Analyze game state and adapt strategies
- **Configuration Management**: Generate and modify server configurations
- **Decision Support**: Provide intelligent recommendations for bot actions

## Performance Considerations

### Concurrency Limits

Different providers have varying concurrency capabilities:
- **Azure OpenAI**: 50 concurrent requests
- **OpenAI**: 50 concurrent requests
- **Ollama**: 1 concurrent request (local processing)
- **Fake Provider**: Unlimited (testing only)

### Memory Management

- Automatic disposal of AI clients
- Efficient chat history management
- Configurable caching strategies

## Error Handling

The service implements retry logic for transient failures and provides robust error handling:

```csharp
try
{
    var result = await promptRunner.RunChatAsync(chatHistory, cancellationToken);
    return result;
}
catch (OllamaException ex)
{
    return $"Ollama API Error: {ex.Message}";
}
catch (Exception ex)
{
    return $"Unexpected error: {ex.Message}";
}
```

Automatic retry up to 10 times:

```csharp
do
{
    try
    {
        var response = await client.PostAsync(...);
        if (response.IsSuccessStatusCode)
            return result;
    }
    catch (Exception e)
    {
        tryCount++;
    }
} while (tryCount < 10);
```

## Security Considerations

### API Key Management

- Use secure configuration providers
- Avoid hardcoding credentials
- Implement proper key rotation

### Local AI Processing

- Ollama provider keeps data local
- No external API calls for sensitive data
- Full control over model execution

## Troubleshooting

### Common Issues

**Ollama Connection Failures**:
- Verify Ollama service is running
- Check URI configuration
- Ensure model is downloaded locally

**Azure OpenAI Rate Limits**:
- Implement exponential backoff
- Monitor quota usage
- Consider request batching

**Memory Issues with Large Conversations**:
- Implement chat history pruning
- Use streaming responses when available
- Monitor memory usage patterns

## Extending the Service

### Adding a New Provider

1. Create a class implementing `IPromptRunner`:

```csharp
public class MyCustomRunner : IPromptRunner
{
    public async Task<string?> RunChatAsync(
        IEnumerable<KeyValuePair<string, string?>> chatHistory,
        CancellationToken cancellationToken)
    {
        // Your implementation
    }

    public int MaxConcurrent => 10;

    public void Dispose() { }
}
```

2. Register in `PromptRunnerFactory`
3. Add configuration options

### Adding a New Prompt Function

Create a class extending `PromptFunctionBase`:

```csharp
public class MyCustomFunction : PromptFunctionBase
{
    public MyCustomFunction(IPromptRunner promptRunner) : base(promptRunner) { }

    protected override string SystemPrompt =>
        "You are a helpful World of Warcraft assistant...";

    public override async Task CompleteAsync(CancellationToken cancellationToken)
    {
        var response = await RunChatAsync("Your prompt here", cancellationToken);
        // Process response...
    }
}
```

Or extend for specific use cases:

```csharp
public class MyPromptFunction : PromptFunctionBase
{
    public override string SystemPrompt =>
        "You are an expert at...";

    public async Task<MyResult> ExecuteAsync(string input)
    {
        var response = await RunAsync(input);
        return ParseResponse(response);
    }
}
```

## Testing

### Unit Test Example

```csharp
[Test]
public async Task ParsePromptIntent_GameMechanics_ReturnsCorrectHandler()
{
    // Arrange
    var ollamaRunner = new OllamaPromptRunner(_ollamaUri, "llama3");
    var request = new IntentionParserFunction.UserRequest
    {
        Request = "How does threat work in World of Warcraft?"
    };

    // Act
    var result = await IntentionParserFunction.ParsePromptIntent(
        ollamaRunner, request, CancellationToken.None);

    // Assert
    Assert.Equal("Send to MechanicsExplainerRunner: Explain how threat works", result);
}
```

## Related Documentation

- See [StateManager README](../StateManager/README.md) for service orchestration and coordination
- See [BotCommLayer README](../../Exports/BotCommLayer/README.md) for inter-service communication
- See [BackgroundBotRunner README](../BackgroundBotRunner/README.md) for AI-driven bot automation
- See [DecisionEngineService README](../DecisionEngineService/README.md) for strategic decision making
- See `ARCHITECTURE.md` for system overview

---

*This component is part of the WWoW (Westworld of Warcraft) simulation platform.*
