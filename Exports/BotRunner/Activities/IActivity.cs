using BotRunner.Interfaces;

namespace BotRunner.Activities;

/// <summary>
/// High-level goal assigned to a bot by StateManager. An activity owns whatever
/// sub-objectives are required to make the goal happen: getting to the right
/// location, satisfying the required loadout (items/skills/spells), and then
/// performing the activity itself (fishing, dungeon run, battleground queue,
/// gathering, grinding, etc.).
///
/// When <see cref="CreateTask"/> is invoked with <c>useGmCommands=true</c>,
/// the activity is allowed to short-circuit travel/outfit via GM chat
/// commands (<c>.go xyz</c>, <c>.additem</c>, <c>.learn</c>, <c>.setskill</c>).
/// This is the shortcut path used by tests and authoring environments.
///
/// When <c>useGmCommands=false</c>, the activity must fall back to natural
/// sub-objectives — flight-path travel, buying items, visiting trainers,
/// grinding reputation, etc. That path is not required to be implemented
/// for every activity yet; initial implementations may log
/// "non-GM path not yet implemented" and no-op until the DecisionEngine
/// fills the gap.
/// </summary>
public interface IActivity
{
    /// <summary>Short activity name, e.g. <c>"Fishing"</c> or <c>"Battleground"</c>.</summary>
    string Name { get; }

    /// <summary>
    /// Optional location qualifier parsed from the assigned-activity descriptor,
    /// e.g. <c>"Ratchet"</c> from <c>"Fishing[Ratchet]"</c>.
    /// </summary>
    string? Location { get; }

    /// <summary>
    /// Build the root task for this activity. The bot runner pushes the
    /// returned task onto its task stack once the bot is safely in-world.
    /// </summary>
    IBotTask CreateTask(IBotContext context, bool useGmCommands);
}
