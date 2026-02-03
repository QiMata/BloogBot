using System.Reactive; // for Unit

namespace WoWSharpClient.Networking.ClientComponents.I
{
    /// <summary>
    /// Interface for handling class trainer operations in World of Warcraft.
    /// Manages learning new spells, abilities, and skills from NPC trainers.
    /// </summary>
    public interface ITrainerNetworkClientComponent : INetworkClientComponent
    {
        /// <summary>
        /// Gets a value indicating whether a trainer window is currently open.
        /// </summary>
        bool IsTrainerWindowOpen { get; }

        /// <summary>
        /// Gets the GUID of the currently open trainer, if any.
        /// </summary>
        ulong? CurrentTrainerGuid { get; }

        /// <summary>
        /// Event fired when a trainer window is opened.
        /// </summary>
        event Action<ulong>? TrainerWindowOpened;

        /// <summary>
        /// Event fired when a trainer window is closed.
        /// </summary>
        event Action? TrainerWindowClosed;

        /// <summary>
        /// Event fired when a spell/ability is successfully learned.
        /// </summary>
        /// <param name="spellId">The ID of the learned spell.</param>
        /// <param name="cost">The cost in copper.</param>
        event Action<uint, uint>? SpellLearned;

        /// <summary>
        /// Event fired when trainer services (spells/abilities) are received from the server.
        /// </summary>
        /// <param name="availableSpells">The list of available spells/abilities that can be learned.</param>
        event Action<TrainerServiceData[]>? TrainerServicesReceived;

        /// <summary>
        /// Event fired when a trainer operation fails.
        /// </summary>
        /// <param name="error">The error message.</param>
        event Action<string>? TrainerError;

        // Reactive observables (preferred)
        IObservable<(ulong TrainerGuid, TrainerServiceData[] Services)> TrainerWindowsOpened { get; }
        IObservable<Unit> TrainerWindowsClosed { get; }
        IObservable<(uint SpellId, uint Cost)> SpellsLearned { get; }
        IObservable<TrainerServiceData[]> TrainerServicesUpdated { get; }
        IObservable<string> TrainerErrors { get; }

        /// <summary>
        /// Opens the trainer window by greeting the specified trainer NPC.
        /// Sends CMSG_GOSSIP_HELLO to initiate trainer interaction.
        /// </summary>
        /// <param name="trainerGuid">The GUID of the trainer NPC.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task OpenTrainerAsync(ulong trainerGuid, CancellationToken cancellationToken = default);

        /// <summary>
        /// Requests the trainer's available services (spells/abilities).
        /// Sends CMSG_TRAINER_LIST to get available training options.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task GetTrainerServicesAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Requests the trainer's available services for a specific trainer.
        /// Sends CMSG_TRAINER_LIST to get available training options.
        /// </summary>
        /// <param name="trainerGuid">The GUID of the trainer NPC.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task RequestTrainerServicesAsync(ulong trainerGuid, CancellationToken cancellationToken = default);

        /// <summary>
        /// Learns a spell or ability from the trainer.
        /// Sends CMSG_TRAINER_BUY_SPELL with the spell ID.
        /// </summary>
        /// <param name="spellId">The ID of the spell to learn.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task LearnSpellAsync(uint spellId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Learns a spell or ability from a specific trainer.
        /// Sends CMSG_TRAINER_BUY_SPELL with the trainer GUID and spell ID.
        /// </summary>
        /// <param name="trainerGuid">The GUID of the trainer NPC.</param>
        /// <param name="spellId">The ID of the spell to learn.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task LearnSpellAsync(ulong trainerGuid, uint spellId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Closes the trainer window.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task CloseTrainerAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets all available trainer services.
        /// </summary>
        /// <returns>Array of available trainer services.</returns>
        TrainerServiceData[] GetAvailableServices();

        /// <summary>
        /// Gets trainer services that the player can afford.
        /// </summary>
        /// <param name="currentMoney">The player's current money in copper.</param>
        /// <returns>Array of affordable trainer services.</returns>
        TrainerServiceData[] GetAffordableServices(uint currentMoney);

        /// <summary>
        /// Checks if a spell is available for learning.
        /// </summary>
        /// <param name="spellId">The ID of the spell to check.</param>
        /// <returns>True if the spell is available, false otherwise.</returns>
        bool IsSpellAvailable(uint spellId);

        /// <summary>
        /// Gets the cost of a spell.
        /// </summary>
        /// <param name="spellId">The ID of the spell.</param>
        /// <returns>The cost of the spell, or null if not found.</returns>
        uint? GetSpellCost(uint spellId);

        /// <summary>
        /// Checks if the trainer is open for a specific trainer.
        /// </summary>
        /// <param name="trainerGuid">The GUID of the trainer to check.</param>
        /// <returns>True if the trainer is open, false otherwise.</returns>
        bool IsTrainerOpen(ulong trainerGuid);

        /// <summary>
        /// Learns a spell by index from the available services.
        /// </summary>
        /// <param name="trainerGuid">The GUID of the trainer.</param>
        /// <param name="serviceIndex">The index of the service to learn.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task LearnSpellByIndexAsync(ulong trainerGuid, uint serviceIndex, CancellationToken cancellationToken = default);

        /// <summary>
        /// Performs a quick learn of a spell (opens trainer, learns spell, closes trainer).
        /// </summary>
        /// <param name="trainerGuid">The GUID of the trainer.</param>
        /// <param name="spellId">The ID of the spell to learn.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task QuickLearnSpellAsync(ulong trainerGuid, uint spellId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Learns multiple spells from a trainer.
        /// </summary>
        /// <param name="trainerGuid">The GUID of the trainer.</param>
        /// <param name="spellIds">Array of spell IDs to learn.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task LearnMultipleSpellsAsync(ulong trainerGuid, uint[] spellIds, CancellationToken cancellationToken = default);

        /// <summary>
        /// Updates trainer services.
        /// </summary>
        /// <param name="services">The new trainer services.</param>
        void UpdateTrainerServices(TrainerServiceData[] services);
    }

    /// <summary>
    /// Represents a trainer service (spell/ability) that can be learned.
    /// </summary>
    public class TrainerServiceData
    {
        /// <summary>
        /// Gets or sets the spell ID.
        /// </summary>
        public uint SpellId { get; set; }

        /// <summary>
        /// Gets or sets the service index.
        /// </summary>
        public uint ServiceIndex { get; set; }

        /// <summary>
        /// Gets or sets the name of the spell/ability.
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the cost in copper.
        /// </summary>
        public uint Cost { get; set; }

        /// <summary>
        /// Gets or sets the required level.
        /// </summary>
        public uint RequiredLevel { get; set; }

        /// <summary>
        /// Gets or sets the required skill.
        /// </summary>
        public uint RequiredSkill { get; set; }

        /// <summary>
        /// Gets or sets the required skill level.
        /// </summary>
        public uint RequiredSkillLevel { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether this service can be learned.
        /// </summary>
        public bool CanLearn { get; set; }
    }
}