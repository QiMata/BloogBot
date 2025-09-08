namespace WoWSharpClient.Networking.Agent.I
{
    /// <summary>
    /// Interface for handling class trainer operations in World of Warcraft.
    /// Manages learning new spells, abilities, and skills from NPC trainers.
    /// </summary>
    public interface ITrainerNetworkAgent
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
        event Action<TrainerService[]>? TrainerServicesReceived;

        /// <summary>
        /// Event fired when a trainer operation fails.
        /// </summary>
        /// <param name="error">The error message.</param>
        event Action<string>? TrainerError;

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
        /// <param name="trainerGuid">The GUID of the trainer NPC.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task RequestTrainerServicesAsync(ulong trainerGuid, CancellationToken cancellationToken = default);

        /// <summary>
        /// Learns a spell or ability from the trainer.
        /// Sends CMSG_TRAINER_BUY_SPELL with the specified spell ID.
        /// </summary>
        /// <param name="trainerGuid">The GUID of the trainer NPC.</param>
        /// <param name="spellId">The ID of the spell/ability to learn.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task LearnSpellAsync(ulong trainerGuid, uint spellId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Learns a spell or ability from the trainer by trainer service index.
        /// Sends CMSG_TRAINER_BUY_SPELL with the specified service index.
        /// </summary>
        /// <param name="trainerGuid">The GUID of the trainer NPC.</param>
        /// <param name="serviceIndex">The index of the service in the trainer's list.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task LearnSpellByIndexAsync(ulong trainerGuid, uint serviceIndex, CancellationToken cancellationToken = default);

        /// <summary>
        /// Closes the trainer window.
        /// This typically happens automatically when moving away from the trainer.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task CloseTrainerAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Checks if the specified trainer GUID has an open trainer window.
        /// </summary>
        /// <param name="trainerGuid">The GUID to check.</param>
        /// <returns>True if the trainer window is open for the specified GUID, false otherwise.</returns>
        bool IsTrainerOpen(ulong trainerGuid);

        /// <summary>
        /// Performs a complete trainer interaction: open, learn spell, close.
        /// This is a convenience method for simple spell learning.
        /// </summary>
        /// <param name="trainerGuid">The GUID of the trainer NPC.</param>
        /// <param name="spellId">The ID of the spell/ability to learn.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task QuickLearnSpellAsync(ulong trainerGuid, uint spellId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Learns multiple spells from the trainer in sequence.
        /// Opens trainer, learns each spell, then closes.
        /// </summary>
        /// <param name="trainerGuid">The GUID of the trainer NPC.</param>
        /// <param name="spellIds">The IDs of the spells/abilities to learn.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task LearnMultipleSpellsAsync(ulong trainerGuid, uint[] spellIds, CancellationToken cancellationToken = default);

        /// <summary>
        /// Checks if a specific spell/ability is available for learning from the current trainer.
        /// </summary>
        /// <param name="spellId">The ID of the spell to check.</param>
        /// <returns>True if the spell is available for learning, false otherwise.</returns>
        bool IsSpellAvailable(uint spellId);

        /// <summary>
        /// Gets the cost of learning a specific spell/ability from the current trainer.
        /// </summary>
        /// <param name="spellId">The ID of the spell to check.</param>
        /// <returns>The cost in copper, or null if the spell is not available.</returns>
        uint? GetSpellCost(uint spellId);

        /// <summary>
        /// Gets all available trainer services that can be learned.
        /// </summary>
        /// <returns>An array of available trainer services.</returns>
        TrainerService[] GetAvailableServices();

        /// <summary>
        /// Gets all affordable trainer services based on current money.
        /// </summary>
        /// <param name="currentMoney">The current amount of money in copper.</param>
        /// <returns>An array of affordable trainer services.</returns>
        TrainerService[] GetAffordableServices(uint currentMoney);
    }

    /// <summary>
    /// Represents a service (spell/ability) available from a trainer.
    /// </summary>
    public class TrainerService
    {
        /// <summary>
        /// Gets or sets the spell ID of the service.
        /// </summary>
        public uint SpellId { get; set; }

        /// <summary>
        /// Gets or sets the index of the service in the trainer's list.
        /// </summary>
        public uint ServiceIndex { get; set; }

        /// <summary>
        /// Gets or sets the name of the spell/ability.
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the rank of the spell/ability (if applicable).
        /// </summary>
        public uint Rank { get; set; }

        /// <summary>
        /// Gets or sets the cost in copper to learn this service.
        /// </summary>
        public uint Cost { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether this service can be learned.
        /// This considers prerequisites, level requirements, etc.
        /// </summary>
        public bool CanLearn { get; set; }

        /// <summary>
        /// Gets or sets the required level to learn this service.
        /// </summary>
        public uint RequiredLevel { get; set; }

        /// <summary>
        /// Gets or sets the required skill level (if applicable).
        /// </summary>
        public uint RequiredSkillLevel { get; set; }

        /// <summary>
        /// Gets or sets the skill type this service teaches (if applicable).
        /// </summary>
        public uint SkillType { get; set; }

        /// <summary>
        /// Gets or sets the trainer service type.
        /// </summary>
        public TrainerServiceType ServiceType { get; set; }
    }

    /// <summary>
    /// Represents the type of service offered by a trainer.
    /// </summary>
    public enum TrainerServiceType : byte
    {
        /// <summary>
        /// A regular spell or ability.
        /// </summary>
        Spell = 0,

        /// <summary>
        /// A profession or secondary skill.
        /// </summary>
        Profession = 1,

        /// <summary>
        /// A class-specific talent or ability.
        /// </summary>
        ClassSkill = 2
    }
}