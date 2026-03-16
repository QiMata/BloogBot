using BotRunner.Combat;
using BotRunner.Interfaces;
using GameData.Core.Enums;
using GameData.Core.Frames;
using GameData.Core.Interfaces;
using GameData.Core.Models;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;

namespace BotRunner.Tasks;

/// <summary>
/// Owns the full fishing loop: equip a pole, approach a visible fishing pool,
/// cast, wait for the bobber/loot event, and loot the catch before completing.
/// </summary>
public class FishingTask : BotTask, IBotTask
{
    public FishingTask(IBotContext botContext, IReadOnlyList<Position>? searchWaypoints = null)
        : base(botContext)
    {
        _searchWaypoints = searchWaypoints ?? [];
    }

    private enum FishingState
    {
        EnsurePoleEquipped,
        AwaitPoleEquip,
        EnsureLureApplied,
        AwaitLureApply,
        AcquireFishingPool,
        SearchForPool,
        MoveToFishingPool,
        ResolveAndCast,
        AwaitCastConfirmation,
        AwaitCatchResolution,
        AwaitLootWindow,
        LootCatch,
        AwaitLootCompletion,
    }

    internal const float DesiredPoolDistance = 18f;
    internal const float PoolDistanceTolerance = 4f;
    internal const float MinCastingDistance = DesiredPoolDistance - PoolDistanceTolerance;
    internal const float MaxCastingDistance = DesiredPoolDistance + PoolDistanceTolerance;
    private const float CastTargetInsetFromPool = 4f;
    private const int EquipTimeoutMs = 4000;
    private const int LureApplyTimeoutMs = 6000;
    private const int PoolAcquireTimeoutMs = 15000;
    private const int SearchWalkTimeoutMs = 180_000;
    private const float SearchWaypointArrivalRadius = 12f;
    private const int CastStabilizeDelayMs = 250;
    private const int CastStabilizeTimeoutMs = 1500;
    private const int CastConfirmationTimeoutMs = 5000;
    private const int CatchResolutionTimeoutMs = 28000;
    private const int LootWindowTimeoutMs = 5000;
    private const int LootCompletionTimeoutMs = 3000;

    private FishingState _state = FishingState.EnsurePoleEquipped;
    private DateTime _stateEnteredAt = DateTime.UtcNow;
    private uint _fishingSpellId;
    private int _castsAttempted;
    private int _equipAttempts;
    private int _lureAttempts;
    private ulong _activePoolGuid;
    private uint _activeLureItemId;
    private int _activeLureStartingCount;
    private bool _sawFishingState;
    private bool _sawBobber;
    private bool _sawLootWindow;
    private bool _sawLootItem;
    private readonly HashSet<uint> _lootItemIds = [];
    private readonly Dictionary<uint, int> _startingBagItemCounts = [];
    private bool _startingBagSnapshotCaptured;
    private float _lastApproachDiagnosticDistance = float.MaxValue;
    private readonly IReadOnlyList<Position> _searchWaypoints;
    private int _searchWaypointIndex;
    private DateTime _searchStartedAt;

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

        EnsureStartingBagSnapshot();

