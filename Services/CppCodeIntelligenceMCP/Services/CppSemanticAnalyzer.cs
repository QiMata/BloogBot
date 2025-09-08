using CppCodeIntelligenceMCP.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Text.RegularExpressions;

namespace CppCodeIntelligenceMCP.Services;

public class CppSemanticAnalyzer
{
    private readonly ILogger<CppSemanticAnalyzer> _logger;
    private readonly IConfiguration _configuration;
    private readonly CppAnalysisService _analysisService;
    private readonly string _workspaceRoot;
    private readonly Dictionary<string, FileAnalysis> _analysisCache = new();

    public CppSemanticAnalyzer(
        ILogger<CppSemanticAnalyzer> logger, 
        IConfiguration configuration,
        CppAnalysisService analysisService)
    {
        _logger = logger;
        _configuration = configuration;
        _analysisService = analysisService;
        _workspaceRoot = _configuration["CppCodeIntelligence:WorkspaceRoot"] ?? "../../../";
    }

    public async Task<List<SymbolSearchResult>> SearchSymbolsAsync(string query, string symbolType = "all")
    {
        var results = new List<SymbolSearchResult>();
        
        try
        {
            var allFiles = GetAllCppFiles();
            var searchPattern = new Regex(Regex.Escape(query), RegexOptions.IgnoreCase);

            foreach (var file in allFiles.Take(50)) // Limit to avoid performance issues
            {
                var analysis = await GetOrCreateAnalysis(file);
                
                // Search functions
                if (symbolType == "all" || symbolType == "function")
                {
                    foreach (var function in analysis.Functions)
                    {
                        if (searchPattern.IsMatch(function.Name))
                        {
                            results.Add(new SymbolSearchResult
                            {
                                Name = function.Name,
                                Type = "function",
                                FilePath = GetRelativePath(file),
                                LineNumber = function.LineNumber,
                                Signature = $"{function.ReturnType} {function.Name}({string.Join(", ", function.Parameters.Select(p => $"{p.Type} {p.Name}"))})",
                                RelevanceScore = CalculateRelevanceScore(function.Name, query)
                            });
                        }
                    }
                }

                // Search classes
                if (symbolType == "all" || symbolType == "class")
                {
                    foreach (var cls in analysis.Classes)
                    {
                        if (searchPattern.IsMatch(cls.Name))
                        {
                            results.Add(new SymbolSearchResult
                            {
                                Name = cls.Name,
                                Type = "class",
                                FilePath = GetRelativePath(file),
                                LineNumber = cls.LineNumber,
                                Signature = $"class {cls.Name}",
                                RelevanceScore = CalculateRelevanceScore(cls.Name, query)
                            });
                        }
                    }
                }

                // Search variables
                if (symbolType == "all" || symbolType == "variable")
                {
                    foreach (var variable in analysis.Variables)
                    {
                        if (searchPattern.IsMatch(variable.Name))
                        {
                            results.Add(new SymbolSearchResult
                            {
                                Name = variable.Name,
                                Type = "variable",
                                FilePath = GetRelativePath(file),
                                LineNumber = variable.LineNumber,
                                Signature = $"{variable.Type} {variable.Name}",
                                RelevanceScore = CalculateRelevanceScore(variable.Name, query)
                            });
                        }
                    }
                }

                // Search structs
                if (symbolType == "all" || symbolType == "struct")
                {
                    foreach (var structInfo in analysis.Structs)
                    {
                        if (searchPattern.IsMatch(structInfo.Name))
                        {
                            results.Add(new SymbolSearchResult
                            {
                                Name = structInfo.Name,
                                Type = "struct",
                                FilePath = GetRelativePath(file),
                                LineNumber = structInfo.LineNumber,
                                Signature = $"struct {structInfo.Name}",
                                RelevanceScore = CalculateRelevanceScore(structInfo.Name, query)
                            });
                        }
                    }
                }

                // Search enums
                if (symbolType == "all" || symbolType == "enum")
                {
                    foreach (var enumInfo in analysis.Enums)
                    {
                        if (searchPattern.IsMatch(enumInfo.Name))
                        {
                            results.Add(new SymbolSearchResult
                            {
                                Name = enumInfo.Name,
                                Type = "enum",
                                FilePath = GetRelativePath(file),
                                LineNumber = enumInfo.LineNumber,
                                Signature = $"enum {enumInfo.Name}",
                                RelevanceScore = CalculateRelevanceScore(enumInfo.Name, query)
                            });
                        }
                    }
                }

                // Search macros
                if (symbolType == "all" || symbolType == "macro")
                {
                    foreach (var macro in analysis.Macros)
                    {
                        if (searchPattern.IsMatch(macro.Name))
                        {
                            results.Add(new SymbolSearchResult
                            {
                                Name = macro.Name,
                                Type = "macro",
                                FilePath = GetRelativePath(file),
                                LineNumber = macro.LineNumber,
                                Signature = $"#define {macro.Name}",
                                RelevanceScore = CalculateRelevanceScore(macro.Name, query)
                            });
                        }
                    }
                }
            }

            // Sort by relevance score (descending)
            results = results.OrderByDescending(r => r.RelevanceScore).Take(100).ToList();

            _logger.LogDebug("Found {Count} symbols matching '{Query}' of type '{SymbolType}'", 
                results.Count, query, symbolType);

            return results;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error searching symbols for query '{Query}'", query);
            throw;
        }
    }

