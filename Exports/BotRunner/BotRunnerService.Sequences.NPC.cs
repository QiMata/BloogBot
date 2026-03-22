<<<<<<< HEAD
=======
using Serilog;
>>>>>>> cpp_physics_system
using System.Linq;
using Xas.FluentBehaviourTree;

namespace BotRunner
{
    public partial class BotRunnerService
    {
        /// <summary>
        /// Sequence to select a gossip option from an NPC's menu.
        /// </summary>
        /// <param name="selection">The index of the gossip option to select.</param>
        /// <returns>IBehaviourTreeNode that manages selecting a gossip option.</returns>
        private IBehaviourTreeNode BuildSelectGossipSequence(int selection) => new BehaviourTreeBuilder()
            .Sequence("Select Gossip Sequence")
<<<<<<< HEAD
=======
                // Ensure GossipFrame is available (null on BG bot)
                .Condition("GossipFrame Available", time =>
                {
                    if (_objectManager.GossipFrame != null) return true;
                    Log.Warning("[BOT RUNNER] GossipFrame is null — requires FG bot or packet-based path");
                    return false;
                })
>>>>>>> cpp_physics_system
                // Ensure the bot has a valid target with gossip options
                .Condition("Has Valid Gossip Target", time => _objectManager.GossipFrame.IsOpen
                                                            && _objectManager.GossipFrame.Options.Count > 0)

                // Select the gossip option
                .Do("Select Gossip Option", time =>
                {
                    _objectManager.GossipFrame.SelectGossipOption(selection);
                    return BehaviourTreeStatus.Success;
                })
            .End()
            .Build();
        /// <summary>
        /// Sequence to select a taxi node (flight path) for fast travel.
        /// </summary>
        /// <param name="nodeId">The ID of the taxi node to select.</param>
        /// <returns>IBehaviourTreeNode that manages selecting the taxi node.</returns>
        private IBehaviourTreeNode BuildSelectTaxiNodeSequence(int nodeId) => new BehaviourTreeBuilder()
            .Sequence("Select Taxi Node Sequence")
<<<<<<< HEAD
=======
                // Ensure TaxiFrame is available (null on BG bot)
                .Condition("TaxiFrame Available", time =>
                {
                    if (_objectManager.TaxiFrame != null) return true;
                    Log.Warning("[BOT RUNNER] TaxiFrame is null — requires FG bot or packet-based path");
                    return false;
                })
>>>>>>> cpp_physics_system
                // Ensure the bot has access to the selected taxi node
                .Condition("Has Taxi Node Unlocked", time => _objectManager.TaxiFrame.HasNodeUnlocked(nodeId))

                // Ensure the bot has enough gold for the flight
                .Condition("Has Enough Gold", time => _objectManager.Player.Copper > _objectManager.TaxiFrame.Nodes[nodeId].Cost)

                // Select the taxi node
                .Do("Select Taxi Node", time =>
                {
                    _objectManager.TaxiFrame.SelectNode(nodeId);
                    return BehaviourTreeStatus.Success;
                })
            .End()
            .Build();
        /// <summary>
        /// Sequence to accept a quest from an NPC. This checks if the quest is available and the bot meets the prerequisites.
        /// </summary>
        /// <returns>IBehaviourTreeNode that manages accepting the quest.</returns>
        private IBehaviourTreeNode AcceptQuestSequence => new BehaviourTreeBuilder()
            .Sequence("Accept Quest Sequence")
<<<<<<< HEAD
=======
                // Ensure QuestFrame is available (null on BG bot)
                .Condition("QuestFrame Available", time =>
                {
                    if (_objectManager.QuestFrame != null) return true;
                    Log.Warning("[BOT RUNNER] QuestFrame is null — requires FG bot or packet-based path");
                    return false;
                })
>>>>>>> cpp_physics_system
                // Ensure the bot can accept the quest (e.g., meets level requirements)
                .Condition("Can Accept Quest", time => _objectManager.QuestFrame.IsOpen)

