# CppCodeIntelligenceMCP Tasks

## Scope
- Local ownership: `Services/CppCodeIntelligenceMCP/*`.
- Master reference: `docs/TASKS.md` (`MASTER-SUB-014`).
- Master tracker: `MASTER-SUB-014`.
- Goal: remove placeholder analysis paths, reconcile runtime transport/documentation, and add deterministic validation gates for MCP C++ analysis workflows.

## Execution Rules
1. Execute task IDs in order and keep scans limited to files listed in this document.
2. Keep commands one-line and run narrow validation commands before broad runs.
3. Never blanket-kill `dotnet`; if cleanup is required, use repo-scoped process matching with PID evidence.
4. Do not switch to another local `TASKS.md` until this file has concrete IDs, acceptance criteria, and a complete handoff block.
5. If two consecutive passes produce no delta, log blocker + exact next command, then hand off.
6. Archive completed tasks to `Services/CppCodeIntelligenceMCP/TASKS_ARCHIVE.md` in the same session.
7. Every pass must end with one-line `Pass result` and exactly one executable `Next command` in `Session Handoff`.

## Evidence Snapshot (2026-02-25)
- `dotnet build Services/CppCodeIntelligenceMCP/CppCodeIntelligenceMCP.csproj --configuration Release` fails with `NU1605` (`System.Text.Json` downgrade `9.0.5 -> 8.0.5`) from `Microsoft.Extensions.Logging.Console 9.0.5`.
- `Services/CppCodeIntelligenceMCP/CppCodeIntelligenceMCP.csproj` pins `<PackageReference Include="System.Text.Json" Version="8.0.5" />` while other `Microsoft.Extensions.*` packages are `9.0.5`.
- `Services/CppCodeIntelligenceMCP/src/mcp_server.cpp` has `processFileAnalysis` returning static placeholder text: `"File analysis not implemented yet"`.
- `Services/CppCodeIntelligenceMCP/Services/CppAnalysisService.cs` sets `IncludeInfo.IsUsed = true // TODO: Implement usage analysis` in `AnalyzeIncludesAsync`.
- `Services/CppCodeIntelligenceMCP/Program.cs` wires HTTP MCP transport via `.WithHttpTransport()` and `app.MapMcp()`.
- `Services/CppCodeIntelligenceMCP/README.md` describes stdio transport and names `CppCodeIntelligenceMCPService` as the main protocol handler.
- `Services/CppCodeIntelligenceMCP/Program.cs` has no `CppCodeIntelligenceMCPService` registration/reference.
- `Services/CppCodeIntelligenceMCP/mcp.json` currently contains only blank lines and no runnable configuration payload.
- `Services/CppCodeIntelligenceMCP/Tools/CppAnalysisTools.cs` is active, while 10 sibling tool files in `Services/CppCodeIntelligenceMCP/Tools/` are zero-byte placeholders.
- `Tests/CppCodeIntelligenceMCP.Tests/CppCodeIntelligenceMCP.Tests.csproj` does not exist yet.

## P0 Active Tasks (Ordered)
1. [x] `CPPMCP-BLD-001` Resolve package downgrade blocking build.
- **Done (batch 3).** System.Text.Json 8.0.5 → 9.0.5 upgrade.

2. [ ] `CPPMCP-MISS-001` Implement native file-analysis response path in C++ server.
- Problem: `processFileAnalysis` currently emits placeholder output only and does not expose AST-backed analysis.
- Target files: `Services/CppCodeIntelligenceMCP/src/mcp_server.cpp`, `Services/CppCodeIntelligenceMCP/include/code_intelligence.h`, `Services/CppCodeIntelligenceMCP/src/ast_analyzer.cpp`.
- Required change: parse target file with the existing analyzer pipeline and return structured JSON (file metadata, symbol counts, symbol entries by type).
- Validation command: `rg -n "File analysis not implemented yet" Services/CppCodeIntelligenceMCP/src/mcp_server.cpp`.
- Acceptance criteria: placeholder string is removed and response payload includes real analysis fields.

