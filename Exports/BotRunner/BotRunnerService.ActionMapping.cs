using BotRunner.Interfaces;
using Communication;
using GameData.Core.Enums;
using GameData.Core.Interfaces;
using Serilog;
using System;
using System.Collections.Generic;

namespace BotRunner
{
    public partial class BotRunnerService
    {
        /// <summary>
        /// Maps Communication.ActionType (proto) to CharacterAction (C# enum).
        /// These enums are NOT identical — proto is missing AbandonQuest and has
        /// StartMeleeAttack/StartRangedAttack/StartWandAttack that C# doesn't.
        /// A direct (CharacterAction)(int) cast gives wrong values for most actions.
        /// </summary>
        private static CharacterAction? MapProtoActionType(Communication.ActionType actionType) => actionType switch
        {
            Communication.ActionType.Wait => CharacterAction.Wait,
            Communication.ActionType.Goto => CharacterAction.GoTo,
            Communication.ActionType.InteractWith => CharacterAction.InteractWith,
            Communication.ActionType.SelectGossip => CharacterAction.SelectGossip,
            Communication.ActionType.SelectTaxiNode => CharacterAction.SelectTaxiNode,
            Communication.ActionType.AcceptQuest => CharacterAction.AcceptQuest,
            Communication.ActionType.DeclineQuest => CharacterAction.DeclineQuest,
            Communication.ActionType.SelectReward => CharacterAction.SelectReward,
            Communication.ActionType.CompleteQuest => CharacterAction.CompleteQuest,
            Communication.ActionType.TrainSkill => CharacterAction.TrainSkill,
            Communication.ActionType.TrainTalent => CharacterAction.TrainTalent,
            Communication.ActionType.OfferTrade => CharacterAction.OfferTrade,
            Communication.ActionType.OfferGold => CharacterAction.OfferGold,
            Communication.ActionType.OfferItem => CharacterAction.OfferItem,
            Communication.ActionType.AcceptTrade => CharacterAction.AcceptTrade,
            Communication.ActionType.DeclineTrade => CharacterAction.DeclineTrade,
            Communication.ActionType.EnchantTrade => CharacterAction.EnchantTrade,
            Communication.ActionType.LockpickTrade => CharacterAction.LockpickTrade,
            Communication.ActionType.PromoteLeader => CharacterAction.PromoteLeader,
            Communication.ActionType.PromoteAssistant => CharacterAction.PromoteAssistant,
            Communication.ActionType.PromoteLootManager => CharacterAction.PromoteLootManager,
            Communication.ActionType.SetGroupLoot => CharacterAction.SetGroupLoot,
            Communication.ActionType.AssignLoot => CharacterAction.AssignLoot,
            Communication.ActionType.LootRollNeed => CharacterAction.LootRollNeed,
            Communication.ActionType.LootRollGreed => CharacterAction.LootRollGreed,
            Communication.ActionType.LootPass => CharacterAction.LootPass,
            Communication.ActionType.SendGroupInvite => CharacterAction.SendGroupInvite,
            Communication.ActionType.AcceptGroupInvite => CharacterAction.AcceptGroupInvite,
            Communication.ActionType.DeclineGroupInvite => CharacterAction.DeclineGroupInvite,
            Communication.ActionType.KickPlayer => CharacterAction.KickPlayer,
            Communication.ActionType.LeaveGroup => CharacterAction.LeaveGroup,
            Communication.ActionType.DisbandGroup => CharacterAction.DisbandGroup,
            Communication.ActionType.StartMeleeAttack => CharacterAction.StartMeleeAttack,
            Communication.ActionType.StopAttack => CharacterAction.StopAttack,
            Communication.ActionType.CastSpell => CharacterAction.CastSpell,
            Communication.ActionType.StopCast => CharacterAction.StopCast,
            Communication.ActionType.UseItem => CharacterAction.UseItem,
            Communication.ActionType.EquipItem => CharacterAction.EquipItem,
            Communication.ActionType.UnequipItem => CharacterAction.UnequipItem,
            Communication.ActionType.DestroyItem => CharacterAction.DestroyItem,
            Communication.ActionType.MoveItem => CharacterAction.MoveItem,
            Communication.ActionType.SplitStack => CharacterAction.SplitStack,
            Communication.ActionType.BuyItem => CharacterAction.BuyItem,
            Communication.ActionType.BuybackItem => CharacterAction.BuybackItem,
            Communication.ActionType.SellItem => CharacterAction.SellItem,
            Communication.ActionType.RepairItem => CharacterAction.RepairItem,
            Communication.ActionType.RepairAllItems => CharacterAction.RepairAllItems,
            Communication.ActionType.DismissBuff => CharacterAction.DismissBuff,
            Communication.ActionType.Resurrect => CharacterAction.Resurrect,
            Communication.ActionType.Craft => CharacterAction.Craft,
            Communication.ActionType.Login => CharacterAction.Login,
            Communication.ActionType.Logout => CharacterAction.Logout,
            Communication.ActionType.CreateCharacter => CharacterAction.CreateCharacter,
            Communication.ActionType.DeleteCharacter => CharacterAction.DeleteCharacter,
            Communication.ActionType.EnterWorld => CharacterAction.EnterWorld,
            Communication.ActionType.LootCorpse => CharacterAction.LootCorpse,
            Communication.ActionType.ReleaseCorpse => CharacterAction.ReleaseCorpse,
            Communication.ActionType.RetrieveCorpse => CharacterAction.RetrieveCorpse,
            Communication.ActionType.SkinCorpse => CharacterAction.SkinCorpse,
            Communication.ActionType.GatherNode => CharacterAction.GatherNode,
            Communication.ActionType.SendChat => CharacterAction.SendChat,
            Communication.ActionType.SetFacing => CharacterAction.SetFacing,
            _ => null,
        };

