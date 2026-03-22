using BotRunner.Interfaces;
using GameData.Core.Enums;
using GameData.Core.Interfaces;
using GameData.Core.Models;
using System;
using System.Collections.Generic;
using System.Linq;

namespace BotRunner.Tasks.Questing;

/// <summary>
/// Main quest coordinator task that manages the quest loop.
/// Scans for quest objectives, moves to hotspots, and coordinates sub-tasks.
/// </summary>
public class QuestingTask(IBotContext botContext) : BotTask(botContext), IBotTask
{
    private static readonly Dictionary<int, QuestTaskData> _questCache = new();
    private static Position? _currentHotSpot;

    /// <summary>
    /// Current quest being worked on.
    /// </summary>
    public static string? CurrentQuestName { get; private set; }

    /// <summary>
    /// Current task description for UI display.
    /// </summary>
    public static string? CurrentTask { get; private set; }

    /// <summary>
    /// Blacklist of target GUIDs that should be skipped temporarily.
    /// </summary>
    public static Dictionary<ulong, DateTime> TargetGuidBlacklist { get; } = new();

    public void Update()
    {
        var player = ObjectManager.Player;
        if (player == null || player.Level >= 60) return;

        // Handle combat interruption
        if (player.IsInCombat)
        {
            var aggressor = ObjectManager.Aggressors.FirstOrDefault();
            if (aggressor != null)
            {
                // Push combat task - let the class container handle it
                return;
            }
        }

        // Quest-unit scanning deferred — requires quest objective→unit mapping and NPC filter design (BR-MISS-001)

        // Get remaining quest objectives
        var questObjectives = GetRemainingQuestObjectives().ToList();
        var questGivers = GetNearbyQuestGivers().ToList();

        if (questGivers.Count == 0 && questObjectives.Count != 0)
        {
            // Move to next hotspot for quest objectives
            var position = GetNextPositionHotSpot();
            if (position != null)
            {
                CurrentTask = $"[Questing: {CurrentQuestName}] Moving to X:{position.X:F0}, Y:{position.Y:F0}, Z:{position.Z:F0}";
                BotTasks.Push(new MoveToPositionTask(BotContext, position));
            }
        }
        else if (GetActiveQuests().Any() && questObjectives.Count == 0)
        {
            // All objectives complete, find quest to turn in
            var completedQuest = GetActiveQuests()
                .Where(q => IsQuestComplete(q.QuestId))
                .OrderBy(q => q.TurnInPosition?.DistanceTo(player.Position) ?? float.MaxValue)
                .FirstOrDefault();

            if (completedQuest != null && completedQuest.TurnInPosition != null)
            {
                CurrentQuestName = completedQuest.Name;
                CurrentTask = $"[Questing: {CurrentQuestName}] Returning for turn in";
                BotTasks.Push(new MoveToPositionTask(BotContext, completedQuest.TurnInPosition));
            }
        }
    }

    /// <summary>
    /// Get the closest incomplete quest objective.
    /// </summary>
    public QuestObjectiveData? GetClosestObjective()
    {
        var objectives = GetRemainingQuestObjectives().ToList();
        if (!objectives.Any()) return null;

        var playerPos = ObjectManager.Player?.Position;
        if (playerPos == null) return null;

        return objectives
            .Where(o => o.HotSpots.Any())
            .OrderBy(o => o.HotSpots.Min(h => h.DistanceTo(playerPos)))
            .FirstOrDefault();
    }

    /// <summary>
    /// Get remaining incomplete quest objectives.
    /// </summary>
    public IEnumerable<QuestObjectiveData> GetRemainingQuestObjectives()
    {
        return GetActiveQuests()
            .Where(q => !IsQuestComplete(q.QuestId))
            .SelectMany(q => q.Objectives)
            .Where(o => !o.IsComplete);
    }

    /// <summary>
    /// Get active quests from player's quest log with cached task data.
    /// </summary>
    public IEnumerable<QuestTaskData> GetActiveQuests()
    {
        var questLog = ObjectManager.Player?.QuestLog;
        if (questLog == null) yield break;

        foreach (var slot in questLog.Where(s => s.QuestId > 0))
        {
            var questId = (int)slot.QuestId;

            // Remove cached quests no longer in log
            var staleQuests = _questCache.Keys.Where(k => !questLog.Any(s => s.QuestId == k)).ToList();
            foreach (var stale in staleQuests)
                _questCache.Remove(stale);

            // Get or create quest task data
            if (!_questCache.TryGetValue(questId, out var questData))
            {
                questData = BuildQuestTaskData(questId);
                if (questData != null)
                    _questCache[questId] = questData;
            }

            if (questData != null)
                yield return questData;
        }
    }

