using System.ComponentModel;
using CppCodeIntelligenceMCP.Services;
using ModelContextProtocol.Server;

namespace CppCodeIntelligenceMCP.Tools;

[McpServerToolType]
public class CppAnalysisTools
{
    private readonly CppAnalysisService _analysisService;
    private readonly CppSemanticAnalyzer _semanticAnalyzer;
    private readonly CppProjectStructureService _projectStructureService;
    private readonly ILogger<CppAnalysisTools> _logger;

    public CppAnalysisTools(
        CppAnalysisService analysisService,
        CppSemanticAnalyzer semanticAnalyzer,
        CppProjectStructureService projectStructureService,
        ILogger<CppAnalysisTools> logger)
    {
        _analysisService = analysisService;
        _semanticAnalyzer = semanticAnalyzer;
        _projectStructureService = projectStructureService;
        _logger = logger;
    }

    [McpServerTool]
    [Description("Analyze a C++ file and extract semantic information")]
    public async Task<string> AnalyzeCppFile(
        [Description("Path to the C++ file to analyze")] string filePath)
    {
        try
        {
            if (string.IsNullOrEmpty(filePath))
            {
                return "Error: filePath parameter is required";
            }

            _logger.LogInformation("Analyzing C++ file: {FilePath}", filePath);
            var analysis = await _analysisService.AnalyzeFileAsync(filePath);
            
            var result = $"C++ File Analysis for: {filePath}\n\n";
            result += $"Line Count: {analysis.LineCount}\n";
            result += $"Last Modified: {analysis.LastModified}\n\n";
            result += $"Functions: {analysis.Functions.Count}\n";
            result += $"Classes: {analysis.Classes.Count}\n";
            result += $"Variables: {analysis.Variables.Count}\n";
            result += $"Includes: {analysis.Includes.Count}\n";
            result += $"Structs: {analysis.Structs.Count}\n";
            result += $"Enums: {analysis.Enums.Count}\n";
            result += $"Macros: {analysis.Macros.Count}\n\n";

            if (analysis.Functions.Any())
            {
                result += "Functions:\n";
                foreach (var func in analysis.Functions.Take(10))
                {
                    result += $"  - {func.ReturnType} {func.Name}(...) [Line {func.LineNumber}]\n";
                }
                if (analysis.Functions.Count > 10)
                    result += $"  ... and {analysis.Functions.Count - 10} more functions\n";
                result += "\n";
            }

            if (analysis.Classes.Any())
            {
                result += "Classes:\n";
                foreach (var cls in analysis.Classes.Take(5))
                {
                    result += $"  - class {cls.Name} [Line {cls.LineNumber}]\n";
                    if (cls.BaseClasses.Any())
                        result += $"    Inherits from: {string.Join(", ", cls.BaseClasses)}\n";
                }
                if (analysis.Classes.Count > 5)
                    result += $"  ... and {analysis.Classes.Count - 5} more classes\n";
            }

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error analyzing C++ file: {FilePath}", filePath);
            return $"Error analyzing file: {ex.Message}";
        }
    }

    [McpServerTool]
    [Description("Get the structure of C++ projects in the workspace")]
    public async Task<string> GetProjectStructure()
    {
        try
        {
            _logger.LogInformation("Getting C++ project structure");
            var structure = await _projectStructureService.GetProjectStructureAsync();
            
            var result = $"C++ Project Structure (Last scanned: {structure.LastScanned})\n\n";
            result += $"Total Projects: {structure.Projects.Count}\n";
            result += $"Total Files: {structure.AllFiles.Count}\n\n";

            result += "Projects:\n";
            foreach (var project in structure.Projects)
            {
                result += $"  - {project.Name} ({project.Path})\n";
                result += $"    Build System: {project.BuildSystem ?? "Unknown"}\n";
                result += $"    Source Files: {project.SourceFiles.Count}\n";
                result += $"    Header Files: {project.HeaderFiles.Count}\n";
                if (project.Dependencies.Any())
                {
                    result += $"    Dependencies: {string.Join(", ", project.Dependencies.Take(3))}";
                    if (project.Dependencies.Count > 3)
                        result += $" and {project.Dependencies.Count - 3} more";
                    result += "\n";
                }
                result += "\n";
            }

            if (structure.FilesByExtension.Any())
            {
                result += "Files by Extension:\n";
                foreach (var ext in structure.FilesByExtension)
                {
                    result += $"  {ext.Key}: {ext.Value.Count} files\n";
                }
            }

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting project structure");
            return $"Error getting project structure: {ex.Message}";
        }
    }

