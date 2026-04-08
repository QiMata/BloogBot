using GameData.Core.Constants;
using GameData.Core.Enums;
using GameData.Core.Interfaces;
using GameData.Core.Models;
using Serilog; // TODO: migrate to ILogger when DI is available
using System;
using System.Collections.Generic;
using System.Linq;
using WoWSharpClient.Networking.ClientComponents.I;
using Xas.FluentBehaviourTree;

namespace BotRunner.SequenceBuilders
{
    /// <summary>
    /// Builds interaction-related behavior tree sequences: NPC, Trade, Party, Login, Inventory.
    /// Combines small sequence partials that were each under 200 LOC.
    /// </summary>
    internal sealed class InteractionSequenceBuilder
    {
        private readonly IObjectManager _objectManager;
        private readonly Func<IAgentFactory?>? _agentFactoryAccessor;

        internal InteractionSequenceBuilder(
            IObjectManager objectManager,
            Func<IAgentFactory?>? agentFactoryAccessor)
        {
            _objectManager = objectManager ?? throw new ArgumentNullException(nameof(objectManager));
            _agentFactoryAccessor = agentFactoryAccessor;
        }

        // =====================================================================
        // NPC sequences (from BotRunnerService.Sequences.NPC.cs)
        // =====================================================================

        internal IBehaviourTreeNode BuildSelectGossipSequence(int selection) => new BehaviourTreeBuilder()
            .Sequence("Select Gossip Sequence")
                .Condition("GossipFrame Available", time =>
                {
                    if (_objectManager.GossipFrame != null) return true;
                    Log.Warning("[BOT RUNNER] GossipFrame is null -- requires FG bot or packet-based path");
                    return false;
                })
                .Condition("Has Valid Gossip Target", time => _objectManager.GossipFrame.IsOpen
                                                            && _objectManager.GossipFrame.Options.Count > 0)
                .Do("Select Gossip Option", time =>
                {
                    _objectManager.GossipFrame.SelectGossipOption(selection);
                    return BehaviourTreeStatus.Success;
                })
            .End()
            .Build();

        internal IBehaviourTreeNode BuildSelectTaxiNodeSequence(int nodeId) => new BehaviourTreeBuilder()
            .Sequence("Select Taxi Node Sequence")
                .Condition("TaxiFrame Available", time =>
                {
                    if (_objectManager.TaxiFrame != null) return true;
                    Log.Warning("[BOT RUNNER] TaxiFrame is null -- requires FG bot or packet-based path");
                    return false;
                })
                .Condition("Has Taxi Node Unlocked", time => _objectManager.TaxiFrame.HasNodeUnlocked(nodeId))
                .Condition("Has Enough Gold", time => _objectManager.Player.Copper > _objectManager.TaxiFrame.Nodes[nodeId].Cost)
                .Do("Select Taxi Node", time =>
                {
                    _objectManager.TaxiFrame.SelectNode(nodeId);
                    return BehaviourTreeStatus.Success;
                })
            .End()
            .Build();

        internal IBehaviourTreeNode AcceptQuestSequence => new BehaviourTreeBuilder()
            .Sequence("Accept Quest Sequence")
                .Condition("QuestFrame Available", time =>
                {
                    if (_objectManager.QuestFrame != null) return true;
                    Log.Warning("[BOT RUNNER] QuestFrame is null -- requires FG bot or packet-based path");
                    return false;
                })
                .Condition("Can Accept Quest", time => _objectManager.QuestFrame.IsOpen)
                .Do("Accept Quest", time =>
                {
                    _objectManager.QuestFrame.AcceptQuest();
                    return BehaviourTreeStatus.Success;
                })
            .End()
            .Build();

        internal IBehaviourTreeNode DeclineQuestSequence => new BehaviourTreeBuilder()
            .Sequence("Decline Quest Sequence")
                .Condition("QuestFrame Available", time =>
                {
                    if (_objectManager.QuestFrame != null) return true;
                    Log.Warning("[BOT RUNNER] QuestFrame is null -- requires FG bot or packet-based path");
                    return false;
                })
                .Condition("Can Decline Quest", time => _objectManager.QuestFrame.IsOpen)
                .Do("Decline Quest", time =>
                {
                    _objectManager.QuestFrame.DeclineQuest();
                    return BehaviourTreeStatus.Success;
                })
            .End()
            .Build();

        internal IBehaviourTreeNode BuildSelectRewardSequence(int rewardIndex) => new BehaviourTreeBuilder()
            .Sequence("Select Reward Sequence")
                .Condition("QuestFrame Available", time =>
                {
                    if (_objectManager.QuestFrame != null) return true;
                    Log.Warning("[BOT RUNNER] QuestFrame is null -- requires FG bot or packet-based path");
                    return false;
                })
                .Condition("Can Select Reward", time => _objectManager.QuestFrame.IsOpen)
                .Do("Select Reward", time =>
                {
                    _objectManager.QuestFrame.CompleteQuest(rewardIndex);
                    return BehaviourTreeStatus.Success;
                })
            .End()
            .Build();

