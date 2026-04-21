using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.ServiceDiscovery;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace Systems.ServiceDefaults;

// Adds common .NET Aspire services: service discovery, resilience, health checks, and OpenTelemetry.
// This project should be referenced by each service project in your solution.
// To learn more about using this project, see https://aka.ms/dotnet/aspire/service-defaults
public static class Extensions
{
    public static TBuilder AddServiceDefaults<TBuilder>(this TBuilder builder) where TBuilder : IHostApplicationBuilder
    {
        builder.ConfigureOpenTelemetry();

        builder.AddDefaultHealthChecks();

        builder.Services.AddServiceDiscovery();
        ConfigureServiceDiscoveryPolicy(builder.Services, builder.Configuration);

        builder.Services.ConfigureHttpClientDefaults(http =>
        {
            if (IsStandardResilienceEnabled(builder.Configuration))
            {
                http.AddStandardResilienceHandler();
            }

            http.AddServiceDiscovery();
        });

        return builder;
    }

    public static TBuilder ConfigureOpenTelemetry<TBuilder>(this TBuilder builder) where TBuilder : IHostApplicationBuilder
    {
        builder.Logging.AddOpenTelemetry(logging =>
        {
            logging.IncludeFormattedMessage = true;
            logging.IncludeScopes = true;
        });

        builder.Services.AddOpenTelemetry()
            .ConfigureResource(resource => ConfigureTelemetryResource(resource, builder))
            .WithMetrics(metrics =>
            {
                metrics.AddAspNetCoreInstrumentation()
                    .AddHttpClientInstrumentation()
                    .AddRuntimeInstrumentation();
            })
            .WithTracing(tracing =>
            {
                tracing.AddSource(builder.Environment.ApplicationName)
                    .AddAspNetCoreInstrumentation()
                    // Uncomment the following line to enable gRPC instrumentation (requires the OpenTelemetry.Instrumentation.GrpcNetClient package)
                    //.AddGrpcClientInstrumentation()
                    .AddHttpClientInstrumentation();
            });

        builder.AddOpenTelemetryExporters();

        return builder;
    }

    private static TBuilder AddOpenTelemetryExporters<TBuilder>(this TBuilder builder) where TBuilder : IHostApplicationBuilder
    {
        var useOtlpExporter = !string.IsNullOrWhiteSpace(builder.Configuration["OTEL_EXPORTER_OTLP_ENDPOINT"]);

        if (useOtlpExporter)
        {
            builder.Services.AddOpenTelemetry().UseOtlpExporter();
        }

        // Uncomment the following lines to enable the Azure Monitor exporter (requires the Azure.Monitor.OpenTelemetry.AspNetCore package)
        //if (!string.IsNullOrEmpty(builder.Configuration["APPLICATIONINSIGHTS_CONNECTION_STRING"]))
        //{
        //    builder.Services.AddOpenTelemetry()
        //       .UseAzureMonitor();
        //}

        return builder;
    }

    public static TBuilder AddDefaultHealthChecks<TBuilder>(this TBuilder builder) where TBuilder : IHostApplicationBuilder
    {
        builder.Services.AddHealthChecks()
            // Add a default liveness check to ensure app is responsive
            .AddCheck("self", () => HealthCheckResult.Healthy(), ["live"]);

        return builder;
    }

    public static WebApplication MapDefaultEndpoints(this WebApplication app)
    {
        if (ShouldMapHealthEndpoints(app.Configuration, app.Environment))
        {
            app.MapHealthChecks("/health");

            app.MapHealthChecks("/alive", new HealthCheckOptions
            {
                Predicate = r => r.Tags.Contains("live")
            });
        }

        return app;
    }

    internal static IReadOnlyList<KeyValuePair<string, object>> BuildTelemetryResourceAttributes(
        IConfiguration configuration)
    {
        var attributes = new List<KeyValuePair<string, object>>();

        AddAttribute(attributes, "wwow.bot.role", configuration["ServiceDefaults:Telemetry:BotRole"]);
        AddAttribute(attributes, "wwow.scenario.id", configuration["ServiceDefaults:Telemetry:ScenarioId"]);
        AddAttribute(attributes, "wwow.test.id", configuration["ServiceDefaults:Telemetry:TestId"]);

        return attributes;
    }

    internal static string ResolveTelemetryServiceName(IConfiguration configuration, IHostEnvironment environment)
    {
        var configuredName = configuration["ServiceDefaults:Telemetry:ServiceName"];
        return string.IsNullOrWhiteSpace(configuredName)
            ? environment.ApplicationName
            : configuredName;
    }

    internal static bool ShouldMapHealthEndpoints(IConfiguration configuration, IHostEnvironment environment)
    {
        return environment.IsDevelopment() ||
               ReadBool(configuration, "ServiceDefaults:Health:ExposeEndpoints", fallback: false);
    }

    internal static bool IsStandardResilienceEnabled(IConfiguration configuration)
    {
        return ReadBool(configuration, "ServiceDefaults:Resilience:EnableStandardHandler", fallback: true);
    }

    internal static string[] ResolveAllowedServiceDiscoverySchemes(IConfiguration configuration)
    {
        var configured = ReadStringList(configuration, "ServiceDefaults:ServiceDiscovery:AllowedSchemes");
        if (configured.Length > 0)
        {
            return configured;
        }

        return ReadBool(configuration, "ServiceDefaults:ServiceDiscovery:AllowAllSchemes", fallback: true)
            ? []
            : ["https"];
    }

    internal static bool ShouldAllowAllServiceDiscoverySchemes(IConfiguration configuration)
    {
        var configured = ReadStringList(configuration, "ServiceDefaults:ServiceDiscovery:AllowedSchemes");
        return configured.Length == 0 &&
               ReadBool(configuration, "ServiceDefaults:ServiceDiscovery:AllowAllSchemes", fallback: true);
    }

    private static void ConfigureTelemetryResource<TBuilder>(ResourceBuilder resource, TBuilder builder)
        where TBuilder : IHostApplicationBuilder
    {
        resource.AddService(ResolveTelemetryServiceName(builder.Configuration, builder.Environment));

        var attributes = BuildTelemetryResourceAttributes(builder.Configuration);
        if (attributes.Count > 0)
        {
            resource.AddAttributes(attributes);
        }
    }

    private static void ConfigureServiceDiscoveryPolicy(IServiceCollection services, IConfiguration configuration)
    {
        var allowAllSchemes = ShouldAllowAllServiceDiscoverySchemes(configuration);
        var allowedSchemes = ResolveAllowedServiceDiscoverySchemes(configuration);

        services.Configure<ServiceDiscoveryOptions>(options =>
        {
            options.AllowAllSchemes = allowAllSchemes;

            if (!allowAllSchemes)
            {
                options.AllowedSchemes = allowedSchemes;
            }
        });
    }

    private static string[] ReadStringList(IConfiguration configuration, string key)
    {
        var directValue = configuration[key];
        if (!string.IsNullOrWhiteSpace(directValue))
        {
            return directValue
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }

        return configuration.GetSection(key)
            .GetChildren()
            .Select(child => child.Value)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static bool ReadBool(IConfiguration configuration, string key, bool fallback)
    {
        return bool.TryParse(configuration[key], out var value) ? value : fallback;
    }

    private static void AddAttribute(
        ICollection<KeyValuePair<string, object>> attributes,
        string name,
        string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            attributes.Add(new KeyValuePair<string, object>(name, value));
        }
    }
}
