using BotRunner.Combat;
using BotRunner.Helpers;
using BotRunner.Interfaces;
using BotRunner.SequenceBuilders;
using Communication;
using GameData.Core.Enums;
using GameData.Core.Interfaces;
using GameData.Core.Models;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using WoWSharpClient.Networking.ClientComponents.I;
using Xas.FluentBehaviourTree;

namespace BotRunner
{
    /// <summary>
    /// Maps CharacterAction enums to behavior tree sequences via the sequence builders.
    /// Extracted from BotRunnerService.ActionDispatch.cs + ActionMapping.cs partials.
    /// </summary>
    internal sealed class ActionDispatcher
    {
        private readonly IObjectManager _objectManager;
        private readonly IDependencyContainer _container;
        private readonly Func<IAgentFactory?>? _agentFactoryAccessor;
        private readonly Constants.BotBehaviorConfig _behaviorConfig;
        private readonly CombatSequenceBuilder _combat;
        private readonly MovementSequenceBuilder _movement;
        private readonly InteractionSequenceBuilder _interaction;

        // Delegate for pushing bot tasks and accessing shared state
        private readonly Func<Stack<IBotTask>> _botTasksAccessor;
        private readonly Action<string> _enqueueDiagnosticMessage;
        private readonly Action<string> _diagLog;

        // Death state checks passed in from BotRunnerService
        private readonly Func<IWoWLocalPlayer, bool> _isDeadOrGhostState;
        private readonly Func<IWoWLocalPlayer, bool> _isGhostState;
        private readonly Func<IWoWLocalPlayer, bool> _isCorpseState;

        // Shared state from BotRunnerService
        private readonly Func<DateTime> _getLastReleaseSpiritCommandUtc;
        private readonly Action<DateTime> _setLastReleaseSpiritCommandUtc;
        private readonly Func<Position?> _getLastKnownAlivePosition;

        internal ActionDispatcher(
            IObjectManager objectManager,
            IDependencyContainer container,
            Func<IAgentFactory?>? agentFactoryAccessor,
            Constants.BotBehaviorConfig behaviorConfig,
            CombatSequenceBuilder combat,
            MovementSequenceBuilder movement,
            InteractionSequenceBuilder interaction,
            Func<Stack<IBotTask>> botTasksAccessor,
            Action<string> enqueueDiagnosticMessage,
            Action<string> diagLog,
            Func<IWoWLocalPlayer, bool> isDeadOrGhostState,
            Func<IWoWLocalPlayer, bool> isGhostState,
            Func<IWoWLocalPlayer, bool> isCorpseState,
            Func<DateTime> getLastReleaseSpiritCommandUtc,
            Action<DateTime> setLastReleaseSpiritCommandUtc,
            Func<Position?> getLastKnownAlivePosition)
        {
            _objectManager = objectManager;
            _container = container;
            _agentFactoryAccessor = agentFactoryAccessor;
            _behaviorConfig = behaviorConfig;
            _combat = combat;
            _movement = movement;
            _interaction = interaction;
            _botTasksAccessor = botTasksAccessor;
            _enqueueDiagnosticMessage = enqueueDiagnosticMessage;
            _diagLog = diagLog;
            _isDeadOrGhostState = isDeadOrGhostState;
            _isGhostState = isGhostState;
            _isCorpseState = isCorpseState;
            _getLastReleaseSpiritCommandUtc = getLastReleaseSpiritCommandUtc;
            _setLastReleaseSpiritCommandUtc = setLastReleaseSpiritCommandUtc;
            _getLastKnownAlivePosition = getLastKnownAlivePosition;
        }

        // =====================================================================
        // Action mapping (from BotRunnerService.ActionMapping.cs)
        // =====================================================================

        /// <summary>
        /// Maps Communication.ActionType (proto) to CharacterAction (C# enum).
        /// These enums are NOT identical -- proto is missing AbandonQuest and has
        /// StartMeleeAttack/StartRangedAttack/StartWandAttack that C# doesn't.
        /// A direct (CharacterAction)(int) cast gives wrong values for most actions.
        /// </summary>
        internal static CharacterAction? MapProtoActionType(Communication.ActionType actionType) => actionType switch
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
            Communication.ActionType.StartRangedAttack => CharacterAction.StartRangedAttack,
            Communication.ActionType.StartWandAttack => CharacterAction.StartWandAttack,
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
            Communication.ActionType.VisitVendor => CharacterAction.VisitVendor,
            Communication.ActionType.VisitTrainer => CharacterAction.VisitTrainer,
            Communication.ActionType.VisitFlightMaster => CharacterAction.VisitFlightMaster,
            Communication.ActionType.StartFishing => CharacterAction.StartFishing,
            Communication.ActionType.StartGatheringRoute => CharacterAction.StartGatheringRoute,
            Communication.ActionType.CheckMail => CharacterAction.CheckMail,
            Communication.ActionType.StartDungeoneering => CharacterAction.StartDungeoneering,
            Communication.ActionType.ConvertToRaid => CharacterAction.ConvertToRaid,
            Communication.ActionType.ChangeRaidSubgroup => CharacterAction.ChangeRaidSubgroup,
            Communication.ActionType.FollowTarget => CharacterAction.FollowTarget,
            Communication.ActionType.JoinBattleground => CharacterAction.JoinBattleground,
            Communication.ActionType.AcceptBattleground => CharacterAction.AcceptBattleground,
            Communication.ActionType.LeaveBattleground => CharacterAction.LeaveBattleground,
            Communication.ActionType.TravelTo => CharacterAction.TravelTo,
            _ => null,
        };

        internal static List<(CharacterAction, List<object>)> ConvertActionMessageToCharacterActions(Communication.ActionMessage action)
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

        internal static IReadOnlyList<string> SendChatThroughBestAvailablePath(IObjectManager objectManager, string chatMsg)
        {
            ArgumentNullException.ThrowIfNull(objectManager);

            if (!string.IsNullOrWhiteSpace(chatMsg)
                && chatMsg.StartsWith(".", StringComparison.Ordinal)
                && objectManager.SupportsDirectGmCommandCapture)
            {
                return objectManager.SendGmCommandAsync(chatMsg, GetDirectGmCommandCaptureTimeoutMs(chatMsg)).GetAwaiter().GetResult();
            }

            objectManager.SendChatMessage(chatMsg);
            return Array.Empty<string>();
        }

