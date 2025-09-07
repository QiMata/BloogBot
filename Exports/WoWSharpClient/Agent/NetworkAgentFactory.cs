using Microsoft.Extensions.Logging;
using WoWSharpClient.Client;

namespace WoWSharpClient.Agent
{
    /// <summary>
    /// Network Agent Factory that provides coordinated access to all network agents.
    /// Uses a lazy builder pattern where agents are created only when first accessed.
    /// </summary>
    public class NetworkAgentFactory : IAgentFactory
    {
        private readonly IWorldClient _worldClient;
        private readonly ILoggerFactory _loggerFactory;
        private readonly ILogger<NetworkAgentFactory> _logger;

        // Lazy-initialized agents
        private ITargetingNetworkAgent? _targetingAgent;
        private IAttackNetworkAgent? _attackAgent;
        private IQuestNetworkAgent? _questAgent;
        private ILootingNetworkAgent? _lootingAgent;
        private IGameObjectNetworkAgent? _gameObjectAgent;

        // Thread safety locks
        private readonly object _targetingLock = new object();
        private readonly object _attackLock = new object();
        private readonly object _questLock = new object();
        private readonly object _lootingLock = new object();
        private readonly object _gameObjectLock = new object();

        // Event handler setup tracking
        private bool _eventsSetup = false;
        private readonly object _eventsLock = new object();

        #region Agent Access Properties

        /// <inheritdoc />
        public ITargetingNetworkAgent TargetingAgent
        {
            get
            {
                if (_targetingAgent == null)
                {
                    lock (_targetingLock)
                    {
                        if (_targetingAgent == null)
                        {
                            _logger.LogDebug("Creating TargetingNetworkAgent lazily");
                            _targetingAgent = AgentFactory.CreateTargetingNetworkAgent(_worldClient, _loggerFactory);
                            SetupEventHandlersIfNeeded();
                            _logger.LogDebug("TargetingNetworkAgent created successfully");
                        }
                    }
                }
                return _targetingAgent;
            }
        }

        /// <inheritdoc />
        public IAttackNetworkAgent AttackAgent
        {
            get
            {
                if (_attackAgent == null)
                {
                    lock (_attackLock)
                    {
                        if (_attackAgent == null)
                        {
                            _logger.LogDebug("Creating AttackNetworkAgent lazily");
                            _attackAgent = AgentFactory.CreateAttackNetworkAgent(_worldClient, _loggerFactory);
                            SetupEventHandlersIfNeeded();
                            _logger.LogDebug("AttackNetworkAgent created successfully");
                        }
                    }
                }
                return _attackAgent;
            }
        }

        /// <inheritdoc />
        public IQuestNetworkAgent QuestAgent
        {
            get
            {
                if (_questAgent == null)
                {
                    lock (_questLock)
                    {
                        if (_questAgent == null)
                        {
                            _logger.LogDebug("Creating QuestNetworkAgent lazily");
                            _questAgent = AgentFactory.CreateQuestNetworkAgent(_worldClient, _loggerFactory);
                            SetupEventHandlersIfNeeded();
                            _logger.LogDebug("QuestNetworkAgent created successfully");
                        }
                    }
                }
                return _questAgent;
            }
        }

        /// <inheritdoc />
        public ILootingNetworkAgent LootingAgent
        {
            get
            {
                if (_lootingAgent == null)
                {
                    lock (_lootingLock)
                    {
                        if (_lootingAgent == null)
                        {
                            _logger.LogDebug("Creating LootingNetworkAgent lazily");
                            _lootingAgent = AgentFactory.CreateLootingNetworkAgent(_worldClient, _loggerFactory);
                            SetupEventHandlersIfNeeded();
                            _logger.LogDebug("LootingNetworkAgent created successfully");
                        }
                    }
                }
                return _lootingAgent;
            }
        }

