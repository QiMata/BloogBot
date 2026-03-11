using BotRunner.Combat;
using BotRunner.Interfaces;
using GameData.Core.Interfaces;
using Serilog;
using System;
using System.Linq;

namespace BotRunner.Tasks;

/// <summary>
/// Resolves the current fishing rank, casts it, then waits for the fishing cycle to finish.
/// The live fishing test remains responsible for asserting catch metrics; this task owns the
/// cast/channel/bobber side of the behavior instead of raw action dispatch.
/// </summary>
public class FishingTask(IBotContext botContext) : BotTask(botContext), IBotTask
{
    private enum FishingState
    {
        ResolveAndCast,
        AwaitCastConfirmation,
        AwaitCatchResolution,
    }

    private const int CastConfirmationTimeoutMs = 5000;
    private const int CatchResolutionTimeoutMs = 35000;

    private FishingState _state = FishingState.ResolveAndCast;
    private DateTime _stateEnteredAt = DateTime.UtcNow;
    private uint _fishingSpellId;
    private bool _sawFishingState;

    public void Update()
    {
        var player = ObjectManager.Player;
        if (player?.Position == null)
        {
            PopTask("no_player");
            return;
        }

        if (player.IsInCombat)
        {
            ObjectManager.StopAllMovement();
            PopTask("combat");
            return;
        }

        switch (_state)
        {
            case FishingState.ResolveAndCast:
                ResolveAndCast(player);
                return;
            case FishingState.AwaitCastConfirmation:
                AwaitCastConfirmation(player);
                return;
            case FishingState.AwaitCatchResolution:
                AwaitCatchResolution(player);
                return;
        }
    }

    private void ResolveAndCast(IWoWLocalPlayer player)
    {
        _fishingSpellId = FishingData.ResolveCastableFishingSpellId(ObjectManager.KnownSpellIds, GetFishingSkill(player));
        if (_fishingSpellId == 0)
        {
            Log.Warning("[FISH] No castable fishing spell found in known spells or skill data.");
            PopTask("no_fishing_spell");
            return;
        }

        if (!ObjectManager.CanCastSpell((int)_fishingSpellId, 0))
        {
            Log.Warning("[FISH] Cannot cast fishing spell {SpellId}.", _fishingSpellId);
            PopTask("cannot_cast");
            return;
        }

        ObjectManager.ForceStopImmediate();
        ObjectManager.CastSpell((int)_fishingSpellId);
        Log.Information("[FISH] Cast initiated with spell {SpellId}.", _fishingSpellId);
        SetState(FishingState.AwaitCastConfirmation);
    }

    private void AwaitCastConfirmation(IWoWLocalPlayer player)
    {
        if (HasFishingState(player))
        {
            _sawFishingState = true;
            Log.Information("[FISH] Fishing cast confirmed. channel={ChannelingId} bobber={HasBobber}",
                player.ChannelingId, FindActiveBobber() != null);
            SetState(FishingState.AwaitCatchResolution);
            return;
        }

        if (ElapsedMs >= CastConfirmationTimeoutMs)
        {
            Log.Warning("[FISH] No channel or bobber detected within {TimeoutMs}ms for spell {SpellId}.",
                CastConfirmationTimeoutMs, _fishingSpellId);
            PopTask("no_channel_or_bobber");
        }
    }

    private void AwaitCatchResolution(IWoWLocalPlayer player)
    {
        if (HasFishingState(player))
        {
            _sawFishingState = true;
            if (ElapsedMs >= CatchResolutionTimeoutMs)
            {
                Log.Warning("[FISH] Fishing cycle timed out after {TimeoutMs}ms for spell {SpellId}.",
                    CatchResolutionTimeoutMs, _fishingSpellId);
                PopTask("fishing_timeout");
            }
            return;
        }

        if (_sawFishingState)
        {
            Log.Information("[FISH] Fishing cycle completed for spell {SpellId}.", _fishingSpellId);
            PopTask("fishing_cycle_complete");
            return;
        }

        if (ElapsedMs >= CatchResolutionTimeoutMs)
        {
            Log.Warning("[FISH] Fishing cycle ended without confirmed channel/bobber state.");
            PopTask("no_confirmed_fishing_state");
        }
    }

    private bool HasFishingState(IWoWLocalPlayer player)
        => player.ChannelingId == _fishingSpellId
            || player.IsChanneling
            || FindActiveBobber() != null;

    private IWoWGameObject? FindActiveBobber()
    {
        var playerGuid = ObjectManager.PlayerGuid.FullGuid;
        return ObjectManager.GameObjects.FirstOrDefault(go =>
            (go.DisplayId == FishingData.BobberDisplayId || go.TypeId == 17)
            && (go.CreatedBy.FullGuid == 0UL || go.CreatedBy.FullGuid == playerGuid));
    }

    private void SetState(FishingState state)
    {
        _state = state;
        _stateEnteredAt = DateTime.UtcNow;
    }

    private int ElapsedMs => (int)(DateTime.UtcNow - _stateEnteredAt).TotalMilliseconds;

    private static int GetFishingSkill(IWoWLocalPlayer player)
    {
        foreach (var skill in player.SkillInfo ?? Array.Empty<GameData.Core.Models.SkillInfo>())
        {
            var skillId = skill.SkillInt1 & 0xFFFF;
            if (skillId == FishingData.FishingSkillId)
                return (int)(skill.SkillInt2 & 0xFFFF);
        }

        return 0;
    }
}
