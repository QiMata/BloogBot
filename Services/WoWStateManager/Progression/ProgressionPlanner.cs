using Communication;
using WoWStateManager.Settings;
using Microsoft.Extensions.Logging;
using System.Linq;

namespace WoWStateManager.Progression
{
    public class ProgressionPlanner
    {
        private readonly ILogger<ProgressionPlanner> _logger;

        public ProgressionPlanner(ILogger<ProgressionPlanner> logger)
        {
            _logger = logger;
        }

        /// <summary>
        /// Evaluate all goals and return the highest-priority action, or null if bot should self-direct.
        /// Priority order:
        ///   1. Survival (dead/ghost) — deferred to BotRunner's autonomous death recovery
        ///   2. Level-up training — visit trainer when new spells are available
        ///   3. Gear — farm BiS item from highest-priority gap
        ///   4. Reputation — grind rep for goals below target
        ///   5. Mount — gold farm or purchase at level 40+/60
        ///   6. Gold — grind if below savings target
        ///   7. Profession — train if at tier boundary
        ///   8. Default — null (bot self-directs: grind/quest)
        /// </summary>
        public ActionMessage? GetNextAction(WoWActivitySnapshot snapshot, CharacterBuildConfig? config)
        {
            if (config == null) return null;
            if (snapshot.ConnectionState != BotConnectionState.BotInWorld) return null;
            if (!snapshot.IsObjectManagerValid) return null;
            if (snapshot.IsMapTransition) return null;

            var player = snapshot.Player;
            if (player?.Unit?.GameObject == null) return null;

            // P1: Survival — handled by BotRunner's autonomous death recovery

            // P2: Level-up training — deferred to auto-train on level-up event

            // P3: Gear-driven activity (P22.8)
            if (config.TargetGearSet != null && config.TargetGearSet.Count > 0)
            {
                var highestPriorityGap = config.TargetGearSet
                    .OrderBy(g => g.Priority)
                    .FirstOrDefault(g => !IsGearSlotFilled(player, g));

                if (highestPriorityGap != null)
                {
                    var activity = ResolveGearSource(highestPriorityGap.Source, snapshot);
                    if (activity != null)
                    {
                        _logger.LogDebug("Bot {Account}: gear gap in {Slot} — {Item} from {Source}",
                            snapshot.AccountName, highestPriorityGap.Slot, highestPriorityGap.ItemName, highestPriorityGap.Source);
                        return activity;
                    }
                }
            }

            // P4: Reputation-driven activity (P22.11)
            if (config.ReputationGoals != null)
            {
                foreach (var repGoal in config.ReputationGoals)
                {
                    // Check if reputation is below target using snapshot's reputationStandings
                    if (player.ReputationStandings.TryGetValue((uint)repGoal.FactionId, out var currentRep))
                    {
                        var targetStandingValue = GetStandingThreshold(repGoal.TargetStanding);
                        if (currentRep < targetStandingValue)
                        {
                            var activity = ResolveRepSource(repGoal.GrindMethod, snapshot);
                            if (activity != null)
                            {
                                _logger.LogDebug("Bot {Account}: rep gap for {Faction} ({Current}/{Target})",
                                    snapshot.AccountName, repGoal.FactionName, currentRep, targetStandingValue);
                                return activity;
                            }
                        }
                    }
                }
            }

            // P5: Mount goal (P22.18 gold tracking)
            if (config.MountGoal != null)
            {
                var mountLevel = config.MountGoal.RequiredLevel;
                var playerLevel = player.Unit?.GameObject?.Level ?? 0;
                if (playerLevel >= (uint)mountLevel && player.Coinage < (uint)config.MountGoal.GoldCostCopper)
                {
                    _logger.LogDebug("Bot {Account}: needs {Gold}c for mount (has {Have}c)",
                        snapshot.AccountName, config.MountGoal.GoldCostCopper, player.Coinage);
                    // Gold farming — return null to let bot grind (default behavior earns gold)
                }
            }

            // P6: Gold savings target (P22.18)
            if (config.GoldTargetCopper > 0 && player.Coinage < (uint)config.GoldTargetCopper)
            {
                _logger.LogDebug("Bot {Account}: gold {Current}/{Target}c",
                    snapshot.AccountName, player.Coinage, config.GoldTargetCopper);
                // Let bot grind naturally — no override
            }

            // P7: Profession training (P22.15)
            foreach (var skillTarget in config.SkillPriorities)
            {
                var parts = skillTarget.Split(':');
                if (parts.Length != 2 || !int.TryParse(parts[1], out var targetLevel)) continue;
                var profName = parts[0];

                // Check if at tier boundary (75, 150, 225) needing trainer visit (P22.17)
                var currentLevel = GetProfessionLevel(player, profName);
                if (currentLevel > 0 && currentLevel < targetLevel && IsAtTierBoundary(currentLevel))
                {
                    _logger.LogDebug("Bot {Account}: {Prof} at tier boundary {Level}, needs trainer",
                        snapshot.AccountName, profName, currentLevel);
                    // TODO: Return TravelTo trainer + TrainSkill action
                }
            }

            // P8: Quest chain progress (P22.23)
            if (config.QuestChains.Count > 0)
            {
                // Get completed quest IDs from quest log entries
                // questLog1 contains the quest ID in the low 16 bits
                var completedQuestIds = new System.Collections.Generic.HashSet<uint>();
                if (player.QuestLogEntries != null)
                {
                    foreach (var entry in player.QuestLogEntries)
                    {
                        if (entry.QuestLog1 != 0)
                            completedQuestIds.Add(entry.QuestLog1 & 0xFFFF);
                    }
                }

                foreach (var chainId in config.QuestChains)
                {
                    var chain = BotRunner.Progression.QuestChainData.GetChain(chainId);
                    if (chain == null) continue;

                    // Find the first incomplete quest in the chain
                    foreach (var step in chain.Steps)
                    {
                        if (!completedQuestIds.Contains(step.QuestId))
                        {
                            _logger.LogDebug("Bot {Account}: quest chain '{Chain}' next step: {Quest} ({QuestId})",
                                snapshot.AccountName, chain.DisplayName, step.QuestName, step.QuestId);
                            // TODO: Return TravelTo quest giver + AcceptQuest/CompleteQuest action
                            break;
                        }
                    }
                }
            }

            return null; // No override — bot self-directs (grind/quest in current zone)
        }

