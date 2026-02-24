using Communication;
using GameData.Core.Enums;
using Serilog;
using System;
using System.Collections.Generic;
using System.Threading;
using Xas.FluentBehaviourTree;

namespace BotRunner
{
    public partial class BotRunnerService
    {
        private IBehaviourTreeNode BuildBehaviorTreeFromActions(List<(CharacterAction, List<object>)> actionMap)
        {
            var context = new BotRunnerContext(_objectManager, _botTasks, _container, _behaviorConfig);
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
                        builder.Splice(BuildGoToSequence((float)actionEntry.Item2[0], (float)actionEntry.Item2[1], (float)actionEntry.Item2[2], (float)actionEntry.Item2[3]));
                        break;
                    case CharacterAction.InteractWith:
                        builder.Splice(BuildInteractWithSequence(UnboxGuid(actionEntry.Item2[0])));
                        break;

                    case CharacterAction.SelectGossip:
                        builder.Splice(BuildSelectGossipSequence((int)actionEntry.Item2[0]));
                        break;

                    case CharacterAction.SelectTaxiNode:
                        builder.Splice(BuildSelectTaxiNodeSequence((int)actionEntry.Item2[0]));
                        break;

                    case CharacterAction.AcceptQuest:
                        builder.Splice(AcceptQuestSequence);
                        break;
                    case CharacterAction.DeclineQuest:
                        builder.Splice(DeclineQuestSequence);
                        break;
                    case CharacterAction.SelectReward:
                        builder.Splice(BuildSelectRewardSequence((int)actionEntry.Item2[0]));
                        break;
                    case CharacterAction.CompleteQuest:
                        builder.Splice(CompleteQuestSequence);
                        break;

                    case CharacterAction.TrainSkill:
                        builder.Splice(BuildTrainSkillSequence((int)actionEntry.Item2[0]));
                        break;
                    case CharacterAction.TrainTalent:
                        builder.Splice(BuildLearnTalentSequence((int)actionEntry.Item2[0]));
                        break;

                    case CharacterAction.OfferTrade:
                        builder.Splice(BuildOfferTradeSequence(UnboxGuid(actionEntry.Item2[0])));
                        break;
                    case CharacterAction.OfferGold:
                        builder.Splice(BuildOfferMoneySequence((int)actionEntry.Item2[0]));
                        break;
                    case CharacterAction.OfferItem:
                        builder.Splice(BuildOfferItemSequence((int)actionEntry.Item2[0], (int)actionEntry.Item2[1], (int)actionEntry.Item2[2], (int)actionEntry.Item2[3]));
                        break;
                    case CharacterAction.AcceptTrade:
                        builder.Splice(AcceptTradeSequence);
                        break;
                    case CharacterAction.DeclineTrade:
                        builder.Splice(DeclineTradeSequence);
                        break;
                    case CharacterAction.EnchantTrade:
                        builder.Splice(BuildOfferEnchantSequence((int)actionEntry.Item2[0]));
                        break;
                    case CharacterAction.LockpickTrade:
                        builder.Splice(OfferLockpickSequence);
                        break;

                    case CharacterAction.PromoteLeader:
                        builder.Splice(BuildPromoteLeaderSequence(UnboxGuid(actionEntry.Item2[0])));
                        break;
                    case CharacterAction.PromoteAssistant:
                        builder.Splice(BuildPromoteAssistantSequence(UnboxGuid(actionEntry.Item2[0])));
                        break;
                    case CharacterAction.PromoteLootManager:
                        builder.Splice(BuildPromoteLootManagerSequence(UnboxGuid(actionEntry.Item2[0])));
                        break;
                    case CharacterAction.SetGroupLoot:
                        builder.Splice(BuildSetGroupLootSequence((GroupLootSetting)actionEntry.Item2[0]));
                        break;
                    case CharacterAction.AssignLoot:
                        builder.Splice(BuildAssignLootSequence((int)actionEntry.Item2[0], UnboxGuid(actionEntry.Item2[1])));
                        break;

                    case CharacterAction.LootRollNeed:
                        builder.Splice(BuildLootRollNeedSequence((int)actionEntry.Item2[0]));
                        break;
                    case CharacterAction.LootRollGreed:
                        builder.Splice(BuildLootRollGreedSequence((int)actionEntry.Item2[0]));
                        break;
                    case CharacterAction.LootPass:
                        builder.Splice(BuildLootPassSequence((int)actionEntry.Item2[0]));
                        break;

                    case CharacterAction.SendGroupInvite:
                        if (actionEntry.Item2[0] is string playerName)
                            builder.Splice(BuildSendGroupInviteByNameSequence(playerName));
                        else
                            builder.Splice(BuildSendGroupInviteSequence(UnboxGuid(actionEntry.Item2[0])));
                        break;
                    case CharacterAction.AcceptGroupInvite:
                        builder.Splice(AcceptGroupInviteSequence);
                        break;
                    case CharacterAction.DeclineGroupInvite:
                        builder.Splice(DeclineGroupInviteSequence);
                        break;
                    case CharacterAction.KickPlayer:
                        builder.Splice(BuildKickPlayerSequence(UnboxGuid(actionEntry.Item2[0])));
                        break;
                    case CharacterAction.LeaveGroup:
                        builder.Splice(LeaveGroupSequence);
                        break;
                    case CharacterAction.DisbandGroup:
                        builder.Splice(DisbandGroupSequence);
                        break;
                    case CharacterAction.StartMeleeAttack:
                        builder.Splice(BuildStartMeleeAttackSequence(UnboxGuid(actionEntry.Item2[0])));
                        break;
                    case CharacterAction.StopAttack:
                        builder.Splice(StopAttackSequence);
                        break;
                    case CharacterAction.CastSpell:
                        var castTargetGuid = actionEntry.Item2.Count > 1 ? UnboxGuid(actionEntry.Item2[1]) : 0UL;
                        builder.Splice(BuildCastSpellSequence((int)actionEntry.Item2[0], castTargetGuid));
                        break;
                    case CharacterAction.StopCast:
                        builder.Splice(StopCastSequence);
                        break;

                    case CharacterAction.UseItem:
                        if (actionEntry.Item2.Count >= 3)
                            builder.Splice(BuildUseItemSequence((int)actionEntry.Item2[0], (int)actionEntry.Item2[1], (ulong)actionEntry.Item2[2]));
                        else
                            builder.Splice(BuildUseItemByIdSequence((int)actionEntry.Item2[0]));
                        break;
                    case CharacterAction.EquipItem:
                        if (actionEntry.Item2.Count >= 3)
                            builder.Splice(BuildEquipItemSequence((int)actionEntry.Item2[0], (int)actionEntry.Item2[1], (EquipSlot)actionEntry.Item2[2]));
                        else if (actionEntry.Item2.Count >= 2)
                            builder.Splice(BuildEquipItemSequence((int)actionEntry.Item2[0], (int)actionEntry.Item2[1]));
                        else
                            builder.Splice(BuildEquipItemByIdSequence((int)actionEntry.Item2[0]));
                        break;
                    case CharacterAction.UnequipItem:
                        builder.Splice(BuildUnequipItemSequence((EquipSlot)actionEntry.Item2[0]));
                        break;
                    case CharacterAction.DestroyItem:
                        builder.Splice(BuildDestroyItemSequence((int)actionEntry.Item2[0], (int)actionEntry.Item2[1], (int)actionEntry.Item2[2]));
                        break;
                    case CharacterAction.MoveItem:
                        builder.Splice(BuildMoveItemSequence((int)actionEntry.Item2[0], (int)actionEntry.Item2[1], (int)actionEntry.Item2[2], (int)actionEntry.Item2[3], (int)actionEntry.Item2[4]));
                        break;
                    case CharacterAction.SplitStack:
                        builder.Splice(BuildSplitStackSequence((int)actionEntry.Item2[0],
                            (int)actionEntry.Item2[1],
                            (int)actionEntry.Item2[2],
                            (int)actionEntry.Item2[3],
                            (int)actionEntry.Item2[4]));
                        break;

                    case CharacterAction.BuyItem:
                        builder.Splice(BuildBuyItemSequence((int)actionEntry.Item2[0], (int)actionEntry.Item2[1]));
                        break;
                    case CharacterAction.BuybackItem:
                        builder.Splice(BuildBuybackItemSequence((int)actionEntry.Item2[0], (int)actionEntry.Item2[1]));
                        break;
                    case CharacterAction.SellItem:
                        builder.Splice(BuildSellItemSequence((int)actionEntry.Item2[0], (int)actionEntry.Item2[1], (int)actionEntry.Item2[2]));
                        break;
                    case CharacterAction.RepairItem:
                        builder.Splice(BuildRepairItemSequence((int)actionEntry.Item2[0]));
                        break;
                    case CharacterAction.RepairAllItems:
                        builder.Splice(RepairAllItemsSequence);
                        break;

                    case CharacterAction.DismissBuff:
                        builder.Splice(BuildDismissBuffSequence((string)actionEntry.Item2[0]));
                        break;

                    case CharacterAction.Resurrect:
                        builder.Splice(ResurrectSequence);
                        break;

                    case CharacterAction.Craft:
                        builder.Splice(BuildCraftSequence((int)actionEntry.Item2[0]));
                        break;

                    case CharacterAction.Login:
                        builder.Splice(BuildLoginSequence((string)actionEntry.Item2[0], (string)actionEntry.Item2[1]));
                        break;
                    case CharacterAction.Logout:
                        builder.Splice(LogoutSequence);
                        break;
                    case CharacterAction.CreateCharacter:
                        builder.Splice(BuildCreateCharacterSequence(actionEntry.Item2));
                        break;
                    case CharacterAction.DeleteCharacter:
                        builder.Splice(BuildDeleteCharacterSequence((ulong)actionEntry.Item2[0]));
                        break;
                    case CharacterAction.EnterWorld:
                        builder.Splice(BuildEnterWorldSequence((ulong)actionEntry.Item2[0]));
                        break;

                    case CharacterAction.GatherNode:
                        var gatherGuid = UnboxGuid(actionEntry.Item2[0]);
                        int gatherSpellId = actionEntry.Item2.Count > 1 ? (int)actionEntry.Item2[1] : 0;
                        builder.Splice(BuildGatherNodeSequence(gatherGuid, gatherSpellId));
                        break;

                    case CharacterAction.SendChat:
                        var chatMsg = (string)actionEntry.Item2[0];
                        builder.Do($"Send Chat: {chatMsg}", time =>
                        {
                            var player = _objectManager.Player;
                            var isDeadOrGhost = player != null && IsDeadOrGhostState(player);
                            if (isDeadOrGhost)
                            {
                                Log.Information("[BOT RUNNER] Skipping chat while dead/ghost: {ChatMessage}", chatMsg);
                                return BehaviourTreeStatus.Success;
                            }

                            Log.Information($"[BOT RUNNER] Sending chat message: {chatMsg}");
                            _objectManager.SendChatMessage(chatMsg);
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
                            if (player != null)
                            {
                                if (IsGhostState(player))
                                {
                                    var corpsePos = player.CorpsePosition;
                                    if (IsZeroPosition(corpsePos) && _lastKnownAlivePosition != null)
                                    {
                                        corpsePos = new GameData.Core.Models.Position(
                                            _lastKnownAlivePosition.X,
                                            _lastKnownAlivePosition.Y,
                                            _lastKnownAlivePosition.Z);
                                    }

                                    if (corpsePos.X != 0 || corpsePos.Y != 0 || corpsePos.Z != 0)
                                    {
                                        if (_botTasks.Count == 0 || _botTasks.Peek() is not Tasks.RetrieveCorpseTask)
                                        {
                                            Log.Information("[BOT RUNNER] Queueing pathfinding corpse run to ({X:F0}, {Y:F0}, {Z:F0})",
                                                corpsePos.X, corpsePos.Y, corpsePos.Z);
                                            _botTasks.Push(new Tasks.RetrieveCorpseTask(context, corpsePos));
                                        }
                                        return BehaviourTreeStatus.Success;
                                    }
                                }

                                var reclaimDelay = player.CorpseRecoveryDelaySeconds;
                                if (reclaimDelay > 0)
                                {
                                    Log.Information("[BOT RUNNER] Corpse reclaim cooldown active ({Seconds}s remaining); waiting.", reclaimDelay);
                                    return BehaviourTreeStatus.Success;
                                }
                            }

                            Log.Information("[BOT RUNNER] Retrieving corpse (CMSG_RECLAIM_CORPSE direct)");
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

                    default:
                        break;
                }
            }

            return builder.End().Build();
        }
    }
}
