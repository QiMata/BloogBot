
# Copilot Integration Style Guide

## Current Task: StateUpdateIntegrationTest
*Create a comprehensive integration test that validates the interaction between various components of the WoW bot project, ensuring that state updates are handled correctly across the system. It is important to not simulate or mock components in this test, as the focus is on real-world interactions and behaviors. This is an end to end integration test that should utilize the FrontEndBotRunner injected inside of the WOW.exe using the Loader.dll. Creation of this test is not finished until the test passes. Continously build, run, and debug the test and all involved components until it passes. The test should be ran in a seperate .net window to allow for the copilot to asynchrinously track it and prevent bugs within visual studio. The Loader issue is the main component to get this project up and running and should be prioritized. The injected bot should act inside the wow process just as a user would, interacting with the UI when needed and bypassing it when possible. Refer to the memory at 88b52498-c116-4608-bb87-72628725859b for suggestions of how to implement the bot's interactions within the wow process.*


## 1. AI Documentation Rules

* **Check before asking** ? search existing docs first.
* **Location** ? all docs live in `docs/` folder, use Markdown format.
* **Update docs when**:

  * A new build/run command is added.
  * A new behavior script class is introduced (QuestAI, CombatAI, etc).
  * A dependency is added to C++ or C# projects.
  * A database schema migration is created.
* **Never overwrite** existing docs ? append with *date + version*.
* **Never overwrite** this document.

---

## 2. Primary Role of Copilot

Copilot acts as a code-writing and debugging assistant for the **WoW bot project**, focusing on:

* Code automation (C++/C# updates)
* Database-aware logic (read-only queries)
* Documentation upkeep (clarity and traceability)
* Debugging with GM commands

---

## 3. Database Usage Rules

* **Allowed**: read-only queries

  * Example:

    ```sql
    SELECT * FROM creature_template WHERE entry=1234;
    DESCRIBE characters;
    ```
* **Not allowed**: `INSERT`, `UPDATE`, `DELETE`, schema changes.
* Always use `mangos_readonly` account.

---

## 4. GM Command Usage

* GM commands = **debugging/testing only**
  Examples:

  * Spawning NPCs/objects
  * Teleporting for movement debugging
  * Resetting environments
* **Rules**:

  * Never in production bot logic
  * Must be documented in `docs/debugging.md`
  * Wrap in `#ifdef DEBUG` blocks

---

## 5. Code Modification Guidelines

* Modify code when:

  * Adding/refining bot behaviors
  * Integrating DB lookups into logic
  * Fixing C++/C# bugs
* Respect project split:

  * **C++** ? core bot behavior
  * **C#** ? scripting/logic layer

---

## 6. Documentation Rules (Extended)

* Update docs when:

  * Adding/changing public-facing functions/classes
  * Modifying DB-dependent logic
  * Creating new subsystems (AI handler, combat, movement, etc)
* Format:

  * Markdown in `docs/`
  * Inline docstrings in C++/C#
  * Debug/GM usage ? `docs/debugging.md`

---

## 7. Code & Query Safety

* Verify schema with `DESCRIBE` before using columns.
* Handle nulls/empty results gracefully.
* Never hardcode credentials ? use config/env.

---

## 8. Coding Standards

* Follow repo conventions.
* Use **C++17** for core bot code.
* Use standard **C# conventions** (PascalCase, camelCase).
* Build must succeed (`cmake && make`) before commit.

---

## 9. Decision Flow

1. Need schema/data? ? Query DB (read-only).
2. Need debugging in world? ? Use GM commands (debug-only).
3. Need logic change? ? Modify C++/C# code.
4. Changed systems/logic? ? Update docs.
5. Unclear? ? Suggest instead of executing.

---

# MCP Servers & Tools

MCP server tools should be used before actioning.
The Logging MCP server is the primary entry point for structured logging and telemetry. Tasks should be tracked by logging with this server. Log first, then act. Read logs, then act.
The C++ Intelligence MCP server provides semantic analysis of C++ codebases, useful for understanding and navigating complex projects. If there is c++ code, project or cpp file within context, analyze it with this server, then act.

## A. Logging MCP Server

**URL:** `http://localhost:5001`
**Purpose:** Structured logging, telemetry, monitoring.
**Integration:** VS Code MCP extension, Swagger UI at `/`.

### Features

* Full **HTTP-based MCP 2024-11-05** protocol
* Structured logging, telemetry collection, real-time monitoring
* Swagger UI for interactive API
* VS Code integration

### API Endpoints

| Category     | Endpoint                     | Description      |
| ------------ | ---------------------------- | ---------------- |
| Health       | `GET /health`                | Server status    |
| MCP Protocol | `POST /api/mcp/initialize`   | Init session     |
|              | `GET /api/mcp/tools`         | List tools       |
|              | `POST /api/mcp/tools/call`   | Execute tool     |
| Logs         | `POST /api/logs/event`       | Create log event |
|              | `GET /api/logs`              | Recent logs      |
|              | `GET /api/logs/query`        | Query logs       |
|              | `GET /api/logs/stats`        | Log stats        |
| Telemetry    | `GET /api/telemetry/metrics` | System metrics   |
|              | `POST /api/telemetry/events` | Record event     |

### MCP Tools

* **`log_event`** ? create log
* **`get_logs`** ? fetch logs

(Examples are unchanged from your original file.)

---

## B. C++ Intelligence MCP Server

**Transport:** stdio (JSON-RPC 2.0)
**Purpose:** Semantic analysis of C++ projects.

### Key Features

* File & symbol analysis
* Dependency & reference tracking
* Code explanation & error detection

### Tools

1. `analyze_cpp_file` ? extract semantic info
2. `get_project_structure` ? project overview
3. `search_cpp_symbols` ? find symbols
4. `get_file_dependencies` ? include/dependency analysis
5. `explain_cpp_code` ? explain code snippet
6. `find_symbol_references` ? symbol references
7. `get_function_signature` ? detailed function info
8. `get_class_hierarchy` ? inheritance analysis
9. `analyze_includes` ? include issues
10. `get_compilation_errors` ? list syntax/build errors

---

## C. Memorizer MCP

**Purpose:** Long-term memory system.

### Tools

* `store` ? store new memory
* `search` ? similarity search
* `get` / `getMany` ? retrieve memory
* `delete` ? remove memory
* `createRelationship` ? link memories

---

## D. Microsoft Docs MCP

**Purpose:** Authoritative Microsoft Docs integration.

### Tools

* `search` ? find docs
* `get` ? retrieve doc
* `getMany` ? retrieve multiple

---

# Quick Start

### 1. Run Logging MCP

```bash
cd Services/LoggingMCPServer
dotnet build
dotnet run
```

### 2. Access Swagger

Open: [http://localhost:5001](http://localhost:5001)

### 3. Test MCP Tools

```bash
# List tools
curl -X GET "http://localhost:5001/api/mcp/tools"

# Create log event
curl -X POST "http://localhost:5001/api/mcp/tools/call" \
  -H "Content-Type: application/json" \
  -d '{"name": "log_event", "arguments": {"message": "Test", "level": "info"}}'
```

---
