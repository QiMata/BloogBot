using BotRunner.Interfaces;
using GameData.Core.Enums;
using Serilog;
using System;

namespace BotRunner.Tasks.Travel;

/// <summary>
/// Warlock Ritual of Summoning (spell 698).
/// Requires: 3 party members at location, Soul Shard reagent.
/// Steps:
/// 1. Warlock casts Ritual of Summoning.
/// 2. Two nearby party members click the portal.
/// 3. Target absent member accepts via CMSG_SUMMON_RESPONSE.
/// 4. Summoned member appears at the warlock's location.
/// </summary>
public class WarlockSummonTask : BotTask, IBotTask
{
    private enum SummonState { CheckPrereqs, CastRitual, WaitForHelpers, WaitForAccept, Complete }

    private SummonState _state = SummonState.CheckPrereqs;
    private readonly ulong _targetPlayerGuid;
    private long _castStartMs;
    private const uint RitualOfSummoningSpellId = 698;
    private const uint SoulShardItemId = 6265;
    private const int RitualTimeoutMs = 30_000;

    public WarlockSummonTask(IBotContext context, ulong targetPlayerGuid)
        : base(context)
    {
        _targetPlayerGuid = targetPlayerGuid;
    }

    public void Update()
    {
        var player = ObjectManager.Player;
        if (player == null) return;

        switch (_state)
        {
            case SummonState.CheckPrereqs:
                if (player.Class != Class.Warlock)
                {
                    Log.Warning("[WarlockSummon] Not a Warlock.");
                    BotContext.BotTasks.Pop();
                    return;
                }

                // Check for Soul Shard
                bool hasSoulShard = false;
                foreach (var item in ObjectManager.Items)
                {
                    if (item.ItemId == SoulShardItemId)
                    {
                        hasSoulShard = true;
                        break;
                    }
                }

                if (!hasSoulShard)
                {
                    Log.Warning("[WarlockSummon] No Soul Shard in inventory.");
                    BotContext.BotTasks.Pop();
                    return;
                }

                _state = SummonState.CastRitual;
                break;

            case SummonState.CastRitual:
                ObjectManager.StopMovement(ControlBits.Front | ControlBits.Back | ControlBits.StrafeLeft | ControlBits.StrafeRight);
                ObjectManager.CastSpell("Ritual of Summoning");
                _castStartMs = Environment.TickCount64;
                _state = SummonState.WaitForHelpers;
                Log.Information("[WarlockSummon] Casting Ritual of Summoning for target {Guid:X}.", _targetPlayerGuid);
                break;

            case SummonState.WaitForHelpers:
                // Wait for 2 helpers to click the portal
                if (Environment.TickCount64 - _castStartMs > RitualTimeoutMs)
                {
                    Log.Warning("[WarlockSummon] Ritual timed out waiting for helpers.");
                    BotContext.BotTasks.Pop();
                    return;
                }
                // TODO: Detect portal created event and helper clicks
                // For now, advance after 10s (assumes helpers will click)
                if (Environment.TickCount64 - _castStartMs > 10_000)
                {
                    _state = SummonState.WaitForAccept;
                }
                break;

            case SummonState.WaitForAccept:
                // Wait for target to accept the summon
                if (Environment.TickCount64 - _castStartMs > RitualTimeoutMs)
                {
                    Log.Warning("[WarlockSummon] Summon timed out waiting for accept.");
                    _state = SummonState.Complete;
                    return;
                }
                // TODO: Detect SMSG_MEETINGSTONE_COMPLETE or target position change
                break;

            case SummonState.Complete:
                BotContext.BotTasks.Pop();
                break;
        }
    }
}