        internal IBehaviourTreeNode CompleteQuestSequence => new BehaviourTreeBuilder()
            .Sequence("Complete Quest Sequence")
                .Condition("QuestFrame Available", time =>
                {
                    if (_objectManager.QuestFrame != null) return true;
                    Log.Warning("[BOT RUNNER] QuestFrame is null -- requires FG bot or packet-based path");
                    return false;
                })
                .Condition("Can Complete Quest", time => _objectManager.QuestFrame.IsOpen)
                .Do("Complete Quest", time =>
                {
                    _objectManager.QuestFrame.CompleteQuest();
                    return BehaviourTreeStatus.Success;
                })
            .End()
            .Build();

        internal IBehaviourTreeNode BuildTrainSkillSequence(int spellIndex) => new BehaviourTreeBuilder()
            .Sequence("Train Skill Sequence")
                .Condition("TrainerFrame Available", time =>
                {
                    if (_objectManager.TrainerFrame != null) return true;
                    Log.Warning("[BOT RUNNER] TrainerFrame is null -- requires FG bot or packet-based path");
                    return false;
                })
                .Condition("Is At Trainer", time => _objectManager.TrainerFrame.IsOpen)
                .Condition("Has Enough Gold", time => _objectManager.Player.Copper > _objectManager.TrainerFrame.Spells.ElementAt(spellIndex).Cost)
                .Do("Train Skill", time =>
                {
                    _objectManager.TrainerFrame.TrainSpell(spellIndex);
                    return BehaviourTreeStatus.Success;
                })
            .End()
            .Build();

        internal IBehaviourTreeNode BuildLearnTalentSequence(int talentSpellId) => new BehaviourTreeBuilder()
            .Sequence("Train Talent Sequence")
                .Condition("TalentFrame Available", time =>
                {
                    if (_objectManager.TalentFrame != null) return true;
                    Log.Warning("[BOT RUNNER] TalentFrame is null -- requires FG bot or packet-based path");
                    return false;
                })
                .Condition("Can Train Talent", time => _objectManager.TalentFrame.TalentPointsAvailable > 1)
                .Do("Train Talent", time =>
                {
                    _objectManager.TalentFrame.LearnTalent(talentSpellId);
                    return BehaviourTreeStatus.Success;
                })
            .End()
            .Build();

        internal IBehaviourTreeNode BuildBuyItemSequence(int slotId, int quantity) => new BehaviourTreeBuilder()
                .Sequence("BuyItem Sequence")
                    .Condition("MerchantFrame Available", time =>
                    {
                        if (_objectManager.MerchantFrame != null) return true;
                        Log.Warning("[BOT RUNNER] MerchantFrame is null -- use vendorGuid-based BuyItem for BG bot");
                        return false;
                    })
                    .Do("Buy Item", time =>
                    {
                        _objectManager.MerchantFrame.BuyItem(slotId, quantity);
                        return BehaviourTreeStatus.Success;
                    })
                .End()
                .Build();

        internal IBehaviourTreeNode BuildBuybackItemSequence(int slotId, int quantity) => new BehaviourTreeBuilder()
                .Sequence("BuybackItem Sequence")
                    .Condition("MerchantFrame Available", time =>
                    {
                        if (_objectManager.MerchantFrame != null) return true;
                        Log.Warning("[BOT RUNNER] MerchantFrame is null -- buyback requires FG bot or vendorGuid path");
                        return false;
                    })
                    .Do("Buyback Item", time =>
                    {
                        _objectManager.MerchantFrame.BuybackItem(slotId, quantity);
                        return BehaviourTreeStatus.Success;
                    })
                .End()
                .Build();

        internal IBehaviourTreeNode BuildSellItemSequence(int bagId, int slotId, int quantity) => new BehaviourTreeBuilder()
                .Sequence("SellItem Sequence")
                    .Condition("MerchantFrame Available", time =>
                    {
                        if (_objectManager.MerchantFrame != null) return true;
                        Log.Warning("[BOT RUNNER] MerchantFrame is null -- use vendorGuid-based SellItem for BG bot");
                        return false;
                    })
                    .Do("Sell Item", time =>
                    {
                        _objectManager.MerchantFrame.SellItem(bagId, slotId, quantity);
                        return BehaviourTreeStatus.Success;
                    })
                .End()
                .Build();

        // =====================================================================
        // Inventory sequences (from BotRunnerService.Sequences.Inventory.cs)
        // =====================================================================