        internal static int GetDirectGmCommandCaptureTimeoutMs(string chatMsg)
        {
            if (string.IsNullOrWhiteSpace(chatMsg))
                return 2000;

            return chatMsg.StartsWith(".pool spawns ", StringComparison.OrdinalIgnoreCase)
                ? 3000
                : 2000;
        }

        // =====================================================================
        // Action dispatch (from BotRunnerService.ActionDispatch.cs)
        // =====================================================================

        internal IBehaviourTreeNode BuildBehaviorTreeFromActions(List<(CharacterAction, List<object>)> actionMap)
        {
            var botTasks = _botTasksAccessor();
            var context = new BotRunnerContext(_objectManager, botTasks, _container, _behaviorConfig, _enqueueDiagnosticMessage);
            var builder = new BehaviourTreeBuilder()
                .Sequence("StateManager Action Sequence");

            foreach (var actionEntry in actionMap)
            {
                switch (actionEntry.Item1)
                {
                    case CharacterAction.Wait:
                        builder.Splice(MovementSequenceBuilder.BuildWaitSequence((float)actionEntry.Item2[0]));
                        break;
                    case CharacterAction.GoTo:
                    {
                        var gotoX = (float)actionEntry.Item2[0];
                        var gotoY = (float)actionEntry.Item2[1];
                        var gotoZ = (float)actionEntry.Item2[2];
                        var gotoTolerance = (float)actionEntry.Item2[3];
                        builder.Do("Push GoTo Task", time =>
                        {
                            botTasks.Push(new Tasks.GoToTask(context, gotoX, gotoY, gotoZ, gotoTolerance));
                            return BehaviourTreeStatus.Success;
                        });
                        break;
                    }
                    case CharacterAction.InteractWith:
                    {
                        var interactGuid = UnboxGuid(actionEntry.Item2[0]);
                        var isGameObject = _objectManager.GameObjects.Any(x => x.Guid == interactGuid);
                        if (isGameObject)
                        {
                            builder.Splice(_movement.BuildInteractWithSequence(interactGuid, _combat.CheckForTarget(interactGuid)));
                        }
                        else
                        {
                            builder.Do($"Interact With NPC {interactGuid:X}", time =>
                            {
                                _objectManager.InteractWithNpcAsync(interactGuid, CancellationToken.None)
                                    .GetAwaiter().GetResult();
                                return BehaviourTreeStatus.Success;
                            });
                        }
                        break;
                    }

                    case CharacterAction.SelectGossip:
                        if (actionEntry.Item2.Count >= 2)
                        {
                            var gossipNpcGuid = UnboxGuid(actionEntry.Item2[0]);
                            var gossipOptionIndex = (uint)(int)actionEntry.Item2[1];
                            builder.Do($"Select Gossip Option {gossipOptionIndex} on NPC {gossipNpcGuid:X}", time =>
                            {
                                var factory = _agentFactoryAccessor?.Invoke();
                                if (factory != null)
                                {
                                    factory.GossipAgent.SelectGossipOptionAsync(gossipOptionIndex, CancellationToken.None)
                                        .GetAwaiter().GetResult();
                                    return BehaviourTreeStatus.Success;
                                }
                                Log.Warning("[BOT RUNNER] AgentFactory unavailable for SelectGossip packet path");
                                return BehaviourTreeStatus.Failure;
                            });
                        }
                        else
                        {
                            builder.Splice(_interaction.BuildSelectGossipSequence((int)actionEntry.Item2[0]));
                        }
                        break;

                    case CharacterAction.SelectTaxiNode:
                        if (actionEntry.Item2.Count >= 3)
                        {
                            var taxiFmGuid = UnboxGuid(actionEntry.Item2[0]);
                            var taxiSourceNode = (uint)(int)actionEntry.Item2[1];
                            var taxiDestNode = (uint)(int)actionEntry.Item2[2];
                            builder.Do($"Activate Taxi {taxiSourceNode}->{taxiDestNode} via FM {taxiFmGuid:X}", time =>
                            {
                                var factory = _agentFactoryAccessor?.Invoke();
                                if (factory != null)
                                {
                                    factory.FlightMasterAgent.ActivateFlightAsync(taxiFmGuid, taxiSourceNode, taxiDestNode, CancellationToken.None)
                                        .GetAwaiter().GetResult();
                                    return BehaviourTreeStatus.Success;
                                }
                                Log.Warning("[BOT RUNNER] AgentFactory unavailable for SelectTaxiNode packet path");
                                return BehaviourTreeStatus.Failure;
                            });
                        }
                        else
                        {
                            builder.Splice(_interaction.BuildSelectTaxiNodeSequence((int)actionEntry.Item2[0]));
                        }
                        break;

                    case CharacterAction.VisitFlightMaster:
                        builder.Do("Queue Flight Master Visit Task", time =>
                        {
                            if (botTasks.Count == 0 || botTasks.Peek() is not Tasks.FlightMasterVisitTask)
                                botTasks.Push(new Tasks.FlightMasterVisitTask(context));
                            return BehaviourTreeStatus.Success;
                        });
                        break;

                    case CharacterAction.AcceptQuest:
                        if (actionEntry.Item2.Count >= 2)
                        {
                            var questNpcGuid = UnboxGuid(actionEntry.Item2[0]);
                            var questId = (uint)(int)actionEntry.Item2[1];
                            builder.Do($"Accept Quest {questId} from NPC {questNpcGuid:X}", time =>
                            {
                                _objectManager.AcceptQuestFromNpcAsync(questNpcGuid, questId, CancellationToken.None)
                                    .GetAwaiter().GetResult();
                                return BehaviourTreeStatus.Success;
                            });
                        }
                        else
                        {
                            builder.Splice(_interaction.AcceptQuestSequence);
                        }
                        break;
                    case CharacterAction.DeclineQuest:
                        builder.Splice(_interaction.DeclineQuestSequence);
                        break;
                    case CharacterAction.AbandonQuest:
                        if (actionEntry.Item2.Count >= 1)
                        {
                            var questSlot = (byte)(int)actionEntry.Item2[0];
                            builder.Do("Abandon Quest", time =>
                            {
                                var factory = _agentFactoryAccessor?.Invoke();
                                if (factory != null)
                                {
                                    _ = factory.QuestAgent.RemoveQuestFromLogAsync(questSlot);
                                    return BehaviourTreeStatus.Success;
                                }
                                return BehaviourTreeStatus.Failure;
                            });
                        }
                        break;
                    case CharacterAction.SelectReward:
                        builder.Splice(_interaction.BuildSelectRewardSequence((int)actionEntry.Item2[0]));
                        break;
                    case CharacterAction.CompleteQuest:
                        if (actionEntry.Item2.Count >= 2)
                        {
                            var turnInNpcGuid = UnboxGuid(actionEntry.Item2[0]);
                            var turnInQuestId = (uint)(int)actionEntry.Item2[1];
                            uint turnInReward = actionEntry.Item2.Count >= 3 ? (uint)(int)actionEntry.Item2[2] : 0u;
                            builder.Do($"Complete Quest {turnInQuestId} at NPC {turnInNpcGuid:X}", time =>
                            {
                                _objectManager.TurnInQuestAsync(turnInNpcGuid, turnInQuestId, turnInReward, CancellationToken.None)
                                    .GetAwaiter().GetResult();
                                return BehaviourTreeStatus.Success;
                            });
                        }
                        else
                        {
                            builder.Splice(_interaction.CompleteQuestSequence);
                        }
                        break;

                    case CharacterAction.TrainSkill:
                        if (actionEntry.Item2.Count >= 2)
                        {
                            var trainSkillGuid = UnboxGuid(actionEntry.Item2[0]);
                            var trainSpellId = (uint)(int)actionEntry.Item2[1];
                            builder.Do($"Train Spell {trainSpellId} from trainer {trainSkillGuid:X}", time =>
                            {
                                var factory = _agentFactoryAccessor?.Invoke();
                                if (factory != null)
                                {
                                    factory.TrainerAgent.LearnSpellAsync(trainSkillGuid, trainSpellId, CancellationToken.None)
                                        .GetAwaiter().GetResult();
                                    return BehaviourTreeStatus.Success;
                                }
                                Log.Warning("[BOT RUNNER] AgentFactory unavailable for TrainSkill packet path");
                                return BehaviourTreeStatus.Failure;
                            });
                        }
                        else
                        {
                            builder.Splice(_interaction.BuildTrainSkillSequence((int)actionEntry.Item2[0]));
                        }
                        break;
                    case CharacterAction.TrainTalent:
                        if (actionEntry.Item2.Count >= 2)
                        {
                            var talentId = (uint)(int)actionEntry.Item2[0];
                            builder.Do($"Learn Talent {talentId} (packet)", time =>
                            {
                                var factory = _agentFactoryAccessor?.Invoke();
                                if (factory != null)
                                {
                                    factory.TalentAgent.LearnTalentAsync(talentId, CancellationToken.None)
                                        .GetAwaiter().GetResult();
                                    return BehaviourTreeStatus.Success;
                                }
                                Log.Warning("[BOT RUNNER] AgentFactory unavailable for TrainTalent packet path");
                                return BehaviourTreeStatus.Failure;
                            });
                        }
                        else
                        {
                            builder.Splice(_interaction.BuildLearnTalentSequence((int)actionEntry.Item2[0]));
                        }
                        break;

                    case CharacterAction.VisitTrainer:
                        builder.Do("Queue Trainer Visit Task", time =>
                        {
                            if (botTasks.Count == 0 || botTasks.Peek() is not Tasks.TrainerVisitTask)
                                botTasks.Push(new Tasks.TrainerVisitTask(context));
                            return BehaviourTreeStatus.Success;
                        });
                        break;

                    case CharacterAction.OfferTrade:
                        builder.Do("Initiate Trade (packet)", time =>
                        {
                            _objectManager.InitiateTradeAsync(UnboxGuid(actionEntry.Item2[0]), CancellationToken.None)
                                .GetAwaiter().GetResult();
                            return BehaviourTreeStatus.Success;
                        });
                        break;
                    case CharacterAction.OfferGold:
                        if (_objectManager.TradeFrame == null)
                        {
                            var goldCopper = (uint)(int)actionEntry.Item2[0];
                            builder.Do($"Set Trade Gold {goldCopper}c (packet)", time =>
                            {
                                _objectManager.SetTradeGoldAsync(goldCopper, CancellationToken.None)
                                    .GetAwaiter().GetResult();
                                return BehaviourTreeStatus.Success;
                            });
                        }
                        else
                        {
                            builder.Splice(_interaction.BuildOfferMoneySequence((int)actionEntry.Item2[0]));
                        }
                        break;
                    case CharacterAction.OfferItem:
                        if (_objectManager.TradeFrame == null)
                        {
                            var tradeSlot = (byte)(int)actionEntry.Item2[3];
                            var bagId = (byte)(int)actionEntry.Item2[0];
                            var slotId = (byte)(int)actionEntry.Item2[1];
                            builder.Do($"Set Trade Item bag={bagId} slot={slotId} (packet)", time =>
                            {
                                _objectManager.SetTradeItemAsync(tradeSlot, bagId, slotId, CancellationToken.None)
                                    .GetAwaiter().GetResult();
                                return BehaviourTreeStatus.Success;
                            });
                        }
                        else
                        {
                            builder.Splice(_interaction.BuildOfferItemSequence((int)actionEntry.Item2[0], (int)actionEntry.Item2[1], (int)actionEntry.Item2[2], (int)actionEntry.Item2[3]));
                        }
                        break;
                    case CharacterAction.AcceptTrade:
                        if (_objectManager.TradeFrame == null)
                        {
                            builder.Do("Accept Trade (packet)", time =>
                            {
                                _objectManager.AcceptTradeAsync(CancellationToken.None)
                                    .GetAwaiter().GetResult();
                                return BehaviourTreeStatus.Success;
                            });
                        }
                        else
                        {
                            builder.Splice(_interaction.AcceptTradeSequence);
                        }
                        break;
                    case CharacterAction.DeclineTrade:
                        if (_objectManager.TradeFrame == null)
                        {
                            builder.Do("Cancel Trade (packet)", time =>
                            {
                                _objectManager.CancelTradeAsync(CancellationToken.None)
                                    .GetAwaiter().GetResult();
                                return BehaviourTreeStatus.Success;
                            });
                        }
                        else
                        {
                            builder.Splice(_interaction.DeclineTradeSequence);
                        }
                        break;
                    case CharacterAction.EnchantTrade:
                        builder.Splice(_interaction.BuildOfferEnchantSequence((int)actionEntry.Item2[0]));
                        break;
                    case CharacterAction.LockpickTrade:
                        builder.Splice(_interaction.OfferLockpickSequence);
                        break;

                    case CharacterAction.PromoteLeader:
                        builder.Splice(_interaction.BuildPromoteLeaderSequence(UnboxGuid(actionEntry.Item2[0])));
                        break;
                    case CharacterAction.PromoteAssistant:
                        builder.Splice(_interaction.BuildPromoteAssistantSequence(UnboxGuid(actionEntry.Item2[0])));
                        break;
                    case CharacterAction.PromoteLootManager:
                        builder.Splice(_interaction.BuildPromoteLootManagerSequence(UnboxGuid(actionEntry.Item2[0])));
                        break;
                    case CharacterAction.SetGroupLoot:
                        builder.Splice(_interaction.BuildSetGroupLootSequence((GroupLootSetting)actionEntry.Item2[0]));
                        break;
                    case CharacterAction.AssignLoot:
                        builder.Splice(_interaction.BuildAssignLootSequence((int)actionEntry.Item2[0], UnboxGuid(actionEntry.Item2[1])));
                        break;

                    case CharacterAction.LootRollNeed:
                        builder.Splice(_interaction.BuildLootRollNeedSequence((int)actionEntry.Item2[0]));
                        break;
                    case CharacterAction.LootRollGreed:
                        builder.Splice(_interaction.BuildLootRollGreedSequence((int)actionEntry.Item2[0]));
                        break;
                    case CharacterAction.LootPass:
                        builder.Splice(_interaction.BuildLootPassSequence((int)actionEntry.Item2[0]));
                        break;

                    case CharacterAction.SendGroupInvite:
                        if (actionEntry.Item2[0] is string playerName)
                            builder.Splice(_interaction.BuildSendGroupInviteByNameSequence(playerName));
                        else
                            builder.Splice(_interaction.BuildSendGroupInviteSequence(UnboxGuid(actionEntry.Item2[0])));
                        break;
                    case CharacterAction.AcceptGroupInvite:
                        builder.Splice(_interaction.AcceptGroupInviteSequence);
                        break;
                    case CharacterAction.DeclineGroupInvite:
                        builder.Splice(_interaction.DeclineGroupInviteSequence);
                        break;
                    case CharacterAction.KickPlayer:
                        builder.Splice(_interaction.BuildKickPlayerSequence(UnboxGuid(actionEntry.Item2[0])));
                        break;
                    case CharacterAction.LeaveGroup:
                        builder.Splice(_interaction.LeaveGroupSequence);
                        break;
                    case CharacterAction.DisbandGroup:
                        builder.Splice(_interaction.DisbandGroupSequence);
                        break;
                    case CharacterAction.StartMeleeAttack:
                        builder.Splice(_combat.BuildStartMeleeAttackSequence(UnboxGuid(actionEntry.Item2[0])));
                        break;
                    case CharacterAction.StartRangedAttack:
                        builder.Splice(_combat.BuildStartRangedAttackSequence(UnboxGuid(actionEntry.Item2[0])));
                        break;
                    case CharacterAction.StartWandAttack:
                        builder.Splice(_combat.BuildStartWandAttackSequence(UnboxGuid(actionEntry.Item2[0])));
                        break;
                    case CharacterAction.StopAttack:
                        builder.Splice(_combat.StopAttackSequence);
                        break;
                    case CharacterAction.CastSpell:
                        var castTargetGuid = actionEntry.Item2.Count > 1 ? UnboxGuid(actionEntry.Item2[1]) : 0UL;
                        builder.Splice(_combat.BuildCastSpellSequence((int)actionEntry.Item2[0], castTargetGuid));
                        break;
                    case CharacterAction.StartFishing:
                    {
                        var fishingSearchWaypoints = ParseGatheringRoutePositions(actionEntry.Item2);
                        builder.Do("Queue Fishing Task", time =>
                        {
                            if (botTasks.Count == 0 || botTasks.Peek() is not Tasks.FishingTask)
                                botTasks.Push(new Tasks.FishingTask(context, fishingSearchWaypoints.Count > 0 ? fishingSearchWaypoints : null));
                            return BehaviourTreeStatus.Success;
                        });
                        break;
                    }
                    case CharacterAction.StartGatheringRoute:
                    {
                        int gatheringRouteSpellId = (int)actionEntry.Item2[0];
                        var allowedEntries = ParseGatheringEntries((string)actionEntry.Item2[1]);
                        int routeLoops = 1;
                        int positionStartIndex = 2;
                        if (actionEntry.Item2.Count > 2 && actionEntry.Item2[2] is int loopParam)
                        {
                            routeLoops = Math.Max(1, loopParam);
                            positionStartIndex = 3;
                        }
                        var routePositions = ParseGatheringRoutePositions(actionEntry.Item2.Skip(positionStartIndex));
                        builder.Do("Queue Gathering Route Task", time =>
                        {
                            if (routePositions.Count == 0 || allowedEntries.Count == 0)
                            {
                                Log.Warning("[BOT RUNNER] Ignoring StartGatheringRoute with invalid parameters. positions={Count} entries={EntryCount}",
                                    routePositions.Count, allowedEntries.Count);
                                return BehaviourTreeStatus.Success;
                            }

                            if (botTasks.Count == 0 || botTasks.Peek() is not Tasks.GatheringRouteTask)
                                botTasks.Push(new Tasks.GatheringRouteTask(context, routePositions, allowedEntries, gatheringRouteSpellId, maxRouteLoops: routeLoops));
                            return BehaviourTreeStatus.Success;
                        });
                        break;
                    }
                    case CharacterAction.StopCast:
                        builder.Splice(_combat.StopCastSequence);
                        break;

                    case CharacterAction.UseItem:
                        if (actionEntry.Item2.Count >= 3)
                            builder.Splice(_interaction.BuildUseItemSequence((int)actionEntry.Item2[0], (int)actionEntry.Item2[1], (ulong)actionEntry.Item2[2]));
                        else
                            builder.Splice(_interaction.BuildUseItemByIdSequence((int)actionEntry.Item2[0]));
                        break;
                    case CharacterAction.EquipItem:
                        if (actionEntry.Item2.Count >= 3)
                            builder.Splice(_interaction.BuildEquipItemSequence((int)actionEntry.Item2[0], (int)actionEntry.Item2[1], (EquipSlot)actionEntry.Item2[2]));
                        else if (actionEntry.Item2.Count >= 2)
                            builder.Splice(_interaction.BuildEquipItemSequence((int)actionEntry.Item2[0], (int)actionEntry.Item2[1]));
                        else
                            builder.Splice(_interaction.BuildEquipItemByIdSequence((int)actionEntry.Item2[0]));
                        break;
                    case CharacterAction.UnequipItem:
                        builder.Splice(_interaction.BuildUnequipItemSequence((EquipSlot)actionEntry.Item2[0]));
                        break;
                    case CharacterAction.DestroyItem:
                        builder.Splice(_interaction.BuildDestroyItemSequence((int)actionEntry.Item2[0], (int)actionEntry.Item2[1], (int)actionEntry.Item2[2]));
                        break;
                    case CharacterAction.MoveItem:
                        builder.Splice(_interaction.BuildMoveItemSequence((int)actionEntry.Item2[0], (int)actionEntry.Item2[1], (int)actionEntry.Item2[2], (int)actionEntry.Item2[3], (int)actionEntry.Item2[4]));
                        break;
                    case CharacterAction.SplitStack:
                        builder.Splice(_interaction.BuildSplitStackSequence((int)actionEntry.Item2[0],
                            (int)actionEntry.Item2[1],
                            (int)actionEntry.Item2[2],
                            (int)actionEntry.Item2[3],
                            (int)actionEntry.Item2[4]));
                        break;

                    case CharacterAction.BuyItem:
                        if (actionEntry.Item2.Count >= 3)
                        {
                            var buyVendorGuid = UnboxGuid(actionEntry.Item2[0]);
                            var buyItemId = (uint)(int)actionEntry.Item2[1];
                            var buyQuantity = (uint)(int)actionEntry.Item2[2];
                            builder.Do($"Buy Item {buyItemId} x{buyQuantity} from vendor {buyVendorGuid:X}", time =>
                            {
                                _objectManager.BuyItemFromVendorAsync(buyVendorGuid, buyItemId, buyQuantity, CancellationToken.None)
                                    .GetAwaiter().GetResult();
                                return BehaviourTreeStatus.Success;
                            });
                        }
                        else
                        {
                            builder.Splice(_interaction.BuildBuyItemSequence((int)actionEntry.Item2[0], (int)actionEntry.Item2[1]));
                        }
                        break;
                    case CharacterAction.BuybackItem:
                        if (actionEntry.Item2.Count >= 3)
                        {
                            var buybackVendorGuid = UnboxGuid(actionEntry.Item2[0]);
                            var buybackSlot = (uint)(int)actionEntry.Item2[1];
                            builder.Do($"Buyback Item slot {buybackSlot} from vendor {buybackVendorGuid:X}", time =>
                            {
                                var factory = _agentFactoryAccessor?.Invoke();
                                if (factory != null)
                                {
                                    factory.VendorAgent.BuybackItemAsync(buybackVendorGuid, buybackSlot, CancellationToken.None)
                                        .GetAwaiter().GetResult();
                                    return BehaviourTreeStatus.Success;
                                }
                                Log.Warning("[BOT RUNNER] AgentFactory unavailable for BuybackItem packet path");
                                return BehaviourTreeStatus.Failure;
                            });
                        }
                        else
                        {
                            builder.Splice(_interaction.BuildBuybackItemSequence((int)actionEntry.Item2[0], (int)actionEntry.Item2[1]));
                        }
                        break;
                    case CharacterAction.SellItem:
                        if (actionEntry.Item2.Count >= 4)
                        {
                            var sellVendorGuid = UnboxGuid(actionEntry.Item2[0]);
                            var sellBagId = (byte)(int)actionEntry.Item2[1];
                            var sellSlotId = (byte)(int)actionEntry.Item2[2];
                            var sellQuantity = (uint)(int)actionEntry.Item2[3];
                            builder.Do($"Sell Item bag={sellBagId} slot={sellSlotId} x{sellQuantity} to vendor {sellVendorGuid:X}", time =>
                            {
                                _objectManager.SellItemToVendorAsync(sellVendorGuid, sellBagId, sellSlotId, sellQuantity, CancellationToken.None)
                                    .GetAwaiter().GetResult();
                                return BehaviourTreeStatus.Success;
                            });
                        }
                        else
                        {
                            builder.Splice(_interaction.BuildSellItemSequence((int)actionEntry.Item2[0], (int)actionEntry.Item2[1], (int)actionEntry.Item2[2]));
                        }
                        break;
                    case CharacterAction.RepairItem:
                        if (actionEntry.Item2.Count >= 2)
                        {
                            var repairItemVendorGuid = UnboxGuid(actionEntry.Item2[0]);
                            var repairSlot = (int)actionEntry.Item2[1];
                            builder.Do($"Repair slot {repairSlot} at vendor {repairItemVendorGuid:X}", time =>
                            {
                                _objectManager.RepairAllItemsAsync(repairItemVendorGuid, CancellationToken.None)
                                    .GetAwaiter().GetResult();
                                return BehaviourTreeStatus.Success;
                            });
                        }
                        else
                        {
                            builder.Splice(_interaction.BuildRepairItemSequence((int)actionEntry.Item2[0]));
                        }
                        break;
                    case CharacterAction.RepairAllItems:
                        if (actionEntry.Item2.Count >= 1)
                        {
                            var repairVendorGuid = UnboxGuid(actionEntry.Item2[0]);
                            builder.Do($"Repair All Items at vendor {repairVendorGuid:X}", time =>
                            {
                                _objectManager.RepairAllItemsAsync(repairVendorGuid, CancellationToken.None)
                                    .GetAwaiter().GetResult();
                                return BehaviourTreeStatus.Success;
                            });
                        }
                        else
                        {
                            builder.Splice(_interaction.RepairAllItemsSequence);
                        }
                        break;

                    case CharacterAction.VisitVendor:
                        builder.Do("Queue Vendor Visit Task", time =>
                        {
                            if (botTasks.Count == 0 || botTasks.Peek() is not Tasks.VendorVisitTask)
                                botTasks.Push(new Tasks.VendorVisitTask(context));
                            return BehaviourTreeStatus.Success;
                        });
                        break;

                    case CharacterAction.DismissBuff:
                        builder.Splice(_interaction.BuildDismissBuffSequence((string)actionEntry.Item2[0]));
                        break;

                    case CharacterAction.Resurrect:
                        builder.Splice(_combat.ResurrectSequence);
                        break;

                    case CharacterAction.Craft:
                        if (actionEntry.Item2.Count >= 2)
                        {
                            var craftSpellId = (int)actionEntry.Item2[0];
                            var craftQuantity = (int)actionEntry.Item2[1];
                            builder.Do($"Craft spell {craftSpellId} x{craftQuantity} (packet)", time =>
                            {
                                for (int i = 0; i < craftQuantity; i++)
                                {
                                    _objectManager.CastSpell(craftSpellId);
                                    if (craftQuantity > 1 && i < craftQuantity - 1)
                                        Thread.Sleep(500);
                                }
                                return BehaviourTreeStatus.Success;
                            });
                        }
                        else
                        {
                            builder.Splice(_interaction.BuildCraftSequence((int)actionEntry.Item2[0]));
                        }
                        break;

                    case CharacterAction.Login:
                        builder.Splice(_interaction.BuildLoginSequence((string)actionEntry.Item2[0], (string)actionEntry.Item2[1]));
                        break;
                    case CharacterAction.Logout:
                        builder.Splice(_interaction.LogoutSequence);
                        break;
                    case CharacterAction.CreateCharacter:
                        builder.Splice(_interaction.BuildCreateCharacterSequence(actionEntry.Item2));
                        break;
                    case CharacterAction.DeleteCharacter:
                        builder.Splice(_interaction.BuildDeleteCharacterSequence((ulong)actionEntry.Item2[0]));
                        break;
                    case CharacterAction.EnterWorld:
                        builder.Splice(_interaction.BuildEnterWorldSequence((ulong)actionEntry.Item2[0]));
                        break;

                    case CharacterAction.GatherNode:
                        var gatherGuid = UnboxGuid(actionEntry.Item2[0]);
                        int gatherSpellId = actionEntry.Item2.Count > 1 ? (int)actionEntry.Item2[1] : 0;
                        builder.Splice(_movement.BuildGatherNodeSequence(gatherGuid, gatherSpellId));
                        break;

                    case CharacterAction.SendChat:
                        var chatMsg = (string)actionEntry.Item2[0];

                        if (chatMsg.Equals(".targetself", StringComparison.OrdinalIgnoreCase))
                        {
                            builder.Do("Target Self", time =>
                            {
                                var player = _objectManager.Player;
                                if (player == null) return BehaviourTreeStatus.Failure;
                                _objectManager.SetTarget(player.Guid);
                                Log.Information("[BOT RUNNER] Self-targeted (GUID=0x{Guid:X})", player.Guid);
                                return BehaviourTreeStatus.Success;
                            });
                            break;
                        }

                        builder.Do($"Send Chat: {chatMsg}", time =>
                        {
                            var player = _objectManager.Player;
                            var isDeadOrGhost = player != null && _isDeadOrGhostState(player);
                            _diagLog($"SENDCHAT-ACTION: chatMsg='{chatMsg}' dead={isDeadOrGhost} health={player?.Health ?? 0}");
                            if (isDeadOrGhost)
                            {
                                Log.Information("[BOT RUNNER] Skipping chat while dead/ghost: {ChatMessage}", chatMsg);
                                return BehaviourTreeStatus.Success;
                            }

                            Log.Information($"[BOT RUNNER] Sending chat message: {chatMsg}");
                            var gmResponses = SendChatThroughBestAvailablePath(_objectManager, chatMsg);
                            foreach (var response in gmResponses)
                            {
                                if (!string.IsNullOrWhiteSpace(response))
                                    _enqueueDiagnosticMessage($"[SYSTEM] {response}");
                            }
                            _diagLog($"SENDCHAT-SENT: '{chatMsg}'");
                            return BehaviourTreeStatus.Success;
                        });
                        break;

                    case CharacterAction.SetFacing:
                        var facingAngle = (float)actionEntry.Item2[0];
                        builder.Do($"Set Facing: {facingAngle:F2}", time =>
                        {
                            Log.Information("[BOT RUNNER] Setting facing to {Facing:F2} rad", facingAngle);
                            _objectManager.SetFacing(facingAngle);
                            return BehaviourTreeStatus.Success;
                        });
                        break;

                    case CharacterAction.ReleaseCorpse:
                        builder.Do("Release Spirit", time =>
                        {
                            var player = _objectManager.Player;
                            if (player == null)
                                return BehaviourTreeStatus.Success;

                            if (_isGhostState(player))
                            {
                                Log.Information("[BOT RUNNER] Skipping ReleaseCorpse: player is already ghosted.");
                                return BehaviourTreeStatus.Success;
                            }

                            if (!_isCorpseState(player))
                            {
                                Log.Information("[BOT RUNNER] Skipping ReleaseCorpse: player is not in corpse state.");
                                return BehaviourTreeStatus.Success;
                            }

                            if (DateTime.UtcNow - _getLastReleaseSpiritCommandUtc() < TimeSpan.FromSeconds(2))
                            {
                                Log.Debug("[BOT RUNNER] Skipping duplicate ReleaseCorpse command within cooldown window.");
                                return BehaviourTreeStatus.Success;
                            }

                            _setLastReleaseSpiritCommandUtc(DateTime.UtcNow);
                            Log.Information("[BOT RUNNER] Releasing spirit (CMSG_REPOP_REQUEST)");
                            _objectManager.ReleaseSpirit();
                            return BehaviourTreeStatus.Success;
                        });
                        break;

                    case CharacterAction.RetrieveCorpse:
                        builder.Do("Retrieve Corpse", time =>
                        {
                            var player = _objectManager.Player;
                            _diagLog($"[RETRIEVE_DIAG] player={player != null} playerFlags=0x{(player != null ? (uint)player.PlayerFlags : 0u):X} hp={player?.Health ?? -1u}/{player?.MaxHealth ?? -1u}");
                            if (player != null)
                            {
                                var ghostResult = _isGhostState(player);
                                _diagLog($"[RETRIEVE_DIAG] IsGhostState={ghostResult} HasGhostFlag={DeathStateDetection.HasGhostFlag(player)}");
                                if (ghostResult)
                                {
                                    var corpsePos = player.CorpsePosition;
                                    _diagLog($"[RETRIEVE_DIAG] corpsePos=({corpsePos?.X:F1},{corpsePos?.Y:F1},{corpsePos?.Z:F1})");
                                    var lastAlive = _getLastKnownAlivePosition();
                                    if (BotRunnerService.IsZeroPosition(corpsePos) && lastAlive != null)
                                    {
                                        corpsePos = new GameData.Core.Models.Position(
                                            lastAlive.X,
                                            lastAlive.Y,
                                            lastAlive.Z);
                                        _diagLog($"[RETRIEVE_DIAG] using fallback corpsePos=({corpsePos.X:F1},{corpsePos.Y:F1},{corpsePos.Z:F1})");
                                    }

                                    if (corpsePos.X != 0 || corpsePos.Y != 0 || corpsePos.Z != 0)
                                    {
                                        if (botTasks.Count == 0 || botTasks.Peek() is not Tasks.RetrieveCorpseTask)
                                        {
                                            Log.Information("[BOT RUNNER] Queueing pathfinding corpse run to ({X:F0}, {Y:F0}, {Z:F0})",
                                                corpsePos.X, corpsePos.Y, corpsePos.Z);
                                            _diagLog($"[RETRIEVE_DIAG] PUSHING RetrieveCorpseTask corpse=({corpsePos.X:F1},{corpsePos.Y:F1},{corpsePos.Z:F1})");
                                            botTasks.Push(new Tasks.RetrieveCorpseTask(context, corpsePos));
                                        }
                                        else
                                        {
                                            _diagLog("[RETRIEVE_DIAG] RetrieveCorpseTask already on stack");
                                        }
                                        return BehaviourTreeStatus.Success;
                                    }
                                    else
                                    {
                                        _diagLog("[RETRIEVE_DIAG] corpsePos is ZERO, skipping task push");
                                    }
                                }

                                var reclaimDelay = player.CorpseRecoveryDelaySeconds;
                                if (reclaimDelay > 0)
                                {
                                    Log.Information("[BOT RUNNER] Corpse reclaim cooldown active ({Seconds}s remaining); waiting.", reclaimDelay);
                                    _diagLog($"[RETRIEVE_DIAG] reclaimDelay={reclaimDelay}s -- NOT pushing task");
                                    return BehaviourTreeStatus.Success;
                                }
                            }

                            Log.Information("[BOT RUNNER] Retrieving corpse (CMSG_RECLAIM_CORPSE direct)");
                            _diagLog("[RETRIEVE_DIAG] fallthrough to direct RetrieveCorpse()");
                            _objectManager.RetrieveCorpse();
                            return BehaviourTreeStatus.Success;
                        });
                        break;

                    case CharacterAction.LootCorpse:
                    {
                        var lootGuid = UnboxGuid(actionEntry.Item2[0]);
                        builder.Do($"Loot Corpse {lootGuid:X}", time =>
                        {
                            Log.Information("[BOT RUNNER] Looting corpse {Guid:X}", lootGuid);
                            _objectManager.LootTargetAsync(lootGuid, CancellationToken.None)
                                .GetAwaiter().GetResult();
                            return BehaviourTreeStatus.Success;
                        });
                        break;
                    }

                    case CharacterAction.SkinCorpse:
                    {
                        var skinGuid = UnboxGuid(actionEntry.Item2[0]);
                        builder.Do($"Skin Corpse {skinGuid:X}", time =>
                        {
                            Log.Information("[BOT RUNNER] Skinning corpse {Guid:X}", skinGuid);
                            _objectManager.LootTargetAsync(skinGuid, CancellationToken.None)
                                .GetAwaiter().GetResult();
                            return BehaviourTreeStatus.Success;
                        });
                        break;
                    }

                    case CharacterAction.CheckMail:
                    {
                        var mailboxGuid = UnboxGuid(actionEntry.Item2[0]);
                        builder.Do($"Check Mail at mailbox {mailboxGuid:X}", time =>
                        {
                            Log.Information("[BOT RUNNER] Collecting mail from mailbox {Guid:X}", mailboxGuid);
                            _objectManager.CollectAllMailAsync(mailboxGuid, CancellationToken.None)
                                .GetAwaiter().GetResult();
                            return BehaviourTreeStatus.Success;
                        });
                        break;
                    }

                    case CharacterAction.ConvertToRaid:
                        builder.Do("Convert Party to Raid", time =>
                        {
                            _objectManager.ConvertToRaid();
                            return BehaviourTreeStatus.Success;
                        });
                        break;

                    case CharacterAction.ChangeRaidSubgroup:
                    {
                        var rsgName = actionEntry.Item2.Count > 0 ? (string)actionEntry.Item2[0] : "";
                        var rsgGroup = actionEntry.Item2.Count > 1 ? (byte)(int)actionEntry.Item2[1] : (byte)0;
                        builder.Do($"Change Raid Subgroup: {rsgName} -> group {rsgGroup}", time =>
                        {
                            _objectManager.ChangeRaidSubgroup(rsgName, rsgGroup);
                            return BehaviourTreeStatus.Success;
                        });
                        break;
                    }

                    case CharacterAction.StartDungeoneering:
                    {
                        bool isLeader = actionEntry.Item2.Count > 0 && (int)actionEntry.Item2[0] == 1;
                        uint targetMapId = actionEntry.Item2.Count > 1 ? (uint)(int)actionEntry.Item2[1] : 0;
                        var waypointPositions = actionEntry.Item2.Count > 2
                            ? ParseGatheringRoutePositions(actionEntry.Item2.Skip(2))
                            : null;

                        builder.Do("Queue Dungeoneering Task", time =>
                        {
                            var existingTask = botTasks.OfType<Tasks.Dungeoneering.DungeoneeringTask>().FirstOrDefault();
                            Log.Information("[BOT RUNNER] StartDungeoneering: isLeader={IsLeader}, existing={Existing}, existingIsLeader={ExLeader}",
                                isLeader, existingTask != null, existingTask?.IsLeader);
                            if (existingTask != null && existingTask.IsLeader == isLeader)
                            {
                                // Same role task already running
                            }
                            else
                            {
                                uint mapId = targetMapId != 0
                                    ? targetMapId
                                    : (_objectManager.Player?.MapId ?? 0);
                                var mapWaypoints = Tasks.Dungeoneering.DungeonWaypoints.GetWaypointsForMap(mapId);
                                IReadOnlyList<GameData.Core.Models.Position>? waypoints = waypointPositions?.Count > 0
                                    ? waypointPositions
                                    : mapWaypoints;

                                botTasks.Push(new Tasks.Dungeoneering.DungeoneeringTask(context, isLeader, waypoints, mapId));
                            }
                            return BehaviourTreeStatus.Success;
                        });
                        break;
                    }

                    case CharacterAction.FollowTarget:
                    {
                        var followGuid = UnboxGuid(actionEntry.Item2[0]);
                        var followDistance = actionEntry.Item2.Count > 1 ? (float)actionEntry.Item2[1] : 5.0f;
                        builder.Splice(_movement.BuildFollowTargetSequence(followGuid, followDistance));
                        break;
                    }

                    case CharacterAction.JoinBattleground:
                    {
                        var bgTypeId = (int)actionEntry.Item2[0];
                        var expectedMapId = actionEntry.Item2.Count > 1 ? (uint)(int)actionEntry.Item2[1] : 0u;

                        builder.Do("Queue BG Join Task", time =>
                        {
                            WoWSharpClient.Networking.ClientComponents.BattlegroundNetworkClientComponent? bgClient = null;
                            var factory = _agentFactoryAccessor?.Invoke();
                            if (factory != null)
                            {
                                bgClient = factory.BattlegroundAgent;
                            }

                            botTasks.Push(new Tasks.Battlegrounds.BattlegroundQueueTask(
                                context,
                                (BotRunner.Travel.BattlemasterData.BattlegroundType)bgTypeId,
                                expectedMapId,
                                bgClient));

                            return BehaviourTreeStatus.Success;
                        });
                        break;
                    }

                    case CharacterAction.AcceptBattleground:
                    {
                        builder.Do("Accept BG Invite", time =>
                        {
                            var factory = _agentFactoryAccessor?.Invoke();
                            var bgClient = factory?.BattlegroundAgent;
                            if (bgClient != null)
                            {
                                bgClient.AcceptInviteAsync().GetAwaiter().GetResult();
                                Log.Information("[BOT RUNNER] BG invite accepted");
                            }
                            else
                            {
                                _objectManager.AcceptBattlegroundInvite();
                                Log.Information("[BOT RUNNER] BG invite accepted via ObjectManager");
                            }
                            return BehaviourTreeStatus.Success;
                        });
                        break;
                    }

                    case CharacterAction.LeaveBattleground:
                    {
                        builder.Do("Leave BG", time =>
                        {
                            var factory = _agentFactoryAccessor?.Invoke();
                            var bgClient = factory?.BattlegroundAgent;
                            if (bgClient != null)
                            {
                                bgClient.LeaveAsync().GetAwaiter().GetResult();
                                Log.Information("[BOT RUNNER] Left battleground");
                            }
                            else
                            {
                                _objectManager.LeaveBattleground();
                                Log.Information("[BOT RUNNER] Cleared battleground queue via ObjectManager");
                            }
                            return BehaviourTreeStatus.Success;
                        });
                        break;
                    }

                    case CharacterAction.TravelTo:
                    {
                        var targetMapId = (uint)Convert.ToInt32(actionEntry.Item2[0]);
                        var targetX = Convert.ToSingle(actionEntry.Item2[1]);
                        var targetY = Convert.ToSingle(actionEntry.Item2[2]);
                        var targetZ = Convert.ToSingle(actionEntry.Item2[3]);
                        Position[]? travelPath = null;
                        int travelWaypointIdx = 0;
                        builder.Do($"TravelTo map={targetMapId} ({targetX:F0},{targetY:F0},{targetZ:F0})", time =>
                        {
                            if (_objectManager.Player.MapId != targetMapId)
                            {
                                Log.Warning("[BOT RUNNER] TravelTo cross-map not yet implemented (target map {Map})", targetMapId);
                                return BehaviourTreeStatus.Failure;
                            }

                            var target = new Position(targetX, targetY, targetZ);
                            var finalDist = _objectManager.Player.Position.DistanceTo2D(target);
                            if (finalDist <= 15f)
                            {
                                _objectManager.StopAllMovement();
                                return BehaviourTreeStatus.Success;
                            }

                            if (travelPath == null)
                            {
                                try
                                {
                                    var pfClient = _container.PathfindingClient;
                                    travelPath = pfClient.GetPath(targetMapId,
                                        _objectManager.Player.Position, target,
                                        nearbyObjects: null,
                                        smoothPath: true,
                                        race: _objectManager.Player.Race,
                                        gender: _objectManager.Player.Gender);
                                    travelWaypointIdx = 0;
                                    Log.Information("[TRAVEL] Path computed: {Count} waypoints to ({X:F0},{Y:F0},{Z:F0})",
                                        travelPath.Length, targetX, targetY, targetZ);
                                }
                                catch (Exception ex)
                                {
                                    Log.Warning(ex, "[TRAVEL] Pathfinding failed, using direct line");
                                    travelPath = [];
                                }
                            }

                            if (travelPath.Length > 0 && travelWaypointIdx < travelPath.Length)
                            {
                                var wp = travelPath[travelWaypointIdx];
                                var wpDist = _objectManager.Player.Position.DistanceTo2D(wp);
                                if (wpDist <= 3f)
                                {
                                    travelWaypointIdx++;
                                    if (travelWaypointIdx >= travelPath.Length)
                                    {
                                        _objectManager.MoveToward(target);
                                        return BehaviourTreeStatus.Running;
                                    }
                                    wp = travelPath[travelWaypointIdx];
                                }
                                _objectManager.MoveToward(wp);
                            }
                            else
                            {
                                _objectManager.MoveToward(target);
                            }
                            return BehaviourTreeStatus.Running;
                        });
                        break;
                    }

                    default:
                        break;
                }
            }

            return builder.End().Build();
        }

