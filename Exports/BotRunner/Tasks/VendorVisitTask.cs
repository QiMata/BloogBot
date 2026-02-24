using BotRunner.Combat;
using BotRunner.Interfaces;
using GameData.Core.Enums;
using GameData.Core.Interfaces;
using GameData.Core.Models;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace BotRunner.Tasks;

/// <summary>
/// Task that walks to a vendor NPC, sells junk, repairs, then pops itself.
/// Uses IObjectManager.QuickVendorVisitAsync — FG via Lua, BG via packets.
/// </summary>
public class VendorVisitTask : BotTask, IBotTask
{
    private readonly Position _returnPosition;

    private enum VendorState { FindVendor, MoveToVendor, InteractVendor, VendorActions, ReturnToGrind, Done }
    private VendorState _state = VendorState.FindVendor;

    private IWoWUnit? _vendorUnit;
    private ulong _vendorGuid;
    private DateTime _lastAction = DateTime.MinValue;
    private DateTime _stateEnteredAt = DateTime.Now;
    private int _actionAttempts;
    private const int ACTION_COOLDOWN_MS = 1500;

    public VendorVisitTask(IBotContext botContext) : base(botContext)
    {
        _returnPosition = ObjectManager.Player?.Position ?? new Position(0, 0, 0);
    }

    public void Update()
    {
        var player = ObjectManager.Player;
        if (player?.Position == null)
        {
            Pop();
            return;
        }

        // Abort if in combat
        if (ObjectManager.Aggressors.Any())
        {
            Log.Information("[VENDOR] Combat detected, aborting vendor visit");
            ObjectManager.StopAllMovement();
            Pop();
            return;
        }

        // Timeout protection
        if ((DateTime.Now - _stateEnteredAt).TotalMilliseconds > Config.StuckTimeoutMs)
        {
            Log.Warning("[VENDOR] Timed out in {State}, aborting", _state);
            ObjectManager.StopAllMovement();
            Pop();
            return;
        }

        switch (_state)
        {
            case VendorState.FindVendor:
                FindVendor(player);
                break;
            case VendorState.MoveToVendor:
                MoveToVendor(player);
                break;
            case VendorState.InteractVendor:
                InteractVendor(player);
                break;
            case VendorState.VendorActions:
                DoVendorActions();
                break;
            case VendorState.ReturnToGrind:
                ReturnToGrind(player);
                break;
            case VendorState.Done:
                Pop();
                break;
        }
    }

    private void FindVendor(IWoWUnit player)
    {
        // Look for nearby vendor NPCs (prefer repair vendors)
        var repairVendor = ObjectManager.Units
            .Where(u => u.Health > 0
                && u.Position != null
                && (u.NpcFlags & NPCFlags.UNIT_NPC_FLAG_REPAIR) != 0)
            .OrderBy(u => player.Position.DistanceTo(u.Position))
            .FirstOrDefault();

        var generalVendor = ObjectManager.Units
            .Where(u => u.Health > 0
                && u.Position != null
                && (u.NpcFlags & NPCFlags.UNIT_NPC_FLAG_VENDOR) != 0)
            .OrderBy(u => player.Position.DistanceTo(u.Position))
            .FirstOrDefault();

        _vendorUnit = repairVendor ?? generalVendor;

        if (_vendorUnit == null)
        {
            Log.Warning("[VENDOR] No vendor found nearby, aborting");
            Pop();
            return;
        }

        _vendorGuid = _vendorUnit.Guid;
        var dist = player.Position.DistanceTo(_vendorUnit.Position);
        Log.Information("[VENDOR] Found vendor: {Name} ({Dist:F0}y away, Repair: {HasRepair})",
            _vendorUnit.Name, dist,
            (_vendorUnit.NpcFlags & NPCFlags.UNIT_NPC_FLAG_REPAIR) != 0);

        SetState(VendorState.MoveToVendor);
    }

    private void MoveToVendor(IWoWUnit player)
    {
        if (_vendorUnit == null || _vendorUnit.Position == null)
        {
            SetState(VendorState.FindVendor);
            return;
        }

        var dist = player.Position.DistanceTo(_vendorUnit.Position);

        if (dist <= Config.NpcInteractRange)
        {
            ObjectManager.StopAllMovement();
            SetState(VendorState.InteractVendor);
            return;
        }

        // Pathfind toward vendor (NavigationPath caches + throttles internally)
        NavigateToward(_vendorUnit.Position);
    }

    private void InteractVendor(IWoWUnit player)
    {
        if (!Wait.For("vendor_interact", ACTION_COOLDOWN_MS, true))
            return;

        _actionAttempts++;
        if (_actionAttempts > 5)
        {
            Log.Warning("[VENDOR] Failed to interact with vendor after {Attempts} attempts", _actionAttempts);
            Pop();
            return;
        }

        // Target the vendor and interact
        ObjectManager.SetTarget(_vendorGuid);
        SetState(VendorState.VendorActions);
    }

    private void DoVendorActions()
    {
        if (!Wait.For("vendor_action", ACTION_COOLDOWN_MS, true))
            return;

        try
        {
            // Determine consumables to buy based on player level and current inventory
            Dictionary<uint, uint>? itemsToBuy = null;
            var player = ObjectManager.Player;
            if (player != null)
            {
                bool usesMana = player.MaxMana > 0;
                var playerClass = (player as IWoWPlayer)?.Class ?? GameData.Core.Enums.Class.Warrior;
                itemsToBuy = ConsumableData.GetConsumablesToBuy(ObjectManager, player.Level, usesMana, playerClass);

                if (itemsToBuy.Count > 0)
                {
                    foreach (var kvp in itemsToBuy)
                        Log.Information("[VENDOR] Will buy {Qty}x item {ItemId}", kvp.Value, kvp.Key);
                }
            }

            // Sell junk, repair, and buy consumables via IObjectManager
            ObjectManager.QuickVendorVisitAsync(_vendorGuid, itemsToBuy, CancellationToken.None)
                .GetAwaiter().GetResult();

            Log.Information("[VENDOR] Vendor visit complete");
            SetState(VendorState.Done);
        }
        catch (Exception ex)
        {
            Log.Warning("[VENDOR] Vendor action failed: {Error}", ex.Message);
            _actionAttempts++;
            if (_actionAttempts > 3)
            {
                Log.Warning("[VENDOR] Too many failures, aborting");
                SetState(VendorState.Done);
            }
        }
    }

    private void ReturnToGrind(IWoWUnit player)
    {
        // Simply pop — returns to IdleTask (or whatever is below on the stack)
        ObjectManager.StopAllMovement();
        Pop();
    }

    private void SetState(VendorState newState)
    {
        if (_state != newState)
        {
            _state = newState;
            _stateEnteredAt = DateTime.Now;
            _actionAttempts = 0;
        }
    }

    private void Pop()
    {
        Wait.Remove("vendor_move");
        Wait.Remove("vendor_interact");
        Wait.Remove("vendor_action");
        BotTasks.Pop();
    }
}
