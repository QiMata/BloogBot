using BotRunner.Clients;
using BotRunner.Combat;
using BotRunner.Interfaces;
using BotRunner.Tasks;
using Communication;
using GameData.Core.Enums;
using GameData.Core.Interfaces;
using GameData.Core.Models;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using WoWSharpClient.Networking.ClientComponents.I;
using Xas.FluentBehaviourTree;

namespace BotRunner
{
    public class BotRunnerService
    {
        private readonly IObjectManager _objectManager;

        private readonly CharacterStateUpdateClient _characterStateUpdateClient;
        private readonly ILootingService _lootingService;
        private readonly IVendorService? _vendorService;
        private readonly ITrainerService? _trainerService;
        private readonly ITalentService? _talentService;
        private readonly IEquipmentService? _equipmentService;
        private readonly IFlightMasterService? _flightMasterService;
        private readonly IMailCollectionService? _mailCollectionService;
        private readonly IBankingService? _bankingService;
        private readonly IAuctionHouseService? _auctionHouseService;
        private readonly ICraftingService? _craftingService;
        private readonly IDependencyContainer _container;
        private readonly Func<IAgentFactory?>? _agentFactoryAccessor;
        private readonly Constants.BotBehaviorConfig _behaviorConfig;

        private WoWActivitySnapshot _activitySnapshot;
        private int _lastLoggedContainedItems = -1;
        private int _lastLoggedItemObjects = -1;

        private Task? _asyncBotTaskRunnerTask;
        private CancellationTokenSource? _cts;

        private IBehaviourTreeNode? _behaviorTree;
        private BehaviourTreeStatus _behaviorTreeStatus = BehaviourTreeStatus.Success;

        // Spell-cast lockout: prevents movement actions from interrupting active spell casts.
        // Channeled spells like fishing need time to complete without being overridden by GoTo.
        private DateTime _spellCastLockoutUntil = DateTime.MinValue;
        private const double SpellCastLockoutSeconds = 20.0;

        private readonly Stack<IBotTask> _botTasks = new();
        private bool _tasksInitialized;

        public BotRunnerService(IObjectManager objectManager,
                                 CharacterStateUpdateClient characterStateUpdateClient,
                                 ILootingService lootingService,
                                 IDependencyContainer container,
                                 Func<IAgentFactory?>? agentFactoryAccessor = null,
                                 string? accountName = null,
                                 IVendorService? vendorService = null,
                                 ITrainerService? trainerService = null,
                                 ITalentService? talentService = null,
                                 IEquipmentService? equipmentService = null,
                                 IFlightMasterService? flightMasterService = null,
                                 IMailCollectionService? mailCollectionService = null,
                                 IBankingService? bankingService = null,
                                 IAuctionHouseService? auctionHouseService = null,
                                 ICraftingService? craftingService = null,
                                 Constants.BotBehaviorConfig? behaviorConfig = null)
        {
            _objectManager = objectManager ?? throw new ArgumentNullException(nameof(objectManager));
            _activitySnapshot = new() { AccountName = accountName ?? "?" };
            _agentFactoryAccessor = agentFactoryAccessor;

            _characterStateUpdateClient = characterStateUpdateClient ?? throw new ArgumentNullException(nameof(characterStateUpdateClient));
            _lootingService = lootingService ?? throw new ArgumentNullException(nameof(lootingService));
            _vendorService = vendorService;
            _trainerService = trainerService;
            _talentService = talentService;
            _equipmentService = equipmentService;
            _flightMasterService = flightMasterService;
            _mailCollectionService = mailCollectionService;
            _bankingService = bankingService;
            _auctionHouseService = auctionHouseService;
            _craftingService = craftingService;
            _container = container ?? throw new ArgumentNullException(nameof(container));
            _behaviorConfig = behaviorConfig ?? new Constants.BotBehaviorConfig();
        }

        public void Start()
        {
            if (_asyncBotTaskRunnerTask == null || _asyncBotTaskRunnerTask.IsCompleted)
            {
                _cts = new CancellationTokenSource();
                _asyncBotTaskRunnerTask = StartBotTaskRunnerAsync(_cts.Token);
            }
        }

        public void Stop()
        {
            try
            {
                _cts?.Cancel();
                _asyncBotTaskRunnerTask?.Wait(1000);
            }
            catch { }
            finally
            {
                _asyncBotTaskRunnerTask?.Dispose();
                _asyncBotTaskRunnerTask = null;
                _cts?.Dispose();
                _cts = null;
            }
        }

        private async Task StartBotTaskRunnerAsync(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    PopulateSnapshotFromObjectManager();

                    var incomingActivityMemberState = _characterStateUpdateClient.SendMemberStateUpdate(_activitySnapshot);

                    UpdateBehaviorTree(incomingActivityMemberState);

                    if (_behaviorTree != null)
                    {
                        _behaviorTreeStatus = _behaviorTree.Tick(new TimeData(0.1f));
                    }

                    // Process BotTask stack when in-world and behavior tree isn't running
                    if (_objectManager.HasEnteredWorld && _botTasks.Count > 0
                        && (_behaviorTree == null || _behaviorTreeStatus != BehaviourTreeStatus.Running))
                    {
                        _botTasks.Peek().Update();
                    }

                    _activitySnapshot = incomingActivityMemberState;
                }
                catch (Exception ex)
                {
                    Log.Error($"[BOT RUNNER] {ex}");
                }

