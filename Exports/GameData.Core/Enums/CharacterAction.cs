namespace GameData.Core.Enums;

public enum CharacterAction
{
    Wait,               // Do nothing until a trigger changes state.
    GoTo,               // Move to a specified location.
    InteractWith,       // Interact with an NPC, object, or other elements.

    SelectGossip,       // Select a gossip option in a dialog.
    SelectTaxiNode,     // Select a taxi node to fly to.

    AcceptQuest,        // Accept a quest from an NPC.
    DeclineQuest,       // Decline a quest from an NPC.
    AbandonQuest,       // Abandon a quest from the quest log.
    SelectReward,       // Choose a reward upon completing a quest.
    CompleteQuest,      // Turn in a quest to an NPC.

    TrainSkill,         // Train a skill from a trainer.
    TrainTalent,        // Train a talent from a trainer.

    OfferTrade,         // Offer a trade with another player.
    OfferGold,          // Offer a certain amount of gold in a trade.
    OfferItem,          // Offer an item in a trade window.
    AcceptTrade,        // Accept the current trade offer.
    DeclineTrade,       // Decline the current trade offer.
    EnchantTrade,       // Enchant an item in a trade window.
    LockpickTrade,      // Picks a lock in a trade window.

    PromoteLeader,      // Promote a group member to leader.
    PromoteAssistant,   // Promote a group member to assistant.
    PromoteLootManager, // Promote a group member to loot manager.
    SetGroupLoot,       // Set the loot distribution method for a group.
    AssignLoot,         // Assign loot to a player in a group.
    LootRollNeed,       // Roll "Need" on a loot item.
    LootRollGreed,      // Roll "Greed" on a loot item.
    LootPass,           // Pass on a loot item.

    SendGroupInvite,    // Send an invitation to join a group.
    AcceptGroupInvite,  // Accept an invitation to join a group.
    DeclineGroupInvite, // Decline an invitation to join a group.
    KickPlayer,         // Kick a player from the group.
    LeaveGroup,         // Leave the current group.
    DisbandGroup,       // Disband the current group.

    StopAttack,         // Cease any ongoing attack.
    CastSpell,          // Cast or channel a spell on a target or location.
    StopCast,           // Stop casting a spell.

    UseItem,            // Use an item from inventory.
    EquipItem,          // Equip an item from the inventory.
    UnequipItem,        // Unequip an item from the character.
    DestroyItem,        // Destroy an item from the character.
    MoveItem,           // Move an item within the inventory.
    SplitStack,         // Unequip an item from the character.

    BuyItem,            // Purchase an item from a vendor.
    BuybackItem,        // Purchase a previously sold item from a vendor.
    SellItem,           // Sell an item to a vendor.
    RepairItem,         // Repair an item with a vendor.
    RepairAllItems,     // Repair all items with a vendor.

    DismissBuff,        // Dismiss a buff from the character.

    Resurrect,          // Accept a resurrection from another player or near corpse.

    Craft,              // Perform a crafting action.

    Login,              // Log in to the game world.
    Logout,             // Log out of the game world.
    CreateCharacter,    // Create a new character.
    DeleteCharacter,    // Delete an existing character.
    EnterWorld,         // Enter the game world with a character.
}