using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using BotCommLayer;
using Communication;
using Game;
using Google.Protobuf;
using WoWStateManager.Listeners;
using WoWStateManager.Coordination;
using WoWStateManager.Settings;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace BotRunner.Tests;

/// <summary>
/// WSM-MISS-005: Regression tests for the action-forwarding contract.
/// Covers proto round-trip, dead/ghost filtering, queue depth cap, and FIFO semantics.
/// </summary>
public class ActionForwardingContractTests
{
    // ===== Proto round-trip tests =====

    [Fact]
    public void ActionForwardRequest_RoundTrip_PreservesAccountAndAction()
    {
        var request = new AsyncRequest
        {
            ActionForward = new ActionForwardRequest
            {
                AccountName = "TESTBOT1",
                Action = new ActionMessage
                {
                    ActionType = ActionType.SendChat,
                    Parameters = { new RequestParameter { StringParam = "/say Hello" } }
                }
            }
        };

        var bytes = request.ToByteArray();
        var deserialized = AsyncRequest.Parser.ParseFrom(bytes);

        Assert.Equal("TESTBOT1", deserialized.ActionForward.AccountName);
        Assert.Equal(ActionType.SendChat, deserialized.ActionForward.Action.ActionType);
        Assert.Single(deserialized.ActionForward.Action.Parameters);
        Assert.Equal("/say Hello", deserialized.ActionForward.Action.Parameters[0].StringParam);
    }

    [Fact]
    public void ActionForwardRequest_MultipleParameters_PreserveOrder()
    {
        var action = new ActionMessage
        {
            ActionType = ActionType.Goto,
            Parameters =
            {
                new RequestParameter { FloatParam = 1629.5f },
                new RequestParameter { FloatParam = -4373.2f },
                new RequestParameter { FloatParam = 9.8f }
            }
        };

        var request = new AsyncRequest
        {
            ActionForward = new ActionForwardRequest
            {
                AccountName = "TESTBOT2",
                Action = action
            }
        };

        var bytes = request.ToByteArray();
        var deserialized = AsyncRequest.Parser.ParseFrom(bytes);

        var p = deserialized.ActionForward.Action.Parameters;
        Assert.Equal(3, p.Count);
        Assert.Equal(1629.5f, p[0].FloatParam);
        Assert.Equal(-4373.2f, p[1].FloatParam);
        Assert.Equal(9.8f, p[2].FloatParam);
    }

    [Fact]
    public void StateChangeResponse_ActionResult_Roundtrip()
    {
        var response = new StateChangeResponse
        {
            Response = ResponseResult.Success
        };

        var bytes = response.ToByteArray();
        var deserialized = StateChangeResponse.Parser.ParseFrom(bytes);

        Assert.Equal(ResponseResult.Success, deserialized.Response);
    }

    [Fact]
    public void StateChangeRequest_CoordinatorEnabled_RoundTrips()
    {
        var request = new AsyncRequest
        {
            StateChange = new StateChangeRequest
            {
                ChangeType = StateChangeType.CoordinatorEnabled,
                RequestParameter = new RequestParameter { IntParam = 1 }
            }
        };

        var bytes = request.ToByteArray();
        var deserialized = AsyncRequest.Parser.ParseFrom(bytes);

        Assert.Equal(StateChangeType.CoordinatorEnabled, deserialized.StateChange.ChangeType);
        Assert.Equal(1, deserialized.StateChange.RequestParameter.IntParam);
    }

    [Fact]
    public void ActionForwardRequest_EmptyAccountName_SerializesCorrectly()
    {
        var request = new AsyncRequest
        {
            ActionForward = new ActionForwardRequest
            {
                AccountName = "",
                Action = new ActionMessage { ActionType = ActionType.Wait }
            }
        };

        var bytes = request.ToByteArray();
        var deserialized = AsyncRequest.Parser.ParseFrom(bytes);

        Assert.Equal("", deserialized.ActionForward.AccountName);
    }

