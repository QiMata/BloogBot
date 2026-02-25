# LoggingMCPServer Tasks

Master tracker: `MASTER-SUB-017`

## Scope
- Directory: `Services/LoggingMCPServer`
- Focus: enforce one canonical MCP host path and deterministic logging/telemetry behavior for FG/BG parity diagnostics.
- Queue dependency: `docs/TASKS.md` controls execution order and handoff pointers.

## Execution Rules
1. Keep this file limited to `Services/LoggingMCPServer` implementation and tests.
2. Prefer one canonical HTTP MCP flow; remove or archive alternate dead paths.
3. Never blanket-kill `dotnet`; use repo-scoped cleanup only.
4. Archive completed items to `Services/LoggingMCPServer/TASKS_ARCHIVE.md` in the same session.
5. If two consecutive passes produce no delta, record blocker + exact next command in `Session Handoff` and advance the master queue pointer.
6. Every pass must write one-line `Pass result` (`delta shipped` or `blocked`) and exactly one executable `Next command`.

## Evidence Snapshot (2026-02-25)
- Build check passes: `dotnet build Services/LoggingMCPServer/LoggingMCPServer.csproj --configuration Release --no-restore` -> `0 Error(s)`, `0 Warning(s)`.
- Canonical HTTP MCP wiring exists in `Program.cs` via `AddMcpServer().WithHttpTransport()` and `app.MapMcp()` (`Program.cs:26-27`, `Program.cs:73`).
- Route overlap is present:
  - `McpBaseController`: `[Route("api/mcp")]` with `[HttpGet]` + `[HttpPost]` (`Controllers/McpBaseController.cs:8`, `:28`, `:29`).
  - `McpController`: `[Route("api/[controller]")]` with initialize/tools/call endpoints (`Controllers/McpController.cs:8`, `:28`, `:61`, `:118`).
- Duplicate model/service classes exist in both service layer and tools:
  - Service-side definitions: `Services/LogEvent.cs:5`, `Services/LogEventProcessor.cs:6`, `Services/TelemetryCollector.cs:6`.
  - Tool-side duplicates: `Tools/LoggingTools.cs:152`, `:164`, `:236`.
- `GetRecentLogs` mutates queue state on read (`TryDequeue` + re-enqueue subset) in `Services/LogEventProcessor.cs:45-59`.
- Zero-byte placeholder files remain and are compile-included by default globbing:
  - `SimpleProgram.cs`
  - `Services/LoggingMCPServiceNew.cs`
  - `Services/LoggingMCPServiceSimple.cs`
  - `Services/SimpleTest.cs`
- `LoggingMCPService` (stdio background-service host) exists but has no instantiation/wiring from `Program.cs` (`Services/LoggingMCPService.cs:8`).

## P0 Active Tasks (Ordered)
1. [ ] `LMCP-MISS-001` Consolidate MCP host entrypoints and remove dead/alternate host paths.
- Problem: project contains HTTP host wiring plus orphan/placeholder host files and a non-wired stdio background host.
- Target files: `Program.cs`, `LoggingMCPServer.csproj`, `SimpleProgram.cs`, `Services/LoggingMCPService.cs`, `Services/LoggingMCPServiceNew.cs`, `Services/LoggingMCPServiceSimple.cs`, `Services/SimpleTest.cs`.
- Required change: keep one canonical host path, remove/archive unused host variants, and ensure csproj reflects only active host sources.
- Validation command: `Get-ChildItem Services/LoggingMCPServer -Filter *.cs -Recurse | Where-Object { $_.Length -eq 0 }`
- Acceptance criteria: no zero-byte `.cs` files remain; canonical host path is explicit in code and docs.

2. [ ] `LMCP-MISS-002` Remove duplicate in-tool model/service implementations and use DI-backed shared services.
- Problem: `Tools/LoggingTools.cs` defines duplicate `LogEvent`, `LogEventProcessor`, and `TelemetryCollector` classes.
- Target files: `Tools/LoggingTools.cs`, `Services/LogEvent.cs`, `Services/LogEventProcessor.cs`, `Services/TelemetryCollector.cs`, `Program.cs`.
- Required change: tools consume shared service-layer types only; delete duplicate implementations from tool file.
- Validation command: `rg -n "^public class (LogEvent|LogEventProcessor|TelemetryCollector)" Services/LoggingMCPServer/Tools/LoggingTools.cs Services/LoggingMCPServer/Services/LogEvent.cs Services/LoggingMCPServer/Services/LogEventProcessor.cs Services/LoggingMCPServer/Services/TelemetryCollector.cs`
- Acceptance criteria: each class exists in one authoritative location only.

