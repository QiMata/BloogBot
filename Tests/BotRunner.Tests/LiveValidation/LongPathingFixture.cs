using System;
using System.IO;
using System.Threading.Tasks;
using Xunit;

namespace BotRunner.Tests.LiveValidation;

/// <summary>
/// Long-pathing live fixture that starts StateManager with the long-pathing
/// roster before the shared live fixture initialization launches any clients.
/// </summary>
public sealed class LongPathingFixture : LiveBotFixture, IAsyncLifetime
{
    async Task IAsyncLifetime.InitializeAsync()
    {
        SetCustomSettingsPath(ResolveLongPathingSettingsPath());
        await base.InitializeAsync();
    }

    private static string ResolveLongPathingSettingsPath()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null)
        {
            var candidate = Path.Combine(
                dir.FullName,
                "Services",
                "WoWStateManager",
                "Settings",
                "Configs",
                "LongPathing.config.json");
            if (File.Exists(candidate))
                return candidate;

            dir = dir.Parent;
        }

        throw new FileNotFoundException("Could not find LongPathing.config.json.");
    }
}

[CollectionDefinition(Name)]
public sealed class LongPathingValidationCollection : ICollectionFixture<LongPathingFixture>
{
    public const string Name = "LongPathing";
}