    /// <summary>
    /// Build quest task data from database.
    /// </summary>
    private QuestTaskData? BuildQuestTaskData(int questId)
    {
        var repo = Container?.QuestRepository;
        if (repo == null) return null;

        var template = repo.GetQuestTemplateById(questId);
        if (template == null) return null;

        var questData = new QuestTaskData
        {
            QuestId = questId,
            Name = template.Title
        };

        // Get turn-in NPC
        var relatedNpcIds = repo.GetQuestRelatedNpcIds(questId).ToList();
        if (relatedNpcIds.Any())
        {
            var spawns = repo.GetCreatureSpawnsById(relatedNpcIds.First());
            var firstSpawn = spawns.FirstOrDefault();
            if (firstSpawn != null)
            {
                questData.TurnInPosition = new Position(firstSpawn.PositionX, firstSpawn.PositionY, firstSpawn.PositionZ);
            }
        }

        // Build objectives
        BuildObjectives(questData, template, repo);

        return questData;
    }

    private void BuildObjectives(QuestTaskData questData, QuestTemplateData template, IQuestRepository repo)
    {
        // Objective 1
        if (template.ReqCreatureOrGOId1 != 0 || template.ReqItemId1 != 0)
            questData.Objectives.Add(BuildObjective(1, template, repo));

        // Objective 2
        if (template.ReqCreatureOrGOId2 != 0 || template.ReqItemId2 != 0)
            questData.Objectives.Add(BuildObjective(2, template, repo));

        // Objective 3
        if (template.ReqCreatureOrGOId3 != 0 || template.ReqItemId3 != 0)
            questData.Objectives.Add(BuildObjective(3, template, repo));

        // Objective 4
        if (template.ReqCreatureOrGOId4 != 0 || template.ReqItemId4 != 0)
            questData.Objectives.Add(BuildObjective(4, template, repo));
    }

    private QuestObjectiveData BuildObjective(int index, QuestTemplateData template, IQuestRepository repo)
    {
        var objective = new QuestObjectiveData { QuestId = template.Entry, Index = index };

        var (reqCreatureOrGO, reqCount, reqItemId, reqItemCount) = index switch
        {
            1 => (template.ReqCreatureOrGOId1, template.ReqCreatureOrGOCount1, template.ReqItemId1, template.ReqItemCount1),
            2 => (template.ReqCreatureOrGOId2, template.ReqCreatureOrGOCount2, template.ReqItemId2, template.ReqItemCount2),
            3 => (template.ReqCreatureOrGOId3, template.ReqCreatureOrGOCount3, template.ReqItemId3, template.ReqItemCount3),
            4 => (template.ReqCreatureOrGOId4, template.ReqCreatureOrGOCount4, template.ReqItemId4, template.ReqItemCount4),
            _ => (0, 0, 0, 0)
        };

        // Set creature or game object target
        if (reqCreatureOrGO > 0)
        {
            objective.TargetCreatureId = reqCreatureOrGO;
            objective.TargetsNeeded = reqCount;

            // Get spawn positions
            var spawns = repo.GetCreatureSpawnsById(reqCreatureOrGO);
            foreach (var spawn in spawns)
                objective.HotSpots.Add(new Position(spawn.PositionX, spawn.PositionY, spawn.PositionZ));
        }
        else if (reqCreatureOrGO < 0)
        {
            objective.TargetGameObjectId = Math.Abs(reqCreatureOrGO);
            objective.TargetsNeeded = reqCount;

            // Get game object spawn positions
            var spawns = repo.GetGameObjectSpawnsById(Math.Abs(reqCreatureOrGO));
            foreach (var spawn in spawns)
                objective.HotSpots.Add(new Position(spawn.PositionX, spawn.PositionY, spawn.PositionZ));
        }

        // Handle item requirements (loot from creatures or objects)
        if (reqItemId != 0)
        {
            objective.RequiredItemId = reqItemId;
            objective.RequiredItemCount = reqItemCount;

            // Find creatures that drop this item
            var creatures = repo.GetCreaturesByLootableItemId(reqItemId).ToList();
            if (creatures.Any())
            {
                objective.TargetCreatureId = creatures.First().CreatureId;
                foreach (var spawn in creatures)
                    objective.HotSpots.Add(new Position(spawn.PositionX, spawn.PositionY, spawn.PositionZ));
            }
            else
            {
                // Find game objects that contain this item
                var objects = repo.GetGameObjectsByLootableItemId(reqItemId);
                foreach (var spawn in objects)
                {
                    if (objective.TargetGameObjectId == 0)
                        objective.TargetGameObjectId = spawn.GameObjectId;
                    objective.HotSpots.Add(new Position(spawn.PositionX, spawn.PositionY, spawn.PositionZ));
                }
            }
        }

        // Set usable item if quest provides one
        if (template.SrcItemId != 0 && objective.TargetCreatureId > 0)
        {
            objective.UsableItemId = template.SrcItemId;
        }

        return objective;
    }

