using System.Reactive.Linq;
using System.Reactive.Subjects;
using GameData.Core.Enums;
using Microsoft.Extensions.Logging;
using WoWSharpClient.Client;
using WoWSharpClient.Networking.ClientComponents.I;
using WoWSharpClient.Networking.ClientComponents.Models;

namespace WoWSharpClient.Networking.ClientComponents
{
    /// <summary>
    /// Implementation of gossip network agent that handles NPC dialogue and multi-step conversations.
    /// Manages gossip operations, service navigation, and quest reward selection using reactive observables.
    /// </summary>
    public class GossipNetworkClientComponent : IGossipNetworkClientComponent, IDisposable
    {
        private readonly IWorldClient _worldClient;
        private readonly ILogger<GossipNetworkClientComponent> _logger;
        private bool _isGossipWindowOpen;
        private DateTime? _lastOperationTime;
        private ulong? _currentNpcGuid;
        private GossipMenuState _menuState;
        private GossipMenuData? _currentMenu;

        // Reactive observables
        private readonly Subject<GossipMenuData> _gossipMenus = new();
        private readonly Subject<GossipOptionData> _selectedOptions = new();
        private readonly Subject<GossipErrorData> _gossipErrors = new();
        private readonly Subject<GossipServiceData> _serviceDiscovered = new();

        // Filtered observables (lazy-initialized)
        private IObservable<GossipMenuData>? _gossipMenuOpened;
        private IObservable<GossipMenuData>? _gossipMenuClosed;

        private bool _disposed;

        /// <summary>
        /// Initializes a new instance of the GossipNetworkClientComponent class.
        /// </summary>
        /// <param name="worldClient">The world client for sending packets.</param>
        /// <param name="logger">Logger instance.</param>
        public GossipNetworkClientComponent(IWorldClient worldClient, ILogger<GossipNetworkClientComponent> logger)
        {
            _worldClient = worldClient ?? throw new ArgumentNullException(nameof(worldClient));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _menuState = GossipMenuState.Closed;
        }

        #region Properties

        /// <inheritdoc />
        public bool IsGossipWindowOpen => _isGossipWindowOpen;

        /// <inheritdoc />
        public DateTime? LastOperationTime => _lastOperationTime;

        /// <inheritdoc />
        public ulong? CurrentNpcGuid => _currentNpcGuid;

        /// <inheritdoc />
        public GossipMenuState MenuState => _menuState;

        #endregion

        #region Reactive Observables

        /// <inheritdoc />
        public IObservable<GossipMenuData> GossipMenus => _gossipMenus;

        /// <inheritdoc />
        public IObservable<GossipOptionData> SelectedOptions => _selectedOptions;

        /// <inheritdoc />
        public IObservable<GossipErrorData> GossipErrors => _gossipErrors;

        /// <inheritdoc />
        public IObservable<GossipMenuData> GossipMenuOpened =>
            _gossipMenuOpened ??= _gossipMenus.Where(m => _isGossipWindowOpen);

        /// <inheritdoc />
        public IObservable<GossipMenuData> GossipMenuClosed =>
            _gossipMenuClosed ??= _gossipMenus.Where(m => !_isGossipWindowOpen);

        /// <inheritdoc />
        public IObservable<GossipServiceData> ServiceDiscovered => _serviceDiscovered;

        #endregion

        #region Basic Operations

        /// <inheritdoc />
        public async Task GreetNpcAsync(ulong npcGuid, CancellationToken cancellationToken = default)
        {
            if (_disposed) throw new ObjectDisposedException(nameof(GossipNetworkClientComponent));

            try
            {
                _logger.LogDebug("Greeting NPC: {NpcGuid:X}", npcGuid);
                _menuState = GossipMenuState.Opening;

                var payload = new byte[8];
                BitConverter.GetBytes(npcGuid).CopyTo(payload, 0);

                await _worldClient.SendOpcodeAsync(Opcode.CMSG_GOSSIP_HELLO, payload, cancellationToken);

                _currentNpcGuid = npcGuid;
                _lastOperationTime = DateTime.UtcNow;
                _logger.LogInformation("Gossip hello sent to NPC: {NpcGuid:X}", npcGuid);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to greet NPC: {NpcGuid:X}", npcGuid);
                _menuState = GossipMenuState.Error;

                var errorData = new GossipErrorData(ex.Message, npcGuid, GossipOperationType.Greet);
                _gossipErrors.OnNext(errorData);

                throw;
            }
        }

