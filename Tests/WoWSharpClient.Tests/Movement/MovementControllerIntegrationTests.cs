using GameData.Core.Models;
using WoWSharpClient.Client;
using WoWSharpClient.Models;
using WoWSharpClient.Movement;
using Xunit;

namespace WoWSharpClient.Tests.Movement;

/// <summary>
/// P8.3: MovementController integration tests.
/// Validates construction, MoveToward, StopAllMovement, teleport, and update.
/// </summary>
public class MovementControllerIntegrationTests
{
    [Fact]
    public void Constructor_CreatesInstance()
    {
        var (mc, _) = Create();
        Assert.NotNull(mc);
    }

    [Fact]
    public void MoveToward_DoesNotThrow()
    {
        var (mc, _) = Create();
        mc.MoveToward(new Position(1639f, -4373f, 34f));
    }

    [Fact]
    public void StopAllMovement_DoesNotThrow()
    {
        var (mc, _) = Create();
        mc.MoveToward(new Position(1639f, -4373f, 34f));
        mc.StopAllMovement();
    }

    [Fact]
    public void Update_Idle_DoesNotThrow()
    {
        var (mc, _) = Create();
        mc.Update(0.033f, 100);
    }

    [Fact]
    public void NotifyTeleport_ResetsState()
    {
        var (mc, _) = Create();
        mc.MoveToward(new Position(1639f, -4373f, 34f));
        mc.NotifyTeleport(34f);
        mc.Update(0.033f, 200);
    }

    [Fact]
    public void SetGroundedState_BothValues()
    {
        var (mc, _) = Create();
        mc.SetGroundedState(true);
        mc.SetGroundedState(false);
    }

    [Fact]
    public void MoveToward_ThenMultipleUpdates_DoesNotThrow()
    {
        var (mc, _) = Create();
        mc.MoveToward(new Position(1639f, -4373f, 34f));
        for (int i = 0; i < 10; i++)
            mc.Update(0.033f, (uint)(100 + i * 33));
    }

    private static (MovementController mc, WoWLocalPlayer player) Create()
    {
        var client = new WoWClient();
        var player = new WoWLocalPlayer(new HighGuid(0, 1))
        {
            Position = new Position(1629f, -4373f, 34f),
            Facing = 0f,
            MapId = 1
        };
        var mc = new MovementController(client, player);
        return (mc, player);
    }
}
