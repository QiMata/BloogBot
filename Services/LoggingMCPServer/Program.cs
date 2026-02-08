using ModelContextProtocol.Server;
using LoggingMCPServer.Tools;
using Serilog;

// Configure Serilog
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .Enrich.FromLogContext()
    .WriteTo.Console()
    .WriteTo.File("logs/loggingmcp-.txt", rollingInterval: RollingInterval.Day)
    .CreateLogger();

var builder = WebApplication.CreateBuilder(args);

// Use Serilog for logging
builder.Host.UseSerilog();

// Add controllers for REST API endpoints
builder.Services.AddControllers();

// Add our custom services first (these are required by controllers and MCP tools)
builder.Services.AddSingleton<LogEventProcessor>();
builder.Services.AddSingleton<TelemetryCollector>();

// Add MCP server with HTTP transport
builder.Services.AddMcpServer()
    .WithHttpTransport()
    .WithToolsFromAssembly();

// Add Swagger for API documentation
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new Microsoft.OpenApi.Models.OpenApiInfo 
    { 
        Title = "BloogBot Logging MCP Server", 
        Version = "v1",
        Description = "A comprehensive logging and telemetry MCP server with REST API endpoints"
    });
});

// Add CORS for cross-origin requests
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(builder =>
    {
        builder.AllowAnyOrigin()
               .AllowAnyMethod()
               .AllowAnyHeader();
    });
});

var app = builder.Build();

// Enable CORS
app.UseCors();

// Enable Swagger in development
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "BloogBot Logging MCP Server v1");
        c.RoutePrefix = "swagger"; // Swagger UI at /swagger
    });
}

// Map controllers for REST API endpoints
app.MapControllers();

// Map MCP endpoints (this will create the /sse endpoint)
app.MapMcp();

// Health check endpoint
app.MapGet("/health", () => new { 
    status = "healthy", 
    timestamp = DateTime.UtcNow,
    service = "LoggingMCPServer",
    version = "1.0.0"
});

// Root endpoint that redirects to Swagger
app.MapGet("/", () => Results.Redirect("/swagger"));

try 
{
    Log.Information("Starting BloogBot Logging MCP Server on http://localhost:5001");
    Log.Information("MCP SSE endpoint available at: http://localhost:5001/sse");
    Log.Information("REST API endpoints available at: http://localhost:5001/api/");
    Log.Information("Swagger UI available at: http://localhost:5001/swagger");
    Log.Information("Root URL redirects to Swagger: http://localhost:5001/");
    
    // Configure URL
    app.Urls.Add("http://localhost:5001");
    
    await app.RunAsync();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Application terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}
