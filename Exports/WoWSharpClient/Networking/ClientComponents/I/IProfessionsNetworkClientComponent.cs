using System;
using System.Reactive;
using GameData.Core.Enums;
using WoWSharpClient.Networking.ClientComponents.Models;

namespace WoWSharpClient.Networking.ClientComponents.I
{
    /// <summary>
    /// Interface for handling profession operations in World of Warcraft.
    /// Manages profession skill training, crafting, and gathering interactions.
    /// Uses opcode-backed IObservable streams (no events/subjects).
    /// </summary>
    public interface IProfessionsNetworkClientComponent : INetworkClientComponent
    {
        // --- State ---
        bool IsTrainerWindowOpen { get; }
        bool IsCraftingWindowOpen { get; }
        ulong? CurrentTrainerGuid { get; }
        ProfessionType? CurrentProfession { get; }

        // --- Reactive streams ---
        // Trainer UI lifecycle
        IObservable<(ulong TrainerGuid, ProfessionType? Profession)> TrainerWindowOpened { get; }
        IObservable<Unit> TrainerWindowClosed { get; }

        // Crafting UI lifecycle
        IObservable<ProfessionType?> CraftingWindowOpened { get; }
        IObservable<Unit> CraftingWindowClosed { get; }

        // Training results / services
        IObservable<(uint SpellId, uint Cost)> SkillLearned { get; }
        IObservable<ProfessionService[]> ProfessionServicesReceived { get; }

        // Crafting results
        IObservable<(uint ItemId, uint Quantity)> ItemCrafted { get; }
        IObservable<(uint RecipeId, string Reason)> CraftingFailed { get; }

        // Gathering results
        IObservable<(ulong NodeGuid, uint ItemId, uint Quantity)> ResourceGathered { get; }
        IObservable<(ulong NodeGuid, string Reason)> GatheringFailed { get; }

        // Errors from server/opcode responses
        IObservable<string> ProfessionErrors { get; }

        // --- Operations ---
        Task OpenProfessionTrainerAsync(ulong trainerGuid, ProfessionType professionType, CancellationToken cancellationToken = default);
        Task RequestProfessionServicesAsync(ulong trainerGuid, CancellationToken cancellationToken = default);
        Task LearnProfessionSkillAsync(ulong trainerGuid, uint spellId, CancellationToken cancellationToken = default);
        Task LearnMultipleProfessionSkillsAsync(ulong trainerGuid, uint[] spellIds, CancellationToken cancellationToken = default);
        Task CloseProfessionTrainerAsync(CancellationToken cancellationToken = default);

        Task OpenCraftingWindowAsync(ProfessionType professionType, CancellationToken cancellationToken = default);
        Task CraftItemAsync(uint recipeId, uint quantity = 1, CancellationToken cancellationToken = default);
        Task CraftMultipleItemsAsync(CraftingRequest[] craftingQueue, CancellationToken cancellationToken = default);
        Task CloseCraftingWindowAsync(CancellationToken cancellationToken = default);

        Task GatherResourceAsync(ulong nodeGuid, GatheringType gatheringType, CancellationToken cancellationToken = default);
        Task QuickTrainProfessionSkillsAsync(ulong trainerGuid, ProfessionType professionType, uint[] skillIds, CancellationToken cancellationToken = default);

        // --- Queries ---
        ProfessionService[] GetAvailableProfessionServices();
        ProfessionService[] GetAffordableProfessionServices(uint availableMoney);
        bool IsProfessionSkillAvailable(uint spellId);
        uint GetProfessionSkillCost(uint spellId);
        bool CanCraftRecipe(uint recipeId);
        RecipeMaterial[] GetRecipeMaterials(uint recipeId);
    }

    /// <summary>
    /// Enumeration for profession types in World of Warcraft.
    /// </summary>
    public enum ProfessionType : uint
    {
        Alchemy = 171,
        Blacksmithing = 164,
        Enchanting = 333,
        Engineering = 202,
        Herbalism = 182,
        Leatherworking = 165,
        Mining = 186,
        Skinning = 393,
        Tailoring = 197,
        Cooking = 185,
        FirstAid = 129,
        Fishing = 356
    }

    /// <summary>
    /// Enumeration for gathering types.
    /// </summary>
    public enum GatheringType
    {
        Herbalism,
        Mining,
        Skinning,
        Fishing
    }

    /// <summary>
    /// Represents a profession service (skill or recipe) available from a trainer.
    /// </summary>
    public class ProfessionService
    {
        public uint SpellId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public uint Cost { get; set; }
        public uint RequiredLevel { get; set; }
        public ProfessionType ProfessionType { get; set; }
        public ProfessionServiceType ServiceType { get; set; }
        public uint Rank { get; set; }
        public uint MaxRank { get; set; }
        public bool IsAvailable { get; set; }
        public uint[] Prerequisites { get; set; } = [];
    }

    /// <summary>
    /// Enumeration for profession service types.
    /// </summary>
    public enum ProfessionServiceType
    {
        Skill,
        Recipe,
        GatheringTechnique,
        Specialization
    }

    /// <summary>
    /// Represents a crafting request for bulk crafting operations.
    /// </summary>
    public class CraftingRequest
    {
        public uint RecipeId { get; set; }
        public uint Quantity { get; set; }
        public int Priority { get; set; }
    }

    /// <summary>
    /// Represents a material required for a recipe.
    /// </summary>
    public class RecipeMaterial
    {
        public uint ItemId { get; set; }
        public string Name { get; set; } = string.Empty;
        public uint RequiredQuantity { get; set; }
        public uint AvailableQuantity { get; set; }
        public bool HasSufficientQuantity => AvailableQuantity >= RequiredQuantity;
    }
}