        /// <inheritdoc />
        public async Task SelectGossipOptionAsync(uint optionIndex, CancellationToken cancellationToken = default)
        {
            if (_disposed) throw new ObjectDisposedException(nameof(GossipNetworkClientComponent));

            if (!_isGossipWindowOpen || _currentNpcGuid == null)
            {
                throw new InvalidOperationException("No gossip window is currently open");
            }

            try
            {
                _logger.LogDebug("Selecting gossip option {OptionIndex} for NPC: {NpcGuid:X}", optionIndex, _currentNpcGuid);
                _menuState = GossipMenuState.Waiting;

                var payload = new byte[12];
                BitConverter.GetBytes(_currentNpcGuid.Value).CopyTo(payload, 0);
                BitConverter.GetBytes(optionIndex).CopyTo(payload, 8);

                await _worldClient.SendOpcodeAsync(Opcode.CMSG_GOSSIP_SELECT_OPTION, payload, cancellationToken);

                // Find the selected option for reactive observable
                if (_currentMenu != null && optionIndex < _currentMenu.Options.Count)
                {
                    var selectedOption = _currentMenu.Options[(int)optionIndex];
                    _selectedOptions.OnNext(selectedOption);
                }

                _lastOperationTime = DateTime.UtcNow;
                _logger.LogInformation("Gossip option {OptionIndex} selected for NPC: {NpcGuid:X}", optionIndex, _currentNpcGuid);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to select gossip option {OptionIndex} for NPC: {NpcGuid:X}", optionIndex, _currentNpcGuid);
                _menuState = GossipMenuState.Error;

                var errorData = new GossipErrorData(ex.Message, _currentNpcGuid, GossipOperationType.SelectOption);
                _gossipErrors.OnNext(errorData);

                throw;
            }
        }

        /// <inheritdoc />
        public async Task QueryNpcTextAsync(uint textId, ulong npcGuid, CancellationToken cancellationToken = default)
        {
            if (_disposed) throw new ObjectDisposedException(nameof(GossipNetworkClientComponent));

            try
            {
                _logger.LogDebug("Querying NPC text {TextId} for NPC: {NpcGuid:X}", textId, npcGuid);

                var payload = new byte[12];
                BitConverter.GetBytes(textId).CopyTo(payload, 0);
                BitConverter.GetBytes(npcGuid).CopyTo(payload, 4);

                await _worldClient.SendOpcodeAsync(Opcode.CMSG_NPC_TEXT_QUERY, payload, cancellationToken);

                _lastOperationTime = DateTime.UtcNow;
                _logger.LogInformation("NPC text query sent: TextId {TextId} for NPC: {NpcGuid:X}", textId, npcGuid);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to query NPC text {TextId} for NPC: {NpcGuid:X}", textId, npcGuid);

                var errorData = new GossipErrorData(ex.Message, npcGuid, GossipOperationType.QueryText);
                _gossipErrors.OnNext(errorData);

                throw;
            }
        }

        /// <inheritdoc />
        public async Task CloseGossipAsync(CancellationToken cancellationToken = default)
        {
            if (_disposed) throw new ObjectDisposedException(nameof(GossipNetworkClientComponent));

            try
            {
                _logger.LogDebug("Closing gossip conversation with NPC: {NpcGuid:X}", _currentNpcGuid ?? 0);
                _menuState = GossipMenuState.Closing;

                // Gossip windows typically close by moving away or interacting with something else
                // For now, we'll just update our internal state
                _isGossipWindowOpen = false;
                _currentNpcGuid = null;
                _currentMenu = null;
                _menuState = GossipMenuState.Closed;

                _lastOperationTime = DateTime.UtcNow;
                _logger.LogInformation("Gossip conversation closed");

                await Task.CompletedTask;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to close gossip conversation");
                _menuState = GossipMenuState.Error;

                var errorData = new GossipErrorData(ex.Message, _currentNpcGuid, GossipOperationType.Close);
                _gossipErrors.OnNext(errorData);

                throw;
            }
        }

