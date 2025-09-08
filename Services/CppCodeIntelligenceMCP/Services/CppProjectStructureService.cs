using CppCodeIntelligenceMCP.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Text.RegularExpressions;

namespace CppCodeIntelligenceMCP.Services;

public class CppProjectStructureService
{
    private readonly ILogger<CppProjectStructureService> _logger;
    private readonly IConfiguration _configuration;
    private readonly string _workspaceRoot;
    private readonly List<string> _cppProjects;
    private readonly List<string> _fileExtensions;

    public CppProjectStructureService(ILogger<CppProjectStructureService> logger, IConfiguration configuration)
    {
        _logger = logger;
        _configuration = configuration;
        _workspaceRoot = _configuration["CppCodeIntelligence:WorkspaceRoot"] ?? "../../../";
        _cppProjects = _configuration.GetSection("CppCodeIntelligence:CppProjects").Get<List<string>>() ?? new();
        _fileExtensions = _configuration.GetSection("CppCodeIntelligence:FileExtensions").Get<List<string>>() ?? new();
    }

    public async Task InitializeAsync()
    {
        _logger.LogInformation("Initializing C++ Project Structure Service");
        _logger.LogInformation("Workspace root: {WorkspaceRoot}", Path.GetFullPath(_workspaceRoot));
        
        await Task.CompletedTask;
    }

    public async Task<ProjectStructure> GetProjectStructureAsync()
    {
        try
        {
            var structure = new ProjectStructure
            {
                LastScanned = DateTime.UtcNow,
                Projects = new List<ProjectInfo>(),
                AllFiles = new List<string>(),
                FilesByExtension = new Dictionary<string, List<string>>()
            };

            // Initialize file extension dictionary
            foreach (var ext in _fileExtensions)
            {
                structure.FilesByExtension[ext] = new List<string>();
            }

            // Scan each configured C++ project
            foreach (var projectPath in _cppProjects)
            {
                var fullProjectPath = Path.Combine(_workspaceRoot, projectPath);
                
                if (Directory.Exists(fullProjectPath))
                {
                    var projectInfo = await AnalyzeProject(projectPath, fullProjectPath);
                    structure.Projects.Add(projectInfo);
                    
                    // Add files to global lists
                    structure.AllFiles.AddRange(projectInfo.SourceFiles);
                    structure.AllFiles.AddRange(projectInfo.HeaderFiles);
                    
                    // Categorize by extension
                    foreach (var file in projectInfo.SourceFiles.Concat(projectInfo.HeaderFiles))
                    {
                        var extension = Path.GetExtension(file);
                        if (structure.FilesByExtension.ContainsKey(extension))
                        {
                            structure.FilesByExtension[extension].Add(file);
                        }
                    }
                }
                else
                {
                    _logger.LogWarning("Project directory not found: {ProjectPath}", fullProjectPath);
                }
            }

            // Also scan for any additional C++ files in the workspace root
            await ScanAdditionalFiles(structure);

            _logger.LogInformation("Scanned {ProjectCount} projects, found {FileCount} total files", 
                structure.Projects.Count, structure.AllFiles.Count);

            return structure;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting project structure");
            throw;
        }
    }

