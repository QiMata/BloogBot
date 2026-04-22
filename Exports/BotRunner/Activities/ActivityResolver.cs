using System;
using Serilog;

namespace BotRunner.Activities;

/// <summary>
/// Parses an <c>AssignedActivity</c> descriptor (from CharacterSettings /
/// the <c>WWOW_ASSIGNED_ACTIVITY</c> env var) into a concrete
/// <see cref="IActivity"/>. Descriptors are of the form <c>"Name[Location]"</c>
/// where the location is optional — e.g. <c>"Fishing[Ratchet]"</c>,
/// <c>"Battleground[WSG]"</c>, <c>"Dungeon[RFC]"</c>, or bare <c>"Idle"</c>.
///
/// Unknown descriptors log a warning and return null so the bot falls back
/// to its default idle sequence rather than crashing.
/// </summary>
public static class ActivityResolver
{
    /// <summary>
    /// Parse <paramref name="descriptor"/> into an activity. Returns null when
    /// the descriptor is null/empty or references an unknown activity.
    /// </summary>
    public static IActivity? Resolve(string? descriptor)
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

        switch (name)
        {
            case "Fishing" when string.Equals(location, "Ratchet", StringComparison.OrdinalIgnoreCase):
                return new FishingAtRatchetActivity();

            default:
                Log.Warning(
                    "[ACTIVITY] Unknown assigned-activity descriptor '{Descriptor}' (name='{Name}', location='{Location}'); falling back to idle.",
                    descriptor, name, location ?? "(none)");
                return null;
        }
    }
}