        #endregion

        #region Advanced Operations

        /// <inheritdoc />
        public async Task NavigateToServiceAsync(GossipServiceType serviceType, CancellationToken cancellationToken = default)
        {
            if (_disposed) throw new ObjectDisposedException(nameof(GossipNetworkClientComponent));

            try
            {
                _logger.LogDebug("Navigating to service: {ServiceType}", serviceType);

                if (!_isGossipWindowOpen || _currentMenu == null)
                {
                    throw new InvalidOperationException("No gossip menu is currently open");
                }

                // Find the option that leads to the desired service
                var serviceOption = _currentMenu.Options.FirstOrDefault(o => o.ServiceType == serviceType);
                if (serviceOption == null)
                {
                    throw new InvalidOperationException($"Service {serviceType} is not available in the current gossip menu");
                }

                await SelectGossipOptionAsync(serviceOption.Index, cancellationToken);

                // Notify about service discovery
                var serviceData = new GossipServiceData(_currentNpcGuid ?? 0, serviceType, serviceOption);
                _serviceDiscovered.OnNext(serviceData);

                _logger.LogInformation("Successfully navigated to service: {ServiceType}", serviceType);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to navigate to service: {ServiceType}", serviceType);

                var errorData = new GossipErrorData(ex.Message, _currentNpcGuid, GossipOperationType.NavigateToService);
                _gossipErrors.OnNext(errorData);

                throw;
            }
        }

        /// <inheritdoc />
        public async Task HandleMultiStepConversationAsync(GossipNavigationStrategy strategy, CancellationToken cancellationToken = default)
        {
            if (_disposed) throw new ObjectDisposedException(nameof(GossipNetworkClientComponent));

            try
            {
                _logger.LogDebug("Handling multi-step conversation with strategy: {Strategy}", strategy);

                switch (strategy)
                {
                    case GossipNavigationStrategy.FindQuests:
                        await FindAndNavigateToQuestsAsync(cancellationToken);
                        break;

                    case GossipNavigationStrategy.FindVendor:
                        await NavigateToServiceAsync(GossipServiceType.Vendor, cancellationToken);
                        break;

                    case GossipNavigationStrategy.FindTrainer:
                        await NavigateToServiceAsync(GossipServiceType.Trainer, cancellationToken);
                        break;

                    case GossipNavigationStrategy.FindTaxi:
                        await NavigateToServiceAsync(GossipServiceType.Taxi, cancellationToken);
                        break;

                    case GossipNavigationStrategy.FindBanker:
                        await NavigateToServiceAsync(GossipServiceType.Banker, cancellationToken);
                        break;

                    case GossipNavigationStrategy.FindAnyService:
                        await FindAndNavigateToAnyServiceAsync(cancellationToken);
                        break;

                    case GossipNavigationStrategy.ExploreAll:
                        await ExploreAllOptionsAsync(cancellationToken);
                        break;

                    case GossipNavigationStrategy.Custom:
                        _logger.LogWarning("Custom navigation strategy not implemented");
                        break;

                    default:
                        throw new ArgumentException($"Unknown navigation strategy: {strategy}");
                }

                _logger.LogInformation("Multi-step conversation handled with strategy: {Strategy}", strategy);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to handle multi-step conversation with strategy: {Strategy}", strategy);

                var errorData = new GossipErrorData(ex.Message, _currentNpcGuid, GossipOperationType.NavigateToService);
                _gossipErrors.OnNext(errorData);

                throw;
            }
        }

        /// <inheritdoc />
        public async Task<IReadOnlyList<GossipServiceType>> DiscoverAvailableServicesAsync(CancellationToken cancellationToken = default)
        {
            if (_disposed) throw new ObjectDisposedException(nameof(GossipNetworkClientComponent));

            try
            {
                _logger.LogDebug("Discovering available services");

                if (!_isGossipWindowOpen || _currentMenu == null)
                {
                    return [];
                }

                var services = new List<GossipServiceType>();

                // Discover services from gossip options
                foreach (var option in _currentMenu.Options)
                {
                    if (option.ServiceType != GossipServiceType.Gossip && !services.Contains(option.ServiceType))
                    {
                        services.Add(option.ServiceType);
                        
                        // Emit service discovery event
                        var serviceData = new GossipServiceData(_currentNpcGuid ?? 0, option.ServiceType, option);
                        _serviceDiscovered.OnNext(serviceData);
                    }
                }

                // Check for quest services
                if (_currentMenu.HasQuestOptions)
                {
                    services.Add(GossipServiceType.QuestGiver);
                }

                _logger.LogInformation("Discovered {ServiceCount} services: {Services}", services.Count, string.Join(", ", services));
                return services.AsReadOnly();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to discover available services");

                var errorData = new GossipErrorData(ex.Message, _currentNpcGuid, GossipOperationType.NavigateToService);
                _gossipErrors.OnNext(errorData);

                return [];
            }
        }

