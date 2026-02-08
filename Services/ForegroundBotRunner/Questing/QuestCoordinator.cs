using BotRunner.Interfaces;
using ForegroundBotRunner.Mem;
using ForegroundBotRunner.Objects;
using ForegroundBotRunner.Statics;
using GameData.Core.Interfaces;
using GameData.Core.Models;
using Serilog;

namespace ForegroundBotRunner.Questing
{
    /// <summary>
    /// Coordinates quest activities: pickup, objectives, turn-in.
    /// Called from GrindBot during FindTarget phase when questing is enabled.
    ///
    /// Returns quest-driven actions (positions to move to, NPCs to interact with)
    /// that GrindBot can execute through its existing movement and combat systems.
    /// </summary>
    public class QuestCoordinator
    {
        private readonly IQuestRepository _questRepo;
        private readonly ObjectManager _objectManager;
        private List<QuestLogEntry> _cachedQuests = new();
        private int _lastQuestRefreshTick;
        private const int QUEST_REFRESH_INTERVAL_MS = 5000;
        private const int MAX_QUEST_LOG_SIZE = 20;
        private const float NPC_INTERACTION_RANGE = 5f;

        // Current quest action state
        private QuestAction _currentAction = QuestAction.None;
        private WoWUnit? _targetNpc;
        private int _interactionStartTick;
        private int _interactionAttempts;
        private const int INTERACTION_TIMEOUT_MS = 15000;

        public QuestCoordinator(IQuestRepository questRepo, ObjectManager objectManager)
        {
            _questRepo = questRepo;
            _objectManager = objectManager;
        }

        public enum QuestAction
        {
            None,
            MoveToQuestGiver,
            InteractPickup,
            MoveToObjective,
            MoveToTurnIn,
            InteractTurnIn,
        }

        public QuestAction CurrentAction => _currentAction;

        /// <summary>
        /// Evaluate quest state and return recommended action.
        /// Called each tick from GrindBot's FindTarget phase.
        /// Returns a Position to move toward, or null if no quest action needed.
        /// </summary>
        public Position? Update(IWoWLocalPlayer player)
        {
            RefreshQuestLog();

            // Handle ongoing NPC interaction
            if (_currentAction == QuestAction.InteractPickup || _currentAction == QuestAction.InteractTurnIn)
            {
                return HandleInteraction(player);
            }

            // Check for complete quests needing turn-in
            var completeQuest = _cachedQuests.FirstOrDefault(q => q.IsComplete);
            if (completeQuest != null)
            {
                var turnInNpc = FindQuestTurnInNpc(completeQuest, player);
                if (turnInNpc != null)
                {
                    var distance = player.Position.DistanceTo(turnInNpc.Position);
                    if (distance <= NPC_INTERACTION_RANGE)
                    {
                        StartInteraction(turnInNpc, QuestAction.InteractTurnIn);
                        return null;
                    }
                    _currentAction = QuestAction.MoveToTurnIn;
                    return turnInNpc.Position;
                }
            }

            // Check for available quest givers (if we have room in log)
            if (_cachedQuests.Count < MAX_QUEST_LOG_SIZE)
            {
                var questGiver = FindNearbyQuestGiver(player);
                if (questGiver != null)
                {
                    var distance = player.Position.DistanceTo(questGiver.Position);
                    if (distance <= NPC_INTERACTION_RANGE)
                    {
                        StartInteraction(questGiver, QuestAction.InteractPickup);
                        return null;
                    }
                    _currentAction = QuestAction.MoveToQuestGiver;
                    return questGiver.Position;
                }
            }

            // Find objectives for incomplete quests
            foreach (var quest in _cachedQuests.Where(q => !q.IsComplete))
            {
                var objectivePos = FindQuestObjectivePosition(quest, player);
                if (objectivePos != null)
                {
                    _currentAction = QuestAction.MoveToObjective;
                    return objectivePos;
                }
            }

            _currentAction = QuestAction.None;
            return null;
        }