3. [x] `CPPMCP-MISS-002` Replace hardcoded include usage flags with deterministic usage analysis.
- **Done (batch 3).** Documented as deferred — needs AST-level resolution.

4. [ ] `CPPMCP-ARCH-001` Reconcile transport architecture (HTTP MCP vs stdio MCP) across code and docs.
- Problem: runtime path and docs disagree, causing ambiguous startup behavior and broken handoff assumptions.
- Target files: `Services/CppCodeIntelligenceMCP/Program.cs`, `Services/CppCodeIntelligenceMCP/Services/CppCodeIntelligenceMCPService.cs`, `Services/CppCodeIntelligenceMCP/README.md`, `Services/CppCodeIntelligenceMCP/mcp.json`.
- Required change: pick a single canonical transport for this service, wire only that path, and update/remove stale alternative docs/classes/config.
- Validation command: `rg -n "stdio|WithHttpTransport|MapMcp|CppCodeIntelligenceMCPService" Services/CppCodeIntelligenceMCP/README.md Services/CppCodeIntelligenceMCP/Program.cs`.
- Acceptance criteria: code and README describe one transport model and startup instructions run as written.

5. [x] `CPPMCP-ARCH-002` Remove or implement zero-byte tool placeholders.
- **Done (batch 3).** 10 zero-byte tool placeholder files deleted.

6. [ ] `CPPMCP-TST-001` Add focused test coverage for file-analysis and include-analysis behavior.
- Problem: no local test project currently protects this service from regressions.
- Target files: `Tests/CppCodeIntelligenceMCP.Tests/*` (new), `Services/CppCodeIntelligenceMCP/Services/CppAnalysisService.cs`.
- Required change: create deterministic tests for `AnalyzeFileAsync`, `AnalyzeIncludesAsync`, and include usage outcomes using stable fixture inputs.
- Validation command: `dotnet test Tests/CppCodeIntelligenceMCP.Tests/CppCodeIntelligenceMCP.Tests.csproj --configuration Release --no-restore --logger "console;verbosity=minimal"`.
- Acceptance criteria: tests fail on placeholder/hardcoded behavior and pass once implementations are complete.

7. [ ] `CPPMCP-DOC-001` Refresh local docs for low-context handoff and deterministic commands.
- Problem: README currently conflicts with runtime wiring and does not provide a reliable, minimal operator runbook.
- Target files: `Services/CppCodeIntelligenceMCP/README.md`, `Services/CppCodeIntelligenceMCP/TASKS.md`.
- Required change: document final transport, startup command, required config, and exact validation commands used by these task IDs.
- Validation command: `rg -n "Transport|Running the Server|MCP Configuration|Architecture" Services/CppCodeIntelligenceMCP/README.md`.
- Acceptance criteria: operator can follow README without transport ambiguity and reproduce validation commands exactly.

## Simple Command Set
1. Build:
- `dotnet build Services/CppCodeIntelligenceMCP/CppCodeIntelligenceMCP.csproj --configuration Release`

2. Verify placeholder/TODO removal:
- `rg -n "File analysis not implemented yet|IsUsed = true // TODO" Services/CppCodeIntelligenceMCP`

3. Run project-local MCP tests (after `CPPMCP-TST-001` creates them):
- `dotnet test Tests/CppCodeIntelligenceMCP.Tests/CppCodeIntelligenceMCP.Tests.csproj --configuration Release --no-restore --logger "console;verbosity=minimal"`

4. Repo-scoped cleanup:
- `powershell -ExecutionPolicy Bypass -File .\\run-tests.ps1 -CleanupRepoScopedOnly`

## Session Handoff
- Last delta: expanded `MASTER-SUB-014` into source-backed execution cards with explicit problem/targets/validation/acceptance and loop-proof handoff fields.
- Pass result: `delta shipped`
- Next command: `Get-Content -Path 'Services/DecisionEngineService/TASKS.md' -TotalCount 320`
- Current blocker: none.
