# PromptHandlingService — LLM Dialog Automation

Lightweight service for LLM-based dialog/prompt automation. Supports 4 provider backends. ~315 LOC total.

## Key Files

| File | Purpose |
|------|---------|
| `PromptHandlingServiceWorker.cs` | BackgroundService with PromptCache |
| `IPromptRunner.cs` | Provider interface: `RunPromptAsync(prompt)` |
| `IPromptFunction.cs` | Function interface for structured prompt operations |
| `PromptFunctionBase.cs` | Base class for prompt functions |
| `PromptRunnerFactory.cs` | Factory: selects provider based on config |
| `ServiceCollectionExtensions.cs` | DI registration |
| `Cache/PromptCache.cs` | SQLite-backed prompt/response cache |
| `Utilities/StringParserUtilities.cs` | Response parsing helpers |

## Providers

| Provider | File | Notes |
|----------|------|-------|
| Azure AI | `Providers/AzureAIPromptRunner.cs` | Azure OpenAI endpoint |
| OpenAI | `Providers/OpenAIPromptRunner.cs` | Direct OpenAI API |
| Ollama | `Providers/OllamaPromptRunner.cs` | Local Ollama instance |
| Fake | `Providers/FakePromptRunner.cs` | Testing stub — returns canned responses |

## Architecture

```
PromptRunnerFactory (config-driven provider selection)
  → IPromptRunner.RunPromptAsync(prompt)
    → PromptCache (SQLite, avoids redundant LLM calls)
      → Provider-specific HTTP call
```

## Dependencies

- Microsoft.Extensions.Hosting, System.Data.SQLite
- No dependency on GameData.Core or BotCommLayer

## Testing

- `Tests/PromptHandlingService.Tests/` — uses FakePromptRunner for deterministic tests