3. [ ] `LMCP-MISS-003` Fix `GetRecentLogs` to be non-destructive and deterministic.
- Problem: current read path dequeues and rewrites queue state, changing retention/results as a side effect of querying.
- Target files: `Services/LogEventProcessor.cs`.
- Required change: implement non-destructive snapshot retrieval with explicit retention policy and stable ordering.
- Validation command: `dotnet test Services/LoggingMCPServer/LoggingMCPServer.csproj --configuration Release --no-build --logger "console;verbosity=minimal"`
- Acceptance criteria: repeated reads over unchanged source data return stable ordering and do not drop records outside configured retention.

4. [ ] `LMCP-MISS-004` Unify MCP API contract and route ownership.
- Problem: overlapping MCP endpoints (`MapMcp`, `McpBaseController`, `McpController`) create drift risk.
- Target files: `Program.cs`, `Controllers/McpBaseController.cs`, `Controllers/McpController.cs`, `README.md`.
- Required change: designate one protocol owner, remove duplicate handlers, and align docs/examples to that owner.
- Validation command: `rg -n "\\[Route\\(|\\[Http(Get|Post)|MapMcp" Services/LoggingMCPServer/Controllers/McpBaseController.cs Services/LoggingMCPServer/Controllers/McpController.cs Services/LoggingMCPServer/Program.cs`
- Acceptance criteria: one canonical MCP route surface with consistent tool list/call response schema.

5. [ ] `LMCP-MISS-005` Add regression coverage for log ordering/schema and route parity.
- Problem: no dedicated guard prevents ordering/schema regressions in logging + MCP responses.
- Target files: add tests under `Services/LoggingMCPServer` (or dedicated test project) covering writes, reads, filters, and tool responses.
- Required change: add deterministic tests for ordering, filter correctness, and schema consistency.
- Validation command: `dotnet test Services/LoggingMCPServer/LoggingMCPServer.csproj --configuration Release --no-build --logger "console;verbosity=minimal"`
- Acceptance criteria: regressions fail with actionable test output.

6. [ ] `LMCP-MISS-006` Add run-correlation fields for parity and teardown diagnostics.
- Problem: current log payloads do not guarantee scenario/run/process correlation needed to prove timeout teardown and FG/BG parity behavior.
- Target files: `Services/LogEvent.cs`, `Services/LogEventProcessor.cs`, `Controllers/LogsController.cs`, `Tools/LoggingTools.cs`, `README.md`.
- Required change: capture scenario ID, run ID, process/PID, timeout outcome, and teardown evidence consistently in write/read paths.
- Validation command: `powershell -Command \"Invoke-RestMethod -Method Get -Uri 'http://localhost:5001/api/logs/recent?count=5' | ConvertTo-Json -Depth 6\"`
- Acceptance criteria: correlation fields appear in API/tool outputs for success and timeout/failure paths.

## Simple Command Set
1. Restore: `dotnet restore Services/LoggingMCPServer/LoggingMCPServer.csproj`
2. Build: `dotnet build Services/LoggingMCPServer/LoggingMCPServer.csproj --configuration Release --no-restore`
3. Run service: `dotnet run --project Services/LoggingMCPServer/LoggingMCPServer.csproj --configuration Release`
4. MCP tools smoke: `powershell -Command \"Invoke-RestMethod -Method Get -Uri 'http://localhost:5001/api/mcp/tools' | ConvertTo-Json -Depth 6\"`
5. Repo-scoped cleanup: `powershell -ExecutionPolicy Bypass -File .\\run-tests.ps1 -CleanupRepoScopedOnly`

## Session Handoff
- Last updated: 2026-02-25
- Pass result: `delta shipped`
- Last delta: normalized to execution-card format, refreshed with command-verified MCP route/duplicate/queue-mutation evidence, and tightened acceptance gates.
- Next task: `LMCP-MISS-001`
- Next command: `Get-Content -Path 'Services/PathfindingService/TASKS.md' -TotalCount 320`
- Blockers: none
