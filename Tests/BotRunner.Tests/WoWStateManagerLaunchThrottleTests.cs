using System.Collections.Generic;
using Communication;
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
}
