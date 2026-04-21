using System.Collections.Generic;
using System.IO;
using System.Linq;
using Communication;
using Newtonsoft.Json;
using WoWStateManager;
using WoWStateManager.Settings;

namespace BotRunner.Tests;

public class WoWStateManagerLaunchThrottleTests
{
    [Fact]
    public void IsWorldReadyForLaunchProgress_RequiresObjectManagerValidity()
    {
        Assert.False(StateManagerWorker.IsWorldReadyForLaunchProgress(null));
        Assert.False(StateManagerWorker.IsWorldReadyForLaunchProgress(new WoWActivitySnapshot { IsObjectManagerValid = false }));
        Assert.True(StateManagerWorker.IsWorldReadyForLaunchProgress(new WoWActivitySnapshot { IsObjectManagerValid = true }));
    }

    [Fact]
    public void CountPendingStartupBots_CountsOnlyManagedBotsThatAreNotWorldReady()
    {
        var configuredSettings = new[]
        {
            new CharacterSettings { AccountName = "READY", ShouldRun = true },
            new CharacterSettings { AccountName = "PENDING", ShouldRun = true },
            new CharacterSettings { AccountName = "DISABLED", ShouldRun = false },
            new CharacterSettings { AccountName = "UNMANAGED", ShouldRun = true },
        };

        var snapshots = new Dictionary<string, WoWActivitySnapshot>(StringComparer.OrdinalIgnoreCase)
        {
            ["READY"] = new() { IsObjectManagerValid = true },
            ["PENDING"] = new() { IsObjectManagerValid = false },
            ["DISABLED"] = new() { IsObjectManagerValid = false },
            ["UNMANAGED"] = new() { IsObjectManagerValid = false },
        };

        var managedAccounts = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "READY",
            "PENDING",
            "DISABLED",
        };

        var pending = StateManagerWorker.CountPendingStartupBots(
            configuredSettings,
            snapshots,
            managedAccounts);

        Assert.Equal(1, pending);
    }

    [Fact]
    public void CountPendingStartupBots_TreatsMissingSnapshotsAsPending()
    {
        var configuredSettings = new[]
        {
            new CharacterSettings { AccountName = "READY", ShouldRun = true },
            new CharacterSettings { AccountName = "MISSING", ShouldRun = true },
        };

        var snapshots = new Dictionary<string, WoWActivitySnapshot>(StringComparer.OrdinalIgnoreCase)
        {
            ["READY"] = new() { IsObjectManagerValid = true },
        };

        var managedAccounts = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "READY",
            "MISSING",
        };

        var pending = StateManagerWorker.CountPendingStartupBots(
            configuredSettings,
            snapshots,
            managedAccounts);

        Assert.Equal(1, pending);
    }

    [Fact]
    public void OrderLaunchSettings_PrioritizesForeground_WhilePreservingRelativeOrder()
    {
        var configuredSettings = new[]
        {
            new CharacterSettings { AccountName = "BG_A", RunnerType = WoWStateManager.Settings.BotRunnerType.Background },
            new CharacterSettings { AccountName = "FG_A", RunnerType = WoWStateManager.Settings.BotRunnerType.Foreground },
            new CharacterSettings { AccountName = "BG_B", RunnerType = WoWStateManager.Settings.BotRunnerType.Background },
            new CharacterSettings { AccountName = "FG_B", RunnerType = WoWStateManager.Settings.BotRunnerType.Foreground },
        };

        var ordered = StateManagerWorker.OrderLaunchSettings(configuredSettings)
            .Select(settings => settings.AccountName)
            .ToArray();

        Assert.Equal(["FG_A", "FG_B", "BG_A", "BG_B"], ordered);
    }

    [Fact]
    public void ResolveLaunchThrottleActivationBotCount_UsesPositiveEnvOverride()
    {
        var originalValue = Environment.GetEnvironmentVariable(StateManagerWorker.LaunchThrottleActivationBotCountEnvVar);

        try
        {
            Environment.SetEnvironmentVariable(StateManagerWorker.LaunchThrottleActivationBotCountEnvVar, "8");
            Assert.Equal(8, StateManagerWorker.ResolveLaunchThrottleActivationBotCount());

            Environment.SetEnvironmentVariable(StateManagerWorker.LaunchThrottleActivationBotCountEnvVar, "0");
            Assert.Equal(StateManagerWorker.LaunchThrottleActivationBotCount, StateManagerWorker.ResolveLaunchThrottleActivationBotCount());
        }
        finally
        {
            Environment.SetEnvironmentVariable(StateManagerWorker.LaunchThrottleActivationBotCountEnvVar, originalValue);
        }
    }

    [Fact]
    public void ResolveMaxPendingStartupBots_UsesPositiveEnvOverride()
    {
        var originalValue = Environment.GetEnvironmentVariable(StateManagerWorker.MaxPendingStartupBotsEnvVar);

        try
        {
            Environment.SetEnvironmentVariable(StateManagerWorker.MaxPendingStartupBotsEnvVar, "8");
            Assert.Equal(8, StateManagerWorker.ResolveMaxPendingStartupBots());

            Environment.SetEnvironmentVariable(StateManagerWorker.MaxPendingStartupBotsEnvVar, "-1");
            Assert.Equal(StateManagerWorker.MaxPendingStartupBots, StateManagerWorker.ResolveMaxPendingStartupBots());
        }
        finally
        {
            Environment.SetEnvironmentVariable(StateManagerWorker.MaxPendingStartupBotsEnvVar, originalValue);
        }
    }

    [Fact]
    public void AlteracValleySettings_IncludeAllianceAccountsInLaunchOrder()
    {
        var configPath = FindRepoFile(
            "Services",
            "WoWStateManager",
            "Settings",
            "Configs",
            "AlteracValley.config.json");

        var settings = JsonConvert.DeserializeObject<List<CharacterSettings>>(
            File.ReadAllText(configPath)) ?? [];

        var launchOrder = StateManagerWorker.OrderLaunchSettings(settings)
            .Where(setting => setting.ShouldRun)
            .Select(setting => setting.AccountName)
            .ToArray();

        Assert.Equal(80, launchOrder.Length);
        Assert.Equal(["AVBOT1", "AVBOTA1"], launchOrder.Take(2));

        var allianceAccounts = settings
            .Where(setting => setting.AccountName.StartsWith("AVBOTA", StringComparison.OrdinalIgnoreCase))
            .ToArray();

        Assert.Equal(40, allianceAccounts.Length);
        Assert.All(allianceAccounts, setting => Assert.True(setting.ShouldRun));
        Assert.All(allianceAccounts, setting => Assert.Contains(setting.AccountName, launchOrder));
        Assert.Equal(39, allianceAccounts.Count(setting => setting.RunnerType == WoWStateManager.Settings.BotRunnerType.Background));
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