    [McpServerTool]
    [Description("Search for C++ symbols (functions, classes, variables) across the codebase")]
    public async Task<string> SearchCppSymbols(
        [Description("Search query for symbol names")] string query,
        [Description("Type of symbol to search for (function, class, variable, all)")] string symbolType = "all")
    {
        try
        {
            if (string.IsNullOrEmpty(query))
            {
                return "Error: query parameter is required";
            }

            _logger.LogInformation("Searching C++ symbols: {Query} (type: {SymbolType})", query, symbolType);
            var symbols = await _semanticAnalyzer.SearchSymbolsAsync(query, symbolType);
            
            var result = $"C++ Symbol Search Results for '{query}' (type: {symbolType})\n";
            result += $"Found {symbols.Count} symbols:\n\n";

            foreach (var symbol in symbols.Take(20))
            {
                result += $"  - {symbol.Name} ({symbol.Type})\n";
                result += $"    File: {symbol.FilePath} [Line {symbol.LineNumber}]\n";
                if (!string.IsNullOrEmpty(symbol.Signature))
                    result += $"    Signature: {symbol.Signature}\n";
                result += $"    Relevance: {symbol.RelevanceScore:F1}\n\n";
            }

            if (symbols.Count > 20)
                result += $"... and {symbols.Count - 20} more symbols\n";

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error searching C++ symbols: {Query}", query);
            return $"Error searching symbols: {ex.Message}";
        }
    }

    [McpServerTool]
    [Description("Get the include dependencies for a C++ file")]
    public async Task<string> GetFileDependencies(
        [Description("Path to the C++ file")] string filePath)
    {
        try
        {
            if (string.IsNullOrEmpty(filePath))
            {
                return "Error: filePath parameter is required";
            }

            _logger.LogInformation("Getting file dependencies: {FilePath}", filePath);
            var dependencies = await _analysisService.GetFileDependenciesAsync(filePath);
            
            var result = $"File Dependencies for: {filePath}\n\n";
            result += $"Direct Includes ({dependencies.DirectIncludes.Count}):\n";
            foreach (var include in dependencies.DirectIncludes)
            {
                result += $"  - {include}\n";
            }

            if (dependencies.ExternalDependencies.Any())
            {
                result += $"\nExternal Dependencies ({dependencies.ExternalDependencies.Count}):\n";
                foreach (var external in dependencies.ExternalDependencies)
                {
                    result += $"  - {external}\n";
                }
            }

            if (dependencies.DependentFiles.Any())
            {
                result += $"\nFiles that depend on this file ({dependencies.DependentFiles.Count}):\n";
                foreach (var dependent in dependencies.DependentFiles.Take(10))
                {
                    result += $"  - {dependent}\n";
                }
                if (dependencies.DependentFiles.Count > 10)
                    result += $"  ... and {dependencies.DependentFiles.Count - 10} more files\n";
            }

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting file dependencies: {FilePath}", filePath);
            return $"Error getting dependencies: {ex.Message}";
        }
    }

    [McpServerTool]
    [Description("Explain a section of C++ code with semantic analysis")]
    public async Task<string> ExplainCppCode(
        [Description("Path to the C++ file")] string filePath,
        [Description("Starting line number (optional)")] int? startLine = null,
        [Description("Ending line number (optional)")] int? endLine = null)
    {
        try
        {
            if (string.IsNullOrEmpty(filePath))
            {
                return "Error: filePath parameter is required";
            }

            _logger.LogInformation("Explaining C++ code: {FilePath} (lines {StartLine}-{EndLine})", filePath, startLine, endLine);
            var explanation = await _semanticAnalyzer.ExplainCodeAsync(filePath, startLine, endLine);
            
            var result = $"C++ Code Explanation for: {filePath}\n";
            if (startLine.HasValue || endLine.HasValue)
                result += $"Lines: {startLine ?? 1} - {(endLine?.ToString() ?? "end")}\n";
            result += $"\nPurpose: {explanation.Purpose}\n\n";
            result += $"Explanation:\n{explanation.Explanation}\n\n";

            if (explanation.KeyConcepts.Any())
            {
                result += $"Key Concepts:\n";
                foreach (var concept in explanation.KeyConcepts)
                {
                    result += $"  - {concept}\n";
                }
                result += "\n";
            }

            if (explanation.SymbolsUsed.Any())
            {
                result += $"Symbols Used:\n";
                foreach (var symbol in explanation.SymbolsUsed.Take(10))
                {
                    result += $"  - {symbol.Name} ({symbol.Type})\n";
                }
                result += "\n";
            }

            if (explanation.Warnings.Any())
            {
                result += $"Warnings:\n";
                foreach (var warning in explanation.Warnings)
                {
                    result += $"  ?? {warning}\n";
                }
            }

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error explaining C++ code: {FilePath}", filePath);
            return $"Error explaining code: {ex.Message}";
        }
    }