        /// <inheritdoc />
        public IGameObjectNetworkAgent GameObjectAgent
        {
            get
            {
                if (_gameObjectAgent == null)
                {
                    lock (_gameObjectLock)
                    {
                        if (_gameObjectAgent == null)
                        {
                            _logger.LogDebug("Creating GameObjectNetworkAgent lazily");
                            _gameObjectAgent = AgentFactory.CreateGameObjectNetworkAgent(_worldClient, _loggerFactory);
                            SetupEventHandlersIfNeeded();
                            _logger.LogDebug("GameObjectNetworkAgent created successfully");
                        }
                    }
                }
                return _gameObjectAgent;
            }
        }

        #endregion

        #region Constructors

        /// <summary>
        /// Initializes a new instance of the NetworkAgentFactory class.
        /// Agents are created lazily when their properties are first accessed.
        /// </summary>
        /// <param name="worldClient">The world client for network communication.</param>
        /// <param name="loggerFactory">Logger factory for creating loggers.</param>
        public NetworkAgentFactory(IWorldClient worldClient, ILoggerFactory loggerFactory)
        {
            ArgumentNullException.ThrowIfNull(worldClient);
            ArgumentNullException.ThrowIfNull(loggerFactory);

            _worldClient = worldClient;
            _loggerFactory = loggerFactory;
            _logger = loggerFactory.CreateLogger<NetworkAgentFactory>();

            _logger.LogInformation("Network Agent Factory initialized with lazy builder pattern");
        }

        /// <summary>
        /// Initializes a new instance of the NetworkAgentFactory class with individual agents.
        /// This constructor is maintained for compatibility but doesn't use the lazy pattern.
        /// </summary>
        /// <param name="targetingAgent">The targeting network agent.</param>
        /// <param name="attackAgent">The attack network agent.</param>
        /// <param name="questAgent">The quest network agent.</param>
        /// <param name="lootingAgent">The looting network agent.</param>
        /// <param name="gameObjectAgent">The game object network agent.</param>
        /// <param name="logger">Logger instance.</param>
        public NetworkAgentFactory(
            ITargetingNetworkAgent targetingAgent,
            IAttackNetworkAgent attackAgent,
            IQuestNetworkAgent questAgent,
            ILootingNetworkAgent lootingAgent,
            IGameObjectNetworkAgent gameObjectAgent,
            ILogger<NetworkAgentFactory> logger)
        {
            _targetingAgent = targetingAgent ?? throw new ArgumentNullException(nameof(targetingAgent));
            _attackAgent = attackAgent ?? throw new ArgumentNullException(nameof(attackAgent));
            _questAgent = questAgent ?? throw new ArgumentNullException(nameof(questAgent));
            _lootingAgent = lootingAgent ?? throw new ArgumentNullException(nameof(lootingAgent));
            _gameObjectAgent = gameObjectAgent ?? throw new ArgumentNullException(nameof(gameObjectAgent));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            // These fields won't be used in this constructor path, but we need to initialize them
            _worldClient = null!;
            _loggerFactory = null!;

            SetupEventHandlers();
            _logger.LogInformation("Network Agent Factory initialized with provided agents");
        }

        #endregion

        #region Event Setup

        /// <summary>
        /// Sets up event handlers for coordinating between agents.
        /// Thread-safe and only sets up events once.
        /// </summary>
        private void SetupEventHandlersIfNeeded()
        {
            if (!_eventsSetup)
            {
                lock (_eventsLock)
                {
                    if (!_eventsSetup)
                    {
                        SetupEventHandlers();
                        _eventsSetup = true;
                    }
                }
            }
        }

