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
        /// Maps Communication.ObjectiveType (proto) to CharacterAction (C# enum).
        /// These enums are NOT identical -- proto is missing AbandonQuest and has
        /// StartMeleeAttack/StartRangedAttack/StartWandAttack that C# doesn't.
        /// A direct (CharacterAction)(int) cast gives wrong values for most actions.
        /// </summary>
        internal static CharacterAction? MapProtoObjectiveType(Communication.ObjectiveType objectiveType) => objectiveType switch
        {
            Communication.ObjectiveType.Wait => CharacterAction.Wait,
            Communication.ObjectiveType.Goto => CharacterAction.GoTo,
            Communication.ObjectiveType.InteractWith => CharacterAction.InteractWith,
            Communication.ObjectiveType.SelectGossip => CharacterAction.SelectGossip,
            Communication.ObjectiveType.SelectTaxiNode => CharacterAction.SelectTaxiNode,
            Communication.ObjectiveType.AcceptQuest => CharacterAction.AcceptQuest,
            Communication.ObjectiveType.DeclineQuest => CharacterAction.DeclineQuest,
            Communication.ObjectiveType.SelectReward => CharacterAction.SelectReward,
            Communication.ObjectiveType.CompleteQuest => CharacterAction.CompleteQuest,
            Communication.ObjectiveType.TrainSkill => CharacterAction.TrainSkill,
            Communication.ObjectiveType.TrainTalent => CharacterAction.TrainTalent,
            Communication.ObjectiveType.OfferTrade => CharacterAction.OfferTrade,
            Communication.ObjectiveType.OfferGold => CharacterAction.OfferGold,
            Communication.ObjectiveType.OfferItem => CharacterAction.OfferItem,
            Communication.ObjectiveType.AcceptTrade => CharacterAction.AcceptTrade,
            Communication.ObjectiveType.DeclineTrade => CharacterAction.DeclineTrade,
            Communication.ObjectiveType.EnchantTrade => CharacterAction.EnchantTrade,
            Communication.ObjectiveType.LockpickTrade => CharacterAction.LockpickTrade,
            Communication.ObjectiveType.PromoteLeader => CharacterAction.PromoteLeader,
            Communication.ObjectiveType.PromoteAssistant => CharacterAction.PromoteAssistant,
            Communication.ObjectiveType.PromoteLootManager => CharacterAction.PromoteLootManager,
            Communication.ObjectiveType.SetGroupLoot => CharacterAction.SetGroupLoot,
            Communication.ObjectiveType.AssignLoot => CharacterAction.AssignLoot,
            Communication.ObjectiveType.LootRollNeed => CharacterAction.LootRollNeed,
            Communication.ObjectiveType.LootRollGreed => CharacterAction.LootRollGreed,
            Communication.ObjectiveType.LootPass => CharacterAction.LootPass,
            Communication.ObjectiveType.SendGroupInvite => CharacterAction.SendGroupInvite,
            Communication.ObjectiveType.AcceptGroupInvite => CharacterAction.AcceptGroupInvite,
            Communication.ObjectiveType.DeclineGroupInvite => CharacterAction.DeclineGroupInvite,
            Communication.ObjectiveType.KickPlayer => CharacterAction.KickPlayer,
            Communication.ObjectiveType.LeaveGroup => CharacterAction.LeaveGroup,
            Communication.ObjectiveType.DisbandGroup => CharacterAction.DisbandGroup,
            Communication.ObjectiveType.StartMeleeAttack => CharacterAction.StartMeleeAttack,
            Communication.ObjectiveType.StartRangedAttack => CharacterAction.StartRangedAttack,
            Communication.ObjectiveType.StartWandAttack => CharacterAction.StartWandAttack,
            Communication.ObjectiveType.StopAttack => CharacterAction.StopAttack,
            Communication.ObjectiveType.CastSpell => CharacterAction.CastSpell,
            Communication.ObjectiveType.StopCast => CharacterAction.StopCast,
            Communication.ObjectiveType.UseItem => CharacterAction.UseItem,
            Communication.ObjectiveType.EquipItem => CharacterAction.EquipItem,
            Communication.ObjectiveType.UnequipItem => CharacterAction.UnequipItem,
            Communication.ObjectiveType.DestroyItem => CharacterAction.DestroyItem,
            Communication.ObjectiveType.MoveItem => CharacterAction.MoveItem,
            Communication.ObjectiveType.SplitStack => CharacterAction.SplitStack,
            Communication.ObjectiveType.BuyItem => CharacterAction.BuyItem,
            Communication.ObjectiveType.BuybackItem => CharacterAction.BuybackItem,
            Communication.ObjectiveType.SellItem => CharacterAction.SellItem,
            Communication.ObjectiveType.RepairItem => CharacterAction.RepairItem,
            Communication.ObjectiveType.RepairAllItems => CharacterAction.RepairAllItems,
            Communication.ObjectiveType.DismissBuff => CharacterAction.DismissBuff,
            Communication.ObjectiveType.Resurrect => CharacterAction.Resurrect,
            Communication.ObjectiveType.Craft => CharacterAction.Craft,
            Communication.ObjectiveType.Login => CharacterAction.Login,
            Communication.ObjectiveType.Logout => CharacterAction.Logout,
            Communication.ObjectiveType.CreateCharacter => CharacterAction.CreateCharacter,
            Communication.ObjectiveType.DeleteCharacter => CharacterAction.DeleteCharacter,
            Communication.ObjectiveType.EnterWorld => CharacterAction.EnterWorld,
            Communication.ObjectiveType.LootCorpse => CharacterAction.LootCorpse,
            Communication.ObjectiveType.ReleaseCorpse => CharacterAction.ReleaseCorpse,
            Communication.ObjectiveType.RetrieveCorpse => CharacterAction.RetrieveCorpse,
            Communication.ObjectiveType.SkinCorpse => CharacterAction.SkinCorpse,
            Communication.ObjectiveType.GatherNode => CharacterAction.GatherNode,
            Communication.ObjectiveType.SendChat => CharacterAction.SendChat,
            Communication.ObjectiveType.SetFacing => CharacterAction.SetFacing,
            Communication.ObjectiveType.VisitVendor => CharacterAction.VisitVendor,
            Communication.ObjectiveType.VisitTrainer => CharacterAction.VisitTrainer,
            Communication.ObjectiveType.VisitFlightMaster => CharacterAction.VisitFlightMaster,
            Communication.ObjectiveType.StartFishing => CharacterAction.StartFishing,
            Communication.ObjectiveType.StartGatheringRoute => CharacterAction.StartGatheringRoute,
            Communication.ObjectiveType.CheckMail => CharacterAction.CheckMail,
            Communication.ObjectiveType.StartDungeoneering => CharacterAction.StartDungeoneering,
            Communication.ObjectiveType.ConvertToRaid => CharacterAction.ConvertToRaid,
            Communication.ObjectiveType.ChangeRaidSubgroup => CharacterAction.ChangeRaidSubgroup,
            Communication.ObjectiveType.FollowTarget => CharacterAction.FollowTarget,
            Communication.ObjectiveType.JoinBattleground => CharacterAction.JoinBattleground,
            Communication.ObjectiveType.AcceptBattleground => CharacterAction.AcceptBattleground,
            Communication.ObjectiveType.LeaveBattleground => CharacterAction.LeaveBattleground,
            Communication.ObjectiveType.TravelTo => CharacterAction.TravelTo,
            Communication.ObjectiveType.Jump => CharacterAction.Jump,
            Communication.ObjectiveType.StartMovement => CharacterAction.StartMovement,
            Communication.ObjectiveType.StopMovement => CharacterAction.StopMovement,
            _ => null,
        };

        internal static List<(CharacterAction, List<object>)> ConvertObjectiveMessageToCharacterActions(Communication.ObjectiveMessage action)
        {
            var result = new List<(CharacterAction, List<object>)>();

            var charAction = MapProtoObjectiveType(action.ObjectiveType);
            if (charAction == null)
            {
                Log.Warning($"[BOT RUNNER] Unknown ObjectiveType {action.ObjectiveType} ({(int)action.ObjectiveType}), cannot map to CharacterAction");
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

        /// <summary>
        /// Safely unboxes a numeric value to ulong. Handles boxed long, ulong, int, uint.
        /// Needed because protobuf int64 fields are boxed as long, and C# cannot unbox long to ulong directly.
        /// </summary>
        internal static ulong UnboxGuid(object value) => value switch
        {
            long l => unchecked((ulong)l),
            ulong u => u,
            int i => unchecked((ulong)i),
            uint u => u,
            _ => throw new InvalidCastException($"Cannot convert {value?.GetType()} to GUID (ulong)")
        };

        /// <summary>
        /// BotRunnerContext implementation shared across partial-class files and bot tasks.
        /// </summary>
        internal sealed class BotRunnerContext(
            IObjectManager objectManager,
            Stack<IBotTask> tasks,
            IDependencyContainer container,
            Constants.BotBehaviorConfig config,
            Action<string> addDiagnosticMessage,
            Action<string> addImmediateDiagnostic) : IBotContext
        {
            public Microsoft.Extensions.Logging.ILoggerFactory? LoggerFactory => null;
            public IObjectManager ObjectManager => objectManager;
            public Stack<IBotTask> BotTasks => tasks;
            public IDependencyContainer Container => container;
            public Constants.BotBehaviorConfig Config => config;
            public IWoWEventHandler EventHandler => objectManager.EventHandler;
            public void AddDiagnosticMessage(string message) => addDiagnosticMessage(message);
            public void AddImmediateDiagnostic(string message) => addImmediateDiagnostic(message);
        }
    }
}
