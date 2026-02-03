using WoWSharpClient.Networking.ClientComponents.I;

namespace WoWSharpClient.Networking.ClientComponents.Models
{
    /// <summary>
    /// Represents information about a player's profession.
    /// </summary>
    public class ProfessionInfo
    {
        /// <summary>
        /// Gets or sets the profession type.
        /// </summary>
        public ProfessionType ProfessionType { get; set; }

        /// <summary>
        /// Gets or sets the current skill level.
        /// </summary>
        public uint CurrentSkill { get; set; }

        /// <summary>
        /// Gets or sets the maximum skill level.
        /// </summary>
        public uint MaxSkill { get; set; }

        /// <summary>
        /// Gets or sets the skill bonus from equipment.
        /// </summary>
        public uint SkillBonus { get; set; }

        /// <summary>
        /// Gets or sets whether this profession is active.
        /// </summary>
        public bool IsActive { get; set; }

        /// <summary>
        /// Gets or sets the rank of the profession.
        /// </summary>
        public uint Rank { get; set; }

        /// <summary>
        /// Gets or sets the number of skill-ups since last check.
        /// </summary>
        public uint SkillUps { get; set; }
    }

    /// <summary>
    /// Represents information about a recipe.
    /// </summary>
    public class RecipeInfo
    {
        /// <summary>
        /// Gets or sets the recipe ID.
        /// </summary>
        public uint RecipeId { get; set; }

        /// <summary>
        /// Gets or sets the spell ID that creates the item.
        /// </summary>
        public uint SpellId { get; set; }

        /// <summary>
        /// Gets or sets the item ID that will be created.
        /// </summary>
        public uint ItemId { get; set; }

        /// <summary>
        /// Gets or sets the recipe name.
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the difficulty of the recipe.
        /// </summary>
        public RecipeDifficulty Difficulty { get; set; }

        /// <summary>
        /// Gets or sets the required skill level.
        /// </summary>
        public uint RequiredSkill { get; set; }

        /// <summary>
        /// Gets or sets whether the player knows this recipe.
        /// </summary>
        public bool IsKnown { get; set; }

        /// <summary>
        /// Gets or sets whether the recipe is available for crafting.
        /// </summary>
        public bool IsAvailable { get; set; }

        /// <summary>
        /// Gets or sets the number of items created per craft.
        /// </summary>
        public uint YieldCount { get; set; } = 1;

        /// <summary>
        /// Gets or sets the profession type this recipe belongs to.
        /// </summary>
        public ProfessionType ProfessionType { get; set; }
    }

    /// <summary>
    /// Represents gathered resource data.
    /// </summary>
    public class GatheredResourceData
    {
        /// <summary>
        /// Gets or sets the node GUID that was gathered from.
        /// </summary>
        public ulong NodeGuid { get; set; }

        /// <summary>
        /// Gets or sets the item ID that was gathered.
        /// </summary>
        public uint ItemId { get; set; }

        /// <summary>
        /// Gets or sets the item name.
        /// </summary>
        public string ItemName { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the quantity gathered.
        /// </summary>
        public uint Quantity { get; set; }

        /// <summary>
        /// Gets or sets the gathering type.
        /// </summary>
        public GatheringType GatheringType { get; set; }

        /// <summary>
        /// Gets or sets the skill gained (if any).
        /// </summary>
        public uint SkillGained { get; set; }

        /// <summary>
        /// Gets or sets the timestamp when gathered.
        /// </summary>
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    }

    /// <summary>
    /// Represents crafting result data.
    /// </summary>
    public class CraftingResultData
    {
        /// <summary>
        /// Gets or sets the recipe ID that was crafted.
        /// </summary>
        public uint RecipeId { get; set; }

        /// <summary>
        /// Gets or sets the item ID that was created.
        /// </summary>
        public uint ItemId { get; set; }

        /// <summary>
        /// Gets or sets the item name.
        /// </summary>
        public string ItemName { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the quantity created.
        /// </summary>
        public uint Quantity { get; set; }

        /// <summary>
        /// Gets or sets whether the crafting was successful.
        /// </summary>
        public bool Success { get; set; }

        /// <summary>
        /// Gets or sets the skill gained (if any).
        /// </summary>
        public uint SkillGained { get; set; }

        /// <summary>
        /// Gets or sets the error message (if failed).
        /// </summary>
        public string? ErrorMessage { get; set; }

        /// <summary>
        /// Gets or sets the timestamp when crafted.
        /// </summary>
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    }
}