        private static List<(CharacterAction, List<object>)> ConvertActionMessageToCharacterActions(Communication.ActionMessage action)
        {
            var result = new List<(CharacterAction, List<object>)>();

            var charAction = MapProtoActionType(action.ActionType);
            if (charAction == null)
            {
                Log.Warning($"[BOT RUNNER] Unknown ActionType {action.ActionType} ({(int)action.ActionType}), cannot map to CharacterAction");
                return result;
            }

            var parameters = new List<object>();

            foreach (var param in action.Parameters)
            {
                switch (param.ParameterCase)
                {
                    case Communication.RequestParameter.ParameterOneofCase.FloatParam:
                        parameters.Add(param.FloatParam);
                        break;
                    case Communication.RequestParameter.ParameterOneofCase.IntParam:
                        parameters.Add(param.IntParam);
                        break;
                    case Communication.RequestParameter.ParameterOneofCase.LongParam:
                        parameters.Add(param.LongParam);
                        break;
                    case Communication.RequestParameter.ParameterOneofCase.StringParam:
                        parameters.Add(param.StringParam);
                        break;
                }
            }

            result.Add((charAction.Value, parameters));
            return result;
        }

        private sealed class BotRunnerContext(IObjectManager objectManager, Stack<IBotTask> tasks, IDependencyContainer container, Constants.BotBehaviorConfig config) : IBotContext
        {
            public IObjectManager ObjectManager => objectManager;
            public Stack<IBotTask> BotTasks => tasks;
            public IDependencyContainer Container => container;
            public Constants.BotBehaviorConfig Config => config;
            public IWoWEventHandler EventHandler => objectManager.EventHandler;
        }

        /// <summary>
        /// Safely unboxes a numeric value to ulong. Handles boxed long, ulong, int, uint.
        /// Needed because protobuf int64 fields are boxed as long, and C# cannot unbox long to ulong directly.
        /// </summary>
        private static ulong UnboxGuid(object value) => value switch
        {
            long l => unchecked((ulong)l),
            ulong u => u,
            int i => unchecked((ulong)i),
            uint u => u,
            _ => throw new InvalidCastException($"Cannot convert {value?.GetType()} to GUID (ulong)")
        };
    }
}
