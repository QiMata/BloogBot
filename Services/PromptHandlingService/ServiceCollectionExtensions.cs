using System.IO;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using PromptHandlingService.Cache;

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