        /// <summary>
        /// Reset interaction state (e.g., on phase change or target loss).
        /// </summary>
        public void Reset()
        {
            _currentAction = QuestAction.None;
            _targetNpc = null;
            _interactionAttempts = 0;
        }

        private void RefreshQuestLog()
        {
            if (Environment.TickCount - _lastQuestRefreshTick < QUEST_REFRESH_INTERVAL_MS)
                return;

            _lastQuestRefreshTick = Environment.TickCount;
            _cachedQuests = QuestLog.GetActiveQuests();
        }

        private Position? HandleInteraction(IWoWLocalPlayer player)
        {
            if (_targetNpc == null || _targetNpc.Health == 0)
            {
                Reset();
                return null;
            }

            // Timeout
            if (Environment.TickCount - _interactionStartTick > INTERACTION_TIMEOUT_MS)
            {
                Log.Warning("[QuestCoord] Interaction timeout, resetting");
                QuestNpcInteraction.CloseFrames();
                Reset();
                return null;
            }

            // Move closer if needed
            var distance = player.Position.DistanceTo(_targetNpc.Position);
            if (distance > NPC_INTERACTION_RANGE)
            {
                return _targetNpc.Position;
            }

            // Stop moving for interaction
            _objectManager.StopAllMovement();
            _objectManager.Face(_targetNpc.Position);

            // First attempt: right-click NPC to open gossip/quest frame
            if (_interactionAttempts == 0)
            {
                ThreadSynchronizer.RunOnMainThread(() =>
                {
                    ((WoWObject)_targetNpc).Interact();
                });
                _interactionAttempts++;
                return null;
            }

            // Subsequent attempts: click through quest frames
            if (_currentAction == QuestAction.InteractPickup)
            {
                if (QuestNpcInteraction.TryAcceptQuest())
                {
                    Log.Information("[QuestCoord] Quest accepted");
                    QuestNpcInteraction.CloseFrames();
                    _lastQuestRefreshTick = 0; // Force quest log refresh
                    Reset();
                }
                else if (!QuestNpcInteraction.IsQuestFrameVisible() && _interactionAttempts > 3)
                {
                    // Frame never opened, retry interaction
                    _interactionAttempts = 0;
                }
            }
            else if (_currentAction == QuestAction.InteractTurnIn)
            {
                if (QuestNpcInteraction.TryTurnInQuest())
                {
                    Log.Information("[QuestCoord] Quest turned in");
                    QuestNpcInteraction.CloseFrames();
                    _lastQuestRefreshTick = 0; // Force quest log refresh
                    Reset();
                }
                else if (!QuestNpcInteraction.IsQuestFrameVisible() && _interactionAttempts > 3)
                {
                    _interactionAttempts = 0;
                }
            }

            _interactionAttempts++;
            return null;
        }

        private void StartInteraction(WoWUnit npc, QuestAction action)
        {
            _targetNpc = npc;
            _currentAction = action;
            _interactionStartTick = Environment.TickCount;
            _interactionAttempts = 0;
            Log.Debug("[QuestCoord] Starting {Action} with {NpcName}", action, npc.Name);
        }

        /// <summary>
        /// Find nearby NPCs with quest turn-in markers (yellow ?) for a complete quest.
        /// </summary>
        private WoWUnit? FindQuestTurnInNpc(QuestLogEntry quest, IWoWLocalPlayer player)
        {
            // Get NPC IDs from database
            var npcIds = _questRepo.GetQuestRelatedNpcIds(quest.QuestId).ToHashSet();
            if (npcIds.Count == 0) return null;

            // Search object manager for matching units
            try
            {
                return _objectManager.Units
                    .OfType<WoWUnit>()
                    .Where(u => u.Health > 0 && npcIds.Contains((int)u.Entry))
                    .OrderBy(u => u.Position.DistanceTo(player.Position))
                    .FirstOrDefault();
            }
            catch { return null; }
        }