    private async Task<ProjectInfo> AnalyzeProject(string projectRelativePath, string fullPath)
    {
        var projectInfo = new ProjectInfo
        {
            Name = Path.GetFileName(projectRelativePath),
            Path = projectRelativePath,
            SourceFiles = new List<string>(),
            HeaderFiles = new List<string>(),
            IncludeDirectories = new List<string>(),
            Dependencies = new List<string>()
        };

        try
        {
            // Detect build system
            projectInfo.BuildSystem = DetectBuildSystem(fullPath);

            // Find source and header files
            var sourceExtensions = new[] { ".cpp", ".c", ".cc", ".cxx" };
            var headerExtensions = new[] { ".h", ".hpp", ".hxx" };

            var allFiles = Directory.GetFiles(fullPath, "*.*", SearchOption.AllDirectories)
                .Where(f => _fileExtensions.Contains(Path.GetExtension(f)))
                .ToList();

            foreach (var file in allFiles)
            {
                var relativePath = GetRelativePath(file);
                var extension = Path.GetExtension(file);

                if (sourceExtensions.Contains(extension))
                {
                    projectInfo.SourceFiles.Add(relativePath);
                }
                else if (headerExtensions.Contains(extension))
                {
                    projectInfo.HeaderFiles.Add(relativePath);
                }
            }

            // Analyze include directories
            projectInfo.IncludeDirectories = await AnalyzeIncludeDirectories(fullPath, projectInfo.SourceFiles.Concat(projectInfo.HeaderFiles));

            // Analyze dependencies
            projectInfo.Dependencies = await AnalyzeDependencies(fullPath, projectInfo.SourceFiles.Concat(projectInfo.HeaderFiles));

            _logger.LogDebug("Analyzed project {ProjectName}: {SourceCount} source files, {HeaderCount} header files", 
                projectInfo.Name, projectInfo.SourceFiles.Count, projectInfo.HeaderFiles.Count);

            return projectInfo;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error analyzing project {ProjectPath}", projectRelativePath);
            throw;
        }
    }

    private string DetectBuildSystem(string projectPath)
    {
        // Check for CMake
        if (File.Exists(Path.Combine(projectPath, "CMakeLists.txt")))
        {
            return "CMake";
        }

        // Check for MSBuild (Visual Studio)
        var vcxprojFiles = Directory.GetFiles(projectPath, "*.vcxproj", SearchOption.TopDirectoryOnly);
        if (vcxprojFiles.Any())
        {
            return "MSBuild";
        }

        // Check for Makefile
        if (File.Exists(Path.Combine(projectPath, "Makefile")) || 
            File.Exists(Path.Combine(projectPath, "makefile")))
        {
            return "Make";
        }

        // Check for other build systems
        if (File.Exists(Path.Combine(projectPath, "build.ninja")))
        {
            return "Ninja";
        }

        if (File.Exists(Path.Combine(projectPath, "premake5.lua")) || 
            File.Exists(Path.Combine(projectPath, "premake4.lua")))
        {
            return "Premake";
        }

        return "Unknown";
    }

    private async Task<List<string>> AnalyzeIncludeDirectories(string projectPath, IEnumerable<string> files)
    {
        var includeDirectories = new HashSet<string>();

        try
        {
            // Add common include directories
            var commonIncludes = new[]
            {
                projectPath,
                Path.Combine(projectPath, "include"),
                Path.Combine(projectPath, "src"),
                Path.Combine(projectPath, "headers")
            };

            foreach (var dir in commonIncludes)
            {
                if (Directory.Exists(dir))
                {
                    includeDirectories.Add(GetRelativePath(dir));
                }
            }

            // Analyze actual includes in files to find more directories
            foreach (var file in files.Take(20)) // Limit for performance
            {
                var fullPath = Path.IsPathRooted(file) ? file : Path.Combine(_workspaceRoot, file);
                
                if (File.Exists(fullPath))
                {
                    var content = await File.ReadAllTextAsync(fullPath);
                    var includeMatches = Regex.Matches(content, @"#include\s*""([^""]+)""");

                    foreach (Match match in includeMatches)
                    {
                        var includePath = match.Groups[1].Value;
                        var includeDir = Path.GetDirectoryName(includePath);
                        
                        if (!string.IsNullOrEmpty(includeDir))
                        {
                            var fullIncludeDir = Path.Combine(projectPath, includeDir);
                            if (Directory.Exists(fullIncludeDir))
                            {
                                includeDirectories.Add(GetRelativePath(fullIncludeDir));
                            }
                        }
                    }
                }
            }

            return includeDirectories.ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error analyzing include directories for {ProjectPath}", projectPath);
            return new List<string>();
        }
    }

