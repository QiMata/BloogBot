using BotRunner.Interfaces;
using Serilog;
using System;

namespace BotRunner.Tasks.Progression;

/// <summary>
/// Farm loop for rare drops. Given a dungeon + target item:
/// 1. Travel to dungeon entrance (via TravelTask).
/// 2. Enter dungeon.
/// 3. Clear to boss (via DungeoneeringTask waypoints).
/// 4. Kill boss.
/// 5. Check loot for target item.
/// 6. If not found: exit dungeon, reset instance, repeat.
/// 7. If found: equip/bank item, mark goal complete.
/// Tracks attempt count for statistics.
/// </summary>
public class FarmBossTask : BotTask, IBotTask
{
    private enum FarmState { TravelToDungeon, EnterDungeon, ClearToBoss, CheckLoot, ResetAndRepeat, Complete }

    private FarmState _state = FarmState.TravelToDungeon;
    private readonly uint _dungeonMapId;
    private readonly int _targetItemId;
    private readonly string _targetItemName;
    private int _attemptCount;
    private const int MaxAttempts = 100;

    public FarmBossTask(IBotContext context, uint dungeonMapId, int targetItemId, string targetItemName)
        : base(context)
    {
        _dungeonMapId = dungeonMapId;
        _targetItemId = targetItemId;
        _targetItemName = targetItemName;
    }

    public void Update()
    {
        var player = ObjectManager.Player;
        if (player == null) return;

        switch (_state)
        {
            case FarmState.TravelToDungeon:
                // TODO: Push TravelTask to dungeon entrance
                Log.Information("[FarmBoss] Attempt #{Attempt}: Travel to dungeon map {MapId} for {Item}",
                    _attemptCount + 1, _dungeonMapId, _targetItemName);
                _state = FarmState.EnterDungeon;
                break;

            case FarmState.EnterDungeon:
                // Check if we're in the dungeon instance
                if (player.MapId == _dungeonMapId)
                {
                    _state = FarmState.ClearToBoss;
                    return;
                }
                // TODO: Navigate to portal, enter
                break;

            case FarmState.ClearToBoss:
                // TODO: Push DungeoneeringTask to clear to boss
                _state = FarmState.CheckLoot;
                break;

            case FarmState.CheckLoot:
                _attemptCount++;
                // Check if target item is in inventory
                bool hasItem = false;
                foreach (var item in ObjectManager.Items)
                {
                    if (item.ItemId == (uint)_targetItemId)
                    {
                        hasItem = true;
                        break;
                    }
                }

                if (hasItem)
                {
                    Log.Information("[FarmBoss] Got {Item} after {Attempts} attempts!",
                        _targetItemName, _attemptCount);
                    _state = FarmState.Complete;
                }
                else if (_attemptCount >= MaxAttempts)
                {
                    Log.Warning("[FarmBoss] Gave up after {Attempts} attempts for {Item}.",
                        _attemptCount, _targetItemName);
                    _state = FarmState.Complete;
                }
                else
                {
                    Log.Information("[FarmBoss] Attempt #{Attempt}: {Item} not found. Resetting.",
                        _attemptCount, _targetItemName);
                    _state = FarmState.ResetAndRepeat;
                }
                break;

            case FarmState.ResetAndRepeat:
                // TODO: Exit dungeon, reset instance via .instance reset
                _state = FarmState.TravelToDungeon;
                break;

            case FarmState.Complete:
                BotContext.BotTasks.Pop();
                break;
        }
    }

    public int AttemptCount => _attemptCount;
}