        private static bool IsGearSlotFilled(Game.WoWPlayer player, GearGoalEntry goal)
        {
            // Check if the target item ID is in the player's inventory or equipment
            // Simplified: check bagContents for the item ID
            return player.BagContents.Values.Contains((uint)goal.ItemId)
                || player.Inventory.Values.Any(guid => guid != 0); // Placeholder — needs item ID comparison
        }

        private static ActionMessage? ResolveGearSource(string source, WoWActivitySnapshot snapshot)
        {
            // Source format: "Dungeon:StratholmeBaron", "Quest:InMyHour", "Vendor:X", "Craft:X", "AH"
            if (string.IsNullOrEmpty(source)) return null;

            // TODO: Map source to ActionMessage for DungeoneeringTask, QuestingTask, etc.
            // For now, return null (bot self-directs to grind)
            return null;
        }

        private static ActionMessage? ResolveRepSource(string grindMethod, WoWActivitySnapshot snapshot)
        {
            // GrindMethod: "Quests", "Dungeon:Stratholme", "Turnin:RuneclothBandage", "Mob:TimbermawFurbolg"
            if (string.IsNullOrEmpty(grindMethod)) return null;

            // TODO: Map grind method to ActionMessage
            return null;
        }

        private static int GetStandingThreshold(string standing) => standing switch
        {
            "Friendly" => 3000,
            "Honored" => 9000,
            "Revered" => 21000,
            "Exalted" => 42000,
            _ => 0,
        };

        private static int GetProfessionLevel(Game.WoWPlayer player, string professionName)
        {
            // TODO: Map profession name to skill ID and check skillInfo
            // SkillInfo in proto is map<uint32, uint32> — need profession name → ID mapping
            return 0;
        }

        private static bool IsAtTierBoundary(int currentLevel)
            => currentLevel == 75 || currentLevel == 150 || currentLevel == 225;
    }
}
