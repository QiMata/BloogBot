# C++ Code Intelligence MCP Server

This MCP (Model Context Protocol) server provides semantic analysis and intelligence for C++ code in the BloogBot project. It helps AI assistants understand the C++ codebase structure, analyze code semantically, and provide intelligent insights for better automated code generation and analysis.

## Features

- **File Analysis**: Deep semantic analysis of C++ files including functions, classes, variables, structs, enums, and macros
- **Symbol Search**: Search for C++ symbols across the entire codebase with relevance scoring
- **Code Explanation**: Intelligent explanation of C++ code sections with context and purpose
- **Dependency Analysis**: Track include dependencies and file relationships
- **Reference Finding**: Find all references to symbols across the codebase
- **Project Structure**: Understand the overall structure of C++ projects
- **Class Hierarchy**: Analyze inheritance relationships and virtual functions
- **Include Analysis**: Detect include issues, circular dependencies, and unused includes
- **Compilation Error Detection**: Basic syntax and include error detection

## Transport Protocol

This server uses **stdio transport** (standard input/output) for communication with MCP clients, following the JSON-RPC 2.0 protocol. It runs as a console application that reads JSON-RPC messages from stdin and writes responses to stdout.

## Available Tools

### 1. `analyze_cpp_file`
Analyzes a C++ file and extracts comprehensive semantic information.

**Parameters:**
- `filePath` (string, required): Path to the C++ file to analyze

**Returns:** Complete file analysis including functions, classes, variables, etc.

### 2. `get_project_structure`
Gets the structure of all C++ projects in the workspace.

**Returns:** Project structure with file categorization and build system detection

### 3. `search_cpp_symbols`
Searches for C++ symbols across the codebase.

**Parameters:**
- `query` (string, required): Search query for symbol names
- `symbolType` (string, optional): Type of symbol (function, class, variable, struct, enum, macro, all)

**Returns:** List of matching symbols with relevance scoring

### 4. `get_file_dependencies`
Gets include dependencies for a C++ file.

**Parameters:**
- `filePath` (string, required): Path to the C++ file

**Returns:** Direct and indirect dependencies, external libraries

### 5. `explain_cpp_code`
Explains a section of C++ code with semantic analysis.

**Parameters:**
- `filePath` (string, required): Path to the C++ file
- `startLine` (integer, optional): Starting line number
- `endLine` (integer, optional): Ending line number

**Returns:** Code explanation with key concepts and purpose

### 6. `find_symbol_references`
Finds all references to a C++ symbol.

**Parameters:**
- `symbol` (string, required): Symbol name to find references for

**Returns:** All references with context and reference type

### 7. `get_function_signature`
Gets detailed information about a C++ function.

**Parameters:**
- `functionName` (string, required): Name of the function

**Returns:** Function signature with parameters and return type

### 8. `get_class_hierarchy`
Gets inheritance hierarchy for a C++ class.

**Parameters:**
- `className` (string, required): Name of the class

**Returns:** Base classes, derived classes, and virtual functions

### 9. `analyze_includes`
Analyzes include dependencies and detects issues.

**Parameters:**
- `filePath` (string, required): Path to the C++ file

**Returns:** Include analysis with resolved paths and issue detection

### 10. `get_compilation_errors`
Gets compilation errors for C++ files.

**Parameters:**
- `filePath` (string, optional): Path to specific file, or all files if not provided

**Returns:** List of compilation errors with suggestions

## Configuration

The server is configured through `appsettings.json`:

```json
{
  "CppCodeIntelligence": {
    "WorkspaceRoot": "../../..",
    "CppProjects": [
      "Exports/FastCall",
      "Exports/Navigation", 
      "Exports/Loader"
    ],
    "IncludeDirectories": [
      "Exports/FastCall",
      "Exports/Navigation",
      "Exports/Loader"
    ],
    "FileExtensions": [".cpp", ".h", ".hpp", ".c", ".cc", ".cxx"]
  }
}
```

## Running the Server

### Console Application
The server runs as a .NET 8 console application and communicates via stdio:

```bash
cd Services/CppCodeIntelligenceMCP
dotnet run
```

### MCP Configuration
Add to your `.mcp.json` file:

```json
{
  "servers": {
    "cpp-intelligence": {
      "command": "dotnet",
      "args": ["run", "--project", "Services/CppCodeIntelligenceMCP"],
      "env": {
        "DOTNET_ENVIRONMENT": "Development"
      }
    }
  }
}
```

## Architecture

The service consists of several components:

- **CppCodeIntelligenceMCPService**: Main MCP protocol handler using JSON-RPC 2.0 over stdio
- **CppAnalysisService**: Core C++ code parsing and analysis engine
- **CppSemanticAnalyzer**: Semantic understanding and intelligent analysis
- **CppProjectStructureService**: Project structure analysis and build system detection

## Key Design Changes

This server was designed to work with **stdio transport** instead of HTTP/SSE transport like other MCP servers in the project. This means:

1. **Console Application**: Runs as a console app, not a web service
2. **Stdin/Stdout Communication**: Reads JSON-RPC messages from stdin, writes responses to stdout
3. **No Background Service**: Executes directly without the .NET hosting framework
4. **Simplified Dependencies**: Minimal package dependencies for lighter footprint

## Supported C++ Features

- Function declarations and definitions
- Class declarations with inheritance
- Variable declarations (local and global)
- Struct and enum definitions
- Macro definitions
- Include directives
- Template classes and functions (basic support)
- Virtual functions and polymorphism
- Namespace declarations

## Limitations

- Uses regex-based parsing (not a full C++ parser)
- No real-time compilation integration
- Limited template specialization support
- Basic error detection only
- Performance limitations on very large codebases

## Integration with AI Assistants

This MCP server enables AI assistants to:

1. **Understand C++ Code Structure**: Get detailed information about classes, functions, and their relationships
2. **Provide Contextual Code Generation**: Generate code that fits with existing patterns and conventions
3. **Suggest Improvements**: Identify potential issues and suggest better practices
4. **Navigate Complex Codebases**: Find symbols and understand dependencies quickly
5. **Explain Complex Code**: Provide clear explanations of C++ code sections
6. **Assist with Refactoring**: Understand code relationships for safe refactoring

The goal is to make AI assistants much more effective at working with C++ code by providing them with deep semantic understanding of the codebase structure and patterns.

## Troubleshooting

### Server Won't Start from .mcp.json
- Ensure the working directory is correct
- Check that `appsettings.json` exists and is properly configured
- Verify the `dotnet` command is available in PATH
- Check logs written to stderr for initialization errors

### No Response to MCP Messages
- Verify the server is reading from stdin correctly
- Check the JSON-RPC message format
- Ensure logging is configured to write to stderr, not stdout (to avoid interfering with MCP communication)

### File Analysis Errors
- Check that the workspace root path is correct
- Ensure the specified C++ project directories exist
- Verify file permissions for reading source files