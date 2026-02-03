using CppCodeIntelligenceMCP.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Text.RegularExpressions;

namespace CppCodeIntelligenceMCP.Services;

public class CppAnalysisService
{
    private readonly ILogger<CppAnalysisService> _logger;
    private readonly IConfiguration _configuration;
    private readonly string _workspaceRoot;
    private readonly List<string> _cppProjects;
    private readonly List<string> _fileExtensions;

    public CppAnalysisService(ILogger<CppAnalysisService> logger, IConfiguration configuration)
    {
        _logger = logger;
        _configuration = configuration;
        _workspaceRoot = _configuration["CppCodeIntelligence:WorkspaceRoot"] ?? "../../../";
        _cppProjects = _configuration.GetSection("CppCodeIntelligence:CppProjects").Get<List<string>>() ?? new();
        _fileExtensions = _configuration.GetSection("CppCodeIntelligence:FileExtensions").Get<List<string>>() ?? new();
    }

    public async Task InitializeAsync()
    {
        _logger.LogInformation("Initializing C++ Analysis Service");
        _logger.LogInformation("Workspace root: {WorkspaceRoot}", Path.GetFullPath(_workspaceRoot));
        _logger.LogInformation("C++ projects: {Projects}", string.Join(", ", _cppProjects));
        
        await Task.CompletedTask;
    }

    public async Task<FileAnalysis> AnalyzeFileAsync(string filePath)
    {
        try
        {
            var fullPath = Path.IsPathRooted(filePath) ? filePath : Path.Combine(_workspaceRoot, filePath);
            
            if (!File.Exists(fullPath))
            {
                throw new FileNotFoundException($"File not found: {fullPath}");
            }

            var content = await File.ReadAllTextAsync(fullPath);
            var lines = content.Split('\n');

            var analysis = new FileAnalysis
            {
                FilePath = filePath,
                LineCount = lines.Length,
                LastModified = File.GetLastWriteTime(fullPath)
            };

            // Extract includes
            analysis.Includes = ExtractIncludes(content);

            // Extract functions
            analysis.Functions = ExtractFunctions(content);

            // Extract classes
            analysis.Classes = ExtractClasses(content);

            // Extract variables
            analysis.Variables = ExtractVariables(content);

            // Extract structs
            analysis.Structs = ExtractStructs(content);

            // Extract enums
            analysis.Enums = ExtractEnums(content);

            // Extract macros
            analysis.Macros = ExtractMacros(content);

            _logger.LogDebug("Analyzed file {FilePath}: {FunctionCount} functions, {ClassCount} classes", 
                filePath, analysis.Functions.Count, analysis.Classes.Count);

            return analysis;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error analyzing file {FilePath}", filePath);
            throw;
        }
    }

    public async Task<FileDependencyInfo> GetFileDependenciesAsync(string filePath)
    {
        try
        {
            var fullPath = Path.IsPathRooted(filePath) ? filePath : Path.Combine(_workspaceRoot, filePath);
            
            if (!File.Exists(fullPath))
            {
                throw new FileNotFoundException($"File not found: {fullPath}");
            }

            var content = await File.ReadAllTextAsync(fullPath);
            var includes = ExtractIncludes(content);

            var dependencies = new FileDependencyInfo
            {
                FilePath = filePath,
                DirectIncludes = includes
            };

            // Find external vs internal dependencies
            foreach (var include in includes)
            {
                if (include.StartsWith('<') || include.Contains("windows.h") || include.Contains("std"))
                {
                    dependencies.ExternalDependencies.Add(include);
                }
            }

            // Find files that depend on this file (reverse lookup)
            dependencies.DependentFiles = await FindFilesThatInclude(filePath);

            return dependencies;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting file dependencies for {FilePath}", filePath);
            throw;
        }
    }