        internal IBehaviourTreeNode BuildUseItemSequence(int fromBag, int fromSlot, ulong targetGuid) => new BehaviourTreeBuilder()
            .Sequence("Use Item Sequence")
                .Condition("Has Item", time => _objectManager.GetContainedItem(fromBag, fromSlot) != null)
                .Do("Use Item", time =>
                {
                    _objectManager.UseItem(fromBag, fromSlot, targetGuid);
                    return BehaviourTreeStatus.Success;
                })
            .End()
            .Build();

        internal IBehaviourTreeNode BuildUseItemByIdSequence(int itemId)
        {
            return new BehaviourTreeBuilder()
                .Sequence("Use Item By ID")
                    .Do("Find and Use Item", time =>
                    {
                        // Search all bag slots for the item by ID
                        for (int bag = 0; bag <= 4; bag++)
                        {
                            int maxSlot = bag == 0 ? 16 : 36;
                            for (int slot = 0; slot < maxSlot; slot++)
                            {
                                var contained = _objectManager.GetContainedItem(bag, slot);
                                if (contained != null && contained.ItemId == (uint)itemId)
                                {
                                    Log.Information("[BOT RUNNER] Found item {ItemId} at bag={Bag}, slot={Slot}. Using.", itemId, bag, slot);
                                    _objectManager.UseItem(bag, slot, 0);
                                    return BehaviourTreeStatus.Success;
                                }
                            }
                        }

                        // Fallback: brute-force use all backpack slots (server ignores invalid)
                        Log.Warning("[BOT RUNNER] Item {ItemId} not found in tracked inventory. Trying brute-force use for all backpack slots.", itemId);
                        for (int slot = 0; slot < 16; slot++)
                            _objectManager.UseItem(0, slot, 0);
                        return BehaviourTreeStatus.Success;
                    })
                .End()
                .Build();
        }

        internal IBehaviourTreeNode BuildMoveItemSequence(int fromBag, int fromSlot, int quantity, int toBag, int toSlot) => new BehaviourTreeBuilder()
            .Sequence("Move Item Sequence")
                .Condition("Has Item to Move", time => _objectManager.GetContainedItem(fromBag, fromSlot).Quantity >= quantity)
                .Do("Move Item", time =>
                {
                    _objectManager.PickupContainedItem(fromBag, fromSlot, quantity);
                    _objectManager.PlaceItemInContainer(toBag, toSlot);
                    return BehaviourTreeStatus.Success;
                })
            .End()
            .Build();

        internal IBehaviourTreeNode BuildDestroyItemSequence(int bagId, int slotId, int quantity) => new BehaviourTreeBuilder()
            .Sequence("Destroy Item Sequence")
                .Do("Destroy Item", time =>
                {
                    Log.Information("[BOT RUNNER] DestroyItem: bag={Bag}, slot={Slot}, qty={Qty}", bagId, slotId, quantity);
                    _objectManager.DestroyItemInContainer(bagId, slotId, quantity);
                    return BehaviourTreeStatus.Success;
                })
            .End()
            .Build();

        internal IBehaviourTreeNode BuildEquipItemByIdSequence(int itemId)
        {
            var allItems = _objectManager.GetContainedItems().ToList();
            var allObjects = _objectManager.Objects.ToList();
            var objectsByType = allObjects.GroupBy(o => o.ObjectType)
                .Select(g => $"{g.Key}={g.Count()}")
                .ToList();
            Log.Information("[BOT RUNNER] BuildEquipItemByIdSequence: itemId={ItemId}, containedItems={Count}, itemIds=[{Items}], totalObjects={Total}, byType=[{Types}]",
                itemId, allItems.Count, string.Join(",", allItems.Select(i => i.ItemId)),
                allObjects.Count, string.Join(",", objectsByType));
            return new BehaviourTreeBuilder()
                .Sequence("Equip Item By ID")
                    .Do("Find and Equip Item", time =>
                    {
                        // Fast path: find item by ID in tracked inventory
                        foreach (var item in _objectManager.GetContainedItems())
                        {
                            if (item.ItemId == (uint)itemId)
                            {
                                for (int bag = 0; bag <= 4; bag++)
                                {
                                    int maxSlot = bag == 0 ? 16 : 36;
                                    for (int slot = 0; slot < maxSlot; slot++)
                                    {
                                        var contained = _objectManager.GetContainedItem(bag, slot);
                                        if (contained != null && contained.ItemId == (uint)itemId)
                                        {
                                            Log.Information("[BOT RUNNER] Found item {ItemId} at bag={Bag}, slot={Slot}. Equipping.", itemId, bag, slot);
                                            _objectManager.EquipItem(bag, slot);
                                            return BehaviourTreeStatus.Success;
                                        }
                                    }
                                }
                            }
                        }

                        // Fallback: item not tracked in ObjectManager (e.g., added via GM command).
                        Log.Warning("[BOT RUNNER] Item {ItemId} not found in tracked inventory. Trying brute-force equip for all backpack slots.", itemId);
                        for (int slot = 0; slot < 16; slot++)
                            _objectManager.EquipItem(0, slot);
                        return BehaviourTreeStatus.Success;
                    })
                .End()
                .Build();
        }

