using CppCodeIntelligenceMCP.Services;
using CppCodeIntelligenceMCP.Tools;
using ModelContextProtocol.Server;
using Serilog;
using System.Diagnostics;

// Configure Serilog with more detailed logging for debugging
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Debug()
    .Enrich.FromLogContext()
    .WriteTo.Console()
    .WriteTo.File("logs/cppintelligence-.txt", rollingInterval: RollingInterval.Day)
    .CreateLogger();

Log.Information("=== C++ Intelligence MCP Server Starting ===");
Log.Information("Process ID: {ProcessId}", Environment.ProcessId);
Log.Information("Working Directory: {WorkingDirectory}", Environment.CurrentDirectory);
Log.Information("Command Line Args: {Args}", Environment.GetCommandLineArgs());

var builder = WebApplication.CreateBuilder(args);

// Use Serilog for logging
builder.Host.UseSerilog();

// Add configuration with fallback defaults
var configBuilder = new ConfigurationBuilder();

// Try to find and add appsettings.json from multiple possible locations
var currentDir = Directory.GetCurrentDirectory();
var possiblePaths = new[]
{
    currentDir, // Current working directory
    Path.Combine(currentDir, "Services", "CppCodeIntelligenceMCP"), // Solution root -> project
    Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location) ?? currentDir // Assembly location
};

var configFileFound = false;
foreach (var path in possiblePaths)
{
    var configFile = Path.Combine(path, "appsettings.json");
    Log.Information("Checking for config at: {ConfigFile}", configFile);
    if (File.Exists(configFile))
    {
        Log.Information("Found config file at: {ConfigFile}", configFile);
        configBuilder.SetBasePath(path);
        configBuilder.AddJsonFile("appsettings.json", optional: true);
        
        var envConfig = $"appsettings.{Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT") ?? "Production"}.json";
        if (File.Exists(Path.Combine(path, envConfig)))
        {
            configBuilder.AddJsonFile(envConfig, optional: true);
        }
        
        configFileFound = true;
        break;
    }
}

if (!configFileFound)
{
    Log.Information("No appsettings.json found, using fallback configuration");
    // Use fallback in-memory configuration if no appsettings.json found
    configBuilder.AddInMemoryCollection(new[]
    {
        new KeyValuePair<string, string?>("CppCodeIntelligence:WorkspaceRoot", "../../.."),
        new KeyValuePair<string, string?>("CppCodeIntelligence:CppProjects:0", "Exports/FastCall"),
        new KeyValuePair<string, string?>("CppCodeIntelligence:CppProjects:1", "Exports/Navigation"),
        new KeyValuePair<string, string?>("CppCodeIntelligence:CppProjects:2", "Exports/Loader"),
        new KeyValuePair<string, string?>("CppCodeIntelligence:IncludeDirectories:0", "Exports/FastCall"),
        new KeyValuePair<string, string?>("CppCodeIntelligence:IncludeDirectories:1", "Exports/Navigation"),
        new KeyValuePair<string, string?>("CppCodeIntelligence:IncludeDirectories:2", "Exports/Loader"),
        new KeyValuePair<string, string?>("CppCodeIntelligence:FileExtensions:0", ".cpp"),
        new KeyValuePair<string, string?>("CppCodeIntelligence:FileExtensions:1", ".h"),
        new KeyValuePair<string, string?>("CppCodeIntelligence:FileExtensions:2", ".hpp"),
        new KeyValuePair<string, string?>("CppCodeIntelligence:FileExtensions:3", ".c"),
        new KeyValuePair<string, string?>("CppCodeIntelligence:FileExtensions:4", ".cc"),
        new KeyValuePair<string, string?>("CppCodeIntelligence:FileExtensions:5", ".cxx")
    });
}

var configuration = configBuilder.Build();
builder.Configuration.AddConfiguration(configuration);

Log.Information("Configuration built successfully");

try
{
    // Add C++ analysis services
    Log.Information("Registering C++ analysis services...");
    builder.Services.AddSingleton<CppAnalysisService>();
    builder.Services.AddSingleton<CppSemanticAnalyzer>();
    builder.Services.AddSingleton<CppProjectStructureService>();

    // Add the tools class
    builder.Services.AddSingleton<CppAnalysisTools>();

    Log.Information("Registering MCP server with HTTP transport...");
    // Add MCP server with HTTP transport
    builder.Services.AddMcpServer()
        .WithHttpTransport()
        .WithToolsFromAssembly();

    Log.Information("Building application...");
    var app = builder.Build();

    // Map MCP endpoints (this will create the /sse endpoint)
    Log.Information("Mapping MCP endpoints...");
    app.MapMcp();

    // Health check endpoint
    app.MapGet("/health", () => {
        Log.Information("Health check requested");
        return new { 
            status = "healthy", 
            timestamp = DateTime.UtcNow,
            service = "BloogBot.CppCodeIntelligenceMCP",
            version = "1.0.0",
            uptime = DateTime.UtcNow.Subtract(Process.GetCurrentProcess().StartTime)
        };
    });

    // Add root endpoint for debugging
    app.MapGet("/", () => {
        Log.Information("Root endpoint requested");
        return new {
            message = "C++ Intelligence MCP Server is running",
            endpoints = new {
                health = "/health",
                sse = "/sse",
                mcp = "/mcp"
            },
            timestamp = DateTime.UtcNow
        };
    });

    Log.Information("Starting BloogBot C++ Intelligence MCP Server on http://localhost:5002");
    Log.Information("MCP SSE endpoint available at: http://localhost:5002/sse");
    Log.Information("Health check endpoint: http://localhost:5002/health");
    
    // Configure URL
    app.Urls.Add("http://localhost:5002");
    
    Log.Information("Server is ready to accept connections...");
    await app.RunAsync();
}
catch (Exception ex)
{
    Log.Fatal(ex, "C++ Intelligence MCP Server terminated unexpectedly");
    throw;
}
finally
{
    Log.CloseAndFlush();
}