    public async Task<IncludeAnalysis> AnalyzeIncludesAsync(string filePath)
    {
        try
        {
            var fullPath = Path.IsPathRooted(filePath) ? filePath : Path.Combine(_workspaceRoot, filePath);
            
            if (!File.Exists(fullPath))
            {
                throw new FileNotFoundException($"File not found: {fullPath}");
            }

            var content = await File.ReadAllTextAsync(fullPath);
            var lines = content.Split('\n');

            var analysis = new IncludeAnalysis
            {
                FilePath = filePath
            };

            // Extract include information with line numbers
            for (int i = 0; i < lines.Length; i++)
            {
                var line = lines[i].Trim();
                if (line.StartsWith("#include"))
                {
                    var includeMatch = Regex.Match(line, @"#include\s*[<""]([^>""]+)[>""]");
                    if (includeMatch.Success)
                    {
                        var includePath = includeMatch.Groups[1].Value;
                        var isSystemInclude = line.Contains('<');

                        var includeInfo = new IncludeInfo
                        {
                            IncludePath = includePath,
                            IsSystemInclude = isSystemInclude,
                            LineNumber = i + 1,
                            IsUsed = true // TODO: Implement usage analysis
                        };

                        // Try to resolve the path
                        if (!isSystemInclude)
                        {
                            var resolvedPath = TryResolveIncludePath(includePath, Path.GetDirectoryName(fullPath)!);
                            includeInfo.ResolvedPath = resolvedPath;
                            
                            if (string.IsNullOrEmpty(resolvedPath))
                            {
                                analysis.UnresolvedIncludes.Add(includePath);
                            }
                        }

                        analysis.Includes.Add(includeInfo);
                    }
                }
            }

            analysis.IncludeDepth = analysis.Includes.Count;

            return analysis;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error analyzing includes for {FilePath}", filePath);
            throw;
        }
    }

    public async Task<List<CompilationError>> GetCompilationErrorsAsync(string? filePath = null)
    {
        // Mock implementation - in a real scenario, this would integrate with clang or MSVC
        var errors = new List<CompilationError>();

        try
        {
            if (!string.IsNullOrEmpty(filePath))
            {
                // Simulate some common C++ compilation errors
                var fullPath = Path.IsPathRooted(filePath) ? filePath : Path.Combine(_workspaceRoot, filePath);
                
                if (File.Exists(fullPath))
                {
                    var content = await File.ReadAllTextAsync(fullPath);
                    var lines = content.Split('\n');

                    // Simple syntax checking
                    for (int i = 0; i < lines.Length; i++)
                    {
                        var line = lines[i];
                        
                        // Check for missing semicolons (very basic check)
                        if (line.Trim().EndsWith('}') && !line.Trim().EndsWith("};") && 
                            (line.Contains("class") || line.Contains("struct")))
                        {
                            errors.Add(new CompilationError
                            {
                                FilePath = filePath,
                                LineNumber = i + 1,
                                Column = line.Length,
                                ErrorCode = "C2143",
                                Message = "syntax error: missing ';' before '}'",
                                Severity = "error",
                                Suggestion = "Add semicolon after class/struct definition"
                            });
                        }
                        
                        // Check for undefined includes
                        if (line.Trim().StartsWith("#include") && !line.Contains('<'))
                        {
                            var includeMatch = Regex.Match(line, @"#include\s*""([^""]+)""");
                            if (includeMatch.Success)
                            {
                                var includePath = includeMatch.Groups[1].Value;
                                var resolvedPath = TryResolveIncludePath(includePath, Path.GetDirectoryName(fullPath)!);
                                
                                if (string.IsNullOrEmpty(resolvedPath))
                                {
                                    errors.Add(new CompilationError
                                    {
                                        FilePath = filePath,
                                        LineNumber = i + 1,
                                        Column = 1,
                                        ErrorCode = "C1083",
                                        Message = $"Cannot open include file: '{includePath}': No such file or directory",
                                        Severity = "error",
                                        Suggestion = "Check the include path and ensure the file exists"
                                    });
                                }
                            }
                        }
                    }
                }
            }

            return errors;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting compilation errors");
            return new List<CompilationError>
            {
                new()
                {
                    FilePath = filePath ?? "unknown",
                    LineNumber = 1,
                    Column = 1,
                    ErrorCode = "ANALYSIS_ERROR",
                    Message = $"Error during analysis: {ex.Message}",
                    Severity = "error"
                }
            };
        }
    }

    private List<string> ExtractIncludes(string content)
    {
        var includes = new List<string>();
        var includeRegex = new Regex(@"#include\s*[<""]([^>""]+)[>""]", RegexOptions.Multiline);
        
        foreach (Match match in includeRegex.Matches(content))
        {
            includes.Add(match.Groups[1].Value);
        }
        
        return includes;
    }