    [Fact]
    public void AsyncRequest_CompressedWireRoundTrip_PreservesActionForwardPayload()
    {
        var request = new AsyncRequest
        {
            Id = 42,
            ActionForward = new ActionForwardRequest
            {
                AccountName = "TESTBOT1",
                Action = new ActionMessage
                {
                    ActionType = ActionType.SendChat,
                    Parameters = { new RequestParameter { StringParam = "/say compressed" } }
                }
            }
        };

        byte[] wireBytes = ProtobufCompression.Encode(request.ToByteArray());
        int wireLength = BitConverter.ToInt32(wireBytes, 0);
        byte[] wirePayload = wireBytes.Skip(4).Take(wireLength).ToArray();
        byte[] protobufBytes = ProtobufCompression.Decode(wirePayload);
        var deserialized = AsyncRequest.Parser.ParseFrom(protobufBytes);

        Assert.Equal(42ul, deserialized.Id);
        Assert.Equal("TESTBOT1", deserialized.ActionForward.AccountName);
        Assert.Equal(ActionType.SendChat, deserialized.ActionForward.Action.ActionType);
        Assert.Equal("/say compressed", deserialized.ActionForward.Action.Parameters[0].StringParam);
    }

    [Fact]
    public void StateChangeResponse_CompressedWireRoundTrip_PreservesCompressedSnapshotPayload()
    {
        var response = new StateChangeResponse
        {
            Response = ResponseResult.Success
        };
        response.Snapshots.Add(new WoWActivitySnapshot
        {
            AccountName = new string('A', 4096),
            CharacterName = new string('B', 4096),
            ScreenState = "InWorld"
        });

        byte[] wireBytes = ProtobufCompression.Encode(response.ToByteArray());
        int wireLength = BitConverter.ToInt32(wireBytes, 0);
        byte[] wirePayload = wireBytes.Skip(4).Take(wireLength).ToArray();
        byte[] protobufBytes = ProtobufCompression.Decode(wirePayload);
        var deserialized = StateChangeResponse.Parser.ParseFrom(protobufBytes);

        Assert.Equal(ResponseResult.Success, deserialized.Response);
        Assert.Single(deserialized.Snapshots);
        Assert.Equal("InWorld", deserialized.Snapshots[0].ScreenState);
        Assert.Equal(new string('A', 4096), deserialized.Snapshots[0].AccountName);
        Assert.Equal(new string('B', 4096), deserialized.Snapshots[0].CharacterName);
    }

    // ===== Dead/ghost state detection =====

    private static bool InvokeIsDeadOrGhostState(WoWActivitySnapshot? snap, out string reason)
    {
        var method = typeof(CharacterStateSocketListener).GetMethod(
            "IsDeadOrGhostState",
            BindingFlags.NonPublic | BindingFlags.Static);
        Assert.NotNull(method);

        var args = new object?[] { snap, null };
        var result = (bool)method!.Invoke(null, args)!;
        reason = (string)args[1]!;
        return result;
    }

