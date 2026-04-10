using Communication;
using GameData.Core.Enums;
using GameData.Core.Interfaces;
using GameData.Core.Models;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Xas.FluentBehaviourTree;

namespace BotRunner
{
    public partial class BotRunnerService
    {
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

        private IBehaviourTreeNode BuildBehaviorTreeFromActions(List<(CharacterAction, List<object>)> actionMap)
        {
            var context = new BotRunnerContext(_objectManager, _botTasks, _container, _behaviorConfig, EnqueueDiagnosticMessage);
            var builder = new BehaviourTreeBuilder()
                .Sequence("StateManager Action Sequence");

            // Iterate over the action map and build sequences for each action with its parameters
            foreach (var actionEntry in actionMap)
            {
                switch (actionEntry.Item1)
                {
                    case CharacterAction.Wait:
                        builder.Splice(BuildWaitSequence((float)actionEntry.Item2[0]));
                        break;
                    case CharacterAction.GoTo:
                    {
                        // Upsert a persistent GoToTask instead of pushing a new one every
                        // poll cycle. Repeated Goto dispatches can otherwise reset the task's
                        // cached NavigationPath and stall long routes.
                        var gotoX = (float)actionEntry.Item2[0];
                        var gotoY = (float)actionEntry.Item2[1];
                        var gotoZ = (float)actionEntry.Item2[2];
                        var gotoTolerance = (float)actionEntry.Item2[3];
                        builder.Do("Upsert GoTo Task", time =>
                        {
                            var result = UpsertGoToTask(_botTasks, context, gotoX, gotoY, gotoZ, gotoTolerance);
                            if (result != GoToTaskUpsertResult.Duplicate)
                            {
                                Log.Information("[BOT RUNNER] GoTo upsert: {Result} target=({X:F1},{Y:F1},{Z:F1}) tolerance={Tolerance:F1}",
                                    result, gotoX, gotoY, gotoZ, gotoTolerance);
                            }
                            return BehaviourTreeStatus.Success;
                        });
                        break;
                    }
                    case CharacterAction.InteractWith:
                    {
                        var interactGuid = UnboxGuid(actionEntry.Item2[0]);
                        // Try GameObjects first; fall back to NPC interaction via AgentFactory
                        var isGameObject = _objectManager.GameObjects.Any(x => x.Guid == interactGuid);
                        if (isGameObject)
                        {
                            builder.Splice(BuildInteractWithSequence(interactGuid));
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
                        // With npcGuid + optionIndex: packet-based via GossipAgent (BG compatible)
                        // Without: legacy GossipFrame path (FG only)
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
                            builder.Splice(_interactionSequences.BuildSelectGossipSequence((int)actionEntry.Item2[0]));
                        }
                        break;

                    case CharacterAction.SelectTaxiNode:
                        // With flightMasterGuid + sourceNodeId + destinationNodeId: packet-based (BG compatible)
                        // Without: legacy TaxiFrame path (FG only)
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
                            builder.Splice(_interactionSequences.BuildSelectTaxiNodeSequence((int)actionEntry.Item2[0]));
                        }
                        break;

                    case CharacterAction.VisitFlightMaster:
                        builder.Do("Queue Flight Master Visit Task", time =>
                        {
                            // LiveValidation/NpcInteractionTests uses this task-owned path for BG taxi discovery coverage.
                            if (_botTasks.Count == 0 || _botTasks.Peek() is not Tasks.FlightMasterVisitTask)
                                _botTasks.Push(new Tasks.FlightMasterVisitTask(context));
                            return BehaviourTreeStatus.Success;
                        });
                        break;

                    case CharacterAction.AcceptQuest:
                        // With params: [0]=npcGuid, [1]=questId — packet-based via AgentFactory
                        // Without params: legacy QuestFrame path (FG only)
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
                            builder.Splice(_interactionSequences.AcceptQuestSequence);
                        }
                        break;
                    case CharacterAction.DeclineQuest:
                        builder.Splice(_interactionSequences.DeclineQuestSequence);
                        break;
                    case CharacterAction.AbandonQuest:
                        // Params: [0]=questLogSlot (byte index in quest log)
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
                        builder.Splice(_interactionSequences.BuildSelectRewardSequence((int)actionEntry.Item2[0]));
                        break;
                    case CharacterAction.CompleteQuest:
                        // With params: [0]=npcGuid, [1]=questId, optional [2]=rewardIndex
                        // Without params: legacy QuestFrame path (FG only)
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
                            builder.Splice(_interactionSequences.CompleteQuestSequence);
                        }
                        break;

                    case CharacterAction.TrainSkill:
                        // With trainerGuid + spellId: packet-based via TrainerAgent (BG compatible)
                        // Without: legacy TrainerFrame path (FG only)
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
                            builder.Splice(_interactionSequences.BuildTrainSkillSequence((int)actionEntry.Item2[0]));
                        }
                        break;
                    case CharacterAction.TrainTalent:
                        // With talentId as sole param AND agentFactory available: packet-based (BG compatible)
                        // The frame path takes a talentSpellId; the packet path takes a talentId.
                        // When 2+ params: [0]=talentId — use packet path via TalentAgent
                        // When 1 param: legacy TalentFrame path (FG only)
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
                            builder.Splice(_interactionSequences.BuildLearnTalentSequence((int)actionEntry.Item2[0]));
                        }
                        break;

