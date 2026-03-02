# Tri-Agent Workflow: Copilot + Codex + Claude

## Role Summary

| Agent | Role | Cost | When to use |
|-------|------|------|-------------|
| **Copilot CLI** | Read-only codebase Q&A | Cheap | "What is X?", "Where is Y?", "How does Z work?" |
| **Codex CLI** | Implementation + tests | Cheap | "Change code, run tests, fix failures" |
| **Claude Code** | Orchestrator | Expensive | Planning, reviewing, documenting, deciding next steps |

## When to Use Each

### Copilot (Ask-mode) — `.ai/copilot-ask.ps1`
- Understanding codebase structure
- Finding where something is defined
- Reading docs/config to answer questions
- **Cannot:** write files, run commands, fetch URLs, store memory

### Codex (Implementation) — `.ai/codex-task.ps1`
- Writing/modifying code
- Running builds and tests
- Fixing test failures iteratively
- **Can:** read files, write files, run shell commands

### Claude Code (Orchestrator)
- Planning multi-step work
- Reviewing Codex output and test logs
- Updating documentation and task tracking
- Making architectural decisions
- Deciding when to consult Copilot vs hand off to Codex

## The Loop

```
1. Claude identifies the next decision / unknown
2. Copilot answers codebase questions (read-only)
3. Claude converts answer into a precise task + acceptance criteria
4. Codex implements + runs tests + fixes failures
5. Claude reviews output, updates docs, decides next step
```

## Prompt Templates

### Copilot Ask-mode Template

```
.\.ai\copilot-ask.ps1 -Prompt "Read files: <path1>, <path2>. Answer: <question>. Cite file paths and relevant symbols. Keep it short."
```

Examples:
```powershell
.\.ai\copilot-ask.ps1 -Prompt "Read Exports/Navigation/PhysicsEngine.cpp lines 1900-1930. What conditions trigger JUMP_VELOCITY? Cite line numbers."
.\.ai\copilot-ask.ps1 -Prompt "What test projects exist under Tests/? List project names and their .csproj paths."
.\.ai\copilot-ask.ps1 -Prompt "Based on CLAUDE.md, what is the SOAP endpoint and credentials for MaNGOS?"
```

### Codex Implementation Template

```
.\.ai\codex-task.ps1 -Prompt "Implement: <change>. Constraints: <rules>. Run: <test command(s)>. Fix failures. Return summary + commands run + files changed."
```

Examples:
```powershell
.\.ai\codex-task.ps1 -Prompt "Implement: Add a null check in PhysicsEngine.cpp StepV2 before accessing nearbyObjects. Constraints: Do not change function signatures. Run: dotnet test Tests/Navigation.Physics.Tests/Navigation.Physics.Tests.csproj --configuration Release --settings Tests/Navigation.Physics.Tests/test.runsettings. Fix failures. Return summary."
.\.ai\codex-task.ps1 -Prompt "Implement: Remove all EnableGmModeAsync() calls from Tests/BotRunner.Tests/LiveValidation/BasicLoopTests.cs. Run: dotnet build Tests/BotRunner.Tests/BotRunner.Tests.csproj. Fix build errors. Return files changed."
```

## Cost Discipline Rules

1. Default to **Copilot** for "what/where/how" questions about the codebase
2. Use **Codex** for "change code + run tests + fix failures"
3. Use **Claude** tokens mainly for: planning, reviewing, documenting
4. Keep all prompts **short** — reference file paths, don't paste large blocks
5. Don't use Claude to do work that Copilot or Codex can do cheaper
