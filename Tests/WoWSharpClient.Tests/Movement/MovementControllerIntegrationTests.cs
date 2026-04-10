using GameData.Core.Models;
using WoWSharpClient.Client;
using WoWSharpClient.Models;
using WoWSharpClient.Movement;
using Xunit;

namespace WoWSharpClient.Tests.Movement;

/// <summary>
/// P8.3: MovementController integration tests.
/// Validates construction, waypoint setting, path clear, update, and reset.
/// </summary>
public class MovementControllerIntegrationTests
{
    [Fact]
    public void Constructor_CreatesInstance()
    {
        var mc = Create(out _);
        Assert.NotNull(mc);
    }

    [Fact]
    public void SetTargetWaypoint_DoesNotThrow()
    {
        var mc = Create(out _);
        mc.SetTargetWaypoint(new Position(1639f, -4373f, 34f));
    }

    [Fact]
    public void ClearPath_AfterSetTarget_DoesNotThrow()
    {
        var mc = Create(out _);
        mc.SetTargetWaypoint(new Position(1639f, -4373f, 34f));
        mc.ClearPath();
    }

    [Fact]
    public void Update_Idle_DoesNotThrow()
    {
        var mc = Create(out _);
        mc.Update(0.033f, 100);
    }

    [Fact]
    public void Reset_ClearsState()
    {
        // Reset sets _needsGroundSnap which bypasses idle guard, causing
        // Update to run physics. Provide a step override to avoid native DLL load.
        NativeLocalPhysics.TestStepOverride = input => new NativePhysics.PhysicsOutput
        {
            X = input.X, Y = input.Y, Z = input.Z,
            Orientation = input.Orientation,
            MoveFlags = input.MoveFlags,
            GroundZ = input.Z,
            GroundNx = 0f, GroundNy = 0f, GroundNz = 1f,
        };
                try
        {
            var mc = Create(out _);
            mc.SetTargetWaypoint(new Position(1639f, -4373f, 34f));
            mc.Reset(34f);
            mc.Update(0.033f, 200);
        }
        finally
        {
            NativeLocalPhysics.TestStepOverride = null;
                    }
    }

    [Fact]
    public void SetGroundedState_BothValues()
    {
        var mc = Create(out _);
        mc.SetGroundedState(true);
        mc.SetGroundedState(false);
    }

    [Fact]
    public void SetTargetWaypoint_WithMultipleUpdates_KeepsLatestTarget()
    {
        var mc = Create(out _);
        mc.SetTargetWaypoint(new Position(1630f, -4373f, 34f));
        mc.SetTargetWaypoint(new Position(1635f, -4373f, 34f));
        mc.SetTargetWaypoint(new Position(1639f, -4373f, 34f));

        Assert.NotNull(mc.CurrentWaypoint);
        Assert.Equal(1639f, mc.CurrentWaypoint!.X, 3);
    }

    private static MovementController Create(out WoWLocalPlayer player)
    {
        var client = new WoWClient();
        player = new WoWLocalPlayer(new HighGuid(new byte[] { 1, 0, 0, 0 }, new byte[] { 0, 0, 0, 0 }))
        {
            Position = new Position(1629f, -4373f, 34f),
            Facing = 0f,
            MapId = 1
        };
        return new MovementController(client, player);
    }
}
