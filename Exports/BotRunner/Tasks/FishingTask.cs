using BotRunner.Combat;
using BotRunner.Helpers;
using BotRunner.Interfaces;
using GameData.Core.Enums;
using GameData.Core.Frames;
using GameData.Core.Interfaces;
using GameData.Core.Models;
using Microsoft.Extensions.Logging;
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
    public FishingTask(
        IBotContext botContext,
        IReadOnlyList<Position>? searchWaypoints = null,
        string? location = null,
        bool useGmCommands = false,
        uint? masterPoolId = null)
        : base(botContext)
    {
        _searchWaypoints = searchWaypoints ?? [];
        _location = string.IsNullOrWhiteSpace(location) ? null : location.Trim();
        _useGmCommands = useGmCommands;
        _masterPoolId = masterPoolId;
        _state = useGmCommands
            ? FishingState.EnsureGearAndSpells
            : (_location != null ? FishingState.TravelToLocation : FishingState.EnsurePoleEquipped);
        _activityAnnounced = false;
    }

    private enum FishingState
    {
        EnsureGearAndSpells,
        AwaitGearAndSpells,
        TravelToLocation,
        AwaitTravelArrival,
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

    private readonly string? _location;
    private readonly bool _useGmCommands;
    private readonly uint? _masterPoolId;
    private int _outfitStep;
    private int _outfitSettleTicks;
    private Position? _travelStartPosition;
    private DateTime _travelDispatchedAt;
    private bool _travelDispatched;
    private bool _activityAnnounced;
    private const int TravelTimeoutMs = 12_000;
    private const int OutfitSettleTicks = 5;

    // WoW fishing bobbers land 15-30y from the player. Pool fishing works up to ~30y.
    // The bot should cast from docks/shoreline without walking into water.
    // Previous 18±4y range forced the bot too close and off docks into water.
    internal const float DesiredPoolDistance = 24f;
    internal const float PoolDistanceTolerance = 14f;
    internal const float MinCastingDistance = DesiredPoolDistance - PoolDistanceTolerance;
    internal const float MaxCastingDistance = DesiredPoolDistance + PoolDistanceTolerance;
    internal const float CurrentPositionCastDistance = 18f;
    private const float CastTargetInsetFromPool = 4f;
    private const int EquipTimeoutMs = 4000;
    private const int LureApplyTimeoutMs = 6000;
    private const int PoolAcquireTimeoutMs = 15000;
    private const int SearchWalkTimeoutMs = 180_000;
    private const int SearchWaypointStallTimeoutMs = 20_000;
    private const float SearchWaypointArrivalRadius = 6f;
    private const float SearchWaypointArrivalMaxZDelta = SearchWaypointDirectFallbackMaxZDelta;
    private const float SearchWaypointProgressResetDistance = 2f;
    private const float SearchWaypointTravelStride = 8f;
    private const float SearchWaypointMicroStride = 1f;
    private const float SearchWaypointDirectFallbackRadius = 6f;
    private const float SearchWaypointDirectFallbackMaxZDelta = 1.5f;
    private const float SearchWaypointSnapRadius = 8f;
    private const float SearchWaypointSnapMaxZDelta = 4f;
    private const int SearchWaypointRefineAngleSteps = 16;
    private const float SearchWaypointRefineStep = 2f;
    private const float SearchWaypointRefineRadius = 6f;
    private const float SearchWaypointProbeLowerSurfacePenaltyScale = 8f;
    private const float SearchWaypointRouteMaxDropZ = 2.5f;
    private const float SearchWaypointRouteMaxRiseZ = 3.5f;
    private const float SearchWaypointRouteMaxExtraDetourDistance = 12f;
    private const float SearchWaypointRouteMaxDetourMultiplier = 2f;
    private const float SearchWaypointMinForwardProgress = 1.5f;
    private const float SearchWaypointMicroStrideMinForwardProgress = 0.75f;
    private const float FishingApproachWalkableSnapRadius = 4f;
    private const float FishingApproachCandidateDedupRadius = 1.5f;
    private const float FishingApproachLowerSurfacePenaltyScale = 6f;
    private const float FailedApproachRejectRadius = 4f;
    private const int ApproachStallTimeoutMs = 12000;
    private const float ApproachProgressResetDistance = 1.5f;
    private const float ResolvedCastArrivalRadius2D = 3f;
    private const float ResolvedCastArrivalMaxZDelta = 1.5f;
    private const float FellOffPierOnApproachZTolerance = 1.5f;
    private const float FellOffPierZThreshold = 3f;
    // Sweet-spot distance where the bobber (which lands ~18y ahead of the player in their
    // facing direction) touches down on the pool. Casting from exactly this distance makes
    // the bobber land on the pool itself, which is what triggers the fishing-pool loot.
    private const float IdealCastingDistanceFromPool = 18f;
    // Maximum Z delta between the player (who is standing on the pier when the resolver
    // runs) and a pathfinding-interpolated standoff or any intermediate navmesh node.
    // Anything farther away means the navmesh route drops into water / climbs terrain —
    // not a valid pier standoff.
    private const float PathfindingPierLayerZTolerance = 3f;
    // Pool acquisition reach. Set to match FishingPoolDetectRange so any pool the
    // ObjectManager has streamed in can be locked immediately after teleport, which
    // skips the blind radial search walk. Skipping search walk is important because
    // FG's WoW.exe physics can overshoot waypoints and climb nearby non-dock terrain
    // while trying to find a pool, leaving the bot far from a walkable cast position.
    private const float MaxPoolLockDistance = 80f;
    private const int CastStabilizeDelayMs = 250;
    private const int CastStabilizeTimeoutMs = 1500;
    private const int CastConfirmationTimeoutMs = 5000;
    private const int CatchResolutionTimeoutMs = 28000;
    private const int LootWindowTimeoutMs = 5000;
    private const int LootCompletionTimeoutMs = 3000;

    private FishingState _state;
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
    private Position? _cachedApproachPosition;
    private ulong _cachedApproachPoolGuid;
    private FishingCastPosition? _cachedCastPosition;
    private ulong _cachedCastPoolGuid;
    private bool _reachedApproachLevelForActivePool;
    private ulong _failedApproachPoolGuid;
    private readonly List<Position> _failedApproachPositions = [];
    private DateTime _approachProgressAt = DateTime.UtcNow;
    private float _approachProgressDistance = float.MaxValue;
    private bool _castPreparationIssued;
    private IReadOnlyList<Position> _searchWaypoints;
    private bool _defaultSearchWaypointsGenerated;
    private readonly Dictionary<int, Position> _resolvedSearchWaypoints = [];
    private int _searchWaypointIndex;
    private DateTime _searchStartedAt;
    private DateTime _searchWaypointEnteredAt;
    private float _searchWaypointResetDistance = float.MaxValue;
    private Position? _searchWaypointRawDiagnostic;
    private Position? _searchWaypointResolvedDiagnostic;
    private Position? _searchWaypointTravelTargetDiagnostic;
    private Position? _searchWaypointPathHeadDiagnostic;
    private int _searchWaypointPathNodesDiagnostic;
    private string _searchWaypointTravelModeDiagnostic = "none";
    private readonly HashSet<ulong> _unreachablePoolGuids = [];
    private readonly object _inventoryFullLock = new();
    private string? _pendingInventoryFullMessage;
    private bool _inventoryErrorHandlerSubscribed;

    private void EnsureInventoryErrorHandler()
    {
        if (_inventoryErrorHandlerSubscribed)
            return;

        var handler = EventHandler;
        if (handler == null)
            return;

        handler.OnErrorMessage += OnInventoryErrorMessage;
        _inventoryErrorHandlerSubscribed = true;
    }

    private void ReleaseInventoryErrorHandler()
    {
        if (!_inventoryErrorHandlerSubscribed)
            return;

        var handler = EventHandler;
        if (handler != null)
            handler.OnErrorMessage -= OnInventoryErrorMessage;
        _inventoryErrorHandlerSubscribed = false;
    }

    private void OnInventoryErrorMessage(object? sender, OnUiMessageArgs args)
    {
        var message = args?.Message;
        if (string.IsNullOrWhiteSpace(message))
            return;

        // Server-side SMSG_INVENTORY_CHANGE_FAILURE is rendered as "Inventory change
        // failed: Inventory is full" by the WorldClient pipeline. The UI client also
        // surfaces "Inventory is full." directly. Both indicate the bag can't hold
        // the pending loot item, which is a fatal condition for a fishing task that
        // is supposed to actually catch and receive a fish.
        if (message.IndexOf("Inventory is full", StringComparison.OrdinalIgnoreCase) < 0
            && message.IndexOf("InventoryFull", StringComparison.OrdinalIgnoreCase) < 0)
        {
            return;
        }

        lock (_inventoryFullLock)
            _pendingInventoryFullMessage = message;
    }

    private bool TryConsumeInventoryFull(out string message)
    {
        lock (_inventoryFullLock)
        {
            message = _pendingInventoryFullMessage ?? string.Empty;
            _pendingInventoryFullMessage = null;
            return message.Length > 0;
        }
    }

    public void Update()
    {
        EnsureInventoryErrorHandler();

        var player = ObjectManager.Player;
        if (player?.Position == null)
        {
            PopFishingTask("no_player");
            return;
        }

        if (player.IsInCombat)
        {
            ObjectManager.StopAllMovement();
            PopFishingTask("combat");
            return;
        }

        // If the server told us the bag is full, no amount of retrying will let a fish
        // land. Surface the specific reason so the test can distinguish "bobber missed
        // the pool" from "caught a fish but bag was full" and bail out immediately.
        if (TryConsumeInventoryFull(out var inventoryFullMessage))
        {
            Logger.LogWarning("[FISH] Inventory-full error from server: {Message}", inventoryFullMessage);
            BotContext.AddDiagnosticMessage(
                $"[TASK] FishingTask inventory_full message=\"{inventoryFullMessage}\"");
            PopFishingTask("inventory_full");
            return;
        }

        EnsureStartingBagSnapshot();

        if (!_activityAnnounced)
        {
            _activityAnnounced = true;
            BotContext.AddDiagnosticMessage(
                $"[TASK] FishingTask activity_start location={_location ?? "(none)"} useGm={_useGmCommands} masterPoolId={_masterPoolId?.ToString() ?? "(none)"}");
        }

        switch (_state)
        {
            case FishingState.EnsureGearAndSpells:
                TickEnsureGearAndSpells();
                return;
            case FishingState.AwaitGearAndSpells:
                TickAwaitGearAndSpells();
                return;
            case FishingState.TravelToLocation:
                TickTravelToLocation(player);
                return;
            case FishingState.AwaitTravelArrival:
                TickAwaitTravelArrival(player);
                return;
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

    private void TickEnsureGearAndSpells()
    {
        // One chat command per tick to avoid flooding. Steps are idempotent
        // (.learn/.setskill no-op when already satisfied). Step 0 strips the
        // bag before re-adding the pole + bait so the test doesn't accumulate
        // duplicate fishing poles across runs and then silently hit "Inventory
        // is full" when a fish tries to drop.
        var om = ObjectManager;
        switch (_outfitStep)
        {
            case 0:
                om.SendChatMessage(".reset items");
                BotContext.AddDiagnosticMessage("[TASK] FishingTask outfit_reset_items");
                break;
            case 1:
                om.SendChatMessage($".additem {FishingData.FishingPoleItemId} 1");
                break;
            case 2:
                om.SendChatMessage($".additem {FishingData.NightcrawlerBaitItemId} 1");
                break;
            case 3:
                om.SendChatMessage($".learn {FishingData.FishingRank1}");
                break;
            case 4:
                om.SendChatMessage($".learn {FishingData.FishingPoleProficiency}");
                break;
            case 5:
                om.SendChatMessage($".setskill {FishingData.FishingSkillId} 75 300");
                break;
            case 6:
                if (_masterPoolId.HasValue)
                {
                    om.SendChatMessage($".pool update {_masterPoolId.Value}");
                    BotContext.AddDiagnosticMessage(
                        $"[TASK] FishingTask pool_refresh_dispatched master={_masterPoolId.Value}");
                }
                break;
            case 7:
                BotContext.AddDiagnosticMessage("[TASK] FishingTask outfit_complete");
                _outfitSettleTicks = 0;
                SetState(FishingState.AwaitGearAndSpells);
                _outfitStep++;
                return;
        }

        _outfitStep++;
    }

    private void TickAwaitGearAndSpells()
    {
        // Brief settle to let the GM commands apply (.additem/.learn/.setskill)
        // before we start checking pole/lure inventory. The actual readiness
        // gate is the existing EnsurePoleEquipped state below.
        _outfitSettleTicks++;
        if (_outfitSettleTicks < OutfitSettleTicks)
            return;

        SetState(_location != null
            ? FishingState.TravelToLocation
            : FishingState.EnsurePoleEquipped);
    }

    private void TickTravelToLocation(IWoWLocalPlayer player)
    {
        if (_location == null)
        {
            SetState(FishingState.EnsurePoleEquipped);
            return;
        }

        // Use the GM tele command to jump to the named location. We send
        // ".tele name <character> <location>" when player.Name is populated;
        // BG-side name population can lag a few ticks after login/teleport,
        // so we fall back to the self-form ".tele <location>" if needed
        // (server still resolves the executing character by chat sender).
        // .tele resolves to a server-defined safe location, so we never
        // hardcode coordinates here.
        _travelStartPosition = player.Position;
        var characterName = player.Name;
        var command = string.IsNullOrWhiteSpace(characterName)
            ? $".tele {_location}"
            : $".tele name {characterName} {_location}";
        ObjectManager.SendChatMessage(command);
        BotContext.AddDiagnosticMessage(
            $"[TASK] FishingTask travel_dispatched location={_location} command='{command}'");
        _travelDispatched = true;
        _travelDispatchedAt = DateTime.UtcNow;
        SetState(FishingState.AwaitTravelArrival);
    }

    private void TickAwaitTravelArrival(IWoWLocalPlayer player)
    {
        if (!_travelDispatched || _travelStartPosition == null)
        {
            SetState(FishingState.TravelToLocation);
            return;
        }

        var moved = player.Position.DistanceTo(_travelStartPosition);
        if (moved > 10f)
        {
            BotContext.AddDiagnosticMessage(
                $"[TASK] FishingTask travel_arrived location={_location} pos=({player.Position.X:F1},{player.Position.Y:F1},{player.Position.Z:F1})");
            SetState(FishingState.EnsurePoleEquipped);
            return;
        }

        if ((DateTime.UtcNow - _travelDispatchedAt).TotalMilliseconds >= TravelTimeoutMs)
        {
            BotContext.AddDiagnosticMessage(
                $"[TASK] FishingTask travel_timeout location={_location} pos=({player.Position.X:F1},{player.Position.Y:F1},{player.Position.Z:F1})");
            SetState(FishingState.EnsurePoleEquipped);
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
            Logger.LogWarning("[FISH] No fishing pole found in bags.");
            PopFishingTask("no_fishing_pole");
            return;
        }

        _equipAttempts++;
        ObjectManager.EquipItem(poleLocation.Value.bag, poleLocation.Value.slot);
        Logger.LogInformation("[FISH] Equipping fishing pole from bag={Bag} slot={Slot} (attempt {Attempt}).",
            poleLocation.Value.bag, poleLocation.Value.slot, _equipAttempts);
        BotContext.AddDiagnosticMessage(
            $"[TASK] FishingTask equipping_pole bag={poleLocation.Value.bag} slot={poleLocation.Value.slot} attempt={_equipAttempts}");
        SetState(FishingState.AwaitPoleEquip);
    }

    private void AwaitPoleEquip()
    {
        if (FishingData.HasFishingPoleEquipped(ObjectManager))
        {
            Logger.LogInformation("[FISH] Fishing pole equipped.");
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

        Logger.LogWarning("[FISH] Fishing pole equip timed out.");
        PopFishingTask("pole_equip_timeout");
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
        Logger.LogInformation("[FISH] Applying lure item={ItemId} from bag={Bag} slot={Slot} (attempt {Attempt}).",
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
            Logger.LogInformation("[FISH] Lure application confirmed. item={ItemId} countDropped={CountDropped} mainhandEnchanted={MainhandEnchanted}",
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

        Logger.LogWarning("[FISH] Fishing lure application timed out for item {ItemId}.", _activeLureItemId);
        PopFishingTask("lure_apply_timeout");
    }

    private void AcquireFishingPool(IWoWLocalPlayer player)
    {
        var pool = FindTrackedOrNearestPool(player.Position);
        var poolDistance = pool?.Position != null ? player.Position.DistanceTo(pool.Position) : float.MaxValue;
        if (pool == null || poolDistance > MaxPoolLockDistance)
        {
            if (ElapsedMs >= PoolAcquireTimeoutMs)
            {
                // If explicit waypoints were configured by the caller, use them. Otherwise
                // don't start a blind radial sweep — it oscillates the bot around the
                // teleport landing without actually finding anything the pier-layer
                // acquisition filter would accept. Fail cleanly instead so the caller
                // (and the test) see a clear "nothing fishable from this pier" signal.
                if (_searchWaypoints.Count > 0 && _searchWaypointIndex < _searchWaypoints.Count)
                {
                    Logger.LogInformation("[FISH] No pool visible after {Timeout}ms; starting search walk with {Count} caller-supplied waypoints.",
                        PoolAcquireTimeoutMs, _searchWaypoints.Count);
                    BotContext.AddDiagnosticMessage(
                        $"[TASK] FishingTask search_walk_start waypoints={_searchWaypoints.Count}");
                    _searchStartedAt = DateTime.UtcNow;
                    BeginSearchWaypointWindow(_searchStartedAt);
                    SetState(FishingState.SearchForPool);
                }
                else
                {
                    var rejectedCount = _unreachablePoolGuids.Count;
                    var reason = rejectedCount > 0 ? "no_pier_reachable_pool" : "no_fishing_pool";
                    Logger.LogWarning("[FISH] No pier-reachable fishing pool within {Range}y (rejected={Rejected}).",
                        Config.FishingPoolDetectRange, rejectedCount);
                    BotContext.AddDiagnosticMessage(
                        $"[TASK] FishingTask {reason} rejected={rejectedCount} detectRange={Config.FishingPoolDetectRange:F0}");
                    PopFishingTask(reason);
                }
            }

            return;
        }

        // Validate cast position BEFORE committing to this pool. A pool whose only
        // reachable standoff would land the bot in water (bad navmesh snap, far offshore
        // spawn) is better skipped — the next-nearest pool may be casteable from the
        // same pier. If no candidate resolves we mark the pool unreachable so the
        // acquisition loop doesn't re-pick it on every tick.
        var castPreview = TryResolveCastPosition(player.MapId, player.Position, pool.Position, out var previewSource);
        if (castPreview == null)
        {
            MarkPoolUnreachable(pool.Guid, "no_valid_cast_position", player.Position, pool.Position);
            return;
        }

        TrackActivePool(pool.Guid);
        _cachedCastPosition = castPreview;
        _cachedCastPoolGuid = pool.Guid;
        BotContext.AddDiagnosticMessage(
            $"[TASK] FishingTask pool_acquired guid=0x{pool.Guid:X} entry={pool.Entry} distance={poolDistance:F1} castSource={previewSource}");

        // Always go through MoveToFishingPool so elevated-surface cast positions
        // and shoreline approaches are resolved before casting.
        ClearNavigation();
        SetState(FishingState.MoveToFishingPool);
    }

    private void SearchForPool(IWoWLocalPlayer player)
    {
        var pool = FindTrackedOrNearestPool(player.Position);
        var poolDistance = pool?.Position != null ? player.Position.DistanceTo(pool.Position) : float.MaxValue;
        if (pool != null && poolDistance <= MaxPoolLockDistance)
        {
            var castPreview = TryResolveCastPosition(player.MapId, player.Position, pool.Position, out var previewSource);
            if (castPreview == null)
            {
                MarkPoolUnreachable(pool.Guid, "no_valid_cast_position", player.Position, pool.Position);
                return;
            }

            TrackActivePool(pool.Guid);
            _cachedCastPosition = castPreview;
            _cachedCastPoolGuid = pool.Guid;
            BotContext.AddDiagnosticMessage(
                $"[TASK] FishingTask search_walk_found_pool guid=0x{pool.Guid:X} entry={pool.Entry} distance={poolDistance:F1} waypoint={_searchWaypointIndex}/{_searchWaypoints.Count} castSource={previewSource}");
            Logger.LogInformation("[FISH] Pool found during search walk at {Distance:F1}y.", poolDistance);
            ClearNavigation();
            // Always move to the resolved cast/approach position first, even if
            // the pool is already in raw casting range.
            SetState(FishingState.MoveToFishingPool);
            return;
        }

        var searchElapsed = (int)(DateTime.UtcNow - _searchStartedAt).TotalMilliseconds;
        if (searchElapsed >= SearchWalkTimeoutMs)
        {
            Logger.LogWarning("[FISH] Search walk timed out after {Elapsed}ms.", searchElapsed);
            BotContext.AddDiagnosticMessage(
                $"[TASK] FishingTask search_walk_timeout elapsed={searchElapsed}ms waypoint={_searchWaypointIndex}/{_searchWaypoints.Count}");
            PopFishingTask("search_timeout");
            return;
        }

        if (_searchWaypointIndex >= _searchWaypoints.Count)
        {
            Logger.LogWarning("[FISH] Search walk exhausted all {Count} waypoints without finding a pool.", _searchWaypoints.Count);
            BotContext.AddDiagnosticMessage(
                $"[TASK] FishingTask search_walk_exhausted waypoints={_searchWaypoints.Count} elapsed={searchElapsed}ms");
            PopFishingTask("search_exhausted");
            return;
        }

        var rawWaypoint = _searchWaypoints[_searchWaypointIndex];
        var waypoint = ResolveSearchWaypoint(player.MapId, player.Position, _searchWaypointIndex, rawWaypoint);
        var waypointDistance = player.Position.DistanceTo(waypoint);

        if (CanTreatSearchWaypointAsArrived(player.Position, waypoint, waypointDistance))
        {
            RecordSearchWaypointDiagnostic(rawWaypoint, waypoint, waypoint, null, "arrived");
            var arrivedWaypoint = _searchWaypointIndex + 1;
            var arrivalDetail = BuildSearchWaypointDiagnosticDetail(player.Position);
            AdvanceSearchWaypoint();
            BotContext.AddDiagnosticMessage(
                $"[TASK] FishingTask search_walk waypoint={arrivedWaypoint}/{_searchWaypoints.Count} distance={waypointDistance:F1} {arrivalDetail}");
            return;
        }

        if (_searchWaypointResetDistance == float.MaxValue)
        {
            _searchWaypointResetDistance = waypointDistance;
        }
        else if (waypointDistance <= _searchWaypointResetDistance - SearchWaypointProgressResetDistance)
        {
            BeginSearchWaypointWindow();
            _searchWaypointResetDistance = waypointDistance;
        }

        var waypointElapsed = (int)(DateTime.UtcNow - _searchWaypointEnteredAt).TotalMilliseconds;
        if (waypointElapsed >= SearchWaypointStallTimeoutMs)
        {
            ClearNavigation();
            var stalledWaypoint = _searchWaypointIndex + 1;
            RecordSearchWaypointDiagnostic(
                rawWaypoint,
                waypoint,
                _searchWaypointTravelTargetDiagnostic,
                _searchWaypointPathHeadDiagnostic,
                _searchWaypointTravelModeDiagnostic,
                _searchWaypointPathNodesDiagnostic);
            var stalledDetail = BuildSearchWaypointDiagnosticDetail(player.Position);
            AdvanceSearchWaypoint();
            BotContext.AddDiagnosticMessage(
                $"[TASK] FishingTask search_walk_stalled waypoint={stalledWaypoint}/{_searchWaypoints.Count} distance={waypointDistance:F1} elapsed={waypointElapsed}ms {stalledDetail}");
            return;
        }

        var (travelTarget, travelPath) = ResolveSearchWaypointTravelTarget(player.MapId, player.Position, waypoint);
        if (CanDirectSearchWalkFallback(player.MapId, player.Position, travelTarget))
        {
            RecordSearchWaypointDiagnostic(rawWaypoint, waypoint, travelTarget, null, "direct");
            ClearNavigation();
            ObjectManager.MoveToward(travelTarget);
            return;
        }

        if (TryFollowSearchWaypointPath(player.Position, rawWaypoint, waypoint, travelTarget, travelPath))
            return;

        // The search-walk probe path is the authoritative guard for these short local
        // streaming steps. If the probe path was rejected, do not immediately requery
        // NavigationPath for the same candidate and walk the same lower-layer / steep
        // recovery route anyway.
        var rejectedProbeRoute = Container.PathfindingClient != null && travelPath == null;
        if (!rejectedProbeRoute && TryNavigateToward(travelTarget))
        {
            RecordSearchWaypointDiagnostic(rawWaypoint, waypoint, travelTarget, null, "navigate");
            return;
        }

        // Search-walk waypoints are only staging probes to bring the client close enough
        // to stream the fishing pool. When pathfinding says a waypoint is not usable, do
        // not blindly steer straight toward it across pier/water geometry. Only keep the
        // direct fallback for very short same-layer nudges; otherwise skip the waypoint.
        if (CanDirectSearchWalkFallback(player.MapId, player.Position, travelTarget))
        {
            RecordSearchWaypointDiagnostic(rawWaypoint, waypoint, travelTarget, null, "direct_fallback");
            ObjectManager.MoveToward(travelTarget);
            return;
        }

        ClearNavigation();
        RecordSearchWaypointDiagnostic(rawWaypoint, waypoint, travelTarget, null, rejectedProbeRoute ? "probe_rejected" : "unreachable");
        var skippedWaypoint = _searchWaypointIndex + 1;
        var unreachableDetail = BuildSearchWaypointDiagnosticDetail(player.Position);
        AdvanceSearchWaypoint();
        BotContext.AddDiagnosticMessage(
            $"[TASK] FishingTask search_walk_unreachable waypoint={skippedWaypoint}/{_searchWaypoints.Count} distance={waypointDistance:F1} {unreachableDetail}");
    }

    private void MoveToFishingPool(IWoWLocalPlayer player)
    {
        // If the bot walked into water during approach, the chosen standoff wasn't
        // actually pier-reachable (bad navmesh bridge, detached structure, etc.).
        // Mark the pool unreachable and let acquisition pick the next candidate —
        // the task shouldn't pop on a single bad pool when others may be fishable.
        var tracked = _activePoolGuid != 0
            ? ObjectManager.GameObjects.FirstOrDefault(g => g.Guid == _activePoolGuid)
            : null;
        if (player.IsSwimming)
        {
            ObjectManager.ForceStopImmediate();
            Logger.LogWarning("[FISH] Entered water while approaching pool; skipping this pool.");
            BotContext.AddDiagnosticMessage(
                $"[TASK] FishingTask player_swimming_approach guid=0x{_activePoolGuid:X} playerPos=({player.Position.X:F1},{player.Position.Y:F1},{player.Position.Z:F1})");
            if (_activePoolGuid != 0 && tracked?.Position != null)
                MarkPoolUnreachable(_activePoolGuid, "player_swimming_approach", player.Position, tracked.Position);
            ClearNavigation();
            _cachedApproachPosition = null;
            ClearCastPositionCache();
            _activePoolGuid = 0;
            SetState(FishingState.AcquireFishingPool);
            return;
        }

        var pool = FindTrackedOrNearestPool(player.Position);
        if (pool?.Position == null)
        {
            if (ElapsedMs >= PoolAcquireTimeoutMs)
            {
                Logger.LogWarning("[FISH] Lost fishing pool while approaching (trackedGuid=0x{Guid:X}).", _activePoolGuid);
                BotContext.AddDiagnosticMessage(
                    $"[TASK] FishingTask lost_fishing_pool trackedGuid=0x{_activePoolGuid:X} playerZ={player.Position.Z:F1} pos=({player.Position.X:F1},{player.Position.Y:F1},{player.Position.Z:F1})");
                PopFishingTask("lost_fishing_pool");
            }

            return;
        }

        var poolDistance = player.Position.DistanceTo(pool.Position);
        FishingCastPosition? resolvedCastPosition = null;
        if (_cachedCastPoolGuid != pool.Guid)
        {
            _cachedCastPosition = TryResolveCastPosition(player.MapId, player.Position, pool.Position, out var castPositionSource);
            _cachedCastPoolGuid = pool.Guid;
            if (_cachedCastPosition != null)
            {
                var castPosition = _cachedCastPosition.Value;
                BotContext.AddDiagnosticMessage(
                    $"[TASK] FishingTask cast_position_resolved source={castPositionSource} pos=({castPosition.Position.X:F1},{castPosition.Position.Y:F1},{castPosition.Position.Z:F1}) facing={castPosition.FacingRadians:F2} edgeDist={castPosition.EdgeDistance:F1} los={castPosition.HasLineOfSight}");
            }
            else
            {
                BotContext.AddDiagnosticMessage(
                    $"[TASK] FishingTask cast_position_unresolved guid=0x{pool.Guid:X} playerPos=({player.Position.X:F1},{player.Position.Y:F1},{player.Position.Z:F1}) poolPos=({pool.Position.X:F1},{pool.Position.Y:F1},{pool.Position.Z:F1})");
            }
        }

        // If no valid cast standoff resolves, don't fall through to the geometric
        // shoreline approach (which is content to project into open water). Mark the
        // pool unreachable and let AcquireFishingPool pick the next-nearest pool.
        if (_cachedCastPosition == null || _cachedCastPoolGuid != pool.Guid)
        {
            MarkPoolUnreachable(pool.Guid, "no_valid_cast_position", player.Position, pool.Position);
            ClearNavigation();
            _activePoolGuid = 0;
            SetState(FishingState.AcquireFishingPool);
            return;
        }

        resolvedCastPosition = _cachedCastPosition.Value;
        // Do not skip the shoreline resolver from every legal cast distance. Ratchet's
        // farther dock standoffs can satisfy LOS while still producing NOT_FISHABLE from
        // the server. Only the tighter near-edge positions should cast in place.
        if (resolvedCastPosition == null
            && !IsRejectedApproachPosition(player.Position)
            && poolDistance <= CurrentPositionCastDistance
            && IsInCastingWindow(player.Position, pool.Position)
            && CanCastFromPosition(player.MapId, player.Position, pool.Position))
        {
            ObjectManager.ForceStopImmediate();
            ObjectManager.Face(pool.Position);
            Logger.LogInformation("[FISH] Pool already castable from current position at {Distance:F1}y (pool=0x{Guid:X}), playerZ={PlayerZ:F1}.",
                poolDistance, pool.Guid, player.Position.Z);
            BotContext.AddDiagnosticMessage(
                $"[TASK] FishingTask in_cast_range_current guid=0x{pool.Guid:X} distance={poolDistance:F1} playerZ={player.Position.Z:F1}");
            SetState(FishingState.ResolveAndCast);
            return;
        }

        // Elevated docks/piers use the native cast-position finder. Everything else
        // falls back to the existing shoreline approach resolver.
        // Don't stop at any arbitrary point in the casting window — walk all the way
        // to the approach position so we're on the pier edge near the water.
        var approachPosition = resolvedCastPosition?.Position ?? ResolveFishingApproachPosition(player, pool.Position);
        var approachDistance = resolvedCastPosition != null
            ? player.Position.DistanceTo2D(approachPosition)
            : player.Position.DistanceTo(approachPosition);
        const float approachArrivalRadius = 5f;

        ObserveApproachProgress(approachDistance);

        // Note whether the bot has actually reached the approach elevation at any point
        // since acquiring this pool. The "fell_off_pier" guard below only fires when we
        // observe a real drop *after* being on the pier — not when a search-walk finishes
        // at water level and the cast resolver picks a dock-top standoff the bot has
        // never stood on. Without this phase gate the task pops immediately the first
        // time MoveToFishingPool runs from below the dock.
        if (MathF.Abs(player.Position.Z - approachPosition.Z) <= FellOffPierOnApproachZTolerance)
            _reachedApproachLevelForActivePool = true;

        // Detect if the bot fell off the pier (Z dropped significantly below approach position).
        // When this happens the navmesh claimed the candidate was reachable but physics
        // disagreed (step-off, broken plank, unmodeled edge). Don't pop the whole task —
        // mark this pool unreachable so the acquisition loop picks the next nearest pool
        // that actually works from this pier.
        if (_reachedApproachLevelForActivePool
            && approachPosition.Z - player.Position.Z > FellOffPierZThreshold)
        {
            ObjectManager.ForceStopImmediate();
            Logger.LogWarning("[FISH] Player Z ({PlayerZ:F1}) dropped far below approach Z ({ApproachZ:F1}) — fell off pier.",
                player.Position.Z, approachPosition.Z);
            BotContext.AddDiagnosticMessage(
                $"[TASK] FishingTask fell_off_pier playerZ={player.Position.Z:F1} approachZ={approachPosition.Z:F1} guid=0x{pool.Guid:X}");
            MarkPoolUnreachable(pool.Guid, "fell_off_pier", player.Position, pool.Position);
            ClearNavigation();
            _cachedApproachPosition = null;
            ClearCastPositionCache();
            _activePoolGuid = 0;
            SetState(FishingState.AcquireFishingPool);
            return;
        }

        if (resolvedCastPosition != null)
        {
            var approachZDelta = MathF.Abs(player.Position.Z - approachPosition.Z);
            if (approachDistance <= ResolvedCastArrivalRadius2D
                && approachZDelta <= ResolvedCastArrivalMaxZDelta)
            {
                ObjectManager.ForceStopImmediate();
                ObjectManager.SetFacing(resolvedCastPosition.Value.FacingRadians);
                Logger.LogInformation("[FISH] Reached resolved cast position at {Distance:F1}y from pool (pool=0x{Guid:X}), edgeDist={EdgeDist:F1}, los={HasLineOfSight}.",
                    poolDistance, pool.Guid, resolvedCastPosition.Value.EdgeDistance, resolvedCastPosition.Value.HasLineOfSight);
                BotContext.AddDiagnosticMessage(
                    $"[TASK] FishingTask cast_position_arrived guid=0x{pool.Guid:X} distance={poolDistance:F1} edgeDist={resolvedCastPosition.Value.EdgeDistance:F1} los={resolvedCastPosition.Value.HasLineOfSight}");
                SetState(FishingState.ResolveAndCast);
                return;
            }
        }
        else if (!IsRejectedApproachPosition(player.Position)
            && approachDistance <= approachArrivalRadius
            && CanCastFromPosition(player.MapId, player.Position, pool.Position))
        {
            ObjectManager.ForceStopImmediate();
            ObjectManager.Face(pool.Position);
            Logger.LogInformation("[FISH] Reached approach position at {Distance:F1}y from pool (pool=0x{Guid:X}), playerZ={PlayerZ:F1}.",
                poolDistance, pool.Guid, player.Position.Z);
            BotContext.AddDiagnosticMessage(
                $"[TASK] FishingTask in_cast_range guid=0x{pool.Guid:X} distance={poolDistance:F1} playerZ={player.Position.Z:F1}");
            SetState(FishingState.ResolveAndCast);
            return;
        }

        if ((DateTime.UtcNow - _approachProgressAt).TotalMilliseconds >= ApproachStallTimeoutMs)
        {
            RetryFromPool("approach_stalled", approachPosition);
            return;
        }

        if (_lastApproachDiagnosticDistance == float.MaxValue || poolDistance <= _lastApproachDiagnosticDistance - 2f)
        {
            _lastApproachDiagnosticDistance = poolDistance;
            BotContext.AddDiagnosticMessage(
                $"[TASK] FishingTask approaching_pool guid=0x{pool.Guid:X} distance={poolDistance:F1} playerZ={player.Position.Z:F1}");
        }

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
            Logger.LogWarning("[FISH] Max cast attempts reached without a catch.");
            PopFishingTask("max_casts_reached");
            return;
        }

        if (player.IsSwimming)
        {
            Logger.LogWarning("[FISH] Player entered water before cast; aborting fishing attempt.");
            BotContext.AddDiagnosticMessage("[TASK] FishingTask retry reason=player_swimming");
            PopFishingTask("player_swimming");
            return;
        }

        if (!_castPreparationIssued)
        {
            // Issue the facing/stop edge once, then let the client settle before casting.
            // Re-sending stop+face every tick right up to the cast packet keeps the player
            // in a moving/turning state long enough for the server to reject the cast.
            ObjectManager.Face(pool.Position);
            ObjectManager.ForceStopImmediate();
            _castPreparationIssued = true;
        }

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
            Logger.LogWarning("[FISH] No castable fishing spell found in known spells or skill data.");
            PopFishingTask("no_fishing_spell");
            return;
        }

        if (!ObjectManager.CanCastSpell((int)_fishingSpellId, 0))
        {
            Logger.LogWarning("[FISH] Cannot cast fishing spell {SpellId}.", _fishingSpellId);
            PopFishingTask("cannot_cast");
            return;
        }

        _castsAttempted++;
        _sawFishingState = false;
        _sawBobber = false;
        _sawLootWindow = false;
        _sawLootItem = false;
        // Keep fishing casts on the object-manager abstraction instead of hand-authoring
        // packets here. FG uses the native client spell pipeline and BG mirrors that
        // zero-target cast shape through WoWSharpObjectManager.
        ObjectManager.CastSpell((int)_fishingSpellId);
        Logger.LogInformation(
            "[FISH] Cast {Attempt}/{MaxAttempts} started at pool 0x{Guid:X} with spell {SpellId} distance={Distance:F1}.",
            _castsAttempted,
            Config.MaxFishingCasts,
            pool.Guid,
            _fishingSpellId,
            player.Position.DistanceTo(pool.Position));
        BotContext.AddDiagnosticMessage(
            $"[TASK] FishingTask cast_started attempt={_castsAttempted} pool=0x{pool.Guid:X} spell={_fishingSpellId} distance={player.Position.DistanceTo(pool.Position):F1} mode=fishing_cast");
        SetState(FishingState.AwaitCastConfirmation);
    }

    private void AwaitCastConfirmation(IWoWLocalPlayer player)
    {
        if (FindActiveBobber() != null)
            _sawBobber = true;

        if (HasFishingState(player))
        {
            _sawFishingState = true;
            Logger.LogInformation("[FISH] Fishing cast confirmed. channel={ChannelingId} bobber={HasBobber}",
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
            if (TryRecordLootedBagDelta())
            {
                PopWithSuccess();
                return;
            }

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

        if (TryRecordLootedBagDelta())
        {
            PopWithSuccess();
            return;
        }

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
        Logger.LogInformation("[FISH] Loot window open. guid=0x{Guid:X} count={Count} items=[{Items}] coins={Coins}",
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

        // Exclude pools we already proved we can't cast at (no on-pier standoff available).
        // Otherwise the same far-offshore pool would get re-picked on every acquisition
        // tick and the task would never consider the closer, actually-reachable pool.
        return ObjectManager.GameObjects
            .Where(FishingData.IsFishingPool)
            .Where(gameObject => gameObject.Position != null
                && gameObject.Position.DistanceTo(playerPosition) <= Config.FishingPoolDetectRange
                && !_unreachablePoolGuids.Contains(gameObject.Guid))
            .OrderBy(gameObject => gameObject.Position!.DistanceTo(playerPosition))
            .FirstOrDefault();
    }

    private FishingCastPosition? TryResolveCastPosition(uint mapId, Position playerPosition, Position poolPosition, out string source)
    {
        // Primary: pool-centric ring sweep (BG / scene-data-backed). Probes ground Z in a
        // ring around the pool, keeps only on-pier hits, LOS-tests each against the pool,
        // and validates that the bot can actually reach the candidate via navmesh without
        // the route dropping into water. That chain rejects candidates on detached piers,
        // on far shores reachable only by swimming, and inside pier-support pillars.
        if (SupportsNativeLocalPhysicsQueries)
        {
            var native = FishingCastPositionFinder.FindForPool(
                mapId,
                playerPosition,
                poolPosition,
                candidate => IsCastStandoffReachable(mapId, playerPosition, candidate));
            if (native != null)
            {
                source = "native";
                return native;
            }
        }

        // Fallback: pathfinding interpolation with a pier-layer Z guard. Used when local
        // physics isn't available (FG) or the ring sweep found no LOS-clear candidate.
        var pathfinding = TryResolveCastViaPathfinding(mapId, playerPosition, poolPosition);
        if (pathfinding != null)
        {
            source = "pathfinding";
            return pathfinding;
        }

        source = "none";
        return null;
    }

    private bool IsCastStandoffReachable(uint mapId, Position playerPosition, Position candidate)
    {
        var client = Container.PathfindingClient;
        if (client == null || !client.IsAvailable)
            return true; // No pathfinding available: accept the candidate, caller will fail loudly if it's wrong.

        Position[] path;
        try
        {
            path = client.GetPath(mapId, playerPosition, candidate, smoothPath: false);
        }
        catch
        {
            return false;
        }

        if (path == null || path.Length == 0)
            return false;

        // Coarse gate: every path node must sit near the pier layer. A route that dips
        // well below (through water) means the candidate is only reachable by swimming.
        foreach (var node in path)
        {
            if ((playerPosition.Z - node.Z) > PathfindingPierLayerZTolerance)
                return false;
        }

        // Fine gate: sample ground Z along each path segment. Detour sometimes bridges
        // two nearby on-pier polys whose midpoint is actually over open water (detached
        // wreckage, disconnected shore). The node Z stays on-pier at both endpoints but
        // there's no walkable surface between them. Densely sampling exposes the gap
        // before the bot commits to the walk. Only feasible when local physics is
        // available; for FG we fall back to the coarse check.
        if (SupportsNativeLocalPhysicsQueries)
        {
            var pierZ = playerPosition.Z;
            const int samplesPerSegment = 5;
            for (var i = 0; i < path.Length - 1; i++)
            {
                var a = path[i];
                var b = path[i + 1];
                for (var s = 1; s <= samplesPerSegment; s++)
                {
                    var t = s / (float)(samplesPerSegment + 1);
                    var sx = a.X + ((b.X - a.X) * t);
                    var sy = a.Y + ((b.Y - a.Y) * t);
                    var (sz, found) = WoWSharpClient.Movement.NativeLocalPhysics.GetGroundZ(
                        mapId, sx, sy, pierZ + 4f, 15f);
                    if (!found)
                        return false;
                    if (MathF.Abs(sz - pierZ) > PathfindingPierLayerZTolerance)
                        return false;
                }
            }
        }

        return true;
    }

    private void MarkPoolUnreachable(ulong poolGuid, string reason, Position playerPosition, Position poolPosition)
    {
        if (_unreachablePoolGuids.Add(poolGuid))
        {
            BotContext.AddDiagnosticMessage(
                $"[TASK] FishingTask pool_unreachable guid=0x{poolGuid:X} reason={reason} playerPos=({playerPosition.X:F1},{playerPosition.Y:F1},{playerPosition.Z:F1}) poolPos=({poolPosition.X:F1},{poolPosition.Y:F1},{poolPosition.Z:F1})");
        }
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

    private void RetryFromPool(string reason, Position? rejectedApproachPosition = null)
    {
        Logger.LogWarning("[FISH] {Reason}; resetting fishing state for another cast.", reason);
        if (rejectedApproachPosition != null)
            RememberRejectedApproachPosition(rejectedApproachPosition, reason);
        else
            RememberFailedApproachPosition(reason);
        BotContext.AddDiagnosticMessage($"[TASK] FishingTask retry reason={reason}");
        ClearNavigation();
        _cachedApproachPosition = null;
        ClearCastPositionCache();
        SetState(FishingState.AcquireFishingPool);
    }

    private FishingCastPosition? TryResolveCastViaPathfinding(uint mapId, Position playerPosition, Position poolPosition)
    {
        var client = Container.PathfindingClient;
        if (client == null || !client.IsAvailable)
            return null;

        Position[] path;
        try
        {
            path = client.GetPath(mapId, playerPosition, poolPosition, smoothPath: false);
        }
        catch (Exception ex)
        {
            Logger.LogDebug(ex, "[FISH] Pathfinding cast-position query failed.");
            return null;
        }

        if (path == null || path.Length == 0)
            return null;

        // Pool positions on Vanilla are at the water plane (often Z=0) and MaNGOS
        // sometimes serves a far-out pool whose navmesh snap lands in deep/submerged
        // navmesh (Z dozens of yards below the pier). Reject any standoff that isn't
        // on the same layer as the bot — otherwise the interpolation happily returns a
        // point at Z=-23 in the middle of the bay and the bot walks straight in.
        bool IsOnPierLayer(Position p) => MathF.Abs(p.Z - playerPosition.Z) <= PathfindingPierLayerZTolerance;

        // Navmesh pathfinding snaps both endpoints to walkable polys, so every returned
        // segment is on solid ground. Walk the path from the pool end back toward the bot
        // and interpolate to find the point at IdealCastingDistanceFromPool from the pool.
        for (var i = path.Length - 1; i > 0; i--)
        {
            var nearPool = path[i];
            var nearPlayer = path[i - 1];
            var distNearPool = nearPool.DistanceTo(poolPosition);
            var distNearPlayer = nearPlayer.DistanceTo(poolPosition);

            var minDist = MathF.Min(distNearPool, distNearPlayer);
            var maxDist = MathF.Max(distNearPool, distNearPlayer);
            if (IdealCastingDistanceFromPool < minDist || IdealCastingDistanceFromPool > maxDist)
                continue;

            var segmentLen = MathF.Max(distNearPlayer - distNearPool, 0.0001f);
            var t = (IdealCastingDistanceFromPool - distNearPool) / segmentLen;
            t = Math.Clamp(t, 0f, 1f);
            var interpolated = new Position(
                nearPool.X + ((nearPlayer.X - nearPool.X) * t),
                nearPool.Y + ((nearPlayer.Y - nearPool.Y) * t),
                nearPool.Z + ((nearPlayer.Z - nearPool.Z) * t));

            if (!IsOnPierLayer(interpolated))
                continue;

            return BuildPathfindingCastPosition(interpolated, poolPosition, IdealCastingDistanceFromPool);
        }

        // No segment brackets the ideal distance on-layer. Fall back to the path node in
        // cast range that's on the pier layer AND closest to the ideal distance.
        Position? bestNode = null;
        float bestScore = float.MaxValue;
        foreach (var node in path)
        {
            if (!IsOnPierLayer(node))
                continue;
            var distToPool = node.DistanceTo(poolPosition);
            if (distToPool < MinCastingDistance || distToPool > MaxCastingDistance)
                continue;
            var score = MathF.Abs(distToPool - IdealCastingDistanceFromPool);
            if (score < bestScore)
            {
                bestNode = node;
                bestScore = score;
            }
        }

        if (bestNode != null)
            return BuildPathfindingCastPosition(bestNode, poolPosition, bestNode.DistanceTo(poolPosition));

        // If no path node is in casting range AND on the pier layer, decline. The caller
        // will emit cast_position_unresolved and the task will back off rather than
        // marching the bot into the water after a bad navmesh snap.
        var endpoint = path[path.Length - 1];
        var endpointDist = endpoint.DistanceTo(poolPosition);
        if (endpointDist > MaxCastingDistance + 10f || !IsOnPierLayer(endpoint))
            return null;

        return BuildPathfindingCastPosition(endpoint, poolPosition, endpointDist);
    }

    private static FishingCastPosition BuildPathfindingCastPosition(Position standoff, Position poolPosition, float distToPool, bool hasLineOfSight = true)
    {
        var facing = MathF.Atan2(poolPosition.Y - standoff.Y, poolPosition.X - standoff.X);
        if (facing < 0f)
            facing += MathF.PI * 2f;
        return new FishingCastPosition(standoff, facing, distToPool, HasLineOfSight: hasLineOfSight);
    }

    private Position ResolveFishingApproachPosition(IWoWLocalPlayer player, Position poolPosition)
    {
        // Return cached position if we're still targeting the same pool.
        if (_cachedApproachPosition != null && _cachedApproachPoolGuid == _activePoolGuid)
            return _cachedApproachPosition;

        // Sweep outward from the pool in a radial pattern. Sample ground Z at each point.
        // The nearest point with solid ground (dock/shore) at player elevation is the approach.
        var result = FindNearestPierPoint(player.MapId, player.Position, poolPosition, player.Position.Z);
        if (result != null)
        {
            _cachedApproachPosition = result;
            _cachedApproachPoolGuid = _activePoolGuid;
            var dist = poolPosition.DistanceTo(result);
            Logger.LogInformation("[FISH] Pier sweep found approach at ({X:F1}, {Y:F1}, {Z:F1}) — {Dist:F1}y from pool.",
                result.X, result.Y, result.Z, dist);
            return result;
        }

        // Fallback: direct approach toward pool from player position (pathfinding keeps bot on dock).
        Logger.LogWarning("[FISH] Pier sweep found no solid ground near pool; using direct approach.");
        var fallback = FishingData.GetPoolApproachPosition(player.Position, poolPosition, DesiredPoolDistance);
        _cachedApproachPosition = fallback;
        _cachedApproachPoolGuid = _activePoolGuid;
        return fallback;
    }

    /// <summary>
    /// Sweeps outward from the pool in all directions, sampling ground height.
    /// Returns the closest point to the pool that has solid ground (pier/dock/shore)
    /// at roughly the player's elevation and within casting range.
    /// </summary>
    private Position? FindNearestPierPoint(uint mapId, Position playerPosition, Position poolPosition, float referenceZ)
    {
        const float maxGroundZDelta = 4f;
        const int angleSteps = 24;          // every 15°
        const float startDist = MinCastingDistance;
        const float endDist = MaxCastingDistance;
        const float distStep = 3f;

        var client = Container.PathfindingClient;
        if (client == null || !SupportsNativeLocalPhysicsQueries)
            return null;

        // Build sample points: rings radiating outward from pool at each angle.
        var samplePoints = new List<Position>();
        var distStepCount = (int)((endDist - startDist) / distStep) + 1;
        for (var a = 0; a < angleSteps; a++)
        {
            var angle = a * (2f * MathF.PI / angleSteps);
            var cos = MathF.Cos(angle);
            var sin = MathF.Sin(angle);
            for (var d = 0; d < distStepCount; d++)
            {
                var dist = startDist + (d * distStep);
                samplePoints.Add(new Position(
                    poolPosition.X + (cos * dist),
                    poolPosition.Y + (sin * dist),
                    referenceZ));
            }
        }

        // Batch query ground Z for all sample points in one IPC call.
        (float groundZ, bool found)[] results;
        try
        {
            results = client.BatchGetGroundZ(mapId, samplePoints.ToArray());
        }
        catch
        {
            return null;
        }

        // Find the most practical walkable point near the pool (prefer points with LOS).
        // Fishing only needs a castable shoreline point. Prefer candidates that snap onto
        // a real walkable support surface near the ring around the pool, then bias toward
        // the player's current side and against lower shoreline shelves below the pier lip.
        Position? bestWithLos = null;
        float bestWithLosScore = float.MaxValue;
        Position? bestAny = null;
        float bestAnyScore = float.MaxValue;
        var resolvedCandidates = new List<Position>();

        for (var i = 0; i < results.Length; i++)
        {
            if (!results[i].found)
                continue;
            if (MathF.Abs(results[i].groundZ - referenceZ) > maxGroundZDelta)
                continue;

            // Start from the sampled support point, then snap to the nearest walkable edge
            // when the pathfinding service can resolve one. This shifts the approach chooser
            // away from arbitrary terrain samples and toward the actual pier/shore walkable lip.
            var rawCandidate = new Position(samplePoints[i].X, samplePoints[i].Y, results[i].groundZ);
            var candidate = ResolveFishingApproachCandidate(mapId, rawCandidate, referenceZ);
            if (candidate == null)
                continue;

            MergeFishingApproachCandidate(resolvedCandidates, candidate);
        }

        foreach (var candidate in resolvedCandidates)
        {
            if (IsRejectedApproachPosition(candidate))
                continue;

            var distToPool = poolPosition.DistanceTo(candidate);
            var score = ScoreFishingApproachCandidate(playerPosition, poolPosition, candidate, distToPool, referenceZ);

            if (score < bestAnyScore)
            {
                bestAny = candidate;
                bestAnyScore = score;
            }

            if (score < bestWithLosScore && CanCastFromPosition(mapId, candidate, poolPosition))
            {
                bestWithLos = candidate;
                bestWithLosScore = score;
            }
        }

        return bestWithLos ?? bestAny;
    }

    private Position? ResolveFishingApproachCandidate(uint mapId, Position rawCandidate, float referenceZ)
    {
        var client = Container.PathfindingClient;
        if (client == null)
            return rawCandidate;

        try
        {
            var nearestPoint = TrySnapToReferenceLayer(
                mapId,
                rawCandidate,
                FishingApproachWalkableSnapRadius,
                referenceZ,
                SearchWaypointSnapMaxZDelta);
            if (nearestPoint == null)
                return null;

            return nearestPoint;
        }
        catch (Exception ex)
        {
            Logger.LogDebug(ex, "[FISH] Fishing approach walkable snap failed; using raw shoreline candidate.");
            return rawCandidate;
        }
    }

    private static void MergeFishingApproachCandidate(List<Position> resolvedCandidates, Position candidate)
    {
        for (var i = 0; i < resolvedCandidates.Count; i++)
        {
            if (resolvedCandidates[i].DistanceTo2D(candidate) > FishingApproachCandidateDedupRadius)
                continue;

            if (candidate.Z > resolvedCandidates[i].Z)
                resolvedCandidates[i] = candidate;

            return;
        }

        resolvedCandidates.Add(candidate);
    }

    private static float ScoreFishingApproachCandidate(Position playerPosition, Position poolPosition, Position candidate, float distToPool, float referenceZ)
    {
        var distToPlayer = playerPosition.DistanceTo2D(candidate);
        var poolDistancePenalty = MathF.Abs(distToPool - DesiredPoolDistance);
        var oppositeSidePenalty = IsOppositeSideOfPool(playerPosition, poolPosition, candidate) ? 15f : 0f;
        var lowerSurfacePenalty = MathF.Max(0f, referenceZ - candidate.Z) * FishingApproachLowerSurfacePenaltyScale;
        return distToPlayer + (poolDistancePenalty * 1.5f) + oppositeSidePenalty + lowerSurfacePenalty;
    }

    private static bool IsOppositeSideOfPool(Position playerPosition, Position poolPosition, Position candidate)
    {
        var playerDx = playerPosition.X - poolPosition.X;
        var playerDy = playerPosition.Y - poolPosition.Y;
        var candidateDx = candidate.X - poolPosition.X;
        var candidateDy = candidate.Y - poolPosition.Y;
        return ((playerDx * candidateDx) + (playerDy * candidateDy)) < 0f;
    }

    private bool CanDirectSearchWalkFallback(uint mapId, Position playerPosition, Position waypoint)
    {
        if (playerPosition.DistanceTo2D(waypoint) > SearchWaypointDirectFallbackRadius)
            return false;

        if (MathF.Abs(playerPosition.Z - waypoint.Z) > SearchWaypointDirectFallbackMaxZDelta)
            return false;

        // Direct walking bypasses the navmesh. It's only safe when the line-of-sight probe
        // can actually verify the segment. Without reliable local physics (FG) the LOS probe
        // returns "clear" unconditionally, which would let the bot walk straight off a pier
        // lip because Z equality doesn't imply walkable ground. In that case, require a real
        // navmesh path instead of trusting the Z+distance heuristic.
        if (!SupportsNativeLocalPhysicsQueries)
            return false;

        return TryHasLineOfSight(mapId, playerPosition, waypoint);
    }

    private Position ResolveSearchWaypoint(uint mapId, Position playerPosition, int waypointIndex, Position waypoint)
    {
        if (_resolvedSearchWaypoints.TryGetValue(waypointIndex, out var resolvedWaypoint))
            return resolvedWaypoint;

        var resolvedCandidate = FindBestSearchWaypointCandidate(mapId, playerPosition, waypoint)
            ?? ResolveSearchWaypointCandidate(mapId, waypoint, waypoint.Z)
            ?? waypoint;
        _resolvedSearchWaypoints[waypointIndex] = resolvedCandidate;
        return resolvedCandidate;
    }

    private (Position target, Position[]? path) ResolveSearchWaypointTravelTarget(uint mapId, Position playerPosition, Position waypoint)
    {
        Position? fallbackCandidate = null;
        Position[]? fallbackPath = null;
        foreach (var stride in EnumerateSearchWaypointTravelStrides(playerPosition.DistanceTo2D(waypoint)))
        {
            var steppedWaypoint = BuildSearchWaypointTravelStep(playerPosition, waypoint, stride);
            var candidate = FindBestSearchWaypointCandidate(mapId, playerPosition, steppedWaypoint)
                ?? ResolveSearchWaypointCandidate(mapId, steppedWaypoint, waypoint.Z)
                ?? steppedWaypoint;

            if (!IsMeaningfulSearchWaypointTravelCandidate(playerPosition, waypoint, candidate))
                continue;

            fallbackCandidate ??= candidate;
            var path = TryGetReachableSearchWaypointPath(mapId, playerPosition, candidate);
            fallbackPath ??= path;
            if (path != null)
                return (candidate, path);
        }

        return (fallbackCandidate ?? waypoint, fallbackPath);
    }

    private static IEnumerable<float> EnumerateSearchWaypointTravelStrides(float waypointDistance2D)
    {
        if (waypointDistance2D <= 0.01f)
            yield break;

        var emitted = new HashSet<int>();
        foreach (var stride in new[]
        {
            MathF.Min(waypointDistance2D, SearchWaypointTravelStride),
            MathF.Min(waypointDistance2D, SearchWaypointTravelStride * 0.5f),
            MathF.Min(waypointDistance2D, SearchWaypointTravelStride * 0.25f),
            MathF.Min(waypointDistance2D, SearchWaypointMicroStride),
            waypointDistance2D
        })
        {
            var key = (int)MathF.Round(stride * 100f);
            if (stride <= 0.01f || !emitted.Add(key))
                continue;

            yield return stride;
        }
    }

    private static Position BuildSearchWaypointTravelStep(Position playerPosition, Position waypoint, float stride)
    {
        var waypointDistance2D = playerPosition.DistanceTo2D(waypoint);
        if (waypointDistance2D <= Math.Max(stride, 0.01f))
            return waypoint;

        var stepScale = stride / Math.Max(waypointDistance2D, 0.01f);
        return new Position(
            playerPosition.X + ((waypoint.X - playerPosition.X) * stepScale),
            playerPosition.Y + ((waypoint.Y - playerPosition.Y) * stepScale),
            waypoint.Z);
    }

    private static bool CanTreatSearchWaypointAsArrived(Position playerPosition, Position waypoint, float distanceToWaypoint)
    {
        if (distanceToWaypoint > SearchWaypointArrivalRadius)
            return false;

        return MathF.Abs(playerPosition.Z - waypoint.Z) <= SearchWaypointArrivalMaxZDelta;
    }

    private Position[]? TryGetReachableSearchWaypointPath(uint mapId, Position playerPosition, Position candidate)
    {
        var client = Container.PathfindingClient;
        if (client == null)
            return [playerPosition, candidate];

        try
        {
            var path = client.GetPath(mapId, playerPosition, candidate, smoothPath: false);
            if (path.Length == 0)
                return null;

            if (!IsUsableSearchWaypointPath(playerPosition, candidate, path))
                return null;

            // Search-walk probes rely on pathfinding to provide a real local corridor
            // around pier lips and shoreline props. A trivial start->end segment is
            // only safe when that straight probe is actually clear on the current layer.
            if (path.Length <= 2 && !CanSearchWaypointStraightProbePath(mapId, playerPosition, candidate))
                return null;

            return path;
        }
        catch (Exception ex)
        {
            Logger.LogDebug(ex, "[FISH] Search waypoint reachability probe failed; treating candidate as usable.");
            return [playerPosition, candidate];
        }
    }

    private bool TryFollowSearchWaypointPath(Position playerPosition, Position rawWaypoint, Position resolvedWaypoint, Position travelTarget, Position[]? path)
    {
        var nextWaypoint = BotRunnerService.ResolveNextWaypoint(path);
        if (nextWaypoint == null)
            return false;

        if (!IsMeaningfulSearchWaypointTravelCandidate(playerPosition, resolvedWaypoint, nextWaypoint))
            return false;

        RecordSearchWaypointDiagnostic(rawWaypoint, resolvedWaypoint, travelTarget, nextWaypoint, "path", path?.Length ?? 0);
        ClearNavigation();
        ObjectManager.MoveToward(nextWaypoint);

        return true;
    }

    private bool CanSearchWaypointStraightProbePath(uint mapId, Position playerPosition, Position candidate)
    {
        if (MathF.Abs(playerPosition.Z - candidate.Z) > SearchWaypointDirectFallbackMaxZDelta)
            return false;

        // Same reasoning as CanDirectSearchWalkFallback: a 2-node navmesh path is only safe
        // to follow without intermediate nodes when LOS can actually verify the segment.
        // FG returns true from TryHasLineOfSight even when the physics can't check, so fall
        // through to full navmesh routing there instead of accepting the straight probe.
        if (!SupportsNativeLocalPhysicsQueries)
            return false;

        return TryHasLineOfSight(mapId, playerPosition, candidate);
    }

    private static bool IsMeaningfulSearchWaypointTravelCandidate(Position playerPosition, Position waypoint, Position candidate)
    {
        var waypointDistance2D = playerPosition.DistanceTo2D(waypoint);
        if (waypointDistance2D <= SearchWaypointArrivalRadius)
            return true;

        var candidateDistance2D = playerPosition.DistanceTo2D(candidate);
        if (candidateDistance2D <= 0.01f)
            return false;

        var desiredX = waypoint.X - playerPosition.X;
        var desiredY = waypoint.Y - playerPosition.Y;
        var desiredLength = MathF.Sqrt((desiredX * desiredX) + (desiredY * desiredY));
        if (desiredLength <= 0.01f)
            return candidateDistance2D >= SearchWaypointMinForwardProgress;

        var candidateX = candidate.X - playerPosition.X;
        var candidateY = candidate.Y - playerPosition.Y;
        var forwardProgress = ((candidateX * desiredX) + (candidateY * desiredY)) / desiredLength;
        var minRequiredForwardProgress = candidateDistance2D <= (SearchWaypointMicroStride + 0.25f)
            ? SearchWaypointMicroStrideMinForwardProgress
            : SearchWaypointMinForwardProgress;
        return forwardProgress >= minRequiredForwardProgress;
    }

    private static bool IsUsableSearchWaypointPath(Position playerPosition, Position candidate, Position[] path)
    {
        if (path.Length == 0)
            return false;

        var minPathZ = path.Min(position => position.Z);
        var maxPathZ = path.Max(position => position.Z);

        // Search-walk probes are only for streaming nearby pools from the current pier/dock layer.
        // Reject probe routes that dive off the current support surface or require a steep climb back.
        if ((playerPosition.Z - minPathZ) > SearchWaypointRouteMaxDropZ
            || (maxPathZ - playerPosition.Z) > SearchWaypointRouteMaxRiseZ)
        {
            return false;
        }

        var directDistance2D = MathF.Max(playerPosition.DistanceTo2D(candidate), 0.01f);
        var maxAllowedRadius2D = directDistance2D + SearchWaypointRouteMaxExtraDetourDistance;
        var maxPointDistance2D = path.Max(position => playerPosition.DistanceTo2D(position));
        if (maxPointDistance2D > maxAllowedRadius2D)
            return false;

        var totalPathDistance2D = 0f;
        for (var i = 1; i < path.Length; i++)
            totalPathDistance2D += path[i - 1].DistanceTo2D(path[i]);

        var maxAllowedPathDistance2D = MathF.Max(
            maxAllowedRadius2D,
            directDistance2D * SearchWaypointRouteMaxDetourMultiplier);
        return totalPathDistance2D <= maxAllowedPathDistance2D;
    }

    private Position? FindBestSearchWaypointCandidate(uint mapId, Position playerPosition, Position waypoint)
    {
        var client = Container.PathfindingClient;
        if (client == null || !SupportsNativeLocalPhysicsQueries)
            return null;

        var samplePoints = new List<Position> { waypoint };
        var ringCount = (int)(SearchWaypointRefineRadius / SearchWaypointRefineStep);
        for (var ring = 1; ring <= ringCount; ring++)
        {
            var radius = ring * SearchWaypointRefineStep;
            for (var angleIndex = 0; angleIndex < SearchWaypointRefineAngleSteps; angleIndex++)
            {
                var angle = angleIndex * (2f * MathF.PI / SearchWaypointRefineAngleSteps);
                samplePoints.Add(new Position(
                    waypoint.X + (MathF.Cos(angle) * radius),
                    waypoint.Y + (MathF.Sin(angle) * radius),
                    waypoint.Z));
            }
        }

        (float groundZ, bool found)[]? results;
        try
        {
            results = client.BatchGetGroundZ(mapId, samplePoints.ToArray());
        }
        catch (Exception ex)
        {
            Logger.LogDebug(ex, "[FISH] Search waypoint probe refinement failed; falling back to direct walkable snap.");
            return null;
        }

        if (results == null || results.Length != samplePoints.Count)
            return null;

        Position? bestWithLos = null;
        float bestWithLosScore = float.MaxValue;
        Position? bestAny = null;
        float bestAnyScore = float.MaxValue;
        var resolvedCandidates = new List<Position>();

        for (var i = 0; i < results.Length; i++)
        {
            if (!results[i].found)
                continue;
            if (MathF.Abs(results[i].groundZ - waypoint.Z) > SearchWaypointSnapMaxZDelta)
                continue;

            var rawCandidate = new Position(samplePoints[i].X, samplePoints[i].Y, results[i].groundZ);
            var candidate = ResolveSearchWaypointCandidate(mapId, rawCandidate, waypoint.Z);
            if (candidate == null)
                continue;

            MergeFishingApproachCandidate(resolvedCandidates, candidate);
        }

        foreach (var candidate in resolvedCandidates)
        {
            var score = ScoreSearchWaypointCandidate(playerPosition, waypoint, candidate);
            if (score < bestAnyScore)
            {
                bestAny = candidate;
                bestAnyScore = score;
            }

            if (score < bestWithLosScore && TryHasLineOfSight(mapId, playerPosition, candidate))
            {
                bestWithLos = candidate;
                bestWithLosScore = score;
            }
        }

        return bestWithLos ?? bestAny;
    }

    private Position? ResolveSearchWaypointCandidate(uint mapId, Position rawCandidate, float referenceZ)
    {
        var client = Container.PathfindingClient;
        if (client == null)
            return rawCandidate;

        try
        {
            var nearestPoint = TrySnapToReferenceLayer(
                mapId,
                rawCandidate,
                SearchWaypointSnapRadius,
                referenceZ,
                SearchWaypointSnapMaxZDelta);
            if (nearestPoint == null)
                return null;

            return nearestPoint;
        }
        catch (Exception ex)
        {
            Logger.LogDebug(ex, "[FISH] Search waypoint snap failed; using raw probe target.");
            return rawCandidate;
        }
    }

    private Position? TrySnapToReferenceLayer(
        uint mapId,
        Position rawCandidate,
        float searchRadius,
        float referenceZ,
        float maxZDelta)
    {
        var client = Container.PathfindingClient;
        if (client == null)
            return rawCandidate;

        var referenceLayerQuery = new Position(rawCandidate.X, rawCandidate.Y, referenceZ);
        var (areaType, nearestPoint) = client.FindNearestWalkablePoint(mapId, referenceLayerQuery, searchRadius);
        if (areaType == 0 || areaType == 6 || nearestPoint == null)
        {
            (areaType, nearestPoint) = client.FindNearestWalkablePoint(mapId, rawCandidate, searchRadius);
            if (areaType == 0 || areaType == 6 || nearestPoint == null)
                return null;
        }

        if (rawCandidate.DistanceTo2D(nearestPoint) > searchRadius)
            return null;

        if (MathF.Abs(nearestPoint.Z - referenceZ) > maxZDelta)
            return null;

        return nearestPoint;
    }

    private static float ScoreSearchWaypointCandidate(Position playerPosition, Position waypoint, Position candidate)
    {
        var distToPlayer = playerPosition.DistanceTo2D(candidate);
        var distToProbe = waypoint.DistanceTo2D(candidate);
        var oppositeSidePenalty = IsOppositeSideOfPool(playerPosition, waypoint, candidate) ? 10f : 0f;
        var lowerSurfacePenalty = MathF.Max(0f, waypoint.Z - candidate.Z) * SearchWaypointProbeLowerSurfacePenaltyScale;
        return distToPlayer + (distToProbe * 1.5f) + oppositeSidePenalty + lowerSurfacePenalty;
    }

    private void AdvanceSearchWaypoint()
    {
        _searchWaypointIndex++;
        ResetSearchWaypointDiagnostic();
        BeginSearchWaypointWindow();
    }

    private void BeginSearchWaypointWindow(DateTime? now = null)
    {
        _searchWaypointEnteredAt = now ?? DateTime.UtcNow;
        _searchWaypointResetDistance = float.MaxValue;
    }

    private void ResetSearchWaypointDiagnostic()
    {
        _searchWaypointRawDiagnostic = null;
        _searchWaypointResolvedDiagnostic = null;
        _searchWaypointTravelTargetDiagnostic = null;
        _searchWaypointPathHeadDiagnostic = null;
        _searchWaypointPathNodesDiagnostic = 0;
        _searchWaypointTravelModeDiagnostic = "none";
    }

    private void RecordSearchWaypointDiagnostic(
        Position? rawWaypoint,
        Position? resolvedWaypoint,
        Position? travelTarget,
        Position? pathHeadWaypoint,
        string travelMode,
        int pathNodes = 0)
    {
        _searchWaypointRawDiagnostic = rawWaypoint;
        _searchWaypointResolvedDiagnostic = resolvedWaypoint;
        _searchWaypointTravelTargetDiagnostic = travelTarget;
        _searchWaypointPathHeadDiagnostic = pathHeadWaypoint;
        _searchWaypointPathNodesDiagnostic = pathNodes;
        _searchWaypointTravelModeDiagnostic = string.IsNullOrWhiteSpace(travelMode) ? "none" : travelMode;
    }

    private string BuildSearchWaypointDiagnosticDetail(Position playerPosition)
    {
        return
            $"player={FormatDiagnosticPosition(playerPosition)} " +
            $"raw={FormatDiagnosticPosition(_searchWaypointRawDiagnostic)} " +
            $"resolved={FormatDiagnosticPosition(_searchWaypointResolvedDiagnostic)} " +
            $"target={FormatDiagnosticPosition(_searchWaypointTravelTargetDiagnostic)} " +
            $"pathHead={FormatDiagnosticPosition(_searchWaypointPathHeadDiagnostic)} " +
            $"pathNodes={_searchWaypointPathNodesDiagnostic} mode={_searchWaypointTravelModeDiagnostic}";
    }

    private static string FormatDiagnosticPosition(Position? position)
        => position == null
            ? "none"
            : $"({position.X:F1},{position.Y:F1},{position.Z:F1})";

    private bool SupportsNativeLocalPhysicsQueries
        => LocalPhysicsSupport.SupportsReliableQueries(ObjectManager);

    private bool CanCastFromPosition(uint mapId, Position fromPosition, Position poolPosition)
    {
        var castTarget = FishingData.GetPoolCastTarget(fromPosition, poolPosition, CastTargetInsetFromPool);

        // Fishing pools (especially on FG) can report Z=0 from memory reads even when the pool
        // is at water surface. If the cast target Z is far below the player, use the player's Z
        // instead — fishing casts go roughly horizontal to the water surface, not downward into
        // the terrain. Without this, the LOS ray pierces the dock and returns blocked forever.
        if (fromPosition.Z - castTarget.Z > 3f)
            castTarget = new Position(castTarget.X, castTarget.Y, fromPosition.Z);

        // Raise both endpoints by eye height so the LOS ray clears the dock surface geometry.
        // Without this, a ray at exact dock Z clips through the dock plank collision mesh.
        const float eyeHeight = 1.8f;
        var losFrom = new Position(fromPosition.X, fromPosition.Y, fromPosition.Z + eyeHeight);
        var losTo = new Position(castTarget.X, castTarget.Y, castTarget.Z + eyeHeight);
        return TryHasLineOfSight(mapId, losFrom, losTo);
    }

    private bool TryHasLineOfSight(uint mapId, Position fromPosition, Position toPosition)
    {
        if (!SupportsNativeLocalPhysicsQueries)
            return true;

        try
        {
            return WoWSharpClient.Movement.NativeLocalPhysics.LineOfSight(
                mapId,
                fromPosition.X, fromPosition.Y, fromPosition.Z,
                toPosition.X, toPosition.Y, toPosition.Z);
        }
        catch (Exception ex)
        {
            Logger.LogDebug(ex, "[FISH] LOS probe failed; treating native LOS as unavailable.");
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
        Logger.LogInformation("[FISH] LOS blocked for fishing {Phase}. castTarget=({X:F1}, {Y:F1}, {Z:F1}) poolZ={PoolZ:F1} playerZ={PlayerZ:F1}",
            phase, castTarget.X, castTarget.Y, castTarget.Z, poolPosition.Z, player.Position.Z);
    }

    private void PopWithSuccess()
    {
        var lootItems = _lootItemIds.Count > 0 ? string.Join(", ", _lootItemIds.OrderBy(id => id)) : "none";
        BotContext.AddDiagnosticMessage(
            $"[TASK] FishingTask fishing_loot_success lootWindowSeen={_sawLootWindow} lootItemSeen={_sawLootItem} bobberSeen={_sawBobber} lootItems=[{lootItems}]");
        Logger.LogInformation("[FISH] Fishing catch completed. lootWindowSeen={LootWindowSeen} lootItemSeen={LootItemSeen} bobberSeen={BobberSeen} lootItems=[{LootItems}]",
            _sawLootWindow, _sawLootItem, _sawBobber, lootItems);
        PopFishingTask("fishing_loot_success");
    }

    private void ClearCastPositionCache()
    {
        _cachedCastPosition = null;
        _cachedCastPoolGuid = 0;
        _reachedApproachLevelForActivePool = false;
    }

    private static IReadOnlyList<Position> BuildDefaultSearchWaypoints(Position center)
    {
        // 8-direction radial sweep at ~28y. The bot walks toward each in turn
        // until a fishing pool enters streaming range. This kicks in when a
        // named teleport (e.g. ".tele Ratchet") drops the bot at a landmark
        // that doesn't have an immediately visible fishing pool, so we don't
        // hardcode pier coordinates per location.
        const float radius = 28f;
        var waypoints = new Position[8];
        for (var i = 0; i < waypoints.Length; i++)
        {
            var angle = (i / (float)waypoints.Length) * MathF.PI * 2f;
            waypoints[i] = new Position(
                center.X + (MathF.Cos(angle) * radius),
                center.Y + (MathF.Sin(angle) * radius),
                center.Z);
        }
        return waypoints;
    }

    private void PopFishingTask(string reason)
    {
        ClearCastPositionCache();
        ReleaseInventoryErrorHandler();
        PopTask(reason);
    }

    private static bool IsInCastingWindow(Position playerPosition, Position poolPosition)
    {
        var distance = playerPosition.DistanceTo(poolPosition);
        return distance >= MinCastingDistance
            && distance <= MaxCastingDistance;
    }

    private void TrackActivePool(ulong poolGuid)
    {
        if (_activePoolGuid == poolGuid)
            return;

        _activePoolGuid = poolGuid;
        _cachedApproachPosition = null;
        _cachedApproachPoolGuid = 0;
        ClearCastPositionCache();
        _failedApproachPoolGuid = poolGuid;
        _failedApproachPositions.Clear();
    }

    private void ObserveApproachProgress(float distanceToApproach)
    {
        if (_approachProgressDistance == float.MaxValue
            || distanceToApproach <= _approachProgressDistance - ApproachProgressResetDistance)
        {
            _approachProgressDistance = distanceToApproach;
            _approachProgressAt = DateTime.UtcNow;
        }
    }

    private void RememberFailedApproachPosition(string reason)
    {
        var playerPosition = ObjectManager.Player?.Position;
        if (playerPosition == null)
            return;

        RememberRejectedApproachPosition(playerPosition, reason);
    }

    private void RememberRejectedApproachPosition(Position position, string reason)
    {
        if (_activePoolGuid == 0)
            return;

        if (_failedApproachPoolGuid != _activePoolGuid)
        {
            _failedApproachPoolGuid = _activePoolGuid;
            _failedApproachPositions.Clear();
        }

        if (_failedApproachPositions.Any(rejected => rejected.DistanceTo2D(position) <= FailedApproachRejectRadius))
            return;

        _failedApproachPositions.Add(new Position(position.X, position.Y, position.Z));
        BotContext.AddDiagnosticMessage(
            $"[TASK] FishingTask reject_approach guid=0x{_activePoolGuid:X} reason={reason} pos=({position.X:F1},{position.Y:F1},{position.Z:F1})");
    }

    private bool IsRejectedApproachPosition(Position position)
        => _activePoolGuid != 0
            && _failedApproachPoolGuid == _activePoolGuid
            && _failedApproachPositions.Any(rejected => rejected.DistanceTo2D(position) <= FailedApproachRejectRadius);

    private void SetState(FishingState state)
    {
        if ((_state == FishingState.MoveToFishingPool || _state == FishingState.SearchForPool)
            && state != FishingState.MoveToFishingPool && state != FishingState.SearchForPool)
            ClearNavigation();

        if (state == FishingState.MoveToFishingPool)
        {
            _lastApproachDiagnosticDistance = float.MaxValue;
            _approachProgressAt = DateTime.UtcNow;
            _approachProgressDistance = float.MaxValue;
        }

        _castPreparationIssued = false;
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