        switch (_state)
        {
            case FishingState.EnsurePoleEquipped:
                EnsurePoleEquipped();
                return;
            case FishingState.AwaitPoleEquip:
                AwaitPoleEquip();
                return;
            case FishingState.EnsureLureApplied:
                EnsureLureApplied(player);
                return;
            case FishingState.AwaitLureApply:
                AwaitLureApply(player);
                return;
            case FishingState.AcquireFishingPool:
                AcquireFishingPool(player);
                return;
            case FishingState.SearchForPool:
                SearchForPool(player);
                return;
            case FishingState.MoveToFishingPool:
                MoveToFishingPool(player);
                return;
            case FishingState.ResolveAndCast:
                ResolveAndCast(player);
                return;
            case FishingState.AwaitCastConfirmation:
                AwaitCastConfirmation(player);
                return;
            case FishingState.AwaitCatchResolution:
                AwaitCatchResolution(player);
                return;
            case FishingState.AwaitLootWindow:
                AwaitLootWindow();
                return;
            case FishingState.LootCatch:
                LootCatch();
                return;
            case FishingState.AwaitLootCompletion:
                AwaitLootCompletion();
                return;
        }
    }

    private void EnsurePoleEquipped()
    {
        if (FishingData.HasFishingPoleEquipped(ObjectManager))
        {
            SetState(FishingState.EnsureLureApplied);
            return;
        }

        var poleLocation = FishingData.FindFishingPoleInBags(ObjectManager);
        if (poleLocation == null)
        {
            Log.Warning("[FISH] No fishing pole found in bags.");
            PopTask("no_fishing_pole");
            return;
        }

        _equipAttempts++;
        ObjectManager.EquipItem(poleLocation.Value.bag, poleLocation.Value.slot);
        Log.Information("[FISH] Equipping fishing pole from bag={Bag} slot={Slot} (attempt {Attempt}).",
            poleLocation.Value.bag, poleLocation.Value.slot, _equipAttempts);
        BotContext.AddDiagnosticMessage(
            $"[TASK] FishingTask equipping_pole bag={poleLocation.Value.bag} slot={poleLocation.Value.slot} attempt={_equipAttempts}");
        SetState(FishingState.AwaitPoleEquip);
    }

    private void AwaitPoleEquip()
    {
        if (FishingData.HasFishingPoleEquipped(ObjectManager))
        {
            Log.Information("[FISH] Fishing pole equipped.");
            BotContext.AddDiagnosticMessage("[TASK] FishingTask pole_equipped");
            SetState(FishingState.EnsureLureApplied);
            return;
        }

        if (ElapsedMs < EquipTimeoutMs)
            return;

        if (_equipAttempts < 2)
        {
            SetState(FishingState.EnsurePoleEquipped);
            return;
        }

        Log.Warning("[FISH] Fishing pole equip timed out.");
        PopTask("pole_equip_timeout");
    }

    private void EnsureLureApplied(IWoWLocalPlayer player)
    {
        if (player.MainhandIsEnchanted)
        {
            BotContext.AddDiagnosticMessage("[TASK] FishingTask lure_already_active");
            SetState(FishingState.AcquireFishingPool);
            return;
        }

        var lureLocation = FishingData.FindUsableLureInBags(ObjectManager);
        if (lureLocation == null)
        {
            BotContext.AddDiagnosticMessage("[TASK] FishingTask no_lure_available");
            SetState(FishingState.AcquireFishingPool);
            return;
        }

        _lureAttempts++;
        _activeLureItemId = lureLocation.Value.itemId;
        _activeLureStartingCount = CountContainedItem(_activeLureItemId);
        var equippedPoleGuid = GetEquippedPoleGuid();
        ObjectManager.UseItem(lureLocation.Value.bag, lureLocation.Value.slot, equippedPoleGuid);
        Log.Information("[FISH] Applying lure item={ItemId} from bag={Bag} slot={Slot} (attempt {Attempt}).",
            _activeLureItemId,
            lureLocation.Value.bag,
            lureLocation.Value.slot,
            _lureAttempts);
        BotContext.AddDiagnosticMessage(
            $"[TASK] FishingTask lure_use_started item={_activeLureItemId} bag={lureLocation.Value.bag} slot={lureLocation.Value.slot} target=0x{equippedPoleGuid:X} attempt={_lureAttempts}");
        SetState(FishingState.AwaitLureApply);
    }

    private void AwaitLureApply(IWoWLocalPlayer player)
    {
        var lureCountDropped = _activeLureItemId != 0 && CountContainedItem(_activeLureItemId) < _activeLureStartingCount;
        if (player.MainhandIsEnchanted || lureCountDropped)
        {
            Log.Information("[FISH] Lure application confirmed. item={ItemId} countDropped={CountDropped} mainhandEnchanted={MainhandEnchanted}",
                _activeLureItemId,
                lureCountDropped,
                player.MainhandIsEnchanted);
            BotContext.AddDiagnosticMessage(
                $"[TASK] FishingTask lure_applied item={_activeLureItemId} countDropped={lureCountDropped} mainhandEnchanted={player.MainhandIsEnchanted}");
            SetState(FishingState.AcquireFishingPool);
            return;
        }

        if (ElapsedMs < LureApplyTimeoutMs)
            return;

        if (_lureAttempts < 2)
        {
            SetState(FishingState.EnsureLureApplied);
            return;
        }

        Log.Warning("[FISH] Fishing lure application timed out for item {ItemId}.", _activeLureItemId);
        PopTask("lure_apply_timeout");
    }

    private void AcquireFishingPool(IWoWLocalPlayer player)
    {
        var pool = FindTrackedOrNearestPool(player.Position);
        if (pool == null)
        {
            if (ElapsedMs >= PoolAcquireTimeoutMs)
            {
                if (_searchWaypoints.Count > 0 && _searchWaypointIndex < _searchWaypoints.Count)
                {
                    Log.Information("[FISH] No pool visible after {Timeout}ms; starting search walk with {Count} waypoints.",
                        PoolAcquireTimeoutMs, _searchWaypoints.Count);
                    BotContext.AddDiagnosticMessage(
                        $"[TASK] FishingTask search_walk_start waypoints={_searchWaypoints.Count}");
                    _searchStartedAt = DateTime.UtcNow;
                    SetState(FishingState.SearchForPool);
                }
                else
                {
                    Log.Warning("[FISH] No visible fishing pool within {Range}y.", Config.FishingPoolDetectRange);
                    PopTask("no_fishing_pool");
                }
            }

            return;
        }

        _activePoolGuid = pool.Guid;
        var poolDistance = player.Position.DistanceTo(pool.Position!);
        BotContext.AddDiagnosticMessage(
            $"[TASK] FishingTask pool_acquired guid=0x{pool.Guid:X} entry={pool.Entry} distance={poolDistance:F1}");
        if (IsInCastingWindow(player.Position, pool.Position!))
        {
            ObjectManager.ForceStopImmediate();
            ObjectManager.Face(pool.Position!);
            SetState(FishingState.ResolveAndCast);
            return;
        }

        ClearNavigation();
        SetState(FishingState.MoveToFishingPool);
    }

    private void SearchForPool(IWoWLocalPlayer player)
    {
        var pool = FindTrackedOrNearestPool(player.Position);
        if (pool != null)
        {
            _activePoolGuid = pool.Guid;
            var poolDistance = player.Position.DistanceTo(pool.Position!);
            BotContext.AddDiagnosticMessage(
                $"[TASK] FishingTask search_walk_found_pool guid=0x{pool.Guid:X} entry={pool.Entry} distance={poolDistance:F1} waypoint={_searchWaypointIndex}/{_searchWaypoints.Count}");
            Log.Information("[FISH] Pool found during search walk at {Distance:F1}y.", poolDistance);
            ClearNavigation();
            if (IsInCastingWindow(player.Position, pool.Position!))
            {
                ObjectManager.ForceStopImmediate();
                ObjectManager.Face(pool.Position!);
                SetState(FishingState.ResolveAndCast);
            }
            else
            {
                SetState(FishingState.MoveToFishingPool);
            }
            return;
        }

        var searchElapsed = (int)(DateTime.UtcNow - _searchStartedAt).TotalMilliseconds;
        if (searchElapsed >= SearchWalkTimeoutMs)
        {
            Log.Warning("[FISH] Search walk timed out after {Elapsed}ms.", searchElapsed);
            BotContext.AddDiagnosticMessage(
                $"[TASK] FishingTask search_walk_timeout elapsed={searchElapsed}ms waypoint={_searchWaypointIndex}/{_searchWaypoints.Count}");
            PopTask("search_timeout");
            return;
        }

        if (_searchWaypointIndex >= _searchWaypoints.Count)
        {
            Log.Warning("[FISH] Search walk exhausted all {Count} waypoints without finding a pool.", _searchWaypoints.Count);
            BotContext.AddDiagnosticMessage(
                $"[TASK] FishingTask search_walk_exhausted waypoints={_searchWaypoints.Count} elapsed={searchElapsed}ms");
            PopTask("search_exhausted");
            return;
        }

        var waypoint = _searchWaypoints[_searchWaypointIndex];
        var waypointDistance = player.Position.DistanceTo(waypoint);

        if (waypointDistance <= SearchWaypointArrivalRadius)
        {
            _searchWaypointIndex++;
            BotContext.AddDiagnosticMessage(
                $"[TASK] FishingTask search_walk waypoint={_searchWaypointIndex}/{_searchWaypoints.Count} distance={waypointDistance:F1}");
            return;
        }

        if (!TryNavigateToward(waypoint))
            ObjectManager.MoveToward(waypoint);
    }

    private void MoveToFishingPool(IWoWLocalPlayer player)
    {
        var pool = FindTrackedOrNearestPool(player.Position);
        if (pool?.Position == null)
        {
            if (ElapsedMs >= PoolAcquireTimeoutMs)
            {
                Log.Warning("[FISH] Lost fishing pool while approaching.");
                PopTask("lost_fishing_pool");
            }

            return;
        }

        var poolDistance = player.Position.DistanceTo(pool.Position);
        if (IsInCastingWindow(player.Position, pool.Position)
            && CanCastFromPosition(player.MapId, player.Position, pool.Position))
        {
            ObjectManager.ForceStopImmediate();
            ObjectManager.Face(pool.Position);
            Log.Information("[FISH] Reached fishing pool range at {Distance:F1}y (pool=0x{Guid:X}).",
                poolDistance, pool.Guid);
            BotContext.AddDiagnosticMessage(
                $"[TASK] FishingTask in_cast_range guid=0x{pool.Guid:X} distance={poolDistance:F1}");
            SetState(FishingState.ResolveAndCast);
            return;
        }

        var approachPosition = ResolveFishingApproachPosition(player, pool.Position);
        if (_lastApproachDiagnosticDistance == float.MaxValue || poolDistance <= _lastApproachDiagnosticDistance - 2f)
        {
            _lastApproachDiagnosticDistance = poolDistance;
            BotContext.AddDiagnosticMessage(
                $"[TASK] FishingTask approaching_pool guid=0x{pool.Guid:X} distance={poolDistance:F1}");
        }
        else if (IsInCastingWindow(player.Position, pool.Position))
        {
            EmitLosBlockedDiagnostic(player, pool.Position, "move");
        }

        // Prefer the task's normal pathfinding waypoint selection, but fall back to a direct
        // movement nudge if the shoreline approach does not yield a usable short waypoint.
        // Repeated stalls here are pathfinding/terrain evidence, not a fishing-contract failure.
        if (!TryNavigateToward(approachPosition))
            ObjectManager.MoveToward(approachPosition);
    }

    private void ResolveAndCast(IWoWLocalPlayer player)
    {
        var pool = FindTrackedOrNearestPool(player.Position);
        if (pool?.Position == null)
        {
            SetState(FishingState.AcquireFishingPool);
            return;
        }

        if (_castsAttempted >= Config.MaxFishingCasts)
        {
            Log.Warning("[FISH] Max cast attempts reached without a catch.");
            PopTask("max_casts_reached");
            return;
        }

        if (player.IsSwimming)
        {
            Log.Warning("[FISH] Player entered water before cast; aborting fishing attempt.");
            BotContext.AddDiagnosticMessage("[TASK] FishingTask retry reason=player_swimming");
            PopTask("player_swimming");
            return;
        }

        ObjectManager.Face(pool.Position);
        ObjectManager.ForceStopImmediate();

        if (!CanCastFromPosition(player.MapId, player.Position, pool.Position))
        {
            EmitLosBlockedDiagnostic(player, pool.Position, "cast");
            SetState(FishingState.MoveToFishingPool);
            return;
        }

        if (ElapsedMs < CastStabilizeDelayMs || player.IsMoving)
        {
            if (ElapsedMs >= CastStabilizeTimeoutMs)
            {
                RetryFromPool("still_moving_before_cast");
            }

            return;
        }

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

        _castsAttempted++;
        _sawFishingState = false;
        _sawBobber = false;
        _sawLootWindow = false;
        _sawLootItem = false;
        var castTarget = FishingData.GetPoolCastTarget(player.Position, pool.Position, CastTargetInsetFromPool);
        ObjectManager.CastSpellAtLocation((int)_fishingSpellId, castTarget.X, castTarget.Y, castTarget.Z);
        Log.Information(
            "[FISH] Cast {Attempt}/{MaxAttempts} started at pool 0x{Guid:X} with spell {SpellId} targeting ({X:F1}, {Y:F1}, {Z:F1}) distance={Distance:F1}.",
            _castsAttempted,
            Config.MaxFishingCasts,
            pool.Guid,
            _fishingSpellId,
            castTarget.X,
            castTarget.Y,
            castTarget.Z,
            player.Position.DistanceTo(pool.Position));
        BotContext.AddDiagnosticMessage(
            $"[TASK] FishingTask cast_started attempt={_castsAttempted} pool=0x{pool.Guid:X} spell={_fishingSpellId} target=({castTarget.X:F1},{castTarget.Y:F1},{castTarget.Z:F1}) distance={player.Position.DistanceTo(pool.Position):F1}");
        SetState(FishingState.AwaitCastConfirmation);
    }

    private void AwaitCastConfirmation(IWoWLocalPlayer player)
    {
        if (FindActiveBobber() != null)
            _sawBobber = true;

        if (HasFishingState(player))
        {
            _sawFishingState = true;
            Log.Information("[FISH] Fishing cast confirmed. channel={ChannelingId} bobber={HasBobber}",
                player.ChannelingId, _sawBobber);
            SetState(FishingState.AwaitCatchResolution);
            return;
        }

        if (ElapsedMs >= CastConfirmationTimeoutMs)
        {
            RetryFromPool("no_channel_or_bobber");
        }
    }

    private void AwaitCatchResolution(IWoWLocalPlayer player)
    {
        if (ObjectManager.LootFrame?.IsOpen == true)
        {
            _sawLootWindow = true;
            SetState(FishingState.LootCatch);
            return;
        }

        var bobber = FindActiveBobber();
        if (bobber != null)
            _sawBobber = true;

        if (HasFishingState(player))
        {
            _sawFishingState = true;
            if (ElapsedMs >= CatchResolutionTimeoutMs)
                RetryFromPool("fishing_timeout");
            return;
        }

        if (_sawFishingState || _sawBobber)
        {
            SetState(FishingState.AwaitLootWindow);
            return;
        }

        if (ElapsedMs >= CatchResolutionTimeoutMs)
            RetryFromPool("no_confirmed_fishing_state");
    }

    private void AwaitLootWindow()
    {
        if (ObjectManager.LootFrame?.IsOpen == true)
        {
            _sawLootWindow = true;
            SetState(FishingState.LootCatch);
            return;
        }

        var bobber = FindActiveBobber();
        if (bobber != null)
            _sawBobber = true;

        if (ElapsedMs >= LootWindowTimeoutMs)
            RetryFromPool("loot_window_timeout");
    }

    private void LootCatch()
    {
        var lootFrame = ObjectManager.LootFrame;
        if (lootFrame == null)
        {
            TryRecordLootedBagDelta();
            if (_sawLootWindow && _sawLootItem)
            {
                PopWithSuccess();
                return;
            }

            if (ElapsedMs >= LootCompletionTimeoutMs)
                RetryFromPool("loot_frame_unavailable");

            return;
        }

        if (!lootFrame.IsOpen)
        {
            SetState(FishingState.AwaitLootCompletion);
            return;
        }

        var lootItems = lootFrame.LootItems?.Where(item => item != null && item.GotLoot).ToArray() ?? [];
        var lootItemIds = lootItems
            .Where(item => item.ItemId > 0)
            .Select(item => (uint)item.ItemId)
            .ToArray();

        if (lootItemIds.Length == 0 && lootFrame.Coins <= 0 && lootFrame.LootCount <= 0)
        {
            if (ElapsedMs >= LootCompletionTimeoutMs)
            {
                lootFrame.Close();
                RetryFromPool("empty_loot_window");
            }

            return;
        }

        _sawLootWindow = true;
        _sawLootItem |= lootItemIds.Length > 0;
        foreach (var lootItemId in lootItemIds)
            _lootItemIds.Add(lootItemId);

        BotContext.AddDiagnosticMessage(
            $"[TASK] FishingTask loot_window_open count={lootFrame.LootCount} coins={lootFrame.Coins} items=[{string.Join(", ", lootItemIds)}]");
        Log.Information("[FISH] Loot window open. guid=0x{Guid:X} count={Count} items=[{Items}] coins={Coins}",
            lootFrame.LootGuid,
            lootFrame.LootCount,
            string.Join(", ", lootItemIds),
            lootFrame.Coins);

        lootFrame.LootAll();
        lootFrame.Close();
        SetState(FishingState.AwaitLootCompletion);
    }

    private void AwaitLootCompletion()
    {
        if (ObjectManager.LootFrame?.IsOpen == true)
        {
            ObjectManager.LootFrame.LootAll();
            ObjectManager.LootFrame.Close();
            return;
        }

        TryRecordLootedBagDelta();

        if (_sawLootWindow && _sawLootItem)
        {
            PopWithSuccess();
            return;
        }

        if (ElapsedMs >= LootCompletionTimeoutMs)
            RetryFromPool("loot_completion_timeout");
    }

    private IWoWGameObject? FindTrackedOrNearestPool(Position playerPosition)
    {
        var tracked = _activePoolGuid != 0
            ? ObjectManager.GameObjects.FirstOrDefault(gameObject => gameObject.Guid == _activePoolGuid && FishingData.IsFishingPool(gameObject))
            : null;

        if (tracked?.Position != null)
            return tracked;

        return FishingData.FindNearestFishingPool(ObjectManager, playerPosition, Config.FishingPoolDetectRange);
    }

    private bool HasFishingState(IWoWLocalPlayer player)
        => player.ChannelingId == _fishingSpellId
            || player.IsChanneling
            || FindActiveBobber() != null;

    private IWoWGameObject? FindActiveBobber()
    {
        var playerGuid = ObjectManager.PlayerGuid.FullGuid;
        return ObjectManager.GameObjects.FirstOrDefault(gameObject =>
            gameObject.Position != null
            && (gameObject.DisplayId == FishingData.BobberDisplayId || gameObject.TypeId == 17)
            && (gameObject.CreatedBy.FullGuid == 0UL || gameObject.CreatedBy.FullGuid == playerGuid));
    }

    private void RetryFromPool(string reason)
    {
        Log.Warning("[FISH] {Reason}; resetting fishing state for another cast.", reason);
        BotContext.AddDiagnosticMessage($"[TASK] FishingTask retry reason={reason}");
        ClearNavigation();
        SetState(FishingState.AcquireFishingPool);
    }

    private Position ResolveFishingApproachPosition(IWoWLocalPlayer player, Position poolPosition)
    {
        foreach (var candidate in FishingData.GetPoolApproachCandidates(player.Position, poolPosition, DesiredPoolDistance))
        {
            if (CanCastFromPosition(player.MapId, candidate, poolPosition))
                return candidate;
        }

        return FishingData.GetPoolApproachPosition(player.Position, poolPosition, DesiredPoolDistance);
    }

    private bool CanCastFromPosition(uint mapId, Position fromPosition, Position poolPosition)
    {
        var castTarget = FishingData.GetPoolCastTarget(fromPosition, poolPosition, CastTargetInsetFromPool);

        // Fishing pools (especially on FG) can report Z=0 from memory reads even when the pool
        // is at water surface. If the cast target Z is far below the player, use the player's Z
        // instead — fishing casts go roughly horizontal to the water surface, not downward into
        // the terrain. Without this, the LOS ray pierces the dock and returns blocked forever.
        if (fromPosition.Z - castTarget.Z > 3f)
            castTarget = new Position(castTarget.X, castTarget.Y, fromPosition.Z);

        return TryHasLineOfSight(mapId, fromPosition, castTarget);
    }

    private bool TryHasLineOfSight(uint mapId, Position fromPosition, Position toPosition)
    {
        try
        {
            return Container.PathfindingClient?.IsInLineOfSight(mapId, fromPosition, toPosition) ?? true;
        }
        catch (Exception ex)
        {
            Log.Debug(ex, "[FISH] LOS probe failed; treating pathfinding LOS as unavailable.");
            return true;
        }
    }

    private void EmitLosBlockedDiagnostic(IWoWLocalPlayer player, Position poolPosition, string phase)
    {
        var key = $"fishing-los-{phase}-{ObjectManager.PlayerGuid.FullGuid}";
        if (!Wait.For(key, 1000, resetOnSuccess: true))
            return;

        var castTarget = FishingData.GetPoolCastTarget(player.Position, poolPosition, CastTargetInsetFromPool);
        BotContext.AddDiagnosticMessage(
            $"[TASK] FishingTask los_blocked phase={phase} castTarget=({castTarget.X:F1},{castTarget.Y:F1},{castTarget.Z:F1}) poolZ={poolPosition.Z:F1} playerZ={player.Position.Z:F1}");
        Log.Information("[FISH] LOS blocked for fishing {Phase}. castTarget=({X:F1}, {Y:F1}, {Z:F1}) poolZ={PoolZ:F1} playerZ={PlayerZ:F1}",
            phase, castTarget.X, castTarget.Y, castTarget.Z, poolPosition.Z, player.Position.Z);
    }

    private void PopWithSuccess()
    {
        var lootItems = _lootItemIds.Count > 0 ? string.Join(", ", _lootItemIds.OrderBy(id => id)) : "none";
        BotContext.AddDiagnosticMessage(
            $"[TASK] FishingTask fishing_loot_success lootWindowSeen={_sawLootWindow} lootItemSeen={_sawLootItem} bobberSeen={_sawBobber} lootItems=[{lootItems}]");
        Log.Information("[FISH] Fishing catch completed. lootWindowSeen={LootWindowSeen} lootItemSeen={LootItemSeen} bobberSeen={BobberSeen} lootItems=[{LootItems}]",
            _sawLootWindow, _sawLootItem, _sawBobber, lootItems);
        PopTask("fishing_loot_success");
    }

    private static bool IsInCastingWindow(Position playerPosition, Position poolPosition)
    {
        var distance = playerPosition.DistanceTo(poolPosition);
        return distance >= MinCastingDistance
            && distance <= MaxCastingDistance;
    }

    private void SetState(FishingState state)
    {
        if ((_state == FishingState.MoveToFishingPool || _state == FishingState.SearchForPool)
            && state != FishingState.MoveToFishingPool && state != FishingState.SearchForPool)
            ClearNavigation();

        if (state == FishingState.MoveToFishingPool)
            _lastApproachDiagnosticDistance = float.MaxValue;

        _state = state;
        _stateEnteredAt = DateTime.UtcNow;
    }

    private int ElapsedMs => (int)(DateTime.UtcNow - _stateEnteredAt).TotalMilliseconds;

    private void EnsureStartingBagSnapshot()
    {
        if (_startingBagSnapshotCaptured)
            return;

        _startingBagSnapshotCaptured = true;
        foreach (var (itemId, count) in SnapshotCatchBagItemCounts())
            _startingBagItemCounts[itemId] = count;
    }

    private bool TryRecordLootedBagDelta()
    {
        var deltaItemIds = new List<uint>();
        foreach (var (itemId, count) in SnapshotCatchBagItemCounts())
        {
            _startingBagItemCounts.TryGetValue(itemId, out var startingCount);
            if (count > startingCount)
            {
                deltaItemIds.Add(itemId);
                _lootItemIds.Add(itemId);
            }
        }

        if (deltaItemIds.Count == 0)
            return false;

        _sawLootItem = true;
        BotContext.AddDiagnosticMessage(
            $"[TASK] FishingTask loot_bag_delta items=[{string.Join(", ", deltaItemIds.OrderBy(id => id))}]");
        return true;
    }

    private int CountContainedItem(uint itemId)
        => ObjectManager.GetContainedItems()
            .Count(item => item != null && item.ItemId == itemId);

    private ulong GetEquippedPoleGuid()
    {
        foreach (var slot in new[] { EquipSlot.MainHand, EquipSlot.OffHand, EquipSlot.Ranged })
        {
            var equippedItem = ObjectManager.GetEquippedItem(slot);
            var equippedGuid = ObjectManager.GetEquippedItemGuid(slot);
            if (equippedItem != null && FishingData.IsFishingPole(equippedItem.ItemId) && equippedGuid != 0)
                return equippedGuid;
        }

        return ObjectManager.GetEquippedItems()
            .FirstOrDefault(item => item != null && FishingData.IsFishingPole(item.ItemId))
            ?.Guid ?? 0UL;
    }

    private Dictionary<uint, int> SnapshotCatchBagItemCounts()
        => ObjectManager.GetContainedItems()
            .Where(item => item != null && item.ItemId != 0 && !FishingData.IsFishingPole(item.ItemId))
            .GroupBy(item => item.ItemId)
            .ToDictionary(
                group => group.Key,
                group => group.Sum(item =>
                {
                    var stackCount = item.StackCount > 0 ? item.StackCount : item.Quantity;
                    return (int)Math.Max(stackCount, 1u);
                }));

    private static int GetFishingSkill(IWoWLocalPlayer player)
    {
        foreach (var skill in player.SkillInfo ?? Array.Empty<SkillInfo>())
        {
            var skillId = skill.SkillInt1 & 0xFFFF;
            if (skillId == FishingData.FishingSkillId)
                return (int)(skill.SkillInt2 & 0xFFFF);
        }

        return 0;
    }
}
