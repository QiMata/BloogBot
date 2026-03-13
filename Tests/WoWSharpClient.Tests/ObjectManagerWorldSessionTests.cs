using System.Collections.Generic;
using GameData.Core.Enums;
using GameData.Core.Models;
using static GameData.Core.Enums.UpdateFields;
using WoWSharpClient.Models;
using WoWSharpClient.Tests.Handlers;
using WoWSharpClient.Tests.Util;

namespace WoWSharpClient.Tests;

[Collection("Sequential ObjectManager tests")]
public class ObjectManagerWorldSessionTests
{
    [Fact]
    public void ResetWorldSessionState_ClearsObjectsAndPreservesGuid()
    {
        var objectManager = WoWSharpObjectManager.Instance;
        const ulong playerGuid = 0x1234;
        const ulong unitGuid = 0x5678;

        objectManager.EnterWorld(playerGuid);
        var originalPlayer = objectManager.Player;

        objectManager.QueueUpdate(new WoWSharpObjectManager.ObjectStateUpdate(
            unitGuid,
            WoWSharpObjectManager.ObjectUpdateOperation.Add,
            WoWObjectType.Unit,
            null,
            new Dictionary<uint, object?>()));
        UpdateProcessingHelper.DrainPendingUpdates();

        Assert.NotEmpty(objectManager.Objects);

        objectManager.ResetWorldSessionState("test");

        Assert.False(objectManager.HasEnteredWorld);
        Assert.Empty(objectManager.Objects);
        Assert.Equal(playerGuid, objectManager.PlayerGuid.FullGuid);
        Assert.Equal(playerGuid, objectManager.Player.Guid);
        Assert.NotSame(originalPlayer, objectManager.Player);
    }

    [Fact]
    public void LocalPlayerUpdate_AppliesWithoutPriorAddObject()
    {
        var objectManager = WoWSharpObjectManager.Instance;
        const ulong playerGuid = 0x9;

        objectManager.EnterWorld(playerGuid);

        objectManager.QueueUpdate(new WoWSharpObjectManager.ObjectStateUpdate(
            playerGuid,
            WoWSharpObjectManager.ObjectUpdateOperation.Update,
            WoWObjectType.Player,
            new MovementInfoUpdate
            {
                Guid = playerGuid,
                X = 11f,
                Y = 22f,
                Z = 33f,
                Facing = 1.5f,
                MovementFlags = MovementFlags.MOVEFLAG_NONE,
            },
            new Dictionary<uint, object?>
            {
                [(uint)EUnitFields.UNIT_FIELD_HEALTH] = 75u,
                [(uint)EUnitFields.UNIT_FIELD_MAXHEALTH] = 100u,
            }));

        UpdateProcessingHelper.DrainPendingUpdates();

        Assert.Equal(75u, objectManager.Player.Health);
        Assert.Equal(100u, objectManager.Player.MaxHealth);
        Assert.Equal(11f, objectManager.Player.Position.X);
        Assert.Equal(22f, objectManager.Player.Position.Y);
        Assert.Equal(33f, objectManager.Player.Position.Z);
    }
}
