using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using WoWStateManager;

namespace BotRunner.Tests;

public class MangosServerBootstrapperTests
{
    public static IEnumerable<object[]> DefaultConfigFiles()
    {
        yield return [new[] { "Services", "WoWStateManager", "appsettings.json" }];
        yield return [new[] { "Tests", "BotRunner.Tests", "appsettings.test.json" }];
    }

    [Fact]
    public void Options_DefaultToExternalServerOwnership()
    {
        var options = new MangosServerOptions();

        Assert.False(options.AutoLaunch);
        Assert.True(string.IsNullOrWhiteSpace(options.MangosDirectory));
    }

    [Theory]
    [MemberData(nameof(DefaultConfigFiles))]
    public void DefaultConfig_DisablesMangosAutoLaunch(string[] relativeParts)
    {
        var configPath = FindRepoFile(relativeParts);
        using var document = JsonDocument.Parse(File.ReadAllText(configPath));

        Assert.True(document.RootElement.TryGetProperty("MangosServer", out var mangosServer));
        Assert.True(mangosServer.TryGetProperty("AutoLaunch", out var autoLaunch));
        Assert.False(autoLaunch.GetBoolean());

        Assert.False(
            mangosServer.TryGetProperty("MangosDirectory", out var directory) &&
            !string.IsNullOrWhiteSpace(directory.GetString()));
    }

    [Fact]
    public async Task StartAsync_AutoLaunchWithoutDirectorySkipsHostLaunch()
    {
        var bootstrapper = new MangosServerBootstrapper(
            Options.Create(new MangosServerOptions
            {
                AutoLaunch = true,
                MangosDirectory = string.Empty,
            }),
            NullLogger<MangosServerBootstrapper>.Instance);

        await bootstrapper.StartAsync(CancellationToken.None);
    }

    private static string FindRepoFile(params string[] relativeParts)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory != null)
        {
            var candidate = Path.Combine(new[] { directory.FullName }.Concat(relativeParts).ToArray());
            if (File.Exists(candidate))
                return candidate;

            directory = directory.Parent;
        }

        throw new FileNotFoundException($"Could not find repository file: {Path.Combine(relativeParts)}");
    }
}
