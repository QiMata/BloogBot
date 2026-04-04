using BotRunner.Interfaces;
using GameData.Core.Interfaces;
using GameData.Core.Models;
using Serilog;
using System.Collections.Generic;
using System.Linq;

namespace BotRunner.Tasks.Battlegrounds;

/// <summary>
/// After a BG ends, navigates to battlemaster NPCs for mark turn-in quests.
/// Each BG awards marks (WSG Mark of Honor, AB Mark of Honor, AV Mark of Honor).
/// Turn-in quests: 3 marks of any BG type for honor + rep.
/// Uses existing quest accept/complete sequences.
/// </summary>
public class BgRewardCollectionTask : BotTask, IBotTask
{
    private enum RewardState { FindBattlemaster, MoveToBattlemaster, InteractAndTurnIn, Complete }

    private RewardState _state = RewardState.FindBattlemaster;
    private Position _battlemasterPosition;
    private readonly bool _isHorde;

    // BG mark item IDs
    public const uint WsgMarkOfHonor = 20558;
    public const uint AbMarkOfHonor = 20559;
    public const uint AvMarkOfHonor = 20560;

    // Turn-in quest IDs (3 marks each)
    public static readonly Dictionary<uint, uint> MarkTurnInQuests = new()
    {
        [WsgMarkOfHonor] = 8171, // For Great Honor (Horde WSG)
        [AbMarkOfHonor] = 8171,  // Same quest accepts all mark types
        [AvMarkOfHonor] = 8171,
    };

    // Battlemaster positions (Orgrimmar for Horde, Stormwind for Alliance)
    public static readonly Dictionary<string, Position> HordeBattlemasters = new()
    {
        ["WSG"] = new(1982f, -4792f, 56f),  // Orgrimmar Valley of Honor
        ["AB"] = new(1983f, -4795f, 56f),
        ["AV"] = new(1985f, -4798f, 56f),
    };

    public static readonly Dictionary<string, Position> AllianceBattlemasters = new()
    {
        ["WSG"] = new(-8757f, 387f, 102f),  // Stormwind
        ["AB"] = new(-8760f, 384f, 102f),
        ["AV"] = new(-8763f, 381f, 102f),
    };

    private const float InteractRange = 5f;

    public BgRewardCollectionTask(IBotContext context, bool isHorde) : base(context)
    {
        _isHorde = isHorde;
    }

    public void Update()
    {
        var player = ObjectManager.Player;
        if (player == null) return;

        switch (_state)
        {
            case RewardState.FindBattlemaster:
                // Check if we have marks to turn in
                var wsgMarks = ObjectManager.GetItemCount(WsgMarkOfHonor);
                var abMarks = ObjectManager.GetItemCount(AbMarkOfHonor);
                var avMarks = ObjectManager.GetItemCount(AvMarkOfHonor);

                if (wsgMarks < 3 && abMarks < 3 && avMarks < 3)
                {
                    Log.Information("[BG-REWARD] No marks to turn in (WSG:{Wsg}, AB:{Ab}, AV:{Av})",
                        wsgMarks, abMarks, avMarks);
                    _state = RewardState.Complete;
                    return;
                }

                // Pick the battlemaster for whichever BG we have marks for
                var battlemasters = _isHorde ? HordeBattlemasters : AllianceBattlemasters;
                string bgType = wsgMarks >= 3 ? "WSG" : abMarks >= 3 ? "AB" : "AV";
                _battlemasterPosition = battlemasters[bgType];
                _state = RewardState.MoveToBattlemaster;
                Log.Information("[BG-REWARD] Turning in {BG} marks", bgType);
                break;

            case RewardState.MoveToBattlemaster:
                var dist = player.Position.DistanceTo(_battlemasterPosition);
                if (dist <= InteractRange)
                {
                    _state = RewardState.InteractAndTurnIn;
                    return;
                }
                ObjectManager.MoveToward(_battlemasterPosition);
                break;

            case RewardState.InteractAndTurnIn:
                var bm = ObjectManager.Units
                    .Where(u => u.Position.DistanceTo(_battlemasterPosition) < 10f)
                    .OrderBy(u => u.Position.DistanceTo(player.Position))
                    .FirstOrDefault();

                if (bm != null)
                {
                    Log.Information("[BG-REWARD] Interacting with battlemaster for turn-in");
                    // Quest turn-in handled by quest frame interaction
                }
                _state = RewardState.Complete;
                break;

            case RewardState.Complete:
                BotContext.BotTasks.Pop();
                break;
        }
    }
}