        internal static List<uint> ParseGatheringEntries(string csv)
            => csv.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(token => uint.TryParse(token, out var entry) ? entry : 0u)
                .Where(entry => entry != 0)
                .Distinct()
                .ToList();

        internal static List<Position> ParseGatheringRoutePositions(IEnumerable<object> rawParameters)
        {
            var floats = rawParameters
                .Select(parameter => parameter switch
                {
                    float floatParam => (float?)floatParam,
                    int intParam => intParam,
                    long longParam => longParam,
                    _ => null
                })
                .Where(value => value.HasValue)
                .Select(value => value!.Value)
                .ToList();

            var positions = new List<Position>(floats.Count / 3);
            for (int index = 0; index + 2 < floats.Count; index += 3)
                positions.Add(new Position(floats[index], floats[index + 1], floats[index + 2]));

            return positions;
        }

        /// <summary>
        /// BotRunnerContext implementation shared between ActionDispatcher and BotRunnerService.
        /// </summary>
        internal sealed class BotRunnerContext(
            IObjectManager objectManager,
            Stack<IBotTask> tasks,
            IDependencyContainer container,
            Constants.BotBehaviorConfig config,
            Action<string> addDiagnosticMessage) : IBotContext
        {
            public IObjectManager ObjectManager => objectManager;
            public Stack<IBotTask> BotTasks => tasks;
            public IDependencyContainer Container => container;
            public Constants.BotBehaviorConfig Config => config;
            public IWoWEventHandler EventHandler => objectManager.EventHandler;
            public void AddDiagnosticMessage(string message) => addDiagnosticMessage(message);
        }
    }
}
