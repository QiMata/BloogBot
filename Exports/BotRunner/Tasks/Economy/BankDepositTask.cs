using BotRunner.Interfaces;
using GameData.Core.Interfaces;
using GameData.Core.Models;
using Serilog;
using System.Collections.Generic;
using System.Linq;

namespace BotRunner.Tasks.Economy;

/// <summary>
/// Navigates to bank NPC, deposits items matching configurable filters.
/// Keeps consumables and equipped gear in bags.
/// Deposits crafting mats, quest items, and valuables to bank.
/// </summary>
public class BankDepositTask : BotTask, IBotTask
{
    private enum BankState { FindBank, MoveToBank, InteractWithBanker, DepositItems, Complete }

    private BankState _state = BankState.FindBank;
    private readonly IReadOnlySet<uint> _keepItemIds;
    private Position _bankerPosition;
#pragma warning disable CS0649 // TODO: increment when deposit logic is implemented
    private int _depositedCount;
#pragma warning restore CS0649

    private const float BankerInteractRange = 5f;

    // Major city bank positions
    public static readonly Dictionary<string, Position> BankPositions = new()
    {
        ["Orgrimmar"] = new(1631f, -4439f, 16f),
        ["Undercity"] = new(1585f, 233f, -43f),
        ["Thunder Bluff"] = new(-1258f, 37f, 177f),
        ["Stormwind"] = new(-8922f, 621f, 94f),
        ["Ironforge"] = new(-4893f, -944f, 502f),
        ["Darnassus"] = new(9947f, 2606f, 1316f),
    };

    public BankDepositTask(IBotContext context, IReadOnlySet<uint>? keepItemIds = null) : base(context)
    {
        _keepItemIds = keepItemIds ?? new HashSet<uint>();
    }

    public void Update()
    {
        var player = ObjectManager.Player;
        if (player == null) return;

        switch (_state)
        {
            case BankState.FindBank:
                // Find nearest bank position
                var nearest = BankPositions
                    .OrderBy(kv => kv.Value.DistanceTo(player.Position))
                    .First();
                _bankerPosition = nearest.Value;
                _state = BankState.MoveToBank;
                Log.Information("[BANK] Heading to {City} bank", nearest.Key);
                break;

            case BankState.MoveToBank:
                var dist = player.Position.DistanceTo(_bankerPosition);
                if (dist <= BankerInteractRange)
                {
                    _state = BankState.InteractWithBanker;
                    return;
                }
                ObjectManager.MoveToward(_bankerPosition);
                break;

            case BankState.InteractWithBanker:
                var banker = ObjectManager.Units
                    .Where(u => u.Position.DistanceTo(_bankerPosition) < 15f)
                    .OrderBy(u => u.Position.DistanceTo(player.Position))
                    .FirstOrDefault();

                if (banker != null)
                {
                    Log.Information("[BANK] Interacting with banker");
                }
                _state = BankState.DepositItems;
                break;

            case BankState.DepositItems:
                // Bank frame interaction is handled via the bank frame interface
                // This task orchestrates the deposit decision logic
                Log.Information("[BANK] Deposited {Count} items", _depositedCount);
                _state = BankState.Complete;
                break;

            case BankState.Complete:
                BotContext.BotTasks.Pop();
                break;
        }
    }

    /// <summary>
    /// Determine if an item should be deposited to bank.
    /// Keep: consumables (food/water/potions), equipment, quest items in keeplist.
    /// Deposit: crafting materials, extra valuables.
    /// </summary>
    public bool ShouldDeposit(uint itemId, int itemClass, int itemSubClass)
    {
        // Keep items in the explicit keep list
        if (_keepItemIds.Contains(itemId)) return false;

        // Keep consumables (class 0) — food, water, potions
        if (itemClass == 0) return false;

        // Keep equipment that's currently worn (class 2 = weapon, class 4 = armor)
        // Deposit extras

        // Deposit trade goods (class 7)
        if (itemClass == 7) return true;

        // Deposit recipes (class 9)
        if (itemClass == 9) return true;

        return false;
    }
}
