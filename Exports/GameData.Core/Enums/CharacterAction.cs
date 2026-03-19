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

    StartMeleeAttack,   // Start melee auto-attack on a target.
    StartRangedAttack,  // Start ranged auto-attack (bow/gun/thrown) on a target.
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

    LootCorpse,         // Loot a nearby corpse by GUID.
    ReleaseCorpse,      // Release spirit (die → ghost form).
    RetrieveCorpse,     // Navigate to corpse and resurrect.
    SkinCorpse,         // Skin a lootable corpse by GUID.
    GatherNode,         // Gather a resource node (herb/ore) by GUID.
    SendChat,           // Send a chat message (used for GM commands).
    SetFacing,          // Set the character's facing orientation (radians).
    VisitVendor,        // Queue VendorVisitTask for nearby vendor automation.
    VisitTrainer,       // Queue TrainerVisitTask for nearby class-trainer automation.
    VisitFlightMaster,  // Queue FlightMasterVisitTask for nearby taxi-node discovery.
    StartFishing,       // Queue FishingTask to resolve fishing rank and wait for the fishing cycle.
    StartGatheringRoute,// Queue GatheringRouteTask to walk natural node coordinates and gather the first visible match.
    CheckMail,          // Open nearby mailbox, list mail, and take money/items from all pending mail.
    StartDungeoneering, // Queue DungeoneeringTask to navigate dungeon waypoints, pull encounters, and clear the dungeon.
    ConvertToRaid,      // Convert the current party to a raid group (leader only).
    ChangeRaidSubgroup, // Move a player to a specific raid subgroup (0-7). Params: string playerName, int subGroup.
}