        #endregion

        #region Quest Integration

        /// <inheritdoc />
        public async Task SelectOptimalQuestRewardAsync(QuestRewardSelectionStrategy strategy, CancellationToken cancellationToken = default)
        {
            if (_disposed) throw new ObjectDisposedException(nameof(GossipNetworkClientComponent));

            // This method would need quest reward data from the server
            // For now, implement a basic strategy of selecting the first reward
            try
            {
                _logger.LogDebug("Selecting optimal quest reward using strategy: {Strategy}", strategy);

                // Basic implementation - always select first reward to prevent deadlocks
                uint selectedRewardIndex = 0;

                switch (strategy)
                {
                    case QuestRewardSelectionStrategy.FirstReward:
                        selectedRewardIndex = 0;
                        break;

                    case QuestRewardSelectionStrategy.HighestValue:
                    case QuestRewardSelectionStrategy.BestForClass:
                    case QuestRewardSelectionStrategy.BestStatUpgrade:
                    case QuestRewardSelectionStrategy.MostNeeded:
                        // TODO: Implement advanced reward selection logic
                        selectedRewardIndex = 0;
                        _logger.LogWarning("Advanced reward selection strategy {Strategy} not fully implemented, selecting first reward", strategy);
                        break;

                    case QuestRewardSelectionStrategy.Custom:
                        selectedRewardIndex = 0;
                        _logger.LogWarning("Custom reward selection strategy not implemented, selecting first reward");
                        break;
                }

                // Select the quest reward
                await SelectGossipOptionAsync(selectedRewardIndex, cancellationToken);

                _logger.LogInformation("Selected quest reward {RewardIndex} using strategy: {Strategy}", selectedRewardIndex, strategy);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to select quest reward using strategy: {Strategy}", strategy);

                var errorData = new GossipErrorData(ex.Message, _currentNpcGuid, GossipOperationType.SelectQuestReward);
                _gossipErrors.OnNext(errorData);

                throw;
            }
        }

