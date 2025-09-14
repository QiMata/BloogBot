namespace WoWSharpClient.Networking.ClientComponents.I
{
    /// <summary>
    /// Interface for handling talent allocation and respec operations in World of Warcraft.
    /// Manages allocating talent points when leveling up and when respecing.
    /// </summary>
    public interface ITalentNetworkClientComponent : INetworkClientComponent
    {
        /// <summary>
        /// Gets a value indicating whether the talent window is currently open.
        /// </summary>
        bool IsTalentWindowOpen { get; }

        /// <summary>
        /// Gets the current available talent points.
        /// </summary>
        uint AvailableTalentPoints { get; }

        /// <summary>
        /// Gets the total talent points spent.
        /// </summary>
        uint TotalTalentPointsSpent { get; }

        /// <summary>
        /// Gets the cost for the next talent respec in copper.
        /// </summary>
        uint RespecCost { get; }

        /// <summary>
        /// Event fired when the talent window is opened.
        /// </summary>
        event Action? TalentWindowOpened;

        /// <summary>
        /// Event fired when the talent window is closed.
        /// </summary>
        event Action? TalentWindowClosed;

        /// <summary>
        /// Event fired when a talent is successfully learned.
        /// </summary>
        /// <param name="talentId">The ID of the learned talent.</param>
        /// <param name="currentRank">The current rank of the talent after learning.</param>
        /// <param name="pointsRemaining">The number of talent points remaining.</param>
        event Action<uint, uint, uint>? TalentLearned;

        /// <summary>
        /// Event fired when talents are successfully unlearned (respeced).
        /// </summary>
        /// <param name="cost">The cost of the respec in copper.</param>
        /// <param name="pointsRefunded">The number of talent points refunded.</param>
        event Action<uint, uint>? TalentsUnlearned;

        /// <summary>
        /// Event fired when talent information is received from the server.
        /// </summary>
        /// <param name="talentInfo">The talent information for all trees.</param>
        event Action<TalentTreeInfo[]>? TalentInfoReceived;

        /// <summary>
        /// Event fired when a talent operation fails.
        /// </summary>
        /// <param name="error">The error message.</param>
        event Action<string>? TalentError;

        /// <summary>
        /// Opens the talent window.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task OpenTalentWindowAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Closes the talent window.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task CloseTalentWindowAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Learns a talent by spending talent points.
        /// Sends CMSG_LEARN_TALENT with the specified talent ID.
        /// </summary>
        /// <param name="talentId">The ID of the talent to learn.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task LearnTalentAsync(uint talentId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Learns a talent by tab and talent index position.
        /// </summary>
        /// <param name="tabIndex">The talent tree tab index (0-2).</param>
        /// <param name="talentIndex">The talent index within the tab.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task LearnTalentByPositionAsync(uint tabIndex, uint talentIndex, CancellationToken cancellationToken = default);

        /// <summary>
        /// Requests talent respec (unlearn all talents).
        /// Sends CMSG_UNLEARN_TALENTS to initiate talent reset.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task RequestTalentRespecAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Confirms a talent respec after receiving confirmation prompt.
        /// Responds to MSG_TALENT_WIPE_CONFIRM.
        /// </summary>
        /// <param name="confirm">True to confirm the respec, false to cancel.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task ConfirmTalentRespecAsync(bool confirm, CancellationToken cancellationToken = default);

        /// <summary>
        /// Learns multiple talents in sequence based on a talent build.
        /// This is useful for automated talent point allocation.
        /// </summary>
        /// <param name="talentBuild">The talent build specifying which talents to learn.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task ApplyTalentBuildAsync(TalentBuild talentBuild, CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets talent information for a specific talent tree tab.
        /// </summary>
        /// <param name="tabIndex">The talent tree tab index (0-2).</param>
        /// <returns>Talent tree information for the specified tab, or null if not available.</returns>
        TalentTreeInfo? GetTalentTreeInfo(uint tabIndex);

        /// <summary>
        /// Gets the current rank of a specific talent.
        /// </summary>
        /// <param name="talentId">The talent ID to check.</param>
        /// <returns>The current rank of the talent (0 if not learned).</returns>
        uint GetTalentRank(uint talentId);

        /// <summary>
        /// Checks if a talent can be learned (has available points and meets prerequisites).
        /// </summary>
        /// <param name="talentId">The talent ID to check.</param>
        /// <returns>True if the talent can be learned, false otherwise.</returns>
        bool CanLearnTalent(uint talentId);

        /// <summary>
        /// Gets the number of points spent in a specific talent tree.
        /// </summary>
        /// <param name="tabIndex">The talent tree tab index (0-2).</param>
        /// <returns>The number of talent points spent in the specified tree.</returns>
        uint GetPointsInTree(uint tabIndex);

        /// <summary>
        /// Gets all available talent trees for the current character class.
        /// </summary>
        /// <returns>An array of talent tree information.</returns>
        TalentTreeInfo[] GetAllTalentTrees();

        /// <summary>
        /// Validates a talent build against current character state.
        /// Checks if the build is valid and can be applied.
        /// </summary>
        /// <param name="talentBuild">The talent build to validate.</param>
        /// <returns>A validation result indicating if the build is valid and any issues.</returns>
        TalentBuildValidationResult ValidateTalentBuild(TalentBuild talentBuild);
    }

    /// <summary>
    /// Represents information about a talent tree.
    /// </summary>
    public class TalentTreeInfo
    {
        /// <summary>
        /// Gets or sets the tab index of the talent tree (0-2).
        /// </summary>
        public uint TabIndex { get; set; }

        /// <summary>
        /// Gets or sets the name of the talent tree.
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the background image name for the tree.
        /// </summary>
        public string Background { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the number of points spent in this tree.
        /// </summary>
        public uint PointsSpent { get; set; }

        /// <summary>
        /// Gets or sets the talents in this tree.
        /// </summary>
        public TalentInfo[] Talents { get; set; } = [];
    }

    /// <summary>
    /// Represents information about a specific talent.
    /// </summary>
    public class TalentInfo
    {
        /// <summary>
        /// Gets or sets the talent ID.
        /// </summary>
        public uint TalentId { get; set; }

        /// <summary>
        /// Gets or sets the talent tab index.
        /// </summary>
        public uint TabIndex { get; set; }

        /// <summary>
        /// Gets or sets the talent index within the tab.
        /// </summary>
        public uint TalentIndex { get; set; }

        /// <summary>
        /// Gets or sets the row position in the talent tree.
        /// </summary>
        public uint Row { get; set; }

        /// <summary>
        /// Gets or sets the column position in the talent tree.
        /// </summary>
        public uint Column { get; set; }

        /// <summary>
        /// Gets or sets the name of the talent.
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the description of the talent.
        /// </summary>
        public string Description { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the current rank of the talent.
        /// </summary>
        public uint CurrentRank { get; set; }

        /// <summary>
        /// Gets or sets the maximum rank of the talent.
        /// </summary>
        public uint MaxRank { get; set; }

        /// <summary>
        /// Gets or sets the spell IDs for each rank of the talent.
        /// </summary>
        public uint[] SpellIds { get; set; } = [];

        /// <summary>
        /// Gets or sets the prerequisites for this talent.
        /// </summary>
        public TalentPrerequisite[] Prerequisites { get; set; } = [];

        /// <summary>
        /// Gets or sets a value indicating whether this talent can currently be learned.
        /// </summary>
        public bool CanLearn { get; set; }

        /// <summary>
        /// Gets or sets the required points in the tree to unlock this talent.
        /// </summary>
        public uint RequiredTreePoints { get; set; }
    }

    /// <summary>
    /// Represents a prerequisite for learning a talent.
    /// </summary>
    public class TalentPrerequisite
    {
        /// <summary>
        /// Gets or sets the talent ID that is required.
        /// </summary>
        public uint TalentId { get; set; }

        /// <summary>
        /// Gets or sets the minimum rank required for the prerequisite talent.
        /// </summary>
        public uint RequiredRank { get; set; }
    }

    /// <summary>
    /// Represents a talent build configuration.
    /// </summary>
    public class TalentBuild
    {
        /// <summary>
        /// Gets or sets the name of the talent build.
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the description of the talent build.
        /// </summary>
        public string Description { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the character class this build is for.
        /// </summary>
        public uint ClassId { get; set; }

        /// <summary>
        /// Gets or sets the talent allocations for this build.
        /// </summary>
        public TalentAllocation[] Allocations { get; set; } = [];

        /// <summary>
        /// Gets or sets the priority order for learning talents (highest priority first).
        /// </summary>
        public uint[] LearningOrder { get; set; } = [];
    }

    /// <summary>
    /// Represents an allocation of points to a specific talent.
    /// </summary>
    public class TalentAllocation
    {
        /// <summary>
        /// Gets or sets the talent ID.
        /// </summary>
        public uint TalentId { get; set; }

        /// <summary>
        /// Gets or sets the target rank for this talent.
        /// </summary>
        public uint TargetRank { get; set; }

        /// <summary>
        /// Gets or sets the priority for learning this talent (higher number = higher priority).
        /// </summary>
        public uint Priority { get; set; }
    }

    /// <summary>
    /// Represents the result of validating a talent build.
    /// </summary>
    public class TalentBuildValidationResult
    {
        /// <summary>
        /// Gets or sets a value indicating whether the talent build is valid.
        /// </summary>
        public bool IsValid { get; set; }

        /// <summary>
        /// Gets or sets the validation errors, if any.
        /// </summary>
        public List<string> Errors { get; set; } = [];

        /// <summary>
        /// Gets or sets the validation warnings, if any.
        /// </summary>
        public List<string> Warnings { get; set; } = [];

        /// <summary>
        /// Gets or sets the total talent points required for this build.
        /// </summary>
        public uint RequiredPoints { get; set; }

        /// <summary>
        /// Gets or sets the number of points that can be applied immediately.
        /// </summary>
        public uint ApplicablePoints { get; set; }
    }
}