        internal IBehaviourTreeNode BuildEquipItemSequence(int bag, int slot) => new BehaviourTreeBuilder()
            .Sequence("Equip Item Sequence")
                .Condition("Has Item", time => _objectManager.GetContainedItem(bag, slot) != null)
                .Do("Equip Item", time =>
                {
                    _objectManager.EquipItem(bag, slot);
                    return BehaviourTreeStatus.Success;
                })
            .End()
            .Build();

        internal IBehaviourTreeNode BuildEquipItemSequence(int bag, int slot, EquipSlot equipSlot) => new BehaviourTreeBuilder()
            .Sequence("Equip Item Sequence")
                .Condition("Has Item", time => _objectManager.GetContainedItem(bag, slot) != null)
                .Do("Equip Item", time =>
                {
                    _objectManager.EquipItem(bag, slot, equipSlot);
                    return BehaviourTreeStatus.Success;
                })
            .End()
            .Build();

        internal IBehaviourTreeNode BuildUnequipItemSequence(EquipSlot equipSlot) => new BehaviourTreeBuilder()
            .Sequence("Unequip Item Sequence")
                .Condition("Has Item Equipped", time => _objectManager.GetEquippedItem(equipSlot) != null)
                .Do("Unequip Item", time =>
                {
                    _objectManager.UnequipItem(equipSlot);
                    return BehaviourTreeStatus.Success;
                })
            .End()
            .Build();

        internal IBehaviourTreeNode BuildSplitStackSequence(int bag, int slot, int quantity, int destinationBag, int destinationSlot) => new BehaviourTreeBuilder()
            .Sequence("Split Stack Sequence")
                .Condition("Has Item Stack", time => _objectManager.GetContainedItem(bag, slot).Quantity >= quantity)
                .Do("Split Stack", time =>
                {
                    _objectManager.SplitStack(bag, slot, quantity, destinationBag, destinationSlot);
                    return BehaviourTreeStatus.Success;
                })
            .End()
            .Build();

        internal IBehaviourTreeNode BuildRepairItemSequence(int repairSlot) => new BehaviourTreeBuilder()
            .Sequence("Repair Item Sequence")
                .Condition("MerchantFrame Available", time =>
                {
                    if (_objectManager.MerchantFrame != null) return true;
                    Log.Warning("[BOT RUNNER] MerchantFrame is null -- use vendorGuid-based RepairItem for BG bot");
                    return false;
                })
                .Condition("Can Afford Repair", time => _objectManager.Player.Copper > _objectManager.MerchantFrame.RepairCost((EquipSlot)repairSlot))
                .Do("Repair Item", time =>
                {
                    _objectManager.MerchantFrame.RepairByEquipSlot((EquipSlot)repairSlot);
                    return BehaviourTreeStatus.Success;
                })
            .End()
            .Build();

        internal IBehaviourTreeNode RepairAllItemsSequence => new BehaviourTreeBuilder()
            .Sequence("Repair All Items Sequence")
                .Condition("MerchantFrame Available", time =>
                {
                    if (_objectManager.MerchantFrame != null) return true;
                    Log.Warning("[BOT RUNNER] MerchantFrame is null -- use vendorGuid-based RepairAllItems for BG bot");
                    return false;
                })
                .Condition("Can Afford Full Repair", time => _objectManager.Player.Copper > _objectManager.MerchantFrame.TotalRepairCost)
                .Do("Repair All Items", time =>
                {
                    _objectManager.MerchantFrame.RepairAll();
                    return BehaviourTreeStatus.Success;
                })
            .End()
            .Build();

        internal IBehaviourTreeNode BuildDismissBuffSequence(string buff) => new BehaviourTreeBuilder()
            .Sequence("Dismiss Buff Sequence")
                .Condition("Has Buff", time => _objectManager.Player.HasBuff(buff))
                .Do("Dismiss Buff", time =>
                {
                    _objectManager.Player.DismissBuff(buff);
                    return BehaviourTreeStatus.Success;
                })
            .End()
            .Build();

        internal IBehaviourTreeNode BuildCraftSequence(int craftSlotId) => new BehaviourTreeBuilder()
            .Sequence("Craft Sequence")
                .Condition("CraftFrame Available", time =>
                {
                    if (_objectManager.CraftFrame != null) return true;
                    Log.Warning("[BOT RUNNER] CraftFrame is null -- requires FG bot or packet-based path");
                    return false;
                })
                .Condition("Can Craft Item", time => _objectManager.CraftFrame.HasMaterialsNeeded(craftSlotId))
                .Do("Craft Item", time =>
                {
                    _objectManager.CraftFrame.Craft(craftSlotId);
                    return BehaviourTreeStatus.Success;
                })
            .End()
            .Build();

