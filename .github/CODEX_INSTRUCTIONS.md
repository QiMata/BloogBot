# BloogBot Codex Instructions

## Mission Snapshot
- BloogBot (WestworldOfWarcraft) automates legacy World of Warcraft clients (1.12.1, 2.4.3, 3.3.5a).
- Goal: human style characters driven by MEF bot profiles, distributed background services, and process injected foreground runners.
- Stack: C#/.NET 8 workers, WPF UI, Blazor dashboards, native C++ loader components, Semantic Kernel activities, and custom TCP protocols.

## Repository Landmarks
- Exports/: native and C# bridge for process injection, object manager access, and WoWSharp client interop.
- Services/: .NET worker services (StateManager, PathfindingService, etc.) that coordinate via framed TCP messages.
- BotProfiles/: MEF exported implementations of IBot; each profile owns combat, rest, travel, and utility task factories.
- BloogBot.AI/: Semantic Kernel orchestration for intent based activity selection.
- UI/: WPF management client and related ViewModels; Blazor dashboards live here too.
- Tests/: xUnit unit and integration coverage for pathfinding, networking, and behaviour validation.

## Architectural Principles
- Preserve the plugin boundary: the runner only depends on IBot contracts. New behaviour belongs in its own profile project.
- Services should stay stateless and idempotent. Introduce new features by extending protocol DTOs rather than sharing globals.
- Foreground runners touch game memory directly; guard each pointer read or write with range checks to avoid client crashes.
- Pathfinding depends on Detour nav meshes (.mmtile). Always confirm tiles exist before queueing navigation jobs.

## Coding Guidelines
- Follow CODING_STANDARDS.md: explicit access modifiers, PascalCase public members, one class per file, prefer async/await, keep UI logic in ViewModels.
- Use dependency injection for worker services; avoid static service locators.
- Bot tasks implement finite state machines returning TaskStatus capsules. Do not block threads inside behaviour loops.
- Use ILogger<T> for diagnostics. Log actionable state (target id, spell, path node) but never offsets or secrets.
- Version protocol messages or add capability negotiation when changing service communications to retain backward compatibility.

## Testing and Validation
- Build the solution with dotnet build WestworldOfWarcraft.sln before committing.
- Run automated coverage through dotnet test Tests/Tests.csproj; add targeted tests for new rotations, services, or nav mesh handling.
- When adjusting native code, confirm generated DLLs still load and that P/Invoke signatures match the managed side; reuse smoke checks in test_bin.
- Manual service testing: run Services/StateManager alongside Services/PathfindingService and verify they bind to documented ports.

## Configuration and Deployment Notes
- Keep client offsets, API keys, and local paths in bootstrapperSettings.json or user secrets. Never hard code sensitive data.
- Services read appsettings.*.json. Provide safe defaults and document overrides for each environment.
- setup.ps1 downloads required game assets. Update it whenever dependencies or resource locations change.
- Support all targeted client builds. Use capability or version checks to isolate client specific logic.

## Common Tasks
- Add a new bot: create an IBot implementation under BotProfiles/<Class>/, export with [Export(typeof(IBot))], wire combat/rest/travel tasks, and extend tests.
- Extend a service: add DTOs for requests and responses, update worker handlers, and create integration tests that cover the new flow.
- UI update: modify ViewModels first, keep code behind minimal, and validate both WPF and Blazor shells if the change spans them.

## Safety Checklist
- Avoid blocking loops or Thread.Sleep inside async services; rely on cooperative cancellation tokens.
- Re confirm memory offsets against the target client build before shipping.
- Treat inbound socket payloads as untrusted; validate lengths, opcodes, and CRCs before applying them.
- Document new ports, long running jobs, or external dependencies in README.md to keep operators informed.

Help Codex stay within these guardrails so generated changes respect BloogBot architecture and operational constraints.
