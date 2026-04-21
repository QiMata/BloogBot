using FluentAssertions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.ServiceDiscovery;
using Systems.ServiceDefaults;

namespace Systems.ServiceDefaults.Tests;

public class ServiceDefaultsExtensionsTests
{
    [Fact]
    public async Task AddServiceDefaults_RegistersLiveHealthCheck()
    {
        var builder = CreateBuilder(environmentName: Environments.Development);

        builder.AddServiceDefaults();
        using var app = builder.Build();

        var healthCheckService = app.Services.GetRequiredService<HealthCheckService>();
        var result = await healthCheckService.CheckHealthAsync();

        result.Status.Should().Be(HealthStatus.Healthy);
        result.Entries.Should().ContainKey("self");
    }

    [Fact]
    public void ConfigureOpenTelemetry_ResolvesConfiguredResourceFields()
    {
        var builder = CreateBuilder(
            environmentName: Environments.Development,
            new Dictionary<string, string?>
            {
                ["ServiceDefaults:Telemetry:ServiceName"] = "BackgroundBotRunner",
                ["ServiceDefaults:Telemetry:BotRole"] = "BG",
                ["ServiceDefaults:Telemetry:ScenarioId"] = "movement-parity",
                ["ServiceDefaults:Telemetry:TestId"] = "corpse-run-001"
            });

        builder.ConfigureOpenTelemetry();

        Extensions.ResolveTelemetryServiceName(builder.Configuration, builder.Environment)
            .Should().Be("BackgroundBotRunner");

        Extensions.BuildTelemetryResourceAttributes(builder.Configuration)
            .Should().BeEquivalentTo(new[]
            {
                new KeyValuePair<string, object>("wwow.bot.role", "BG"),
                new KeyValuePair<string, object>("wwow.scenario.id", "movement-parity"),
                new KeyValuePair<string, object>("wwow.test.id", "corpse-run-001")
            });
    }

    [Fact]
    public void MapDefaultEndpoints_MapsHealthAndAliveInDevelopment()
    {
        var builder = CreateBuilder(environmentName: Environments.Development);
        builder.AddDefaultHealthChecks();
        using var app = builder.Build();

        app.MapDefaultEndpoints();

        GetRoutePatterns(app).Should().Contain(new[] { "/health", "/alive" });
    }

    [Fact]
    public void MapDefaultEndpoints_DoesNotMapInProductionByDefault()
    {
        var builder = CreateBuilder(environmentName: Environments.Production);
        builder.AddDefaultHealthChecks();
        using var app = builder.Build();

        app.MapDefaultEndpoints();

        GetRoutePatterns(app).Should().NotContain(new[] { "/health", "/alive" });
    }

    [Fact]
    public void MapDefaultEndpoints_MapsInProductionWhenConfigured()
    {
        var builder = CreateBuilder(
            environmentName: Environments.Production,
            new Dictionary<string, string?>
            {
                ["ServiceDefaults:Health:ExposeEndpoints"] = "true"
            });
        builder.AddDefaultHealthChecks();
        using var app = builder.Build();

        app.MapDefaultEndpoints();

        GetRoutePatterns(app).Should().Contain(new[] { "/health", "/alive" });
    }

    [Fact]
    public void AddServiceDefaults_ConfiguresServiceDiscoverySchemePolicy()
    {
        var builder = CreateBuilder(
            environmentName: Environments.Development,
            new Dictionary<string, string?>
            {
                ["ServiceDefaults:ServiceDiscovery:AllowAllSchemes"] = "false",
                ["ServiceDefaults:ServiceDiscovery:AllowedSchemes"] = "https,http"
            });

        builder.AddServiceDefaults();
        using var app = builder.Build();

        var options = app.Services.GetRequiredService<IOptions<ServiceDiscoveryOptions>>().Value;
        options.AllowAllSchemes.Should().BeFalse();
        options.AllowedSchemes.Should().BeEquivalentTo(new[] { "https", "http" });
    }

    [Fact]
    public void ResilienceStandardHandler_DefaultsToEnabledAndCanBeDisabled()
    {
        var enabled = CreateBuilder(environmentName: Environments.Development);
        var disabled = CreateBuilder(
            environmentName: Environments.Development,
            new Dictionary<string, string?>
            {
                ["ServiceDefaults:Resilience:EnableStandardHandler"] = "false"
            });

        Extensions.IsStandardResilienceEnabled(enabled.Configuration).Should().BeTrue();
        Extensions.IsStandardResilienceEnabled(disabled.Configuration).Should().BeFalse();
    }

    [Fact]
    public void ServiceDiscovery_DefaultsToAllowAllAndHttpsFallbackWhenRestrictedWithoutList()
    {
        var allowAll = CreateBuilder(environmentName: Environments.Development);
        var restricted = CreateBuilder(
            environmentName: Environments.Development,
            new Dictionary<string, string?>
            {
                ["ServiceDefaults:ServiceDiscovery:AllowAllSchemes"] = "false"
            });

        Extensions.ShouldAllowAllServiceDiscoverySchemes(allowAll.Configuration).Should().BeTrue();
        Extensions.ResolveAllowedServiceDiscoverySchemes(allowAll.Configuration).Should().BeEmpty();

        Extensions.ShouldAllowAllServiceDiscoverySchemes(restricted.Configuration).Should().BeFalse();
        Extensions.ResolveAllowedServiceDiscoverySchemes(restricted.Configuration).Should().Equal("https");
    }

    private static WebApplicationBuilder CreateBuilder(
        string environmentName,
        IReadOnlyDictionary<string, string?>? settings = null)
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            EnvironmentName = environmentName,
            ApplicationName = "Systems.ServiceDefaults.Tests"
        });

        if (settings is not null)
        {
            builder.Configuration.AddInMemoryCollection(settings);
        }

        return builder;
    }

    private static IReadOnlyList<string> GetRoutePatterns(WebApplication app)
    {
        return ((IEndpointRouteBuilder)app).DataSources
            .SelectMany(source => source.Endpoints)
            .OfType<RouteEndpoint>()
            .Select(endpoint => endpoint.RoutePattern.RawText ?? string.Empty)
            .ToArray();
    }
}
