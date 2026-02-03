using System.Text.Json.Serialization;

namespace CppCodeIntelligenceMCP.Models;

public class MCPRequest
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("method")]
    public string? Method { get; set; }

    [JsonPropertyName("parameters")]
    public Dictionary<string, object>? Parameters { get; set; }
}

public class MCPResponse
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("success")]
    public bool Success { get; set; }

    [JsonPropertyName("data")]
    public object? Data { get; set; }

    [JsonPropertyName("error")]
    public string? Error { get; set; }
}

public class FileAnalysis
{
    public string FilePath { get; set; } = string.Empty;
    public List<string> Includes { get; set; } = new();
    public List<FunctionInfo> Functions { get; set; } = new();
    public List<ClassInfo> Classes { get; set; } = new();
    public List<VariableInfo> Variables { get; set; } = new();
    public List<StructInfo> Structs { get; set; } = new();
    public List<EnumInfo> Enums { get; set; } = new();
    public List<MacroInfo> Macros { get; set; } = new();
    public int LineCount { get; set; }
    public DateTime LastModified { get; set; }
}

public class FunctionInfo
{
    public string Name { get; set; } = string.Empty;
    public string ReturnType { get; set; } = string.Empty;
    public List<ParameterInfo> Parameters { get; set; } = new();
    public int LineNumber { get; set; }
    public int EndLineNumber { get; set; }
    public string Visibility { get; set; } = string.Empty; // public, private, protected
    public bool IsStatic { get; set; }
    public bool IsVirtual { get; set; }
    public bool IsConst { get; set; }
    public string? Documentation { get; set; }
    public List<string> Attributes { get; set; } = new();
}

public class ParameterInfo
{
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string? DefaultValue { get; set; }
    public bool IsConst { get; set; }
    public bool IsReference { get; set; }
    public bool IsPointer { get; set; }
}

public class ClassInfo
{
    public string Name { get; set; } = string.Empty;
    public List<string> BaseClasses { get; set; } = new();
    public List<FunctionInfo> Methods { get; set; } = new();
    public List<VariableInfo> Members { get; set; } = new();
    public int LineNumber { get; set; }
    public int EndLineNumber { get; set; }
    public string? Namespace { get; set; }
    public bool IsTemplate { get; set; }
    public List<string> TemplateParameters { get; set; } = new();
    public string? Documentation { get; set; }
}

public class VariableInfo
{
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public int LineNumber { get; set; }
    public string Visibility { get; set; } = string.Empty;
    public bool IsStatic { get; set; }
    public bool IsConst { get; set; }
    public string? InitialValue { get; set; }
    public string? Documentation { get; set; }
}

public class StructInfo
{
    public string Name { get; set; } = string.Empty;
    public List<VariableInfo> Members { get; set; } = new();
    public int LineNumber { get; set; }
    public int EndLineNumber { get; set; }
    public string? Namespace { get; set; }
    public bool IsTemplate { get; set; }
    public List<string> TemplateParameters { get; set; } = new();
    public string? Documentation { get; set; }
}

public class EnumInfo
{
    public string Name { get; set; } = string.Empty;
    public List<EnumValueInfo> Values { get; set; } = new();
    public int LineNumber { get; set; }
    public string? UnderlyingType { get; set; }
    public bool IsClass { get; set; } // enum class vs enum
    public string? Documentation { get; set; }
}

public class EnumValueInfo
{
    public string Name { get; set; } = string.Empty;
    public string? Value { get; set; }
    public string? Documentation { get; set; }
}

public class MacroInfo
{
    public string Name { get; set; } = string.Empty;
    public string? Value { get; set; }
    public List<string> Parameters { get; set; } = new();
    public int LineNumber { get; set; }
    public string? Documentation { get; set; }
}

public class ProjectStructure
{
    public List<ProjectInfo> Projects { get; set; } = new();
    public List<string> AllFiles { get; set; } = new();
    public Dictionary<string, List<string>> FilesByExtension { get; set; } = new();
    public DateTime LastScanned { get; set; }
}

public class ProjectInfo
{
    public string Name { get; set; } = string.Empty;
    public string Path { get; set; } = string.Empty;
    public List<string> SourceFiles { get; set; } = new();
    public List<string> HeaderFiles { get; set; } = new();
    public List<string> IncludeDirectories { get; set; } = new();
    public List<string> Dependencies { get; set; } = new();
    public string? BuildSystem { get; set; } // CMake, MSBuild, etc.
}

public class SymbolSearchResult
{
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty; // function, class, variable, etc.
    public string FilePath { get; set; } = string.Empty;
    public int LineNumber { get; set; }
    public string? Signature { get; set; }
    public string? Documentation { get; set; }
    public double RelevanceScore { get; set; }
}

public class FileDependencyInfo
{
    public string FilePath { get; set; } = string.Empty;
    public List<string> DirectIncludes { get; set; } = new();
    public List<string> IndirectIncludes { get; set; } = new();
    public List<string> DependentFiles { get; set; } = new(); // Files that include this one
    public List<string> ExternalDependencies { get; set; } = new(); // System/third-party includes
}

public class CodeExplanation
{
    public string FilePath { get; set; } = string.Empty;
    public int StartLine { get; set; }
    public int EndLine { get; set; }
    public string CodeSnippet { get; set; } = string.Empty;
    public string Explanation { get; set; } = string.Empty;
    public List<string> KeyConcepts { get; set; } = new();
    public List<SymbolInfo> SymbolsUsed { get; set; } = new();
    public string? Purpose { get; set; }
    public List<string> Warnings { get; set; } = new();
}

public class SymbolInfo
{
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string? Definition { get; set; }
    public string? FilePath { get; set; }
    public int? LineNumber { get; set; }
}

public class ReferenceInfo
{
    public string Symbol { get; set; } = string.Empty;
    public List<ReferenceLocation> References { get; set; } = new();
    public string? Definition { get; set; }
    public string? DefinitionFile { get; set; }
    public int? DefinitionLine { get; set; }
}

public class ReferenceLocation
{
    public string FilePath { get; set; } = string.Empty;
    public int LineNumber { get; set; }
    public int Column { get; set; }
    public string Context { get; set; } = string.Empty; // Surrounding code
    public string ReferenceType { get; set; } = string.Empty; // declaration, definition, usage
}

public class ClassHierarchy
{
    public string ClassName { get; set; } = string.Empty;
    public List<string> BaseClasses { get; set; } = new();
    public List<string> DerivedClasses { get; set; } = new();
    public Dictionary<string, List<FunctionInfo>> VirtualFunctions { get; set; } = new();
    public bool IsAbstract { get; set; }
    public string? FilePath { get; set; }
    public int? LineNumber { get; set; }
}

public class IncludeAnalysis
{
    public string FilePath { get; set; } = string.Empty;
    public List<IncludeInfo> Includes { get; set; } = new();
    public List<string> UnresolvedIncludes { get; set; } = new();
    public List<string> CircularDependencies { get; set; } = new();
    public List<string> UnusedIncludes { get; set; } = new();
    public int IncludeDepth { get; set; }
}

public class IncludeInfo
{
    public string IncludePath { get; set; } = string.Empty;
    public bool IsSystemInclude { get; set; }
    public string? ResolvedPath { get; set; }
    public int LineNumber { get; set; }
    public bool IsUsed { get; set; }
}

public class CompilationError
{
    public string FilePath { get; set; } = string.Empty;
    public int LineNumber { get; set; }
    public int Column { get; set; }
    public string ErrorCode { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string Severity { get; set; } = string.Empty; // error, warning, info
    public string? Suggestion { get; set; }
}