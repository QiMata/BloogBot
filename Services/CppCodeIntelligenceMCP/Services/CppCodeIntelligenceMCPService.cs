using CppCodeIntelligenceMCP.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace CppCodeIntelligenceMCP.Services;

public class CppCodeIntelligenceMCPService
{
    private readonly ILogger<CppCodeIntelligenceMCPService> _logger;
    private readonly IConfiguration _configuration;
    private readonly CppAnalysisService _analysisService;
    private readonly CppSemanticAnalyzer _semanticAnalyzer;
    private readonly CppProjectStructureService _projectStructureService;
    private bool _servicesInitialized = false;

    public CppCodeIntelligenceMCPService(
        ILogger<CppCodeIntelligenceMCPService> logger,
        IConfiguration configuration,
        CppAnalysisService analysisService,
        CppSemanticAnalyzer semanticAnalyzer,
        CppProjectStructureService projectStructureService)
    {
        _logger = logger;
        _configuration = configuration;
        _analysisService = analysisService;
        _semanticAnalyzer = semanticAnalyzer;
        _projectStructureService = projectStructureService;
    }

    public async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Console.Error.WriteLineAsync("[STDERR] C++ Code Intelligence MCP Server starting with stdio transport");

        try
        {
            // Read from stdin and process MCP messages
            using var stdin = Console.OpenStandardInput();
            using var stdout = Console.OpenStandardOutput();
            using var reader = new StreamReader(stdin);
            using var writer = new StreamWriter(stdout) { AutoFlush = true };

            await Console.Error.WriteLineAsync("[STDERR] MCP stdio loop starting...");

            while (!stoppingToken.IsCancellationRequested)
            {
                var line = await reader.ReadLineAsync();
                if (line == null) 
                {
                    await Console.Error.WriteLineAsync("[STDERR] Received null input, shutting down");
                    break;
                }

                if (string.IsNullOrWhiteSpace(line))
                {
                    continue; // Skip empty lines
                }

                await Console.Error.WriteLineAsync($"[STDERR] Received: {line}");

                try
                {
                    var response = await ProcessMCPMessage(line);
                    if (response != null)
                    {
                        await Console.Error.WriteLineAsync($"[STDERR] Sending: {response}");
                        await writer.WriteLineAsync(response);
                        await writer.FlushAsync();
                    }
                }
                catch (Exception ex)
                {
                    await Console.Error.WriteLineAsync($"[STDERR] Error processing MCP message: {ex.Message}");
                    await Console.Error.WriteLineAsync($"[STDERR] Stack trace: {ex.StackTrace}");
                    
                    // Send error response with proper ID extraction
                    var messageId = ExtractIdFromMessage(line);
                    var errorResponse = JsonSerializer.Serialize(new
                    {
                        jsonrpc = "2.0",
                        id = messageId,
                        error = new
                        {
                            code = -32603,
                            message = "Internal error",
                            data = ex.Message
                        }
                    });
                    await writer.WriteLineAsync(errorResponse);
                    await writer.FlushAsync();
                }
            }
        }
        catch (Exception ex)
        {
            await Console.Error.WriteLineAsync($"[STDERR] Fatal error in C++ Code Intelligence MCP server: {ex.Message}");
            await Console.Error.WriteLineAsync($"[STDERR] Stack trace: {ex.StackTrace}");
            throw;
        }
    }

    private string? ExtractIdFromMessage(string message)
    {
        try
        {
            var jsonNode = JsonNode.Parse(message);
            return jsonNode?["id"]?.ToString();
        }
        catch
        {
            return null;
        }
    }

    private async Task EnsureServicesInitialized()
    {
        if (_servicesInitialized)
            return;

        try
        {
            await Console.Error.WriteLineAsync("[STDERR] Initializing analysis services...");
            await _projectStructureService.InitializeAsync();
            await _analysisService.InitializeAsync();
            _servicesInitialized = true;
            await Console.Error.WriteLineAsync("[STDERR] Services initialized successfully");
        }
        catch (Exception ex)
        {
            await Console.Error.WriteLineAsync($"[STDERR] Failed to initialize services: {ex.Message}");
            throw;
        }
    }

    private async Task<string?> ProcessMCPMessage(string message)
    {
        try
        {
            var jsonNode = JsonNode.Parse(message);
            var method = jsonNode?["method"]?.ToString();
            var id = jsonNode?["id"];

            await Console.Error.WriteLineAsync($"[STDERR] Processing method: {method} with id: {id}");

            return method switch
            {
                "initialize" => HandleInitialize(id),
                "tools/list" => HandleToolsList(id),
                "tools/call" => await HandleToolCall(jsonNode, id),
                _ => null // Ignore unknown methods silently
            };
        }
        catch (Exception ex)
        {
            await Console.Error.WriteLineAsync($"[STDERR] Error parsing MCP message: {ex.Message}");
            return null;
        }
    }

    private string HandleInitialize(JsonNode? id)
    {
        // Don't initialize services here - keep initialization fast
        var response = new
        {
            jsonrpc = "2.0",
            id = id?.ToString(),
            result = new
            {
                protocolVersion = "2024-11-05",
                capabilities = new
                {
                    tools = new { }
                },
                serverInfo = new
                {
                    name = "CppCodeIntelligenceMCP",
                    version = "1.0.0"
                }
            }
        };

        return JsonSerializer.Serialize(response);
    }

    private string HandleToolsList(JsonNode? id)
    {
        var tools = new object[]
        {
            new
            {
                name = "analyze_cpp_file",
                description = "Analyze a C++ file and extract semantic information",
                inputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        filePath = new
                        {
                            type = "string",
                            description = "Path to the C++ file to analyze"
                        }
                    },
                    required = new[] { "filePath" }
                }
            },
            new
            {
                name = "get_project_structure",
                description = "Get the structure of C++ projects in the workspace",
                inputSchema = new
                {
                    type = "object",
                    properties = new { }
                }
            },
            new
            {
                name = "search_cpp_symbols",
                description = "Search for C++ symbols (functions, classes, variables) across the codebase",
                inputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        query = new
                        {
                            type = "string",
                            description = "Search query for symbol names"
                        },
                        symbolType = new
                        {
                            type = "string",
                            description = "Type of symbol to search for (function, class, variable, all)",
                            @enum = new[] { "function", "class", "variable", "struct", "enum", "macro", "all" }
                        }
                    },
                    required = new[] { "query" }
                }
            },
            new
            {
                name = "get_file_dependencies",
                description = "Get the include dependencies for a C++ file",
                inputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        filePath = new
                        {
                            type = "string",
                            description = "Path to the C++ file"
                        }
                    },
                    required = new[] { "filePath" }
                }
            },
            new
            {
                name = "explain_cpp_code",
                description = "Explain a section of C++ code with semantic analysis",
                inputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        filePath = new
                        {
                            type = "string",
                            description = "Path to the C++ file"
                        },
                        startLine = new
                        {
                            type = "integer",
                            description = "Starting line number (optional)"
                        },
                        endLine = new
                        {
                            type = "integer",
                            description = "Ending line number (optional)"
                        }
                    },
                    required = new[] { "filePath" }
                }
            },
            new
            {
                name = "find_symbol_references",
                description = "Find all references to a C++ symbol",
                inputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        symbol = new
                        {
                            type = "string",
                            description = "Symbol name to find references for"
                        }
                    },
                    required = new[] { "symbol" }
                }
            },
            new
            {
                name = "get_function_signature",
                description = "Get the signature and details of a C++ function",
                inputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        functionName = new
                        {
                            type = "string",
                            description = "Name of the function"
                        }
                    },
                    required = new[] { "functionName" }
                }
            },
            new
            {
                name = "get_class_hierarchy",
                description = "Get the inheritance hierarchy for a C++ class",
                inputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        className = new
                        {
                            type = "string",
                            description = "Name of the class"
                        }
                    },
                    required = new[] { "className" }
                }
            },
            new
            {
                name = "analyze_includes",
                description = "Analyze include dependencies and detect issues",
                inputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        filePath = new
                        {
                            type = "string",
                            description = "Path to the C++ file"
                        }
                    },
                    required = new[] { "filePath" }
                }
            },
            new
            {
                name = "get_compilation_errors",
                description = "Get compilation errors for C++ files",
                inputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        filePath = new
                        {
                            type = "string",
                            description = "Path to the C++ file (optional, if not provided returns all errors)"
                        }
                    }
                }
            }
        };

        var response = new
        {
            jsonrpc = "2.0",
            id = id?.ToString(),
            result = new { tools }
        };

        return JsonSerializer.Serialize(response);
    }

    private async Task<string> HandleToolCall(JsonNode? jsonNode, JsonNode? id)
    {
        try
        {
            // Initialize services lazily when first tool is called
            await EnsureServicesInitialized();

            var toolName = jsonNode?["params"]?["name"]?.ToString();
            var arguments = jsonNode?["params"]?["arguments"];

            var result = toolName switch
            {
                "analyze_cpp_file" => await HandleAnalyzeFile(arguments),
                "get_project_structure" => await HandleGetProjectStructure(arguments),
                "search_cpp_symbols" => await HandleSearchSymbols(arguments),
                "get_file_dependencies" => await HandleGetFileDependencies(arguments),
                "explain_cpp_code" => await HandleExplainCode(arguments),
                "find_symbol_references" => await HandleFindReferences(arguments),
                "get_function_signature" => await HandleGetFunctionSignature(arguments),
                "get_class_hierarchy" => await HandleGetClassHierarchy(arguments),
                "analyze_includes" => await HandleAnalyzeIncludes(arguments),
                "get_compilation_errors" => await HandleGetCompilationErrors(arguments),
                _ => new { error = "Unknown tool", tool = toolName }
            };

            var response = new
            {
                jsonrpc = "2.0",
                id = id?.ToString(),
                result = new
                {
                    content = new[]
                    {
                        new
                        {
                            type = "text",
                            text = JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true })
                        }
                    }
                }
            };

            return JsonSerializer.Serialize(response);
        }
        catch (Exception ex)
        {
            await Console.Error.WriteLineAsync($"[STDERR] Error handling tool call: {ex.Message}");
            
            var errorResponse = new
            {
                jsonrpc = "2.0",
                id = id?.ToString(),
                result = new
                {
                    content = new[]
                    {
                        new
                        {
                            type = "text",
                            text = $"Error: {ex.Message}"
                        }
                    }
                }
            };

            return JsonSerializer.Serialize(errorResponse);
        }
    }

    private async Task<object> HandleAnalyzeFile(JsonNode? arguments)
    {
        var filePath = arguments?["filePath"]?.ToString();
        if (string.IsNullOrEmpty(filePath))
        {
            return new { error = "filePath parameter is required" };
        }

        var analysis = await _analysisService.AnalyzeFileAsync(filePath);
        return new { success = true, analysis };
    }

    private async Task<object> HandleGetProjectStructure(JsonNode? arguments)
    {
        var structure = await _projectStructureService.GetProjectStructureAsync();
        return new { success = true, structure };
    }

    private async Task<object> HandleSearchSymbols(JsonNode? arguments)
    {
        var query = arguments?["query"]?.ToString();
        var symbolType = arguments?["symbolType"]?.ToString() ?? "all";

        if (string.IsNullOrEmpty(query))
        {
            return new { error = "query parameter is required" };
        }

        var symbols = await _semanticAnalyzer.SearchSymbolsAsync(query, symbolType);
        return new { success = true, symbols, query, symbolType };
    }

    private async Task<object> HandleGetFileDependencies(JsonNode? arguments)
    {
        var filePath = arguments?["filePath"]?.ToString();
        if (string.IsNullOrEmpty(filePath))
        {
            return new { error = "filePath parameter is required" };
        }

        var dependencies = await _analysisService.GetFileDependenciesAsync(filePath);
        return new { success = true, dependencies };
    }

    private async Task<object> HandleExplainCode(JsonNode? arguments)
    {
        var filePath = arguments?["filePath"]?.ToString();
        var startLine = arguments?["startLine"]?.GetValue<int?>();
        var endLine = arguments?["endLine"]?.GetValue<int?>();

        if (string.IsNullOrEmpty(filePath))
        {
            return new { error = "filePath parameter is required" };
        }

        var explanation = await _semanticAnalyzer.ExplainCodeAsync(filePath, startLine, endLine);
        return new { success = true, explanation };
    }

    private async Task<object> HandleFindReferences(JsonNode? arguments)
    {
        var symbol = arguments?["symbol"]?.ToString();
        if (string.IsNullOrEmpty(symbol))
        {
            return new { error = "symbol parameter is required" };
        }

        var references = await _semanticAnalyzer.FindReferencesAsync(symbol);
        return new { success = true, references };
    }

    private async Task<object> HandleGetFunctionSignature(JsonNode? arguments)
    {
        var functionName = arguments?["functionName"]?.ToString();
        if (string.IsNullOrEmpty(functionName))
        {
            return new { error = "functionName parameter is required" };
        }

        var signature = await _semanticAnalyzer.GetFunctionSignatureAsync(functionName);
        return new { success = true, signature };
    }

    private async Task<object> HandleGetClassHierarchy(JsonNode? arguments)
    {
        var className = arguments?["className"]?.ToString();
        if (string.IsNullOrEmpty(className))
        {
            return new { error = "className parameter is required" };
        }

        var hierarchy = await _semanticAnalyzer.GetClassHierarchyAsync(className);
        return new { success = true, hierarchy };
    }

    private async Task<object> HandleAnalyzeIncludes(JsonNode? arguments)
    {
        var filePath = arguments?["filePath"]?.ToString();
        if (string.IsNullOrEmpty(filePath))
        {
            return new { error = "filePath parameter is required" };
        }

        var includes = await _analysisService.AnalyzeIncludesAsync(filePath);
        return new { success = true, includes };
    }

    private async Task<object> HandleGetCompilationErrors(JsonNode? arguments)
    {
        var filePath = arguments?["filePath"]?.ToString();
        
        var errors = await _analysisService.GetCompilationErrorsAsync(filePath);
        return new { success = true, errors, filePath };
    }
}