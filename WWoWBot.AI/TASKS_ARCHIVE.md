# Task Archive

Completed items moved from TASKS.md.

## 2026-02-25

- [x] `AI-CORE-001` Restored `BotActivity` enum at `WWoWBot.AI/States/BotActivity.cs` and cleared missing-symbol build failures.
- [x] `AI-CORE-002` Restored `Trigger` enum at `WWoWBot.AI/StateMachine/Trigger.cs` and re-enabled state-machine trigger compilation.
- [x] `AI-CORE-003` Added `ActivityPluginAttribute` at `WWoWBot.AI/Annotations/ActivityPluginAttribute.cs` for activity-tagged plugin discovery.
- [x] `AI-SEM-001` Reworked `PluginCatalog` to instantiate plugin instances deterministically and avoid invalid `CreateFromObject(Type, ...)` usage.
- [x] `AI-SEM-002` Namespace-aligned `KernelCoordinator` to `BloogBot.AI.Semantic` and synced `WWoWBot.AI/README.md` usage examples.
- [x] `AI-SEC-001` Upgraded `Microsoft.SemanticKernel.Core` to `1.72.0` and `Microsoft.Extensions.Logging.Abstractions` to `10.0.3`; build now has no `NU1904`.
- [x] `AI-TST-001` Added `Tests/WWoWBot.AI.Tests` with focused coverage for state-machine trigger transitions, forbidden transition enforcement, and activity-attribute plugin mapping; `dotnet test` now passes (`4` tests).
