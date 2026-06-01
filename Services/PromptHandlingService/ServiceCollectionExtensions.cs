using System.IO;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using PromptHandlingService.Foundry.Deployment;
using PromptHandlingService.Cache;
using PromptHandlingService.Foundry;
using PromptHandlingService.Storylines;

namespace PromptHandlingService;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddPromptHandlingServices(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddSingleton<PromptCache>(provider =>
        {
            var environment = provider.GetRequiredService<IHostEnvironment>();
            var configuredPath = configuration["PromptHandling:CachePath"];
            var cachePath = ResolveCachePath(environment, configuredPath);
            EnsureDirectoryExists(cachePath);
            return new PromptCache(cachePath);
        });
        services.AddSingleton(_ => FoundryPersonaRuntimeOptions.FromConfiguration(configuration));
        services.AddSingleton<PersonaPromptAssembler>();
        services.AddSingleton<IFoundryResponsesClient>(provider =>
            new FoundryProjectResponsesClient(provider.GetRequiredService<FoundryPersonaRuntimeOptions>()));
        services.AddSingleton<IFoundryPersonaRuntime, FoundryPersonaRuntime>();
        services.AddSingleton<FoundryAgentProvisioner>();
        services.AddSingleton<IStorylineFoundryDeploymentProvisioner, StorylineFoundryDeploymentProvisioner>();
        services.AddSingleton(provider =>
        {
            var environment = provider.GetRequiredService<IHostEnvironment>();
            return StorylineRuntimeOptions.FromConfiguration(configuration, environment.ContentRootPath);
        });
        services.AddSingleton<IStorylineRepository, SqliteStorylineRepository>();
        services.AddSingleton<StorylineFoundryInstructionBuilder>();
        services.AddSingleton<IStorylineFoundryDeploymentQueue, StorylineFoundryDeploymentQueue>();
        services.AddSingleton<IStorylineFoundryDeploymentService, StorylineFoundryDeploymentService>();
        services.AddSingleton<IStorylineContextResolver, StorylineContextResolver>();
        services.AddSingleton<IStorylinePersonaRuntime, StorylinePersonaRuntime>();

        return services;
    }

    private static string ResolveCachePath(IHostEnvironment environment, string? configuredPath)
    {
        if (!string.IsNullOrWhiteSpace(configuredPath))
        {
            return Path.IsPathRooted(configuredPath)
                ? configuredPath
                : Path.Combine(environment.ContentRootPath, configuredPath);
        }

        return Path.Combine(environment.ContentRootPath, "prompt_cache.sqlite");
    }

    private static void EnsureDirectoryExists(string cachePath)
    {
        var directory = Path.GetDirectoryName(cachePath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }
    }
}
