using GameData.Core.Enums;
using Microsoft.Extensions.Logging;
using WoWSharpClient.Client;
using WoWSharpClient.Networking.ClientComponents.I;

namespace WoWSharpClient.Networking.ClientComponents
{
    /// <summary>
    /// Implementation of trainer network agent that handles trainer operations in World of Warcraft.
    /// Manages learning spells, abilities, and skills from NPC trainers using the Mangos protocol.
    /// </summary>
    public class TrainerNetworkClientComponent : ITrainerNetworkClientComponent
    {
        private readonly IWorldClient _worldClient;
        private readonly ILogger<TrainerNetworkClientComponent> _logger;

        private bool _isTrainerWindowOpen;
        private ulong? _currentTrainerGuid;
        private readonly List<TrainerService> _availableServices;

        /// <summary>
        /// Initializes a new instance of the TrainerNetworkClientComponent class.
        /// </summary>
        /// <param name="worldClient">The world client for sending packets.</param>
        /// <param name="logger">Logger instance.</param>
        public TrainerNetworkClientComponent(IWorldClient worldClient, ILogger<TrainerNetworkClientComponent> logger)
        {
            _worldClient = worldClient ?? throw new ArgumentNullException(nameof(worldClient));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _availableServices = [];
        }

        /// <inheritdoc />
        public bool IsTrainerWindowOpen => _isTrainerWindowOpen;

        /// <inheritdoc />
        public ulong? CurrentTrainerGuid => _currentTrainerGuid;

        /// <inheritdoc />
        public event Action<ulong>? TrainerWindowOpened;

        /// <inheritdoc />
        public event Action? TrainerWindowClosed;

        /// <inheritdoc />
        public event Action<uint, uint>? SpellLearned;

        /// <inheritdoc />
        public event Action<TrainerService[]>? TrainerServicesReceived;

        /// <inheritdoc />
        public event Action<string>? TrainerError;

        /// <inheritdoc />
        public async Task OpenTrainerAsync(ulong trainerGuid, CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogDebug("Opening trainer interaction with: {TrainerGuid:X}", trainerGuid);

                var payload = new byte[8];
                BitConverter.GetBytes(trainerGuid).CopyTo(payload, 0);

                await _worldClient.SendOpcodeAsync(Opcode.CMSG_GOSSIP_HELLO, payload, cancellationToken);

                _logger.LogInformation("Trainer interaction initiated with: {TrainerGuid:X}", trainerGuid);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to open trainer interaction with: {TrainerGuid:X}", trainerGuid);
                TrainerError?.Invoke($"Failed to open trainer: {ex.Message}");
                throw;
            }
        }