        // =====================================================================
        // Trade sequences (from BotRunnerService.Sequences.Trade.cs)
        // =====================================================================

        internal IBehaviourTreeNode BuildOfferTradeSequence(ulong targetGuid) => new BehaviourTreeBuilder()
            .Sequence("Offer Trade Sequence")
                .Condition("Has Valid Trade Target", time => _objectManager.Player.Position.DistanceTo(_objectManager.Players.First(x => x.Guid == targetGuid).Position) < CombatDistance.INTERACTION_DISTANCE + CombatDistance.DEFAULT_PLAYER_BOUNDING_RADIUS)
                .Do("Offer Trade", time =>
                {
                    _objectManager.Players.First(x => x.Guid == targetGuid).OfferTrade();
                    return BehaviourTreeStatus.Success;
                })
            .End()
            .Build();

        internal IBehaviourTreeNode BuildOfferMoneySequence(int copperCount) => new BehaviourTreeBuilder()
            .Sequence("Offer Money Sequence")
                .Condition("TradeFrame Available", time =>
                {
                    if (_objectManager.TradeFrame != null) return true;
                    Log.Warning("[BOT RUNNER] TradeFrame is null -- requires FG bot or packet-based trade path");
                    return false;
                })
                .Condition("Trade Window Valid", time => _objectManager.TradeFrame.IsOpen)
                .Condition("Has Enough Money", time => _objectManager.Player.Copper > copperCount)
                .Do("Offer Money", time =>
                {
                    _objectManager.TradeFrame.OfferMoney(copperCount);
                    return BehaviourTreeStatus.Success;
                })
            .End()
            .Build();

        internal IBehaviourTreeNode BuildOfferItemSequence(int bagId, int slotId, int quantity, int tradeWindowSlot) => new BehaviourTreeBuilder()
            .Sequence("Offer Item Sequence")
                .Condition("TradeFrame Available", time =>
                {
                    if (_objectManager.TradeFrame != null) return true;
                    Log.Warning("[BOT RUNNER] TradeFrame is null -- requires FG bot or packet-based trade path");
                    return false;
                })
                .Condition("Trade Window Valid", time => _objectManager.TradeFrame.IsOpen)
                .Condition("Has Item to Offer", time => _objectManager.GetContainedItem(bagId, slotId).Quantity >= quantity)
                .Do("Offer Item", time =>
                {
                    _objectManager.TradeFrame.OfferItem(bagId, slotId, quantity, tradeWindowSlot);
                    return BehaviourTreeStatus.Success;
                })
            .End()
            .Build();

        internal IBehaviourTreeNode AcceptTradeSequence => new BehaviourTreeBuilder()
            .Sequence("Accept Trade Sequence")
                .Condition("TradeFrame Available", time =>
                {
                    if (_objectManager.TradeFrame != null) return true;
                    Log.Warning("[BOT RUNNER] TradeFrame is null -- requires FG bot or packet-based trade path");
                    return false;
                })
                .Condition("Trade Window Valid", time => _objectManager.TradeFrame.IsOpen)
                .Do("Accept Trade", time =>
                {
                    _objectManager.TradeFrame.AcceptTrade();
                    return BehaviourTreeStatus.Success;
                })
            .End()
            .Build();

        internal IBehaviourTreeNode DeclineTradeSequence => new BehaviourTreeBuilder()
            .Sequence("Decline Trade Sequence")
                .Condition("TradeFrame Available", time =>
                {
                    if (_objectManager.TradeFrame != null) return true;
                    Log.Warning("[BOT RUNNER] TradeFrame is null -- requires FG bot or packet-based trade path");
                    return false;
                })
                .Condition("Trade Window Valid", time => _objectManager.TradeFrame.IsOpen)
                .Do("Decline Trade", time =>
                {
                    _objectManager.TradeFrame.DeclineTrade();
                    return BehaviourTreeStatus.Success;
                })
            .End()
            .Build();

        internal IBehaviourTreeNode BuildOfferEnchantSequence(int enchantId) => new BehaviourTreeBuilder()
            .Sequence("Offer Enchant Sequence")
                .Condition("TradeFrame Available", time =>
                {
                    if (_objectManager.TradeFrame != null) return true;
                    Log.Warning("[BOT RUNNER] TradeFrame is null -- requires FG bot or packet-based trade path");
                    return false;
                })
                .Condition("Trade Window Valid", time => _objectManager.TradeFrame.IsOpen)
                .Do("Offer Enchant", time =>
                {
                    _objectManager.TradeFrame.OfferEnchant(enchantId);
                    return BehaviourTreeStatus.Success;
                })
            .End()
            .Build();