                // Accept the quest from the NPC
                .Do("Accept Quest", time =>
                {
                    _objectManager.QuestFrame.AcceptQuest();
                    return BehaviourTreeStatus.Success;
                })
            .End()
            .Build();
        /// <summary>
        /// Sequence to decline a quest offered by an NPC.
        /// </summary>
        /// <returns>IBehaviourTreeNode that manages declining the quest.</returns>
        private IBehaviourTreeNode DeclineQuestSequence => new BehaviourTreeBuilder()
            .Sequence("Decline Quest Sequence")
<<<<<<< HEAD
=======
                .Condition("QuestFrame Available", time =>
                {
                    if (_objectManager.QuestFrame != null) return true;
                    Log.Warning("[BOT RUNNER] QuestFrame is null — requires FG bot or packet-based path");
                    return false;
                })
>>>>>>> cpp_physics_system
                // Ensure the bot can decline the quest
                .Condition("Can Decline Quest", time => _objectManager.QuestFrame.IsOpen)

                // Decline the quest
                .Do("Decline Quest", time =>
                {
                    _objectManager.QuestFrame.DeclineQuest();
                    return BehaviourTreeStatus.Success;
                })
            .End()
            .Build();
        /// <summary>
        /// Sequence to select a reward from a completed quest.
        /// </summary>
        /// <param name="rewardIndex">The index of the reward to select.</param>
        /// <returns>IBehaviourTreeNode that manages selecting the quest reward.</returns>
        private IBehaviourTreeNode BuildSelectRewardSequence(int rewardIndex) => new BehaviourTreeBuilder()
            .Sequence("Select Reward Sequence")
<<<<<<< HEAD
=======
                .Condition("QuestFrame Available", time =>
                {
                    if (_objectManager.QuestFrame != null) return true;
                    Log.Warning("[BOT RUNNER] QuestFrame is null — requires FG bot or packet-based path");
                    return false;
                })
>>>>>>> cpp_physics_system
                // Ensure the bot is able to select a reward
                .Condition("Can Select Reward", time => _objectManager.QuestFrame.IsOpen)

                // Select the specified reward
                .Do("Select Reward", time =>
                {
                    _objectManager.QuestFrame.CompleteQuest(rewardIndex);
                    return BehaviourTreeStatus.Success;
                })
            .End()
            .Build();
        /// <summary>
        /// Sequence to complete a quest and turn it in to an NPC.
        /// </summary>
        /// <returns>IBehaviourTreeNode that manages completing the quest.</returns>
        private IBehaviourTreeNode CompleteQuestSequence => new BehaviourTreeBuilder()
            .Sequence("Complete Quest Sequence")
<<<<<<< HEAD
=======
                .Condition("QuestFrame Available", time =>
                {
                    if (_objectManager.QuestFrame != null) return true;
                    Log.Warning("[BOT RUNNER] QuestFrame is null — requires FG bot or packet-based path");
                    return false;
                })
>>>>>>> cpp_physics_system
                // Ensure the bot can complete the quest
                .Condition("Can Complete Quest", time => _objectManager.QuestFrame.IsOpen)

                // Complete the quest
                .Do("Complete Quest", time =>
                {
                    _objectManager.QuestFrame.CompleteQuest();
                    return BehaviourTreeStatus.Success;
                })
            .End()
            .Build();
        /// <summary>
        /// Sequence to train a specific skill from a trainer NPC.
        /// </summary>
        /// <param name="spellIndex">The index of the skill or spell to train.</param>
        /// <returns>IBehaviourTreeNode that manages training the skill.</returns>
        private IBehaviourTreeNode BuildTrainSkillSequence(int spellIndex) => new BehaviourTreeBuilder()
            .Sequence("Train Skill Sequence")
<<<<<<< HEAD
=======
                // Ensure TrainerFrame is available (null on BG bot)
                .Condition("TrainerFrame Available", time =>
                {
                    if (_objectManager.TrainerFrame != null) return true;
                    Log.Warning("[BOT RUNNER] TrainerFrame is null — requires FG bot or packet-based path");
                    return false;
                })
>>>>>>> cpp_physics_system
                // Ensure the bot is at a trainer NPC
                .Condition("Is At Trainer", time => _objectManager.TrainerFrame.IsOpen)

