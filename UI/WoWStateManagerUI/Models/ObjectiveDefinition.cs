using System.Collections.Generic;

namespace WoWStateManagerUI.Models
{
    /// <summary>
    /// Catalog-side definition of an Objective: tens-of-seconds sub-goal within
    /// an Activity (e.g. "Reach the instance portal", "Kill the next boss",
    /// "Collect 10 venom sacs"). Objectives are achieved by a series of Tasks.
    ///
    /// See <c>docs/Spec/18_TERMINOLOGY.md</c> for the canonical four-layer
    /// hierarchy: Activity → Objective → Task → Action.
    /// </summary>
    public sealed record ObjectiveDefinition(
        string Id,
        string DisplayName,
        string? Description,
        IReadOnlyList<TaskDefinition> Tasks);

    /// <summary>
    /// Catalog-side definition of a Task: behavior-tree node, 1–tens of seconds
    /// (e.g. <c>TravelToTask</c>, <c>BossEncounterTask</c>, <c>PvERotationTask</c>).
    /// Each Task is composed of one or more atomic Actions.
    ///
    /// <see cref="Family"/> matches the Task Family head from
    /// <c>docs/Spec/03_BOTRUNNER.md#catalog-of-task-families</c>
    /// (Travel, Combat, Questing, Dungeoneering, Raid, Bg, Gathering, etc.).
    /// </summary>
    public sealed record TaskDefinition(
        string Name,
        string? Family,
        string? Description,
        IReadOnlyList<ActionDefinition> Actions);

    /// <summary>
    /// Catalog-side definition of an atomic Action: a single
    /// <c>ObjectiveMessage</c> sent over the protobuf wire. <see cref="ObjectiveType"/>
    /// matches the <c>ObjectiveType</c> enum value from
    /// <c>Exports/BotCommLayer/Models/ProtoDef/*.proto</c>
    /// (e.g. <c>TravelTo</c>, <c>CastSpell</c>, <c>Interact</c>, <c>AcceptQuest</c>).
    /// </summary>
    public sealed record ActionDefinition(
        string ObjectiveType,
        string? Description);
}