                    case CharacterAction.VisitTrainer:
                        builder.Do("Queue Trainer Visit Task", time =>
                        {
                            // LiveValidation/NpcInteractionTests uses this task-owned path for BG trainer coverage.
                            if (_botTasks.Count == 0 || _botTasks.Peek() is not Tasks.TrainerVisitTask)
                                _botTasks.Push(new Tasks.TrainerVisitTask(context));
                            return BehaviourTreeStatus.Success;
                        });
                        break;

                    case CharacterAction.OfferTrade:
                        // Always packet-based: CMSG_INITIATE_TRADE
                        builder.Do("Initiate Trade (packet)", time =>
                        {
                            _objectManager.InitiateTradeAsync(UnboxGuid(actionEntry.Item2[0]), CancellationToken.None)
                                .GetAwaiter().GetResult();
                            return BehaviourTreeStatus.Success;
                        });
                        break;
                    case CharacterAction.OfferGold:
                        // BG: CMSG_SET_TRADE_GOLD, FG: TradeFrame
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
                            builder.Splice(_interactionSequences.BuildOfferMoneySequence((int)actionEntry.Item2[0]));
                        }
                        break;
                    case CharacterAction.OfferItem:
                        // BG: CMSG_SET_TRADE_ITEM, FG: TradeFrame
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
                            builder.Splice(_interactionSequences.BuildOfferItemSequence((int)actionEntry.Item2[0], (int)actionEntry.Item2[1], (int)actionEntry.Item2[2], (int)actionEntry.Item2[3]));
                        }
                        break;
                    case CharacterAction.AcceptTrade:
                        // BG: CMSG_ACCEPT_TRADE, FG: TradeFrame
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
                            builder.Splice(_interactionSequences.AcceptTradeSequence);
                        }
                        break;
                    case CharacterAction.DeclineTrade:
                        // BG: CMSG_CANCEL_TRADE, FG: TradeFrame
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
                            builder.Splice(_interactionSequences.DeclineTradeSequence);
                        }
                        break;
                    case CharacterAction.EnchantTrade:
                        builder.Splice(_interactionSequences.BuildOfferEnchantSequence((int)actionEntry.Item2[0]));
                        break;
                    case CharacterAction.LockpickTrade:
                        builder.Splice(_interactionSequences.OfferLockpickSequence);
                        break;

                    case CharacterAction.PromoteLeader:
                        builder.Splice(_interactionSequences.BuildPromoteLeaderSequence(UnboxGuid(actionEntry.Item2[0])));
                        break;
                    case CharacterAction.PromoteAssistant:
                        builder.Splice(_interactionSequences.BuildPromoteAssistantSequence(UnboxGuid(actionEntry.Item2[0])));
                        break;
                    case CharacterAction.PromoteLootManager:
                        builder.Splice(_interactionSequences.BuildPromoteLootManagerSequence(UnboxGuid(actionEntry.Item2[0])));
                        break;
                    case CharacterAction.SetGroupLoot:
                        builder.Splice(_interactionSequences.BuildSetGroupLootSequence((GroupLootSetting)actionEntry.Item2[0]));
                        break;
                    case CharacterAction.AssignLoot:
                        builder.Splice(_interactionSequences.BuildAssignLootSequence((int)actionEntry.Item2[0], UnboxGuid(actionEntry.Item2[1])));
                        break;

                    case CharacterAction.LootRollNeed:
                        builder.Splice(_interactionSequences.BuildLootRollNeedSequence((int)actionEntry.Item2[0]));
                        break;
                    case CharacterAction.LootRollGreed:
                        builder.Splice(_interactionSequences.BuildLootRollGreedSequence((int)actionEntry.Item2[0]));
                        break;
                    case CharacterAction.LootPass:
                        builder.Splice(_interactionSequences.BuildLootPassSequence((int)actionEntry.Item2[0]));
                        break;

                    case CharacterAction.SendGroupInvite:
                        if (actionEntry.Item2[0] is string playerName)
                            builder.Splice(_interactionSequences.BuildSendGroupInviteByNameSequence(playerName));
                        else
                            builder.Splice(_interactionSequences.BuildSendGroupInviteSequence(UnboxGuid(actionEntry.Item2[0])));
                        break;
                    case CharacterAction.AcceptGroupInvite:
                        builder.Splice(_interactionSequences.AcceptGroupInviteSequence);
                        break;
                    case CharacterAction.DeclineGroupInvite:
                        builder.Splice(_interactionSequences.DeclineGroupInviteSequence);
                        break;
                    case CharacterAction.KickPlayer:
                        builder.Splice(_interactionSequences.BuildKickPlayerSequence(UnboxGuid(actionEntry.Item2[0])));
                        break;
                    case CharacterAction.LeaveGroup:
                        builder.Splice(_interactionSequences.LeaveGroupSequence);
                        break;
                    case CharacterAction.DisbandGroup:
                        builder.Splice(_interactionSequences.DisbandGroupSequence);
                        break;
                    case CharacterAction.StartMeleeAttack:
                        builder.Splice(BuildStartMeleeAttackSequence(UnboxGuid(actionEntry.Item2[0])));
                        break;
                    case CharacterAction.StartRangedAttack:
                        builder.Splice(BuildStartRangedAttackSequence(UnboxGuid(actionEntry.Item2[0])));
                        break;
                    case CharacterAction.StartWandAttack:
                        builder.Splice(BuildStartWandAttackSequence(UnboxGuid(actionEntry.Item2[0])));
                        break;
                    case CharacterAction.StopAttack:
                        builder.Splice(StopAttackSequence);
                        break;
                    case CharacterAction.CastSpell:
                        var castTargetGuid = actionEntry.Item2.Count > 1 ? UnboxGuid(actionEntry.Item2[1]) : 0UL;
                        builder.Splice(BuildCastSpellSequence((int)actionEntry.Item2[0], castTargetGuid));
                        break;
                    case CharacterAction.StartFishing:
                    {
                        var fishingSearchWaypoints = ParseGatheringRoutePositions(actionEntry.Item2);
                        builder.Do("Queue Fishing Task", time =>
                        {
                            if (_botTasks.Count == 0 || _botTasks.Peek() is not Tasks.FishingTask)
                                _botTasks.Push(new Tasks.FishingTask(context, fishingSearchWaypoints.Count > 0 ? fishingSearchWaypoints : null));
                            return BehaviourTreeStatus.Success;
                        });
                        break;
                    }
                    case CharacterAction.StartGatheringRoute:
                    {
                        int gatheringRouteSpellId = (int)actionEntry.Item2[0];
                        var allowedEntries = ParseGatheringEntries((string)actionEntry.Item2[1]);
                        // Optional [2] = maxRouteLoops (int), then [3+] = float positions.
                        // If [2] is an int (not a float), treat it as maxRouteLoops; otherwise positions start at [2].
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

                            if (_botTasks.Count == 0 || _botTasks.Peek() is not Tasks.GatheringRouteTask)
                                _botTasks.Push(new Tasks.GatheringRouteTask(context, routePositions, allowedEntries, gatheringRouteSpellId, maxRouteLoops: routeLoops));
                            return BehaviourTreeStatus.Success;
                        });
                        break;
                    }
                    case CharacterAction.StopCast:
                        builder.Splice(StopCastSequence);
                        break;

                    case CharacterAction.UseItem:
                        if (actionEntry.Item2.Count >= 3)
                            builder.Splice(_interactionSequences.BuildUseItemSequence((int)actionEntry.Item2[0], (int)actionEntry.Item2[1], (ulong)actionEntry.Item2[2]));
                        else
                            builder.Splice(_interactionSequences.BuildUseItemByIdSequence((int)actionEntry.Item2[0]));
                        break;
                    case CharacterAction.EquipItem:
                        if (actionEntry.Item2.Count >= 3)
                            builder.Splice(_interactionSequences.BuildEquipItemSequence((int)actionEntry.Item2[0], (int)actionEntry.Item2[1], (EquipSlot)actionEntry.Item2[2]));
                        else if (actionEntry.Item2.Count >= 2)
                            builder.Splice(_interactionSequences.BuildEquipItemSequence((int)actionEntry.Item2[0], (int)actionEntry.Item2[1]));
                        else
                            builder.Splice(_interactionSequences.BuildEquipItemByIdSequence((int)actionEntry.Item2[0]));
                        break;
                    case CharacterAction.UnequipItem:
                        builder.Splice(_interactionSequences.BuildUnequipItemSequence((EquipSlot)actionEntry.Item2[0]));
                        break;
                    case CharacterAction.DestroyItem:
                        builder.Splice(_interactionSequences.BuildDestroyItemSequence((int)actionEntry.Item2[0], (int)actionEntry.Item2[1], (int)actionEntry.Item2[2]));
                        break;
                    case CharacterAction.MoveItem:
                        builder.Splice(_interactionSequences.BuildMoveItemSequence((int)actionEntry.Item2[0], (int)actionEntry.Item2[1], (int)actionEntry.Item2[2], (int)actionEntry.Item2[3], (int)actionEntry.Item2[4]));
                        break;
                    case CharacterAction.SplitStack:
                        builder.Splice(_interactionSequences.BuildSplitStackSequence((int)actionEntry.Item2[0],
                            (int)actionEntry.Item2[1],
                            (int)actionEntry.Item2[2],
                            (int)actionEntry.Item2[3],
                            (int)actionEntry.Item2[4]));
                        break;

                    case CharacterAction.BuyItem:
                        // With vendorGuid: [0]=vendorGuid, [1]=itemId, [2]=quantity — packet-based
                        // Without: [0]=slotId, [1]=quantity — legacy MerchantFrame (FG only)
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
                            builder.Splice(_interactionSequences.BuildBuyItemSequence((int)actionEntry.Item2[0], (int)actionEntry.Item2[1]));
                        }
                        break;
                    case CharacterAction.BuybackItem:
                        // With vendorGuid + buybackSlot: packet-based via VendorAgent (BG compatible)
                        // Without: legacy MerchantFrame path (FG only)
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
                            builder.Splice(_interactionSequences.BuildBuybackItemSequence((int)actionEntry.Item2[0], (int)actionEntry.Item2[1]));
                        }
                        break;
                    case CharacterAction.SellItem:
                        // With vendorGuid: [0]=vendorGuid, [1]=bagId, [2]=slotId, [3]=quantity — packet-based
                        // Without: [0]=bagId, [1]=slotId, [2]=quantity — legacy MerchantFrame (FG only)
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
                            builder.Splice(_interactionSequences.BuildSellItemSequence((int)actionEntry.Item2[0], (int)actionEntry.Item2[1], (int)actionEntry.Item2[2]));
                        }
                        break;
                    case CharacterAction.RepairItem:
                        // With vendorGuid: [0]=vendorGuid, [1]=repairSlot — packet-based
                        // Without: [0]=repairSlot — legacy MerchantFrame (FG only)
                        if (actionEntry.Item2.Count >= 2)
                        {
                            var repairItemVendorGuid = UnboxGuid(actionEntry.Item2[0]);
                            var repairSlot = (int)actionEntry.Item2[1];
                            builder.Do($"Repair slot {repairSlot} at vendor {repairItemVendorGuid:X}", time =>
                            {
                                // Use RepairAllItems as the packet path — individual slot repair
                                // is not supported by the packet API; repair-all is the server norm.
                                _objectManager.RepairAllItemsAsync(repairItemVendorGuid, CancellationToken.None)
                                    .GetAwaiter().GetResult();
                                return BehaviourTreeStatus.Success;
                            });
                        }
                        else
                        {
                            builder.Splice(_interactionSequences.BuildRepairItemSequence((int)actionEntry.Item2[0]));
                        }
                        break;
                    case CharacterAction.RepairAllItems:
                        // With vendorGuid: [0]=vendorGuid — packet-based
                        // Without: legacy MerchantFrame (FG only)
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
                            builder.Splice(_interactionSequences.RepairAllItemsSequence);
                        }
                        break;

                    case CharacterAction.VisitVendor:
                        builder.Do("Queue Vendor Visit Task", time =>
                        {
                            if (_botTasks.Count == 0 || _botTasks.Peek() is not Tasks.VendorVisitTask)
                                _botTasks.Push(new Tasks.VendorVisitTask(context));
                            return BehaviourTreeStatus.Success;
                        });
                        break;

                    case CharacterAction.DismissBuff:
                        builder.Splice(_interactionSequences.BuildDismissBuffSequence((string)actionEntry.Item2[0]));
                        break;

                    case CharacterAction.Resurrect:
                        builder.Splice(ResurrectSequence);
                        break;

                    case CharacterAction.Craft:
                        // With 2+ params: [0]=spellId, [1]=quantity — packet-based via CastSpell (BG compatible)
                        // With 1 param: legacy CraftFrame path (FG only) OR packet path if frame is null
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
                            builder.Splice(_interactionSequences.BuildCraftSequence((int)actionEntry.Item2[0]));
                        }
                        break;

                    case CharacterAction.Login:
                        builder.Splice(_interactionSequences.BuildLoginSequence((string)actionEntry.Item2[0], (string)actionEntry.Item2[1]));
                        break;
                    case CharacterAction.Logout:
                        builder.Splice(_interactionSequences.LogoutSequence);
                        break;
                    case CharacterAction.CreateCharacter:
                        builder.Splice(_interactionSequences.BuildCreateCharacterSequence(actionEntry.Item2));
                        break;
                    case CharacterAction.DeleteCharacter:
                        builder.Splice(_interactionSequences.BuildDeleteCharacterSequence((ulong)actionEntry.Item2[0]));
                        break;
                    case CharacterAction.EnterWorld:
                        builder.Splice(_interactionSequences.BuildEnterWorldSequence((ulong)actionEntry.Item2[0]));
                        break;

                    case CharacterAction.GatherNode:
                        var gatherGuid = UnboxGuid(actionEntry.Item2[0]);
                        int gatherSpellId = actionEntry.Item2.Count > 1 ? (int)actionEntry.Item2[1] : 0;
                        builder.Splice(BuildGatherNodeSequence(gatherGuid, gatherSpellId));
                        break;

                    case CharacterAction.SendChat:
                        var chatMsg = (string)actionEntry.Item2[0];

                        // Internal bot command: .targetself sets CMSG_SET_SELECTION to the
                        // player's own GUID without sending anything to server chat.
                        // This enables GM commands like .setskill that require a selected target.
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
                            var isDeadOrGhost = player != null && IsDeadOrGhostState(player);
                            DiagLog($"SENDCHAT-ACTION: chatMsg='{chatMsg}' dead={isDeadOrGhost} health={player?.Health ?? 0}");
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
                                    EnqueueDiagnosticMessage($"[SYSTEM] {response}");
                            }
                            DiagLog($"SENDCHAT-SENT: '{chatMsg}'");
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

                            // Only release when actually in corpse state. Releasing while already ghosted
                            // can re-trigger graveyard transitions and stall corpse-run movement.
                            if (IsGhostState(player))
                            {
                                Log.Information("[BOT RUNNER] Skipping ReleaseCorpse: player is already ghosted.");
                                return BehaviourTreeStatus.Success;
                            }

                            if (!IsCorpseState(player))
                            {
                                Log.Information("[BOT RUNNER] Skipping ReleaseCorpse: player is not in corpse state.");
                                return BehaviourTreeStatus.Success;
                            }

                            if (DateTime.UtcNow - _lastReleaseSpiritCommandUtc < ReleaseSpiritCommandCooldown)
                            {
                                Log.Debug("[BOT RUNNER] Skipping duplicate ReleaseCorpse command within cooldown window.");
                                return BehaviourTreeStatus.Success;
                            }

                            _lastReleaseSpiritCommandUtc = DateTime.UtcNow;
                            Log.Information("[BOT RUNNER] Releasing spirit (CMSG_REPOP_REQUEST)");
                            _objectManager.ReleaseSpirit();
                            return BehaviourTreeStatus.Success;
                        });
                        break;

                    case CharacterAction.RetrieveCorpse:
                        builder.Do("Retrieve Corpse", time =>
                        {
                            var player = _objectManager.Player;
                            DiagLog($"[RETRIEVE_DIAG] player={player != null} playerFlags=0x{(player != null ? (uint)player.PlayerFlags : 0u):X} hp={player?.Health ?? -1u}/{player?.MaxHealth ?? -1u}");
                            if (player != null)
                            {
                                var ghostResult = IsGhostState(player);
                                DiagLog($"[RETRIEVE_DIAG] IsGhostState={ghostResult} HasGhostFlag={HasGhostFlag(player)}");
                                if (ghostResult)
                                {
                                    var corpsePos = player.CorpsePosition;
                                    DiagLog($"[RETRIEVE_DIAG] corpsePos=({corpsePos?.X:F1},{corpsePos?.Y:F1},{corpsePos?.Z:F1})");
                                    if (IsZeroPosition(corpsePos) && _lastKnownAlivePosition != null)
                                    {
                                        corpsePos = new GameData.Core.Models.Position(
                                            _lastKnownAlivePosition.X,
                                            _lastKnownAlivePosition.Y,
                                            _lastKnownAlivePosition.Z);
                                        DiagLog($"[RETRIEVE_DIAG] using fallback corpsePos=({corpsePos.X:F1},{corpsePos.Y:F1},{corpsePos.Z:F1})");
                                    }

                                    if (corpsePos.X != 0 || corpsePos.Y != 0 || corpsePos.Z != 0)
                                    {
                                        if (_botTasks.Count == 0 || _botTasks.Peek() is not Tasks.RetrieveCorpseTask)
                                        {
                                            Log.Information("[BOT RUNNER] Queueing pathfinding corpse run to ({X:F0}, {Y:F0}, {Z:F0})",
                                                corpsePos.X, corpsePos.Y, corpsePos.Z);
                                            DiagLog($"[RETRIEVE_DIAG] PUSHING RetrieveCorpseTask corpse=({corpsePos.X:F1},{corpsePos.Y:F1},{corpsePos.Z:F1})");
                                            _botTasks.Push(new Tasks.RetrieveCorpseTask(context, corpsePos));
                                        }
                                        else
                                        {
                                            DiagLog("[RETRIEVE_DIAG] RetrieveCorpseTask already on stack");
                                        }
                                        return BehaviourTreeStatus.Success;
                                    }
                                    else
                                    {
                                        DiagLog("[RETRIEVE_DIAG] corpsePos is ZERO, skipping task push");
                                    }
                                }

                                var reclaimDelay = player.CorpseRecoveryDelaySeconds;
                                if (reclaimDelay > 0)
                                {
                                    Log.Information("[BOT RUNNER] Corpse reclaim cooldown active ({Seconds}s remaining); waiting.", reclaimDelay);
                                    DiagLog($"[RETRIEVE_DIAG] reclaimDelay={reclaimDelay}s — NOT pushing task");
                                    return BehaviourTreeStatus.Success;
                                }
                            }

                            Log.Information("[BOT RUNNER] Retrieving corpse (CMSG_RECLAIM_CORPSE direct)");
                            DiagLog("[RETRIEVE_DIAG] fallthrough to direct RetrieveCorpse()");
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
                        // Params: [0] = string playerName, [1] = int subGroup (0-7)
                        var rsgName = actionEntry.Item2.Count > 0 ? (string)actionEntry.Item2[0] : "";
                        var rsgGroup = actionEntry.Item2.Count > 1 ? (byte)(int)actionEntry.Item2[1] : (byte)0;
                        builder.Do($"Change Raid Subgroup: {rsgName} → group {rsgGroup}", time =>
                        {
                            _objectManager.ChangeRaidSubgroup(rsgName, rsgGroup);
                            return BehaviourTreeStatus.Success;
                        });
                        break;
                    }

                    case CharacterAction.StartDungeoneering:
                    {
                        // Params: [0] = isLeader (int, 1=leader 0=follower)
                        //         [1] = target map ID (int, e.g. 389 for RFC)
                        // Optional: [2..N] = float waypoints (x,y,z triples)
                        // If no waypoints provided, uses map-based defaults from DungeonWaypoints.
                        bool isLeader = actionEntry.Item2.Count > 0 && (int)actionEntry.Item2[0] == 1;
                        uint targetMapId = actionEntry.Item2.Count > 1 ? (uint)(int)actionEntry.Item2[1] : 0;
                        var waypointPositions = actionEntry.Item2.Count > 2
                            ? ParseGatheringRoutePositions(actionEntry.Item2.Skip(2))
                            : null;

                        builder.Do("Queue Dungeoneering Task", time =>
                        {
                            var existingTask = _botTasks.OfType<Tasks.Dungeoneering.DungeoneeringTask>().FirstOrDefault();
                            Log.Information("[BOT RUNNER] StartDungeoneering: isLeader={IsLeader}, existing={Existing}, existingIsLeader={ExLeader}",
                                isLeader, existingTask != null, existingTask?.IsLeader);
                            if (existingTask != null && existingTask.IsLeader == isLeader)
                            {
                                // Same role task already running — skip duplicate dispatch
                            }
                            else
                            {
                                // Use target map ID from coordinator (reliable), falling back to
                                // player's current MapId (may be stale during instance loading).
                                uint mapId = targetMapId != 0
                                    ? targetMapId
                                    : (_objectManager.Player?.MapId ?? 0);
                                var mapWaypoints = Tasks.Dungeoneering.DungeonWaypoints.GetWaypointsForMap(mapId);
                                IReadOnlyList<GameData.Core.Models.Position>? waypoints = waypointPositions?.Count > 0
                                    ? waypointPositions
                                    : mapWaypoints;

                                _botTasks.Push(new Tasks.Dungeoneering.DungeoneeringTask(context, isLeader, waypoints, mapId));
                            }
                            return BehaviourTreeStatus.Success;
                        });
                        break;
                    }

                    case CharacterAction.FollowTarget:
                    {
                        // Params: [0] = targetGuid (long)
                        // [1] = followDistance (float, optional, default 5.0)
                        var followGuid = UnboxGuid(actionEntry.Item2[0]);
                        var followDistance = actionEntry.Item2.Count > 1 ? (float)actionEntry.Item2[1] : 5.0f;
                        builder.Splice(BuildFollowTargetSequence(followGuid, followDistance));
                        break;
                    }

                    case CharacterAction.JoinBattleground:
                    {
                        // Params: [0] = bgTypeId (int), [1] = expectedMapId (int)
                        var bgTypeId = (int)actionEntry.Item2[0];
                        var expectedMapId = actionEntry.Item2.Count > 1 ? (uint)(int)actionEntry.Item2[1] : 0u;

                        builder.Do("Queue BG Join Task", time =>
                        {
                            // Get BG network client from agent factory
                            WoWSharpClient.Networking.ClientComponents.BattlegroundNetworkClientComponent? bgClient = null;
                            var factory = _agentFactoryAccessor?.Invoke();
                            if (factory != null)
                            {
                                bgClient = factory.BattlegroundAgent;
                            }

                            _botTasks.Push(new Tasks.Battlegrounds.BattlegroundQueueTask(
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
                        // Params: [0]=mapId, [1]=x (float), [2]=y (float), [3]=z (float)
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

                            // Compute path on first tick
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

                            // Use pathfinding waypoints if available, else direct line
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

        internal enum GoToTaskUpsertResult
        {
            Pushed,
            Retargeted,
            Duplicate
        }

        internal static GoToTaskUpsertResult UpsertGoToTask(
            Stack<Interfaces.IBotTask> botTasks,
            Interfaces.IBotContext botContext,
            float x,
            float y,
            float z,
            float tolerance)
        {
            var target = new Position(x, y, z);
            var normalizedTolerance = tolerance > 0f ? tolerance : 3f;
            var existingTask = botTasks.OfType<Tasks.GoToTask>().FirstOrDefault();
            if (existingTask != null)
            {
                if (existingTask.MatchesTarget(target, normalizedTolerance))
                    return GoToTaskUpsertResult.Duplicate;

                existingTask.Retarget(target, normalizedTolerance);
                return GoToTaskUpsertResult.Retargeted;
            }

            botTasks.Push(new Tasks.GoToTask(botContext, target.X, target.Y, target.Z, normalizedTolerance));
            return GoToTaskUpsertResult.Pushed;
        }

        private static List<uint> ParseGatheringEntries(string csv)
            => csv.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(token => uint.TryParse(token, out var entry) ? entry : 0u)
                .Where(entry => entry != 0)
                .Distinct()
                .ToList();

        private static List<Position> ParseGatheringRoutePositions(IEnumerable<object> rawParameters)
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
    }
}