        /// <inheritdoc />
        public async Task RequestTrainerServicesAsync(ulong trainerGuid, CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogDebug("Requesting trainer services from: {TrainerGuid:X}", trainerGuid);

                var payload = new byte[8];
                BitConverter.GetBytes(trainerGuid).CopyTo(payload, 0);

                await _worldClient.SendOpcodeAsync(Opcode.CMSG_TRAINER_LIST, payload, cancellationToken);

                _logger.LogInformation("Trainer services request sent to: {TrainerGuid:X}", trainerGuid);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to request trainer services from: {TrainerGuid:X}", trainerGuid);
                TrainerError?.Invoke($"Failed to request trainer services: {ex.Message}");
                throw;
            }
        }

        /// <inheritdoc />
        public async Task LearnSpellAsync(ulong trainerGuid, uint spellId, CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogDebug("Learning spell {SpellId} from trainer: {TrainerGuid:X}", spellId, trainerGuid);

                var payload = new byte[12];
                BitConverter.GetBytes(trainerGuid).CopyTo(payload, 0);
                BitConverter.GetBytes(spellId).CopyTo(payload, 8);

                await _worldClient.SendOpcodeAsync(Opcode.CMSG_TRAINER_BUY_SPELL, payload, cancellationToken);

                _logger.LogInformation("Learn spell request sent for spell {SpellId} from trainer: {TrainerGuid:X}", spellId, trainerGuid);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to learn spell {SpellId} from trainer: {TrainerGuid:X}", spellId, trainerGuid);
                TrainerError?.Invoke($"Failed to learn spell: {ex.Message}");
                throw;
            }
        }

        /// <inheritdoc />
        public async Task LearnSpellByIndexAsync(ulong trainerGuid, uint serviceIndex, CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogDebug("Learning spell by index {ServiceIndex} from trainer: {TrainerGuid:X}", serviceIndex, trainerGuid);

                // Find the spell ID for this index
                var service = _availableServices.FirstOrDefault(s => s.ServiceIndex == serviceIndex);
                if (service == null)
                {
                    var errorMsg = $"Service index {serviceIndex} not found in available services";
                    _logger.LogWarning(errorMsg);
                    TrainerError?.Invoke(errorMsg);
                    return;
                }

                await LearnSpellAsync(trainerGuid, service.SpellId, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to learn spell by index {ServiceIndex} from trainer: {TrainerGuid:X}", serviceIndex, trainerGuid);
                TrainerError?.Invoke($"Failed to learn spell by index: {ex.Message}");
                throw;
            }
        }

        /// <inheritdoc />
        public async Task CloseTrainerAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogDebug("Closing trainer window");

                // Trainer windows typically close automatically when moving away
                // But we can update our internal state
                _isTrainerWindowOpen = false;
                _currentTrainerGuid = null;
                _availableServices.Clear();
                TrainerWindowClosed?.Invoke();

                _logger.LogInformation("Trainer window closed");
                await Task.CompletedTask;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to close trainer window");
                TrainerError?.Invoke($"Failed to close trainer: {ex.Message}");
                throw;
            }
        }

        /// <inheritdoc />
        public bool IsTrainerOpen(ulong trainerGuid)
        {
            return _isTrainerWindowOpen && _currentTrainerGuid == trainerGuid;
        }

        /// <inheritdoc />
        public async Task QuickLearnSpellAsync(ulong trainerGuid, uint spellId, CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogDebug("Performing quick learn of spell {SpellId} from trainer: {TrainerGuid:X}", spellId, trainerGuid);

                await OpenTrainerAsync(trainerGuid, cancellationToken);
                await RequestTrainerServicesAsync(trainerGuid, cancellationToken);
                
                // Small delay to allow trainer window to open and services to load
                await Task.Delay(200, cancellationToken);
                
                await LearnSpellAsync(trainerGuid, spellId, cancellationToken);
                
                // Small delay to allow learning to complete
                await Task.Delay(100, cancellationToken);
                
                await CloseTrainerAsync(cancellationToken);

                _logger.LogInformation("Quick learn completed for spell {SpellId} from trainer: {TrainerGuid:X}", spellId, trainerGuid);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Quick learn failed for spell {SpellId} from trainer: {TrainerGuid:X}", spellId, trainerGuid);
                TrainerError?.Invoke($"Quick learn failed: {ex.Message}");
                throw;
            }
        }

        /// <inheritdoc />
        public async Task LearnMultipleSpellsAsync(ulong trainerGuid, uint[] spellIds, CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogDebug("Learning multiple spells from trainer: {TrainerGuid:X}. Spells: [{SpellIds}]", 
                    trainerGuid, string.Join(", ", spellIds));

                await OpenTrainerAsync(trainerGuid, cancellationToken);
                await RequestTrainerServicesAsync(trainerGuid, cancellationToken);
                
                // Small delay to allow trainer window to open and services to load
                await Task.Delay(200, cancellationToken);

                foreach (var spellId in spellIds)
                {
                    try
                    {
                        await LearnSpellAsync(trainerGuid, spellId, cancellationToken);
                        
                        // Small delay between spell learning attempts
                        await Task.Delay(100, cancellationToken);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to learn spell {SpellId}, continuing with next spell", spellId);
                        // Continue with other spells even if one fails
                    }
                }
                
                await CloseTrainerAsync(cancellationToken);

                _logger.LogInformation("Multiple spell learning completed from trainer: {TrainerGuid:X}", trainerGuid);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Multiple spell learning failed from trainer: {TrainerGuid:X}", trainerGuid);
                TrainerError?.Invoke($"Multiple spell learning failed: {ex.Message}");
                throw;
            }
        }

        /// <inheritdoc />
        public bool IsSpellAvailable(uint spellId)
        {
            return _availableServices.Any(s => s.SpellId == spellId && s.CanLearn);
        }

        /// <inheritdoc />
        public uint? GetSpellCost(uint spellId)
        {
            var service = _availableServices.FirstOrDefault(s => s.SpellId == spellId);
            return service?.Cost;
        }

        /// <inheritdoc />
        public TrainerService[] GetAvailableServices()
        {
            return _availableServices.Where(s => s.CanLearn).ToArray();
        }

        /// <inheritdoc />
        public TrainerService[] GetAffordableServices(uint currentMoney)
        {
            return _availableServices.Where(s => s.CanLearn && s.Cost <= currentMoney).ToArray();
        }

        /// <summary>
        /// Handles server responses for trainer window opening.
        /// This method should be called when SMSG_TRAINER_LIST is received.
        /// </summary>
        /// <param name="trainerGuid">The GUID of the trainer.</param>
        /// <param name="services">The available trainer services.</param>
        public void HandleTrainerWindowOpened(ulong trainerGuid, TrainerService[] services)
        {
            _isTrainerWindowOpen = true;
            _currentTrainerGuid = trainerGuid;
            _availableServices.Clear();
            _availableServices.AddRange(services);
            
            TrainerWindowOpened?.Invoke(trainerGuid);
            TrainerServicesReceived?.Invoke(services);
            
            _logger.LogDebug("Trainer window opened for: {TrainerGuid:X} with {ServiceCount} services", 
                trainerGuid, services.Length);
        }

        /// <summary>
        /// Handles server responses for successful spell learning.
        /// This method should be called when SMSG_TRAINER_BUY_SUCCEEDED is received.
        /// </summary>
        /// <param name="spellId">The ID of the learned spell.</param>
        /// <param name="cost">The cost in copper.</param>
        public void HandleSpellLearned(uint spellId, uint cost)
        {
            SpellLearned?.Invoke(spellId, cost);
            _logger.LogDebug("Spell learned: {SpellId} (cost: {Cost})", spellId, cost);
        }

        /// <summary>
        /// Handles server responses for trainer operation failures.
        /// This method should be called when SMSG_TRAINER_BUY_FAILED or similar error responses are received.
        /// </summary>
        /// <param name="errorMessage">The error message.</param>
        public void HandleTrainerError(string errorMessage)
        {
            TrainerError?.Invoke(errorMessage);
            _logger.LogWarning("Trainer operation failed: {Error}", errorMessage);
        }

        /// <summary>
        /// Updates the trainer services list with new information.
        /// This can be called when the server sends updated trainer information.
        /// </summary>
        /// <param name="services">The updated trainer services.</param>
        public void UpdateTrainerServices(TrainerService[] services)
        {
            _availableServices.Clear();
            _availableServices.AddRange(services);
            TrainerServicesReceived?.Invoke(services);
            
            _logger.LogDebug("Trainer services updated: {ServiceCount} services available", services.Length);
        }
    }
}