        /// <summary>
        /// Find nearby NPCs with quest giver markers (yellow !).
        /// Uses NPC flags to detect quest givers without database lookup.
        /// </summary>
        private WoWUnit? FindNearbyQuestGiver(IWoWLocalPlayer player)
        {
            try
            {
                // Look for NPCs with quest giver markers within a reasonable range
                return _objectManager.Units
                    .OfType<WoWUnit>()
                    .Where(u => u.Health > 0 &&
                               (u.NpcFlags & GameData.Core.Enums.NPCFlags.UNIT_NPC_FLAG_QUESTGIVER) != 0 &&
                               u.Position.DistanceTo(player.Position) <= 30f)
                    .OrderBy(u => u.Position.DistanceTo(player.Position))
                    .FirstOrDefault();
            }
            catch { return null; }
        }

        /// <summary>
        /// Find the nearest position for an incomplete quest objective.
        /// Uses the database to look up creature/object spawn locations.
        /// </summary>
        private Position? FindQuestObjectivePosition(QuestLogEntry quest, IWoWLocalPlayer player)
        {
            var template = _questRepo.GetQuestTemplateById(quest.QuestId);
            if (template == null) return null;

            var mapId = _objectManager.ContinentId;

            // Check kill objectives
            var reqCreatureIds = new[] { template.ReqCreatureOrGOId1, template.ReqCreatureOrGOId2,
                                         template.ReqCreatureOrGOId3, template.ReqCreatureOrGOId4 };

            foreach (var creatureId in reqCreatureIds.Where(id => id > 0))
            {
                // First check if the creature is visible in the object manager
                var visibleUnit = _objectManager.Units
                    .OfType<WoWUnit>()
                    .Where(u => u.Health > 0 && u.Entry == (uint)creatureId)
                    .OrderBy(u => u.Position.DistanceTo(player.Position))
                    .FirstOrDefault();

                if (visibleUnit != null)
                    return visibleUnit.Position;

                // Fall back to database spawn locations
                var spawns = _questRepo.GetCreatureSpawnsById(creatureId)
                    .Where(s => s.MapId == mapId);
                var closest = spawns.OrderBy(s =>
                    player.Position.DistanceTo(new Position(s.PositionX, s.PositionY, s.PositionZ)))
                    .FirstOrDefault();
                if (closest != null)
                    return new Position(closest.PositionX, closest.PositionY, closest.PositionZ);
            }

            // Check item objectives (need to find creatures that drop the items)
            var reqItemIds = new[] { template.ReqItemId1, template.ReqItemId2,
                                     template.ReqItemId3, template.ReqItemId4 };

            foreach (var itemId in reqItemIds.Where(id => id > 0))
            {
                var creatureSpawns = _questRepo.GetCreaturesByLootableItemId(itemId)
                    .Where(s => s.MapId == mapId);
                var closest = creatureSpawns.OrderBy(s =>
                    player.Position.DistanceTo(new Position(s.PositionX, s.PositionY, s.PositionZ)))
                    .FirstOrDefault();
                if (closest != null)
                    return new Position(closest.PositionX, closest.PositionY, closest.PositionZ);

                // Also check game objects
                var goSpawns = _questRepo.GetGameObjectsByLootableItemId(itemId)
                    .Where(s => s.MapId == mapId);
                var closestGo = goSpawns.OrderBy(s =>
                    player.Position.DistanceTo(new Position(s.PositionX, s.PositionY, s.PositionZ)))
                    .FirstOrDefault();
                if (closestGo != null)
                    return new Position(closestGo.PositionX, closestGo.PositionY, closestGo.PositionZ);
            }

            // Check negative IDs (game object objectives)
            foreach (var goId in reqCreatureIds.Where(id => id < 0).Select(id => -id))
            {
                var spawns = _questRepo.GetGameObjectSpawnsById(goId)
                    .Where(s => s.MapId == mapId);
                var closest = spawns.OrderBy(s =>
                    player.Position.DistanceTo(new Position(s.PositionX, s.PositionY, s.PositionZ)))
                    .FirstOrDefault();
                if (closest != null)
                    return new Position(closest.PositionX, closest.PositionY, closest.PositionZ);
            }

            return null;
        }
    }
}
