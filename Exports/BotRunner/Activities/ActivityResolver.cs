using System;
using BotRunner.Interfaces;
using BotRunner.Tasks;
using Serilog;

namespace BotRunner.Activities;

/// <summary>
/// Parses an <c>AssignedActivity</c> descriptor (from CharacterSettings /
/// the <c>WWOW_ASSIGNED_ACTIVITY</c> env var) into a concrete root
/// <see cref="IBotTask"/>. Descriptors are of the form <c>"Name[Location]"</c>
/// where the location is optional — e.g. <c>"Fishing[Ratchet]"</c>,
/// <c>"Battleground[WSG]"</c>, <c>"Dungeon[RFC]"</c>, or bare <c>"Idle"</c>.
///
/// There are no per-activity-per-location class files. The bot task itself
/// (e.g. <see cref="FishingTask"/>) owns the full sequence: outfit → travel
/// (<c>.tele name &lt;character&gt; &lt;location&gt;</c>) → execute. Unknown
/// descriptors log a warning and return null so the bot falls back to its
/// default idle sequence rather than crashing.
/// </summary>
public static class ActivityResolver
{
    /// <summary>
    /// Map of activity location → master pool id used for <c>.pool update</c>
    /// during gear/spell setup, so the test/dev environment doesn't have to
    /// wait on natural respawn timers. Production (<c>useGmCommands=false</c>)
    /// never sends this.
    /// </summary>
    private static uint? ResolveMasterPoolId(string activityName, string? location)
    {
        if (!string.Equals(activityName, "Fishing", StringComparison.OrdinalIgnoreCase))
            return null;
        if (string.Equals(location, "Ratchet", StringComparison.OrdinalIgnoreCase))
            return 2628;
        return null;
    }

    /// <summary>
    /// Resolve the descriptor into the root task to push onto the bot's task
    /// stack. Returns null when the descriptor is null/empty or references an
    /// unknown activity.
    /// </summary>
    public static IBotTask? Resolve(IBotContext context, string? descriptor, bool useGmCommands)
    {
        if (string.IsNullOrWhiteSpace(descriptor))
            return null;

        var trimmed = descriptor.Trim();
        string name = trimmed;
        string? location = null;

        var openBracket = trimmed.IndexOf('[');
        var closeBracket = trimmed.LastIndexOf(']');
        if (openBracket > 0 && closeBracket == trimmed.Length - 1 && closeBracket > openBracket + 1)
        {
            name = trimmed[..openBracket].Trim();
            location = trimmed[(openBracket + 1)..closeBracket].Trim();
            if (location.Length == 0)
                location = null;
        }

        if (string.Equals(name, "Fishing", StringComparison.OrdinalIgnoreCase))
        {
            var masterPoolId = ResolveMasterPoolId(name, location);
            return new FishingTask(
                context,
                searchWaypoints: null,
                location: location,
                useGmCommands: useGmCommands,
                masterPoolId: masterPoolId);
        }

        Log.Warning(
            "[ACTIVITY] Unknown assigned-activity descriptor '{Descriptor}' (name='{Name}', location='{Location}'); falling back to idle.",
            descriptor, name, location ?? "(none)");
        return null;
    }
}