        /// <summary>
        /// Sets up event handlers for coordinating between agents.
        /// </summary>
        private void SetupEventHandlers()
        {
            // Only setup events for agents that are already created
            if (_targetingAgent != null)
            {
                _targetingAgent.TargetChanged += OnTargetChanged;
            }

            if (_attackAgent != null)
            {
                _attackAgent.AttackStarted += OnAttackStarted;
                _attackAgent.AttackStopped += OnAttackStopped;
                _attackAgent.AttackError += OnAttackError;
            }

            if (_questAgent != null)
            {
                _questAgent.QuestOffered += OnQuestOffered;
                _questAgent.QuestAccepted += OnQuestAccepted;
                _questAgent.QuestCompleted += OnQuestCompleted;
                _questAgent.QuestError += OnQuestError;
            }

            if (_lootingAgent != null)
            {
                _lootingAgent.LootWindowOpened += OnLootWindowOpened;
                _lootingAgent.LootWindowClosed += OnLootWindowClosed;
                _lootingAgent.ItemLooted += OnItemLooted;
                _lootingAgent.MoneyLooted += OnMoneyLooted;
                _lootingAgent.LootError += OnLootError;
            }

            if (_gameObjectAgent != null)
            {
                _gameObjectAgent.GameObjectInteracted += OnGameObjectInteracted;
                _gameObjectAgent.ChestOpened += OnChestOpened;
                _gameObjectAgent.NodeHarvested += OnNodeHarvested;
                _gameObjectAgent.GatheringFailed += OnGatheringFailed;
            }
        }

        #endregion

        #region Event Handlers

        private void OnTargetChanged(ulong? newTarget)
        {
            if (newTarget.HasValue)
            {
                _logger.LogDebug("Target changed to: {NewTarget:X}", newTarget.Value);
            }
            else
            {
                _logger.LogDebug("Target cleared");
            }
        }

        private void OnAttackStarted(ulong victimGuid)
        {
            _logger.LogDebug("Attack started on: {VictimGuid:X}", victimGuid);
        }

        private void OnAttackStopped()
        {
            _logger.LogDebug("Attack stopped");
        }

        private void OnAttackError(string error)
        {
            _logger.LogWarning("Attack error: {Error}", error);
        }

        private void OnQuestOffered(uint questId)
        {
            _logger.LogDebug("Quest offered: {QuestId}", questId);
        }

        private void OnQuestAccepted(uint questId)
        {
            _logger.LogDebug("Quest accepted: {QuestId}", questId);
        }

        private void OnQuestCompleted(uint questId)
        {
            _logger.LogDebug("Quest completed: {QuestId}", questId);
        }

        private void OnQuestError(string error)
        {
            _logger.LogWarning("Quest error: {Error}", error);
        }

        private void OnLootWindowOpened(ulong lootTargetGuid)
        {
            _logger.LogDebug("Loot window opened for: {LootTargetGuid:X}", lootTargetGuid);
        }

        private void OnLootWindowClosed()
        {
            _logger.LogDebug("Loot window closed");
        }

        private void OnItemLooted(uint itemId, uint quantity)
        {
            _logger.LogDebug("Item looted: {Quantity}x {ItemId}", quantity, itemId);
        }

        private void OnMoneyLooted(uint amount)
        {
            _logger.LogDebug("Money looted: {Amount} copper", amount);
        }

        private void OnLootError(string error)
        {
            _logger.LogWarning("Loot error: {Error}", error);
        }

        private void OnGameObjectInteracted(ulong gameObjectGuid)
        {
            _logger.LogDebug("Game object interacted: {GameObjectGuid:X}", gameObjectGuid);
        }

        private void OnChestOpened(ulong chestGuid)
        {
            _logger.LogDebug("Chest opened: {ChestGuid:X}", chestGuid);
        }

        private void OnNodeHarvested(ulong nodeGuid, uint itemId)
        {
            _logger.LogDebug("Node harvested: {NodeGuid:X}, Item: {ItemId}", nodeGuid, itemId);
        }

        private void OnGatheringFailed(ulong nodeGuid, string error)
        {
            _logger.LogWarning("Gathering failed for node {NodeGuid:X}: {Error}", nodeGuid, error);
        }

        #endregion
    }
}