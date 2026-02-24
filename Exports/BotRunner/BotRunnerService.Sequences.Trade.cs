using GameData.Core.Enums;
using Serilog;
using System.Linq;
using Xas.FluentBehaviourTree;

namespace BotRunner
{
    public partial class BotRunnerService
    {
        /// <summary>
        /// Sequence to offer a trade to another player or NPC.
        /// </summary>
        /// <param name="targetGuid">The GUID of the target with whom to trade.</param>
        /// <returns>IBehaviourTreeNode that manages offering a trade.</returns>
        private IBehaviourTreeNode BuildOfferTradeSequence(ulong targetGuid) => new BehaviourTreeBuilder()
            .Sequence("Offer Trade Sequence")
                // Ensure the bot has a valid trade target
                .Condition("Has Valid Trade Target", time => _objectManager.Player.Position.DistanceTo(_objectManager.Players.First(x => x.Guid == targetGuid).Position) < 5.33f)

                // Offer trade to the target
                .Do("Offer Trade", time =>
                {
                    _objectManager.Players.First(x => x.Guid == targetGuid).OfferTrade();
                    return BehaviourTreeStatus.Success;
                })
            .End()
            .Build();
        /// <summary>
        /// Sequence to offer money in a trade to another player or NPC.
        /// </summary>
        /// <param name="copperCount">The amount of money (in copper) to offer in the trade.</param>
        /// <returns>IBehaviourTreeNode that manages offering money in the trade.</returns>
        private IBehaviourTreeNode BuildOfferMoneySequence(int copperCount) => new BehaviourTreeBuilder()
            .Sequence("Offer Money Sequence")
                // Ensure the bot has a valid trade window open
                .Condition("Trade Window Valid", time => _objectManager.TradeFrame.IsOpen)

                // Ensure the bot has enough money to offer
                .Condition("Has Enough Money", time => _objectManager.Player.Copper > copperCount)

                // Offer money in the trade
                .Do("Offer Money", time =>
                {
                    _objectManager.TradeFrame.OfferMoney(copperCount);
                    return BehaviourTreeStatus.Success;
                })
            .End()
            .Build();
        /// <summary>
        /// Sequence to offer an item in a trade to another player or NPC.
        /// </summary>
        /// <param name="bagId">The bag ID where the item is stored.</param>
        /// <param name="slotId">The slot ID where the item is located.</param>
        /// <param name="quantity">The quantity of the item to offer.</param>
        /// <param name="tradeWindowSlot">The slot in the trade window to place the item.</param>
        /// <returns>IBehaviourTreeNode that manages offering the item in the trade.</returns>
        private IBehaviourTreeNode BuildOfferItemSequence(int bagId, int slotId, int quantity, int tradeWindowSlot) => new BehaviourTreeBuilder()
            .Sequence("Offer Item Sequence")
                // Ensure the bot has a valid trade window open
                .Condition("Trade Window Valid", time => _objectManager.TradeFrame.IsOpen)

                // Ensure the bot has the item and quantity to offer
                .Condition("Has Item to Offer", time => _objectManager.GetContainedItem(bagId, slotId).Quantity >= quantity)

                // Offer the item in the trade window
                .Do("Offer Item", time =>
                {
                    _objectManager.TradeFrame.OfferItem(bagId, slotId, quantity, tradeWindowSlot);
                    return BehaviourTreeStatus.Success;
                })
            .End()
            .Build();
        /// <summary>
        /// Sequence to accept a trade with another player or NPC.
        /// </summary>
        /// <returns>IBehaviourTreeNode that manages accepting the trade.</returns>
        private IBehaviourTreeNode AcceptTradeSequence => new BehaviourTreeBuilder()
            .Sequence("Accept Trade Sequence")
                // Ensure the bot has a valid trade window open
                .Condition("Trade Window Valid", time => _objectManager.TradeFrame.IsOpen)

                // Accept the trade
                .Do("Accept Trade", time =>
                {
                    _objectManager.TradeFrame.AcceptTrade();
                    return BehaviourTreeStatus.Success;
                })
            .End()
            .Build();
        /// <summary>
        /// Sequence to decline a trade with another player or NPC.
        /// </summary>
        /// <returns>IBehaviourTreeNode that manages declining the trade.</returns>
        private IBehaviourTreeNode DeclineTradeSequence => new BehaviourTreeBuilder()
            .Sequence("Decline Trade Sequence")
                // Ensure the trade window is valid
                .Condition("Trade Window Valid", time => _objectManager.TradeFrame.IsOpen)

                // Decline the trade
                .Do("Decline Trade", time =>
                {
                    _objectManager.TradeFrame.DeclineTrade();
                    return BehaviourTreeStatus.Success;
                })
            .End()
            .Build();
        /// <summary>
        /// Sequence to offer an enchantment in a trade to another player or NPC.
        /// </summary>
        /// <param name="enchantId">The ID of the enchantment to offer.</param>
        /// <returns>IBehaviourTreeNode that manages offering the enchantment in the trade.</returns>
        private IBehaviourTreeNode BuildOfferEnchantSequence(int enchantId) => new BehaviourTreeBuilder()
            .Sequence("Offer Enchant Sequence")
                // Ensure the trade window is valid
                .Condition("Trade Window Valid", time => _objectManager.TradeFrame.IsOpen)

                //// Ensure the bot has the correct enchantment to offer
                //.Condition("Has Enchant Available", time => _objectManager.HasEnchantAvailable(enchantId))

                // Offer the enchantment in the trade
                .Do("Offer Enchant", time =>
                {
                    _objectManager.TradeFrame.OfferEnchant(enchantId);
                    return BehaviourTreeStatus.Success;
                })
            .End()
            .Build();
        /// <summary>
        /// Sequence to offer a lockpicking service in a trade.
        /// </summary>
        /// <returns>IBehaviourTreeNode that manages offering lockpicking in a trade.</returns>
        private IBehaviourTreeNode OfferLockpickSequence => new BehaviourTreeBuilder()
            .Sequence("Lockpick Trade Sequence")
                // Ensure the bot has the ability to lockpick
                .Condition("Can Lockpick", time => _objectManager.Player.Class == Class.Rogue)

                // Offer lockpicking in the trade
                .Do("Offer Lockpick", time =>
                {
                    _objectManager.TradeFrame.OfferLockpick();
                    return BehaviourTreeStatus.Success;
                })
            .End()
            .Build();
    }
}