    public async Task<CodeExplanation> ExplainCodeAsync(string filePath, int? startLine = null, int? endLine = null)
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

            var explanation = new CodeExplanation
            {
                FilePath = filePath,
                StartLine = startLine ?? 1,
                EndLine = endLine ?? lines.Length
            };

            // Extract the code snippet
            var snippetStart = Math.Max(0, (startLine ?? 1) - 1);
            var snippetEnd = Math.Min(lines.Length - 1, (endLine ?? lines.Length) - 1);
            
            explanation.CodeSnippet = string.Join('\n', lines[snippetStart..(snippetEnd + 1)]);

            // Analyze the code snippet
            var analysis = await GetOrCreateAnalysis(fullPath);
            
            // Generate explanation based on what's in the code
            var explanationParts = new List<string>();
            var concepts = new List<string>();
            var symbols = new List<SymbolInfo>();

            // Check for functions in the range
            var functionsInRange = analysis.Functions.Where(f => 
                f.LineNumber >= explanation.StartLine && f.LineNumber <= explanation.EndLine).ToList();

            if (functionsInRange.Any())
            {
                foreach (var func in functionsInRange)
                {
                    explanationParts.Add($"Function '{func.Name}' returns {func.ReturnType} and takes {func.Parameters.Count} parameter(s)");
                    concepts.Add("Function Definition");
                    symbols.Add(new SymbolInfo
                    {
                        Name = func.Name,
                        Type = "function",
                        Definition = $"{func.ReturnType} {func.Name}({string.Join(", ", func.Parameters.Select(p => $"{p.Type} {p.Name}"))})"
                    });
                }
            }

            // Check for classes in the range
            var classesInRange = analysis.Classes.Where(c => 
                c.LineNumber >= explanation.StartLine && c.LineNumber <= explanation.EndLine).ToList();

            if (classesInRange.Any())
            {
                foreach (var cls in classesInRange)
                {
                    explanationParts.Add($"Class '{cls.Name}' with {cls.Methods.Count} methods");
                    if (cls.BaseClasses.Any())
                    {
                        explanationParts.Add($"Inherits from: {string.Join(", ", cls.BaseClasses)}");
                        concepts.Add("Inheritance");
                    }
                    concepts.Add("Class Definition");
                    symbols.Add(new SymbolInfo
                    {
                        Name = cls.Name,
                        Type = "class",
                        Definition = $"class {cls.Name}"
                    });
                }
            }

            // Check for includes
            if (explanation.CodeSnippet.Contains("#include"))
            {
                var includeMatches = Regex.Matches(explanation.CodeSnippet, @"#include\s*[<""]([^>""]+)[>""]");
                foreach (Match match in includeMatches)
                {
                    explanationParts.Add($"Includes header file: {match.Groups[1].Value}");
                    concepts.Add("Header Include");
                }
            }

            // Check for macros
            if (explanation.CodeSnippet.Contains("#define"))
            {
                concepts.Add("Macro Definition");
                explanationParts.Add("Contains macro definitions");
            }

            // Check for common patterns
            if (explanation.CodeSnippet.Contains("malloc") || explanation.CodeSnippet.Contains("free"))
            {
                concepts.Add("Memory Management");
                explanationParts.Add("Contains manual memory management");
                explanation.Warnings.Add("Manual memory management detected - ensure proper cleanup");
            }

            if (explanation.CodeSnippet.Contains("new") || explanation.CodeSnippet.Contains("delete"))
            {
                concepts.Add("Dynamic Allocation");
                explanationParts.Add("Contains dynamic memory allocation");
            }

            if (explanation.CodeSnippet.Contains("virtual"))
            {
                concepts.Add("Polymorphism");
                explanationParts.Add("Contains virtual functions for polymorphism");
            }