        internal IBehaviourTreeNode OfferLockpickSequence => new BehaviourTreeBuilder()
            .Sequence("Lockpick Trade Sequence")
                .Condition("TradeFrame Available", time =>
                {
                    if (_objectManager.TradeFrame != null) return true;
                    Log.Warning("[BOT RUNNER] TradeFrame is null -- requires FG bot or packet-based trade path");
                    return false;
                })
                .Condition("Can Lockpick", time => _objectManager.Player.Class == Class.Rogue)
                .Do("Offer Lockpick", time =>
                {
                    _objectManager.TradeFrame.OfferLockpick();
                    return BehaviourTreeStatus.Success;
                })
            .End()
            .Build();

        // =====================================================================
        // Party sequences (from BotRunnerService.Sequences.Party.cs)
        // =====================================================================

        internal IBehaviourTreeNode BuildPromoteLeaderSequence(ulong playerGuid) => new BehaviourTreeBuilder()
            .Sequence("Promote Leader Sequence")
                .Condition("Is In Group with Player", time => _objectManager.PartyMembers.Any(x => x.Guid == playerGuid))
                .Do("Promote Leader", time =>
                {
                    _objectManager.PromoteLeader(playerGuid);
                    return BehaviourTreeStatus.Success;
                })
            .End()
            .Build();

        internal IBehaviourTreeNode BuildPromoteAssistantSequence(ulong playerGuid) => new BehaviourTreeBuilder()
            .Sequence("Promote Assistant Sequence")
                .Condition("Is In Group with Player", time => _objectManager.PartyMembers.Any(x => x.Guid == playerGuid))
                .Do("Promote Assistant", time =>
                {
                    _objectManager.PromoteAssistant(playerGuid);
                    return BehaviourTreeStatus.Success;
                })
            .End()
            .Build();

        internal IBehaviourTreeNode BuildPromoteLootManagerSequence(ulong playerGuid) => new BehaviourTreeBuilder()
            .Sequence("Promote Loot Manager Sequence")
                .Condition("Is In Group with Player", time => _objectManager.PartyMembers.Any(x => x.Guid == playerGuid))
                .Do("Promote Loot Manager", time =>
                {
                    _objectManager.PromoteLootManager(playerGuid);
                    return BehaviourTreeStatus.Success;
                })
            .End()
            .Build();

        internal IBehaviourTreeNode BuildSetGroupLootSequence(GroupLootSetting setting) => new BehaviourTreeBuilder()
            .Sequence("Set Group Loot Sequence")
                .Condition("Can Set Loot Rules", time => _objectManager.Player != null && _objectManager.PartyLeaderGuid == _objectManager.Player.Guid)
                .Do("Set Group Loot", time =>
                {
                    _objectManager.SetGroupLoot(setting);
                    return BehaviourTreeStatus.Success;
                })
            .End()
            .Build();

        internal IBehaviourTreeNode BuildAssignLootSequence(int itemId, ulong playerGuid) => new BehaviourTreeBuilder()
            .Sequence("Assign Loot Sequence")
                .Condition("Can Assign Loot", time => _objectManager.PartyMembers.Any(x => x.Guid == playerGuid))
                .Do("Assign Loot", time =>
                {
                    _objectManager.AssignLoot(itemId, playerGuid);
                    return BehaviourTreeStatus.Success;
                })
            .End()
            .Build();

        internal IBehaviourTreeNode BuildLootRollNeedSequence(int itemId) => new BehaviourTreeBuilder()
            .Sequence("Loot Roll Need Sequence")
                .Condition("Can Roll Need", time => _objectManager.HasLootRollWindow(itemId))
                .Do("Roll Need", time =>
                {
                    _objectManager.LootRollNeed(itemId);
                    return BehaviourTreeStatus.Success;
                })
            .End()
            .Build();

        internal IBehaviourTreeNode BuildLootRollGreedSequence(int itemId) => new BehaviourTreeBuilder()
            .Sequence("Loot Roll Greed Sequence")
                .Condition("Can Roll Greed", time => _objectManager.HasLootRollWindow(itemId))
                .Do("Roll Greed", time =>
                {
                    _objectManager.LootRollGreed(itemId);
                    return BehaviourTreeStatus.Success;
                })
            .End()
            .Build();

        internal IBehaviourTreeNode BuildLootPassSequence(int itemId) => new BehaviourTreeBuilder()
            .Sequence("Loot Pass Sequence")
                .Condition("Can Pass Loot", time => _objectManager.HasLootRollWindow(itemId))
                .Do("Pass Loot", time =>
                {
                    _objectManager.LootPass(itemId);
                    return BehaviourTreeStatus.Success;
                })
            .End()
            .Build();