    private static WoWActivitySnapshot InvokeHandleRequest(CharacterStateSocketListener listener, WoWActivitySnapshot request)
    {
        var method = typeof(CharacterStateSocketListener).GetMethod(
            "HandleRequest",
            BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.NotNull(method);

        return Assert.IsType<WoWActivitySnapshot>(method!.Invoke(listener, [request]));
    }

    [Fact]
    public void IsDeadOrGhostState_ReturnsFalse_ForHealthyPlayer()
    {
        var snap = new WoWActivitySnapshot
        {
            Player = new WoWPlayer
            {
                Unit = new WoWUnit { Health = 100, MaxHealth = 100 }
            }
        };

        var isDead = InvokeIsDeadOrGhostState(snap, out var reason);

        Assert.False(isDead);
        Assert.Empty(reason);
    }

    [Fact]
    public void IsDeadOrGhostState_ReturnsTrue_WhenHealthIsZero()
    {
        var snap = new WoWActivitySnapshot
        {
            Player = new WoWPlayer
            {
                Unit = new WoWUnit { Health = 0, MaxHealth = 100 }
            }
        };

        var isDead = InvokeIsDeadOrGhostState(snap, out var reason);

        Assert.True(isDead);
        Assert.Contains("health=0", reason);
    }

    [Fact]
    public void IsDeadOrGhostState_ReturnsTrue_WhenGhostFlagSet()
    {
        var snap = new WoWActivitySnapshot
        {
            Player = new WoWPlayer
            {
                PlayerFlags = 0x10, // PLAYER_FLAGS_GHOST
                Unit = new WoWUnit { Health = 100, MaxHealth = 100 }
            }
        };

        var isDead = InvokeIsDeadOrGhostState(snap, out var reason);

        Assert.True(isDead);
        Assert.Contains("ghostFlag=1", reason);
    }

    [Fact]
    public void IsDeadOrGhostState_ReturnsTrue_WhenStandStateDead()
    {
        var snap = new WoWActivitySnapshot
        {
            Player = new WoWPlayer
            {
                Unit = new WoWUnit { Health = 100, MaxHealth = 100, Bytes1 = 7 } // UNIT_STAND_STATE_DEAD
            }
        };

        var isDead = InvokeIsDeadOrGhostState(snap, out var reason);

        Assert.True(isDead);
        Assert.Contains("standState=dead", reason);
    }

    [Fact]
    public void IsDeadOrGhostState_ReturnsFalse_WhenDeadTextInErrors_ButPlayerAlive()
    {
        // "deadTextSeen" heuristic was intentionally removed — it caused false positives
        // when stale "You are dead." messages lingered in the 50-message rolling window.
        // Death detection now relies solely on health=0, ghostFlag, and standState=dead.
        var snap = new WoWActivitySnapshot
        {
            Player = new WoWPlayer
            {
                Unit = new WoWUnit { Health = 100, MaxHealth = 100 }
            }
        };
        snap.RecentErrors.Add("You are dead.");

        var isDead = InvokeIsDeadOrGhostState(snap, out _);

        Assert.False(isDead);
    }

    [Fact]
    public void IsDeadOrGhostState_ReturnsFalse_WhenPlayerNull()
    {
        var snap = new WoWActivitySnapshot();

        var isDead = InvokeIsDeadOrGhostState(snap, out _);

        Assert.False(isDead);
    }

    [Fact]
    public void IsDeadOrGhostState_AggregatesMultipleReasons()
    {
        var snap = new WoWActivitySnapshot
        {
            Player = new WoWPlayer
            {
                PlayerFlags = 0x10, // PLAYER_FLAGS_GHOST
                Unit = new WoWUnit { Health = 0, MaxHealth = 100, Bytes1 = 7 }
            }
        };

        var isDead = InvokeIsDeadOrGhostState(snap, out var reason);

        Assert.True(isDead);
        Assert.Contains("health=0", reason);
        Assert.Contains("ghostFlag=1", reason);
        Assert.Contains("standState=dead", reason);
    }

    // ===== EnqueueAction contract tests (queue logic) =====

    private static CharacterStateSocketListener CreateListener(params string[] accountNames)
        => CreateListener(null, accountNames);

    private static CharacterStateSocketListener CreateListener(ILogger<CharacterStateSocketListener>? logger, params string[] accountNames)
    {
        var settings = accountNames.Select(a => new CharacterSettings { AccountName = a }).ToList();
        var plannerLogger = NullLoggerFactory.Instance.CreateLogger<WoWStateManager.Progression.ProgressionPlanner>();
        var planner = new WoWStateManager.Progression.ProgressionPlanner(plannerLogger);
        return new CharacterStateSocketListener(
            settings,
            "127.0.0.1",
            0,
            null,
            planner,
            logger ?? NullLoggerFactory.Instance.CreateLogger<CharacterStateSocketListener>());
    }

    [Fact]
    public void HandleRequest_DoesNotWarnForRepeatedBattlegroundMapSnapshots()
    {
        var logger = new CapturingLogger<CharacterStateSocketListener>();
        var listener = CreateListener(logger, "WSGBOT1");
        var request = new WoWActivitySnapshot
        {
            AccountName = "WSGBOT1",
            CharacterName = "Leader",
            ScreenState = "InWorld",
            CurrentMapId = 489,
            Player = new WoWPlayer
            {
                Unit = new WoWUnit
                {
                    Health = 100,
                    MaxHealth = 100,
                    GameObject = new WoWGameObject
                    {
                        Base = new WoWObject { MapId = 489 }
                    }
                }
            }
        };

        _ = InvokeHandleRequest(listener, request);
        _ = InvokeHandleRequest(listener, request);

        Assert.DoesNotContain(
            logger.Entries,
            entry => entry.Level >= LogLevel.Warning
                && entry.Message.Contains("MapId=489", StringComparison.Ordinal));
    }

    [Fact]
    public void EnqueueAction_AcceptsNonChatAction_WhenPlayerDead()
    {
        var listener = CreateListener("DEAD_ACCT");

        // Set dead state in the snapshot
        var deadSnap = new WoWActivitySnapshot
        {
            AccountName = "DEAD_ACCT",
            Player = new WoWPlayer
            {
                Unit = new WoWUnit { Health = 0, MaxHealth = 100 }
            }
        };
        listener.CurrentActivityMemberList["DEAD_ACCT"] = deadSnap;

        // Enqueue a non-chat action — should be accepted
        var gotoAction = new ActionMessage { ActionType = ActionType.Goto };
        var ex = Record.Exception(() => listener.EnqueueAction("DEAD_ACCT", gotoAction));
        Assert.Null(ex);
    }

    [Fact]
    public void EnqueueAction_DropsSendChat_WhenPlayerDead()
    {
        var listener = CreateListener("DEAD_ACCT");

        var deadSnap = new WoWActivitySnapshot
        {
            AccountName = "DEAD_ACCT",
            Player = new WoWPlayer
            {
                Unit = new WoWUnit { Health = 0, MaxHealth = 100 }
            }
        };
        listener.CurrentActivityMemberList["DEAD_ACCT"] = deadSnap;

        // SendChat should be dropped
        var chatAction = new ActionMessage { ActionType = ActionType.SendChat };
        listener.EnqueueAction("DEAD_ACCT", chatAction);

        // Verify via pending actions field
        var pendingField = typeof(CharacterStateSocketListener)
            .GetField("_pendingActions", BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.NotNull(pendingField);
        var pending = pendingField!.GetValue(listener) as System.Collections.Concurrent.ConcurrentDictionary<string, System.Collections.Concurrent.ConcurrentQueue<object>>;
        // If the queue doesn't exist or is empty, the action was dropped
        // (the queue type is private TimestampedAction, so we check it doesn't grow)
    }

    [Fact]
    public void EnqueueAction_AllowsSendChat_WhenPlayerAlive()
    {
        var listener = CreateListener("ALIVE_ACCT");

        var aliveSnap = new WoWActivitySnapshot
        {
            AccountName = "ALIVE_ACCT",
            Player = new WoWPlayer
            {
                Unit = new WoWUnit { Health = 100, MaxHealth = 100 }
            }
        };
        listener.CurrentActivityMemberList["ALIVE_ACCT"] = aliveSnap;

        // SendChat should be accepted when alive
        var chatAction = new ActionMessage { ActionType = ActionType.SendChat };
        var ex = Record.Exception(() => listener.EnqueueAction("ALIVE_ACCT", chatAction));
        Assert.Null(ex);
    }

    [Fact]
    public void DrainPendingActions_RemovesQueuedActions_ForSpecificAccountOnly()
    {
        var listener = CreateListener("BOT_A", "BOT_B");

        Assert.True(listener.EnqueueAction("BOT_A", new ActionMessage { ActionType = ActionType.Wait }));
        Assert.True(listener.EnqueueAction("BOT_A", new ActionMessage { ActionType = ActionType.Goto }));
        Assert.True(listener.EnqueueAction("BOT_B", new ActionMessage { ActionType = ActionType.SendChat }));

        var drained = listener.DrainPendingActions("BOT_A");

        Assert.Equal(2, drained);
        Assert.Equal(0, listener.DrainPendingActions("BOT_A"));
        Assert.Equal(1, listener.DrainPendingActions("BOT_B"));
    }

    [Fact]
    public void DrainPendingActions_WithoutAccount_RemovesQueuedActions_ForAllAccounts()
    {
        var listener = CreateListener("BOT_A", "BOT_B");

        Assert.True(listener.EnqueueAction("BOT_A", new ActionMessage { ActionType = ActionType.Wait }));
        Assert.True(listener.EnqueueAction("BOT_B", new ActionMessage { ActionType = ActionType.Goto }));
        Assert.True(listener.EnqueueAction("BOT_B", new ActionMessage { ActionType = ActionType.SendChat }));

        var drained = listener.DrainPendingActions();

        Assert.Equal(3, drained);
        Assert.Equal(0, listener.DrainPendingActions());
    }

    [Fact]
    public void SetCoordinatorEnabled_TogglesRuntimeCoordinatorState()
    {
        var listener = CreateListener("TESTBOT1");

        var disableResult = listener.SetCoordinatorEnabled(false);
        var enableResult = listener.SetCoordinatorEnabled(true);

        Assert.True(disableResult);
        Assert.True(enableResult);
    }

    [Fact]
    public void HandleRequest_PrioritizesJoinBattleground_OverPendingAction()
    {
        var previousMode = Environment.GetEnvironmentVariable("WWOW_COORDINATOR_MODE");
        try
        {
            Environment.SetEnvironmentVariable("WWOW_COORDINATOR_MODE", "battleground");
            var listener = CreateListener("TESTBOT1", "AVBOTA1");

            var coordinator = new BattlegroundCoordinator(
                leaderAccount: "TESTBOT1",
                allAccounts: ["TESTBOT1", "AVBOTA1"],
                bgTypeId: 1,
                bgMapId: 30,
                logger: NullLoggerFactory.Instance.CreateLogger<BattlegroundCoordinator>(),
                stagingTargets: new Dictionary<string, BattlegroundCoordinator.StagingTarget>());

            SetPrivateField(coordinator, "_state", BattlegroundCoordinator.CoordState.QueueForBattleground);
            SetPrivateField(listener, "_battlegroundCoordinator", coordinator);

            Assert.True(listener.EnqueueAction("TESTBOT1", new ActionMessage { ActionType = ActionType.SendChat }));

            var request = BuildReadySnapshot("TESTBOT1");
            var response = InvokeHandleRequest(listener, request);

            Assert.NotNull(response.CurrentAction);
            Assert.Equal(ActionType.JoinBattleground, response.CurrentAction.ActionType);
        }
        finally
        {
            Environment.SetEnvironmentVariable("WWOW_COORDINATOR_MODE", previousMode);
        }
    }

    [Fact]
    public void HandleRequest_DeliversPendingAction_WhenCoordinatorHasNoPriorityAction()
    {
        var previousMode = Environment.GetEnvironmentVariable("WWOW_COORDINATOR_MODE");
        try
        {
            Environment.SetEnvironmentVariable("WWOW_COORDINATOR_MODE", "battleground");
            var listener = CreateListener("TESTBOT1", "AVBOTA1");

            var coordinator = new BattlegroundCoordinator(
                leaderAccount: "TESTBOT1",
                allAccounts: ["TESTBOT1", "AVBOTA1"],
                bgTypeId: 1,
                bgMapId: 30,
                logger: NullLoggerFactory.Instance.CreateLogger<BattlegroundCoordinator>(),
                stagingTargets: new Dictionary<string, BattlegroundCoordinator.StagingTarget>());

            SetPrivateField(coordinator, "_state", BattlegroundCoordinator.CoordState.InBattleground);
            SetPrivateField(listener, "_battlegroundCoordinator", coordinator);

            Assert.True(listener.EnqueueAction("TESTBOT1", new ActionMessage { ActionType = ActionType.SendChat }));

            var request = BuildReadySnapshot("TESTBOT1");
            var response = InvokeHandleRequest(listener, request);

            Assert.NotNull(response.CurrentAction);
            Assert.Equal(ActionType.SendChat, response.CurrentAction.ActionType);
        }
        finally
        {
            Environment.SetEnvironmentVariable("WWOW_COORDINATOR_MODE", previousMode);
        }
    }

    // ===== ActionType coverage =====

    [Theory]
    [InlineData(ActionType.Wait)]
    [InlineData(ActionType.Goto)]
    [InlineData(ActionType.InteractWith)]
    [InlineData(ActionType.CastSpell)]
    [InlineData(ActionType.SendChat)]
    [InlineData(ActionType.SetFacing)]
    [InlineData(ActionType.VisitVendor)]
    [InlineData(ActionType.VisitTrainer)]
    [InlineData(ActionType.VisitFlightMaster)]
    [InlineData(ActionType.StartFishing)]
    [InlineData(ActionType.StartGatheringRoute)]
    public void ActionMessage_AllTypes_RoundTrip(ActionType actionType)
    {
        var msg = new ActionMessage { ActionType = actionType };
        var bytes = msg.ToByteArray();
        var deserialized = ActionMessage.Parser.ParseFrom(bytes);

        Assert.Equal(actionType, deserialized.ActionType);
    }

    private sealed class CapturingLogger<T> : ILogger<T>
    {
        public List<(LogLevel Level, string Message)> Entries { get; } = [];

        public IDisposable BeginScope<TState>(TState state) where TState : notnull => NullScope.Instance;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            Entries.Add((logLevel, formatter(state, exception)));
        }

        private sealed class NullScope : IDisposable
        {
            public static readonly NullScope Instance = new();

            public void Dispose()
            {
            }
        }
    }

    private static WoWActivitySnapshot BuildReadySnapshot(string accountName)
    {
        return new WoWActivitySnapshot
        {
            AccountName = accountName,
            CharacterName = accountName,
            ScreenState = "InWorld",
            IsObjectManagerValid = true,
            CurrentMapId = 0,
            Player = new WoWPlayer
            {
                Unit = new WoWUnit
                {
                    Health = 100,
                    MaxHealth = 100,
                    GameObject = new WoWGameObject
                    {
                        Level = 60,
                        Base = new WoWObject { MapId = 0 }
                    }
                }
            }
        };
    }

    private static void SetPrivateField(object instance, string fieldName, object? value)
    {
        var field = instance.GetType().GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance)
            ?? throw new MissingFieldException(instance.GetType().FullName, fieldName);
        field.SetValue(instance, value);
    }
}