                // Ensure the bot has enough gold to train the skill
                .Condition("Has Enough Gold", time => _objectManager.Player.Copper > _objectManager.TrainerFrame.Spells.ElementAt(spellIndex).Cost)

                // Train the skill
                .Do("Train Skill", time =>
                {
                    _objectManager.TrainerFrame.TrainSpell(spellIndex);
                    return BehaviourTreeStatus.Success;
                })
            .End()
            .Build();
        /// <summary>
        /// Sequence to train a specific talent. This checks if the bot has enough resources and is eligible to train the talent.
        /// </summary>
        /// <param name="talentSpellId">The ID of the talent spell to train.</param>
        /// <returns>IBehaviourTreeNode that manages training the talent.</returns>
        private IBehaviourTreeNode BuildLearnTalentSequence(int talentSpellId) => new BehaviourTreeBuilder()
            .Sequence("Train Talent Sequence")
<<<<<<< HEAD
=======
                // Ensure TalentFrame is available (null on BG bot)
                .Condition("TalentFrame Available", time =>
                {
                    if (_objectManager.TalentFrame != null) return true;
                    Log.Warning("[BOT RUNNER] TalentFrame is null — requires FG bot or packet-based path");
                    return false;
                })
>>>>>>> cpp_physics_system
                // Ensure the bot is eligible to train the talent
                .Condition("Can Train Talent", time => _objectManager.TalentFrame.TalentPointsAvailable > 1)

                // Train the talent
                .Do("Train Talent", time =>
                {
                    _objectManager.TalentFrame.LearnTalent(talentSpellId);
                    return BehaviourTreeStatus.Success;
                })
            .End()
            .Build();
        private IBehaviourTreeNode BuildBuyItemSequence(int slotId, int quantity) => new BehaviourTreeBuilder()
                .Sequence("BuyItem Sequence")
<<<<<<< HEAD
=======
                    .Condition("MerchantFrame Available", time =>
                    {
                        if (_objectManager.MerchantFrame != null) return true;
                        Log.Warning("[BOT RUNNER] MerchantFrame is null — use vendorGuid-based BuyItem for BG bot");
                        return false;
                    })
>>>>>>> cpp_physics_system
                    .Do("Buy Item", time =>
                    {
                        _objectManager.MerchantFrame.BuyItem(slotId, quantity);
                        return BehaviourTreeStatus.Success;
                    })
                .End()
                .Build();
        private IBehaviourTreeNode BuildBuybackItemSequence(int slotId, int quantity) => new BehaviourTreeBuilder()
                .Sequence("BuybackItem Sequence")
<<<<<<< HEAD
                    .Do("Buy Item", time =>
=======
                    .Condition("MerchantFrame Available", time =>
                    {
                        if (_objectManager.MerchantFrame != null) return true;
                        Log.Warning("[BOT RUNNER] MerchantFrame is null — buyback requires FG bot or vendorGuid path");
                        return false;
                    })
                    .Do("Buyback Item", time =>
>>>>>>> cpp_physics_system
                    {
                        _objectManager.MerchantFrame.BuybackItem(slotId, quantity);
                        return BehaviourTreeStatus.Success;
                    })
                .End()
                .Build();
        private IBehaviourTreeNode BuildSellItemSequence(int bagId, int slotId, int quantity) => new BehaviourTreeBuilder()
                .Sequence("SellItem Sequence")
<<<<<<< HEAD
=======
                    .Condition("MerchantFrame Available", time =>
                    {
                        if (_objectManager.MerchantFrame != null) return true;
                        Log.Warning("[BOT RUNNER] MerchantFrame is null — use vendorGuid-based SellItem for BG bot");
                        return false;
                    })
>>>>>>> cpp_physics_system
                    .Do("Sell Item", time =>
                    {
                        _objectManager.MerchantFrame.SellItem(bagId, slotId, quantity);
                        return BehaviourTreeStatus.Success;
                    })
                .End()
                .Build();
    }
}