        internal IBehaviourTreeNode BuildSendGroupInviteSequence(ulong playerGuid) => new BehaviourTreeBuilder()
            .Sequence("Send Group Invite Sequence")
                .Condition("Can Send Group Invite", time => !_objectManager.PartyMembers.Any(x => x.Guid == playerGuid))
                .Do("Send Group Invite", time =>
                {
                    _objectManager.InviteToGroup(playerGuid);
                    return BehaviourTreeStatus.Success;
                })
            .End()
            .Build();

        internal IBehaviourTreeNode BuildSendGroupInviteByNameSequence(string playerName) => new BehaviourTreeBuilder()
            .Sequence("Send Group Invite By Name Sequence")
                .Do("Send Group Invite By Name", time =>
                {
                    var factory = _agentFactoryAccessor?.Invoke();
                    if (factory != null)
                    {
                        Log.Information($"[BOT RUNNER] Inviting player '{playerName}' to group via network agent");
                        _ = factory.PartyAgent.InvitePlayerAsync(playerName);
                        return BehaviourTreeStatus.Success;
                    }

                    // Fallback to IObjectManager for foreground bots (uses Lua InviteByName)
                    Log.Information($"[BOT RUNNER] Inviting player '{playerName}' to group via ObjectManager");
                    _objectManager.InviteByName(playerName);
                    return BehaviourTreeStatus.Success;
                })
            .End()
            .Build();

        internal IBehaviourTreeNode AcceptGroupInviteSequence
        {
            get
            {
                int pollCount = 0;
                return new BehaviourTreeBuilder()
                    .Sequence("Accept Group Invite Sequence")
                        .Do("Accept Group Invite", time =>
                        {
                            pollCount++;
                            var factory = _agentFactoryAccessor?.Invoke();
                            if (factory != null && factory.PartyAgent.HasPendingInvite)
                            {
                                Log.Information("[BOT RUNNER] Accepting group invite via network agent");
                                _ = factory.PartyAgent.AcceptInviteAsync();
                                return BehaviourTreeStatus.Success;
                            }

                            // Fallback to IObjectManager for foreground bots
                            if (_objectManager.HasPendingGroupInvite())
                            {
                                _objectManager.AcceptGroupInvite();
                                return BehaviourTreeStatus.Success;
                            }

                            if (pollCount % 10 == 1)
                                Log.Information($"[BOT RUNNER] Waiting for group invite... (poll {pollCount}, HasPendingInvite={factory?.PartyAgent?.HasPendingInvite})");

                            // Timeout after ~10 seconds (100 ticks at 100ms)
                            if (pollCount >= 100)
                            {
                                Log.Warning("[BOT RUNNER] Timed out waiting for group invite after 10s");
                                return BehaviourTreeStatus.Failure;
                            }

                            return BehaviourTreeStatus.Running;
                        })
                    .End()
                    .Build();
            }
        }

        internal IBehaviourTreeNode DeclineGroupInviteSequence => new BehaviourTreeBuilder()
            .Sequence("Decline Group Invite Sequence")
                .Condition("Has Pending Invite", time => _objectManager.HasPendingGroupInvite())
                .Do("Decline Group Invite", time =>
                {
                    _objectManager.DeclineGroupInvite();
                    return BehaviourTreeStatus.Success;
                })
            .End()
            .Build();

        internal IBehaviourTreeNode BuildKickPlayerSequence(ulong playerGuid) => new BehaviourTreeBuilder()
            .Sequence("Kick Player Sequence")
                .Condition("Can Kick Player", time => _objectManager.Player != null && _objectManager.Player.Guid == _objectManager.PartyLeaderGuid)
                .Do("Kick Player", time =>
                {
                    _objectManager.KickPlayer(playerGuid);
                    return BehaviourTreeStatus.Success;
                })
            .End()
            .Build();

        internal IBehaviourTreeNode LeaveGroupSequence => new BehaviourTreeBuilder()
            .Sequence("Leave Group Sequence")
                .Do("Leave Group", time =>
                {
                    var factory = _agentFactoryAccessor?.Invoke();
                    if (factory != null)
                    {
                        Log.Information("[BOT RUNNER] Leaving group via network agent");
                        _ = factory.PartyAgent.LeaveGroupAsync();
                        return BehaviourTreeStatus.Success;
                    }

                    // Fallback to IObjectManager for foreground bots
                    if (_objectManager.PartyLeaderGuid != 0)
                    {
                        Log.Information("[BOT RUNNER] Leaving group via ObjectManager");
                        _objectManager.LeaveGroup();
                        return BehaviourTreeStatus.Success;
                    }

                    Log.Information("[BOT RUNNER] Not in a group, nothing to leave");
                    return BehaviourTreeStatus.Success;
                })
            .End()
            .Build();