    private List<FunctionInfo> ExtractFunctions(string content)
    {
        var functions = new List<FunctionInfo>();
        
        // Simple regex to match function declarations/definitions
        var functionRegex = new Regex(
            @"(?:(?:inline|static|virtual|extern)\s+)*" +  // Optional keywords
            @"(?:(\w+(?:\s*\*)*)\s+)?" +                    // Return type (optional for constructors)
            @"(\w+)\s*\(" +                                 // Function name and opening paren
            @"([^)]*)\)" +                                  // Parameters
            @"\s*(?:const)?\s*" +                          // Optional const
            @"(?:;|{)",                                     // Ending with ; or {
            RegexOptions.Multiline);

        var lines = content.Split('\n');
        
        foreach (Match match in functionRegex.Matches(content))
        {
            var lineNumber = content.Substring(0, match.Index).Count(c => c == '\n') + 1;
            
            var function = new FunctionInfo
            {
                Name = match.Groups[2].Value,
                ReturnType = match.Groups[1].Value ?? "void",
                LineNumber = lineNumber,
                Parameters = ParseParameters(match.Groups[3].Value)
            };
            
            functions.Add(function);
        }
        
        return functions;
    }

    private List<ClassInfo> ExtractClasses(string content)
    {
        var classes = new List<ClassInfo>();
        
        var classRegex = new Regex(@"class\s+(\w+)(?:\s*:\s*([^{]+))?\s*{", RegexOptions.Multiline);
        var lines = content.Split('\n');
        
        foreach (Match match in classRegex.Matches(content))
        {
            var lineNumber = content.Substring(0, match.Index).Count(c => c == '\n') + 1;
            
            var classInfo = new ClassInfo
            {
                Name = match.Groups[1].Value,
                LineNumber = lineNumber
            };
            
            // Parse base classes
            if (match.Groups[2].Success)
            {
                var baseClasses = match.Groups[2].Value.Split(',')
                    .Select(bc => bc.Trim().Split().Last())
                    .ToList();
                classInfo.BaseClasses = baseClasses;
            }
            
            classes.Add(classInfo);
        }
        
        return classes;
    }

    private List<VariableInfo> ExtractVariables(string content)
    {
        var variables = new List<VariableInfo>();
        
        // Simple variable declaration regex
        var varRegex = new Regex(@"(?:(?:static|const|extern)\s+)*(\w+(?:\s*\*)*)\s+(\w+)(?:\s*=\s*([^;]+))?\s*;", 
            RegexOptions.Multiline);
        
        foreach (Match match in varRegex.Matches(content))
        {
            var lineNumber = content.Substring(0, match.Index).Count(c => c == '\n') + 1;
            
            var variable = new VariableInfo
            {
                Type = match.Groups[1].Value,
                Name = match.Groups[2].Value,
                LineNumber = lineNumber,
                InitialValue = match.Groups[3].Success ? match.Groups[3].Value.Trim() : null
            };
            
            variables.Add(variable);
        }
        
        return variables;
    }

    private List<StructInfo> ExtractStructs(string content)
    {
        var structs = new List<StructInfo>();
        
        var structRegex = new Regex(@"struct\s+(\w+)\s*{", RegexOptions.Multiline);
        
        foreach (Match match in structRegex.Matches(content))
        {
            var lineNumber = content.Substring(0, match.Index).Count(c => c == '\n') + 1;
            
            var structInfo = new StructInfo
            {
                Name = match.Groups[1].Value,
                LineNumber = lineNumber
            };
            
            structs.Add(structInfo);
        }
        
        return structs;
    }

    private List<EnumInfo> ExtractEnums(string content)
    {
        var enums = new List<EnumInfo>();
        
        var enumRegex = new Regex(@"enum(?:\s+class)?\s+(\w+)(?:\s*:\s*(\w+))?\s*{([^}]+)}", 
            RegexOptions.Multiline | RegexOptions.Singleline);
        
        foreach (Match match in enumRegex.Matches(content))
        {
            var lineNumber = content.Substring(0, match.Index).Count(c => c == '\n') + 1;
            
            var enumInfo = new EnumInfo
            {
                Name = match.Groups[1].Value,
                LineNumber = lineNumber,
                UnderlyingType = match.Groups[2].Success ? match.Groups[2].Value : null,
                IsClass = content.Substring(match.Index, match.Length).Contains("enum class")
            };
            
            // Parse enum values
            var valuesText = match.Groups[3].Value;
            var valueMatches = Regex.Matches(valuesText, @"(\w+)(?:\s*=\s*([^,}]+))?");
            
            foreach (Match valueMatch in valueMatches)
            {
                enumInfo.Values.Add(new EnumValueInfo
                {
                    Name = valueMatch.Groups[1].Value,
                    Value = valueMatch.Groups[2].Success ? valueMatch.Groups[2].Value.Trim() : null
                });
            }
            
            enums.Add(enumInfo);
        }
        
        return enums;
    }