    [McpServerTool]
    [Description("Find all references to a C++ symbol")]
    public async Task<string> FindSymbolReferences(
        [Description("Symbol name to find references for")] string symbol)
    {
        try
        {
            if (string.IsNullOrEmpty(symbol))
            {
                return "Error: symbol parameter is required";
            }

            _logger.LogInformation("Finding references for symbol: {Symbol}", symbol);
            var references = await _semanticAnalyzer.FindReferencesAsync(symbol);
            
            var result = $"References for symbol: {symbol}\n";
            result += $"Found {references.References.Count} references:\n\n";

            if (!string.IsNullOrEmpty(references.Definition))
            {
                result += $"Definition: {references.Definition}\n";
                result += $"Defined in: {references.DefinitionFile} [Line {references.DefinitionLine}]\n\n";
            }

            foreach (var reference in references.References.Take(20))
            {
                result += $"  - {reference.FilePath} [Line {reference.LineNumber}, Col {reference.Column}]\n";
                result += $"    Type: {reference.ReferenceType}\n";
                result += $"    Context: {reference.Context}\n\n";
            }

            if (references.References.Count > 20)
                result += $"... and {references.References.Count - 20} more references\n";

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error finding symbol references: {Symbol}", symbol);
            return $"Error finding references: {ex.Message}";
        }
    }

    [McpServerTool]
    [Description("Get the signature and details of a C++ function")]
    public async Task<string> GetFunctionSignature(
        [Description("Name of the function")] string functionName)
    {
        try
        {
            if (string.IsNullOrEmpty(functionName))
            {
                return "Error: functionName parameter is required";
            }

            _logger.LogInformation("Getting function signature: {FunctionName}", functionName);
            var signature = await _semanticAnalyzer.GetFunctionSignatureAsync(functionName);
            
            if (signature == null)
            {
                return $"Function '{functionName}' not found in the codebase.";
            }

            var result = $"Function Signature: {functionName}\n\n";
            result += $"Return Type: {signature.ReturnType}\n";
            result += $"Name: {signature.Name}\n";
            result += $"Location: Line {signature.LineNumber}\n";
            
            if (signature.Parameters.Any())
            {
                result += $"\nParameters ({signature.Parameters.Count}):\n";
                foreach (var param in signature.Parameters)
                {
                    result += $"  - {param.Type} {param.Name}";
                    if (!string.IsNullOrEmpty(param.DefaultValue))
                        result += $" = {param.DefaultValue}";
                    result += "\n";
                }
            }

            if (signature.Attributes.Any())
            {
                result += $"\nAttributes: {string.Join(", ", signature.Attributes)}\n";
            }

            result += $"\nModifiers:";
            if (signature.IsStatic) result += " static";
            if (signature.IsVirtual) result += " virtual";
            if (signature.IsConst) result += " const";
            if (!signature.IsStatic && !signature.IsVirtual && !signature.IsConst) result += " none";

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting function signature: {FunctionName}", functionName);
            return $"Error getting function signature: {ex.Message}";
        }
    }

    [McpServerTool]
    [Description("Get the inheritance hierarchy for a C++ class")]
    public async Task<string> GetClassHierarchy(
        [Description("Name of the class")] string className)
    {
        try
        {
            if (string.IsNullOrEmpty(className))
            {
                return "Error: className parameter is required";
            }

            _logger.LogInformation("Getting class hierarchy: {ClassName}", className);
            var hierarchy = await _semanticAnalyzer.GetClassHierarchyAsync(className);
            
            if (hierarchy == null)
            {
                return $"Class '{className}' not found in the codebase.";
            }

            var result = $"Class Hierarchy: {className}\n\n";
            result += $"Location: {hierarchy.FilePath} [Line {hierarchy.LineNumber}]\n";
            result += $"Is Abstract: {hierarchy.IsAbstract}\n\n";

            if (hierarchy.BaseClasses.Any())
            {
                result += $"Base Classes ({hierarchy.BaseClasses.Count}):\n";
                foreach (var baseClass in hierarchy.BaseClasses)
                {
                    result += $"  - {baseClass}\n";
                }
                result += "\n";
            }

            if (hierarchy.DerivedClasses.Any())
            {
                result += $"Derived Classes ({hierarchy.DerivedClasses.Count}):\n";
                foreach (var derived in hierarchy.DerivedClasses.Take(10))
                {
                    result += $"  - {derived}\n";
                }
                if (hierarchy.DerivedClasses.Count > 10)
                    result += $"  ... and {hierarchy.DerivedClasses.Count - 10} more derived classes\n";
                result += "\n";
            }

            if (hierarchy.VirtualFunctions.Any())
            {
                result += $"Virtual Functions:\n";
                foreach (var kvp in hierarchy.VirtualFunctions)
                {
                    result += $"  Class: {kvp.Key}\n";
                    foreach (var func in kvp.Value.Take(5))
                    {
                        result += $"    - {func.ReturnType} {func.Name}(...)\n";
                    }
                    if (kvp.Value.Count > 5)
                        result += $"    ... and {kvp.Value.Count - 5} more virtual functions\n";
                }
            }

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting class hierarchy: {ClassName}", className);
            return $"Error getting class hierarchy: {ex.Message}";
        }
    }

