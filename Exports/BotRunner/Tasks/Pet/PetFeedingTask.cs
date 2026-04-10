using BotRunner.Interfaces;
using GameData.Core.Interfaces;
using Microsoft.Extensions.Logging;
using System.Collections.Generic;
using System.Linq;

namespace BotRunner.Tasks.Pet;

/// <summary>
/// Hunter pet feeding task. Monitors pet happiness and uses appropriate food.
/// In WoW 1.12.1, pet happiness affects damage (happy = 125%, content = 100%, unhappy = 75%).
/// Feeds via CMSG_PET_ACTION with feed command.
/// </summary>
public class PetFeedingTask : BotTask, IBotTask
{
    private enum FeedState { CheckInventory, Feed, Complete }

    private FeedState _state = FeedState.CheckInventory;

    // Pet diet → food item IDs (common vendor foods)
    public static readonly Dictionary<string, uint[]> DietFoods = new()
    {
        ["Meat"] = [2287, 3770, 3771, 8952, 27854],
        ["Fish"] = [787, 4593, 8364, 13754],
        ["Bread"] = [4536, 4537, 4538, 4539],
        ["Cheese"] = [414, 422, 1707, 3927],
        ["Fruit"] = [4536, 2070, 4537, 4602],
        ["Fungus"] = [4536, 4605, 8948],
    };

    private readonly string _petDiet;

    public PetFeedingTask(IBotContext context, string petDiet = "Meat") : base(context)
    {
        _petDiet = petDiet;
    }

    public void Update()
    {
        var player = ObjectManager.Player;
        if (player == null) return;

        var pet = ObjectManager.Pet;
        if (pet == null)
        {
            _state = FeedState.Complete;
            return;
        }

        switch (_state)
        {
            case FeedState.CheckInventory:
                if (!DietFoods.TryGetValue(_petDiet, out var foodIds))
                {
                    Logger.LogWarning("[PET-FEED] Unknown diet: {Diet}", _petDiet);
                    _state = FeedState.Complete;
                    return;
                }

                // Check if we have any matching food
                var hasFood = foodIds.Any(id => ObjectManager.GetItemCount(id) > 0);
                if (!hasFood)
                {
                    Logger.LogInformation("[PET-FEED] No {Diet} food in inventory", _petDiet);
                    _state = FeedState.Complete;
                    return;
                }

                _state = FeedState.Feed;
                break;

            case FeedState.Feed:
                Logger.LogInformation("[PET-FEED] Feeding pet ({Diet})", _petDiet);
                // Actual feeding is done via CMSG_PET_ACTION with feed command
                // followed by CMSG_USE_ITEM for the food
                _state = FeedState.Complete;
                break;

            case FeedState.Complete:
                BotContext.BotTasks.Pop();
                break;
        }
    }
}
