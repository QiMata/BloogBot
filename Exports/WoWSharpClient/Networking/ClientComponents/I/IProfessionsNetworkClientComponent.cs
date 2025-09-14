namespace WoWSharpClient.Networking.ClientComponents.I
{
    /// <summary>
    /// Interface for handling profession operations in World of Warcraft.
    /// Manages profession skill training, crafting, and gathering interactions.
    /// </summary>
    public interface IProfessionsNetworkClientComponent : INetworkClientComponent
    {
        /// <summary>
        /// Gets a value indicating whether a profession trainer window is currently open.
        /// </summary>
        bool IsTrainerWindowOpen { get; }

        /// <summary>
        /// Gets a value indicating whether a crafting window is currently open.
        /// </summary>
        bool IsCraftingWindowOpen { get; }

        /// <summary>
        /// Gets the GUID of the currently open profession trainer, if any.
        /// </summary>
        ulong? CurrentTrainerGuid { get; }

        /// <summary>
        /// Gets the currently open profession type, if any.
        /// </summary>
        ProfessionType? CurrentProfession { get; }

        /// <summary>
        /// Event fired when a profession trainer window is opened.
        /// </summary>
        event Action<ulong, ProfessionType>? TrainerWindowOpened;

        /// <summary>
        /// Event fired when a profession trainer window is closed.
        /// </summary>
        event Action? TrainerWindowClosed;

        /// <summary>
        /// Event fired when a crafting window is opened.
        /// </summary>
        event Action<ProfessionType>? CraftingWindowOpened;

        /// <summary>
        /// Event fired when a crafting window is closed.
        /// </summary>
        event Action? CraftingWindowClosed;

        /// <summary>
        /// Event fired when a profession skill or recipe is successfully learned.
        /// </summary>
        /// <param name="spellId">The ID of the learned skill/recipe.</param>
        /// <param name="cost">The cost in copper.</param>
        event Action<uint, uint>? SkillLearned;

        /// <summary>
        /// Event fired when an item is successfully crafted.
        /// </summary>
        /// <param name="itemId">The ID of the crafted item.</param>
        /// <param name="quantity">The quantity crafted.</param>
        event Action<uint, uint>? ItemCrafted;

        /// <summary>
        /// Event fired when crafting fails.
        /// </summary>
        /// <param name="recipeId">The ID of the recipe that failed.</param>
        /// <param name="reason">The failure reason.</param>
        event Action<uint, string>? CraftingFailed;

        /// <summary>
        /// Event fired when profession trainer services (skills/recipes) are received from the server.
        /// </summary>
        /// <param name="availableSkills">The list of available skills/recipes that can be learned.</param>
        event Action<ProfessionService[]>? ProfessionServicesReceived;

        /// <summary>
        /// Event fired when a profession operation fails.
        /// </summary>
        /// <param name="error">The error message.</param>
        event Action<string>? ProfessionError;

        /// <summary>
        /// Event fired when gathering operation succeeds.
        /// </summary>
        /// <param name="nodeGuid">The GUID of the gathered node.</param>
        /// <param name="itemId">The ID of the gathered item.</param>
        /// <param name="quantity">The quantity gathered.</param>
        event Action<ulong, uint, uint>? ResourceGathered;

        /// <summary>
        /// Event fired when gathering operation fails.
        /// </summary>
        /// <param name="nodeGuid">The GUID of the node.</param>
        /// <param name="reason">The failure reason.</param>
        event Action<ulong, string>? GatheringFailed;

        /// <summary>
        /// Opens the profession trainer window by greeting the specified trainer NPC.
        /// Sends CMSG_GOSSIP_HELLO to initiate trainer interaction.
        /// </summary>
        /// <param name="trainerGuid">The GUID of the profession trainer NPC.</param>
        /// <param name="professionType">The expected profession type.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task OpenProfessionTrainerAsync(ulong trainerGuid, ProfessionType professionType, CancellationToken cancellationToken = default);

        /// <summary>
        /// Requests the profession trainer's available services (skills/recipes).
        /// Sends CMSG_TRAINER_LIST to get available training options.
        /// </summary>
        /// <param name="trainerGuid">The GUID of the profession trainer NPC.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task RequestProfessionServicesAsync(ulong trainerGuid, CancellationToken cancellationToken = default);

        /// <summary>
        /// Learns a profession skill or recipe from the trainer.
        /// Sends CMSG_TRAINER_BUY_SPELL to learn the specified skill.
        /// </summary>
        /// <param name="trainerGuid">The GUID of the profession trainer NPC.</param>
        /// <param name="spellId">The ID of the skill/recipe to learn.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task LearnProfessionSkillAsync(ulong trainerGuid, uint spellId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Learns multiple profession skills/recipes in sequence.
        /// This is a convenience method that calls LearnProfessionSkillAsync multiple times.
        /// </summary>
        /// <param name="trainerGuid">The GUID of the profession trainer NPC.</param>
        /// <param name="spellIds">The IDs of the skills/recipes to learn.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task LearnMultipleProfessionSkillsAsync(ulong trainerGuid, uint[] spellIds, CancellationToken cancellationToken = default);

        /// <summary>
        /// Closes the profession trainer window.
        /// Sends CMSG_GOSSIP_COMPLETE to close the trainer interaction.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task CloseProfessionTrainerAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Opens the crafting window for the specified profession.
        /// This method assumes the player has already learned the profession.
        /// </summary>
        /// <param name="professionType">The profession to open crafting for.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task OpenCraftingWindowAsync(ProfessionType professionType, CancellationToken cancellationToken = default);

        /// <summary>
        /// Crafts an item using the specified recipe.
        /// Sends craft packet to create the item.
        /// </summary>
        /// <param name="recipeId">The ID of the recipe to craft.</param>
        /// <param name="quantity">The number of items to craft (default: 1).</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task CraftItemAsync(uint recipeId, uint quantity = 1, CancellationToken cancellationToken = default);

        /// <summary>
        /// Crafts multiple items using different recipes.
        /// This is a convenience method for bulk crafting operations.
        /// </summary>
        /// <param name="craftingQueue">The list of recipes and quantities to craft.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task CraftMultipleItemsAsync(CraftingRequest[] craftingQueue, CancellationToken cancellationToken = default);

        /// <summary>
        /// Closes the crafting window.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task CloseCraftingWindowAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Gathers resources from a gathering node (herbs, mining nodes, etc.).
        /// Uses game object interaction to gather from the node.
        /// </summary>
        /// <param name="nodeGuid">The GUID of the node to gather from.</param>
        /// <param name="gatheringType">The type of gathering operation.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task GatherResourceAsync(ulong nodeGuid, GatheringType gatheringType, CancellationToken cancellationToken = default);

        /// <summary>
        /// Performs a quick profession training operation.
        /// Opens trainer, requests services, learns specified skills, and closes trainer.
        /// </summary>
        /// <param name="trainerGuid">The GUID of the profession trainer NPC.</param>
        /// <param name="professionType">The profession type.</param>
        /// <param name="skillIds">The skills to learn.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task QuickTrainProfessionSkillsAsync(ulong trainerGuid, ProfessionType professionType, uint[] skillIds, CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets the list of available profession services from the currently open trainer.
        /// </summary>
        /// <returns>Array of available profession services.</returns>
        ProfessionService[] GetAvailableProfessionServices();

        /// <summary>
        /// Gets the list of affordable profession services based on the specified money amount.
        /// </summary>
        /// <param name="availableMoney">The amount of money available in copper.</param>
        /// <returns>Array of affordable profession services.</returns>
        ProfessionService[] GetAffordableProfessionServices(uint availableMoney);

        /// <summary>
        /// Checks if a specific profession skill/recipe is available for learning.
        /// </summary>
        /// <param name="spellId">The ID of the skill/recipe to check.</param>
        /// <returns>True if the skill/recipe is available, false otherwise.</returns>
        bool IsProfessionSkillAvailable(uint spellId);

        /// <summary>
        /// Gets the cost of learning a specific profession skill/recipe.
        /// </summary>
        /// <param name="spellId">The ID of the skill/recipe.</param>
        /// <returns>The cost in copper, or 0 if not available.</returns>
        uint GetProfessionSkillCost(uint spellId);

        /// <summary>
        /// Checks if a specific recipe can be crafted based on available materials.
        /// </summary>
        /// <param name="recipeId">The ID of the recipe to check.</param>
        /// <returns>True if the recipe can be crafted, false otherwise.</returns>
        bool CanCraftRecipe(uint recipeId);

        /// <summary>
        /// Gets the materials required for a specific recipe.
        /// </summary>
        /// <param name="recipeId">The ID of the recipe.</param>
        /// <returns>Array of required materials.</returns>
        RecipeMaterial[] GetRecipeMaterials(uint recipeId);
    }

    /// <summary>
    /// Enumeration for profession types in World of Warcraft.
    /// </summary>
    public enum ProfessionType : uint
    {
        /// <summary>
        /// Alchemy profession.
        /// </summary>
        Alchemy = 171,

        /// <summary>
        /// Blacksmithing profession.
        /// </summary>
        Blacksmithing = 164,

        /// <summary>
        /// Enchanting profession.
        /// </summary>
        Enchanting = 333,

        /// <summary>
        /// Engineering profession.
        /// </summary>
        Engineering = 202,

        /// <summary>
        /// Herbalism profession (gathering).
        /// </summary>
        Herbalism = 182,

        /// <summary>
        /// Leatherworking profession.
        /// </summary>
        Leatherworking = 165,

        /// <summary>
        /// Mining profession (gathering).
        /// </summary>
        Mining = 186,

        /// <summary>
        /// Skinning profession (gathering).
        /// </summary>
        Skinning = 393,

        /// <summary>
        /// Tailoring profession.
        /// </summary>
        Tailoring = 197,

        /// <summary>
        /// Cooking profession (secondary).
        /// </summary>
        Cooking = 185,

        /// <summary>
        /// First Aid profession (secondary).
        /// </summary>
        FirstAid = 129,

        /// <summary>
        /// Fishing profession (secondary).
        /// </summary>
        Fishing = 356
    }

    /// <summary>
    /// Enumeration for gathering types.
    /// </summary>
    public enum GatheringType
    {
        /// <summary>
        /// Herb gathering.
        /// </summary>
        Herbalism,

        /// <summary>
        /// Ore mining.
        /// </summary>
        Mining,

        /// <summary>
        /// Skinning creatures.
        /// </summary>
        Skinning,

        /// <summary>
        /// Fishing.
        /// </summary>
        Fishing
    }

    /// <summary>
    /// Represents a profession service (skill or recipe) available from a trainer.
    /// </summary>
    public class ProfessionService
    {
        /// <summary>
        /// Gets or sets the spell ID of the skill/recipe.
        /// </summary>
        public uint SpellId { get; set; }

        /// <summary>
        /// Gets or sets the name of the skill/recipe.
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the description of the skill/recipe.
        /// </summary>
        public string Description { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the cost to learn this skill/recipe in copper.
        /// </summary>
        public uint Cost { get; set; }

        /// <summary>
        /// Gets or sets the required profession level to learn this skill/recipe.
        /// </summary>
        public uint RequiredLevel { get; set; }

        /// <summary>
        /// Gets or sets the profession type this service belongs to.
        /// </summary>
        public ProfessionType ProfessionType { get; set; }

        /// <summary>
        /// Gets or sets the service type (skill or recipe).
        /// </summary>
        public ProfessionServiceType ServiceType { get; set; }

        /// <summary>
        /// Gets or sets the rank of the skill/recipe.
        /// </summary>
        public uint Rank { get; set; }

        /// <summary>
        /// Gets or sets the maximum rank of the skill/recipe.
        /// </summary>
        public uint MaxRank { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether this service is currently available for learning.
        /// </summary>
        public bool IsAvailable { get; set; }

        /// <summary>
        /// Gets or sets the prerequisite skills required to learn this service.
        /// </summary>
        public uint[] Prerequisites { get; set; } = [];
    }

    /// <summary>
    /// Enumeration for profession service types.
    /// </summary>
    public enum ProfessionServiceType
    {
        /// <summary>
        /// A basic profession skill.
        /// </summary>
        Skill,

        /// <summary>
        /// A crafting recipe.
        /// </summary>
        Recipe,

        /// <summary>
        /// A gathering technique.
        /// </summary>
        GatheringTechnique,

        /// <summary>
        /// A profession specialization.
        /// </summary>
        Specialization
    }

    /// <summary>
    /// Represents a crafting request for bulk crafting operations.
    /// </summary>
    public class CraftingRequest
    {
        /// <summary>
        /// Gets or sets the recipe ID to craft.
        /// </summary>
        public uint RecipeId { get; set; }

        /// <summary>
        /// Gets or sets the quantity to craft.
        /// </summary>
        public uint Quantity { get; set; }

        /// <summary>
        /// Gets or sets the priority of this crafting request (lower numbers have higher priority).
        /// </summary>
        public int Priority { get; set; }
    }

    /// <summary>
    /// Represents a material required for a recipe.
    /// </summary>
    public class RecipeMaterial
    {
        /// <summary>
        /// Gets or sets the item ID of the required material.
        /// </summary>
        public uint ItemId { get; set; }

        /// <summary>
        /// Gets or sets the name of the material.
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the quantity required.
        /// </summary>
        public uint RequiredQuantity { get; set; }

        /// <summary>
        /// Gets or sets the quantity currently available in inventory.
        /// </summary>
        public uint AvailableQuantity { get; set; }

        /// <summary>
        /// Gets a value indicating whether sufficient materials are available.
        /// </summary>
        public bool HasSufficientQuantity => AvailableQuantity >= RequiredQuantity;
    }
}