                await Task.Delay(100, cancellationToken);
            }
        }

        private void UpdateBehaviorTree(WoWActivitySnapshot incomingActivityMemberState)
        {
            // Check for new incoming actions FIRST — they can interrupt a running tree
            if (_objectManager.HasEnteredWorld
                && incomingActivityMemberState.CurrentAction != null
                && incomingActivityMemberState.CurrentAction.ActionType != Communication.ActionType.Wait)
            {
                var action = incomingActivityMemberState.CurrentAction;

                // Spell-cast lockout: don't let movement actions interrupt active spell casts.
                // Channeled spells (fishing, etc.) need time to complete.
                if (action.ActionType == Communication.ActionType.Goto
                    && DateTime.UtcNow < _spellCastLockoutUntil)
                {
                    return;
                }

                Log.Information($"[BOT RUNNER] Received action from StateManager: {action.ActionType} ({(int)action.ActionType})");
                var actionList = ConvertActionMessageToCharacterActions(action);
                if (actionList.Count > 0)
                {
                    // Set lockout when casting a spell
                    if (action.ActionType == Communication.ActionType.CastSpell)
                    {
                        _spellCastLockoutUntil = DateTime.UtcNow.AddSeconds(SpellCastLockoutSeconds);
                    }

                    Log.Information($"[BOT RUNNER] Building behavior tree for: {actionList[0].Item1}");
                    _behaviorTree = BuildBehaviorTreeFromActions(actionList);
                    _behaviorTreeStatus = BehaviourTreeStatus.Running;
                    _activitySnapshot.PreviousAction = action;
                    return;
                }
            }

            if (_behaviorTree != null && _behaviorTreeStatus == BehaviourTreeStatus.Running)
            {
                return;
            }

            if (!_objectManager.LoginScreen.IsLoggedIn)
            {
                _behaviorTree = BuildLoginSequence(incomingActivityMemberState.AccountName, "PASSWORD");
                return;
            }

            if (_objectManager.RealmSelectScreen.CurrentRealm == null)
            {
                _behaviorTree = BuildRealmSelectionSequence();
                return;
            }

            if (!_objectManager.CharacterSelectScreen.HasReceivedCharacterList)
            {
                if (!_objectManager.CharacterSelectScreen.HasRequestedCharacterList)
                {
                    _behaviorTree = BuildRequestCharacterSequence();
                }

                return;
            }

            if (_objectManager.CharacterSelectScreen.CharacterSelects.Count == 0)
            {
                Class @class = WoWNameGenerator.ParseClassCode(_activitySnapshot.AccountName.Substring(2, 2));
                Race race = WoWNameGenerator.ParseRaceCode(_activitySnapshot.AccountName[..2]);
                Gender gender = WoWNameGenerator.DetermineGender(@class);

                _behaviorTree = BuildCreateCharacterSequence(
                    [
                        WoWNameGenerator.GenerateName(race, gender),
                        race,
                        gender,
                        @class,
                        0,
                        0,
                        0,
                        0,
                        0
                    ]
                );

                return;
            }

            if (!_objectManager.HasEnteredWorld)
            {
                _behaviorTree = BuildEnterWorldSequence(_objectManager.CharacterSelectScreen.CharacterSelects[0].Guid);
                return;
            }

            // Initialize BotTask stack once after entering world
            if (!_tasksInitialized)
            {
                _tasksInitialized = true;
                InitializeTaskSequence();
            }

            // No active behavior tree needed — clear any completed tree so it isn't re-ticked.
            // Task stack (line 104) runs when _behaviorTree is null or status != Running.
            _behaviorTree = null;
        }

        internal static Position? ResolveNextWaypoint(Position[]? positions, Action<string>? logAction = null)
        {
            if (positions == null || positions.Length == 0)
            {
                logAction?.Invoke("Path contained no waypoints. Skipping movement.");
                return null;
            }

            if (positions.Length == 1)
            {
                logAction?.Invoke("Path contained a single waypoint. Using waypoint[0].");
                return positions[0];
            }

            return positions[1];
        }

        private void InitializeTaskSequence()
        {
            var accountName = _activitySnapshot.AccountName;
            if (string.IsNullOrEmpty(accountName) || accountName == "?" || accountName.Length < 4)
            {
                Log.Information("[BOT RUNNER] No valid account name for task initialization, using wait.");
                return;
            }

            var context = new BotRunnerContext(_objectManager, _botTasks, _container, _behaviorConfig);

            try
            {
                var classCode = accountName.Substring(2, 2);
                var @class = WoWNameGenerator.ParseClassCode(classCode);

                // IdleTask sits at the bottom of the stack — all behavior is
                // directed by StateManager via ActionMessage IPC.
                // Push tasks in reverse order (stack is LIFO)
                _botTasks.Push(new Tasks.IdleTask(context));
                _botTasks.Push(new Tasks.WaitTask(context, 3000));
                _botTasks.Push(new Tasks.TeleportTask(context, "valleyoftrials"));
                Log.Information("[BOT RUNNER] Initialized {Class} task sequence for {Account} using {Profile}",
                    @class, accountName, _container.ClassContainer.Name);
            }
            catch (Exception ex)
            {
                Log.Error($"[BOT RUNNER] Failed to initialize task sequence: {ex.Message}");
            }
        }

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

        private IBehaviourTreeNode BuildBehaviorTreeFromActions(List<(CharacterAction, List<object>)> actionMap)
        {
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
                        builder.Splice(BuildUseItemSequence((int)actionEntry.Item2[0], (int)actionEntry.Item2[1], (ulong)actionEntry.Item2[2]));
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

                    case CharacterAction.SendChat:
                        var chatMsg = (string)actionEntry.Item2[0];
                        builder.Do($"Send Chat: {chatMsg}", time =>
                        {
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

                    default:
                        break;
                }
            }

            return builder.End().Build();
        }
        private static IBehaviourTreeNode BuildWaitSequence(float duration) => new BehaviourTreeBuilder()
                .Sequence("Wait Sequence")
                    .Do("Wait", time => BehaviourTreeStatus.Success)
                .End()
                .Build();
        /// <summary>
        /// Sequence to move the bot to a specific location using given coordinates (x, y, z) and a range (f).
        /// </summary>
        /// <param name="x">The x-coordinate of the destination.</param>
        /// <param name="y">The y-coordinate of the destination.</param>
        /// <param name="z">The z-coordinate of the destination.</param>
        /// <param name="tolerance">How close to get before stopping (0 = default 3 yards).</param>
        /// <returns>IBehaviourTreeNode that manages moving the bot to the specified location.</returns>
        private IBehaviourTreeNode BuildGoToSequence(float x, float y, float z, float tolerance) => new BehaviourTreeBuilder()
            .Sequence("GoTo Sequence")
                .Do("Move to Location", time =>
                {
                    var target = new Position(x, y, z);
                    var dist = _objectManager.Player.Position.DistanceTo(target);
                    var arrivalDist = tolerance > 0 ? tolerance : 3f;

                    if (dist < arrivalDist)
                    {
                        _objectManager.StopAllMovement();
                        return BehaviourTreeStatus.Success;
                    }

                    // Calculate facing toward target
                    var dx = x - _objectManager.Player.Position.X;
                    var dy = y - _objectManager.Player.Position.Y;
                    var facing = MathF.Atan2(dy, dx);

                    _objectManager.MoveToward(target, facing);
                    return BehaviourTreeStatus.Running;
                })
            .End()
            .Build();
        /// <summary>
        /// Sequence to interact with a specific target based on its GUID.
        /// </summary>
        /// <param name="guid">The GUID of the target to interact with.</param>
        /// <returns>IBehaviourTreeNode that manages interacting with the specified target.</returns>
        private IBehaviourTreeNode BuildInteractWithSequence(ulong guid) => new BehaviourTreeBuilder()
            .Sequence("Interact With Sequence")
                .Splice(CheckForTarget(guid))
                // Ensure the target is valid for interaction
                .Condition("Has Valid Target", time => _objectManager.Player.TargetGuid == guid)

                // Perform the interaction
                .Do("Interact with Target", time =>
                {
                    _objectManager.GameObjects.First(x => x.Guid == guid).Interact();
                    return BehaviourTreeStatus.Success;
                })
            .End()
            .Build();
        /// <summary>
        /// Property to check if the player has a target, and if not, sets the target to the specified GUID.
        /// </summary>
        /// <param name="guid">The GUID of the target to set.</param>
        /// <returns>IBehaviourTreeNode that checks for and sets a target if needed.</returns>
        private IBehaviourTreeNode CheckForTarget(ulong guid) => new BehaviourTreeBuilder()
            .Sequence("Check for Target")
                .Condition("Player Exists", time => _objectManager.Player != null)
                .Do("Set Target", time =>
                {
                    if (_objectManager.Player.TargetGuid != guid)
                    {
                        _objectManager.SetTarget(guid);
                    }
                    return BehaviourTreeStatus.Success;
                })
            .End()
            .Build();
        /// <summary>
        /// Sequence to select a gossip option from an NPC's menu.
        /// </summary>
        /// <param name="selection">The index of the gossip option to select.</param>
        /// <returns>IBehaviourTreeNode that manages selecting a gossip option.</returns>
        private IBehaviourTreeNode BuildSelectGossipSequence(int selection) => new BehaviourTreeBuilder()
            .Sequence("Select Gossip Sequence")
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
                    .Do("Buy Item", time =>
                    {
                        _objectManager.MerchantFrame.BuyItem(slotId, quantity);
                        return BehaviourTreeStatus.Success;
                    })
                .End()
                .Build();
        private IBehaviourTreeNode BuildBuybackItemSequence(int slotId, int quantity) => new BehaviourTreeBuilder()
                .Sequence("BuybackItem Sequence")
                    .Do("Buy Item", time =>
                    {
                        _objectManager.MerchantFrame.BuybackItem(slotId, quantity);
                        return BehaviourTreeStatus.Success;
                    })
                .End()
                .Build();
        private IBehaviourTreeNode BuildSellItemSequence(int bagId, int slotId, int quantity) => new BehaviourTreeBuilder()
                .Sequence("SellItem Sequence")
                    .Do("Sell Item", time =>
                    {
                        _objectManager.MerchantFrame.SellItem(bagId, slotId, quantity);
                        return BehaviourTreeStatus.Success;
                    })
                .End()
                .Build();
        /// <summary>
        /// Sequence to stop any active auto-attacks, including melee, ranged, and wand.
        /// </summary>
        /// <returns>IBehaviourTreeNode that manages stopping auto-attacks.</returns>
        private IBehaviourTreeNode BuildStartMeleeAttackSequence(ulong targetGuid) => new BehaviourTreeBuilder()
            .Sequence("Start Melee Attack Sequence")
                .Splice(CheckForTarget(targetGuid))
                .Do("Start Melee Attack", time =>
                {
                    _objectManager.SetTarget(targetGuid);
                    _objectManager.StartMeleeAttack();
                    Log.Information($"[BOT RUNNER] Started melee attack on target {targetGuid:X}");
                    return BehaviourTreeStatus.Success;
                })
            .End()
            .Build();

        private IBehaviourTreeNode StopAttackSequence => new BehaviourTreeBuilder()
            .Sequence("Stop Attack Sequence")
                // Check if any auto-attack (melee, ranged, or wand) is active
                .Condition("Is Any Auto-Attack Active", time => _objectManager.Player.IsAutoAttacking)

                // Disable all auto-attacks
                .Do("Stop All Auto-Attacks", time =>
                {
                    _objectManager.StopAttack();
                    return BehaviourTreeStatus.Success;
                })
            .End()
            .Build();
        /// <summary>
        /// Sequence to cast a specific spell. This checks if the bot has sufficient resources,
        /// if the spell is off cooldown, and if the target is in range before casting the spell.
        /// </summary>
        /// <param name="spellId">The ID of the spell to cast.</param>
        /// <returns>IBehaviourTreeNode that manages casting a spell.</returns>
        private IBehaviourTreeNode BuildCastSpellSequence(int spellId, ulong targetGuid)
        {
            Log.Information($"[BOT RUNNER] BuildCastSpellSequence: spell={spellId}, target=0x{targetGuid:X}");
            return new BehaviourTreeBuilder()
                .Sequence("Cast Spell Sequence")
                    .Splice(CheckForTarget(targetGuid))

                    .Condition("Can Cast Spell", time =>
                    {
                        var canCast = _objectManager.CanCastSpell(spellId, targetGuid);
                        if (!canCast)
                            Log.Debug($"[BOT RUNNER] CanCastSpell({spellId}, 0x{targetGuid:X}) = false");
                        return canCast;
                    })

                    .Do("Stop and Face Target", time =>
                    {
                        // Stop movement before casting to prevent INTERRUPTED failures
                        _objectManager.StopAllMovement();

                        // Face the target to prevent UNIT_NOT_INFRONT failures
                        var target = _objectManager.Units.FirstOrDefault(u => u.Guid == targetGuid);
                        if (target?.Position != null)
                        {
                            _objectManager.Face(target.Position);
                        }
                        return BehaviourTreeStatus.Success;
                    })

                    .Do("Cast Spell", time =>
                    {
                        Log.Information($"[BOT RUNNER] Casting spell {spellId} on target 0x{targetGuid:X}");
                        _objectManager.CastSpell(spellId);
                        return BehaviourTreeStatus.Success;
                    })
                .End()
                .Build();
        }
        /// <summary>
        /// Sequence to stop the current spell cast. This will stop any spell the bot is currently casting.
        /// </summary>
        /// <returns>IBehaviourTreeNode that manages stopping a spell cast.</returns>
        private IBehaviourTreeNode StopCastSequence => new BehaviourTreeBuilder()
            .Sequence("Stop Cast Sequence")
                // Ensure the bot is currently casting a spell
                .Condition("Is Casting", time => _objectManager.Player.IsCasting || _objectManager.Player.IsChanneling)

                // Stop the current spell cast
                .Do("Stop Spell Cast", time =>
                {
                    _objectManager.StopCasting();
                    return BehaviourTreeStatus.Success;
                })
            .End()
            .Build();
        /// <summary>
        /// Sequence to resurrect the bot or another target.
        /// </summary>
        /// <returns>IBehaviourTreeNode that manages the resurrection process.</returns>
        private IBehaviourTreeNode ResurrectSequence => new BehaviourTreeBuilder()
            .Sequence("Resurrect Sequence")
                // Ensure the bot or target can be resurrected
                .Condition("Can Resurrect", time => _objectManager.Player.InGhostForm && _objectManager.Player.CanResurrect)

                // Perform the resurrection action
                .Do("Resurrect", time =>
                {
                    _objectManager.AcceptResurrect();
                    return BehaviourTreeStatus.Success;
                })
            .End()
            .Build();
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
        /// <summary>
        /// Sequence to promote another player to group leader.
        /// </summary>
        /// <param name="playerGuid">The GUID of the player to promote to leader.</param>
        /// <returns>IBehaviourTreeNode that manages promoting the player to group leader.</returns>
        private IBehaviourTreeNode BuildPromoteLeaderSequence(ulong playerGuid) => new BehaviourTreeBuilder()
            .Sequence("Promote Leader Sequence")
                // Ensure the bot is in a group with the specified player
                .Condition("Is In Group with Player", time => _objectManager.PartyMembers.Any(x => x.Guid == playerGuid))

                // Promote the player to group leader
                .Do("Promote Leader", time =>
                {
                    _objectManager.PromoteLeader(playerGuid);
                    return BehaviourTreeStatus.Success;
                })
            .End()
            .Build();
        /// <summary>
        /// Sequence to promote another player to group assistant.
        /// </summary>
        /// <param name="playerGuid">The GUID of the player to promote to assistant.</param>
        /// <returns>IBehaviourTreeNode that manages promoting the player to group assistant.</returns>
        private IBehaviourTreeNode BuildPromoteAssistantSequence(ulong playerGuid) => new BehaviourTreeBuilder()
            .Sequence("Promote Assistant Sequence")
                // Ensure the bot is in a group with the specified player
                .Condition("Is In Group with Player", time => _objectManager.PartyMembers.Any(x => x.Guid == playerGuid))

                // Promote the player to group assistant
                .Do("Promote Assistant", time =>
                {
                    _objectManager.PromoteAssistant(playerGuid);
                    return BehaviourTreeStatus.Success;
                })
            .End()
            .Build();
        /// <summary>
        /// Sequence to promote another player to loot manager in the group.
        /// </summary>
        /// <param name="playerGuid">The GUID of the player to promote to loot manager.</param>
        /// <returns>IBehaviourTreeNode that manages promoting the player to loot manager.</returns>
        private IBehaviourTreeNode BuildPromoteLootManagerSequence(ulong playerGuid) => new BehaviourTreeBuilder()
            .Sequence("Promote Loot Manager Sequence")
                // Ensure the bot is in a group with the specified player
                .Condition("Is In Group with Player", time => _objectManager.PartyMembers.Any(x => x.Guid == playerGuid))

                // Promote the player to loot manager
                .Do("Promote Loot Manager", time =>
                {
                    _objectManager.PromoteLootManager(playerGuid);
                    return BehaviourTreeStatus.Success;
                })
            .End()
            .Build();
        /// <summary>
        /// Sequence to set group loot rules for distributing loot in a group.
        /// </summary>
        /// <param name="setting">The group loot setting to apply (e.g., free-for-all, round-robin).</param>
        /// <returns>IBehaviourTreeNode that manages setting the group loot rules.</returns>
        private IBehaviourTreeNode BuildSetGroupLootSequence(GroupLootSetting setting) => new BehaviourTreeBuilder()
            .Sequence("Set Group Loot Sequence")
                // Ensure the bot is in a group and has permission to change loot rules
                .Condition("Can Set Loot Rules", time => _objectManager.PartyLeaderGuid == _objectManager.Player.Guid)

                // Set the group loot rule
                .Do("Set Group Loot", time =>
                {
                    _objectManager.SetGroupLoot(setting);
                    return BehaviourTreeStatus.Success;
                })
            .End()
            .Build();
        /// <summary>
        /// Sequence to assign specific loot to a player in the group.
        /// </summary>
        /// <param name="itemId">The ID of the loot item to assign.</param>
        /// <param name="playerGuid">The GUID of the player to assign the loot to.</param>
        /// <returns>IBehaviourTreeNode that manages assigning the loot.</returns>
        private IBehaviourTreeNode BuildAssignLootSequence(int itemId, ulong playerGuid) => new BehaviourTreeBuilder()
            .Sequence("Assign Loot Sequence")
                // Ensure the bot has permission to assign loot
                .Condition("Can Assign Loot", time => _objectManager.PartyMembers.Any(x => x.Guid == playerGuid))

                // Assign the loot to the specified player
                .Do("Assign Loot", time =>
                {
                    _objectManager.AssignLoot(itemId, playerGuid);
                    return BehaviourTreeStatus.Success;
                })
            .End()
            .Build();
        /// <summary>
        /// Sequence to roll "Need" on a specific loot item during group loot distribution.
        /// </summary>
        /// <param name="itemId">The ID of the item to roll "Need" on.</param>
        /// <returns>IBehaviourTreeNode that manages rolling "Need" for the item.</returns>
        private IBehaviourTreeNode BuildLootRollNeedSequence(int itemId) => new BehaviourTreeBuilder()
            .Sequence("Loot Roll Need Sequence")
                // Ensure the bot can roll "Need" on the item
                .Condition("Can Roll Need", time => _objectManager.HasLootRollWindow(itemId))

                // Roll "Need" for the item
                .Do("Roll Need", time =>
                {
                    _objectManager.LootRollNeed(itemId);
                    return BehaviourTreeStatus.Success;
                })
            .End()
            .Build();
        /// <summary>
        /// Sequence to roll "Greed" on a specific loot item during group loot distribution.
        /// </summary>
        /// <param name="itemId">The ID of the item to roll "Greed" on.</param>
        /// <returns>IBehaviourTreeNode that manages rolling "Greed" for the item.</returns>
        private IBehaviourTreeNode BuildLootRollGreedSequence(int itemId) => new BehaviourTreeBuilder()
            .Sequence("Loot Roll Greed Sequence")
                // Ensure the bot can roll "Greed" on the item
                .Condition("Can Roll Greed", time => _objectManager.HasLootRollWindow(itemId))

                // Roll "Greed" for the item
                .Do("Roll Greed", time =>
                {
                    _objectManager.LootRollGreed(itemId);
                    return BehaviourTreeStatus.Success;
                })
            .End()
            .Build();
        /// <summary>
        /// Sequence to pass on a specific loot item during group loot distribution.
        /// </summary>
        /// <param name="itemId">The ID of the item to pass on.</param>
        /// <returns>IBehaviourTreeNode that manages passing on the item.</returns>
        private IBehaviourTreeNode BuildLootPassSequence(int itemId) => new BehaviourTreeBuilder()
            .Sequence("Loot Pass Sequence")
                // Ensure the bot can pass on the item
                .Condition("Can Pass Loot", time => _objectManager.HasLootRollWindow(itemId))

                // Pass on the loot item
                .Do("Pass Loot", time =>
                {
                    _objectManager.LootPass(itemId);
                    return BehaviourTreeStatus.Success;
                })
            .End()
            .Build();
        /// <summary>
        /// Sequence to send a group invite to another player.
        /// </summary>
        /// <param name="playerGuid">The GUID of the player to invite to the group.</param>
        /// <returns>IBehaviourTreeNode that manages sending the group invite.</returns>
        private IBehaviourTreeNode BuildSendGroupInviteSequence(ulong playerGuid) => new BehaviourTreeBuilder()
            .Sequence("Send Group Invite Sequence")
                // Ensure the player is not already in a group and can be invited
                .Condition("Can Send Group Invite", time => !_objectManager.PartyMembers.Any(x => x.Guid == playerGuid))

                // Send the group invite
                .Do("Send Group Invite", time =>
                {
                    _objectManager.InviteToGroup(playerGuid);
                    return BehaviourTreeStatus.Success;
                })
            .End()
            .Build();
        /// <summary>
        /// Sequence to send a group invite by player name (used by headless clients via PartyNetworkClientComponent).
        /// </summary>
        private IBehaviourTreeNode BuildSendGroupInviteByNameSequence(string playerName) => new BehaviourTreeBuilder()
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
        /// <summary>
        /// Sequence to accept a group invite from another player.
        /// </summary>
        /// <returns>IBehaviourTreeNode that manages accepting the group invite.</returns>
        private IBehaviourTreeNode AcceptGroupInviteSequence
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
        /// <summary>
        /// Sequence to decline a group invite from another player.
        /// </summary>
        /// <returns>IBehaviourTreeNode that manages declining the group invite.</returns>
        private IBehaviourTreeNode DeclineGroupInviteSequence => new BehaviourTreeBuilder()
            .Sequence("Decline Group Invite Sequence")
                // Ensure the bot has a pending invite to decline
                .Condition("Has Pending Invite", time => _objectManager.HasPendingGroupInvite())

                // Decline the group invite
                .Do("Decline Group Invite", time =>
                {
                    _objectManager.DeclineGroupInvite();
                    return BehaviourTreeStatus.Success;
                })
            .End()
            .Build();
        /// <summary>
        /// Sequence to kick a player from the group.
        /// </summary>
        /// <param name="playerGuid">The GUID of the player to kick from the group.</param>
        /// <returns>IBehaviourTreeNode that manages kicking the player from the group.</returns>
        private IBehaviourTreeNode BuildKickPlayerSequence(ulong playerGuid) => new BehaviourTreeBuilder()
            .Sequence("Kick Player Sequence")
                // Ensure the bot has permission to kick players and the target is valid
                .Condition("Can Kick Player", time => _objectManager.Player.Guid == _objectManager.PartyLeaderGuid)

                // Kick the player from the group
                .Do("Kick Player", time =>
                {
                    _objectManager.KickPlayer(playerGuid);
                    return BehaviourTreeStatus.Success;
                })
            .End()
            .Build();
        /// <summary>
        /// Sequence to leave the current group.
        /// </summary>
        /// <returns>IBehaviourTreeNode that manages leaving the group.</returns>
        private IBehaviourTreeNode LeaveGroupSequence => new BehaviourTreeBuilder()
            .Sequence("Leave Group Sequence")
                // Leave the group
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
        /// <summary>
        /// Sequence to disband the current group the bot is leading.
        /// </summary>
        /// <returns>IBehaviourTreeNode that manages disbanding the group.</returns>
        private IBehaviourTreeNode DisbandGroupSequence => new BehaviourTreeBuilder()
            .Sequence("Disband Group Sequence")
                // Ensure the bot is the leader of the group
                .Condition("Is Group Leader", time => _objectManager.Player.Guid == _objectManager.PartyLeaderGuid)

                // Disband the group
                .Do("Disband Group", time =>
                {
                    _objectManager.DisbandGroup();
                    return BehaviourTreeStatus.Success;
                })
            .End()
            .Build();
        /// <summary>
        /// Sequence to use an item, either on the bot or a target.
        /// </summary>
        /// <param name="fromBag">The bag the item is in.</param>
        /// <param name="fromSlot">The slot the item is in.</param>
        /// <param name="targetGuid">The GUID of the target on which to use the item (optional).</param>
        /// <returns>IBehaviourTreeNode that manages using the item.</returns>
        private IBehaviourTreeNode BuildUseItemSequence(int fromBag, int fromSlot, ulong targetGuid) => new BehaviourTreeBuilder()
            .Sequence("Use Item Sequence")
                // Ensure the bot has the item available to use
                .Condition("Has Item", time => _objectManager.GetContainedItem(fromBag, fromSlot) != null)

                // Use the item on the target (or self if target is null)
                .Do("Use Item", time =>
                {
                    _objectManager.UseItem(fromBag, fromSlot, targetGuid);
                    return BehaviourTreeStatus.Success;
                })
            .End()
            .Build();
        /// <summary>
        /// Sequence to move an item from one bag and slot to another bag and slot.
        /// </summary>
        /// <param name="fromBag">The source bag ID.</param>
        /// <param name="fromSlot">The source slot ID.</param>
        /// <param name="toBag">The destination bag ID.</param>
        /// <param name="toSlot">The destination slot ID.</param>
        /// <returns>IBehaviourTreeNode that manages moving the item.</returns>
        private IBehaviourTreeNode BuildMoveItemSequence(int fromBag, int fromSlot, int quantity, int toBag, int toSlot) => new BehaviourTreeBuilder()
            .Sequence("Move Item Sequence")
                // Ensure the bot has the item in the source slot
                .Condition("Has Item to Move", time => _objectManager.GetContainedItem(fromBag, fromSlot).Quantity >= quantity)

                // Move the item to the destination slot
                .Do("Move Item", time =>
                {
                    _objectManager.PickupContainedItem(fromBag, fromSlot, quantity);
                    _objectManager.PlaceItemInContainer(toBag, toSlot);
                    return BehaviourTreeStatus.Success;
                })
            .End()
            .Build();

        /// <summary>
        /// Sequence to destroy an item from the inventory.
        /// </summary>
        /// <param name="itemId">The ID of the item to destroy.</param>
        /// <param name="quantity">The quantity of the item to destroy.</param>
        /// <returns>IBehaviourTreeNode that manages destroying the item.</returns>
        private IBehaviourTreeNode BuildDestroyItemSequence(int bagId, int slotId, int quantity) => new BehaviourTreeBuilder()
            .Sequence("Destroy Item Sequence")
                // Ensure the bot has the item and quantity available to destroy
                .Condition("Has Item to Destroy", time => _objectManager.GetContainedItem(bagId, slotId) != null)

                // Destroy the item
                .Do("Destroy Item", time =>
                {
                    _objectManager.DestroyItemInContainer(bagId, slotId, quantity);
                    return BehaviourTreeStatus.Success;
                })
            .End()
            .Build();
        /// <summary>
        /// Sequence to equip an item from a bag.
        /// </summary>
        /// <param name="bag">The bag where the item is located.</param>
        /// <param name="slot">The slot in the bag where the item is located.</param>
        /// <returns>IBehaviourTreeNode that manages equipping the item.</returns>
        private IBehaviourTreeNode BuildEquipItemByIdSequence(int itemId)
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
                        // Send CMSG_AUTOEQUIP_ITEM for all 16 backpack slots. The server will
                        // equip valid items and ignore empty/non-equippable slots.
                        Log.Warning("[BOT RUNNER] Item {ItemId} not found in tracked inventory. Trying brute-force equip for all backpack slots.", itemId);
                        for (int slot = 0; slot < 16; slot++)
                            _objectManager.EquipItem(0, slot);
                        return BehaviourTreeStatus.Success;
                    })
                .End()
                .Build();
        }

        private IBehaviourTreeNode BuildEquipItemSequence(int bag, int slot) => new BehaviourTreeBuilder()
            .Sequence("Equip Item Sequence")
                // Ensure the bot has the item to equip
                .Condition("Has Item", time => _objectManager.GetContainedItem(bag, slot) != null)

                // Equip the item into the designated equipment slot
                .Do("Equip Item", time =>
                {
                    _objectManager.EquipItem(bag, slot);
                    return BehaviourTreeStatus.Success;
                })
            .End()
            .Build();

        /// <summary>
        /// Sequence to equip an item from a bag into a specific equipment slot.
        /// </summary>
        /// <param name="bag">The bag where the item is located.</param>
        /// <param name="slot">The slot in the bag where the item is located.</param>
        /// <param name="equipSlot">The equipment slot to place the item into.</param>
        /// <returns>IBehaviourTreeNode that manages equipping the item.</returns>
        private IBehaviourTreeNode BuildEquipItemSequence(int bag, int slot, EquipSlot equipSlot) => new BehaviourTreeBuilder()
            .Sequence("Equip Item Sequence")
                // Ensure the bot has the item to equip
                .Condition("Has Item", time => _objectManager.GetContainedItem(bag, slot) != null)

                // Equip the item into the designated equipment slot
                .Do("Equip Item", time =>
                {
                    _objectManager.EquipItem(bag, slot, equipSlot);
                    return BehaviourTreeStatus.Success;
                })
            .End()
            .Build();
        /// <summary>
        /// Sequence to unequip an item from a specific equipment slot and place it in the inventory.
        /// </summary>
        /// <param name="equipSlot">The equipment slot from which to unequip the item.</param>
        /// <returns>IBehaviourTreeNode that manages unequipping the item.</returns>
        private IBehaviourTreeNode BuildUnequipItemSequence(EquipSlot equipSlot) => new BehaviourTreeBuilder()
            .Sequence("Unequip Item Sequence")
                // Ensure there is an item in the specified equipment slot
                .Condition("Has Item Equipped", time => _objectManager.GetEquippedItem(equipSlot) != null)

                // Unequip the item from the specified equipment slot
                .Do("Unequip Item", time =>
                {
                    _objectManager.UnequipItem(equipSlot);
                    return BehaviourTreeStatus.Success;
                })
            .End()
            .Build();
        /// <summary>
        /// Sequence to split a stack of items into two slots in the inventory.
        /// </summary>
        /// <param name="bag">The bag where the stack is located.</param>
        /// <param name="slot">The slot where the stack is located.</param>
        /// <param name="quantity">The quantity to move to a new slot.</param>
        /// <param name="destinationBag">The destination bag for the split stack.</param>
        /// <param name="destinationSlot">The destination slot for the split stack.</param>
        /// <returns>IBehaviourTreeNode that manages splitting the item stack.</returns>
        private IBehaviourTreeNode BuildSplitStackSequence(int bag, int slot, int quantity, int destinationBag, int destinationSlot) => new BehaviourTreeBuilder()
            .Sequence("Split Stack Sequence")
                // Ensure the bot has the stack of items available
                .Condition("Has Item Stack", time => _objectManager.GetContainedItem(bag, slot).Quantity >= quantity)

                // Split the stack into the destination slot
                .Do("Split Stack", time =>
                {
                    _objectManager.SplitStack(bag, slot, quantity, destinationBag, destinationSlot);
                    return BehaviourTreeStatus.Success;
                })
            .End()
            .Build();
        /// <summary>
        /// Sequence to repair a specific item in the inventory.
        /// </summary>
        /// <param name="repairSlot">The slot where the item is located for repair.</param>
        /// <param name="cost">The cost in copper to repair the item.</param>
        /// <returns>IBehaviourTreeNode that manages repairing the item.</returns>
        private IBehaviourTreeNode BuildRepairItemSequence(int repairSlot) => new BehaviourTreeBuilder()
            .Sequence("Repair Item Sequence")
                // Ensure the bot has enough money to repair the item
                .Condition("Can Afford Repair", time => _objectManager.Player.Copper > _objectManager.MerchantFrame.RepairCost((EquipSlot)repairSlot))

                // Repair the item in the specified slot
                .Do("Repair Item", time =>
                {
                    _objectManager.MerchantFrame.RepairByEquipSlot((EquipSlot)repairSlot);
                    return BehaviourTreeStatus.Success;
                })
            .End()
            .Build();
        /// <summary>
        /// Sequence to repair all damaged items in the inventory.
        /// </summary>
        /// <returns>IBehaviourTreeNode that manages repairing all items.</returns>
        private IBehaviourTreeNode RepairAllItemsSequence => new BehaviourTreeBuilder()
            .Sequence("Repair All Items Sequence")
                // Ensure the bot has enough money to repair all items
                .Condition("Can Afford Full Repair", time => _objectManager.Player.Copper > _objectManager.MerchantFrame.TotalRepairCost)

                // Repair all damaged items
                .Do("Repair All Items", time =>
                {
                    _objectManager.MerchantFrame.RepairAll();
                    return BehaviourTreeStatus.Success;
                })
            .End()
            .Build();
        /// <summary>
        /// Sequence to dismiss a currently active buff.
        /// </summary>
        /// <param name="buffSlot">The slot or index of the buff to dismiss.</param>
        /// <returns>IBehaviourTreeNode that manages dismissing the buff.</returns>
        private IBehaviourTreeNode BuildDismissBuffSequence(string buff) => new BehaviourTreeBuilder()
            .Sequence("Dismiss Buff Sequence")
                // Ensure the bot has the buff in the specified slot
                .Condition("Has Buff", time => _objectManager.Player.HasBuff(buff))

                // Dismiss the buff
                .Do("Dismiss Buff", time =>
                {
                    _objectManager.Player.DismissBuff(buff);
                    return BehaviourTreeStatus.Success;
                })
            .End()
            .Build();
        /// <summary>
        /// Sequence to craft an item using a specific craft recipe or slot.
        /// </summary>
        /// <param name="craftSlotId">The ID of the crafting recipe or slot to use.</param>
        /// <returns>IBehaviourTreeNode that manages crafting the item.</returns>
        private IBehaviourTreeNode BuildCraftSequence(int craftSlotId) => new BehaviourTreeBuilder()
            .Sequence("Craft Sequence")
                // Ensure the bot can craft the item
                .Condition("Can Craft Item", time => _objectManager.CraftFrame.HasMaterialsNeeded(craftSlotId))

                // Perform the crafting action
                .Do("Craft Item", time =>
                {
                    _objectManager.CraftFrame.Craft(craftSlotId);
                    return BehaviourTreeStatus.Success;
                })
            .End()
            .Build();

        /// <summary>
        /// Sequence to log the bot into the server to get a session key.
        /// </summary>
        /// <param name="username">The bot's username.</param>
        /// <param name="password">The bot's password.</param>
        /// <returns>IBehaviourTreeNode that manages the login process.</returns>
        private IBehaviourTreeNode BuildLoginSequence(string username, string password) => new BehaviourTreeBuilder()
            .Sequence("Login Sequence")
                // Ensure the bot is on the login screen
                .Condition("Is On Login Screen", time => _objectManager.LoginScreen.IsOpen)

                // Input credentials and wait for async login to complete
                .Do("Input Credentials", time =>
                {
                    if (_objectManager.LoginScreen.IsLoggedIn) return BehaviourTreeStatus.Success;

                    _objectManager.LoginScreen.Login(username, password);
                    return BehaviourTreeStatus.Running; // Stay running while async login completes
                })

                // Verify login completed
                .Condition("Waiting in queue", time => _objectManager.LoginScreen.IsLoggedIn)
                .Do("Select Realm", time =>
                {
                    if (_objectManager.LoginScreen.QueuePosition > 0)
                        return BehaviourTreeStatus.Running;
                    return BehaviourTreeStatus.Success;
                })
            .End()
            .Build();

        /// <summary>
        /// Sequence to log the bot into the server to get a session key.
        /// </summary>
        /// <param name="username">The bot's username.</param>
        /// <param name="password">The bot's password.</param>
        /// <returns>IBehaviourTreeNode that manages the login process.</returns>
        private IBehaviourTreeNode BuildRealmSelectionSequence() => new BehaviourTreeBuilder()
            .Sequence("Realm Selection Sequence")
                // Select the first available realm
                .Condition("On Realm Selection Screen", time => _objectManager.RealmSelectScreen.IsOpen && _objectManager.LoginScreen.IsLoggedIn)
                .Do("Select Realm", time =>
                {
                    if (_objectManager.RealmSelectScreen.CurrentRealm != null) return BehaviourTreeStatus.Success;

                    _objectManager.RealmSelectScreen.SelectRealm(_objectManager.RealmSelectScreen.GetRealmList()[0]);
                    return BehaviourTreeStatus.Success;
                })
            .End()
            .Build();

        /// <summary>
        /// Sequence to log the bot out of the game.
        /// </summary>
        /// <returns>IBehaviourTreeNode that manages the logout process.</returns>
        private IBehaviourTreeNode LogoutSequence => new BehaviourTreeBuilder()
            .Sequence("Logout Sequence")
                // Ensure the bot can log out (not in combat, etc.)
                .Condition("Can Log Out", time => !_objectManager.LoginScreen.IsOpen)

                // Perform the logout action
                .Do("Log Out", time =>
                {
                    _objectManager.Logout();
                    return BehaviourTreeStatus.Success;
                })
            .End()
            .Build();
        /// <summary>
        /// Sequence to create a new character with specified name, race, and class.
        /// </summary>
        /// <param name="parameters">A list containing the name, race, and class of the new character.</param>
        /// <returns>IBehaviourTreeNode that manages creating the character.</returns>
        private IBehaviourTreeNode BuildRequestCharacterSequence() => new BehaviourTreeBuilder()
            .Sequence("Create Character Sequence")
                // Ensure the bot is on the character creation screen
                .Condition("On Character Creation Screen", time => _objectManager.CharacterSelectScreen.IsOpen)

                // Create the new character with the specified details
                .Do("Request Character List", time =>
                {
                    _objectManager.CharacterSelectScreen.RefreshCharacterListFromServer();
                    return BehaviourTreeStatus.Success;
                })
            .End()
            .Build();
        /// <summary>
        /// Sequence to create a new character with specified name, race, and class.
        /// </summary>
        /// <param name="parameters">A list containing the name, race, and class of the new character.</param>
        /// <returns>IBehaviourTreeNode that manages creating the character.</returns>
        private IBehaviourTreeNode BuildCreateCharacterSequence(List<object> parameters) => new BehaviourTreeBuilder()
            .Sequence("Create Character Sequence")
                // Ensure the bot is on the character creation screen
                .Condition("On Character Creation Screen", time => _objectManager.CharacterSelectScreen.IsOpen)

                // Create the new character with the specified details
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
        /// <summary>
        /// Sequence to delete an existing character based on character ID.
        /// </summary>
        /// <param name="characterId">The ID of the character to delete.</param>
        /// <returns>IBehaviourTreeNode that manages deleting the character.</returns>
        private IBehaviourTreeNode BuildDeleteCharacterSequence(ulong characterId) => new BehaviourTreeBuilder()
            .Sequence("Delete Character Sequence")
                // Ensure the bot is on the character selection screen
                .Condition("On Character Select Screen", time => _objectManager.CharacterSelectScreen.IsOpen)

                // Delete the specified character
                .Do("Delete Character", time =>
                {
                    _objectManager.CharacterSelectScreen.DeleteCharacter(characterId);
                    return BehaviourTreeStatus.Success;
                })
            .End()
            .Build();
        /// <summary>
        /// Sequence to enter the game world with a selected character.
        /// </summary>
        /// <param name="characterGuid">The GUID of the character to enter the world with.</param>
        /// <returns>IBehaviourTreeNode that manages entering the game world.</returns>
        private IBehaviourTreeNode BuildEnterWorldSequence(ulong characterGuid) => new BehaviourTreeBuilder()
            .Sequence("Enter World Sequence")
                // Ensure the bot is on the character select screen
                .Condition("On Character Select Screen", time => _objectManager.CharacterSelectScreen.IsOpen)

                // Enter the world with the specified character
                .Do("Enter World", time =>
                {
                    _objectManager.EnterWorld(characterGuid);
                    return BehaviourTreeStatus.Success;
                })
            .End()
            .Build();

        #region Snapshot Building

        private void PopulateSnapshotFromObjectManager()
        {
            _activitySnapshot.Timestamp = (uint)DateTimeOffset.UtcNow.ToUnixTimeSeconds();

            // Clear any action from the previous response to prevent echo-back.
            // Without this, the old CurrentAction stays in the snapshot, gets sent
            // back to StateManager, and is returned again — causing infinite re-execution.
            _activitySnapshot.CurrentAction = null;

            // Detect screen state
            if (_objectManager.HasEnteredWorld && _objectManager.Player != null)
            {
                _activitySnapshot.ScreenState = "InWorld";
                _activitySnapshot.CharacterName = _objectManager.Player.Name ?? string.Empty;
            }
            else if (_objectManager.CharacterSelectScreen?.IsOpen == true)
            {
                _activitySnapshot.ScreenState = "CharacterSelect";
            }
            else if (_objectManager.LoginScreen?.IsLoggedIn == true)
            {
                _activitySnapshot.ScreenState = "RealmSelect";
            }
            else
            {
                _activitySnapshot.ScreenState = "LoginScreen";
            }

            // Only populate game data when in world
            if (_activitySnapshot.ScreenState != "InWorld" || _objectManager.Player == null)
                return;

            var player = _objectManager.Player;

            // Movement data
            try
            {
                var pos = player.Position;
                _activitySnapshot.MovementData = new Game.MovementData
                {
                    MovementFlags = (uint)player.MovementFlags,
                    FallTime = player.FallTime,
                    JumpVerticalSpeed = player.JumpVerticalSpeed,
                    JumpSinAngle = player.JumpSinAngle,
                    JumpCosAngle = player.JumpCosAngle,
                    JumpHorizontalSpeed = player.JumpHorizontalSpeed,
                    SwimPitch = player.SwimPitch,
                    WalkSpeed = player.WalkSpeed,
                    RunSpeed = player.RunSpeed,
                    RunBackSpeed = player.RunBackSpeed,
                    SwimSpeed = player.SwimSpeed,
                    SwimBackSpeed = player.SwimBackSpeed,
                    TurnRate = player.TurnRate,
                    Facing = player.Facing,
                    TransportGuid = player.TransportGuid,
                    TransportOrientation = player.TransportOrientation,
                };
                if (pos != null)
                {
                    _activitySnapshot.MovementData.Position = new Game.Position
                    {
                        X = pos.X,
                        Y = pos.Y,
                        Z = pos.Z,
                    };
                }
            }
            catch (Exception ex)
            {
                Log.Warning($"[BOT RUNNER] Error populating movement data: {ex.Message}");
            }

            // Party leader GUID (0 if not in a group)
            // Use agent factory (headless) if available, otherwise fall back to IObjectManager (foreground)
            try
            {
                var factory = _agentFactoryAccessor?.Invoke();
                if (factory != null)
                {
                    var members = factory.PartyAgent.GetGroupMembers();
                    var leader = members.FirstOrDefault(m => m.IsLeader);
                    _activitySnapshot.PartyLeaderGuid = leader?.Guid ?? (factory.PartyAgent.IsGroupLeader && factory.PartyAgent.GroupSize > 0 ? player.Guid : 0);
                }
                else
                {
                    _activitySnapshot.PartyLeaderGuid = _objectManager.PartyLeaderGuid;
                }
            }
            catch (Exception ex)
            {
                Log.Warning($"[BOT RUNNER] Error populating party leader GUID: {ex.Message}");
            }

            // Player protobuf
            try
            {
                _activitySnapshot.Player = BuildPlayerProtobuf(player);
            }
            catch (Exception ex)
            {
                Log.Warning($"[BOT RUNNER] Error populating player: {ex.Message}");
            }

            // Spell list (known spell IDs for combat coordination)
            try
            {
                if (_activitySnapshot.Player != null)
                {
                    _activitySnapshot.Player.SpellList.Clear();
                    foreach (var spellId in _objectManager.KnownSpellIds)
                        _activitySnapshot.Player.SpellList.Add(spellId);
                }
            }
            catch (Exception ex)
            {
                Log.Warning($"[BOT RUNNER] Error populating spell list: {ex.Message}");
            }

            // Equipment slots (inventory map: slot 0-18 → 64-bit GUID)
            // WoWPlayer.Inventory stores GUID pairs: [slot*2]=LOW, [slot*2+1]=HIGH
            try
            {
                if (_activitySnapshot.Player != null && player is GameData.Core.Interfaces.IWoWPlayer wp)
                {
                    _activitySnapshot.Player.Inventory.Clear();
                    int nonZeroCount = 0;
                    for (uint slot = 0; slot < 19; slot++)
                    {
                        uint lowIdx = slot * 2;
                        uint highIdx = slot * 2 + 1;
                        if (highIdx < (uint)wp.Inventory.Length)
                        {
                            ulong guid = ((ulong)wp.Inventory[highIdx] << 32) | wp.Inventory[lowIdx];
                            if (guid != 0)
                            {
                                _activitySnapshot.Player.Inventory[slot] = guid;
                                nonZeroCount++;
                            }
                        }
                    }
                    if (nonZeroCount > 0)
                        Log.Information("[BOT RUNNER] Equipment: {Count} slots occupied (Inventory[].Length={Len})", nonZeroCount, wp.Inventory.Length);
                }
                else
                {
                    Log.Warning("[BOT RUNNER] Equipment skipped: Player={HasPlayer}, IsIWoWPlayer={IsType}",
                        _activitySnapshot.Player != null, player is GameData.Core.Interfaces.IWoWPlayer);
                }
            }
            catch (Exception ex)
            {
                Log.Warning($"[BOT RUNNER] Error populating equipment inventory: {ex.Message}");
            }

            // Inventory items (bagContents map: sequential index → itemId)
            try
            {
                if (_activitySnapshot.Player != null)
                {
                    _activitySnapshot.Player.BagContents.Clear();
                    uint slotIndex = 0;
                    foreach (var item in _objectManager.GetContainedItems())
                    {
                        _activitySnapshot.Player.BagContents[slotIndex++] = item.ItemId;
                    }

                    // Diagnostic: log item counts when they change
                    var itemObjectCount = _objectManager.Objects.Count(o => o.ObjectType == GameData.Core.Enums.WoWObjectType.Item);
                    if (slotIndex != _lastLoggedContainedItems || itemObjectCount != _lastLoggedItemObjects)
                    {
                        _lastLoggedContainedItems = (int)slotIndex;
                        _lastLoggedItemObjects = itemObjectCount;
                        Log.Information("[BOT RUNNER] Inventory changed: {ContainedItems} contained items, {ItemObjects} item objects in OM",
                            slotIndex, itemObjectCount);
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Warning($"[BOT RUNNER] Error populating inventory: {ex.Message}");
            }

            // Nearby units (within 40y)
            try
            {
                _activitySnapshot.NearbyUnits.Clear();
                var playerPos = player.Position;
                if (playerPos != null)
                {
                    foreach (var unit in _objectManager.Units
                        .Where(u => u.Guid != player.Guid && u.Position != null && u.Position.DistanceTo(playerPos) < 40f))
                    {
                        _activitySnapshot.NearbyUnits.Add(BuildUnitProtobuf(unit));
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Warning($"[BOT RUNNER] Error populating nearby units: {ex.Message}");
            }

            // Nearby game objects (within 40y)
            try
            {
                _activitySnapshot.NearbyObjects.Clear();
                var playerPos = player.Position;
                if (playerPos != null)
                {
                    foreach (var go in _objectManager.GameObjects
                        .Where(g => g.Position != null && g.Position.DistanceTo(playerPos) < 40f))
                    {
                        _activitySnapshot.NearbyObjects.Add(BuildGameObjectProtobuf(go));
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Warning($"[BOT RUNNER] Error populating nearby objects: {ex.Message}");
            }
        }

        private static Game.WoWPlayer BuildPlayerProtobuf(IWoWUnit unit)
        {
            var player = new Game.WoWPlayer
            {
                Unit = BuildUnitProtobuf(unit),
            };

            if (unit is IWoWLocalPlayer lp)
            {
                try { player.Coinage = lp.Copper; } catch { }
            }

            return player;
        }

        private static Game.WoWUnit BuildUnitProtobuf(IWoWUnit unit)
        {
            var pos = unit.Position;
            var protoUnit = new Game.WoWUnit
            {
                GameObject = new Game.WoWGameObject
                {
                    Base = new Game.WoWObject
                    {
                        Guid = unit.Guid,
                        ObjectType = (uint)unit.ObjectType,
                        Facing = unit.Facing,
                        ScaleX = unit.ScaleX,
                    },
                    FactionTemplate = unit.FactionTemplate,
                    Level = unit.Level,
                },
                Health = unit.Health,
                MaxHealth = unit.MaxHealth,
                TargetGuid = unit.TargetGuid,
                UnitFlags = (uint)unit.UnitFlags,
                DynamicFlags = (uint)unit.DynamicFlags,
                MovementFlags = (uint)unit.MovementFlags,
                MountDisplayId = unit.MountDisplayId,
                ChannelSpellId = unit.ChannelingId,
                SummonedBy = unit.SummonedByGuid,
                NpcFlags = (uint)unit.NpcFlags,
            };

            if (pos != null)
            {
                protoUnit.GameObject.Base.Position = new Game.Position { X = pos.X, Y = pos.Y, Z = pos.Z };
            }

            // Power map: Mana, Rage, Energy
            try
            {
                if (unit.Powers.TryGetValue(Powers.MANA, out uint mana)) protoUnit.Power[0] = mana;
                if (unit.MaxPowers.TryGetValue(Powers.MANA, out uint maxMana)) protoUnit.MaxPower[0] = maxMana;
                if (unit.Powers.TryGetValue(Powers.RAGE, out uint rage)) protoUnit.Power[1] = rage;
                if (unit.MaxPowers.TryGetValue(Powers.RAGE, out uint maxRage)) protoUnit.MaxPower[1] = maxRage;
                if (unit.Powers.TryGetValue(Powers.ENERGY, out uint energy)) protoUnit.Power[3] = energy;
                if (unit.MaxPowers.TryGetValue(Powers.ENERGY, out uint maxEnergy)) protoUnit.MaxPower[3] = maxEnergy;
            }
            catch { }

            // Auras (from AuraFields - raw spell IDs)
            try
            {
                if (unit.AuraFields != null)
                {
                    foreach (var auraSpellId in unit.AuraFields.Where(a => a != 0))
                        protoUnit.Auras.Add(auraSpellId);
                }
            }
            catch { }

            return protoUnit;
        }

        private static Game.WoWGameObject BuildGameObjectProtobuf(IWoWGameObject go)
        {
            var pos = go.Position;
            var protoGo = new Game.WoWGameObject
            {
                Base = new Game.WoWObject
                {
                    Guid = go.Guid,
                    ObjectType = (uint)go.ObjectType,
                    Facing = go.Facing,
                },
                DisplayId = go.DisplayId,
                GoState = (uint)go.GoState,
                GameObjectType = go.TypeId,
                Flags = go.Flags,
                Level = go.Level,
            };

            if (pos != null)
            {
                protoGo.Base.Position = new Game.Position { X = pos.X, Y = pos.Y, Z = pos.Z };
            }

            return protoGo;
        }

        #endregion
    }
}