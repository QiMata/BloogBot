# PromptHandlingService.Tests

Unit and integration tests for the PromptHandlingService, validating AI prompt processing, response parsing, and action generation.

## Overview

This test project validates:
- **Prompt Construction**: Building context-aware prompts
- **Response Parsing**: Extracting actions from AI responses
- **Action Generation**: Creating valid bot commands
- **Service Integration**: End-to-end prompt handling

## Test Categories

### Prompt Construction Tests

```csharp
public class PromptConstructionTests
{
    [Fact]
    public void BuildPrompt_IncludesGameState()
    {
        // Test game state inclusion in prompts
    }
    
    [Fact]
    public void BuildPrompt_IncludesPlayerInfo()
    {
        // Test player context in prompts
    }
    
    [Fact]
    public void BuildPrompt_IncludesNearbyEntities()
    {
        // Test nearby unit/object context
    }
}
```

### Response Parsing Tests

```csharp
public class ResponseParsingTests
{
    [Fact]
    public void ParseResponse_ValidAction_ReturnsAction()
    {
        // Test valid action parsing
    }
    
    [Fact]
    public void ParseResponse_InvalidFormat_ReturnsError()
    {
        // Test error handling
    }
    
    [Fact]
    public void ParseResponse_MultipleActions_ReturnsAll()
    {
        // Test action sequence parsing
    }
}
```

### Action Generation Tests

```csharp
public class ActionGenerationTests
{
    [Fact]
    public void GenerateAction_Movement_CreatesGotoAction()
    {
        // Test movement action creation
    }
    
    [Fact]
    public void GenerateAction_Combat_CreatesCastAction()
    {
        // Test combat action creation
    }
    
    [Fact]
    public void GenerateAction_Interaction_CreatesInteractAction()
    {
        // Test interaction action creation
    }
}
```

## Running Tests

### Command Line

```bash
# Run all tests
dotnet test Tests/PromptHandlingService.Tests

# Run with verbose output
dotnet test Tests/PromptHandlingService.Tests -v normal

# Run with coverage
dotnet test Tests/PromptHandlingService.Tests --collect:"XPlat Code Coverage"

# Run specific test
dotnet test Tests/PromptHandlingService.Tests --filter "FullyQualifiedName~ResponseParsingTests"
```

### Visual Studio

1. Open Test Explorer (Test ? Test Explorer)
2. Click "Run All" or select specific tests

## Dependencies

| Package | Version | Purpose |
|---------|---------|---------|
| xunit | 2.5.3 | Test framework |
| xunit.runner.visualstudio | 2.5.3 | VS Test integration |
| Microsoft.NET.Test.Sdk | 17.8.0 | Test SDK |
| coverlet.collector | 6.0.0 | Code coverage |

## Project References

- **PromptHandlingService**: System under test
- **DecisionEngineService**: Decision engine integration
- **BotCommLayer**: Message types
- **GameData.Core**: Game data models

## Mocking

For AI service tests, mock the OpenAI/Azure AI client:

```csharp
var mockClient = new Mock<IAIClient>();
mockClient.Setup(c => c.GetCompletionAsync(It.IsAny<string>()))
    .ReturnsAsync("ACTION: GOTO 1234 5678 90");
```

## Related Documentation

- See `Services/PromptHandlingService/README.md` for service details
- See `Services/DecisionEngineService/README.md` for decision engine