        internal IBehaviourTreeNode DisbandGroupSequence => new BehaviourTreeBuilder()
            .Sequence("Disband Group Sequence")
                .Condition("Is Group Leader", time => _objectManager.Player != null && _objectManager.Player.Guid == _objectManager.PartyLeaderGuid)
                .Do("Disband Group", time =>
                {
                    _objectManager.DisbandGroup();
                    return BehaviourTreeStatus.Success;
                })
            .End()
            .Build();

        // =====================================================================
        // Login sequences (from BotRunnerService.Sequences.Login.cs)
        // =====================================================================

        internal IBehaviourTreeNode BuildLoginSequence(string username, string password) => new BehaviourTreeBuilder()
            .Sequence("Login Sequence")
                .Condition("Is On Login Screen", time => _objectManager.LoginScreen.IsOpen)
                .Do("Input Credentials", time =>
                {
                    if (_objectManager.LoginScreen.IsLoggedIn) return BehaviourTreeStatus.Success;

                    _objectManager.LoginScreen.Login(username, password);
                    return BehaviourTreeStatus.Running;
                })
                .Condition("Waiting in queue", time => _objectManager.LoginScreen.IsLoggedIn)
                .Do("Select Realm", time =>
                {
                    if (_objectManager.LoginScreen.QueuePosition > 0)
                        return BehaviourTreeStatus.Running;
                    return BehaviourTreeStatus.Success;
                })
            .End()
            .Build();

        internal IBehaviourTreeNode BuildRealmSelectionSequence() => new BehaviourTreeBuilder()
            .Sequence("Realm Selection Sequence")
                .Condition("On Realm Selection Screen", time => _objectManager.RealmSelectScreen.IsOpen && _objectManager.LoginScreen.IsLoggedIn)
                .Do("Select Realm", time =>
                {
                    if (_objectManager.RealmSelectScreen.CurrentRealm != null) return BehaviourTreeStatus.Success;

                    _objectManager.RealmSelectScreen.SelectRealm(_objectManager.RealmSelectScreen.GetRealmList()[0]);
                    return BehaviourTreeStatus.Success;
                })
            .End()
            .Build();

        internal IBehaviourTreeNode LogoutSequence => new BehaviourTreeBuilder()
            .Sequence("Logout Sequence")
                .Condition("Can Log Out", time => _objectManager.HasEnteredWorld && _objectManager.LoginScreen?.IsOpen == false)
                .Do("Log Out", time =>
                {
                    _objectManager.Logout();
                    return BehaviourTreeStatus.Success;
                })
            .End()
            .Build();

        internal IBehaviourTreeNode BuildRequestCharacterSequence() => new BehaviourTreeBuilder()
            .Sequence("Create Character Sequence")
                .Condition("On Character Creation Screen", time => _objectManager.CharacterSelectScreen.IsOpen)
                .Do("Request Character List", time =>
                {
                    _objectManager.CharacterSelectScreen.RefreshCharacterListFromServer();
                    return BehaviourTreeStatus.Success;
                })
            .End()
            .Build();

        internal IBehaviourTreeNode BuildCreateCharacterSequence(List<object> parameters) => new BehaviourTreeBuilder()
            .Sequence("Create Character Sequence")
                .Condition("On Character Creation Screen", time => _objectManager.CharacterSelectScreen.IsOpen)
                .Do("Create Character", time =>
                {
                    var name = (string)parameters[0];
                    var race = (Race)parameters[1];
                    var gender = (Gender)parameters[2];
                    var characterClass = (Class)parameters[3];

                    _objectManager.CharacterSelectScreen.CreateCharacter(name, race, gender, characterClass, 0, 0, 0, 0, 0, 0);
                    return BehaviourTreeStatus.Success;
                })
            .End()
            .Build();

        internal IBehaviourTreeNode BuildDeleteCharacterSequence(ulong characterId) => new BehaviourTreeBuilder()
            .Sequence("Delete Character Sequence")
                .Condition("On Character Select Screen", time => _objectManager.CharacterSelectScreen.IsOpen)
                .Do("Delete Character", time =>
                {
                    _objectManager.CharacterSelectScreen.DeleteCharacter(characterId);
                    return BehaviourTreeStatus.Success;
                })
            .End()
            .Build();

        internal IBehaviourTreeNode BuildEnterWorldSequence(ulong characterGuid) => new BehaviourTreeBuilder()
            .Sequence("Enter World Sequence")
                .Condition("On Character Select Screen", time => _objectManager.CharacterSelectScreen.IsOpen)
                .Do("Enter World", time =>
                {
                    _objectManager.EnterWorld(characterGuid);
                    return BehaviourTreeStatus.Success;
                })
            .End()
            .Build();
    }
}
