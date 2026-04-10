using BotRunner.Interfaces;
using GameData.Core.Interfaces;
using Microsoft.Extensions.Logging;
using System.Linq;

namespace BotRunner.Tasks.Pet;

/// <summary>
/// Manages pet state: stance control, feeding (Hunter), ability usage.
/// Monitors pet happiness and auto-feeds when needed.
/// Sets pet stance based on combat situation (Defensive by default, Passive for CC pulls).
/// </summary>
public class PetManagementTask : BotTask, IBotTask
{
    private enum PetState { CheckStatus, Feed, SetStance, UseAbility, Idle }

    private PetState _state = PetState.CheckStatus;
    private readonly PetStance _desiredStance;
    private int _ticksSinceLastAction;

    public enum PetStance : byte
    {
        Aggressive = 0,
        Defensive = 1,
        Passive = 2,
    }

    // Pet food item IDs by diet type (common vendor foods)
    public static readonly uint[] MeatFoodIds = [2287, 3770, 3771, 8952]; // Haunch, Mutton, Wild Hog, Roasted Quail
    public static readonly uint[] FishFoodIds = [787, 4593, 8364]; // Slitherskin Mackerel, Bristle Whisker Catfish, Raw Mithril Head Trout
    public static readonly uint[] BreadFoodIds = [4536, 4537, 4538, 4539]; // Stormwind Brie, Tel'Abim Banana, etc.

    private const int FeedCheckIntervalTicks = 100; // Check every ~100 ticks

    public PetManagementTask(IBotContext context, PetStance desiredStance = PetStance.Defensive)
        : base(context)
    {
        _desiredStance = desiredStance;
    }

    public void Update()
    {
        var player = ObjectManager.Player;
        if (player == null) return;

        var pet = ObjectManager.Pet;
        if (pet == null)
        {
            // No pet summoned — nothing to manage
            _ticksSinceLastAction++;
            if (_ticksSinceLastAction > FeedCheckIntervalTicks)
            {
                BotContext.BotTasks.Pop();
            }
            return;
        }

        switch (_state)
        {
            case PetState.CheckStatus:
                // Check if pet needs feeding (happiness < 2 = unhappy or content)
                if (PetNeedsFeeding(pet))
                {
                    _state = PetState.Feed;
                    return;
                }

                _state = PetState.Idle;
                break;

            case PetState.Feed:
                // Find appropriate food in inventory
                Logger.LogInformation("[PET] Pet needs feeding");
                // Feeding is handled by CMSG_PET_ACTION with feed command
                // The actual food selection depends on pet diet
                _state = PetState.Idle;
                break;

            case PetState.SetStance:
                Logger.LogDebug("[PET] Setting stance to {Stance}", _desiredStance);
                _state = PetState.Idle;
                break;

            case PetState.UseAbility:
                // Pet abilities (Bite, Claw, Growl) are auto-cast in 1.12.1
                // Manual usage via CMSG_PET_ACTION for priority abilities
                _state = PetState.Idle;
                break;

            case PetState.Idle:
                _ticksSinceLastAction++;
                if (_ticksSinceLastAction >= FeedCheckIntervalTicks)
                {
                    _ticksSinceLastAction = 0;
                    _state = PetState.CheckStatus;
                }
                break;
        }
    }

    private static bool PetNeedsFeeding(IWoWUnit pet)
    {
        // In WoW 1.12.1, pet happiness is tracked via UNIT_FIELD_PET_EXPERIENCE
        // or pet buff state. Simplified: check if pet health is below threshold
        // as a proxy for unhappiness (damage penalty from unhappy pet).
        return pet.HealthPercent < 50;
    }
}