    private async Task<List<string>> AnalyzeDependencies(string projectPath, IEnumerable<string> files)
    {
        var dependencies = new HashSet<string>();

        try
        {
            // Analyze external libraries from includes
            foreach (var file in files.Take(20)) // Limit for performance
            {
                var fullPath = Path.IsPathRooted(file) ? file : Path.Combine(_workspaceRoot, file);
                
                if (File.Exists(fullPath))
                {
                    var content = await File.ReadAllTextAsync(fullPath);
                    
                    // Find system includes (external dependencies)
                    var systemIncludes = Regex.Matches(content, @"#include\s*<([^>]+)>");
                    
                    foreach (Match match in systemIncludes)
                    {
                        var includePath = match.Groups[1].Value;
                        
                        // Categorize common libraries
                        if (includePath.StartsWith("windows"))
                        {
                            dependencies.Add("Windows API");
                        }
                        else if (includePath.StartsWith("boost/"))
                        {
                            dependencies.Add("Boost");
                        }
                        else if (includePath.StartsWith("std") || includePath.StartsWith("iostream") || 
                                includePath.StartsWith("vector") || includePath.StartsWith("string"))
                        {
                            dependencies.Add("C++ Standard Library");
                        }
                        else if (includePath.StartsWith("GL/") || includePath.StartsWith("gl/"))
                        {
                            dependencies.Add("OpenGL");
                        }
                        else if (includePath.Contains("d3d") || includePath.Contains("directx"))
                        {
                            dependencies.Add("DirectX");
                        }
                        else if (includePath.Contains("stdio") || includePath.Contains("stdlib") || 
                                includePath.Contains("string.h"))
                        {
                            dependencies.Add("C Standard Library");
                        }
                        else
                        {
                            // Generic external dependency
                            var libName = includePath.Split('/')[0];
                            dependencies.Add($"External Library: {libName}");
                        }
                    }
                }
            }

            // Check for specific build configuration files that might indicate dependencies
            var cmakeFile = Path.Combine(projectPath, "CMakeLists.txt");
            if (File.Exists(cmakeFile))
            {
                var cmakeContent = await File.ReadAllTextAsync(cmakeFile);
                
                // Look for find_package calls
                var packageMatches = Regex.Matches(cmakeContent, @"find_package\s*\(\s*(\w+)", RegexOptions.IgnoreCase);
                foreach (Match match in packageMatches)
                {
                    dependencies.Add($"CMake Package: {match.Groups[1].Value}");
                }
                
                // Look for target_link_libraries
                var linkMatches = Regex.Matches(cmakeContent, @"target_link_libraries\s*\([^)]+\s+(\w+)", RegexOptions.IgnoreCase);
                foreach (Match match in linkMatches)
                {
                    dependencies.Add($"Linked Library: {match.Groups[1].Value}");
                }
            }

            return dependencies.ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error analyzing dependencies for {ProjectPath}", projectPath);
            return new List<string>();
        }
    }

    private async Task ScanAdditionalFiles(ProjectStructure structure)
    {
        try
        {
            // Scan workspace root for any additional C++ files not in configured projects
            var allFiles = Directory.GetFiles(_workspaceRoot, "*.*", SearchOption.AllDirectories)
                .Where(f => _fileExtensions.Contains(Path.GetExtension(f)))
                .Select(GetRelativePath)
                .Where(f => !structure.AllFiles.Contains(f))
                .ToList();

            foreach (var file in allFiles)
            {
                structure.AllFiles.Add(file);
                
                var extension = Path.GetExtension(file);
                if (structure.FilesByExtension.ContainsKey(extension))
                {
                    structure.FilesByExtension[extension].Add(file);
                }
            }

            _logger.LogDebug("Found {AdditionalFileCount} additional C++ files in workspace", allFiles.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error scanning additional files");
        }

        await Task.CompletedTask;
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
}