            if (explanation.CodeSnippet.Contains("template"))
            {
                concepts.Add("Templates");
                explanationParts.Add("Contains template definitions for generic programming");
            }

            // Generate final explanation
            if (explanationParts.Any())
            {
                explanation.Explanation = string.Join(". ", explanationParts) + ".";
            }
            else
            {
                explanation.Explanation = "This code snippet contains general C++ code.";
            }

            explanation.KeyConcepts = concepts.Distinct().ToList();
            explanation.SymbolsUsed = symbols;

            // Determine purpose based on content
            if (functionsInRange.Any(f => f.Name.ToLower().Contains("main")))
            {
                explanation.Purpose = "Program entry point";
            }
            else if (classesInRange.Any())
            {
                explanation.Purpose = "Class definition and implementation";
            }
            else if (explanation.CodeSnippet.Contains("#include"))
            {
                explanation.Purpose = "Header includes and declarations";
            }
            else
            {
                explanation.Purpose = "Implementation code";
            }

            return explanation;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error explaining code for {FilePath}", filePath);
            throw;
        }
    }

    public async Task<ReferenceInfo> FindReferencesAsync(string symbol)
    {
        var references = new ReferenceInfo
        {
            Symbol = symbol,
            References = new List<ReferenceLocation>()
        };

        try
        {
            var allFiles = GetAllCppFiles();
            var symbolRegex = new Regex($@"\b{Regex.Escape(symbol)}\b", RegexOptions.IgnoreCase);

            foreach (var file in allFiles.Take(100)) // Limit for performance
            {
                var content = await File.ReadAllTextAsync(file);
                var lines = content.Split('\n');

                for (int i = 0; i < lines.Length; i++)
                {
                    var line = lines[i];
                    var matches = symbolRegex.Matches(line);

                    foreach (Match match in matches)
                    {
                        var refLocation = new ReferenceLocation
                        {
                            FilePath = GetRelativePath(file),
                            LineNumber = i + 1,
                            Column = match.Index + 1,
                            Context = line.Trim()
                        };

                        // Determine reference type
                        if (line.Contains($"{symbol}(") && (line.Contains("return") || line.Contains("=")))
                        {
                            refLocation.ReferenceType = "usage";
                        }
                        else if (line.Contains($"class {symbol}") || line.Contains($"struct {symbol}"))
                        {
                            refLocation.ReferenceType = "definition";
                            references.Definition = line.Trim();
                            references.DefinitionFile = GetRelativePath(file);
                            references.DefinitionLine = i + 1;
                        }
                        else if (line.Contains($"{symbol}::") || line.Contains($"::{symbol}"))
                        {
                            refLocation.ReferenceType = "member access";
                        }
                        else
                        {
                            refLocation.ReferenceType = "reference";
                        }

                        references.References.Add(refLocation);
                    }
                }
            }

            _logger.LogDebug("Found {Count} references for symbol '{Symbol}'", references.References.Count, symbol);

            return references;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error finding references for symbol '{Symbol}'", symbol);
            throw;
        }
    }

    public async Task<FunctionInfo?> GetFunctionSignatureAsync(string functionName)
    {
        try
        {
            var allFiles = GetAllCppFiles();

            foreach (var file in allFiles)
            {
                var analysis = await GetOrCreateAnalysis(file);
                var function = analysis.Functions.FirstOrDefault(f => 
                    f.Name.Equals(functionName, StringComparison.OrdinalIgnoreCase));

                if (function != null)
                {
                    _logger.LogDebug("Found function '{FunctionName}' in file '{FilePath}'", functionName, file);
                    return function;
                }
            }

            _logger.LogDebug("Function '{FunctionName}' not found", functionName);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting function signature for '{FunctionName}'", functionName);
            throw;
        }
    }

    public async Task<ClassHierarchy?> GetClassHierarchyAsync(string className)
    {
        try
        {
            var allFiles = GetAllCppFiles();
            ClassInfo? targetClass = null;
            string? targetFile = null;

            // Find the target class
            foreach (var file in allFiles)
            {
                var analysis = await GetOrCreateAnalysis(file);
                var cls = analysis.Classes.FirstOrDefault(c => 
                    c.Name.Equals(className, StringComparison.OrdinalIgnoreCase));

                if (cls != null)
                {
                    targetClass = cls;
                    targetFile = file;
                    break;
                }
            }

            if (targetClass == null)
            {
                _logger.LogDebug("Class '{ClassName}' not found", className);
                return null;
            }

            var hierarchy = new ClassHierarchy
            {
                ClassName = targetClass.Name,
                BaseClasses = targetClass.BaseClasses,
                FilePath = GetRelativePath(targetFile!),
                LineNumber = targetClass.LineNumber,
                VirtualFunctions = new Dictionary<string, List<FunctionInfo>>()
            };

            // Find derived classes
            foreach (var file in allFiles)
            {
                var analysis = await GetOrCreateAnalysis(file);
                var derivedClasses = analysis.Classes.Where(c => 
                    c.BaseClasses.Any(bc => bc.Contains(className))).ToList();

                foreach (var derived in derivedClasses)
                {
                    hierarchy.DerivedClasses.Add(derived.Name);
                }
            }

            // Find virtual functions
            var virtualFunctions = targetClass.Methods.Where(m => m.IsVirtual).ToList();
            if (virtualFunctions.Any())
            {
                hierarchy.VirtualFunctions[targetClass.Name] = virtualFunctions;
            }

            // Check if class is abstract (has pure virtual functions)
            hierarchy.IsAbstract = virtualFunctions.Any(f => 
                f.Documentation?.Contains("= 0") == true);

            _logger.LogDebug("Built hierarchy for class '{ClassName}' with {BaseCount} base classes and {DerivedCount} derived classes", 
                className, hierarchy.BaseClasses.Count, hierarchy.DerivedClasses.Count);

            return hierarchy;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting class hierarchy for '{ClassName}'", className);
            throw;
        }
    }

    private async Task<FileAnalysis> GetOrCreateAnalysis(string filePath)
    {
        var relativePath = GetRelativePath(filePath);
        
        if (_analysisCache.TryGetValue(relativePath, out var cachedAnalysis))
        {
            return cachedAnalysis;
        }

        var analysis = await _analysisService.AnalyzeFileAsync(relativePath);
        _analysisCache[relativePath] = analysis;
        
        return analysis;
    }

    private double CalculateRelevanceScore(string symbolName, string query)
    {
        // Exact match gets highest score
        if (symbolName.Equals(query, StringComparison.OrdinalIgnoreCase))
            return 100.0;

        // Starts with query gets high score
        if (symbolName.StartsWith(query, StringComparison.OrdinalIgnoreCase))
            return 80.0;

        // Contains query gets medium score
        if (symbolName.Contains(query, StringComparison.OrdinalIgnoreCase))
            return 60.0;

        // Similar length and characters get lower score
        var similarity = CalculateStringSimilarity(symbolName.ToLower(), query.ToLower());
        return similarity * 40.0;
    }

    private double CalculateStringSimilarity(string s1, string s2)
    {
        var longer = s1.Length > s2.Length ? s1 : s2;
        var shorter = s1.Length > s2.Length ? s2 : s1;

        if (longer.Length == 0)
            return 1.0;

        var editDistance = CalculateLevenshteinDistance(longer, shorter);
        return (longer.Length - editDistance) / (double)longer.Length;
    }

    private int CalculateLevenshteinDistance(string s1, string s2)
    {
        var matrix = new int[s1.Length + 1, s2.Length + 1];

        for (int i = 0; i <= s1.Length; i++)
            matrix[i, 0] = i;

        for (int j = 0; j <= s2.Length; j++)
            matrix[0, j] = j;

        for (int i = 1; i <= s1.Length; i++)
        {
            for (int j = 1; j <= s2.Length; j++)
            {
                var cost = s1[i - 1] == s2[j - 1] ? 0 : 1;
                matrix[i, j] = Math.Min(
                    Math.Min(matrix[i - 1, j] + 1, matrix[i, j - 1] + 1),
                    matrix[i - 1, j - 1] + cost);
            }
        }

        return matrix[s1.Length, s2.Length];
    }

    private string GetRelativePath(string fullPath)
    {
        var workspaceFullPath = Path.GetFullPath(_workspaceRoot);
        var fileFullPath = Path.GetFullPath(fullPath);
        
        if (fileFullPath.StartsWith(workspaceFullPath))
        {
            return Path.GetRelativePath(workspaceFullPath, fileFullPath).Replace('\\', '/');
        }
        
        return fullPath;
    }

    private List<string> GetAllCppFiles()
    {
        var files = new List<string>();
        var fileExtensions = _configuration.GetSection("CppCodeIntelligence:FileExtensions").Get<List<string>>() ?? new();
        var cppProjects = _configuration.GetSection("CppCodeIntelligence:CppProjects").Get<List<string>>() ?? new();
        
        try
        {
            var searchDirectories = cppProjects.Select(proj => Path.Combine(_workspaceRoot, proj)).ToList();
            searchDirectories.Add(_workspaceRoot);
            
            foreach (var directory in searchDirectories)
            {
                if (Directory.Exists(directory))
                {
                    foreach (var extension in fileExtensions)
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