    [McpServerTool]
    [Description("Analyze include dependencies and detect issues")]
    public async Task<string> AnalyzeIncludes(
        [Description("Path to the C++ file")] string filePath)
    {
        try
        {
            if (string.IsNullOrEmpty(filePath))
            {
                return "Error: filePath parameter is required";
            }

            _logger.LogInformation("Analyzing includes: {FilePath}", filePath);
            var includes = await _analysisService.AnalyzeIncludesAsync(filePath);
            
            var result = $"Include Analysis for: {filePath}\n\n";
            result += $"Total Includes: {includes.Includes.Count}\n";
            result += $"Include Depth: {includes.IncludeDepth}\n\n";

            if (includes.Includes.Any())
            {
                result += "Includes:\n";
                foreach (var include in includes.Includes)
                {
                    result += $"  Line {include.LineNumber}: {include.IncludePath}";
                    if (include.IsSystemInclude)
                        result += " (system)";
                    else
                        result += " (local)";
                    
                    if (!string.IsNullOrEmpty(include.ResolvedPath))
                        result += $" ? {include.ResolvedPath}";
                    else if (!include.IsSystemInclude)
                        result += " [UNRESOLVED]";
                    
                    result += "\n";
                }
                result += "\n";
            }

            if (includes.UnresolvedIncludes.Any())
            {
                result += $"?? Unresolved Includes ({includes.UnresolvedIncludes.Count}):\n";
                foreach (var unresolved in includes.UnresolvedIncludes)
                {
                    result += $"  - {unresolved}\n";
                }
                result += "\n";
            }

            if (includes.CircularDependencies.Any())
            {
                result += $"?? Circular Dependencies ({includes.CircularDependencies.Count}):\n";
                foreach (var circular in includes.CircularDependencies)
                {
                    result += $"  - {circular}\n";
                }
                result += "\n";
            }

            if (includes.UnusedIncludes.Any())
            {
                result += $"?? Potentially Unused Includes ({includes.UnusedIncludes.Count}):\n";
                foreach (var unused in includes.UnusedIncludes)
                {
                    result += $"  - {unused}\n";
                }
            }

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error analyzing includes: {FilePath}", filePath);
            return $"Error analyzing includes: {ex.Message}";
        }
    }

    [McpServerTool]
    [Description("Get compilation errors for C++ files")]
    public async Task<string> GetCompilationErrors(
        [Description("Path to the C++ file (optional, if not provided returns all errors)")] string? filePath = null)
    {
        try
        {
            _logger.LogInformation("Getting compilation errors for: {FilePath}", filePath ?? "all files");
            var errors = await _analysisService.GetCompilationErrorsAsync(filePath);
            
            var result = $"Compilation Errors";
            if (!string.IsNullOrEmpty(filePath))
                result += $" for: {filePath}";
            result += $"\n\nFound {errors.Count} errors:\n\n";

            if (!errors.Any())
            {
                result += "? No compilation errors found!";
                return result;
            }

            foreach (var error in errors.Take(20))
            {
                result += $"? {error.FilePath} [Line {error.LineNumber}, Col {error.Column}]\n";
                result += $"   Code: {error.ErrorCode}\n";
                result += $"   Severity: {error.Severity.ToUpper()}\n";
                result += $"   Message: {error.Message}\n";
                if (!string.IsNullOrEmpty(error.Suggestion))
                    result += $"   ?? Suggestion: {error.Suggestion}\n";
                result += "\n";
            }

            if (errors.Count > 20)
                result += $"... and {errors.Count - 20} more errors\n";

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting compilation errors: {FilePath}", filePath);
            return $"Error getting compilation errors: {ex.Message}";
        }
    }
}