    /// <summary>
    /// Check if a quest is complete based on player's quest log.
    /// </summary>
    private bool IsQuestComplete(int questId)
    {
        var questLog = ObjectManager.Player?.QuestLog;
        var slot = questLog?.FirstOrDefault(s => s.QuestId == questId);
        if (slot == null) return false;

        // Check quest state flag (complete = 1)
        return (slot.QuestState & 1) == 1;
    }

    /// <summary>
    /// Get NPCs nearby with quest markers.
    /// </summary>
    private IEnumerable<IWoWUnit> GetNearbyQuestGivers()
    {
        var playerPos = ObjectManager.Player?.Position ?? new Position(0, 0, 0);
        return ObjectManager.Units
            .Where(u => u.NpcFlags.HasFlag(NPCFlags.UNIT_NPC_FLAG_QUESTGIVER)
                && u.Health > 0
                && u.Position != null)
            .OrderBy(u => u.Position!.DistanceTo(playerPos));
    }

    /// <summary>
    /// Get the next position to move to for questing.
    /// </summary>
    private Position? GetNextPositionHotSpot()
    {
        var closestObjective = GetClosestObjective();
        if (closestObjective == null) return null;

        var quest = GetActiveQuests().FirstOrDefault(q =>
            q.Objectives.Any(o => o.QuestId == closestObjective.QuestId && o.Index == closestObjective.Index));
        CurrentQuestName = quest?.Name;

        var playerPos = ObjectManager.Player?.Position;
        if (playerPos == null) return null;

        // If current hotspot is not in this objective, get closest
        if (_currentHotSpot == null || !closestObjective.HotSpots.Any(h => h.DistanceTo(_currentHotSpot) < 5))
        {
            _currentHotSpot = closestObjective.HotSpots
                .OrderBy(h => h.DistanceTo(playerPos))
                .FirstOrDefault();
        }
        else if (closestObjective.HotSpots.Count > 1)
        {
            // Cycle through hotspots
            var index = closestObjective.HotSpots.FindIndex(h => h.DistanceTo(_currentHotSpot!) < 5);
            if (index >= closestObjective.HotSpots.Count - 1)
            {
                closestObjective.HotSpots.Reverse();
                _currentHotSpot = closestObjective.HotSpots[0];
            }
            else
            {
                _currentHotSpot = closestObjective.HotSpots[index + 1];
            }
        }

        return _currentHotSpot;
    }
}

/// <summary>
/// Quest task data built from database template.
/// </summary>
public class QuestTaskData
{
    public int QuestId { get; set; }
    public string Name { get; set; } = string.Empty;
    public Position? TurnInPosition { get; set; }
    public List<QuestObjectiveData> Objectives { get; } = new();
}

/// <summary>
/// Quest objective data built from database.
/// </summary>
public class QuestObjectiveData
{
    public int QuestId { get; set; }
    public int Index { get; set; }
    public int TargetCreatureId { get; set; }
    public int TargetGameObjectId { get; set; }
    public int TargetsNeeded { get; set; }
    public int RequiredItemId { get; set; }
    public int RequiredItemCount { get; set; }
    public int UsableItemId { get; set; }
    public List<Position> HotSpots { get; } = new();

    /// <summary>
    /// Check if this objective is complete based on player progress.
    /// </summary>
    public bool IsComplete { get; set; }
}