        /// <inheritdoc />
        public async Task AcceptAllAvailableQuestsAsync(QuestAcceptanceFilter? filter = null, CancellationToken cancellationToken = default)
        {
            if (_disposed) throw new ObjectDisposedException(nameof(GossipNetworkClientComponent));

            try
            {
                _logger.LogDebug("Accepting all available quests with filter: {HasFilter}", filter != null);

                if (!_isGossipWindowOpen || _currentMenu == null)
                {
                    throw new InvalidOperationException("No gossip menu is currently open");
                }

                var acceptedCount = 0;

                foreach (var questOption in _currentMenu.QuestOptions)
                {
                    // Check if quest is available for acceptance
                    if (questOption.State != QuestGossipState.Available)
                        continue;

                    // Apply filter if provided
                    if (filter != null && !filter.ShouldAcceptQuest(questOption))
                    {
                        _logger.LogDebug("Quest {QuestId} ({QuestTitle}) filtered out", questOption.QuestId, questOption.QuestTitle);
                        continue;
                    }

                    try
                    {
                        await SelectGossipOptionAsync(questOption.Index, cancellationToken);
                        acceptedCount++;
                        _logger.LogDebug("Accepted quest {QuestId} ({QuestTitle})", questOption.QuestId, questOption.QuestTitle);

                        // Small delay to prevent overwhelming the server
                        await Task.Delay(100, cancellationToken);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to accept quest {QuestId} ({QuestTitle})", questOption.QuestId, questOption.QuestTitle);
                    }
                }

                _logger.LogInformation("Accepted {AcceptedCount} out of {TotalCount} available quests", acceptedCount, _currentMenu.QuestOptions.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to accept available quests");

                var errorData = new GossipErrorData(ex.Message, _currentNpcGuid, GossipOperationType.AcceptQuest);
                _gossipErrors.OnNext(errorData);

                throw;
            }
        }

        /// <inheritdoc />
        public async Task<IReadOnlyList<GossipQuestOption>> GetAvailableQuestOptionsAsync()
        {
            if (_disposed) throw new ObjectDisposedException(nameof(GossipNetworkClientComponent));

            try
            {
                if (!_isGossipWindowOpen || _currentMenu == null)
                {
                    return [];
                }

                return _currentMenu.QuestOptions;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get available quest options");
                return [];
            }
        }

        #endregion

        #region Server Response Handling

        /// <inheritdoc />
        public void HandleGossipMenuReceived(GossipMenuData menuData)
        {
            if (_disposed) return;

            try
            {
                _logger.LogInformation("Gossip menu received from NPC {NpcGuid:X}: {OptionCount} options, {QuestCount} quests", 
                    menuData.NpcGuid, menuData.Options.Count, menuData.QuestOptions.Count);

                _currentMenu = menuData;
                _isGossipWindowOpen = true;
                _menuState = GossipMenuState.Open;
                _currentNpcGuid = menuData.NpcGuid;

                _gossipMenus.OnNext(menuData);

                // Auto-discover services
                _ = DiscoverAvailableServicesAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error handling gossip menu received");

                var errorData = new GossipErrorData(ex.Message, menuData.NpcGuid, null);
                _gossipErrors.OnNext(errorData);
            }
        }

        /// <inheritdoc />
        public void HandleGossipOptionResult(GossipOptionResult result)
        {
            if (_disposed) return;

            try
            {
                _logger.LogInformation("Gossip option result: Success={Success}, RequiresRewardChoice={RequiresRewardChoice}", 
                    result.Success, result.RequiresRewardChoice);

                if (!result.Success)
                {
                    var errorData = new GossipErrorData(result.ErrorMessage ?? "Gossip option selection failed", _currentNpcGuid, GossipOperationType.SelectOption);
                    _gossipErrors.OnNext(errorData);
                }

                if (result.RequiresRewardChoice && result.RewardChoices != null)
                {
                    _logger.LogDebug("Quest reward choice required: {RewardCount} options available", result.RewardChoices.Count);
                    // Auto-select first reward to prevent deadlocks
                    _ = SelectOptimalQuestRewardAsync(QuestRewardSelectionStrategy.FirstReward);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error handling gossip option result");

                var errorData = new GossipErrorData(ex.Message, _currentNpcGuid, GossipOperationType.SelectOption);
                _gossipErrors.OnNext(errorData);
            }
        }

        /// <inheritdoc />
        public void HandleNpcTextUpdate(string npcText, uint textId)
        {
            if (_disposed) return;

            try
            {
                _logger.LogInformation("NPC text update received: TextId {TextId}, Length {TextLength}", textId, npcText.Length);

                if (_currentMenu != null)
                {
                    // Update current menu with new text data
                    var updatedMenu = new GossipMenuData(_currentMenu.NpcGuid, _currentMenu.MenuId, textId)
                    {
                        Options = _currentMenu.Options,
                        QuestOptions = _currentMenu.QuestOptions,
                        GossipText = npcText,
                        HasMultiplePages = _currentMenu.HasMultiplePages
                    };

                    _currentMenu = updatedMenu;
                    _gossipMenus.OnNext(updatedMenu);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error handling NPC text update");

                var errorData = new GossipErrorData(ex.Message, _currentNpcGuid, GossipOperationType.QueryText);
                _gossipErrors.OnNext(errorData);
            }
        }

        /// <inheritdoc />
        public void HandleGossipSessionComplete()
        {
            if (_disposed) return;

            try
            {
                _logger.LogInformation("Gossip session completed for NPC: {NpcGuid:X}", _currentNpcGuid ?? 0);

                _isGossipWindowOpen = false;
                _menuState = GossipMenuState.Closed;
                _currentNpcGuid = null;
                _currentMenu = null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error handling gossip session complete");

                var errorData = new GossipErrorData(ex.Message, _currentNpcGuid, GossipOperationType.Close);
                _gossipErrors.OnNext(errorData);
            }
        }

        /// <inheritdoc />
        public void HandleGossipError(string errorMessage, ulong? npcGuid = null)
        {
            if (_disposed) return;

            _logger.LogError("Gossip error: {ErrorMessage} (NPC: {NpcGuid:X})", errorMessage, npcGuid ?? 0);

            _menuState = GossipMenuState.Error;

            var errorData = new GossipErrorData(errorMessage, npcGuid ?? _currentNpcGuid, null);
            _gossipErrors.OnNext(errorData);
        }

        #endregion

        #region Validation and Helper Methods

        /// <inheritdoc />
        public bool CanPerformGossipOperation(GossipOperationType operationType)
        {
            if (_disposed) return false;

            return operationType switch
            {
                GossipOperationType.Greet => !_isGossipWindowOpen,
                GossipOperationType.SelectOption => _isGossipWindowOpen && _menuState == GossipMenuState.Open,
                GossipOperationType.QueryText => _isGossipWindowOpen,
                GossipOperationType.Close => _isGossipWindowOpen,
                GossipOperationType.NavigateToService => _isGossipWindowOpen && _currentMenu != null,
                GossipOperationType.AcceptQuest => _isGossipWindowOpen && _currentMenu?.HasQuestOptions == true,
                GossipOperationType.SelectQuestReward => _isGossipWindowOpen,
                _ => false
            };
        }

        /// <inheritdoc />
        public GossipMenuData? GetCurrentGossipMenu()
        {
            if (_disposed) return null;
            return _currentMenu;
        }

        /// <inheritdoc />
        public bool IsServiceAvailable(GossipServiceType serviceType)
        {
            if (_disposed || !_isGossipWindowOpen || _currentMenu == null) return false;

            return _currentMenu.Options.Any(o => o.ServiceType == serviceType) ||
                   (serviceType == GossipServiceType.QuestGiver && _currentMenu.HasQuestOptions);
        }

        #endregion

        #region Private Helper Methods

        private async Task FindAndNavigateToQuestsAsync(CancellationToken cancellationToken)
        {
            if (_currentMenu?.HasQuestOptions == true)
            {
                // Quests are already available in the current menu
                _logger.LogDebug("Quest options found in current menu");
                return;
            }

            // Look for quest-related gossip options
            var questRelatedOption = _currentMenu?.Options.FirstOrDefault(o => 
                o.Text.ToLower().Contains("quest") || 
                o.ServiceType == GossipServiceType.QuestGiver);

            if (questRelatedOption != null)
            {
                await SelectGossipOptionAsync(questRelatedOption.Index, cancellationToken);
            }
        }

        private async Task FindAndNavigateToAnyServiceAsync(CancellationToken cancellationToken)
        {
            if (_currentMenu == null) return;

            var serviceOption = _currentMenu.Options.FirstOrDefault(o => o.ServiceType != GossipServiceType.Gossip);
            if (serviceOption != null)
            {
                await SelectGossipOptionAsync(serviceOption.Index, cancellationToken);
            }
        }

        private async Task ExploreAllOptionsAsync(CancellationToken cancellationToken)
        {
            if (_currentMenu == null) return;

            _logger.LogDebug("Exploring all available options in gossip menu");

            foreach (var option in _currentMenu.Options.Take(3)) // Limit to first 3 to prevent infinite loops
            {
                try
                {
                    _logger.LogDebug("Exploring option {Index}: {Text}", option.Index, option.Text);
                    await SelectGossipOptionAsync(option.Index, cancellationToken);
                    
                    // Small delay between explorations
                    await Task.Delay(500, cancellationToken);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to explore option {Index}", option.Index);
                }
            }
        }

        #endregion

        #region IDisposable

        /// <summary>
        /// Disposes the gossip network agent and completes all observables.
        /// </summary>
        public void Dispose()
        {
            if (_disposed) return;

            _disposed = true;

            _gossipMenus.OnCompleted();
            _selectedOptions.OnCompleted();
            _gossipErrors.OnCompleted();
            _serviceDiscovered.OnCompleted();

            GC.SuppressFinalize(this);
        }

        #endregion
    }
}