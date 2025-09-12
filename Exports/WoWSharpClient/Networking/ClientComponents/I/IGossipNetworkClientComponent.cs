using WoWSharpClient.Networking.ClientComponents.Models;

namespace WoWSharpClient.Networking.ClientComponents.I
{
    /// <summary>
    /// Interface for handling gossip operations in World of Warcraft.
    /// Manages NPC dialogue, multi-step conversations, and service navigation.
    /// </summary>
    public interface IGossipNetworkAgent
    {
        #region Properties

        /// <summary>
        /// Gets a value indicating whether a gossip window is currently open.
        /// </summary>
        bool IsGossipWindowOpen { get; }

        /// <summary>
        /// Gets the timestamp of the last gossip operation.
        /// </summary>
        DateTime? LastOperationTime { get; }

        /// <summary>
        /// Gets the currently active NPC GUID if in conversation.
        /// </summary>
        ulong? CurrentNpcGuid { get; }

        /// <summary>
        /// Gets the current gossip menu state.
        /// </summary>
        GossipMenuState MenuState { get; }

        #endregion

        #region Reactive Observables

        /// <summary>
        /// Observable stream of gossip menu events.
        /// </summary>
        IObservable<GossipMenuData> GossipMenus { get; }

        /// <summary>
        /// Observable stream of selected gossip options.
        /// </summary>
        IObservable<GossipOptionData> SelectedOptions { get; }

        /// <summary>
        /// Observable stream of gossip errors.
        /// </summary>
        IObservable<GossipErrorData> GossipErrors { get; }

        /// <summary>
        /// Observable stream of gossip menu openings.
        /// </summary>
        IObservable<GossipMenuData> GossipMenuOpened { get; }

        /// <summary>
        /// Observable stream of gossip menu closings.
        /// </summary>
        IObservable<GossipMenuData> GossipMenuClosed { get; }

        /// <summary>
        /// Observable stream of NPC service discoveries.
        /// </summary>
        IObservable<GossipServiceData> ServiceDiscovered { get; }

        #endregion

        #region Basic Operations

        /// <summary>
        /// Initiates conversation with an NPC.
        /// Sends CMSG_GOSSIP_HELLO to the server.
        /// </summary>
        /// <param name="npcGuid">The GUID of the NPC.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task GreetNpcAsync(ulong npcGuid, CancellationToken cancellationToken = default);

        /// <summary>
        /// Selects a gossip option by index.
        /// Sends CMSG_GOSSIP_SELECT_OPTION to the server.
        /// </summary>
        /// <param name="optionIndex">The index of the option to select.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task SelectGossipOptionAsync(uint optionIndex, CancellationToken cancellationToken = default);

        /// <summary>
        /// Queries NPC text for additional dialogue information.
        /// Sends CMSG_NPC_TEXT_QUERY to the server.
        /// </summary>
        /// <param name="textId">The text ID to query.</param>
        /// <param name="npcGuid">The GUID of the NPC.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task QueryNpcTextAsync(uint textId, ulong npcGuid, CancellationToken cancellationToken = default);

        /// <summary>
        /// Closes the current gossip conversation.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task CloseGossipAsync(CancellationToken cancellationToken = default);

        #endregion

        #region Advanced Operations

        /// <summary>
        /// Navigates to a specific NPC service through gossip options.
        /// Automatically finds and selects the appropriate dialogue path.
        /// </summary>
        /// <param name="serviceType">The type of service to navigate to.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task NavigateToServiceAsync(GossipServiceType serviceType, CancellationToken cancellationToken = default);

        /// <summary>
        /// Handles multi-step conversations using the specified navigation strategy.
        /// </summary>
        /// <param name="strategy">The navigation strategy to use.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task HandleMultiStepConversationAsync(GossipNavigationStrategy strategy, CancellationToken cancellationToken = default);

        /// <summary>
        /// Automatically discovers available services from the current gossip menu.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>A list of discovered services.</returns>
        Task<IReadOnlyList<GossipServiceType>> DiscoverAvailableServicesAsync(CancellationToken cancellationToken = default);

        #endregion

        #region Quest Integration

        /// <summary>
        /// Selects the optimal quest reward based on the specified strategy.
        /// </summary>
        /// <param name="strategy">The reward selection strategy.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task SelectOptimalQuestRewardAsync(QuestRewardSelectionStrategy strategy, CancellationToken cancellationToken = default);

        /// <summary>
        /// Accepts all available quests that match the specified filter.
        /// </summary>
        /// <param name="filter">Optional filter for quest acceptance.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task AcceptAllAvailableQuestsAsync(QuestAcceptanceFilter? filter = null, CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets available quest options from the current gossip menu.
        /// </summary>
        /// <returns>A list of available quest options.</returns>
        Task<IReadOnlyList<GossipQuestOption>> GetAvailableQuestOptionsAsync();

        #endregion

        #region Server Response Handling

        /// <summary>
        /// Handles gossip menu received from the server.
        /// This method should be called when SMSG_GOSSIP_MESSAGE is received.
        /// </summary>
        /// <param name="menuData">The gossip menu data.</param>
        void HandleGossipMenuReceived(GossipMenuData menuData);

        /// <summary>
        /// Handles the result of a gossip option selection.
        /// </summary>
        /// <param name="result">The result of the option selection.</param>
        void HandleGossipOptionResult(GossipOptionResult result);

        /// <summary>
        /// Handles NPC text update from the server.
        /// This method should be called when SMSG_NPC_TEXT_UPDATE is received.
        /// </summary>
        /// <param name="npcText">The NPC text.</param>
        /// <param name="textId">The text ID.</param>
        void HandleNpcTextUpdate(string npcText, uint textId);

        /// <summary>
        /// Handles gossip session completion.
        /// This method should be called when SMSG_GOSSIP_COMPLETE is received.
        /// </summary>
        void HandleGossipSessionComplete();

        /// <summary>
        /// Handles gossip error from the server.
        /// </summary>
        /// <param name="errorMessage">The error message.</param>
        /// <param name="npcGuid">The NPC GUID that caused the error.</param>
        void HandleGossipError(string errorMessage, ulong? npcGuid = null);

        #endregion

        #region Validation and Helper Methods

        /// <summary>
        /// Validates whether a gossip operation can be performed.
        /// </summary>
        /// <param name="operationType">The type of operation to validate.</param>
        /// <returns>True if the operation can be performed.</returns>
        bool CanPerformGossipOperation(GossipOperationType operationType);

        /// <summary>
        /// Gets the current gossip menu information.
        /// </summary>
        /// <returns>The current gossip menu, or null if no menu is open.</returns>
        GossipMenuData? GetCurrentGossipMenu();

        /// <summary>
        /// Checks if the specified service is available in the current gossip menu.
        /// </summary>
        /// <param name="serviceType">The service type to check.</param>
        /// <returns>True if the service is available.</returns>
        bool IsServiceAvailable(GossipServiceType serviceType);

        #endregion
    }
}