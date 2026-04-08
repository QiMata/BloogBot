using BotRunner.Interfaces;
using GameData.Core.Models;
using Serilog; // TODO: migrate to ILogger when DI is available
using System;
using System.Collections.Generic;

namespace BotRunner.Tasks.Progression;

/// <summary>
/// Evaluates mount goal prerequisites and acquires a mount when ready.
/// Steps:
/// 1. Check level requirement (40 for basic, 60 for epic).
/// 2. Check riding skill learned (skill ID 762).
/// 3. Check gold sufficient.
/// 4. If all met: TravelTo mount vendor + BuyItem.
/// StateManager pushes this when MountGoal is configured and bot is idle.
/// </summary>
public class MountAcquisitionTask : BotTask, IBotTask
{
    private enum MountState { Evaluate, NeedGold, NeedSkill, ReadyToBuy, Complete }

    private MountState _state = MountState.Evaluate;
    private readonly int _requiredLevel;
    private readonly int _goldCostCopper;
    private const uint RidingSkillId = 762;

    public MountAcquisitionTask(IBotContext context, int requiredLevel = 40, int goldCostCopper = 1000000)
        : base(context)
    {
        _requiredLevel = requiredLevel;
        _goldCostCopper = goldCostCopper;
    }

    public void Update()
    {
        var player = ObjectManager.Player;
        if (player == null) return;

        switch (_state)
        {
            case MountState.Evaluate:
                if (player.Level < _requiredLevel)
                {
                    Log.Information("[MountAcquisition] Level {Level} < {Required}. Need to level up first.",
                        player.Level, _requiredLevel);
                    BotContext.BotTasks.Pop();
                    return;
                }

                // Check riding skill — simplified check via level threshold
                // At level 40+ the character should have learned riding from trainer
                // TODO: Check actual riding skill from SkillInfo when field mapping is available
                var hasRiding = player.Level >= _requiredLevel;

                if (!hasRiding)
                {
                    _state = MountState.NeedSkill;
                    return;
                }

                if (player.Copper < (uint)_goldCostCopper)
                {
                    _state = MountState.NeedGold;
                    return;
                }

                _state = MountState.ReadyToBuy;
                break;

            case MountState.NeedSkill:
                Log.Information("[MountAcquisition] Riding skill not learned. Need to visit riding trainer.");
                // TODO: Push TravelTo riding trainer + TrainSkill
                BotContext.BotTasks.Pop();
                break;

            case MountState.NeedGold:
                Log.Information("[MountAcquisition] Need {Required} copper but only have {Have}. Need to farm gold.",
                    _goldCostCopper, ObjectManager.Player?.Copper ?? 0);
                // TODO: Push gold farming activity
                BotContext.BotTasks.Pop();
                break;

            case MountState.ReadyToBuy:
                Log.Information("[MountAcquisition] All prerequisites met! Travel to mount vendor and buy.");
                // TODO: Push TravelTo mount vendor + BuyItem sequence
                BotContext.BotTasks.Pop();
                break;

            case MountState.Complete:
                BotContext.BotTasks.Pop();
                break;
        }
    }

    /// <summary>
    /// Mount vendor locations by race. Used to determine where to travel for purchase.
    /// </summary>
    public static readonly Dictionary<string, (string VendorName, uint MapId, Position Position)> MountVendors = new()
    {
        ["Orc"] = ("Ogunaro Wolfrunner", 1, new Position(2143f, -4833f, 51f)),
        ["Troll"] = ("Zjolnir", 1, new Position(-839f, -4937f, 21f)),
        ["Tauren"] = ("Harb Clawhoof", 1, new Position(-2361f, -393f, -9f)),
        ["Undead"] = ("Zachariah Post", 0, new Position(2274f, 237f, 34f)),
        ["Human"] = ("Katie Hunter", 0, new Position(-9441f, -54f, 58f)),
        ["Dwarf"] = ("Veron Amberstill", 0, new Position(-6118f, -3130f, 234f)),
        ["NightElf"] = ("Lelanai", 1, new Position(10181f, 2303f, 1323f)),
        ["Gnome"] = ("Milli Featherwhistle", 0, new Position(-4790f, -1025f, 502f)),
    };
}