    private List<MacroInfo> ExtractMacros(string content)
    {
        var macros = new List<MacroInfo>();
        
        var macroRegex = new Regex(@"#define\s+(\w+)(?:\(([^)]*)\))?\s*(.*)$", RegexOptions.Multiline);
        
        foreach (Match match in macroRegex.Matches(content))
        {
            var lineNumber = content.Substring(0, match.Index).Count(c => c == '\n') + 1;
            
            var macro = new MacroInfo
            {
                Name = match.Groups[1].Value,
                LineNumber = lineNumber,
                Value = match.Groups[3].Success ? match.Groups[3].Value.Trim() : null
            };
            
            // Parse parameters if it's a function-like macro
            if (match.Groups[2].Success)
            {
                var parameters = match.Groups[2].Value.Split(',')
                    .Select(p => p.Trim())
                    .Where(p => !string.IsNullOrEmpty(p))
                    .ToList();
                macro.Parameters = parameters;
            }
            
            macros.Add(macro);
        }
        
        return macros;
    }

    private List<ParameterInfo> ParseParameters(string parametersText)
    {
        var parameters = new List<ParameterInfo>();
        
        if (string.IsNullOrWhiteSpace(parametersText))
            return parameters;
        
        var paramParts = parametersText.Split(',');
        
        foreach (var part in paramParts)
        {
            var trimmed = part.Trim();
            if (string.IsNullOrEmpty(trimmed))
                continue;
                
            var tokens = trimmed.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (tokens.Length >= 2)
            {
                var parameter = new ParameterInfo
                {
                    Type = string.Join(" ", tokens.Take(tokens.Length - 1)),
                    Name = tokens.Last()
                };
                
                // Check for default value
                if (parameter.Name.Contains('='))
                {
                    var equalIndex = parameter.Name.IndexOf('=');
                    parameter.DefaultValue = parameter.Name.Substring(equalIndex + 1).Trim();
                    parameter.Name = parameter.Name.Substring(0, equalIndex).Trim();
                }
                
                parameters.Add(parameter);
            }
        }
        
        return parameters;
    }

    private async Task<List<string>> FindFilesThatInclude(string targetFile)
    {
        var dependentFiles = new List<string>();
        
        try
        {
            var allCppFiles = GetAllCppFiles();
            var targetFileName = Path.GetFileName(targetFile);
            
            foreach (var file in allCppFiles)
            {
                if (file == targetFile) continue;
                
                var content = await File.ReadAllTextAsync(file);
                var includes = ExtractIncludes(content);
                
                if (includes.Any(inc => inc.Contains(targetFileName) || 
                                      Path.GetFileName(inc) == targetFileName))
                {
                    dependentFiles.Add(file);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error finding dependent files");
        }
        
        return dependentFiles;
    }

    private string? TryResolveIncludePath(string includePath, string currentDirectory)
    {
        // Try relative to current file
        var relativePath = Path.Combine(currentDirectory, includePath);
        if (File.Exists(relativePath))
            return relativePath;
        
        // Try relative to workspace root
        var workspacePath = Path.Combine(_workspaceRoot, includePath);
        if (File.Exists(workspacePath))
            return workspacePath;
        
        // Try include directories from configuration
        var includeDirectories = _configuration.GetSection("CppCodeIntelligence:IncludeDirectories").Get<List<string>>() ?? new();
        foreach (var includeDir in includeDirectories)
        {
            var fullIncludeDir = Path.IsPathRooted(includeDir) ? includeDir : Path.Combine(_workspaceRoot, includeDir);
            var candidatePath = Path.Combine(fullIncludeDir, includePath);
            if (File.Exists(candidatePath))
                return candidatePath;
        }
        
        return null;
    }

    private List<string> GetAllCppFiles()
    {
        var files = new List<string>();
        
        try
        {
            var searchDirectories = _cppProjects.Select(proj => Path.Combine(_workspaceRoot, proj)).ToList();
            searchDirectories.Add(_workspaceRoot);
            
            foreach (var directory in searchDirectories)
            {
                if (Directory.Exists(directory))
                {
                    foreach (var extension in _fileExtensions)
                    {
                        var pattern = $"*{extension}";
                        files.AddRange(Directory.GetFiles(directory, pattern, SearchOption.AllDirectories));
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting C++ files");
        }
        
        return files.Distinct().ToList();
    }
}